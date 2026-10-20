using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class GameStartupSettings : MonoBehaviour
    {
        private const int FocusAttemptCount = 30;
        private const int TargetFrameRate = 25;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameObject startupObject = new GameObject(nameof(GameStartupSettings));
            DontDestroyOnLoad(startupObject);
            startupObject.hideFlags = HideFlags.HideInHierarchy;
            startupObject.AddComponent<GameStartupSettings>();
        }

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
            HideCursor();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(BringGameWindowToFront());
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                HideCursor();
            }
        }

        private static void HideCursor()
        {
            Cursor.visible = false;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator BringGameWindowToFront()
        {
            Process currentProcess = Process.GetCurrentProcess();

            for (int attempt = 0; attempt < FocusAttemptCount; attempt++)
            {
                yield return null;

                currentProcess.Refresh();
                System.IntPtr windowHandle = currentProcess.MainWindowHandle;
                if (windowHandle == System.IntPtr.Zero)
                {
                    continue;
                }

                ShowWindow(windowHandle, ShowWindowRestore);
                BringWindowToTop(windowHandle);
                SetForegroundWindow(windowHandle);
                yield break;
            }
        }

        private const int ShowWindowRestore = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(System.IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(System.IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(System.IntPtr windowHandle, int command);
#endif
    }
}
