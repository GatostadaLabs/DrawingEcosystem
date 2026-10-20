using System.Collections;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class CapturedDrawingInstance : MonoBehaviour
    {
        private Texture2D ownedTexture;
        private SpawnManager spawnManager;
        private EcosystemManager ecosystemManager;
        private DrawingType drawingType;
        private Coroutine lifetimeRoutine;
        private bool registeredInEcosystem;
        private bool applicationQuitting;

        public void Initialize(Texture2D texture, SpawnManager owner)
        {
            ownedTexture = texture;
            spawnManager = owner;
        }

        public void BeginLifetime(DrawingType type, EcosystemManager manager, float lifetime, float scaleOutDuration)
        {
            if (lifetimeRoutine != null)
            {
                StopCoroutine(lifetimeRoutine);
            }

            drawingType = type;
            ecosystemManager = manager;
            registeredInEcosystem = manager != null;
            lifetimeRoutine = StartCoroutine(LifetimeRoutine(lifetime, scaleOutDuration));
        }

        private IEnumerator LifetimeRoutine(float lifetime, float scaleOutDuration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime));

            float duration = Mathf.Max(0.01f, scaleOutDuration);
            Vector3 initialScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.localScale = Vector3.LerpUnclamped(initialScale, Vector3.zero, progress);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            RemoveFromEcosystem();
            Destroy(gameObject);
        }

        private void RemoveFromEcosystem()
        {
            if (!registeredInEcosystem)
            {
                return;
            }

            registeredInEcosystem = false;
            if (ecosystemManager != null)
            {
                ecosystemManager.UnregisterSpawn(drawingType);
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (!applicationQuitting)
            {
                RemoveFromEcosystem();
            }

            if (spawnManager != null)
            {
                spawnManager.ForgetSpawnedObject(gameObject);
            }

            if (ownedTexture != null)
            {
                Destroy(ownedTexture);
                ownedTexture = null;
            }
        }
    }
}
