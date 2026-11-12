using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Caronte.DrawingEcosystem
{
    public sealed class DrawingTypeScaleManager : MonoBehaviour
    {
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField, HideInInspector] private bool initializedFromDefinitions;

        [Header("Escala por tipo")]
        [SerializeField, Min(0.01f)] private float mycorrhizaScale = 1f;
        [SerializeField, Min(0.01f)] private float fungusScale = 1f;
        [SerializeField, Min(0.01f)] private float bacteriaScale = 1f;
        [SerializeField, Min(0.01f)] private float nematodeScale = 1f;

        [ContextMenu("Aplicar escalas a definitions")]
        public void ApplyScales()
        {
            if (spawnManager == null)
            {
                return;
            }

            ApplyScale(DrawingType.Mycorrhiza, mycorrhizaScale);
            ApplyScale(DrawingType.Fungus, fungusScale);
            ApplyScale(DrawingType.Bacteria, bacteriaScale);
            ApplyScale(DrawingType.Nematode, nematodeScale);
        }

        private void OnValidate()
        {
            if (spawnManager == null)
            {
                return;
            }

            if (!initializedFromDefinitions)
            {
                PullScalesFromDefinitions();
                initializedFromDefinitions = true;
                return;
            }

            ApplyScales();
        }

        private void ApplyScale(DrawingType type, float scale)
        {
            DrawingTypeDefinition definition = spawnManager.GetDefinition(type);
            if (definition == null)
            {
                return;
            }

            definition.SetUniformScale(scale);
#if UNITY_EDITOR
            EditorUtility.SetDirty(definition);
#endif
        }

        [ContextMenu("Leer escalas desde definitions")]
        public void PullScalesFromDefinitions()
        {
            if (spawnManager == null)
            {
                return;
            }

            mycorrhizaScale = ReadScale(DrawingType.Mycorrhiza, mycorrhizaScale);
            fungusScale = ReadScale(DrawingType.Fungus, fungusScale);
            bacteriaScale = ReadScale(DrawingType.Bacteria, bacteriaScale);
            nematodeScale = ReadScale(DrawingType.Nematode, nematodeScale);
        }

        private float ReadScale(DrawingType type, float fallback)
        {
            DrawingTypeDefinition definition = spawnManager.GetDefinition(type);
            return definition != null ? Mathf.Max(0.01f, definition.UniformScale) : Mathf.Max(0.01f, fallback);
        }
    }
}
