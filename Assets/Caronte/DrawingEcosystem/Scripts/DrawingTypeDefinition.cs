using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    [CreateAssetMenu(menuName = "Caronte/Drawing Ecosystem/Drawing Type Definition", fileName = "DrawingTypeDefinition")]
    public sealed class DrawingTypeDefinition : ScriptableObject
    {
        [SerializeField] private DrawingType type;
        [SerializeField] private string displayName = "Nuevo organismo";
        [SerializeField] private GameObject prefab;
        [SerializeField] private Texture2D optionalMask;
        [SerializeField] private RectInt cropRegion = new RectInt(0, 0, 1024, 768);
        [SerializeField] private string textureProperty = "_BaseMap";
        [SerializeField] private Vector3 spawnCenter;
        [SerializeField] private Vector2 spawnArea = new Vector2(6f, 4f);
        [SerializeField, Min(0.01f)] private float uniformScale = 1f;
        [SerializeField] private bool randomYaw = true;

        public DrawingType Type => type;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? type.ToSpanishLabel() : displayName;
        public GameObject Prefab => prefab;
        public Texture2D OptionalMask => optionalMask;
        public RectInt CropRegion => cropRegion;
        public string TextureProperty => string.IsNullOrWhiteSpace(textureProperty) ? "_BaseMap" : textureProperty;
        public Vector3 SpawnCenter => spawnCenter;
        public Vector2 SpawnArea => spawnArea;
        public float UniformScale => uniformScale;
        public bool RandomYaw => randomYaw;

        public Vector3 GetSpawnPosition()
        {
            float x = Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f);
            float z = Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f);
            return spawnCenter + new Vector3(x, 0f, z);
        }

        public Quaternion GetSpawnRotation()
        {
            return randomYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
        }

        public Vector3 GetSpawnScale()
        {
            return Vector3.one * Mathf.Max(0.01f, uniformScale);
        }
    }
}
