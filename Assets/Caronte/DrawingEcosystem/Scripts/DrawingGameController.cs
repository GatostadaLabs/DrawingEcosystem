using UnityEngine;

using UnityEngine.InputSystem;

namespace Caronte.DrawingEcosystem
{
    public sealed class DrawingGameController : MonoBehaviour
    {
        [SerializeField] private DrawingCaptureService captureService;
        [SerializeField] private TextureProcessor textureProcessor;
        [SerializeField] private MonoBehaviour templateDetectorComponent;
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private EcosystemManager ecosystemManager;
        [SerializeField] private BackgroundVideoController backgroundVideoController;

        [Header("Atajos de teclado")]
        [SerializeField] private bool enableSpawnShortcuts = true;

        [Header("Vida de los organismos")]
        [SerializeField, Min(0.1f)] private float organismLifetime = 1800f;
        [SerializeField, InspectorName("Organism Scale Out Duration"), Min(0.01f)] private float organismFadeDuration = 2f;

        [Header("Sonido del botón de captura")]
        [SerializeField] private AudioClip[] captureButtonSounds = new AudioClip[2];
        [SerializeField, Range(0f, 1f)] private float captureButtonSoundVolume = 1f;
        [SerializeField] private AudioSource captureButtonAudioSource;

        private ITemplateDetector templateDetector;
        private DrawingType currentType;
        private string lastStatus = "Inicializando";
        private bool debugToolsEnabled;

        public string LastStatus => lastStatus;
        public DrawingType CurrentType => currentType;

        private void Awake()
        {
            templateDetector = templateDetectorComponent as ITemplateDetector;
            if (templateDetector != null)
            {
                currentType = templateDetector.CurrentType;
            }

            if (captureButtonAudioSource == null)
            {
                captureButtonAudioSource = gameObject.AddComponent<AudioSource>();
                captureButtonAudioSource.playOnAwake = false;
                captureButtonAudioSource.spatialBlend = 0f;
            }
        }

        public void PlayCaptureButtonSounds()
        {
            if (captureButtonAudioSource == null || captureButtonSounds == null)
            {
                return;
            }

            foreach (AudioClip sound in captureButtonSounds)
            {
                if (sound != null)
                {
                    captureButtonAudioSource.PlayOneShot(sound, captureButtonSoundVolume);
                }
            }
        }

        private void OnEnable()
        {
            if (templateDetector != null)
            {
                templateDetector.TemplateChanged += OnTemplateChanged;
            }

            if (ecosystemManager != null)
            {
                ecosystemManager.EcosystemChanged += OnEcosystemChanged;
            }

            if (captureService != null)
            {
                captureService.CaptureFailed += OnCaptureFailed;
            }
        }

        private void OnDisable()
        {
            if (templateDetector != null)
            {
                templateDetector.TemplateChanged -= OnTemplateChanged;
            }

            if (ecosystemManager != null)
            {
                ecosystemManager.EcosystemChanged -= OnEcosystemChanged;
            }

            if (captureService != null)
            {
                captureService.CaptureFailed -= OnCaptureFailed;
            }
        }

        private void Update()
        {
            if (!debugToolsEnabled || !enableSpawnShortcuts || Keyboard.current == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SpawnDebugElement(DrawingType.Mycorrhiza);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SpawnDebugElement(DrawingType.Fungus);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SpawnDebugElement(DrawingType.Bacteria);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                SpawnDebugElement(DrawingType.Nematode);
            }
        }

        public void SetDebugToolsEnabled(bool enabled)
        {
            debugToolsEnabled = enabled;
        }

        public void CaptureSelectedTemplate()
        {
            TryCaptureSelectedTemplate();
        }

