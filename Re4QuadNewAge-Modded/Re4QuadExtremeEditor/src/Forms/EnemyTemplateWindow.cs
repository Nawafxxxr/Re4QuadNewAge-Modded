using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Re4QuadExtremeEditor.src.Class.EnemyTemplates;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src.Forms
{
    public class EnemyTemplateWindow : Window
    {
        private UiTheme.Palette P;
        private ListBox list;
        private TextBox txtSearch;
        private ComboBox cmbFilter;
        private List<EnemyTemplate> _filtered;
        private EnemyTemplate _selected;
        private string _activeFilter = "All";
        private bool _loading = false;

        public EnemyTemplateWindow()
        {
            P = UiTheme.CreatePalette();

            WindowStyle = WindowStyle.None;
            Topmost = true;
            Width = 420;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = P.BWindow;
            Foreground = P.BText;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            BuildUI();
            RebuildFilter();
        }

        private void BuildUI()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // title
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // search
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons

            // == Title bar ==
            Grid bar = new Grid { Height = 32, Background = P.BBar };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            Grid.SetRow(bar, 0);
            TextBlock ttl = new TextBlock { Text = "   Enemy Templates", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = P.BText };
            Grid.SetColumn(ttl, 0);
            bar.Children.Add(ttl);
            Button xb = new Button { Content = "\u2715", Width = 32, Height = 32, FontSize = 14, Foreground = P.BText, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            xb.Click += (s, e) => Close();
            xb.MouseEnter += (s, e) => xb.Background = P.BPressSurface;
            xb.MouseLeave += (s, e) => xb.Background = Brushes.Transparent;
            Grid.SetColumn(xb, 1);
            bar.Children.Add(xb);
            root.Children.Add(bar);

            // == Search row ==
            Grid searchRow = new Grid { Margin = new Thickness(10, 8, 10, 6) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(searchRow, 1);
            txtSearch = new TextBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = P.BInput,
                Foreground = P.BText,
                BorderBrush = P.BBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 0, 6, 0)
            };
            txtSearch.TextChanged += (s, e) => RefreshList();
            Grid.SetColumn(txtSearch, 0);
            searchRow.Children.Add(txtSearch);
            cmbFilter = new ComboBox { Height = 28, MinWidth = 90, Margin = new Thickness(6, 0, 0, 0), Background = P.BInput, Foreground = P.BText, BorderBrush = P.BBorder, BorderThickness = new Thickness(1) };
            cmbFilter.SelectionChanged += (s, e) => { if (_loading) return; _activeFilter = cmbFilter.SelectedItem?.ToString() ?? "All"; RefreshList(); };
            Grid.SetColumn(cmbFilter, 1);
            searchRow.Children.Add(cmbFilter);
            root.Children.Add(searchRow);

            // == List ==
            list = new ListBox
            {
                BorderThickness = new Thickness(1),
                BorderBrush = P.BBorder,
                Background = P.BSurface,
                Foreground = P.BText,
                Padding = new Thickness(4),
                Margin = new Thickness(10, 0, 10, 6)
            };
            list.SelectionChanged += (s, e) =>
            {
                _selected = (list.SelectedItem as ListBoxItem)?.Tag as EnemyTemplate;
            };
            Grid.SetRow(list, 2);
            root.Children.Add(list);

            // == Buttons ==
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 10), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(row, 3);
            row.Children.Add(Btn("Save from Enemy", BtnSave_Click, false));
            row.Children.Add(Btn("Apply Options", BtnOptions_Click, false));
            row.Children.Add(Btn("Apply", BtnApply_Click, true));
            row.Children.Add(Btn("Delete", BtnDelete_Click, false));
            root.Children.Add(row);

            Content = root;
        }

        private void RebuildFilter()
        {
            _loading = true;
            cmbFilter.Items.Clear();
            cmbFilter.Items.Add("All");
            foreach (string c in EnemyTemplateLibrary.Categories)
                if (!cmbFilter.Items.Contains(c)) cmbFilter.Items.Add(c);
            cmbFilter.SelectedIndex = 0;
            _activeFilter = "All";
            _loading = false;
            RefreshList();
        }

        private void RefreshList()
        {
            string q = txtSearch != null ? txtSearch.Text : "";
            _filtered = EnemyTemplateLibrary.Search(_activeFilter, q);
            list.Items.Clear();
            foreach (var t in _filtered)
            {
                list.Items.Add(new ListBoxItem
                {
                    Content = t.Name,
                    Tag = t,
                    Padding = new Thickness(6, 5, 6, 5),
                    Background = P.BSurface,
                    Foreground = P.BText,
                    BorderBrush = P.BBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }
        }

        private Button Btn(string text, RoutedEventHandler click, bool accent)
        {
            Button b = new Button
            {
                Content = text,
                Height = 30,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(12, 0, 12, 0),
                Background = accent ? new SolidColorBrush(P.MAccent) : P.BSurface,
                Foreground = accent ? Brushes.White : P.BText,
                BorderThickness = accent ? new Thickness(0) : new Thickness(1),
                BorderBrush = P.BBorder,
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            b.Click += click;
            return b;
        }

        // == Save template from selected enemy ==
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Object3D obj = DataBase.LastSelectNode as Object3D;
            if (obj == null || obj.Group != GroupType.ESL)
            {
                MessageBox.Show("Select an Enemy in the scene first (ESL).");
                return;
            }
            if (DataBase.FileESL == null || !DataBase.FileESL.Lines.ContainsKey(obj.ObjLineRef)) return;
            ushort eid = obj.ObjLineRef;
            string ename = "Unknown";
            if (DataBase.EnemiesIDs != null && DataBase.EnemiesIDs.List.ContainsKey(eid))
                ename = DataBase.EnemiesIDs.List[eid].Name;

            SaveDialog dlg = new SaveDialog(P, ename, "0x" + eid.ToString("X4"));
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                var t = EnemyTemplate.FromEnemy(eid, ename, DataBase.FileESL.Lines[eid]);
                t.Name = dlg.TemplateName;
                t.Category = dlg.TemplateCategory;
                EnemyTemplateLibrary.Save(t);
                RebuildFilter();
            }
        }

        // == Apply options (selective) ==
        private void BtnOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { MessageBox.Show("Select a template first."); return; }
            OptionsDialog dlg = new OptionsDialog(P, _selected.Apply);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        // == Apply to selected enemy ==
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { MessageBox.Show("Select a template first."); return; }
            Object3D obj = DataBase.LastSelectNode as Object3D;
            if (obj == null || obj.Group != GroupType.ESL)
            {
                MessageBox.Show("Select an Enemy in the scene first (ESL).");
                return;
            }
            if (DataBase.FileESL == null || !DataBase.FileESL.Lines.ContainsKey(obj.ObjLineRef)) return;
            _selected.ApplyToTarget(obj.ObjLineRef);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { MessageBox.Show("Select a template first."); return; }
            EnemyTemplateLibrary.Delete(_selected);
            _selected = null;
            RefreshList();
        }

        // == Simple name + category dialog ==
        internal class SaveDialog : Window
        {
            public string TemplateName { get; private set; }
            public string TemplateCategory { get; private set; }
            private TextBox txtName;
            private ComboBox cmbCat;

            public SaveDialog(UiTheme.Palette p, string enemyName, string enemyId)
            {
                Topmost = true;
                Width = 320;
                Height = 220;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                Background = p.BWindow;
                Foreground = p.BText;
                FontFamily = new FontFamily("Segoe UI");

                Grid root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                StackPanel sp = new StackPanel { Margin = new Thickness(14, 12, 14, 0) };
                sp.Children.Add(new TextBlock { Text = "Save Template", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 8), Foreground = p.BText });
                sp.Children.Add(new TextBlock { Text = enemyName + " (" + enemyId + ")", Foreground = p.BSub, Margin = new Thickness(0, 0, 0, 10) });
                sp.Children.Add(L("Name:", p));
                txtName = new TextBox { Text = "New Template", Height = 26, Background = p.BInput, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1), Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center };
                sp.Children.Add(txtName);
                sp.Children.Add(L("Category:", p));
                cmbCat = new ComboBox { Height = 26, Background = p.BInput, Foreground = p.BText, BorderBrush = p.BBorder, BorderThickness = new Thickness(1) };
                foreach (string c in EnemyTemplateLibrary.Categories) cmbCat.Items.Add(c);
                if (cmbCat.Items.Count == 0) cmbCat.Items.Add("Village");
                cmbCat.SelectedIndex = 0;
                sp.Children.Add(cmbCat);
                Grid.SetRow(sp, 0);
                root.Children.Add(sp);

                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 14, 10) };
                Button ok = new Button { Content = "Save", Height = 28, MinWidth = 70, Background = p.BAccent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0), FontSize = 11 };
                ok.Click += (s, ev) => { TemplateName = txtName.Text.Trim(); TemplateCategory = (string)cmbCat.SelectedItem; DialogResult = true; };
                row.Children.Add(ok);
                Button cancel = new Button { Content = "Cancel", Height = 28, MinWidth = 70, Background = p.BSurface, Foreground = p.BText, BorderThickness = new Thickness(1), BorderBrush = p.BBorder, Cursor = Cursors.Hand, FontSize = 11 };
                cancel.Click += (s, ev) => { DialogResult = false; };
                row.Children.Add(cancel);
                Grid.SetRow(row, 1);
                root.Children.Add(row);
                Content = root;
            }
            private static TextBlock L(string t, UiTheme.Palette p) { return new TextBlock { Text = t, Foreground = p.BSub, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) }; }
        }

        // == Apply options dialog ==
        internal class OptionsDialog : Window
        {
            private CheckBox[] checks;
            private int _idx = 0;
            private ApplyOptions _apply;

            public OptionsDialog(UiTheme.Palette p, ApplyOptions apply)
            {
                _apply = apply;
                checks = new CheckBox[8];
                Topmost = true;
                Width = 280;
                Height = 360;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                Background = p.BWindow;
                Foreground = p.BText;
                FontFamily = new FontFamily("Segoe UI");

                Grid root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                StackPanel sp = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };
                sp.Children.Add(new TextBlock { Text = "Fields to Apply", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = p.BText });
                sp.Children.Add(Ck(p, "Enable", apply.Enable));
                sp.Children.Add(Ck(p, "Enemy ID", apply.EnemyId));
                sp.Children.Add(Ck(p, "Life", apply.Life));
                sp.Children.Add(Ck(p, "Unknown bytes", apply.UnknownBody));
                sp.Children.Add(Ck(p, "Position (override)", apply.Position));
                sp.Children.Add(Ck(p, "Rotation (override)", apply.Rotation));
                sp.Children.Add(Ck(p, "Room ID (override)", apply.RoomId));
                sp.Children.Add(Ck(p, "Unknown tail", apply.UnknownTail));
                Grid.SetRow(sp, 0);
                root.Children.Add(sp);

                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 14, 10) };
                Button ok = new Button { Content = "OK", Height = 28, MinWidth = 70, Background = p.BAccent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0), FontSize = 11 };
                ok.Click += (s, ev) =>
                {
                    _apply.Enable = checks[0].IsChecked == true;
                    _apply.EnemyId = checks[1].IsChecked == true;
                    _apply.Life = checks[2].IsChecked == true;
                    _apply.UnknownBody = checks[3].IsChecked == true;
                    _apply.Position = checks[4].IsChecked == true;
                    _apply.Rotation = checks[5].IsChecked == true;
                    _apply.RoomId = checks[6].IsChecked == true;
                    _apply.UnknownTail = checks[7].IsChecked == true;
                    DialogResult = true;
                };
                row.Children.Add(ok);
                Grid.SetRow(row, 1);
                root.Children.Add(row);
                Content = root;
            }

            private CheckBox Ck(UiTheme.Palette p, string text, bool val)
            {
                var c = new CheckBox { Content = text, IsChecked = val, Foreground = p.BText, Margin = new Thickness(0, 3, 0, 3) };
                if (_idx < checks.Length) checks[_idx++] = c;
                return c;
            }
        }
    }
}
