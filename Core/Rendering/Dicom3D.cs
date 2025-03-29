using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using DeepBridgeWindowsApp.Core.DicomData;
using DeepBridgeWindowsApp.Services;
using DeepBridgeWindowsApp.Utils;
using System.Threading;

namespace DeepBridgeWindowsApp.Core.Rendering
{
    /// <summary>
    /// Handles 3D rendering of DICOM data using OpenGL.
    /// </summary>
    public class Dicom3D : DisposableBase
    {
        #region Fields

        // Rendering data
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _colors = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();

        // OpenGL buffers
        private int[] _vertexBufferObject;
        private int[] _colorBufferObject;
        private int[] _elementBufferObject;
        private int _vertexArrayObject;

        // Callback for progress reporting
        private readonly Action<ProcessingProgress> _progressCallback;

        // Threading synchronization
        private readonly object _lockObject = new object();

        // Clipping planes
        private int _frontClip = 0;
        private int _backClip = 0;
        private int _totalSlices;
        private int _currentVisibleIndices;

        // Slicing
        private readonly Dictionary<Vector3, Vector3> _pointColors = new Dictionary<Vector3, Vector3>();
        private int _sliceWidth;
        private int _sliceHeight;
        private bool _isOpenGLInitialized = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of vertices in the 3D model.
        /// </summary>
        public int VertexCount => _vertices.Count;

        /// <summary>
        /// Gets the number of indices in the 3D model.
        /// </summary>
        public int IndexCount => _indices.Count;

        /// <summary>
        /// Gets the number of visible indices after clipping.
        /// </summary>
        public int VisibleIndices => _currentVisibleIndices;

        /// <summary>
        /// Gets the total number of slices in the DICOM dataset.
        /// </summary>
        public int TotalSlices => _totalSlices;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Dicom3D rendering instance from DICOM data.
        /// </summary>
        /// <param name="ddm">The DICOM display manager containing the data.</param>
        /// <param name="minSlice">The minimum slice index to process.</param>
        /// <param name="maxSlice">The maximum slice index to process.</param>
        /// <param name="carotidRect">Optional rectangle defining carotid region of interest.</param>
        /// <param name="progressCallback">Optional callback for reporting processing progress.</param>
        public Dicom3D(DicomDisplayService ddm, int minSlice, int maxSlice, Rectangle? carotidRect = null, Action<ProcessingProgress> progressCallback = null)
        {
            _progressCallback = progressCallback;
            _totalSlices = ddm.GetTotalSlices();

            Logger.Info($"Creating 3D model from slices {minSlice} to {maxSlice}", "Dicom3D");

            DebugHelper.MeasureExecutionTime(() =>
            {
                ProcessSlices(ddm, minSlice, maxSlice, carotidRect);
            }, "ProcessSlices");

            _currentVisibleIndices = _indices.Count;

            Logger.Info($"Created 3D model with {_vertices.Count} vertices and {_indices.Count} indices", "Dicom3D");
        }

        #endregion

        #region Processing Methods

