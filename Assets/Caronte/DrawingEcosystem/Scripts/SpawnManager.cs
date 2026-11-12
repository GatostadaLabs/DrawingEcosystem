using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class SpawnManager : MonoBehaviour
    {
        [SerializeField] private DrawingTypeDefinition[] definitions;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Renderer backgroundRenderer;
        [SerializeField, Min(0.01f)] private float backgroundOffset = 0.15f;
        [SerializeField, Min(0f)] private float minimumSpawnSeparation = 0.2f;
        [SerializeField, Min(1)] private int maxSpawnPositionAttempts = 40;
        [SerializeField] private bool applyTextureToChildren = true;

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private MaterialPropertyBlock propertyBlock;

        public event Action<GameObject, DrawingTypeDefinition, Texture2D> DrawingSpawned;
        public IReadOnlyList<GameObject> SpawnedObjects => spawnedObjects;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public DrawingTypeDefinition GetDefinition(DrawingType type)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && definitions[i].Type == type)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        public GameObject Spawn(DrawingTypeDefinition definition, Texture2D texture)
        {
            if (definition == null || texture == null)
            {
                return null;
            }

            GameObject instance;
            Vector3 spawnPosition = GetSpawnPosition(definition);
            Quaternion spawnRotation = GetSpawnRotation();
            if (definition.Prefab != null)
            {
                instance = Instantiate(definition.Prefab, spawnPosition, spawnRotation, spawnParent);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(spawnParent, true);
                instance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            }

            instance.name = $"{definition.DisplayName}_Captured_{spawnedObjects.Count + 1:000}";
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, definition.GetSpawnScale());

            if (!TryFindAvailableSpawnPosition(definition, instance, spawnPosition, out spawnPosition))
            {
                Debug.LogWarning($"No hay espacio libre para instanciar {definition.DisplayName} dentro de su zona de spawn.", this);
                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }

                return null;
            }

            instance.transform.position = spawnPosition;
            ApplyTexture(instance, texture, definition.TextureProperty);

            CapturedDrawingInstance drawingInstance = instance.GetComponent<CapturedDrawingInstance>();
            if (drawingInstance == null)
            {
                drawingInstance = instance.AddComponent<CapturedDrawingInstance>();
            }

            drawingInstance.Initialize(texture, this);
            spawnedObjects.Add(instance);
            DrawingSpawned?.Invoke(instance, definition, texture);
            return instance;
        }

        public void ForgetSpawnedObject(GameObject instance)
        {
            spawnedObjects.Remove(instance);
            spawnedObjects.RemoveAll(spawnedObject => spawnedObject == null);
        }

        private Vector3 GetSpawnPosition(DrawingTypeDefinition definition)
        {
            if (backgroundRenderer == null || targetCamera == null)
            {
                return definition.GetSpawnPosition();
            }

            Transform backgroundTransform = backgroundRenderer.transform;
            Vector2 spawnArea = definition.SpawnArea;
            Vector3 spawnCenter = definition.SpawnCenter;
            float horizontalOffset = UnityEngine.Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f);
            float verticalOffset = UnityEngine.Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f);
            Vector3 position = backgroundRenderer.bounds.center;
            position += backgroundTransform.right * (spawnCenter.x + horizontalOffset);
            position += backgroundTransform.up * (spawnCenter.y + verticalOffset);
            position += (targetCamera.transform.position - position).normalized * backgroundOffset;
            return position;
        }

        private bool TryFindAvailableSpawnPosition(
            DrawingTypeDefinition definition,
            GameObject instance,
            Vector3 firstCandidate,
            out Vector3 spawnPosition)
        {
            spawnedObjects.RemoveAll(spawnedObject => spawnedObject == null);
            int attempts = Mathf.Max(1, maxSpawnPositionAttempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 candidate = attempt == 0 ? firstCandidate : GetSpawnPosition(definition);
                instance.transform.position = candidate;

                if (IsSpawnPositionAvailable(instance))
                {
                    spawnPosition = candidate;
                    return true;
                }
            }

            spawnPosition = firstCandidate;
            return false;
        }

        private bool IsSpawnPositionAvailable(GameObject candidate)
        {
            if (!TryGetVisualFootprint(candidate, out Rect candidateFootprint))
            {
                return true;
            }

            float separation = Mathf.Max(0f, minimumSpawnSeparation);
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                GameObject spawnedObject = spawnedObjects[i];
                if (spawnedObject == null || !TryGetVisualFootprint(spawnedObject, out Rect occupiedFootprint))
                {
                    continue;
                }

                bool separated = candidateFootprint.xMax + separation <= occupiedFootprint.xMin
                    || candidateFootprint.xMin - separation >= occupiedFootprint.xMax
                    || candidateFootprint.yMax + separation <= occupiedFootprint.yMin
                    || candidateFootprint.yMin - separation >= occupiedFootprint.yMax;

                if (!separated)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetVisualFootprint(GameObject instance, out Rect footprint)
        {
            Vector3 horizontalAxis = backgroundRenderer != null
                ? backgroundRenderer.transform.right.normalized
                : Vector3.right;
            Vector3 verticalAxis = backgroundRenderer != null
                ? backgroundRenderer.transform.up.normalized
                : Vector3.forward;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            float minHorizontal = 0f;
            float maxHorizontal = 0f;
            float minVertical = 0f;
            float maxVertical = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (!(targetRenderer is MeshRenderer)
                    && !(targetRenderer is SkinnedMeshRenderer)
                    && !(targetRenderer is SpriteRenderer))
                {
                    continue;
                }

                Bounds bounds = targetRenderer.bounds;
                Vector3 extents = bounds.extents;
                float horizontalCenter = Vector3.Dot(bounds.center, horizontalAxis);
                float verticalCenter = Vector3.Dot(bounds.center, verticalAxis);
                float horizontalExtent = Mathf.Abs(horizontalAxis.x) * extents.x
                    + Mathf.Abs(horizontalAxis.y) * extents.y
                    + Mathf.Abs(horizontalAxis.z) * extents.z;
                float verticalExtent = Mathf.Abs(verticalAxis.x) * extents.x
                    + Mathf.Abs(verticalAxis.y) * extents.y
                    + Mathf.Abs(verticalAxis.z) * extents.z;
                float rendererMinHorizontal = horizontalCenter - horizontalExtent;
                float rendererMaxHorizontal = horizontalCenter + horizontalExtent;
                float rendererMinVertical = verticalCenter - verticalExtent;
                float rendererMaxVertical = verticalCenter + verticalExtent;

                if (!hasBounds)
                {
                    minHorizontal = rendererMinHorizontal;
                    maxHorizontal = rendererMaxHorizontal;
                    minVertical = rendererMinVertical;
                    maxVertical = rendererMaxVertical;
                    hasBounds = true;
                    continue;
                }

                minHorizontal = Mathf.Min(minHorizontal, rendererMinHorizontal);
                maxHorizontal = Mathf.Max(maxHorizontal, rendererMaxHorizontal);
                minVertical = Mathf.Min(minVertical, rendererMinVertical);
                maxVertical = Mathf.Max(maxVertical, rendererMaxVertical);
            }

            footprint = hasBounds
                ? Rect.MinMaxRect(minHorizontal, minVertical, maxHorizontal, maxVertical)
                : default;
            return hasBounds;
        }

        private Quaternion GetSpawnRotation()
        {
            return targetCamera != null
                ? targetCamera.transform.rotation * Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.Euler(0f, 180f, 0f);
        }

        private void ApplyTexture(GameObject instance, Texture texture, string textureProperty)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            Renderer[] renderers = applyTextureToChildren ? instance.GetComponentsInChildren<Renderer>() : new[] { instance.GetComponent<Renderer>() };
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(textureProperty, texture);
                propertyBlock.SetTexture("_MainTex", texture);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
