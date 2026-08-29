using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using WinForms = System.Windows.Forms;
using IoPath = System.IO.Path;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src.Forms
{
    public class WelcomeSetupForm : System.Windows.Window
    {
        private const int PageCount = 3;
        private static readonly string[] StepNames = { "Welcome", "Game Folders", "Tools" };

        private int currentStep;
        private bool animating;

        private Grid pageHost;
        private Grid[] pages;

        private Border[] chipBorders;
        private TextBlock[] chipNums;
        private TextBlock[] chipLabels;
        private Rectangle[] chipLines;
        private Ellipse[] chipDots;

        private Button buttonBack;
        private Button buttonNext;
        private Button buttonSkip;

        private readonly List<JSON.LangObjForList> langs = new List<JSON.LangObjForList>();
        private readonly List<string> langNames = new List<string>();
        private int selectedLangIndex;
        private Border langDropdown;
        private TextBlock langDropdownText;
        private Popup langPopup;
        private StackPanel langListPanel;

        internal static readonly Color[] SkyPresets =
        {
            Colors.CornflowerBlue,
            Color.FromRgb(0x87, 0xCE, 0xEB),
            Color.FromRgb(0x46, 0x82, 0xB4),
            Color.FromRgb(0xAF, 0xEE, 0xEE),
            Color.FromRgb(0xE6, 0xE6, 0xFA),
            Colors.White,
            Color.FromRgb(0x80, 0x80, 0x80),
            Color.FromRgb(0x4A, 0x50, 0x58),
            Color.FromRgb(0x06, 0x07, 0x08)
        };

        private Color selectedSkyColor = Colors.CornflowerBlue;
        private System.Drawing.Color skyOriginalLive = System.Drawing.Color.FromArgb(0xFF, 0x94, 0xD2, 0xFF);
        private bool finishedLive;
        private bool langOriginalLoaded;
        private string langOriginalFile = "";
        private Border[] skySwatches;
        private int selectedSkyIndex;
        private Border skyPreviewChip;
        private Func<bool> getThemeDark;
        private Func<bool> getInvertMouse;
        private Func<bool> getForceReload;

        private TextBox txtXFILE, txt2007, txtPS2, txtUHD, txtPS4NS, txtCustom1, txtCustom2, txtCustom3;
        private TextBlock lblDetect;
        private TextBox toolUdas, toolLfs, toolPack, toolGca;

        // Per-window unfrozen palette snapshot (see UiTheme.Palette).
        private UiTheme.Palette P = UiTheme.CreatePalette();
        private bool retheming;

        public WelcomeSetupForm()
        {
            Width = 560;
            Height = 432;
            MinWidth = 500;
            MinHeight = 404;
            MaxWidth = 700;
            MaxHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = P.BWindow;
            FontFamily = new FontFamily("Segoe UI");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            System.Windows.Shell.WindowChrome chrome = new System.Windows.Shell.WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(5);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            LoadState();
            BuildUi();
            FillFields();
            ShowStepImmediate(0);

            PreviewMouseLeftButtonDown += CloseLangPopupIfOutside;
            Deactivated += delegate
            {
                if (langPopup != null) { langPopup.IsOpen = false; }
            };
            Closed += delegate
            {
                if (!finishedLive)
                {
                    Globals.SkyColor = skyOriginalLive;
                    if (Globals.BackupConfigs != null) { Globals.BackupConfigs.SkyColor = skyOriginalLive; }
                }
            };

            PreviewKeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { SkipWizard(); e.Handled = true; }
                else if (e.Key == Key.Enter && !animating) { Advance(); e.Handled = true; }
            };
        }


        // ================================================================
        // state
        // ================================================================

        private void LoadState()
        {
            JSON.Configs cfg = Globals.BackupConfigs;

            if (DataBase.CachedLangList != null) { langs.AddRange(DataBase.CachedLangList); }
            else { langs.AddRange(GetLangList()); }

            if (cfg != null)
            {
                selectedSkyColor = cfg.SkyColor.IsEmpty
                    ? Colors.CornflowerBlue
                    : Color.FromArgb(cfg.SkyColor.A, cfg.SkyColor.R, cfg.SkyColor.G, cfg.SkyColor.B);

                skyOriginalLive = Globals.SkyColor;
                langOriginalLoaded = cfg.LoadLangTranslation;
                langOriginalFile = cfg.LangJsonFile ?? "";
            }
        }

        // ================================================================
        // ui shell
        // ================================================================

        private void BuildUi()
        {
            Grid root = new Grid { Background = P.BWindow };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            BuildHeader(root);
            BuildStepChips(root);
            BuildPageHost(root);
            BuildFooter(root);

            Content = root;
        }

        private void BuildHeader(Grid root)
        {
            Border header = new Border
            {
                Height = 36,
                Background = P.BBar,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            titleStack.MouseLeftButtonDown += delegate
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) { DragMove(); }
            };
            TextBlock t1 = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            RegText(t1, eLang.Wizard_Title, "Setup Wizard");
            t1.MouseLeftButtonDown += delegate
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) { DragMove(); }
            };
            titleStack.Children.Add(t1);
            TextBlock t2 = new TextBlock
            {
                Text = "\u2003RE4 Quad Extreme Editor \u2014 modified edition",
                Foreground = P.BSub,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            t2.MouseLeftButtonDown += delegate
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) { DragMove(); }
            };
            titleStack.Children.Add(t2);

            Grid.SetColumn(titleStack, 0);
            g.Children.Add(titleStack);

            Button close = MakeButton("\u2715", false, 38, 35, 0);
            close.Foreground = P.BSub;
            close.FontSize = 11;
            close.FontWeight = FontWeights.Normal;
            close.Padding = new Thickness(0);
            close.BorderBrush = Brushes.Transparent;
            close.Click += delegate { SkipWizard(); };
            Grid.SetColumn(close, 1);
            g.Children.Add(close);

            header.Child = g;
            Grid.SetRow(header, 0);
            root.Children.Add(header);
        }

        private void BuildStepChips(Grid root)
        {
            Border strip = new Border
            {
                Height = 32,
                Background = P.BWindow,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            Grid g = new Grid { Margin = new Thickness(14, 0, 14, 0) };
            for (int c = 0; c < PageCount; c++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            chipBorders = new Border[PageCount];
            chipNums = new TextBlock[PageCount];
            chipLabels = new TextBlock[PageCount];
            chipLines = new Rectangle[PageCount];
            chipDots = new Ellipse[PageCount];

            for (int i = 0; i < PageCount; i++)
            {
                int idx = i;

                Grid chip = new Grid();
                chip.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                chip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                StackPanel content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid dotWrap = new Grid { Width = 18, Height = 18, Margin = new Thickness(0, 0, 7, 0) };
                Ellipse dot = new Ellipse { Fill = P.BDotIdle };
                dotWrap.Children.Add(dot);
                chipDots[i] = dot;

                TextBlock num = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = P.BText,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dotWrap.Children.Add(num);

                content.Children.Add(dotWrap);

                TextBlock label = new TextBlock
                {
                    Foreground = P.BSub,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RegText(label, i == 0 ? eLang.Wizard_Step1 : (i == 1 ? eLang.Wizard_Step2 : eLang.Wizard_Step3), StepNames[i]);
                content.Children.Add(label);

                Rectangle line = new Rectangle
                {
                    Height = 2,
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = P.BAccent,
                    Margin = new Thickness(0, 0, 0, -1),
                    Visibility = Visibility.Collapsed
                };

                Grid.SetRow(content, 0);
                chip.Children.Add(content);
                Grid.SetRow(line, 1);
                chip.Children.Add(line);

                Border wrapper = new Border
                {
                    Background = Brushes.Transparent,
                    Child = chip,
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(8, 0, 4, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                wrapper.MouseEnter += delegate { if (idx != currentStep) { chipLabels[idx].Foreground = P.BText; } };
                wrapper.MouseLeave += delegate { if (idx != currentStep) { chipLabels[idx].Foreground = P.BSub; } };
                wrapper.MouseLeftButtonUp += delegate { Navigate(idx); };

                chipBorders[i] = wrapper;
                chipNums[i] = num;
                chipLabels[i] = label;
                chipLines[i] = line;

                Grid.SetColumn(wrapper, i);
                g.Children.Add(wrapper);
            }

            strip.Child = g;
            Grid.SetRow(strip, 1);
            root.Children.Add(strip);
        }

        private void BuildPageHost(Grid root)
        {
            pageHost = new Grid { ClipToBounds = true, Background = P.BWindow };
            Grid.SetRow(pageHost, 2);
            root.Children.Add(pageHost);

            pages = new Grid[PageCount];
            pages[0] = BuildPageWelcome();
            pages[1] = BuildPageFolders();
            pages[2] = BuildPageTools();
            for (int i = 0; i < PageCount; i++)
            {
                pages[i].Visibility = Visibility.Collapsed;
                pageHost.Children.Add(pages[i]);
            }
        }

        private void BuildFooter(Grid root)
        {
            Border footer = new Border
            {
                Height = 50,
                Background = P.BBar,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            buttonBack = MakeButton("<  Back", false, double.NaN, 32, 1);
            buttonBack.MinWidth = 84;
            buttonBack.Margin = new Thickness(14, 0, 0, 0);
            buttonBack.FontSize = 12;
            RegButton(buttonBack, eLang.Wizard_Back, "<  Back");
            buttonBack.Click += delegate { Navigate(currentStep - 1); };
            Grid.SetColumn(buttonBack, 0);
            g.Children.Add(buttonBack);

            buttonSkip = MakeLinkButton("Skip setup");
            buttonSkip.Margin = new Thickness(14, 0, 0, 0);
            buttonSkip.VerticalAlignment = VerticalAlignment.Center;
            wizardTextRefreshers.Add(delegate { buttonSkip.Content = T(eLang.Wizard_Skip, "Skip setup"); });
            buttonSkip.Click += delegate { SkipWizard(); };
            Grid.SetColumn(buttonSkip, 1);
            g.Children.Add(buttonSkip);

            buttonNext = MakeButton("Next  >", true, double.NaN, 32, 1);
            buttonNext.MinWidth = 112;
            buttonNext.Margin = new Thickness(0, 0, 14, 0);
            buttonNext.FontSize = 12.5;
            UpdateNavButtonText();
            wizardTextRefreshers.Add(UpdateNavButtonText);
            buttonNext.Click += delegate { Advance(); };
            Grid.SetColumn(buttonNext, 3);
            g.Children.Add(buttonNext);

            footer.Child = g;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
        }

        // ================================================================
        // factories
        // ================================================================

        private Button MakeButton(string text, bool primary, double w, double h, double radius)
        {
            Button b = new Button
            {
                Content = text,
                Width = double.IsNaN(w) ? double.NaN : w,
                Height = h,
                Cursor = Cursors.Hand,
                Focusable = false,
                Foreground = primary ? Brushes.White : P.BText,
                FontWeight = FontWeights.SemiBold
            };

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "bd");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, primary ? P.BAccent : P.BSurface);
            border.SetValue(Border.BorderBrushProperty, primary ? Brushes.Transparent : P.BBorder);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.PaddingProperty, new Thickness(8, 0, 8, 0));

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            ControlTemplate tpl = new ControlTemplate(typeof(Button)) { VisualTree = border };

            Trigger over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, primary ? P.BAccentHover : P.BHoverSurface) { TargetName = "bd" });
            tpl.Triggers.Add(over);

            Trigger press = new Trigger { Property = Button.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(Border.BackgroundProperty, primary ? P.BAccent : P.BPressSurface) { TargetName = "bd" });
            tpl.Triggers.Add(press);

            b.Template = tpl;
            return b;
        }

        private Button MakeLinkButton(string text)
        {
            Button b = new Button
            {
                Content = text,
                Cursor = Cursors.Hand,
                Focusable = false,
                Foreground = P.BSub,
                FontSize = 11.5
            };

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            border.AppendChild(content);

            ControlTemplate tpl = new ControlTemplate(typeof(Button)) { VisualTree = border };

            Trigger over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Control.ForegroundProperty, P.BAccent));
            tpl.Triggers.Add(over);

            b.Template = tpl;
            return b;
        }

        private Style TextBoxStyle()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "bd");
            border.SetValue(Border.BackgroundProperty, P.BInput);
            border.SetValue(Border.BorderBrushProperty, P.BBorder);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(1));
            border.SetValue(Border.PaddingProperty, new Thickness(7, 0, 4, 0));

            FrameworkElementFactory sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            sv.Name = "PART_ContentHost";
            border.AppendChild(sv);

            ControlTemplate tpl = new ControlTemplate(typeof(TextBox)) { VisualTree = border };

            Trigger focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, P.BAccent) { TargetName = "bd" });
            tpl.Triggers.Add(focus);

            return new Style(typeof(TextBox))
            {
                Setters =
                {
                    new Setter(Control.TemplateProperty, tpl),
                    new Setter(Control.ForegroundProperty, P.BText),
                    new Setter(TextBox.CaretBrushProperty, P.BText),
                    new Setter(Control.BackgroundProperty, Brushes.Transparent),
                    new Setter(FrameworkElement.HeightProperty, 26.0),
                    new Setter(Control.FontSizeProperty, 12.5)
                }
            };
        }

        private FrameworkElement MakeSwitch(string label, bool initial, out Func<bool> getter)
        {
            return MakeSwitch(label, initial, out getter, null, 0, null);
        }

        private FrameworkElement MakeSwitch(string label, bool initial, out Func<bool> getter, List<Action> refresherList, eLang langId, string langFallback, Action<bool> onChanged = null)
        {
            bool state = initial;

            StackPanel root = new StackPanel { Orientation = Orientation.Horizontal };

            Grid trackWrap = new Grid
            {
                Width = 40,
                Height = 20,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            Border track = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = state ? (System.Windows.Media.Brush)P.BAccent : (System.Windows.Media.Brush)P.BSwitchOff
            };
            trackWrap.Children.Add(track);

            Ellipse knob = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = P.BKnob,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            TranslateTransform knobMove = new TranslateTransform(state ? 17 : 0, 0);
            knob.RenderTransform = knobMove;
            trackWrap.Children.Add(knob);

            MouseButtonEventHandler toggle = delegate
            {
                state = !state;
                DoubleAnimation move = new DoubleAnimation(
                    state ? 17 : 0,
                    new Duration(TimeSpan.FromMilliseconds(160)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                knobMove.BeginAnimation(TranslateTransform.XProperty, move);

                SolidColorBrush animBrush = new SolidColorBrush(
                    state ? P.MSwitchOff : P.MAccent);
                track.Background = animBrush;
                ColorAnimation colorAnim = new ColorAnimation(
                    state ? P.MAccent : P.MSwitchOff,
                    new Duration(TimeSpan.FromMilliseconds(160)));
                animBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                colorAnim.Completed += delegate
                {
                    track.Background = state
                        ? (System.Windows.Media.Brush)P.BAccent
                        : (System.Windows.Media.Brush)P.BSwitchOff;
                };

                if (onChanged != null) { onChanged(state); }
            };
            trackWrap.MouseLeftButtonDown += toggle;

            TextBlock lbl = new TextBlock
            {
                Text = label,
                Foreground = P.BText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 2, 0, 0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (refresherList != null)
            {
                refresherList.Add(delegate { lbl.Text = T(langId, langFallback); });
                lbl.Text = T(langId, langFallback);
            }
            lbl.MouseLeftButtonDown += delegate { toggle(null, null); };

            root.Children.Add(trackWrap);
            root.Children.Add(lbl);

            getter = () => state;
            return root;
        }

        // ================================================================
        // page builders
        // ================================================================

        private StackPanel PageShell(Grid page, eLang titleId, string titleFallback, eLang subId, string subFallback)
        {
            page.Margin = new Thickness(14, 8, 14, 6);

            StackPanel sp = new StackPanel();
            page.Children.Add(sp);

            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock titleTb = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            RegText(titleTb, titleId, titleFallback);
            head.Children.Add(titleTb);
            sp.Children.Add(head);

            if (!string.IsNullOrEmpty(subFallback))
            {
                TextBlock subTb = new TextBlock
                {
                    Foreground = P.BSub,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                RegText(subTb, subId, subFallback);
                sp.Children.Add(subTb);
            }

            return sp;
        }

        private void Section(StackPanel sp, eLang id, string fallback)
        {
            TextBlock tb = new TextBlock
            {
                Foreground = P.BAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            wizardTextRefreshers.Add(delegate { tb.Text = T(id, fallback).ToUpperInvariant(); });
            tb.Text = T(id, fallback).ToUpperInvariant();
            sp.Children.Add(tb);
        }

        private Grid BuildPageWelcome()
        {
            Grid page = new Grid();
            StackPanel sp = PageShell(page, eLang.Wizard_WelcomeTitle, "Welcome.", eLang.Wizard_WelcomeSub, "Personalize the editor \u2014 everything can be changed later in Options.");

            Border credit = new Border
            {
                Background = P.BSurface,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(0, 10, 0, 0)
            };
            StackPanel creditSp = new StackPanel { Orientation = Orientation.Horizontal };
            Rectangle bar = new Rectangle
            {
                Width = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = P.BAccent,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 0, 10, 0)
            };
            creditSp.Children.Add(bar);
            StackPanel creditText = new StackPanel();
            TextBlock creditRights = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold
            };
            RegText(creditRights, eLang.Wizard_CreditsRights, "All rights belong to JADERLINK.");
            creditText.Children.Add(creditRights);
            TextBlock creditEdition = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 0)
            };
            RegText(creditEdition, eLang.Wizard_CreditsEdition, "Professionally modified edition by Nawaf.");
            creditText.Children.Add(creditEdition);
            creditSp.Children.Add(creditText);
            credit.Child = creditSp;
            sp.Children.Add(credit);

            Section(sp, eLang.Wizard_SectionLanguage, "Language");

            langNames.Clear();
            langNames.Add(Lang.GetText(eLang.OptionsUseInternalLanguage));
            for (int i = 0; i < langs.Count; i++)
            {
                langNames.Add(langs[i].LangName);
            }

            Grid comboRow = new Grid();
            comboRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            comboRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            langDropdown = new Border
            {
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(1),
                Padding = new Thickness(10, 5, 9, 6),
                Width = 250,
                Cursor = Cursors.Hand
            };
            Grid comboGrid = new Grid();
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            langDropdownText = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(langDropdownText, 0);
            comboGrid.Children.Add(langDropdownText);
            TextBlock arrowGlyph = new TextBlock
            {
                Text = "\u25BE",
                Foreground = P.BSub,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(arrowGlyph, 1);
            comboGrid.Children.Add(arrowGlyph);
            langDropdown.Child = comboGrid;

            langDropdown.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (langPopup == null) { return; }
                if (langPopup.IsOpen)
                {
                    langPopup.IsOpen = false;
                }
                else
                {
                    RefreshLangItems();
                    langPopup.IsOpen = true;
                }
            };

            Grid.SetColumn(langDropdown, 0);
            comboRow.Children.Add(langDropdown);

            langListPanel = new StackPanel();
            for (int i = 0; i < langNames.Count; i++)
            {
                int idx = i;
                StackPanel itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                TextBlock itemName = new TextBlock
                {
                    Text = langNames[i],
                    Foreground = P.BText,
                    FontSize = 11.5
                };
                itemContent.Children.Add(itemName);
                TextBlock itemCheck = new TextBlock
                {
                    Text = "\u2713",
                    Foreground = P.BAccent,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10, 0, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                itemContent.Children.Add(itemCheck);

                Border item = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(1),
                    Padding = new Thickness(9, 5, 14, 6),
                    Child = itemContent,
                    Cursor = Cursors.Hand
                };
                item.MouseEnter += delegate { if (idx != selectedLangIndex) { item.Background = P.BHoverSurface; } };
                item.MouseLeave += delegate { if (idx != selectedLangIndex) { item.Background = Brushes.Transparent; } };
                item.MouseLeftButtonUp += delegate
                {
                    selectedLangIndex = idx;
                    UpdateLangDropdown();
                    RefreshLangItems();
                    if (langPopup != null) { langPopup.IsOpen = false; }
                    ApplyLiveLanguage();
                };

                TagPair pair = new TagPair { Name = itemName, Check = itemCheck, Box = item };
                item.Tag = pair;
                langListPanel.Children.Add(item);
            }

            ScrollViewer langScroll = new ScrollViewer
            {
                MaxHeight = 190,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = langListPanel
            };
            langScroll.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();

            Border dropDownBox = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3),
                Child = langScroll,
                MinWidth = 250
            };

            langPopup = new Popup
            {
                PlacementTarget = langDropdown,
                Placement = PlacementMode.Bottom,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = dropDownBox
            };

            sp.Children.Add(comboRow);

            UpdateLangDropdown();
            RefreshLangItems();

            wizardTextRefreshers.Add(delegate
            {
                if (langNames.Count > 0) { langNames[0] = Lang.GetText(eLang.OptionsUseInternalLanguage); }
                UpdateLangDropdown();
                RefreshLangItems();
            });

            Section(sp, eLang.Wizard_SectionAppearance, "Appearance");

            StackPanel skyGroup = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock skyLbl = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            RegText(skyLbl, eLang.Wizard_SkyColor, "Sky color");
            skyGroup.Children.Add(skyLbl);

            skyPreviewChip = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(selectedSkyColor),
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            skyGroup.Children.Add(skyPreviewChip);

            Color[] presets = SkyPresets;
            skySwatches = new Border[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                int idx = i;
                Border ring = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Border chip = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(presets[i])
                };
                ring.Child = chip;
                ring.MouseLeftButtonUp += delegate
                {
                    selectedSkyIndex = idx;
                    selectedSkyColor = presets[idx];
                    RefreshSkySwatches();
                    SetLiveSky(ToDrawing(presets[idx]));
                };
                skySwatches[i] = ring;
                skyGroup.Children.Add(ring);
            }

            Button custom = MakeButton("Custom", false, double.NaN, 22, 1);
            custom.FontSize = 9.5;
            custom.FontWeight = FontWeights.Normal;
            custom.VerticalAlignment = VerticalAlignment.Center;
            custom.Margin = new Thickness(3, 0, 0, 0);
            RegButton(custom, eLang.Wizard_CustomPreset, "Custom");
            custom.Click += delegate
            {
                Color? picked = ShowCustomColorDialog(selectedSkyColor,
                    delegate(Color c) { SetLiveSky(ToDrawing(c)); });
                if (picked.HasValue)
                {
                    selectedSkyColor = picked.Value;
                    selectedSkyIndex = -1;
                    RefreshSkySwatches();
                    SetLiveSky(ToDrawing(picked.Value));
                }
                else
                {
                    selectedSkyIndex = -1;
                    RefreshSkySwatches();
                    SetLiveSky(skyOriginalLive);
                }
            };
            skyGroup.Children.Add(custom);

            sp.Children.Add(skyGroup);

            bool themeInitial = Globals.BackupConfigs != null && Globals.BackupConfigs.UseDarkerGrayTheme;
            FrameworkElement themeSwitch = MakeSwitch("Use the darker theme", themeInitial, out getThemeDark, wizardTextRefreshers, eLang.Wizard_ThemeSwitch, "Use the darker theme",
                delegate(bool on)
                {
                    if (Globals.BackupConfigs != null)
                    {
                        Globals.BackupConfigs.UseDarkerGrayTheme = on;
                        Globals.BackupConfigs.UseLightTheme = !on;
                    }
                    Dispatcher.BeginInvoke(new Action(RethemeWindow),
                        System.Windows.Threading.DispatcherPriority.Background);
                    foreach (WinForms.Form f in WinForms.Application.OpenForms)
                    {
                        MainForm mf = f as MainForm;
                        if (mf != null)
                        {
                            MainForm mfCaptured = mf;
                            mf.BeginInvoke(new Action(delegate
                            {
                                try { mfCaptured.ApplyThemeLive(); } catch { }
                            }));
                            break;
                        }
                    }
                });
            themeSwitch.Margin = new Thickness(0, 11, 0, 0);
            sp.Children.Add(themeSwitch);

            Section(sp, eLang.Wizard_SectionMouse, "Mouse");

            bool invertInitial = Globals.BackupConfigs != null && Globals.BackupConfigs.UseInvertedMouseButtons;
            FrameworkElement mouseSwitch = MakeSwitch("Invert mouse buttons (left camera, right select)", invertInitial, out getInvertMouse, wizardTextRefreshers, eLang.Wizard_MouseSwitch, "Invert mouse buttons (left camera, right select)");
            sp.Children.Add(mouseSwitch);

            return page;
        }

        private Color? ShowCustomColorDialog(Color initial, Action<Color> onLive)
        {
            Color? result = null;
            double hh, ss, vv;
            ColorToHsv(initial, out hh, out ss, out vv);
            bool suppressTextSync = false;

            Window dlg = new Window();
            dlg.Title = T(eLang.Wizard_CustomColorTitle, "Custom color");
            dlg.Width = 356;
            dlg.SizeToContent = SizeToContent.Height;
            dlg.ResizeMode = ResizeMode.NoResize;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dlg.Owner = this;
            dlg.WindowStyle = WindowStyle.None;
            dlg.ShowInTaskbar = false;
            dlg.Background = P.BWindow;
            dlg.FontFamily = new FontFamily("Segoe UI");

            System.Windows.Shell.WindowChrome chrome = new System.Windows.Shell.WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            System.Windows.Shell.WindowChrome.SetWindowChrome(dlg, chrome);

            Border shell = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };
            dlg.Content = shell;

            Grid outer = new Grid();
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.Child = outer;

            Border titleBar = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(14, 8, 4, 9),
                Cursor = Cursors.Hand
            };
            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock titleText = new TextBlock
            {
                Text = T(eLang.Wizard_CustomColorTitle, "Custom color"),
                Foreground = P.BText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleGrid.Children.Add(titleText);
            Button closeBtn = MakeButton("\u2715", false, 38, 24, 1);
            closeBtn.FontSize = 11;
            closeBtn.Padding = new Thickness(0);
            closeBtn.Click += delegate { dlg.Close(); };
            Grid.SetColumn(closeBtn, 1);
            titleGrid.Children.Add(closeBtn);
            titleBar.Child = titleGrid;
            titleBar.MouseLeftButtonDown += delegate
            {
                try { dlg.DragMove(); } catch (InvalidOperationException) { }
            };
            Grid.SetRow(titleBar, 0);
            outer.Children.Add(titleBar);

            StackPanel content = new StackPanel { Margin = new Thickness(16, 4, 16, 12) };

            Grid svSquare = new Grid
            {
                Width = 322,
                Height = 168,
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            Rectangle svBase = new Rectangle();
            Rectangle svWhiteOverlay = new Rectangle();
            LinearGradientBrush whiteGrad = new LinearGradientBrush();
            whiteGrad.StartPoint = new Point(0, 0);
            whiteGrad.EndPoint = new Point(1, 0);
            whiteGrad.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            whiteGrad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            svWhiteOverlay.Fill = whiteGrad;
            Rectangle svBlackOverlay = new Rectangle();
            LinearGradientBrush blackGrad = new LinearGradientBrush();
            blackGrad.StartPoint = new Point(0, 0);
            blackGrad.EndPoint = new Point(0, 1);
            blackGrad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
            blackGrad.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
            svBlackOverlay.Fill = blackGrad;
            Border svThumb = new Border
            {
                Width = 13,
                Height = 13,
                CornerRadius = new CornerRadius(7),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            svSquare.Children.Add(svBase);
            svSquare.Children.Add(svWhiteOverlay);
            svSquare.Children.Add(svBlackOverlay);
            svSquare.Children.Add(svThumb);
            content.Children.Add(svSquare);

            Grid hueBar = new Grid
            {
                Width = 322,
                Height = 14,
                Margin = new Thickness(0, 10, 0, 0),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            Rectangle hueBase = new Rectangle
            {
                RadiusX = 2,
                RadiusY = 2
            };
            LinearGradientBrush hueGrad = new LinearGradientBrush();
            hueGrad.StartPoint = new Point(0, 0);
            hueGrad.EndPoint = new Point(1, 0);
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6));
            hueGrad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));
            hueBase.Fill = hueGrad;
            Border hueThumb = new Border
            {
                Width = 9,
                Height = 20,
                CornerRadius = new CornerRadius(2),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            hueBar.Children.Add(hueBase);
            hueBar.Children.Add(hueThumb);
            content.Children.Add(hueBar);

            Grid valueRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border previewBorder = new Border
            {
                Width = 56,
                Height = 44,
                CornerRadius = new CornerRadius(2),
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(previewBorder, 0);
            valueRow.Children.Add(previewBorder);

            TextBox hexBox = BuildDarkTextBox(96);
            hexBox.Margin = new Thickness(10, 0, 0, 0);
            hexBox.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hexBox, 2);
            valueRow.Children.Add(hexBox);

            content.Children.Add(valueRow);

            Grid rgbRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            for (int i = 0; i < 3; i++)
            {
                rgbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < 2) { rgbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); }
            }
            TextBox tbR = BuildDarkTextBox(double.NaN);
            TextBox tbG = BuildDarkTextBox(double.NaN);
            TextBox tbB = BuildDarkTextBox(double.NaN);
            Grid.SetColumn(tbR, 0);
            Grid.SetColumn(tbG, 2);
            Grid.SetColumn(tbB, 4);
            rgbRow.Children.Add(tbR);
            rgbRow.Children.Add(tbG);
            rgbRow.Children.Add(tbB);
            content.Children.Add(rgbRow);

            Grid footer = new Grid { Margin = new Thickness(16, 2, 16, 14) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button okBtn = MakeButton(T(eLang.Wizard_OK, "OK"), true, 84, 27, 1);
            Button cancelBtn = MakeButton(T(eLang.Wizard_Cancel, "Cancel"), false, 84, 27, 1);
            cancelBtn.Click += delegate { dlg.Close(); };
            okBtn.Click += delegate
            {
                result = ColorFromHsv(hh, ss, vv);
                dlg.Close();
            };
            Grid.SetColumn(cancelBtn, 1);
            Grid.SetColumn(okBtn, 3);
            footer.Children.Add(cancelBtn);
            footer.Children.Add(okBtn);

            Grid.SetRow(content, 1);
            outer.Children.Add(content);
            Grid.SetRow(footer, 2);
            outer.Children.Add(footer);

            Action syncVisualsOnly = delegate
            {
                Color cur = ColorFromHsv(hh, ss, vv);
                svBase.Fill = new SolidColorBrush(ColorFromHsv(hh, 1.0, 1.0));
                previewBorder.Background = new SolidColorBrush(cur);
                double sw = svSquare.ActualWidth <= 0 ? 322 : svSquare.ActualWidth;
                double sh = svSquare.ActualHeight <= 0 ? 168 : svSquare.ActualHeight;
                double hw = hueBar.ActualWidth <= 0 ? 322 : hueBar.ActualWidth;
                svThumb.Margin = new Thickness(Math.Max(-1, Math.Min(sw - 12, ss * sw - 6)), Math.Max(-1, Math.Min(sh - 12, (1 - vv) * sh - 6)), 0, 0);
                hueThumb.Margin = new Thickness(Math.Max(-1, Math.Min(hw - 8, hh / 360.0 * hw - 4)), 0, 0, 0);
                if (onLive != null) { onLive(cur); }
            };

            Action syncAll = delegate
            {
                syncVisualsOnly();
                if (!suppressTextSync)
                {
                    Color cur = ColorFromHsv(hh, ss, vv);
                    hexBox.Text = "#" + cur.R.ToString("X2") + cur.G.ToString("X2") + cur.B.ToString("X2");
                    tbR.Text = cur.R.ToString();
                    tbG.Text = cur.G.ToString();
                    tbB.Text = cur.B.ToString();
                }
            };

            Action commitHex = delegate
            {
                string t = hexBox.Text.Trim().TrimStart('#');
                byte pr = 0, pg = 0, pb = 0;
                bool okHex = t.Length == 6
                    && byte.TryParse(t.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pr)
                    && byte.TryParse(t.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pg)
                    && byte.TryParse(t.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pb);
                if (okHex)
                {
                    suppressTextSync = true;
                    ColorToHsv(Color.FromRgb(pr, pg, pb), out hh, out ss, out vv);
                    suppressTextSync = false;
                }
                syncAll();
            };

            Action commitRgb = delegate
            {
                byte pr, pg, pb;
                if (byte.TryParse(tbR.Text.Trim(), out pr)
                    && byte.TryParse(tbG.Text.Trim(), out pg)
                    && byte.TryParse(tbB.Text.Trim(), out pb))
                {
                    suppressTextSync = true;
                    ColorToHsv(Color.FromRgb(pr, pg, pb), out hh, out ss, out vv);
                    suppressTextSync = false;
                }
                syncAll();
            };

            hexBox.KeyDown += delegate(object s2, KeyEventArgs e2) { if (e2.Key == Key.Enter) { commitHex(); e2.Handled = true; } };
            hexBox.LostFocus += delegate { commitHex(); };
            tbR.KeyDown += delegate(object s2, KeyEventArgs e2) { if (e2.Key == Key.Enter) { commitRgb(); e2.Handled = true; } };
            tbG.KeyDown += delegate(object s2, KeyEventArgs e2) { if (e2.Key == Key.Enter) { commitRgb(); e2.Handled = true; } };
            tbB.KeyDown += delegate(object s2, KeyEventArgs e2) { if (e2.Key == Key.Enter) { commitRgb(); e2.Handled = true; } };
            tbR.LostFocus += delegate { commitRgb(); };
            tbG.LostFocus += delegate { commitRgb(); };
            tbB.LostFocus += delegate { commitRgb(); };

            MouseEventHandler svDrag = delegate(object s2, MouseEventArgs e2)
            {
                Point p = e2.GetPosition(svSquare);
                ss = Math.Max(0, Math.Min(1, p.X / Math.Max(1, svSquare.ActualWidth)));
                vv = 1 - Math.Max(0, Math.Min(1, p.Y / Math.Max(1, svSquare.ActualHeight)));
                syncAll();
            };
            svSquare.MouseLeftButtonDown += delegate(object s2, MouseButtonEventArgs e2)
            {
                svSquare.CaptureMouse();
                svDrag(s2, e2);
                e2.Handled = true;
            };
            svSquare.MouseMove += delegate(object s2, MouseEventArgs e2)
            {
                if (svSquare.IsMouseCaptured) { svDrag(s2, e2); }
            };
            svSquare.MouseLeftButtonUp += delegate(object s2, MouseButtonEventArgs e2)
            {
                if (svSquare.IsMouseCaptured) { svSquare.ReleaseMouseCapture(); }
            };

            MouseEventHandler hueDrag = delegate(object s2, MouseEventArgs e2)
            {
                Point p = e2.GetPosition(hueBar);
                hh = Math.Max(0, Math.Min(359.999, p.X / Math.Max(1, hueBar.ActualWidth) * 360));
                syncAll();
            };
            hueBar.MouseLeftButtonDown += delegate(object s2, MouseButtonEventArgs e2)
            {
                hueBar.CaptureMouse();
                hueDrag(s2, e2);
                e2.Handled = true;
            };
            hueBar.MouseMove += delegate(object s2, MouseEventArgs e2)
            {
                if (hueBar.IsMouseCaptured) { hueDrag(s2, e2); }
            };
            hueBar.MouseLeftButtonUp += delegate(object s2, MouseButtonEventArgs e2)
            {
                if (hueBar.IsMouseCaptured) { hueBar.ReleaseMouseCapture(); }
            };

            dlg.Loaded += delegate { syncAll(); };
            dlg.KeyDown += delegate(object s2, KeyEventArgs e2)
            {
                if (e2.Key == Key.Escape) { dlg.Close(); e2.Handled = true; }
            };

            dlg.ShowDialog();
            return result;
        }

        private TextBox BuildDarkTextBox(double width)
        {
            TextBox box = new TextBox
            {
                Height = 26,
                FontSize = 11.5,
                Foreground = P.BText,
                CaretBrush = P.BAccent,
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 3, 7, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            if (!double.IsNaN(width)) { box.Width = width; }
            return box;
        }

        private static void ColorToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            h = 0;
            if (d > 0)
            {
                if (max == r) { h = 60 * (((g - b) / d) % 6); }
                else if (max == g) { h = 60 * ((b - r) / d + 2); }
                else { h = 60 * ((r - g) / d + 4); }
            }
            if (h < 0) { h += 360; }
            s = max <= 0 ? 0 : d / max;
            v = max;
        }

        private static Color ColorFromHsv(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        private class TagPair
        {
            public TextBlock Name;
            public TextBlock Check;
            public Border Box;
        }

        private void UpdateLangDropdown()
        {
            if (langDropdownText != null && selectedLangIndex >= 0 && selectedLangIndex < langNames.Count)
            {
                langDropdownText.Text = langNames[selectedLangIndex];
            }
        }

        private void RefreshLangItems()
        {
            for (int i = 0; i < langListPanel.Children.Count; i++)
            {
                Border box = (Border)langListPanel.Children[i];
                TagPair pair = (TagPair)box.Tag;
                bool selNow = i == selectedLangIndex;
                pair.Check.Visibility = selNow ? Visibility.Visible : Visibility.Collapsed;
                pair.Box.Background = selNow
                    ? P.BRadioSel
                    : Brushes.Transparent;
            }
        }

        private void RefreshSkySwatches()
        {
            for (int i = 0; i < skySwatches.Length; i++)
            {
                skySwatches[i].BorderBrush = i == selectedSkyIndex ? P.BAccent : P.BBorderSoft;
            }
            if (skyPreviewChip != null)
            {
                skyPreviewChip.Background = new SolidColorBrush(selectedSkyColor);
            }
        }

        private void CloseLangPopupIfOutside(object sender, MouseButtonEventArgs e)
        {
            if (langPopup == null || !langPopup.IsOpen) { return; }
            DependencyObject d = e.OriginalSource as DependencyObject;
            while (d != null)
            {
                if (d == langDropdown || d == langPopup.Child) { return; }
                d = VisualTreeHelper.GetParent(d);
            }
            langPopup.IsOpen = false;
        }

        private static System.Drawing.Color ToDrawing(Color c)
        {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private void SetLiveSky(System.Drawing.Color dc)
        {
            Globals.SkyColor = dc;
            if (Globals.BackupConfigs != null) { Globals.BackupConfigs.SkyColor = dc; }
            RepaintOwnerViewport();
        }

        private void RepaintOwnerViewport()
        {
            foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
            {
                MainForm mf = f as MainForm;
                if (mf != null)
                {
                    mf.InvalidateViewport();
                    break;
                }
            }
        }

        private void RepaintOwnerTexts()
        {
            foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
            {
                MainForm mf = f as MainForm;
                if (mf != null)
                {
                    mf.ApplyTranslationLive();
                    break;
                }
            }
        }

        private string T(eLang id, string fallback)
        {
            string s = Lang.GetText(id);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        private readonly List<Action> wizardTextRefreshers = new List<Action>();

        private void RegText(TextBlock tb, eLang id, string fallback)
        {
            tb.Text = T(id, fallback);
            wizardTextRefreshers.Add(delegate { tb.Text = T(id, fallback); });
        }

        private void RegButton(Button b, eLang id, string fallback)
        {
            b.Content = T(id, fallback);
            wizardTextRefreshers.Add(delegate { b.Content = T(id, fallback); });
        }

        private void RunWizardTextRefresh()
        {
            foreach (Action a in wizardTextRefreshers) { a(); }
        }

        private void ApplyLiveLanguage()
        {
            JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg == null || langs == null || langs.Count == 0) { return; }
            if (selectedLangIndex <= 0)
            {
                cfg.LoadLangTranslation = false;
                cfg.LangJsonFile = "";
                Lang.RestoreEnglishDefaults();
                Lang.LoadedTranslation = false;
            }
            else
            {
                cfg.LoadLangTranslation = true;
                cfg.LangJsonFile = langs[selectedLangIndex - 1].LangJsonFileName;
                Utils.StartLoadLangFile();
            }
            RunWizardTextRefresh();
            UpdateNavButtonText();
            RepaintOwnerTexts();
        }

        private Grid BuildPageFolders()
        {
            Grid page = new Grid();
            StackPanel sp = PageShell(page, eLang.Wizard_FoldersTitle, "Game folders.", 0, "");

            Grid detectRow = new Grid { Margin = new Thickness(0, 10, 0, 2) };
            detectRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            detectRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button detect = MakeButton("Auto-detect (Steam)", true, double.NaN, 26, 1);
            detect.FontSize = 11;
            detect.VerticalAlignment = VerticalAlignment.Top;
            RegButton(detect, eLang.Wizard_AutoDetect, "Auto-detect (Steam)");
            detect.Click += delegate { AutoDetectSteam(); };
            Grid.SetColumn(detect, 0);
            detectRow.Children.Add(detect);

            lblDetect = new TextBlock
            {
                Foreground = P.BAccent,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(lblDetect, 1);
            detectRow.Children.Add(lblDetect);

            sp.Children.Add(detectRow);

            Style tbStyle = TextBoxStyle();

            sp.Children.Add(FieldRow("XFILE  (classic)", ref txtXFILE, tbStyle, false, "", 3));
            sp.Children.Add(FieldRow("RE4 2007", ref txt2007, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("RE4 PS2", ref txtPS2, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("RE4 UHD", ref txtUHD, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("RE4 PS4/NS", ref txtPS4NS, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("Custom 1", ref txtCustom1, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("Custom 2", ref txtCustom2, tbStyle, false, "", 4));
            sp.Children.Add(FieldRow("Custom 3", ref txtCustom3, tbStyle, false, "", 4));

            return page;
        }

        private Grid FieldRow(string label, ref TextBox box, Style tbStyle, bool selectFile, string filterHint, double topGap)
        {
            Grid row = new Grid { Margin = new Thickness(0, topGap, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lbl = new TextBlock
            {
                Text = label,
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            box = new TextBox { Style = tbStyle };
            Grid.SetColumn(box, 1);
            row.Children.Add(box);

            TextBox target = box;

            Button browse = MakeButton("\u2026", false, 38, 26, 1);
            browse.FontSize = 12;
            browse.FontWeight = FontWeights.Normal;
            browse.Margin = new Thickness(6, 0, 0, 0);
            browse.Click += delegate
            {
                if (selectFile)
                {
                    Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select " + label,
                        Filter = filterHint + "|*.exe|All Files (*.*)|*.*"
                    };
                    if (!string.IsNullOrEmpty(target.Text) && File.Exists(target.Text))
                    {
                        dialog.InitialDirectory = IoPath.GetDirectoryName(target.Text);
                        dialog.FileName = IoPath.GetFileName(target.Text);
                    }
                    if (dialog.ShowDialog(this) == true)
                    {
                        target.Text = dialog.FileName;
                    }
                }
                else
                {
                    using (WinForms.FolderBrowserDialog dialog = new WinForms.FolderBrowserDialog())
                    {
                        dialog.Description = "Select " + label;
                        dialog.SelectedPath = Directory.Exists(target.Text) ? target.Text : "";
                        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                        {
                            target.Text = dialog.SelectedPath;
                        }
                    }
                }
            };
            Grid.SetColumn(browse, 2);
            row.Children.Add(browse);

            return row;
        }

        private Grid BuildPageTools()
        {
            Grid page = new Grid();
            StackPanel sp = PageShell(page, eLang.Wizard_ToolsTitle, "External tools.", eLang.Wizard_ToolsSub, "Leave empty to use the bundled tools.");

            Style tbStyle = TextBoxStyle();

            sp.Children.Add(FieldRow("UDAS tool", ref toolUdas, tbStyle, true, "JADERLINK_DATUDAS_EXTRACT / REPACK", 12));
            sp.Children.Add(FieldRow("re4lfs", ref toolLfs, tbStyle, true, "re4lfs", 6));
            sp.Children.Add(FieldRow("UHD pack tool", ref toolPack, tbStyle, true, "RE4_UHD_PACK_TOOL", 6));
            sp.Children.Add(FieldRow("GCA tool", ref toolGca, tbStyle, true, "RE4_2007_GCA_TOOL", 6));

            Rectangle sep = new Rectangle
            {
                Height = 1,
                Fill = P.BBorderSoft,
                Margin = new Thickness(0, 14, 0, 0)
            };
            sp.Children.Add(sep);

            bool forceInitial = false;
            FrameworkElement forceSwitch = MakeSwitch("Force reload models JSON files after finishing", forceInitial, out getForceReload, wizardTextRefreshers, eLang.Wizard_ForceReloadSwitch, "Force reload models JSON files after finishing");
            forceSwitch.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(forceSwitch);

            TextBlock forceHint = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 10.5,
                Margin = new Thickness(50, 3, 0, 0)
            };
            RegText(forceHint, eLang.Wizard_ForceReloadHint, "Reloads every model and JSON database.");
            sp.Children.Add(forceHint);

            return page;
        }

        // ================================================================
        // navigation
        // ================================================================

        private void FillFields()
        {
            txtXFILE.Text = Globals.DirectoryXFILE;
            txt2007.Text = Globals.Directory2007RE4;
            txtPS2.Text = Globals.DirectoryPS2RE4;
            txtUHD.Text = Globals.DirectoryUHDRE4;
            txtPS4NS.Text = Globals.DirectoryPS4NSRE4;
            txtCustom1.Text = Globals.DirectoryCustom1;
            txtCustom2.Text = Globals.DirectoryCustom2;
            txtCustom3.Text = Globals.DirectoryCustom3;

            toolUdas.Text = Globals.ToolPathUDAS;
            toolLfs.Text = Globals.ToolPathLFS;
            toolPack.Text = Globals.ToolPathPACK;
            toolGca.Text = Globals.ToolPathGCA;

            Color[] presets = SkyPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] == selectedSkyColor)
                {
                    selectedSkyIndex = i;
                    break;
                }
            }
            RefreshSkySwatches();
        }

        private void ShowStepImmediate(int step)
        {
            currentStep = step;
            for (int i = 0; i < PageCount; i++)
            {
                pages[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
            }
            RefreshChrome();
        }

        private void Navigate(int target)
        {
            if (target < 0 || target >= PageCount || target == currentStep || animating || pages == null) { return; }
            if (langPopup != null) { langPopup.IsOpen = false; }

            int oldIdx = currentStep;
            currentStep = target;
            RefreshChrome();

            Grid from = pages[oldIdx];
            Grid to = pages[target];
            bool forward = target > oldIdx;

            double w = double.IsNaN(pageHost.ActualWidth) || pageHost.ActualWidth < 50 ? 500 : pageHost.ActualWidth;

            TranslateTransform ttFrom = new TranslateTransform(0, 0);
            TranslateTransform ttTo = new TranslateTransform(forward ? w : -w, 0);
            from.RenderTransform = ttFrom;
            to.RenderTransform = ttTo;
            to.Visibility = Visibility.Visible;

            animating = true;

            Duration dur = new Duration(TimeSpan.FromMilliseconds(280));
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation animFrom = new DoubleAnimation(0, forward ? -w : w, dur) { EasingFunction = ease };
            DoubleAnimation animTo = new DoubleAnimation(forward ? w : -w, 0, dur) { EasingFunction = ease };
            animTo.Completed += delegate
            {
                from.Visibility = Visibility.Collapsed;
                from.RenderTransform = null;
                to.RenderTransform = null;
                to.BeginAnimation(UIElement.OpacityProperty, null);
                to.Opacity = 1;
                animating = false;
            };

            ttFrom.BeginAnimation(TranslateTransform.XProperty, animFrom);
            ttTo.BeginAnimation(TranslateTransform.XProperty, animTo);
            to.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, 1, new Duration(TimeSpan.FromMilliseconds(220))));
        }

        private void UpdateNavButtonText()
        {
            if (buttonNext != null)
            {
                buttonNext.Content = currentStep == PageCount - 1 ? T(eLang.Wizard_Finish, "Finish  \u2713") : T(eLang.Wizard_Next, "Next  >");
            }
        }

        private void Advance()
        {
            if (currentStep < PageCount - 1) { Navigate(currentStep + 1); }
            else { ApplyAndClose(); }
        }

        private void RefreshChrome()
        {
            buttonBack.IsEnabled = currentStep > 0;
            buttonBack.Opacity = currentStep > 0 ? 1 : 0.4;
            buttonNext.Content = currentStep == PageCount - 1 ? T(eLang.Wizard_Finish, "Finish  \u2713") : T(eLang.Wizard_Next, "Next  >");
            buttonSkip.Visibility = currentStep < PageCount - 1 ? Visibility.Visible : Visibility.Collapsed;

            for (int i = 0; i < PageCount; i++)
            {
                bool active = i == currentStep;
                bool done = i < currentStep;

                chipLines[i].Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                chipDots[i].Fill = active || done ? P.BAccent : P.BDotIdle;
                chipNums[i].Text = done ? "\u2713" : (i + 1).ToString();
                chipLabels[i].Foreground = active ? P.BText : P.BSub;
            }
        }

        // ================================================================
        // live theme switch (opacity dip + rebuild, mirrors OptionsForm)
        // ================================================================

        private void RethemeWindow()
        {
            if (retheming) return;
            retheming = true;
            try
            {
                RebuildForTheme();
            }
            catch { }
            retheming = false;
        }

        private void StartSafetyTimer(double milliseconds, Action what)
        {
            System.Windows.Threading.DispatcherTimer t =
                new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            t.Tick += delegate
            {
                t.Stop();
                try { what(); } catch { }
            };
            t.Start();
        }

        /// <summary>Synchronous rebuild with the active palette. Keeps typed
        /// edits, the current page and the picked sky/language. Never throws.</summary>
        private void RebuildForTheme()
        {
            try
            {
                string sxfile = txtXFILE?.Text;
                string s2007 = txt2007?.Text;
                string sps2 = txtPS2?.Text;
                string suhd = txtUHD?.Text;
                string sps4 = txtPS4NS?.Text;
                string sc1 = txtCustom1?.Text;
                string sc2 = txtCustom2?.Text;
                string sc3 = txtCustom3?.Text;
                string sudas = toolUdas?.Text;
                string slfs = toolLfs?.Text;
                string spack = toolPack?.Text;
                string sgca = toolGca?.Text;
                int savedLang = selectedLangIndex;
                Color savedSky = selectedSkyColor;
                int savedSkyIdx = selectedSkyIndex;
                int savedStep = currentStep;
                string savedDetect = lblDetect?.Text;

                P.UpdateColors();
                Background = P.BWindow;

                wizardTextRefreshers.Clear();
                Content = null;
                pages = null;
                chipBorders = null; chipNums = null; chipLabels = null; chipLines = null; chipDots = null;
                skySwatches = null;
                langDropdown = null; langDropdownText = null; langPopup = null; langListPanel = null;

                BuildUi();

                if (txtXFILE != null) txtXFILE.Text = sxfile ?? Globals.DirectoryXFILE;
                if (txt2007 != null) txt2007.Text = s2007 ?? Globals.Directory2007RE4;
                if (txtPS2 != null) txtPS2.Text = sps2 ?? Globals.DirectoryPS2RE4;
                if (txtUHD != null) txtUHD.Text = suhd ?? Globals.DirectoryUHDRE4;
                if (txtPS4NS != null) txtPS4NS.Text = sps4 ?? Globals.DirectoryPS4NSRE4;
                if (txtCustom1 != null) txtCustom1.Text = sc1 ?? Globals.DirectoryCustom1;
                if (txtCustom2 != null) txtCustom2.Text = sc2 ?? Globals.DirectoryCustom2;
                if (txtCustom3 != null) txtCustom3.Text = sc3 ?? Globals.DirectoryCustom3;
                if (toolUdas != null) toolUdas.Text = sudas ?? Globals.ToolPathUDAS;
                if (toolLfs != null) toolLfs.Text = slfs ?? Globals.ToolPathLFS;
                if (toolPack != null) toolPack.Text = spack ?? Globals.ToolPathPACK;
                if (toolGca != null) toolGca.Text = sgca ?? Globals.ToolPathGCA;
                if (lblDetect != null) lblDetect.Text = savedDetect ?? "";

                selectedLangIndex = savedLang;
                selectedSkyColor = savedSky;
                selectedSkyIndex = savedSkyIdx;

                UpdateLangDropdown();
                RefreshLangItems();
                RefreshSkySwatches();
                ShowStepImmediate(savedStep);
                RunWizardTextRefresh();
                UpdateNavButtonText();
            }
            catch { }
        }

        // ================================================================
        // apply / skip
        // ================================================================

        private void ApplyAndClose()
        {
            finishedLive = true;
            JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg == null)
            {
                cfg = JSON.Configs.GetDefaultConfigs();
                Globals.BackupConfigs = cfg;
            }

            if (selectedLangIndex <= 0)
            {
                cfg.LoadLangTranslation = false;
                cfg.LangJsonFile = "";
            }
            else
            {
                cfg.LoadLangTranslation = true;
                cfg.LangJsonFile = langs[selectedLangIndex - 1].LangJsonFileName;
            }

            cfg.SkyColor = System.Drawing.Color.FromArgb(
                selectedSkyColor.A, selectedSkyColor.R, selectedSkyColor.G, selectedSkyColor.B);
            Globals.SkyColor = cfg.SkyColor;
            bool wizardDark = getThemeDark();
            cfg.UseDarkerGrayTheme = wizardDark;
            cfg.UseLightTheme = !wizardDark;
            cfg.UseInvertedMouseButtons = getInvertMouse();

            Globals.DirectoryXFILE = cfg.DirectoryXFILE = FixDirectory(txtXFILE.Text);
            Globals.Directory2007RE4 = cfg.Directory2007RE4 = FixDirectory(txt2007.Text);
            Globals.DirectoryPS2RE4 = cfg.DirectoryPS2RE4 = FixDirectory(txtPS2.Text);
            Globals.DirectoryUHDRE4 = cfg.DirectoryUHDRE4 = FixDirectory(txtUHD.Text);
            Globals.DirectoryPS4NSRE4 = cfg.DirectoryPS4NSRE4 = FixDirectory(txtPS4NS.Text);
            Globals.DirectoryCustom1 = cfg.DirectoryCustom1 = FixDirectory(txtCustom1.Text);
            Globals.DirectoryCustom2 = cfg.DirectoryCustom2 = FixDirectory(txtCustom2.Text);
            Globals.DirectoryCustom3 = cfg.DirectoryCustom3 = FixDirectory(txtCustom3.Text);

            Globals.ToolPathUDAS = cfg.ToolPathUDAS = toolUdas.Text;
            Globals.ToolPathLFS = cfg.ToolPathLFS = toolLfs.Text;
            Globals.ToolPathPACK = cfg.ToolPathPACK = toolPack.Text;
            Globals.ToolPathGCA = cfg.ToolPathGCA = toolGca.Text;

            Utils.StartReloadDirectoryDic();

            cfg.SetupDone = true;
            try { JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, cfg); } catch (Exception) { }

            ApplyLiveLanguage();

            if (getForceReload())
            {
                System.Windows.MessageBox.Show(
                    this,
                    Lang.GetText(eLang.OptionsFormWarningLoadModelsMessageBoxDialog),
                    Lang.GetText(eLang.OptionsFormWarningLoadModelsMessageBoxTitle));
                Utils.ReloadJsonFiles();
                Utils.ReloadModels();
            }

            DialogResult = true;
            Close();
        }

        private void SkipWizard()
        {
            JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg != null)
            {
                cfg.SkyColor = skyOriginalLive;
                cfg.LoadLangTranslation = langOriginalLoaded;
                cfg.LangJsonFile = langOriginalFile;
                bool skippedWizardDark = getThemeDark();
                cfg.UseDarkerGrayTheme = skippedWizardDark;
                cfg.UseLightTheme = !skippedWizardDark;
                cfg.SetupDone = true;
                try { JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, cfg); } catch (Exception) { }
            }
            Globals.SkyColor = skyOriginalLive;
            if (langOriginalLoaded)
            {
                Utils.StartLoadLangFile();
            }
            else
            {
                Lang.RestoreEnglishDefaults();
                Lang.LoadedTranslation = false;
            }
            RepaintOwnerTexts();
            Close();
        }

        private static string FixDirectory(string dir)
        {
            return dir != null && dir.Length > 0 ? (dir + (dir.Last() != '\\' ? "\\" : "")) : "";
        }

        // ================================================================
        // steam detection
        // ================================================================

        private void AutoDetectSteam()
        {
            List<string> roots = new List<string>();

            string steam = GetSteamPath();
            if (!string.IsNullOrEmpty(steam))
            {
                roots.Add(IoPath.Combine(steam, "steamapps", "common"));
                string vdf = IoPath.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    try
                    {
                        foreach (string line in File.ReadAllLines(vdf))
                        {
                            Match m = Regex.Match(line, "\"path\"\\s+\"(.+?)\"");
                            if (m.Success)
                            {
                                roots.Add(IoPath.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common"));
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            roots.Add(@"C:\Program Files (x86)\Steam\steamapps\common");

            string detected = null;
            foreach (string root in roots)
            {
                string gameRoot = IoPath.Combine(root, "Resident Evil 4");
                if (Directory.Exists(gameRoot))
                {
                    detected = gameRoot;
                    break;
                }
            }

            if (detected != null)
            {
                txtUHD.Text = detected;
                lblDetect.Foreground = P.BAccent;
                lblDetect.Text = T(eLang.Wizard_DetectFound, "Found: ") + detected;
            }
            else
            {
                lblDetect.Foreground = P.BSub;
                lblDetect.Text = T(eLang.Wizard_DetectNotFound, "\"Resident Evil 4\" was not found \u2014 please browse for it.");
            }
        }

        private static string GetSteamPath()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string v = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(v)) { return v; }
                    }
                }
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string v = key.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(v)) { return v; }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        private JSON.LangObjForList[] GetLangList()
        {
            List<JSON.LangObjForList> list = new List<JSON.LangObjForList>();
            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.LangDirectory);
            string[] files = new string[0];
            if (Directory.Exists(directory))
            {
                files = Directory.GetFiles(directory, "*.json");
            }
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    JSON.LangObjForList file = JSON.LangFile.ParseFromFileForList(files[i]);
                    if (file != null && !list.Contains(file))
                    {
                        list.Add(file);
                    }
                }
                catch (Exception) { }
            }
            return list.ToArray();
        }
    }
}
