using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using Questionable.External;
using Questionable.Model.Questing;
using Questionable.Utils;
using static Questionable.Utils.LocalizeShortcut;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows.ConfigComponents;

internal sealed class GeneralConfigComponent : ConfigComponent
{
    private static readonly (uint Id, string Name) DefaultMount = (0, _L("随机坐骑"));
    private static readonly (Job ClassJob, string Name) DefaultClassJob = (Job.ADV, _L("自动（等级/装等最高的）"));

    private readonly string[] _grandCompanyNames =
        [_L("未选择（需要时再手动）"), _L("黑涡团"), _L("双蛇党"), _L("恒辉队")];

    private readonly QuestRegistry _questRegistry;
    private readonly TerritoryData _territoryData;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly DailyRoutinesIpc _dailyRoutinesIpc;
    private readonly Lazy<List<Job>> _sortedClassJobs;
    private readonly Lazy<(uint[] Ids, string[] Names)> _mounts;
    private readonly Lazy<(Job[] Ids, string[] Names)> _classJobs;
    private readonly Lazy<(Job[] Ids, string[] Names)> _craftJobs;
    private readonly Lazy<(Job[] Ids, string[] Names)> _gatherJobs;
    private string _mountSearchString = string.Empty;
    private string _langSearchString = string.Empty;

