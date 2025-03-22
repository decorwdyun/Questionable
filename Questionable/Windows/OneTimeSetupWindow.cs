using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using LLib.ImGui;
using Microsoft.Extensions.Logging;
using Questionable.External;

namespace Questionable.Windows;

internal sealed class OneTimeSetupWindow : LWindow
{
    private static readonly IReadOnlyList<PluginInfo> RequiredPlugins =
    [
        new("vnavmesh",
            "vnavmesh",
            """
            vnavmesh handles the navigation within a zone, moving
            your character to the next quest-related objective.
            """,
            new Uri("https://github.com/awgil/ffxiv_navmesh/"),
            new Uri("https://puni.sh/api/repository/veyn")),
        new("Lifestream",
            "Lifestream",
            """
            Used to travel to aethernet shards in cities.
            """,
            new Uri("https://github.com/NightmareXIV/Lifestream"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
        new("TextAdvance",
            "TextAdvance",
            """
            Automatically accepts and turns in quests, skips cutscenes
            and dialogue.
            """,
            new Uri("https://github.com/NightmareXIV/TextAdvance"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
    ];

    private static readonly ReadOnlyDictionary<Configuration.ECombatModule, PluginInfo> CombatPlugins = new Dictionary<Configuration.ECombatModule, PluginInfo>
    {
        {
            Configuration.ECombatModule.BossMod,
            new("Boss Mod (VBM)",
                "BossMod",
                string.Empty,
                new Uri("https://github.com/awgil/ffxiv_bossmod"),
                new Uri("https://puni.sh/api/repository/veyn"))
        },
        {
            Configuration.ECombatModule.WrathCombo,
            new PluginInfo("Wrath Combo",
                "WrathCombo",
                string.Empty,
                new Uri("https://github.com/PunishXIV/WrathCombo"),
                new Uri("https://puni.sh/api/plugins"))
        },
    }.AsReadOnly();

    private readonly IReadOnlyList<PluginInfo> _recommendedPlugins;

    private readonly Configuration _configuration;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiUtils _uiUtils;
    private readonly ILogger<OneTimeSetupWindow> _logger;

    public OneTimeSetupWindow(Configuration configuration, IDalamudPluginInterface pluginInterface, UiUtils uiUtils,
        ILogger<OneTimeSetupWindow> logger, AutomatonIpc automatonIpc)
        : base("Questionable Setup###QuestionableOneTimeSetup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings, true)
    {
        _configuration = configuration;
        _pluginInterface = pluginInterface;
        _uiUtils = uiUtils;
        _logger = logger;
        _recommendedPlugins =
        [
            new PluginInfo("CBT (formerly known as Automaton)",
                "Automaton",
                """
                Automaton is a collection of automation-related tweaks.
                The 'Sniper no sniping' tweak can complete snipe tasks automatically.
                """,
                new Uri("https://github.com/Jaksuhn/Automaton"),
                new Uri("https://puni.sh/api/repository/croizat"),
                [new PluginDetailInfo("'Sniper no sniping' enabled", () => automatonIpc.IsAutoSnipeEnabled)]),
            new("NotificationMaster",
                "NotificationMaster",
                """
                Sends a configurable out-of-game notification if a quest
                requires manual actions.
                """,
                new Uri("https://github.com/NightmareXIV/NotificationMaster"),
                null),
        ];

        RespectCloseHotkey = false;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        IsOpen = !_configuration.IsPluginSetupComplete();
        _logger.LogInformation("One-time setup needed: {IsOpen}", IsOpen);
    }

    public override void Draw()
    {
        float checklistPadding;
        using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            checklistPadding = ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X +
                               ImGui.GetStyle().ItemSpacing.X;
        }

        ImGui.Text("Questionable requires the following plugins to work:");
        bool allRequiredInstalled = true;
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in RequiredPlugins)
                allRequiredInstalled &= DrawPlugin(plugin, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Questionable supports multiple rotation/combat plugins, please pick the one\nyou want to use:");

        using (ImRaii.PushIndent())
        {
            if (ImGui.RadioButton("No rotation/combat plugin (combat must be done manually)",
                    _configuration.General.CombatModule == Configuration.ECombatModule.None))
            {
                _configuration.General.CombatModule = Configuration.ECombatModule.None;
                _pluginInterface.SavePluginConfig(_configuration);
            }

            DrawCombatPlugin(Configuration.ECombatModule.BossMod, checklistPadding);
            DrawCombatPlugin(Configuration.ECombatModule.WrathCombo, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("The following plugins are recommended, but not required:");
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in _recommendedPlugins)
                DrawPlugin(plugin, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (allRequiredInstalled)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGreen))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "Finish Setup"))
                {
                    _logger.LogInformation("Marking setup as complete");
                    _configuration.MarkPluginSetupComplete();
                    _pluginInterface.SavePluginConfig(_configuration);
                    IsOpen = false;
                }
            }
        }
        else
        {
            using (ImRaii.Disabled())
            {
                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                    ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "Missing required plugins");
            }
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Close window & don't enable Questionable"))
        {
            _logger.LogWarning("Closing window without all required plugins installed");
            IsOpen = false;
        }
    }

