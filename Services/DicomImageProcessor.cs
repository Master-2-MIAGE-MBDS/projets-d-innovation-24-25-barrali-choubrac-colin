using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Services
{
    /// <summary>
    /// Service responsible for processing DICOM images and converting them to standard image formats.
    /// </summary>
    public class DicomImageProcessor : DisposableBase
    {
        #region Public Methods

        /// <summary>
        /// Converts a DICOM slice to a bitmap image.
        /// </summary>
        /// <param name="metadata">The DICOM metadata containing pixel data.</param>
        /// <param name="windowWidth">Optional window width for contrast adjustment (uses metadata value if -1).</param>
        /// <param name="windowCenter">Optional window center for contrast adjustment (uses metadata value if -1).</param>
        /// <returns>A bitmap image representing the DICOM slice.</returns>
        public static Bitmap ConvertToBitmap(DicomMetadata metadata, int windowWidth = -1, int windowCenter = -1)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata), "DICOM metadata cannot be null");

            if (metadata.PixelData == null || metadata.PixelData.Count == 0)
                throw new InvalidOperationException("No pixel data available");

            // Use metadata values if not specified
            if (windowWidth == -1)
                windowWidth = metadata.WindowWidth;

            if (windowCenter == -1)
                windowCenter = metadata.WindowCenter;

            Logger.Info($"Converting DICOM to bitmap: {metadata.Columns}x{metadata.Rows}, " +
                       $"Window: {windowWidth}/{windowCenter}");

            // Create output buffer for RGBA pixels
            var pixelDataArray = metadata.PixelData.ToArray();
            var outputData = new byte[metadata.Rows * metadata.Columns * 4];

            // Calculate mask for bit representation
            var mask = (ushort)(ushort.MaxValue >> (metadata.BitsAllocated - metadata.BitsStored));
            var maxValue = Math.Pow(2, metadata.BitsStored);
            var windowHalf = ((windowWidth - 1) / 2.0) - 0.5;

            // Process each pixel
            int outputIndex = 0;
            for (int i = 0; i < pixelDataArray.Length; i += 2)
            {
                // Combine two bytes into a 16-bit pixel value (little-endian)
                ushort pixelValue = (ushort)((pixelDataArray[i]) | (pixelDataArray[i + 1] << 8));
                double hounsfield = pixelValue & mask;

                // Handle signed pixel values
                if (metadata.PixelRepresentation == 1 && hounsfield > (maxValue / 2))
                {
                    hounsfield = hounsfield - maxValue;
                }

                // Apply rescale slope and intercept to convert to Hounsfield units
                hounsfield = metadata.RescaleSlope * hounsfield + metadata.RescaleIntercept;

                // Apply windowing function to map Hounsfield units to display values
                byte intensity;
                if (hounsfield <= windowCenter - windowHalf)
                    intensity = 0;
                else if (hounsfield >= windowCenter + windowHalf)
                    intensity = 255;
                else
                    intensity = (byte)(((hounsfield - (windowCenter - 0.5)) / (windowWidth - 1) + 0.5) * 255);

                // Set RGBA values (grayscale with full alpha)
                outputData[outputIndex++] = intensity;  // B
                outputData[outputIndex++] = intensity;  // G
                outputData[outputIndex++] = intensity;  // R
                outputData[outputIndex++] = 255;        // A (fully opaque)
            }

            // Create and populate the bitmap
            var bitmap = new Bitmap(metadata.Columns, metadata.Rows, PixelFormat.Format32bppArgb);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, metadata.Columns, metadata.Rows),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(outputData, 0, bitmapData.Scan0, outputData.Length);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        /// <summary>
        /// Optimizes window and level settings for a DICOM image based on its histogram.
        /// </summary>
        /// <param name="image">The DICOM image to analyze.</param>
        /// <param name="lowPercentile">The low percentile cutoff (default 0.05).</param>
        /// <param name="highPercentile">The high percentile cutoff (default 0.95).</param>
        /// <returns>A tuple containing the optimized window width and window center.</returns>
        public static (int width, int center) OptimizeWindowSettings(Bitmap image, double lowPercentile = 0.05, double highPercentile = 0.95)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "Image cannot be null");

            try
            {
                // Build histogram
                int[] histogram = new int[256];
                int totalPixels = 0;

                using (var bitmap = new Bitmap(image))
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            int intensity = (pixel.R + pixel.G + pixel.B) / 3;

                            if (intensity > 0) // Ignore completely black pixels
                            {
                                histogram[intensity]++;
                                totalPixels++;
                            }
                        }
                    }
                }

                if (totalPixels == 0)
                    return (400, 40); // Default values

                // Find percentile thresholds
                int lowPixelCount = (int)(totalPixels * lowPercentile);
                int highPixelCount = (int)(totalPixels * highPercentile);
                int pixelSum = 0;

                int minIntensity = 0;
                int maxIntensity = 255;

                // Find low threshold
                for (int i = 0; i < histogram.Length; i++)
                {
                    pixelSum += histogram[i];
                    if (pixelSum >= lowPixelCount)
                    {
                        minIntensity = i;
                        break;
                    }
                }

                // Reset and find high threshold
                pixelSum = 0;
                for (int i = histogram.Length - 1; i >= 0; i--)
                {
                    pixelSum += histogram[i];
                    if (pixelSum >= (totalPixels - highPixelCount))
                    {
                        maxIntensity = i;
                        break;
                    }
                }

                // Calculate window parameters
                int windowWidth = maxIntensity - minIntensity;
                int windowCenter = minIntensity + (windowWidth / 2);

                // Apply constraints for reasonable values
                windowWidth = Math.Max(50, Math.Min(4000, windowWidth));
                windowCenter = Math.Max(0, Math.Min(800, windowCenter));

                Logger.Info($"Optimized window settings: Width={windowWidth}, Center={windowCenter}");
                return (windowWidth, windowCenter);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to optimize window settings", ex);
                return (400, 40); // Default values on error
            }
        }

        /// <summary>
        /// Applies a filter to a DICOM bitmap for enhanced visualization.
        /// </summary>
        /// <param name="image">The image to process.</param>
        /// <param name="filterType">The type of filter to apply.</param>
        /// <returns>A new bitmap with the filter applied.</returns>
        public static Bitmap ApplyFilter(Bitmap image, FilterType filterType)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "Image cannot be null");

            Bitmap result = new Bitmap(image.Width, image.Height);

            try
            {
                // Common operations for all filter types
                BitmapData sourceData = null;
                BitmapData resultData = null;

                try
                {
                    unsafe
                    {
                        sourceData = image.LockBits(
                            new Rectangle(0, 0, image.Width, image.Height),
                            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                        resultData = result.LockBits(
                            new Rectangle(0, 0, result.Width, result.Height),
                            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        byte* srcPtr = (byte*)sourceData.Scan0;
                        byte* destPtr = (byte*)resultData.Scan0;

                        switch (filterType)
                        {
                            case FilterType.Edge:
                                ApplyEdgeDetectionFilter(srcPtr, destPtr, image.Width, image.Height, sourceData.Stride, resultData.Stride);
                                break;

                            case FilterType.Sharpen:
                                ApplySharpenFilter(srcPtr, destPtr, image.Width, image.Height, sourceData.Stride, resultData.Stride);
                                break;

                            case FilterType.Smooth:
                                ApplySmoothFilter(srcPtr, destPtr, image.Width, image.Height, sourceData.Stride, resultData.Stride);
                                break;

                            case FilterType.None:
                            default:
                                // Simple copy
                                int size = Math.Abs(sourceData.Stride) * image.Height;
                                Buffer.MemoryCopy(srcPtr, destPtr, size, size);
                                break;
                        }
                    }
                }
                finally
                {
                    if (sourceData != null)
                        image.UnlockBits(sourceData);

                    if (resultData != null)
                        result.UnlockBits(resultData);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply {filterType} filter", ex);
                result.Dispose();
                return new Bitmap(image); // Return a copy of the original on error
            }
        }

        #endregion

        #region Private Helper Methods

        private static unsafe void ApplyEdgeDetectionFilter(byte* srcPtr, byte* destPtr, int width, int height, int srcStride, int destStride)
        {
            // Sobel edge detection filter
            int[] xKernel = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] yKernel = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            // Process each pixel except border
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int sumX = 0;
                    int sumY = 0;
                    int kernelIndex = 0;

                    // Apply kernel
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int pixelPos = (y + ky) * srcStride + (x + kx) * 4;
                            int intensity = (srcPtr[pixelPos] + srcPtr[pixelPos + 1] + srcPtr[pixelPos + 2]) / 3;

                            sumX += intensity * xKernel[kernelIndex];
                            sumY += intensity * yKernel[kernelIndex];
                            kernelIndex++;
                        }
                    }

                    // Calculate gradient magnitude
                    double magnitude = Math.Sqrt(sumX * sumX + sumY * sumY);
                    byte edge = (byte)Math.Min(255, magnitude);

                    // Set result pixel
                    int destPos = y * destStride + x * 4;
                    destPtr[destPos] = edge;     // B
                    destPtr[destPos + 1] = edge; // G
                    destPtr[destPos + 2] = edge; // R
                    destPtr[destPos + 3] = 255;  // A
                }
            }

            // Clear border pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        int destPos = y * destStride + x * 4;
                        destPtr[destPos] = 0;      // B
                        destPtr[destPos + 1] = 0;  // G
                        destPtr[destPos + 2] = 0;  // R
                        destPtr[destPos + 3] = 255;// A
                    }
                }
            }
        }

        private static unsafe void ApplySharpenFilter(byte* srcPtr, byte* destPtr, int width, int height, int srcStride, int destStride)
        {
            // Sharpen kernel
            int[] kernel = { 0, -1, 0, -1, 5, -1, 0, -1, 0 };

            // Process each pixel except border
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int sumR = 0;
                    int sumG = 0;
                    int sumB = 0;
                    int kernelIndex = 0;

                    // Apply kernel
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int pixelPos = (y + ky) * srcStride + (x + kx) * 4;
                            sumB += srcPtr[pixelPos] * kernel[kernelIndex];
                            sumG += srcPtr[pixelPos + 1] * kernel[kernelIndex];
                            sumR += srcPtr[pixelPos + 2] * kernel[kernelIndex];
                            kernelIndex++;
                        }
                    }

                    // Clamp values
                    byte valueB = (byte)Math.Max(0, Math.Min(255, sumB));
                    byte valueG = (byte)Math.Max(0, Math.Min(255, sumG));
                    byte valueR = (byte)Math.Max(0, Math.Min(255, sumR));

                    // Set result pixel
                    int destPos = y * destStride + x * 4;
                    destPtr[destPos] = valueB;     // B
                    destPtr[destPos + 1] = valueG; // G
                    destPtr[destPos + 2] = valueR; // R
                    destPtr[destPos + 3] = 255;    // A
                }
            }

            // Copy border pixels directly
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        int srcPos = y * srcStride + x * 4;
                        int destPos = y * destStride + x * 4;
                        destPtr[destPos] = srcPtr[srcPos];         // B
                        destPtr[destPos + 1] = srcPtr[srcPos + 1]; // G
                        destPtr[destPos + 2] = srcPtr[srcPos + 2]; // R
                        destPtr[destPos + 3] = 255;                // A
                    }
                }
            }
        }

        private static unsafe void ApplySmoothFilter(byte* srcPtr, byte* destPtr, int width, int height, int srcStride, int destStride)
        {
            // Gaussian blur kernel
            double[] kernel = { 0.0625, 0.125, 0.0625, 0.125, 0.25, 0.125, 0.0625, 0.125, 0.0625 };

            // Process each pixel except border
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double sumR = 0;
                    double sumG = 0;
                    double sumB = 0;
                    int kernelIndex = 0;

                    // Apply kernel
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int pixelPos = (y + ky) * srcStride + (x + kx) * 4;
                            sumB += srcPtr[pixelPos] * kernel[kernelIndex];
                            sumG += srcPtr[pixelPos + 1] * kernel[kernelIndex];
                            sumR += srcPtr[pixelPos + 2] * kernel[kernelIndex];
                            kernelIndex++;
                        }
                    }

                    // Set result pixel
                    int destPos = y * destStride + x * 4;
                    destPtr[destPos] = (byte)sumB;     // B
                    destPtr[destPos + 1] = (byte)sumG; // G
                    destPtr[destPos + 2] = (byte)sumR; // R
                    destPtr[destPos + 3] = 255;        // A
                }
            }

            // Copy border pixels directly
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        int srcPos = y * srcStride + x * 4;
                        int destPos = y * destStride + x * 4;
                        destPtr[destPos] = srcPtr[srcPos];         // B
                        destPtr[destPos + 1] = srcPtr[srcPos + 1]; // G
                        destPtr[destPos + 2] = srcPtr[srcPos + 2]; // R
                        destPtr[destPos + 3] = 255;                // A
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Types of image filters that can be applied to DICOM images.
    /// </summary>
    public enum FilterType
    {
        /// <summary>No filter, original image.</summary>
        None,

        /// <summary>Edge detection filter to highlight boundaries.</summary>
        Edge,

        /// <summary>Sharpen filter to enhance details.</summary>
        Sharpen,

        /// <summary>Smooth filter to reduce noise.</summary>
        Smooth
    }
}