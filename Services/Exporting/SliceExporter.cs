using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Services.Exporting
{
    /// <summary>
    /// Service for exporting DICOM slices to various image formats.
    /// </summary>
    public class SliceExporter
    {
        #region Export Methods

        /// <summary>
        /// Exports a slice image to a file with the specified format.
        /// </summary>
        /// <param name="image">The image to export.</param>
        /// <param name="filePath">The file path to save to.</param>
        /// <param name="format">The image format to use.</param>
        /// <returns>True if the export was successful.</returns>
        public bool ExportSlice(Bitmap image, string filePath, ImageFormat format)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "Image cannot be null");

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");

            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save the image with the specified format
                image.Save(filePath, format);
                Logger.Info($"Exported slice to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export slice to {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Exports multiple slice images to a directory.
        /// </summary>
        /// <param name="images">The images to export.</param>
        /// <param name="directory">The directory to save to.</param>
        /// <param name="baseName">The base name for the files.</param>
        /// <param name="format">The image format to use.</param>
        /// <returns>The number of successfully exported images.</returns>
        public async Task<int> ExportMultipleSlicesAsync(Bitmap[] images, string directory, string baseName, ImageFormat format)
        {
            if (images == null || images.Length == 0)
                throw new ArgumentNullException(nameof(images), "Images cannot be null or empty");

            if (string.IsNullOrEmpty(directory))
                throw new ArgumentNullException(nameof(directory), "Directory cannot be null or empty");

            // Create directory if it doesn't exist
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            int successCount = 0;

            // Determine file extension based on format
            string extension = GetFileExtension(format);

            await Task.Run(() =>
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] == null)
                        continue;

                    string fileName = $"{baseName}_{i:D4}.{extension}";
                    string filePath = Path.Combine(directory, fileName);

                    try
                    {
                        images[i].Save(filePath, format);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to export slice {i} to {filePath}", ex);
                    }
                }
            });

            Logger.Info($"Exported {successCount} of {images.Length} slices to {directory}");
            return successCount;
        }

        /// <summary>
        /// Exports a slice image to the standard location for a patient.
        /// </summary>
        /// <param name="image">The image to export.</param>
        /// <param name="patientDirectory">The patient directory.</param>
        /// <param name="sliceType">The type of the slice (e.g., "axial", "sagittal").</param>
        /// <returns>The path to the exported file, or null if export failed.</returns>
        public string ExportSliceToPatientDirectory(Bitmap image, string patientDirectory, string sliceType)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "Image cannot be null");

            if (string.IsNullOrEmpty(patientDirectory))
                throw new ArgumentNullException(nameof(patientDirectory), "Patient directory cannot be null or empty");

            try
            {
                // Create output directory structure
                string layersDir = Path.Combine(patientDirectory, "calque");
                if (!Directory.Exists(layersDir))
                {
                    Directory.CreateDirectory(layersDir);
                }

                // Find next available layer directory
                int layerNumber = 1;
                string layerSubDir;
                do
                {
                    layerSubDir = Path.Combine(layersDir, $"calque#{layerNumber}");
                    layerNumber++;
                } while (Directory.Exists(layerSubDir));

                // Create the subdirectory
                Directory.CreateDirectory(layerSubDir);

                // Generate filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"{sliceType}_slice_{timestamp}.png";
                string fullPath = Path.Combine(layerSubDir, filename);

                // Save the image
                image.Save(fullPath, ImageFormat.Png);
                Logger.Info($"Exported slice to {fullPath}");
                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export slice to patient directory {patientDirectory}", ex);
                return null;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the file extension for an image format.
        /// </summary>
        /// <param name="format">The image format.</param>
        /// <returns>The file extension (without dot).</returns>
        private string GetFileExtension(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Png))
                return "png";
            else if (format.Equals(ImageFormat.Jpeg))
                return "jpg";
            else if (format.Equals(ImageFormat.Bmp))
                return "bmp";
            else if (format.Equals(ImageFormat.Tiff))
                return "tiff";
            else
                return "png"; // Default to PNG
        }

        #endregion
    }
}