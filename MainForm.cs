using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Text.Json;

namespace LauncherExplorer;

public class MainForm : Form
{
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "launcher_config.json");
    private AppConfig _config = new();

    private readonly SplitContainer _shell = new() { Dock = DockStyle.Fill, SplitterDistance = 300 };
    private readonly TabControl _contentTabs = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
    private readonly TreeView _drivesTree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ListBox _shortcutList = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    private readonly FlowLayoutPanel _homeFlow = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16) };
    private readonly ListView _libraryView = new() { Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false };
    private readonly ImageList _smallIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(22, 22) };
    private readonly ImageList _largeIcons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(72, 72) };

    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(22, 29, 40) };
    private readonly Label _pathLabel = new() { Dock = DockStyle.Top, Height = 28, ForeColor = Color.Gainsboro, Padding = new Padding(8, 6, 8, 0) };
    private readonly ComboBox _layoutCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly TrackBar _zoom = new() { Minimum = 64, Maximum = 160, Value = 96, TickStyle = TickStyle.None, Width = 150 };

    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public MainForm()
    {
        Text = "Launcher Explorer X";
        Width = 1560;
        Height = 940;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        BuildUi();
        LoadConfig();
        ApplyTheme();
        LoadDrives();
        LoadHome();
        LoadDirectory(_currentPath);
        FormClosing += (_, _) => SaveConfig();
    }

    private void BuildUi()
    {
        var topBar = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(16, 10, 16, 10) };
        var title = new Label { Text = "Launcher Explorer X", AutoSize = true, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Color.White };
        var addPoster = new Button { Text = "Add Poster/Banner", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var editTheme = new Button { Text = "Theme", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var toHome = new Button { Text = "Home", AutoSize = true, FlatStyle = FlatStyle.Flat };
        var toLibrary = new Button { Text = "Library", AutoSize = true, FlatStyle = FlatStyle.Flat };

        foreach (var b in new[] { addPoster, editTheme, toHome, toLibrary })
        {
            b.FlatAppearance.BorderSize = 0;
            b.ForeColor = Color.White;
            b.BackColor = Color.FromArgb(40, 58, 86);
            b.Padding = new Padding(10, 4, 10, 4);
        }

        addPoster.Click += (_, _) => AddPosterDialog();
        editTheme.Click += (_, _) => ThemeDialog();
        toHome.Click += (_, _) => _contentTabs.SelectedIndex = 0;
        toLibrary.Click += (_, _) => _contentTabs.SelectedIndex = 1;

        var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
        rightButtons.Controls.AddRange([toHome, toLibrary, addPoster, editTheme]);
        topBar.Controls.Add(title);
        topBar.Controls.Add(rightButtons);

        Controls.Add(_shell);
        Controls.Add(topBar);

        var leftTop = new Label { Text = "Shortcuts", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Gainsboro, Padding = new Padding(8, 8, 0, 0), Font = new Font("Segoe UI Semibold", 9.5f) };
        var drivesTop = new Label { Text = "Drives", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Gainsboro, Padding = new Padding(8, 8, 0, 0), Font = new Font("Segoe UI Semibold", 9.5f) };
        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        _shell.Panel1.Controls.Add(left);
        left.Controls.Add(_drivesTree);
        left.Controls.Add(drivesTop);
        left.Controls.Add(_shortcutList);
        left.Controls.Add(leftTop);

        _contentTabs.TabPages.Add(new TabPage("Home"));
        _contentTabs.TabPages.Add(new TabPage("Library"));
        _contentTabs.TabPages[0].Controls.Add(_homeFlow);

        var libraryShell = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 1020 };
        _contentTabs.TabPages[1].Controls.Add(libraryShell);
        _shell.Panel2.Controls.Add(_contentTabs);

        var libraryTop = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        _layoutCombo.Items.AddRange(["List", "Tiles", "Icons"]);
        _layoutCombo.SelectedIndex = 0;
        _layoutCombo.SelectedIndexChanged += (_, _) => ApplyLayout();
        _zoom.ValueChanged += (_, _) => ApplyLayout();
        libraryTop.Controls.Add(new Label { Text = "View", AutoSize = true, ForeColor = Color.Gainsboro, Padding = new Padding(6, 8, 0, 0) });
        libraryTop.Controls.Add(_layoutCombo);
        libraryTop.Controls.Add(new Label { Text = "Scale", AutoSize = true, ForeColor = Color.Gainsboro, Padding = new Padding(20, 8, 0, 0) });
        libraryTop.Controls.Add(_zoom);

        var browserPanel = new Panel { Dock = DockStyle.Fill };
        browserPanel.Controls.Add(_libraryView);
        browserPanel.Controls.Add(libraryTop);
        browserPanel.Controls.Add(_pathLabel);
        libraryShell.Panel1.Controls.Add(browserPanel);

        var previewWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        previewWrap.Controls.Add(_preview);
        libraryShell.Panel2.Controls.Add(previewWrap);

        _libraryView.SmallImageList = _smallIcons;
        _libraryView.LargeImageList = _largeIcons;
        _libraryView.DoubleClick += (_, _) => OpenSelectedInLibrary();
        _libraryView.SelectedIndexChanged += (_, _) => UpdatePreview();
        _libraryView.MouseUp += LibraryRightClick;

        _drivesTree.NodeMouseDoubleClick += (_, e) => { if (Directory.Exists(e.Node.Text)) OpenLibraryPath(e.Node.Text); };
        _shortcutList.DoubleClick += (_, _) => OpenShortcut();
    }

    private void ApplyTheme()
    {
        var bg = ColorTranslator.FromHtml(_config.Theme.Background);
        var surf = ColorTranslator.FromHtml(_config.Theme.Surface);
        var accent = ColorTranslator.FromHtml(_config.Theme.Accent);

        BackColor = bg;
        _shell.BackColor = bg;
        _shell.Panel1.BackColor = surf;
        _shell.Panel2.BackColor = bg;
        _contentTabs.BackColor = bg;
        _homeFlow.BackColor = Color.FromArgb(14, 20, 32);
        _libraryView.BackColor = Color.FromArgb(17, 22, 34);
        _libraryView.ForeColor = Color.White;
        _libraryView.BorderStyle = BorderStyle.None;
        _drivesTree.BackColor = Color.FromArgb(21, 28, 40);
        _drivesTree.ForeColor = Color.Gainsboro;
        _shortcutList.BackColor = Color.FromArgb(21, 28, 40);
        _shortcutList.ForeColor = Color.Gainsboro;

        Paint += (_, e) =>
        {
            using var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(11, 16, 24), accent, 120f);
            e.Graphics.FillRectangle(b, ClientRectangle);
        };
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

    private void LoadDrives()
    {
        _drivesTree.Nodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            _drivesTree.Nodes.Add(new TreeNode(drive.RootDirectory.FullName));
        _shortcutList.Items.Clear();
        foreach (var s in _config.Sections.Where(s => s.Placement == "shortcut").Select(s => s.Name))
            _shortcutList.Items.Add(s);
    }

    private void LoadHome()
    {
        _homeFlow.SuspendLayout();
        _homeFlow.Controls.Clear();

        foreach (var group in _config.Sections.Where(s => s.Placement == "home").GroupBy(s => s.Group))
        {
            var groupLabel = new Label
            {
                Text = group.Key,
                AutoSize = false,
                Width = 1120,
                Height = 30,
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = Color.White,
                Margin = new Padding(2, 12, 2, 6)
            };
            _homeFlow.Controls.Add(groupLabel);

            var row = new FlowLayoutPanel { Width = 1160, Height = 220, WrapContents = false, AutoScroll = true, Margin = new Padding(0, 0, 0, 12) };
            foreach (var item in group)
            {
                var btn = BuildPosterCard(item);
                row.Controls.Add(btn);
            }
            _homeFlow.Controls.Add(row);
        }

        _homeFlow.ResumeLayout();
    }

    private Control BuildPosterCard(PosterItem item)
    {
        var panel = new Panel
        {
            Width = item.Visual == "banner" ? 360 : 168,
            Height = item.Visual == "banner" ? 176 : 208,
            BackColor = Color.FromArgb(26, 34, 48),
            Margin = new Padding(6),
            Cursor = Cursors.Hand
        };
        var title = new Label { Text = item.Name, Dock = DockStyle.Bottom, Height = 34, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0) };
        var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
        {
            pic.Image = Image.FromFile(item.ImagePath);
        }

        panel.Controls.Add(pic);
        panel.Controls.Add(title);
        panel.DoubleClick += (_, _) => OpenPoster(item);
        pic.DoubleClick += (_, _) => OpenPoster(item);
        title.DoubleClick += (_, _) => OpenPoster(item);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Edit", null, (_, _) => EditPoster(item));
        menu.Items.Add("Delete", null, (_, _) => { _config.Sections.Remove(item); LoadHome(); SaveConfig(); LoadDrives(); });
        panel.ContextMenuStrip = menu;
        pic.ContextMenuStrip = menu;
        title.ContextMenuStrip = menu;

        return panel;
    }

    private void OpenPoster(PosterItem item)
    {
        if (item.Type == "folder" || Directory.Exists(item.TargetPath))
        {
            OpenLibraryPath(item.TargetPath);
            return;
        }
        OpenExternal(item.TargetPath);
    }

    private void OpenShortcut()
    {
        if (_shortcutList.SelectedItem is not string selected) return;
        var item = _config.Sections.FirstOrDefault(s => s.Name == selected && s.Placement == "shortcut");
        if (item is null) return;
        OpenPoster(item);
    }

    private void OpenLibraryPath(string path)
    {
        _contentTabs.SelectedIndex = 1;
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

        var dirs = Directory.GetDirectories(path).Select(d => new FileInfoView(d, true));
        var files = Directory.GetFiles(path).Select(f => new FileInfoView(f, false));

        foreach (var item in dirs.Concat(files))
        {
            var icon = ResolveIcon(item.Path, item.IsDirectory);
            _smallIcons.Images.Add(item.Path, icon.small);
            _largeIcons.Images.Add(item.Path, icon.large);

            var lvi = new ListViewItem(item.Name) { Tag = item.Path, ImageKey = item.Path };
            lvi.SubItems.Add(item.IsDirectory ? "Folder" : item.Extension);
            lvi.SubItems.Add(item.SizeText);
            lvi.SubItems.Add(item.Modified.ToString());
            _libraryView.Items.Add(lvi);
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        _smallIcons.ImageSize = new Size(Math.Clamp(_zoom.Value / 3, 18, 54), Math.Clamp(_zoom.Value / 3, 18, 54));
        _largeIcons.ImageSize = new Size(_zoom.Value, _zoom.Value);

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
                _libraryView.Columns.Add("Name", 320);
                _libraryView.Columns.Add("Type", 120);
                _libraryView.Columns.Add("Size", 120);
                _libraryView.Columns.Add("Modified", 200);
                break;
        }
    }

    private (Image small, Image large) ResolveIcon(string path, bool isDir)
    {
        if (_config.FileIcons.TryGetValue(path, out var custom) && File.Exists(custom))
        {
            using var src = Image.FromFile(custom);
            return (new Bitmap(src, _smallIcons.ImageSize), new Bitmap(src, _largeIcons.ImageSize));
        }

        var color = isDir ? Color.FromArgb(242, 180, 80) : Color.FromArgb(90, 160, 255);
        Bitmap Make(Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(color);
            g.FillRoundedRectangle(br, 1, 1, size.Width - 2, size.Height - 2, 8);
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
            _preview.Image = Image.FromFile(path);
            return;
        }
        _preview.Image = null;
    }

    private static bool IsImage(string path) => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant());

    private void OpenSelectedInLibrary()
    {
        if (_libraryView.SelectedItems.Count == 0) return;
        var path = _libraryView.SelectedItems[0].Tag?.ToString() ?? "";
        if (Directory.Exists(path))
        {
            LoadDirectory(path);
            return;
        }
        OpenExternal(path);
    }

    private static void OpenExternal(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
        menu.Items.Add("Change Icon Image", null, (_, _) => ChangeIcon(path));
        menu.Items.Add("Remove Custom Icon", null, (_, _) => { _config.FileIcons.Remove(path); SaveConfig(); LoadDirectory(_currentPath); });
        menu.Items.Add("Properties", null, (_, _) => MessageBox.Show(path, "Path"));
        menu.Show(_libraryView, e.Location);
    }

    private void RenamePath(string path)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("New name", "Rename", Path.GetFileName(path));
        if (string.IsNullOrWhiteSpace(name)) return;
        var parent = Directory.GetParent(path)?.FullName;
        if (parent is null) return;
        var newPath = Path.Combine(parent, name);
        if (Directory.Exists(path)) Directory.Move(path, newPath); else File.Move(path, newPath);
        LoadDirectory(_currentPath);
    }

    private void CompressPath(string path)
    {
        var zip = path.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        if (Directory.Exists(path)) ZipFile.CreateFromDirectory(path, zip);
        else
        {
            using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(path, Path.GetFileName(path));
        }
        LoadDirectory(_currentPath);
    }

    private void DeletePath(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
        LoadDirectory(_currentPath);
    }

    private void ChangeIcon(string path)
    {
        using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        _config.FileIcons[path] = ofd.FileName;
        SaveConfig();
        LoadDirectory(_currentPath);
    }

    private void AddPosterDialog()
    {
        using var dlg = new PosterEditorForm();
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Item is null) return;
        _config.Sections.Add(dlg.Item);
        SaveConfig();
        LoadHome();
        LoadDrives();
    }

    private void EditPoster(PosterItem item)
    {
        using var dlg = new PosterEditorForm(item);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Item is null) return;
        item.Name = dlg.Item.Name;
        item.TargetPath = dlg.Item.TargetPath;
        item.Type = dlg.Item.Type;
        item.Visual = dlg.Item.Visual;
        item.Group = dlg.Item.Group;
        item.Placement = dlg.Item.Placement;
        item.ImagePath = dlg.Item.ImagePath;
        SaveConfig();
        LoadHome();
        LoadDrives();
    }

    private void ThemeDialog()
    {
        using var dlg = new ThemeEditorForm(_config.Theme);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Theme is null) return;
        _config.Theme = dlg.Theme;
        SaveConfig();
        ApplyTheme();
    }

    private sealed record FileInfoView(string Path, bool IsDirectory)
    {
        public string Name => System.IO.Path.GetFileName(Path);
        public string Extension => IsDirectory ? "Folder" : System.IO.Path.GetExtension(Path).Trim('.').ToUpperInvariant();
        public DateTime Modified => IsDirectory ? Directory.GetLastWriteTime(Path) : File.GetLastWriteTime(Path);
        public string SizeText => IsDirectory ? "—" : new FileInfo(Path).Length switch
        {
            > 1_000_000_000 => $"{new FileInfo(Path).Length / 1_000_000_000.0:F1} GB",
            > 1_000_000 => $"{new FileInfo(Path).Length / 1_000_000.0:F1} MB",
            > 1_000 => $"{new FileInfo(Path).Length / 1_000.0:F1} KB",
            var n => $"{n} B"
        };
    }
}

