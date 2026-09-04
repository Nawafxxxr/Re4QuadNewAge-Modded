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
using Newtonsoft.Json.Linq;
using OpenTK;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.ElementLibrary;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Class.Enums;
using WinForms = System.Windows.Forms;

namespace Re4QuadExtremeEditor.src.Forms
{
    /// <summary>
    /// Element Library window. Lets the user save any element from the current
    /// file into a reusable library, then re-insert it (with editable coordinates)
    /// into any other room. Built to mirror EnemyTemplateWindow: same palette,
    /// same WindowChrome, same card language.
    /// </summary>
    public class ElementLibraryWindow : Window
    {
        private UiTheme.Palette P;
        private bool retheming;

        // state
        private List<ElementLibraryEntry> _filtered = new List<ElementLibraryEntry>();
        private ElementLibraryEntry _selected;
        private string _activeCategory = "All";
        private string _activeType = "All Types";
        private string _search = "";
        private bool _buildingDetail;

        // header / filter
        private TextBox txtSearch;
        private DarkCombo cmbCategory;
        private DarkCombo cmbType;
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

        // detail editable fields
        private TextBox txtName;
        private TextBox txtCategory;
        private TextBox txtRe4Version;
        private TextBox txtDescription;
        private TextBox txtPosX;
        private TextBox txtPosY;
        private TextBox txtPosZ;
        private CheckBox chkOverridePos;
        private TextBlock txtTypeDetail;

        // footer actions
        private Button btnImport;
        private Button btnExport;
        private Button btnDuplicate;
        private Button btnDelete;
        private Button btnRename;
        private Button btnSaveSelected;
        private Button btnInsert;

        private readonly List<DarkCombo> _combos = new List<DarkCombo>();

        public ElementLibraryWindow()
        {
            P = UiTheme.CreatePalette();
            Title = "Element Library";
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

            BuildUI();
            RefreshCategoryCombo();
            RefreshTypeCombo();
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
            ElementLibrary.LibraryChanged += OnLibraryChanged;
            Closed += (s, e) => { try { ElementLibrary.LibraryChanged -= OnLibraryChanged; } catch { } };
        }

        private void OnLibraryChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshCategoryCombo(false);
                RefreshTypeCombo(false);
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
            TextBlock t1 = new TextBlock { Text = "Element Library", Foreground = P.BText, FontSize = 12.8, FontWeight = FontWeights.SemiBold, LineHeight = 14 };
            TextBlock t2 = new TextBlock { Text = "Reusable elements  \u00B7  RE4 Quad", Foreground = P.BSub, FontSize = 10.2, Margin = new Thickness(0, 1, 0, 0) };
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
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Style tbStyle = TextBoxStyle();
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
                CaretBrush = P.BText,
                Height = 30
            };
            txtSearch.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            ToolTipService.SetToolTip(txtSearch, "Search by name, type, category, description or tags");
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
            Grid.SetColumn(searchBorder, 0);
            g.Children.Add(searchBorder);

