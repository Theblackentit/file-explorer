using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Text.Json;

namespace LauncherExplorer;

public class MainForm : Form
{
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "launcher_config.json");
    private AppConfig _config = new();

    private readonly SidebarListBox _shortcutList = new();
    private readonly TreeView _drivesTree = new();

    private readonly TabControl _mainTabs = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _homeFlow = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 12, 20, 20), WrapContents = false, FlowDirection = FlowDirection.TopDown };

    private readonly ListView _libraryView = new() { Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None };
    private readonly ImageList _smallIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(20, 20) };
    private readonly ImageList _largeIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(88, 88) };

    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 28, 44), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _pathLabel = new() { Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 7, 8, 0), ForeColor = Color.Gainsboro };
    private readonly ComboBox _layoutCombo = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _zoomBar = new() { Width = 170, Minimum = 72, Maximum = 170, TickStyle = TickStyle.None, Value = 96 };

    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public MainForm()
    {
        Text = "Launcher Explorer X";
        Width = 1650;
        Height = 950;
        MinimumSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        BuildUi();
        LoadConfig();
        ApplyTheme();
        LoadSidebar();
        LoadHome();
        LoadDirectory(_currentPath);

        FormClosing += (_, _) => SaveConfig();
    }

    private void BuildUi()
    {
        var titleBar = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            Padding = new Padding(18, 14, 18, 14),
            ColorA = Color.FromArgb(6, 10, 22),
            ColorB = Color.FromArgb(22, 36, 66),
            Angle = 20f
        };

        var titleWrap = new Panel { Dock = DockStyle.Left, Width = 440 };
        titleWrap.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Launcher File Explorer",
            Font = new Font("Segoe UI Semibold", 22f),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        });
        titleBar.Controls.Add(titleWrap);

        var subtitle = new Label
        {
            Dock = DockStyle.Left,
            Width = 300,
            Text = "Anime launcher vibe • custom themes • rich cards",
            ForeColor = Color.FromArgb(205, 224, 255),
            TextAlign = ContentAlignment.MiddleLeft
        };
        titleBar.Controls.Add(subtitle);

        var actionStrip = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
        actionStrip.Controls.Add(MakeTopButton("+ Add Section", (_, _) => AddPosterDialog()));
        actionStrip.Controls.Add(MakeTopButton("Theme Studio", (_, _) => ThemeDialog()));
        actionStrip.Controls.Add(MakeTopButton("Refresh", (_, _) => { LoadSidebar(); LoadHome(); LoadDirectory(_currentPath); }));
        titleBar.Controls.Add(actionStrip);

        Controls.Add(titleBar);

        var shell = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 290,
            BackColor = Color.FromArgb(10, 16, 28),
            IsSplitterFixed = false,
            BorderStyle = BorderStyle.None
        };
        Controls.Add(shell);

        shell.Panel1.Padding = new Padding(12);
        shell.Panel2.Padding = new Padding(0, 8, 8, 8);

        var side = BuildSidebar();
        shell.Panel1.Controls.Add(side);

        _mainTabs.Appearance = TabAppearance.Normal;
        _mainTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _mainTabs.ItemSize = new Size(120, 32);
        _mainTabs.DrawItem += DrawTabs;
        _mainTabs.Controls.Add(new TabPage("Home") { BackColor = Color.FromArgb(8, 14, 24) });
        _mainTabs.Controls.Add(new TabPage("Library") { BackColor = Color.FromArgb(8, 14, 24) });

        _mainTabs.TabPages[0].Controls.Add(BuildHomePanel());
        _mainTabs.TabPages[1].Controls.Add(BuildLibraryPanel());
        shell.Panel2.Controls.Add(_mainTabs);

        _shortcutList.DoubleClick += (_, _) => OpenShortcut();
        _drivesTree.NodeMouseDoubleClick += (_, e) =>
        {
            if (Directory.Exists(e.Node.Text))
            {
                _mainTabs.SelectedIndex = 1;
                LoadDirectory(e.Node.Text);
            }
        };

        _libraryView.DoubleClick += (_, _) => OpenSelectedInLibrary();
        _libraryView.MouseUp += LibraryRightClick;
        _libraryView.SelectedIndexChanged += (_, _) => UpdatePreview();

        _layoutCombo.Items.AddRange(["List", "Tiles", "Icons"]);
        _layoutCombo.SelectedIndex = 0;
        _layoutCombo.SelectedIndexChanged += (_, _) => ApplyLayout();
        _zoomBar.ValueChanged += (_, _) => ApplyLayout();
    }

    private Panel BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(6, 10, 20), Padding = new Padding(8) };
        panel.Controls.Add(new Label
        {
            Text = "Drives",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 12f),
            Padding = new Padding(2, 8, 0, 0)
        });

        _drivesTree.Dock = DockStyle.Fill;
        _drivesTree.BorderStyle = BorderStyle.None;
        _drivesTree.HideSelection = false;
        _drivesTree.Indent = 18;
        _drivesTree.ItemHeight = 24;

        panel.Controls.Add(_drivesTree);
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
        panel.Controls.Add(new Label
        {
            Text = "Shortcuts",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 12f),
            Padding = new Padding(2, 8, 0, 0)
        });

        _shortcutList.Dock = DockStyle.Top;
        _shortcutList.Height = 230;
        panel.Controls.Add(_shortcutList);
        return panel;
    }

    private Control BuildHomePanel()
    {
        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 10, 10), BackColor = Color.Transparent };

        var viewBar = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColorA = Color.FromArgb(22, 32, 52),
            ColorB = Color.FromArgb(8, 14, 24),
            Angle = 0f,
            Padding = new Padding(10, 8, 10, 8)
        };

        var homeBtn = MakeTopButton("Home", (_, _) => _mainTabs.SelectedIndex = 0);
        var libBtn = MakeTopButton("Library", (_, _) => _mainTabs.SelectedIndex = 1);
        homeBtn.Width = 90;
        libBtn.Width = 90;

        viewBar.Controls.Add(libBtn);
        viewBar.Controls.Add(homeBtn);
        libBtn.Dock = DockStyle.Left;
        homeBtn.Dock = DockStyle.Left;

        wrap.Controls.Add(_homeFlow);
        wrap.Controls.Add(viewBar);
        return wrap;
    }

    private Control BuildLibraryPanel()
    {
        var layout = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 980, BackColor = Color.Transparent };

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 8), BackColor = Color.FromArgb(9, 16, 28) };
        var top = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColorA = Color.FromArgb(22, 32, 52),
            ColorB = Color.FromArgb(8, 14, 24),
            Angle = 0f,
            Padding = new Padding(8)
        };

        var labelView = new Label { Text = "Layout:", Width = 56, ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Left };
        var labelZoom = new Label { Text = "Scale:", Width = 50, ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Left };
        _layoutCombo.Dock = DockStyle.Left;
        _zoomBar.Dock = DockStyle.Left;

        top.Controls.Add(_zoomBar);
        top.Controls.Add(labelZoom);
        top.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 16 });
        top.Controls.Add(_layoutCombo);
        top.Controls.Add(labelView);

        _pathLabel.BackColor = Color.FromArgb(11, 20, 34);

        _libraryView.SmallImageList = _smallIcons;
        _libraryView.LargeImageList = _largeIcons;

        left.Controls.Add(_libraryView);
        left.Controls.Add(top);
        left.Controls.Add(_pathLabel);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(10, 16, 28) };
        right.Controls.Add(_preview);

        layout.Panel1.Controls.Add(left);
        layout.Panel2.Controls.Add(right);
        return layout;
    }

    private Button MakeTopButton(string text, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Height = 36,
            Width = 126,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(26, 45, 79),
            Font = new Font("Segoe UI Semibold", 10f)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += onClick;
        return b;
    }

    private void DrawTabs(object? sender, DrawItemEventArgs e)
    {
        var isSel = e.Index == _mainTabs.SelectedIndex;
        using var back = new SolidBrush(isSel ? Color.FromArgb(38, 60, 96) : Color.FromArgb(16, 24, 40));
        using var fore = new SolidBrush(Color.White);
        e.Graphics.FillRectangle(back, e.Bounds);
        var text = _mainTabs.TabPages[e.Index].Text;
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(text, new Font("Segoe UI Semibold", 9.5f), fore, e.Bounds, sf);
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath)) return;
        try
        {
            _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath)) ?? new AppConfig();
        }
        catch
        {
            _config = new AppConfig();
        }
    }

    private void SaveConfig()
    {
        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ApplyTheme()
    {
        Color Parse(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        var bg = Parse(_config.Theme.Background, Color.FromArgb(8, 14, 24));
        var surface = Parse(_config.Theme.Surface, Color.FromArgb(12, 20, 34));
        var accent = Parse(_config.Theme.Accent, Color.FromArgb(70, 145, 255));

        BackColor = bg;
        _homeFlow.BackColor = Color.FromArgb(Math.Max(bg.R - 3, 0), Math.Max(bg.G - 3, 0), Math.Max(bg.B - 3, 0));
        _shortcutList.BackColor = surface;
        _shortcutList.ForeColor = Color.White;
        _drivesTree.BackColor = surface;
        _drivesTree.ForeColor = Color.WhiteSmoke;
        _libraryView.BackColor = Color.FromArgb(13, 20, 34);
        _libraryView.ForeColor = Color.White;
        _pathLabel.ForeColor = Color.Gainsboro;

        Paint -= PaintGradient;
        Paint += PaintGradient;

        void PaintGradient(object? _, PaintEventArgs e)
        {
            using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(8, 14, 24), Color.FromArgb(accent.R / 2, accent.G / 2, accent.B / 2), 135f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        Invalidate();
    }

    private void LoadSidebar()
    {
        _shortcutList.Items.Clear();
        foreach (var s in _config.Sections.Where(s => s.Placement == "shortcut"))
            _shortcutList.Items.Add(s.Name);

        _drivesTree.Nodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            _drivesTree.Nodes.Add(new TreeNode(drive.RootDirectory.FullName));
    }

    private void LoadHome()
    {
        _homeFlow.SuspendLayout();
        _homeFlow.Controls.Clear();

        var groups = _config.Sections.Where(s => s.Placement == "home").GroupBy(s => string.IsNullOrWhiteSpace(s.Group) ? "Recently Added" : s.Group);
        foreach (var group in groups)
        {
            _homeFlow.Controls.Add(new Label
            {
                Text = group.Key,
                Width = _homeFlow.Width - 60,
                Height = 42,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 21f),
                Margin = new Padding(0, 8, 0, 4)
            });

            var row = new FlowLayoutPanel
            {
                Width = _homeFlow.Width - 70,
                Height = 285,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(9, 18, 34),
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(10)
            };

            foreach (var item in group)
                row.Controls.Add(BuildPosterCard(item));

            _homeFlow.Controls.Add(row);
        }

        _homeFlow.ResumeLayout();
    }

    private Control BuildPosterCard(PosterItem item)
    {
        var card = new RoundedPanel
        {
            Width = item.Visual == "banner" ? 360 : 190,
            Height = item.Visual == "banner" ? 228 : 260,
            Radius = 16,
            FillColor = Color.FromArgb(30, 40, 58),
            Margin = new Padding(8),
            Cursor = Cursors.Hand,
            Padding = new Padding(8)
        };

        var title = new Label
        {
            Text = item.Name,
            Dock = DockStyle.Bottom,
            Height = 42,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 11f)
        };

        var subtitle = new Label
        {
            Text = item.Type == "folder" ? "[folder]" : "[app]",
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.FromArgb(180, 208, 255),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9f)
        };

        var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(20, 28, 44) };
        if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
        {
            try { picture.Image = Image.FromFile(item.ImagePath); } catch { }
        }

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        var open = new LinkLabel { Text = "Open", Dock = DockStyle.Left, Width = 60, LinkColor = Color.White, ActiveLinkColor = Color.LightBlue };
        var edit = new LinkLabel { Text = "Edit", Dock = DockStyle.Right, Width = 60, LinkColor = Color.White, ActiveLinkColor = Color.LightBlue, TextAlign = ContentAlignment.MiddleRight };

        open.Click += (_, _) => OpenPoster(item);
        edit.Click += (_, _) => EditPoster(item);

        footer.Controls.Add(open);
        footer.Controls.Add(edit);

        card.Controls.Add(picture);
        card.Controls.Add(footer);
        card.Controls.Add(subtitle);
        card.Controls.Add(title);

        card.DoubleClick += (_, _) => OpenPoster(item);
        picture.DoubleClick += (_, _) => OpenPoster(item);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenPoster(item));
        menu.Items.Add("Edit", null, (_, _) => EditPoster(item));
        menu.Items.Add("Delete", null, (_, _) =>
        {
            _config.Sections.Remove(item);
            SaveConfig();
            LoadSidebar();
            LoadHome();
        });
        card.ContextMenuStrip = menu;
        picture.ContextMenuStrip = menu;

        return card;
    }

    private void OpenPoster(PosterItem item)
    {
        if (item.Type == "folder" || Directory.Exists(item.TargetPath))
        {
            if (Directory.Exists(item.TargetPath))
            {
                _mainTabs.SelectedIndex = 1;
                LoadDirectory(item.TargetPath);
            }
            else
            {
                MessageBox.Show("Target folder does not exist.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (File.Exists(item.TargetPath))
        {
            OpenExternal(item.TargetPath);
            return;
        }

        MessageBox.Show("Target file/app does not exist.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenShortcut()
    {
        if (_shortcutList.SelectedItem is not string name) return;
        var item = _config.Sections.FirstOrDefault(s => s.Placement == "shortcut" && s.Name == name);
        if (item is not null) OpenPoster(item);
    }

    private void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        _currentPath = path;
        _pathLabel.Text = path;
        _libraryView.Items.Clear();
        _smallIcons.Images.Clear();
        _largeIcons.Images.Clear();

        IEnumerable<FileEntry> dirs = [];
        IEnumerable<FileEntry> files = [];

        try
        {
            dirs = Directory.GetDirectories(path).Select(d => new FileEntry(d, true));
            files = Directory.GetFiles(path).Select(f => new FileEntry(f, false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        foreach (var entry in dirs.Concat(files))
        {
            var (small, large) = ResolveIcon(entry.FullPath, entry.IsDirectory);
            _smallIcons.Images.Add(entry.FullPath, small);
            _largeIcons.Images.Add(entry.FullPath, large);

            var item = new ListViewItem(entry.Name) { Tag = entry.FullPath, ImageKey = entry.FullPath };
            item.SubItems.Add(entry.IsDirectory ? "Folder" : entry.Extension);
            item.SubItems.Add(entry.SizeText);
            item.SubItems.Add(entry.Modified.ToString("g"));
            _libraryView.Items.Add(item);
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var smallSize = Math.Clamp(_zoomBar.Value / 3, 18, 58);
        _smallIcons.ImageSize = new Size(smallSize, smallSize);
        _largeIcons.ImageSize = new Size(_zoomBar.Value, _zoomBar.Value);

        _libraryView.Columns.Clear();
        switch (_layoutCombo.SelectedItem?.ToString())
        {
            case "Icons":
                _libraryView.View = View.LargeIcon;
                break;
            case "Tiles":
                _libraryView.View = View.Tile;
                break;
            default:
                _libraryView.View = View.Details;
                _libraryView.Columns.Add("Name", 330);
                _libraryView.Columns.Add("Type", 120);
                _libraryView.Columns.Add("Size", 120);
                _libraryView.Columns.Add("Modified", 170);
                break;
        }
    }

    private (Image small, Image large) ResolveIcon(string path, bool isDir)
    {
        if (_config.FileIcons.TryGetValue(path, out var customPath) && File.Exists(customPath))
        {
            try
            {
                using var src = Image.FromFile(customPath);
                return (new Bitmap(src, _smallIcons.ImageSize), new Bitmap(src, _largeIcons.ImageSize));
            }
            catch { }
        }

        var color = isDir ? Color.FromArgb(250, 188, 68) : Color.FromArgb(64, 152, 255);
        Bitmap Build(Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(color);
            using var p = new GraphicsPath();
            p.AddArc(1, 1, 10, 10, 180, 90);
            p.AddArc(size.Width - 11, 1, 10, 10, 270, 90);
            p.AddArc(size.Width - 11, size.Height - 11, 10, 10, 0, 90);
            p.AddArc(1, size.Height - 11, 10, 10, 90, 90);
            p.CloseFigure();
            g.FillPath(b, p);
            return bmp;
        }

        return (Build(_smallIcons.ImageSize), Build(_largeIcons.ImageSize));
    }

    private void UpdatePreview()
    {
        if (_libraryView.SelectedItems.Count == 0) return;
        var path = _libraryView.SelectedItems[0].Tag?.ToString() ?? "";
        if (File.Exists(path) && IsImage(path))
        {
            try { _preview.Image = Image.FromFile(path); return; } catch { }
        }

        _preview.Image = null;
    }

    private static bool IsImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    }

    private void OpenSelectedInLibrary()
    {
        if (_libraryView.SelectedItems.Count == 0) return;
        var path = _libraryView.SelectedItems[0].Tag?.ToString() ?? "";

        if (Directory.Exists(path))
        {
            LoadDirectory(path);
            return;
        }

        if (File.Exists(path)) OpenExternal(path);
    }

    private static void OpenExternal(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void LibraryRightClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _libraryView.GetItemAt(e.X, e.Y);
        if (hit is null) return;
        hit.Selected = true;

        var path = hit.Tag?.ToString() ?? "";
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenSelectedInLibrary());
        menu.Items.Add("Rename", null, (_, _) => RenamePath(path));
        menu.Items.Add("Compress (.zip)", null, (_, _) => CompressPath(path));
        menu.Items.Add("Delete", null, (_, _) => DeletePath(path));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Change Icon Image", null, (_, _) => ChangeIcon(path));
        menu.Items.Add("Remove Custom Icon", null, (_, _) => { _config.FileIcons.Remove(path); SaveConfig(); LoadDirectory(_currentPath); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Properties", null, (_, _) => MessageBox.Show(path, "Path", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Show(_libraryView, e.Location);
    }

    private void RenamePath(string path)
    {
        var form = new InputDialog("Rename", "New name:", Path.GetFileName(path));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var newName = form.Value.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        var parent = Directory.GetParent(path)?.FullName;
        if (string.IsNullOrWhiteSpace(parent)) return;
        var newPath = Path.Combine(parent, newName);

        try
        {
            if (Directory.Exists(path)) Directory.Move(path, newPath);
            else if (File.Exists(path)) File.Move(path, newPath);
            if (_config.FileIcons.Remove(path, out var iconPath)) _config.FileIcons[newPath] = iconPath;
            SaveConfig();
            LoadDirectory(_currentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CompressPath(string path)
    {
        var zip = path.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        try
        {
            if (Directory.Exists(path)) ZipFile.CreateFromDirectory(path, zip);
            else
            {
                using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(path, Path.GetFileName(path));
            }
            LoadDirectory(_currentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Compress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeletePath(string path)
    {
        if (MessageBox.Show($"Delete '{Path.GetFileName(path)}'?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else if (File.Exists(path)) File.Delete(path);
            _config.FileIcons.Remove(path);
            SaveConfig();
            LoadDirectory(_currentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ChangeIcon(string path)
    {
        using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        _config.FileIcons[path] = ofd.FileName;
        SaveConfig();
        LoadDirectory(_currentPath);
    }

    private void AddPosterDialog()
    {
        using var editor = new PosterEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Item is null) return;

        _config.Sections.Add(editor.Item);
        SaveConfig();
        LoadSidebar();
        LoadHome();
    }

    private void EditPoster(PosterItem item)
    {
        using var editor = new PosterEditorForm(item);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Item is null) return;

        item.Name = editor.Item.Name;
        item.TargetPath = editor.Item.TargetPath;
        item.Type = editor.Item.Type;
        item.Visual = editor.Item.Visual;
        item.Group = editor.Item.Group;
        item.Placement = editor.Item.Placement;
        item.ImagePath = editor.Item.ImagePath;

        SaveConfig();
        LoadSidebar();
        LoadHome();
    }

    private void ThemeDialog()
    {
        using var editor = new ThemeEditorForm(_config.Theme);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Theme is null) return;

        _config.Theme = editor.Theme;
        SaveConfig();
        ApplyTheme();
        LoadHome();
    }

    private sealed record FileEntry(string FullPath, bool IsDirectory)
    {
        public string Name => Path.GetFileName(FullPath);
        public string Extension => IsDirectory ? "Folder" : Path.GetExtension(FullPath).Trim('.').ToUpperInvariant();
        public DateTime Modified => IsDirectory ? Directory.GetLastWriteTime(FullPath) : File.GetLastWriteTime(FullPath);

        public string SizeText
        {
            get
            {
                if (IsDirectory) return "—";
                var bytes = new FileInfo(FullPath).Length;
                if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1} GB";
                if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1} MB";
                if (bytes >= 1_000) return $"{bytes / 1_000.0:F1} KB";
                return $"{bytes} B";
            }
        }
    }
}

public sealed class PosterEditorForm : Form
{
    public PosterItem? Item { get; private set; }

    private readonly TextBox _nameBox = new();
    private readonly TextBox _targetBox = new();
    private readonly TextBox _groupBox = new();
    private readonly ComboBox _typeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _visualBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _placementBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _imagePathLabel = new() { ForeColor = Color.Gainsboro, AutoEllipsis = true, Dock = DockStyle.Fill };

    private string? _imagePath;

    public PosterEditorForm(PosterItem? source = null)
    {
        Text = source is null ? "Add Section" : "Edit Section";
        Width = 580;
        Height = 510;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(14, 20, 32);
        ForeColor = Color.White;

        var value = source is null ? new PosterItem() : new PosterItem
        {
            Name = source.Name,
            TargetPath = source.TargetPath,
            Type = source.Type,
            Visual = source.Visual,
            Group = source.Group,
            Placement = source.Placement,
            ImagePath = source.ImagePath
        };

        _nameBox.Text = value.Name;
        _targetBox.Text = value.TargetPath;
        _groupBox.Text = value.Group;
        _typeBox.Items.AddRange(["app", "folder"]);
        _visualBox.Items.AddRange(["poster", "banner"]);
        _placementBox.Items.AddRange(["home", "shortcut"]);
        _typeBox.SelectedItem = value.Type;
        _visualBox.SelectedItem = value.Visual;
        _placementBox.SelectedItem = value.Placement;
        _imagePath = value.ImagePath;
        _imagePathLabel.Text = string.IsNullOrWhiteSpace(_imagePath) ? "No image selected" : _imagePath;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            Padding = new Padding(14),
            BackColor = Color.Transparent
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        AddRow(table, 0, "Name", _nameBox, null);
        AddRow(table, 1, "Target", _targetBox, BuildTargetButtons());
        AddRow(table, 2, "Group", _groupBox, null);
        AddRow(table, 3, "Type", _typeBox, null);
        AddRow(table, 4, "Visual", _visualBox, null);
        AddRow(table, 5, "Placement", _placementBox, null);
        AddRow(table, 6, "Image", _imagePathLabel, BuildImageButton());

        var save = new Button { Text = "Save", Dock = DockStyle.Fill, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 64, 105), ForeColor = Color.White };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => SaveAndClose();
        table.Controls.Add(save, 0, 7);
        table.SetColumnSpan(save, 3);

        Controls.Add(table);
    }

    private Control BuildTargetButtons()
    {
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var file = new Button { Text = "File", Width = 52, FlatStyle = FlatStyle.Flat };
        var folder = new Button { Text = "Folder", Width = 58, FlatStyle = FlatStyle.Flat };

        foreach (var b in new[] { file, folder })
        {
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(26, 45, 79);
            b.ForeColor = Color.White;
        }

        file.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog();
            if (ofd.ShowDialog(this) == DialogResult.OK) _targetBox.Text = ofd.FileName;
        };
        folder.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog(this) == DialogResult.OK) _targetBox.Text = fbd.SelectedPath;
        };

        wrap.Controls.Add(file);
        wrap.Controls.Add(folder);
        return wrap;
    }

    private Control BuildImageButton()
    {
        var button = new Button { Text = "Pick", Width = 86, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _imagePath = ofd.FileName;
                _imagePathLabel.Text = _imagePath;
            }
        };
        return button;
    }

    private void AddRow(TableLayoutPanel table, int row, string label, Control field, Control? action)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gainsboro
        }, 0, row);

        field.Dock = DockStyle.Fill;
        table.Controls.Add(field, 1, row);
        table.Controls.Add(action ?? new Panel(), 2, row);
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_targetBox.Text))
        {
            MessageBox.Show("Please pick a target file or folder.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Item = new PosterItem
        {
            Name = _nameBox.Text.Trim(),
            TargetPath = _targetBox.Text.Trim(),
            Group = _groupBox.Text.Trim(),
            Type = _typeBox.SelectedItem?.ToString() ?? "app",
            Visual = _visualBox.SelectedItem?.ToString() ?? "poster",
            Placement = _placementBox.SelectedItem?.ToString() ?? "home",
            ImagePath = _imagePath
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}

public sealed class ThemeEditorForm : Form
{
    public ThemeConfig? Theme { get; private set; }

    private readonly TextBox _name = new();
    private readonly TextBox _accent = new();
    private readonly TextBox _surface = new();
    private readonly TextBox _background = new();

    public ThemeEditorForm(ThemeConfig current)
    {
        Text = "Theme Studio";
        Width = 540;
        Height = 380;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(14, 20, 32);
        ForeColor = Color.White;

        _name.Text = current.Name;
        _accent.Text = current.Accent;
        _surface.Text = current.Surface;
        _background.Text = current.Background;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(14) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        AddColorRow(table, 0, "Theme", _name, null);
        AddColorRow(table, 1, "Accent", _accent, () => PickColor(_accent));
        AddColorRow(table, 2, "Surface", _surface, () => PickColor(_surface));
        AddColorRow(table, 3, "Background", _background, () => PickColor(_background));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        var save = new Button { Text = "Save Theme", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 64, 105), ForeColor = Color.White };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) =>
        {
            Theme = new ThemeConfig { Name = _name.Text.Trim(), Accent = _accent.Text.Trim(), Surface = _surface.Text.Trim(), Background = _background.Text.Trim(), BackgroundImage = current.BackgroundImage };
            DialogResult = DialogResult.OK;
            Close();
        };
        table.Controls.Add(save, 0, 4);
        table.SetColumnSpan(save, 3);

        Controls.Add(table);
    }

    private void AddColorRow(TableLayoutPanel table, int row, string label, TextBox box, Action? picker)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gainsboro }, 0, row);

        box.Dock = DockStyle.Fill;
        table.Controls.Add(box, 1, row);

        if (picker is null)
        {
            table.Controls.Add(new Panel(), 2, row);
        }
        else
        {
            var button = new Button { Text = "Pick", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => picker();
            table.Controls.Add(button, 2, row);
        }
    }

    private static void PickColor(TextBox target)
    {
        using var dlg = new ColorDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }
}

public sealed class InputDialog : Form
{
    public string Value => _box.Text;
    private readonly TextBox _box = new();

    public InputDialog(string title, string label, string value)
    {
        Text = title;
        Width = 380;
        Height = 170;
        StartPosition = FormStartPosition.CenterParent;

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(10) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _box.Text = value;
        _box.Dock = DockStyle.Fill;
        panel.Controls.Add(_box, 0, 1);

        var ok = new Button { Text = "OK", Dock = DockStyle.Right, Width = 84 };
        ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        panel.Controls.Add(ok, 0, 2);

        Controls.Add(panel);
    }
}

public sealed class SidebarListBox : ListBox
{
    public SidebarListBox()
    {
        BorderStyle = BorderStyle.None;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 28;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= Items.Count) return;

        var isSel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var b = new SolidBrush(isSel ? Color.FromArgb(40, 65, 108) : BackColor);
        e.Graphics.FillRectangle(b, e.Bounds);

        using var fore = new SolidBrush(ForeColor);
        var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
        e.Graphics.DrawString(Items[e.Index].ToString(), Font, fore, textRect, new StringFormat { LineAlignment = StringAlignment.Center });
        e.DrawFocusRectangle();
    }
}

public sealed class GradientPanel : Panel
{
    public Color ColorA { get; set; } = Color.Black;
    public Color ColorB { get; set; } = Color.Gray;
    public float Angle { get; set; } = 90f;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle, ColorA, ColorB, Angle);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

public sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 10;
    public Color FillColor { get; set; } = Color.FromArgb(30, 40, 58);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        var r = Radius;
        path.AddArc(0, 0, r, r, 180, 90);
        path.AddArc(Width - r - 1, 0, r, r, 270, 90);
        path.AddArc(Width - r - 1, Height - r - 1, r, r, 0, 90);
        path.AddArc(0, Height - r - 1, r, r, 90, 90);
        path.CloseFigure();

        using var b = new SolidBrush(FillColor);
        e.Graphics.FillPath(b, path);
    }
}
