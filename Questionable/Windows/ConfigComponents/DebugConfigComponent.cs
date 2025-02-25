using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
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
            "启用此处的任何选项都可能会产生非预期行为。请谨慎使用。");

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
        if (ImGui.Checkbox("显示额外的状态信息", ref additionalStatusInformation))
        {
            Configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        ImGui.Separator();

        ImGui.Text("AutoDuty 选项");
        using (var _ = ImRaii.PushIndent())
        {
            ImGui.AlignTextToFramePadding();
            bool disableAutoDutyBareMode = Configuration.Advanced.DisableAutoDutyBareMode;
            if (ImGui.Checkbox("使用 Pre-Loop/Loop/Post-Loop 设置", ref disableAutoDutyBareMode))
            {
                Configuration.Advanced.DisableAutoDutyBareMode = disableAutoDutyBareMode;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "通常情况下，在使用 Questionable 运行副本时，AutoDuty 的循环设置会被禁用，因为它们可能会导致问题（甚至关闭您的电脑）。");
        }

        ImGui.EndTabItem();
    }
}
