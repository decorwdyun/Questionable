using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Controller.Steps.Interactions;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Windows.ConfigComponents;

internal sealed class SinglePlayerDutyConfigComponent : ConfigComponent
{
    private readonly TerritoryData _territoryData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly IDataManager _dataManager;
    private readonly ILogger<SinglePlayerDutyConfigComponent> _logger;

    private static readonly List<(EClassJob ClassJob, string Name)> RoleQuestCategories =
    [
        (EClassJob.Paladin, "Tank Role Quests"),
        (EClassJob.WhiteMage, "Healer Role Quests"),
        (EClassJob.Lancer, "Melee Role Quests"),
        (EClassJob.Bard, "Physical Ranged Role Quests"),
        (EClassJob.BlackMage, "Magical Ranged Role Quests"),
    ];

    private ImmutableDictionary<EAetheryteLocation, List<SinglePlayerDutyInfo>> _startingCityBattles = ImmutableDictionary<EAetheryteLocation, List<SinglePlayerDutyInfo>>.Empty;
    private ImmutableDictionary<EExpansionVersion, List<SinglePlayerDutyInfo>> _mainScenarioBattles = ImmutableDictionary<EExpansionVersion, List<SinglePlayerDutyInfo>>.Empty;
    private ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>> _jobQuestBattles = ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>>.Empty;
    private ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>> _roleQuestBattles = ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>>.Empty;
    private ImmutableList<SinglePlayerDutyInfo> _otherRoleQuestBattles = ImmutableList<SinglePlayerDutyInfo>.Empty;
    private ImmutableList<(string Label, List<SinglePlayerDutyInfo>)> _otherQuestBattles = ImmutableList<(string Label, List<SinglePlayerDutyInfo>)>.Empty;

