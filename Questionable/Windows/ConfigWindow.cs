using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.ImGui;
using Lumina.Excel.GeneratedSheets;

namespace Questionable.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;

    [SuppressMessage("Performance", "CA1861", Justification = "One time initialization")]
    public ConfigWindow(DalamudPluginInterface pluginInterface, Configuration configuration, IDataManager dataManager)
        : base("Config - Questionable###QuestionableConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _configuration = configuration;

        var mounts = dataManager.GetExcelSheet<Mount>()!
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        _mountIds = new uint[] { 0 }.Concat(mounts.Select(x => x.MountId)).ToArray();
        _mountNames = new[] { "Mount Roulette" }.Concat(mounts.Select(x => x.Name)).ToArray();
    }

    public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

    public override void Draw()
    {
        if (ImGui.BeginTabBar("QuestionableConfigTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                int selectedMount = Array.FindIndex(_mountIds, x => x == _configuration.General.MountId);
                if (selectedMount == -1)
                {
                    selectedMount = 0;
                    _configuration.General.MountId = _mountIds[selectedMount];
                    Save();
                }

                if (ImGui.Combo("Preferred Mount", ref selectedMount, _mountNames, _mountNames.Length))
                {
                    _configuration.General.MountId = _mountIds[selectedMount];
                    Save();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {
                ImGui.TextColored(ImGuiColors.DalamudRed,
                    "Enabling any option here may cause unexpected behavior. Use at your own risk.");

                ImGui.Separator();

                bool neverFly = _configuration.Advanced.NeverFly;
                if (ImGui.Checkbox("Disable flying (even if unlocked for the zone)", ref neverFly))
                {
                    _configuration.Advanced.NeverFly = neverFly;
                    Save();
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void Save() => _pluginInterface.SavePluginConfig(_configuration);

    public void SaveWindowConfig() => Save();
}
