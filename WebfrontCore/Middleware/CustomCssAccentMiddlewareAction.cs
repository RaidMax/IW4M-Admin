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
        private readonly Color _primaryColor;
        private readonly Color _secondaryColor;
        private readonly Color _originalPrimaryColor;
        private readonly Color _originalSecondaryColor;

        public CustomCssAccentMiddlewareAction(string originalPrimaryColor, string originalSecondaryColor, string primaryColor, string secondaryColor)
        {
            _originalPrimaryColor = Color.FromArgb(Convert.ToInt32(originalPrimaryColor.Substring(1).ToString(), 16));
            _originalSecondaryColor = Color.FromArgb(Convert.ToInt32(originalSecondaryColor.Substring(1).ToString(), 16));
            _primaryColor = string.IsNullOrEmpty(primaryColor) ? _originalPrimaryColor : Color.FromArgb(Convert.ToInt32(primaryColor.Substring(1).ToString(), 16));
            _secondaryColor = string.IsNullOrEmpty(secondaryColor) ? _originalSecondaryColor : Color.FromArgb(Convert.ToInt32(secondaryColor.Substring(1).ToString(), 16));
        }

        public async Task<string> Invoke(string original)
        {
            string originalPrimaryHex = ColorToHex(_originalPrimaryColor);
            string originalPrimaryDec = ColorToDec(_originalPrimaryColor);
            string originalSecondaryHex = ColorToHex(_originalSecondaryColor);
            string originalSecondaryDec = ColorToDec(_originalSecondaryColor);

            string primaryHex = ColorToHex(_primaryColor);
            string primaryDec = ColorToDec(_primaryColor);
            string secondaryHex = ColorToHex(_secondaryColor);
            string secondaryDec = ColorToDec(_secondaryColor);

            string originalPrimaryDarkenHex = ColorToHex(LightenDarkenColor(_originalPrimaryColor, -10));
            string originalPrimaryDarkenDec = ColorToDec(LightenDarkenColor(_originalPrimaryColor, -10));
            string originalSecondaryDarkenHex = ColorToHex(LightenDarkenColor(_originalSecondaryColor, -10));
            string originalSecondaryDarkenDec = ColorToDec(LightenDarkenColor(_originalSecondaryColor, -10));

            string primaryDarkenHex = ColorToHex(LightenDarkenColor(_primaryColor, -10));
            string primaryDarkenDec = ColorToDec(LightenDarkenColor(_primaryColor, -10));
            string secondaryDarkenHex = ColorToHex(LightenDarkenColor(_secondaryColor, -10));
            string secondaryDarkenDec = ColorToDec(LightenDarkenColor(_secondaryColor, -10));

            return original
                .Replace(originalPrimaryHex, primaryHex, StringComparison.OrdinalIgnoreCase)
                .Replace(originalPrimaryDec, primaryDec, StringComparison.OrdinalIgnoreCase)
                .Replace(originalSecondaryHex, secondaryHex, StringComparison.OrdinalIgnoreCase)
                .Replace(originalSecondaryDec, secondaryDec, StringComparison.OrdinalIgnoreCase)
                .Replace(originalPrimaryDarkenHex, primaryDarkenHex, StringComparison.OrdinalIgnoreCase)
                .Replace(originalPrimaryDarkenDec, primaryDarkenDec, StringComparison.OrdinalIgnoreCase)
                .Replace(originalSecondaryDarkenHex, secondaryDarkenHex, StringComparison.OrdinalIgnoreCase)
                .Replace(originalSecondaryDarkenDec, secondaryDarkenDec, StringComparison.OrdinalIgnoreCase);
        }

        private string ColorToHex(Color color) => $"#{color.R.ToString("X2")}{color.G.ToString("X2")}{color.B.ToString("X2")}";
        private string ColorToDec(Color color) => $"{(int)color.R}, {(int)color.G}, {(int)color.B}";

        /// <summary>
        /// Adapted from https://css-tricks.com/snippets/javascript/lighten-darken-color/
        /// </summary>
        /// <param name="col"></param>
        /// <param name="amt"></param>
        /// <returns></returns>
        private Color LightenDarkenColor(Color col, float amt)
        {
            var num = col.ToArgb();

            int r = (num >> 16) + (int)(amt * (num >> 16)); 

            if (r > 255) r = 255;
            else if (r < 0) r = 0;

            int g = ((num >> 8) & 0x00FF) + (int)(amt * ((num >> 8) & 0x00FF));

            if (g > 255) g = 255;
            else if (g < 0) g = 0;

            int b = (num & 0x0000FF) + (int)(amt * (num & 0x0000FF));

            if (b > 255) b = 255;
            else if (b < 0) b = 0;

            return Color.FromArgb(r, g, b);
        }
    }
}
