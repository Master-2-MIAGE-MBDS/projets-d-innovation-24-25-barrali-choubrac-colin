using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.Services
{
    /// <summary>
    /// Service for managing the display of DICOM series and slices.
    /// Handles loading, caching, and presentation of DICOM data.
    /// </summary>
    public class DicomDisplayService : DisposableBase
    {
        #region Fields

        private readonly DicomReader _reader;
        private DicomMetadata[] _slices;
        private DicomMetadata _globalView;
        private int _currentSliceIndex;
        private int _windowWidth;
        private int _windowCenter;
        private readonly Dictionary<int, Bitmap> _imageCache = new Dictionary<int, Bitmap>();

        // Cache parameters
        private readonly int _maxCacheSize = 30; // Maximum number of images to keep in cache
        private Queue<int> _cacheQueue = new Queue<int>(); // For tracking cache order

        #endregion

        #region Properties

        /// <summary>
        /// Gets the directory path of the DICOM series.
        /// </summary>
        public string DirectoryPath => _reader.DirectoryPath;

        /// <summary>
        /// Gets the current window width setting.
        /// </summary>
        public int WindowWidth => _windowWidth;

        /// <summary>
        /// Gets the current window center setting.
        /// </summary>
        public int WindowCenter => _windowCenter;

        /// <summary>
        /// Gets the total number of slices in the series.
        /// </summary>
        public int TotalSlices => _slices?.Length ?? 0;

        /// <summary>
        /// Gets the global view metadata.
        /// </summary>
        public DicomMetadata GlobalView => _globalView;

        /// <summary>
        /// Gets all slices in the series.
        /// </summary>
        public DicomMetadata[] Slices => _slices;

        // Legacy property accessors for backward compatibility

        /// <summary>
        /// Legacy property for backward compatibility. Use Slices instead.
        /// </summary>
        [Obsolete("Use Slices property instead")]
        public DicomMetadata[] slices => _slices;

        /// <summary>
        /// Legacy property for backward compatibility. Use GlobalView instead.
        /// </summary>
        [Obsolete("Use GlobalView property instead")]
        public DicomMetadata globalView => _globalView;

        /// <summary>
        /// Legacy property for backward compatibility. Use WindowWidth instead.
        /// </summary>
        [Obsolete("Use WindowWidth property instead")]
        public int windowWidth => _windowWidth;

        /// <summary>
        /// Legacy property for backward compatibility. Use WindowCenter instead.
        /// </summary>
        [Obsolete("Use WindowCenter property instead")]
        public int windowCenter => _windowCenter;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomDisplayService instance.
        /// </summary>
        /// <param name="reader">The DICOM reader to use for loading data.</param>
        public DicomDisplayService(DicomReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader), "DICOM reader cannot be null");
            InitializeFromReader();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a specific slice metadata by index.
        /// </summary>
        /// <param name="sliceIndex">The index of the slice to retrieve.</param>
        /// <returns>The DICOM metadata for the specified slice.</returns>
        public DicomMetadata GetSlice(int sliceIndex)
        {
            ThrowIfDisposed();

            if (_slices == null || sliceIndex < 0 || sliceIndex >= _slices.Length)
                throw new ArgumentOutOfRangeException(nameof(sliceIndex), "Slice index out of range");

            return _slices[sliceIndex];
        }

        /// <summary>
        /// Sets the current slice index.
        /// </summary>
        /// <param name="index">The index of the slice to make current.</param>
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
        public int GetTotalSlices() => TotalSlices;

        /// <summary>
        /// Updates window settings for contrast adjustment.
        /// </summary>
        /// <param name="width">The window width.</param>
        /// <param name="center">The window center.</param>
        public void UpdateWindowSettings(int width, int center)
        {
            ThrowIfDisposed();

            // Update window settings
            _windowWidth = width;
            _windowCenter = center;

            // Clear cache as all images need to be regenerated with new window settings
            ClearImageCache();
            Logger.Info($"Window settings updated: Width={width}, Center={center}");
        }

        /// <summary>
        /// Gets the image for the current slice.
        /// </summary>
        /// <param name="windowWidth">Optional window width override.</param>
        /// <param name="windowCenter">Optional window center override.</param>
        /// <returns>A bitmap image of the current slice.</returns>
        public Bitmap GetCurrentSliceImage(int windowWidth = -1, int windowCenter = -1)
        {
            ThrowIfDisposed();

            if (_slices == null || _currentSliceIndex < 0 || _currentSliceIndex >= _slices.Length)
                throw new InvalidOperationException("No current slice available");

            // Use provided values or current settings
            int width = windowWidth > 0 ? windowWidth : _windowWidth;
            int center = windowCenter > 0 ? windowCenter : _windowCenter;

            // Use cached image if window settings match
            if (windowWidth == -1 && windowCenter == -1 && _imageCache.TryGetValue(_currentSliceIndex, out var cachedImage))
            {
                return new Bitmap(cachedImage); // Return a copy to prevent modification
            }

            // Create a new image
            var image = DicomImageProcessor.ConvertToBitmap(_slices[_currentSliceIndex], width, center);

            // Cache the image if using default window settings
            if (windowWidth == -1 && windowCenter == -1)
            {
                CacheImage(_currentSliceIndex, image);
            }

            return image;
        }

        /// <summary>
        /// Gets the global view image.
        /// </summary>
        /// <returns>A bitmap image of the global view.</returns>
        public Bitmap GetGlobalViewImage()
        {
            ThrowIfDisposed();

            if (_globalView == null)
                throw new InvalidOperationException("No global view available");

            return DicomImageProcessor.ConvertToBitmap(_globalView);
        }

        /// <summary>
        /// Pre-loads images into the cache for smoother navigation.
        /// </summary>
        /// <param name="startIndex">The starting slice index.</param>
        /// <param name="count">The number of slices to pre-load.</param>
        public async Task PreloadImagesAsync(int startIndex, int count)
        {
            ThrowIfDisposed();

            await Task.Run(() =>
            {
                try
                {
                    Logger.Info($"Preloading {count} images starting at index {startIndex}");
                    int endIndex = Math.Min(startIndex + count, TotalSlices);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (!_imageCache.ContainsKey(i))
                        {
                            var image = DicomImageProcessor.ConvertToBitmap(_slices[i], _windowWidth, _windowCenter);
                            CacheImage(i, image);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error during image preloading", ex);
                }
            });
        }

        /// <summary>
        /// Automatically optimizes window settings based on image content.
        /// </summary>
        /// <returns>A tuple containing the optimized width and center values.</returns>
        public (int width, int center) OptimizeWindowSettings()
        {
            ThrowIfDisposed();

            try
            {
                // Get current image with a wide window to see all details
                var image = DicomImageProcessor.ConvertToBitmap(_slices[_currentSliceIndex], 4000, 400);
                var (width, center) = DicomImageProcessor.OptimizeWindowSettings(image);

                // Update internal settings
                _windowWidth = width;
                _windowCenter = center;

                // Clear cache as all images need to be regenerated
                ClearImageCache();

                image.Dispose();
                return (width, center);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to optimize window settings", ex);
                return (_windowWidth, _windowCenter); // Return current settings on error
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes slice data from the DICOM reader.
        /// </summary>
        private void InitializeFromReader()
        {
            _globalView = _reader.GlobalView;
            _slices = _reader.Slices ?? Array.Empty<DicomMetadata>();
            _currentSliceIndex = 0;

            // Initialize window settings from the first slice
            if (_slices.Length > 0)
            {
                _windowWidth = _slices[0].WindowWidth;
                _windowCenter = _slices[0].WindowCenter;
                Logger.Info($"Initial window settings: Width={_windowWidth}, Center={_windowCenter}");
            }
        }

        /// <summary>
        /// Adds an image to the cache, managing cache size.
        /// </summary>
        /// <param name="sliceIndex">The slice index of the image.</param>
        /// <param name="image">The image to cache.</param>
        private void CacheImage(int sliceIndex, Bitmap image)
        {
            // If key already exists, remove and re-add it
            if (_imageCache.ContainsKey(sliceIndex))
            {
                _imageCache[sliceIndex].Dispose();
                _imageCache.Remove(sliceIndex);
                _cacheQueue = new Queue<int>(_cacheQueue.Where(i => i != sliceIndex));
            }

            // Add to cache
            _imageCache[sliceIndex] = new Bitmap(image); // Store a copy
            _cacheQueue.Enqueue(sliceIndex);

            // Trim cache if needed
            while (_cacheQueue.Count > _maxCacheSize)
            {
                int oldestIndex = _cacheQueue.Dequeue();
                if (_imageCache.TryGetValue(oldestIndex, out var oldImage))
                {
                    oldImage.Dispose();
                    _imageCache.Remove(oldestIndex);
                }
            }
        }

        /// <summary>
        /// Clears the image cache.
        /// </summary>
        private void ClearImageCache()
        {
            foreach (var image in _imageCache.Values)
            {
                image.Dispose();
            }

            _imageCache.Clear();
            _cacheQueue.Clear();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Clear image cache
            ClearImageCache();

            // We don't dispose the _reader or the _slices array since they are typically owned by the caller
            // Set fields to null
            _slices = null;
            _globalView = null;

            Logger.Info("DicomDisplayService disposed");
            base.DisposeManagedResources();
        }

        #endregion
    }
}