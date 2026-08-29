using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Controls;

namespace Re4QuadExtremeEditor
{
    public partial class MainForm
    {
        private bool modernUiReady;
        private ModernCardPanel modernTreeCard;
        private ModernCardPanel modernPropertyCard;
        private ModernCardPanel modernViewportCard;
        private ModernCardPanel modernControlsCard;
        private Panel sceneSearchBar;
        private ModernSearchTextBox sceneSearchTextBox;
        private Button sceneSearchClearButton;
        private System.Collections.Generic.HashSet<TreeNode> expandedNodesBeforeSearch;
        private System.Collections.Generic.List<TreeNode> currentSearchMatches;
        private int currentSearchMatchIndex;
        private Label inspectorSelectionLabel;
        private ToolTip sceneNodeToolTip;
        private TreeNode sceneTooltipNode;

        private bool IsModernDarkMode
        {
            // follows the ACTIVE theme (Dark ON / Light mirror), not a legacy flag
            get { return !UiTheme.IsLight; }
        }

        /// <summary>
        /// Re-themes the already-open editor window in place (live Dark/Light
        /// switch). A short opacity dip softens the recolor so the change reads
        /// as a smooth transition instead of a hard snap. Safe to call often.
        /// </summary>
        public void ApplyThemeLive()
        {
            try
            {
                DoThemeRestyle();
            }
            catch { }
        }