        /// <summary>
        /// Processes DICOM slices to create a 3D point cloud.
        /// </summary>
        /// <param name="ddm">The DICOM display manager containing the data.</param>
        /// <param name="minSlice">The minimum slice index to process.</param>
        /// <param name="maxSlice">The maximum slice index to process.</param>
        /// <param name="carotidRect">Optional rectangle defining carotid region of interest.</param>
        private void ProcessSlices(DicomDisplayService ddm, int minSlice, int maxSlice, Rectangle? carotidRect = null)
        {
            // Get pixel spacing (in mm)
            var pixelSpacing = ddm.GetSlice(0).PixelSpacing;
            float pixelSpacingX = (float)pixelSpacing;
            float pixelSpacingY = (float)pixelSpacing;

            // Get physical dimensions of a slice in mm
            var firstSlice = ddm.GetCurrentSliceImage();
            float physicalWidth = firstSlice.Width * pixelSpacingX;
            float physicalHeight = firstSlice.Height * pixelSpacingY;

            // Store slice dimensions
            _sliceWidth = firstSlice.Width;
            _sliceHeight = firstSlice.Height;

            // Calculate z-axis physical dimensions
            float firstSliceLocation = (float)ddm.GetSlice(0).SliceLocation;
            float lastSliceLocation = (float)ddm.GetSlice(ddm.GetTotalSlices() - 1).SliceLocation;
            float totalPhysicalDepth = Math.Abs(lastSliceLocation - firstSliceLocation);

            // Calculate scaling factors
            float maxDimension = Math.Max(Math.Max(physicalWidth, physicalHeight), totalPhysicalDepth);
            float scaleX = physicalWidth / maxDimension;
            float scaleY = physicalHeight / maxDimension;
            float scaleZ = totalPhysicalDepth / maxDimension;

            // Clean up first slice
            firstSlice.Dispose();

            // Set up progress tracking
            var slicesToProcess = maxSlice - minSlice;
            var progress = new ProcessingProgress
            {
                TotalValue = slicesToProcess,
                CurrentValue = 0,
                CurrentStep = "Processing DICOM slices"
            };

            // Process slices in parallel
            Parallel.For(minSlice, maxSlice, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, z =>
            {
                ProcessSingleSlice(ddm, z, carotidRect, new ProcessingContext
                {
                    FirstSliceLocation = firstSliceLocation,
                    TotalPhysicalDepth = totalPhysicalDepth,
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    ScaleZ = scaleZ,
                    PhysicalWidth = physicalWidth,
                    PhysicalHeight = physicalHeight
                });

                // Update progress
                //int completedSlices = Interlocked.Increment(ref progress.CurrentValue);
                _progressCallback?.Invoke(progress);
            });
        }

        /// <summary>
        /// Processing context containing scaling and dimension information.
        /// </summary>
        private class ProcessingContext
        {
            public float FirstSliceLocation { get; set; }
            public float TotalPhysicalDepth { get; set; }
            public float ScaleX { get; set; }
            public float ScaleY { get; set; }
            public float ScaleZ { get; set; }
            public float PhysicalWidth { get; set; }
            public float PhysicalHeight { get; set; }
        }

