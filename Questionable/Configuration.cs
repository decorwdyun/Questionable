using Dalamud.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LLib.ImGui;

namespace Questionable;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public GeneralConfiguration General { get; } = new();
    public AdvancedConfiguration Advanced { get; } = new();
    public WindowConfig DebugWindowConfig { get; } = new();
    public WindowConfig ConfigWindowConfig { get; } = new();

    internal sealed class GeneralConfiguration
    {
        public uint MountId { get; set; } = 71;
        public GrandCompany GrandCompany { get; set; } = GrandCompany.None;
        public bool HideInAllInstances { get; set; } = true;
        public bool UseEscToCancelQuesting { get; set; } = true;
        public bool ShowIncompleteSeasonalEvents { get; set; } = true;
        public bool AutomaticallyCompleteSnipeTasks { get; set; }
        public bool ConfigureTextAdvance { get; set; } = true;
    }

    internal sealed class AdvancedConfiguration
    {
        public bool DebugOverlay { get; set; }
        public bool NeverFly { get; set; }
        public bool AdditionalStatusInformation { get; set; }
    }
}