    public GeneralConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        IDataManager dataManager,
        ClassJobUtils classJobUtils,
        QuestRegistry questRegistry,
        TerritoryData territoryData,
        DailyRoutinesIpc dailyRoutinesIpc)
        : base(pluginInterface, configuration)
    {
        _questRegistry = questRegistry;
        _territoryData = territoryData;
        _pluginInterface = pluginInterface;
        _dailyRoutinesIpc = dailyRoutinesIpc;

        _sortedClassJobs = new(() => [.. classJobUtils.SortedClassJobs.Select(x => x.ClassJob)]);
        _mounts = new(() => BuildMounts(dataManager));
        _classJobs = new(() => BuildJobList(
            Enum.GetValues<Job>().Where(x => x != Job.ADV && !x.IsCrafter() && !x.IsGatherer() && !x.IsClass()),
            prependDefault: true));
        _craftJobs = new(() => BuildJobList(
            Enum.GetValues<Job>().Where(x => x != Job.ADV && x.IsCrafter()),
            prependDefault: false));
        _gatherJobs = new(() => BuildJobList(
            Enum.GetValues<Job>().Where(x => x == Job.MIN || x == Job.BTN),
            prependDefault: false));
    }

    private static (uint[] Ids, string[] Names) BuildMounts(IDataManager dataManager)
    {
        List<(uint MountId, string Name)> mounts = dataManager.GetExcelSheet<Mount>()
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        uint[] ids = [DefaultMount.Id, .. mounts.Select(x => x.MountId)];
        string[] names = [DefaultMount.Name, .. mounts.Select(x => x.Name)];
        return (ids, names);
    }

    private (Job[] Ids, string[] Names) BuildJobList(IEnumerable<Job> source, bool prependDefault)
    {
        List<Job> sorted = _sortedClassJobs.Value;
        List<Job> jobs = [.. source.OrderBy(x => sorted.IndexOf(x))];
        if (prependDefault)
        {
            Job[] ids = [DefaultClassJob.ClassJob, .. jobs];
            string[] names = [DefaultClassJob.Name, .. jobs.Select(x => x.ToString())];
            return (ids, names);
        }
        else
        {
            return ([.. jobs], [.. jobs.Select(x => x.ToString())]);
        }
    }

    public override void DrawTab()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("通用") + "###General");
        if (!tab)
            return;
        Dictionary<string, string> languages = new(){
            { "en",    _L("English") },
            { "ja-jp", _L("Japanese") },
            { "zh-cn", _L("Chinese (Simplified)") },
            { "af",    _L("Afrikaans") + " (WIP)" },
            { "ar",    _L("Arabic") + " (WIP)" },
            { "sq",    _L("Albanian") + " (WIP)" },
            { "eu",    _L("Basque") + " (WIP)" },
            { "be",    _L("Belarusian") + " (WIP)" },
            { "bg",    _L("Bulgarian") + " (WIP)" },
            { "ca",    _L("Catalan") + " (WIP)" },
            { "zh-tw", _L("Chinese (Traditional)") + " (WIP)" },
            { "hr",    _L("Croatian") + " (WIP)" },
            { "cs",    _L("Czech") + " (WIP)" },
            { "en-au", _L("English (Australian)") + " (WIP)" },
            { "fr",    _L("French") + " (WIP)" },
            { "de",    _L("German") + " (WIP)" },
            { "es",    _L("Spanish") + " (WIP)" },
        };
        string language = Configuration.General.Language;
        if (ImGuiComponentsLocal.DrawSearchableCombo(_L("Language"), languages.Keys.ToArray(), languages.Values.ToArray(),
            Configuration.General.Language, ref _langSearchString, ref language))
        {
            var was = Configuration.General.Language;
            Configuration.General.Language = language;
            Save();
            if (was != Configuration.General.Language)
                DalamudInitializer.SetupI18N(Configuration.General.Language);
        }

        Configuration.ECombatModule combatModule = Configuration.General.CombatModule;
        if (ImGuiEx.EnumCombo(_L("首选战斗模块"), ref combatModule))
        {
            Configuration.General.CombatModule = combatModule;
            Save();
        }

        (uint[] mountIds, string[] mountNames) = _mounts.Value;
        uint mountId = Configuration.General.MountId;
        if (ImGuiComponentsLocal.DrawSearchableCombo(_L("首选坐骑"), mountIds, mountNames,
            Configuration.General.MountId, ref _mountSearchString, ref mountId))
        {
            Configuration.General.MountId = mountId;
            Save();
        }

        int grandCompany = (int)Configuration.General.GrandCompany;
        if (ImGui.Combo(_L("首选部队阵营"), ref grandCompany, _grandCompanyNames,
            _grandCompanyNames.Length))
        {
            Configuration.General.GrandCompany = (GrandCompany)grandCompany;
            Save();
        }

        (Job[] classJobIds, string[] classJobNames) = _classJobs.Value;
        DrawComboOption(_L("Preferred Combat Job"), classJobIds, classJobNames,
            () => Configuration.General.CombatJob,
            v => Configuration.General.CombatJob = v);


        (Job[] craftJobIds, string[] craftJobNames) = _craftJobs.Value;
        DrawComboOption(_L("首选生产职业"), craftJobIds, craftJobNames,
            () => Configuration.General.CraftingJob,
            v => Configuration.General.CraftingJob = v);

        (Job[] gatherJobIds, string[] gatherJobNames) = _gatherJobs.Value;
        DrawComboOption(_L("首选采集职业"), gatherJobIds, gatherJobNames,
            () => Configuration.General.GatheringJob,
            v => Configuration.General.GatheringJob = v);


        using (ImRaii.Disabled(!StylistIpc.IsInstalled))
        {
            Configuration.EGearsetUpdateSource gearsetSource = Configuration.General.GearsetUpdateSource;
            if (ImGuiEx.EnumCombo(_L("装备管理器（一键最强）"), ref gearsetSource))
            {
                Configuration.General.GearsetUpdateSource = gearsetSource;
                Save();
            }
            if (!StylistIpc.IsInstalled && gearsetSource is Configuration.EGearsetUpdateSource.Stylist)
            {
                Svc.Chat.Print(_L("你设置了使用 Stylist 管理装备，但该插件未安装。已重置为默认。"), CommandHandler.MessageTag, CommandHandler.TagColor);
                Configuration.General.GearsetUpdateSource = Configuration.EGearsetUpdateSource.Vanilla;
                Save();
            }
        }

        string chocoboName = Configuration.General.ChocoboName;
        if (ImGui.InputText(_L("陆行鸟名字"), ref chocoboName, 20))
            Configuration.General.ChocoboName = chocoboName;

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (string.IsNullOrWhiteSpace(Configuration.General.ChocoboName))
                Configuration.General.ChocoboName = _L("陆行鸟");
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                ImGui.Text(_L("在\"我的专属陆行鸟\"任务中为你的陆行鸟取的名字。"));
                ImGui.Text(_L("如果留空，将默认为\"陆行鸟\"。"));
            }
        }

        string displayName = Configuration.General.DisplayName;
        if (ImGui.InputText(_L("Display name"), ref displayName, 20))
            Configuration.General.DisplayName = displayName;

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (string.IsNullOrWhiteSpace(Configuration.General.DisplayName))
                Configuration.General.DisplayName = _L("Anonymous");
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                ImGui.Text(_L("The name associated with submissions to help with QST's development."));
                ImGui.Text(_L("Defaults to \"Anonymous\" if left blank."));
            }
        }

        ImGui.Separator();
        ImGui.Text(_L("界面"));
        using (ImRaii.PushIndent())
        {
            bool hideInAllInstances = Configuration.General.HideInAllInstances;
            if (ImGui.Checkbox(_L("在所有副本中隐藏任务窗口"), ref hideInAllInstances))
            {
                Configuration.General.HideInAllInstances = hideInAllInstances;
                Save();
            }

            bool useEscToCancelQuesting = Configuration.General.UseEscToCancelQuesting;
            if (ImGui.Checkbox(_L("使用 ESC 取消任务/移动"), ref useEscToCancelQuesting))
            {
                Configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
                Save();
            }

            bool showIncompleteSeasonalEvents = Configuration.General.ShowIncompleteSeasonalEvents;
            if (ImGui.Checkbox(_L("显示未完成季节活动详情"), ref showIncompleteSeasonalEvents))
            {
                Configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
                Save();
            }

            bool hideSponsorButton = Configuration.General.HideSponsorButton;
            if (ImGui.Checkbox(_L("隐藏赞助按钮"), ref hideSponsorButton))
            {
                Configuration.General.HideSponsorButton = hideSponsorButton;
                Save();
            }
        }

