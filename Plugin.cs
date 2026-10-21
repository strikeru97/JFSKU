using JFSKU.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JFSKU;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }
    public override string Name => "JFSKU";
    public override Guid Id => Guid.Parse("A1B2C3D4-E5F6-7890-1234-56789ABCDEF0");
    public override string Description => "Ergänzt deutsche Altersfreigaben über TMDb.";

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = "JFSKU.Web.configPage.html"
        }
    };
}
