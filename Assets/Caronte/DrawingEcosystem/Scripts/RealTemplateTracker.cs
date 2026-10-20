using System;
using System.Collections.Generic;
using UnityEngine;
using ZXing;
using ZXing.Common;

namespace Caronte.DrawingEcosystem
{
    public sealed class RealTemplateTracker : MonoBehaviour, ITrackedTemplateDetector
    {
        private const int ReferenceWidth = 1190;
        private const int ReferenceHeight = 1683;
        private const float MarkerLeft = 102f / ReferenceWidth;
        private const float MarkerRight = 1088f / ReferenceWidth;
        private const float MarkerTop = 102f / ReferenceHeight;
        private const float MarkerBottom = 1581f / ReferenceHeight;

        private static readonly string[] MarkerPatterns =
        {
            "111111100001101101101101111101111111",
            "111111111101111101101011110001111111",
            "111111111111100011101001110001111111",
            "111111111011101011111111100001111111"
        };

        [SerializeField] private DrawingCaptureService captureService;
        [SerializeField, Range(320, 960)] private int analysisWidth = 640;
        [SerializeField, Range(0.05f, 0.5f)] private float analysisInterval = 0.12f;
        [SerializeField, Range(32, 220)] private int darkThreshold = 115;
        [SerializeField, Range(6, 48)] private int minimumMarkerPixels = 12;
        [SerializeField, Range(32, 180)] private int maximumMarkerPixels = 110;
        [SerializeField, Range(0.05f, 0.5f)] private float maximumPatternError = 0.28f;
        [SerializeField, Range(0.05f, 1f)] private float trackingSmoothing = 0.55f;
        [SerializeField, Range(0.5f, 12f)] private float stablePixelTolerance = 3f;
        [SerializeField, Range(1, 12)] private int requiredStableFrames = 2;
        [SerializeField, Range(0.2f, 2f)] private float lostTrackingDelay = 0.7f;
        [SerializeField] private int rectifiedWidth = ReferenceWidth;
        [SerializeField] private int rectifiedHeight = ReferenceHeight;

