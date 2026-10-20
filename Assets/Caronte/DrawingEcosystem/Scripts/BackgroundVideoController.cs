using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace Caronte.DrawingEcosystem
{
    public sealed class BackgroundVideoController : MonoBehaviour
    {
        private const float VideoAspectRatio = 16f / 9f;

        [SerializeField] private VideoClip[] backgroundClips = new VideoClip[5];
        [SerializeField] private VideoPlayer playerA;
        [SerializeField] private VideoPlayer playerB;
        [SerializeField] private Renderer rendererA;
        [SerializeField] private Renderer rendererB;
        [SerializeField] private float fadeDuration = 1.5f;
        [SerializeField, Min(0f)] private float loopPreparationLeadTime = 0.5f;
        [SerializeField] private string videoTextureProperty = "_BaseMap";
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool fitToCamera = true;
        [SerializeField, Min(1f)] private float cameraOverscan = 1.02f;

        private int currentIndex;
        private bool usingA = true;
        private Coroutine fadeRoutine;
        private Material materialA;
        private Material materialB;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public event Action<int> BackgroundChanged;
        public int CurrentIndex => currentIndex;
        public string CurrentLabel => $"Fondo {currentIndex + 1}";

        private void Awake()
        {
            materialA = rendererA != null ? rendererA.material : null;
            materialB = rendererB != null ? rendererB.material : null;
            ConfigurePlayer(playerA, rendererA);
            ConfigurePlayer(playerB, rendererB);
            SetAlpha(materialA, 1f);
            SetAlpha(materialB, 0f);
            FitBackgroundsToCamera();
        }

        private void LateUpdate()
        {
            if (fitToCamera && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
            {
                FitBackgroundsToCamera();
            }

            TryStartLoopCrossfade();
        }

        private void Start()
        {
            if (playOnStart)
            {
                SetBackgroundIndex(0, true);
            }
        }

        public void SetBackgroundIndex(int index) => SetBackgroundIndex(index, false);

        public void SetBackgroundIndex(int index, bool immediate)
        {
            int safeIndex = Mathf.Clamp(index, 0, 4);
            if (backgroundClips == null || backgroundClips.Length == 0 || backgroundClips[Mathf.Min(safeIndex, backgroundClips.Length - 1)] == null)
            {
                currentIndex = safeIndex;
                BackgroundChanged?.Invoke(currentIndex);
                return;
            }

            if (!immediate && safeIndex == currentIndex)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(ChangeBackgroundRoutine(safeIndex, immediate));
        }

        public void ForceBackgroundIndex(int index)
        {
            SetBackgroundIndex(index, false);
        }

        private IEnumerator ChangeBackgroundRoutine(int index, bool immediate)
        {
            VideoPlayer incomingPlayer = usingA ? playerB : playerA;
            VideoPlayer outgoingPlayer = usingA ? playerA : playerB;
            Material incomingMaterial = usingA ? materialB : materialA;
            Material outgoingMaterial = usingA ? materialA : materialB;
            Renderer incomingRenderer = usingA ? rendererB : rendererA;
            Renderer outgoingRenderer = usingA ? rendererA : rendererB;
            VideoClip clip = backgroundClips[Mathf.Min(index, backgroundClips.Length - 1)];

            SetAlpha(incomingMaterial, 0f);
            SetAlpha(outgoingMaterial, 1f);
            SetRenderOrder(outgoingRenderer, incomingRenderer);

            incomingPlayer.Stop();
            incomingPlayer.clip = clip;
            incomingPlayer.isLooping = false;
            incomingPlayer.Prepare();
            while (!incomingPlayer.isPrepared)
            {
                yield return null;
            }

            incomingPlayer.time = GetSynchronizedTime(outgoingPlayer, clip);
            incomingPlayer.Play();

            if (immediate)
            {
                SetAlpha(incomingMaterial, 1f);
                SetAlpha(outgoingMaterial, 0f);
            }
            else
            {
                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, fadeDuration);
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetAlpha(incomingMaterial, t);
                    yield return null;
                }
            }

            SetAlpha(incomingMaterial, 1f);
            SetAlpha(outgoingMaterial, 0f);
            if (outgoingPlayer != null && outgoingPlayer.isPlaying)
            {
                outgoingPlayer.Stop();
            }

            usingA = !usingA;
            currentIndex = index;
            BackgroundChanged?.Invoke(currentIndex);
            fadeRoutine = null;
        }

        private void ConfigurePlayer(VideoPlayer player, Renderer targetRenderer)
        {
            if (player == null)
            {
                return;
            }

            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.MaterialOverride;
            player.targetMaterialRenderer = targetRenderer;
            player.targetMaterialProperty = videoTextureProperty;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
        }

        private void TryStartLoopCrossfade()
        {
            if (fadeRoutine != null)
            {
                return;
            }

            VideoPlayer activePlayer = usingA ? playerA : playerB;
            if (activePlayer == null || !activePlayer.isPrepared || !activePlayer.isPlaying || activePlayer.clip == null)
            {
                return;
            }

            double remainingTime = activePlayer.length - activePlayer.time;
            double preparationThreshold = Mathf.Max(0.01f, fadeDuration) + loopPreparationLeadTime;
            if (remainingTime <= preparationThreshold)
            {
                fadeRoutine = StartCoroutine(LoopCrossfadeRoutine());
            }
        }

        private IEnumerator LoopCrossfadeRoutine()
        {
            VideoPlayer outgoingPlayer = usingA ? playerA : playerB;
            VideoPlayer incomingPlayer = usingA ? playerB : playerA;
            Material outgoingMaterial = usingA ? materialA : materialB;
            Material incomingMaterial = usingA ? materialB : materialA;
            Renderer outgoingRenderer = usingA ? rendererA : rendererB;
            Renderer incomingRenderer = usingA ? rendererB : rendererA;
            VideoClip clip = outgoingPlayer != null ? outgoingPlayer.clip : null;

            if (outgoingPlayer == null || incomingPlayer == null || clip == null)
            {
                fadeRoutine = null;
                yield break;
            }

            SetAlpha(incomingMaterial, 0f);
            SetAlpha(outgoingMaterial, 1f);
            SetRenderOrder(outgoingRenderer, incomingRenderer);

            incomingPlayer.Stop();
            incomingPlayer.clip = clip;
            incomingPlayer.isLooping = false;
            incomingPlayer.Prepare();
            while (!incomingPlayer.isPrepared)
            {
                yield return null;
            }

            float duration = Mathf.Max(0.01f, fadeDuration);
            while (outgoingPlayer.isPlaying && outgoingPlayer.length - outgoingPlayer.time > duration)
            {
                yield return null;
            }

            incomingPlayer.time = 0d;
            incomingPlayer.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(incomingMaterial, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            SetAlpha(incomingMaterial, 1f);
            SetAlpha(outgoingMaterial, 0f);
            outgoingPlayer.Stop();
            usingA = !usingA;
            fadeRoutine = null;
        }

        private static double GetSynchronizedTime(VideoPlayer outgoingPlayer, VideoClip incomingClip)
        {
            if (outgoingPlayer == null || !outgoingPlayer.isPrepared || incomingClip == null || incomingClip.length <= 0d)
            {
                return 0d;
            }

            double synchronizedTime = outgoingPlayer.time % incomingClip.length;
            return synchronizedTime < 0d ? synchronizedTime + incomingClip.length : synchronizedTime;
        }

        private void FitBackgroundsToCamera()
        {
            if (!fitToCamera)
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }

            FitRendererToCamera(rendererA, targetCamera);
            FitRendererToCamera(rendererB, targetCamera);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void FitRendererToCamera(Renderer targetRenderer, Camera targetCamera)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Transform target = targetRenderer.transform;
            float distance = Vector3.Dot(target.position - targetCamera.transform.position, targetCamera.transform.forward);
            if (distance <= 0f)
            {
                return;
            }

            float worldHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * targetCamera.aspect;
            float cameraAspect = worldWidth / worldHeight;
            if (cameraAspect > VideoAspectRatio)
            {
                worldHeight = worldWidth / VideoAspectRatio;
            }
            else
            {
                worldWidth = worldHeight * VideoAspectRatio;
            }

            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            Vector3 localScale = target.localScale;
            localScale.x = worldWidth * cameraOverscan / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            localScale.y = worldHeight * cameraOverscan / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
            target.localScale = localScale;
        }

        private static void SetRenderOrder(Renderer outgoingRenderer, Renderer incomingRenderer)
        {
            if (outgoingRenderer != null)
            {
                outgoingRenderer.sortingOrder = 0;
            }

            if (incomingRenderer != null)
            {
                incomingRenderer.sortingOrder = 1;
            }
        }

        private static void SetAlpha(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            Color color = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                color.a = alpha;
                material.SetColor("_Color", color);
            }
        }
    }
}
