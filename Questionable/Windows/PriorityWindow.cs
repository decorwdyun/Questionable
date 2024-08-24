using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows;

internal sealed class PriorityWindow : LWindow
{
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;
    private readonly IDalamudPluginInterface _pluginInterface;

    private string _searchString = string.Empty;
    private ElementId? _draggedItem;

    public PriorityWindow(QuestController questController, QuestRegistry questRegistry, QuestFunctions questFunctions,
        QuestTooltipComponent questTooltipComponent, UiUtils uiUtils, IDalamudPluginInterface pluginInterface)
        : base("Quest Priority###QuestionableQuestPriority")
    {
        _questController = questController;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
        _pluginInterface = pluginInterface;

        Size = new Vector2(400, 400);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(400, 999)
        };
    }

    public override void Draw()
    {
        ImGui.Text("Quests to do first:");
        DrawQuestFilter();
        DrawQuestList();
        ImGui.Spacing();

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped(
            "If you have an active MSQ quest, Questionable will generally try to do:");
        ImGui.BulletText("'Priority' quests: class quests, ARR primals, ARR raids");
        ImGui.BulletText(
            "Supported quests in your 'To-Do list'\n(quests from your Journal that are always on-screen)");
        ImGui.BulletText("MSQ quest (if available, unless it is marked as 'ignored'\nin your Journal)");
        ImGui.TextWrapped(
            "If you don't have any active MSQ quest, it will always try to pick up the next quest in the MSQ first.");
    }

    private void DrawQuestFilter()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.BeginCombo($"##QuestSelection", "Add Quest...", ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            bool addFirst = ImGui.InputTextWithHint("", "Filter...", ref _searchString, 256,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);

            IEnumerable<Quest> foundQuests;
            if (!string.IsNullOrEmpty(_searchString))
            {
                foundQuests = _questRegistry.AllQuests
                    .Where(x => x.Info.Name.Contains(_searchString, StringComparison.CurrentCultureIgnoreCase))
                    .Where(x => x.Id is not QuestId questId || !_questFunctions.IsQuestUnobtainable(questId));
            }
            else
            {
                foundQuests = _questRegistry.AllQuests.Where(x => _questFunctions.IsQuestAccepted(x.Id));
            }

            foreach (var quest in foundQuests)
            {
                if (quest.Info.IsMainScenarioQuest || _questController.ManualPriorityQuests.Contains(quest))
                    continue;

                bool addThis = ImGui.Selectable(quest.Info.Name);
                if (addThis || addFirst)
                {
                    _questController.ManualPriorityQuests.Add(quest);

                    if (addFirst)
                    {
                        ImGui.CloseCurrentPopup();
                        addFirst = false;
                    }
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
    }

    private void DrawQuestList()
    {
        List<Quest> priorityQuests = _questController.ManualPriorityQuests;
        Quest? itemToRemove = null;
        Quest? itemToAdd = null;
        int indexToAdd = 0;

        float width = ImGui.GetContentRegionAvail().X;
        List<(Vector2 TopLeft, Vector2 BottomRight)> itemPositions = [];

        for (int i = 0; i < priorityQuests.Count; ++i)
        {
            Vector2 topLeft = ImGui.GetCursorScreenPos() +
                              new Vector2(0, -ImGui.GetStyle().ItemSpacing.Y / 2);
            var quest = priorityQuests[i];
            ImGui.PushID($"Quest{quest.Id}");

            var style = _uiUtils.GetQuestStyle(quest.Id);
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

            if (priorityQuests.Count > 1)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X +
                               ImGui.GetStyle().WindowPadding.X -
                               ImGui.CalcTextSize(FontAwesomeIcon.ArrowsUpDown.ToIconString()).X -
                               ImGui.CalcTextSize(FontAwesomeIcon.Times.ToIconString()).X -
                               ImGui.GetStyle().FramePadding.X * 4 -
                               ImGui.GetStyle().ItemSpacing.X);
                ImGui.PopFont();

                if (_draggedItem == quest.Id)
                {
                    ImGuiComponents.IconButton("##Move", FontAwesomeIcon.ArrowsUpDown,
                        ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.ButtonActive)));
                }
                else
                    ImGuiComponents.IconButton("##Move", FontAwesomeIcon.ArrowsUpDown);

                if (_draggedItem == null && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    _draggedItem = quest.Id;

                ImGui.SameLine();
            }
            else
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X +
                               ImGui.GetStyle().WindowPadding.X -
                               ImGui.CalcTextSize(FontAwesomeIcon.Times.ToIconString()).X -
                               ImGui.GetStyle().FramePadding.X * 2);
                ImGui.PopFont();
            }

            if (ImGuiComponents.IconButton($"##Remove{i}", FontAwesomeIcon.Times))
                itemToRemove = quest;

            ImGui.PopID();

            Vector2 bottomRight = new Vector2(topLeft.X + width,
                ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y + 2);
            itemPositions.Add((topLeft, bottomRight));
        }

        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            _draggedItem = null;
        else if (_draggedItem != null)
        {
            var draggedItem = priorityQuests.Single(x => x.Id == _draggedItem);
            int oldIndex = priorityQuests.IndexOf(draggedItem);

            var (topLeft, bottomRight) = itemPositions[oldIndex];
            ImGui.GetWindowDrawList().AddRect(topLeft, bottomRight, ImGui.GetColorU32(ImGuiColors.DalamudGrey), 3f,
                ImDrawFlags.RoundCornersAll);

            int newIndex = itemPositions.FindIndex(x => ImGui.IsMouseHoveringRect(x.TopLeft, x.BottomRight, true));
            if (newIndex >= 0 && oldIndex != newIndex)
            {
                itemToAdd = priorityQuests.Single(x => x.Id == _draggedItem);
                indexToAdd = newIndex;
            }
        }

        if (itemToRemove != null)
        {
            priorityQuests.Remove(itemToRemove);
        }

        if (itemToAdd != null)
        {
            priorityQuests.Remove(itemToAdd);
            priorityQuests.Insert(indexToAdd, itemToAdd);
        }
    }
}
