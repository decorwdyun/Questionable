using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Questionable.Windows.ConfigComponents;

internal sealed class DebugConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration) : ConfigComponent(pluginInterface, configuration)
{
    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("高级###Debug");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "启用这里的任何选项都可能导致不可预期的行为，请自行承担风险。");

        ImGui.Separator();

        bool debugOverlay = Configuration.Advanced.DebugOverlay;
        if (ImGui.Checkbox("启用调试叠加层", ref debugOverlay))
        {
            Configuration.Advanced.DebugOverlay = debugOverlay;
            Save();
        }

        using (ImRaii.Disabled(!debugOverlay))
        {
            using (ImRaii.PushIndent())
            {
                bool combatDataOverlay = Configuration.Advanced.CombatDataOverlay;
                if (ImGui.Checkbox("启用战斗数据叠加层", ref combatDataOverlay))
                {
                    Configuration.Advanced.CombatDataOverlay = combatDataOverlay;
                    Save();
                }
            }
        }

        bool highlightNpc = Configuration.Advanced.HighlightSelectedNpc;
        if (ImGui.Checkbox("高亮显示当前任务线相关的 NPC", ref highlightNpc))
        {
            Configuration.Advanced.HighlightSelectedNpc = highlightNpc;
            Save();
        }

        using (ImRaii.Disabled(!highlightNpc))
        {
            using (ImRaii.PushIndent())
            {
                var highlightColorNames = Enum.GetNames<ObjectHighlightColor>();
                var highlightColorValues = Enum.GetValues<ObjectHighlightColor>();
                var selectedHighlightColor = Array.IndexOf(highlightColorValues, Configuration.Advanced.HighlightColor);
                ImGui.SetNextItemWidth(150f);
                if (ImGui.Combo("高亮颜色", ref selectedHighlightColor, highlightColorNames, highlightColorNames.Length))
                {
                    Configuration.Advanced.HighlightColor = (ObjectHighlightColor)selectedHighlightColor;
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
        if (ImGui.Checkbox("绘制额外状态信息", ref additionalStatusInformation))
        {
            Configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        if (additionalStatusInformation)
        {
            bool showTracked = Configuration.Advanced.ShowTracked;
            bool showDailies = Configuration.Advanced.ShowDailies;
            bool showDirector = Configuration.Advanced.ShowDirector;
            bool showActionManager = Configuration.Advanced.ShowActionManager;
            bool showNewGamePlus = Configuration.Advanced.ShowNewGamePlus;
            using (ImRaii.PushIndent())
            {
                ImGui.AlignTextToFramePadding();
                if (ImGui.Checkbox("显示已追踪任务", ref showTracked))
                {
                    Configuration.Advanced.ShowTracked = showTracked;
                    Save();
                }
                if (ImGui.Checkbox("显示已接受/已完成的日常任务", ref showDailies))
                {
                    Configuration.Advanced.ShowDailies = showDailies;
                    Save();
                }
                if (ImGui.Checkbox("显示 Director 信息", ref showDirector))
                {
                    Configuration.Advanced.ShowDirector = showDirector;
                    Save();
                }
                if (ImGui.Checkbox("显示 Action Manager", ref showActionManager))
                {
                    Configuration.Advanced.ShowActionManager = showActionManager;
                    Save();
                }
                if (ImGui.Checkbox("显示 NG+ 章节", ref showNewGamePlus))
                {
                    Configuration.Advanced.ShowNewGamePlus = showNewGamePlus;
                    Save();
                }
            }
        }

        ImGui.Separator();

        ImGui.Text("AutoDuty 设置");
        using (ImRaii.PushIndent())
        {
            ImGui.AlignTextToFramePadding();
            bool disableAutoDutyBareMode = Configuration.Advanced.DisableAutoDutyBareMode;
            if (ImGui.Checkbox("使用 预循环 / 循环 / 后循环 设置", ref disableAutoDutyBareMode))
            {
                Configuration.Advanced.DisableAutoDutyBareMode = disableAutoDutyBareMode;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "通常在配合 Questionable 跑副本时，会禁用 AutoDuty 的循环设置，因为这些设置可能导致问题（甚至会把你的电脑搞死机）。");
        }

        ImGui.Separator();
        ImGui.Text("任务 / 交互跳过");
        using (ImRaii.PushIndent())
        {
            bool skipAetherCurrents = Configuration.Advanced.SkipAetherCurrents;
            if (ImGui.Checkbox("不拾取风脉 / 风脉任务", ref skipAetherCurrents))
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
            ImGuiComponents.HelpMarker("水晶塔系列任务为进入3.0主线的必要条件（氪了直升包可勾选）。.");
            bool preventQuestCompletion = Configuration.Advanced.PreventQuestCompletion;
            if (ImGui.Checkbox("不要自动交任务", ref preventQuestCompletion))
            {
                Configuration.Advanced.PreventQuestCompletion = preventQuestCompletion;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("启用后，Questionable 在做任务时不会自动交任务。除了最后一步交任务，这之前的步骤都会自动帮你完成。");

            bool namazuPreferCraft = Configuration.Advanced.NamazuPreferCraft;
            if (ImGui.Checkbox("鲶鱼精：优先使用生产职业而非采集", ref namazuPreferCraft))
            {
                Configuration.Advanced.NamazuPreferCraft = namazuPreferCraft;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("鲶鱼精部族任务可以用生产或采集完成，这里可以设置你的偏好。");

            bool showWindowOnStart = Configuration.Advanced.ShowWindowOnStart;
            if (ImGui.Checkbox("启动时显示窗口", ref showWindowOnStart))
            {
                Configuration.Advanced.ShowWindowOnStart = showWindowOnStart;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("启用后，Questionable 的任务进度窗口将在插件加载时显示。");

            bool startMinimized = Configuration.Advanced.StartMinimized;
            if (ImGui.Checkbox("启动时最小化", ref startMinimized))
            {
                Configuration.Advanced.StartMinimized = startMinimized;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("启用后，Questionable 的任务进度窗口将在加载时处于最小化状态。");

#if DEBUG
            bool openEditor = Configuration.Advanced.OpenEditor;
            if (ImGui.Checkbox("开始任务时打开编辑器", ref openEditor))
            {
                Configuration.Advanced.OpenEditor = openEditor;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker("启用后，Questionable 会在你的默认文本编辑器中打开当前任务对应的路径文件。");
#endif
        }
    }
}