public class ThemeEditorForm : Form
{
    public ThemeConfig? Theme { get; private set; }

    public ThemeEditorForm(ThemeConfig current)
    {
        Width = 480;
        Height = 300;
        Text = "Theme Studio";
        StartPosition = FormStartPosition.CenterParent;

        var accent = new TextBox { Text = current.Accent, Dock = DockStyle.Top };
        var surface = new TextBox { Text = current.Surface, Dock = DockStyle.Top };
        var back = new TextBox { Text = current.Background, Dock = DockStyle.Top };
        var name = new TextBox { Text = current.Name, Dock = DockStyle.Top };
        var save = new Button { Text = "Save", Dock = DockStyle.Bottom, Height = 38 };

        save.Click += (_, _) =>
        {
            Theme = new ThemeConfig { Name = name.Text, Accent = accent.Text, Surface = surface.Text, Background = back.Text, BackgroundImage = current.BackgroundImage };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(save);
        Controls.Add(new Label { Text = "Background", Dock = DockStyle.Top });
        Controls.Add(back);
        Controls.Add(new Label { Text = "Surface", Dock = DockStyle.Top });
        Controls.Add(surface);
        Controls.Add(new Label { Text = "Accent", Dock = DockStyle.Top });
        Controls.Add(accent);
        Controls.Add(new Label { Text = "Theme Name", Dock = DockStyle.Top });
        Controls.Add(name);
    }
}

public class PosterEditorForm : Form
{
    public PosterItem? Item { get; private set; }

