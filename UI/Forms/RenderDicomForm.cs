using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK.GLControl;
using OpenTK.Mathematics;
using DeepBridgeWindowsApp.Core.Rendering;
using DeepBridgeWindowsApp.Services;
using DeepBridgeWindowsApp.Services.Exporting;
using DeepBridgeWindowsApp.UI.ViewModels;
using DeepBridgeWindowsApp.UI.Rendering;
using DeepBridgeWindowsApp.Utils;
using System.Diagnostics;
using System.Linq;
using OpenTK.GLControl;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace DeepBridgeWindowsApp.UI.Forms
{
    /// <summary>
    /// Form for rendering 3D visualizations of DICOM data.
    /// </summary>
    public partial class RenderDicomForm : Form
    {
        #region Fields

        // Main objects
        private readonly RenderingViewModel _model;
        private readonly ShaderManager _shaderManager;
        private GLControl _glControl;

        // UI Controls
        private ProgressBar _progressBar;
        private Label _progressLabel;
        private Label _controlsHelpLabel;
        private TrackBar _frontClipTrackBar;
        private TrackBar _backClipTrackBar;
        private Label _frontClipLabel;
        private Label _backClipLabel;
        private Button _sliceButton;
        private PictureBox _slicePreview;
        private NumericUpDown _slicePositionX;
        private NumericUpDown _slicePositionZ;
        private CheckBox _showLiveSliceCheckBox;
        private NumericUpDown _angleYZInput;
        private NumericUpDown _angleXYInput;
        private Label _debugLabel;
        private SliceExporter _sliceExporter;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new RenderDicomForm instance.
        /// </summary>
        /// <param name="displayService">The DICOM display service.</param>
        /// <param name="minSlice">The minimum slice index to render.</param>
        /// <param name="maxSlice">The maximum slice index to render.</param>
        /// <param name="patientDirectory">The patient directory path.</param>
        /// <param name="carotidRect">Optional carotid rectangle.</param>
        public RenderDicomForm(
            DicomDisplayService displayService,
            int minSlice,
            int maxSlice,
            string patientDirectory,
            Rectangle? carotidRect = null)
        {
            // Create view model and supporting services
            _model = new RenderingViewModel(displayService, minSlice, maxSlice, patientDirectory, carotidRect);
            _shaderManager = new ShaderManager();
            _sliceExporter = new SliceExporter();

            // Initialize UI components
            InitializeComponents();
            InitializeKeyboardControls();

            // Register event handlers
            _model.ViewChanged += (s, e) => _glControl?.Invalidate();
            _model.ProcessingProgressChanged += OnProcessingProgressChanged;
        }

        #endregion

        #region Form Initialization

        /// <summary>
        /// Initializes the form's UI components.
        /// </summary>
        private void InitializeComponents()
        {
            // Form settings
            this.Size = new Size(1424, 768);
            this.Text = "3D DICOM Render";
            this.Icon = SystemIcons.Application;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Initialize panels and controls
            InitializeLeftPanel();
            InitializeGLControl();
            InitializeProgressBar();

            // Add controls to form
            this.Controls.Add(_glControl);
        }

        /// <summary>
        /// Initializes the left control panel.
        /// </summary>
        private void InitializeLeftPanel()
        {
            // Create left panel for controls
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Create flow panel for vertical layout
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            // Add controls help label
            _controlsHelpLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 9),
                Text = "Controls:\n\n" +
                       "WASD / Arrow Keys:\nMovement\n\n" +
                       "E/C:\nUp/Down\n\n" +
                       "Mouse Drag:\nRotation\n\n" +
                       "Mouse Wheel:\nZoom\n\n" +
                       "R:\nReset View\n\n"
            };
            flowPanel.Controls.Add(_controlsHelpLabel);

            // Add clip plane controls
            var clipPanel = CreateClipPlaneControls();
            flowPanel.Controls.Add(clipPanel);

            // Add slice controls
            _showLiveSliceCheckBox = new CheckBox
            {
                Text = "Show Live Slice",
                ForeColor = Color.White,
                AutoSize = true,
                Checked = false
            };
            _showLiveSliceCheckBox.CheckedChanged += (s, e) =>
            {
                _model.ShowLiveSlice = _showLiveSliceCheckBox.Checked;
                if (_model.ShowLiveSlice)
                {
                    // Initialize preview if needed
                    if (_slicePreview == null)
                    {
                        InitializeSlicePreview();
                    }
                    UpdateSlicePreview();
                }
                else if (_slicePreview != null)
                {
                    _slicePreview.Visible = false;
                }
            };
            flowPanel.Controls.Add(_showLiveSliceCheckBox);

            // X position control
            var slicePositionXLabel = new Label
            {
                Text = "X Position",
                ForeColor = Color.White,
                AutoSize = true
            };
            flowPanel.Controls.Add(slicePositionXLabel);

            _slicePositionX = new NumericUpDown
            {
                Minimum = -50,
                Maximum = 50,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = (decimal)_model.SlicePositionX * 100,
                Width = 120
            };
            _slicePositionX.ValueChanged += (s, e) => _model.SlicePositionX = (float)_slicePositionX.Value / 100f;
            flowPanel.Controls.Add(_slicePositionX);

            // Z position control
            var slicePositionZLabel = new Label
            {
                Text = "Z Position",
                ForeColor = Color.White,
                AutoSize = true
            };
            flowPanel.Controls.Add(slicePositionZLabel);

            _slicePositionZ = new NumericUpDown
            {
                Minimum = -50,
                Maximum = 50,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = (decimal)_model.SlicePositionZ * 100,
                Width = 120
            };
            _slicePositionZ.ValueChanged += (s, e) => _model.SlicePositionZ = (float)_slicePositionZ.Value / 100f;
            flowPanel.Controls.Add(_slicePositionZ);

            // YZ rotation control
            var angleYZLabel = new Label
            {
                Text = "YZ Rotation (degrees)",
                ForeColor = Color.White,
                AutoSize = true
            };
            flowPanel.Controls.Add(angleYZLabel);

            _angleYZInput = new NumericUpDown
            {
                Minimum = -180,
                Maximum = 180,
                Value = 90,
                DecimalPlaces = 1,
                Increment = 1m,
                Width = 120
            };
            _angleYZInput.ValueChanged += (s, e) =>
                _model.AngleYZ = (float)((double)_angleYZInput.Value * Math.PI / 180.0);
            flowPanel.Controls.Add(_angleYZInput);

            // XY rotation control
            var angleXYLabel = new Label
            {
                Text = "XY Rotation (degrees)",
                ForeColor = Color.White,
                AutoSize = true
            };
            flowPanel.Controls.Add(angleXYLabel);

            _angleXYInput = new NumericUpDown
            {
                Minimum = -180,
                Maximum = 180,
                Value = 0,
                DecimalPlaces = 1,
                Increment = 1m,
                Width = 120
            };
            _angleXYInput.ValueChanged += (s, e) =>
                _model.AngleXY = (float)((double)_angleXYInput.Value * Math.PI / 180.0);
            flowPanel.Controls.Add(_angleXYInput);

            // Add slice button
            _sliceButton = new Button
            {
                Text = "Save Slice",
                ForeColor = Color.Black,
                BackColor = SystemColors.Control,
                Dock = DockStyle.Bottom,
                Height = 30,
                Margin = new Padding(10)
            };
            _sliceButton.Click += SaveSliceButton_Click;

            // Add debug label
            _debugLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(3),
                Location = new Point(this.ClientSize.Width - 120, this.ClientSize.Height - 25)
            };
            this.Controls.Add(_debugLabel);

            // Update debug label position on resize
            this.Resize += (s, e) =>
            {
                _debugLabel.Location = new Point(
                    this.ClientSize.Width - _debugLabel.Width - 10,
                    this.ClientSize.Height - _debugLabel.Height - 10);
            };

            // Add controls to form
            leftPanel.Controls.Add(flowPanel);
            leftPanel.Controls.Add(_sliceButton);
            this.Controls.Add(leftPanel);
        }

        /// <summary>
        /// Creates the clip plane control panel.
        /// </summary>
        private Panel CreateClipPlaneControls()
        {
            var clipPanel = new Panel
            {
                Width = 190,
                Height = 120,
                BackColor = Color.FromArgb(50, 50, 50),
                Margin = new Padding(0, 5, 0, 5)
            };

            var clipLabel = new Label
            {
                Text = "Clipping Planes:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(5, 5)
            };
            clipPanel.Controls.Add(clipLabel);

            _frontClipTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = _model.TotalSlices - 1,
                Value = _model.FrontClip,
                Location = new Point(10, 25),
                Width = 170,
                TickStyle = TickStyle.None
            };
            _frontClipTrackBar.ValueChanged += (s, e) =>
            {
                // Ensure front clip doesn't overlap with back clip
                if (_frontClipTrackBar.Value + _backClipTrackBar.Value >= _model.TotalSlices)
                {
                    _frontClipTrackBar.Value = _model.TotalSlices - 1 - _backClipTrackBar.Value;
                }

                _model.FrontClip = _frontClipTrackBar.Value;
                _frontClipLabel.Text = $"Front Clip: {_model.FrontClip}";
            };

            _backClipTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = _model.TotalSlices - 1,
                Value = _model.BackClip,
                Location = new Point(10, 70),
                Width = 170,
                TickStyle = TickStyle.None
            };
            _backClipTrackBar.ValueChanged += (s, e) =>
            {
                // Ensure back clip doesn't overlap with front clip
                if (_frontClipTrackBar.Value + _backClipTrackBar.Value >= _model.TotalSlices)
                {
                    _backClipTrackBar.Value = _model.TotalSlices - 1 - _frontClipTrackBar.Value;
                }

                _model.BackClip = _backClipTrackBar.Value;
                _backClipLabel.Text = $"Back Clip: {_model.BackClip}";
            };

            // Add labels for clip values
            _frontClipLabel = new Label
            {
                Text = $"Front Clip: {_model.FrontClip}",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 50)
            };

            _backClipLabel = new Label
            {
                Text = $"Back Clip: {_model.BackClip}",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 95)
            };

            clipPanel.Controls.Add(_frontClipTrackBar);
            clipPanel.Controls.Add(_backClipTrackBar);
            clipPanel.Controls.Add(_frontClipLabel);
            clipPanel.Controls.Add(_backClipLabel);

            return clipPanel;
        }

        /// <summary>
        /// Initializes the OpenGL control.
        /// </summary>
        private void InitializeGLControl()
        {
            _glControl = new GLControl
            {
                Dock = DockStyle.Fill
            };

            // Register event handlers
            _glControl.Load += GLControl_Load;
            _glControl.Resize += GLControl_Resize;
            _glControl.Paint += GLControl_Paint;

            // Register mouse handlers
            _glControl.MouseDown += GLControl_MouseDown;
            _glControl.MouseUp += GLControl_MouseUp;
            _glControl.MouseMove += GLControl_MouseMove;
            _glControl.MouseWheel += GLControl_MouseWheel;

            // Set focus on OpenGL control
            _glControl.Focus();
        }

        /// <summary>
        /// Initializes the progress bar.
        /// </summary>
        private void InitializeProgressBar()
        {
            _progressBar = new ProgressBar
            {
                Width = 300,
                Height = 23,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            _progressLabel = new Label
            {
                AutoSize = true,
                Width = 300,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            _progressBar.Location = new Point(
                (this.ClientSize.Width - _progressBar.Width) / 2,
                (this.ClientSize.Height - _progressBar.Height) / 2
            );

            _progressLabel.Location = new Point(
                (this.ClientSize.Width - _progressLabel.Width) / 2,
                _progressBar.Location.Y - 25
            );

            this.Controls.Add(_progressBar);
            this.Controls.Add(_progressLabel);
        }

        /// <summary>
        /// Initializes keyboard control handlers.
        /// </summary>
        private void InitializeKeyboardControls()
        {
            this.KeyPreview = true;
            this.KeyDown += RenderDicomForm_KeyDown;
            this.KeyUp += RenderDicomForm_KeyUp;
            this.Activated += (s, e) => _glControl?.Focus();
        }

        /// <summary>
        /// Initializes the slice preview control.
        /// </summary>
        private void InitializeSlicePreview()
        {
            _slicePreview = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 300,
                Height = 300,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Black,
                Visible = false
            };

            // Position in top-right corner
            _slicePreview.Location = new Point(
                this.ClientSize.Width - _slicePreview.Width - 20,
                20
            );

            // Create context menu for saving
            _slicePreview.ContextMenuStrip = new ContextMenuStrip();
            var saveMenuItem = new ToolStripMenuItem("Save Slice");
            saveMenuItem.Click += SaveSliceButton_Click;
            _slicePreview.ContextMenuStrip.Items.Add(saveMenuItem);

            // Add to form and bring to front
            this.Controls.Add(_slicePreview);
            _slicePreview.BringToFront();
        }

        #endregion

        #region OpenGL Event Handlers

        private void GLControl_Load(object sender, EventArgs e)
        {
            // Show progress indicator
            ShowProgress(true);

            try
            {
                // Initialize OpenGL
                _glControl.MakeCurrent();
                GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                // Initialize the 3D model in a background task
                Task.Run(() =>
                {
                    try
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            // Initialize shaders
                            _shaderManager.CreateShaderProgram("point",
                                "#version 330 core\n" +
                                "layout(location = 0) in vec3 aPosition;\n" +
                                "layout(location = 1) in vec3 aColor;\n" +
                                "out vec3 vertexColor;\n" +
                                "uniform mat4 model;\n" +
                                "uniform mat4 view;\n" +
                                "uniform mat4 projection;\n" +
                                "void main()\n" +
                                "{\n" +
                                "    gl_Position = projection * view * model * vec4(aPosition, 1.0);\n" +
                                "    vertexColor = aColor;\n" +
                                "}",

                                "#version 330 core\n" +
                                "in vec3 vertexColor;\n" +
                                "out vec4 FragColor;\n" +
                                "void main()\n" +
                                "{\n" +
                                "    FragColor = vec4(vertexColor, 1.0);\n" +
                                "}"
                            );
                        });

                        // Initialize 3D model (may take some time)
                        _model.InitializeGL();

                        this.Invoke((MethodInvoker)delegate
                        {
                            // Hide progress and enable rendering
                            ShowProgress(false);
                            _glControl.Invalidate();
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            ShowProgress(false);
                            MessageBox.Show($"Error initializing 3D model: {ex.Message}",
                                "Initialization Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            Logger.Error("Failed to initialize 3D model", ex);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show($"Error initializing OpenGL: {ex.Message}",
                    "OpenGL Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Logger.Error("Failed to initialize OpenGL", ex);
            }
        }

        private void GLControl_Resize(object sender, EventArgs e)
        {
            if (_glControl == null) return;

            try
            {
                _glControl.MakeCurrent();
                GL.Viewport(0, 0, _glControl.ClientSize.Width, _glControl.ClientSize.Height);
                _glControl.Invalidate();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in GLControl_Resize", ex);
            }
        }

        private void GLControl_Paint(object sender, PaintEventArgs e)
        {
            if (_glControl == null) return;

            try
            {
                _glControl.MakeCurrent();
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // Calculate view matrices
                float aspect = (float)_glControl.ClientSize.Width / _glControl.ClientSize.Height;
                Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                    MathHelper.PiOver4, aspect, 0.1f, 100f);
                Matrix4 view = Matrix4.LookAt(
                    _model.CameraPosition,
                    _model.CameraTarget,
                    _model.CameraUp);
                Matrix4 model = Matrix4.Identity;

                // Apply model transformations
                model *= Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_model.RotationX));
                model *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_model.RotationY));

                // Render the 3D model
                _model.Render(_shaderManager.GetShaderProgram("point"), model, view, projection);

                // Draw bounding box and slice indicator
                DrawBoundingBox(model, view, projection);
                if (_model.ShowLiveSlice)
                {
                    DrawSliceIndicator(model, view, projection);
                }

                _glControl.SwapBuffers();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in GLControl_Paint", ex);
            }
        }

        private void DrawBoundingBox(Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            GL.UseProgram(_shaderManager.GetShaderProgram("color"));

            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "model"),
                false, ref model);
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "view"),
                false, ref view);
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "projection"),
                false, ref projection);

            GL.Uniform3(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "color"),
                0.8f, 0.2f, 0.2f); // Red color

            GL.Begin(PrimitiveType.Lines);

            // Bottom face
            GL.Vertex3(-0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, -0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, -0.5f, 0.5f);
            GL.Vertex3(0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, -0.5f, 0.5f);
            GL.Vertex3(-0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, -0.5f, -0.5f);

            // Top face
            GL.Vertex3(-0.5f, 0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, 0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, 0.5f);
            GL.Vertex3(0.5f, 0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, 0.5f);
            GL.Vertex3(-0.5f, 0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, -0.5f);

            // Vertical edges
            GL.Vertex3(-0.5f, -0.5f, -0.5f); GL.Vertex3(-0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, -0.5f); GL.Vertex3(0.5f, 0.5f, -0.5f);
            GL.Vertex3(0.5f, -0.5f, 0.5f); GL.Vertex3(0.5f, 0.5f, 0.5f);
            GL.Vertex3(-0.5f, -0.5f, 0.5f); GL.Vertex3(-0.5f, 0.5f, 0.5f);

            GL.End();
        }

        private void DrawSliceIndicator(Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            GL.UseProgram(_shaderManager.GetShaderProgram("color"));

            // Get normalized positions
            float normalizedX = _model.SlicePositionX;
            float normalizedZ = _model.SlicePositionZ;

            // Create rotations
            Quaternion rotationZ = Quaternion.FromAxisAngle(Vector3.UnitZ, _model.AngleXY);
            Quaternion rotationY = Quaternion.FromAxisAngle(Vector3.UnitY, _model.AngleYZ);
            Quaternion combinedRotation = rotationY * rotationZ;

            // Create rotation matrix
            Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(combinedRotation);

            // Apply model transformation first, then rotation, then translation
            Matrix4 sliceModel = model * rotationMatrix * Matrix4.CreateTranslation(normalizedX, 0, normalizedZ);

            // Set uniforms
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "model"),
                false, ref sliceModel);
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "view"),
                false, ref view);
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "projection"),
                false, ref projection);

            // Set color (semi-transparent red)
            GL.Uniform3(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "color"),
                1.0f, 0.3f, 0.3f);

            // Draw the slice plane
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(0f, 0.5f, 0.5f);
            GL.Vertex3(0f, -0.5f, 0.5f);
            GL.Vertex3(0f, -0.5f, -0.5f);
            GL.Vertex3(0f, 0.5f, -0.5f);
            GL.End();

            // Draw coordinate axes
            float axisLength = 0.2f;
            GL.LineWidth(2.0f);

            GL.Begin(PrimitiveType.Lines);

            // X axis (Normal) - Red
            GL.Uniform3(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "color"),
                1.0f, 0.0f, 0.0f);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(axisLength, 0, 0);

            // Y axis - Green
            GL.Uniform3(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "color"),
                0.0f, 1.0f, 0.0f);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, axisLength, 0);

            // Z axis - Blue
            GL.Uniform3(
                GL.GetUniformLocation(_shaderManager.GetShaderProgram("color"), "color"),
                0.0f, 0.0f, 1.0f);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 0, axisLength);

            GL.End();
            GL.LineWidth(1.0f);
        }

        #endregion

        #region Mouse Event Handlers

        private bool _isMouseDown = false;
        private Point _lastMousePos;

        private void GLControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isMouseDown = true;
                _lastMousePos = e.Location;
            }
        }

        private void GLControl_MouseUp(object sender, MouseEventArgs e)
        {
            _isMouseDown = false;
        }

        private void GLControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown) return;

            // Calculate rotation deltas
            float deltaX = (e.X - _lastMousePos.X) * 0.5f;
            float deltaY = (e.Y - _lastMousePos.Y) * 0.5f;

            // Apply rotation to the model
            _model.RotateCamera(deltaX, deltaY);

            // Update view direction for debug display
            Vector3 viewDir = (_model.CameraTarget - _model.CameraPosition).Normalized();
            float angleX = MathHelper.RadiansToDegrees((float)Math.Atan2(viewDir.Y, Math.Sqrt(viewDir.X * viewDir.X + viewDir.Z * viewDir.Z)));
            float angleY = MathHelper.RadiansToDegrees((float)Math.Atan2(viewDir.X, viewDir.Z));
            float angleZ = MathHelper.RadiansToDegrees((float)Math.Atan2(_model.CameraUp.X, _model.CameraUp.Y));

            // Update debug info
            _debugLabel.Text = $"X: {angleX:F1}° | Y: {angleY:F1}° | Z: {angleZ:F1}°";

            _lastMousePos = e.Location;
        }

        private void GLControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // Apply zoom to the model
            _model.ZoomCamera(e.Delta * 0.001f);
        }

        #endregion

        #region Keyboard Event Handlers

        private readonly HashSet<Keys> _pressedKeys = new HashSet<Keys>();
        private System.Windows.Forms.Timer _moveTimer;
        private readonly float _moveSpeed = 0.1f;

        private void RenderDicomForm_KeyDown(object sender, KeyEventArgs e)
        {
            _pressedKeys.Add(e.KeyCode);

            if (_moveTimer == null || !_moveTimer.Enabled)
            {
                // Start timer for smooth movement
                _moveTimer = new System.Windows.Forms.Timer
                {
                    Interval = 16, // ~60 FPS
                    Enabled = true
                };
                _moveTimer.Tick += MoveTimer_Tick;
            }

            // Handle individual key presses
            if (e.KeyCode == Keys.R)
            {
                // Reset camera
                _model.ResetCamera();
            }
        }

        private void RenderDicomForm_KeyUp(object sender, KeyEventArgs e)
        {
            _pressedKeys.Remove(e.KeyCode);

            // Stop timer if no keys pressed
            if (_pressedKeys.Count == 0 && _moveTimer != null)
            {
                _moveTimer.Stop();
            }
        }

        private void MoveTimer_Tick(object sender, EventArgs e)
        {
            bool moved = false;
            float deltaX = 0, deltaY = 0, deltaZ = 0;

            // Handle WASD/Arrow keys for movement
            if (_pressedKeys.Contains(Keys.W) || _pressedKeys.Contains(Keys.Up))
            {
                deltaZ -= _moveSpeed;
                moved = true;
            }
            if (_pressedKeys.Contains(Keys.S) || _pressedKeys.Contains(Keys.Down))
            {
                deltaZ += _moveSpeed;
                moved = true;
            }
            if (_pressedKeys.Contains(Keys.A) || _pressedKeys.Contains(Keys.Left))
            {
                deltaX -= _moveSpeed;
                moved = true;
            }
            if (_pressedKeys.Contains(Keys.D) || _pressedKeys.Contains(Keys.Right))
            {
                deltaX += _moveSpeed;
                moved = true;
            }

            // Handle E/C for up/down movement
            if (_pressedKeys.Contains(Keys.E))
            {
                deltaY += _moveSpeed;
                moved = true;
            }
            if (_pressedKeys.Contains(Keys.C))
            {
                deltaY -= _moveSpeed;
                moved = true;
            }

            if (moved)
            {
                // Apply movement to model
                _model.MoveCamera(deltaX, deltaY, deltaZ);
            }
        }

        #endregion

        #region Slice and Progress Methods

        private void UpdateSlicePreview()
        {
            if (!_model.ShowLiveSlice) return;

            Task.Run(async () =>
            {
                try
                {
                    var slice = _model.ExtractSlice();

                    if (slice != null)
                    {
                        await this.InvokeAsync(() =>
                        {
                            // Dispose previous image
                            if (_slicePreview.Image != null)
                            {
                                var oldImage = _slicePreview.Image;
                                _slicePreview.Image = null;
                                oldImage.Dispose();
                            }

                            // Calculate preview size
                            int maxPreviewDimension = 300;
                            float scale = Math.Min(
                                (float)maxPreviewDimension / slice.Width,
                                (float)maxPreviewDimension / slice.Height);

                            int previewWidth = (int)(slice.Width * scale);
                            int previewHeight = (int)(slice.Height * scale);

                            // Update preview control
                            _slicePreview.Width = previewWidth;
                            _slicePreview.Height = previewHeight;
                            _slicePreview.Location = new Point(
                                this.ClientSize.Width - _slicePreview.Width - 10,
                                10);

                            _slicePreview.Image = slice;
                            _slicePreview.Visible = true;

                            // Add resolution label
                            AddResolutionLabel(slice.Width, slice.Height);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error updating slice preview", ex);
                }
            });
        }

        private async Task InvokeAsync(Action action)
        {
            if (this.InvokeRequired)
            {
                await Task.Run(() => this.Invoke(action));
            }
            else
            {
                action();
            }
        }

        private void AddResolutionLabel(int width, int height)
        {
            // Find or create resolution label
            var resolutionLabel = this.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Tag?.ToString() == "ResolutionLabel");

            if (resolutionLabel == null)
            {
                resolutionLabel = new Label
                {
                    AutoSize = true,
                    BackColor = Color.FromArgb(100, 0, 0, 0),
                    ForeColor = Color.White,
                    Padding = new Padding(5),
                    Tag = "ResolutionLabel"
                };
                this.Controls.Add(resolutionLabel);
            }

            // Update label
            resolutionLabel.Text = $"Resolution: {width} x {height}";
            resolutionLabel.Location = new Point(
                _slicePreview.Left + 5,
                _slicePreview.Bottom - resolutionLabel.Height - 5);

            resolutionLabel.BringToFront();
        }

        private void SaveSliceButton_Click(object sender, EventArgs e)
        {
            if (_model.ShowLiveSlice && _slicePreview.Image != null)
            {
                try
                {
                    Cursor = Cursors.WaitCursor;

                    string filePath = _sliceExporter.ExportSliceToPatientDirectory(
                        (Bitmap)_slicePreview.Image,
                        _model.PatientDirectory,
                        "custom");

                    Cursor = Cursors.Default;

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        MessageBox.Show($"Slice saved to:\n{filePath}",
                            "Save Successful",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to save slice.",
                            "Save Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageBox.Show($"Error saving slice: {ex.Message}",
                        "Save Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Logger.Error("Error saving slice", ex);
                }
            }
            else
            {
                // Extract and save a new slice
                try
                {
                    Cursor = Cursors.WaitCursor;

                    var slice = _model.ExtractSlice();
                    string filePath = _sliceExporter.ExportSliceToPatientDirectory(
                        slice,
                        _model.PatientDirectory,
                        "custom");

                    Cursor = Cursors.Default;

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        MessageBox.Show($"Slice saved to:\n{filePath}",
                            "Save Successful",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to save slice.",
                            "Save Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    // Clean up
                    slice.Dispose();
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageBox.Show($"Error saving slice: {ex.Message}",
                        "Save Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Logger.Error("Error saving slice", ex);
                }
            }
        }

        private void OnProcessingProgressChanged(object sender, ProcessingProgress progress)
        {
            try
            {
                if (!IsDisposed && !Disposing)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            _progressBar.Value = (int)progress.Percentage;
                            _progressLabel.Text = $"{progress.CurrentStep} - {progress.CurrentValue} of {progress.TotalValue} slices ({progress.Percentage:F1}%)";
                        }
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                // Ignore - form is being disposed
                Debug.WriteLine("Form disposed during progress update");
            }
            catch (InvalidOperationException)
            {
                // Ignore - form is being disposed
                Debug.WriteLine("Form disposed during progress update");
            }
        }

        private void ShowProgress(bool visible)
        {
            _progressBar.Visible = visible;
            _progressLabel.Visible = visible;

            if (visible)
            {
                _progressBar.Value = 0;
                _progressBar.Maximum = 100;
                _progressBar.Minimum = 0;

                // Center progress bar
                _progressBar.Location = new Point(
                    (this.ClientSize.Width - _progressBar.Width) / 2,
                    (this.ClientSize.Height - _progressBar.Height) / 2);

                _progressLabel.Location = new Point(
                    (this.ClientSize.Width - _progressLabel.Width) / 2,
                    _progressBar.Location.Y - 25);
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
                _shaderManager?.Dispose();

                // Dispose timers
                if (_moveTimer != null)
                {
                    _moveTimer.Stop();
                    _moveTimer.Dispose();
                    _moveTimer = null;
                }

                // Dispose controls
                if (_slicePreview?.Image != null)
                {
                    _slicePreview.Image.Dispose();
                    _slicePreview.Image = null;
                }

                // Dispose GL control
                if (_glControl != null)
                {
                    _glControl.Dispose();
                    _glControl = null;
                }

                // Clear collections
                _pressedKeys.Clear();
            }

            base.Dispose(disposing);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop any active processes
            if (_moveTimer != null)
            {
                _moveTimer.Stop();
            }

            // Detach event handlers
            if (_glControl != null)
            {
                _glControl.Paint -= GLControl_Paint;
                _glControl.Resize -= GLControl_Resize;
                _glControl.Load -= GLControl_Load;
                _glControl.MouseDown -= GLControl_MouseDown;
                _glControl.MouseUp -= GLControl_MouseUp;
                _glControl.MouseMove -= GLControl_MouseMove;
                _glControl.MouseWheel -= GLControl_MouseWheel;
            }

            // Detach model event handlers
            if (_model != null)
            {
                _model.ViewChanged -= (s, e) => _glControl?.Invalidate();
                _model.ProcessingProgressChanged -= OnProcessingProgressChanged;
            }

            base.OnFormClosing(e);
        }

        #endregion
    }
}