#if REPORTING
        ImGui.Separator();
        ImGui.Text(_L("问题反馈"));
        using (ImRaii.PushIndent())
        {
            bool reportOptOut = Configuration.General.ReportsDisabled;
            if (ImGui.Checkbox(_L("不发送问题反馈"), ref reportOptOut))
            {
                Configuration.General.ReportsDisabled = reportOptOut;
                Configuration.General.DismissedReportWarning = true;
                Save();
            }

            bool dismissedReportWarning = Configuration.General.DismissedReportWarning;
            if (ImGui.Checkbox(_L("隐藏问题反馈提醒"), ref dismissedReportWarning))
            {
                Configuration.General.DismissedReportWarning = dismissedReportWarning;
                Save();
            }

            if (!reportOptOut)
            {
                string reportMessage = Configuration.General.ReportMessage;
                if (ImGui.InputText(_L("反馈备注"), ref reportMessage, 256))
                {
                    Configuration.General.ReportMessage = reportMessage;
                    Save();
                }
            }
        }
#endif

        ImGui.Separator();
        ImGui.Text(_L("任务设置"));
        using (ImRaii.PushIndent())
        {
            bool configureTextAdvance = Configuration.General.ConfigureTextAdvance;
            if (ImGui.Checkbox(_L("自动配置 TextAdvance"),
                ref configureTextAdvance))
            {
                Configuration.General.ConfigureTextAdvance = configureTextAdvance;
                Save();
            }

            if (configureTextAdvance)
            {
                bool dontSkipCutscenes = Configuration.General.DontSkipCutscenes;
                using (ImRaii.PushIndent())
                {
                    if (ImGui.Checkbox(_L("但不跳过过场和对话"), ref dontSkipCutscenes))
                    {
                        Configuration.General.DontSkipCutscenes = dontSkipCutscenes;
                        Save();
                    }
                }
                if (dontSkipCutscenes)
                {
                    using (ImRaii.PushIndent(2))
                    {
                        bool dontShowAnswerSuggestions = Configuration.General.DontShowAnswerSuggestions;
                        if (ImGui.Checkbox(_L("并且不显示系统会帮你选择的答案"), ref dontShowAnswerSuggestions))
                        {
                            Configuration.General.DontShowAnswerSuggestions = dontShowAnswerSuggestions;
                            Save();
                        }
                    }
                }
            }

            bool skipLowPriorityInstances = Configuration.General.SkipLowPriorityDuties;
            if (ImGui.Checkbox(_L("解锁部分可选副本和大型任务（而不是等待手动完成）"), ref skipLowPriorityInstances))
            {
                Configuration.General.SkipLowPriorityDuties = skipLowPriorityInstances;
                Save();
            }

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text(_L("Questionable 插件会自动接取一些可选任务（例如风脉泉任务，或 2.0 版本的 24 人团队任务）。"));
                    ImGui.Text(_L("如果开启此设置，Questionable 将继续推进其他任务，而不会等待你手动完成该副本。"));

                    ImGui.Separator();
                    ImGui.Text(_L("此设置将影响以下副本和大型任务："));
                    foreach ((uint ContentFinderConditionId, ElementId QuestId, int Sequence) lowPriorityCfc in _questRegistry.LowPriorityContentFinderConditionQuests)
                    {
                        if (_territoryData.TryGetContentFinderCondition(lowPriorityCfc.ContentFinderConditionId, out TerritoryData.ContentFinderConditionData? cfcData))
                            ImGui.BulletText($"{cfcData.Name}");
                    }
                }
            }

            bool useTickets = Configuration.General.UseTickets;
            if (ImGui.Checkbox(_L("可用时使用传送券"), ref useTickets))
            {
                Configuration.General.UseTickets = useTickets;
                Save();
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text(_L("最好在游戏内传送设置中配置，这里只是为了方便。"));
                }
            }

