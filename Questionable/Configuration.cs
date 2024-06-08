using Dalamud.Configuration;
using LLib.ImGui;

namespace Questionable;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public WindowConfig DebugWindowConfig { get; set; } = new();
}
