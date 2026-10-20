using System.Collections;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class EcosystemPresentationController : MonoBehaviour
    {
        [Header("Eventos del juego")]
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private BackgroundVideoController backgroundVideoController;

        [Header("Efecto de aparición")]
        [SerializeField] private Shader revealShader;
        [SerializeField, Min(0.1f)] private float spawnEffectDuration = 1.8f;
        [SerializeField] private Color[] organismEffectColors =
        {
            new Color(0.15f, 1f, 0.65f, 1f),
            new Color(0.75f, 0.25f, 1f, 1f),
            new Color(0.25f, 0.85f, 1f, 1f),
            new Color(1f, 0.75f, 0.15f, 1f)
        };

        [Header("Sonidos de aparición: Micorriza, Hongo, Bacteria, Nematodo")]
        [SerializeField] private AudioClip[] spawnScanSounds = new AudioClip[2];
        [SerializeField] private AudioClip[] organismSpawnSounds = new AudioClip[4];
        [SerializeField, Range(0f, 1f)] private float organismSoundVolume = 1f;
        [SerializeField] private AudioSource organismAudioSource;

        [Header("Ambientes: Fondo 1, 2, 3, 4, 5")]
        [SerializeField] private AudioClip[] backgroundAmbienceClips = new AudioClip[5];
        [SerializeField, Range(0f, 1f)] private float backgroundVolume = 0.55f;
        [SerializeField, Min(0f)] private float backgroundAudioFadeDuration = 1.25f;
        [SerializeField] private AudioSource backgroundAudioSource;

        private Coroutine backgroundAudioRoutine;

        private void Awake()
        {
            if (spawnManager == null)
            {
                spawnManager = FindFirstObjectByType<SpawnManager>();
            }

            if (backgroundVideoController == null)
            {
                backgroundVideoController = FindFirstObjectByType<BackgroundVideoController>();
            }

            EnsureAudioSources();
            if (revealShader == null)
            {
                revealShader = Shader.Find("Caronte/Digital Reveal");
            }
        }

        private void OnEnable()
        {
            if (spawnManager != null)
            {
                spawnManager.DrawingSpawned += OnDrawingSpawned;
            }

            if (backgroundVideoController != null)
            {
                backgroundVideoController.BackgroundChanged += OnBackgroundChanged;
            }
        }

        private void Start()
        {
            if (backgroundVideoController != null)
            {
                OnBackgroundChanged(backgroundVideoController.CurrentIndex);
            }
        }

        private void OnDisable()
        {
            if (spawnManager != null)
            {
                spawnManager.DrawingSpawned -= OnDrawingSpawned;
            }

            if (backgroundVideoController != null)
            {
                backgroundVideoController.BackgroundChanged -= OnBackgroundChanged;
            }
        }

        private void OnDrawingSpawned(GameObject instance, DrawingTypeDefinition definition, Texture2D texture)
        {
            if (instance == null || definition == null)
            {
                return;
            }

            int index = (int)definition.Type;
            Color effectColor = GetArrayValue(organismEffectColors, index, Color.cyan);
            SpawnRevealEffect revealEffect = instance.AddComponent<SpawnRevealEffect>();
            revealEffect.Play(effectColor, spawnEffectDuration, revealShader);

            PlayOneShots(spawnScanSounds);
            AudioClip spawnSound = GetArrayValue<AudioClip>(organismSpawnSounds, index, null);
            if (spawnSound != null)
            {
                StartCoroutine(PlaySpawnSoundAfterDelay(spawnSound, spawnEffectDuration));
            }
        }

        private IEnumerator PlaySpawnSoundAfterDelay(AudioClip spawnSound, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, delay));
            if (organismAudioSource != null && spawnSound != null)
            {
                organismAudioSource.PlayOneShot(spawnSound, organismSoundVolume);
            }
        }

        private void PlayOneShots(AudioClip[] sounds)
        {
            if (organismAudioSource == null || sounds == null)
            {
                return;
            }

            foreach (AudioClip sound in sounds)
            {
                if (sound != null)
                {
                    organismAudioSource.PlayOneShot(sound, organismSoundVolume);
                }
            }
        }

        private void OnBackgroundChanged(int backgroundIndex)
        {
            AudioClip ambience = GetArrayValue<AudioClip>(backgroundAmbienceClips, backgroundIndex, null);
            if (backgroundAudioSource != null && backgroundAudioSource.isPlaying && backgroundAudioSource.clip == ambience)
            {
                return;
            }

            if (backgroundAudioRoutine != null)
            {
                StopCoroutine(backgroundAudioRoutine);
            }

            backgroundAudioRoutine = StartCoroutine(ChangeBackgroundAudio(ambience));
        }

        private IEnumerator ChangeBackgroundAudio(AudioClip nextClip)
        {
            if (backgroundAudioSource == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, backgroundAudioFadeDuration);
            float startVolume = backgroundAudioSource.volume;
            float elapsed = 0f;
            while (elapsed < duration && backgroundAudioSource.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                backgroundAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            backgroundAudioSource.Stop();
            backgroundAudioSource.clip = nextClip;
            if (nextClip == null)
            {
                backgroundAudioSource.volume = backgroundVolume;
                backgroundAudioRoutine = null;
                yield break;
            }

            backgroundAudioSource.loop = true;
            backgroundAudioSource.volume = 0f;
            backgroundAudioSource.Play();

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                backgroundAudioSource.volume = Mathf.Lerp(0f, backgroundVolume, elapsed / duration);
                yield return null;
            }

            backgroundAudioSource.volume = backgroundVolume;
            backgroundAudioRoutine = null;
        }

        private void EnsureAudioSources()
        {
            if (organismAudioSource == null)
            {
                organismAudioSource = gameObject.AddComponent<AudioSource>();
                organismAudioSource.playOnAwake = false;
                organismAudioSource.spatialBlend = 0f;
            }

            if (backgroundAudioSource == null)
            {
                backgroundAudioSource = gameObject.AddComponent<AudioSource>();
                backgroundAudioSource.playOnAwake = false;
                backgroundAudioSource.loop = true;
                backgroundAudioSource.spatialBlend = 0f;
                backgroundAudioSource.volume = backgroundVolume;
            }
        }

        private static T GetArrayValue<T>(T[] values, int index, T fallback)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }
    }
}
