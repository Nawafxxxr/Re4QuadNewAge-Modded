using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.EnemyTemplates;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Class.Enums;
using WinForms = System.Windows.Forms;

namespace Re4QuadExtremeEditor.src.Forms
{
    /// <summary>
    /// Professional Enemy Templates window.
    /// Built to feel native to OptionsForm / WelcomeSetupForm: same palette,
    /// same WindowChrome, same Brush factories, same card language.
    /// </summary>
    public class EnemyTemplateWindow : Window
    {
        private UiTheme.Palette P;
        private bool retheming;

        // state
        private List<EnemyTemplate> _filtered = new List<EnemyTemplate>();
        private EnemyTemplate _selected;
        private string _activeCategory = "All";
        private string _search = "";

        // header / filter
        private TextBox txtSearch;
        private DarkCombo cmbCategory;
        private TextBlock countText;
        private TextBlock statusText;
        private Button clearSearchBtn;

        // content
        private StackPanel listPanel;
        private ScrollViewer listScroll;
        private Border emptyState;
        private StackPanel detailStack;
        private ScrollViewer detailScroll;
        private Border detailCard;
        private Border listCard;

        // footer actions
        private Button btnImport;
        private Button btnExport;
        private Button btnDuplicate;
        private Button btnDelete;
        private Button btnApply;

        private readonly List<DarkCombo> _combos = new List<DarkCombo>();

        public EnemyTemplateWindow()
        {
            P = UiTheme.CreatePalette();
            Title = "Enemy Templates";
            Width = 860;
            Height = 560;
            MinWidth = 760;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = P.BWindow;
            Foreground = P.BText;
            FontFamily = new FontFamily("Segoe UI");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var chrome = new System.Windows.Shell.WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            // live theme hook: OptionsForm pushes dark/light; we watch size? For now hook via Deactivated? 
            // Simpler: close+reopen respects theme. Hot reload handled via public Retheme().
            BuildUI();
            RefreshCategoryCombo();
            RefreshList();
            UpdateDetail();

            Loaded += (s, e) =>
            {
                txtSearch?.Focus();
                double maxW = SystemParameters.WorkArea.Width - 20;
                double maxH = SystemParameters.WorkArea.Height - 40;
                if (ActualWidth > maxW) Width = maxW;
                if (ActualHeight > maxH) Height = maxH;
            };
            PreviewKeyDown += OnPreviewKey;
            Deactivated += (s, e) => CloseAllPopups();
            PreviewMouseLeftButtonDown += ClosePopupsIfOutside;
            EnemyTemplateLibrary.LibraryChanged += OnLibraryChanged;
            Closed += (s, e) => { try { EnemyTemplateLibrary.LibraryChanged -= OnLibraryChanged; } catch { } };
        }

