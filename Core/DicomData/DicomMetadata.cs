using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Element;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Core.DicomData
{
    /// <summary>
    /// Represents essential metadata from a DICOM object with optimized memory usage.
    /// Only stores data that changes between slices to reduce memory footprint.
    /// </summary>
    public class DicomMetadata : DisposableBase
    {
        #region Properties

        // Common properties (shared across all slices in a series)
        public string PatientID { get; private set; }
        public string PatientName { get; private set; }
        public string PatientSex { get; private set; }
        public string Modality { get; private set; }
        public int Series { get; private set; }
        public string SeriesTime { get; private set; }
        public int Rows { get; private set; }
        public int Columns { get; private set; }
        public double PixelSpacing { get; private set; }
        public int BitsAllocated { get; private set; }
        public int BitsStored { get; private set; }
        public int HighBit { get; private set; }
        public int PixelRepresentation { get; private set; }
        public double RescaleIntercept { get; private set; }
        public double RescaleSlope { get; private set; }

        // Per-slice properties (these can vary between slices)
        public int WindowCenter { get; private set; }
        public int WindowWidth { get; private set; }
        public double SliceThickness { get; private set; }
        public double SliceLocation { get; private set; }
        public string ContentTime { get; private set; }

        // Pixel data - can be large, so we manage disposal explicitly
        public List<byte> PixelData { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomMetadata instance from a DICOMObject.
        /// </summary>
        /// <param name="dicomObject">The DICOM object to extract metadata from.</param>
        public DicomMetadata(DICOMObject dicomObject)
        {
            if (dicomObject == null)
                throw new ArgumentNullException(nameof(dicomObject), "DICOM object cannot be null");

            try
            {
                ExtractMetadata(dicomObject);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to extract DICOM metadata", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new DicomMetadata instance that shares common properties with another instance.
        /// </summary>
        /// <param name="commonMetadata">The source of common metadata.</param>
        /// <param name="dicomObject">The DICOM object to extract slice-specific metadata from.</param>
        public DicomMetadata(DicomMetadata commonMetadata, DICOMObject dicomObject)
        {
            if (commonMetadata == null)
                throw new ArgumentNullException(nameof(commonMetadata), "Common metadata cannot be null");
            if (dicomObject == null)
                throw new ArgumentNullException(nameof(dicomObject), "DICOM object cannot be null");

            // Copy common properties
            PatientID = commonMetadata.PatientID;
            PatientName = commonMetadata.PatientName;
            PatientSex = commonMetadata.PatientSex;
            Modality = commonMetadata.Modality;
            Series = commonMetadata.Series;
            SeriesTime = commonMetadata.SeriesTime;
            Rows = commonMetadata.Rows;
            Columns = commonMetadata.Columns;
            PixelSpacing = commonMetadata.PixelSpacing;
            BitsAllocated = commonMetadata.BitsAllocated;
            BitsStored = commonMetadata.BitsStored;
            HighBit = commonMetadata.HighBit;
            PixelRepresentation = commonMetadata.PixelRepresentation;
            RescaleIntercept = commonMetadata.RescaleIntercept;
            RescaleSlope = commonMetadata.RescaleSlope;

            // Extract only slice-specific metadata
            try
            {
                ExtractSliceSpecificMetadata(dicomObject);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to extract slice-specific DICOM metadata", ex);
                throw;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Extracts all metadata from a DICOM object.
        /// </summary>
        /// <param name="dicomObject">The DICOM object to extract metadata from.</param>
        private void ExtractMetadata(DICOMObject dicomObject)
        {
            // Extract patient information
            PatientID = dicomObject.FindFirst(TagHelper.PatientID)?.DData.ToString();
            PatientName = dicomObject.FindFirst(TagHelper.PatientName)?.DData.ToString();
            PatientSex = dicomObject.FindFirst(TagHelper.PatientSex)?.DData.ToString();

            // Extract study/series information
            Modality = dicomObject.FindFirst(TagHelper.Modality)?.DData.ToString();
            Series = Convert.ToInt32(dicomObject.FindFirst(TagHelper.SeriesNumber)?.DData ?? 0);
            SeriesTime = dicomObject.FindFirst(TagHelper.SeriesTime)?.DData.ToString();
            ContentTime = dicomObject.FindFirst(TagHelper.ContentTime)?.DData.ToString();

            // Extract image properties
            Rows = Convert.ToInt32(dicomObject.FindFirst(TagHelper.Rows)?.DData ?? 0);
            Columns = Convert.ToInt32(dicomObject.FindFirst(TagHelper.Columns)?.DData ?? 0);
            WindowCenter = Convert.ToInt32(dicomObject.FindFirst(TagHelper.WindowCenter)?.DData ?? 0);
            WindowWidth = Convert.ToInt32(dicomObject.FindFirst(TagHelper.WindowWidth)?.DData ?? 0);
            SliceThickness = Convert.ToDouble(dicomObject.FindFirst(TagHelper.SliceThickness)?.DData ?? 0);
            SliceLocation = Convert.ToDouble(dicomObject.FindFirst(TagHelper.SliceLocation)?.DData ?? 0);

            // Extract pixel spacing (first value of pair)
            var spacingValues = dicomObject.FindFirst(TagHelper.PixelSpacing)?.DData.ToString();
            if (!string.IsNullOrEmpty(spacingValues))
            {
                try
                {
                    PixelSpacing = spacingValues.Split('\\').Select(double.Parse).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing pixel spacing: {ex.Message}");
                    PixelSpacing = 1.0; // Default value
                }
            }

            // Extract technical properties
            BitsAllocated = Convert.ToInt32(dicomObject.FindFirst(TagHelper.BitsAllocated)?.DData ?? 0);
            BitsStored = Convert.ToInt32(dicomObject.FindFirst(TagHelper.BitsStored)?.DData ?? 0);
            HighBit = Convert.ToInt32(dicomObject.FindFirst(TagHelper.HighBit)?.DData ?? 0);
            PixelRepresentation = Convert.ToInt32(dicomObject.FindFirst(TagHelper.PixelRepresentation)?.DData ?? 0);
            RescaleIntercept = Convert.ToDouble(dicomObject.FindFirst(TagHelper.RescaleIntercept)?.DData ?? 0);
            RescaleSlope = Convert.ToDouble(dicomObject.FindFirst(TagHelper.RescaleSlope)?.DData ?? 0);

            // Extract pixel data
            var pixelDataElement = dicomObject.FindFirst(TagHelper.PixelData);
            if (pixelDataElement?.DData_ != null)
            {
                PixelData = (List<byte>)pixelDataElement.DData_;
            }
            else
            {
                PixelData = new List<byte>();
                Debug.WriteLine("Warning: No pixel data found in DICOM object");
            }
        }

        /// <summary>
        /// Extracts only slice-specific metadata from a DICOM object.
        /// </summary>
        /// <param name="dicomObject">The DICOM object to extract metadata from.</param>
        private void ExtractSliceSpecificMetadata(DICOMObject dicomObject)
        {
            // Extract only properties that can vary between slices
            WindowCenter = Convert.ToInt32(dicomObject.FindFirst(TagHelper.WindowCenter)?.DData ?? 0);
            WindowWidth = Convert.ToInt32(dicomObject.FindFirst(TagHelper.WindowWidth)?.DData ?? 0);
            SliceThickness = Convert.ToDouble(dicomObject.FindFirst(TagHelper.SliceThickness)?.DData ?? 0);
            SliceLocation = Convert.ToDouble(dicomObject.FindFirst(TagHelper.SliceLocation)?.DData ?? 0);
            ContentTime = dicomObject.FindFirst(TagHelper.ContentTime)?.DData.ToString();

            // Extract pixel data
            var pixelDataElement = dicomObject.FindFirst(TagHelper.PixelData);
            if (pixelDataElement?.DData_ != null)
            {
                PixelData = (List<byte>)pixelDataElement.DData_;
            }
            else
            {
                PixelData = new List<byte>();
                Debug.WriteLine("Warning: No pixel data found in DICOM object");
            }
        }

        /// <summary>
        /// Prints information about this DICOM metadata to the console.
        /// </summary>
        public void PrintInfo()
        {
            Logger.Info($"--- DICOM Metadata Info ---");
            Logger.Info($"Patient: {PatientName} (ID: {PatientID}, Sex: {PatientSex})");
            Logger.Info($"Series: {Series}, Time: {SeriesTime}, Modality: {Modality}");
            Logger.Info($"Dimensions: {Rows}x{Columns}, Spacing: {PixelSpacing}mm");
            Logger.Info($"Window: Center={WindowCenter}, Width={WindowWidth}");
            Logger.Info($"Slice: Thickness={SliceThickness}mm, Location={SliceLocation}mm");
            Logger.Info($"Pixel Data Size: {PixelData?.Count ?? 0} bytes");
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Clear the pixel data to free memory
            if (PixelData != null)
            {
                PixelData.Clear();
                PixelData = null;
            }

            // Clear other reference type fields
            PatientID = null;
            PatientName = null;
            PatientSex = null;
            Modality = null;
            SeriesTime = null;
            ContentTime = null;

            base.DisposeManagedResources();
        }

        #endregion
    }
}