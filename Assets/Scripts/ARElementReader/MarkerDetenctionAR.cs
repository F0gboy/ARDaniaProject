#define USE_WEBCAM_IN_EDITOR

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class MarkerDetectionAR : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private Camera arCamera;

    [Header("Webcam (Editor Only)")]
    [SerializeField] private int webcamIndex = 0;
    private WebCamTexture webcam;

    [Header("UI Output (RawImage)")]
    [SerializeField] private RawImage originalImage;
    [SerializeField] private RawImage grayscaleImage;
    [SerializeField] private RawImage thresholdImage;
    [SerializeField] private RawImage contoursImage;
    [SerializeField] private RawImage warpedImage;
    [SerializeField] private RawImage gridImage;
    [SerializeField] private RawImage finalImage;

    [Header("Detection")]
    [SerializeField] private int warpSize = 300;
    [SerializeField] private double minContourArea = 6500;
    [SerializeField] private bool drawGridValues = true;

    private readonly Dictionary<RawImage, Texture2D> textures = new Dictionary<RawImage, Texture2D>();
    private NativeArray<byte> cameraNativeBuffer;
    private byte[] cameraBuffer;

    private Mat colorFrame;
    private Mat rgbaFrame;
    private Mat grayFrame;
    private Mat thresholdFrame;
    private Mat contoursFrame;
    private Mat warpedFrame;
    private Mat gridFrame;
    private Mat finalFrame;

    private PointF[] dstQuad;
    private List<MarkerPattern> markers;

    private void OnEnable()
    {
        warpedFrame = new Mat(warpSize, warpSize, DepthType.Cv8U, 1);
        gridFrame = new Mat(warpSize, warpSize, DepthType.Cv8U, 3);

        dstQuad = new[]
        {
            new PointF(0, 0),
            new PointF(warpSize - 1, 0),
            new PointF(warpSize - 1, warpSize - 1),
            new PointF(0, warpSize - 1)
        };

        markers = CreateMarkerPatterns();

#if UNITY_EDITOR && USE_WEBCAM_IN_EDITOR
        StartWebcam();
#else
        arCameraManager.frameReceived += OnCameraFrameReceived;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR && USE_WEBCAM_IN_EDITOR
        if (webcam != null && webcam.isPlaying)
            webcam.Stop();
#else
        arCameraManager.frameReceived -= OnCameraFrameReceived;
#endif

        if (cameraNativeBuffer.IsCreated)
            cameraNativeBuffer.Dispose();
    }

#if UNITY_EDITOR && USE_WEBCAM_IN_EDITOR
    private void StartWebcam()
    {
        if (originalImage == null)
            return;

        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
            return;

        webcam = new WebCamTexture(devices[webcamIndex].name, 1280, 720, 30);
        originalImage.texture = webcam;
        webcam.Play();
    }

    private void Update()
    {
        if (webcam == null || !webcam.didUpdateThisFrame)
            return;

        int width = webcam.width;
        int height = webcam.height;

        Color32[] pixels = webcam.GetPixels32();
        int length = pixels.Length;

        byte[] rgba = new byte[length * 4];

        for (int i = 0; i < length; i++)
        {
            rgba[i * 4 + 0] = pixels[i].r;
            rgba[i * 4 + 1] = pixels[i].g;
            rgba[i * 4 + 2] = pixels[i].b;
            rgba[i * 4 + 3] = pixels[i].a;
        }

        ProcessFrame(rgba, width, height);
    }
