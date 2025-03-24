using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Core.DicomData
{
    /// <summary>
    /// Handles loading and reading DICOM files from a directory.
    /// </summary>
    public class DicomReader : DisposableBase
    {
        #region Fields

        private readonly string _directoryPath;
        private DicomMetadata[] _slices;
        private DicomMetadata _globalView;
        private bool _loaded = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the directory path containing the DICOM files.
        /// </summary>
        public string DirectoryPath => _directoryPath;

        /// <summary>
        /// Gets the array of DICOM slices loaded from the directory.
        /// </summary>
        public DicomMetadata[] Slices => _slices;

        /// <summary>
        /// Gets the global view DICOM metadata.
        /// </summary>
        public DicomMetadata GlobalView => _globalView;

        /// <summary>
        /// Gets whether DICOM files have been loaded.
        /// </summary>
        public bool IsLoaded => _loaded;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomReader for the specified directory.
        /// </summary>
        /// <param name="directoryPath">The directory path containing DICOM files.</param>
        public DicomReader(string directoryPath)
        {
            _directoryPath = !string.IsNullOrEmpty(directoryPath)
                ? directoryPath
                : throw new ArgumentNullException(nameof(directoryPath), "Directory path cannot be null or empty");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads only the global view DICOM file from the directory.
        /// </summary>
        public void LoadGlobalView()
        {
            ThrowIfDisposed();

            try
            {
                var dicomFiles = GetValidatedDicomFiles();
                if (dicomFiles.Length == 0)
                    return;

                var firstDicom = DICOMObject.Read(dicomFiles[0]);
                _globalView = new DicomMetadata(firstDicom);

                Logger.Info($"Loaded global view from {dicomFiles[0]}", "DicomReader");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load global view", ex, "DicomReader");
                throw;
            }
        }

        /// <summary>
        /// Loads all DICOM files from the directory.
        /// </summary>
        public void LoadAllFiles()
        {
            ThrowIfDisposed();

            if (_loaded && _slices != null && _slices.Length > 0)
            {
                Logger.Info("DICOM files already loaded, skipping", "DicomReader");
                return;
            }

            try
            {
                var dicomFiles = GetValidatedDicomFiles();
                if (dicomFiles.Length == 0)
                    return;

                Logger.Info($"Found {dicomFiles.Length} DICOM files in {_directoryPath}", "DicomReader");

                // Load the first file as the global view
                var firstDicom = DICOMObject.Read(dicomFiles[0]);
                _globalView = new DicomMetadata(firstDicom);

                // Get the series number from the first file
                var seriesNumber = firstDicom.FindFirst(TagHelper.SeriesNumber)?.DData.ToString();

                // Load the rest of the files
                var slicesList = new List<DicomMetadata>();

                foreach (var file in dicomFiles)
                {
                    try
                    {
                        var dicomObject = DICOMObject.Read(file);

                        // Verify that this file belongs to the same series
                        var currentSeriesNumber = dicomObject.FindFirst(TagHelper.SeriesNumber)?.DData.ToString();
                        if (currentSeriesNumber != seriesNumber)
                        {
                            Logger.Warning($"Skipping file {file} - belongs to different series", "DicomReader");
                            continue;
                        }

                        // Create metadata with common properties shared from global view
                        var metadata = new DicomMetadata(_globalView, dicomObject);
                        slicesList.Add(metadata);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load DICOM file {file}", ex, "DicomReader");
                    }
                }

                // Sort slices by location
                _slices = slicesList.OrderBy(slice => slice.SliceLocation).ToArray();
                _loaded = true;

                Logger.Info($"Loaded {_slices.Length} DICOM slices", "DicomReader");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load DICOM files", ex, "DicomReader");
                throw;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets validated DICOM files from the directory.
        /// </summary>
        /// <returns>An array of DICOM file paths.</returns>
        private string[] GetValidatedDicomFiles()
        {
            if (!Directory.Exists(_directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {_directoryPath}");

            var dicomFiles = Directory.GetFiles(_directoryPath, "*.dcm");
            if (dicomFiles.Length == 0)
                throw new FileNotFoundException($"No DICOM files found in directory: {_directoryPath}");

            return dicomFiles;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Dispose of DICOM metadata objects
            if (_slices != null)
            {
                foreach (var slice in _slices)
                {
                    slice?.Dispose();
                }
                _slices = null;
            }

            _globalView?.Dispose();
            _globalView = null;

            _loaded = false;

            base.DisposeManagedResources();
        }

        #endregion
    }
}