            // Category combo
            cmbCategory = MakeCombo(140);
            cmbCategory.Box.ToolTip = "Filter by category";
            cmbCategory.SelectionChanged = () =>
            {
                _activeCategory = cmbCategory.SelectedIndex >= 0 && cmbCategory.SelectedIndex < cmbCategory.Items.Length
                    ? cmbCategory.Items[cmbCategory.SelectedIndex]?.ToString() ?? "All"
                    : "All";
                if (string.IsNullOrEmpty(_activeCategory)) _activeCategory = "All";
                RefreshList();
            };
            cmbCategory.Box.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cmbCategory.Box, 2);
            g.Children.Add(cmbCategory.Box);

            // Type combo
            cmbType = MakeCombo(150);
            cmbType.Box.ToolTip = "Filter by element type";
            cmbType.SelectionChanged = () =>
            {
                _activeType = cmbType.SelectedIndex >= 0 && cmbType.SelectedIndex < cmbType.Items.Length
                    ? cmbType.Items[cmbType.SelectedIndex]?.ToString() ?? "All Types"
                    : "All Types";
                if (string.IsNullOrEmpty(_activeType)) _activeType = "All Types";
                RefreshList();
            };
            cmbType.Box.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cmbType.Box, 4);
            g.Children.Add(cmbType.Box);

            // count
            countText = new TextBlock { Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = "0 elements" };
            Grid.SetColumn(countText, 6);
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
            TextBlock lhTitle = new TextBlock { Text = "ELEMENTS", Foreground = P.BSub, FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
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
            TextBlock empT = new TextBlock { Text = "No elements found", Foreground = P.BText, FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock empSub = new TextBlock { Text = "Try another search or filter.", Foreground = P.BSub, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 220, TextAlignment = TextAlignment.Center };
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

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            statusText = new TextBlock { Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = "" };
            left.Children.Add(statusText);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

            btnImport = MakeButton("Import", false, double.NaN, 26, 4);
            btnImport.ToolTip = "Import .json element(s)";
            btnImport.Padding = new Thickness(10, 0, 10, 0);
            btnImport.Margin = new Thickness(4, 0, 0, 0);
            btnImport.Click += BtnImport_Click;
            right.Children.Add(btnImport);

            btnExport = MakeButton("Export", false, double.NaN, 26, 4);
            btnExport.ToolTip = "Export selected element";
            btnExport.Padding = new Thickness(10, 0, 10, 0);
            btnExport.Margin = new Thickness(4, 0, 0, 0);
            btnExport.Click += BtnExport_Click;
            right.Children.Add(btnExport);

            btnSaveSelected = MakeButton("Save Selected", false, double.NaN, 26, 4);
            btnSaveSelected.ToolTip = "Capture the selected elements in the scene";
            btnSaveSelected.Padding = new Thickness(10, 0, 10, 0);
            btnSaveSelected.Margin = new Thickness(4, 0, 0, 0);
            btnSaveSelected.Click += BtnSaveSelected_Click;
            right.Children.Add(btnSaveSelected);

            btnDuplicate = MakeButton("Duplicate", false, double.NaN, 26, 4);
            btnDuplicate.ToolTip = "Clone selected element";
            btnDuplicate.Padding = new Thickness(10, 0, 10, 0);
            btnDuplicate.Margin = new Thickness(4, 0, 0, 0);
            btnDuplicate.Click += BtnDuplicate_Click;
            right.Children.Add(btnDuplicate);

            btnRename = MakeButton("Rename", false, double.NaN, 26, 4);
            btnRename.ToolTip = "Rename selected element (F2)";
            btnRename.Padding = new Thickness(10, 0, 10, 0);
            btnRename.Margin = new Thickness(4, 0, 0, 0);
            btnRename.Click += BtnRename_Click;
            right.Children.Add(btnRename);

            btnDelete = MakeButton("Delete", false, double.NaN, 26, 4);
            btnDelete.ToolTip = "Delete selected element (Del)";
            btnDelete.Padding = new Thickness(10, 0, 10, 0);
            btnDelete.Margin = new Thickness(4, 0, 0, 0);
            btnDelete.Click += BtnDelete_Click;
            right.Children.Add(btnDelete);

            btnInsert = MakeButton("Insert", true, double.NaN, 28, 4);
            btnInsert.ToolTip = "Insert selected element into the current file (Enter)";
            btnInsert.Padding = new Thickness(14, 0, 14, 0);
            btnInsert.Margin = new Thickness(8, 0, 0, 0);
            btnInsert.FontWeight = FontWeights.SemiBold;
            btnInsert.Click += BtnInsert_Click;
            right.Children.Add(btnInsert);

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
            cats.AddRange(ElementLibrary.Categories);
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
                countText.Text = ElementLibrary.Entries.Count + " elements";
        }

        private void RefreshTypeCombo(bool keepSelection = true)
        {
            string keep = _activeType;
            cmbType.Items = new object[0];
            cmbType.ListPanel.Children.Clear();
            var types = new List<string> { "All Types" };
            types.AddRange(ElementLibrary.Entries.Select(x => x.GroupType).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s));
            object[] items = types.Cast<object>().ToArray();
            PopulateCombo(cmbType, items, x => x.ToString());
            int idx = 0;
            if (keepSelection && !string.IsNullOrEmpty(keep))
            {
                idx = Array.FindIndex(items, x => x.ToString().Equals(keep, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) idx = 0;
            }
            cmbType.SelectedIndex = idx;
            _activeType = items[idx].ToString();
            cmbType.Refresh();
        }

        private void RefreshList()
        {
            if (listPanel == null) return;

            GroupType? typeFilter = null;
            GroupType tg;
            if (!string.IsNullOrEmpty(_activeType) && !_activeType.Equals("All Types") && Enum.TryParse(_activeType, out tg))
                typeFilter = tg;

            _filtered = ElementLibrary.Search(_activeCategory, _search, typeFilter);
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
                if (string.IsNullOrWhiteSpace(_search) && _activeCategory == "All" && _activeType == "All Types")
                    countText.Text = _filtered.Count + " elements";
                else
                    countText.Text = _filtered.Count + " / " + ElementLibrary.Entries.Count;
            }

            bool selStillVisible = _selected != null && _filtered.Any(x => x.Id == _selected.Id);

            for (int i = 0; i < _filtered.Count; i++)
            {
                var t = _filtered[i];
                bool isSel = _selected != null && _selected.Id == t.Id;
                Border card = MakeElementCard(t, isSel);
                listPanel.Children.Add(card);
            }

            if (!selStillVisible && _filtered.Count > 0 && _selected != null)
            {
            }

            UpdateFooterAvailability();
        }

        private Border MakeElementCard(ElementLibraryEntry t, bool isSelected)
        {
            Border b = new Border
            {
                Background = isSelected ? P.BRadioSel : Brushes.Transparent,
                BorderBrush = isSelected ? P.BAccent : P.BBorderSoft,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            b.MouseEnter += (s, e) => { if (!isSelected) b.Background = P.BHoverSurface; };
            b.MouseLeave += (s, e) => { if (!isSelected) b.Background = Brushes.Transparent; };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            string typeLabel;
            try { typeLabel = ElementLibraryService.GroupLabel(t.GetGroupType()); }
            catch { typeLabel = t.GroupType ?? "NULL"; }
            TextBlock idTx = new TextBlock { Text = typeLabel, Foreground = P.BSub, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 };
            idPill.Child = idTx;
            mid.Children.Add(idPill);

            TextBlock typeTx = new TextBlock { Text = t.GroupType ?? "", Foreground = P.BSub, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = new FontFamily("Consolas"), MaxWidth = 90 };
            mid.Children.Add(typeTx);

            Grid.SetRow(mid, 1);
            g.Children.Add(mid);

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
            b.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { SetSelected(t); BtnInsert_Click(null, null); }
            };
            b.MouseRightButtonUp += (s, e) => { SetSelected(t); ShowCardContext(t, b); e.Handled = true; };

            return b;
        }

        private void ShowCardContext(ElementLibraryEntry t, Border anchor)
        {
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

        private void SetSelected(ElementLibraryEntry t)
        {
            _selected = t;
            RefreshList();
            UpdateDetail();
            UpdateFooterAvailability();
        }

        private void UpdateFooterAvailability()
        {
            bool hasSel = _selected != null;
            if (btnInsert != null) { btnInsert.IsEnabled = hasSel; btnInsert.Opacity = hasSel ? 1 : 0.55; }
            if (btnDelete != null) { btnDelete.IsEnabled = hasSel; btnDelete.Opacity = hasSel ? 1 : 0.55; }
            if (btnDuplicate != null) { btnDuplicate.IsEnabled = hasSel; btnDuplicate.Opacity = hasSel ? 1 : 0.55; }
            if (btnExport != null) { btnExport.IsEnabled = hasSel; btnExport.Opacity = hasSel ? 1 : 0.55; }
            if (btnRename != null) { btnRename.IsEnabled = hasSel; btnRename.Opacity = hasSel ? 1 : 0.55; }
        }

        private void UpdateDetail()
        {
            if (detailStack == null) return;
            if (_buildingDetail) return;
            _buildingDetail = true;
            try
            {
                detailStack.Children.Clear();
                txtName = null; txtCategory = null; txtDescription = null;
                txtRe4Version = null; txtTypeDetail = null;
                txtPosX = null; txtPosY = null; txtPosZ = null; chkOverridePos = null;

            if (_selected == null)
            {
                StackPanel ph = new StackPanel { Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                Border icWrap = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Background = P.BHoverSurface, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
                TextBlock ic = new TextBlock { Text = "\u2726", Foreground = P.BSub, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                icWrap.Child = ic;
                ph.Children.Add(icWrap);
                ph.Children.Add(new TextBlock { Text = "Select an element", Foreground = P.BText, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
                ph.Children.Add(new TextBlock { Text = "Choose a saved element on the left to edit it or insert it into the current file.", Foreground = P.BSub, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 300, Margin = new Thickness(0, 6, 0, 0) });
                StackPanel hints = new StackPanel { Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                hints.Children.Add(HintRow("Double-click", "insert instantly"));
                hints.Children.Add(HintRow("Right-click", "duplicate / rename / export"));
                hints.Children.Add(HintRow("Enter", "insert  •  Del — delete"));
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
            Border typeBadge = new Border { Background = P.BAccent, CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(0, 0, 6, 0) };
            string typeLabelD;
            try { typeLabelD = ElementLibraryService.GroupLabel(t.GetGroupType()); }
            catch { typeLabelD = t.GroupType ?? "NULL"; }
            typeBadge.Child = new TextBlock { Text = typeLabelD, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold };
            sub.Children.Add(typeBadge);
            Border rawBadge = new Border { Background = P.BSurface, BorderBrush = P.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 3), Margin = new Thickness(0, 0, 6, 0) };
            rawBadge.Child = new TextBlock { Text = t.GroupType ?? "", Foreground = P.BSub, FontSize = 11, FontFamily = new FontFamily("Consolas") };
            sub.Children.Add(rawBadge);
            Border catBadge = new Border { Background = P.BRadioSel, BorderBrush = P.BBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 3) };
            catBadge.Child = new TextBlock { Text = t.GetCategorySafe(), Foreground = P.BAccent, FontSize = 11, FontWeight = FontWeights.SemiBold };
            sub.Children.Add(catBadge);
            titleLeft.Children.Add(sub);
            Grid.SetColumn(titleLeft, 0);
            titleGrid.Children.Add(titleLeft);

            TextBlock rightMeta = new TextBlock { Text = "Created " + t.CreatedAt.ToString("yyyy-MM-dd"), Foreground = P.BSub, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(rightMeta, 1);
            titleGrid.Children.Add(rightMeta);

            detailStack.Children.Add(titleGrid);

            detailStack.Children.Add(new Border { Height = 1, Background = P.BBorderSoft, Margin = new Thickness(0, 12, 0, 10) });

            // Editable fields
            Section(detailStack, "DETAILS");
            Grid f0 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            f0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
            f0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            f0.Children.Add(FieldLabel("Name", 0));
            txtName = DetailTextBox(t.Name);
            txtName.TextChanged += (s, e) => { if (_selected != null) { _selected.Name = txtName.Text ?? ""; } };
            Grid.SetColumn(txtName, 1);
            f0.Children.Add(txtName);
            detailStack.Children.Add(f0);

            detailStack.Children.Add(DetailRowLabeled("Type", t.GroupType ?? "", ref txtTypeDetail));

            Grid f2 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            f2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
            f2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            f2.Children.Add(FieldLabel("Category", 0));
            txtCategory = DetailTextBox(t.GetCategorySafe());
            txtCategory.TextChanged += (s, e) => { if (_selected != null) _selected.Category = txtCategory.Text ?? ""; };
            Grid.SetColumn(txtCategory, 1);
            f2.Children.Add(txtCategory);
            detailStack.Children.Add(f2);

            Grid f3 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            f3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
            f3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            f3.Children.Add(FieldLabel("Re4 version", 0));
            txtRe4Version = DetailTextBox(t.Re4Version ?? "");
            txtRe4Version.TextChanged += (s, e) => { if (_selected != null) _selected.Re4Version = txtRe4Version.Text ?? ""; };
            Grid.SetColumn(txtRe4Version, 1);
            f3.Children.Add(txtRe4Version);
            detailStack.Children.Add(f3);

            Grid f4 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            f4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
            f4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            f4.Children.Add(FieldLabel("Desc", 0));
            txtDescription = new TextBox
            {
                Text = t.Description ?? "",
                MinHeight = 56,
                MaxHeight = 80,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = P.BInput,
                Foreground = P.BText,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 5, 7, 5),
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            txtDescription.Resources[typeof(ScrollBar)] = UiTheme.ScrollBarStyle();
            txtDescription.TextChanged += (s, e) => { if (_selected != null) _selected.Description = txtDescription.Text ?? ""; };
            Grid.SetColumn(txtDescription, 1);
            f4.Children.Add(txtDescription);
            detailStack.Children.Add(f4);

            detailStack.Children.Add(new Border { Height = 1, Background = P.BBorderSoft, Margin = new Thickness(0, 12, 0, 10) });

            // Stored position
            Section(detailStack, "STORE POSITION");
            OpenTK.Vector3 storedPos = ReadStoredPos(t);
            Grid posGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(posGrid, 0); posGrid.Margin = new Thickness(0, 6, 0, 0);

            txtPosX = PositionBox("X", storedPos.X);
            txtPosY = PositionBox("Y", storedPos.Y);
            txtPosZ = PositionBox("Z", storedPos.Z);
            Grid.SetColumn(txtPosX, 0);
            Grid.SetColumn(txtPosY, 2);
            Grid.SetColumn(txtPosZ, 4);
            posGrid.Children.Add(txtPosX);
            posGrid.Children.Add(txtPosY);
            posGrid.Children.Add(txtPosZ);
            detailStack.Children.Add(posGrid);

            chkOverridePos = new CheckBox { Content = "Override position on insert", Foreground = P.BText, FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0) };
            chkOverridePos.Foreground = P.BText;
            chkOverridePos.Checked += (s, e) => UpdatePosEditable();
            chkOverridePos.Unchecked += (s, e) => UpdatePosEditable();
            detailStack.Children.Add(chkOverridePos);

            TextBlock posHint = new TextBlock { Text = "Enable to force X/Y/Z when inserting, instead of the stored position.", Foreground = P.BSub, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            detailStack.Children.Add(posHint);
            UpdatePosEditable();

            TextBlock meta = new TextBlock
            {
                Text = string.Format("Created {0:yyyy-MM-dd HH:mm}  \u00B7  Updated {1:yyyy-MM-dd HH:mm}", t.CreatedAt, t.UpdatedAt),
                Foreground = P.BSub, FontSize = 10, Margin = new Thickness(0, 10, 0, 0), Opacity = 0.85
            };
            detailStack.Children.Add(meta);
            }
            finally { _buildingDetail = false; }
        }

        private void UpdatePosEditable()
        {
            bool en = chkOverridePos != null && chkOverridePos.IsChecked == true;
            if (txtPosX != null) { txtPosX.IsEnabled = en; }
            if (txtPosY != null) { txtPosY.IsEnabled = en; }
            if (txtPosZ != null) { txtPosZ.IsEnabled = en; }
        }

        protected Border FieldLabel(string text, int top)
        {
            return new Border
            {
                Child = new TextBlock { Text = text, Foreground = P.BSub, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }
            };
        }

        protected TextBox DetailTextBox(string initial)
        {
            var tb = new TextBox
            {
                Text = initial,
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = P.BInput,
                Foreground = P.BText,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 0, 7, 0),
                FontSize = 11.5
            };
            return tb;
        }

        protected FrameworkElement DetailRowLabeled(string label, string value, ref TextBlock dest)
        {
            Grid g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border lbl = FieldLabel(label, 0);
            Grid.SetColumn(lbl, 0);
            g.Children.Add(lbl);
            dest = new TextBlock { Text = value, Foreground = P.BText, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas"), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(dest, 1);
            g.Children.Add(dest);
            return g;
        }

        protected TextBox PositionBox(string prefix, float value)
        {
            var tb = new TextBox
            {
                Text = value.ToString("0.###"),
                Background = P.BInput,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = P.BText,
                CaretBrush = P.BText,
                FontSize = 11.5,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Height = 28,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            tb.ToolTip = prefix + " coordinate";
            return tb;
        }

        private void Section(StackPanel sp, string title)
        {
            TextBlock tb = new TextBlock { Text = title, Foreground = P.BAccent, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(tb);
        }

        private FrameworkElement HintRow(string k, string v)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock a = new TextBlock { Text = k, Foreground = P.BAccent, FontSize = 10.5, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Consolas") };
            TextBlock b = new TextBlock { Text = "  " + v, Foreground = P.BSub, FontSize = 10.5 };
            sp.Children.Add(a); sp.Children.Add(b);
            return sp;
        }

        private void TouchSelected()
        {
            if (_selected == null) return;
            _selected.UpdatedAt = DateTime.Now;
            try { ElementLibrary.Save(_selected); } catch { }
        }

        private void SetStatus(string text, bool isError)
        {
            if (statusText == null) return;
            statusText.Text = text ?? "";
            statusText.Foreground = isError ? new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x5A)) : P.BSub;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            string snap = statusText.Text;
            t.Tick += (s, e) => { t.Stop(); if (statusText.Text == snap) statusText.Text = ""; };
            t.Start();
        }

        // ================================================================
        // ACTIONS
        // ================================================================

        private void BtnSaveSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var nodes = new List<Object3D>();
                var sel = Re4QuadExtremeEditor.src.DataBase.SelectedNodes;
                if (sel != null)
                {
                    foreach (var kv in sel)
                        if (kv.Value is Object3D) nodes.Add((Object3D)kv.Value);
                }
                nodes = nodes.Where(n => ElementLibraryService.IsStorable(n.Group)).OrderBy(n => n.Index).ToList();
                if (nodes.Count == 0)
                {
                    SetStatus("Nothing storable selected. Select an element in the room tree first.", true);
                    return;
                }

                int ok = 0;
                string lastName = "";
                foreach (var node in nodes)
                {
                    var entry = new ElementLibraryEntry();
                    if (!ElementLibraryService.Capture(entry, node)) continue;
                    if (string.IsNullOrWhiteSpace(entry.Description))
                        entry.Description = "Captured from room element";
                    entry.Category = "Custom";
                    string baseN = NiceName(node);
                    entry.Name = ElementLibrary.ContainsName(baseN) ? ElementLibrary.NextCloneName(baseN) : baseN;
                    try { ElementLibrary.Save(entry); } catch (ArgumentException) { continue; }
                    ok++;
                    lastName = entry.Name;
                }
                SetStatus(ok > 0 ? (ok == 1 ? "Saved 1 element: \"" + lastName + "\"" : "Saved " + ok + " elements. Last: \"" + lastName + "\"") : "Could not capture selection.", ok == 0);
                RefreshList();
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, true);
            }
        }

        private string NiceName(Object3D node)
        {
            try
            {
                string alt = node.AltText;
                if (!string.IsNullOrWhiteSpace(alt) && alt != "Error Text")
                    return alt.Trim();
            }
            catch { }
            string baseName = node.Group + " Element";
            try
            {
                string g = ElementLibraryService.GroupLabel(node.Group);
                if (!string.IsNullOrWhiteSpace(g) && g != node.Group.ToString())
                    baseName = g;
            }
            catch { }
            return baseName;
        }

        private void CommitDetailEdits()
        {
            if (_selected == null) return;
            try { ElementLibrary.Save(_selected); } catch { }
        }

        private void BtnInsert_Click(object sender, RoutedEventArgs e)
        {
            var entry = _selected;
            if (entry == null)
            {
                SetStatus("Select a saved element first.", true);
                return;
            }
            OpenTK.Vector3? overridePos = null;
            if (chkOverridePos != null && chkOverridePos.IsChecked == true)
            {
                float x = ParseFloat(txtPosX != null ? txtPosX.Text : "");
                float y = ParseFloat(txtPosY != null ? txtPosY.Text : "");
                float z = ParseFloat(txtPosZ != null ? txtPosZ.Text : "");
                overridePos = new OpenTK.Vector3(x, y, z);
            }
            try
            {
                var created = ElementLibraryService.Insert(entry, overridePos);
                if (created.Count > 0)
                {
                    SetStatus("Inserted '" + entry.Name + "' into the scene.", false);
                    TryRefreshEditor();
                }
                else
                {
                    SetStatus("Insert failed: open the matching room/file for this type first.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Insert failed: " + ex.Message, true);
            }
        }

        private static float ParseFloat(string s)
        {
            float r;
            if (float.TryParse(s?.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r))
                return r;
            return 0f;
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { SetStatus("Select an element first.", true); return; }
            var c = ElementLibrary.Duplicate(_selected);
            if (c != null) { SetSelected(c); SetStatus("Duplicated as \"" + c.Name + "\"", false); }
            else SetStatus("Duplicate failed.", true);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { SetStatus("Select an element first.", true); return; }
            string name = _selected.Name;
            var res = MessageBox.Show("Delete element \"" + name + "\"?\nThis cannot be undone.", "Delete Element", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            ElementLibrary.Delete(_selected);
            _selected = null;
            RefreshList();
            UpdateDetail();
            SetStatus("Deleted \"" + name + "\"", false);
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
                if (ElementLibrary.ContainsName(newName))
                {
                    MessageBox.Show("An element with that name already exists.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (ElementLibrary.Rename(_selected, newName))
                {
                    SetSelected(_selected);
                    SetStatus("Renamed to \"" + newName + "\"", false);
                }
            }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            RenameSelected();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { SetStatus("Select an element first.", true); return; }
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export element",
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = ElementLibrary.SafeName(_selected.Name) + ".json",
                DefaultExt = "json"
            };
            bool? ok = sfd.ShowDialog(this);
            if (ok == true)
            {
                if (ElementLibrary.Export(_selected, sfd.FileName))
                    SetStatus("Exported to " + sfd.FileName, false);
                else
                    SetStatus("Export failed.", true);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import element(s)",
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = true
            };
            bool? ok = ofd.ShowDialog(this);
            if (ok == true)
            {
                int imported = 0;
                foreach (string f in ofd.FileNames)
                {
                    var t = ElementLibrary.Import(f);
                    if (t != null) imported++;
                }
                if (imported > 0)
                {
                    RefreshCategoryCombo();
                    RefreshTypeCombo();
                    RefreshList();
                    SetStatus(imported == 1 ? "Imported 1 element." : "Imported " + imported + " elements.", false);
                }
                else SetStatus("Nothing imported.", true);
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
            try { EditorConsole.Log("Inserted \"" + _selected?.Name + "\""); } catch { }
        }

        private OpenTK.Vector3 ReadStoredPos(ElementLibraryEntry e)
        {
            try
            {
                var d = e.Data;
                if (d == null) return OpenTK.Vector3.Zero;
                var po = d["pos"] as JObject;
                if (po != null)
                {
                    return new OpenTK.Vector3(
                        po.Value<float?>("x") ?? 0f,
                        po.Value<float?>("y") ?? 0f,
                        po.Value<float?>("z") ?? 0f);
                }
                return new OpenTK.Vector3(
                    d.Value<float?>("posX") ?? 0f,
                    d.Value<float?>("posY") ?? 0f,
                    d.Value<float?>("posZ") ?? 0f);
            }
            catch { return OpenTK.Vector3.Zero; }
        }

        // ================================================================
        // KEYBOARD
        // ================================================================

        private void OnPreviewKey(object sender, KeyEventArgs e)
        {
            bool searchFocused = txtSearch != null && txtSearch.IsKeyboardFocused;
            bool inTextBox = e.OriginalSource is TextBox;
            if (e.Key == Key.Escape)
            {
                if (_combos.Any(c => c.Pop != null && c.Pop.IsOpen)) { CloseAllPopups(); e.Handled = true; return; }
                Close(); e.Handled = true;
            }
            else if (e.Key == Key.Enter && _selected != null && !searchFocused && !inTextBox)
            {
                CommitDetailEdits();
                BtnInsert_Click(null, null); e.Handled = true;
            }
            else if (e.Key == Key.Delete && _selected != null && !inTextBox)
            {
                BtnDelete_Click(null, null); e.Handled = true;
            }
            else if (e.Key == Key.F2 && _selected != null)
            {
                RenameSelected(); e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                ElementLibrary.Reload(); e.Handled = true;
            }
        }

        // ================================================================
        // THEME LIVE
        // ================================================================

        public void Retheme()
        {
            if (retheming) return;
            retheming = true;
            try
            {
                string keepSearch = txtSearch?.Text;
                int keepCat = cmbCategory?.SelectedIndex ?? 0;
                var keepSel = _selected?.Id;

                P.UpdateColors();
                Background = P.BWindow;

                _combos.Clear();
                Content = null;
                BuildUI();
                if (txtSearch != null && keepSearch != null) txtSearch.Text = keepSearch;
                RefreshCategoryCombo(false);
                if (cmbCategory != null) cmbCategory.SelectedIndex = Math.Min(keepCat, cmbCategory.Items.Length - 1);
                _activeCategory = cmbCategory.Items[cmbCategory.SelectedIndex].ToString();
                RefreshTypeCombo(false);
                RefreshList();
                if (!string.IsNullOrEmpty(keepSel))
                {
                    var found = ElementLibrary.Entries.FirstOrDefault(x => x.Id == keepSel);
                    if (found != null) _selected = found;
                }
                UpdateDetail();
            }
            catch { }
            retheming = false;
        }

        // ================================================================
        // FACTORIES (mirrored from EnemyTemplateWindow)
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
        // DIALOGS
        // ================================================================

        internal class RenameDialog : Window
        {
            public string NewName { get; private set; }
            private TextBox tb;
            private UiTheme.Palette P;

            public RenameDialog(UiTheme.Palette p, string current)
            {
                P = p;
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
                TextBlock ht = new TextBlock { Text = "  Rename Element", Foreground = p.BText, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
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
    }
}