using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using LLib.ImGui;
using Lumina.Excel.Sheets;
using Questionable.External;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly NotificationMasterIpc _notificationMasterIpc;
    private readonly Configuration _configuration;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;

    private readonly string[] _grandCompanyNames =
        ["None (manually pick quest)", "Maelstrom", "Twin Adder", "Immortal Flames"];

    [SuppressMessage("Performance", "CA1861", Justification = "One time initialization")]
    public ConfigWindow(IDalamudPluginInterface pluginInterface, NotificationMasterIpc notificationMasterIpc, Configuration configuration, IDataManager dataManager)
        : base("Config - Questionable###QuestionableConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _notificationMasterIpc = notificationMasterIpc;
        _configuration = configuration;

        var mounts = dataManager.GetExcelSheet<Mount>()
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
        using var tabBar = ImRaii.TabBar("QuestionableConfigTabs");
        if (!tabBar)
            return;

        DrawGeneralTab();
        DrawNotificationsTab();
        DrawAdvancedTab();
    }

    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem("General");
        if (!tab)
            return;
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

        int grandCompany = (int)_configuration.General.GrandCompany;
        if (ImGui.Combo("Preferred Grand Company", ref grandCompany, _grandCompanyNames,
                _grandCompanyNames.Length))
        {
            _configuration.General.GrandCompany = (GrandCompany)grandCompany;
            Save();
        }

        bool hideInAllInstances = _configuration.General.HideInAllInstances;
        if (ImGui.Checkbox("Hide quest window in all instanced duties", ref hideInAllInstances))
        {
            _configuration.General.HideInAllInstances = hideInAllInstances;
            Save();
        }

        bool useEscToCancelQuesting = _configuration.General.UseEscToCancelQuesting;
        if (ImGui.Checkbox("Use ESC to cancel questing/movement", ref useEscToCancelQuesting))
        {
            _configuration.General.UseEscToCancelQuesting = useEscToCancelQuesting;
            Save();
        }

        bool showIncompleteSeasonalEvents = _configuration.General.ShowIncompleteSeasonalEvents;
        if (ImGui.Checkbox("Show details for incomplete seasonal events", ref showIncompleteSeasonalEvents))
        {
            _configuration.General.ShowIncompleteSeasonalEvents = showIncompleteSeasonalEvents;
            Save();
        }

        bool configureTextAdvance = _configuration.General.ConfigureTextAdvance;
        if (ImGui.Checkbox("Automatically configure TextAdvance with the recommended settings",
                ref configureTextAdvance))
        {
            _configuration.General.ConfigureTextAdvance = configureTextAdvance;
            Save();
        }
    }

    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem("Notifications");
        if (!tab)
            return;

        bool enabled = _configuration.Notifications.Enabled;
        if (ImGui.Checkbox("Enable notifications when manual interaction is required", ref enabled))
        {
            _configuration.Notifications.Enabled = enabled;
            Save();
        }

        using (ImRaii.Disabled(!_configuration.Notifications.Enabled))
        {
            using (ImRaii.PushIndent())
            {
                var xivChatTypes = Enum.GetValues<XivChatType>()
                    .Where(x => x != XivChatType.StandardEmote)
                    .ToArray();
                var selectedChatType = Array.IndexOf(xivChatTypes, _configuration.Notifications.ChatType);
                string[] chatTypeNames = xivChatTypes
                    .Select(t => t.GetAttribute<XivChatTypeInfoAttribute>()?.FancyName ?? t.ToString())
                    .ToArray();
                if (ImGui.Combo("Chat channel", ref selectedChatType, chatTypeNames,
                        chatTypeNames.Length))
                {
                    _configuration.Notifications.ChatType = xivChatTypes[selectedChatType];
                    Save();
                }

                ImGui.Separator();
                ImGui.Text("NotificationMaster settings");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Requires the plugin 'NotificationMaster' to be installed.");
                using (ImRaii.Disabled(!_notificationMasterIpc.Enabled))
                {
                    bool showTrayMessage = _configuration.Notifications.ShowTrayMessage;
                    if (ImGui.Checkbox("Show tray notification", ref showTrayMessage))
                    {
                        _configuration.Notifications.ShowTrayMessage = showTrayMessage;
                        Save();
                    }

                    bool flashTaskbar = _configuration.Notifications.FlashTaskbar;
                    if (ImGui.Checkbox("Flash taskbar icon", ref flashTaskbar))
                    {
                        _configuration.Notifications.FlashTaskbar = flashTaskbar;
                        Save();
                    }
                }
            }
        }
    }

    private void DrawAdvancedTab()
    {
        using var tab = ImRaii.TabItem("Advanced");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "Enabling any option here may cause unexpected behavior. Use at your own risk.");

        ImGui.Separator();

        bool debugOverlay = _configuration.Advanced.DebugOverlay;
        if (ImGui.Checkbox("Enable debug overlay", ref debugOverlay))
        {
            _configuration.Advanced.DebugOverlay = debugOverlay;
            Save();
        }

        bool neverFly = _configuration.Advanced.NeverFly;
        if (ImGui.Checkbox("Disable flying (even if unlocked for the zone)", ref neverFly))
        {
            _configuration.Advanced.NeverFly = neverFly;
            Save();
        }

        bool additionalStatusInformation = _configuration.Advanced.AdditionalStatusInformation;
        if (ImGui.Checkbox("Draw additional status information", ref additionalStatusInformation))
        {
            _configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        ImGui.EndTabItem();
    }

    private void Save() => _pluginInterface.SavePluginConfig(_configuration);

    public void SaveWindowConfig() => Save();
}