    private bool DrawPlugin(PluginInfo plugin, float checklistPadding)
    {
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            bool isInstalled = IsPluginInstalled(plugin);
            _uiUtils.ChecklistItem(plugin.DisplayName, isInstalled);

            DrawPluginDetails(plugin, checklistPadding, isInstalled);
            return isInstalled;
        }
    }

    private void DrawCombatPlugin(Configuration.ECombatModule combatModule, float checklistPadding)
    {
        ImGui.Spacing();

        PluginInfo plugin = CombatPlugins[combatModule];
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            bool isInstalled = IsPluginInstalled(plugin);
            if (ImGui.RadioButton(plugin.DisplayName, _configuration.General.CombatModule == combatModule))
            {
                _configuration.General.CombatModule = combatModule;
                _pluginInterface.SavePluginConfig(_configuration);
            }

            ImGui.SameLine(0);
            using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            {
                var iconColor = isInstalled ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;
                var icon = isInstalled ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;

                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(iconColor, icon.ToIconString());
            }


            DrawPluginDetails(plugin, checklistPadding, isInstalled);
        }
    }

    private void DrawPluginDetails(PluginInfo plugin, float checklistPadding, bool isInstalled)
    {
        using (ImRaii.PushIndent(checklistPadding))
        {
            if (!string.IsNullOrEmpty(plugin.Details))
                ImGui.TextUnformatted(plugin.Details);

            if (plugin.DetailsToCheck != null)
            {
                foreach (var detail in plugin.DetailsToCheck)
                    _uiUtils.ChecklistItem(detail.DisplayName, isInstalled && detail.Predicate());
            }

            ImGui.Spacing();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Globe, "Open Website"))
                Util.OpenLink(plugin.WebsiteUri.ToString());

            ImGui.SameLine();
            if (plugin.DalamudRepositoryUri != null)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Code, "Open Repository"))
                    Util.OpenLink(plugin.DalamudRepositoryUri.ToString());
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                ImGuiComponents.HelpMarker("Available on official Dalamud Repository");
            }
        }
    }

    private bool IsPluginInstalled(PluginInfo pluginInfo)
    {
        return _pluginInterface.InstalledPlugins.Any(x => x.InternalName == pluginInfo.InternalName && x.IsLoaded);
    }

    private sealed record PluginInfo(
        string DisplayName,
        string InternalName,
        string Details,
        Uri WebsiteUri,
        Uri? DalamudRepositoryUri,
        List<PluginDetailInfo>? DetailsToCheck = null);

    private sealed record PluginDetailInfo(string DisplayName, Func<bool> Predicate);
}
