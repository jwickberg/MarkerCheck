using System;

namespace MarkerCheck
{
    /// <summary>
    /// Simple color structure 
    /// </summary>
    public struct RgbColor
    {
        private readonly int value;

        public RgbColor(int red, int green, int blue, int alpha = 0xFF)
        {
            if (red < 0 || red > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(red));
            if (green < 0 || green > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(green));
            if (blue < 0 || blue > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(blue));
            if (alpha < 0 || alpha > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(alpha));

            value = ((alpha & 0xFF) << 24) | ((red & 0xFF) << 16) | ((green & 0xFF) << 8) | (blue & 0xFF);
        }

        /// <summary>
        /// Creates a new color from an RGB value and an alpha value
        /// </summary>
        public RgbColor(int rgb, int alpha)
        {
            if (alpha < 0 || alpha > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(alpha));
            if (rgb < 0 || rgb > 0xFFFFFF)
                throw new ArgumentOutOfRangeException(nameof(rgb));

            value = ((alpha & 0xFF) << 24) | rgb;
        }

        /// <summary>
        /// Creates a new color from an ARGB value - useful for creating from a System.Drawing.Color
        /// </summary>
        public RgbColor(int argb)
        {
            value = argb;
        }

        /// <summary>
        /// Gets the color as an ARGB color - useful for converting to a System.Drawing.Color
        /// </summary>
        public int ARGB => value;

        /// <summary>
        /// Gets the red component
        /// </summary>
        public byte R => (byte)((value >> 16) & 0xFF);

        /// <summary>
        /// Gets the green component
        /// </summary>
        public byte G => (byte)((value >> 8) & 0xFF);

        /// <summary>
        /// Gets the blue component
        /// </summary>
        public byte B => (byte)(value & 0xFF);

        public byte A => (byte)((value >> 24) & 0xFF);
    }
}