    public PosterEditorForm(PosterItem? source = null)
    {
        var item = source is null ? new PosterItem() : new PosterItem
        {
            Name = source.Name,
            TargetPath = source.TargetPath,
            Type = source.Type,
            Visual = source.Visual,
            Group = source.Group,
            Placement = source.Placement,
            ImagePath = source.ImagePath
        };

        Width = 520;
        Height = 420;
        Text = source is null ? "Add Poster/Banner" : "Edit Poster/Banner";
        StartPosition = FormStartPosition.CenterParent;

        var name = new TextBox { Text = item.Name, Dock = DockStyle.Top };
        var target = new TextBox { Text = item.TargetPath, Dock = DockStyle.Top };
        var group = new TextBox { Text = item.Group, Dock = DockStyle.Top };
        var type = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        type.Items.AddRange(["app", "folder"]);
        type.SelectedItem = item.Type;
        var visual = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        visual.Items.AddRange(["poster", "banner"]);
        visual.SelectedItem = item.Visual;
        var placement = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        placement.Items.AddRange(["home", "shortcut"]);
        placement.SelectedItem = item.Placement;

        var imageBtn = new Button { Text = "Pick Image", Dock = DockStyle.Top };
        imageBtn.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
            if (ofd.ShowDialog() == DialogResult.OK) item.ImagePath = ofd.FileName;
        };