        /// <summary>
        /// Processes a single DICOM slice to extract point data.
        /// </summary>
        private void ProcessSingleSlice(DicomDisplayService ddm, int z, Rectangle? carotidRect, ProcessingContext context)
        {
            var localVertices = new List<Vector3>();
            var localColors = new List<Vector3>();
            var localIndices = new List<int>();

            // Get the slice data
            ddm.SetSliceIndex(z);
            var slice = ddm.GetCurrentSliceImage();
            float currentSliceLocation = (float)ddm.GetSlice(z).SliceLocation;

            // Calculate z position with explicit steps
            float normalizedZ = ((currentSliceLocation - context.FirstSliceLocation) / context.TotalPhysicalDepth) - 0.5f;
            float finalZ = normalizedZ * context.ScaleZ;

            BitmapData bitmapData = null;
            try
            {
                bitmapData = slice.LockBits(
                    new Rectangle(0, 0, slice.Width, slice.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0;

                    for (int y = 0; y < slice.Height; y++)
                    {
                        for (int x = 0; x < slice.Width; x++)
                        {
                            // Skip pixels outside the carotid rectangle if specified
                            if (carotidRect.HasValue)
                            {
                                if (x < carotidRect.Value.X || x > carotidRect.Value.Right ||
                                    y < carotidRect.Value.Y || y > carotidRect.Value.Bottom)
                                {
                                    continue;
                                }
                            }

                            // Calculate pixel offset and get RGB values
                            int offset = y * bitmapData.Stride + x * 4;
                            byte b = ptr[offset];
                            byte g = ptr[offset + 1];
                            byte r = ptr[offset + 2];

                            // Calculate intensity (grayscale)
                            float intensity = (r * 0.299f + g * 0.587f + b * 0.114f) / 255f;

                            // Only add points above a certain intensity threshold
                            if (intensity > 0.15f)
                            {
                                // Convert to physical coordinates
                                float physicalX = (x * context.ScaleX / _sliceWidth);
                                float physicalY = (y * context.ScaleY / _sliceHeight);

                                // Create the vertex in normalized coordinates
                                Vector3 vertex = new Vector3(
                                    ((physicalX / context.PhysicalWidth) - 0.5f) * context.ScaleX,
                                    ((physicalY / context.PhysicalHeight) - 0.5f) * context.ScaleY,
                                    finalZ
                                );

                                // Create color vector (grayscale)
                                Vector3 color = new Vector3(intensity, intensity, intensity);

                                // Store the point and color globally for later slice extraction
                                lock (_lockObject)
                                {
                                    _pointColors[vertex] = color;
                                }

                                // Add to local collections
                                localVertices.Add(vertex);
                                localColors.Add(color);
                                localIndices.Add(localVertices.Count - 1);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (bitmapData != null)
                {
                    slice.UnlockBits(bitmapData);
                }
                slice.Dispose();
            }

            // Add local collections to the global collections with proper index offset
            lock (_lockObject)
            {
                int baseIndex = _vertices.Count;
                _vertices.AddRange(localVertices);
                _colors.AddRange(localColors);
                _indices.AddRange(localIndices.Select(i => i + baseIndex));
            }
        }

        #endregion

        #region OpenGL Methods

        /// <summary>
        /// Initializes OpenGL resources for rendering.
        /// </summary>
        public void InitializeGL()
        {
            if (_isOpenGLInitialized)
            {
                Logger.Warning("OpenGL resources already initialized.", "Dicom3D");
                return;
            }

            try
            {
                _vertexBufferObject = new int[1];
                _colorBufferObject = new int[1];
                _elementBufferObject = new int[1];
                _vertexArrayObject = GL.GenVertexArray();
                GL.GenBuffers(1, _vertexBufferObject);
                GL.GenBuffers(1, _colorBufferObject);
                GL.GenBuffers(1, _elementBufferObject);

                GL.BindVertexArray(_vertexArrayObject);

                // Upload vertex positions
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject[0]);
                GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Count * Vector3.SizeInBytes, _vertices.ToArray(), BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
                GL.EnableVertexAttribArray(0);

                // Upload vertex colors
                GL.BindBuffer(BufferTarget.ArrayBuffer, _colorBufferObject[0]);
                GL.BufferData(BufferTarget.ArrayBuffer, _colors.Count * Vector3.SizeInBytes, _colors.ToArray(), BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
                GL.EnableVertexAttribArray(1);

                // Upload indices
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject[0]);
                GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Count * sizeof(int), _indices.ToArray(), BufferUsageHint.StaticDraw);

                _isOpenGLInitialized = true;
                Logger.Info($"OpenGL resources initialized with {_vertices.Count} vertices", "Dicom3D");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize OpenGL resources", ex, "Dicom3D");
                throw;
            }
        }

        /// <summary>
        /// Sets clipping planes to limit the visible slices.
        /// </summary>
        /// <param name="front">Front clipping plane.</param>
        /// <param name="back">Back clipping plane.</param>
        public void SetClipPlanes(int front, int back)
        {
            ThrowIfDisposed();
            _frontClip = front;
            _backClip = back;
            UpdateVisibleVertices();
        }

        /// <summary>
        /// Updates the visible vertices based on clipping planes.
        /// </summary>
        private void UpdateVisibleVertices()
        {
            ThrowIfDisposed();
            if (!_isOpenGLInitialized)
                return;

            var visibleIndices = new List<int>();

            // Filter indices based on z-coordinate
            for (int i = 0; i < _indices.Count; i++)
            {
                int vertexIndex = _indices[i];
                float zPos = _vertices[vertexIndex].Z + 0.5f; // Adjust for -0.5f offset
                int slice = (int)(zPos * _totalSlices);

                if (slice >= _frontClip && slice <= (_totalSlices - _backClip - 1))
                {
                    visibleIndices.Add(_indices[i]);
                }
            }

            // Update element buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject[0]);
            GL.BufferData(BufferTarget.ElementArrayBuffer, visibleIndices.Count * sizeof(int),
                visibleIndices.ToArray(), BufferUsageHint.DynamicDraw);

            _currentVisibleIndices = visibleIndices.Count;
            Logger.Info($"Updated visible vertices: {_currentVisibleIndices} of {_indices.Count}", "Dicom3D");
        }

        /// <summary>
        /// Renders the 3D model using the specified shader and transformation matrices.
        /// </summary>
        /// <param name="shader">The shader program to use.</param>
        /// <param name="model">The model transformation matrix.</param>
        /// <param name="view">The view transformation matrix.</param>
        /// <param name="projection">The projection transformation matrix.</param>
        public void Render(int shader, Matrix4 model, Matrix4 view, Matrix4 projection)
        {
            ThrowIfDisposed();
            if (!_isOpenGLInitialized)
            {
                Logger.Warning("Cannot render: OpenGL resources not initialized.", "Dicom3D");
                return;
            }

            // Use the specified shader
            GL.UseProgram(shader);

            // Set transformation matrices
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "projection"), false, ref projection);

            // Draw points
            GL.PointSize(2.0f);
            GL.BindVertexArray(_vertexArrayObject);
            GL.DrawElements(PrimitiveType.Points, _currentVisibleIndices, DrawElementsType.UnsignedInt, 0);
        }

        #endregion

        #region Slice Extraction

        /// <summary>
        /// Extracts a 2D slice from the 3D model at the specified position and angle.
        /// </summary>
        /// <param name="xPosition">X-position of the slice (-0.5 to 0.5).</param>
        /// <param name="zPosition">Z-position of the slice (-0.5 to 0.5).</param>
        /// <param name="angleYZ">Rotation angle in YZ plane (radians).</param>
        /// <param name="angleXY">Rotation angle in XY plane (radians).</param>
        /// <returns>A bitmap containing the extracted slice.</returns>
        public Bitmap ExtractSlice(float xPosition, float zPosition, float angleYZ = 0, float angleXY = 0)
        {
            ThrowIfDisposed();

            // Convert normalized positions (-0.5 to 0.5)
            Vector3 positionVector = new Vector3(xPosition, 0, zPosition);

            // Create rotation quaternions
            Quaternion rotationZ = Quaternion.FromAxisAngle(Vector3.UnitZ, angleXY);
            Quaternion rotationY = Quaternion.FromAxisAngle(Vector3.UnitY, angleYZ);
            Quaternion rotation = rotationY * rotationZ;

            // Get the normal vector for our slice plane
            Vector3 normal = Vector3.Transform(Vector3.UnitX, rotation);
            normal = Vector3.Normalize(normal);

            // Get rotated basis vectors for our 2D slice space
            Vector3 upVector = Vector3.Transform(Vector3.UnitY, rotation);
            Vector3 rightVector = Vector3.Transform(Vector3.UnitZ, rotation);

            // Create a bitmap using the original pixel dimensions
            var bitmap = new Bitmap(_sliceWidth, _sliceHeight);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            const float THICKNESS_THRESHOLD = 0.005f;

            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;

                // Use arrays for direct pixel access
                var intensityArray = new float[_sliceHeight, _sliceWidth];
                var countArray = new int[_sliceHeight, _sliceWidth];

                // Project points onto the bitmap
                foreach (var kvp in _pointColors)
                {
                    Vector3 pointVector = kvp.Key - positionVector;
                    float distance = Math.Abs(Vector3.Dot(pointVector, normal));

                    // Only consider points close to the slice plane
                    if (distance <= THICKNESS_THRESHOLD)
                    {
                        // Project point onto slice plane
                        Vector3 projected = kvp.Key - normal * Vector3.Dot(kvp.Key - positionVector, normal);

                        // Get coordinates in rotated space relative to original pixel grid
                        float y = Vector3.Dot(projected - positionVector, upVector);
                        float z = Vector3.Dot(projected - positionVector, rightVector);

                        // Convert to pixel coordinates maintaining original grid
                        int imageY = (int)Math.Round((y + 0.5f) * (_sliceHeight - 1));
                        int imageX = (int)Math.Round((z + 0.5f) * (_sliceWidth - 1));

                        // Ensure coordinates are within bounds
                        if (imageY >= 0 && imageY < _sliceHeight && imageX >= 0 && imageX < _sliceWidth)
                        {
                            intensityArray[imageY, imageX] += kvp.Value.X;
                            countArray[imageY, imageX]++;
                        }
                    }
                }

                // Fill the bitmap using the accumulated values
                for (int y = 0; y < _sliceHeight; y++)
                {
                    for (int x = 0; x < _sliceWidth; x++)
                    {
                        int offset = y * bitmapData.Stride + x * 4;
                        byte value = 0;

                        if (countArray[y, x] > 0)
                        {
                            float avgIntensity = intensityArray[y, x] / countArray[y, x];
                            value = (byte)(avgIntensity * 255);
                        }

                        ptr[offset] = value;     // B
                        ptr[offset + 1] = value; // G
                        ptr[offset + 2] = value; // R
                        ptr[offset + 3] = 255;   // A
                    }
                }
            }

            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Dispose managed resources used by this instance.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Clear collections to free memory
            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
            _pointColors.Clear();

            Logger.Info("Managed resources disposed", "Dicom3D");
        }

