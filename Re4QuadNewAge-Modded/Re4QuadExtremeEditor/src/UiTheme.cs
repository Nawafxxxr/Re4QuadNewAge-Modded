using System;
using SD = System.Drawing;
using System.Windows.Media;
using Re4QuadExtremeEditor.src;

namespace Re4QuadExtremeEditor
{
    /// <summary>
    /// Central dual-palette theme service. Every window (WinForms via DarkTheme,
    /// WPF wizard/options directly) reads its colors from here so Dark Mode and
    /// Light Mode are pixel-for-pixel mirrors of each other.
    /// </summary>
    internal static class UiTheme
    {
        public static bool IsLight
        {
            get { return Globals.BackupConfigs != null && Globals.BackupConfigs.UseLightTheme; }
        }

        private static SD.Color Hex(int rgb)
        {
            return SD.Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        // ---------------- WinForms palette (consumed by DarkTheme) ----------------

        public static SD.Color WinWindow        { get { return IsLight ? Hex(0xF2F4F7) : Hex(0x111418); } }
        public static SD.Color WinSurface       { get { return IsLight ? Hex(0xFFFFFF) : Hex(0x161A1F); } }
        public static SD.Color WinSurface2      { get { return IsLight ? Hex(0xF7F9FB) : Hex(0x1C2127); } }
        public static SD.Color WinSurface3      { get { return IsLight ? Hex(0xEDF1F5) : Hex(0x22282F); } }
        public static SD.Color WinInput         { get { return IsLight ? Hex(0xFFFFFF) : Hex(0x13171C); } }
        public static SD.Color WinBorder        { get { return IsLight ? Hex(0xC5CDD6) : Hex(0x313943); } }
        public static SD.Color WinBorderSoft    { get { return IsLight ? Hex(0xDCE2E8) : Hex(0x252C34); } }
        public static SD.Color WinText          { get { return IsLight ? Hex(0x1B2027) : Hex(0xECEFF4); } }
        public static SD.Color WinTextSecondary { get { return IsLight ? Hex(0x5B6674) : Hex(0xA0AAB7); } }
        public static SD.Color WinDisabled      { get { return IsLight ? Hex(0x9AA5B1) : Hex(0x66707E); } }
        public static SD.Color WinAccent        { get { return IsLight ? Hex(0x3577DC) : Hex(0x5F99E0); } }
        public static SD.Color WinAccentHover   { get { return IsLight ? Hex(0x4B89E6) : Hex(0x74ABEB); } }
        public static SD.Color WinAccentPressed { get { return IsLight ? Hex(0x2763BC) : Hex(0x4577B5); } }
        public static SD.Color WinSelection     { get { return IsLight ? Hex(0xE4EEFB) : Hex(0x2D3139); } }
        public static SD.Color WinMenuHover     { get { return IsLight ? Hex(0xEBF2FA) : Hex(0x1E242B); } }
        public static SD.Color WinSelectionText { get { return IsLight ? Hex(0x12181F) : Hex(0xF9FAFC); } }

        public static SD.Color WinTitleBarCaption
        {
            get { return IsLight ? Hex(0xF3F5F8) : Hex(0x1B1E24); }
        }

        // ---------------- WPF palette (wizard + options windows) ----------------

        private static System.Windows.Media.Color M(int rgb)
        {
            return System.Windows.Media.Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        private static SolidColorBrush NewBrush(int rgb)
        {
            SolidColorBrush b = new SolidColorBrush(M(rgb));
            return b;
        }

        /// <summary>
        /// A private, per-window set of unfrozen WPF brushes. WPF force-freezes
        /// brushes used inside styles/templates, so shared global brush instances
        /// would become read-only; giving each window its own palette avoids that
        /// while keeping every window in perfect sync with the active theme
        /// (windows created after a theme switch simply build a new palette).
        /// </summary>
        public sealed class Palette
        {
            public SolidColorBrush BWindow;
            public SolidColorBrush BBar;
            public SolidColorBrush BSurface;
            public SolidColorBrush BInput;
            public SolidColorBrush BBorder;
            public SolidColorBrush BBorderSoft;
            public SolidColorBrush BText;
            public SolidColorBrush BSub;
            public SolidColorBrush BAccent;
            public SolidColorBrush BAccentHover;
            public SolidColorBrush BHoverSurface;
            public SolidColorBrush BPressSurface;
            public SolidColorBrush BDotIdle;
            public SolidColorBrush BThumb;
            public SolidColorBrush BThumbHover;
            public SolidColorBrush BKnob;
            public SolidColorBrush BSwitchOff;
            public SolidColorBrush BRadioSel;

            public System.Windows.Media.Color MAccent;
            public System.Windows.Media.Color MSwitchOff;

            internal Palette()
            {
                ApplyColors();
            }

            public void UpdateColors()
            {
                ApplyColors();
                _scrollBarStyle = null;
            }

            private void ApplyColors()
            {
                bool light = IsLight;
                if (light)
                {
                    InitOrSet(ref BWindow, 0xF2F4F7);
                    InitOrSet(ref BBar, 0xE9EDF2);
                    InitOrSet(ref BSurface, 0xFFFFFF);
                    InitOrSet(ref BInput, 0xFFFFFF);
                    InitOrSet(ref BBorder, 0xC9D1DA);
                    InitOrSet(ref BBorderSoft, 0xDEE3E9);
                    InitOrSet(ref BText, 0x1B2027);
                    InitOrSet(ref BSub, 0x5B6674);
                    InitOrSet(ref BAccent, 0x3577DC);
                    InitOrSet(ref BAccentHover, 0x4B89E6);
                    InitOrSet(ref BHoverSurface, 0xEDF2F8);
                    InitOrSet(ref BPressSurface, 0xE2EAF2);
                    InitOrSet(ref BDotIdle, 0xD4DBE3);
                    InitOrSet(ref BThumb, 0xC3CCD6);
                    InitOrSet(ref BThumbHover, 0xA9B5C2);
                    InitOrSet(ref BKnob, 0xFFFFFF);
                    InitOrSet(ref BSwitchOff, 0xCBD3DC);
                    InitOrSet(ref BRadioSel, 0xE2EDFB);
                    MAccent = M(0x3577DC);
                    MSwitchOff = M(0xCBD3DC);
                }
                else
                {
                    InitOrSet(ref BWindow, 0x14181E);
                    InitOrSet(ref BBar, 0x10141A);
                    InitOrSet(ref BSurface, 0x1B2129);
                    InitOrSet(ref BInput, 0x20262F);
                    InitOrSet(ref BBorder, 0x2A313B);
                    InitOrSet(ref BBorderSoft, 0x22282F);
                    InitOrSet(ref BText, 0xE8ECF1);
                    InitOrSet(ref BSub, 0x96A0AC);
                    InitOrSet(ref BAccent, 0x4E93E8);
                    InitOrSet(ref BAccentHover, 0x63A4F2);
                    InitOrSet(ref BHoverSurface, 0x232B35);
                    InitOrSet(ref BPressSurface, 0x181E26);
                    InitOrSet(ref BDotIdle, 0x333D49);
                    InitOrSet(ref BThumb, 0x39434F);
                    InitOrSet(ref BThumbHover, 0x4C5968);
                    InitOrSet(ref BKnob, 0xF2F5F8);
                    InitOrSet(ref BSwitchOff, 0x39424E);
                    InitOrSet(ref BRadioSel, 0x243347);
                    MAccent = M(0x4E93E8);
                    MSwitchOff = M(0x39424E);
                }
            }

            private static void InitOrSet(ref SolidColorBrush field, int rgb)
            {
                if (field == null || field.IsFrozen)
                    field = new SolidColorBrush(M(rgb));
                else
                    field.Color = M(rgb);
            }
        }

        /// <summary>Builds a fresh unfrozen brush set for one window.</summary>
        public static Palette CreatePalette()
        {
            return new Palette();
        }
        // ---------------- themed scrollbar template (shared) ----------------

        private static System.Windows.Style _scrollBarStyle;
        private static bool _scrollBarWasLight;

        public static System.Windows.Style ScrollBarStyle()
        {
            bool light = IsLight;
            if (_scrollBarStyle != null && _scrollBarWasLight == light) { return _scrollBarStyle; }

            string thumb = light ? "#C3CCD6" : "#39434F";
            string thumbHover = light ? "#A9B5C2" : "#4C5968";
            string thumbDrag = light ? "#93A1B0" : "#5A6878";

            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                " xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'" +
                " TargetType='{x:Type ScrollBar}'>" +
                "  <Grid Background='Transparent'>" +
                "    <Track x:Name='PART_Track' IsDirectionReversed='true'>" +
                "      <Track.DecreaseRepeatButton>" +
                "        <RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'>" +
                "          <RepeatButton.Template>" +
                "            <ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate>" +
                "          </RepeatButton.Template>" +
                "        </RepeatButton>" +
                "      </Track.DecreaseRepeatButton>" +
                "      <Track.IncreaseRepeatButton>" +
                "        <RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'>" +
                "          <RepeatButton.Template>" +
                "            <ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate>" +
                "          </RepeatButton.Template>" +
                "        </RepeatButton>" +
                "      </Track.IncreaseRepeatButton>" +
                "      <Track.Thumb>" +
                "        <Thumb>" +
                "          <Thumb.Template>" +
                "            <ControlTemplate TargetType='Thumb'>" +
                "              <Border x:Name='bd' Background='" + thumb + "' CornerRadius='4' Padding='2,0'/>" +
                "              <ControlTemplate.Triggers>" +
                "                <Trigger Property='IsMouseOver' Value='True'>" +
                "                  <Setter TargetName='bd' Property='Background' Value='" + thumbHover + "'/>" +
                "                </Trigger>" +
                "                <Trigger Property='IsDragging' Value='True'>" +
                "                  <Setter TargetName='bd' Property='Background' Value='" + thumbDrag + "'/>" +
                "                </Trigger>" +
                "              </ControlTemplate.Triggers>" +
                "            </ControlTemplate>" +
                "          </Thumb.Template>" +
                "        </Thumb>" +
                "      </Track.Thumb>" +
                "    </Track>" +
                "  </Grid>" +
                "</ControlTemplate>";

            System.Windows.Controls.ControlTemplate tpl =
                (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);

            System.Windows.Style st = new System.Windows.Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
            st.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.TemplateProperty, tpl));
            st.Setters.Add(new System.Windows.Setter(System.Windows.FrameworkElement.WidthProperty, 10.0));

            _scrollBarStyle = st;
            _scrollBarWasLight = light;
            return st;
        }
    }
}
