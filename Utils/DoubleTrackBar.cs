﻿using System;
using System.Drawing;
using System.Windows.Forms;

public class DoubleTrackBar : TrackBar
{
    private int minValue;
    private int maxValue;
    private bool isMinThumbDragging;
    private bool isMaxThumbDragging;

    public int MinValue
    {
        get => minValue;
        set
        {
            if (value < 0 || value > maxValue)
                throw new ArgumentOutOfRangeException(nameof(MinValue));
            minValue = value;
            Invalidate();
        }
    }

    public int MaxValue
    {
        get => maxValue;
        set
        {
            if (value < minValue || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(MaxValue));
            maxValue = value;
            Invalidate();
        }
    }

    public DoubleTrackBar()
    {
        minValue = 0;
        maxValue = Maximum;
        SetStyle(ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Draw the track bar
        Rectangle trackRect = new Rectangle(0, Height / 2 - 2, Width, 4);
        e.Graphics.FillRectangle(Brushes.LightGray, trackRect);

        // Draw the min thumb
        int minThumbX = (int)((float)minValue / Maximum * Width);
        Rectangle minThumbRect = new Rectangle(minThumbX - 5, Height / 2 - 10, 10, 20);
        e.Graphics.FillRectangle(Brushes.Blue, minThumbRect);

        // Draw the max thumb
        int maxThumbX = (int)((float)maxValue / Maximum * Width);
        Rectangle maxThumbRect = new Rectangle(maxThumbX - 5, Height / 2 - 10, 10, 20);
        e.Graphics.FillRectangle(Brushes.Red, maxThumbRect);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        int minThumbX = (int)((float)minValue / Maximum * Width);
        int maxThumbX = (int)((float)maxValue / Maximum * Width);

        Rectangle minThumbRect = new Rectangle(minThumbX - 5, Height / 2 - 10, 10, 20);
        Rectangle maxThumbRect = new Rectangle(maxThumbX - 5, Height / 2 - 10, 10, 20);

        if (minThumbRect.Contains(e.Location))
        {
            isMinThumbDragging = true;
        }
        else if (maxThumbRect.Contains(e.Location))
        {
            isMaxThumbDragging = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (isMinThumbDragging)
        {
            int newMinValue = (int)((float)e.X / Width * Maximum);
            if (newMinValue >= 0 && newMinValue <= maxValue)
            {
                MinValue = newMinValue;
            }
        }
        else if (isMaxThumbDragging)
        {
            int newMaxValue = (int)((float)e.X / Width * Maximum);
            if (newMaxValue >= minValue && newMaxValue <= Maximum)
            {
                MaxValue = newMaxValue;
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        isMinThumbDragging = false;
        isMaxThumbDragging = false;
    }
}
