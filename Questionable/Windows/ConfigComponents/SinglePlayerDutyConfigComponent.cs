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
    private static readonly List<(EClassJob ClassJob, string Name)> RoleQuestCategories =
    [
        (EClassJob.Paladin, "防护职业职能任务"),
        (EClassJob.WhiteMage, "治疗职业职能任务"),
        (EClassJob.Lancer, "近战职业职能任务"),
        (EClassJob.Bard, "远程物理职业职能任务"),
        (EClassJob.BlackMage, "远程魔法职业职能任务"),
    ];

#if false
    private readonly string[] _retryDifficulties = ["Normal", "Easy", "Very Easy"];
#endif

    private readonly TerritoryData _territoryData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly IDataManager _dataManager;
    private readonly ILogger<SinglePlayerDutyConfigComponent> _logger;
    private readonly List<(EClassJob ClassJob, int Category)> _sortedClassJobs;

    private ImmutableDictionary<EAetheryteLocation, List<SinglePlayerDutyInfo>> _startingCityBattles =
        ImmutableDictionary<EAetheryteLocation, List<SinglePlayerDutyInfo>>.Empty;

    private ImmutableDictionary<EExpansionVersion, List<SinglePlayerDutyInfo>> _mainScenarioBattles =
        ImmutableDictionary<EExpansionVersion, List<SinglePlayerDutyInfo>>.Empty;

    private ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>> _jobQuestBattles =
        ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>>.Empty;

    private ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>> _roleQuestBattles =
        ImmutableDictionary<EClassJob, List<SinglePlayerDutyInfo>>.Empty;

    private ImmutableList<SinglePlayerDutyInfo> _otherRoleQuestBattles = ImmutableList<SinglePlayerDutyInfo>.Empty;

    private ImmutableList<(string Label, List<SinglePlayerDutyInfo>)> _otherQuestBattles =
        ImmutableList<(string Label, List<SinglePlayerDutyInfo>)>.Empty;

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

        _sortedClassJobs = dataManager.GetExcelSheet<ClassJob>()
            .Where(x => x is { RowId: > 0, UIPriority: < 100 })
            .Select(x => (ClassJob: (EClassJob)x.RowId, Priority: x.UIPriority))
            .OrderBy(x => x.Priority)
            .Select(x => (x.ClassJob, x.Priority / 10))
            .ToList();
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
            (bool enabled, SinglePlayerDutyOptions options) = FindDutyOptions(questId, index);

            string name = $"{FormatLevel(questInfo.Level)} {questInfo.Name}";
            if (!string.IsNullOrEmpty(cfcData.Name) && !questInfo.Name.EndsWith(cfcData.Name, StringComparison.Ordinal))
                name += $" ({cfcData.Name})";

            if (questsWithMultipleBattles.Contains(questId))
                name += $" (Part {options.Index + 1})";
            else if (cfcData.ContentFinderConditionId is 674 or 691)
                name += " (Melee/Phys. Ranged)";

            var dutyInfo = new SinglePlayerDutyInfo(name, questInfo, cfcData, options, enabled);

            if (dutyInfo.IsLimsaStart)
                startingCityBattles[EAetheryteLocation.Limsa].Add(dutyInfo);
            else if (dutyInfo.IsGridaniaStart)
                startingCityBattles[EAetheryteLocation.Gridania].Add(dutyInfo);
            else if (dutyInfo.IsUldahStart)
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
            else if (dutyInfo.IsOtherRoleQuest)
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
                        .DistinctBy(y => y.ContentFinderConditionId)
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

    private (bool Enabled, SinglePlayerDutyOptions Options) FindDutyOptions(ElementId questId, byte index)
    {
        SinglePlayerDutyOptions options = new()
        {
            Index = 0,
            Enabled = false,
        };
        if (_questRegistry.TryGetQuest(questId, out var quest))
        {
            if (quest.Root.Disabled)
            {
                _logger.LogDebug("Disabling quest battle for quest {QuestId}, quest is disabled", questId);
                return (false, options);
            }
            else
            {
                var foundStep = quest.AllSteps()
                    .Select(x => x.Step)
                    .FirstOrDefault(x =>
                        x.InteractionType == EInteractionType.SinglePlayerDuty &&
                        x.SinglePlayerDutyIndex == index);
                if (foundStep == null)
                {
                    _logger.LogWarning(
                        "Disabling quest battle for quest {QuestId}, no battle with index {Index} found", questId,
                        index);
                    return (false, options);
                }
                else
                {
                    return (true, foundStep.SinglePlayerDutyOptions ?? options);
                }
            }
        }
        else
        {
            _logger.LogDebug("Disabling quest battle for quest {QuestId}, unknown quest", questId);
            return (false, options);
        }
    }

    private string BuildJournalGenreLabel(uint journalGenreId)
    {
        var journalGenre = _dataManager.GetExcelSheet<JournalGenre>().GetRow(journalGenreId);
        var journalCategory = journalGenre.JournalCategory.Value;

        string genreName = journalGenre.Name.ExtractText();
        string categoryName = journalCategory.Name.ExtractText();

        return $"{categoryName} \u203B {genreName}";
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("单人副本###QuestBattles");
        if (!tab)
            return;

        bool runSoloInstancesWithBossMod = Configuration.SinglePlayerDuties.RunSoloInstancesWithBossMod;
        if (ImGui.Checkbox("使用 BossMod 自动完成单人副本", ref runSoloInstancesWithBossMod))
        {
            Configuration.SinglePlayerDuties.RunSoloInstancesWithBossMod = runSoloInstancesWithBossMod;
            Save();
        }

        using (ImRaii.PushIndent(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X))
        {
            using (_ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
            {
                ImGui.TextUnformatted("此功能正在开发中，尚未完善：");
                ImGui.BulletText("将始终使用 BossMod 进行战斗（忽略配置的战斗模块）。");
                ImGui.BulletText("只有一小部分单人副本经过测试——其中大多数并不是主线任务。");
                ImGui.BulletText("重新尝试失败的战斗时，将始终从‘正常’难度开始。");
                ImGui.BulletText("请在使用 BossMod 的分支（如 Reborn）时不要启用此选项；\n由于缺少战斗模块配置，它可能不兼容。");
            }


#if false
            using (ImRaii.Disabled(!runSoloInstancesWithBossMod))
            {
                ImGui.Spacing();
                int retryDifficulty = Configuration.SinglePlayerDuties.RetryDifficulty;
                if (ImGui.Combo("Difficulty when retrying a quest battle", ref retryDifficulty, _retryDifficulties,
                        _retryDifficulties.Length))
                {
                    Configuration.SinglePlayerDuties.RetryDifficulty = (byte)retryDifficulty;
                    Save();
                }
            }
#endif
        }

        ImGui.Separator();

        using (ImRaii.Disabled(!runSoloInstancesWithBossMod))
        {
            ImGui.Text(
                "Questionable 包含了一份默认的单人副本列表，适用于安装了 BossMod 的情况。");
            ImGui.Text("每次更新后，包含的单人副本列表可能会发生变化。");

            ImGui.Separator();
            ImGui.Text("你可以单独调整每个单人副本的设置：");


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
        using var tab = ImRaii.TabItem("主线任务###MSQ");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        if (ImGui.CollapsingHeader($"利姆萨·罗敏萨 ({FormatLevel(5)} - {FormatLevel(14)})"))
            DrawQuestTable("LimsaLominsa", _startingCityBattles[EAetheryteLocation.Limsa]);

        if (ImGui.CollapsingHeader($"格里达尼亚 ({FormatLevel(5)} - {FormatLevel(14)})"))
            DrawQuestTable("Gridania", _startingCityBattles[EAetheryteLocation.Gridania]);

        if (ImGui.CollapsingHeader($"乌尔达哈 ({FormatLevel(4)} - {FormatLevel(14)})"))
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
        using var tab = ImRaii.TabItem("职业任务###JobQuests");
        if (!tab)
            return;

        using var child = BeginChildArea();
        if (!child)
            return;

        int oldPriority = 0;
        foreach (var (classJob, priority) in _sortedClassJobs)
        {
            if (_jobQuestBattles.TryGetValue(classJob, out var dutyInfos))
            {
                if (priority != oldPriority)
                {
                    oldPriority = priority;
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

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
        using var tab = ImRaii.TabItem("职能任务###RoleQuests");
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

        if (ImGui.CollapsingHeader("通用职能任务"))
            DrawQuestTable("RoleQuestsGeneral", _otherRoleQuestBattles);
    }

    private void DrawOtherQuestConfigTable()
    {
        using var tab = ImRaii.TabItem("其他任务###MiscQuests");
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

                string[] labels = dutyInfo.EnabledByDefault
                    ? SupportedCfcOptions
                    : UnsupportedCfcOptions;
                int value = 0;
                if (Configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Contains(dutyInfo.ContentFinderConditionId))
                    value = 1;
                if (Configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Contains(dutyInfo.ContentFinderConditionId))
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
                            ImGui.BulletText($"ContentFinderConditionId: {dutyInfo.ContentFinderConditionId}");
                        }
                    }

                    if (!dutyInfo.Enabled)
                    {
                        ImGuiComponents.HelpMarker("Questionable 尚未支持此任务.",
                            FontAwesomeIcon.Times, ImGuiColors.DalamudRed);
                    }
                    else if (dutyInfo.Notes.Count > 0)
                        DrawNotes(dutyInfo.EnabledByDefault, dutyInfo.Notes);
                }

                if (ImGui.TableNextColumn())
                {
                    using var _ = ImRaii.PushId($"##Duty{dutyInfo.ContentFinderConditionId}");
                    using (ImRaii.Disabled(!dutyInfo.Enabled))
                    {
                        ImGui.SetNextItemWidth(200);
                        if (ImGui.Combo(string.Empty, ref value, labels, labels.Length))
                        {
                            Configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Remove(dutyInfo.ContentFinderConditionId);
                            Configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Remove(dutyInfo.ContentFinderConditionId);

                            if (value == 1)
                                Configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Add(dutyInfo.ContentFinderConditionId);
                            else if (value == 2)
                                Configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Add(dutyInfo.ContentFinderConditionId);

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
            if (ImGui.Button("重置全部"))
            {
                Configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Clear();
                Configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Clear();
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住 Ctrl 解锁此按钮。");
    }

    private sealed record SinglePlayerDutyInfo(
        string Name,
        IQuestInfo QuestInfo,
        TerritoryData.ContentFinderConditionData ContentFinderConditionData,
        SinglePlayerDutyOptions Options,
        bool Enabled)
    {
        public EExpansionVersion Expansion => QuestInfo.Expansion;
        public uint JournalGenreId => QuestInfo.JournalGenre ?? uint.MaxValue;
        public ushort SortKey => QuestInfo.SortKey;
        public uint ContentFinderConditionId => ContentFinderConditionData.ContentFinderConditionId;
        public uint TerritoryId => ContentFinderConditionData.TerritoryId;
        public byte Index => Options.Index;
        public bool EnabledByDefault => Options.Enabled;
        public IReadOnlyList<string> Notes => Options.Notes;

        public bool IsLimsaStart => ContentFinderConditionId is 332 or 333 or 313 or 334;
        public bool IsGridaniaStart => ContentFinderConditionId is 296 or 297 or 299 or 298;
        public bool IsUldahStart => ContentFinderConditionId is 335 or 312 or 337 or 336;

        /// <summary>
        /// 'Other' role quest is the post-EW/DT role quests.
        /// </summary>
        public bool IsOtherRoleQuest => ContentFinderConditionId is 845 or 1016;
    }
}