#if false
            ImGui.Spacing();
            bool autoStepRefreshEnabled = Configuration.General.AutoStepRefreshEnabled;
            if (ImGui.Checkbox(_L("卡住时自动刷新任务步骤（开发中，见提示）"), ref autoStepRefreshEnabled))
            {
                Configuration.General.AutoStepRefreshEnabled = autoStepRefreshEnabled;
                Save();
            }

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
            }

            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text(_L("如果任务步骤在配置的延迟后疑似卡住，Questionable 会自动刷新该步骤。"));
                    ImGui.Text(_L("这有助于在中断发生后恢复自动任务。"));
                    ImGui.Text(_L("此功能仍在开发中，可能并不完整。"));
                }
            }

            using (ImRaii.Disabled(!autoStepRefreshEnabled))
            {
                ImGui.Indent();
                int autoStepRefreshDelay = Configuration.General.AutoStepRefreshDelaySeconds;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderInt(_L("刷新延迟（秒）"), ref autoStepRefreshDelay, 30, 180))
                {
                    Configuration.General.AutoStepRefreshDelaySeconds = autoStepRefreshDelay;
                    Save();
                }

                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f),
                    _LF("如果 {0} 秒内没有进展，任务步骤将自动刷新。", autoStepRefreshDelay));
                ImGui.Unindent();
            }
#endif
        }

        if (_pluginInterface.InstalledPlugins.Any(x => x is { InternalName: "DailyRoutines", IsLoaded: true }))
        {
            ImGui.Separator();
            ImGui.Text("DailyRoutines 兼容性");
            using (ImRaii.PushIndent())
            {
                bool configureDailyRoutines = Configuration.General.ConfigureDailyRoutines;
                if (ImGui.Checkbox("插件工作时临时禁用 Daily Routines 中的冲突模块", ref configureDailyRoutines))
                {
                    Configuration.General.ConfigureDailyRoutines = configureDailyRoutines;
                    Save();
                }

                ImGuiComponents.HelpMarker($"{string.Join("\n", _dailyRoutinesIpc.ConflictingModules)}\n如果你发现还有其他冲突模块未列入，请联系汉化作者。");

                bool useDailyRoutinesTeleport = Configuration.General.UsingDailyRoutinesTeleport;
                if (ImGui.Checkbox("使用 Daily Routines 进行小水晶传送（请仔细阅读右侧说明）",
                        ref useDailyRoutinesTeleport))
                {
                    Configuration.General.UsingDailyRoutinesTeleport = useDailyRoutinesTeleport;
                    Save();
                }

                ImGuiComponents.HelpMarker("使用【更好的传送界面】模块，如果未启用将帮你自动启用。\n" +
                                           "勾选后，主城内将不会再寻路前往小水晶传送，而是直接瞬移，使用请自负风险。\n" +
                                           "如果遇到任何问题，请安装 Lifestream 并禁用此选项。");
            }
        }
    }
}
