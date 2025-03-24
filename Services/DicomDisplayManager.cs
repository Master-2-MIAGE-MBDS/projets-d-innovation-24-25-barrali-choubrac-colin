using System;
using System.Drawing;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Services
{
    /// <summary>
    /// Legacy manager for DICOM display functionality.
    /// Provides backward compatibility with existing forms.
    /// In new code, use DicomDisplayService instead.
    /// </summary>
    public class DicomDisplayManager : DisposableBase
    {
        #region Fields

        private readonly DicomReader _reader;
        private DicomMetadata[] _slices;
        private DicomMetadata _globalView;
        private int _currentSliceIndex;
        private int _windowWidth;
        private int _windowCenter;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the directory path of the DICOM files.
        /// </summary>
        public string DirectoryPath => _reader.DirectoryPath;

        /// <summary>
        /// Gets the DICOM slices.
        /// </summary>
        public DicomMetadata[] slices => _slices;

        /// <summary>
        /// Gets the global view metadata.
        /// </summary>
        public DicomMetadata globalView => _globalView;

        /// <summary>
        /// Gets or sets the current window width setting.
        /// </summary>
        public int windowWidth
        {
            get => _windowWidth;
            private set => _windowWidth = value;
        }

        /// <summary>
        /// Gets or sets the current window center setting.
        /// </summary>
        public int windowCenter
        {
            get => _windowCenter;
            private set => _windowCenter = value;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomDisplayManager instance.
        /// </summary>
        /// <param name="reader">The DICOM reader to use.</param>
        public DicomDisplayManager(DicomReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));

            // Initialize from reader
            _globalView = reader.GlobalView;
            _slices = reader.Slices ?? Array.Empty<DicomMetadata>();
            _currentSliceIndex = 0;

            // Initialize window settings
            if (_slices.Length > 0)
            {
                _windowWidth = _slices[0].WindowWidth;
                _windowCenter = _slices[0].WindowCenter;
            }

            Logger.Info($"DicomDisplayManager created with {_slices.Length} slices");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a specific slice metadata.
        /// </summary>
        /// <param name="sliceIndex">The index of the slice.</param>
        /// <returns>The metadata for the specified slice.</returns>
        public DicomMetadata GetSlice(int sliceIndex)
        {
            ThrowIfDisposed();

            if (_slices == null || sliceIndex < 0 || sliceIndex >= _slices.Length)
                throw new ArgumentOutOfRangeException(nameof(sliceIndex));

            return _slices[sliceIndex];
        }

        /// <summary>
        /// Gets the current slice image.
        /// </summary>
        /// <param name="windowWidth">Optional window width override.</param>
        /// <param name="windowCenter">Optional window center override.</param>
        /// <returns>A bitmap of the current slice.</returns>
        public Bitmap GetCurrentSliceImage(int windowWidth = -1, int windowCenter = -1)
        {
            ThrowIfDisposed();

            if (_slices == null || _currentSliceIndex < 0 || _currentSliceIndex >= _slices.Length)
                throw new InvalidOperationException("No current slice available");

            return DicomImageProcessor.ConvertToBitmap(
                _slices[_currentSliceIndex],
                windowWidth > 0 ? windowWidth : _windowWidth,
                windowCenter > 0 ? windowCenter : _windowCenter);
        }

        /// <summary>
        /// Gets the global view image.
        /// </summary>
        /// <returns>A bitmap of the global view.</returns>
        public Bitmap GetGlobalViewImage()
        {
            ThrowIfDisposed();

            if (_globalView == null)
                throw new InvalidOperationException("No global view available");

            return DicomImageProcessor.ConvertToBitmap(_globalView);
        }

        /// <summary>
        /// Sets the current slice index.
        /// </summary>
        /// <param name="index">The index to set.</param>
        public void SetSliceIndex(int index)
        {
            ThrowIfDisposed();

            if (_slices != null && index >= 0 && index < _slices.Length)
            {
                _currentSliceIndex = index;
            }
        }

        /// <summary>
        /// Gets the current slice index.
        /// </summary>
        /// <returns>The current slice index.</returns>
        public int GetCurrentSliceIndex() => _currentSliceIndex;

        /// <summary>
        /// Gets the total number of slices.
        /// </summary>
        /// <returns>The total number of slices.</returns>
        public int GetTotalSlices() => _slices?.Length ?? 0;

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // We don't dispose the _reader or the _slices array since they are owned by the caller

            // Set fields to null
            _slices = null;
            _globalView = null;

            base.DisposeManagedResources();
        }

        #endregion
    }
}