        public bool TryCaptureSelectedTemplate()
        {
            if (captureService == null || textureProcessor == null || spawnManager == null || ecosystemManager == null)
            {
                lastStatus = "Faltan referencias en DrawingGameController.";
                return false;
            }

            DrawingTypeDefinition definition = spawnManager.GetDefinition(currentType);
            if (definition == null)
            {
                lastStatus = $"No existe DrawingTypeDefinition para {currentType.ToSpanishLabel()}.";
                return false;
            }

            ITrackedTemplateDetector trackedDetector = templateDetector as ITrackedTemplateDetector;
            if (trackedDetector != null && !trackedDetector.HasValidTracking)
            {
                lastStatus = $"Captura bloqueada: {trackedDetector.TrackingStatus}.";
                return false;
            }

            Texture2D frame = trackedDetector != null
                ? trackedDetector.CaptureRectifiedFrame()
                : captureService.CaptureCurrentFrame();
            if (frame == null)
            {
                lastStatus = trackedDetector != null
                    ? "No se pudo rectificar la plantilla detectada."
                    : "No se pudo capturar frame de webcam.";
                return false;
            }

            Texture2D processedTexture = textureProcessor.Process(frame, definition);
            if (processedTexture == null)
            {
                lastStatus = "No se pudo procesar la textura capturada.";
                Destroy(frame);
                return false;
            }

            GameObject spawned = spawnManager.Spawn(definition, processedTexture);
            if (spawned == null)
            {
                lastStatus = "No se pudo instanciar el prefab.";
                Destroy(processedTexture);
                return false;
            }

            ecosystemManager.RegisterSpawn(definition.Type);
            BeginOrganismLifetime(spawned, definition.Type);
            lastStatus = $"Instanciado {definition.DisplayName}.";
            return true;
        }

        public void SelectType(DrawingType type)
        {
            currentType = type;
            if (templateDetectorComponent is MockTemplateDetector mockTemplateDetector)
            {
                mockTemplateDetector.SelectType(type);
            }
        }

        public void SpawnDebugElement(DrawingType type)
        {
            if (!debugToolsEnabled)
            {
                return;
            }

            if (spawnManager == null || ecosystemManager == null)
            {
                lastStatus = "Faltan referencias para instanciar el organismo de debug.";
                return;
            }

            DrawingTypeDefinition definition = spawnManager.GetDefinition(type);
            if (definition == null)
            {
                lastStatus = $"No existe DrawingTypeDefinition para {type.ToSpanishLabel()}.";
                return;
            }

            Texture2D debugTexture = CreateDebugTexture(definition);
            GameObject spawned = spawnManager.Spawn(definition, debugTexture);
            if (spawned == null)
            {
                Destroy(debugTexture);
                lastStatus = $"No se pudo instanciar {definition.DisplayName} en modo debug.";
                return;
            }

            ecosystemManager.RegisterSpawn(type);
            BeginOrganismLifetime(spawned, type);
            lastStatus = $"Instanciado {definition.DisplayName} en modo debug.";
        }

        private void BeginOrganismLifetime(GameObject spawned, DrawingType type)
        {
            CapturedDrawingInstance drawingInstance = spawned != null ? spawned.GetComponent<CapturedDrawingInstance>() : null;
            if (drawingInstance != null)
            {
                drawingInstance.BeginLifetime(type, ecosystemManager, organismLifetime, organismFadeDuration);
            }
        }

        private void OnTemplateChanged(DrawingType type)
        {
            currentType = type;
            lastStatus = $"Plantilla seleccionada: {type.ToSpanishLabel()}.";
        }

        private void OnEcosystemChanged(EcosystemSnapshot snapshot)
        {
            if (backgroundVideoController != null)
            {
                backgroundVideoController.SetBackgroundIndex(snapshot.backgroundIndex);
            }
        }

        private void OnCaptureFailed(string reason)
        {
            lastStatus = reason;
        }

        private static Texture2D CreateDebugTexture(DrawingTypeDefinition definition)
        {
            Texture2D mask = definition.OptionalMask;
            int width = mask != null ? mask.width : 2;
            int height = mask != null ? mask.height : 2;
            Color color = GetDebugColor(definition.Type);
            Color[] pixels = new Color[width * height];

            if (mask != null)
            {
                Color[] maskPixels = mask.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color maskColor = maskPixels[i];
                    pixels[i] = new Color(color.r, color.g, color.b, maskColor.a * maskColor.grayscale);
                }
            }
            else
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color;
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = $"Debug_{definition.Type}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Color GetDebugColor(DrawingType type)
        {
            switch (type)
            {
                case DrawingType.Mycorrhiza:
                    return new Color(0.85f, 0.45f, 0.2f, 1f);
                case DrawingType.Fungus:
                    return new Color(0.75f, 0.35f, 0.85f, 1f);
                case DrawingType.Bacteria:
                    return new Color(0.25f, 0.8f, 0.45f, 1f);
                case DrawingType.Nematode:
                    return new Color(0.25f, 0.65f, 0.95f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
