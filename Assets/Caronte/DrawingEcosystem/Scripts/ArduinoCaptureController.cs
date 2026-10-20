using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class ArduinoCaptureController : MonoBehaviour
    {
        private const string ButtonPressedSignal = "BUTTON_PRESSED";
        private const string StartLightsSignal = "START_LIGHTS\n";

        [SerializeField] private DrawingGameController gameController;
        [SerializeField] private string deviceNameContains = "Arduino";
        [SerializeField] private int baudRate = 9600;
        [SerializeField] private float captureDelaySeconds = 2f;
        [SerializeField, Min(0f)] private float captureReadinessTimeoutSeconds = 5f;
        [SerializeField, Min(0.02f)] private float captureRetryIntervalSeconds = 0.1f;
        [SerializeField] private float postCaptureLockSeconds = 4f;

        private readonly ConcurrentQueue<string> receivedSignals = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> serialErrors = new ConcurrentQueue<string>();
        private readonly object portLock = new object();
        private Thread readThread;
        private IntPtr portHandle = NativeMethods.InvalidHandleValue;
        private volatile bool listening;
        private bool inputLocked;
        private string connectedPortName;

        public bool IsConnected => portHandle != NativeMethods.InvalidHandleValue;
        public bool IsInputLocked => inputLocked;
        public string ConnectedPortName => connectedPortName;

        private void Awake()
        {
            if (gameController == null)
            {
                gameController = FindFirstObjectByType<DrawingGameController>();
            }
        }

        private void OnEnable()
        {
            OpenPort();
        }

        private void Update()
        {
            while (serialErrors.TryDequeue(out string error))
            {
                Debug.LogError(error, this);
            }

            while (receivedSignals.TryDequeue(out string signal))
            {
                if (signal == ButtonPressedSignal && !inputLocked)
                {
                    StartCoroutine(RunCaptureSequence());
                }
            }
        }

        private void OnDisable()
        {
            ClosePort();
            inputLocked = false;
        }

        private void OnApplicationQuit()
        {
            ClosePort();
        }

        private IEnumerator RunCaptureSequence()
        {
            inputLocked = true;
            if (gameController != null)
            {
                gameController.PlayCaptureButtonSounds();
            }

            SendSignal(StartLightsSignal);

            yield return new WaitForSecondsRealtime(captureDelaySeconds);
            if (gameController != null)
            {
                bool captureCompleted = false;
                float deadline = Time.realtimeSinceStartup + captureReadinessTimeoutSeconds;
                do
                {
                    if (gameController.TryCaptureSelectedTemplate())
                    {
                        captureCompleted = true;
                        break;
                    }

                    yield return new WaitForSecondsRealtime(captureRetryIntervalSeconds);
                }
                while (Time.realtimeSinceStartup < deadline);

                if (!captureCompleted)
                {
                    Debug.LogWarning($"Captura cancelada tras esperar a webcam/tracking: {gameController.LastStatus}", this);
                }
            }
            else
            {
                Debug.LogError("ArduinoCaptureController no tiene un DrawingGameController asignado.", this);
            }

            yield return new WaitForSecondsRealtime(postCaptureLockSeconds);
            inputLocked = false;
        }

        private void OpenPort()
        {
            if (IsConnected)
            {
                return;
            }

            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
            {
                Debug.LogError("ArduinoCaptureController solo admite puertos serie en Windows.", this);
                return;
            }

            connectedPortName = FindPortByDeviceName(deviceNameContains);
            if (string.IsNullOrEmpty(connectedPortName))
            {
                Debug.LogError($"No se encontró ningún puerto serie cuyo nombre contenga '{deviceNameContains}'.", this);
                return;
            }

            string devicePath = $"\\\\.\\{connectedPortName}";
            IntPtr handle = NativeMethods.CreateFile(
                devicePath,
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                0,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            if (handle == NativeMethods.InvalidHandleValue)
            {
                Debug.LogError($"No se pudo abrir el Arduino en {connectedPortName}. Error de Windows: {Marshal.GetLastWin32Error()}.", this);
                return;
            }

            NativeMethods.Dcb configuration = new NativeMethods.Dcb
            {
                DcbLength = (uint)Marshal.SizeOf<NativeMethods.Dcb>()
            };

            if (!NativeMethods.GetCommState(handle, ref configuration))
            {
                CloseFailedHandle(handle, "leer la configuración");
                return;
            }

            configuration.BaudRate = (uint)baudRate;
            configuration.Flags = NativeMethods.BinaryMode | NativeMethods.DtrEnabled | NativeMethods.RtsEnabled;
            configuration.ByteSize = 8;
            configuration.Parity = 0;
            configuration.StopBits = 0;

            if (!NativeMethods.SetCommState(handle, ref configuration))
            {
                CloseFailedHandle(handle, "configurar el puerto");
                return;
            }

            NativeMethods.CommTimeouts timeouts = new NativeMethods.CommTimeouts
            {
                ReadIntervalTimeout = 50,
                ReadTotalTimeoutMultiplier = 0,
                ReadTotalTimeoutConstant = 50,
                WriteTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 250
            };

            if (!NativeMethods.SetCommTimeouts(handle, ref timeouts))
            {
                CloseFailedHandle(handle, "configurar los tiempos de espera");
                return;
            }

            NativeMethods.PurgeComm(handle, NativeMethods.PurgeRxClear | NativeMethods.PurgeTxClear);
            portHandle = handle;
            listening = true;
            readThread = new Thread(ReadSerialLoop)
            {
                IsBackground = true,
                Name = "Arduino capture listener"
            };
            readThread.Start();
            Debug.Log($"Arduino detectado automáticamente y conectado en {connectedPortName} a {baudRate} baudios.", this);
        }

        private void ReadSerialLoop()
        {
            byte[] buffer = new byte[1];
            StringBuilder line = new StringBuilder();

            while (listening)
            {
                IntPtr handle = portHandle;
                if (handle == NativeMethods.InvalidHandleValue)
                {
                    break;
                }

                bool success = NativeMethods.ReadFile(handle, buffer, 1, out uint bytesRead, IntPtr.Zero);
                if (!success)
                {
                    if (listening)
                    {
                        serialErrors.Enqueue($"Error leyendo el Arduino. Error de Windows: {Marshal.GetLastWin32Error()}.");
                    }

                    break;
                }

                if (bytesRead == 0)
                {
                    continue;
                }

                char character = (char)buffer[0];
                if (character == '\n')
                {
                    string signal = line.ToString().Trim();
                    line.Clear();
                    if (!string.IsNullOrEmpty(signal))
                    {
                        receivedSignals.Enqueue(signal);
                    }
                }
                else if (character != '\r')
                {
                    line.Append(character);
                }
            }
        }

        private void SendSignal(string signal)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(signal);
            lock (portLock)
            {
                if (!IsConnected)
                {
                    Debug.LogError("No se puede enviar START_LIGHTS porque el Arduino no está conectado.", this);
                    return;
                }

                if (!NativeMethods.WriteFile(portHandle, bytes, (uint)bytes.Length, out uint bytesWritten, IntPtr.Zero) ||
                    bytesWritten != bytes.Length)
                {
                    Debug.LogError($"Error enviando START_LIGHTS al Arduino. Error de Windows: {Marshal.GetLastWin32Error()}.", this);
                }
            }
        }

        private void ClosePort()
        {
            listening = false;
            lock (portLock)
            {
                if (IsConnected)
                {
                    NativeMethods.CloseHandle(portHandle);
                    portHandle = NativeMethods.InvalidHandleValue;
                }
            }

            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join(250);
            }

            readThread = null;
            connectedPortName = null;
        }

        private void CloseFailedHandle(IntPtr handle, string operation)
        {
            int error = Marshal.GetLastWin32Error();
            NativeMethods.CloseHandle(handle);
            Debug.LogError($"No se pudo {operation} de {connectedPortName}. Error de Windows: {error}.", this);
            connectedPortName = null;
        }

        private static string FindPortByDeviceName(string deviceName)
        {
            Guid portsClassGuid = NativeMethods.PortsClassGuid;
            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref portsClassGuid,
                null,
                IntPtr.Zero,
                NativeMethods.PresentDevices);

            if (deviceInfoSet == NativeMethods.InvalidHandleValue)
            {
                return null;
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    NativeMethods.SpDeviceInfoData deviceInfo = new NativeMethods.SpDeviceInfoData
                    {
                        Size = (uint)Marshal.SizeOf<NativeMethods.SpDeviceInfoData>()
                    };

                    if (!NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
                    {
                        break;
                    }

                    StringBuilder friendlyName = new StringBuilder(256);
                    if (!NativeMethods.SetupDiGetDeviceRegistryProperty(
                            deviceInfoSet,
                            ref deviceInfo,
                            NativeMethods.FriendlyNameProperty,
                            out _,
                            friendlyName,
                            friendlyName.Capacity,
                            out _))
                    {
                        continue;
                    }

                    string name = friendlyName.ToString();
                    if (name.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    int portStart = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
                    int portEnd = name.IndexOf(')', portStart >= 0 ? portStart : 0);
                    if (portStart >= 0 && portEnd > portStart)
                    {
                        return name.Substring(portStart + 1, portEnd - portStart - 1);
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return null;
        }

        private static class NativeMethods
        {
            internal static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

            internal const uint GenericRead = 0x80000000;
            internal const uint GenericWrite = 0x40000000;
            internal const uint OpenExisting = 3;
            internal const uint BinaryMode = 0x00000001;
            internal const uint DtrEnabled = 0x00000010;
            internal const uint RtsEnabled = 0x00001000;
            internal const uint PurgeTxClear = 0x0004;
            internal const uint PurgeRxClear = 0x0008;
            internal const uint PresentDevices = 0x00000002;
            internal const uint FriendlyNameProperty = 0x0000000C;

            internal static readonly Guid PortsClassGuid = new Guid("4d36e978-e325-11ce-bfc1-08002be10318");

            [StructLayout(LayoutKind.Sequential)]
            internal struct Dcb
            {
                internal uint DcbLength;
                internal uint BaudRate;
                internal uint Flags;
                internal ushort Reserved;
                internal ushort XonLimit;
                internal ushort XoffLimit;
                internal byte ByteSize;
                internal byte Parity;
                internal byte StopBits;
                internal sbyte XonChar;
                internal sbyte XoffChar;
                internal sbyte ErrorChar;
                internal sbyte EofChar;
                internal sbyte EventChar;
                internal ushort Reserved1;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct CommTimeouts
            {
                internal uint ReadIntervalTimeout;
                internal uint ReadTotalTimeoutMultiplier;
                internal uint ReadTotalTimeoutConstant;
                internal uint WriteTotalTimeoutMultiplier;
                internal uint WriteTotalTimeoutConstant;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct SpDeviceInfoData
            {
                internal uint Size;
                internal Guid ClassGuid;
                internal uint DeviceInstance;
                internal IntPtr Reserved;
            }

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr SetupDiGetClassDevs(
                ref Guid classGuid,
                string enumerator,
                IntPtr parentWindow,
                uint flags);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetupDiEnumDeviceInfo(
                IntPtr deviceInfoSet,
                uint memberIndex,
                ref SpDeviceInfoData deviceInfoData);

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetupDiGetDeviceRegistryProperty(
                IntPtr deviceInfoSet,
                ref SpDeviceInfoData deviceInfoData,
                uint property,
                out uint propertyRegistryDataType,
                StringBuilder propertyBuffer,
                int propertyBufferSize,
                out uint requiredSize);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr CreateFile(
                string fileName,
                uint desiredAccess,
                uint shareMode,
                IntPtr securityAttributes,
                uint creationDisposition,
                uint flagsAndAttributes,
                IntPtr templateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetCommState(IntPtr file, ref Dcb configuration);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetCommState(IntPtr file, ref Dcb configuration);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetCommTimeouts(IntPtr file, ref CommTimeouts timeouts);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool PurgeComm(IntPtr file, uint flags);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ReadFile(
                IntPtr file,
                byte[] buffer,
                uint bytesToRead,
                out uint bytesRead,
                IntPtr overlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool WriteFile(
                IntPtr file,
                byte[] buffer,
                uint bytesToWrite,
                out uint bytesWritten,
                IntPtr overlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);
        }
    }
}
