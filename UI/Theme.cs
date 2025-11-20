using System.Drawing;
using MusicBeePlugin.Config;

namespace MusicBeePlugin.UI
{
    public class Theme
    {
        public Color Background { get; }
        public Color Foreground { get; }
        public Color Highlight { get; }
        
        // Derived Colors
        public Color SecondaryText { get; }
        public Color Icon { get; }
        public Color IconDimmed { get; }
        public Color Border { get; }
        public Color ScrollBarTrack { get; }
        public Color ScrollBarThumb { get; }

        public Theme(SearchUIConfig config)
        {
            Background = config.BaseColor;
            Foreground = config.TextColor;
            Highlight = config.ResultHighlightColor;

            // Calculate derived colors based on Foreground/Background contrast
            // Slightly reduced brightness for detail text
            SecondaryText = Blend(Foreground, Background, 0.45);
            
            // Icons: Standard ones were DarkGray (Lighter), Artist was Gray (Darker)
            // We simulate this relative to the text color.
            Icon = Blend(Foreground, Background, 0.60);       
            IconDimmed = Blend(Foreground, Background, 0.40); 

            // Borders and Scrollbars often look better with Alpha transparency 
            // so they blend with whatever is behind them (if using composition)
            // or simply look softer against the background.
            // Significantly reduced alpha for borders to prevent them being too bright
            Border = Color.FromArgb(45, Foreground);
            
            // Scrollbar: Track should be very subtle, Thumb distinct but not harsh
            ScrollBarTrack = Color.FromArgb(20, Foreground);
            // Reduced thumb brightness to match previous aesthetics
            ScrollBarThumb = Color.FromArgb(100, Blend(Foreground, Background, 0.5));
        }

        /// <summary>
        /// Blends the foreground color onto the background color by the specified amount (0.0 to 1.0).
        /// 1.0 = 100% Foreground, 0.0 = 100% Background.
        /// </summary>
        private static Color Blend(Color fore, Color back, double amount)
        {
            byte r = (byte)((fore.R * amount) + (back.R * (1 - amount)));
            byte g = (byte)((fore.G * amount) + (back.G * (1 - amount)));
            byte b = (byte)((fore.B * amount) + (back.B * (1 - amount)));
            return Color.FromArgb(255, r, g, b);
        }
    }
}