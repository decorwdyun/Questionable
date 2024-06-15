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
        public bool AutoAcceptNextQuest { get; set; }
        public uint MountId { get; set; } = 71;
        public GrandCompany GrandCompany { get; set; } = GrandCompany.None;
    }

    internal sealed class AdvancedConfiguration
    {
        public bool NeverFly { get; set; }
    }
}
