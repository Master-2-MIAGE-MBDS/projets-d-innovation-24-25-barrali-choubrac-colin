using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Core.Rendering;
using DeepBridgeWindowsApp.Services;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.ViewModels
{
    /// <summary>
    /// ViewModel for 3D rendering of DICOM data.
    /// </summary>
    public class RenderingViewModel : DisposableBase
    {
        #region Fields

        private readonly DicomDisplayManager _displayManager;
        private readonly Dicom3D _dicom3D;
        private readonly string _patientDirectory;
        private readonly Rectangle? _carotidRect;

        // Camera properties
        private Vector3 _cameraPosition = new Vector3(0, 0, 3f);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitY;
        private float _rotationX = 0;
        private float _rotationY = 0;
        private float _zoom = 3.0f;

        // Slice properties
        private float _slicePositionX = 0;
        private float _slicePositionZ = 0;
        private float _angleYZ = 0;
        private float _angleXY = 0;
        private bool _showLiveSlice = false;

        // Clip planes
        private int _frontClip = 0;
        private int _backClip = 0;

        // Cancellation for slice updates
        private CancellationTokenSource _sliceUpdateCts;
        private Task _currentSliceTask;
        private readonly int _debounceMs = 150;

        // Event handlers
        public event EventHandler ViewChanged;
        public event EventHandler<ProcessingProgress> ProcessingProgressChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the camera position.
        /// </summary>
        public Vector3 CameraPosition
        {
            get => _cameraPosition;
            set
            {
                _cameraPosition = value;
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the camera target.
        /// </summary>
        public Vector3 CameraTarget
        {
            get => _cameraTarget;
            set
            {
                _cameraTarget = value;
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the camera up vector.
        /// </summary>
        public Vector3 CameraUp
        {
            get => _cameraUp;
            set
            {
                _cameraUp = value;
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the X rotation.
        /// </summary>
        public float RotationX
        {
            get => _rotationX;
            set
            {
                _rotationX = value;
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the Y rotation.
        /// </summary>
        public float RotationY
        {
            get => _rotationY;
            set
            {
                _rotationY = value;
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the zoom level.
        /// </summary>
        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.1f, value); // Prevent negative zoom
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the X position of the slice plane.
        /// </summary>
        public float SlicePositionX
        {
            get => _slicePositionX;
            set
            {
                _slicePositionX = Math.Max(-0.5f, Math.Min(0.5f, value));
                UpdateSlicePreview();
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the Z position of the slice plane.
        /// </summary>
        public float SlicePositionZ
        {
            get => _slicePositionZ;
            set
            {
                _slicePositionZ = Math.Max(-0.5f, Math.Min(0.5f, value));
                UpdateSlicePreview();
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the YZ plane angle in radians.
        /// </summary>
        public float AngleYZ
        {
            get => _angleYZ;
            set
            {
                _angleYZ = value;
                UpdateSlicePreview();
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the XY plane angle in radians.
        /// </summary>
        public float AngleXY
        {
            get => _angleXY;
            set
            {
                _angleXY = value;
                UpdateSlicePreview();
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether to show the live slice preview.
        /// </summary>
        public bool ShowLiveSlice
        {
            get => _showLiveSlice;
            set
            {
                _showLiveSlice = value;
                if (value)
                    UpdateSlicePreview();
                RaiseViewChanged();
            }
        }

        /// <summary>
        /// Gets or sets the front clip plane.
        /// </summary>
        public int FrontClip
        {
            get => _frontClip;
            set
            {
                if (_frontClip != value)
                {
                    _frontClip = value;
                    _dicom3D.SetClipPlanes(_frontClip, _backClip);
                    RaiseViewChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the back clip plane.
        /// </summary>
        public int BackClip
        {
            get => _backClip;
            set
            {
                if (_backClip != value)
                {
                    _backClip = value;
                    _dicom3D.SetClipPlanes(_frontClip, _backClip);
                    RaiseViewChanged();
                }
            }
        }

        /// <summary>
        /// Gets the Dicom3D instance.
        /// </summary>
        public Dicom3D Dicom3D => _dicom3D;

        /// <summary>
        /// Gets the patient directory.
        /// </summary>
        public string PatientDirectory => _patientDirectory;

        /// <summary>
        /// Gets the carotid rectangle.
        /// </summary>
        public Rectangle? CarotidRect => _carotidRect;

        /// <summary>
        /// Gets the total number of slices.
        /// </summary>
        public int TotalSlices => _displayManager.GetTotalSlices();

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new RenderingViewModel instance.
        /// </summary>
        /// <param name="displayManager">The DICOM display manager.</param>
        /// <param name="minSlice">The minimum slice index.</param>
        /// <param name="maxSlice">The maximum slice index.</param>
        /// <param name="patientDirectory">The patient directory.</param>
        /// <param name="carotidRect">Optional carotid rectangle.</param>
        public RenderingViewModel(
            DicomDisplayManager displayManager,
            int minSlice,
            int maxSlice,
            string patientDirectory,
            Rectangle? carotidRect = null)
        {
            _displayManager = displayManager ?? throw new ArgumentNullException(nameof(displayManager));
            _patientDirectory = patientDirectory ?? throw new ArgumentNullException(nameof(patientDirectory));
            _carotidRect = carotidRect;

            // Initialize slice position
            _slicePositionX = 0;
            _slicePositionZ = (float)(minSlice + (maxSlice - minSlice) / 2) / displayManager.GetTotalSlices() - 0.5f;

            // Create the 3D model
            _dicom3D = new Dicom3D(displayManager, minSlice, maxSlice, carotidRect, OnProcessingProgress);

            Logger.Info($"RenderingViewModel created with slices {minSlice} to {maxSlice}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the OpenGL resources.
        /// </summary>
        public void InitializeGL()
        {
            ThrowIfDisposed();
            _dicom3D.InitializeGL();
        }

        /// <summary>
        /// Renders the 3D model.
        /// </summary>
        /// <param name="shader">The shader program.</param>
        /// <param name="model">The model matrix.</param>
        /// <param name="view">The view matrix.</param>
        /// <param name="projection">The projection matrix.</param>
        public void Render(int shader, Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            ThrowIfDisposed();
            _dicom3D.Render(shader, model, view, projection);
        }

        /// <summary>
        /// Extracts a slice from the 3D model.
        /// </summary>
        /// <returns>A bitmap containing the extracted slice.</returns>
        public Bitmap ExtractSlice()
        {
            ThrowIfDisposed();
            return _dicom3D.ExtractSlice(_slicePositionX, _slicePositionZ, _angleYZ, _angleXY);
        }

        /// <summary>
        /// Saves the current slice to the patient directory.
        /// </summary>
        /// <returns>The path to the saved file.</returns>
        public string SaveSlice()
        {
            ThrowIfDisposed();

            try
            {
                var slice = ExtractSlice();
                var exporter = new DeepBridgeWindowsApp.Services.Exporting.SliceExporter();
                string sliceType = "custom";

                return exporter.ExportSliceToPatientDirectory(slice, _patientDirectory, sliceType);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save slice", ex);
                return null;
            }
        }

        /// <summary>
        /// Resets the camera to its default position.
        /// </summary>
        public void ResetCamera()
        {
            _cameraPosition = new Vector3(0, 0, 3f);
            _cameraTarget = Vector3.Zero;
            _cameraUp = Vector3.UnitY;
            _rotationX = 0;
            _rotationY = 0;
            _zoom = 3.0f;
            RaiseViewChanged();
        }

        /// <summary>
        /// Moves the camera by the specified delta values.
        /// </summary>
        /// <param name="deltaX">The X movement.</param>
        /// <param name="deltaY">The Y movement.</param>
        /// <param name="deltaZ">The Z movement.</param>
        public void MoveCamera(float deltaX, float deltaY, float deltaZ)
        {
            Vector3 viewDir = (CameraTarget - CameraPosition).Normalized();
            Vector3 right = Vector3.Cross(CameraUp, viewDir).Normalized();
            Vector3 up = Vector3.Cross(viewDir, right);

            Vector3 movement = right * -deltaX + up * deltaY + viewDir * -deltaZ;

            CameraPosition += movement;
            CameraTarget += movement;
        }

        /// <summary>
        /// Rotates the camera around its target.
        /// </summary>
        /// <param name="deltaX">The X rotation in degrees.</param>
        /// <param name="deltaY">The Y rotation in degrees.</param>
        public void RotateCamera(float deltaX, float deltaY)
        {
            Vector3 viewDir = (CameraTarget - CameraPosition).Normalized();
            Vector3 right = Vector3.Cross(CameraUp, viewDir).Normalized();
            Vector3 up = Vector3.Cross(viewDir, right);

            Quaternion rotX = Quaternion.FromAxisAngle(up, MathHelper.DegreesToRadians(deltaX));
            Quaternion rotY = Quaternion.FromAxisAngle(right, MathHelper.DegreesToRadians(deltaY));
            Quaternion rotation = rotX * rotY;

            CameraPosition = Vector3.Transform(CameraPosition - CameraTarget, rotation) + CameraTarget;
            CameraUp = Vector3.Transform(CameraUp, rotation);
        }

        /// <summary>
        /// Zooms the camera.
        /// </summary>
        /// <param name="delta">The zoom delta.</param>
        public void ZoomCamera(float delta)
        {
            float zoomFactor = 1.0f - (delta * 0.1f);
            Vector3 zoomDir = CameraPosition - CameraTarget;
            CameraPosition = CameraTarget + zoomDir * zoomFactor;
        }

        #endregion

        #region Private Methods

        private void RaiseViewChanged()
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnProcessingProgress(ProcessingProgress progress)
        {
            ProcessingProgressChanged?.Invoke(this, progress);
        }

        private async void UpdateSlicePreview()
        {
            if (!_showLiveSlice)
                return;

            if (_sliceUpdateCts != null)
            {
                _sliceUpdateCts.Cancel();
                _sliceUpdateCts.Dispose();
            }

            _sliceUpdateCts = new CancellationTokenSource();
            var token = _sliceUpdateCts.Token;

            try
            {
                // Wait for debounce period
                await Task.Delay(_debounceMs, token);

                if (_dicom3D == null)
                    return;

                _currentSliceTask = Task.Run(() => _dicom3D.ExtractSlice(_slicePositionX, _slicePositionZ, _angleYZ, _angleXY), token);

                // Wait for extraction to complete
                await _currentSliceTask;
            }
            catch (OperationCanceledException)
            {
                // Ignore - this is expected when canceling previous operations
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Cancel any pending operations
            if (_sliceUpdateCts != null)
            {
                _sliceUpdateCts.Cancel();
                _sliceUpdateCts.Dispose();
                _sliceUpdateCts = null;
            }

            // Wait for any current task to complete
            if (_currentSliceTask != null)
            {
                try
                {
                    _currentSliceTask.Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // Ignore exceptions during cleanup
                }

                _currentSliceTask = null;
            }

            // Dispose Dicom3D
            if (_dicom3D != null)
            {
                _dicom3D.Dispose();
            }

            base.DisposeManagedResources();
        }

        #endregion
    }
}