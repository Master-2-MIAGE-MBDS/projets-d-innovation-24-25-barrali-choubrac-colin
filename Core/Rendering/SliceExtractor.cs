using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Mathematics;
using DeepBridgeWindowsApp.Utils;
using System.Collections.Generic;

namespace DeepBridgeWindowsApp.Core.Rendering
{
    /// <summary>
    /// Handles extraction of 2D slices from 3D DICOM data.
    /// Provides functionality for various slice orientations and transformations.
    /// </summary>
    public class SliceExtractor : DisposableBase
    {
        #region Fields

        private readonly int _sliceWidth;
        private readonly int _sliceHeight;
        private readonly Dictionary<Vector3, Vector3> _pointColors;
        private const float THICKNESS_THRESHOLD = 0.005f;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new SliceExtractor for generating 2D slices from 3D point data.
        /// </summary>
        /// <param name="sliceWidth">The width of the target slice.</param>
        /// <param name="sliceHeight">The height of the target slice.</param>
        /// <param name="pointColors">The dictionary of 3D points and their color values.</param>
        public SliceExtractor(int sliceWidth, int sliceHeight, Dictionary<Vector3, Vector3> pointColors)
        {
            _sliceWidth = sliceWidth > 0
                ? sliceWidth
                : throw new ArgumentOutOfRangeException(nameof(sliceWidth), "Slice width must be positive");

            _sliceHeight = sliceHeight > 0
                ? sliceHeight
                : throw new ArgumentOutOfRangeException(nameof(sliceHeight), "Slice height must be positive");

            _pointColors = pointColors ??
                throw new ArgumentNullException(nameof(pointColors), "Point colors cannot be null");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Extracts a slice from the 3D data at the specified position and angles.
        /// </summary>
        /// <param name="xPosition">The X position of the slice (-0.5 to 0.5).</param>
        /// <param name="zPosition">The Z position of the slice (-0.5 to 0.5).</param>
        /// <param name="angleYZ">The angle in the YZ plane in radians.</param>
        /// <param name="angleXY">The angle in the XY plane in radians.</param>
        /// <returns>A bitmap containing the extracted slice.</returns>
        public Bitmap ExtractSlice(float xPosition, float zPosition, float angleYZ = 0, float angleXY = 0)
        {
            ThrowIfDisposed();

            // Create the position vector
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

            // Create a new bitmap for the slice
            var bitmap = new Bitmap(_sliceWidth, _sliceHeight);

            try
            {
                // Lock the bitmap for direct pixel access
                BitmapData bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0;

                    // Create arrays for accumulating intensity values
                    var intensityArray = new float[_sliceHeight, _sliceWidth];
                    var countArray = new int[_sliceHeight, _sliceWidth];

                    // Process each point in the 3D data
                    foreach (var kvp in _pointColors)
                    {
                        // Calculate distance from point to slice plane
                        Vector3 pointVector = kvp.Key - positionVector;
                        float distance = Math.Abs(Vector3.Dot(pointVector, normal));

                        // Only include points close to the slice plane
                        if (distance <= THICKNESS_THRESHOLD)
                        {
                            // Project point onto slice plane
                            Vector3 projected = kvp.Key - normal * Vector3.Dot(kvp.Key - positionVector, normal);

                            // Get coordinates in rotated space
                            float y = Vector3.Dot(projected - positionVector, upVector);
                            float z = Vector3.Dot(projected - positionVector, rightVector);

                            // Convert to image coordinates
                            int imageY = (int)Math.Round((y + 0.5f) * (_sliceHeight - 1));
                            int imageX = (int)Math.Round((z + 0.5f) * (_sliceWidth - 1));

                            // Accumulate intensity if coordinates are within bounds
                            if (imageY >= 0 && imageY < _sliceHeight && imageX >= 0 && imageX < _sliceWidth)
                            {
                                // Use distance as a weighting factor (closer points have more influence)
                                float weight = 1.0f - (distance / THICKNESS_THRESHOLD);
                                intensityArray[imageY, imageX] += kvp.Value.X * weight;
                                countArray[imageY, imageX]++;
                            }
                        }
                    }

                    // Convert accumulated intensity values to pixel data
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

                            // Set BGRA values (grayscale)
                            ptr[offset] = value;     // B
                            ptr[offset + 1] = value; // G
                            ptr[offset + 2] = value; // R
                            ptr[offset + 3] = 255;   // A (fully opaque)
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);
                return bitmap;
            }
            catch (Exception ex)
            {
                bitmap.Dispose();
                Logger.Error("Failed to extract slice", ex, "SliceExtractor");
                throw;
            }
        }

        /// <summary>
        /// Creates a multiplanar reconstruction (MPR) view from the 3D data.
        /// </summary>
        /// <param name="orientation">The orientation of the MPR view.</param>
        /// <param name="position">The position along the normal axis (0-1).</param>
        /// <returns>A bitmap containing the MPR view.</returns>
        public Bitmap CreateMPR(MPROrientation orientation, float position)
        {
            ThrowIfDisposed();

            switch (orientation)
            {
                case MPROrientation.Axial:
                    // Axial view (XZ plane)
                    return ExtractSlice(0, 0, 0, 0);

                case MPROrientation.Sagittal:
                    // Sagittal view (YZ plane)
                    return ExtractSlice(position, 0, (float)Math.PI / 2, 0);

                case MPROrientation.Coronal:
                    // Coronal view (XY plane)
                    return ExtractSlice(0, position, 0, (float)Math.PI / 2);

                default:
                    throw new ArgumentException("Invalid MPR orientation", nameof(orientation));
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Does not need to dispose of any resources as it doesn't own the _pointColors dictionary.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Nothing to dispose
            base.DisposeManagedResources();
        }

        #endregion
    }

    /// <summary>
    /// Represents the orientation for Multiplanar Reconstruction (MPR) views.
    /// </summary>
    public enum MPROrientation
    {
        /// <summary>Axial view (perpendicular to the long axis of the body).</summary>
        Axial,

        /// <summary>Sagittal view (divides the body into left and right portions).</summary>
        Sagittal,

        /// <summary>Coronal view (divides the body into anterior and posterior portions).</summary>
        Coronal
    }
}