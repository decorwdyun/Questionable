using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Windows.QuestComponents;
using Questionable.Windows.Utils;

namespace Questionable.Windows.ConfigComponents;

internal sealed class StopConditionComponent : ConfigComponent
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestSelector _questSelector;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;
    private readonly IClientState _clientState;
    //private readonly IPlayerState _playerState;

    public StopConditionComponent(
        IDalamudPluginInterface pluginInterface,
        QuestSelector questSelector,
        QuestFunctions questFunctions,
        QuestRegistry questRegistry,
        QuestTooltipComponent questTooltipComponent,
        UiUtils uiUtils,
        IClientState clientState,
        //IPlayerState playerState,
        Configuration configuration)
        : base(pluginInterface, configuration)
    {
        _pluginInterface = pluginInterface;
        _questSelector = questSelector;
        _questRegistry = questRegistry;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
        _clientState = clientState;
        //_playerState = playerState;

        _questSelector.SuggestionPredicate = quest => configuration.Stop.QuestsToStopAfter.All(x => x != quest.Id);
        _questSelector.DefaultPredicate = quest => quest.Info.IsMainScenarioQuest && questFunctions.IsQuestAccepted(quest.Id);
        _questSelector.QuestSelected = quest =>
        {
            configuration.Stop.QuestsToStopAfter.Add(quest.Id);
            Save();
        };
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("自动停止###StopConditionns");
        if (!tab)
            return;

        bool enabled = Configuration.Stop.Enabled;
        if (ImGui.Checkbox("满足以下任意条件时停止任务执行", ref enabled))
        {
            Configuration.Stop.Enabled = enabled;
            Save();
        }

        ImGui.Separator();

        using (ImRaii.Disabled(!enabled))
        {
            // Level stop condition section
            ImGui.Text("角色达到指定等级时停止:");

            bool levelToStopAfter = Configuration.Stop.LevelToStopAfter;
            if (ImGui.Checkbox("启用等级停止条件", ref levelToStopAfter))
            {
                Configuration.Stop.LevelToStopAfter = levelToStopAfter;
                Save();
            }

            using (ImRaii.Disabled(!levelToStopAfter))
            {
                int targetLevel = Configuration.Stop.TargetLevel;
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt("停止等级", ref targetLevel, 1, 5))
                {
                    Configuration.Stop.TargetLevel = Math.Max(1, Math.Min(100, targetLevel));
                    Save();
                }

                // Show current level for reference
                unsafe
                {
                    var playerState = PlayerState.Instance();
                    var currentLevel = playerState->CurrentLevel;
                    if (currentLevel > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"(当前等级: {currentLevel})");
                    }
                }
            }

            ImGui.Separator();

            // Quest completion stop condition section
            ImGui.Text("完成以下任意一个任务时停止：");

            _questSelector.DrawSelection();

            List<ElementId> questsToStopAfter = Configuration.Stop.QuestsToStopAfter;

            // 'Clear All' button if there are quests to clear for fast removal
            if (questsToStopAfter.Count > 0)
            {
                using (ImRaii.Disabled(!ImGui.IsKeyDown(ImGuiKey.ModCtrl)))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, "清空所有"))
                    {
                        Configuration.Stop.QuestsToStopAfter.Clear();
                        Save();
                    }
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("按住 CTRL 键解锁此按钮。");

                ImGui.Separator();
            }

            Quest? itemToRemove = null;
            for (int i = 0; i < questsToStopAfter.Count; i++)
            {
                ElementId questId = questsToStopAfter[i];

                if (!_questRegistry.TryGetQuest(questId, out Quest? quest))
                    continue;

                using (ImRaii.PushId($"Quest{questId}"))
                {
                    var style = _uiUtils.GetQuestStyle(questId);
                    bool hovered;
                    using (var _ = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                    {
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextColored(style.Color, style.Icon.ToIconString());
                        hovered = ImGui.IsItemHovered();
                    }

                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(quest.Info.Name);
                    hovered |= ImGui.IsItemHovered();

                    if (hovered)
                        _questTooltipComponent.Draw(quest.Info);

                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X +
                                       ImGui.GetStyle().WindowPadding.X -
                                       ImGui.CalcTextSize(FontAwesomeIcon.Times.ToIconString()).X -
                                       ImGui.GetStyle().FramePadding.X * 2);
                    }

                    if (ImGuiComponents.IconButton($"##Remove{i}", FontAwesomeIcon.Times))
                        itemToRemove = quest;
                }
            }

            if (itemToRemove != null)
            {
                Configuration.Stop.QuestsToStopAfter.Remove(itemToRemove.Id);
                Save();
            }
        }
    }
}