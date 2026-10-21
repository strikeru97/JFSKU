using MediaBrowser.Model.Plugins;

namespace JFSKU.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string TmdbApiKey { get; set; } = string.Empty;
    public bool OverwriteExisting { get; set; }
    public bool DryRun { get; set; } = true;
}
