using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Caronte.DrawingEcosystem
{
    public sealed class DrawingCaptureService : MonoBehaviour
    {
        public enum ColorTemperatureMode
        {
            Disabled,
            Manual,
            Automatic
        }

        [SerializeField] private string preferredDeviceNameContains = "IPEVO";
        [SerializeField] private int requestedWidth = 1920;
        [SerializeField] private int requestedHeight = 1080;
        [SerializeField] private int requestedFps = 30;
        [SerializeField] private bool startAutomatically = true;
        [SerializeField] private Renderer previewRenderer;
        [SerializeField] private RawImage previewRawImage;
        [SerializeField] private string previewTextureProperty = "_BaseMap";
        [Header("Temperatura de color")]
        [SerializeField] private ColorTemperatureMode colorTemperatureMode = ColorTemperatureMode.Manual;
        [SerializeField, Range(-1f, 1f)] private float manualColorTemperature;
        [SerializeField, Range(-1f, 1f)] private float automaticTemperatureBias;
        [SerializeField, Range(0f, 1f)] private float automaticCorrectionStrength = 0.8f;
        [SerializeField, Range(0.05f, 0.8f)] private float automaticNeutralSaturation = 0.3f;
        [SerializeField, Range(0.1f, 0.95f)] private float automaticMinimumBrightness = 0.45f;

        private readonly List<string> deviceNames = new List<string>();
        private WebCamTexture activeWebcam;
        private string activeDeviceName;
        private float lastAutomaticTemperature;

        public event Action<IReadOnlyList<string>> DevicesChanged;
        public event Action<string> DeviceStarted;
        public event Action<string> CaptureFailed;

        public WebCamTexture ActiveWebcam => activeWebcam;
        public string ActiveDeviceName => activeDeviceName;
        public IReadOnlyList<string> DeviceNames => deviceNames;
        public bool IsRunning => activeWebcam != null && activeWebcam.isPlaying;
        public bool IsFrameReady => activeWebcam != null && activeWebcam.isPlaying && activeWebcam.width > 16 && activeWebcam.height > 16 && activeWebcam.didUpdateThisFrame;
        public ColorTemperatureMode TemperatureMode => colorTemperatureMode;
        public float ManualColorTemperature => manualColorTemperature;
        public float LastAutomaticTemperature => lastAutomaticTemperature;

        private void Awake()
        {
            SetPreviewVisible(false);
            RefreshDevices();
        }

        private void Start()
        {
            if (startAutomatically)
            {
                StartPreferredOrFirstDevice();
            }
        }

        private void OnDisable()
        {
            StopCamera();
        }

        private void OnDestroy()
        {
            StopCamera();
        }

        public void RefreshDevices()
        {
            deviceNames.Clear();
            WebCamDevice[] devices = WebCamTexture.devices;
            for (int i = 0; i < devices.Length; i++)
            {
                deviceNames.Add(devices[i].name);
            }

            DevicesChanged?.Invoke(deviceNames);
        }

        public void StartPreferredOrFirstDevice()
        {
            RefreshDevices();
            if (deviceNames.Count == 0)
            {
                CaptureFailed?.Invoke("No se ha encontrado ninguna webcam.");
                return;
            }

            int selectedIndex = 0;
            for (int i = 0; i < deviceNames.Count; i++)
            {
                if (deviceNames[i].IndexOf(preferredDeviceNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selectedIndex = i;
                    break;
                }
            }

            StartDevice(selectedIndex);
        }

        public void StartDevice(int deviceIndex)
        {
            RefreshDevices();
            if (deviceIndex < 0 || deviceIndex >= deviceNames.Count)
            {
                CaptureFailed?.Invoke("Índice de webcam inválido.");
                return;
            }

            StartDevice(deviceNames[deviceIndex]);
        }

        public void StartDevice(string deviceName)
        {
            StopCamera();

            activeDeviceName = deviceName;
            activeWebcam = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
            activeWebcam.Play();
            AssignPreviewTexture(activeWebcam);
            DeviceStarted?.Invoke(activeDeviceName);
        }

        public Texture2D CaptureCurrentFrame()
        {
            if (activeWebcam == null || !activeWebcam.isPlaying || activeWebcam.width <= 16 || activeWebcam.height <= 16)
            {
                CaptureFailed?.Invoke("La webcam todavía no tiene un frame válido.");
                return null;
            }

            Color32[] pixels = activeWebcam.GetPixels32();
            ApplyColorTemperature(pixels);

            Texture2D frame = new Texture2D(activeWebcam.width, activeWebcam.height, TextureFormat.RGBA32, false);
            frame.SetPixels32(pixels);
            frame.Apply(false, false);
            return frame;
        }

        public void SetColorTemperatureMode(ColorTemperatureMode mode)
        {
            colorTemperatureMode = mode;
        }

        public void SetManualColorTemperature(float temperature)
        {
            manualColorTemperature = Mathf.Clamp(temperature, -1f, 1f);
        }

        public void SetAutomaticTemperatureBias(float bias)
        {
            automaticTemperatureBias = Mathf.Clamp(bias, -1f, 1f);
        }

        public void ApplyColorTemperature(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0 || colorTemperatureMode == ColorTemperatureMode.Disabled)
            {
                return;
            }

            float temperature = manualColorTemperature;
            if (colorTemperatureMode == ColorTemperatureMode.Automatic)
            {
                lastAutomaticTemperature = EstimateAutomaticTemperature(pixels);
                temperature = Mathf.Clamp(lastAutomaticTemperature * automaticCorrectionStrength + automaticTemperatureBias, -1f, 1f);
            }

            ApplyTemperatureToPixels(pixels, temperature);
        }

        public void SetPreviewVisible(bool visible)
        {
            if (previewRenderer != null)
            {
                previewRenderer.enabled = visible;
            }

            if (previewRawImage != null)
            {
                previewRawImage.enabled = visible;
            }
        }

        public void StopCamera()
        {
            if (activeWebcam == null)
            {
                return;
            }

            if (activeWebcam.isPlaying)
            {
                activeWebcam.Stop();
            }

            Destroy(activeWebcam);
            activeWebcam = null;
            AssignPreviewTexture(null);
        }

        private void AssignPreviewTexture(Texture texture)
        {
            if (previewRenderer != null)
            {
                previewRenderer.material.SetTexture(previewTextureProperty, texture);
            }

            if (previewRawImage != null)
            {
                previewRawImage.texture = texture;
            }
        }

        private float EstimateAutomaticTemperature(Color32[] pixels)
        {
            double redTotal = 0d;
            double blueTotal = 0d;
            int sampleCount = 0;
            int sampleStep = Mathf.Max(1, pixels.Length / 12000);

            for (int i = 0; i < pixels.Length; i += sampleStep)
            {
                Color32 pixel = pixels[i];
                float maximum = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) / 255f;
                float minimum = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b)) / 255f;
                float saturation = maximum > 0.001f ? (maximum - minimum) / maximum : 0f;

                if (maximum < automaticMinimumBrightness || saturation > automaticNeutralSaturation)
                {
                    continue;
                }

                redTotal += pixel.r;
                blueTotal += pixel.b;
                sampleCount++;
            }

            if (sampleCount < 16)
            {
                return 0f;
            }

            float averageRed = (float)(redTotal / sampleCount);
            float averageBlue = (float)(blueTotal / sampleCount);
            float averageLevel = Mathf.Max(1f, (averageRed + averageBlue) * 0.5f);
            return Mathf.Clamp((averageBlue - averageRed) / averageLevel, -1f, 1f);
        }

        private static void ApplyTemperatureToPixels(Color32[] pixels, float temperature)
        {
            if (Mathf.Abs(temperature) < 0.001f)
            {
                return;
            }

            float redGain = 1f + temperature * 0.45f;
            float greenGain = 1f + Mathf.Abs(temperature) * 0.03f;
            float blueGain = 1f - temperature * 0.45f;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                pixel.r = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * redGain), 0, 255);
                pixel.g = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.g * greenGain), 0, 255);
                pixel.b = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.b * blueGain), 0, 255);
                pixels[i] = pixel;
            }
        }
    }
}
