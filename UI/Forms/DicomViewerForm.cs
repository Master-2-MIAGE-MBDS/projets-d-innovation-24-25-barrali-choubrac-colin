using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Services;
using DeepBridgeWindowsApp.Services.Exporting;
using DeepBridgeWindowsApp.UI.Controls;
using DeepBridgeWindowsApp.UI.ViewModels;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.Forms
{
    /// <summary>
    /// Form for viewing and interacting with DICOM images.
    /// </summary>
    public partial class DicomViewerForm : Form
    {
        #region Fields

        private readonly DicomViewerModel _model;
        private PictureBox _mainPictureBox;
        private TrackBar _sliceTrackBar;
        private TrackBar _windowWidthTrackBar;
        private TrackBar _windowCenterTrackBar;
        private DoubleTrackBar _doubleTrackBar;
        private DicomInfoPanel _infoPanel;
        private Label _sliceLabel;
        private Label _windowCenterLabel;
        private Label _windowWidthLabel;
        private Label _minLabel;
        private Label _maxLabel;
        private Label _startPointLabel;
        private Label _endPointLabel;
        private Label _areaLabel;
        private Button _optimizeWindowButton;
        private Button _resetSelectionButton;
        private Button _findNeckButton;
        private Button _findCarotidButton;
        private Button _renderButton;
        private const int TARGET_SIZE = 512;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomViewerForm instance.
        /// </summary>
        /// <param name="reader">The DICOM reader containing the data to view.</param>
        public DicomViewerForm(DicomReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            // Create model
            var displayManager = new DicomDisplayService(reader);
            _model = new DicomViewerModel(displayManager);

            // Initialize UI
            InitializeComponents();

            // Attach event handlers
            _mainPictureBox.MouseDown += MainPictureBox_MouseDown;
            _mainPictureBox.MouseMove += MainPictureBox_MouseMove;
            _mainPictureBox.MouseUp += MainPictureBox_MouseUp;
            _mainPictureBox.Paint += MainPictureBox_Paint;

            // Initial UI update
            UpdateDisplay();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponents()
        {
            // Form settings
            this.Size = new Size(1424, 768);
            this.Text = "DICOM Viewer";

            // Initialize panels and controls
            InitializeInfoPanel();
            InitializeGlobalViewPanel();
            InitializeMainContentPanel();
        }

        private void InitializeInfoPanel()
        {
            // Create info panel
            _infoPanel = new DicomInfoPanel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = SystemColors.Control,
            };

            // Add labels for selection info
            _startPointLabel = new Label
            {
                Text = "Start Point: (0, 0)",
                AutoSize = true,
                Location = new Point(10, 200)
            };

            _endPointLabel = new Label
            {
                Text = "End Point: (0, 0)",
                AutoSize = true,
                Location = new Point(10, 225)
            };

            _areaLabel = new Label
            {
                Text = "Area: 0",
                AutoSize = true,
                Location = new Point(10, 250)
            };

            // Create button panel
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Height = 120
            };

            // Create buttons
            _findNeckButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "Locate Neck",
                AutoSize = true,
                Margin = new Padding(10, 5, 10, 5),
                Height = 30
            };
            _findNeckButton.Click += FindNeckButton_Click;

            _findCarotidButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "Locate Carotids",
                AutoSize = true,
                Margin = new Padding(10, 5, 10, 5),
                Height = 30
            };
            _findCarotidButton.Click += FindCarotidButton_Click;

            _resetSelectionButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "Reset Selections",
                AutoSize = true,
                Margin = new Padding(10, 5, 10, 5),
                Height = 30
            };
            _resetSelectionButton.Click += ResetSelection_Click;

            _renderButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "3D Render",
                AutoSize = true,
                Margin = new Padding(10, 5, 10, 5),
                Height = 30
            };
            _renderButton.Click += RenderButton_Click;

            // Add buttons to button panel
            buttonPanel.Controls.Add(_resetSelectionButton);
            buttonPanel.Controls.Add(_findNeckButton);
            buttonPanel.Controls.Add(_findCarotidButton);
            buttonPanel.Controls.Add(_renderButton);

            // Add labels to info panel
            _infoPanel.Controls.Add(_startPointLabel);
            _infoPanel.Controls.Add(_endPointLabel);
            _infoPanel.Controls.Add(_areaLabel);
            _infoPanel.Controls.Add(buttonPanel);

            // Add info panel to form
            this.Controls.Add(_infoPanel);
        }

        private void InitializeGlobalViewPanel()
        {
            // Create right panel for global view and controls
            Panel globalViewPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BackColor = SystemColors.Control
            };

            // Create top panel for global view image
            Panel globalTopViewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 768 / 2
            };

            // Create picture box for global view
            PictureBox globalViewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            globalViewPictureBox.Image = _model.DisplayService.GetGlobalViewImage();
            globalTopViewPanel.Controls.Add(globalViewPictureBox);

            // Create bottom panel for controls
            Panel globalBottomViewPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 768 / 2,
            };

            // Create control panel
            Panel controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 768 / 4
            };

            // Create window width track bar
            _windowWidthTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = 4000,
                Value = _model.WindowWidth,
                TickStyle = TickStyle.TopLeft
            };
            _windowWidthTrackBar.ValueChanged += WindowTrackBar_ValueChanged;

            // Create window center track bar
            _windowCenterTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = 800,
                Value = _model.WindowCenter,
                TickStyle = TickStyle.TopLeft
            };
            _windowCenterTrackBar.ValueChanged += WindowTrackBar_ValueChanged;

            // Create window labels
            _windowCenterLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Window Center: " + _model.WindowCenter
            };

            _windowWidthLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Window Width: " + _model.WindowWidth
            };

            // Create optimize window button
            _optimizeWindowButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "Optimize Window",
                Height = 30,
                Margin = new Padding(0, 5, 0, 5)
            };
            _optimizeWindowButton.Click += OptimizeWindowButton_Click;

            // Add controls to control panel
            controlPanel.Controls.AddRange(new Control[] {
                _windowCenterLabel,
                _windowCenterTrackBar,
                _windowWidthLabel,
                _windowWidthTrackBar,
                _optimizeWindowButton
            });

            // Add panels to global view panel
            globalBottomViewPanel.Controls.Add(controlPanel);
            globalViewPanel.Controls.AddRange(new Control[] { globalTopViewPanel, globalBottomViewPanel });

            // Add global view panel to form
            this.Controls.Add(globalViewPanel);
        }

        private void InitializeMainContentPanel()
        {
            // Create main content panel
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // Create main picture box
            _mainPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Create slice track bar
            _sliceTrackBar = new TrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = _model.TotalSlices - 1,
                TickStyle = TickStyle.TopLeft
            };
            _sliceTrackBar.ValueChanged += SliceTrackBar_ValueChanged;

            // Create slice label
            _sliceLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20
            };

            // Create double track bar
            _doubleTrackBar = new DoubleTrackBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = _model.TotalSlices,
                MinValue = 0,
                MaxValue = _model.TotalSlices,
                Height = 30
            };
            _doubleTrackBar.ValueChanged += DoubleTrackBar_ValueChanged;

            // Create min/max labels
            _minLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = "Min: 0"
            };

            _maxLabel = new Label
            {
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
                Text = $"Max: {_model.TotalSlices}"
            };

            // Add controls to content panel
            contentPanel.Controls.AddRange(new Control[] {
                _mainPictureBox,
                _sliceLabel,
                _sliceTrackBar,
                _doubleTrackBar,
                _minLabel,
                _maxLabel
            });

            // Add content panel to form
            this.Controls.Add(contentPanel);
        }

        #endregion

        #region Event Handlers

        private void SliceTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _model.SetSliceIndex(_sliceTrackBar.Value);
            UpdateDisplay();
        }

        private void WindowTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _model.UpdateWindowSettings(_windowWidthTrackBar.Value, _windowCenterTrackBar.Value);
            UpdateDisplay();
        }

        private void DoubleTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _model.MinSlice = _doubleTrackBar.MinValue;
            _model.MaxSlice = _doubleTrackBar.MaxValue;

            _minLabel.Text = $"Min: {_model.MinSlice}";
            _maxLabel.Text = $"Max: {_model.MaxSlice}";
        }

        private void FindNeckButton_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var (top, center, bottom, success) = _model.FindNeckPosition();

                if (success)
                {
                    // Update UI
                    _sliceTrackBar.Value = center;
                    _doubleTrackBar.MinValue = top;
                    _doubleTrackBar.MaxValue = bottom;

                    // Update model
                    _model.MinSlice = top;
                    _model.MaxSlice = bottom;
                    _model.SetSliceIndex(center);

                    // Update labels
                    _minLabel.Text = $"Min: {top}";
                    _maxLabel.Text = $"Max: {bottom}";

                    MessageBox.Show($"Neck position found:\n" +
                                   $"- Top (jaw): slice {top + 1}\n" +
                                   $"- Center: slice {center + 1}\n" +
                                   $"- Bottom (shoulders): slice {bottom + 1}",
                                   "Neck Location",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Information);

                    // Update display
                    UpdateDisplay();
                }
                else
                {
                    MessageBox.Show("Failed to locate neck position.",
                                   "Location Failed",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finding neck position: {ex.Message}",
                               "Error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Logger.Error("Error finding neck position", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void FindCarotidButton_Click(object sender, EventArgs e)
        {
            if (_doubleTrackBar.MinValue >= _doubleTrackBar.MaxValue)
            {
                MessageBox.Show("Please locate the neck or select a valid slice range first.",
                               "Invalid Slice Range",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                // Use an optimized window setting for carotids
                _windowWidthTrackBar.Value = 300;
                _windowCenterTrackBar.Value = 120;
                _model.UpdateWindowSettings(300, 120);

                // Create carotid selection
                if (_model.CreateCarotidSelection(_mainPictureBox.ClientSize))
                {
                    // Update display
                    UpdateDisplay();

                    // Update selection info
                    UpdateSelectionInfo();

                    MessageBox.Show("Carotid region selected.",
                                  "Location Complete",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to locate carotid region.",
                                   "Location Failed",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finding carotid region: {ex.Message}",
                               "Error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Logger.Error("Error finding carotid region", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ResetSelection_Click(object sender, EventArgs e)
        {
            _model.ResetSelections();

            // Update UI
            _doubleTrackBar.MinValue = 0;
            _doubleTrackBar.MaxValue = _model.TotalSlices;
            _minLabel.Text = "Min: 0";
            _maxLabel.Text = $"Max: {_model.TotalSlices}";

            // Update selection info
            _startPointLabel.Text = "Start Point: (0, 0)";
            _endPointLabel.Text = "End Point: (0, 0)";
            _areaLabel.Text = "Area: 0";

            // Redraw
            _mainPictureBox.Invalidate();

            MessageBox.Show("All selections have been reset.",
                           "Reset Complete",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Information);
        }

        private void OptimizeWindowButton_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Show window preset menu
                ShowWindowPresetsMenu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error optimizing window settings: {ex.Message}",
                               "Error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Logger.Error("Error optimizing window settings", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RenderButton_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Get active selection rectangle in image coordinates
                Rectangle? carotidRect = null;

                if (_model.ActiveSelectionRectangle.HasValue)
                {
                    Size displayedSize = _model.GetDisplayedImageSize(
                        new Size(TARGET_SIZE, TARGET_SIZE),
                        _mainPictureBox.ClientSize);

                    carotidRect = _model.ConvertToImageRectangle(
                        _model.ActiveSelectionRectangle.Value,
                        displayedSize,
                        _mainPictureBox.ClientSize,
                        TARGET_SIZE);
                }

                // Get patient directory
                string basePath = Path.GetDirectoryName(_model.DisplayService.DirectoryPath);
                string scanFolderName = new DirectoryInfo(_model.DisplayService.DirectoryPath).Name;
                string fullPath = Path.Combine(basePath, scanFolderName);

                // Create and show 3D render form
                //var renderForm = new RenderDicomForm(
                //    _model.DisplayService,
                //    _model.MinSlice,
                //    _model.MaxSlice,
                //    fullPath,
                //    carotidRect);

                //renderForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating 3D render: {ex.Message}",
                               "Error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Logger.Error("Error creating 3D render", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void MainPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _model.IsDrawing = true;
                _model.StartPoint = e.Location;

                // Convert point to image coordinates
                Size displayedSize = _model.GetDisplayedImageSize(
                    new Size(TARGET_SIZE, TARGET_SIZE),
                    _mainPictureBox.ClientSize);

                Point resizedPoint = _model.ConvertToImageCoordinates(
                    e.Location,
                    displayedSize,
                    _mainPictureBox.ClientSize,
                    TARGET_SIZE);

                if (resizedPoint != Point.Empty)
                {
                    _startPointLabel.Text = $"Start Point: ({resizedPoint.X}, {resizedPoint.Y})";
                }

                _mainPictureBox.Invalidate();
            }
        }

        private void MainPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_model.IsDrawing)
            {
                _model.EndPoint = e.Location;

                // Convert point to image coordinates
                Size displayedSize = _model.GetDisplayedImageSize(
                    new Size(TARGET_SIZE, TARGET_SIZE),
                    _mainPictureBox.ClientSize);

                Point resizedPoint = _model.ConvertToImageCoordinates(
                    e.Location,
                    displayedSize,
                    _mainPictureBox.ClientSize,
                    TARGET_SIZE);

                if (resizedPoint != Point.Empty)
                {
                    _endPointLabel.Text = $"End Point: ({resizedPoint.X}, {resizedPoint.Y})";

                    Rectangle resizedRect = _model.ConvertToImageRectangle(
                        _model.GetRectangle(_model.StartPoint, _model.EndPoint),
                        displayedSize,
                        _mainPictureBox.ClientSize,
                        TARGET_SIZE);

                    if (resizedRect != Rectangle.Empty)
                    {
                        _areaLabel.Text = $"Area: {resizedRect.Width * resizedRect.Height}";
                    }
                }

                _mainPictureBox.Invalidate();
            }
        }

        private void MainPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _model.IsDrawing = false;
                _model.EndPoint = e.Location;

                // Convert point to image coordinates
                Size displayedSize = _model.GetDisplayedImageSize(
                    new Size(TARGET_SIZE, TARGET_SIZE),
                    _mainPictureBox.ClientSize);

                Point resizedPoint = _model.ConvertToImageCoordinates(
                    e.Location,
                    displayedSize,
                    _mainPictureBox.ClientSize,
                    TARGET_SIZE);

                if (resizedPoint != Point.Empty)
                {
                    _endPointLabel.Text = $"End Point: ({resizedPoint.X}, {resizedPoint.Y})";

                    // Get resized rectangle for image coordinates
                    Rectangle resizedRect = _model.ConvertToImageRectangle(
                        _model.GetRectangle(_model.StartPoint, _model.EndPoint),
                        displayedSize,
                        _mainPictureBox.ClientSize,
                        TARGET_SIZE);

                    if (resizedRect != Rectangle.Empty)
                    {
                        _areaLabel.Text = $"Area: {resizedRect.Width * resizedRect.Height}";

                        Debug.WriteLine($"Manual rectangle: Display={_model.SelectionRectangle}, Image={resizedRect}");
                    }
                }

                _mainPictureBox.Invalidate();
            }
        }

        private void MainPictureBox_Paint(object sender, PaintEventArgs e)
        {
            // Draw selection rectangle if drawing or complete
            if (_model.IsDrawing || _model.StartPoint != _model.EndPoint)
            {
                Rectangle rect = _model.GetRectangle(_model.StartPoint, _model.EndPoint);
                e.Graphics.DrawRectangle(Pens.Red, rect);
            }

            // Draw carotid selection if enabled
            if (_model.ShowCarotidSelection)
            {
                using (Pen redPen = new Pen(Color.Red, 2))
                {
                    e.Graphics.DrawRectangle(redPen, _model.CarotidRectangle);

                    // Calculate center and marker points
                    int centerX = _model.CarotidRectangle.X + _model.CarotidRectangle.Width / 2;
                    int centerY = _model.CarotidRectangle.Y + _model.CarotidRectangle.Height / 2;

                    // Draw markers at 1/4 and 3/4 of width
                    int leftX = _model.CarotidRectangle.X + _model.CarotidRectangle.Width / 4;
                    int rightX = _model.CarotidRectangle.X + _model.CarotidRectangle.Width * 3 / 4;

                    e.Graphics.FillEllipse(Brushes.Red, leftX - 3, centerY - 3, 6, 6);
                    e.Graphics.FillEllipse(Brushes.Red, rightX - 3, centerY - 3, 6, 6);
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the display with current settings.
        /// </summary>
        private void UpdateDisplay()
        {
            // Dispose previous image
            _mainPictureBox.Image?.Dispose();

            // Get new image
            Bitmap originalImage = _model.GetCurrentSliceImage();

            // Resize to target size
            var resizedImage = new Bitmap(TARGET_SIZE, TARGET_SIZE);
            using (var g = Graphics.FromImage(resizedImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(originalImage, 0, 0, TARGET_SIZE, TARGET_SIZE);
            }

            // Clean up original
            originalImage.Dispose();

            // Set image
            _mainPictureBox.Image = resizedImage;

            // Update labels
            _sliceLabel.Text = $"Slice {_model.CurrentSliceIndex + 1} of {_model.TotalSlices}";
            _windowCenterLabel.Text = "Window Center: " + _model.WindowCenter;
            _windowWidthLabel.Text = "Window Width: " + _model.WindowWidth;

            // Update info panel
            UpdateInfoPanel();
        }

        /// <summary>
        /// Updates the DICOM info panel with current metadata.
        /// </summary>
        private void UpdateInfoPanel()
        {
            var currentSlice = _model.GetSlice(_model.CurrentSliceIndex);
            _infoPanel.UpdateMetadata(currentSlice);
            _infoPanel.CurrentSliceIndex = _model.CurrentSliceIndex;
            _infoPanel.TotalSlices = _model.TotalSlices;
            _infoPanel.WindowWidth = _model.WindowWidth;
            _infoPanel.WindowCenter = _model.WindowCenter;
            _infoPanel.UpdateSliceInfo();
            _infoPanel.UpdateWindowSettings();
        }

        /// <summary>
        /// Updates selection information labels.
        /// </summary>
        private void UpdateSelectionInfo()
        {
            // Get displayed size
            Size displayedSize = _model.GetDisplayedImageSize(
                new Size(TARGET_SIZE, TARGET_SIZE),
                _mainPictureBox.ClientSize);

            // Update start point info
            Point startResized = _model.ConvertToImageCoordinates(
                _model.StartPoint,
                displayedSize,
                _mainPictureBox.ClientSize,
                TARGET_SIZE);

            if (startResized != Point.Empty)
            {
                _startPointLabel.Text = $"Start Point: ({startResized.X}, {startResized.Y})";
            }

            // Update end point info
            Point endResized = _model.ConvertToImageCoordinates(
                _model.EndPoint,
                displayedSize,
                _mainPictureBox.ClientSize,
                TARGET_SIZE);

            if (endResized != Point.Empty)
            {
                _endPointLabel.Text = $"End Point: ({endResized.X}, {endResized.Y})";
            }

            // Update area info
            if (_model.ActiveSelectionRectangle.HasValue)
            {
                Rectangle resizedRect = _model.ConvertToImageRectangle(
                    _model.ActiveSelectionRectangle.Value,
                    displayedSize,
                    _mainPictureBox.ClientSize,
                    TARGET_SIZE);

                if (resizedRect != Rectangle.Empty)
                {
                    _areaLabel.Text = $"Area: {resizedRect.Width * resizedRect.Height}";
                }
            }
        }

        /// <summary>
        /// Shows a context menu with window presets.
        /// </summary>
        private void ShowWindowPresetsMenu()
        {
            var presetsMenu = new ContextMenuStrip();

            // Add carotid preset
            AddPresetMenuItem(presetsMenu, "Angiography (Carotids)", 300, 120);

            // Add tissue presets
            AddPresetMenuItem(presetsMenu, "Neck Soft Tissue", 350, 70);
            AddPresetMenuItem(presetsMenu, "Brain", 80, 40);
            AddPresetMenuItem(presetsMenu, "Lung", 1500, -600);
            AddPresetMenuItem(presetsMenu, "Bone", 2500, 480);
            AddPresetMenuItem(presetsMenu, "Standard Contrast", 400, 50);

            // Add auto-optimization option
            var autoItem = new ToolStripMenuItem("Auto Optimize");
            autoItem.Click += (s, e) => OptimizeWindowSettings();
            presetsMenu.Items.Add(autoItem);

            // Show menu next to button
            presetsMenu.Show(_optimizeWindowButton, new Point(0, _optimizeWindowButton.Height));
        }

        /// <summary>
        /// Adds a preset menu item to the context menu.
        /// </summary>
        private void AddPresetMenuItem(ContextMenuStrip menu, string name, int width, int center)
        {
            var item = new ToolStripMenuItem(name);
            item.Click += (s, e) =>
            {
                _windowWidthTrackBar.Value = Math.Min(_windowWidthTrackBar.Maximum,
                    Math.Max(_windowWidthTrackBar.Minimum, width));

                _windowCenterTrackBar.Value = Math.Min(_windowCenterTrackBar.Maximum,
                    Math.Max(_windowCenterTrackBar.Minimum, center));

                _model.UpdateWindowSettings(_windowWidthTrackBar.Value, _windowCenterTrackBar.Value);
                UpdateDisplay();
            };
            menu.Items.Add(item);
        }

        /// <summary>
        /// Automatically optimizes window settings based on image content.
        /// </summary>
        private void OptimizeWindowSettings()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Get optimized settings
                var (width, center) = _model.OptimizeWindowSettings();

                // Update trackbars
                _windowWidthTrackBar.Value = Math.Min(_windowWidthTrackBar.Maximum,
                    Math.Max(_windowWidthTrackBar.Minimum, width));

                _windowCenterTrackBar.Value = Math.Min(_windowCenterTrackBar.Maximum,
                    Math.Max(_windowCenterTrackBar.Minimum, center));

                // Update display
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error optimizing window settings: {ex.Message}",
                               "Error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                Logger.Error("Error optimizing window settings", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        #endregion

        #region IDisposable Implementation

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                _model?.Dispose();

                // Dispose images
                _mainPictureBox?.Image?.Dispose();
                if (_infoPanel != null && _infoPanel.Controls != null)
                {
                    foreach (Control c in _infoPanel.Controls)
                    {
                        if (c is PictureBox pb)
                        {
                            pb.Image?.Dispose();
                        }
                    }
                }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}