using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace CAL_QR.Services
{
    /// <summary>
    /// تطبيق الثيم في وقت التشغيل: 4 لوحات ألوان × وضعا فاتح/داكن.
    /// يُحدِّث MaterialDesign PaletteHelper والفراشي المخصّصة في App.Resources معاً.
    /// </summary>
    public static class ThemeService
    {
        public static readonly IReadOnlyDictionary<string, (Color Primary, Color Accent)> Palettes =
            new Dictionary<string, (Color, Color)>
            {
                ["Steel"]  = (Color.FromRgb(0x16, 0x2D, 0x50), Color.FromRgb(0x00, 0xB4, 0xA0)),
                ["Gold"]   = (Color.FromRgb(0x2C, 0x30, 0x40), Color.FromRgb(0xD4, 0x96, 0x0A)),
                ["Cobalt"] = (Color.FromRgb(0x0D, 0x21, 0x37), Color.FromRgb(0x1B, 0x6C, 0xF2)),
                ["Olive"]  = (Color.FromRgb(0x17, 0x28, 0x18), Color.FromRgb(0xC8, 0x88, 0x3A)),
            };

        public static void Apply(string paletteName, string mode)
        {
            if (!Palettes.TryGetValue(paletteName, out var p)) return;

            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            theme.SetPrimaryColor(p.Primary);
            theme.SetSecondaryColor(p.Accent);

            bool isDark = mode == "Dark" ||
                (mode == "System" && IsSystemDark());

            var baseTheme = mode switch
            {
                "Dark"   => BaseTheme.Dark,
                "System" => BaseTheme.Inherit,
                _        => BaseTheme.Light,
            };
            theme.SetBaseTheme(baseTheme);
            paletteHelper.SetTheme(theme);

            // Custom brushes used in non-MD XAML
            SetBrush("PrimaryNavy",         p.Primary);
            SetBrush("GoldAccent",          p.Accent);
            SetBrush("PrimaryHueMidBrush",  p.Primary);
            SetBrush("PrimaryHueDarkBrush", Darken(p.Primary, 0.60f));
            SetBrush("PrimaryHueLightBrush",Lighten(p.Primary, 50));
            SetBrush("SecondaryAccentBrush",p.Accent);

            // Input foreground: white text in dark mode, primary colour in light
            var inputColor = isDark ? Colors.White : p.Primary;
            SetBrush("PrimaryInputForeground", inputColor);

            // Tinted surface colours — derive from palette for both modes
            if (isDark)
            {
                // Dark: very dark tinted surfaces
                SetBrush("AppBackground",  TintDark(p.Primary, 0.12f, 0x12));
                SetBrush("AppSurface",     TintDark(p.Primary, 0.10f, 0x1C));
                SetBrush("AppSurfaceAlt",  TintDark(p.Primary, 0.08f, 0x16));
                SetBrush("AppBorder",      TintDark(p.Primary, 0.25f, 0x30));
            }
            else
            {
                // Light: very pale tinted surfaces (5-8% primary hue)
                SetBrush("AppBackground",  TintLight(p.Primary, 0.06f));
                SetBrush("AppSurface",     TintLight(p.Primary, 0.02f));
                SetBrush("AppSurfaceAlt",  TintLight(p.Primary, 0.04f));
                SetBrush("AppBorder",      TintLight(p.Primary, 0.12f));
            }
        }

        private static void SetBrush(string key, Color color)
        {
            if (Application.Current?.Resources.Contains(key) == true)
                Application.Current.Resources[key] = new SolidColorBrush(color);
        }

        // Blend primary hue into white at given ratio (0=white, 1=full primary)
        private static Color TintLight(Color primary, float ratio) =>
            Color.FromRgb(
                (byte)(255 - (255 - primary.R) * ratio),
                (byte)(255 - (255 - primary.G) * ratio),
                (byte)(255 - (255 - primary.B) * ratio));

        // Blend primary hue into a dark base (baseValue = brightness of dark layer)
        private static Color TintDark(Color primary, float ratio, byte baseValue) =>
            Color.FromRgb(
                (byte)(baseValue + (primary.R - baseValue) * ratio),
                (byte)(baseValue + (primary.G - baseValue) * ratio),
                (byte)(baseValue + (primary.B - baseValue) * ratio));

        private static Color Darken(Color c, float factor) =>
            Color.FromRgb(
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));

        private static Color Lighten(Color c, byte delta) =>
            Color.FromRgb(
                (byte)Math.Min(255, c.R + delta),
                (byte)Math.Min(255, c.G + delta),
                (byte)Math.Min(255, c.B + delta));

        private static bool IsSystemDark()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }
    }
}