        private readonly Vector2[] trackedMarkers = new Vector2[4];
        private readonly Vector2[] detectedMarkers = new Vector2[4];
        private readonly bool[] detectedMarkerFlags = new bool[4];
        private readonly float[] detectedMarkerScores = new float[4];
        private readonly double[] homography = new double[8];
        private readonly MultiFormatReader qrReader = new MultiFormatReader();
        private readonly IDictionary<DecodeHintType, object> qrHints = new Dictionary<DecodeHintType, object>
        {
            { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
            { DecodeHintType.TRY_HARDER, true }
        };

        private Color32[] sourcePixels;
        private bool[] darkPixels;
        private bool[] visitedPixels;
        private int[] floodQueue;
        private byte[] qrPixels;
        private int analysisHeight;
        private float nextAnalysisTime;
        private float lastTrackingTime;
        private float nextQrReadTime;
        private int stableFrames;
        private bool hasTrackedMarkers;
        private bool hasRecognizedTemplate;
        private bool usingEstimatedMarker;
        private DrawingType currentType;
        private string trackingStatus = "Buscando marcadores";

        public event Action<DrawingType> TemplateChanged;

        public DrawingType CurrentType => currentType;
        public bool HasValidTracking => hasTrackedMarkers && hasRecognizedTemplate && stableFrames >= requiredStableFrames;
        public float TrackingStability => Mathf.Clamp01(stableFrames / (float)Mathf.Max(1, requiredStableFrames));
        public string TrackingStatus => trackingStatus;

        private void Update()
        {
            if (captureService == null || !captureService.IsFrameReady)
            {
                UpdateLostTracking();
                return;
            }

            if (Time.unscaledTime < nextAnalysisTime)
            {
                UpdateLostTracking();
                return;
            }

            nextAnalysisTime = Time.unscaledTime + analysisInterval;
            AnalyzeFrame(captureService.ActiveWebcam);
            UpdateLostTracking();
        }

        public Texture2D CaptureRectifiedFrame()
        {
            if (!HasValidTracking || captureService == null || captureService.ActiveWebcam == null)
            {
                return null;
            }

            WebCamTexture webcam = captureService.ActiveWebcam;
            if (!TryReadSourcePixels(webcam) || !BuildHomography(webcam.width, webcam.height))
            {
                return null;
            }

            int outputWidth = Mathf.Max(64, rectifiedWidth);
            int outputHeight = Mathf.Max(64, rectifiedHeight);
            Color32[] outputPixels = new Color32[outputWidth * outputHeight];
            Color32 white = new Color32(255, 255, 255, 255);

            for (int rawY = 0; rawY < outputHeight; rawY++)
            {
                double canonicalY = outputHeight - 1 - rawY;
                for (int x = 0; x < outputWidth; x++)
                {
                    if (TryTransform(homography, x, canonicalY, out double sourceX, out double sourceY))
                    {
                        outputPixels[rawY * outputWidth + x] = SampleBilinear(sourcePixels, webcam.width, webcam.height, sourceX, sourceY, white);
                    }
                    else
                    {
                        outputPixels[rawY * outputWidth + x] = white;
                    }
                }
            }

            captureService.ApplyColorTemperature(outputPixels);

            Texture2D output = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            output.name = $"Rectified_{currentType}_{DateTime.Now:HHmmss}";
            output.filterMode = FilterMode.Bilinear;
            output.wrapMode = TextureWrapMode.Clamp;
            output.SetPixels32(outputPixels);
            output.Apply(false, false);
            return output;
        }

        public bool AnalyzeStillImage(Texture2D image)
        {
            if (image == null || !image.isReadable)
            {
                return false;
            }

            sourcePixels = image.GetPixels32();
            AnalyzePixels(image.width, image.height);
            return HasValidTracking;
        }

        private void AnalyzeFrame(WebCamTexture webcam)
        {
            if (!TryReadSourcePixels(webcam))
            {
                return;
            }

            AnalyzePixels(webcam.width, webcam.height);
        }

        private void AnalyzePixels(int sourceWidth, int sourceHeight)
        {

            int width = Mathf.Min(analysisWidth, sourceWidth);
            analysisHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * (width / (float)sourceWidth)));
            EnsureAnalysisBuffers(width, analysisHeight);
            BuildDarkImage(sourcePixels, sourceWidth, sourceHeight, width, analysisHeight);

            Array.Clear(visitedPixels, 0, visitedPixels.Length);
            Array.Clear(detectedMarkerFlags, 0, detectedMarkerFlags.Length);
            for (int i = 0; i < detectedMarkerScores.Length; i++)
            {
                detectedMarkerScores[i] = float.MaxValue;
            }

            FindMarkerComponents(width, analysisHeight);
            int detectedCount = CountDetectedMarkers();
            usingEstimatedMarker = false;
            if (detectedCount == 3 && TryEstimateMissingMarker(width, analysisHeight))
            {
                usingEstimatedMarker = true;
                detectedCount = 4;
            }

            for (int i = 0; i < detectedMarkerFlags.Length; i++)
            {
                if (!detectedMarkerFlags[i])
                {
                    trackingStatus = $"Falta marcador {GetMarkerLabel(i)} ({detectedCount}/4)";
                    return;
                }
            }

            float maximumMotion = 0f;
            for (int i = 0; i < trackedMarkers.Length; i++)
            {
                Vector2 normalized = new Vector2(detectedMarkers[i].x / width, detectedMarkers[i].y / analysisHeight);
                if (hasTrackedMarkers)
                {
                    Vector2 previousPixels = new Vector2(trackedMarkers[i].x * width, trackedMarkers[i].y * analysisHeight);
                    maximumMotion = Mathf.Max(maximumMotion, Vector2.Distance(previousPixels, detectedMarkers[i]));
                    trackedMarkers[i] = Vector2.Lerp(trackedMarkers[i], normalized, trackingSmoothing);
                }
                else
                {
                    trackedMarkers[i] = normalized;
                }
            }

