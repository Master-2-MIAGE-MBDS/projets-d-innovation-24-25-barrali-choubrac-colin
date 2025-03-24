using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.Controls
{
    /// <summary>
    /// A track bar control with two movable thumbs for selecting a range of values.
    /// </summary>
    public class DoubleTrackBar : Control
    {
        #region Fields

        private int _minimum = 0;
        private int _maximum = 100;
        private int _minValue = 0;
        private int _maxValue = 100;
        private bool _isMinThumbDragging = false;
        private bool _isMaxThumbDragging = false;
        private readonly int _thumbWidth = 10;
        private readonly int _thumbHeight = 20;
        private readonly int _trackHeight = 4;
        private readonly int _trackPadding = 15; // Padding from edges
        private readonly Color _trackColor = Color.LightGray;
        private readonly Color _selectedRangeColor = Color.LightBlue;
        private readonly Color _minThumbColor = Color.Blue;
        private readonly Color _maxThumbColor = Color.Red;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when either thumb value changes.
        /// </summary>
        [Category("Action")]
        [Description("Occurs when either thumb value changes")]
        public event EventHandler ValueChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the minimum possible value of the control.
        /// </summary>
        [Category("Behavior")]
        [Description("The minimum possible value of the control")]
        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                if (value >= _maximum)
                    throw new ArgumentException("Minimum cannot be greater than or equal to Maximum");

                _minimum = value;
                if (_minValue < _minimum)
                    MinValue = _minimum;

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the maximum possible value of the control.
        /// </summary>
        [Category("Behavior")]
        [Description("The maximum possible value of the control")]
        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                if (value <= _minimum)
                    throw new ArgumentException("Maximum cannot be less than or equal to Minimum");

                _maximum = value;
                if (_maxValue > _maximum)
                    MaxValue = _maximum;

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the value of the minimum thumb.
        /// </summary>
        [Category("Behavior")]
        [Description("The value of the minimum thumb")]
        [DefaultValue(0)]
        public int MinValue
        {
            get => _minValue;
            set
            {
                if (value < _minimum) value = _minimum;
                if (value > _maxValue) value = _maxValue;

                if (_minValue != value)
                {
                    _minValue = value;
                    OnValueChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of the maximum thumb.
        /// </summary>
        [Category("Behavior")]
        [Description("The value of the maximum thumb")]
        [DefaultValue(100)]
        public int MaxValue
        {
            get => _maxValue;
            set
            {
                if (value > _maximum) value = _maximum;
                if (value < _minValue) value = _minValue;

                if (_maxValue != value)
                {
                    _maxValue = value;
                    OnValueChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DoubleTrackBar instance.
        /// </summary>
        public DoubleTrackBar()
        {
            SetStyle(ControlStyles.UserPaint |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);

            Size = new Size(200, 30);
            MinValue = Minimum;
            MaxValue = Maximum;
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// Raises the Paint event.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw track background
            int trackY = Height / 2 - _trackHeight / 2;
            Rectangle trackRect = new Rectangle(_trackPadding, trackY, Width - 2 * _trackPadding, _trackHeight);
            g.FillRectangle(new SolidBrush(_trackColor), trackRect);

            // Calculate thumb positions
            int valueRange = _maximum - _minimum;
            float pixelRange = Width - 2 * _trackPadding - _thumbWidth;

            int minThumbX = _trackPadding + (int)((float)(_minValue - _minimum) / valueRange * pixelRange);
            int maxThumbX = _trackPadding + (int)((float)(_maxValue - _minimum) / valueRange * pixelRange);

            // Draw selected range
            Rectangle selectedRect = new Rectangle(
                minThumbX + _thumbWidth / 2,
                trackY,
                maxThumbX - minThumbX,
                _trackHeight);
            g.FillRectangle(new SolidBrush(_selectedRangeColor), selectedRect);

            // Draw minimum thumb
            Rectangle minThumbRect = new Rectangle(
                minThumbX,
                Height / 2 - _thumbHeight / 2,
                _thumbWidth,
                _thumbHeight);
            g.FillRectangle(new SolidBrush(_minThumbColor), minThumbRect);
            g.DrawRectangle(Pens.Black, minThumbRect);

            // Draw maximum thumb
            Rectangle maxThumbRect = new Rectangle(
                maxThumbX,
                Height / 2 - _thumbHeight / 2,
                _thumbWidth,
                _thumbHeight);
            g.FillRectangle(new SolidBrush(_maxThumbColor), maxThumbRect);
            g.DrawRectangle(Pens.Black, maxThumbRect);
        }

        /// <summary>
        /// Raises the MouseDown event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            // Calculate thumb positions
            int valueRange = _maximum - _minimum;
            float pixelRange = Width - 2 * _trackPadding - _thumbWidth;

            int minThumbX = _trackPadding + (int)((float)(_minValue - _minimum) / valueRange * pixelRange);
            int maxThumbX = _trackPadding + (int)((float)(_maxValue - _minimum) / valueRange * pixelRange);

            // Check if we clicked on a thumb
            Rectangle minThumbRect = new Rectangle(
                minThumbX,
                Height / 2 - _thumbHeight / 2,
                _thumbWidth,
                _thumbHeight);

            Rectangle maxThumbRect = new Rectangle(
                maxThumbX,
                Height / 2 - _thumbHeight / 2,
                _thumbWidth,
                _thumbHeight);

            if (minThumbRect.Contains(e.Location))
            {
                _isMinThumbDragging = true;
            }
            else if (maxThumbRect.Contains(e.Location))
            {
                _isMaxThumbDragging = true;
            }
            else
            {
                // If we clicked on the track, move the closest thumb
                int distToMin = Math.Abs(e.X - (minThumbX + _thumbWidth / 2));
                int distToMax = Math.Abs(e.X - (maxThumbX + _thumbWidth / 2));

                if (distToMin <= distToMax)
                {
                    _isMinThumbDragging = true;
                    // Update position immediately
                    OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
                }
                else
                {
                    _isMaxThumbDragging = true;
                    // Update position immediately
                    OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
                }
            }
        }

        /// <summary>
        /// Raises the MouseMove event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isMinThumbDragging && !_isMaxThumbDragging)
                return;

            // Calculate the new value based on mouse position
            int valueRange = _maximum - _minimum;
            float pixelRange = Width - 2 * _trackPadding - _thumbWidth;

            int pixelPos = e.X - _trackPadding;
            int newValue = _minimum + (int)(pixelPos / pixelRange * valueRange);

            // Clamp to valid range
            newValue = Math.Max(_minimum, Math.Min(_maximum, newValue));

            // Update appropriate thumb
            if (_isMinThumbDragging)
            {
                MinValue = newValue;
            }
            else if (_isMaxThumbDragging)
            {
                MaxValue = newValue;
            }
        }

        /// <summary>
        /// Raises the MouseUp event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            _isMinThumbDragging = false;
            _isMaxThumbDragging = false;
        }

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }

        #endregion
    }
}