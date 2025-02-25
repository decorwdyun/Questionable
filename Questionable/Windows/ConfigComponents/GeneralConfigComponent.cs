using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows.ConfigComponents;

internal sealed class GeneralConfigComponent : ConfigComponent
{
    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "随机坐骑")];

    private readonly CombatController _combatController;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;
    private readonly string[] _combatModuleNames = ["None", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn"];

    private readonly string[] _grandCompanyNames =
        ["None (manually pick quest)", "Maelstrom", "Twin Adder", "Immortal Flames"];

    public GeneralConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        CombatController combatController,
        IDataManager dataManager)
        : base(pluginInterface, configuration)
    {
        _combatController = combatController;

        var mounts = dataManager.GetExcelSheet<Mount>()
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        _mountIds = DefaultMounts.Select(x => x.Id).Concat(mounts.Select(x => x.MountId)).ToArray();
        _mountNames = DefaultMounts.Select(x => x.Name).Concat(mounts.Select(x => x.Name)).ToArray();
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
    }
}