            if (hasTrackedMarkers && maximumMotion <= stablePixelTolerance)
            {
                stableFrames++;
            }
            else
            {
                stableFrames = Mathf.Max(0, stableFrames - 1);
            }
            hasTrackedMarkers = true;
            lastTrackingTime = Time.unscaledTime;

            if (stableFrames < requiredStableFrames)
            {
                trackingStatus = $"Estabilizando hoja {stableFrames}/{requiredStableFrames}";
                return;
            }

            if (Time.unscaledTime >= nextQrReadTime)
            {
                nextQrReadTime = Time.unscaledTime + 0.35f;
                TryRecognizeQr(sourceWidth, sourceHeight);
            }

            trackingStatus = hasRecognizedTemplate
                ? $"Tracking estable{(usingEstimatedMarker ? " (3+1 estimado)" : string.Empty)}: {currentType.ToSpanishLabel()}"
                : "Hoja estable, QR no reconocido";
        }

        private bool TryReadSourcePixels(WebCamTexture webcam)
        {
            if (webcam == null || !webcam.isPlaying || webcam.width <= 16 || webcam.height <= 16)
            {
                return false;
            }

            sourcePixels = webcam.GetPixels32(sourcePixels);
            return sourcePixels != null && sourcePixels.Length == webcam.width * webcam.height;
        }

        private void EnsureAnalysisBuffers(int width, int height)
        {
            int length = width * height;
            if (darkPixels == null || darkPixels.Length != length)
            {
                darkPixels = new bool[length];
                visitedPixels = new bool[length];
                floodQueue = new int[length];
            }
        }

