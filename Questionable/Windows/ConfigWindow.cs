using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using LLib.ImGui;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Questionable.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
    private const string DutyClipboardPrefix = "qst:duty:";
    private const string DutyClipboardSeparator = ";";
    private const string DutyWhitelistPrefix = "+";
    private const string DutyBlacklistPrefix = "-";

    private static readonly List<(uint Id, string Name)> DefaultMounts = [(0, "Mount Roulette")];

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly NotificationMasterIpc _notificationMasterIpc;
    private readonly Configuration _configuration;
    private readonly CombatController _combatController;
    private readonly QuestRegistry _questRegistry;
    private readonly AutoDutyIpc _autoDutyIpc;

    private readonly uint[] _mountIds;
    private readonly string[] _mountNames;

    private readonly string[] _combatModuleNames = ["None", "Boss Mod (VBM)", "Wrath Combo", "Rotation Solver Reborn"];

    private readonly string[] _grandCompanyNames =
        ["None (manually pick quest)", "Maelstrom", "Twin Adder", "Immortal Flames"];

    private readonly string[] _supportedCfcOptions =
    [
        $"{SeIconChar.Circle.ToIconChar()} Enabled (Default)",
        $"{SeIconChar.Circle.ToIconChar()} Enabled",
        $"{SeIconChar.Cross.ToIconChar()} Disabled"
    ];

    private readonly string[] _unsupportedCfcOptions =
    [
        $"{SeIconChar.Cross.ToIconChar()} Disabled (Default)",
        $"{SeIconChar.Circle.ToIconChar()} Enabled",
        $"{SeIconChar.Cross.ToIconChar()} Disabled"
    ];

    private readonly Dictionary<EExpansionVersion, List<DutyInfo>> _contentFinderConditionNames;

    public ConfigWindow(IDalamudPluginInterface pluginInterface,
        NotificationMasterIpc notificationMasterIpc,
        Configuration configuration,
        IDataManager dataManager,
        CombatController combatController,
        TerritoryData territoryData,
        QuestRegistry questRegistry,
        AutoDutyIpc autoDutyIpc)
        : base("Config - Questionable###QuestionableConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _notificationMasterIpc = notificationMasterIpc;
        _configuration = configuration;
        _combatController = combatController;
        _questRegistry = questRegistry;
        _autoDutyIpc = autoDutyIpc;

        var mounts = dataManager.GetExcelSheet<Mount>()
            .Where(x => x is { RowId: > 0, Icon: > 0 })
            .Select(x => (MountId: x.RowId, Name: x.Singular.ToString()))
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
        _mountIds = DefaultMounts.Select(x => x.Id).Concat(mounts.Select(x => x.MountId)).ToArray();
        _mountNames = DefaultMounts.Select(x => x.Name).Concat(mounts.Select(x => x.Name)).ToArray();

        _contentFinderConditionNames = dataManager.GetExcelSheet<DawnContent>()
            .Where(x => x is { RowId: > 0, Unknown16: false })
            .OrderBy(x => x.Unknown15) // SortKey for the support UI
            .Select(x => x.Content.ValueNullable)
            .Where(x => x != null)
            .Select(x => x!.Value)
            .Select(x => new
            {
                Expansion = (EExpansionVersion)x.TerritoryType.Value.ExVersion.RowId,
                CfcId = x.RowId,
                Name = territoryData.GetContentFinderCondition(x.RowId)?.Name ?? "?",
                TerritoryId = x.TerritoryType.RowId,
                ContentType = x.ContentType.RowId,
                Level = x.ClassJobLevelRequired,
                x.SortKey
            })
            .GroupBy(x => x.Expansion)
            .ToDictionary(x => x.Key,
                x => x
                    .Select(y => new DutyInfo(y.CfcId, y.TerritoryId, $"{SeIconChar.LevelEn.ToIconChar()}{FormatLevel(y.Level)} {y.Name}"))
                    .ToList());
    }

    public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

    private static string FormatLevel(int level)
    {
        if (level == 0)
            return string.Empty;

        return $"{FormatLevel(level / 10)}{(SeIconChar.Number0 + level % 10).ToIconChar()}";
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("QuestionableConfigTabs");
        if (!tabBar)
            return;

        DrawGeneralTab();
        DrawDutiesTab();
        DrawNotificationsTab();
        DrawAdvancedTab();
    }

    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem("General");
        if (!tab)
            return;

        using (ImRaii.Disabled(_combatController.IsRunning))
        {
            int selectedCombatModule = (int)_configuration.General.CombatModule;
            if (ImGui.Combo("Preferred Combat Module", ref selectedCombatModule, _combatModuleNames,
                    _combatModuleNames.Length))
            {
                _configuration.General.CombatModule = (Configuration.ECombatModule)selectedCombatModule;
                Save();
            }
        }

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

    private void DrawDutiesTab()
    {
        using var tab = ImRaii.TabItem("Duties");
        if (!tab)
            return;

        bool runInstancedContentWithAutoDuty = _configuration.Duties.RunInstancedContentWithAutoDuty;
        if (ImGui.Checkbox("Run instanced content with AutoDuty and BossMod", ref runInstancedContentWithAutoDuty))
        {
            _configuration.Duties.RunInstancedContentWithAutoDuty = runInstancedContentWithAutoDuty;
            Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "The combat module used for this is configured by AutoDuty, ignoring whichever selection you've made in Questionable's \"General\" configuration.");

        ImGui.Separator();

        using (ImRaii.Disabled(!runInstancedContentWithAutoDuty))
        {
            ImGui.Text(
                "Questionable includes a default list of duties that work if AutoDuty and BossMod are installed.");

            ImGui.Text("The included list of duties can change with each update, and is based on the following spreadsheet:");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.GlobeEurope, "Open AutoDuty spreadsheet"))
                Util.OpenLink(
                    "https://docs.google.com/spreadsheets/d/151RlpqRcCpiD_VbQn6Duf-u-S71EP7d0mx3j1PDNoNA/edit?pli=1#gid=0");

            ImGui.Separator();
            ImGui.Text("You can override the dungeon settings for each individual dungeon/trial:");

            using (var child = ImRaii.Child("DutyConfiguration", new Vector2(-1, 400), true))
            {
                if (child)
                {
                    foreach (EExpansionVersion expansion in Enum.GetValues<EExpansionVersion>())
                    {
                        if (ImGui.CollapsingHeader(expansion.ToString()))
                        {
                            using var table = ImRaii.Table($"Duties{expansion}", 2, ImGuiTableFlags.SizingFixedFit);
                            if (table)
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                                ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed, 200f);

                                if (_contentFinderConditionNames.TryGetValue(expansion, out var cfcNames))
                                {
                                    foreach (var (cfcId, territoryId, name) in cfcNames)
                                    {
                                        if (_questRegistry.TryGetDutyByContentFinderConditionId(cfcId,
                                                out bool autoDutyEnabledByDefault))
                                        {
                                            ImGui.TableNextRow();

                                            string[] labels = autoDutyEnabledByDefault
                                                ? _supportedCfcOptions
                                                : _unsupportedCfcOptions;
                                            int value = 0;
                                            if (_configuration.Duties.WhitelistedDutyCfcIds.Contains(cfcId))
                                                value = 1;
                                            if (_configuration.Duties.BlacklistedDutyCfcIds.Contains(cfcId))
                                                value = 2;

                                            if (ImGui.TableNextColumn())
                                            {
                                                ImGui.AlignTextToFramePadding();
                                                ImGui.TextUnformatted(name);
                                                if (ImGui.IsItemHovered() && _configuration.Advanced.AdditionalStatusInformation)
                                                {
                                                    using var tooltip = ImRaii.Tooltip();
                                                    if (tooltip)
                                                    {
                                                        ImGui.TextUnformatted(name);
                                                        ImGui.Separator();
                                                        ImGui.BulletText($"TerritoryId: {territoryId}");
                                                        ImGui.BulletText($"ContentFinderConditionId: {cfcId}");
                                                    }
                                                }

                                                if (runInstancedContentWithAutoDuty && !_autoDutyIpc.HasPath(cfcId))
                                                    ImGuiComponents.HelpMarker("This duty is not supported by AutoDuty", FontAwesomeIcon.Times, ImGuiColors.DalamudRed);
                                            }

                                            if (ImGui.TableNextColumn())
                                            {
                                                using var _ = ImRaii.PushId($"##Dungeon{cfcId}");
                                                ImGui.SetNextItemWidth(200);
                                                if (ImGui.Combo(string.Empty, ref value, labels, labels.Length))
                                                {
                                                    _configuration.Duties.WhitelistedDutyCfcIds.Remove(cfcId);
                                                    _configuration.Duties.BlacklistedDutyCfcIds.Remove(cfcId);

                                                    if (value == 1)
                                                        _configuration.Duties.WhitelistedDutyCfcIds.Add(cfcId);
                                                    else if (value == 2)
                                                        _configuration.Duties.BlacklistedDutyCfcIds.Add(cfcId);

                                                    Save();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            using (ImRaii.Disabled(_configuration.Duties.WhitelistedDutyCfcIds.Count +
                       _configuration.Duties.BlacklistedDutyCfcIds.Count == 0))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, "Export to clipboard"))
                {
                    var whitelisted =
                        _configuration.Duties.WhitelistedDutyCfcIds.Select(x => $"{DutyWhitelistPrefix}{x}");
                    var blacklisted =
                        _configuration.Duties.BlacklistedDutyCfcIds.Select(x => $"{DutyBlacklistPrefix}{x}");
                    string text = DutyClipboardPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        string.Join(DutyClipboardSeparator, whitelisted.Concat(blacklisted))));
                    ImGui.SetClipboardText(text);
                }
            }

            ImGui.SameLine();

            string? clipboardText = GetClipboardText();
            using (ImRaii.Disabled(clipboardText == null || !clipboardText.StartsWith(DutyClipboardPrefix, StringComparison.InvariantCulture)))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Paste, "Import from Clipboard"))
                {
                    clipboardText = clipboardText!.Substring(DutyClipboardPrefix.Length);
                    string text = Encoding.UTF8.GetString(Convert.FromBase64String(clipboardText));

                    _configuration.Duties.WhitelistedDutyCfcIds.Clear();
                    _configuration.Duties.BlacklistedDutyCfcIds.Clear();
                    foreach (string part in text.Split(DutyClipboardSeparator))
                    {
                        if (part.StartsWith(DutyWhitelistPrefix, StringComparison.InvariantCulture) &&
                            uint.TryParse(part.AsSpan(DutyWhitelistPrefix.Length), CultureInfo.InvariantCulture,
                                out uint whitelistedCfcId))
                            _configuration.Duties.WhitelistedDutyCfcIds.Add(whitelistedCfcId);

                        if (part.StartsWith(DutyBlacklistPrefix, StringComparison.InvariantCulture) &&
                            uint.TryParse(part.AsSpan(DutyBlacklistPrefix.Length), CultureInfo.InvariantCulture,
                                out uint blacklistedCfcId))
                            _configuration.Duties.WhitelistedDutyCfcIds.Add(blacklistedCfcId);
                    }
                }
            }

            ImGui.SameLine();

            using (var unused = ImRaii.Disabled(!ImGui.IsKeyDown(ImGuiKey.ModCtrl)))
            {
                if (ImGui.Button("Reset to default"))
                {
                    _configuration.Duties.WhitelistedDutyCfcIds.Clear();
                    _configuration.Duties.BlacklistedDutyCfcIds.Clear();
                    Save();
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL to enable this button.");
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

    /// <summary>
    /// The default implementation for <see cref="ImGui.GetClipboardText"/> throws an NullReferenceException if the clipboard is empty, maybe also if it doesn't contain text.
    /// </summary>
    private unsafe string? GetClipboardText()
    {
        byte* ptr = ImGuiNative.igGetClipboardText();
        if (ptr == null)
            return null;

        int byteCount = 0;
        while (ptr[byteCount] != 0)
            ++byteCount;
        return Encoding.UTF8.GetString(ptr, byteCount);
    }

    private sealed record DutyInfo(uint CfcId, uint TerritoryId, string Name);
}
