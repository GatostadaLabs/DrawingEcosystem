using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class TextureProcessor : MonoBehaviour
    {
        [SerializeField] private bool useMask = true;
        [SerializeField] private bool destroySourceAfterProcessing = true;
        [SerializeField] private FilterMode outputFilterMode = FilterMode.Bilinear;
        [SerializeField] private TextureWrapMode outputWrapMode = TextureWrapMode.Clamp;
        [SerializeField] private bool normalizePaperWhite = true;
        [SerializeField, Range(0.7f, 1f)] private float targetPaperWhite = 0.92f;
        [SerializeField, Range(0.75f, 0.99f)] private float paperWhitePercentile = 0.9f;
        [SerializeField, Range(1f, 3f)] private float maximumExposureGain = 1.8f;
        [SerializeField, Range(0.5f, 1.5f)] private float brightness = 1f;
        [SerializeField, Range(0.5f, 1.5f)] private float contrast = 1f;
        [SerializeField, Range(0f, 2f)] private float saturation = 1f;

        public float LastExposureGain { get; private set; } = 1f;

        public Texture2D Process(Texture2D source, DrawingTypeDefinition definition)
        {
            if (source == null || definition == null)
            {
                return null;
            }

            RectInt crop = ClampCrop(definition.CropRegion, source.width, source.height);
            if (crop.width <= 0 || crop.height <= 0)
            {
                return null;
            }

            Color[] pixels = source.GetPixels(crop.x, crop.y, crop.width, crop.height);
            LastExposureGain = normalizePaperWhite ? CalculateExposureGain(pixels) : 1f;
            ApplyColorCorrection(pixels, LastExposureGain);
            Texture2D mask = useMask ? definition.OptionalMask : null;

            if (mask != null)
            {
                ApplyMask(pixels, crop.width, crop.height, mask);
            }

            Texture2D output = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, true);
            output.name = $"Captured_{definition.Type}_{System.DateTime.Now:HHmmss}";
            output.filterMode = outputFilterMode;
            output.wrapMode = outputWrapMode;
            output.SetPixels(pixels);
            output.Apply(true, false);

            if (destroySourceAfterProcessing)
            {
                Destroy(source);
            }

            return output;
        }

        private void ApplyColorCorrection(Color[] pixels, float exposureGain)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                Vector3 color = new Vector3(pixel.r, pixel.g, pixel.b) * exposureGain;
                color = (color - Vector3.one * 0.5f) * contrast + Vector3.one * 0.5f;
                float luminance = color.x * 0.2126f + color.y * 0.7152f + color.z * 0.0722f;
                color = Vector3.Lerp(Vector3.one * luminance, color, saturation);
                color *= brightness;
                pixel.r = Mathf.Clamp01(color.x);
                pixel.g = Mathf.Clamp01(color.y);
                pixel.b = Mathf.Clamp01(color.z);
                pixels[i] = pixel;
            }
        }

        private float CalculateExposureGain(Color[] pixels)
        {
            const int histogramSize = 256;
            int[] histogram = new int[histogramSize];
            int sampleStep = Mathf.Max(1, pixels.Length / 100000);
            int sampleCount = 0;

            for (int i = 0; i < pixels.Length; i += sampleStep)
            {
                Color pixel = pixels[i];
                float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                int bin = Mathf.Clamp(Mathf.RoundToInt(luminance * (histogramSize - 1)), 0, histogramSize - 1);
                histogram[bin]++;
                sampleCount++;
            }

            int targetSample = Mathf.CeilToInt(sampleCount * paperWhitePercentile);
            int accumulated = 0;
            int whiteBin = histogramSize - 1;
            for (int i = 0; i < histogramSize; i++)
            {
                accumulated += histogram[i];
                if (accumulated >= targetSample)
                {
                    whiteBin = i;
                    break;
                }
            }

            float detectedPaperWhite = Mathf.Max(0.05f, whiteBin / (float)(histogramSize - 1));
            return Mathf.Clamp(targetPaperWhite / detectedPaperWhite, 0.75f, maximumExposureGain);
        }

        private static RectInt ClampCrop(RectInt crop, int sourceWidth, int sourceHeight)
        {
            int x = Mathf.Clamp(crop.x, 0, sourceWidth - 1);
            int y = Mathf.Clamp(crop.y, 0, sourceHeight - 1);
            int width = Mathf.Clamp(crop.width, 1, sourceWidth - x);
            int height = Mathf.Clamp(crop.height, 1, sourceHeight - y);
            return new RectInt(x, y, width, height);
        }

        private static void ApplyMask(Color[] pixels, int width, int height, Texture2D mask)
        {
            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0f : y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    Color maskColor = mask.GetPixelBilinear(u, v);
                    int index = y * width + x;
                    float alpha = maskColor.a * maskColor.grayscale;
                    pixels[index].a *= alpha;
                }
            }
        }
    }
}
