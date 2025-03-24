using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows.ConfigComponents;

internal sealed class GeneralConfigComponent : ConfigComponent
{
    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "随机坐骑")];
    private static readonly List<(EClassJob ClassJob, string Name)> DefaultClassJobs = [(EClassJob.Adventurer, "自动（等级/装等最高的）")];

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly CombatController _combatController;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;
    private readonly string[] _combatModuleNames = ["未选择", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn", "AEAssist"];

    private readonly string[] _grandCompanyNames = ["未选择（需要时再手动）", "黑涡团", "双蛇党", "恒辉队"];

    private readonly EClassJob[] _classJobIds;
    private readonly string[] _classJobNames;

    public GeneralConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        CombatController combatController,
        IDataManager dataManager,
        ClassJobUtils classJobUtils)
        : base(pluginInterface, configuration)
    {
        _pluginInterface = pluginInterface;
        _combatController = combatController;

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

        using (ImRaii.Disabled(_combatController.IsRunning))
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

        bool hideInAllInstances = Configuration.General.HideInAllInstances;
        if (ImGui.Checkbox("在所有实例副本中隐藏任务窗口", ref hideInAllInstances))
        {
            Configuration.General.HideInAllInstances = hideInAllInstances;
            Save();
        }

        bool useEscToCancelQuesting = Configuration.General.UseEscToCancelQuesting;
        if (ImGui.Checkbox("使用 ESC 键取消任务/移动", ref useEscToCancelQuesting))
        {
            Configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
            Save();
        }

        bool showIncompleteSeasonalEvents = Configuration.General.ShowIncompleteSeasonalEvents;
        if (ImGui.Checkbox("显示未完成的季节活动详情", ref showIncompleteSeasonalEvents))
        {
            Configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
            Save();
        }

        bool configureTextAdvance = Configuration.General.ConfigureTextAdvance;
        if (ImGui.Checkbox("自动配置 TextAdvance 为推荐设置",
                ref configureTextAdvance))
        {
            Configuration.General.ConfigureTextAdvance = configureTextAdvance;
            Save();
        }
        
        if (_pluginInterface.InstalledPlugins.Any(x => x is { InternalName: "DailyRoutines", IsLoaded: true }))
        {
            var configureDailyRoutines = Configuration.General.ConfigureDailyRoutines;
            if (ImGui.Checkbox("插件工作时临时禁用 Daily Routines 中的冲突模块",
                    ref configureDailyRoutines))
            {
                Configuration.General.ConfigureDailyRoutines = configureDailyRoutines;
                Save();
            }
            ImGui.TextColored(ImGuiColors.DalamudOrange, "需要 Daily Routines v1.4.4.0 以上");
        }

        ImGui.Separator();
        ImGui.TextColored(ImGuiColors.DalamudYellow, "版本 7.1 独有内容（国际服）");
        using (_ = ImRaii.PushIndent())
        {
            bool pickUpFreeFantasia = Configuration.General.PickUpFreeFantasia;
            if (ImGui.Checkbox("尝试在“女儿节”任务期间领取免费限时幻想药",
                    ref pickUpFreeFantasia))
            {
                Configuration.General.PickUpFreeFantasia = pickUpFreeFantasia;
                Save();
            }
        }
    }
}