        private void OnLibraryChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshCategoryCombo(false);
                RefreshList();
                UpdateDetail();
            }));
        }

        // ================================================================
        // BUILD
        // ================================================================

        private void BuildUI()
        {
            Grid root = new Grid { Background = P.BWindow };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filter strip
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

            BuildHeader(root);
            BuildFilterStrip(root);
            BuildContent(root);
            BuildFooter(root);

            Content = root;
        }

        private void BuildHeader(Grid root)
        {
            Border header = new Border
            {
                Height = 38,
                Background = P.BBar,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            header.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            left.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };

            StackPanel titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock t1 = new TextBlock { Text = "Enemy Templates", Foreground = P.BText, FontSize = 12.8, FontWeight = FontWeights.SemiBold, LineHeight = 14 };
            TextBlock t2 = new TextBlock { Text = "ESL presets  \u00B7  RE4 Quad", Foreground = P.BSub, FontSize = 10.2, Margin = new Thickness(0, 1, 0, 0) };
            t1.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };
            t2.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };
            titles.Children.Add(t1);
            titles.Children.Add(t2);
            left.Children.Add(titles);

            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            Button close = MakeButton("\u2715", false, 40, 26, 1);
            close.Foreground = P.BSub;
            close.FontSize = 11;
            close.FontWeight = FontWeights.Normal;
            close.Padding = new Thickness(0);
            close.Margin = new Thickness(0, 0, 6, 0);
            close.Height = 28; close.Width = 36;
            close.Click += (s, e) => Close();
            close.ToolTip = "Close (Esc)";
            Grid.SetColumn(close, 1);
            g.Children.Add(close);

            header.Child = g;
            Grid.SetRow(header, 0);
            root.Children.Add(header);
        }

        private void BuildFilterStrip(Grid root)
        {
            Border strip = new Border
            {
                Background = P.BWindow,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Search field (styled)
            Style tbStyle = TextBoxStyle();
            Grid searchWrap = new Grid();
            searchWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border searchBorder = new Border
            {
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 0, 4, 0)
            };
            Grid sInner = new Grid { Height = 30 };
            sInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock searchIcon = new TextBlock { Text = "\u2315", Foreground = P.BSub, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0) };
            Grid.SetColumn(searchIcon, 0);
            sInner.Children.Add(searchIcon);

            txtSearch = new TextBox
            {
                Style = tbStyle,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 0, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Foreground = P.BText,
                CaretBrush = P.BText
            };
            txtSearch.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            // override height set by style — keep 30
            txtSearch.Height = 30;
            txtSearch.BorderThickness = new Thickness(0);
            txtSearch.Background = Brushes.Transparent;
            ToolTipService.SetToolTip(txtSearch, "Search by name, enemy, ID, category or tags");
            txtSearch.TextChanged += (s, e) => { _search = txtSearch.Text ?? ""; RefreshList(); };
            txtSearch.GotFocus += (s, e) => { searchBorder.BorderBrush = P.BAccent; };
            txtSearch.LostFocus += (s, e) => { searchBorder.BorderBrush = P.BBorder; };
            Grid.SetColumn(txtSearch, 1);
            sInner.Children.Add(txtSearch);

            clearSearchBtn = MakeMiniIconButton("\u00D7", 18);
            clearSearchBtn.ToolTip = "Clear search";
            clearSearchBtn.Click += (s, e) => { txtSearch.Clear(); txtSearch.Focus(); };
            clearSearchBtn.Visibility = Visibility.Collapsed;
            txtSearch.TextChanged += (s, e) => { clearSearchBtn.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Collapsed : Visibility.Visible; };
            Grid.SetColumn(clearSearchBtn, 2);
            sInner.Children.Add(clearSearchBtn);

            searchBorder.Child = sInner;
            Grid.SetColumn(searchWrap, 0);
            // place searchBorder inside wrap grid? simpler: put directly
            // We'll just use searchBorder as element 0
            Grid.SetColumn(searchBorder, 0);
            g.Children.Add(searchBorder);

            // Category combo
            cmbCategory = MakeCombo(160);
            cmbCategory.Box.ToolTip = "Filter by category";
            cmbCategory.SelectionChanged = () =>
            {
                _activeCategory = cmbCategory.SelectedIndex >= 0 && cmbCategory.SelectedIndex < cmbCategory.Items.Length
                    ? cmbCategory.Items[cmbCategory.SelectedIndex]?.ToString() ?? "All"
                    : "All";
                // normalize "All" vs first item
                if (string.IsNullOrEmpty(_activeCategory)) _activeCategory = "All";
                RefreshList();
            };
            cmbCategory.Box.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cmbCategory.Box, 2);
            g.Children.Add(cmbCategory.Box);

            // count
            countText = new TextBlock { Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = "0 templates" };
            Grid.SetColumn(countText, 4);
            g.Children.Add(countText);

            strip.Child = g;
            Grid.SetRow(strip, 1);
            root.Children.Add(strip);
        }

        private void BuildContent(Grid root)
        {
            Grid cols = new Grid { Background = P.BWindow, Margin = new Thickness(12, 10, 12, 10) };
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340, GridUnitType.Pixel) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left card
            listCard = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(0),
                ClipToBounds = true
            };
            Grid leftInner = new Grid();
            leftInner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftInner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border leftHead = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            Grid lhGrid = new Grid();
            lhGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lhGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lhTitle = new TextBlock { Text = "TEMPLATES", Foreground = P.BSub, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lhTitle, 0);
            lhGrid.Children.Add(lhTitle);
            TextBlock lhHint = new TextBlock { Text = "Click to preview", Foreground = P.BSub, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.9 };
            Grid.SetColumn(lhHint, 1);
            lhGrid.Children.Add(lhHint);
            leftHead.Child = lhGrid;
            Grid.SetRow(leftHead, 0);
            leftInner.Children.Add(leftHead);

            listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(6, 6, 6, 6),
                Background = P.BSurface
            };
            listScroll.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
            listPanel = new StackPanel();
            listScroll.Content = listPanel;

            emptyState = new Border
            {
                Margin = new Thickness(8, 18, 8, 8),
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            StackPanel emp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock empIcon = new TextBlock { Text = "\u25CB", Foreground = P.BBorder, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            TextBlock empT = new TextBlock { Text = "No templates found", Foreground = P.BText, FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock empSub = new TextBlock { Text = "Try another search or category.", Foreground = P.BSub, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 220, TextAlignment = TextAlignment.Center };
            emp.Children.Add(empIcon); emp.Children.Add(empT); emp.Children.Add(empSub);
            emptyState.Child = emp;

            Grid listHost = new Grid();
            listHost.Children.Add(listScroll);
            listHost.Children.Add(emptyState);
            Grid.SetRow(listHost, 1);
            leftInner.Children.Add(listHost);

            listCard.Child = leftInner;
            Grid.SetColumn(listCard, 0);
            cols.Children.Add(listCard);

            // Detail card
            detailCard = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(0),
                ClipToBounds = true
            };
            detailScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0),
                Background = P.BSurface
            };
            detailScroll.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
            detailStack = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };
            detailScroll.Content = detailStack;

            Grid detailWrapper = new Grid();
            detailWrapper.Children.Add(detailScroll);
            detailCard.Child = detailWrapper;
            Grid.SetColumn(detailCard, 2);
            cols.Children.Add(detailCard);

            Grid.SetRow(cols, 2);
            root.Children.Add(cols);
        }

        private void BuildFooter(Grid root)
        {
            Border footer = new Border
            {
                Height = 44,
                Background = P.BBar,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10, 0, 8, 0)
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // left status
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            statusText = new TextBlock { Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = "" };
            left.Children.Add(statusText);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

            btnImport = MakeButton("Import", false, double.NaN, 26, 4);
            btnImport.ToolTip = "Import .json template(s)";
            btnImport.Padding = new Thickness(10, 0, 10, 0);
            btnImport.Margin = new Thickness(4, 0, 0, 0);
            btnImport.Click += BtnImport_Click;
            right.Children.Add(btnImport);

            btnExport = MakeButton("Export", false, double.NaN, 26, 4);
            btnExport.ToolTip = "Export selected template";
            btnExport.Padding = new Thickness(10, 0, 10, 0);
            btnExport.Margin = new Thickness(4, 0, 0, 0);
            btnExport.Click += BtnExport_Click;
            right.Children.Add(btnExport);

            Button btnSave = MakeButton("Save from Enemy", false, double.NaN, 26, 4);
            btnSave.ToolTip = "Create template from the enemy currently selected in the scene (ESL)";
            btnSave.Padding = new Thickness(10, 0, 10, 0);
            btnSave.Margin = new Thickness(4, 0, 0, 0);
            btnSave.Click += BtnSave_Click;
            right.Children.Add(btnSave);

            btnDuplicate = MakeButton("Duplicate", false, double.NaN, 26, 4);
            btnDuplicate.ToolTip = "Clone selected template";
            btnDuplicate.Padding = new Thickness(10, 0, 10, 0);
            btnDuplicate.Margin = new Thickness(8, 0, 0, 0);
            btnDuplicate.Click += BtnDuplicate_Click;
            right.Children.Add(btnDuplicate);

            btnDelete = MakeButton("Delete", false, double.NaN, 26, 4);
            btnDelete.ToolTip = "Delete selected template (Del)";
            btnDelete.Padding = new Thickness(10, 0, 10, 0);
            btnDelete.Margin = new Thickness(4, 0, 0, 0);
            btnDelete.Click += BtnDelete_Click;
            right.Children.Add(btnDelete);

            btnApply = MakeButton("Apply to Selection", true, double.NaN, 28, 4);
            btnApply.ToolTip = "Apply template to the selected ESL enemy (Enter)";
            btnApply.Padding = new Thickness(14, 0, 14, 0);
            btnApply.Margin = new Thickness(8, 0, 0, 0);
            btnApply.FontWeight = FontWeights.SemiBold;
            btnApply.Click += BtnApply_Click;
            right.Children.Add(btnApply);

            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            footer.Child = g;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
        }

        // ================================================================
        // DATA
        // ================================================================

        private void RefreshCategoryCombo(bool keepSelection = true)
        {
            string keep = _activeCategory;
            cmbCategory.Items = new object[0];
            cmbCategory.ListPanel.Children.Clear();
            var cats = new List<string> { "All" };
            cats.AddRange(EnemyTemplateLibrary.Categories);
            object[] items = cats.Cast<object>().ToArray();
            PopulateCombo(cmbCategory, items, x => x.ToString());
            int idx = 0;
            if (keepSelection && !string.IsNullOrEmpty(keep))
            {
                idx = Array.FindIndex(items, x => x.ToString().Equals(keep, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) idx = 0;
            }
            cmbCategory.SelectedIndex = idx;
            _activeCategory = items[idx].ToString();
            cmbCategory.Refresh();
            if (countText != null)
                countText.Text = EnemyTemplateLibrary.Templates.Count + " templates";
        }

        private void RefreshList()
        {
            if (listPanel == null) return;
            _filtered = EnemyTemplateLibrary.Search(_activeCategory, _search);
            listPanel.Children.Clear();

            if (_filtered.Count == 0)
            {
                listScroll.Visibility = Visibility.Collapsed;
                emptyState.Visibility = Visibility.Visible;
            }
            else
            {
                listScroll.Visibility = Visibility.Visible;
                emptyState.Visibility = Visibility.Collapsed;
            }

            if (countText != null)
            {
                if (string.IsNullOrWhiteSpace(_search) && _activeCategory == "All")
                    countText.Text = _filtered.Count + " templates";
                else
                    countText.Text = _filtered.Count + " / " + EnemyTemplateLibrary.Templates.Count;
            }

            // if selected no longer in filtered, clear selection highlight but keep detail?
            bool selStillVisible = _selected != null && _filtered.Any(t => t.Name.Equals(_selected.Name, StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < _filtered.Count; i++)
            {
                var t = _filtered[i];
                bool isSel = _selected != null && _selected.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase);
                Border card = MakeTemplateCard(t, isSel);
                listPanel.Children.Add(card);
            }

            if (!selStillVisible && _filtered.Count > 0 && _selected != null)
            {
                // keep detail as is but dim apply? no auto switch
            }

            UpdateFooterAvailability();
        }

        private Border MakeTemplateCard(EnemyTemplate t, bool isSelected)
        {
            Border b = new Border
            {
                Background = isSelected ? P.BRadioSel : Brushes.Transparent,
                BorderBrush = isSelected ? P.BAccent : P.BBorderSoft,
                BorderThickness = new Thickness(isSelected ? 1 : 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            if (isSelected) b.BorderThickness = new Thickness(1);
            else b.BorderThickness = new Thickness(1);
            // subtle hover
            b.MouseEnter += (s, e) => { if (!isSelected) b.Background = P.BHoverSurface; };
            b.MouseLeave += (s, e) => { if (!isSelected) b.Background = Brushes.Transparent; };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // row0: name + category pill
            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock nameTb = new TextBlock
            {
                Text = t.Name,
                Foreground = P.BText,
                FontSize = 12.2,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameTb, 0);
            top.Children.Add(nameTb);

            Border catPill = new Border
            {
                Background = P.BRadioSel,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 2),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock catTx = new TextBlock { Text = t.GetCategorySafe(), Foreground = P.BAccent, FontSize = 10, FontWeight = FontWeights.SemiBold };
            catPill.Child = catTx;
            Grid.SetColumn(catPill, 1);
            top.Children.Add(catPill);
            Grid.SetRow(top, 0);
            g.Children.Add(top);

            // row1: enemy identity
            StackPanel mid = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            Border idPill = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 0, 5, 1),
                Margin = new Thickness(0, 0, 6, 0)
            };
            TextBlock idTx = new TextBlock { Text = "0x" + t.EnemyId.ToString("X4"), Foreground = P.BSub, FontSize = 10.5, FontFamily = new FontFamily("Consolas") };
            idPill.Child = idTx;
            mid.Children.Add(idPill);

            TextBlock enemyTx = new TextBlock { Text = string.IsNullOrWhiteSpace(t.EnemyName) ? "Unknown" : t.EnemyName, Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 170 };
            mid.Children.Add(enemyTx);

            // enable dot
            Border dot = new Border
            {
                Width = 7, Height = 7, CornerRadius = new CornerRadius(4),
                Background = t.Enable == 0 ? new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x5A)) : new SolidColorBrush(Color.FromRgb(0x3A, 0xC2, 0x6B)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = t.Enable == 0 ? "Disabled" : "Enabled"
            };
            mid.Children.Add(dot);

            // HP badge if >0
            if (t.Life != 0)
            {
                Border hp = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = P.BBorderSoft,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 0, 5, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                hp.Child = new TextBlock { Text = "HP " + t.Life, Foreground = P.BSub, FontSize = 10.5 };
                mid.Children.Add(hp);
            }

            Grid.SetRow(mid, 1);
            g.Children.Add(mid);

            // row2: description / tags + small meta
            if (!string.IsNullOrWhiteSpace(t.Description) || (t.Tags != null && t.Tags.Count > 0))
            {
                StackPanel bot = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
                if (!string.IsNullOrWhiteSpace(t.Description))
                {
                    TextBlock desc = new TextBlock { Text = t.Description, Foreground = P.BSub, FontSize = 10.8, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 32 };
                    bot.Children.Add(desc);
                }
                if (t.Tags != null && t.Tags.Count > 0)
                {
                    WrapPanel tags = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                    foreach (string tag in t.Tags.Take(4))
                    {
                        Border tb = new Border { Background = P.BHoverSurface, CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 0, 5, 1), Margin = new Thickness(0, 0, 4, 2) };
                        tb.Child = new TextBlock { Text = "#" + tag, Foreground = P.BSub, FontSize = 10 };
                        tags.Children.Add(tb);
                    }
                    bot.Children.Add(tags);
                }
                Grid.SetRow(bot, 2);
                g.Children.Add(bot);
            }

            b.Child = g;
            b.MouseLeftButtonUp += (s, e) => { SetSelected(t); e.Handled = true; };
            // double click to apply
            b.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { SetSelected(t); BtnApply_Click(null, null); }
            };
            // context menu via right click
            b.MouseRightButtonUp += (s, e) => { SetSelected(t); ShowCardContext(t, b); e.Handled = true; };

            return b;
        }

        private void ShowCardContext(EnemyTemplate t, Border anchor)
        {
            // simple popup context: Duplicate / Rename / Export / Delete
            // Use a Popup with StackPanel of buttons
            Popup pop = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            Border box = new Border
            {
                Background = P.BSurface,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4),
                MinWidth = 160
            };
            StackPanel sp = new StackPanel();
            Action<string, Action> add = (label, act) =>
            {
                Border item = new Border { Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 6, 10, 6), Cursor = Cursors.Hand };
                TextBlock tx = new TextBlock { Text = label, Foreground = P.BText, FontSize = 11.5 };
                item.Child = tx;
                item.MouseEnter += (s, e) => item.Background = P.BHoverSurface;
                item.MouseLeave += (s, e) => item.Background = Brushes.Transparent;
                item.MouseLeftButtonUp += (s, e) => { pop.IsOpen = false; act(); };
                sp.Children.Add(item);
            };
            add("Duplicate", () => BtnDuplicate_Click(null, null));
            add("Rename…", () => RenameSelected());
            add("Export…", () => BtnExport_Click(null, null));
            Thickness sepT = new Thickness(4, 4, 4, 4);
            Border sep = new Border { Height = 1, Background = P.BBorderSoft, Margin = sepT };
            sp.Children.Add(sep);
            Border del = new Border { Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 6, 10, 6), Cursor = Cursors.Hand };
            TextBlock delTx = new TextBlock { Text = "Delete", Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x5A)), FontSize = 11.5 };
            del.Child = delTx;
            del.MouseEnter += (s, e) => del.Background = new SolidColorBrush(Color.FromArgb(30, 0xE0, 0x5A, 0x5A));
            del.MouseLeave += (s, e) => del.Background = Brushes.Transparent;
            del.MouseLeftButtonUp += (s, e) => { pop.IsOpen = false; BtnDelete_Click(null, null); };
            sp.Children.Add(del);

            box.Child = sp;
            pop.Child = box;
            pop.IsOpen = true;
        }

        private void SetSelected(EnemyTemplate t)
        {
            _selected = t;
            RefreshList(); // to update selection highlight
            UpdateDetail();
            UpdateFooterAvailability();
        }

        private void UpdateFooterAvailability()
        {
            bool hasSel = _selected != null;
            if (btnApply != null) { btnApply.IsEnabled = hasSel; btnApply.Opacity = hasSel ? 1 : 0.55; }
            if (btnDelete != null) { btnDelete.IsEnabled = hasSel; btnDelete.Opacity = hasSel ? 1 : 0.55; }
            if (btnDuplicate != null) { btnDuplicate.IsEnabled = hasSel; btnDuplicate.Opacity = hasSel ? 1 : 0.55; }
            if (btnExport != null) { btnExport.IsEnabled = hasSel; btnExport.Opacity = hasSel ? 1 : 0.55; }
        }

        private void UpdateDetail()
        {
            if (detailStack == null) return;
            detailStack.Children.Clear();

            if (_selected == null)
            {
                // placeholder
                StackPanel ph = new StackPanel { Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                Border icWrap = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Background = P.BHoverSurface, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
                TextBlock ic = new TextBlock { Text = "\u2726", Foreground = P.BSub, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                icWrap.Child = ic;
                ph.Children.Add(icWrap);
                ph.Children.Add(new TextBlock { Text = "Select a template", Foreground = P.BText, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
                ph.Children.Add(new TextBlock { Text = "Choose a preset on the left to preview its fields and apply it to the enemy selected in the scene.", Foreground = P.BSub, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 300, Margin = new Thickness(0, 6, 0, 0) });
                StackPanel hints = new StackPanel { Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                hints.Children.Add(HintRow("Double-click", "apply instantly"));
                hints.Children.Add(HintRow("Right-click", "duplicate / rename / export"));
                hints.Children.Add(HintRow("Enter", "apply  •  Del — delete"));
                ph.Children.Add(hints);
                detailStack.Children.Add(ph);
                return;
            }

            var t = _selected;

            // Title row
            Grid titleGrid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleLeft = new StackPanel();
            TextBlock nameTx = new TextBlock { Text = t.Name, Foreground = P.BText, FontSize = 16, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            titleLeft.Children.Add(nameTx);
            StackPanel sub = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            // Enemy badge
            Border enBadge = new Border { Background = P.BAccent, CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(0, 0, 6, 0) };
            enBadge.Child = new TextBlock { Text = (string.IsNullOrWhiteSpace(t.EnemyName) ? "Unknown" : t.EnemyName), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold };
            sub.Children.Add(enBadge);
            Border idBadge = new Border { Background = P.BSurface, BorderBrush = P.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 3), Margin = new Thickness(0, 0, 6, 0) };
            idBadge.Child = new TextBlock { Text = "0x" + t.EnemyId.ToString("X4"), Foreground = P.BSub, FontSize = 11, FontFamily = new FontFamily("Consolas") };
            sub.Children.Add(idBadge);
            Border catBadge = new Border { Background = P.BRadioSel, BorderBrush = P.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 3) };
            catBadge.Child = new TextBlock { Text = t.GetCategorySafe(), Foreground = P.BAccent, FontSize = 11, FontWeight = FontWeights.SemiBold };
            sub.Children.Add(catBadge);
            titleLeft.Children.Add(sub);
            Grid.SetColumn(titleLeft, 0);
            titleGrid.Children.Add(titleLeft);

            // enable indicator + room
            StackPanel rightMeta = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            Border enDot = new Border { Background = t.Enable == 0 ? new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x5A)) : new SolidColorBrush(Color.FromRgb(0x2E, 0xB8, 0x6B)), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 3, 8, 4) };
            enDot.Child = new TextBlock { Text = t.Enable == 0 ? "DISABLED" : "ENABLED", Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.Bold };
            rightMeta.Children.Add(enDot);
            TextBlock roomTx = new TextBlock { Text = "Room 0x" + t.RoomId.ToString("X3"), Foreground = P.BSub, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            rightMeta.Children.Add(roomTx);
            Grid.SetColumn(rightMeta, 1);
            titleGrid.Children.Add(rightMeta);

            detailStack.Children.Add(titleGrid);

            if (!string.IsNullOrWhiteSpace(t.Description))
            {
                TextBlock desc = new TextBlock { Text = t.Description, Foreground = P.BSub, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
                detailStack.Children.Add(desc);
            }

            // Tags
            if (t.Tags != null && t.Tags.Count > 0)
            {
                WrapPanel tags = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                foreach (string tag in t.Tags)
                {
                    Border tb = new Border { Background = P.BHoverSurface, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(0, 0, 6, 4) };
                    tb.Child = new TextBlock { Text = "#" + tag, Foreground = P.BSub, FontSize = 10.5 };
                    tags.Children.Add(tb);
                }
                detailStack.Children.Add(tags);
            }

            // Divider
            detailStack.Children.Add(new Border { Height = 1, Background = P.BBorderSoft, Margin = new Thickness(0, 12, 0, 10) });

            // Overview grid
            Section(detailStack, "OVERVIEW");
            Grid over = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            over.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            over.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            over.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            over.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            over.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            over.Children.Add(FieldCard("Life", t.Life.ToString(), 0, 0));
            over.Children.Add(FieldCard("Room", "0x" + t.RoomId.ToString("X3"), 0, 1));
            over.Children.Add(FieldCard("Enable", t.Enable == 0 ? "00 (off)" : t.Enable == 1 ? "01 (on)" : "0x" + t.Enable.ToString("X2"), 1, 0));
            over.Children.Add(FieldCard("Unknown 0A·0B", string.Format("{0:X2} {1:X2}", t.Unknown0A, t.Unknown0B), 1, 1));
            over.Children.Add(FieldCard("Position", string.Format("{0}, {1}, {2}", t.PositionX, t.PositionY, t.PositionZ), 2, 0));
            over.Children.Add(FieldCard("Rotation", string.Format("{0}, {1}, {2}", t.RotationX, t.RotationY, t.RotationZ), 2, 1));
            detailStack.Children.Add(over);

            // Unknowns collapsible
            Section(detailStack, "RAW BYTES");
            detailStack.Children.Add(HexLinePreview(t));
            Grid unks = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            unks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            unks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            unks.Children.Add(FieldCard("Body 03-07", string.Format("{0:X2} {1:X2} {2:X2} {3:X2} {4:X2}", t.Unknown03, t.Unknown04, t.Unknown05, t.Unknown06, t.Unknown07), 0, 0));
            unks.Children.Add(FieldCard("Tail 1A-1F", string.Format("{0:X2} {1:X2} {2:X2} {3:X2} {4:X2} {5:X2}", t.Unknown1A, t.Unknown1B, t.Unknown1C, t.Unknown1D, t.Unknown1E, t.Unknown1F), 0, 1));
            detailStack.Children.Add(unks);

            // Apply rules
            detailStack.Children.Add(new Border { Height = 1, Background = P.BBorderSoft, Margin = new Thickness(0, 12, 0, 10) });
            Grid applyHead = new Grid();
            applyHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            applyHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock ahT = new TextBlock { Text = "APPLY RULES", Foreground = P.BAccent, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ahT, 0); applyHead.Children.Add(ahT);
            TextBlock ahSub = new TextBlock { Text = "Fields copied on Apply", Foreground = P.BSub, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ahSub, 1); applyHead.Children.Add(ahSub);
            detailStack.Children.Add(applyHead);

            StackPanel applyList = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            applyList.Children.Add(ApplyToggle("Enable", "Byte 0x00", t.Apply.Enable, v => { t.Apply.Enable = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Enemy ID", "0x01-0x02 (BE)", t.Apply.EnemyId, v => { t.Apply.EnemyId = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Life", "0x08-0x09", t.Apply.Life, v => { t.Apply.Life = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Body bytes", "03-07 + 0A-0B", t.Apply.UnknownBody, v => { t.Apply.UnknownBody = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Position", "0x0C-0x11 (overwrites placement!)", t.Apply.Position, v => { t.Apply.Position = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Rotation", "0x12-0x17 (overwrites facing!)", t.Apply.Rotation, v => { t.Apply.Rotation = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Room", "0x18-0x19", t.Apply.RoomId, v => { t.Apply.RoomId = v; TouchTemplate(t); }));
            applyList.Children.Add(ApplyToggle("Tail bytes", "0x1A-0x1F", t.Apply.UnknownTail, v => { t.Apply.UnknownTail = v; TouchTemplate(t); }));
            detailStack.Children.Add(applyList);

            // quick actions inside detail
            Border actionRow = new Border { Background = P.BHoverSurface, CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Margin = new Thickness(0, 12, 0, 0) };
            Grid arGrid = new Grid();
            arGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            arGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock arTx = new TextBlock { Text = "Apply copies only the checked fields — position/rotation stay where the target already is unless you enable them.", Foreground = P.BSub, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(arTx, 0); arGrid.Children.Add(arTx);
            Button arBtn = MakeButton("Apply now", true, double.NaN, 26, 4);
            arBtn.Padding = new Thickness(12, 0, 12, 0);
            arBtn.Click += BtnApply_Click;
            Grid.SetColumn(arBtn, 1); arGrid.Children.Add(arBtn);
            actionRow.Child = arGrid;
            detailStack.Children.Add(actionRow);

            // footer meta
            TextBlock meta = new TextBlock
            {
                Text = string.Format("Created {0:yyyy-MM-dd HH:mm}  \u00B7  Updated {1:yyyy-MM-dd HH:mm}", t.CreatedAt, t.UpdatedAt),
                Foreground = P.BSub, FontSize = 10, Margin = new Thickness(0, 10, 0, 0), Opacity = 0.85
            };
            detailStack.Children.Add(meta);
        }

        private void TouchTemplate(EnemyTemplate t)
        {
            t.UpdatedAt = DateTime.Now;
            try { EnemyTemplateLibrary.Save(t); } catch { }
            SetStatus("Apply rules updated for \"" + t.Name + "\"", false);
        }

        private FrameworkElement HintRow(string k, string v)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock a = new TextBlock { Text = k, Foreground = P.BAccent, FontSize = 10.5, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Consolas") };
            TextBlock b = new TextBlock { Text = "  " + v, Foreground = P.BSub, FontSize = 10.5 };
            sp.Children.Add(a); sp.Children.Add(b);
            return sp;
        }

        private void Section(StackPanel sp, string title)
        {
            TextBlock tb = new TextBlock { Text = title, Foreground = P.BAccent, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(tb);
        }

        private FrameworkElement FieldCard(string label, string value, int row, int col)
        {
            Border b = new Border
            {
                Background = P.BWindow,
                BorderBrush = P.BBorderSoft,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(col == 0 ? 0 : 6, row == 0 ? 0 : 6, 0, 0)
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), Foreground = P.BSub, FontSize = 10, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = value, Foreground = P.BText, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
            b.Child = sp;
            Grid.SetRow(b, row); Grid.SetColumn(b, col);
            return b;
        }

        private FrameworkElement HexLinePreview(EnemyTemplate t)
        {
            byte[] line = t.ToLineBytes();
            string hex = BitConverter.ToString(line).Replace("-", " ");
            Border b = new Border
            {
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 6, 0, 0)
            };
            StackPanel sp = new StackPanel();
            TextBlock lbl = new TextBlock { Text = "32-byte ESL line preview", Foreground = P.BSub, FontSize = 10 };
            sp.Children.Add(lbl);
            TextBlock hexTx = new TextBlock { Text = hex, Foreground = P.BText, FontSize = 10, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            sp.Children.Add(hexTx);
            Button copyBtn = MakeButton("Copy hex", false, double.NaN, 22, 3);
            copyBtn.FontSize = 10;
            copyBtn.Padding = new Thickness(8, 0, 8, 0);
            copyBtn.Margin = new Thickness(0, 6, 0, 0);
            copyBtn.HorizontalAlignment = HorizontalAlignment.Left;
            copyBtn.Click += (s, e) => { try { Clipboard.SetText(hex.Replace(" ", "")); SetStatus("Hex copied.", false); } catch { } };
            sp.Children.Add(copyBtn);
            b.Child = sp;
            return b;
        }

        private FrameworkElement ApplyToggle(string title, string hint, bool initial, Action<bool> onChanged)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            bool state = initial;
            Border track = new Border
            {
                Width = 36, Height = 18, CornerRadius = new CornerRadius(9),
                Background = state ? (Brush)P.BAccent : (Brush)P.BSwitchOff,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            Ellipse knob = new Ellipse { Width = 12, Height = 12, Fill = P.BKnob, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), IsHitTestVisible = false };
            TranslateTransform mv = new TranslateTransform(state ? 17 : 0, 0);
            knob.RenderTransform = mv;
            // overlay for hit
            Border hit = new Border { Width = 36, Height = 18, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            Grid wrap = new Grid { Width = 36, Height = 18 };
            wrap.Children.Add(track); wrap.Children.Add(knob); wrap.Children.Add(hit);
            Grid.SetColumn(wrap, 0);
            g.Children.Add(wrap);

            StackPanel txt = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock tt = new TextBlock { Text = title, Foreground = P.BText, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            TextBlock ht = new TextBlock { Text = hint, Foreground = P.BSub, FontSize = 10.5 };
            txt.Children.Add(tt); txt.Children.Add(ht);
            Grid.SetColumn(txt, 1);
            g.Children.Add(txt);

            // store brushes for animation? keep simple instant switch, no animation needed for inline
            Action toggle = null;
            toggle = () =>
            {
                state = !state;
                track.Background = state ? (Brush)P.BAccent : (Brush)P.BSwitchOff;
                // animate knob
                DoubleAnimation an = new DoubleAnimation(state ? 17 : 0, new Duration(TimeSpan.FromMilliseconds(140))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                mv.BeginAnimation(TranslateTransform.XProperty, an);
                onChanged(state);
            };
            hit.MouseLeftButtonUp += (s, e) => toggle();
            txt.MouseLeftButtonUp += (s, e) => toggle();
            // hover
            hit.MouseEnter += (s, e) => { if (!state) track.Background = P.BBorder; };
            hit.MouseLeave += (s, e) => { if (!state) track.Background = P.BSwitchOff; };

            return g;
        }

        // ================================================================
        // ACTIONS
        // ================================================================

        private Object3D GetSelectedEnemy(out ushort id, out string name)
        {
            id = 0; name = "Unknown";
            Object3D obj = DataBase.LastSelectNode as Object3D;
            if (obj == null || obj.Group != GroupType.ESL) return null;
            if (DataBase.FileESL == null || !DataBase.FileESL.Lines.ContainsKey(obj.ObjLineRef)) return null;
            id = obj.ObjLineRef;
            try
            {
                if (DataBase.EnemiesIDs != null && DataBase.EnemiesIDs.List.ContainsKey(id))
                    name = DataBase.EnemiesIDs.List[id].Name;
                else
                {
                    // try resolve via enemy type id at 0x01-0x02
                    byte[] line = DataBase.FileESL.Lines[id];
                    ushort eid = 0;
                    try
                    {
                        byte[] tmp = new byte[2]; tmp[1] = line[0x01]; tmp[0] = line[0x02];
                        eid = BitConverter.ToUInt16(tmp, 0);
                        if (DataBase.EnemiesIDs != null)
                        {
                            string sv = eid.ToString("X4");
                            string svff = sv[0].ToString() + sv[1].ToString() + "FF";
                            ushort vff = ushort.Parse(svff, System.Globalization.NumberStyles.HexNumber);
                            if (DataBase.EnemiesIDs.List.ContainsKey(eid)) name = DataBase.EnemiesIDs.List[eid].Name;
                            else if (DataBase.EnemiesIDs.List.ContainsKey(vff)) name = DataBase.EnemiesIDs.List[vff].Name;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return obj;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ushort eid;
            string ename;
            var obj = GetSelectedEnemy(out eid, out ename);
            if (obj == null)
            {
                SetStatus("Select an ESL enemy in the scene first.", true);
                MessageBox.Show("Select an Enemy in the scene first (ESL).", "Enemy Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!DataBase.FileESL.Lines.ContainsKey(eid)) return;
            byte[] line = DataBase.FileESL.Lines[eid];
            // resolve display name for enemy type
            string typeName = ename;
            try
            {
                ushort typeId;
                byte[] tmp = new byte[2]; tmp[1] = line[0x01]; tmp[0] = line[0x02];
                typeId = BitConverter.ToUInt16(tmp, 0);
                if (DataBase.EnemiesIDs != null && DataBase.EnemiesIDs.List.ContainsKey(typeId))
                    typeName = DataBase.EnemiesIDs.List[typeId].Name;
                else if (DataBase.EnemiesIDs != null)
                {
                    string sv = typeId.ToString("X4");
                    string svff = sv.Substring(0, 2) + "FF";
                    ushort vff = ushort.Parse(svff, System.Globalization.NumberStyles.HexNumber);
                    if (DataBase.EnemiesIDs.List.ContainsKey(vff)) typeName = DataBase.EnemiesIDs.List[vff].Name;
                }
            }
            catch { }

            var dlg = new TemplateSaveDialog(P, typeName, "0x" + eid.ToString("X4"), eid, typeName);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                var t = EnemyTemplate.FromEnemy(eid, typeName, line);
                t.Name = dlg.TemplateName.Trim();
                t.Category = dlg.TemplateCategory;
                t.Description = dlg.TemplateDescription.Trim();
                t.Tags = dlg.TemplateTags;
                // Apply rules from dialog? keep defaults unless user edited? dlg returns nothing; keep default
                if (dlg.ApplyOptions != null) t.Apply = dlg.ApplyOptions;
                string err;
                if (!t.IsValid(out err))
                {
                    MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                EnemyTemplateLibrary.Save(t);
                SetSelected(t);
                SetStatus("Template \"" + t.Name + "\" saved.", false);
            }
        }

        private void BtnOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                SetStatus("Select a template first.", true);
                MessageBox.Show("Select a template first.", "Enemy Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new TemplateOptionsDialog(P, _selected.Apply.Clone());
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                _selected.Apply = dlg.Result;
                _selected.UpdatedAt = DateTime.Now;
                EnemyTemplateLibrary.Save(_selected);
                UpdateDetail();
                SetStatus("Apply rules updated.", false);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                SetStatus("Select a template first.", true);
                MessageBox.Show("Select a template first.", "Enemy Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ushort targetId;
            string dummy;
            var obj = GetSelectedEnemy(out targetId, out dummy);
            if (obj == null)
            {
                SetStatus("Select a target ESL enemy in the scene.", true);
                MessageBox.Show("Select an Enemy in the scene first (ESL).", "Enemy Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // support multi selection if Extras? For now apply to single LastSelectNode.
            // If tree has multi, we could iterate selected nodes, but keep single for safety.
            bool ok = _selected.ApplyToTarget(targetId);
            if (!ok)
            {
                SetStatus("Apply failed — ESL not found.", true);
                return;
            }
            // notify
            try
            {
                EnemyTemplateLibrary.Save(_selected); // touch updatedAt already? not needed but keeps order
            }
            catch { }
            // refresh viewport and property grid if possible
            TryRefreshEditor();
            SetStatus("Applied \"" + _selected.Name + "\" to 0x" + targetId.ToString("X4"), false);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                SetStatus("Select a template first.", true);
                return;
            }
            string name = _selected.Name;
            var res = MessageBox.Show("Delete template \"" + name + "\"?\nThis cannot be undone.", "Delete Template", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            EnemyTemplateLibrary.Delete(_selected);
            _selected = null;
            RefreshList();
            UpdateDetail();
            SetStatus("Deleted \"" + name + "\"", false);
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                SetStatus("Select a template first.", true);
                return;
            }
            var dup = EnemyTemplateLibrary.Duplicate(_selected);
            if (dup != null)
            {
                SetSelected(dup);
                SetStatus("Duplicated as \"" + dup.Name + "\"", false);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                SetStatus("Select a template first.", true);
                return;
            }
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export template",
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = EnemyTemplateLibrary.SafeName(_selected.Name) + ".json",
                DefaultExt = "json"
            };
            bool? ok = sfd.ShowDialog(this);
            if (ok == true)
            {
                if (EnemyTemplateLibrary.Export(_selected, sfd.FileName))
                    SetStatus("Exported to " + sfd.FileName, false);
                else
                    SetStatus("Export failed.", true);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import template(s)",
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = true
            };
            bool? ok = ofd.ShowDialog(this);
            if (ok == true)
            {
                int imported = 0;
                foreach (string f in ofd.FileNames)
                {
                    var t = EnemyTemplateLibrary.Import(f);
                    if (t != null) imported++;
                }
                if (imported > 0)
                {
                    RefreshCategoryCombo();
                    RefreshList();
                    SetStatus(imported == 1 ? "Imported 1 template." : "Imported " + imported + " templates.", false);
                }
                else SetStatus("Nothing imported.", true);
            }
        }

        private void RenameSelected()
        {
            if (_selected == null) return;
            var dlg = new RenameDialog(P, _selected.Name);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                string newName = dlg.NewName.Trim();
                if (string.IsNullOrWhiteSpace(newName) || newName.Equals(_selected.Name, StringComparison.OrdinalIgnoreCase))
                    return;
                if (EnemyTemplateLibrary.Templates.Any(x => x != _selected && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("A template with that name already exists.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (EnemyTemplateLibrary.Rename(_selected, newName))
                {
                    SetSelected(_selected);
                    SetStatus("Renamed to \"" + newName + "\"", false);
                }
            }
        }

        private void TryRefreshEditor()
        {
            try
            {
                foreach (WinForms.Form f in WinForms.Application.OpenForms)
                {
                    MainForm mf = f as MainForm;
                    if (mf != null)
                    {
                        mf.BeginInvoke(new Action(() =>
                        {
                            try { mf.Invalidate(); } catch { }
                            try { mf.Refresh(); } catch { }
                        }));
                        break;
                    }
                }
            }
            catch { }
            try { EditorConsole.Log("Applied template \"" + _selected.Name + "\""); } catch { }
        }

        private void SetStatus(string text, bool isError)
        {
            if (statusText == null) return;
            statusText.Text = text ?? "";
            statusText.Foreground = isError ? new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x5A)) : P.BSub;
            // fade out after 4s
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            string snap = statusText.Text;
            t.Tick += (s, e) => { t.Stop(); if (statusText.Text == snap) statusText.Text = ""; };
            t.Start();
        }

        // ================================================================
        // KEYBOARD
        // ================================================================

        private void OnPreviewKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_combos.Any(c => c.Pop != null && c.Pop.IsOpen)) { CloseAllPopups(); e.Handled = true; return; }
                Close(); e.Handled = true;
            }
            else if (e.Key == Key.Enter && _selected != null && txtSearch != null && !txtSearch.IsKeyboardFocused)
            {
                BtnApply_Click(null, null); e.Handled = true;
            }
            else if (e.Key == Key.Delete && _selected != null)
            {
                BtnDelete_Click(null, null); e.Handled = true;
            }
            else if (e.Key == Key.F2 && _selected != null)
            {
                RenameSelected(); e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                EnemyTemplateLibrary.Reload(); e.Handled = true;
            }
        }

        // ================================================================
        // THEME LIVE (mirror OptionsForm)
        // ================================================================

        public void Retheme()
        {
            if (retheming) return;
            retheming = true;
            try
            {
                string keepSearch = txtSearch?.Text;
                int keepCat = cmbCategory?.SelectedIndex ?? 0;
                var keepSel = _selected?.Name;

                P.UpdateColors();
                Background = P.BWindow;

                // stash
                _combos.Clear();
                Content = null;
                BuildUI();
                if (txtSearch != null && keepSearch != null) txtSearch.Text = keepSearch;
                RefreshCategoryCombo(false);
                if (cmbCategory != null) cmbCategory.SelectedIndex = Math.Min(keepCat, cmbCategory.Items.Length - 1);
                _activeCategory = cmbCategory.Items[cmbCategory.SelectedIndex].ToString();
                RefreshList();
                if (!string.IsNullOrEmpty(keepSel))
                {
                    var found = EnemyTemplateLibrary.Templates.FirstOrDefault(t => t.Name.Equals(keepSel, StringComparison.OrdinalIgnoreCase));
                    if (found != null) _selected = found;
                }
                UpdateDetail();
            }
            catch { }
            retheming = false;
        }

        // ================================================================
        // FACTORIES (mirrored from OptionsForm, trimmed for this window)
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
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5
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
            Trigger dis = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            dis.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55) { TargetName = "bd" });
            tpl.Triggers.Add(dis);
            b.Template = tpl;
            return b;
        }

        private Button MakeMiniIconButton(string text, double size)
        {
            Button b = new Button
            {
                Content = text,
                Width = size, Height = size,
                Cursor = Cursors.Hand,
                Foreground = P.BSub,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Padding = new Thickness(0)
            };
            var bf = new FrameworkElementFactory(typeof(Border), "bd");
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            bf.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            var tpl = new ControlTemplate(typeof(Button)) { VisualTree = bf };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, P.BHoverSurface) { TargetName = "bd" });
            over.Setters.Add(new Setter(Control.ForegroundProperty, P.BText));
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
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
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
                    new Setter(FrameworkElement.HeightProperty, 30.0),
                    new Setter(Control.FontSizeProperty, 12.0)
                }
            };
        }

        private sealed class ComboTag { public TextBlock Name; public TextBlock Check; }

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
                get { if (SelectedIndex >= 0 && SelectedIndex < Items.Length) return Items[SelectedIndex]; return null; }
            }

            public void Refresh()
            {
                if (LabelText != null)
                    LabelText.Text = SelectedItem != null && Display != null ? Display(SelectedItem) : "—";
                if (ListPanel != null)
                {
                    for (int i = 0; i < ListPanel.Children.Count; i++)
                    {
                        Border box = (Border)ListPanel.Children[i];
                        ComboTag tag = (ComboTag)box.Tag;
                        bool selNow = i == SelectedIndex;
                        tag.Check.Visibility = selNow ? Visibility.Visible : Visibility.Collapsed;
                        box.Background = selNow ? P.BRadioSel : Brushes.Transparent;
                        if (Display != null && i < Items.Length) tag.Name.Text = Display(Items[i]);
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
                TextBlock itemName = new TextBlock { Text = display(c.Items[i]), Foreground = P.BText, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
                itemContent.Children.Add(itemName);
                TextBlock itemCheck = new TextBlock { Text = "\u2713", Foreground = P.BAccent, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
                itemContent.Children.Add(itemCheck);
                Border item = new Border { Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(9, 5, 9, 6), Child = itemContent, Cursor = Cursors.Hand };
                item.MouseEnter += (s, e) => { if (idx != c.SelectedIndex) item.Background = P.BHoverSurface; };
                item.MouseLeave += (s, e) => { if (idx != c.SelectedIndex) item.Background = Brushes.Transparent; };
                item.MouseLeftButtonUp += (s, e) =>
                {
                    c.SelectedIndex = idx;
                    c.Refresh();
                    if (c.Pop != null) c.Pop.IsOpen = false;
                    c.SelectionChanged?.Invoke();
                };
                ComboTag tag = new ComboTag { Name = itemName, Check = itemCheck };
                item.Tag = tag;
                c.ListPanel.Children.Add(item);
            }
            c.Refresh();
        }

        private DarkCombo MakeCombo(double width)
        {
            DarkCombo c = new DarkCombo(); c.P = P;
            c.Box = new Border
            {
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 0, 9, 0),
                Cursor = Cursors.Hand,
                Height = 30
            };
            if (!double.IsNaN(width)) { c.Box.Width = width; c.Box.MinWidth = width; }
            Grid comboGrid = new Grid();
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            c.LabelText = new TextBlock { Foreground = P.BText, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(c.LabelText, 0); comboGrid.Children.Add(c.LabelText);
            TextBlock arrow = new TextBlock { Text = "\u25BE", Foreground = P.BSub, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(arrow, 1); comboGrid.Children.Add(arrow);
            c.Box.Child = comboGrid;

            c.Box.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (c.Pop == null) return;
                foreach (DarkCombo other in _combos) if (other != c && other.Pop != null) other.Pop.IsOpen = false;
                if (c.Pop.IsOpen) c.Pop.IsOpen = false;
                else { c.Refresh(); c.Pop.IsOpen = true; }
            };

            c.ListPanel = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = c.ListPanel };
            scroll.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
            Border drop = new Border { Background = P.BSurface, BorderBrush = P.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(3), Child = scroll };
            if (!double.IsNaN(width)) drop.MinWidth = width;
            c.Pop = new Popup { PlacementTarget = c.Box, Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade, Child = drop };
            _combos.Add(c);
            return c;
        }

        private void CloseAllPopups()
        {
            foreach (var cc in _combos) if (cc.Pop != null) cc.Pop.IsOpen = false;
        }

        private void ClosePopupsIfOutside(object sender, MouseButtonEventArgs e)
        {
            foreach (DarkCombo cc in _combos)
            {
                if (cc.Pop == null || !cc.Pop.IsOpen) continue;
                DependencyObject d = e.OriginalSource as DependencyObject;
                bool inside = false;
                while (d != null)
                {
                    if (d == cc.Box || d == cc.Pop.Child) { inside = true; break; }
                    d = VisualTreeHelper.GetParent(d);
                }
                if (!inside) cc.Pop.IsOpen = false;
            }
        }

        // ================================================================
        // DIALOGS (save / rename / options) — same shell as OptionsForm
        // ================================================================

        internal class TemplateSaveDialog : Window
        {
            public string TemplateName { get; private set; }
            public string TemplateCategory { get; private set; }
            public string TemplateDescription { get; private set; }
            public List<string> TemplateTags { get; private set; }
            public ApplyOptions ApplyOptions { get; private set; }

            private TextBox txtName;
            private DarkCombo cmbCat;
            private TextBox txtDesc;
            private TextBox txtTags;
            private readonly List<DarkCombo> combos = new List<DarkCombo>();

            public TemplateSaveDialog(UiTheme.Palette p, string enemyName, string enemyId, ushort rawId, string resolvedName)
            {
                Width = 420; Height = 360; MinWidth = 380; MinHeight = 300;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                Background = p.BWindow; Foreground = p.BText;
                FontFamily = new FontFamily("Segoe UI");
                WindowStyle = WindowStyle.None;
                var ch = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, ch);
                UseLayoutRounding = true;
                TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

                ApplyOptions = ApplyOptions.CreateDefault();
                TemplateTags = new List<string>();

                Grid root = new Grid { Background = p.BWindow };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // header
                Border hdr = new Border { Height = 34, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 0, 0, 1) };
                hdr.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };
                Grid hg = new Grid();
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel ht = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                ht.Children.Add(new TextBlock { Text = "Save Template", Foreground = p.BText, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                TextBlock sub = new TextBlock { Text = "   " + enemyName + "  " + enemyId, Foreground = p.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                ht.Children.Add(sub);
                Grid.SetColumn(ht, 0); hg.Children.Add(ht);
                Button xb = new Button { Content = "\u2715", Width = 32, Height = 28, Foreground = p.BSub, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 11 };
                xb.Click += (s, e) => { DialogResult = false; };
                Grid.SetColumn(xb, 1); hg.Children.Add(xb);
                hdr.Child = hg;
                Grid.SetRow(hdr, 0); root.Children.Add(hdr);

                ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14, 10, 14, 8) };
                sv.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
                StackPanel sp = new StackPanel();
                sv.Content = sp;

                // name
                sp.Children.Add(Label("Name", p));
                txtName = TB(p, resolvedName + " preset");
                sp.Children.Add(txtName);
                // category
                sp.Children.Add(Label("Category", p, 8));
                // fake combo using DarkCombo inline
                var catCombo = BuildCatCombo(p);
                cmbCat = catCombo;
                sp.Children.Add(catCombo.Box);
                // description
                sp.Children.Add(Label("Description (optional)", p, 8));
                txtDesc = new TextBox { MinHeight = 56, MaxHeight = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = p.BInput, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), Padding = new Thickness(7, 5, 7, 5), FontSize = 11.5 };
                txtDesc.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
                sp.Children.Add(txtDesc);
                // tags
                sp.Children.Add(Label("Tags (comma separated)", p, 8));
                txtTags = TB(p, "");
                txtTags.ToolTip = "e.g. village, strong, castle";
                sp.Children.Add(txtTags);

                Grid.SetRow(sv, 1); root.Children.Add(sv);

                // footer
                Border ft = new Border { Height = 42, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(12, 0, 12, 0) };
                Grid fg = new Grid();
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Button cancel = MakeBtnStatic(p, "Cancel", false, 88, 26, 4);
                cancel.Click += (s, e) => { DialogResult = false; };
                Grid.SetColumn(cancel, 1); fg.Children.Add(cancel);
                Button ok = MakeBtnStatic(p, "Save", true, 88, 26, 4);
                ok.Margin = new Thickness(6, 0, 0, 0);
                ok.Click += (s, e) =>
                {
                    string n = txtName.Text?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(n)) { MessageBox.Show("Name is required.", "Save Template", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    if (n.Length > 64) { MessageBox.Show("Name too long (max 64).", "Save Template", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    if (EnemyTemplateLibrary.Templates.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("A template with that name already exists.", "Save Template", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    TemplateName = n;
                    TemplateCategory = cmbCat.SelectedItem?.ToString() ?? "Village";
                    TemplateDescription = txtDesc.Text ?? "";
                    TemplateTags = (txtTags.Text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s2 => s2.Trim()).Where(s2 => !string.IsNullOrWhiteSpace(s2)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    DialogResult = true;
                };
                Grid.SetColumn(ok, 2); fg.Children.Add(ok);
                ft.Child = fg;
                Grid.SetRow(ft, 2); root.Children.Add(ft);

                Content = root;
                Loaded += (s, e) => { txtName.Focus(); txtName.SelectAll(); };
                PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; } };
                Deactivated += (s, e) => { foreach (var c in combos) if (c.Pop != null) c.Pop.IsOpen = false; };
                PreviewMouseLeftButtonDown += (s, e) =>
                {
                    foreach (var c in combos)
                    {
                        if (c.Pop == null || !c.Pop.IsOpen) continue;
                        var d = e.OriginalSource as DependencyObject; bool inside = false;
                        while (d != null) { if (d == c.Box || d == c.Pop.Child) { inside = true; break; } d = VisualTreeHelper.GetParent(d); }
                        if (!inside) c.Pop.IsOpen = false;
                    }
                };
            }

            private static TextBlock Label(string t, UiTheme.Palette p, double top = 0) { return new TextBlock { Text = t, Foreground = p.BSub, FontSize = 11, Margin = new Thickness(0, top, 0, 4), FontWeight = FontWeights.SemiBold }; }
            private static TextBox TB(UiTheme.Palette p, string txt)
            {
                var tb = new TextBox { Text = txt, Height = 28, Background = p.BInput, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), Padding = new Thickness(7, 0, 7, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
                return tb;
            }
            private DarkCombo BuildCatCombo(UiTheme.Palette p)
            {
                DarkCombo c = new DarkCombo(); c.P = p;
                c.Box = new Border { Background = p.BInput, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 0, 9, 0), Cursor = Cursors.Hand, Height = 28 };
                Grid gg = new Grid();
                gg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                c.LabelText = new TextBlock { Foreground = p.BText, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(c.LabelText, 0); gg.Children.Add(c.LabelText);
                var ar = new TextBlock { Text = "\u25BE", Foreground = p.BSub, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                Grid.SetColumn(ar, 1); gg.Children.Add(ar);
                c.Box.Child = gg;
                c.Box.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    foreach (var other in combos) if (other != c && other.Pop != null) other.Pop.IsOpen = false;
                    if (c.Pop.IsOpen) c.Pop.IsOpen = false; else { c.Refresh(); c.Pop.IsOpen = true; }
                };
                c.ListPanel = new StackPanel();
                var sv = new ScrollViewer { MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = c.ListPanel };
                sv.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
                var drop = new Border { Background = p.BSurface, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(3), Child = sv, MinWidth = 180 };
                c.Pop = new Popup { PlacementTarget = c.Box, Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade, Child = drop };
                combos.Add(c);
                // populate
                var cats = EnemyTemplateLibrary.Categories.ToArray();
                object[] items = cats.Cast<object>().ToArray();
                c.Items = items;
                c.Display = x => x.ToString();
                c.SelectedIndex = 0;
                // quick populate
                for (int i = 0; i < items.Length; i++)
                {
                    int idx = i;
                    var itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                    var nm = new TextBlock { Text = cats[i], Foreground = p.BText, FontSize = 11.5 };
                    var chk = new TextBlock { Text = "\u2713", Foreground = p.BAccent, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 0, 0, 0), Visibility = Visibility.Collapsed };
                    itemContent.Children.Add(nm); itemContent.Children.Add(chk);
                    var item = new Border { Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(9, 5, 9, 6), Child = itemContent, Cursor = Cursors.Hand };
                    item.MouseEnter += (s, e) => { if (idx != c.SelectedIndex) item.Background = p.BHoverSurface; };
                    item.MouseLeave += (s, e) => { if (idx != c.SelectedIndex) item.Background = Brushes.Transparent; };
                    item.MouseLeftButtonUp += (s, e) => { c.SelectedIndex = idx; c.Refresh(); c.Pop.IsOpen = false; };
                    var tag = new ComboTag { Name = nm, Check = chk };
                    item.Tag = tag;
                    c.ListPanel.Children.Add(item);
                }
                c.Refresh();
                return c;
            }

            private static Button MakeBtnStatic(UiTheme.Palette p, string text, bool primary, double w, double h, double radius)
            {
                var b = new Button { Content = text, Width = w, Height = h, Cursor = Cursors.Hand, Focusable = false, Foreground = primary ? Brushes.White : p.BText, FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
                var bf = new FrameworkElementFactory(typeof(Border), "bd");
                bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
                bf.SetValue(Border.BackgroundProperty, primary ? p.BAccent : p.BSurface);
                bf.SetValue(Border.BorderBrushProperty, primary ? Brushes.Transparent : p.BBorder);
                bf.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                bf.SetValue(Border.PaddingProperty, new Thickness(8, 0, 8, 0));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.ContentSourceProperty, "Content");
                cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                bf.AppendChild(cp);
                var tpl = new ControlTemplate(typeof(Button)) { VisualTree = bf };
                var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                over.Setters.Add(new Setter(Border.BackgroundProperty, primary ? p.BAccentHover : p.BHoverSurface) { TargetName = "bd" });
                tpl.Triggers.Add(over);
                var pr = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pr.Setters.Add(new Setter(Border.BackgroundProperty, primary ? p.BAccent : p.BPressSurface) { TargetName = "bd" });
                tpl.Triggers.Add(pr);
                b.Template = tpl;
                return b;
            }
        }

        internal class RenameDialog : Window
        {
            public string NewName { get; private set; }
            private TextBox tb;
            public RenameDialog(UiTheme.Palette p, string current)
            {
                Width = 360; Height = 160; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
                Background = p.BWindow; Foreground = p.BText; FontFamily = new FontFamily("Segoe UI");
                WindowStyle = WindowStyle.None;
                var ch = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, ch);

                Grid root = new Grid { Background = p.BWindow };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border hdr = new Border { Height = 34, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 0, 0, 1) };
                hdr.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };
                TextBlock ht = new TextBlock { Text = "  Rename Template", Foreground = p.BText, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                hdr.Child = ht;
                Grid.SetRow(hdr, 0); root.Children.Add(hdr);

                StackPanel sp = new StackPanel { Margin = new Thickness(14, 12, 14, 8) };
                sp.Children.Add(new TextBlock { Text = "New name:", Foreground = p.BSub, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                tb = new TextBox { Text = current, Height = 28, Background = p.BInput, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), Padding = new Thickness(7, 0, 7, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
                sp.Children.Add(tb);
                Grid.SetRow(sp, 1); root.Children.Add(sp);

                Border ft = new Border { Height = 40, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(12, 0, 12, 0) };
                Grid fg = new Grid();
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cancel = new Button { Content = "Cancel", Width = 80, Height = 26, Background = p.BSurface, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FontSize = 11 };
                cancel.Click += (s, e) => { DialogResult = false; };
                Grid.SetColumn(cancel, 1); fg.Children.Add(cancel);
                var ok = new Button { Content = "Rename", Width = 80, Height = 26, Background = p.BAccent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(6, 0, 0, 0), FontSize = 11, FontWeight = FontWeights.SemiBold };
                ok.Click += (s, e) => { string n = tb.Text?.Trim() ?? ""; if (string.IsNullOrWhiteSpace(n)) return; NewName = n; DialogResult = true; };
                Grid.SetColumn(ok, 2); fg.Children.Add(ok);
                // template-ize buttons for hover
                foreach (Button bb in new[] { cancel, ok })
                {
                    var isPrimary = bb == ok;
                    var bf = new FrameworkElementFactory(typeof(Border), "bd");
                    bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                    bf.SetValue(Border.BackgroundProperty, isPrimary ? p.BAccent : p.BSurface);
                    bf.SetValue(Border.BorderBrushProperty, isPrimary ? Brushes.Transparent : p.BBorder);
                    bf.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                    var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                    cp.SetValue(ContentPresenter.ContentSourceProperty, "Content");
                    cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                    bf.AppendChild(cp);
                    var tpl = new ControlTemplate(typeof(Button)) { VisualTree = bf };
                    var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                    over.Setters.Add(new Setter(Border.BackgroundProperty, isPrimary ? p.BAccentHover : p.BHoverSurface) { TargetName = "bd" });
                    tpl.Triggers.Add(over);
                    bb.Template = tpl;
                }
                ft.Child = fg;
                Grid.SetRow(ft, 2); root.Children.Add(ft);
                Content = root;
                Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
            }
        }

        internal class TemplateOptionsDialog : Window
        {
            public ApplyOptions Result { get; private set; }
            private ApplyOptions _apply;
            private UiTheme.Palette P;

            public TemplateOptionsDialog(UiTheme.Palette p, ApplyOptions apply)
            {
                P = p; _apply = apply;
                Width = 360; Height = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
                Background = p.BWindow; Foreground = p.BText; FontFamily = new FontFamily("Segoe UI");
                WindowStyle = WindowStyle.None;
                var ch = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, ch);
                UseLayoutRounding = true;

                Grid root = new Grid { Background = p.BWindow };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border hdr = new Border { Height = 34, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 0, 0, 1) };
                hdr.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch { } };
                Grid hg = new Grid();
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock ht = new TextBlock { Text = "  Apply Options", Foreground = p.BText, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                Grid.SetColumn(ht, 0); hg.Children.Add(ht);
                Button xb = new Button { Content = "\u2715", Width = 32, Height = 28, Foreground = p.BSub, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
                xb.Click += (s, e) => { DialogResult = false; };
                Grid.SetColumn(xb, 1); hg.Children.Add(xb);
                hdr.Child = hg;
                Grid.SetRow(hdr, 0); root.Children.Add(hdr);

                ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14, 10, 14, 8) };
                sv.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
                StackPanel sp = new StackPanel();
                sv.Content = sp;

                TextBlock intro = new TextBlock { Text = "Choose which groups are copied when the template is applied. Leave position/rotation off to keep the target where it is.", Foreground = p.BSub, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
                sp.Children.Add(intro);

                var checks = new List<CheckBox>();
                Func<string, string, bool, CheckBox> mk = (title, hint, val) =>
                {
                    Grid g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    CheckBox cb = new CheckBox { IsChecked = val, VerticalAlignment = VerticalAlignment.Center, Foreground = p.BText };
                    // style checkbox accent?
                    Grid.SetColumn(cb, 0); g.Children.Add(cb);
                    StackPanel tt = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    tt.Children.Add(new TextBlock { Text = title, Foreground = p.BText, FontSize = 11.5, FontWeight = FontWeights.SemiBold });
                    tt.Children.Add(new TextBlock { Text = hint, Foreground = p.BSub, FontSize = 10.5 });
                    Grid.SetColumn(tt, 1); g.Children.Add(tt);
                    sp.Children.Add(g);
                    checks.Add(cb);
                    return cb;
                };
                var c0 = mk("Enable", "Byte 0x00", _apply.Enable);
                var c1 = mk("Enemy ID", "Bytes 0x01-0x02", _apply.EnemyId);
                var c2 = mk("Life", "Bytes 0x08-0x09", _apply.Life);
                var c3 = mk("Body bytes", "03-07 + 0A-0B", _apply.UnknownBody);
                var c4 = mk("Position", "0x0C-0x11 — overwrites placement", _apply.Position);
                var c5 = mk("Rotation", "0x12-0x17 — overwrites facing", _apply.Rotation);
                var c6 = mk("Room ID", "Bytes 0x18-0x19", _apply.RoomId);
                var c7 = mk("Tail bytes", "0x1A-0x1F", _apply.UnknownTail);

                Grid.SetRow(sv, 1); root.Children.Add(sv);

                Border ft = new Border { Height = 42, Background = p.BBar, BorderBrush = p.BBorderSoft, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(12, 0, 12, 0) };
                Grid fg = new Grid();
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Button cancel = MakeBtnStatic(p, "Cancel", false, 84, 26, 4);
                cancel.Click += (s, e) => { DialogResult = false; };
                Grid.SetColumn(cancel, 1); fg.Children.Add(cancel);
                Button ok = MakeBtnStatic(p, "Save", true, 84, 26, 4);
                ok.Margin = new Thickness(6, 0, 0, 0);
                ok.Click += (s, e) =>
                {
                    _apply.Enable = c0.IsChecked == true;
                    _apply.EnemyId = c1.IsChecked == true;
                    _apply.Life = c2.IsChecked == true;
                    _apply.UnknownBody = c3.IsChecked == true;
                    _apply.Position = c4.IsChecked == true;
                    _apply.Rotation = c5.IsChecked == true;
                    _apply.RoomId = c6.IsChecked == true;
                    _apply.UnknownTail = c7.IsChecked == true;
                    Result = _apply;
                    DialogResult = true;
                };
                Grid.SetColumn(ok, 2); fg.Children.Add(ok);
                ft.Child = fg;
                Grid.SetRow(ft, 2); root.Children.Add(ft);

                Content = root;
            }

            private static Button MakeBtnStatic(UiTheme.Palette p, string text, bool primary, double w, double h, double radius)
            {
                var b = new Button { Content = text, Width = w, Height = h, Cursor = Cursors.Hand, Focusable = false, Foreground = primary ? Brushes.White : p.BText, FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
                var bf = new FrameworkElementFactory(typeof(Border), "bd");
                bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
                bf.SetValue(Border.BackgroundProperty, primary ? p.BAccent : p.BSurface);
                bf.SetValue(Border.BorderBrushProperty, primary ? Brushes.Transparent : p.BBorder);
                bf.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.ContentSourceProperty, "Content");
                cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                bf.AppendChild(cp);
                var tpl = new ControlTemplate(typeof(Button)) { VisualTree = bf };
                var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                over.Setters.Add(new Setter(Border.BackgroundProperty, primary ? p.BAccentHover : p.BHoverSurface) { TargetName = "bd" });
                tpl.Triggers.Add(over);
                var pr = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pr.Setters.Add(new Setter(Border.BackgroundProperty, primary ? p.BAccent : p.BPressSurface) { TargetName = "bd" });
                tpl.Triggers.Add(pr);
                b.Template = tpl;
                return b;
            }
        }
    }
}
