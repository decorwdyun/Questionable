using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
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
    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "Mount Roulette")];
    private static readonly List<(EClassJob ClassJob, string Name)> DefaultClassJobs = [(EClassJob.Adventurer, "Auto (highest level/item level)")];

    private readonly CombatController _combatController;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;
    private readonly string[] _combatModuleNames = ["None", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn"];

    private readonly string[] _grandCompanyNames =
        ["None (manually pick quest)", "Maelstrom", "Twin Adder", "Immortal Flames"];

    private readonly EClassJob[] _classJobIds;
    private readonly string[] _classJobNames;

    private Configuration.ECombatModule? _pendingCombatModule;

    public GeneralConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        CombatController combatController,
        IDataManager dataManager,
        ClassJobUtils classJobUtils)
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
        using var tab = ImRaii.TabItem("General###General");
        if (!tab)
            return;

        using (ImRaii.Disabled(_combatController.IsRunning))
        {
            DrawCombatModule();
        }

        int selectedMount = Array.FindIndex(_mountIds, x => x == Configuration.General.MountId);
        if (selectedMount == -1)
        {
            selectedMount = 0;
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        if (ImGui.Combo("Preferred Mount", ref selectedMount, _mountNames, _mountNames.Length))
        {
            Configuration.General.MountId = _mountIds[selectedMount];
            Save();
        }

        int grandCompany = (int)Configuration.General.GrandCompany;
        if (ImGui.Combo("Preferred Grand Company", ref grandCompany, _grandCompanyNames,
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

        if (ImGui.Combo("Preferred Combat Job", ref combatJob, _classJobNames, _classJobNames.Length))
        {
            Configuration.General.CombatJob = _classJobIds[combatJob];
            Save();
        }

        bool hideInAllInstances = Configuration.General.HideInAllInstances;
        if (ImGui.Checkbox("Hide quest window in all instanced duties", ref hideInAllInstances))
        {
            Configuration.General.HideInAllInstances = hideInAllInstances;
            Save();
        }

        bool useEscToCancelQuesting = Configuration.General.UseEscToCancelQuesting;
        if (ImGui.Checkbox("Use ESC to cancel questing/movement", ref useEscToCancelQuesting))
        {
            Configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
            Save();
        }

        bool showIncompleteSeasonalEvents = Configuration.General.ShowIncompleteSeasonalEvents;
        if (ImGui.Checkbox("Show details for incomplete seasonal events", ref showIncompleteSeasonalEvents))
        {
            Configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
            Save();
        }

        bool configureTextAdvance = Configuration.General.ConfigureTextAdvance;
        if (ImGui.Checkbox("Automatically configure TextAdvance with the recommended settings",
                ref configureTextAdvance))
        {
            Configuration.General.ConfigureTextAdvance = configureTextAdvance;
            Save();
        }
    }

    private void DrawCombatModule()
    {
        using (_ = ImRaii.Disabled(_pendingCombatModule != null))
        {
            int selectedCombatModule = (int)Configuration.General.CombatModule;
            if (ImGui.Combo("Preferred Combat Module", ref selectedCombatModule, _combatModuleNames,
                    _combatModuleNames.Length))
            {
                if (selectedCombatModule == (int)Configuration.ECombatModule.RotationSolverReborn)
                {
                    Configuration.General.CombatModule = Configuration.ECombatModule.None;
                    _pendingCombatModule = Configuration.ECombatModule.RotationSolverReborn;
                }
                else
                {
                    Configuration.General.CombatModule = (Configuration.ECombatModule)selectedCombatModule;
                    _pendingCombatModule = null;
                }
                Save();
            }

            if (Configuration.General.CombatModule == Configuration.ECombatModule.RotationSolverReborn)
            {
                ImGuiComponents.HelpMarker("The 'Rotation Solver Reborn' module will be removed in Patch 7.3.", FontAwesomeIcon.ExclamationTriangle, ImGuiColors.DalamudYellow);
            }
        }

        if (_pendingCombatModule == null)
            return;

        using var indent = ImRaii.PushIndent();

        ImGui.TextColored(ImGuiColors.DalamudYellow, "The 'Rotation Solver Reborn' module is unsupported, obsolete and will be removed in Patch 7.3.");
        ImGui.Text("According to the RSR development team, RSR is not meant to run without human inputs/automatically/as a bot.");
        ImGui.Text("Additionally, they have considered disabling RSR if it detects that Questionable is installed.");
        ImGui.Text("Please consider switching to 'Wrath Combo' or 'Boss Mod (VBM)'.");

        if (ImGui.Button("Use 'Wrath Combo'"))
        {
            Configuration.General.CombatModule = Configuration.ECombatModule.WrathCombo;
            _pendingCombatModule = null;
            Save();
        }

        ImGui.SameLine();

        if (ImGui.Button("Use 'Boss Mod (VBM)'"))
        {
            Configuration.General.CombatModule = Configuration.ECombatModule.BossMod;
            _pendingCombatModule = null;
            Save();
        }

        ImGui.SameLine();

        if (ImGui.Button("Use 'Rotation Solver Reborn'"))
        {
            Configuration.General.CombatModule = Configuration.ECombatModule.RotationSolverReborn;
            _pendingCombatModule = null;
            Save();
        }


        ImGui.Separator();
    }
}
