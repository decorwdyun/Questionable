using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;

namespace Questionable.Windows.ConfigComponents;

internal sealed class DebugConfigComponent : ConfigComponent
{
    public DebugConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration)
        : base(pluginInterface, configuration)
    {
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("高级###Debug");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "启用此处的任何选项可能会导致意外行为。请谨慎使用。");

        ImGui.Separator();

        bool debugOverlay = Configuration.Advanced.DebugOverlay;
        if (ImGui.Checkbox("启用 Debug 覆盖层", ref debugOverlay))
        {
            Configuration.Advanced.DebugOverlay = debugOverlay;
            Save();
        }

        bool neverFly = Configuration.Advanced.NeverFly;
        if (ImGui.Checkbox("禁用飞行（即使该区域已解锁飞行）", ref neverFly))
        {
            Configuration.Advanced.NeverFly = neverFly;
            Save();
        }

        bool additionalStatusInformation = Configuration.Advanced.AdditionalStatusInformation;
        if (ImGui.Checkbox("显示额外状态信息", ref additionalStatusInformation))
        {
            Configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        ImGui.EndTabItem();
    }
}
