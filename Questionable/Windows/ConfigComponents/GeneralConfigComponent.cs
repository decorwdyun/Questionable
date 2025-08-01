using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using Questionable.External;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows.ConfigComponents;

internal sealed class GeneralConfigComponent : ConfigComponent
{
    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "随机坐骑")];
    private static readonly List<(EClassJob ClassJob, string Name)> DefaultClassJobs = [(EClassJob.Adventurer, "自动（等级/装等最高的）")];
    private readonly QuestRegistry _questRegistry;
    private readonly TerritoryData _territoryData;

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly DailyRoutinesIpc _dailyRoutinesIpc;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;
    private readonly string[] _combatModuleNames = ["未选择", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn", "AEAssist"];

    private readonly string[] _grandCompanyNames = ["未选择（需要时再手动）", "黑涡团", "双蛇党", "恒辉队"];

    private readonly EClassJob[] _classJobIds;
    private readonly string[] _classJobNames;

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

        var mounts = dataManager.GetExcelSheet<Mount>()
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        _mountIds = DefaultMounts.Select(x => x.Id).Concat(mounts.Select(x => x.MountId)).ToArray();
        _mountNames = DefaultMounts.Select(x => x.Name).Concat(mounts.Select(x => x.Name)).ToArray();

        var sortedClassJobs = classJobUtils.SortedClassJobs.Select(x => x.ClassJob).ToList();
        var classJobs = Enum.GetValues<EClassJob>()
            .Where(x => x != EClassJob.Adventurer)
            .Where(x => !x.IsCrafter() && !x.IsGatherer())
            .Where(x => !x.IsClass())
            .OrderBy(x => sortedClassJobs.IndexOf(x))
            .ToList();
        _classJobIds = DefaultClassJobs.Select(x => x.ClassJob).Concat(classJobs).ToArray();
        _classJobNames = DefaultClassJobs.Select(x => x.Name).Concat(classJobs.Select(x => x.ToFriendlyString())).ToArray();
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("通用###General");
        if (!tab)
            return;


        {
            int selectedCombatModule = (int)Configuration.General.CombatModule;
            if (ImGui.Combo("首选战斗模块", ref selectedCombatModule, _combatModuleNames,
                    _combatModuleNames.Length))
            {
                Configuration.General.CombatModule = (Configuration.ECombatModule)selectedCombatModule;
                Save();
            }
        }

        int selectedMount = Array.FindIndex(_mountIds, x => x == Configuration.General.MountId);
        if (selectedMount == -1)
        {
            selectedMount = 0;
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        if (ImGui.Combo("首选坐骑", ref selectedMount, _mountNames, _mountNames.Length))
        {
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        int grandCompany = (int)Configuration.General.GrandCompany;
        if (ImGui.Combo("首选部队阵营", ref grandCompany, _grandCompanyNames,
                _grandCompanyNames.Length))
        {
            Configuration.General.GrandCompany = (GrandCompany)grandCompany;
            Save();
        }

        int combatJob = Array.IndexOf(_classJobIds, Configuration.General.CombatJob);
        if (combatJob == -1)
        {
            Configuration.General.CombatJob = EClassJob.Adventurer;
            Save();

            combatJob = 0;
        }

        if (ImGui.Combo("首选战斗职业", ref combatJob, _classJobNames, _classJobNames.Length))
        {
            Configuration.General.CombatJob = _classJobIds[combatJob];
            Save();
        }

        ImGui.Separator();
        ImGui.Text("界面设置");
        using (ImRaii.PushIndent())
        {
            bool hideInAllInstances = Configuration.General.HideInAllInstances;
            if (ImGui.Checkbox("在副本任务中隐藏任务窗口", ref hideInAllInstances))
            {
                Configuration.General.HideInAllInstances = hideInAllInstances;
                Save();
            }

            bool useEscToCancelQuesting = Configuration.General.UseEscToCancelQuesting;
            if (ImGui.Checkbox("使用 ESC 取消移动/任务", ref useEscToCancelQuesting))
            {
                Configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
                Save();
            }

            bool showIncompleteSeasonalEvents = Configuration.General.ShowIncompleteSeasonalEvents;
            if (ImGui.Checkbox("显示未完成的季节活动信息", ref showIncompleteSeasonalEvents))
            {
                Configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
                Save();
            }
        }

        ImGui.Separator();
        ImGui.Text("任务设置");
        using (ImRaii.PushIndent())
        {
            bool configureTextAdvance = Configuration.General.ConfigureTextAdvance;
            if (ImGui.Checkbox("自动配置 TextAdvance",
                    ref configureTextAdvance))
            {
                Configuration.General.ConfigureTextAdvance = configureTextAdvance;
                Save();
            }

            bool skipLowPriorityInstances = Configuration.General.SkipLowPriorityDuties;
            if (ImGui.Checkbox("解锁某些可选的副本和大型任务（无需等待完成）", ref skipLowPriorityInstances))
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
                    ImGui.Text("Questionable 会自动接取一些可选任务（例如风脉泉任务或 2.0 团本）。");
                    ImGui.Text("如果启用此设置，Questionable 将继续推进其他任务，而无需等待手动完成对应副本。");

                    ImGui.Separator();
                    ImGui.Text("此设置将影响以下副本和大型任务：");
                    foreach (var lowPriorityCfc in _questRegistry.LowPriorityContentFinderConditionQuests)
                    {
                        if (_territoryData.TryGetContentFinderCondition(lowPriorityCfc.ContentFinderConditionId, out var cfcData))
                        {
                            ImGui.BulletText($"{cfcData.Name}");
                        }
                    }
                }
            }
        }
        if (_pluginInterface.InstalledPlugins.Any(x => x is { InternalName: "DailyRoutines", IsLoaded: true }))
        {
            ImGui.Separator();
            ImGui.Text("DailyRoutines 兼容性");
            using (ImRaii.PushIndent())
            {
                    var configureDailyRoutines = Configuration.General.ConfigureDailyRoutines;
                    if (ImGui.Checkbox("插件工作时临时禁用 Daily Routines 中的冲突模块",
                            ref configureDailyRoutines))
                    {
                        Configuration.General.ConfigureDailyRoutines = configureDailyRoutines;
                        Save();
                    }

                    ImGuiComponents.HelpMarker($"{string.Join("\n", _dailyRoutinesIpc.ConflictingModules)} \n如果你发现还有其他冲突模块未列入，请联系汉化作者。");

                    var useDailyRoutinesTeleport = Configuration.General.UsingDailyRoutinesTeleport;
                    if (ImGui.Checkbox("使用 Daily Routines 进行小水晶传送（请仔细阅读右侧说明）",
                            ref useDailyRoutinesTeleport))
                    {
                        Configuration.General.UsingDailyRoutinesTeleport = useDailyRoutinesTeleport;
                        Save();
                    }

                    ImGuiComponents.HelpMarker("使用了【更好的传送界面】模块，如果未启用将帮你自动启用\n" +
                                               "勾选后，主城内将不会再寻路前往小水晶传送，而是直接【瞬移】，使用请自负风险\n" +
                                               "如果遇到任何问题，请安装 Lifestream 并禁用此选项。");
            }
        }
    }
}
