using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.Forms
{
    /// <summary>
    /// Main form of the application for selecting and opening DICOM series.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Fields

        private string _currentDirectory;
        private readonly string _defaultDirectory = @"C:\dataset_chu_nice_2020_2021\scan\SF103E8_10.241.3.232_20210118173228207_CT_SR\SF103E8_10.241.3.232_20210118173228207";
        private ListView _contentListView;
        private Button _viewDicomButton;
        private TextBox _directoryTextBox;
        private Label _infoLabel;
        private PictureBox _globalViewPictureBox;
        private Button _testResourcesButton;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new MainForm instance.
        /// </summary>
        public MainForm()
        {
            InitializeComponents();

            // Use default directory or try to find a suitable one
            _currentDirectory = File.Exists(_defaultDirectory) ?
                _defaultDirectory :
                FindDefaultDirectory();

            _directoryTextBox.Text = _currentDirectory;

            LoadDirectory(_currentDirectory);

            // Log start
            Logger.Info($"MainForm initialized with directory: {_currentDirectory}");
        }

        #endregion

        #region UI Initialization

        /// <summary>
        /// Initializes the form components.
        /// </summary>
        private void InitializeComponents()
        {
            // Form settings
            Text = "DICOM Viewer - Main";
            Size = new Size(1000, 600);
            MinimumSize = new Size(800, 400);

            // Create main table layout
            TableLayoutPanel mainTableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10),
            };

            // Configure row styles
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Create top panel for directory controls
            TableLayoutPanel topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 85));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            // Create directory textbox
            _directoryTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = _defaultDirectory
            };

            // Create browse button
            Button browseButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Browse",
                Margin = new Padding(5, 0, 0, 0)
            };
            browseButton.Click += BrowseButton_Click;

            // Add controls to top panel
            topPanel.Controls.Add(_directoryTextBox, 0, 0);
            topPanel.Controls.Add(browseButton, 1, 0);

            // Create content panel
            TableLayoutPanel contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 10, 0, 0)
            };
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            // Create content list view
            _contentListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            _contentListView.Columns.Add("Name", -2);
            _contentListView.Columns.Add("Type", -2);
            _contentListView.Columns.Add("DICOM Files", -2);
            _contentListView.SelectedIndexChanged += ContentListView_SelectedIndexChanged;

            // Create right panel
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(10, 0, 0, 0)
            };

            // Create info label
            _infoLabel = new Label
            {
                Location = new Point(10, 10),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 150
            };
            rightPanel.Controls.Add(_infoLabel);

            // Create view DICOM button
            _viewDicomButton = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = "View DICOM Images",
                Enabled = false,
                Margin = new Padding(10)
            };
            _viewDicomButton.Click += ViewDicomButton_Click;
            rightPanel.Controls.Add(_viewDicomButton);

            // Add controls to content panel
            contentPanel.Controls.Add(_contentListView, 0, 0);
            contentPanel.Controls.Add(rightPanel, 1, 0);

            // Add panels to main layout
            mainTableLayout.Controls.Add(topPanel, 0, 0);
            mainTableLayout.Controls.Add(contentPanel, 0, 1);

            // Add main layout to form
            Controls.Add(mainTableLayout);

            // Add resize event handler
            Resize += MainForm_Resize;
        }

        #endregion

        #region Event Handlers

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Adjust column widths in ListView
            if (_contentListView.Columns.Count > 0)
            {
                int totalWidth = _contentListView.ClientSize.Width;
                _contentListView.Columns[0].Width = (int)(totalWidth * 0.5);  // Name: 50%
                _contentListView.Columns[1].Width = (int)(totalWidth * 0.25); // Type: 25%
                _contentListView.Columns[2].Width = (int)(totalWidth * 0.25); // DICOM Files: 25%
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.SelectedPath = _currentDirectory;
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _currentDirectory = folderDialog.SelectedPath;
                    _directoryTextBox.Text = _currentDirectory;
                    LoadDirectory(_currentDirectory);
                }
            }
        }

        private void ContentListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_contentListView.SelectedItems.Count == 0)
                return;

            var selectedPath = _contentListView.SelectedItems[0].Tag.ToString();
            ShowDicomInfo(selectedPath);
        }

        private void ViewDicomButton_Click(object sender, EventArgs e)
        {
            var path = _viewDicomButton.Tag.ToString();

            try
            {
                Cursor = Cursors.WaitCursor;

                var reader = new DicomReader(path);
                reader.LoadAllFiles();

                var viewerForm = new DicomViewerForm(reader);
                viewerForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading DICOM files: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error("Error opening DICOM viewer", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Loads the DICOM directories from the specified path.
        /// </summary>
        /// <param name="path">The path to load directories from.</param>
        private void LoadDirectory(string path)
        {
            _contentListView.Items.Clear();
            _infoLabel.Text = string.Empty;
            _viewDicomButton.Enabled = false;

            if (_globalViewPictureBox != null)
            {
                _globalViewPictureBox.Image?.Dispose();
                _globalViewPictureBox.Image = null;
            }

            try
            {
                // Get directories that directly contain .dcm files (no recursion)
                var directories = Directory.GetDirectories(path)
                    .Where(dir => Directory.GetFiles(dir, "*.dcm").Length > 0);

                // Add directories with DICOM files to the list
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var dicomCount = Directory.GetFiles(dir, "*.dcm").Length;

                    if (dicomCount <= 10)
                        continue;

                    var item = new ListViewItem(new[]
                    {
                        dirInfo.Name,
                        "Folder",
                        dicomCount.ToString()
                    });
                    item.Tag = dir;
                    _contentListView.Items.Add(item);
                }

                // Check if current directory has DICOM files
                var currentDirDicomFiles = Directory.GetFiles(path, "*.dcm");
                if (currentDirDicomFiles.Length > 0)
                {
                    ShowDicomInfo(path);
                }

                Logger.Info($"Loaded directory with {_contentListView.Items.Count} DICOM series: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading directory: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error($"Error loading directory: {path}", ex);
            }
        }

        /// <summary>
        /// Shows information about the DICOM files in the specified path.
        /// </summary>
        /// <param name="path">The path containing DICOM files.</param>
        private void ShowDicomInfo(string path)
        {
            try
            {
                var dicomFiles = Directory.GetFiles(path, "*.dcm", SearchOption.TopDirectoryOnly);
                if (dicomFiles.Length == 0)
                    return;

                var reader = new DicomReader(path);
                reader.LoadGlobalView();

                using (var displayManager = new Services.DicomDisplayService(reader))
                {
                    // Display basic info
                    _infoLabel.Text = $"{Path.GetFileName(path)}\n" +
                                      $"Number of DICOM files: {dicomFiles.Length}\n" +
                                      $"Total size: {GetDirectorySize(path) / 1024.0 / 1024.0:F2} MB\n\n" +
                                      $"Patient ID: {displayManager.globalView.PatientID}\n" +
                                      $"Patient Name: {displayManager.globalView.PatientName}\n" +
                                      $"Patient Sex: {displayManager.globalView.PatientSex}\n" +
                                      $"Modality: {displayManager.globalView.Modality}\n" +
                                      $"Resolution: {displayManager.globalView.Rows} x {displayManager.globalView.Columns}";

                    // Show global view image
                    if (_globalViewPictureBox == null)
                    {
                        _globalViewPictureBox = new PictureBox
                        {
                            Dock = DockStyle.Fill,
                            SizeMode = PictureBoxSizeMode.Zoom
                        };

                        Panel rightPanel = (Panel)_viewDicomButton.Parent;
                        rightPanel.Controls.Add(_globalViewPictureBox);
                        _globalViewPictureBox.BringToFront();
                    }

                    _globalViewPictureBox.Image?.Dispose();
                    _globalViewPictureBox.Image = displayManager.GetGlobalViewImage();

                    // Enable view button
                    _viewDicomButton.Enabled = true;
                    _viewDicomButton.Tag = path;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading DICOM info: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error($"Error showing DICOM info for: {path}", ex);
            }
        }

        /// <summary>
        /// Gets the total size of DICOM files in the specified directory.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>The total size in bytes.</returns>
        private long GetDirectorySize(string path)
        {
            return Directory.GetFiles(path, "*.dcm")
                           .Sum(file => new FileInfo(file).Length);
        }

        /// <summary>
        /// Tries to find a default DICOM directory if the configured one doesn't exist.
        /// </summary>
        /// <returns>A path to a directory containing DICOM files, or the current directory if none found.</returns>
        private string FindDefaultDirectory()
        {
            // Possible locations to check
            string[] possibleLocations = {
                @"C:\dataset_chu_nice_2020_2021",
                @"D:\dataset_chu_nice_2020_2021",
                @"C:\DICOM",
                @"D:\DICOM",
                @"C:\Users\Public\Documents\DICOM"
            };

            foreach (var location in possibleLocations)
            {
                if (Directory.Exists(location))
                {
                    // Check if this directory or any subdirectory contains DICOM files
                    if (Directory.GetFiles(location, "*.dcm", SearchOption.AllDirectories).Length > 0)
                    {
                        return location;
                    }
                }
            }

            // Default to current directory if no suitable location found
            return Environment.CurrentDirectory;
        }

        #endregion
    }
}