using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using WinForms = System.Windows.Forms;
using IoPath = System.IO.Path;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src.Forms
{
    public class OptionsForm : System.Windows.Window
    {
        private const int PageCount = 5;

        private int currentStep;
        private bool animating;

        private Grid pageHost;
        private Grid[] pages;
        private bool[] pageBuilt;
        private Func<Grid>[] pageBuilders;

        private Border[] chipBorders;
        private TextBlock[] chipNums;
        private TextBlock[] chipLabels;
        private Rectangle[] chipLines;
        private Border[] chipDots;

        private Button buttonOK;
        private Button buttonCancel;

        public event Class.CustomDelegates.ActivateMethod OnOKButtonClick;

        private readonly List<Action> textRefreshers = new List<Action>();
        private readonly List<DarkCombo> combos = new List<DarkCombo>();

        private TextBox txtXFILE, txt2007, txtPS2, txtUHD, txtPS4NS, txtCustom1, txtCustom2, txtCustom3;
        private TextBox toolUdas, toolLfs, toolPack, toolGca;

        private JSON.ObjectInfoList[] enemiesLists, etcModelsLists, itemsLists;
        private JSON.QuadCustomInfoList[] quadCustomLists;
        private DarkCombo comboEnemies, comboEtcModels, comboItems, comboQuadCustom;

        private readonly List<JSON.LangObjForList> langs = new List<JSON.LangObjForList>();
        private int selectedLangIndex;
        private DarkCombo comboLang;
        private bool langOriginalLoaded;
        private string langOriginalFile = "";

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
        private Border[] skySwatches;
        private int selectedSkyIndex;
        private Border skyPreviewChip;
        private Func<bool> getThemeDark;
        private bool themeOriginalDark;
        private Func<bool> getInvertMouse;
        private Func<bool> getForceReload;

        private int fracInSymbol;
        private int fracOutSymbol;
        private StackPanel outSymbolGroup;
        private int frationalAmount = 9;
        private TextBlock amountValue;
        private Func<bool> getRotDisable;
        private Func<bool> getRotIgnoreZeroXYZ;
        private Func<bool> getRotIgnoreZNotGTZero;
        private double dividerValue = 1;
        private double multiplierValue = 1;
        private TextBox dividerBox, multiplierBox;
        private DarkCombo comboRotOrder;

        public OptionsForm()
        {
            Width = 640;
            Height = 520;
            MinWidth = 560;
            MinHeight = 430;
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

            PreviewMouseLeftButtonDown += ClosePopupsIfOutside;
            Loaded += delegate
            {
                double maxW = SystemParameters.WorkArea.Width - 20;
                double maxH = SystemParameters.WorkArea.Height - 40;

                foreach (WinForms.Form f in WinForms.Application.OpenForms)
                {
                    MainForm mf = f as MainForm;
                    if (mf != null && mf.WindowState == WinForms.FormWindowState.Normal)
                    {
                        double scale = 1.0;
                        System.Windows.Interop.HwndSource src = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                        if (src != null && src.CompositionTarget != null)
                        {
                            scale = src.CompositionTarget.TransformToDevice.M11;
                            if (scale <= 0) { scale = 1.0; }
                        }
                        maxW = Math.Min(maxW, (mf.Width / scale) - 30);
                        maxH = Math.Min(maxH, (mf.Height / scale) - 30);
                        break;
                    }
                }

                if (ActualHeight > maxH) { Height = Math.Max(MinHeight, maxH); }
                if (ActualWidth > maxW) { Width = Math.Max(MinWidth, maxW); }
            };
            Deactivated += delegate
            {
                foreach (DarkCombo c in combos) { if (c.Pop != null) { c.Pop.IsOpen = false; } }
            };
            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { DoCancel(); e.Handled = true; }
                else if (e.Key == Key.Enter && !animating) { ApplyAndClose(); e.Handled = true; }
            };
        }

        // Per-window unfrozen palette snapshot (see UiTheme.Palette).
        // Re-created on live theme switches so the open window recolors in place.
        private UiTheme.Palette P = UiTheme.CreatePalette();
        private bool retheming;

        /// <summary>
        /// LIVE theme switch orchestrator: dips this window's opacity, rebuilds
        /// the content with a fresh palette at the bottom of the dip, then eases
        /// back in — a smooth, crash-proof transition. The open editor window is
        /// re-themed on its own UI thread.
        /// </summary>
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

        /// <summary>Live theme recolor: rebuilds the window with the fresh
        /// palette so frozen Style brushes, ScrollBar templates and hover
        /// triggers all pick up the new colours. Keeps typed edits.</summary>
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
                string sdiv = dividerBox?.Text;
                string smul = multiplierBox?.Text;
                int selEnemies = comboEnemies?.SelectedIndex ?? -1;
                int selEtc = comboEtcModels?.SelectedIndex ?? -1;
                int selItems = comboItems?.SelectedIndex ?? -1;
                int selQuad = comboQuadCustom?.SelectedIndex ?? -1;
                int selLang = comboLang?.SelectedIndex ?? selectedLangIndex;
                int selRot = comboRotOrder?.SelectedIndex ?? -1;
                int savedAmount = frationalAmount;
                Color savedSky = selectedSkyColor;
                int savedSkyIdx = selectedSkyIndex;
                int savedStep = currentStep;

                P.UpdateColors();
                Background = P.BWindow;

                textRefreshers.Clear();
                combos.Clear();
                radioRows.Clear();
                if (pageHost != null) pageHost.Children.Clear();
                pages = new Grid[PageCount];
                pageBuilt = new bool[PageCount];
                chipBorders = null; chipNums = null; chipLabels = null; chipLines = null; chipDots = null;
                skySwatches = null; skyPreviewChip = null;

                Content = null;
                BuildUi();

                if (txtXFILE != null && sxfile != null) txtXFILE.Text = sxfile;
                if (txt2007 != null && s2007 != null) txt2007.Text = s2007;
                if (txtPS2 != null && sps2 != null) txtPS2.Text = sps2;
                if (txtUHD != null && suhd != null) txtUHD.Text = suhd;
                if (txtPS4NS != null && sps4 != null) txtPS4NS.Text = sps4;
                if (txtCustom1 != null && sc1 != null) txtCustom1.Text = sc1;
                if (txtCustom2 != null && sc2 != null) txtCustom2.Text = sc2;
                if (txtCustom3 != null && sc3 != null) txtCustom3.Text = sc3;
                if (toolUdas != null && sudas != null) toolUdas.Text = sudas;
                if (toolLfs != null && slfs != null) toolLfs.Text = slfs;
                if (toolPack != null && spack != null) toolPack.Text = spack;
                if (toolGca != null && sgca != null) toolGca.Text = sgca;
                if (dividerBox != null && sdiv != null) dividerBox.Text = sdiv;
                if (multiplierBox != null && smul != null) multiplierBox.Text = smul;

                frationalAmount = savedAmount;
                if (amountValue != null) amountValue.Text = frationalAmount.ToString();
                selectedSkyColor = savedSky;
                selectedSkyIndex = savedSkyIdx;
                SafeSelect(comboEnemies, selEnemies);
                SafeSelect(comboEtcModels, selEtc);
                SafeSelect(comboItems, selItems);
                SafeSelect(comboQuadCustom, selQuad);
                SafeSelect(comboLang, selLang);
                SafeSelect(comboRotOrder, selRot);
                if (comboEnemies != null) comboEnemies.Refresh();
                if (comboEtcModels != null) comboEtcModels.Refresh();
                if (comboItems != null) comboItems.Refresh();
                if (comboQuadCustom != null) comboQuadCustom.Refresh();
                if (comboLang != null) comboLang.Refresh();
                if (comboRotOrder != null) comboRotOrder.Refresh();

                RefreshSkySwatches();
                ShowStepImmediate(savedStep);
            }
            catch { }
        }

        private static void SafeSelect(DarkCombo c, int index)
        {
            if (c != null && index >= 0)
            {
                int max = c.Items != null ? c.Items.Length - 1 : -1;
                c.SelectedIndex = Math.Min(index, max);
            }
        }

        private string T(eLang id, string fallback)
        {
            string s = Lang.GetText(id);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        private void RegText(TextBlock tb, eLang id, string fallback)
        {
            tb.Text = T(id, fallback);
            textRefreshers.Add(delegate { tb.Text = T(id, fallback); });
        }

        private void RegButton(Button b, eLang id, string fallback)
        {
            b.Content = T(id, fallback);
            textRefreshers.Add(delegate { b.Content = T(id, fallback); });
        }

        private void RunTextRefresh()
        {
            foreach (Action a in textRefreshers) { a(); }
        }

        // ================================================================
        // state
        // ================================================================

        private void LoadState()
        {
            JSON.Configs cfg = Globals.BackupConfigs;

            if (DataBase.CachedLangList != null) { langs.AddRange(DataBase.CachedLangList); }
            else { langs.AddRange(GetLangList()); }

            enemiesLists = DataBase.CachedEnemiesLists ?? GetEnemiesListJson();
            etcModelsLists = DataBase.CachedEtcModelsLists ?? GetEtcModelsListJson();
            itemsLists = DataBase.CachedItemsLists ?? GetItemsListJson();
            quadCustomLists = DataBase.CachedQuadCustomLists ?? GetQuadCustomListJson();

            if (cfg != null)
            {
                selectedSkyColor = cfg.SkyColor.IsEmpty
                    ? Colors.CornflowerBlue
                    : Color.FromArgb(cfg.SkyColor.A, cfg.SkyColor.R, cfg.SkyColor.G, cfg.SkyColor.B);

                skyOriginalLive = Globals.SkyColor;
                langOriginalLoaded = cfg.LoadLangTranslation;
                langOriginalFile = cfg.LangJsonFile ?? "";
                themeOriginalDark = cfg.UseDarkerGrayTheme;
                selectedLangIndex = 0;
                if (cfg.LoadLangTranslation && !string.IsNullOrEmpty(cfg.LangJsonFile))
                {
                    for (int i = 0; i < langs.Count; i++)
                    {
                        if (langs[i].LangJsonFileName == cfg.LangJsonFile) { selectedLangIndex = i + 1; break; }
                    }
                }

                switch (cfg.FrationalSymbol)
                {
                    case ConfigFrationalSymbol.AcceptsCommaAndPeriod_OutputComma:
                        fracInSymbol = 0; fracOutSymbol = 0;
                        break;
                    case ConfigFrationalSymbol.OnlyAcceptComma:
                        fracInSymbol = 1; fracOutSymbol = 0;
                        break;
                    case ConfigFrationalSymbol.OnlyAcceptPeriod:
                        fracInSymbol = 2; fracOutSymbol = 1;
                        break;
                    case ConfigFrationalSymbol.AcceptsCommaAndPeriod_OutputPeriod:
                    default:
                        fracInSymbol = 0; fracOutSymbol = 1;
                        break;
                }
                frationalAmount = cfg.FrationalAmount;
                dividerValue = cfg.ItemRotationCalculationDivider;
                multiplierValue = cfg.ItemRotationCalculationMultiplier;
            }
            else
            {
                frationalAmount = Globals.FrationalAmount;
                dividerValue = Globals.ItemRotationCalculationDivider;
                multiplierValue = Globals.ItemRotationCalculationMultiplier;
            }
            if (frationalAmount < 4) { frationalAmount = 4; }
            if (frationalAmount > 9) { frationalAmount = 9; }
        }

        private static int IndexOfFileName(object[] items, string fileName)
        {
            for (int i = 0; i < items.Length; i++)
            {
                JSON.ObjectInfoList l = items[i] as JSON.ObjectInfoList;
                if (l != null && l.JsonFileName == fileName) { return i; }
            }
            return -1;
        }

        private void FillFields()
        {
            if (txtXFILE != null) txtXFILE.Text = Globals.DirectoryXFILE;
            if (txt2007 != null) txt2007.Text = Globals.Directory2007RE4;
            if (txtPS2 != null) txtPS2.Text = Globals.DirectoryPS2RE4;
            if (txtUHD != null) txtUHD.Text = Globals.DirectoryUHDRE4;
            if (txtPS4NS != null) txtPS4NS.Text = Globals.DirectoryPS4NSRE4;
            if (txtCustom1 != null) txtCustom1.Text = Globals.DirectoryCustom1;
            if (txtCustom2 != null) txtCustom2.Text = Globals.DirectoryCustom2;
            if (txtCustom3 != null) txtCustom3.Text = Globals.DirectoryCustom3;

            if (toolUdas != null) toolUdas.Text = Globals.ToolPathUDAS;
            if (toolLfs != null) toolLfs.Text = Globals.ToolPathLFS;
            if (toolPack != null) toolPack.Text = Globals.ToolPathPACK;
            if (toolGca != null) toolGca.Text = Globals.ToolPathGCA;

            if (comboEnemies != null) comboEnemies.SelectedIndex = IndexOfFileName(enemiesLists, Globals.FileDiretoryEnemiesList);
            if (comboEtcModels != null) comboEtcModels.SelectedIndex = IndexOfFileName(etcModelsLists, Globals.FileDiretoryEtcModelsList);
            if (comboItems != null) comboItems.SelectedIndex = IndexOfFileName(itemsLists, Globals.FileDiretoryItemsList);

            if (comboQuadCustom != null)
            {
                for (int i = 0; i < quadCustomLists.Length; i++)
                {
                    if (quadCustomLists[i].JsonFileName == Globals.FileDiretoryQuadCustomList)
                    {
                        comboQuadCustom.SelectedIndex = i;
                        break;
                    }
                }
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
            RegText(t1, eLang.OptionsForm, "Options");
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
            close.Click += delegate { ApplyAndClose(); };
            Grid.SetColumn(close, 1);
            g.Children.Add(close);

            header.Child = g;
            Grid.SetRow(header, 0);
            root.Children.Add(header);
        }

        private static readonly string[] ChipFallbacks = { "Diretory", "Lists", "Other", "Tools", "Shortcuts" };
        private static readonly eLang[] ChipIds =
        {
            eLang.tabPageDiretory, eLang.tabPageLists, eLang.tabPageOthers, eLang.Wizard_ToolsTitle, eLang.tabPageShortcuts
        };

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
            chipDots = new Border[PageCount];

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

                Grid dotWrap = new Grid { Width = 22, Height = 16, Margin = new Thickness(0, 0, 8, 0) };
                Border badge = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = P.BDotIdle
                };
                dotWrap.Children.Add(badge);
                chipDots[i] = badge;

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
                RegText(label, ChipIds[i], ChipFallbacks[i]);
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
            pageBuilt = new bool[PageCount];
            pageBuilders = new Func<Grid>[PageCount];
            pageBuilders[0] = BuildPageDirectories;
            pageBuilders[1] = BuildPageLists;
            pageBuilders[2] = BuildPageOthers;
            pageBuilders[3] = BuildPageTools;
            pageBuilders[4] = BuildPageShortcuts;

            for (int i = 0; i < PageCount; i++)
            {
                EnsurePage(i);
                pages[i].Visibility = i == currentStep ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void EnsurePage(int index)
        {
            if (index < 0 || index >= PageCount) return;
            if (pageBuilt[index]) return;
            pages[index] = pageBuilders[index]();
            pageBuilt[index] = true;
            pages[index].Visibility = Visibility.Collapsed;
            if (pageHost != null && !pageHost.Children.Contains(pages[index]))
                pageHost.Children.Add(pages[index]);
        }

        private void BuildFooter(Grid root)
        {
            Border footer = new Border
            {
                Height = 40,
                Background = P.BBar,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            Grid g = new Grid { Margin = new Thickness(14, 0, 8, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            bool forceInitial = false;
            FrameworkElement forceSwitch = MakeSwitch(
                () => T(eLang.checkBoxForceReloadModels, "Force Reload Models And Json Files"),
                forceInitial, out getForceReload);
            forceSwitch.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(forceSwitch, 0);
            g.Children.Add(forceSwitch);

            buttonOK = MakeButton(T(eLang.Options_buttonOK, "OK"), true, 105, 24, 1);
            buttonOK.FontSize = 11.5;
            buttonOK.Padding = new Thickness(0);
            buttonOK.VerticalAlignment = VerticalAlignment.Center;
            RegButton(buttonOK, eLang.Options_buttonOK, "OK");
            buttonOK.Click += delegate { ApplyAndClose(); };
            Grid.SetColumn(buttonOK, 2);
            g.Children.Add(buttonOK);

            buttonCancel = MakeButton(T(eLang.Options_buttonCancel, "CANCEL"), false, 105, 24, 1);
            buttonCancel.FontSize = 11.5;
            buttonCancel.FontWeight = FontWeights.Normal;
            buttonCancel.Padding = new Thickness(0);
            buttonCancel.Margin = new Thickness(5, 0, 0, 0);
            buttonCancel.VerticalAlignment = VerticalAlignment.Center;
            RegButton(buttonCancel, eLang.Options_buttonCancel, "CANCEL");
            buttonCancel.Click += delegate { DoCancel(); };
            Grid.SetColumn(buttonCancel, 3);
            g.Children.Add(buttonCancel);

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

        private FrameworkElement MakeSwitch(Func<string> label, bool initial, out Func<bool> getter, Action<bool> onChanged = null)
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
                Text = label(),
                Foreground = P.BText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 2, 0, 0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 430,
                TextWrapping = TextWrapping.Wrap
            };
            textRefreshers.Add(delegate { lbl.Text = label(); });
            lbl.MouseLeftButtonDown += delegate { toggle(null, null); };

            root.Children.Add(trackWrap);
            root.Children.Add(lbl);

            getter = () => state;
            return root;
        }

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
            textRefreshers.Add(delegate { tb.Text = T(id, fallback).ToUpperInvariant(); });
            tb.Text = T(id, fallback).ToUpperInvariant();
            sp.Children.Add(tb);
        }

        // ================================================================
        // combo (dark dropdown)
        // ================================================================

        private sealed class ComboTag
        {
            public TextBlock Name;
            public TextBlock Check;
        }

        private sealed class DarkCombo
        {
            public UiTheme.Palette P;
            public Border Box;
            public TextBlock LabelText;
            public Popup Pop;
            public StackPanel ListPanel;
            public object[] Items = new object[0];
            public Func<object, string> Display;
            public int SelectedIndex;
            public Action SelectionChanged;

            public object SelectedItem
            {
                get
                {
                    if (SelectedIndex >= 0 && SelectedIndex < Items.Length) { return Items[SelectedIndex]; }
                    return null;
                }
            }

            public void Refresh()
            {
                if (LabelText != null)
                {
                    LabelText.Text = SelectedItem != null && Display != null ? Display(SelectedItem) : "";
                }
                if (ListPanel != null)
                {
                    for (int i = 0; i < ListPanel.Children.Count; i++)
                    {
                        Border box = (Border)ListPanel.Children[i];
                        ComboTag tag = (ComboTag)box.Tag;
                        bool selNow = i == SelectedIndex;
                        tag.Check.Visibility = selNow ? Visibility.Visible : Visibility.Collapsed;
                        box.Background = selNow
                            ? P.BRadioSel
                            : Brushes.Transparent;
                        if (Display != null && i < Items.Length) { tag.Name.Text = Display(Items[i]); }
                    }
                }
            }
        }

        private void PopulateCombo(DarkCombo c, object[] items, Func<object, string> display)
        {
            c.Items = items ?? new object[0];
            c.Display = display;
            int sel = Math.Max(0, Math.Min(c.SelectedIndex, Math.Max(0, c.Items.Length - 1)));
            c.SelectedIndex = sel;
            c.ListPanel.Children.Clear();
            for (int i = 0; i < c.Items.Length; i++)
            {
                int idx = i;
                StackPanel itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                TextBlock itemName = new TextBlock
                {
                    Text = display(c.Items[i]),
                    Foreground = P.BText,
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                itemContent.Children.Add(itemName);
                TextBlock itemCheck = new TextBlock
                {
                    Text = "\u2713",
                    Foreground = P.BAccent,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                itemContent.Children.Add(itemCheck);

                Border item = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(1),
                    Padding = new Thickness(9, 5, 9, 6),
                    Child = itemContent,
                    Cursor = Cursors.Hand
                };
                item.MouseEnter += delegate { if (idx != c.SelectedIndex) { item.Background = P.BHoverSurface; } };
                item.MouseLeave += delegate { if (idx != c.SelectedIndex) { item.Background = Brushes.Transparent; } };
                item.MouseLeftButtonUp += delegate
                {
                    c.SelectedIndex = idx;
                    c.Refresh();
                    if (c.Pop != null) { c.Pop.IsOpen = false; }
                    if (c.SelectionChanged != null) { c.SelectionChanged(); }
                };

                ComboTag tag = new ComboTag { Name = itemName, Check = itemCheck };
                item.Tag = tag;
                c.ListPanel.Children.Add(item);
            }
            c.Refresh();
        }

        private DarkCombo MakeCombo(double width)
        {
            DarkCombo c = new DarkCombo();
            c.P = P;

            c.Box = new Border
            {
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(1),
                Padding = new Thickness(10, 5, 9, 6),
                Cursor = Cursors.Hand
            };
            if (!double.IsNaN(width)) { c.Box.Width = width; c.Box.MinWidth = width; }
            Grid comboGrid = new Grid();
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            c.LabelText = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(c.LabelText, 0);
            comboGrid.Children.Add(c.LabelText);
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
            c.Box.Child = comboGrid;

            c.Box.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (c.Pop == null) { return; }
                foreach (DarkCombo other in combos)
                {
                    if (other != c && other.Pop != null) { other.Pop.IsOpen = false; }
                }
                if (c.Pop.IsOpen) { c.Pop.IsOpen = false; }
                else { c.Refresh(); c.Pop.IsOpen = true; }
            };

            c.ListPanel = new StackPanel();
            ScrollViewer scroll = new ScrollViewer
            {
                MaxHeight = 190,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = c.ListPanel
            };
            scroll.Resources[typeof(ScrollBar)] = DarkScrollBarStyle();
            Border dropDownBox = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3),
                Child = scroll
            };
            if (!double.IsNaN(width)) { dropDownBox.MinWidth = width; }
            c.Pop = new Popup
            {
                PlacementTarget = c.Box,
                Placement = PlacementMode.Bottom,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = dropDownBox
            };

            combos.Add(c);
            return c;
        }

        private void ClosePopupsIfOutside(object sender, MouseButtonEventArgs e)
        {
            foreach (DarkCombo c in combos)
            {
                if (c.Pop == null || !c.Pop.IsOpen) { continue; }
                DependencyObject d = e.OriginalSource as DependencyObject;
                bool inside = false;
                while (d != null)
                {
                    if (d == c.Box || d == c.Pop.Child) { inside = true; break; }
                    d = VisualTreeHelper.GetParent(d);
                }
                if (!inside) { c.Pop.IsOpen = false; }
            }
        }

        // ================================================================
        // field rows
        // ================================================================

        private Grid FieldRow(Func<string> label, ref TextBox box, Style tbStyle, bool selectFile, string filterHint, string browseSuffix, double topGap)
        {
            Grid row = new Grid { Margin = new Thickness(0, topGap, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lbl = new TextBlock
            {
                Text = label(),
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            textRefreshers.Add(delegate { lbl.Text = label(); });
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
                        Title = T(eLang.OptionsFormSelectDiretory, "Select folder directory to:") + " " + browseSuffix,
                        Filter = filterHint + "|*.exe|All Files (*.*)|*.*"
                    };
                    if (!string.IsNullOrEmpty(target.Text) && File.Exists(target.Text))
                    {
                        dialog.InitialDirectory = IoPath.GetDirectoryName(target.Text);
                        dialog.FileName = IoPath.GetFileName(target.Text);
                    }
                    dialog.ShowDialog(this);
                    if (dialog.FileName != null && File.Exists(dialog.FileName))
                    {
                        target.Text = dialog.FileName;
                    }
                }
                else
                {
                    using (WinForms.FolderBrowserDialog dialog = new WinForms.FolderBrowserDialog())
                    {
                        dialog.Description = T(eLang.OptionsFormSelectDiretory, "Select folder directory to:") + " " + browseSuffix;
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

        private FrameworkElement ComboRow(Func<string> label, DarkCombo combo, double topGap)
        {
            Grid row = new Grid { Margin = new Thickness(0, topGap, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lbl = new TextBlock
            {
                Text = label(),
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            textRefreshers.Add(delegate { lbl.Text = label(); });
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            combo.Box.HorizontalAlignment = HorizontalAlignment.Left;
            combo.Box.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(combo.Box, 1);
            row.Children.Add(combo.Box);

            return row;
        }

        // ================================================================
        // pages
        // ================================================================

        private static Grid Scrollable(Grid content)
        {
            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = content
            };
            sv.Resources[typeof(ScrollBar)] = DarkScrollBarStyle();
            Grid g = new Grid();
            g.Children.Add(sv);
            return g;
        }

        private static Style DarkScrollBarStyle()
        {
            return UiTheme.ScrollBarStyle();
        }

        private Grid BuildPageDirectories()
        {
            Grid pageInner = new Grid();
            StackPanel sp = PageShell(pageInner, eLang.groupBoxDirectory, "Directories", eLang.tabPageDiretory, "Diretory");

            Section(sp, eLang.groupBoxDirectory, "Directories");

            Style tbStyle = TextBoxStyle();

            sp.Children.Add(FieldRow(() => "XFILE " + T(eLang.labelOptionsDirectory, "Directory"), ref txtXFILE, tbStyle, false, "", "XFILE", 3));
            sp.Children.Add(FieldRow(() => "RE4 2007 " + T(eLang.labelOptionsDirectory, "Directory"), ref txt2007, tbStyle, false, "", "RE4 2007", 4));
            sp.Children.Add(FieldRow(() => "RE4 PS2 " + T(eLang.labelOptionsDirectory, "Directory"), ref txtPS2, tbStyle, false, "", "RE4 PS2", 4));
            sp.Children.Add(FieldRow(() => "RE4 UHD " + T(eLang.labelOptionsDirectory, "Directory"), ref txtUHD, tbStyle, false, "", "RE4 UHD", 4));
            sp.Children.Add(FieldRow(() => "RE4 PS4/NS " + T(eLang.labelOptionsDirectory, "Directory"), ref txtPS4NS, tbStyle, false, "", "RE4 PS4/NS", 4));
            sp.Children.Add(FieldRow(() => "Custom1 " + T(eLang.labelOptionsDirectory, "Directory"), ref txtCustom1, tbStyle, false, "", "Custom1", 4));
            sp.Children.Add(FieldRow(() => "Custom2 " + T(eLang.labelOptionsDirectory, "Directory"), ref txtCustom2, tbStyle, false, "", "Custom2", 4));
            sp.Children.Add(FieldRow(() => "Custom3 " + T(eLang.labelOptionsDirectory, "Directory"), ref txtCustom3, tbStyle, false, "", "Custom3", 4));

            return Scrollable(pageInner);
        }

        private Grid BuildPageTools()
        {
            Grid pageInner = new Grid();
            StackPanel sp = PageShell(pageInner, eLang.Wizard_ToolsTitle, "External Tools", 0, "");

            Section(sp, eLang.Wizard_ToolsTitle, "External tools.");
            TextBlock toolsHint = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 0, 2)
            };
            RegText(toolsHint, eLang.Wizard_ToolsSub, "Leave empty to use the bundled tools.");

            Style tbStyle = TextBoxStyle();
            sp.Children.Add(toolsHint);

            sp.Children.Add(FieldRow(() => "UDAS", ref toolUdas, tbStyle, true, "JADERLINK_DATUDAS_EXTRACT / REPACK", "UDAS tool", 6));
            sp.Children.Add(FieldRow(() => "re4lfs", ref toolLfs, tbStyle, true, "re4lfs", "re4lfs tool", 4));
            sp.Children.Add(FieldRow(() => "PACK", ref toolPack, tbStyle, true, "RE4_UHD_PACK_TOOL", "UHD pack tool", 4));
            sp.Children.Add(FieldRow(() => "GCA", ref toolGca, tbStyle, true, "RE4_2007_GCA_TOOL", "GCA tool", 4));

            return Scrollable(pageInner);
        }

        private Grid BuildPageShortcuts()
        {
            Grid pageInner = new Grid();
            StackPanel sp = PageShell(pageInner, eLang.tabPageShortcuts, "Shortcuts", eLang.shortcutsNavigation, "Keyboard shortcuts reference.");

            Action<string, string> Row = (key, desc) =>
            {
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                TextBlock keyTb = new TextBlock
                {
                    Text = key,
                    Foreground = P.BAccent,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    MinWidth = 150
                };
                TextBlock descTb = new TextBlock
                {
                    Text = desc,
                    Foreground = P.BText,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(keyTb);
                row.Children.Add(descTb);
                sp.Children.Add(row);
            };

            // Navigation
            Section(sp, eLang.shortcutsNavigation, "NAVIGATION");
            Row("Alt + W/A/S/D", "Camera move (precision)");
            Row("Alt + Space / C", "Camera up / down");
            Row("W/A/S/D (viewport)", "Camera move");
            Row("Space / C (viewport)", "Camera up / down");
            Row("E", "Enter first-person camera view");
            Row("Escape", "Exit camera view");
            Row("F", "Focus on selection");
            Row("G", "Get: bring selection to camera");
            Row("Mouse wheel", "Camera zoom");
            Row("Middle mouse drag", "Camera orbit");
            Row("Right mouse drag", "Camera pan");

            // Selection & Editing
            Section(sp, eLang.shortcutsViewport, "SELECTION & EDITING");
            Row("Left click (viewport)", "Select object");
            Row("Left click + drag", "Rubber-band select");
            Row("Ctrl + D", "Duplicate selection");
            Row("Delete / X", "Delete selected objects");
            Row("Ctrl + Insert", "Add new object");
            Row("Alt + Up / Down", "Move up / down in tree");
            Row("Arrow keys + mouse", "Nudge objects");

            // Viewport
            Section(sp, eLang.shortcutsSelection, "VIEWPORT");
            Row("1 / 2 / 3 / 0", "Snap grid: 1.0 / 0.1 / 0.01 / off");
            Row("Ctrl + NumPad 1-7", "Room render modes");
            Row("Ctrl + Alt + NumPad 1-7", "Model render modes");
            Row("Ctrl + 1-5", "Toggle room/ESL/ETS/ITA/AEV");
            Row("Ctrl + Shift + 1-5", "Toggle FSE/SAR/EAR/ESE/EMI");
            Row("Alt + 1-3", "Toggle QuadCustom/LIT/EFF");
            Row("Ctrl + 6", "Hide disabled enemies");

            // General
            Section(sp, eLang.shortcutsOther, "GENERAL");
            Row("Ctrl + S", "Save project");
            Row("Ctrl + Z", "Undo");
            Row("Ctrl + Y", "Redo");
            Row("F2", "Search (ITA items / AVL keys)");
            Row("Ctrl + F8", "Hide side panel");
            Row("Ctrl + Shift + F8", "Hide bottom panel");
            Row("H", "Toggle isolate selection");
            Row("Click + drag file", "Drag & drop file to open");

            return Scrollable(pageInner);
        }

        private Grid BuildPageLists()
        {
            Grid pageInner = new Grid();
            StackPanel sp = PageShell(pageInner, eLang.groupBoxLists, "Lists", 0, "");

            Section(sp, eLang.groupBoxLists, "Lists");

            comboEnemies = MakeCombo(340);
            comboEtcModels = MakeCombo(340);
            comboItems = MakeCombo(340);
            comboQuadCustom = MakeCombo(340);

            PopulateCombo(comboEnemies, enemiesLists, x => x.ToString());
            PopulateCombo(comboEtcModels, etcModelsLists, x => x.ToString());
            PopulateCombo(comboItems, itemsLists, x => x.ToString());
            PopulateCombo(comboQuadCustom, quadCustomLists, x => x.ToString());

            sp.Children.Add(ComboRow(() => T(eLang.labelEnemies, "Enemies"), comboEnemies, 3));
            sp.Children.Add(ComboRow(() => T(eLang.labelEtcModels, "Etc Models"), comboEtcModels, 8));
            sp.Children.Add(ComboRow(() => T(eLang.labelItems, "Items"), comboItems, 8));
            sp.Children.Add(ComboRow(() => T(eLang.labelQuadCustom, "Quad Custom"), comboQuadCustom, 8));

            return Scrollable(pageInner);
        }

        private Grid BuildPageOthers()
        {
            Grid pageInner = new Grid();
            StackPanel sp = PageShell(pageInner, eLang.tabPageOthers, "Other", 0, "");

            // ---------------- Colors ----------------
            Section(sp, eLang.groupBoxColors, "Colors");

            StackPanel skyGroup = new StackPanel { Orientation = Orientation.Horizontal };
            Border skySwatch = new Border
            {
                Width = 35,
                Height = 26,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(selectedSkyColor),
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            skyPreviewChip = skySwatch;
            skySwatch.MouseLeftButtonUp += delegate
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
            skyGroup.Children.Add(skySwatch);

            TextBlock skyLbl = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            RegText(skyLbl, eLang.labelSkyColor, "Sky Color");
            skyGroup.Children.Add(skyLbl);
            sp.Children.Add(skyGroup);

            // ---------------- Float Style ----------------
            Section(sp, eLang.groupBoxFloatStyle, "Float Style");

            Grid floatGrid = new Grid();
            floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(281, GridUnitType.Star) });
            floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260, GridUnitType.Star) });
            floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(199, GridUnitType.Star) });

            StackPanel inCol = new StackPanel();
            TextBlock inCaption = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            };
            RegText(inCaption, eLang.groupBoxInputFractionalSymbol, "Input fractional symbol");
            inCol.Children.Add(inCaption);

            StackPanel inHost = new StackPanel();
            getInSymbolGetter = MakeRadioRowInto(inHost,
                new eLang[]
                {
                    eLang.radioButtonAcceptsCommaAndPeriod,
                    eLang.radioButtonOnlyAcceptComma,
                    eLang.radioButtonOnlyAcceptPeriod
                },
                new string[]
                {
                    "Accepts , and .",
                    "Accepts only ,",
                    "Accepts only ."
                },
                fracInSymbol,
                delegate(int sel) { UpdateOutputEnabled(); });
            inCol.Children.Add(inHost);
            Grid.SetColumn(inCol, 0);
            floatGrid.Children.Add(inCol);

            StackPanel outCol = new StackPanel();
            TextBlock outCaption = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            };
            RegText(outCaption, eLang.groupBoxOutputFractionalSymbol, "Output fractional symbol");
            outCol.Children.Add(outCaption);

            outSymbolGroup = new StackPanel();
            getOutSymbolValue = MakeRadioRowInto(outSymbolGroup,
                new eLang[] { eLang.radioButtonOutputComma, eLang.radioButtonOutputPeriod },
                new string[] { "Outputs ,", "Outputs ." },
                fracOutSymbol,
                null);
            outCol.Children.Add(outSymbolGroup);
            Grid.SetColumn(outCol, 2);
            floatGrid.Children.Add(outCol);

            StackPanel amountCol = new StackPanel();
            TextBlock amountCaption = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            };
            RegText(amountCaption, eLang.groupBoxFractionalPart, "Fractional part amount");
            amountCol.Children.Add(amountCaption);

            StackPanel amountRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button minus = MakeButton("\u00AB", false, 37, 24, 1);
            minus.FontSize = 11;
            minus.FontWeight = FontWeights.Normal;
            minus.Padding = new Thickness(0);
            minus.Click += delegate
            {
                if (frationalAmount > 4) { frationalAmount--; amountValue.Text = frationalAmount.ToString(); }
            };
            amountRow.Children.Add(minus);

            amountValue = new TextBlock
            {
                Text = frationalAmount.ToString(),
                Foreground = P.BAccent,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 40,
                TextAlignment = TextAlignment.Center
            };
            amountRow.Children.Add(amountValue);

            Button plus = MakeButton("\u00BB", false, 37, 24, 1);
            plus.FontSize = 11;
            plus.FontWeight = FontWeights.Normal;
            plus.Padding = new Thickness(0);
            plus.Click += delegate
            {
                if (frationalAmount < 9) { frationalAmount++; amountValue.Text = frationalAmount.ToString(); }
            };
            amountRow.Children.Add(plus);
            amountCol.Children.Add(amountRow);
            Grid.SetColumn(amountCol, 4);
            floatGrid.Children.Add(amountCol);

            sp.Children.Add(floatGrid);
            UpdateOutputEnabled();

            // ---------------- Item Rotations ----------------
            Section(sp, eLang.groupBoxItemRotations, "Item Rotations");

            bool rotDisableInit = Globals.ItemDisableRotationAll;
            FrameworkElement swDisable = MakeSwitch(
                () => T(eLang.checkBoxDisableItemRotations, "Disable Item Rotations"),
                rotDisableInit, out getRotDisable);
            sp.Children.Add(swDisable);

            bool rotZeroInit = Globals.ItemDisableRotationIfXorYorZequalZero;
            FrameworkElement swZero = MakeSwitch(
                () => T(eLang.checkBoxIgnoreRotationForZeroXYZ, "Ignore rotation if X or Y or Z is equal to zero"),
                rotZeroInit, out getRotIgnoreZeroXYZ);
            swZero.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(swZero);

            bool rotZInit = Globals.ItemDisableRotationIfZisNotGreaterThanZero;
            FrameworkElement swZ = MakeSwitch(
                () => T(eLang.checkBoxIgnoreRotationForZisNotGreaterThanZero, "Ignore rotation if Z is not greater than zero"),
                rotZInit, out getRotIgnoreZNotGTZero);
            swZ.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(swZ);

            TextBlock lblOrder = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 0)
            };
            RegText(lblOrder, eLang.labelitemRotationOrderText, "Rotation Order:");
            sp.Children.Add(lblOrder);

            comboRotOrder = MakeCombo(double.NaN);
            PopulateCombo(comboRotOrder, RotationItems(), x => ((UshortObjForListBox)x).Description);
            textRefreshers.Add(delegate { PopulateCombo(comboRotOrder, RotationItems(), x => ((UshortObjForListBox)x).Description); });
            comboRotOrder.Box.HorizontalAlignment = HorizontalAlignment.Stretch;
            comboRotOrder.Box.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(comboRotOrder.Box);

            TextBlock extraCalc = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 11,
                Margin = new Thickness(0, 12, 0, 3)
            };
            RegText(extraCalc, eLang.labelItemExtraCalculation, "");
            sp.Children.Add(extraCalc);

            Grid calcRow = new Grid();
            calcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            calcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            calcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

            TextBlock lblMul = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            RegText(lblMul, eLang.labelMultiplier, "Multiplier:");
            Grid.SetColumn(lblMul, 0);
            calcRow.Children.Add(lblMul);

            multiplierBox = BuildDarkTextBox(double.NaN);
            multiplierBox.Text = multiplierValue.ToString(CultureInfo.InvariantCulture);
            CommitOn(multiplierBox, delegate
            {
                float v;
                if (float.TryParse(multiplierBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    multiplierValue = v;
                }
                else { multiplierBox.Text = multiplierValue.ToString(CultureInfo.InvariantCulture); }
            });
            Grid.SetColumn(multiplierBox, 1);
            calcRow.Children.Add(multiplierBox);

            TextBlock lblDiv = new TextBlock
            {
                Foreground = P.BText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            RegText(lblDiv, eLang.labelDivider, "Divider:");
            Grid.SetColumn(lblDiv, 3);
            calcRow.Children.Add(lblDiv);

            dividerBox = BuildDarkTextBox(double.NaN);
            dividerBox.Text = dividerValue.ToString(CultureInfo.InvariantCulture);
            CommitOn(dividerBox, delegate
            {
                float v;
                if (float.TryParse(dividerBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    dividerValue = v;
                }
                else { dividerBox.Text = dividerValue.ToString(CultureInfo.InvariantCulture); }
            });
            Grid.SetColumn(dividerBox, 4);
            calcRow.Children.Add(dividerBox);

            sp.Children.Add(calcRow);

            // ---------------- Language ----------------
            Section(sp, eLang.groupBoxLanguage, "Language");

            TextBlock langWarn = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap
            };
            RegText(langWarn, eLang.labelLanguageWarning, "");
            sp.Children.Add(langWarn);

            comboLang = MakeCombo(double.NaN);
            PopulateCombo(comboLang, LangItemArray(), DisplayLang);
            comboLang.SelectedIndex = selectedLangIndex;
            comboLang.Refresh();
            comboLang.SelectionChanged += delegate
            {
                selectedLangIndex = comboLang.SelectedIndex;
                ApplyLiveLanguage();
            };
            textRefreshers.Add(delegate
            {
                object[] items = LangItemArray();
                int keep = comboLang.SelectedIndex;
                PopulateCombo(comboLang, items, DisplayLang);
                comboLang.SelectedIndex = Math.Max(0, Math.Min(keep, items.Length - 1));
                comboLang.Refresh();
            });
            comboLang.Box.HorizontalAlignment = HorizontalAlignment.Stretch;
            comboLang.Box.Margin = new Thickness(0, 5, 0, 0);
            sp.Children.Add(comboLang.Box);

            // ---------------- Theme ----------------
            Section(sp, eLang.groupBoxTheme, "Theme");

            TextBlock themeWarn = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap
            };
            RegText(themeWarn, eLang.labelThemeWarning, "");
            sp.Children.Add(themeWarn);

            bool themeInitial = Globals.BackupConfigs != null && Globals.BackupConfigs.UseDarkerGrayTheme;
            // single master switch: ON = Dark Mode, OFF = Light Mode (exact mirror)
            FrameworkElement themeSwitch = MakeSwitch(
                () => T(eLang.checkBoxUseDarkerGrayTheme, "Dark Mode"),
                themeInitial, out getThemeDark,
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
            themeSwitch.Margin = new Thickness(0, 5, 0, 0);
            sp.Children.Add(themeSwitch);

            // ---------------- Inverted Mouse Buttons ----------------
            Section(sp, eLang.groupBoxInvertedMouseButtons, "Inverted Mouse Buttons");

            TextBlock mouseWarn = new TextBlock
            {
                Foreground = P.BSub,
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap
            };
            RegText(mouseWarn, eLang.labelInvertedMouseButtonsWarning, "");
            sp.Children.Add(mouseWarn);

            bool invertInitial = Globals.BackupConfigs != null && Globals.BackupConfigs.UseInvertedMouseButtons;
            FrameworkElement mouseSwitch = MakeSwitch(
                () => T(eLang.checkBoxUseInvertedMouseButtons, "Use inverted mouse buttons in the 3d viewer"),
                invertInitial, out getInvertMouse);
            mouseSwitch.Margin = new Thickness(0, 5, 0, 0);
            sp.Children.Add(mouseSwitch);

            return Scrollable(pageInner);
        }

        private object[] LangItemArray()
        {
            object[] items = new object[1 + langs.Count];
            items[0] = new object();
            for (int i = 0; i < langs.Count; i++) { items[i + 1] = langs[i]; }
            return items;
        }

        private string DisplayLang(object x)
        {
            JSON.LangObjForList l = x as JSON.LangObjForList;
            if (l != null) { return l.LangName; }
            return Lang.GetText(eLang.OptionsUseInternalLanguage);
        }

        private UshortObjForListBox[] RotationItems()
        {
            return Utils.ItemRotationOrderForListBox();
        }

        private void CommitOn(TextBox box, Action commit)
        {
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { commit(); e.Handled = true; }
            };
            box.LostFocus += delegate { commit(); };
        }

        private readonly List<RadioRow> radioRows = new List<RadioRow>();

        private sealed class RadioRow
        {
            public UiTheme.Palette P;
            public Border Box;
            public Ellipse Dot;
            public int Index;

            public void SetSelected(bool on)
            {
                Dot.Fill = on ? P.BAccent : Brushes.Transparent;
                Dot.Stroke = on ? P.BAccent : P.BSub;
                Dot.StrokeThickness = 1.6;
                Box.Background = on
                    ? P.BRadioSel
                    : Brushes.Transparent;
            }
        }

        private Func<int> MakeRadioRow(StackPanel sp, eLang[] ids, string[] fallbacks, int initial, Action<int> onChanged)
        {
            StackPanel host = new StackPanel();
            sp.Children.Add(host);
            return MakeRadioRowInto(host, ids, fallbacks, initial, onChanged);
        }

        private Func<int> MakeRadioRowInto(StackPanel host, eLang[] ids, string[] fallbacks, int initial, Action<int> onChanged)
        {
            int selected = Math.Max(0, Math.Min(initial, ids.Length - 1));
            List<RadioRow> rows = new List<RadioRow>();

            for (int i = 0; i < ids.Length; i++)
            {
                int idx = i;
                Border rowBorder = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(1),
                    Padding = new Thickness(8, 4, 8, 5),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MinWidth = 220
                };
                StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                Ellipse dot = new Ellipse { Width = 13, Height = 13, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                content.Children.Add(dot);
                TextBlock lbl = new TextBlock
                {
                    Foreground = P.BText,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                textRefreshers.Add(delegate { lbl.Text = T(ids[idx], fallbacks[idx]); });
                lbl.Text = T(ids[idx], fallbacks[idx]);
                content.Children.Add(lbl);
                rowBorder.Child = content;
                host.Children.Add(rowBorder);

                RadioRow rr = new RadioRow { P = P, Box = rowBorder, Dot = dot, Index = idx };
                rows.Add(rr);
                radioRows.Add(rr);

                rowBorder.MouseLeftButtonUp += delegate
                {
                    selected = idx;
                    RefreshRadioGroup(rows, idx);
                    if (onChanged != null) { onChanged(idx); }
                };
                rowBorder.MouseEnter += delegate { if (idx != selected) { rowBorder.Background = P.BHoverSurface; } };
                rowBorder.MouseLeave += delegate { if (idx != selected) { rowBorder.Background = Brushes.Transparent; } };
            }

            RefreshRadioGroup(rows, selected);

            return delegate { return selected; };
        }

        private void RefreshRadioGroup(List<RadioRow> rows, int selectedIdx)
        {
            foreach (RadioRow r in rows)
            {
                r.SetSelected(r.Index == selectedIdx);
            }
        }

        private void UpdateOutputEnabled()
        {
            if (outSymbolGroup == null) { return; }
            bool enabled = getInSymbolGetter == null || getInSymbolGetter() == 0;
            outSymbolGroup.Opacity = enabled ? 1 : 0.45;
            outSymbolGroup.IsHitTestVisible = enabled;
        }

        private Func<int> getInSymbolGetter;

        // ================================================================
        // live appearance
        // ================================================================

        private void RefreshSkySwatches()
        {
            if (skySwatches != null)
            {
                for (int i = 0; i < skySwatches.Length; i++)
                {
                    skySwatches[i].BorderBrush = i == selectedSkyIndex ? P.BAccent : P.BBorderSoft;
                }
            }
            if (skyPreviewChip != null)
            {
                skyPreviewChip.Background = new SolidColorBrush(selectedSkyColor);
            }
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
            foreach (WinForms.Form f in WinForms.Application.OpenForms)
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
            foreach (WinForms.Form f in WinForms.Application.OpenForms)
            {
                MainForm mf = f as MainForm;
                if (mf != null)
                {
                    mf.ApplyTranslationLive();
                    break;
                }
            }
        }

        // ================================================================
        // custom color dialog
        // ================================================================

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
            Rectangle hueBase = new Rectangle { RadiusX = 2, RadiusY = 2 };
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
                byte pr, pg, pb;
                if (t.Length == 6
                    && byte.TryParse(t.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pr)
                    && byte.TryParse(t.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pg)
                    && byte.TryParse(t.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pb))
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

        // ================================================================
        // language live
        // ================================================================

        private void ApplyLiveLanguage()
        {
            JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg == null || langs == null || langs.Count == 0)
            {
                RunTextRefresh();
                return;
            }
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
            RunTextRefresh();
            RepaintOwnerTexts();
        }

        // ================================================================
        // apply / cancel
        // ================================================================

        private static string FixDirectory(string dir)
        {
            return dir != null && dir.Length > 0 ? (dir + (dir.Last() != '\\' ? "\\" : "")) : "";
        }

        private void ApplyAndClose()
        {
            JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg == null)
            {
                cfg = JSON.Configs.GetDefaultConfigs();
                Globals.BackupConfigs = cfg;
            }

            Globals.DirectoryXFILE = FixDirectory(txtXFILE.Text);
            Globals.Directory2007RE4 = FixDirectory(txt2007.Text);
            Globals.DirectoryPS2RE4 = FixDirectory(txtPS2.Text);
            Globals.DirectoryUHDRE4 = FixDirectory(txtUHD.Text);
            Globals.DirectoryPS4NSRE4 = FixDirectory(txtPS4NS.Text);
            Globals.DirectoryCustom1 = FixDirectory(txtCustom1.Text);
            Globals.DirectoryCustom2 = FixDirectory(txtCustom2.Text);
            Globals.DirectoryCustom3 = FixDirectory(txtCustom3.Text);

            JSON.ObjectInfoList selEnemies = comboEnemies.SelectedItem as JSON.ObjectInfoList;
            JSON.ObjectInfoList selEtcModels = comboEtcModels.SelectedItem as JSON.ObjectInfoList;
            JSON.ObjectInfoList selItems = comboItems.SelectedItem as JSON.ObjectInfoList;
            JSON.QuadCustomInfoList selQuadCustom = comboQuadCustom.SelectedItem as JSON.QuadCustomInfoList;

            Globals.FileDiretoryEnemiesList = selEnemies != null ? selEnemies.JsonFileName : Globals.FileDiretoryEnemiesList;
            Globals.FileDiretoryEtcModelsList = selEtcModels != null ? selEtcModels.JsonFileName : Globals.FileDiretoryEtcModelsList;
            Globals.FileDiretoryItemsList = selItems != null ? selItems.JsonFileName : Globals.FileDiretoryItemsList;
            Globals.FileDiretoryQuadCustomList = selQuadCustom != null ? selQuadCustom.JsonFileName : Globals.FileDiretoryQuadCustomList;

            Globals.ToolPathUDAS = toolUdas.Text;
            Globals.ToolPathLFS = toolLfs.Text;
            Globals.ToolPathPACK = toolPack.Text;
            Globals.ToolPathGCA = toolGca.Text;

            Utils.StartReloadDirectoryDic();

            System.Drawing.Color skyDc = ToDrawing(selectedSkyColor);
            Globals.SkyColor = skyDc;

            if (getInSymbolGetter() == 0)
            {
                Globals.FrationalSymbol = getOutSymbolValue() == 0
                    ? ConfigFrationalSymbol.AcceptsCommaAndPeriod_OutputComma
                    : ConfigFrationalSymbol.AcceptsCommaAndPeriod_OutputPeriod;
            }
            else if (getInSymbolGetter() == 1)
            {
                Globals.FrationalSymbol = ConfigFrationalSymbol.OnlyAcceptComma;
            }
            else
            {
                Globals.FrationalSymbol = ConfigFrationalSymbol.OnlyAcceptPeriod;
            }

            Globals.FrationalAmount = frationalAmount;

            Globals.ItemDisableRotationAll = getRotDisable();
            Globals.ItemDisableRotationIfXorYorZequalZero = getRotIgnoreZeroXYZ();
            Globals.ItemDisableRotationIfZisNotGreaterThanZero = getRotIgnoreZNotGTZero();
            Globals.ItemRotationCalculationDivider = (float)dividerValue;
            Globals.ItemRotationCalculationMultiplier = (float)multiplierValue;

            UshortObjForListBox rotSel = comboRotOrder.SelectedItem as UshortObjForListBox;
            if (rotSel != null)
            {
                Globals.ItemRotationOrder = (ObjRotationOrder)rotSel.ID;
            }

            cfg.DirectoryXFILE = Globals.DirectoryXFILE;
            cfg.Directory2007RE4 = Globals.Directory2007RE4;
            cfg.DirectoryPS2RE4 = Globals.DirectoryPS2RE4;
            cfg.DirectoryUHDRE4 = Globals.DirectoryUHDRE4;
            cfg.DirectoryPS4NSRE4 = Globals.DirectoryPS4NSRE4;
            cfg.DirectoryCustom1 = Globals.DirectoryCustom1;
            cfg.DirectoryCustom2 = Globals.DirectoryCustom2;
            cfg.DirectoryCustom3 = Globals.DirectoryCustom3;

            cfg.FileDiretoryEnemiesList = Globals.FileDiretoryEnemiesList;
            cfg.FileDiretoryEtcModelsList = Globals.FileDiretoryEtcModelsList;
            cfg.FileDiretoryItemsList = Globals.FileDiretoryItemsList;
            cfg.FileDiretoryQuadCustomList = Globals.FileDiretoryQuadCustomList;

            cfg.ToolPathUDAS = Globals.ToolPathUDAS;
            cfg.ToolPathLFS = Globals.ToolPathLFS;
            cfg.ToolPathPACK = Globals.ToolPathPACK;
            cfg.ToolPathGCA = Globals.ToolPathGCA;

            cfg.SkyColor = Globals.SkyColor;
            cfg.FrationalAmount = Globals.FrationalAmount;
            cfg.FrationalSymbol = Globals.FrationalSymbol;
            cfg.ItemDisableRotationAll = Globals.ItemDisableRotationAll;
            cfg.ItemDisableRotationIfXorYorZequalZero = Globals.ItemDisableRotationIfXorYorZequalZero;
            cfg.ItemDisableRotationIfZisNotGreaterThanZero = Globals.ItemDisableRotationIfZisNotGreaterThanZero;
            cfg.ItemRotationCalculationDivider = Globals.ItemRotationCalculationDivider;
            cfg.ItemRotationCalculationMultiplier = Globals.ItemRotationCalculationMultiplier;
            cfg.ItemRotationOrder = Globals.ItemRotationOrder;

            bool optionsDark = getThemeDark();
            cfg.UseDarkerGrayTheme = optionsDark;
            cfg.UseLightTheme = !optionsDark;
            cfg.UseInvertedMouseButtons = getInvertMouse();
            if (Globals.BackupConfigs != null) Globals.BackupConfigs.UseInvertedMouseButtons = cfg.UseInvertedMouseButtons;
            try { foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms) if (f is MainForm mf) mf.ApplyInvertedMouseButtons(); } catch { }

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

            try { JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, cfg); } catch (Exception) { }

            bool ForceReload = getForceReload();
            if (ForceReload)
            {
                System.Windows.MessageBox.Show(
                    this,
                    Lang.GetText(eLang.OptionsFormWarningLoadModelsMessageBoxDialog),
                    Lang.GetText(eLang.OptionsFormWarningLoadModelsMessageBoxTitle));

                Utils.ReloadJsonFiles();
                Utils.ReloadModels();
            }

            OnOKButtonClick?.Invoke();
            Close();
        }

        private Func<int> getOutSymbolValue;

        private void DoCancel()
        {
            JSON.Configs cfg = Globals.BackupConfigs;
            bool themeFlipped = getThemeDark != null && getThemeDark() != themeOriginalDark;
            if (cfg != null)
            {
                cfg.SkyColor = skyOriginalLive;
                cfg.LoadLangTranslation = langOriginalLoaded;
                cfg.LangJsonFile = langOriginalFile;
                if (themeFlipped)
                {
                    // the live switch already mutated these: undo it on cancel
                    cfg.UseDarkerGrayTheme = themeOriginalDark;
                    cfg.UseLightTheme = !themeOriginalDark;
                }
            }
            selectedSkyColor = Color.FromArgb(skyOriginalLive.A, skyOriginalLive.R, skyOriginalLive.G, skyOriginalLive.B);
            SetLiveSky(skyOriginalLive);
            if (langOriginalLoaded)
            {
                Utils.StartLoadLangFile();
            }
            else
            {
                Lang.RestoreEnglishDefaults();
                Lang.LoadedTranslation = false;
            }
            if (themeFlipped)
            {
                // visually roll the live switch back to the original theme
                Dispatcher.BeginInvoke(new Action(RethemeWindow),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
            RepaintOwnerTexts();
            Close();
        }

        // ================================================================
        // navigation
        // ================================================================

        private void ShowStepImmediate(int step)
        {
            EnsurePage(step);
            currentStep = step;
            for (int i = 0; i < PageCount; i++)
            {
                if (pages[i] != null)
                    pages[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
            }
            RefreshChrome();
        }

        private void Navigate(int target)
        {
            if (target < 0 || target >= PageCount || target == currentStep || animating || pages == null) { return; }
            EnsurePage(target);
            foreach (DarkCombo c in combos) { if (c.Pop != null) { c.Pop.IsOpen = false; } }

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

        private void RefreshChrome()
        {
            for (int i = 0; i < PageCount; i++)
            {
                bool active = i == currentStep;
                chipLines[i].Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                chipDots[i].Background = active ? P.BAccent : P.BDotIdle;
                chipNums[i].Text = (i + 1).ToString();
                chipLabels[i].Foreground = active ? P.BText : P.BSub;
            }
        }

        // ================================================================
        // list loaders (same behavior as the previous WinForms options)
        // ================================================================

        private JSON.LangObjForList[] GetLangList()
        {
            List<JSON.LangObjForList> list = new List<JSON.LangObjForList>();

            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.LangDirectory);

            string[] Files = new string[0];

            if (Directory.Exists(directory))
            {
                Files = Directory.GetFiles(directory, "*.json");
            }

            for (int i = 0; i < Files.Length; i++)
            {
                try
                {
                    var file = JSON.LangFile.ParseFromFileForList(Files[i]);
                    if (file != null && !list.Contains(file))
                    {
                        list.Add(file);
                    }
                }
                catch (Exception)
                {
                }
            }

            return list.ToArray();
        }

        private JSON.ObjectInfoList[] GetEnemiesListJson()
        {
            List<JSON.ObjectInfoList> lists = new List<JSON.ObjectInfoList>();

            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.EnemiesDirectory);

            string[] Files = new string[0];

            if (Directory.Exists(directory))
            {
                Files = Directory.GetFiles(directory, "*.json");
            }

            for (int i = 0; i < Files.Length; i++)
            {
                try
                {
                    var file = JSON.ObjectInfoListFile.ParseFromFileForOptions(Files[i], Consts.NameEnemiesList);
                    if (file != null && !lists.Contains(file))
                    {
                        lists.Add(file);
                    }
                }
                catch (Exception)
                {
                }
            }

            JSON.ObjectInfoList _default = new JSON.ObjectInfoList(Consts.DefaultEnemiesListFileDirectory, "Default List", "null", new Dictionary<ushort, JSON.ObjectInfo>());
            if (_default != null && !lists.Contains(_default))
            {
                lists.Add(_default);
            }

            return lists.ToArray();
        }

        private JSON.ObjectInfoList[] GetEtcModelsListJson()
        {
            List<JSON.ObjectInfoList> lists = new List<JSON.ObjectInfoList>();

            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.EtcModelsDirectory);

            string[] Files = new string[0];

            if (Directory.Exists(directory))
            {
                Files = Directory.GetFiles(directory, "*.json");
            }

            for (int i = 0; i < Files.Length; i++)
            {
                try
                {
                    var file = JSON.ObjectInfoListFile.ParseFromFileForOptions(Files[i], Consts.NameEtcModelsList);
                    if (file != null && !lists.Contains(file))
                    {
                        lists.Add(file);
                    }
                }
                catch (Exception)
                {
                }
            }

            JSON.ObjectInfoList _default = new JSON.ObjectInfoList(Consts.DefaultEtcModelsListFileDirectory, "Default List", "null", new Dictionary<ushort, JSON.ObjectInfo>());
            if (_default != null && !lists.Contains(_default))
            {
                lists.Add(_default);
            }

            return lists.ToArray();
        }

        private JSON.ObjectInfoList[] GetItemsListJson()
        {
            List<JSON.ObjectInfoList> lists = new List<JSON.ObjectInfoList>();

            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.ItemsDirectory);

            string[] Files = new string[0];

            if (Directory.Exists(directory))
            {
                Files = Directory.GetFiles(directory, "*.json");
            }

            for (int i = 0; i < Files.Length; i++)
            {
                try
                {
                    var file = JSON.ObjectInfoListFile.ParseFromFileForOptions(Files[i], Consts.NameItemsList);
                    if (file != null && !lists.Contains(file))
                    {
                        lists.Add(file);
                    }
                }
                catch (Exception)
                {
                }
            }

            JSON.ObjectInfoList _default = new JSON.ObjectInfoList(Consts.DefaultItemsListFileDirectory, "Default List", "null", new Dictionary<ushort, JSON.ObjectInfo>());
            if (_default != null && !lists.Contains(_default))
            {
                lists.Add(_default);
            }

            return lists.ToArray();
        }

        private JSON.QuadCustomInfoList[] GetQuadCustomListJson()
        {
            List<JSON.QuadCustomInfoList> lists = new List<JSON.QuadCustomInfoList>();

            string directory = IoPath.Combine(AppContext.BaseDirectory, Consts.QuadCustomDirectory);

            string[] Files = new string[0];

            if (Directory.Exists(directory))
            {
                Files = Directory.GetFiles(directory, "*.json");
            }

            for (int i = 0; i < Files.Length; i++)
            {
                try
                {
                    var file = JSON.QuadCustomInfoListFile.ParseFromFileForOptions(Files[i]);
                    if (file != null && !lists.Contains(file))
                    {
                        lists.Add(file);
                    }
                }
                catch (Exception)
                {
                }
            }

            JSON.QuadCustomInfoList _default = new JSON.QuadCustomInfoList(Consts.DefaultQuadCustomModelsListFileDirectory, "Default List", "null", new Dictionary<uint, JSON.QuadCustomInfo>());
            if (_default != null && !lists.Contains(_default))
            {
                lists.Add(_default);
            }

            return lists.ToArray();
        }
    }
}
