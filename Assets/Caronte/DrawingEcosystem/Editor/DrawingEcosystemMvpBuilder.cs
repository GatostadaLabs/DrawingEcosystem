using System.IO;
using Caronte.DrawingEcosystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Caronte.DrawingEcosystem.Editor
{
    public static class DrawingEcosystemMvpBuilder
    {
        private const string Root = "Assets/Caronte/DrawingEcosystem";
        private const string ScenePath = Root + "/Scenes/DrawingEcosystem_MVP.unity";
        private const string PrefabPath = Root + "/Prefabs";
        private const string ScriptableObjectPath = Root + "/ScriptableObjects";
        private const string GeneratedPath = Root + "/Generated";

        [MenuItem("Tools/Caronte/Drawing Ecosystem/Create MVP Scene")]
        public static void CreateMvpScene()
        {
            EnsureFolders();
            Material organismMaterial = CreateMaterial("M_DrawingOrganism", Color.white, false);
            Material backgroundMaterialA = CreateMaterial("M_BackgroundVideo_A", Color.white, true);
            Material backgroundMaterialB = CreateMaterial("M_BackgroundVideo_B", new Color(1f, 1f, 1f, 0f), true);
            Texture2D whiteMask = CreateWhiteMask();
            Texture2D mycorrhizaMask = LoadMask("Mask_Mycorrhiza", whiteMask);
            Texture2D fungusMask = LoadMask("Mask_Fungus", whiteMask);
            Texture2D bacteriaMask = LoadMask("Mask_Bacteria", whiteMask);
            Texture2D nematodeMask = LoadMask("Mask_Nematode", whiteMask);

            GameObject mycorrhizaPrefab = CreatePrefab(DrawingType.Mycorrhiza, PrimitiveType.Capsule, organismMaterial, new Vector3(-2.4f, 0.5f, 0f));
            GameObject fungusPrefab = CreateFungusPrefab(organismMaterial);
            GameObject bacteriaPrefab = CreatePrefab(DrawingType.Bacteria, PrimitiveType.Sphere, organismMaterial, new Vector3(0.8f, 0.5f, 0f));
            GameObject nematodePrefab = CreatePrefab(DrawingType.Nematode, PrimitiveType.Capsule, organismMaterial, new Vector3(2.4f, 0.5f, 0f));

            DrawingTypeDefinition mycorrhiza = CreateDefinition(DrawingType.Mycorrhiza, "Micorriza", mycorrhizaPrefab, mycorrhizaMask, new Vector3(-2.4f, 0f, 0f));
            DrawingTypeDefinition fungus = CreateDefinition(DrawingType.Fungus, "Hongo", fungusPrefab, fungusMask, new Vector3(-0.8f, 0f, 0f));
            DrawingTypeDefinition bacteria = CreateDefinition(DrawingType.Bacteria, "Bacteria", bacteriaPrefab, bacteriaMask, new Vector3(0.8f, 0f, 0f));
            DrawingTypeDefinition nematode = CreateDefinition(DrawingType.Nematode, "Nematodo", nematodePrefab, nematodeMask, new Vector3(2.4f, 0f, 0f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DrawingEcosystem_MVP";

            Camera camera = CreateCamera();
            CreateLight();
            CreateGround();

            GameObject backgroundA = CreateQuad("BackgroundVideo_A", new Vector3(0f, 1.6f, 5.5f), new Vector3(8f, 4.5f, 1f), backgroundMaterialA);
            GameObject backgroundB = CreateQuad("BackgroundVideo_B", new Vector3(0f, 1.6f, 5.45f), new Vector3(8f, 4.5f, 1f), backgroundMaterialB);
            GameObject preview = CreateQuad("WebcamPreview", new Vector3(3.4f, 1.3f, -1.2f), new Vector3(1.8f, 1.2f, 1f), CreateMaterial("M_WebcamPreview", Color.white, false));
            preview.transform.rotation = Quaternion.Euler(0f, -25f, 0f);

            GameObject managers = new GameObject("DrawingEcosystem_Managers");
            DrawingCaptureService capture = managers.AddComponent<DrawingCaptureService>();
            TextureProcessor processor = managers.AddComponent<TextureProcessor>();
            RealTemplateTracker detector = managers.AddComponent<RealTemplateTracker>();
            SpawnManager spawn = managers.AddComponent<SpawnManager>();
            EcosystemManager ecosystem = managers.AddComponent<EcosystemManager>();
            BackgroundVideoController background = managers.AddComponent<BackgroundVideoController>();
            DrawingGameController controller = managers.AddComponent<DrawingGameController>();
            DebugGameUI debugUi = managers.AddComponent<DebugGameUI>();

            GameObject spawnRoot = new GameObject("SpawnedDrawings");
            Assign(capture, "previewRenderer", preview.GetComponent<Renderer>());
            Assign(detector, "captureService", capture);
            Assign(spawn, "definitions", new[] { mycorrhiza, fungus, bacteria, nematode });
            Assign(spawn, "spawnParent", spawnRoot.transform);
            Assign(background, "playerA", backgroundA.AddComponent<VideoPlayer>());
            Assign(background, "playerB", backgroundB.AddComponent<VideoPlayer>());
            Assign(background, "rendererA", backgroundA.GetComponent<Renderer>());
            Assign(background, "rendererB", backgroundB.GetComponent<Renderer>());
            Assign(controller, "captureService", capture);
            Assign(controller, "textureProcessor", processor);
            Assign(controller, "templateDetectorComponent", detector);
            Assign(controller, "spawnManager", spawn);
            Assign(controller, "ecosystemManager", ecosystem);
            Assign(controller, "backgroundVideoController", background);
            Assign(debugUi, "gameController", controller);
            Assign(debugUi, "captureService", capture);
            Assign(debugUi, "realTemplateTracker", detector);
            Assign(debugUi, "ecosystemManager", ecosystem);
            Assign(debugUi, "backgroundVideoController", background);
            Assign(debugUi, "showInterface", false);

            Selection.activeObject = managers;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Drawing Ecosystem MVP scene created: {ScenePath}. Assign five VideoClips in BackgroundVideoController and replace placeholder prefabs/masks when ready.", camera);
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Caronte");
            CreateFolder("Assets/Caronte", "DrawingEcosystem");
            CreateFolder(Root, "Scenes");
            CreateFolder(Root, "Prefabs");
            CreateFolder(Root, "ScriptableObjects");
            CreateFolder(Root, "Generated");
            CreateFolder(Root, "Masks");
        }

        private static Texture2D LoadMask(string name, Texture2D fallback)
        {
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Masks/{name}.png");
            return mask != null ? mask : fallback;
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material CreateMaterial(string name, Color color, bool transparent)
        {
            string path = GeneratedPath + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Texture");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateWhiteMask()
        {
            string path = GeneratedPath + "/WhiteMask.png";
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[512 * 512];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject CreatePrefab(DrawingType type, PrimitiveType primitiveType, Material material, Vector3 previewPosition)
        {
            string path = PrefabPath + "/PF_" + type + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = "PF_" + type;
            instance.transform.position = previewPosition;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            if (type == DrawingType.Nematode)
            {
                instance.transform.localScale = new Vector3(0.35f, 1.8f, 0.35f);
                instance.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateFungusPrefab(Material material)
        {
            string path = PrefabPath + "/PF_Fungus.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject("PF_Fungus");
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(root.transform);
            stem.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            stem.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            stem.GetComponent<Renderer>().sharedMaterial = material;
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform);
            cap.transform.localPosition = new Vector3(0f, 1f, 0f);
            cap.transform.localScale = new Vector3(1f, 0.35f, 1f);
            cap.GetComponent<Renderer>().sharedMaterial = material;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static DrawingTypeDefinition CreateDefinition(DrawingType type, string displayName, GameObject prefab, Texture2D mask, Vector3 spawnCenter)
        {
            string path = ScriptableObjectPath + "/DT_" + type + ".asset";
            DrawingTypeDefinition definition = AssetDatabase.LoadAssetAtPath<DrawingTypeDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<DrawingTypeDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            Assign(definition, "type", type);
            Assign(definition, "displayName", displayName);
            Assign(definition, "prefab", prefab);
            Assign(definition, "optionalMask", mask);
            Assign(definition, "cropRegion", new RectInt(100, 220, 990, 1280));
            Assign(definition, "textureProperty", "_BaseMap");
            Assign(definition, "spawnCenter", spawnCenter);
            Assign(definition, "spawnArea", new Vector2(1.2f, 1.2f));
            Assign(definition, "uniformScale", 1.025f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.8f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Spawn Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 3f);
        }

        private static GameObject CreateQuad(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.position = position;
            quad.transform.localScale = scale;
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad;
        }

        private static void Assign(Object target, string propertyName, object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Property not found: {target.name}.{propertyName}");
                return;
            }

            AssignProperty(property, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignProperty(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as Object;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value as string;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = (int)value;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = (RectInt)value;
                    break;
                default:
                    if (property.isArray && value is Object[] objects)
                    {
                        property.arraySize = objects.Length;
                        for (int i = 0; i < objects.Length; i++)
                        {
                            property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                        }
                    }
                    break;
            }
        }
    }
}
