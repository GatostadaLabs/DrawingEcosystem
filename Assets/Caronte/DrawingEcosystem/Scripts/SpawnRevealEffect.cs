using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Caronte.DrawingEcosystem
{
    public sealed class SpawnRevealEffect : MonoBehaviour
    {
        private Renderer[] targetRenderers;
        private Material[][] originalMaterials;
        private readonly List<Material> revealMaterials = new List<Material>();
        private readonly List<Material> visualMaterials = new List<Material>();
        private Bounds originalBounds;
        private GameObject visualRoot;
        private LineRenderer energyRing;
        private Texture2D particleTexture;
        private MaterialPropertyBlock propertyBlock;
        private Coroutine revealRoutine;
        private bool materialsRestored;
        private Vector3 scanDirection;
        private Vector3 ringRight;
        private Vector3 ringUp;
        private Vector3 originalLocalScale;
        private bool scaleInitialized;

        public void Play(Color effectColor, float duration, Shader customRevealShader = null)
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            targetRenderers = GetRevealRenderers();
            originalBounds = CalculateBounds(targetRenderers, transform.position);
            propertyBlock = new MaterialPropertyBlock();
            ConfigureScanAxis();
            ConfigurePresentationAxes();
            originalLocalScale = transform.localScale;
            scaleInitialized = true;
            transform.localScale = originalLocalScale * 0.88f;
            PrepareRevealMaterials(effectColor, customRevealShader);
            CreateSpawnVisuals(effectColor, duration);
            revealRoutine = StartCoroutine(Reveal(effectColor, Mathf.Max(0.1f, duration)));
        }

        private IEnumerator Reveal(Color effectColor, float duration)
        {
            GetProjectionRange(originalBounds, scanDirection, out float minimumProjection, out float maximumProjection);
            float revealSize = maximumProjection - minimumProjection;
            float edgeWidth = Mathf.Max(0.015f, revealSize * 0.055f);
            float startPosition = minimumProjection - edgeWidth;
            float endPosition = maximumProjection + edgeWidth;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float scanTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
                float revealPosition = Mathf.Lerp(startPosition, endPosition, scanTime);
                UpdateRevealProperties(effectColor, revealPosition, edgeWidth);
                UpdatePresentation(normalizedTime, effectColor);

                yield return null;
            }

            transform.localScale = originalLocalScale;
            RestoreOriginalMaterials();
            DestroyVisuals();
            revealRoutine = null;
            Destroy(this);
        }

        private void PrepareRevealMaterials(Color effectColor, Shader customRevealShader = null)
        {
            Shader revealShader = customRevealShader != null ? customRevealShader : Shader.Find("Caronte/Digital Reveal");
            if (revealShader == null)
            {
                Debug.LogError("No se encontró el shader Caronte/Digital Reveal.", this);
                return;
            }

            originalMaterials = new Material[targetRenderers.Length][];
            for (int rendererIndex = 0; rendererIndex < targetRenderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = targetRenderers[rendererIndex];
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] sourceMaterials = targetRenderer.sharedMaterials;
                originalMaterials[rendererIndex] = sourceMaterials;
                Material[] temporaryMaterials = new Material[sourceMaterials.Length];
                for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                {
                    Material sourceMaterial = sourceMaterials[materialIndex];
                    Material revealMaterial = new Material(revealShader)
                    {
                        name = $"Digital Reveal ({sourceMaterial?.name ?? "Material"})"
                    };

                    Texture baseTexture = GetBaseTexture(sourceMaterial);
                    if (baseTexture != null)
                    {
                        revealMaterial.SetTexture("_BaseMap", baseTexture);
                    }

                    revealMaterial.SetColor("_BaseColor", GetBaseColor(sourceMaterial));
                    revealMaterial.SetColor("_RevealColor", effectColor);
                    temporaryMaterials[materialIndex] = revealMaterial;
                    revealMaterials.Add(revealMaterial);
                }

                targetRenderer.sharedMaterials = temporaryMaterials;
            }
        }

        private void UpdateRevealProperties(Color effectColor, float revealPosition, float edgeWidth)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_RevealColor", effectColor);
                propertyBlock.SetFloat("_RevealPosition", revealPosition);
                propertyBlock.SetVector("_RevealDirection", scanDirection);
                propertyBlock.SetFloat("_RevealEdgeWidth", edgeWidth);
                propertyBlock.SetFloat("_RevealSoftness", edgeWidth * 0.18f);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void CreateSpawnVisuals(Color effectColor, float duration)
        {
            visualRoot = new GameObject($"BioScan_VFX_{gameObject.name}");
            visualRoot.transform.position = originalBounds.center;
            particleTexture = CreateSoftParticleTexture();

            Material particleMaterial = CreateAdditiveMaterial(
                "BioScan Particle Material",
                effectColor,
                "Universal Render Pipeline/Particles/Unlit");
            ApplyParticleTexture(particleMaterial, particleTexture);
            visualMaterials.Add(particleMaterial);

            CreateSparkBurst(effectColor, duration, particleMaterial);
            CreateRisingMotes(effectColor, duration, particleMaterial);
            CreateFinalFlash(effectColor, duration, particleMaterial);
            CreateEnergyRing(effectColor);
        }

        private void CreateSparkBurst(Color effectColor, float duration, Material material)
        {
            GameObject sparkObject = new GameObject("Spark Burst");
            sparkObject.transform.SetParent(visualRoot.transform, false);
            ParticleSystem particles = sparkObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, duration);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Clamp(duration * 0.24f, 0.18f, 0.55f),
                Mathf.Clamp(duration * 0.55f, 0.45f, 1.2f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.08f, originalBounds.extents.magnitude * 0.25f),
                Mathf.Max(0.15f, originalBounds.extents.magnitude * 0.75f));
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.01f, originalBounds.extents.magnitude * 0.025f),
                Mathf.Max(0.025f, originalBounds.extents.magnitude * 0.075f));
            main.startColor = effectColor;
            main.maxParticles = 96;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.05f, originalBounds.size.x * 0.72f),
                Mathf.Max(0.05f, originalBounds.size.y * 0.72f),
                Mathf.Max(0.02f, originalBounds.size.z * 0.72f));
            shape.randomDirectionAmount = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(effectColor, 0f),
                    new GradientColorKey(Color.white, 0.45f),
                    new GradientColorKey(effectColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.7f, 0.55f),
                new Keyframe(1f, 0f)));

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = material;
            particleRenderer.sortingOrder = 10;
            particles.Play();
        }

        private void CreateRisingMotes(Color effectColor, float duration, Material material)
        {
            GameObject moteObject = new GameObject("Rising Motes");
            moteObject.transform.SetParent(visualRoot.transform, false);
            ParticleSystem particles = moteObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float effectSize = Mathf.Max(0.1f, originalBounds.extents.magnitude);
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, duration);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.55f, duration * 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, effectSize * 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(effectSize * 0.018f, effectSize * 0.05f);
            main.startColor = new Color(effectColor.r, effectColor.g, effectColor.b, 0.8f);
            main.maxParticles = 48;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(duration * 0.08f, 24) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.05f, originalBounds.size.x * 0.9f),
                Mathf.Max(0.05f, originalBounds.size.y * 0.45f),
                Mathf.Max(0.02f, originalBounds.size.z * 0.9f));
            shape.position = -scanDirection * originalBounds.extents.magnitude * 0.3f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = scanDirection.x * effectSize * 0.35f;
            velocity.y = scanDirection.y * effectSize * 0.35f;
            velocity.z = scanDirection.z * effectSize * 0.35f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(effectColor, 0.3f),
                    new GradientColorKey(effectColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = material;
            particleRenderer.sortingOrder = 9;
            particles.Play();
        }

        private void CreateFinalFlash(Color effectColor, float duration, Material material)
        {
            GameObject flashObject = new GameObject("Completion Flash");
            flashObject.transform.SetParent(visualRoot.transform, false);
            ParticleSystem particles = flashObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float effectSize = Mathf.Max(0.1f, originalBounds.extents.magnitude);
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, duration);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Mathf.Clamp(duration * 0.22f, 0.16f, 0.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(effectSize * 0.65f, effectSize * 1.15f);
            main.startColor = new Color(effectColor.r, effectColor.g, effectColor.b, 0.7f);
            main.maxParticles = 8;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(duration * 0.72f, 3) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = effectSize * 0.08f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(effectColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.65f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.35f)));

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = material;
            particleRenderer.sortingOrder = 8;
            particles.Play();
        }

        private void CreateEnergyRing(Color effectColor)
        {
            GameObject ringObject = new GameObject("Energy Ring");
            ringObject.transform.SetParent(visualRoot.transform, false);
            energyRing = ringObject.AddComponent<LineRenderer>();
            energyRing.useWorldSpace = true;
            energyRing.loop = true;
            energyRing.positionCount = 64;
            energyRing.numCornerVertices = 2;
            energyRing.numCapVertices = 2;
            energyRing.textureMode = LineTextureMode.Stretch;
            energyRing.alignment = LineAlignment.View;

            Material ringMaterial = CreateAdditiveMaterial(
                "BioScan Energy Ring Material",
                effectColor,
                "Universal Render Pipeline/Unlit");
            visualMaterials.Add(ringMaterial);
            energyRing.sharedMaterial = ringMaterial;
            energyRing.sortingOrder = 7;
            UpdateEnergyRing(0f, effectColor);
        }

        private void UpdatePresentation(float normalizedTime, Color effectColor)
        {
            float scaleProgress = Mathf.Clamp01(normalizedTime / 0.72f);
            float easedScale = EaseOutBack(scaleProgress);
            transform.localScale = Vector3.LerpUnclamped(originalLocalScale * 0.88f, originalLocalScale, easedScale);
            UpdateEnergyRing(normalizedTime, effectColor);
        }

        private void UpdateEnergyRing(float normalizedTime, Color effectColor)
        {
            if (energyRing == null)
            {
                return;
            }

            float ringProgress = Mathf.Clamp01(normalizedTime / 0.82f);
            float fade = 1f - Mathf.SmoothStep(0.35f, 1f, ringProgress);
            float effectSize = Mathf.Max(0.1f, originalBounds.extents.magnitude);
            float radius = Mathf.Lerp(effectSize * 0.12f, effectSize * 1.18f, Mathf.SmoothStep(0f, 1f, ringProgress));
            float width = Mathf.Lerp(effectSize * 0.065f, effectSize * 0.012f, ringProgress);
            Color ringColor = new Color(effectColor.r, effectColor.g, effectColor.b, fade * 0.85f);
            energyRing.startColor = ringColor;
            energyRing.endColor = ringColor;
            energyRing.startWidth = width;
            energyRing.endWidth = width;

            for (int i = 0; i < energyRing.positionCount; i++)
            {
                float angle = i / (float)energyRing.positionCount * Mathf.PI * 2f;
                Vector3 offset = (ringRight * Mathf.Cos(angle) + ringUp * Mathf.Sin(angle)) * radius;
                energyRing.SetPosition(i, originalBounds.center + offset);
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (materialsRestored || originalMaterials == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null && originalMaterials[i] != null)
                {
                    targetRenderers[i].sharedMaterials = originalMaterials[i];
                }
            }

            for (int i = 0; i < revealMaterials.Count; i++)
            {
                if (revealMaterials[i] != null)
                {
                    Destroy(revealMaterials[i]);
                }
            }

            revealMaterials.Clear();
            materialsRestored = true;
        }

        private static Texture GetBaseTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        private static Color GetBaseColor(Material material)
        {
            if (material != null && material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material != null && material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private Renderer[] GetRevealRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            List<Renderer> filteredRenderers = new List<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (targetRenderer is MeshRenderer || targetRenderer is SkinnedMeshRenderer)
                {
                    filteredRenderers.Add(targetRenderer);
                }
            }

            return filteredRenderers.ToArray();
        }

        private static Bounds CalculateBounds(Renderer[] renderers, Vector3 fallbackCenter)
        {
            Bounds bounds = new Bounds(fallbackCenter, Vector3.one * 0.5f);
            bool foundRenderer = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!foundRenderer)
                {
                    bounds = renderers[i].bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private void ConfigureScanAxis()
        {
            Camera targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (targetCamera != null)
            {
                scanDirection = targetCamera.transform.up.normalized;
            }
            else
            {
                scanDirection = Vector3.up;
            }
        }

        private void ConfigurePresentationAxes()
        {
            Camera targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (targetCamera != null)
            {
                ringRight = targetCamera.transform.right.normalized;
                ringUp = targetCamera.transform.up.normalized;
                return;
            }

            ringRight = transform.right.normalized;
            ringUp = transform.up.normalized;
        }

        private static void GetProjectionRange(Bounds bounds, Vector3 direction, out float minimum, out float maximum)
        {
            float center = Vector3.Dot(bounds.center, direction);
            Vector3 extents = bounds.extents;
            float projectedExtent = Mathf.Abs(direction.x) * extents.x +
                                    Mathf.Abs(direction.y) * extents.y +
                                    Mathf.Abs(direction.z) * extents.z;
            minimum = center - projectedExtent;
            maximum = center + projectedExtent;
        }

        private static Material CreateAdditiveMaterial(string materialName, Color color, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private static void ApplyParticleTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static Texture2D CreateSoftParticleTexture()
        {
            const int textureSize = 32;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "BioScan Soft Particle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[textureSize * textureSize];
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float inverseRadius = 1f / center.x;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) * inverseRadius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.35f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private void DestroyVisuals()
        {
            if (visualRoot != null)
            {
                Destroy(visualRoot);
                visualRoot = null;
                energyRing = null;
            }

            for (int i = 0; i < visualMaterials.Count; i++)
            {
                if (visualMaterials[i] != null)
                {
                    Destroy(visualMaterials[i]);
                }
            }

            visualMaterials.Clear();

            if (particleTexture != null)
            {
                Destroy(particleTexture);
                particleTexture = null;
            }
        }

        private void OnDestroy()
        {
            if (scaleInitialized)
            {
                transform.localScale = originalLocalScale;
            }

            RestoreOriginalMaterials();
            DestroyVisuals();
        }
    }
}