        /// <summary>
        /// Dispose unmanaged OpenGL resources.
        /// </summary>
        protected override void DisposeUnmanagedResources()
        {
            try
            {
                if (_isOpenGLInitialized)
                {
                    // Check if we have a valid GL context
                    GL.GetInteger(GetPName.MaxVertexAttribs, out int _);

                    if (_vertexBufferObject != null)
                    {
                        GL.DeleteBuffers(_vertexBufferObject.Length, _vertexBufferObject);
                        _vertexBufferObject = null;
                    }

                    if (_colorBufferObject != null)
                    {
                        GL.DeleteBuffers(_colorBufferObject.Length, _colorBufferObject);
                        _colorBufferObject = null;
                    }

                    if (_elementBufferObject != null)
                    {
                        GL.DeleteBuffers(_elementBufferObject.Length, _elementBufferObject);
                        _elementBufferObject = null;
                    }

                    if (_vertexArrayObject != 0)
                    {
                        GL.DeleteVertexArray(_vertexArrayObject);
                        _vertexArrayObject = 0;
                    }

                    _isOpenGLInitialized = false;
                    Logger.Info("OpenGL resources disposed", "Dicom3D");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing OpenGL resources", ex, "Dicom3D");
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a processing progress information for reporting progress during long-running operations.
    /// </summary>
    public class ProcessingProgress
    {
        /// <summary>
        /// Gets or sets the current step description.
        /// </summary>
        public string CurrentStep { get; set; }

        /// <summary>
        /// Gets or sets the current value.
        /// </summary>
        public int CurrentValue { get; set; }

        /// <summary>
        /// Gets or sets the total value.
        /// </summary>
        public int TotalValue { get; set; }

        /// <summary>
        /// Gets the percentage completion from 0 to 100.
        /// </summary>
        public float Percentage => (float)CurrentValue / TotalValue * 100;
    }

    /// <summary>
    /// Represents a plane for slicing through 3D DICOM data.
    /// </summary>
    public class SlicePlane
    {
        /// <summary>
        /// Gets the normal vector of the plane.
        /// </summary>
        public Vector3 Normal { get; private set; }

        /// <summary>
        /// Gets a point on the plane.
        /// </summary>
        public Vector3 Point { get; private set; }

        /// <summary>
        /// Creates a new slice plane with the specified normal vector and position.
        /// </summary>
        /// <param name="normal">The normal vector of the plane.</param>
        /// <param name="xPosition">The x-position of the plane.</param>
        public SlicePlane(Vector3 normal, float xPosition)
        {
            Normal = normal.Normalized();
            Point = new Vector3(xPosition, 0, 0);
        }

        /// <summary>
        /// Gets the signed distance from a point to the plane.
        /// </summary>
        /// <param name="point">The point to calculate distance for.</param>
        /// <returns>The signed distance (negative if behind the plane).</returns>
        public float GetSignedDistance(Vector3 point)
        {
            return Vector3.Dot(Normal, point - Point);
        }
    }
}