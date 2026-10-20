using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Caronte.DrawingEcosystem
{
    public sealed class DebugGameUI : MonoBehaviour, ICaptureTrigger
    {
        [SerializeField] private DrawingGameController gameController;
        [SerializeField] private DrawingCaptureService captureService;
        [SerializeField] private MockTemplateDetector mockTemplateDetector;
        [SerializeField] private RealTemplateTracker realTemplateTracker;
        [SerializeField] private EcosystemManager ecosystemManager;
        [SerializeField] private BackgroundVideoController backgroundVideoController;
        [SerializeField] private bool showInterface;
        [SerializeField] private Rect windowRect = new Rect(16f, 16f, 360f, 520f);

        public event Action CaptureRequested;

        private Vector2 scroll;

        private void Awake()
        {
            SetDebugToolsEnabled(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                SetDebugToolsEnabled(!showInterface);
            }
        }

        private void OnGUI()
        {
            if (!showInterface)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Drawing Ecosystem Debug");
        }

        public void RequestCapture()
        {
            CaptureRequested?.Invoke();
            if (gameController != null)
            {
                gameController.PlayCaptureButtonSounds();
                gameController.CaptureSelectedTemplate();
            }
        }

        private void SetDebugToolsEnabled(bool enabled)
        {
            showInterface = enabled;

            if (captureService != null)
            {
                captureService.SetPreviewVisible(enabled);
            }

            if (gameController != null)
            {
                gameController.SetDebugToolsEnabled(enabled);
            }
        }

        private void DrawWindow(int windowId)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            DrawTemplateButtons();
            GUILayout.Space(8f);
            DrawCaptureControls();
            GUILayout.Space(8f);
            DrawCameraControls();
            GUILayout.Space(8f);
            DrawEcosystemInfo();
            GUILayout.Space(8f);
            DrawSpawnControls();
            GUILayout.Space(8f);
            DrawBackgroundControls();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawTemplateButtons()
        {
            if (realTemplateTracker != null)
            {
                DrawingType detected = gameController != null ? gameController.CurrentType : realTemplateTracker.CurrentType;
                GUILayout.Label($"Plantilla detectada: {detected.ToSpanishLabel()}");
                GUILayout.Label(realTemplateTracker.TrackingStatus);
                GUILayout.HorizontalSlider(realTemplateTracker.TrackingStability, 0f, 1f);
                return;
            }

            GUILayout.Label("Selección manual (debug)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Micorriza")) Select(DrawingType.Mycorrhiza);
            if (GUILayout.Button("Hongo")) Select(DrawingType.Fungus);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bacteria")) Select(DrawingType.Bacteria);
            if (GUILayout.Button("Nematodo")) Select(DrawingType.Nematode);
            GUILayout.EndHorizontal();
            DrawingType selected = gameController != null ? gameController.CurrentType : (mockTemplateDetector != null ? mockTemplateDetector.CurrentType : DrawingType.Mycorrhiza);
            GUILayout.Label($"Seleccionado: {selected.ToSpanishLabel()}");
        }

        private void DrawCaptureControls()
        {
            if (GUILayout.Button("Capturar dibujo"))
            {
                RequestCapture();
            }

            if (gameController != null)
            {
                GUILayout.Label($"Estado: {gameController.LastStatus}");
            }
        }

        private void DrawCameraControls()
        {
            GUILayout.Label("Webcam");
            if (captureService == null)
            {
                GUILayout.Label("Sin DrawingCaptureService asignado.");
                return;
            }

            GUILayout.Label($"Activa: {(string.IsNullOrEmpty(captureService.ActiveDeviceName) ? "Ninguna" : captureService.ActiveDeviceName)}");
            if (GUILayout.Button("Refrescar webcams"))
            {
                captureService.RefreshDevices();
            }

            for (int i = 0; i < captureService.DeviceNames.Count; i++)
            {
                string label = captureService.DeviceNames[i];
                if (GUILayout.Button(label))
                {
                    captureService.StartDevice(i);
                }
            }
        }

        private void DrawEcosystemInfo()
        {
            GUILayout.Label("Ecosistema");
            if (ecosystemManager == null)
            {
                GUILayout.Label("Sin EcosystemManager asignado.");
                return;
            }

            EcosystemSnapshot snapshot = ecosystemManager.Snapshot;
            GUILayout.Label($"Total: {snapshot.totalCount}");
            GUILayout.Label($"Micorriza: {snapshot.mycorrhizaCount} ({snapshot.mycorrhizaProportion:P0})");
            GUILayout.Label($"Hongo: {snapshot.fungusCount} ({snapshot.fungusProportion:P0})");
            GUILayout.Label($"Bacteria: {snapshot.bacteriaCount} ({snapshot.bacteriaProportion:P0})");
            GUILayout.Label($"Nematodo: {snapshot.nematodeCount} ({snapshot.nematodeProportion:P0})");
            GUILayout.Label($"Puntuación: {snapshot.score:0.0}/100");
            GUILayout.Label($"Fondo calculado: {snapshot.backgroundIndex + 1}");
        }

        private void DrawBackgroundControls()
        {
            GUILayout.Label("Forzar fondo");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 5; i++)
            {
                if (GUILayout.Button((i + 1).ToString()))
                {
                    if (backgroundVideoController != null)
                    {
                        backgroundVideoController.ForceBackgroundIndex(i);
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (backgroundVideoController != null)
            {
                GUILayout.Label($"Estado fondo: {backgroundVideoController.CurrentLabel}");
            }
        }

        private void DrawSpawnControls()
        {
            GUILayout.Label("Spawnear organismo");
            if (gameController == null)
            {
                GUILayout.Label("Sin DrawingGameController asignado.");
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Micorriza")) gameController.SpawnDebugElement(DrawingType.Mycorrhiza);
            if (GUILayout.Button("Hongo")) gameController.SpawnDebugElement(DrawingType.Fungus);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bacteria")) gameController.SpawnDebugElement(DrawingType.Bacteria);
            if (GUILayout.Button("Nematodo")) gameController.SpawnDebugElement(DrawingType.Nematode);
            GUILayout.EndHorizontal();
        }

        private void Select(DrawingType type)
        {
            if (mockTemplateDetector != null)
            {
                mockTemplateDetector.SelectType(type);
            }
            else if (gameController != null)
            {
                gameController.SelectType(type);
            }
        }
    }
}
