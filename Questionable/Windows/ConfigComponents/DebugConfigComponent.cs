using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Questionable.PathData;
using static Questionable.Utils.LocalizeShortcut;
namespace Questionable.Windows.ConfigComponents;

internal sealed class DebugConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration, PathDataUpdater pathDataUpdater) : ConfigComponent(pluginInterface, configuration)
{
    public override void DrawTab()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("高级") + "###Debug");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            _L("启用这里的任何选项都可能导致不可预期的行为，请自行承担风险。"));

        ImGui.Separator();

        bool neverFly = Configuration.Advanced.NeverFly;
        if (ImGui.Checkbox(_L("禁用飞行（即使该区域已解锁飞行）"), ref neverFly))
        {
            Configuration.Advanced.NeverFly = neverFly;
            Save();
        }

        if (ImGui.CollapsingHeader(_L("信息")))
        {
            using (ImRaii.PushIndent())
            {
                bool debugOverlay = Configuration.Advanced.DebugOverlay;
                if (ImGui.Checkbox(_L("启用调试叠加层"), ref debugOverlay))
                {
                    Configuration.Advanced.DebugOverlay = debugOverlay;
                    Save();
                }

                using (ImRaii.Disabled(!debugOverlay))
                {
                    using (ImRaii.PushIndent())
                    {
                        bool combatDataOverlay = Configuration.Advanced.CombatDataOverlay;
                        if (ImGui.Checkbox(_L("启用战斗数据叠加层"), ref combatDataOverlay))
                        {
                            Configuration.Advanced.CombatDataOverlay = combatDataOverlay;
                            Save();
                        }
                    }
                }

                bool highlightNpc = Configuration.Advanced.HighlightSelectedNpc;
                if (ImGui.Checkbox(_L("高亮显示当前任务线相关的 NPC"), ref highlightNpc))
                {
                    Configuration.Advanced.HighlightSelectedNpc = highlightNpc;
                    Save();
                }

                using (ImRaii.Disabled(!highlightNpc))
                {
                    using (ImRaii.PushIndent())
                    {
                        ImGui.SetNextItemWidth(150f);
                        DrawComboOption(("高亮颜色"),
                            Enum.GetValues<ObjectHighlightColor>(),
                            Enum.GetNames<ObjectHighlightColor>(),
                            () => Configuration.Advanced.HighlightColor,
                            v => Configuration.Advanced.HighlightColor = v);
                    }
                }

                bool additionalStatusInformation = Configuration.Advanced.AdditionalStatusInformation;
                if (ImGui.Checkbox(_L("绘制额外状态信息"), ref additionalStatusInformation))
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
                        if (ImGui.Checkbox(_L("显示已追踪任务"), ref showTracked))
                        {
                            Configuration.Advanced.ShowTracked = showTracked;
                            Save();
                        }

                        if (ImGui.Checkbox(_L("显示已接受/已完成的日常任务"), ref showDailies))
                        {
                            Configuration.Advanced.ShowDailies = showDailies;
                            Save();
                        }

                        if (ImGui.Checkbox(_L("显示 Director 信息"), ref showDirector))
                        {
                            Configuration.Advanced.ShowDirector = showDirector;
                            Save();
                        }

                        if (ImGui.Checkbox(_L("显示 Action Manager"), ref showActionManager))
                        {
                            Configuration.Advanced.ShowActionManager = showActionManager;
                            Save();
                        }

                        if (ImGui.Checkbox(_L("显示 NG+ 章节"), ref showNewGamePlus))
                        {
                            Configuration.Advanced.ShowNewGamePlus = showNewGamePlus;
                            Save();
                        }
                    }
                }
            }
        }

        ImGui.Separator();

        ImGui.Text(_L("AutoDuty 设置"));
        using (ImRaii.PushIndent())
        {
            ImGui.AlignTextToFramePadding();
            bool disableAutoDutyBareMode = Configuration.Advanced.DisableAutoDutyBareMode;
            if (ImGui.Checkbox(_L("使用 AutoDuty 自身的设置"), ref disableAutoDutyBareMode))
            {
                Configuration.Advanced.DisableAutoDutyBareMode = disableAutoDutyBareMode;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                _L("通常 Questionable 在运行时会禁用 AutoDuty 自己的循环设置，因为这些设置可能导致问题（甚至会让电脑关机）。"));
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(_L("任务 / 交互跳过")))
        {
            using (ImRaii.PushIndent())
            {
                bool skipAetherCurrents = Configuration.Advanced.SkipAetherCurrents;
                if (ImGui.Checkbox(_L("不共鸣风脉 / 风脉任务"), ref skipAetherCurrents))
                {
                    Configuration.Advanced.SkipAetherCurrents = skipAetherCurrents;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("如果未通过 Questionable 在主线任务时完成，你将需要手动完成遗漏的风脉泉/任务。Questionable 没有办法自动查缺补漏。"));

                bool skipClassJobQuests = Configuration.Advanced.SkipClassJobQuests;
                if (ImGui.Checkbox(_L("不接取职业/特职/职能任务"), ref skipClassJobQuests))
                {
                    Configuration.Advanced.SkipClassJobQuests = skipClassJobQuests;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("部分职业技能必须通过职业任务获得。若计划和其他玩家攻略副本，不建议勾选。"));

                bool skipARealmRebornHardModePrimals = Configuration.Advanced.SkipARealmRebornHardModePrimals;
                if (ImGui.Checkbox(_L("不接取 2.0 极神任务"), ref skipARealmRebornHardModePrimals))
                {
                    Configuration.Advanced.SkipARealmRebornHardModePrimals = skipARealmRebornHardModePrimals;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("极伊弗利特/迦楼罗/泰坦为进入 3.0 的必要条件（已购买直升可勾选）。"));

                bool skipCrystalTowerRaids = Configuration.Advanced.SkipCrystalTowerRaids;
                if (ImGui.Checkbox(_L("不接取水晶塔系列任务"), ref skipCrystalTowerRaids))
                {
                    Configuration.Advanced.SkipCrystalTowerRaids = skipCrystalTowerRaids;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("水晶塔系列任务为进入 3.0 主线的必要条件（已购买直升可勾选）。"));

                bool preventQuestCompletion = Configuration.Advanced.PreventQuestCompletion;
                if (ImGui.Checkbox(_L("不要自动交任务"), ref preventQuestCompletion))
                {
                    Configuration.Advanced.PreventQuestCompletion = preventQuestCompletion;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 在做任务时不会自动交任务。除了最后一步交任务，这之前的步骤都会自动帮你完成。"));

                bool abandonQuestBeforeCompletion = Configuration.Advanced.AbandonQuestBeforeCompletion;
                bool removeFromPriorityWhenAbandoned = Configuration.Advanced.RemoveFromPriorityWhenAbandoned;
                if (ImGui.Checkbox(_L("交任务前放弃任务"), ref abandonQuestBeforeCompletion))
                {
                    Configuration.Advanced.AbandonQuestBeforeCompletion = abandonQuestBeforeCompletion;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 将在到达交任务步骤时尝试向服务器发送放弃任务指令。 " +
                    "此设置会在插件加载时重置为关闭。"));
                if (ImGui.Checkbox(_L("放弃时从优先队列移除"), ref removeFromPriorityWhenAbandoned))
                {
                    Configuration.Advanced.RemoveFromPriorityWhenAbandoned = removeFromPriorityWhenAbandoned;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 在放弃任务时也会从优先级队列中移除该任务。 " +
                    "此设置会在插件加载时重置为关闭。"));

                bool namazuPreferCraft = Configuration.Advanced.NamazuPreferCraft;
                if (ImGui.Checkbox(_L("鲶鱼精：优先使用生产职业而非采集"), ref namazuPreferCraft))
                {
                    Configuration.Advanced.NamazuPreferCraft = namazuPreferCraft;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("鲶鱼精部族任务可以用生产或采集完成，这里可以设置你的偏好。"));

                bool showWindowOnStart = Configuration.Advanced.ShowWindowOnStart;
                if (ImGui.Checkbox(_L("启动时显示窗口"), ref showWindowOnStart))
                {
                    Configuration.Advanced.ShowWindowOnStart = showWindowOnStart;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 的任务进度窗口将在插件加载时显示。"));

                bool startMinimized = Configuration.Advanced.StartMinimized;
                if (ImGui.Checkbox(_L("启动时最小化"), ref startMinimized))
                {
                    Configuration.Advanced.StartMinimized = startMinimized;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 的任务进度窗口将在加载时处于最小化状态。"));

                bool openEditor = Configuration.Advanced.OpenEditor;
                if (ImGui.Checkbox(_L("开始任务时打开编辑器"), ref openEditor))
                {
                    Configuration.Advanced.OpenEditor = openEditor;
                    Save();
                }

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("启用后，Questionable 会在你的默认文本编辑器中打开当前任务对应的路径文件。"));
            }
        }

        ImGui.Separator();
        ImGui.Text(_L("路径数据"));
        using (ImRaii.PushIndent())
        {
            bool autoUpdatePaths = Configuration.PathData.AutoUpdate;
            if (ImGui.Checkbox(_L("自动下载任务/采集路径更新"), ref autoUpdatePaths))
            {
                Configuration.PathData.AutoUpdate = autoUpdatePaths;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(_L("无需完整插件更新即可下载较新的任务/采集路径。"));

            if (ImGui.Button(_L("立即检查路径更新")))
                pathDataUpdater.CheckForUpdatesManually();

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, pathDataUpdater.Status);

            long installedVersion = Configuration.PathData.InstalledDataVersion;
            ImGui.TextColored(ImGuiColors.DalamudGrey,
                installedVersion == 0
                    ? _L("正在使用插件内置的路径数据。")
                    : _LF("已下载的路径数据版本: {0}", installedVersion));
        }
    }
}
