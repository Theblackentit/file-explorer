using System.Text.Json.Serialization;

namespace LauncherExplorer;

public class AppConfig
{
    public ThemeConfig Theme { get; set; } = new();
    public List<PosterItem> Sections { get; set; } = [];
    public Dictionary<string, string> FileIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PosterItem> Shortcuts { get; set; } = [];
}

public class ThemeConfig
{
    public string Name { get; set; } = "Nebula";
    public string Accent { get; set; } = "#4CC2FF";
    public string Surface { get; set; } = "#141A24";
    public string Background { get; set; } = "#0C1018";
    public string? BackgroundImage { get; set; }
}

public class PosterItem
{
    public string Name { get; set; } = "New Item";
    public string TargetPath { get; set; } = "";
    public string Type { get; set; } = "app"; // app|folder
    public string Visual { get; set; } = "poster"; // poster|banner
    public string Group { get; set; } = "Recently Added";
    public string Placement { get; set; } = "home"; // home|shortcut
    public string? ImagePath { get; set; }

    [JsonIgnore]
    public bool IsDirectory => Directory.Exists(TargetPath);
}
