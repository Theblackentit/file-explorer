using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Text.Json;

namespace LauncherExplorer;

public class MainForm : Form
{
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "launcher_config.json");
    private AppConfig _config = new();

    private readonly ListBox _shortcutList = new();
    private readonly ListBox _driveList = new();

    private readonly Panel _homePanel = new() { Dock = DockStyle.Fill };
    private readonly Panel _libraryPanel = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly FlowLayoutPanel _homeFlow = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10) };

    private readonly ListView _libraryView = new() { Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None };
    private readonly ImageList _smallIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(20, 20) };
    private readonly ImageList _largeIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(92, 92) };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 30, 46), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _pathLabel = new() { Dock = DockStyle.Top, Height = 32, ForeColor = Color.Gainsboro, Padding = new Padding(8, 8, 0, 0) };
    private readonly ComboBox _layoutCombo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _zoomBar = new() { Width = 150, Minimum = 72, Maximum = 180, Value = 100, TickStyle = TickStyle.None };

    private readonly ComboBox _sortCombo = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _homeZoomBar = new() { Width = 160, Minimum = 70, Maximum = 170, Value = 100, TickStyle = TickStyle.None };

    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public MainForm()
    {
        Text = "Launcher File Explorer";
        Width = 1680;
        Height = 950;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 780);
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
        var top = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 74,
            ColorA = Color.FromArgb(6, 12, 23),
            ColorB = Color.FromArgb(14, 31, 67),
            Angle = 0f,
            Padding = new Padding(10)
        };

        var titleWrap = new Panel { Dock = DockStyle.Left, Width = 720 };
        titleWrap.Controls.Add(new Label
        {
            Text = "Launcher File Explorer",
            Font = new Font("Segoe UI Semibold", 24f),
            ForeColor = Color.White,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Left,
            Width = 420
        });
        titleWrap.Controls.Add(new Label
        {
            Text = "Anime launcher vibe • custom themes • rich cards",
            ForeColor = Color.FromArgb(220, 230, 255),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 27, 0, 0)
        });
        top.Controls.Add(titleWrap);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.Add(MakeTopButton("+ Add Section", (_, _) => AddPosterDialog()));
        actions.Controls.Add(MakeTopButton("Map File Image", (_, _) => MapCurrentFileImage()));
        actions.Controls.Add(MakeTopButton("Theme Studio", (_, _) => ThemeDialog()));
        actions.Controls.Add(MakeTopButton("Refresh", (_, _) => { LoadSidebar(); LoadHome(); LoadDirectory(_currentPath); }));
        top.Controls.Add(actions);
        Controls.Add(top);

        var app = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 175, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(app);

        app.Panel1.Controls.Add(BuildSidebar());
        app.Panel2.Controls.Add(BuildContent());

        _shortcutList.DoubleClick += (_, _) => OpenShortcut();
        _driveList.DoubleClick += (_, _) =>
        {
            if (_driveList.SelectedItem is not string drive) return;
            OpenLibraryPath(drive);
        };

        _layoutCombo.Items.AddRange(["List", "Tiles", "Icons"]);
        _layoutCombo.SelectedIndex = 0;
        _layoutCombo.SelectedIndexChanged += (_, _) => ApplyLayout();
        _zoomBar.ValueChanged += (_, _) => ApplyLayout();

        _sortCombo.Items.AddRange(["Alphabetical (A-Z)", "Alphabetical (Z-A)"]);
        _sortCombo.SelectedIndex = 0;
        _sortCombo.SelectedIndexChanged += (_, _) => LoadHome();
        _homeZoomBar.ValueChanged += (_, _) => LoadHome();

        _libraryView.SmallImageList = _smallIcons;
        _libraryView.LargeImageList = _largeIcons;
        _libraryView.DoubleClick += (_, _) => OpenSelectedInLibrary();
        _libraryView.SelectedIndexChanged += (_, _) => UpdatePreview();
        _libraryView.MouseUp += LibraryRightClick;
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(2, 8, 18) };

        panel.Controls.Add(new Label { Text = "Drives", Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), Padding = new Padding(0, 6, 0, 0) });
        _driveList.Dock = DockStyle.Fill;
        _driveList.BackColor = Color.White;
        _driveList.ForeColor = Color.Black;
        panel.Controls.Add(_driveList);

        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14 });
        panel.Controls.Add(new Label { Text = "Shortcuts", Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), Padding = new Padding(0, 6, 0, 0) });
        _shortcutList.Dock = DockStyle.Top;
        _shortcutList.Height = 230;
        _shortcutList.BackColor = Color.White;
        _shortcutList.ForeColor = Color.Black;
        panel.Controls.Add(_shortcutList);

        return panel;
    }

    private Control BuildContent()
    {
        var container = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(7, 16, 30), Padding = new Padding(10) };

        var viewRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(5, 14, 30) };
        viewRow.Controls.Add(new Label { Text = "View:", ForeColor = Color.White, Width = 44, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft });
        var homeBtn = MakeTopButton("Home", (_, _) => SetMode(true));
        var libBtn = MakeTopButton("Library", (_, _) => SetMode(false));
        homeBtn.Width = 92;
        libBtn.Width = 92;
        homeBtn.Dock = DockStyle.Left;
        libBtn.Dock = DockStyle.Left;
        viewRow.Controls.Add(libBtn);
        viewRow.Controls.Add(homeBtn);

        container.Controls.Add(_homePanel);
        container.Controls.Add(_libraryPanel);
        container.Controls.Add(viewRow);

        BuildHomeUi();
        BuildLibraryUi();
        return container;
    }

    private void BuildHomeUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(15, 26, 44), Padding = new Padding(8, 8, 8, 8) };
        top.Controls.Add(new Label { Text = "100%", ForeColor = Color.White, Dock = DockStyle.Right, Width = 60, TextAlign = ContentAlignment.MiddleRight });
        top.Controls.Add(_homeZoomBar);
        top.Controls.Add(new Label { Text = "Zoom:", ForeColor = Color.White, Dock = DockStyle.Left, Width = 52, TextAlign = ContentAlignment.MiddleLeft });
        top.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 16 });
        top.Controls.Add(_sortCombo);
        top.Controls.Add(new Label { Text = "Sort:", ForeColor = Color.White, Dock = DockStyle.Left, Width = 42, TextAlign = ContentAlignment.MiddleLeft });

        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        wrap.Controls.Add(_homeFlow);
        wrap.Controls.Add(top);

        _homePanel.Controls.Add(wrap);
    }

    private void BuildLibraryUi()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 980 };

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 16, 30) };
        var libTop = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(15, 26, 44), Padding = new Padding(8) };
        libTop.Controls.Add(_zoomBar);
        libTop.Controls.Add(new Label { Text = "Zoom:", Dock = DockStyle.Left, Width = 48, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
        libTop.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12 });
        libTop.Controls.Add(_layoutCombo);
        libTop.Controls.Add(new Label { Text = "Layout:", Dock = DockStyle.Left, Width = 56, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });

        _pathLabel.BackColor = Color.FromArgb(10, 19, 34);
        left.Controls.Add(_libraryView);
        left.Controls.Add(libTop);
        left.Controls.Add(_pathLabel);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.FromArgb(9, 16, 30) };
        right.Controls.Add(_preview);

        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);
        _libraryPanel.Controls.Add(split);
    }

    private Button MakeTopButton(string text, EventHandler click)
    {
        var b = new Button
        {
            Text = text,
            Width = 124,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 44, 78),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10f)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private void SetMode(bool home)
    {
        _homePanel.Visible = home;
        _libraryPanel.Visible = !home;
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath)) return;
        try { _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath)) ?? new AppConfig(); }
        catch { _config = new AppConfig(); }
    }

    private void SaveConfig() => File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));

    private void ApplyTheme()
    {
        Color Parse(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        var bg = Parse(_config.Theme.Background, Color.FromArgb(5, 12, 24));
        var accent = Parse(_config.Theme.Accent, Color.FromArgb(80, 150, 255));

        BackColor = bg;
        Paint -= OnPaintGradient;
        Paint += OnPaintGradient;
        Invalidate();

        void OnPaintGradient(object? _, PaintEventArgs e)
        {
            using var br = new LinearGradientBrush(ClientRectangle, bg, Color.FromArgb(accent.R / 3, accent.G / 3, accent.B / 3), 135f);
            e.Graphics.FillRectangle(br, ClientRectangle);
        }
    }

    private void LoadSidebar()
    {
        _shortcutList.Items.Clear();
        foreach (var item in _config.Sections.Where(s => s.Placement == "shortcut")) _shortcutList.Items.Add(item.Name);

        _driveList.Items.Clear();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady)) _driveList.Items.Add(d.RootDirectory.FullName);
    }

    private void LoadHome()
    {
        _homeFlow.SuspendLayout();
        _homeFlow.Controls.Clear();

        var sections = _config.Sections.Where(s => s.Placement == "home");
        sections = _sortCombo.SelectedIndex == 1 ? sections.OrderByDescending(s => s.Name) : sections.OrderBy(s => s.Name);

        var grouped = sections.GroupBy(s => string.IsNullOrWhiteSpace(s.Group) ? "Recently Added" : s.Group);
        foreach (var group in grouped)
        {
            _homeFlow.Controls.Add(new Label
            {
                Text = group.Key,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 22f),
                Height = 44,
                Width = _homeFlow.Width - 48,
                Margin = new Padding(4, 8, 0, 4)
            });

            var zoom = _homeZoomBar.Value / 100f;
            var row = new FlowLayoutPanel
            {
                Width = _homeFlow.Width - 52,
                Height = (int)(300 * zoom),
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(25, 34, 52),
                Padding = new Padding(8),
                Margin = new Padding(4, 0, 4, 14)
            };

            foreach (var item in group) row.Controls.Add(BuildPosterCard(item, zoom));
            _homeFlow.Controls.Add(row);
        }

        _homeFlow.ResumeLayout();
    }

    private Control BuildPosterCard(PosterItem item, float zoom)
    {
        var width = item.Visual == "banner" ? (int)(390 * zoom) : (int)(220 * zoom);
        var height = item.Visual == "banner" ? (int)(248 * zoom) : (int)(300 * zoom);

        var card = new RoundedPanel { Width = width, Height = height, FillColor = Color.FromArgb(35, 44, 62), Radius = 16, Margin = new Padding(8), Padding = new Padding(8), Cursor = Cursors.Hand };
        var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(20, 28, 42) };
        if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
        {
            try { picture.Image = Image.FromFile(item.ImagePath); } catch { }
        }

        var name = new Label { Text = item.Name, Dock = DockStyle.Bottom, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 12f) };
        var type = new Label { Text = item.Type == "folder" ? "[folder]" : "[shortcut]", Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(194, 214, 250) };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        var open = new LinkLabel { Text = "Open", Dock = DockStyle.Left, Width = 60, LinkColor = Color.White };
        var edit = new LinkLabel { Text = "Edit", Dock = DockStyle.Right, Width = 60, LinkColor = Color.White, TextAlign = ContentAlignment.MiddleRight };
        open.Click += (_, _) => OpenPoster(item);
        edit.Click += (_, _) => EditPoster(item);
        footer.Controls.Add(open);
        footer.Controls.Add(edit);

        card.Controls.Add(picture);
        card.Controls.Add(footer);
        card.Controls.Add(type);
        card.Controls.Add(name);

        card.DoubleClick += (_, _) => OpenPoster(item);
        picture.DoubleClick += (_, _) => OpenPoster(item);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenPoster(item));
        menu.Items.Add("Edit", null, (_, _) => EditPoster(item));
        menu.Items.Add("Delete", null, (_, _) => { _config.Sections.Remove(item); SaveConfig(); LoadSidebar(); LoadHome(); });
        card.ContextMenuStrip = menu;
        picture.ContextMenuStrip = menu;
        return card;
    }

    private void OpenPoster(PosterItem item)
    {
        if (item.Type == "folder" || Directory.Exists(item.TargetPath))
        {
            if (Directory.Exists(item.TargetPath)) OpenLibraryPath(item.TargetPath);
            else MessageBox.Show("Folder target does not exist.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (File.Exists(item.TargetPath)) OpenExternal(item.TargetPath);
        else MessageBox.Show("App/file target does not exist.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenShortcut()
    {
        if (_shortcutList.SelectedItem is not string name) return;
        var item = _config.Sections.FirstOrDefault(s => s.Placement == "shortcut" && s.Name == name);
        if (item is not null) OpenPoster(item);
    }

    private void OpenLibraryPath(string path)
    {
        SetMode(false);
        LoadDirectory(path);
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
            MessageBox.Show(ex.Message, "Library", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        foreach (var file in dirs.Concat(files))
        {
            var icon = ResolveIcon(file.FullPath, file.IsDirectory);
            _smallIcons.Images.Add(file.FullPath, icon.small);
            _largeIcons.Images.Add(file.FullPath, icon.large);

            var item = new ListViewItem(file.Name) { Tag = file.FullPath, ImageKey = file.FullPath };
            item.SubItems.Add(file.IsDirectory ? "Folder" : file.Extension);
            item.SubItems.Add(file.SizeText);
            item.SubItems.Add(file.Modified.ToString("g"));
            _libraryView.Items.Add(item);
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        _smallIcons.ImageSize = new Size(Math.Clamp(_zoomBar.Value / 4, 18, 56), Math.Clamp(_zoomBar.Value / 4, 18, 56));
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
        if (_config.FileIcons.TryGetValue(path, out var custom) && File.Exists(custom))
        {
            try
            {
                using var src = Image.FromFile(custom);
                return (new Bitmap(src, _smallIcons.ImageSize), new Bitmap(src, _largeIcons.ImageSize));
            }
            catch { }
        }

        var color = isDir ? Color.FromArgb(245, 185, 70) : Color.FromArgb(88, 158, 255);
        Bitmap Make(Size s)
        {
            var bmp = new Bitmap(s.Width, s.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(color);
            using var path = new GraphicsPath();
            path.AddArc(1, 1, 10, 10, 180, 90);
            path.AddArc(s.Width - 11, 1, 10, 10, 270, 90);
            path.AddArc(s.Width - 11, s.Height - 11, 10, 10, 0, 90);
            path.AddArc(1, s.Height - 11, 10, 10, 90, 90);
            path.CloseFigure();
            g.FillPath(b, path);
            return bmp;
        }

        return (Make(_smallIcons.ImageSize), Make(_largeIcons.ImageSize));
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
        if (Directory.Exists(path)) LoadDirectory(path);
        else if (File.Exists(path)) OpenExternal(path);
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
        menu.Items.Add("Properties", null, (_, _) => MessageBox.Show(path, "Path", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Show(_libraryView, e.Location);
    }

    private void RenamePath(string path)
    {
        using var prompt = new InputDialog("Rename", "New name:", Path.GetFileName(path));
        if (prompt.ShowDialog(this) != DialogResult.OK) return;

        var newName = prompt.Value.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;
        var parent = Directory.GetParent(path)?.FullName;
        if (string.IsNullOrWhiteSpace(parent)) return;
        var newPath = Path.Combine(parent, newName);

        try
        {
            if (Directory.Exists(path)) Directory.Move(path, newPath);
            else if (File.Exists(path)) File.Move(path, newPath);
            if (_config.FileIcons.Remove(path, out var img)) _config.FileIcons[newPath] = img;
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
        var outZip = path.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        try
        {
            if (Directory.Exists(path)) ZipFile.CreateFromDirectory(path, outZip);
            else
            {
                using var zip = ZipFile.Open(outZip, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(path, Path.GetFileName(path));
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
        if (MessageBox.Show($"Delete '{Path.GetFileName(path)}'?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

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

    private void MapCurrentFileImage()
    {
        if (_libraryView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Select a file/folder in Library first.", "Map File Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var path = _libraryView.SelectedItems[0].Tag?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(path)) return;
        ChangeIcon(path);
    }

    private void AddPosterDialog()
    {
        using var dlg = new PosterEditorForm();
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Item is null) return;
        _config.Sections.Add(dlg.Item);
        SaveConfig();
        LoadSidebar();
        LoadHome();
    }

    private void EditPoster(PosterItem item)
    {
        using var dlg = new PosterEditorForm(item);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Item is null) return;

        item.Name = dlg.Item.Name;
        item.TargetPath = dlg.Item.TargetPath;
        item.Type = dlg.Item.Type;
        item.Visual = dlg.Item.Visual;
        item.Group = dlg.Item.Group;
        item.Placement = dlg.Item.Placement;
        item.ImagePath = dlg.Item.ImagePath;

        SaveConfig();
        LoadSidebar();
        LoadHome();
    }

    private void ThemeDialog()
    {
        using var dlg = new ThemeEditorForm(_config.Theme);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Theme is null) return;
        _config.Theme = dlg.Theme;
        SaveConfig();
        ApplyTheme();
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
        BackColor = Color.FromArgb(14, 20, 32);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

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

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(14) };
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

        var save = new Button { Text = "Save", Dock = DockStyle.Fill, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(35, 62, 103), ForeColor = Color.White };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => SaveAndClose();
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.Controls.Add(save, 0, 7);
        table.SetColumnSpan(save, 3);

        Controls.Add(table);
    }

    private Control BuildTargetButtons()
    {
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var file = new Button { Text = "File", Width = 52, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
        var folder = new Button { Text = "Folder", Width = 58, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
        file.FlatAppearance.BorderSize = 0;
        folder.FlatAppearance.BorderSize = 0;

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
        var b = new Button { Text = "Pick", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _imagePath = ofd.FileName;
                _imagePathLabel.Text = _imagePath;
            }
        };
        return b;
    }

    private static void AddRow(TableLayoutPanel table, int row, string text, Control field, Control? action)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        table.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gainsboro }, 0, row);
        field.Dock = DockStyle.Fill;
        table.Controls.Add(field, 1, row);
        table.Controls.Add(action ?? new Panel(), 2, row);
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text) || string.IsNullOrWhiteSpace(_targetBox.Text))
        {
            MessageBox.Show("Name and target are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    public ThemeEditorForm(ThemeConfig current)
    {
        Text = "Theme Studio";
        Width = 540;
        Height = 380;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(14, 20, 32);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        var name = new TextBox { Text = current.Name, Dock = DockStyle.Fill };
        var accent = new TextBox { Text = current.Accent, Dock = DockStyle.Fill };
        var surface = new TextBox { Text = current.Surface, Dock = DockStyle.Fill };
        var background = new TextBox { Text = current.Background, Dock = DockStyle.Fill };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(14) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        AddThemeRow(table, 0, "Theme", name, null);
        AddThemeRow(table, 1, "Accent", accent, () => PickColor(accent));
        AddThemeRow(table, 2, "Surface", surface, () => PickColor(surface));
        AddThemeRow(table, 3, "Background", background, () => PickColor(background));

        var save = new Button { Text = "Save Theme", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(35, 62, 103), ForeColor = Color.White };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) =>
        {
            Theme = new ThemeConfig { Name = name.Text.Trim(), Accent = accent.Text.Trim(), Surface = surface.Text.Trim(), Background = background.Text.Trim(), BackgroundImage = current.BackgroundImage };
            DialogResult = DialogResult.OK;
            Close();
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.Controls.Add(save, 0, 4);
        table.SetColumnSpan(save, 3);

        Controls.Add(table);
    }

    private static void AddThemeRow(TableLayoutPanel table, int row, string label, TextBox box, Action? picker)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gainsboro }, 0, row);
        table.Controls.Add(box, 1, row);
        if (picker is null)
        {
            table.Controls.Add(new Panel(), 2, row);
            return;
        }

        var pick = new Button { Text = "Pick", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(26, 45, 79), ForeColor = Color.White };
        pick.FlatAppearance.BorderSize = 0;
        pick.Click += (_, _) => picker();
        table.Controls.Add(pick, 2, row);
    }

    private static void PickColor(TextBox box)
    {
        using var dlg = new ColorDialog();
        if (dlg.ShowDialog() == DialogResult.OK) box.Text = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }
}

public sealed class InputDialog : Form
{
    public string Value => _box.Text;
    private readonly TextBox _box = new();

    public InputDialog(string title, string label, string initial)
    {
        Text = title;
        Width = 380;
        Height = 180;
        StartPosition = FormStartPosition.CenterParent;

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 3 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _box.Text = initial;
        _box.Dock = DockStyle.Fill;
        panel.Controls.Add(_box, 0, 1);

        var ok = new Button { Text = "OK", Dock = DockStyle.Right, Width = 86 };
        ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        panel.Controls.Add(ok, 0, 2);

        Controls.Add(panel);
    }
}

public sealed class GradientPanel : Panel
{
    public Color ColorA { get; set; } = Color.Black;
    public Color ColorB { get; set; } = Color.Gray;
    public float Angle { get; set; } = 90f;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var b = new LinearGradientBrush(ClientRectangle, ColorA, ColorB, Angle);
        e.Graphics.FillRectangle(b, ClientRectangle);
    }
}

public sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 12;
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
