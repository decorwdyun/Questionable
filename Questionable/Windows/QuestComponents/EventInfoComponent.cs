using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Humanizer;
using Humanizer.Localisation;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.QuestComponents;

internal sealed class EventInfoComponent{
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
    private readonly List<EventQuest> _eventQuests =
    [
        new EventQuest("星芒节 2025 Vol.2", [new QuestId(5231)], AtDailyReset(new DateOnly(2025, 12, 31)))
    ];

    private readonly QuestData _questData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestController _questController;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly Configuration _configuration;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;

    public EventInfoComponent(QuestData questData,
        QuestRegistry questRegistry,
        QuestFunctions questFunctions,
        UiUtils uiUtils,
        QuestController questController,
        QuestTooltipComponent questTooltipComponent,
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IChatGui chatGui)
    {
        _questData = questData;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _uiUtils = uiUtils;
        _questController = questController;
        _questTooltipComponent = questTooltipComponent;
        _configuration = configuration;
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _chatGui = chatGui;
        _clientState.Login += OnLogin;
    }

    private void OnLogin()
    {
        if (!_configuration.General.ShowIncompleteSeasonalEvents) return;
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
            var now = DateTime.UtcNow;
            var incomplete = _eventQuests
                .Where(e => e.EndsAtUtc != DateTime.MaxValue && e.EndsAtUtc > now && IsIncomplete(e))
                .ToList();

            foreach (var message in incomplete.Select(eventQuest => new SeStringBuilder()
                         .AddUiForeground("[Questionable] ",25)
                         .AddText($"尚未完成的活动任务：")
                         .AddUiForeground(eventQuest.Name, 45)
                         .AddText("，剩余时间：")
                         .AddUiForeground(FormatRemaining(eventQuest.EndsAtUtc), 65)))
            {
                _chatGui.Print(message.Build());
                UIGlobals.PlayChatSoundEffect(3);
            }
        });
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
        if (eventQuest.EndsAtUtc != DateTime.MaxValue)
        {
            ImGui.Text($"限时活动：{eventQuest.Name} ({FormatRemaining(eventQuest.EndsAtUtc)}后结束)");
        }
        else
            ImGui.Text(eventQuest.Name);

        List<ElementId> startableQuests = eventQuest.QuestIds.Where(x =>
                _questRegistry.IsKnownQuest(x) &&
                _questFunctions.IsReadyToAcceptQuest(x) &&
                x != _questController.StartedQuest?.Quest.Id &&
                x != _questController.NextQuest?.Quest.Id)
            .ToList();
        foreach (var questId in eventQuest.QuestIds)
        {
            if (_questFunctions.IsQuestComplete(questId))
                continue;

            using (ImRaii.PushId($"##EventQuestSelection{questId}"))
            {
                string questName = _questData.GetQuestInfo(questId).Name;
                if (startableQuests.Contains(questId) &&
                    _questRegistry.TryGetQuest(questId, out Quest? quest))
                {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
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
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX());

                    var style = _uiUtils.GetQuestStyle(questId);
                    if (_uiUtils.ChecklistItem(questName, style.Color, style.Icon, ImGui.GetStyle().FramePadding.X))
                        _questTooltipComponent.Draw(_questData.GetQuestInfo(questId));
                }
            }
        }
    }
    
    private bool IsIncomplete(EventQuest eventQuest)
    {
        if (eventQuest.EndsAtUtc <= DateTime.UtcNow)
            return false;

        return eventQuest.QuestIds.Any(ShouldShowQuest);
    }

    public IEnumerable<ElementId> GetCurrentlyActiveEventQuests()
    {
        return _eventQuests
            .Where(x => x.EndsAtUtc >= DateTime.UtcNow)
            .SelectMany(x => x.QuestIds)
            .Where(ShouldShowQuest);
    }

    private bool ShouldShowQuest(ElementId elementId) => !_questFunctions.IsQuestComplete(elementId) &&
                                                         !_questFunctions.IsQuestUnobtainable(elementId);

    private sealed record EventQuest(string Name, List<ElementId> QuestIds, DateTime EndsAtUtc);

    public void Dispose()
    {
        _clientState.Login -= OnLogin;
    }
    
    private static string FormatRemaining(DateTime targetUtc)
    {
        var now = DateTime.UtcNow;
        var span = targetUtc - now;

        if (span <= TimeSpan.Zero)
            return "已结束";
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}天";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}小时";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}分钟";
        
        return $"{(int)span.TotalSeconds}秒";
    }
}
