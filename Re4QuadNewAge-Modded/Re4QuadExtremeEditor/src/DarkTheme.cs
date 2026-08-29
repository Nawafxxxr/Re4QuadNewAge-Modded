using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Re4QuadExtremeEditor
{
    /// <summary>
    /// Centralized dark theme for every WinForms window in RE4 Quad Extreme Editor.
    /// It intentionally uses a soft charcoal palette rather than pure black/white.
    /// </summary>
    internal static class DarkTheme
    {
        // Per-HWND set of native controls already opted into Dark Mode. Unlike a
        // process-wide one-shot flag, this lets lazily-created scrollbars, the
        // PropertyGrid drop-down list, and secondary forms be themed whenever
        // their handle first shows up, without re-applying to the same HWND on
        // every Apply pass.
        private static readonly HashSet<IntPtr> _nativeThemedHandles = new HashSet<IntPtr>();

        // Per-form guard that stops EnsureDarkNativeHandles from force-creating
        // handles for controls that were already scanned during an earlier Apply.
        private static readonly HashSet<Form> _darkEnsuredForms = new HashSet<Form>();

        // Palette is served by UiTheme so Dark Mode and Light Mode are exact
        // mirrors of each other; these members keep the historical DarkTheme API.
        public static Color Window        { get { return UiTheme.WinWindow; } }
        public static Color Surface       { get { return UiTheme.WinSurface; } }
        public static Color Surface2      { get { return UiTheme.WinSurface2; } }
        public static Color Surface3      { get { return UiTheme.WinSurface3; } }
        public static Color Input         { get { return UiTheme.WinInput; } }
        public static Color Border        { get { return UiTheme.WinBorder; } }
        public static Color BorderSoft    { get { return UiTheme.WinBorderSoft; } }
        public static Color Text          { get { return UiTheme.WinText; } }
        public static Color TextSecondary { get { return UiTheme.WinTextSecondary; } }
        public static Color Disabled      { get { return UiTheme.WinDisabled; } }
        public static Color Accent        { get { return UiTheme.WinAccent; } }
        public static Color AccentHover   { get { return UiTheme.WinAccentHover; } }
        public static Color AccentPressed { get { return UiTheme.WinAccentPressed; } }
        public static Color Selection     { get { return UiTheme.WinSelection; } }
        public static Color MenuHover     { get { return UiTheme.WinMenuHover; } }
        public static Color SelectionText { get { return UiTheme.WinSelectionText; } }

        /// <summary>True when the light mirror of the dark palette is active.</summary>
        public static bool IsLightMode
        {
            get { return UiTheme.IsLight; }
        }

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed) return;

            form.SuspendLayout();
            try
            {
                SetPreferredDarkMode(!UiTheme.IsLight);
                form.BackColor = Window;
                form.ForeColor = Text;
                form.HandleCreated -= ThemeForm_HandleCreated;
                form.HandleCreated += ThemeForm_HandleCreated;
                form.Shown -= ThemeForm_Shown;
                form.Shown += ThemeForm_Shown;
                if (form.IsHandleCreated)
                    ApplyWindowChrome(form);

                if (UiTheme.IsLight)
                    ToolStripManager.Renderer = new ToolStripProfessionalRenderer();
                else
                    ToolStripManager.Renderer = DarkRenderer;
                ApplyControl(form);

                if (!UiTheme.IsLight)
                {
                    if (_darkEnsuredForms.Add(form))
                    {
                        form.Disposed -= DarkEnsuredForm_Disposed;
                        form.Disposed += DarkEnsuredForm_Disposed;
                        EnsureDarkNativeHandles(form);
                    }
                }
                else
                {
                    _darkEnsuredForms.Clear();
                    _nativeThemedHandles.Clear();
                }

                form.Invalidate(true);
            }
            finally
            {
                form.ResumeLayout(true);
            }
        }

        /// <summary>
        /// Themes a control subtree that was created after Apply(Form) ran
        /// (e.g. dynamically built tabs), without touching window chrome.
        /// </summary>
        public static void ApplyToControl(Control c)
        {
            if (c == null || c.IsDisposed) return;
            SetPreferredDarkMode(!UiTheme.IsLight);
            if (UiTheme.IsLight)
                ToolStripManager.Renderer = new ToolStripProfessionalRenderer();
            else
                ToolStripManager.Renderer = DarkRenderer;
            ApplyControl(c);
            EnsureDarkNativeHandles(c);
            c.Invalidate(true);
        }

        /// <summary>
        /// Re-scans native child HWNDs after the UI has finished constructing.
        /// WinForms common controls may create their ScrollBar child lazily, so a
        /// second pass is needed to catch that HWND and apply the dark theme.
        /// This method is called only by the dark-mode shell.
        /// </summary>
        internal static void RefreshNativeScrollbars(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            try
            {
                if (root.IsHandleCreated &&
                    (root is PropertyGrid || root is TreeView || root is ListBox ||
                     root is CheckedListBox || root is ComboBox || root is TextBoxBase))
                {
                    ApplyDarkNativeTheme(root);
                }

                foreach (Control child in root.Controls)
                    RefreshNativeScrollbars(child);
            }
            catch { }
        }

        private static void NativeHandleDestroyed(object sender, EventArgs e)
        {
            if (sender is Control c && c.IsHandleCreated)
                _nativeThemedHandles.Remove(c.Handle);
        }

        private static void DarkEnsuredForm_Disposed(object sender, EventArgs e)
        {
            _darkEnsuredForms.Remove(sender as Form);
        }

        private static void ThemeForm_HandleCreated(object sender, EventArgs e)
        {
            var form = sender as Form;
            if (form == null || form.IsDisposed) return;

            ApplyWindowChrome(form);
            try { form.BeginInvoke(new Action(() => ApplyWindowChrome(form))); } catch { }
        }

        private static void ThemeForm_Shown(object sender, EventArgs e)
        {
            var form = sender as Form;
            if (form == null || form.IsDisposed) return;
            ApplyWindowChrome(form);
        }

        public static void ApplyWindowChrome(Form form)
        {
            try
            {
                if (!form.IsHandleCreated) return;
                // Dark Mode: dark caption buttons/text. Light Mode: standard light chrome.
                int dark = UiTheme.IsLight ? 0 : 1;
                DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));

                int caption = ToColorRef(UiTheme.WinTitleBarCaption);
                int border = ToColorRef(BorderSoft);
                int text = ToColorRef(Text);
                DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 34, ref border, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 36, ref text, sizeof(int));

                // Windows 11: rounded window corners (silently ignored on older systems).
                try
                {
                    int preference = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(form.Handle, 33, ref preference, sizeof(int));
                }
                catch { }
            }
            catch { /* DWM attributes are optional on older Windows. */ }
        }

        private static void ApplyControl(Control c)
        {
            if (c == null) return;

            // Keep the OpenGL viewport and image controls visually independent.
            bool isGL = c.GetType().FullName != null && c.GetType().FullName.IndexOf("GLControl", StringComparison.OrdinalIgnoreCase) >= 0;

            UpgradeControlFont(c);

            if (!isGL)
            {
                c.ForeColor = Text;
                if (c is Form || c is UserControl || c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel ||
                    c is SplitContainer || c is TabPage || c is GroupBox || c is StatusStrip || c is ToolStripContainer)
                    c.BackColor = Surface;
            }

            if (c is Re4QuadExtremeEditor.src.Controls.DarkGroupBox darkGroupBox)
            {
                darkGroupBox.SetDarkMode(!UiTheme.IsLight);
                darkGroupBox.BackColor = Surface;
                darkGroupBox.ForeColor = Text;
            }
            else if (c is Re4QuadExtremeEditor.src.Controls.DarkTabControl darkTabs)
            {
                darkTabs.SetDarkMode(!UiTheme.IsLight);
                darkTabs.BackColor = Window;
                darkTabs.ForeColor = Text;
            }
            else if (c is MenuStrip menu)
            {
                // Dark Mode only: disable the native Windows visual-style painter
                // for MenuStrip. On some WinForms/Windows combinations it draws a
                // bright focus/selection rectangle over File/Edit/View AFTER the
                // ProfessionalColorTable, which is why the item can look white even
                // though the renderer is configured correctly. Light Mode restores
                // the native visual style so no dark painter stays behind.
                if (UiTheme.IsLight)
                    RestoreVisualStyles(menu);
                else
                    DisableVisualStyles(menu);
                menu.BackColor = Surface;
                menu.ForeColor = Text;
                menu.Renderer = UiTheme.IsLight ? null : DarkRenderer;
                menu.ShowItemToolTips = true;
                ApplyToolStripItems(menu.Items);
            }
            else if (c is ContextMenuStrip contextMenu)
            {
                if (UiTheme.IsLight)
                    RestoreVisualStyles(contextMenu);
                else
                    DisableVisualStyles(contextMenu);
                contextMenu.BackColor = Surface;
                contextMenu.ForeColor = Text;
                contextMenu.Renderer = UiTheme.IsLight ? null : DarkRenderer;
                ApplyToolStripItems(contextMenu.Items);
            }
            else if (c is ToolStripDropDown dropDown)
            {
                if (UiTheme.IsLight)
                    RestoreVisualStyles(dropDown);
                else
                    DisableVisualStyles(dropDown);
                dropDown.BackColor = Surface;
                dropDown.ForeColor = Text;
                dropDown.Renderer = UiTheme.IsLight ? null : DarkRenderer;
                if (!UiTheme.IsLight)
                    ConfigureDarkDropDown(dropDown);
                ApplyToolStripItems(dropDown.Items);
            }
            else if (c is ToolStrip tool)
            {
                tool.BackColor = Surface;
                tool.ForeColor = Text;
                tool.Renderer = UiTheme.IsLight ? null : DarkRenderer;
                ApplyToolStripItems(tool.Items);
            }
            else if (c is StatusStrip status)
            {
                status.BackColor = Surface;
                status.ForeColor = Text;
                status.Renderer = UiTheme.IsLight ? null : DarkRenderer;
            }
            else if (c is TabControl tabs)
            {
                tabs.BackColor = Surface;
                tabs.ForeColor = Text;
                bool lightTabs = UiTheme.IsLight;
                tabs.DrawMode = lightTabs ? TabDrawMode.Normal : TabDrawMode.OwnerDrawFixed;
                tabs.Appearance = TabAppearance.Normal;
                tabs.SizeMode = lightTabs ? TabSizeMode.Normal : TabSizeMode.Fixed;
                tabs.DrawItem -= DrawDarkTab;
                if (!lightTabs)
                    tabs.DrawItem += DrawDarkTab;
                foreach (TabPage page in tabs.TabPages)
                {
                    page.UseVisualStyleBackColor = lightTabs;
                    page.BackColor = Window;
                    page.ForeColor = Text;
                }
            }
            else if (c is TextBoxBase textBox)
            {
                ApplyDarkNativeTheme(textBox);
                textBox.BackColor = Input;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ComboBox combo)
            {
                ApplyDarkNativeTheme(combo);
                combo.BackColor = Input;
                combo.ForeColor = Text;
                bool lightCombo = UiTheme.IsLight;
                combo.FlatStyle = lightCombo ? FlatStyle.Standard : FlatStyle.Flat;
                // Owner draw (Dark Mode only) removes the Windows-blue selected row
                // from combo drop-down lists while preserving ComboBox behavior.
                // Light Mode returns to the native drop-down so no dark row styling
                // stays behind after a live theme switch.
                combo.DrawMode = lightCombo ? DrawMode.Normal : DrawMode.OwnerDrawFixed;
                combo.DrawItem -= DrawDarkComboItem;
                if (!lightCombo)
                    combo.DrawItem += DrawDarkComboItem;
            }
            else if (c is ListBox list)
            {
                ApplyDarkNativeTheme(list);
                list.BackColor = Input;
                list.ForeColor = Text;
                list.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is CheckedListBox checkedList)
            {
                ApplyDarkNativeTheme(checkedList);
                checkedList.BackColor = Input;
                checkedList.ForeColor = Text;
                checkedList.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is TreeView tree)
            {
                ApplyDarkNativeTheme(tree);
                ApplyDarkNativeThemeRecursive(tree);
                tree.BackColor = Input;
                tree.ForeColor = Text;
                tree.LineColor = Border;

                // Dark Mode only: soften the selected-node highlight.
                // The designer/MainForm sets a bright light-blue selection color,
                // which clashes with the charcoal dark theme.
                if (tree is NsMultiselectTreeView.MultiselectTreeView multiTree)
                {
                    multiTree.UseThemedSelectedNodeBackColor = true;
                    multiTree.SelectedNodeBackColor = Selection;
                    multiTree.Invalidate();
                }
            }
            else if (c is DataGridView dataGrid)
            {
                dataGrid.BackgroundColor = Window;
                dataGrid.GridColor = BorderSoft;
                dataGrid.DefaultCellStyle.BackColor = Input;
                dataGrid.DefaultCellStyle.ForeColor = Text;
                dataGrid.DefaultCellStyle.SelectionBackColor = Selection;
                dataGrid.DefaultCellStyle.SelectionForeColor = SelectionText;
                dataGrid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
                dataGrid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                dataGrid.RowHeadersDefaultCellStyle.BackColor = Surface2;
                dataGrid.RowHeadersDefaultCellStyle.ForeColor = Text;
                dataGrid.EnableHeadersVisualStyles = false;
            }
            else if (c is PropertyGrid propGrid)
            {
                ApplyDarkNativeTheme(propGrid);
                // Keep the entire property grid on one continuous surface.
                // WinForms normally uses BackColor for the name column and
                // ViewBackColor for the value column, which creates two visibly
                // different panels in dark mode. Use the same charcoal input
                // surface for both columns.
                propGrid.BackColor = Input;
                propGrid.ForeColor = Text;
                propGrid.ViewBackColor = Input;
                propGrid.ViewForeColor = Text;
                propGrid.CategoryForeColor = Text;
                propGrid.DisabledItemForeColor = Text;
                propGrid.CommandsBackColor = Input;
                propGrid.CommandsForeColor = Text;
                propGrid.HelpBackColor = Input;
                propGrid.HelpForeColor = Text;
                propGrid.LineColor = Input;
                propGrid.ViewBorderColor = BorderSoft;
                propGrid.CategorySplitterColor = BorderSoft;
                propGrid.HelpBorderColor = BorderSoft;
                propGrid.CommandsBorderColor = BorderSoft;
                propGrid.SelectedItemWithFocusBackColor = Selection;
                propGrid.SelectedItemWithFocusForeColor = SelectionText;

                // Read-only PropertyGrid rows are drawn with the internal
                // PropertyGridView disabled-text color. Some .NET Framework
                // builds create these internal controls after the form theme
                // has already been applied, so hook the grid for dynamic children
                // and handle-created events as well. This affects Dark Mode only.
                ApplyPropertyGridInternalColors(propGrid);
                ApplyDarkNativeThemeRecursive(propGrid);
                HookDarkModeDynamicChildren(propGrid);
            }
            else if (c is GroupBox group)
            {
                group.BackColor = Surface;
                group.ForeColor = Text;
                group.FlatStyle = FlatStyle.Flat;
                if (UiTheme.IsLight)
                    RestoreVisualStyles(group);
                else
                    DisableVisualStyles(group);
            }
            else if (c is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.BackColor = Surface2;
                button.ForeColor = Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                // Hover uses the theme palette so it does not stay a dark charcoal
                // in Light Mode after a live switch.
                button.FlatAppearance.MouseOverBackColor = UiTheme.IsLight
                    ? UiTheme.WinSurface3
                    : Color.FromArgb(42, 47, 56);
                button.FlatAppearance.MouseDownBackColor = AccentPressed;
            }
            else if (c is CheckBox check)
            {
                check.UseVisualStyleBackColor = false;
                check.BackColor = Color.Transparent;
                check.ForeColor = Text;
                check.FlatStyle = FlatStyle.Standard;
            }
            else if (c is RadioButton radio)
            {
                radio.UseVisualStyleBackColor = false;
                radio.BackColor = Color.Transparent;
                radio.ForeColor = Text;
                radio.FlatStyle = FlatStyle.Standard;
            }
            else if (c is Label label)
            {
                // Labels are part of the application chrome. In dark mode always use
                // the theme text color so a designer/system black cannot disappear
                // against a charcoal surface. Links are handled separately below.
                label.ForeColor = Text;
                if (label.BackColor == SystemColors.Control || label.BackColor == Color.White)
                    label.BackColor = Color.Transparent;
            }
            else if (c is LinkLabel link)
            {
                link.BackColor = Color.Transparent;
                link.ForeColor = AccentHover;
                link.LinkColor = AccentHover;
                link.ActiveLinkColor = Color.FromArgb(104, 185, 255);
                link.VisitedLinkColor = Color.FromArgb(129, 150, 175);
            }
            else if (c is NumericUpDown numeric)
            {
                numeric.BackColor = Input;
                numeric.ForeColor = Text;
            }
            else if (c is TrackBar track)
            {
                track.BackColor = Surface;
                track.ForeColor = Text;
            }
            else if (c is PictureBox picture)
            {
                if (picture.BackColor == SystemColors.Control || picture.BackColor == Color.White)
                    picture.BackColor = Surface;
            }

            // Fix controls which the designer or a dynamic WinForms editor left on
            // the Windows light palette. This is deliberately inside ApplyControl,
            // which is called only while Dark Mode is being applied.
            if (!isGL && (c.BackColor == SystemColors.Control || c.BackColor == Color.White))
                c.BackColor = Surface;
            if (!isGL && (c.ForeColor == SystemColors.ControlText || c.ForeColor == Color.Black))
                c.ForeColor = Text;

            // Native/common controls can create their scrollbar handles lazily.
            // Re-apply the Windows dark common-control theme only to controls that
            // actually use native editors/scrollbars. This keeps buttons and other
            // custom-drawn controls unchanged.
            if (!isGL && c.IsHandleCreated &&
                (c is PropertyGrid || c is TreeView || c is ListBox ||
                 c is CheckedListBox || c is ComboBox || c is TextBoxBase))
            {
                ApplyDarkNativeTheme(c);
            }

            foreach (Control child in c.Controls)
                ApplyControl(child);
        }

        /// <summary>
        /// Replaces legacy Windows fonts with their modern equivalents while
        /// preserving size and weight. "Microsoft Sans Serif" (the WinForms
        /// default since 2002) becomes Segoe UI, and "Courier New" becomes
        /// Consolas. Applied only in dark mode so light mode stays untouched.
        /// </summary>
        private static void UpgradeControlFont(Control c)
        {
            try
            {
                if (c == null || c is Form) return;
                string name = c.Font?.Name;
                if (string.IsNullOrEmpty(name)) return;

                string target = null;
                if (name.Equals("Microsoft Sans Serif", StringComparison.OrdinalIgnoreCase))
                    target = "Segoe UI";
                else if (name.Equals("Courier New", StringComparison.OrdinalIgnoreCase))
                    target = "Consolas";

                if (target != null)
                    c.Font = new Font(target, c.Font.Size, c.Font.Style, c.Font.Unit, c.Font.GdiCharSet);
            }
            catch { }
        }

        private static readonly DarkToolStripRenderer DarkRenderer = new DarkToolStripRenderer();

        private static void ConfigureDarkDropDown(ToolStripDropDown dropDown)
        {
            dropDown.BackColor = Surface;
            dropDown.ForeColor = Text;
            dropDown.Renderer = DarkRenderer;

            if (dropDown is ToolStripDropDownMenu menu)
            {
                // Keep the image gutter ONLY when the items actually carry
                // icons. Otherwise hide both margins so
                // icon-free dropdowns stay completely dark and compact.
                menu.ShowImageMargin = HasAnyImage(menu.Items);
                menu.ShowCheckMargin = false;
                menu.AutoSize = true;
                menu.BackColor = Surface;
                menu.ForeColor = Text;
                menu.Padding = new Padding(0, 2, 0, 2);
            }
        }

        private static bool HasAnyImage(ToolStripItemCollection items)
        {
            if (items == null) return false;
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem mi)
                {
                    if (mi.Image != null) return true;
                    if (mi.HasDropDownItems && HasAnyImage(mi.DropDownItems)) return true;
                }
            }
            return false;
        }

        private static void ApplyToolStripItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = Text;
                item.BackColor = Surface;
                // NOTE: item images, if any are ever added again, must
                // survive theming passes.
                if (item is ToolStripMenuItem menuItem && menuItem.DropDown is ToolStripDropDown dropDown)
                {
                    dropDown.BackColor = Surface;
                    dropDown.ForeColor = Text;
                    dropDown.Renderer = DarkRenderer;
                    ConfigureDarkDropDown(dropDown);
                    ApplyToolStripItems(dropDown.Items);
                }
            }
        }

        private static void DarkMenuItem_MouseEnter(object sender, EventArgs e)
        {
            if (sender is ToolStripItem item)
            {
                item.BackColor = MenuHover;
                item.ForeColor = Text;
                item.Invalidate();
            }
        }

        private static void DarkMenuItem_MouseLeave(object sender, EventArgs e)
        {
            if (sender is ToolStripItem item)
            {
                item.BackColor = Surface;
                item.ForeColor = Text;
                item.Invalidate();
            }
        }

        private static void DrawDarkTab(object sender, DrawItemEventArgs e)
        {
            var tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? Selection : Surface;
            Color foreground = Text;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);

            using (var pen = new Pen(selected ? Accent : BorderSoft))
                e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static void ApplyPropertyGridInternalColors(PropertyGrid propertyGrid)
        {
            try
            {
                foreach (Control child in propertyGrid.Controls)
                {
                    ApplyPropertyGridInternalColorsRecursive(child);
                }
            }
            catch { }
        }

        private static void HookDarkModeDynamicChildren(PropertyGrid propertyGrid)
        {
            if (propertyGrid == null) return;

            // Avoid attaching the same handlers every time DarkTheme.Apply() is called.
            propertyGrid.ControlAdded -= PropertyGrid_ControlAdded;
            propertyGrid.ControlAdded += PropertyGrid_ControlAdded;
            propertyGrid.HandleCreated -= PropertyGrid_HandleCreated;
            propertyGrid.HandleCreated += PropertyGrid_HandleCreated;

            foreach (Control child in propertyGrid.Controls)
                HookDarkModeDynamicChild(child);
        }

        private static void PropertyGrid_ControlAdded(object sender, ControlEventArgs e)
        {
            if (e == null || e.Control == null) return;
            HookDarkModeDynamicChild(e.Control);
            ApplyControl(e.Control);
        }

        private static void PropertyGrid_HandleCreated(object sender, EventArgs e)
        {
            if (sender is PropertyGrid propertyGrid)
            {
                ApplyDarkNativeTheme(propertyGrid);
                // Ensure selection colors match the active theme even when the handle is recreated after a live switch.
                propertyGrid.SelectedItemWithFocusBackColor = Selection;
                propertyGrid.SelectedItemWithFocusForeColor = SelectionText;
                propertyGrid.ViewBackColor = Input;
                propertyGrid.ViewForeColor = Text;
                propertyGrid.CategoryForeColor = Text;
                propertyGrid.BackColor = Input;
                propertyGrid.LineColor = Input;
                ApplyPropertyGridInternalColors(propertyGrid);
                foreach (Control child in propertyGrid.Controls)
                    ApplyControl(child);
            }
        }

        private static void HookDarkModeDynamicChild(Control control)
        {
            if (control == null) return;

            control.HandleCreated -= DarkDynamicControl_HandleCreated;
            control.HandleCreated += DarkDynamicControl_HandleCreated;
            control.ControlAdded -= DynamicControl_ControlAdded;
            control.ControlAdded += DynamicControl_ControlAdded;
            foreach (Control child in control.Controls)
                HookDarkModeDynamicChild(child);
        }

        private static void DynamicControl_ControlAdded(object sender, ControlEventArgs e)
        {
            if (e == null || e.Control == null) return;
            HookDarkModeDynamicChild(e.Control);
            ApplyControl(e.Control);
            // PropertyGrid editors (the small TextBox for "TriggerZone Corner0.Z" etc.)
            // are created on demand and the grid sets their BackColor to Window (white)
            // *after* ControlAdded. Force the theme-correct color on the next pump.
            // Also handle any TextBox/ComboBox editor that may be added elsewhere.
            if (e.Control is TextBoxBase || e.Control is ComboBox)
            {
                try { e.Control.BeginInvoke(new Action(() => ForcePropertyGridEditorTheme(e.Control))); } catch { }
                // For containers, also check children that may be editors
                try { e.Control.BeginInvoke(new Action(() => { if (e.Control is Control c) ThemeActiveEditorsRecursive(c); })); } catch { }
            }
            else if (IsPropertyGridEditor(e.Control))
            {
                try { e.Control.BeginInvoke(new Action(() => ForcePropertyGridEditorTheme(e.Control))); } catch { }
            }
        }

        private static void ThemeActiveEditorsRecursive(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is TextBoxBase || c is ComboBox) ForcePropertyGridEditorTheme(c);
                if (c.Controls.Count > 0) ThemeActiveEditorsRecursive(c);
            }
        }

        private static bool IsPropertyGridEditor(Control c)
        {
            if (c is TextBoxBase || c is ComboBox)
            {
                Control p = c.Parent;
                while (p != null)
                {
                    if (p is PropertyGrid) return true;
                    p = p.Parent;
                }
            }
            return false;
        }

        private static void ForcePropertyGridEditorTheme(Control c)
        {
            if (c == null || c.IsDisposed) return;
            try
            {
                // Diagnostic log for the "Tool created by: JADERLINK" black selection bug
                try
                {
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_debug.log");
                    string info = string.Format("[{0:HH:mm:ss}] ForceEditor IsLight={1} Input={2:X6} Text={3:X6} Ctrl={4} Handle={5} Parent={6}\r\n",
                        DateTime.Now, UiTheme.IsLight, Input.ToArgb() & 0xFFFFFF, Text.ToArgb() & 0xFFFFFF,
                        c.GetType().Name, c.IsHandleCreated ? c.Handle.ToString("X") : "0",
                        c.Parent != null ? c.Parent.GetType().Name : "null");
                    System.IO.File.AppendAllText(logPath, info);
                }
                catch { }
                c.BackColor = Input;
                c.ForeColor = Text;
                if (c is TextBoxBase tb) tb.BorderStyle = BorderStyle.FixedSingle;
                if (c.IsHandleCreated) ApplyDarkNativeTheme(c);
                c.Invalidate(true);
            }
            catch { }
        }

        private static void DarkDynamicControl_HandleCreated(object sender, EventArgs e)
        {
            if (sender is Control control)
            {
                ApplyDarkNativeThemeRecursive(control);
                ApplyControl(control);
                if (IsPropertyGridEditor(control))
                {
                    try { control.BeginInvoke(new Action(() => ForcePropertyGridEditorTheme(control))); } catch { }
                }
            }
        }

        private static void ApplyPropertyGridInternalColorsRecursive(Control control)
        {
            if (control == null) return;

            // PropertyGridView is an internal WinForms control. Depending on the
            // .NET Framework version, the disabled/read-only text color is exposed
            // as a non-public property. Use reflection only when it exists; this
            // keeps the source compatible with framework versions that do not have it.
            if (control.GetType().Name.IndexOf("PropertyGridView", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // PropertyGrid has several internal surfaces that are not exposed by
                // the public PropertyGrid API. If these are left untouched, WinForms
                // paints category rows/command areas with a different grey, which
                // breaks the single-surface dark theme.
                TrySetColorProperty(control, "DisabledItemForeColor", Text);
                TrySetColorProperty(control, "ViewForeColor", Text);
                TrySetColorProperty(control, "ViewBackColor", Input);
                TrySetColorProperty(control, "CategoryForeColor", Text);
                TrySetColorProperty(control, "CategoryBackColor", Input);
                TrySetColorProperty(control, "CommandsForeColor", Text);
                TrySetColorProperty(control, "CommandsBackColor", Input);
                TrySetColorProperty(control, "HelpForeColor", Text);
                TrySetColorProperty(control, "HelpBackColor", Input);
                TrySetColorProperty(control, "LineColor", Input);
                TrySetColorProperty(control, "ViewBorderColor", BorderSoft);
                TrySetColorProperty(control, "CategorySplitterColor", BorderSoft);
                TrySetColorProperty(control, "HelpBorderColor", BorderSoft);
                TrySetColorProperty(control, "CommandsBorderColor", BorderSoft);
                control.ForeColor = Text;
                control.BackColor = Input;
                ApplyDarkNativeTheme(control);
                control.Invalidate(true);
            }

            foreach (Control child in control.Controls)
                ApplyPropertyGridInternalColorsRecursive(child);
        }

        private static void TrySetColorProperty(Control control, string propertyName, Color value)
        {
            try
            {
                var property = control.GetType().GetProperty(
                    propertyName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (property != null && property.CanWrite && property.PropertyType == typeof(Color))
                    property.SetValue(control, value, null);
            }
            catch { }
        }

        private static void DisableVisualStyles(Control control)
        {
            if (!control.IsHandleCreated)
                return;
            try
            {
                SetWindowTheme(control.Handle, string.Empty, string.Empty);
            }
            catch { }
        }

        /// <summary>
        /// Re-enables the standard Windows theme on a control whose visual styles
        /// had been disabled for Dark Mode. Needed when switching back to Light
        /// Mode, otherwise MenuStrip/GroupBox stay in the disabled painter look.
        /// </summary>
        private static void RestoreVisualStyles(Control control)
        {
            if (!control.IsHandleCreated)
                return;
            try
            {
                SetWindowTheme(control.Handle, null, null);
            }
            catch { }
        }

        /// <summary>
        /// Uses the Windows dark common-control theme for native scrollbars.
        /// Without this, TreeView/PropertyGrid keep the Windows light scrollbar
        /// (the white strip visible at the right side of the panels).
        /// This is intentionally used only by Dark Mode.
        /// </summary>
        internal static void ApplyDarkNativeTheme(Control control)
        {
            if (control == null || !control.IsHandleCreated)
                return;

            // Diagnostic for GridViewEdit black selection bug
            try
            {
                string cname = control.GetType().Name;
                if (cname.IndexOf("GridViewEdit", StringComparison.OrdinalIgnoreCase) >= 0 || cname.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string logPath2 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_debug.log");
                    string info2 = string.Format("[{0:HH:mm:ss}] ApplyDarkNative IsLight={1} Ctrl={2} Handle={3:X} Parent={4}\r\n",
                        DateTime.Now, UiTheme.IsLight, cname, control.Handle.ToInt64(), control.Parent != null ? control.Parent.GetType().Name : "null");
                    System.IO.File.AppendAllText(logPath2, info2);
                }
            }
            catch { }
            // When Light Mode is active, undo any dark native common-control
            // theme (DarkMode_ItemsView/DarkMode_Explorer + AllowDarkModeForWindow)
            // that an earlier Dark Mode session applied. Without this the native
            // edit/scrollbar/editor HWNDs keep their dark palette after a live
            // Dark -> Light switch, leaving dark "residue" inside a light skin.
            if (UiTheme.IsLight)
            {
                _nativeThemedHandles.Remove(control.Handle);
                RevertNativeToLight(control);
                return;
            }

            // Only apply the dark native theme once per HWND. New handles (created
            // lazily: scrollbars, the PropertyGrid drop-down list, secondary
            // windows) are not in the set yet, so they get themed on the pass
            // that first sees them.
            control.HandleDestroyed -= NativeHandleDestroyed;
            control.HandleDestroyed += NativeHandleDestroyed;

            // A MultiselectTreeView with DarkScrollBar enabled renders its own
            // dark bar and suppresses the native non-client painter itself.
            // Do not apply any theme class here; just make sure no themed
            // painter can ever draw a second scrollbar on the HWND.
            var ownedTree = control as NsMultiselectTreeView.MultiselectTreeView;
            if (ownedTree != null && ownedTree.DarkScrollBar)
            {
                SetWindowTheme(control.Handle, string.Empty, string.Empty);
                return;
            }

            bool alreadyThemed = !_nativeThemedHandles.Add(control.Handle);
            try
            {
                if (!alreadyThemed)
                {
                    // First opt the individual common-control HWND into dark mode, then
                    // apply the dark theme class. This is different from merely disabling
                    // visual styles (which produces the bright classic white scrollbar).
                    AllowDarkModeForWindow(control.Handle, true);
                    // TreeView ignores the dark ScrollBar part of DarkMode_Explorer on
                    // several Windows 10/11 builds and keeps a white strip. The
                    // "DarkMode_ItemsView" class is the one that supplies the dark
                    // ScrollBar part for item-view controls (TreeView/ListView), so it
                    // is preferred for TreeView, with DarkMode_Explorer as fallback.
                    bool isTree = control is TreeView;
                    string darkClass = isTree ? "DarkMode_ItemsView" : "DarkMode_Explorer";
                    string lightFallback = isTree ? "DarkMode_Explorer" : "Explorer";
                    int hr = SetWindowTheme(control.Handle, darkClass, null);
                    if (hr != 0)
                        SetWindowTheme(control.Handle, lightFallback, null);
                }

                // Always re-scan child HWNDs so a native scrollbar that WinForms
                // created lazily after the previous pass still receives the dark
                // common-control theme (white strip disappears on TreeView/
                // PropertyGrid/drop-down list).
                ThemeAllChildWindows(control.Handle);
                SendThemeChanged(control.Handle);

                // The custom scrollbar skin was removed entirely: it painted a
                // second bar on top of the native dark scrollbar, which produced
                // doubled/flickering bars (scene tree, grid drop-downs). The
                // DarkMode_Explorer theme above alone renders stable dark bars.
                control.Invalidate(true);
            }
            catch { }
        }

        /// <summary>
        /// Restores the stock Windows light common-control theme on an HWND that
        /// was previously opted into Dark Mode. Called only while Light Mode is
        /// being (re)applied so native scrollbars/editors do not keep the dark
        /// palette after a live theme switch.
        /// </summary>
        private static void RevertNativeToLight(Control control)
        {
            if (control == null || !control.IsHandleCreated)
                return;
            try
            {
                // Owned dark scrollbar skin is light-mode disabled by the shell,
                // so simply restore the normal themed surface.
                AllowDarkModeForWindow(control.Handle, false);
                // "Explorer" is the standard light class for common controls;
                // an empty class would disable parts of the scrollbar theming.
                SetWindowTheme(control.Handle, "Explorer", null);
                RevertChildWindowsToLight(control.Handle);
                SendThemeChanged(control.Handle);
                control.Invalidate(true);
            }
            catch { }
        }

        private static void RevertChildWindowsToLight(IntPtr parent)
        {
            if (parent == IntPtr.Zero) return;
            try
            {
                EnumChildWindows(parent, (hwnd, lParam) =>
                {
                    try
                    {
                        AllowDarkModeForWindow(hwnd, false);
                        SetWindowTheme(hwnd, "Explorer", null);
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private static void ThemeAllChildWindows(IntPtr parent)
        {
            if (parent == IntPtr.Zero) return;
            try
            {
                EnumChildWindows(parent, (hwnd, lParam) =>
                {
                    try
                    {
                        AllowDarkModeForWindow(hwnd, true);
                        int hr = SetWindowTheme(hwnd, "Explorer", null);
                        if (hr != 0)
                            SetWindowTheme(hwnd, "DarkMode_Explorer", null);
                        SendMessage(hwnd, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private static void DrawDarkComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null || e.Index < 0) return;

            Color back = (e.State & DrawItemState.Selected) != 0 ? Selection : Input;
            Color fore = SelectionText;
            if ((e.State & DrawItemState.ComboBoxEdit) == 0)
                fore = Text;

            using (var backBrush = new SolidBrush(back))
                e.Graphics.FillRectangle(backBrush, e.Bounds);

            string text = combo.GetItemText(combo.Items[e.Index]);
            Rectangle textRect = new Rectangle(e.Bounds.X + 5, e.Bounds.Y + 1,
                Math.Max(0, e.Bounds.Width - 8), Math.Max(0, e.Bounds.Height - 2));
            TextRenderer.DrawText(e.Graphics, text, combo.Font, textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if ((e.State & DrawItemState.Focus) != 0)
            {
                using (var pen = new Pen(BorderSoft))
                    e.Graphics.DrawRectangle(pen, new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1));
            }
            e.DrawFocusRectangle();
        }

        private static void SendThemeChanged(IntPtr hwnd)
        {
            try
            {
                SendMessage(hwnd, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch { }
        }

        private static void EnsureDarkNativeHandles(Control control)
        {
            // Light Mode keeps the stock light native handles.
            if (UiTheme.IsLight) return;
            if (control == null) return;

            bool nativeTarget = control is PropertyGrid ||
                                control is TreeView ||
                                control is ListBox ||
                                control is CheckedListBox ||
                                control is ComboBox ||
                                control is TextBoxBase;

            if (nativeTarget)
            {
                try
                {
                    control.CreateControl();
                    ApplyDarkNativeTheme(control);
                }
                catch { }
            }

            foreach (Control child in control.Controls)
                EnsureDarkNativeHandles(child);
        }

        private static void ApplyDarkNativeThemeRecursive(Control control)
        {
            if (UiTheme.IsLight) return;
            if (control == null) return;

            ApplyDarkNativeTheme(control);
            foreach (Control child in control.Controls)
            {
                ApplyDarkNativeThemeRecursive(child);
            }
        }

        private static void SetPreferredDarkMode(bool enabled)
        {
            try
            {
                IntPtr hUxTheme = LoadLibrary("uxtheme.dll");
                if (hUxTheme == IntPtr.Zero) return;

                // Undocumented-but-stable on supported Windows 10/11 builds:
                // SetPreferredAppMode(PreferredAppMode.AllowDark) = 1.
                IntPtr proc = GetProcAddress(hUxTheme, (IntPtr)135);
                if (proc == IntPtr.Zero) return;
                var fn = (SetPreferredAppModeDelegate)Marshal.GetDelegateForFunctionPointer(proc, typeof(SetPreferredAppModeDelegate));
                fn(enabled ? 1 : 3); // AllowDark / ForceLight
            }
            catch { }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetPreferredAppModeDelegate(int preferredMode);

        private static bool AllowDarkModeForWindow(IntPtr hwnd, bool allow)
        {
            try
            {
                IntPtr hUxTheme = LoadLibrary("uxtheme.dll");
                if (hUxTheme == IntPtr.Zero) return false;

                // Ordinal 133 is AllowDarkModeForWindow on supported Windows 10/11
                // builds. Resolve dynamically so older systems remain compatible.
                IntPtr proc = GetProcAddress(hUxTheme, (IntPtr)133);
                if (proc == IntPtr.Zero) return false;

                var fn = (AllowDarkModeForWindowDelegate)Marshal.GetDelegateForFunctionPointer(proc, typeof(AllowDarkModeForWindowDelegate));
                return fn(hwnd, allow);
            }
            catch { return false; }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool AllowDarkModeForWindowDelegate(IntPtr hwnd, bool allow);

        private const int WM_THEMECHANGED = 0x031A;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr lpProcName);

        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }

    internal sealed class DarkToolStripColorTable : ProfessionalColorTable
    {
        public DarkToolStripColorTable()
        {
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => DarkTheme.Surface;
        public override Color MenuStripGradientEnd => DarkTheme.Surface;
        public override Color ToolStripGradientBegin => DarkTheme.Surface;
        public override Color ToolStripGradientMiddle => DarkTheme.Surface;
        public override Color ToolStripGradientEnd => DarkTheme.Surface;
        public override Color ToolStripDropDownBackground => DarkTheme.Surface;
        public override Color ToolStripBorder => DarkTheme.BorderSoft;
        public override Color ImageMarginGradientBegin => DarkTheme.Surface2;
        public override Color ImageMarginGradientMiddle => DarkTheme.Surface2;
        public override Color ImageMarginGradientEnd => DarkTheme.Surface2;
        public override Color MenuItemSelected => DarkTheme.MenuHover;
        public override Color MenuItemSelectedGradientBegin => DarkTheme.MenuHover;
        public override Color MenuItemSelectedGradientEnd => DarkTheme.MenuHover;
        public override Color MenuItemPressedGradientBegin => DarkTheme.Selection;
        public override Color MenuItemPressedGradientEnd => DarkTheme.Selection;
        public override Color MenuBorder => DarkTheme.Border;
        public override Color MenuItemBorder => DarkTheme.Border;
        public override Color SeparatorDark => DarkTheme.BorderSoft;
        public override Color SeparatorLight => DarkTheme.BorderSoft;
        public override Color ButtonSelectedBorder => DarkTheme.BorderSoft;
        public override Color ButtonSelectedGradientBegin => DarkTheme.Surface;
        public override Color ButtonSelectedGradientEnd => DarkTheme.Surface;
        public override Color ButtonPressedBorder => DarkTheme.BorderSoft;
        public override Color ButtonPressedGradientBegin => DarkTheme.Selection;
        public override Color ButtonPressedGradientEnd => DarkTheme.Selection;
    }

    internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkToolStripColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(DarkTheme.Surface))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        // WinForms can route a top-level MenuStrip item through the generic
        // item-background renderer while the item is hot/selected. Intercept that
        // path as well so File/Edit/View never fall back to the system's bright
        // white highlight in Dark Mode.
        protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem)
            {
                Color background = DarkTheme.Surface;
                if (e.Item.Pressed)
                    background = DarkTheme.Selection;
                else if (e.Item.Selected)
                    background = DarkTheme.MenuHover;

                e.Item.BackColor = background;
                e.Item.ForeColor = DarkTheme.Text;
                using (var brush = new SolidBrush(background))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                return;
            }

            base.OnRenderItemBackground(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            // Paint the complete item ourselves. System visual styles can paint a
            // bright blue selection after the normal background pass, so do not call
            // the base implementation here.
            Color background = DarkTheme.Surface;
            if (e.Item.Pressed)
                background = DarkTheme.Selection;
            else if (e.Item.Selected)
                background = DarkTheme.MenuHover;

            e.Item.BackColor = background;
            e.Item.ForeColor = DarkTheme.Text;
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, bounds);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            // Top-level MenuStrip items are painted through this path on some .NET
            // Framework builds. Paint them directly so hover never falls back to the
            // Windows blue highlight.
            Color background = DarkTheme.Surface;
            if (e.Item.Pressed)
                background = DarkTheme.Selection;
            else if (e.Item.Selected)
                background = DarkTheme.MenuHover;

            e.Item.BackColor = background;
            e.Item.ForeColor = DarkTheme.Text;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Color background = e.Item.Pressed ? DarkTheme.Selection :
                (e.Item.Selected ? DarkTheme.MenuHover : DarkTheme.Surface);

            e.Item.BackColor = background;
            e.Item.ForeColor = DarkTheme.Text;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? DarkTheme.Text : DarkTheme.Disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? DarkTheme.TextSecondary : DarkTheme.Disabled;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(DarkTheme.BorderSoft))
                e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(DarkTheme.Surface2))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(DarkTheme.BorderSoft))
            {
                Rectangle r = e.AffectedBounds;
                if (r.Width > 1 && r.Height > 1)
                    e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Some WinForms versions paint the check/image gutter before this callback.
            // Explicitly cover that gutter so no white square can leak through.
            Rectangle gutter = new Rectangle(0, 0, Math.Max(28, e.Item.Height + 8), e.Item.Height);
            using (var brush = new SolidBrush(DarkTheme.Surface))
                e.Graphics.FillRectangle(brush, gutter);

            Rectangle r = new Rectangle(6, 4, Math.Max(12, e.Item.Height - 8), Math.Max(12, e.Item.Height - 8));
            using (var brush = new SolidBrush(DarkTheme.Surface2))
                e.Graphics.FillRectangle(brush, r);
            if (e.Item is ToolStripMenuItem item && item.Checked)
            {
                using (var pen = new Pen(DarkTheme.Accent, 2f))
                {
                    e.Graphics.DrawLine(pen, r.Left + 3, r.Top + r.Height / 2, r.Left + r.Width / 2 - 1, r.Bottom - 3);
                    e.Graphics.DrawLine(pen, r.Left + r.Width / 2 - 1, r.Bottom - 3, r.Right - 3, r.Top + 3);
                }
            }
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            // Keep the image gutter dark even if an old designer/resource image exists.
            Rectangle r = new Rectangle(0, 0, Math.Max(28, e.Item.Height + 8), e.Item.Height);
            using (var brush = new SolidBrush(DarkTheme.Surface))
                e.Graphics.FillRectangle(brush, r);

            // CRITICAL: actually paint the icon. This override previously only
            // filled the gutter and returned, so every menu icon showed as an
            // empty dark square.
            if (e.Image != null)
            {
                System.Drawing.Drawing2D.InterpolationMode oldInterp =
                    e.Graphics.InterpolationMode;
                System.Drawing.Drawing2D.PixelOffsetMode oldOffset =
                    e.Graphics.PixelOffsetMode;
                e.Graphics.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode =
                    System.Drawing.Drawing2D.PixelOffsetMode.Half;
                e.Graphics.DrawImage(e.Image, e.ImageRectangle);
                e.Graphics.InterpolationMode = oldInterp;
                e.Graphics.PixelOffsetMode = oldOffset;
            }
        }

    }
}
