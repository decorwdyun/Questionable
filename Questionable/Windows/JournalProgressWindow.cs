using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows;

internal sealed class JournalProgressWindow : LWindow, IDisposable
{
    private readonly JournalData _journalData;
    private readonly QuestRegistry _questRegistry;
    private readonly GameFunctions _gameFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly IClientState _clientState;
    private readonly ICommandManager _commandManager;

    private readonly Dictionary<JournalData.Genre, (int Available, int Completed)> _genreCounts = new();
    private readonly Dictionary<JournalData.Category, (int Available, int Completed)> _categoryCounts = new();
    private readonly Dictionary<JournalData.Section, (int Available, int Completed)> _sectionCounts = new();

    public JournalProgressWindow(JournalData journalData,
        QuestRegistry questRegistry,
        GameFunctions gameFunctions,
        UiUtils uiUtils,
        QuestTooltipComponent questTooltipComponent,
        IClientState clientState,
        ICommandManager commandManager)
        : base("Journal Progress###QuestionableJournalProgress")
    {
        _journalData = journalData;
        _questRegistry = questRegistry;
        _gameFunctions = gameFunctions;
        _uiUtils = uiUtils;
        _questTooltipComponent = questTooltipComponent;
        _clientState = clientState;
        _commandManager = commandManager;

        _clientState.Login += RefreshCounts;
        _clientState.Logout -= ClearCounts;
        _questRegistry.Reloaded += OnQuestsReloaded;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 500)
        };
    }

    private void OnQuestsReloaded(object? sender, EventArgs e) => RefreshCounts();

    public override void OnOpen() => RefreshCounts();

    public override void Draw()
    {
        ImGui.Text("The list below contains all quests that appear in your journal.");
        ImGui.BulletText("'Supported' lists quests that Questionable can do for you");
        ImGui.BulletText("'Completed' lists quests your current character has completed.");
        ImGui.BulletText(
            "Not all quests can be completed even if they're listed as available, e.g. starting city quest chains.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using var table = ImRaii.Table("Quests", 3, ImGuiTableFlags.NoSavedSettings);
        if (!table)
            return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide);
        ImGui.TableSetupColumn("Supported", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("Completed", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableHeadersRow();

        foreach (var section in _journalData.Sections)
        {
            DrawSection(section);
        }
    }

    private void DrawSection(JournalData.Section section)
    {
        if (section.QuestCount == 0)
            return;

        (int supported, int completed) = _sectionCounts.GetValueOrDefault(section);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(section.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, section.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, section.QuestCount);

        if (open)
        {
            foreach (var category in section.Categories)
                DrawCategory(category);

            ImGui.TreePop();
        }
    }

    private void DrawCategory(JournalData.Category category)
    {
        if (category.QuestCount == 0)
            return;

        (int supported, int completed) = _categoryCounts.GetValueOrDefault(category);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(category.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, category.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, category.QuestCount);

        if (open)
        {
            foreach (var genre in category.Genres)
                DrawGenre(genre);

            ImGui.TreePop();
        }
    }

    private void DrawGenre(JournalData.Genre genre)
    {
        if (genre.QuestCount == 0)
            return;

        (int supported, int completed) = _genreCounts.GetValueOrDefault(genre);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(genre.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, genre.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, genre.QuestCount);

        if (open)
        {
            foreach (var quest in genre.Quests)
                DrawQuest(quest);

            ImGui.TreePop();
        }
    }

    private void DrawQuest(QuestInfo questInfo)
    {
        _questRegistry.TryGetQuest(questInfo.QuestId, out var quest);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TreeNodeEx(questInfo.Name,
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth);


        if (ImGui.IsItemClicked() && _commandManager.Commands.TryGetValue("/questinfo", out var commandInfo))
        {
            _commandManager.DispatchCommand("/questinfo",
                questInfo.QuestId.ToString(CultureInfo.InvariantCulture), commandInfo);
        }

        if (ImGui.IsItemHovered())
            _questTooltipComponent.Draw(questInfo);

        ImGui.TableNextColumn();
        List<string> authors = quest?.Root.Author ?? [];
        _uiUtils.ChecklistItem(authors.Count > 0 ? string.Join(", ", authors) : string.Empty,
            quest is { Root.Disabled: false });

        ImGui.TableNextColumn();
        var (color, icon, text) = _uiUtils.GetQuestStyle(questInfo.QuestId);
        _uiUtils.ChecklistItem(text, color, icon);
    }

    private static void DrawCount(int count, int total)
    {
        string len = 9999.ToString(CultureInfo.CurrentCulture);
        ImGui.PushFont(UiBuilder.MonoFont);

        string text =
            $"{count.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)} / {total.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)}";
        if (count == total)
            ImGui.TextColored(ImGuiColors.ParsedGreen, text);
        else
            ImGui.TextUnformatted(text);

        ImGui.PopFont();
    }

    private void RefreshCounts()
    {
        _genreCounts.Clear();
        _categoryCounts.Clear();
        _sectionCounts.Clear();

        foreach (var genre in _journalData.Genres)
        {
            int available = genre.Quests.Count(x =>
                _questRegistry.TryGetQuest(x.QuestId, out var quest) && !quest.Root.Disabled);
            int completed = genre.Quests.Count(x => _gameFunctions.IsQuestComplete(x.QuestId));
            _genreCounts[genre] = (available, completed);
        }

        foreach (var category in _journalData.Categories)
        {
            var counts = _genreCounts
                .Where(x => category.Genres.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int completed = counts.Sum(x => x.Completed);
            _categoryCounts[category] = (available, completed);
        }

        foreach (var section in _journalData.Sections)
        {
            var counts = _categoryCounts
                .Where(x => section.Categories.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int completed = counts.Sum(x => x.Completed);
            _sectionCounts[section] = (available, completed);
        }
    }

    private void ClearCounts()
    {
        foreach (var genreCount in _genreCounts.ToList())
            _genreCounts[genreCount.Key] = (genreCount.Value.Available, 0);

        foreach (var categoryCount in _categoryCounts.ToList())
            _categoryCounts[categoryCount.Key] = (categoryCount.Value.Available, 0);

        foreach (var sectionCount in _sectionCounts.ToList())
            _sectionCounts[sectionCount.Key] = (sectionCount.Value.Available, 0);
    }

    public void Dispose()
    {
        _questRegistry.Reloaded -= OnQuestsReloaded;
        _clientState.Logout -= ClearCounts;
        _clientState.Login -= RefreshCounts;
    }
}