        private void DoThemeRestyle()
        {
            try
            {
                try
                {
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_debug.log");
                    System.IO.File.AppendAllText(logPath, string.Format("[{0:HH:mm:ss}] DoThemeRestyle IsLight={1} Input={2:X6} Text={3:X6}\r\n", DateTime.Now, UiTheme.IsLight, DarkTheme.Input.ToArgb() & 0xFFFFFF, DarkTheme.Text.ToArgb() & 0xFFFFFF));
                }
                catch { }
                DarkTheme.Apply(this);
                ApplyHudThemeColors();
                if (modernUiReady)
                {
                    StyleModernShell();
                }
                // PropertyGrid/TreeView selection was staying dark (near-black) after a
                // live switch to Light mode because the grid's handle was already created.
                // Force the correct palette-aware colors immediately and re-select the
                // current grid item so the already-painted row repaints with the new brush.
                try
                {
                    if (propertyGridObjs != null)
                    {
                        propertyGridObjs.SelectedItemWithFocusBackColor = UiTheme.IsLight
                            ? Color.FromArgb(0xE4, 0xEE, 0xFB)
                            : Color.FromArgb(0x2D, 0x31, 0x39);
                        propertyGridObjs.SelectedItemWithFocusForeColor = UiTheme.IsLight
                            ? Color.FromArgb(0x12, 0x18, 0x1F)
                            : Color.FromArgb(0xF9, 0xFA, 0xFC);
                        propertyGridObjs.ViewBackColor = DarkTheme.Input;
                        propertyGridObjs.ViewForeColor = DarkTheme.Text;
                        propertyGridObjs.BackColor = DarkTheme.Input;
                        propertyGridObjs.LineColor = DarkTheme.Input;
                        propertyGridObjs.CategoryForeColor = DarkTheme.Text;
                        // Re-select to force the already-visible row (e.g. "Tool created by: JADERLINK")
                        // to repaint with the new selection brush - otherwise it keeps the old dark bitmap until restart.
                        var curSel = propertyGridObjs.SelectedGridItem;
                        var curObj = propertyGridObjs.SelectedObject;
                        propertyGridObjs.SelectedObject = null;
                        propertyGridObjs.SelectedObject = curObj;
                        if (curSel != null)
                        {
                            try { propertyGridObjs.SelectedGridItem = curSel; } catch { }
                        }
                        propertyGridObjs.Refresh();
                    }
                    if (treeViewObjs is NsMultiselectTreeView.MultiselectTreeView mt)
                    {
                        mt.SelectedNodeBackColor = UiTheme.IsLight
                            ? Color.FromArgb(0xE4, 0xEE, 0xFB)
                            : Color.FromArgb(0x2D, 0x31, 0x39);
                        mt.UseThemedSelectedNodeBackColor = true;
                        mt.Invalidate();
                    }
                    // The in-place editor (e.g. "Unknown LO" Byte 01) is created on demand
                    // inside PropertyGridView and was staying white in Dark mode.
                    void ThemeEditor(Control root)
                    {
                        foreach (Control c in root.Controls)
                        {
                            if (c is TextBoxBase tb)
                            {
                                tb.BackColor = DarkTheme.Input;
                                tb.ForeColor = DarkTheme.Text;
                                tb.BorderStyle = BorderStyle.FixedSingle;
                                if (tb.IsHandleCreated) DarkTheme.ApplyDarkNativeTheme(tb);
                            }
                            else if (c is ComboBox cb)
                            {
                                cb.BackColor = DarkTheme.Input;
                                cb.ForeColor = DarkTheme.Text;
                                if (cb.IsHandleCreated) DarkTheme.ApplyDarkNativeTheme(cb);
                            }
                            if (c.Controls.Count > 0) ThemeEditor(c);
                        }
                    }
                    if (propertyGridObjs != null) ThemeEditor(propertyGridObjs);
                }
                catch { }
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (treeViewObjs != null) { treeViewObjs.DarkScrollBar = IsModernDarkMode; }
                        DarkTheme.RefreshNativeScrollbars(this);
                        Invalidate(true);
                        foreach (Control c in Controls) { c.Invalidate(true); }
                    }));
                }
                catch { }
            }
            catch { }
        }

        private void MainForm_ShownModernUI(object sender, EventArgs e)
        {
            // The redesigned shell renders in BOTH themes: every color flows
            // through DarkTheme/UiTheme, so Light Mode is an exact mirror.
            if (modernUiReady)
                return;

            BuildModernShell();
            StyleModernShell();
            modernUiReady = true;

            Resize += MainForm_ModernResize;
            MainForm_ModernResize(this, EventArgs.Empty);
            UpdateSceneSearch("");

            // Native TreeView scrollbars can be created lazily after the initial
            // dark-theme pass. Re-apply the dark native theme once the modern shell
            // and its child HWNDs are fully built. This is Dark Mode only.
            try
            {
                BeginInvoke(new Action(() =>
                {
                    // The TreeView handle can be recreated while the modern shell
                    // is being reparented. Re-enable the owned dark scrollbar and
                    // re-apply dark themes after the final handle exists.
                    treeViewObjs.DarkScrollBar = IsModernDarkMode;
                    DarkTheme.RefreshNativeScrollbars(this);
                }));
            }
            catch { }
        }

        private void BuildModernShell()
        {
            SuspendLayout();
            try
            {
                // Keep the original lightweight MenuStrip only. No command toolbar
                // and no extra app-bar above the viewport.
                menuStripMenu.Dock = DockStyle.Top;
                menuStripMenu.Height = 26;
                menuStripMenu.Padding = new Padding(8, 2, 8, 2);

                BuildLeftScenePanel();
                BuildLeftInspectorPanel();
                BuildViewportPanel();
                StyleBottomControlDeck();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void BuildLeftScenePanel()
        {
            Control panel = splitContainerLeft.Panel1;
            panel.SuspendLayout();
            panel.Padding = new Padding(4, 4, 4, 4);
            panel.Controls.Clear();
            panel.BackColor = DarkTheme.Window;

            modernTreeCard = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = DarkTheme.Surface,
                // The scene panel is a layout surface, not a card.
                // Keeping its border identical to the surface removes the
                // second rectangle around the search field.
                BorderColor = DarkTheme.Surface,
                Radius = 0
            };

            // Compact search row only. No permanent SCENE title and no extra header
            // row: the tree starts immediately underneath the search field.
            TableLayoutPanel sceneLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = DarkTheme.Surface,
                ColumnCount = 1,
                RowCount = 2
            };
            sceneLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            sceneLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27f));
            sceneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel sceneSearchBar = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 1),
                Padding = Padding.Empty,
                BackColor = DarkTheme.Input,
                BorderStyle = BorderStyle.None
            };
            // Paint the single search outline here. The TextBox itself stays borderless,
            // preventing the old double-border effect while keeping the clear button
            // visually inside the same search surface.
            sceneSearchBar.Paint += delegate(object sender, PaintEventArgs args)
            {
                if (sceneSearchBar.Width < 2 || sceneSearchBar.Height < 2) return;
                using (var pen = new Pen(DarkTheme.BorderSoft, 1f))
                {
                    var r = new Rectangle(0, 0, sceneSearchBar.Width - 1, sceneSearchBar.Height - 1);
                    args.Graphics.DrawRectangle(pen, r);
                }
            };
            this.sceneSearchBar = sceneSearchBar;

            Label searchIcon = new Label
            {
                Dock = DockStyle.Left,
                Width = 16,
                Text = "⌕",
                Font = new Font("Segoe UI Symbol", 10f, FontStyle.Regular),
                ForeColor = DarkTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            sceneSearchClearButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 21,
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.Input,
                ForeColor = DarkTheme.TextSecondary,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                TabStop = false,
                Margin = new Padding(0)
            };
            sceneSearchClearButton.FlatAppearance.BorderSize = 0;
            sceneSearchClearButton.FlatAppearance.MouseOverBackColor = DarkTheme.Surface3;
            sceneSearchClearButton.FlatAppearance.MouseDownBackColor = DarkTheme.Selection;
            sceneSearchClearButton.Click += SceneSearchClearButton_Click;

            sceneSearchTextBox = new ModernSearchTextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = DarkTheme.Input,
                ForeColor = DarkTheme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Text = "",
                Margin = Padding.Empty,
                TabStop = true,
                AcceptsReturn = false,
                Multiline = false,
                ShortcutsEnabled = true,
                AutoSize = true
            };
            sceneSearchTextBox.TextChanged += SceneSearchTextBox_TextChanged;
            sceneSearchTextBox.KeyDown += SceneSearchTextBox_KeyDown;

            // Dock order matters: clear button is visually inside the same bordered
            // search rectangle, not a separate floating control.
            sceneSearchBar.Controls.Add(sceneSearchTextBox);
            sceneSearchBar.Controls.Add(sceneSearchClearButton);
            sceneSearchBar.Controls.Add(searchIcon);

            sceneSearchBar.Resize += delegate(object sender, EventArgs args)
            {
                int iconWidth = searchIcon.Width;
                int clearWidth = sceneSearchClearButton.Width;
                int textLeft = iconWidth + 3;
                int textWidth = Math.Max(10, sceneSearchBar.ClientSize.Width - textLeft - clearWidth - 3);
                int textHeight = Math.Max(18, sceneSearchTextBox.PreferredHeight);
                sceneSearchTextBox.SetBounds(textLeft, Math.Max(0, (sceneSearchBar.ClientSize.Height - textHeight) / 2), textWidth, textHeight);
            };
            sceneSearchBar.Resize += delegate(object sender, EventArgs args) { sceneSearchBar.Invalidate(); };
            sceneSearchBar.PerformLayout();

            treeViewObjs.Dock = DockStyle.Fill;
            treeViewObjs.Margin = new Padding(0);
            treeViewObjs.Padding = new Padding(0, 4, 0, 3);
            treeViewObjs.BackColor = DarkTheme.Input;
            treeViewObjs.ForeColor = DarkTheme.Text;
            treeViewObjs.LineColor = DarkTheme.BorderSoft;
            treeViewObjs.ItemHeight = 23;
            treeViewObjs.Font = new Font("Segoe UI", 8.7f, FontStyle.Regular);
            treeViewObjs.HideSelection = false;
            treeViewObjs.BorderStyle = BorderStyle.None;
            // 2026 look: no dotted connector lines, no native +/- boxes.
            // Disclosure chevrons are owner-drawn in MultiselectTreeView.
            treeViewObjs.ShowLines = false;
            treeViewObjs.ShowRootLines = false;
            treeViewObjs.ShowPlusMinus = false;
            treeViewObjs.AfterSelect -= ModernTree_AfterSelect;
            treeViewObjs.AfterSelect += ModernTree_AfterSelect;
            treeViewObjs.MouseMove -= ModernTree_MouseMove;
            treeViewObjs.MouseMove += ModernTree_MouseMove;
            treeViewObjs.MouseLeave -= ModernTree_MouseLeave;
            treeViewObjs.MouseLeave += ModernTree_MouseLeave;

            sceneNodeToolTip = new ToolTip
            {
                AutoPopDelay = 7000,
                InitialDelay = 450,
                ReshowDelay = 200,
                ShowAlways = true
            };

            sceneLayout.Controls.Add(sceneSearchBar, 0, 0);

            // Dedicated host panel for the tree: the dark scrollbar overlay is an
            // absolutely positioned sibling inside this plain panel. Keeping it
            // out of the TableLayoutPanel preserves the layout that sizes the
            // tree to the full card height.
            Panel treeHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = DarkTheme.Input
            };
            treeHostPanel.Controls.Add(treeViewObjs);
            sceneLayout.Controls.Add(treeHostPanel, 0, 1);
            modernTreeCard.Controls.Add(sceneLayout);
            panel.Controls.Add(modernTreeCard);
            panel.ResumeLayout(true);

            currentSearchMatches = new System.Collections.Generic.List<TreeNode>();
            currentSearchMatchIndex = -1;
        }

        private void BuildLeftInspectorPanel()
        {
            Control panel = splitContainerLeft.Panel2;
            panel.SuspendLayout();
            panel.Padding = new Padding(4, 2, 4, 4);
            panel.Controls.Clear();
            panel.BackColor = DarkTheme.Window;

            modernPropertyCard = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = DarkTheme.Surface,
                BorderColor = DarkTheme.BorderSoft,
                Radius = 8
            };

            inspectorSelectionLabel = null;

            propertyGridObjs.Dock = DockStyle.Fill;
            propertyGridObjs.Margin = new Padding(0);
            propertyGridObjs.BackColor = DarkTheme.Input;
            propertyGridObjs.ViewBackColor = DarkTheme.Input;
            propertyGridObjs.ViewBorderColor = DarkTheme.BorderSoft;
            propertyGridObjs.HelpBackColor = DarkTheme.Input;
            propertyGridObjs.HelpVisible = false;
            propertyGridObjs.CommandsVisibleIfAvailable = false;
            propertyGridObjs.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            propertyGridObjs.ToolbarVisible = false;
            propertyGridObjs.PropertySort = PropertySort.Categorized;
            propertyGridObjs.LineColor = DarkTheme.BorderSoft;
            propertyGridObjs.CategorySplitterColor = DarkTheme.BorderSoft;
            propertyGridObjs.ViewBorderColor = DarkTheme.BorderSoft;

            modernPropertyCard.Controls.Add(propertyGridObjs);
            panel.Controls.Add(modernPropertyCard);
            panel.ResumeLayout(true);

            UpdateInspectorHeader();
        }

        private void BuildViewportPanel()
        {
            Control panel = splitContainerRight.Panel1;
            panel.SuspendLayout();
            panel.Padding = new Padding(8, 8, 8, 4);
            panel.BackColor = DarkTheme.Window;
            panel.Controls.Clear();

            modernViewportCard = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = Color.Black,
                BorderColor = DarkTheme.BorderSoft,
                Radius = 8
            };

            // No header, no title strip, no extra blue/dark band.
            // The OpenGL viewport owns the full card area.
            glControl.Dock = DockStyle.Fill;
            glControl.Margin = new Padding(0);
            modernViewportCard.Controls.Add(glControl);
            panel.Controls.Add(modernViewportCard);
            panel.ResumeLayout(true);
        }

        private void StyleBottomControlDeck()
        {
            Control panel = splitContainerRight.Panel2;
            panel.BackColor = DarkTheme.Window;
            panel.Padding = new Padding(4, 3, 4, 3);

            modernControlsCard = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                BorderColor = DarkTheme.BorderSoft,
                BackColor = DarkTheme.Surface,
                Radius = 8,
                Padding = new Padding(0),
                AutoScroll = false
            };

            if (objectMove.Parent != null)
                objectMove.Parent.Controls.Remove(objectMove);
            if (cameraMove.Parent != null)
                cameraMove.Parent.Controls.Remove(cameraMove);

            modernControlsCard.Controls.Add(objectMove);
            modernControlsCard.Controls.Add(cameraMove);

            objectMove.BackColor = Color.Transparent;
            cameraMove.BackColor = Color.Transparent;
            objectMove.Anchor = AnchorStyles.None;
            cameraMove.Anchor = AnchorStyles.None;
            objectMove.Location = new Point(8, 8);
            cameraMove.Location = new Point(8, 8);

            // Host the controls card inside the shared utility tab control's
            // "Controls" page, keeping the "Console" tab reachable in dark mode
            // too (same tabbed bottom deck as Re4QuadX).
            if (controlsTab != null)
            {
                controlsTab.Controls.Add(modernControlsCard);
                utilityPanel.SelectedTab = controlsTab;
            }
            else
            {
                panel.Controls.Add(modernControlsCard);
                modernControlsCard.BringToFront();
            }

            // The two legacy control decks remain side-by-side. The main form minimum
            // width prevents them from overlapping when the window is resized.
        }

        private void StyleModernShell()
        {
            BackColor = DarkTheme.Window;
            ForeColor = DarkTheme.Text;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            splitContainerMain.BackColor = DarkTheme.Window;
            splitContainerMain.SplitterWidth = 1;
            splitContainerMain.Panel1MinSize = 300;
            // The bottom ObjectMove (430px) and CameraMove (250px) controls must
            // always remain side-by-side. Reserve enough width for both plus margins.
            splitContainerMain.Panel2MinSize = 730;

            splitContainerLeft.SplitterWidth = 1;
            splitContainerLeft.Panel1MinSize = 200;
            splitContainerLeft.Panel2MinSize = 150;

            splitContainerRight.SplitterWidth = 1;
            // The legacy movement controls are fixed-height UserControls. Their
            // runtime Height can be larger than the designer value when WinForms
            // DPI/font scaling is active, so never hard-code a smaller Panel2 height.
            splitContainerRight.Panel2MinSize = GetBottomControlDeckMinHeight();

            menuStripMenu.BackColor = DarkTheme.Surface;
            menuStripMenu.ForeColor = DarkTheme.Text;

            DarkTheme.Apply(this);
            treeViewObjs.DarkScrollBar = IsModernDarkMode;
            treeViewObjs.BackColor = DarkTheme.Input;
            propertyGridObjs.BackColor = DarkTheme.Input;
            propertyGridObjs.ViewBackColor = DarkTheme.Input;

            if (modernTreeCard != null) modernTreeCard.BackColor = DarkTheme.Surface;
            if (modernPropertyCard != null) modernPropertyCard.BackColor = DarkTheme.Surface;
            if (modernViewportCard != null) modernViewportCard.BackColor = Color.Black;
            if (modernControlsCard != null) modernControlsCard.BackColor = DarkTheme.Surface;

            // DarkTheme.Apply() also styles normal TextBox controls globally.
            // The compact scene search is intentionally a borderless editor
            // inside its single bordered container, so restore that state here.
            if (sceneSearchTextBox != null)
            {
                sceneSearchTextBox.BorderStyle = BorderStyle.None;
                sceneSearchTextBox.BackColor = DarkTheme.Input;
                sceneSearchTextBox.ForeColor = DarkTheme.Text;
                sceneSearchTextBox.AutoSize = true;
                PositionSearchTextBox();
            }

            if (sceneSearchBar != null)
            {
                sceneSearchBar.BackColor = DarkTheme.Input;
                sceneSearchBar.BorderStyle = BorderStyle.None;
                sceneSearchBar.Invalidate();
            }
        }

        private void PositionSearchTextBox()
        {
            if (sceneSearchBar == null || sceneSearchTextBox == null || sceneSearchClearButton == null)
                return;

            int textLeft = 19;
            int textWidth = Math.Max(10, sceneSearchBar.ClientSize.Width - textLeft - sceneSearchClearButton.Width - 3);
            int textHeight = Math.Max(18, sceneSearchTextBox.PreferredHeight);
            sceneSearchTextBox.SetBounds(textLeft, Math.Max(0, (sceneSearchBar.ClientSize.Height - textHeight) / 2), textWidth, textHeight);
        }

        private int GetBottomControlDeckMinHeight()
        {
            const int deckPadding = 12; // top+bottom margin around the card
            const int safety = 6;       // border/layout rounding/DPI safety margin
            const int tabHeader = 30;   // utility tab control header row ("Controls"/"Console")

            int objectHeight = objectMove != null ? objectMove.Height : 0;
            int cameraHeight = cameraMove != null ? cameraMove.Height : 0;
            int controlHeight = Math.Max(objectHeight, cameraHeight);

            // The returned value is the minimum height of SplitContainer.Panel2,
            // not the card itself, so the outer panel padding is included here.
            // The utility tab header only exists when the deck lives in its page.
            int extra = controlsTab != null ? tabHeader : 0;
            return Math.Max(132 + extra, controlHeight + deckPadding + safety + extra);
        }

        private void MainForm_ModernResize(object sender, EventArgs e)
        {
            if (!modernUiReady)
                return;

            int left = (int)(ClientSize.Width * 0.285f);
            left = Math.Max(320, Math.Min(460, left));
            if (splitContainerMain.Width > left + splitContainerMain.Panel2MinSize)
                splitContainerMain.SplitterDistance = left;

            int leftHeight = splitContainerLeft.Height;
            int leftTop = Math.Max(220, Math.Min(leftHeight - splitContainerLeft.Panel2MinSize, (int)(leftHeight * 0.52f)));
            if (leftHeight > splitContainerLeft.Panel1MinSize + splitContainerLeft.Panel2MinSize)
                splitContainerLeft.SplitterDistance = leftTop;

            // Keep the two legacy control decks side-by-side at all times.
            // They are fixed-size controls (ObjectMove = 430px, CameraMove = 250px),
            // so the modern form reserves enough width instead of stacking them.
            // IMPORTANT: calculate the required bottom-panel height from the
            // ACTUAL runtime control sizes. A fixed 140px value clips the bottom
            // rows on scaled Windows displays (especially 125%/150% DPI).
            // Keep both controls side-by-side; only give the deck more vertical
            // room when their real height requires it.
            int wideBottomDeck = GetBottomControlDeckMinHeight();
            splitContainerRight.Panel2MinSize = wideBottomDeck;

            // Do not allow the main window to become narrower than the horizontal
            // control deck can physically display. This prevents overlap/clipping
            // while preserving the requested side-by-side layout.
            int requiredClientWidth = splitContainerMain.Panel1MinSize
                + splitContainerMain.Panel2MinSize
                + splitContainerMain.SplitterWidth
                + 8;
            if (MinimumSize.Width < requiredClientWidth)
            {
                MinimumSize = new Size(requiredClientWidth, Math.Max(MinimumSize.Height, 620));
            }
            if (ClientSize.Width < requiredClientWidth)
            {
                Width = requiredClientWidth + (Width - ClientSize.Width);
            }

            // Panel2 is the fixed bottom deck. Always reserve its full required
            // height; otherwise SplitterDistance can leave the controls partially
            // outside the visible client area after a resize.
            int availableRightHeight = splitContainerRight.Height;
            int minimumRightHeight = splitContainerRight.Panel1MinSize
                + wideBottomDeck
                + splitContainerRight.SplitterWidth;

            if (availableRightHeight >= minimumRightHeight)
                splitContainerRight.SplitterDistance = availableRightHeight - wideBottomDeck;

            PositionSearchTextBox();

            if (modernControlsCard != null)
            {
                // Always horizontal: ObjectMove on the left, CameraMove on the right.
                // The form minimum width guarantees these fixed-size controls have room.
                objectMove.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                cameraMove.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                objectMove.Left = 4;
                cameraMove.Left = Math.Max(4, modernControlsCard.ClientSize.Width - cameraMove.Width - 4);

                // Vertically center, clamped so a control can never hang past
                // the card's bottom border even if fonts/DPI inflate its height.
                int objTop = (modernControlsCard.ClientSize.Height - objectMove.Height) / 2;
                if (objTop < 0) objTop = 0;
                if (objTop > modernControlsCard.ClientSize.Height - objectMove.Height) objTop = Math.Max(0, modernControlsCard.ClientSize.Height - objectMove.Height);
                objectMove.Top = Math.Max(0, Math.Min(objTop, Math.Max(0, modernControlsCard.ClientSize.Height - objectMove.Height)));

                int camTop = (modernControlsCard.ClientSize.Height - cameraMove.Height) / 2;
                camTop = Math.Max(0, Math.Min(camTop, Math.Max(0, modernControlsCard.ClientSize.Height - cameraMove.Height)));
                cameraMove.Top = camTop;
            }
        }

        private void ModernTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateInspectorHeader();
            UpdateSceneObjectCount();
        }

        private void UpdateInspectorHeader()
        {
            if (inspectorSelectionLabel == null || treeViewObjs == null)
                return;

            int selectedCount = treeViewObjs.SelectedNodes == null ? 0 : treeViewObjs.SelectedNodes.Count;
            if (selectedCount > 1)
            {
                inspectorSelectionLabel.Text = "INSPECTOR  •  " + selectedCount + " selected";
            }
            else
            {
                inspectorSelectionLabel.Text = "INSPECTOR";
            }

            // The shell should not waste the inspector area on the legacy
            // welcome/info object when there is no selection. The editor's
            // selection/property logic remains unchanged elsewhere.
            if (selectedCount == 0 && propertyGridObjs != null)
            {
                propertyGridObjs.SelectedObject = null;
            }
        }

        private void UpdateSceneObjectCount()
        {
            // Intentionally empty. The old permanent object-count label was
            // removed to keep the Scene header compact.
        }

        private int CountAllNodes(TreeNodeCollection nodes)
        {
            int count = 0;
            foreach (TreeNode node in nodes)
            {
                count++;
                count += CountAllNodes(node.Nodes);
            }
            return count;
        }

        private void ModernTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (sceneNodeToolTip == null || treeViewObjs == null)
                return;

            TreeNode node = treeViewObjs.GetNodeAt(e.Location);
            if (node == null || node == sceneTooltipNode)
                return;

            sceneTooltipNode = node;
            string text = node.Text ?? string.Empty;
            Font font = node.NodeFont ?? treeViewObjs.Font;
            int textWidth = TextRenderer.MeasureText(text, font).Width;
            int available = Math.Max(0, treeViewObjs.ClientSize.Width - node.Bounds.Left - 12);
            if (textWidth > available)
                sceneNodeToolTip.SetToolTip(treeViewObjs, text);
            else
                sceneNodeToolTip.SetToolTip(treeViewObjs, string.Empty);
        }

        private void ModernTree_MouseLeave(object sender, EventArgs e)
        {
            sceneTooltipNode = null;
            if (sceneNodeToolTip != null && treeViewObjs != null)
                sceneNodeToolTip.SetToolTip(treeViewObjs, string.Empty);
        }

        private void SceneSearchTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdateSceneSearch(sceneSearchTextBox == null ? "" : sceneSearchTextBox.Text, false);
        }

        private void SceneSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Keep keyboard behavior identical to a normal Windows text search box:
            // typing stays in the edit control, Ctrl+A selects all, Enter advances,
            // Escape clears. The tree never steals focus while the user is typing.
            if (e.Control && e.KeyCode == Keys.A)
            {
                sceneSearchTextBox.SelectAll();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (currentSearchMatches == null || currentSearchMatches.Count == 0)
                    UpdateSceneSearch(sceneSearchTextBox.Text, true);
                else
                    SelectNextSearchMatch();

                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                sceneSearchTextBox.Clear();
                e.SuppressKeyPress = true;
            }
        }

        private void SceneSearchClearButton_Click(object sender, EventArgs e)
        {
            if (sceneSearchTextBox != null)
            {
                sceneSearchTextBox.Clear();
                sceneSearchTextBox.Focus();
                sceneSearchTextBox.SelectionStart = 0;
            }
        }

        private void UpdateSceneSearch(string query)
        {
            UpdateSceneSearch(query, false);
        }

        private void UpdateSceneSearch(string query, bool forceFocus)
        {
            if (treeViewObjs == null)
                return;

            string needle = (query ?? "").Trim();

            if (needle.Length == 0)
            {
                RestoreTreeExpansionState();
                currentSearchMatches = new System.Collections.Generic.List<TreeNode>();
                currentSearchMatchIndex = -1;
                return;
            }

            if (expandedNodesBeforeSearch == null)
                CaptureTreeExpansionState();

            currentSearchMatches = new System.Collections.Generic.List<TreeNode>();
            foreach (TreeNode root in treeViewObjs.Nodes)
                FindAndOpenMatches(root, needle, currentSearchMatches);

            if (currentSearchMatches.Count == 0)
            {
                if (sceneSearchTextBox != null && !sceneSearchTextBox.IsDisposed)
                    sceneSearchTextBox.Focus();
                return;
            }

            currentSearchMatchIndex = 0;
            SelectSearchMatch(currentSearchMatches[0]);
        }

        private void SelectNextSearchMatch()
        {
            if (currentSearchMatches == null || currentSearchMatches.Count == 0)
                return;

            int next = currentSearchMatchIndex + 1;
            if (next >= currentSearchMatches.Count)
                next = 0;

            currentSearchMatchIndex = next;
            SelectSearchMatch(currentSearchMatches[currentSearchMatchIndex]);
        }

        private void SelectSearchMatch(TreeNode node)
        {
            if (node == null || treeViewObjs == null)
                return;

            // Selection/scrolling is allowed to update the editor, but focus must
            // immediately return to the search box so the next character is typed
            // normally (no click/refocus between characters).
            node.EnsureVisible();
            treeViewObjs.SelectedNode = node;

            if (sceneSearchTextBox != null && !sceneSearchTextBox.IsDisposed)
            {
                sceneSearchTextBox.Focus();
                sceneSearchTextBox.SelectionStart = sceneSearchTextBox.TextLength;
                sceneSearchTextBox.SelectionLength = 0;
            }
        }

        private void FindAndOpenMatches(TreeNode node, string needle, System.Collections.Generic.List<TreeNode> matches)
        {
            bool selfMatch = GetSceneNodeSearchText(node).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            bool childMatch = false;

            foreach (TreeNode child in node.Nodes)
            {
                int before = matches.Count;
                FindAndOpenMatches(child, needle, matches);
                if (matches.Count > before)
                    childMatch = true;
            }

            if (selfMatch)
                matches.Add(node);

            if (selfMatch || childMatch)
                node.Expand();
            else if (node.Parent != null)
                node.Collapse();
        }

        private void CaptureTreeExpansionState()
        {
            expandedNodesBeforeSearch = new System.Collections.Generic.HashSet<TreeNode>();
            foreach (TreeNode root in treeViewObjs.Nodes)
                CaptureExpandedNodes(root);
        }

        private void CaptureExpandedNodes(TreeNode node)
        {
            if (node.IsExpanded)
                expandedNodesBeforeSearch.Add(node);
            foreach (TreeNode child in node.Nodes)
                CaptureExpandedNodes(child);
        }

        private void RestoreTreeExpansionState()
        {
            if (expandedNodesBeforeSearch == null)
                return;

            foreach (TreeNode root in treeViewObjs.Nodes)
                RestoreExpandedNodes(root);

            expandedNodesBeforeSearch = null;
        }

        private void RestoreExpandedNodes(TreeNode node)
        {
            if (expandedNodesBeforeSearch.Contains(node))
                node.Expand();
            else
                node.Collapse();

            foreach (TreeNode child in node.Nodes)
                RestoreExpandedNodes(child);
        }

        private string GetSceneNodeSearchText(TreeNode node)
        {
            if (node == null)
                return string.Empty;

            if (node is NsMultiselectTreeView.IAltNode alt && !string.IsNullOrEmpty(alt.AltText))
                return alt.AltText;

            return node.Text ?? string.Empty;
        }


    }

    internal sealed class ModernSearchTextBox : TextBox
    {
        private const int WS_BORDER = 0x00800000;
        private const int WS_EX_CLIENTEDGE = 0x00000200;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style &= ~WS_BORDER;
                cp.ExStyle &= ~WS_EX_CLIENTEDGE;
                return cp;
            }
        }
    }

    internal sealed class ModernCardPanel : Panel
    {
        public int Radius { get; set; }
        public Color BorderColor { get; set; }

        public ModernCardPanel()
        {
            Radius = 8;
            BorderColor = UiTheme.IsLight
                ? Color.FromArgb(214, 220, 228)
                : Color.FromArgb(45, 50, 58);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // border follows the ACTIVE theme so live switches repaint correctly
            Color bc = UiTheme.IsLight
                ? Color.FromArgb(214, 220, 228)
                : Color.FromArgb(45, 50, 58);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Rounded(rect, Radius))
            using (var pen = new Pen(bc, 1f))
                e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