        private void BuildDarkImage(Color32[] pixels, int sourceWidth, int sourceHeight, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sourceTopY = Mathf.Min(sourceHeight - 1, y * sourceHeight / height);
                int sourceRawY = sourceHeight - 1 - sourceTopY;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(sourceWidth - 1, x * sourceWidth / width);
                    Color32 color = pixels[sourceRawY * sourceWidth + sourceX];
                    int luminance = (color.r * 77 + color.g * 150 + color.b * 29) >> 8;
                    darkPixels[y * width + x] = luminance <= darkThreshold;
                }
            }
        }

        private void FindMarkerComponents(int width, int height)
        {
            for (int index = 0; index < darkPixels.Length; index++)
            {
                if (!darkPixels[index] || visitedPixels[index])
                {
                    continue;
                }

                int head = 0;
                int tail = 0;
                floodQueue[tail++] = index;
                visitedPixels[index] = true;
                int minX = index % width;
                int maxX = minX;
                int minY = index / width;
                int maxY = minY;
                int count = 0;

                while (head < tail)
                {
                    int current = floodQueue[head++];
                    int x = current % width;
                    int y = current / width;
                    count++;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);

                    TryQueuePixel(x - 1, y, width, height, ref tail);
                    TryQueuePixel(x + 1, y, width, height, ref tail);
                    TryQueuePixel(x, y - 1, width, height, ref tail);
                    TryQueuePixel(x, y + 1, width, height, ref tail);
                }

                int componentWidth = maxX - minX + 1;
                int componentHeight = maxY - minY + 1;
                if (componentWidth < minimumMarkerPixels || componentHeight < minimumMarkerPixels ||
                    componentWidth > maximumMarkerPixels || componentHeight > maximumMarkerPixels)
                {
                    continue;
                }

                float aspect = componentWidth / (float)componentHeight;
                float fill = count / (float)(componentWidth * componentHeight);
                if (aspect < 0.72f || aspect > 1.38f || fill < 0.28f || fill > 0.92f)
                {
                    continue;
                }

                if (TryIdentifyMarker(minX, minY, componentWidth, componentHeight, width, out int markerIndex, out float score) &&
                    score < detectedMarkerScores[markerIndex])
                {
                    detectedMarkerScores[markerIndex] = score;
                    detectedMarkerFlags[markerIndex] = true;
                    detectedMarkers[markerIndex] = new Vector2(minX + componentWidth * 0.5f, minY + componentHeight * 0.5f);
                }
            }
        }

        private void TryQueuePixel(int x, int y, int width, int height, ref int tail)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (!darkPixels[index] || visitedPixels[index])
            {
                return;
            }

            visitedPixels[index] = true;
            floodQueue[tail++] = index;
        }

        private bool TryIdentifyMarker(int minX, int minY, int width, int height, int imageWidth, out int markerIndex, out float score)
        {
            bool[] observed = new bool[36];
            for (int gridY = 0; gridY < 6; gridY++)
            {
                int cellMinY = minY + gridY * height / 6;
                int cellMaxY = minY + (gridY + 1) * height / 6;
                for (int gridX = 0; gridX < 6; gridX++)
                {
                    int cellMinX = minX + gridX * width / 6;
                    int cellMaxX = minX + (gridX + 1) * width / 6;
                    int darkCount = 0;
                    int pixelCount = 0;
                    for (int y = cellMinY; y < cellMaxY; y++)
                    {
                        for (int x = cellMinX; x < cellMaxX; x++)
                        {
                            pixelCount++;
                            if (darkPixels[y * imageWidth + x])
                            {
                                darkCount++;
                            }
                        }
                    }

                    observed[gridY * 6 + gridX] = pixelCount > 0 && darkCount >= pixelCount * 0.5f;
                }
            }

            markerIndex = -1;
            int bestErrors = int.MaxValue;
            int secondBestErrors = int.MaxValue;
            for (int patternIndex = 0; patternIndex < MarkerPatterns.Length; patternIndex++)
            {
                int patternBestErrors = int.MaxValue;
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    int errors = CountPatternErrors(observed, MarkerPatterns[patternIndex], rotation);
                    patternBestErrors = Mathf.Min(patternBestErrors, errors);
                }

                if (patternBestErrors < bestErrors)
                {
                    secondBestErrors = bestErrors;
                    bestErrors = patternBestErrors;
                    markerIndex = patternIndex;
                }
                else if (patternBestErrors < secondBestErrors)
                {
                    secondBestErrors = patternBestErrors;
                }
            }

            score = bestErrors / 36f;
            return score <= maximumPatternError && secondBestErrors - bestErrors >= 1;
        }

        private bool TryEstimateMissingMarker(int width, int height)
        {
            int missingIndex = -1;
            for (int i = 0; i < detectedMarkerFlags.Length; i++)
            {
                if (!detectedMarkerFlags[i])
                {
                    missingIndex = i;
                    break;
                }
            }

            Vector2 estimated;
            switch (missingIndex)
            {
                case 0:
                    estimated = detectedMarkers[1] + detectedMarkers[2] - detectedMarkers[3];
                    break;
                case 1:
                    estimated = detectedMarkers[0] + detectedMarkers[3] - detectedMarkers[2];
                    break;
                case 2:
                    estimated = detectedMarkers[0] + detectedMarkers[3] - detectedMarkers[1];
                    break;
                case 3:
                    estimated = detectedMarkers[1] + detectedMarkers[2] - detectedMarkers[0];
                    break;
                default:
                    return false;
            }

            if (estimated.x < 0f || estimated.x >= width || estimated.y < 0f || estimated.y >= height)
            {
                return false;
            }

            detectedMarkers[missingIndex] = estimated;
            detectedMarkerFlags[missingIndex] = true;
            return true;
        }

        private static string GetMarkerLabel(int markerIndex)
        {
            switch (markerIndex)
            {
                case 0:
                    return "superior izquierdo";
                case 1:
                    return "superior derecho";
                case 2:
                    return "inferior izquierdo";
                case 3:
                    return "inferior derecho";
                default:
                    return "desconocido";
            }
        }

        private static int CountPatternErrors(bool[] observed, string expected, int rotation)
        {
            int errors = 0;
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    int observedX;
                    int observedY;
                    switch (rotation)
                    {
                        case 1:
                            observedX = y;
                            observedY = 5 - x;
                            break;
                        case 2:
                            observedX = 5 - x;
                            observedY = 5 - y;
                            break;
                        case 3:
                            observedX = 5 - y;
                            observedY = x;
                            break;
                        default:
                            observedX = x;
                            observedY = y;
                            break;
                    }

                    bool expectedDark = expected[y * 6 + x] == '1';
                    if (observed[observedY * 6 + observedX] != expectedDark)
                    {
                        errors++;
                    }
                }
            }

            return errors;
        }

        private int CountDetectedMarkers()
        {
            int count = 0;
            for (int i = 0; i < detectedMarkerFlags.Length; i++)
            {
                if (detectedMarkerFlags[i])
                {
                    count++;
                }
            }

            return count;
        }

        private void TryRecognizeQr(int sourceWidth, int sourceHeight)
        {
            if (!BuildHomography(sourceWidth, sourceHeight))
            {
                return;
            }

            const int qrSize = 192;
            if (qrPixels == null || qrPixels.Length != qrSize * qrSize)
            {
                qrPixels = new byte[qrSize * qrSize];
            }

            double minX = rectifiedWidth * (515d / ReferenceWidth);
            double maxX = rectifiedWidth * (675d / ReferenceWidth);
            double minY = rectifiedHeight * (1490d / ReferenceHeight);
            double maxY = rectifiedHeight * (1650d / ReferenceHeight);

            for (int y = 0; y < qrSize; y++)
            {
                double canonicalY = Mathf.Lerp((float)minY, (float)maxY, (y + 0.5f) / qrSize);
                for (int x = 0; x < qrSize; x++)
                {
                    double canonicalX = Mathf.Lerp((float)minX, (float)maxX, (x + 0.5f) / qrSize);
                    byte luminance = 255;
                    if (TryTransform(homography, canonicalX, canonicalY, out double sourceX, out double sourceY))
                    {
                        Color32 color = SampleBilinear(sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY, new Color32(255, 255, 255, 255));
                        luminance = (byte)((color.r * 77 + color.g * 150 + color.b * 29) >> 8);
                    }

                    qrPixels[y * qrSize + x] = luminance;
                }
            }

            try
            {
                RGBLuminanceSource source = new RGBLuminanceSource(qrPixels, qrSize, qrSize, RGBLuminanceSource.BitmapFormat.Gray8);
                BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
                Result result = qrReader.decode(bitmap, qrHints);
                qrReader.reset();
                if (result != null && TryMapQr(result.Text, out DrawingType detectedType))
                {
                    bool changed = !hasRecognizedTemplate || currentType != detectedType;
                    currentType = detectedType;
                    hasRecognizedTemplate = true;
                    if (changed)
                    {
                        TemplateChanged?.Invoke(currentType);
                    }
                }
            }
            catch (ReaderException)
            {
                qrReader.reset();
            }
        }

        private static bool TryMapQr(string value, out DrawingType type)
        {
            switch (value != null ? value.Trim() : string.Empty)
            {
                case "1":
                    type = DrawingType.Bacteria;
                    return true;
                case "2":
                    type = DrawingType.Fungus;
                    return true;
                case "3":
                    type = DrawingType.Mycorrhiza;
                    return true;
                case "4":
                    type = DrawingType.Nematode;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        private bool BuildHomography(int sourceWidth, int sourceHeight)
        {
            if (!hasTrackedMarkers)
            {
                return false;
            }

            Vector2[] destination =
            {
                new Vector2(MarkerLeft * rectifiedWidth, MarkerTop * rectifiedHeight),
                new Vector2(MarkerRight * rectifiedWidth, MarkerTop * rectifiedHeight),
                new Vector2(MarkerLeft * rectifiedWidth, MarkerBottom * rectifiedHeight),
                new Vector2(MarkerRight * rectifiedWidth, MarkerBottom * rectifiedHeight)
            };
            Vector2[] source =
            {
                new Vector2(trackedMarkers[0].x * sourceWidth, trackedMarkers[0].y * sourceHeight),
                new Vector2(trackedMarkers[1].x * sourceWidth, trackedMarkers[1].y * sourceHeight),
                new Vector2(trackedMarkers[2].x * sourceWidth, trackedMarkers[2].y * sourceHeight),
                new Vector2(trackedMarkers[3].x * sourceWidth, trackedMarkers[3].y * sourceHeight)
            };
            return SolveHomography(destination, source, homography);
        }

        private static bool SolveHomography(Vector2[] from, Vector2[] to, double[] result)
        {
            double[,] matrix = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double x = from[i].x;
                double y = from[i].y;
                double targetX = to[i].x;
                double targetY = to[i].y;
                int row = i * 2;

                matrix[row, 0] = x;
                matrix[row, 1] = y;
                matrix[row, 2] = 1d;
                matrix[row, 6] = -targetX * x;
                matrix[row, 7] = -targetX * y;
                matrix[row, 8] = targetX;

                matrix[row + 1, 3] = x;
                matrix[row + 1, 4] = y;
                matrix[row + 1, 5] = 1d;
                matrix[row + 1, 6] = -targetY * x;
                matrix[row + 1, 7] = -targetY * y;
                matrix[row + 1, 8] = targetY;
            }

            for (int column = 0; column < 8; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < 8; row++)
                {
                    if (Math.Abs(matrix[row, column]) > Math.Abs(matrix[pivot, column]))
                    {
                        pivot = row;
                    }
                }

                if (Math.Abs(matrix[pivot, column]) < 0.0000001d)
                {
                    return false;
                }

                if (pivot != column)
                {
                    for (int valueColumn = column; valueColumn < 9; valueColumn++)
                    {
                        double temporary = matrix[column, valueColumn];
                        matrix[column, valueColumn] = matrix[pivot, valueColumn];
                        matrix[pivot, valueColumn] = temporary;
                    }
                }

                double divisor = matrix[column, column];
                for (int valueColumn = column; valueColumn < 9; valueColumn++)
                {
                    matrix[column, valueColumn] /= divisor;
                }

                for (int row = 0; row < 8; row++)
                {
                    if (row == column)
                    {
                        continue;
                    }

                    double factor = matrix[row, column];
                    for (int valueColumn = column; valueColumn < 9; valueColumn++)
                    {
                        matrix[row, valueColumn] -= factor * matrix[column, valueColumn];
                    }
                }
            }

            for (int i = 0; i < 8; i++)
            {
                result[i] = matrix[i, 8];
            }

            return true;
        }

        private static bool TryTransform(double[] transform, double x, double y, out double transformedX, out double transformedY)
        {
            double denominator = transform[6] * x + transform[7] * y + 1d;
            if (Math.Abs(denominator) < 0.0000001d)
            {
                transformedX = 0d;
                transformedY = 0d;
                return false;
            }

            transformedX = (transform[0] * x + transform[1] * y + transform[2]) / denominator;
            transformedY = (transform[3] * x + transform[4] * y + transform[5]) / denominator;
            return true;
        }

        private static Color32 SampleBilinear(Color32[] pixels, int width, int height, double topX, double topY, Color32 fallback)
        {
            if (topX < 0d || topY < 0d || topX > width - 1d || topY > height - 1d)
            {
                return fallback;
            }

            double rawY = height - 1d - topY;
            int x0 = Mathf.Clamp((int)Math.Floor(topX), 0, width - 1);
            int y0 = Mathf.Clamp((int)Math.Floor(rawY), 0, height - 1);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = (float)(topX - x0);
            float ty = (float)(rawY - y0);
            Color first = Color.Lerp(pixels[y0 * width + x0], pixels[y0 * width + x1], tx);
            Color second = Color.Lerp(pixels[y1 * width + x0], pixels[y1 * width + x1], tx);
            return Color.Lerp(first, second, ty);
        }

        private void UpdateLostTracking()
        {
            if (!hasTrackedMarkers || Time.unscaledTime - lastTrackingTime <= lostTrackingDelay)
            {
                return;
            }

            hasTrackedMarkers = false;
            hasRecognizedTemplate = false;
            stableFrames = 0;
            trackingStatus = "Buscando marcadores";
        }
    }
}
