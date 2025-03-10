using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin;
using Humanizer;
using Humanizer.Localisation;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.QuestComponents;

internal sealed class EventInfoComponent
{
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
    private readonly List<EventQuest> _eventQuests =
    [
        new("Little Ladies' Day", [new(5237), new(5238)], AtDailyReset(new(2025, 3, 17))),
    ];

    private readonly QuestData _questData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestController _questController;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly Configuration _configuration;
    private readonly IDalamudPluginInterface _pluginInterface;

    public EventInfoComponent(QuestData questData, QuestRegistry questRegistry, QuestFunctions questFunctions,
        UiUtils uiUtils, QuestController questController, QuestTooltipComponent questTooltipComponent,
        Configuration configuration, IDalamudPluginInterface pluginInterface)
    {
        _questData = questData;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _uiUtils = uiUtils;
        _questController = questController;
        _questTooltipComponent = questTooltipComponent;
        _configuration = configuration;
        _pluginInterface = pluginInterface;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static DateTime AtDailyReset(DateOnly date)
    {
        return new DateTime(date, new TimeOnly(14, 59), DateTimeKind.Utc);
    }

    public bool ShouldDraw => _configuration.General.ShowIncompleteSeasonalEvents && _eventQuests.Any(IsIncomplete);

    public void Draw()
    {
        foreach (var eventQuest in _eventQuests)
        {
            if (IsIncomplete(eventQuest))
                DrawEventQuest(eventQuest);
        }
    }

    private void DrawEventQuest(EventQuest eventQuest)
    {
        string time = (eventQuest.EndsAtUtc - DateTime.UtcNow).Humanize(
            precision: 1,
            culture: CultureInfo.InvariantCulture,
            minUnit: TimeUnit.Minute,
            maxUnit: TimeUnit.Day);
        ImGui.Text($"{eventQuest.Name} ({time})");

        float width;
        using (var _ = _pluginInterface.UiBuilder.IconFontHandle.Push())
            width = ImGui.CalcTextSize(FontAwesomeIcon.Play.ToIconString()).X + ImGui.GetStyle().FramePadding.X;

        using (var _ = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            width -= ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;

        List<QuestId> startableQuests = eventQuest.QuestIds.Where(x =>
                _questRegistry.IsKnownQuest(x) &&
                _questFunctions.IsReadyToAcceptQuest(x) &&
                x != _questController.StartedQuest?.Quest.Id &&
                x != _questController.NextQuest?.Quest.Id)
            .ToList();
        if (startableQuests.Count == 0)
            width = 0;

        foreach (var questId in eventQuest.QuestIds)
        {
            if (_questFunctions.IsQuestComplete(questId))
                continue;

            string questName = _questData.GetQuestInfo(questId).Name;
            if (startableQuests.Contains(questId) &&
                _questRegistry.TryGetQuest(questId, out Quest? quest))
            {
                ImGuiComponents.IconButton(FontAwesomeIcon.Play);
                if (ImGui.IsItemClicked())
                {
                    _questController.SetNextQuest(quest);
                    _questController.Start("SeasonalEventSelection");
                }

                bool hovered = ImGui.IsItemHovered();

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(questName);
                hovered |= ImGui.IsItemHovered();

                if (hovered)
                    _questTooltipComponent.Draw(quest.Info);
            }
            else
            {
                if (width > 0)
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + width);

                var style = _uiUtils.GetQuestStyle(questId);
                if (_uiUtils.ChecklistItem(questName, style.Color, style.Icon, ImGui.GetStyle().FramePadding.X))
                    _questTooltipComponent.Draw(_questData.GetQuestInfo(questId));
            }
        }
    }

    private bool IsIncomplete(EventQuest eventQuest)
    {
        if (eventQuest.EndsAtUtc <= DateTime.UtcNow)
            return false;

        return !eventQuest.QuestIds.All(x => _questFunctions.IsQuestComplete(x));
    }

    public IEnumerable<QuestId> GetCurrentlyActiveEventQuests()
    {
        return _eventQuests
            .Where(x => x.EndsAtUtc >= DateTime.UtcNow)
            .SelectMany(x => x.QuestIds);
    }

    private sealed record EventQuest(string Name, List<QuestId> QuestIds, DateTime EndsAtUtc);
}