#endif

    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        using (image)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize(conversionParams);

            if (cameraNativeBuffer.IsCreated)
                cameraNativeBuffer.Dispose();

            cameraNativeBuffer = new NativeArray<byte>(size, Allocator.Persistent);
            cameraBuffer = new byte[size];

            image.Convert(conversionParams, cameraNativeBuffer);
            NativeArray<byte>.Copy(cameraNativeBuffer, cameraBuffer, size);

            ProcessFrame(cameraBuffer, image.width, image.height);
        }
    }

    private void ProcessFrame(byte[] rgbaData, int width, int height)
    {
        EnsureFrameMats(width, height);

        int byteLength = width * height * 4;
        Marshal.Copy(rgbaData, 0, rgbaFrame.DataPointer, byteLength);

        CvInvoke.CvtColor(rgbaFrame, colorFrame, ColorConversion.Rgba2Bgr);
        CvInvoke.CvtColor(colorFrame, grayFrame, ColorConversion.Bgr2Gray);
        CvInvoke.Threshold(grayFrame, thresholdFrame, 0, 255, ThresholdType.Otsu | ThresholdType.Binary);

        UpdateRawImage(colorFrame, originalImage);
        UpdateRawImage(grayFrame, grayscaleImage);
        UpdateRawImage(thresholdFrame, thresholdImage);

        DetectMarkers();
    }

    private void DetectMarkers()
    {
        using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
        using (Mat hierarchy = new Mat())
        using (VectorOfVectorOfPoint quadContours = new VectorOfVectorOfPoint())
        {
            CvInvoke.FindContours(thresholdFrame, contours, hierarchy, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            for (int i = 0; i < contours.Size; i++)
            {
                using (VectorOfPoint contour = contours[i])
                using (VectorOfPoint approx = new VectorOfPoint())
                {
                    CvInvoke.ApproxPolyDP(contour, approx, 4, true);

                    if (approx.Size != 4)
                        continue;

                    double area = CvInvoke.ContourArea(approx);
                    if (area < minContourArea)
                        continue;

                    quadContours.Push(approx);
                }
            }

            contoursFrame = colorFrame.Clone();
            CvInvoke.DrawContours(contoursFrame, quadContours, -1, new MCvScalar(100, 255, 255), 2);
            UpdateRawImage(contoursFrame, contoursImage);

            if (quadContours.Size == 0)
            {
                UpdateRawImage(colorFrame, finalImage);
                return;
            }

            PointF[] srcQuad = Array.ConvertAll(quadContours[0].ToArray(), p => new PointF(p.X, p.Y));
            srcQuad = OrderQuadPoints(srcQuad);

            using (Mat homography = CvInvoke.FindHomography(srcQuad, dstQuad, RobustEstimationAlgorithm.Ransac))
            {
                if (homography == null || homography.IsEmpty)
                    return;

                CvInvoke.WarpPerspective(grayFrame, warpedFrame, homography, new Size(warpSize, warpSize));
                CvInvoke.Threshold(warpedFrame, warpedFrame, 0, 255, ThresholdType.Otsu | ThresholdType.Binary);
                UpdateRawImage(warpedFrame, warpedImage);

                byte[,] grid = ExtractGridValues(warpedFrame, 6);
                byte[,] normalized = NormalizeGrid(grid, 128);

                BuildGridOverlay(warpedFrame, normalized, gridFrame, drawGridValues);
                UpdateRawImage(gridFrame, gridImage);

                finalFrame = colorFrame.Clone();

                if (TryMatchMarker(normalized, out int markerId, out int rotationDeg))
                {
                    CvInvoke.Polylines(finalFrame,
                        Array.ConvertAll(srcQuad, p => new System.Drawing.Point((int)p.X, (int)p.Y)),
                        true,
                        new MCvScalar(0, 255, 0),
                        2);

                    CvInvoke.PutText(finalFrame,
                        $"Marker {markerId} ({rotationDeg}°)",
                        new System.Drawing.Point((int)srcQuad[0].X, (int)srcQuad[0].Y - 10),
                        FontFace.HersheySimplex,
                        0.7,
                        new MCvScalar(255, 255, 255),
                        2);

                    Vector2 screenPos = new Vector2(
                        (srcQuad[0].X + srcQuad[1].X + srcQuad[2].X + srcQuad[3].X) / 4f,
                        (srcQuad[0].Y + srcQuad[1].Y + srcQuad[2].Y + srcQuad[3].Y) / 4f);

                    MarkerElementManager.Instance.RegisterMarker(markerId, rotationDeg, screenPos, srcQuad);
                }

                UpdateRawImage(finalFrame, finalImage);
            }
        }
    }

    private bool TryMatchMarker(byte[,] observed, out int markerId, out int rotationDeg)
    {
        markerId = -1;
        rotationDeg = 0;

        foreach (MarkerPattern pattern in markers)
        {
            for (int i = 0; i < pattern.Rotations.Count; i++)
            {
                if (GridEquals(observed, pattern.Rotations[i]))
                {
                    markerId = pattern.Id;
                    rotationDeg = i * 90;
                    return true;
                }
            }
        }

        return false;
    }

    private void EnsureFrameMats(int width, int height)
    {
        DisposeMat(ref colorFrame);
        DisposeMat(ref rgbaFrame);
        DisposeMat(ref grayFrame);
        DisposeMat(ref thresholdFrame);
        DisposeMat(ref contoursFrame);
        DisposeMat(ref finalFrame);

        rgbaFrame = new Mat(height, width, DepthType.Cv8U, 4);
        colorFrame = new Mat(height, width, DepthType.Cv8U, 3);
        grayFrame = new Mat(height, width, DepthType.Cv8U, 1);
        thresholdFrame = new Mat(height, width, DepthType.Cv8U, 1);
        contoursFrame = new Mat(height, width, DepthType.Cv8U, 3);
        finalFrame = new Mat(height, width, DepthType.Cv8U, 3);
    }

    private static void DisposeMat(ref Mat mat)
    {
        if (mat == null) return;
        mat.Dispose();
        mat = null;
    }

    private void UpdateRawImage(Mat source, RawImage target)
    {
        if (target == null || source == null || source.IsEmpty)
            return;

        using (Mat rgba = new Mat())
        {
            if (source.NumberOfChannels == 1)
                CvInvoke.CvtColor(source, rgba, ColorConversion.Gray2Rgba);
            else
                CvInvoke.CvtColor(source, rgba, ColorConversion.Bgr2Rgba);

            int width = rgba.Cols;
            int height = rgba.Rows;

            if (!textures.TryGetValue(target, out Texture2D texture) ||
                texture.width != width || texture.height != height)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Point;
                textures[target] = texture;
                target.texture = texture;
            }

            int byteLength = width * height * 4;
            byte[] data = new byte[byteLength];
            Marshal.Copy(rgba.DataPointer, data, 0, byteLength);
            texture.LoadRawTextureData(data);
            texture.Apply(false, false);
        }
    }

    private static PointF[] OrderQuadPoints(PointF[] points)
    {
        PointF center = new PointF(points.Average(p => p.X), points.Average(p => p.Y));
        PointF[] sorted = points
            .OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X))
            .ToArray();

        int topLeftIndex = 0;
        float bestScore = float.MaxValue;
        for (int i = 0; i < sorted.Length; i++)
        {
            float score = sorted[i].X + sorted[i].Y;
            if (score < bestScore)
            {
                bestScore = score;
                topLeftIndex = i;
            }
        }

        PointF[] ordered = new PointF[4];
        for (int i = 0; i < 4; i++)
            ordered[i] = sorted[(topLeftIndex + i) % 4];

        return ordered;
    }

    private static byte[,] ExtractGridValues(Mat binaryMarker, int gridSize)
    {
        int width = binaryMarker.Cols;
        int height = binaryMarker.Rows;
        int cellWidth = width / gridSize;
        int cellHeight = height / gridSize;
        byte[,] values = new byte[gridSize, gridSize];

        byte[] raw = new byte[height * (int)binaryMarker.Step];
        Marshal.Copy(binaryMarker.DataPointer, raw, 0, raw.Length);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int sampleX = x * cellWidth + cellWidth / 2;
                int sampleY = y * cellHeight + cellHeight / 2;
                int index = sampleY * (int)binaryMarker.Step + sampleX;
                values[y, x] = raw[index];
            }
        }

        return values;
    }

    private static byte[,] NormalizeGrid(byte[,] values, int threshold)
    {
        int rows = values.GetLength(0);
        int cols = values.GetLength(1);
        byte[,] normalized = new byte[rows, cols];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
                normalized[y, x] = values[y, x] >= threshold ? (byte)255 : (byte)0;
        }

        return normalized;
    }

    private static void BuildGridOverlay(Mat warpedBinary, byte[,] normalized, Mat output, bool drawValues)
    {
        CvInvoke.CvtColor(warpedBinary, output, ColorConversion.Gray2Bgr);

        int gridSize = normalized.GetLength(0);
        int cellWidth = output.Cols / gridSize;
        int cellHeight = output.Rows / gridSize;

        for (int y = 0; y <= gridSize; y++)
        {
            int yPos = y * cellHeight;
            CvInvoke.Line(output, new System.Drawing.Point(0, yPos),
                new System.Drawing.Point(output.Cols, yPos),
                new MCvScalar(0, 200, 0), 1);
        }

        for (int x = 0; x <= gridSize; x++)
        {
            int xPos = x * cellWidth;
            CvInvoke.Line(output, new System.Drawing.Point(xPos, 0),
                new System.Drawing.Point(xPos, output.Rows),
                new MCvScalar(0, 200, 0), 1);
        }

        if (!drawValues)
            return;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int cx = x * cellWidth + cellWidth / 2 - 6;
                int cy = y * cellHeight + cellHeight / 2 + 6;
                string value = normalized[y, x] == 255 ? "1" : "0";
                CvInvoke.PutText(output, value,
                    new System.Drawing.Point(cx, cy),
                    FontFace.HersheySimplex,
                    0.5,
                    new MCvScalar(255, 0, 0),
                    1);
            }
        }
    }

    private static bool GridEquals(byte[,] a, byte[,] b)
    {
        int rows = a.GetLength(0);
        int cols = a.GetLength(1);

        if (rows != b.GetLength(0) || cols != b.GetLength(1))
            return false;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (a[y, x] != b[y, x])
                    return false;
            }
        }

        return true;
    }

    private static List<MarkerPattern> CreateMarkerPatterns()
    {
        List<MarkerPattern> patterns = new List<MarkerPattern>();

        byte[,] marker1 =
        {
            { 0,0,0,0,0,0 },
            { 0,0,255,255,255,0 },
            { 0,255,0,0,255,0 },
            { 0,255,255,0,0,0 },
            { 0,255,255,0,255,0 },
            { 0,0,0,0,0,0 }
        };
        patterns.Add(new MarkerPattern(1, GenerateAllTransforms(marker1)));

        byte[,] marker2 =
        {
            {0,0,0,0,0,0},
            {0,255,0,255,255,0},
            {0,0,255,0,255,0},
            {0,0,0,255,255,0},
            {0,0,0,0,255,0},
            {0,0,0,0,0,0}
        };
        patterns.Add(new MarkerPattern(2, GenerateAllTransforms(marker2)));

        byte[,] marker3 =
        {
            {0,0,0,0,0,0},
            {0,0,0,0,0,0},
            {0,255,255,255,255,0},
            {0,255,0,0,255,0},
            {0,255,0,255,0,0},
            {0,0,0,0,0,0 }
        };
        patterns.Add(new MarkerPattern(3, GenerateAllTransforms(marker3)));

        byte[,] marker4 =
        {
            {0,0,0,0,0,0},
            {0,0,0,255,255,0},
            {0,0,0,255,255,0},
            {0,0,0,255,0,0},
            {0,255,255,0,255,0},
            {0,0,0,0,0,0}
        };
        patterns.Add(new MarkerPattern(4, GenerateAllTransforms(marker4)));

        return patterns;
    }

    private static List<byte[,]> GenerateAllTransforms(byte[,] baseMarker)
    {
        List<byte[,]> list = new List<byte[,]>();

        byte[,] r0 = baseMarker;
        byte[,] r90 = Rotate90(r0);
        byte[,] r180 = Rotate90(r90);
        byte[,] r270 = Rotate90(r180);

        list.Add(r0);
        list.Add(r90);
        list.Add(r180);
        list.Add(r270);

        list.Add(FlipHorizontal(r0));
        list.Add(FlipHorizontal(r90));
        list.Add(FlipHorizontal(r180));
        list.Add(FlipHorizontal(r270));

        list.Add(FlipVertical(r0));
        list.Add(FlipVertical(r90));
        list.Add(FlipVertical(r180));
        list.Add(FlipVertical(r270));

        return list;
    }

    private static byte[,] FlipHorizontal(byte[,] input)
    {
        int rows = input.GetLength(0);
        int cols = input.GetLength(1);
        byte[,] output = new byte[rows, cols];

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                output[y, x] = input[y, cols - 1 - x];

        return output;
    }

    private static byte[,] FlipVertical(byte[,] input)
    {
        int rows = input.GetLength(0);
        int cols = input.GetLength(1);
        byte[,] output = new byte[rows, cols];

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                output[y, x] = input[rows - 1 - y, x];

        return output;
    }

    private static byte[,] Rotate90(byte[,] input)
    {
        Matrix<byte> src = new Matrix<byte>(input);
        using (Mat rotatedMat = new Mat())
        {
            CvInvoke.Rotate(src.Mat, rotatedMat, RotateFlags.Rotate90Clockwise);
            Matrix<byte> rotated = new Matrix<byte>(rotatedMat.Rows, rotatedMat.Cols);
            rotatedMat.CopyTo(rotated);
            return rotated.Data;
        }
    }

    private sealed class MarkerPattern
    {
        public int Id { get; }
        public List<byte[,]> Rotations { get; }

        public MarkerPattern(int id, List<byte[,]> rotations)
        {
            Id = id;
            Rotations = rotations;
        }
    }
}
