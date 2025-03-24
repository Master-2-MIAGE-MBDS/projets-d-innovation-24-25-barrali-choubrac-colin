using System;
using System.Drawing;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Services;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the DICOM viewer, separating UI logic from presentation.
    /// </summary>
    public class DicomViewerModel : DisposableBase
    {
        #region Fields

        private readonly DicomDisplayService _displayService;
        private Rectangle _selectionRectangle = Rectangle.Empty;
        private Rectangle _carotidRectangle = Rectangle.Empty;
        private bool _isDrawing = false;
        private Point _startPoint = Point.Empty;
        private Point _endPoint = Point.Empty;
        private bool _showCarotidSelection = false;
        private int _minSlice = 0;
        private int _maxSlice = 0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current selection rectangle in display coordinates.
        /// </summary>
        public Rectangle SelectionRectangle => _selectionRectangle;

        /// <summary>
        /// Gets the carotid rectangle in display coordinates.
        /// </summary>
        public Rectangle CarotidRectangle => _carotidRectangle;

        /// <summary>
        /// Gets or sets whether drawing is in progress.
        /// </summary>
        public bool IsDrawing
        {
            get => _isDrawing;
            set
            {
                if (_isDrawing != value)
                {
                    _isDrawing = value;
                    // If drawing is finished, calculate the selection rectangle
                    if (!value && _startPoint != _endPoint)
                    {
                        _selectionRectangle = GetRectangle(_startPoint, _endPoint);
                        // When manual drawing occurs, carotid selection is hidden
                        _showCarotidSelection = false;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the start point of the selection in display coordinates.
        /// </summary>
        public Point StartPoint
        {
            get => _startPoint;
            set => _startPoint = value;
        }

        /// <summary>
        /// Gets or sets the end point of the selection in display coordinates.
        /// </summary>
        public Point EndPoint
        {
            get => _endPoint;
            set => _endPoint = value;
        }

        /// <summary>
        /// Gets or sets whether to show the carotid selection.
        /// </summary>
        public bool ShowCarotidSelection
        {
            get => _showCarotidSelection;
            set => _showCarotidSelection = value;
        }

        /// <summary>
        /// Gets or sets the minimum slice index.
        /// </summary>
        public int MinSlice
        {
            get => _minSlice;
            set => _minSlice = Math.Max(0, Math.Min(value, _displayService.GetTotalSlices() - 1));
        }

        /// <summary>
        /// Gets or sets the maximum slice index.
        /// </summary>
        public int MaxSlice
        {
            get => _maxSlice;
            set => _maxSlice = Math.Min(value, _displayService.GetTotalSlices() - 1);
        }

        /// <summary>
        /// Gets the current slice index.
        /// </summary>
        public int CurrentSliceIndex => _displayService.GetCurrentSliceIndex();

        /// <summary>
        /// Gets the total number of slices.
        /// </summary>
        public int TotalSlices => _displayService.GetTotalSlices();

        /// <summary>
        /// Gets the current window width.
        /// </summary>
        public int WindowWidth => _displayService.WindowWidth;

        /// <summary>
        /// Gets the current window center.
        /// </summary>
        public int WindowCenter => _displayService.WindowCenter;

        /// <summary>
        /// Gets whether a carotid selection is available.
        /// </summary>
        public bool HasCarotidSelection => _showCarotidSelection && _carotidRectangle != Rectangle.Empty;

        /// <summary>
        /// Gets whether a manual selection is available.
        /// </summary>
        public bool HasManualSelection => !_showCarotidSelection && _selectionRectangle != Rectangle.Empty;

        /// <summary>
        /// Gets the active selection rectangle for 3D rendering.
        /// </summary>
        public Rectangle? ActiveSelectionRectangle
        {
            get
            {
                if (_showCarotidSelection && _carotidRectangle != Rectangle.Empty)
                    return _carotidRectangle;
                else if (!_showCarotidSelection && _selectionRectangle != Rectangle.Empty)
                    return _selectionRectangle;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the display service used by this model.
        /// </summary>
        public DicomDisplayService DisplayService => _displayService;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomViewerModel with the specified display service.
        /// </summary>
        /// <param name="displayService">The DICOM display service to use.</param>
        public DicomViewerModel(DicomDisplayService displayService)
        {
            _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));

            // Initialize min/max slices to full range
            _minSlice = 0;
            _maxSlice = _displayService.GetTotalSlices() - 1;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the current slice index.
        /// </summary>
        /// <param name="index">The slice index to set.</param>
        public void SetSliceIndex(int index)
        {
            _displayService.SetSliceIndex(index);
        }

        /// <summary>
        /// Gets the current slice image.
        /// </summary>
        /// <param name="windowWidth">Optional window width override.</param>
        /// <param name="windowCenter">Optional window center override.</param>
        /// <returns>The current slice image.</returns>
        public Bitmap GetCurrentSliceImage(int windowWidth = -1, int windowCenter = -1)
        {
            return _displayService.GetCurrentSliceImage(windowWidth, windowCenter);
        }

        /// <summary>
        /// Updates window settings.
        /// </summary>
        /// <param name="width">The new window width.</param>
        /// <param name="center">The new window center.</param>
        public void UpdateWindowSettings(int width, int center)
        {
            _displayService.UpdateWindowSettings(width, center);
        }

        /// <summary>
        /// Optimizes window settings based on the current image.
        /// </summary>
        /// <returns>A tuple containing the optimized width and center.</returns>
        public (int width, int center) OptimizeWindowSettings()
        {
            return _displayService.OptimizeWindowSettings();
        }

        /// <summary>
        /// Gets a DICOM metadata slice by index.
        /// </summary>
        /// <param name="sliceIndex">The slice index.</param>
        /// <returns>The slice metadata.</returns>
        public DicomMetadata GetSlice(int sliceIndex)
        {
            return _displayService.GetSlice(sliceIndex);
        }

        /// <summary>
        /// Sets up an automatic carotid selection region.
        /// </summary>
        /// <param name="pictureBoxSize">The size of the picture box.</param>
        /// <returns>True if the selection was created successfully.</returns>
        public bool CreateCarotidSelection(Size pictureBoxSize)
        {
            try
            {
                // Calculate proportional rectangle in the center of the picture box
                double rectWidthPercent = 0.4;  // 40% of width
                double rectHeightPercent = 0.3; // 30% of height

                int rectWidth = (int)(pictureBoxSize.Width * rectWidthPercent);
                int rectHeight = (int)(pictureBoxSize.Height * rectHeightPercent);

                int rectX = (pictureBoxSize.Width - rectWidth) / 2;
                int rectY = (pictureBoxSize.Height - rectHeight) / 2;

                // Set up the carotid rectangle
                _carotidRectangle = new Rectangle(rectX, rectY, rectWidth, rectHeight);
                _showCarotidSelection = true;

                // Update start and end points for consistent interface
                _startPoint = new Point(_carotidRectangle.X, _carotidRectangle.Y);
                _endPoint = new Point(_carotidRectangle.Right, _carotidRectangle.Bottom);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create carotid selection", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a neck position range by finding optimal slice range.
        /// </summary>
        /// <returns>A tuple with found ranges and center index.</returns>
        public (int top, int center, int bottom, bool success) FindNeckPosition()
        {
            try
            {
                int totalSlices = _displayService.GetTotalSlices();

                // Search range around the middle ±35%
                int centerSlice = totalSlices / 2;
                int searchRange = (int)(totalSlices * 0.35);
                int startSlice = Math.Max(0, centerSlice - searchRange);
                int endSlice = Math.Min(totalSlices - 1, centerSlice + searchRange);

                // These would be calculated by analyzing slice data to find neck
                // This is a simplified implementation - actual implementation would examine 
                // pixel data to identify where the neck narrows
                // 
                // For now, we'll just return a reasonable slice range in the middle

                int neckLength = (int)(totalSlices * 0.4); // 40% of slices
                int neckTop = Math.Max(0, centerSlice - (neckLength / 2));
                int neckBottom = Math.Min(totalSlices - 1, centerSlice + (neckLength / 2));

                return (neckTop, centerSlice, neckBottom, true);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to find neck position", ex);
                return (0, 0, 0, false);
            }
        }

        /// <summary>
        /// Resets all selections and ranges.
        /// </summary>
        public void ResetSelections()
        {
            _selectionRectangle = Rectangle.Empty;
            _carotidRectangle = Rectangle.Empty;
            _startPoint = Point.Empty;
            _endPoint = Point.Empty;
            _showCarotidSelection = false;
            _isDrawing = false;

            // Reset slice range to full dataset
            _minSlice = 0;
            _maxSlice = _displayService.GetTotalSlices() - 1;
        }

        /// <summary>
        /// Converts display coordinates to image coordinates.
        /// </summary>
        /// <param name="displayPoint">The point in display coordinates.</param>
        /// <param name="displayedSize">The size of the displayed image.</param>
        /// <param name="pictureBoxSize">The size of the picture box.</param>
        /// <param name="targetSize">The target image size.</param>
        /// <returns>The point in image coordinates, or Point.Empty if outside bounds.</returns>
        public Point ConvertToImageCoordinates(Point displayPoint, Size displayedSize, Size pictureBoxSize, int targetSize)
        {
            // Calculate offsets for centered image
            int offsetX = (pictureBoxSize.Width - displayedSize.Width) / 2;
            int offsetY = (pictureBoxSize.Height - displayedSize.Height) / 2;

            // Adjust for offsets
            displayPoint.X -= offsetX;
            displayPoint.Y -= offsetY;

            // Check if the point is within the displayed image
            if (displayPoint.X < 0 || displayPoint.Y < 0 ||
                displayPoint.X >= displayedSize.Width || displayPoint.Y >= displayedSize.Height)
            {
                return Point.Empty;
            }

            // Calculate scaling factors
            float scaleX = (float)targetSize / displayedSize.Width;
            float scaleY = (float)targetSize / displayedSize.Height;

            // Convert to image coordinates
            return new Point(
                (int)(displayPoint.X * scaleX),
                (int)(displayPoint.Y * scaleY)
            );
        }

        /// <summary>
        /// Converts a display rectangle to image coordinates.
        /// </summary>
        /// <param name="displayRect">The rectangle in display coordinates.</param>
        /// <param name="displayedSize">The size of the displayed image.</param>
        /// <param name="pictureBoxSize">The size of the picture box.</param>
        /// <param name="targetSize">The target image size.</param>
        /// <returns>The rectangle in image coordinates, or Rectangle.Empty if invalid.</returns>
        public Rectangle ConvertToImageRectangle(Rectangle displayRect, Size displayedSize, Size pictureBoxSize, int targetSize)
        {
            Point topLeft = ConvertToImageCoordinates(
                new Point(displayRect.X, displayRect.Y),
                displayedSize,
                pictureBoxSize,
                targetSize
            );

            Point bottomRight = ConvertToImageCoordinates(
                new Point(displayRect.Right, displayRect.Bottom),
                displayedSize,
                pictureBoxSize,
                targetSize
            );

            if (topLeft == Point.Empty || bottomRight == Point.Empty)
                return Rectangle.Empty;

            return new Rectangle(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );
        }

        /// <summary>
        /// Gets a rectangle from two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>A rectangle defined by the two points.</returns>
        public Rectangle GetRectangle(Point p1, Point p2)
        {
            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y)
            );
        }

        /// <summary>
        /// Calculates the size of the displayed image in the picture box.
        /// </summary>
        /// <param name="imageSize">The original image size.</param>
        /// <param name="pictureBoxSize">The picture box size.</param>
        /// <returns>The size of the displayed image.</returns>
        public Size GetDisplayedImageSize(Size imageSize, Size pictureBoxSize)
        {
            if (imageSize.Width == 0 || imageSize.Height == 0)
                return Size.Empty;

            float imageRatio = (float)imageSize.Width / imageSize.Height;
            float containerRatio = (float)pictureBoxSize.Width / pictureBoxSize.Height;

            if (imageRatio > containerRatio)
            {
                // Image is wider than container proportionally
                return new Size(
                    pictureBoxSize.Width,
                    (int)(pictureBoxSize.Width / imageRatio)
                );
            }
            else
            {
                // Image is taller than container proportionally
                return new Size(
                    (int)(pictureBoxSize.Height * imageRatio),
                    pictureBoxSize.Height
                );
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Clear fields that might hold references
            _startPoint = Point.Empty;
            _endPoint = Point.Empty;
            _selectionRectangle = Rectangle.Empty;
            _carotidRectangle = Rectangle.Empty;

            base.DisposeManagedResources();
        }

        #endregion
    }
}