using System;

namespace ThreadedMosaic.Core.Models
{
    /// <summary>
    /// Cross-platform color information structure to replace System.Drawing.Color
    /// </summary>
    public struct ColorInfo : IEquatable<ColorInfo>
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public ColorInfo(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public ColorInfo(int argb)
        {
            A = (byte)((argb >> 24) & 0xFF);
            R = (byte)((argb >> 16) & 0xFF);
            G = (byte)((argb >> 8) & 0xFF);
            B = (byte)(argb & 0xFF);
        }

        /// <summary>
        /// Gets the color as a 32-bit ARGB value
        /// </summary>
        public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;

        /// <summary>
        /// Gets the color as a hex string
        /// </summary>
        public string ToHexString() => $"#{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// Gets the color as a hex string with alpha
        /// </summary>
        public string ToHexStringWithAlpha() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// Calculates the perceived brightness using the standard formula
        /// </summary>
        public double GetBrightness()
        {
            return (0.299 * R + 0.587 * G + 0.114 * B) / 255.0;
        }

        /// <summary>
        /// Calculates the hue in degrees (0-360)
        /// </summary>
        public double GetHue()
        {
            var max = Math.Max(R, Math.Max(G, B));
            var min = Math.Min(R, Math.Min(G, B));
            
            if (max == min) return 0; // Grayscale

            double hue;
            var delta = max - min;

            if (max == R)
                hue = ((G - B) / (double)delta) % 6;
            else if (max == G)
                hue = (B - R) / (double)delta + 2;
            else
                hue = (R - G) / (double)delta + 4;

            hue *= 60;
            if (hue < 0) hue += 360;

            return hue;
        }

        /// <summary>
        /// Calculates the saturation (0-1)
        /// </summary>
        public double GetSaturation()
        {
            var max = Math.Max(R, Math.Max(G, B));
            var min = Math.Min(R, Math.Min(G, B));
            
            if (max == 0) return 0;
            
            return 1.0 - (min / (double)max);
        }

        /// <summary>
        /// Calculates the Euclidean distance between two colors
        /// </summary>
        public double GetDistanceTo(ColorInfo other)
        {
            var deltaR = R - other.R;
            var deltaG = G - other.G;
            var deltaB = B - other.B;
            
            return Math.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB);
        }

        /// <summary>
        /// Creates a ColorInfo from HSV values
        /// </summary>
        public static ColorInfo FromHsv(double hue, double saturation, double value, byte alpha = 255)
        {
            var c = value * saturation;
            var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            var m = value - c;

            double r, g, b;

            if (hue >= 0 && hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue >= 60 && hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue >= 120 && hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue >= 180 && hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue >= 240 && hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            return new ColorInfo(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255),
                alpha);
        }

        public static bool operator ==(ColorInfo left, ColorInfo right) => left.Equals(right);
        public static bool operator !=(ColorInfo left, ColorInfo right) => !left.Equals(right);

        public bool Equals(ColorInfo other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object? obj) => obj is ColorInfo other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(R, G, B, A);

        public override string ToString() => $"ColorInfo(R={R}, G={G}, B={B}, A={A})";

        // Common color constants
        public static readonly ColorInfo Black = new(0, 0, 0);
        public static readonly ColorInfo White = new(255, 255, 255);
        public static readonly ColorInfo Red = new(255, 0, 0);
        public static readonly ColorInfo Green = new(0, 255, 0);
        public static readonly ColorInfo Blue = new(0, 0, 255);
        public static readonly ColorInfo Transparent = new(0, 0, 0, 0);
    }
}