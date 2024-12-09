using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Validation;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestJournalComponent
{
    private readonly Dictionary<JournalData.Genre, JournalCounts> _genreCounts = [];
    private readonly Dictionary<JournalData.Category, JournalCounts> _categoryCounts = [];
    private readonly Dictionary<JournalData.Section, JournalCounts> _sectionCounts = [];

    private readonly JournalData _journalData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly UiUtils _uiUtils;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestJournalUtils _questJournalUtils;
    private readonly QuestValidator _questValidator;

    private List<FilteredSection> _filteredSections = [];
    private string _searchText = string.Empty;

    public QuestJournalComponent(JournalData journalData, QuestRegistry questRegistry, QuestFunctions questFunctions,
        UiUtils uiUtils, QuestTooltipComponent questTooltipComponent, IDalamudPluginInterface pluginInterface,
        QuestJournalUtils questJournalUtils, QuestValidator questValidator)
    {
        _journalData = journalData;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _uiUtils = uiUtils;
        _questTooltipComponent = questTooltipComponent;
        _pluginInterface = pluginInterface;
        _questJournalUtils = questJournalUtils;
        _questValidator = questValidator;
    }

    public void DrawQuests()
    {
        using var tab = ImRaii.TabItem("Quests");
        if (!tab)
            return;

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
                DrawSection(section);
        }
        else
            ImGui.Text("No quest or category matches your search text.");
    }

    private void DrawSection(FilteredSection filter)
    {
        if (filter.Section.QuestCount == 0)
            return;

        (int available, int obtainable, int completed) = _sectionCounts.GetValueOrDefault(filter.Section, new());

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Section.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(available, filter.Section.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

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

        (int available, int obtainable, int completed) = _categoryCounts.GetValueOrDefault(filter.Category, new());

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Category.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(available, filter.Category.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

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

        (int supported, int obtainable, int completed) = _genreCounts.GetValueOrDefault(filter.Genre, new());

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(filter.Genre.Name, ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(supported, filter.Genre.QuestCount);
        ImGui.TableNextColumn();
        DrawCount(completed, obtainable);

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
        ImGui.TreeNodeEx($"{questInfo.Name} ({questInfo.QuestId})",
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth);


        if (ImGui.IsItemHovered())
            _questTooltipComponent.Draw(questInfo);

        _questJournalUtils.ShowContextMenu(questInfo, quest, nameof(QuestJournalComponent));

        ImGui.TableNextColumn();
        float spacing;
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            spacing = ImGui.GetColumnWidth() / 2 - ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);

        if (quest is { Root.Disabled: false })
        {
            List<ValidationIssue> issues = _questValidator.GetIssues(quest.Id);
            if (issues.Any(x => x.Severity == EIssueSeverity.Error))
                _uiUtils.ChecklistItem(string.Empty, ImGuiColors.DalamudRed, FontAwesomeIcon.ExclamationTriangle);
            else if (issues.Count > 0)
                _uiUtils.ChecklistItem(string.Empty, ImGuiColors.ParsedBlue, FontAwesomeIcon.InfoCircle);
            else
                _uiUtils.ChecklistItem(string.Empty, true);
        }
        else
            _uiUtils.ChecklistItem(string.Empty, false);

        ImGui.TableNextColumn();
        var (color, icon, text) = _uiUtils.GetQuestStyle(questInfo.QuestId);
        _uiUtils.ChecklistItem(text, color, icon);
    }

    private static void DrawCount(int count, int total)
    {
        string len = 9999.ToString(CultureInfo.CurrentCulture);
        ImGui.PushFont(UiBuilder.MonoFont);

        if (total == 0)
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"{"-".PadLeft(len.Length)} / {"-".PadLeft(len.Length)}");
        else
        {
            string text =
                $"{count.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)} / {total.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)}";
            if (count == total)
                ImGui.TextColored(ImGuiColors.ParsedGreen, text);
            else
                ImGui.TextUnformatted(text);
        }

        ImGui.PopFont();
    }

    public void UpdateFilter()
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
                    .Select(x => FilterGenre(x, _ => true)!)
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

    internal void RefreshCounts()
    {
        _genreCounts.Clear();
        _categoryCounts.Clear();
        _sectionCounts.Clear();

        foreach (var genre in _journalData.Genres)
        {
            int available = genre.Quests.Count(x =>
                _questRegistry.TryGetQuest(x.QuestId, out var quest) && !quest.Root.Disabled);
            int obtainable = genre.Quests.Count(x => !_questFunctions.IsQuestUnobtainable(x.QuestId));
            int completed = genre.Quests.Count(x => _questFunctions.IsQuestComplete(x.QuestId));
            _genreCounts[genre] = new(available, obtainable, completed);
        }

        foreach (var category in _journalData.Categories)
        {
            var counts = _genreCounts
                .Where(x => category.Genres.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int obtainable = counts.Sum(x => x.Obtainable);
            int completed = counts.Sum(x => x.Completed);
            _categoryCounts[category] = new(available, obtainable, completed);
        }

        foreach (var section in _journalData.Sections)
        {
            var counts = _categoryCounts
                .Where(x => section.Categories.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            int available = counts.Sum(x => x.Available);
            int obtainable = counts.Sum(x => x.Obtainable);
            int completed = counts.Sum(x => x.Completed);
            _sectionCounts[section] = new(available, obtainable, completed);
        }
    }

    internal void ClearCounts(int type, int code)
    {
        foreach (var genreCount in _genreCounts.ToList())
            _genreCounts[genreCount.Key] = genreCount.Value with { Completed = 0 };

        foreach (var categoryCount in _categoryCounts.ToList())
            _categoryCounts[categoryCount.Key] = categoryCount.Value with { Completed = 0 };

        foreach (var sectionCount in _sectionCounts.ToList())
            _sectionCounts[sectionCount.Key] = sectionCount.Value with { Completed = 0 };
    }

    private sealed record FilteredSection(JournalData.Section Section, List<FilteredCategory> Categories);

    private sealed record FilteredCategory(JournalData.Category Category, List<FilteredGenre> Genres);

    private sealed record FilteredGenre(JournalData.Genre Genre, List<IQuestInfo> Quests);

    private sealed record JournalCounts(int Available = 0, int Obtainable = 0, int Completed = 0);
}
