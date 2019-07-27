using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Middleware
{
    public class CustomCssAccentMiddlewareAction : IMiddlewareAction<string>
    {
        private readonly List<ColorMap> ColorReplacements = new List<ColorMap>();

        private class ColorMap
        {
            public Color Original { get; set; }
            public Color Replacement { get; set; }
        }

        public CustomCssAccentMiddlewareAction(string originalPrimaryColor, string originalSecondaryColor, string primaryColor, string secondaryColor)
        {
            primaryColor = string.IsNullOrWhiteSpace(primaryColor) ? originalPrimaryColor : primaryColor;
            secondaryColor = string.IsNullOrWhiteSpace(secondaryColor) ? originalSecondaryColor : secondaryColor;
            try
            {
                ColorReplacements.AddRange(new[]
                {
                    new ColorMap()
                    {
                        Original = Color.FromArgb(Convert.ToInt32(originalPrimaryColor.Substring(1).ToString(), 16)),
                        Replacement = Color.FromArgb(Convert.ToInt32(primaryColor.Substring(1).ToString(), 16))
                    },
                    new ColorMap()
                    {
                        Original = Color.FromArgb(Convert.ToInt32(originalSecondaryColor.Substring(1).ToString(), 16)),
                        Replacement = Color.FromArgb(Convert.ToInt32(secondaryColor.Substring(1).ToString(), 16))
                    }
                });
            }

            catch (FormatException)
            {

            }
        }

        public async Task<string> Invoke(string original)
        {
            foreach (var color in ColorReplacements)
            {
                foreach (var shade in new[] { 0, -19, -25 })
                {
                    original = original
                        .Replace(ColorToHex(LightenDarkenColor(color.Original, shade)), ColorToHex(LightenDarkenColor(color.Replacement, shade)), StringComparison.OrdinalIgnoreCase)
                        .Replace(ColorToDec(LightenDarkenColor(color.Original, shade)), ColorToDec(LightenDarkenColor(color.Replacement, shade)), StringComparison.OrdinalIgnoreCase);
                }
            }

            return original;
        }

        /// <summary>
        /// converts color to the hex string representation
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private string ColorToHex(Color color) => $"#{color.R.ToString("X2")}{color.G.ToString("X2")}{color.B.ToString("X2")}";

        /// <summary>
        /// converts color to the rgb tuples representation
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private string ColorToDec(Color color) => $"{(int)color.R}, {(int)color.G}, {(int)color.B}";

        /// <summary>
        /// lightens or darkens a color on the given amount
        /// Based off SASS darken/lighten function
        /// </summary>
        /// <param name="color"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private Color LightenDarkenColor(Color color, float amount)
        {
            int r = color.R + (int)((amount / 100.0f) * color.R);

            if (r > 255) r = 255;
            else if (r < 0) r = 0;

            int g = color.G + (int)((amount / 100.0f) * color.G);

            if (g > 255) g = 255;
            else if (g < 0) g = 0;

            int b = color.B + (int)((amount / 100.0f) * color.B);

            if (b > 255) b = 255;
            else if (b < 0) b = 0;

            return Color.FromArgb(r, g, b);
        }
    }
}