    public SinglePlayerDutyConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        TerritoryData territoryData,
        QuestRegistry questRegistry,
        QuestData questData,
        IDataManager dataManager,
        ILogger<SinglePlayerDutyConfigComponent> logger)
        : base(pluginInterface, configuration)
    {
        _territoryData = territoryData;
        _questRegistry = questRegistry;
        _questData = questData;
        _dataManager = dataManager;
        _logger = logger;
    }

    public void Reload()
    {
        List<ElementId> questsWithMultipleBattles = _territoryData.GetAllQuestsWithQuestBattles()
            .GroupBy(x => x.QuestId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        List<SinglePlayerDutyInfo> mainScenarioBattles = [];
        Dictionary<EAetheryteLocation, List<SinglePlayerDutyInfo>> startingCityBattles =
            new()
            {
                { EAetheryteLocation.Limsa, [] },
                { EAetheryteLocation.Gridania, [] },
                { EAetheryteLocation.Uldah, [] },
            };

        List<SinglePlayerDutyInfo> otherBattles = [];

        Dictionary<ElementId, EClassJob> questIdsToJob = Enum.GetValues<EClassJob>()
            .Where(x => x != EClassJob.Adventurer && !x.IsCrafter() && !x.IsGatherer())
            .Where(x => x.IsClass() || !x.HasBaseClass())
            .SelectMany(x => _questRegistry.GetKnownClassJobQuests(x, false).Select(y => (y.QuestId, ClassJob: x)))
            .ToDictionary(x => x.QuestId, x => x.ClassJob);
        Dictionary<EClassJob, List<SinglePlayerDutyInfo>> jobQuestBattles = questIdsToJob.Values.Distinct()
            .ToDictionary(x => x, _ => new List<SinglePlayerDutyInfo>());

        Dictionary<ElementId, List<EClassJob>> questIdToRole = RoleQuestCategories
            .SelectMany(x => _questData.GetRoleQuests(x.ClassJob).Select(y => (y.QuestId, x.ClassJob)))
            .GroupBy(x => x.QuestId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ClassJob).ToList());
        Dictionary<EClassJob, List<SinglePlayerDutyInfo>> roleQuestBattles = RoleQuestCategories
            .ToDictionary(x => x.ClassJob, _ => new List<SinglePlayerDutyInfo>());
        List<SinglePlayerDutyInfo> otherRoleQuestBattles = [];

        foreach (var (questId, index, cfcData) in _territoryData.GetAllQuestsWithQuestBattles())
        {
            IQuestInfo questInfo = _questData.GetQuestInfo(questId);
            QuestStep questStep = new QuestStep
                {
                    SinglePlayerDutyIndex = 0,
                    BossModEnabled = false,
                };
            bool enabled;
            if (_questRegistry.TryGetQuest(questId, out var quest))
            {
                if (quest.Root.Disabled)
                {
                    _logger.LogDebug("Disabling quest battle for quest {QuestId}, quest is disabled", questId);
                    enabled = false;
                }
                else
                {
                    var foundStep = quest.AllSteps().FirstOrDefault(x =>
                        x.Step.InteractionType == EInteractionType.SinglePlayerDuty &&
                        x.Step.SinglePlayerDutyIndex == index);
                    if (foundStep == default)
                    {
                        _logger.LogWarning("Disabling quest battle for quest {QuestId}, no battle with index {Index} found", questId, index);
                        enabled = false;
                    }
                    else
                    {
                        questStep = foundStep.Step;
                        enabled = true;
                    }
                }
            }
            else
            {
                _logger.LogDebug("Disabling quest battle for quest {QuestId}, unknown quest", questId);
                enabled = false;
            }

            string name = $"{FormatLevel(questInfo.Level)} {questInfo.Name}";
            if (!string.IsNullOrEmpty(cfcData.Name) && !questInfo.Name.EndsWith(cfcData.Name, StringComparison.Ordinal))
                name += $" ({cfcData.Name})";

            if (questsWithMultipleBattles.Contains(questId))
                name += $" (Part {questStep.SinglePlayerDutyIndex + 1})";
            else if (cfcData.ContentFinderConditionId is 674 or 691)
                name += " (Melee/Phys. Ranged)";

            var dutyInfo = new SinglePlayerDutyInfo(
                cfcData.ContentFinderConditionId,
                cfcData.TerritoryId,
                name,
                questInfo.Expansion,
                questInfo.JournalGenre ?? uint.MaxValue,
                questInfo.SortKey,
                questStep.SinglePlayerDutyIndex,
                enabled,
                questStep.BossModEnabled);

            if (cfcData.ContentFinderConditionId is 332 or 333 or 313 or 334)
                startingCityBattles[EAetheryteLocation.Limsa].Add(dutyInfo);
            else if (cfcData.ContentFinderConditionId is 296 or 297 or 299 or 298)
                startingCityBattles[EAetheryteLocation.Gridania].Add(dutyInfo);
            else if (cfcData.ContentFinderConditionId is 335 or 312 or 337 or 336)
                startingCityBattles[EAetheryteLocation.Uldah].Add(dutyInfo);
            else if (questInfo.IsMainScenarioQuest)
                mainScenarioBattles.Add(dutyInfo);
            else if (questIdsToJob.TryGetValue(questId, out EClassJob classJob))
                jobQuestBattles[classJob].Add(dutyInfo);
            else if (questIdToRole.TryGetValue(questId, out var classJobs))
            {
                foreach (var roleClassJob in classJobs)
                    roleQuestBattles[roleClassJob].Add(dutyInfo);
            }
            else if (dutyInfo.CfcId is 845 or 1016)
                otherRoleQuestBattles.Add(dutyInfo);
            else
                otherBattles.Add(dutyInfo);
        }

        _startingCityBattles = startingCityBattles
            .ToImmutableDictionary(x => x.Key,
                x => x.Value.OrderBy(y => y.SortKey)
                    .ToList());
        _mainScenarioBattles = mainScenarioBattles
            .GroupBy(x => x.Expansion)
            .ToImmutableDictionary(x => x.Key,
                x =>
                    x.OrderBy(y => y.JournalGenreId)
                        .ThenBy(y => y.SortKey)
                        .ThenBy(y => y.Index)
                        .ToList());
        _jobQuestBattles = jobQuestBattles
            .Where(x => x.Value.Count > 0)
            .ToImmutableDictionary(x => x.Key,
                x =>
                    x.Value
                        // level 10 quests use the same quest battle for [you started as this class] and [you picked this class up later]
                        .DistinctBy(y => y.CfcId)
                        .OrderBy(y => y.JournalGenreId)
                        .ThenBy(y => y.SortKey)
                        .ThenBy(y => y.Index)
                        .ToList());
        _roleQuestBattles = roleQuestBattles
            .ToImmutableDictionary(x => x.Key,
                x =>
                    x.Value.OrderBy(y => y.JournalGenreId)
                        .ThenBy(y => y.SortKey)
                        .ThenBy(y => y.Index)
                        .ToList());
        _otherRoleQuestBattles = otherRoleQuestBattles.ToImmutableList();
        _otherQuestBattles = otherBattles
            .OrderBy(x => x.JournalGenreId)
            .ThenBy(x => x.SortKey)
            .ThenBy(x => x.Index)
            .GroupBy(x => x.JournalGenreId)
            .Select(x => (BuildJournalGenreLabel(x.Key), x.ToList()))
            .ToImmutableList();
    }

    private string BuildJournalGenreLabel(uint journalGenreId)
    {
        var journalGenre = _dataManager.GetExcelSheet<JournalGenre>().GetRow(journalGenreId);
        var journalCategory = journalGenre.JournalCategory.Value;

        string genreName = journalGenre.Name.ExtractText();
        string categoryName = journalCategory.Name.ExtractText();

        return $"{categoryName} {SeIconChar.ArrowRight.ToIconString()} {genreName}";
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("Quest Battles###QuestBattles");
        if (!tab)
            return;

        bool runSoloInstancesWithBossMod = Configuration.SinglePlayerDuties.RunSoloInstancesWithBossMod;
        if (ImGui.Checkbox("Run quest battles with BossMod", ref runSoloInstancesWithBossMod))
        {
            Configuration.SinglePlayerDuties.RunSoloInstancesWithBossMod = runSoloInstancesWithBossMod;
            Save();
        }

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "Work in Progress: For now, this will always use BossMod for combat.");

        ImGui.Separator();

        using (ImRaii.Disabled(!runSoloInstancesWithBossMod))
        {
            ImGui.Text(
                "Questionable includes a default list of quest battles that work if BossMod is installed.");
            ImGui.Text("The included list of quest battles can change with each update.");

            ImGui.Separator();
            ImGui.Text("You can override the settings for each individual quest battle:");


            using var tabBar = ImRaii.TabBar("QuestionableConfigTabs");
            if (tabBar)
            {
                DrawMainScenarioConfigTable();
                DrawJobQuestConfigTable();
                DrawRoleQuestConfigTable();
                DrawOtherQuestConfigTable();
            }

            DrawResetButton();
        }
    }

    private void DrawMainScenarioConfigTable()
    {
        using var tab = ImRaii.TabItem("MSQ###MSQ");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        if (ImGui.CollapsingHeader($"Limsa Lominsa ({FormatLevel(5)} - {FormatLevel(14)})"))
            DrawQuestTable("LimsaLominsa", _startingCityBattles[EAetheryteLocation.Limsa]);

        if (ImGui.CollapsingHeader($"Gridania ({FormatLevel(5)} - {FormatLevel(14)})"))
            DrawQuestTable("Gridania", _startingCityBattles[EAetheryteLocation.Gridania]);

        if (ImGui.CollapsingHeader($"Ul'dah ({FormatLevel(4)} - {FormatLevel(14)})"))
            DrawQuestTable("Uldah", _startingCityBattles[EAetheryteLocation.Uldah]);

        foreach (EExpansionVersion expansion in Enum.GetValues<EExpansionVersion>())
        {
            if (_mainScenarioBattles.TryGetValue(expansion, out var dutyInfos))
            {
                if (ImGui.CollapsingHeader(expansion.ToFriendlyString()))
                    DrawQuestTable($"Duties{expansion}", dutyInfos);
            }
        }
    }

    private void DrawJobQuestConfigTable()
    {
        using var tab = ImRaii.TabItem("Class/Job Quests###JobQuests");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        foreach (EClassJob classJob in Enum.GetValues<EClassJob>())
        {
            if (_jobQuestBattles.TryGetValue(classJob, out var dutyInfos))
            {
                string jobName = classJob.ToFriendlyString();
                if (classJob.IsClass())
                    jobName += $" / {classJob.AsJob().ToFriendlyString()}";

                if (ImGui.CollapsingHeader(jobName))
                    DrawQuestTable($"JobQuests{classJob}", dutyInfos);
            }
        }
    }

    private void DrawRoleQuestConfigTable()
    {
        using var tab = ImRaii.TabItem("Role Quests###RoleQuests");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        foreach (var (classJob, label) in RoleQuestCategories)
        {
            if (_roleQuestBattles.TryGetValue(classJob, out var dutyInfos))
            {
                if (ImGui.CollapsingHeader(label))
                    DrawQuestTable($"RoleQuests{classJob}", dutyInfos);
            }
        }

        if(ImGui.CollapsingHeader("General Role Quests"))
            DrawQuestTable("RoleQuestsGeneral", _otherRoleQuestBattles);
    }

    private void DrawOtherQuestConfigTable()
    {
        using var tab = ImRaii.TabItem("Other Quests###MiscQuests");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        foreach (var (label, dutyInfos) in _otherQuestBattles)
        {
            if (ImGui.CollapsingHeader(label))
                DrawQuestTable($"Other{label}", dutyInfos);
        }
    }

    private void DrawQuestTable(string label, IReadOnlyList<SinglePlayerDutyInfo> dutyInfos)
    {
        using var table = ImRaii.Table(label, 2, ImGuiTableFlags.SizingFixedFit);
        if (table)
        {
            ImGui.TableSetupColumn("Quest", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed, 200f);

            foreach (var dutyInfo in dutyInfos)
            {
                ImGui.TableNextRow();

                string[] labels = dutyInfo.BossModEnabledByDefault
                    ? SupportedCfcOptions
                    : UnsupportedCfcOptions;
                int value = 0;
                if (Configuration.Duties.WhitelistedDutyCfcIds.Contains(dutyInfo.CfcId))
                    value = 1;
                if (Configuration.Duties.BlacklistedDutyCfcIds.Contains(dutyInfo.CfcId))
                    value = 2;

                if (ImGui.TableNextColumn())
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(dutyInfo.Name);

                    if (ImGui.IsItemHovered() && Configuration.Advanced.AdditionalStatusInformation)
                    {
                        using var tooltip = ImRaii.Tooltip();
                        if (tooltip)
                        {
                            ImGui.TextUnformatted(dutyInfo.Name);
                            ImGui.Separator();
                            ImGui.BulletText($"TerritoryId: {dutyInfo.TerritoryId}");
                            ImGui.BulletText($"ContentFinderConditionId: {dutyInfo.CfcId}");
                        }
                    }

                    if (!dutyInfo.Enabled)
                    {
                        ImGuiComponents.HelpMarker("Questionable doesn't include support for this quest.",
                            FontAwesomeIcon.Times, ImGuiColors.DalamudRed);
                    }
                }

                if (ImGui.TableNextColumn())
                {
                    using var _ = ImRaii.PushId($"##Duty{dutyInfo.CfcId}");
                    using (ImRaii.Disabled(!dutyInfo.Enabled))
                    {
                        ImGui.SetNextItemWidth(200);
                        if (ImGui.Combo(string.Empty, ref value, labels, labels.Length))
                        {
                            Configuration.Duties.WhitelistedDutyCfcIds.Remove(dutyInfo.CfcId);
                            Configuration.Duties.BlacklistedDutyCfcIds.Remove(dutyInfo.CfcId);

                            if (value == 1)
                                Configuration.Duties.WhitelistedDutyCfcIds.Add(dutyInfo.CfcId);
                            else if (value == 2)
                                Configuration.Duties.BlacklistedDutyCfcIds.Add(dutyInfo.CfcId);

                            Save();
                        }
                    }
                }
            }
        }
    }

    private static ImRaii.IEndObject BeginChildArea() => ImRaii.Child("DutyConfiguration", new Vector2(650, 400), true);

    private void DrawResetButton()
    {
        using (ImRaii.Disabled(!ImGui.IsKeyDown(ImGuiKey.ModCtrl)))
        {
            if (ImGui.Button("Reset to default"))
            {
                Configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Clear();
                Configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Clear();
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL to enable this button.");
    }

    private sealed record SinglePlayerDutyInfo(
        uint CfcId,
        uint TerritoryId,
        string Name,
        EExpansionVersion Expansion,
        uint JournalGenreId,
        ushort SortKey,
        byte Index,
        bool Enabled,
        bool BossModEnabledByDefault);
}
