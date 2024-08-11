using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows;

internal sealed class JournalProgressWindow : LWindow, IDisposable
{
    private readonly JournalData _journalData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly ICommandManager _commandManager;

    private readonly Dictionary<JournalData.Genre, (int Available, int Completed)> _genreCounts = new();
    private readonly Dictionary<JournalData.Category, (int Available, int Completed)> _categoryCounts = new();
    private readonly Dictionary<JournalData.Section, (int Available, int Completed)> _sectionCounts = new();

    private List<FilteredSection> _filteredSections = [];
    private string _searchText = string.Empty;

    public JournalProgressWindow(JournalData journalData,
        QuestRegistry questRegistry,
        QuestFunctions questFunctions,
        UiUtils uiUtils,
        QuestTooltipComponent questTooltipComponent,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        ICommandManager commandManager)
        : base("Journal Progress###QuestionableJournalProgress")
    {
        _journalData = journalData;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _uiUtils = uiUtils;
        _questTooltipComponent = questTooltipComponent;
        _pluginInterface = pluginInterface;
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

    public override void OnOpen()
    {
        UpdateFilter();
        RefreshCounts();
    }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("Explanation", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text("The list below contains all quests that appear in your journal.");
            ImGui.BulletText("'Supported' lists quests that Questionable can do for you");
            ImGui.BulletText("'Completed' lists quests your current character has completed.");
            ImGui.BulletText(
                "Not all quests can be completed even if they're listed as available, e.g. starting city quest chains.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint(string.Empty, "Search quests and categories", ref _searchText, 256))
            UpdateFilter();

        if (_filteredSections.Count > 0)
        {
            using var table = ImRaii.Table("Quests", 3, ImGuiTableFlags.NoSavedSettings);
            if (!table)
                return;

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide);
            ImGui.TableSetupColumn("Supported", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn("Completed", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableHeadersRow();

            foreach (var section in _filteredSections)
            {
                DrawSection(section);
            }
        }
        else
            ImGui.Text("No quest or category matches your search text.");
    }

    private void DrawSection(FilteredSection filter)
    {
        if (filter.Section.QuestCount == 0)
            return;

        (int supported, int completed) = _sectionCounts.GetValueOrDefault(filter.Section);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Section.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, filter.Section.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, filter.Section.QuestCount);

        if (open)
        {
            foreach (var category in filter.Categories)
                DrawCategory(category);

            ImGui.TreePop();
        }
    }

    private void DrawCategory(FilteredCategory filter)
    {
        if (filter.Category.QuestCount == 0)
            return;

        (int supported, int completed) = _categoryCounts.GetValueOrDefault(filter.Category);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Category.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, filter.Category.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, filter.Category.QuestCount);

        if (open)
        {
            foreach (var genre in filter.Genres)
                DrawGenre(genre);

            ImGui.TreePop();
        }
    }

    private void DrawGenre(FilteredGenre filter)
    {
        if (filter.Genre.QuestCount == 0)
            return;

        (int supported, int completed) = _genreCounts.GetValueOrDefault(filter.Genre);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Genre.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, filter.Genre.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, filter.Genre.QuestCount);

        if (open)
        {
            foreach (var quest in filter.Quests)
                DrawQuest(quest);

            ImGui.TreePop();
        }
    }

    private void DrawQuest(IQuestInfo questInfo)
    {
        _questRegistry.TryGetQuest(questInfo.QuestId, out var quest);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TreeNodeEx(questInfo.Name,
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth);


        if (questInfo is QuestInfo && ImGui.IsItemClicked() && _commandManager.Commands.TryGetValue("/questinfo", out var commandInfo))
        {
            _commandManager.DispatchCommand("/questinfo", questInfo.QuestId.ToString() ?? string.Empty, commandInfo);
        }

        if (ImGui.IsItemHovered())
            _questTooltipComponent.Draw(questInfo);

        ImGui.TableNextColumn();
        float spacing;
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            spacing = ImGui.GetColumnWidth() / 2 - ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);
        _uiUtils.ChecklistItem(string.Empty, quest is { Root.Disabled: false });

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

    private void UpdateFilter()
    {
        Predicate<string> match;
        if (string.IsNullOrWhiteSpace(_searchText))
            match = _ => true;
        else
            match = x => x.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase);

        _filteredSections = _journalData.Sections
            .Select(section => FilterSection(section, match))
            .Where(x => x != null)
            .Cast<FilteredSection>()
            .ToList();
    }

    private static FilteredSection? FilterSection(JournalData.Section section, Predicate<string> match)
    {
        if (match(section.Name))
        {
            return new FilteredSection(section,
                section.Categories
                    .Select(x => FilterCategory(x, _ => true))
                    .Cast<FilteredCategory>()
                    .ToList());
        }
        else
        {
            List<FilteredCategory> filteredCategories = section.Categories
                .Select(category => FilterCategory(category, match))
                .Where(x => x != null)
                .Cast<FilteredCategory>()
                .ToList();
            if (filteredCategories.Count > 0)
                return new FilteredSection(section, filteredCategories);

            return null;
        }
    }

    private static FilteredCategory? FilterCategory(JournalData.Category category, Predicate<string> match)
    {
        if (match(category.Name))
        {
            return new FilteredCategory(category,
                category.Genres
                    .Select(x => FilterGenre(x, _ => true))
                    .Cast<FilteredGenre>()
                    .ToList());
        }
        else
        {
            List<FilteredGenre> filteredGenres = category.Genres
                .Select(genre => FilterGenre(genre, match))
                .Where(x => x != null)
                .Cast<FilteredGenre>()
                .ToList();
            if (filteredGenres.Count > 0)
                return new FilteredCategory(category, filteredGenres);

            return null;
        }
    }

    private static FilteredGenre? FilterGenre(JournalData.Genre genre, Predicate<string> match)
    {
        if (match(genre.Name))
            return new FilteredGenre(genre, genre.Quests);
        else
        {
            List<IQuestInfo> filteredQuests = genre.Quests
                .Where(x => match(x.Name))
                .ToList();
            if (filteredQuests.Count > 0)
                return new FilteredGenre(genre, filteredQuests);
        }

        return null;
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
            int completed = genre.Quests.Count(x => _questFunctions.IsQuestComplete(x.QuestId));
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

    private sealed record FilteredSection(JournalData.Section Section, List<FilteredCategory> Categories);

    private sealed record FilteredCategory(JournalData.Category Category, List<FilteredGenre> Genres);

    private sealed record FilteredGenre(JournalData.Genre Genre, List<IQuestInfo> Quests);
}