        var browseBtn = new Button { Text = "Pick Target", Dock = DockStyle.Top };
        browseBtn.Click += (_, _) =>
        {
            using var menu = new ContextMenuStrip();
            menu.Items.Add("Pick File", null, (_, _) =>
            {
                using var f = new OpenFileDialog();
                if (f.ShowDialog() == DialogResult.OK) target.Text = f.FileName;
            });
            menu.Items.Add("Pick Folder", null, (_, _) =>
            {
                using var fd = new FolderBrowserDialog();
                if (fd.ShowDialog() == DialogResult.OK) target.Text = fd.SelectedPath;
            });
            menu.Show(browseBtn, new Point(0, browseBtn.Height));
        };

        var save = new Button { Text = "Save", Dock = DockStyle.Bottom, Height = 40 };
        save.Click += (_, _) =>
        {
            Item = new PosterItem
            {
                Name = name.Text,
                TargetPath = target.Text,
                Type = type.SelectedItem?.ToString() ?? "app",
                Visual = visual.SelectedItem?.ToString() ?? "poster",
                Group = group.Text,
                Placement = placement.SelectedItem?.ToString() ?? "home",
                ImagePath = item.ImagePath
            };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(save);
        Controls.Add(imageBtn);
        Controls.Add(browseBtn);
        Controls.Add(new Label { Text = "Placement", Dock = DockStyle.Top });
        Controls.Add(placement);
        Controls.Add(new Label { Text = "Visual", Dock = DockStyle.Top });
        Controls.Add(visual);
        Controls.Add(new Label { Text = "Type", Dock = DockStyle.Top });
        Controls.Add(type);
        Controls.Add(new Label { Text = "Group", Dock = DockStyle.Top });
        Controls.Add(group);
        Controls.Add(new Label { Text = "Target", Dock = DockStyle.Top });
        Controls.Add(target);
        Controls.Add(new Label { Text = "Name", Dock = DockStyle.Top });
        Controls.Add(name);
    }
}

static class GraphicsExt
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
