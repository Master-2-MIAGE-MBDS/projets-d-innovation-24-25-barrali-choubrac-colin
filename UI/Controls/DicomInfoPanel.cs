using System;
using System.Drawing;
using System.Windows.Forms;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.Controls
{
    /// <summary>
    /// A custom control for displaying DICOM metadata information.
    /// </summary>
    public class DicomInfoPanel : Panel
    {
        #region Fields

        private TableLayoutPanel _patientInfoTable;
        private Label _sliceLocationLabel;
        private Label _windowSettingsLabel;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the DICOM metadata to display.
        /// </summary>
        public DicomMetadata Metadata { get; private set; }

        /// <summary>
        /// Gets or sets the current slice index.
        /// </summary>
        public int CurrentSliceIndex { get; set; }

        /// <summary>
        /// Gets or sets the total number of slices.
        /// </summary>
        public int TotalSlices { get; set; }

        /// <summary>
        /// Gets or sets the current window width.
        /// </summary>
        public int WindowWidth { get; set; }

        /// <summary>
        /// Gets or sets the current window center.
        /// </summary>
        public int WindowCenter { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DicomInfoPanel instance.
        /// </summary>
        public DicomInfoPanel()
        {
            InitializeComponents();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the panel with new DICOM metadata.
        /// </summary>
        /// <param name="metadata">The DICOM metadata to display.</param>
        public void UpdateMetadata(DicomMetadata metadata)
        {
            try
            {
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
                UpdatePatientInfo();
                UpdateSliceInfo();
                UpdateWindowSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update DICOM metadata display", ex);
            }
        }

        /// <summary>
        /// Updates just the slice information.
        /// </summary>
        public void UpdateSliceInfo()
        {
            if (Metadata == null)
                return;

            _sliceLocationLabel.Text = $"Slice: {CurrentSliceIndex + 1} of {TotalSlices}\n" +
                                      $"Location: {Metadata.SliceLocation:F2} mm\n" +
                                      $"Thickness: {Metadata.SliceThickness:F2} mm";
        }

        /// <summary>
        /// Updates just the window settings information.
        /// </summary>
        public void UpdateWindowSettings()
        {
            _windowSettingsLabel.Text = $"Window Width: {WindowWidth}\n" +
                                       $"Window Center: {WindowCenter}";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the control components.
        /// </summary>
        private void InitializeComponents()
        {
            // Panel settings
            Dock = DockStyle.Left;
            Width = 250;
            BackColor = SystemColors.Control;
            Padding = new Padding(5, 5, 5, 10);

            // Patient info table
            _patientInfoTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            // Add placeholder rows
            AddInfoRow(_patientInfoTable, "Patient ID", "");
            AddInfoRow(_patientInfoTable, "Patient Name", "");
            AddInfoRow(_patientInfoTable, "Patient Sex", "");
            AddInfoRow(_patientInfoTable, "Modality", "");
            AddInfoRow(_patientInfoTable, "Series", "");
            AddInfoRow(_patientInfoTable, "Resolution", "");

            // Slice location label
            _sliceLocationLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(3, 10, 3, 0),
                Text = "Slice: - of -\nLocation: - mm\nThickness: - mm"
            };

            // Window settings label
            _windowSettingsLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(3, 10, 3, 0),
                Text = "Window Width: -\nWindow Center: -"
            };

            // Add controls to panel
            Controls.Add(_windowSettingsLabel);
            Controls.Add(_sliceLocationLabel);
            Controls.Add(_patientInfoTable);
        }

        /// <summary>
        /// Adds an information row to the table.
        /// </summary>
        /// <param name="table">The table to add to.</param>
        /// <param name="label">The label text.</param>
        /// <param name="value">The value text.</param>
        private void AddInfoRow(TableLayoutPanel table, string label, string value)
        {
            var labelControl = new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(2),
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            var valueControl = new Label
            {
                Text = value,
                AutoSize = true,
                Margin = new Padding(2)
            };

            table.Controls.Add(labelControl);
            table.Controls.Add(valueControl);
        }

        /// <summary>
        /// Updates the patient information table with current metadata.
        /// </summary>
        private void UpdatePatientInfo()
        {
            if (Metadata == null)
                return;

            _patientInfoTable.SuspendLayout();

            // Update existing rows
            UpdateTableCellValue(0, Metadata.PatientID);
            UpdateTableCellValue(1, Metadata.PatientName);
            UpdateTableCellValue(2, Metadata.PatientSex);
            UpdateTableCellValue(3, Metadata.Modality);
            UpdateTableCellValue(4, Metadata.Series.ToString());
            UpdateTableCellValue(5, $"{Metadata.Rows} x {Metadata.Columns}");

            _patientInfoTable.ResumeLayout();
        }

        /// <summary>
        /// Updates a cell value in the table.
        /// </summary>
        /// <param name="rowIndex">The row index.</param>
        /// <param name="value">The new value.</param>
        private void UpdateTableCellValue(int rowIndex, string value)
        {
            // Each row has two cells (label and value)
            int cellIndex = rowIndex * 2 + 1; // +1 to get the value cell

            if (cellIndex < _patientInfoTable.Controls.Count)
            {
                Label label = _patientInfoTable.Controls[cellIndex] as Label;
                if (label != null)
                {
                    label.Text = value ?? "-";
                }
            }
        }

        #endregion
    }
}