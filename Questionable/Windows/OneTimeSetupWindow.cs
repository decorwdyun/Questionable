using System;
using System.Collections.Generic;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using LLib;
using LLib.ImGui;
using Microsoft.Extensions.Logging;
using Questionable.External;

namespace Questionable.Windows;

internal sealed class OneTimeSetupWindow : LWindow
{
    private static readonly IReadOnlyList<PluginInfo> RequiredPlugins =
    [
        new("vnavmesh",
            """
            vnavmesh handles the navigation within a zone, moving
            your character to the next quest-related objective.
            """,
            new Uri("https://github.com/awgil/ffxiv_navmesh/"),
            new Uri("https://puni.sh/api/repository/veyn")),
        new("Lifestream",
            """
            Used to travel to aethernet shards in cities.
            """,
            new Uri("https://github.com/NightmareXIV/Lifestream"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
        new("TextAdvance",
            """
            Automatically accepts and turns in quests, skips cutscenes
            and dialogue.
            """,
            new Uri("https://github.com/NightmareXIV/TextAdvance"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
    ];

    private readonly IReadOnlyList<PluginInfo> _recommendedPlugins;

    private readonly Configuration _configuration;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiUtils _uiUtils;
    private readonly DalamudReflector _dalamudReflector;
    private readonly ILogger<OneTimeSetupWindow> _logger;

    public OneTimeSetupWindow(Configuration configuration, IDalamudPluginInterface pluginInterface, UiUtils uiUtils,
        DalamudReflector dalamudReflector, ILogger<OneTimeSetupWindow> logger, AutomatonIpc automatonIpc)
        : base("Questionable Setup###QuestionableOneTimeSetup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings, true)
    {
        _configuration = configuration;
        _pluginInterface = pluginInterface;
        _uiUtils = uiUtils;
        _dalamudReflector = dalamudReflector;
        _logger = logger;

        _recommendedPlugins =
        [
            new("Rotation Solver Reborn",
                """
                Automatically handles most combat interactions you encounter
                during quests, including being interrupted by mobs.
                """,
                new Uri("https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
                new Uri(
                    "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json")),
            new PluginInfo("CBT (formerly known as Automaton)",
                """
                Automaton is a collection of automation-related tweaks.
                The 'Sniper no sniping' tweak can complete snipe tasks automatically.
                """,
                new Uri("https://github.com/Jaksuhn/Automaton"),
                new Uri("https://puni.sh/api/repository/croizat"),
                [new PluginDetailInfo("'Sniper no sniping' enabled", () => automatonIpc.IsAutoSnipeEnabled)]),
            new("NotificationMaster",
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
        bool isInstalled = IsPluginInstalled(plugin.DisplayName);
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            _uiUtils.ChecklistItem(plugin.DisplayName, isInstalled);
            using (ImRaii.PushIndent(checklistPadding))
            {
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

        return isInstalled;
    }

    private bool IsPluginInstalled(string internalName)
    {
        return _dalamudReflector.TryGetDalamudPlugin(internalName, out _, suppressErrors: true, ignoreCache: true);
    }

    private sealed record PluginInfo(
        string DisplayName,
        string Details,
        Uri WebsiteUri,
        Uri? DalamudRepositoryUri,
        List<PluginDetailInfo>? DetailsToCheck = null);

    private sealed record PluginDetailInfo(string DisplayName, Func<bool> Predicate);
}
