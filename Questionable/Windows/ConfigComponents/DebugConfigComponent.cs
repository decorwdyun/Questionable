using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using Questionable.External;
using Questionable.Model.Common;

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

        using (ImRaii.Disabled(!debugOverlay))
        {
            using (ImRaii.PushIndent())
            {
                bool combatDataOverlay = Configuration.Advanced.CombatDataOverlay;
                if (ImGui.Checkbox("Enable combat data overlay", ref combatDataOverlay))
                {
                    Configuration.Advanced.CombatDataOverlay = combatDataOverlay;
                    Save();
                }
            }
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
        using (ImRaii.PushIndent())
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
                "通常情况下，在使用 Questionable 运行副本时，AutoDuty 的循环设置会被禁用，因为它们可能会导致问题（甚至把的电脑关机）。");
        }

        ImGui.Separator();
        ImGui.Text("任务/交互跳过");
        using (ImRaii.PushIndent())
        {
            bool skipAetherCurrents = Configuration.Advanced.SkipAetherCurrents;
            if (ImGui.Checkbox("不共鸣风脉泉/接取风脉任务", ref skipAetherCurrents))
            {
                Configuration.Advanced.SkipAetherCurrents = skipAetherCurrents;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("如果未通过 Questionable 功能在主线任务时完成，你将需要手动完成遗漏的风脉泉/任务。Questionable 没有任何办法可以自动查缺补漏。");

            bool skipClassJobQuests = Configuration.Advanced.SkipClassJobQuests;
            if (ImGui.Checkbox("不接取职业/特职/职能任务", ref skipClassJobQuests))
            {
                Configuration.Advanced.SkipClassJobQuests = skipClassJobQuests;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("部分职业技能必须通过职业任务获得。若计划和其他玩家攻略副本，不建议勾选。");

            bool skipARealmRebornHardModePrimals = Configuration.Advanced.SkipARealmRebornHardModePrimals;
            if (ImGui.Checkbox("不接取 2.0 极神任务", ref skipARealmRebornHardModePrimals))
            {
                Configuration.Advanced.SkipARealmRebornHardModePrimals = skipARealmRebornHardModePrimals;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("极伊弗利特/迦楼罗/泰坦为进入3.0的必要条件（氪了直升包可勾选）。");

            bool skipCrystalTowerRaids = Configuration.Advanced.SkipCrystalTowerRaids;
            if (ImGui.Checkbox("不接取水晶塔系列任务", ref skipCrystalTowerRaids))
            {
                Configuration.Advanced.SkipCrystalTowerRaids = skipCrystalTowerRaids;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("水晶塔系列任务为进入3.0的必要条件（氪了直升包可勾选）。");
        }
    }
}
