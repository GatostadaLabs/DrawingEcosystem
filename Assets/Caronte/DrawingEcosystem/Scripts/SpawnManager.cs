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
