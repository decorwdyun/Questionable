using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using Questionable.Controller;
using Questionable.External;

namespace Questionable.Windows.ConfigComponents;

internal sealed class PluginConfigComponent : ConfigComponent
{
    private static readonly IReadOnlyList<PluginInfo> RequiredPlugins =
    [
        new("vnavmesh",
            "vnavmesh",
            """
            vnavmesh 处理寻路、导航，负责将你的角色移动到下一个与任务相关的目的地。
            """,
            new Uri("https://github.com/awgil/ffxiv_navmesh/"),
            new Uri("https://puni.sh/api/repository/veyn")),
        new("Lifestream",
            "Lifestream",
            """
            用于在城市小型以太之光间传送。
            """,
            new Uri("https://github.com/NightmareXIV/Lifestream"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
        new("TextAdvance",
            "TextAdvance",
            """
            自动接受并交付任务，跳过过场动画和对话。
            """,
            new Uri("https://github.com/NightmareXIV/TextAdvance"),
            new Uri("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json")),
    ];

    private static readonly ReadOnlyDictionary<Configuration.ECombatModule, PluginInfo> CombatPlugins =
        new Dictionary<Configuration.ECombatModule, PluginInfo>
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
            {
                Configuration.ECombatModule.RotationSolverReborn,
                new("Rotation Solver Reborn",
                    "RotationSolver",
                    string.Empty,
                    new Uri("https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
                    new Uri(
                        "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"))
            },
            {
                Configuration.ECombatModule.AEAssist,
                new("AEAssist",
                    "AEAssistV3",
                    "如果你希望使用 AEAssist，请自行寻找安装方式",
                    new Uri("https://www.adobe.com/products/aftereffects.html"),
                    new Uri(
                        "https://www.adobe.com/products/aftereffects.html"))
            },
        }.AsReadOnly();

    private readonly IReadOnlyList<PluginInfo> _recommendedPlugins;

    private readonly Configuration _configuration;
    private readonly CombatController _combatController;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiUtils _uiUtils;
    private readonly ICommandManager _commandManager;

    public PluginConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        CombatController combatController,
        UiUtils uiUtils,
        ICommandManager commandManager)
        : base(pluginInterface, configuration)
    {
        _configuration = configuration;
        _combatController = combatController;
        _pluginInterface = pluginInterface;
        _uiUtils = uiUtils;
        _commandManager = commandManager;
        _recommendedPlugins =
        [
            new("NotificationMaster",
                "NotificationMaster",
                """
                如果任务需要手动操作，发送游戏外的 Windows 通知。
                """,
                new Uri("https://github.com/NightmareXIV/NotificationMaster"),
                null),
        ];
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("插件依赖###Plugins");
        if (!tab)
            return;

        Draw(out bool allRequiredInstalled);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (allRequiredInstalled)
            ImGui.TextColored(ImGuiColors.ParsedGreen, "所有需要的插件都已安装.");
        else
            ImGui.TextColored(ImGuiColors.DalamudRed,
                "缺少必需的插件，Questionable 可能无法正常工作.");
    }

    public void Draw(out bool allRequiredInstalled)
    {
        float checklistPadding;
        using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            checklistPadding = ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X +
                               ImGui.GetStyle().ItemSpacing.X;
        }

        ImGui.Text("Questionable 必须安装以下 3 个插件才能正常工作：");
        allRequiredInstalled = true;
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in RequiredPlugins)
                allRequiredInstalled &= DrawPlugin(plugin, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Questionable 支持多个自动输出/循环插件，请选择你想要使用的：");

        using (ImRaii.PushIndent())
        {
            using (ImRaii.Disabled(_combatController.IsRunning))
            {
                if (ImGui.RadioButton("不使用自动输出/循环插件（战斗必须手动进行）",
                        _configuration.General.CombatModule == Configuration.ECombatModule.None))
                {
                    _configuration.General.CombatModule = Configuration.ECombatModule.None;
                    _pluginInterface.SavePluginConfig(_configuration);
                }

                allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.BossMod, checklistPadding);
                allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.WrathCombo, checklistPadding);
                allRequiredInstalled &=
                    DrawCombatPlugin(Configuration.ECombatModule.RotationSolverReborn, checklistPadding);
                allRequiredInstalled &=
                    DrawCombatPlugin(Configuration.ECombatModule.AEAssist, checklistPadding);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("以下插件不是必需的，但是推荐安装：");
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in _recommendedPlugins)
                DrawPlugin(plugin, checklistPadding);
        }
    }

    private bool DrawPlugin(PluginInfo plugin, float checklistPadding)
    {
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            IExposedPlugin? installedPlugin = FindInstalledPlugin(plugin);
            bool isInstalled = installedPlugin != null;
            string label = plugin.DisplayName;
            if (installedPlugin != null)
                label += $" v{installedPlugin.Version}";

            _uiUtils.ChecklistItem(label, isInstalled);

            DrawPluginDetails(plugin, checklistPadding, isInstalled);
            return isInstalled;
        }
    }

    private bool DrawCombatPlugin(Configuration.ECombatModule combatModule, float checklistPadding)
    {
        ImGui.Spacing();

        PluginInfo plugin = CombatPlugins[combatModule];
        using (ImRaii.PushId("plugin_" + plugin.DisplayName))
        {
            IExposedPlugin? installedPlugin = FindInstalledPlugin(plugin);
            bool isInstalled = installedPlugin != null;
            string label = plugin.DisplayName;
            if (installedPlugin != null)
                label += $" v{installedPlugin.Version}";

            if (ImGui.RadioButton(label, _configuration.General.CombatModule == combatModule))
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
            return isInstalled || _configuration.General.CombatModule != combatModule;
        }
    }

    private void DrawPluginDetails(PluginInfo plugin, float checklistPadding, bool isInstalled)
    {
        using (ImRaii.PushIndent(checklistPadding))
        {
            if (!string.IsNullOrEmpty(plugin.Details))
                ImGui.TextUnformatted(plugin.Details);

            bool allDetailsOk = true;
            if (plugin.DetailsToCheck != null)
            {
                foreach (var detail in plugin.DetailsToCheck)
                {
                    bool detailOk = detail.Predicate();
                    allDetailsOk &= detailOk;

                    _uiUtils.ChecklistItem(detail.DisplayName, isInstalled && detailOk);
                    if (!string.IsNullOrEmpty(detail.Details))
                    {
                        using (ImRaii.PushIndent(checklistPadding))
                        {
                            ImGui.TextUnformatted(detail.Details);
                        }
                    }
                }
            }

            ImGui.Spacing();

            if (isInstalled)
            {
                if (!allDetailsOk && plugin.ConfigCommand != null && plugin.ConfigCommand.StartsWith('/'))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Cog, "Open configuration"))
                        _commandManager.ProcessCommand(plugin.ConfigCommand);
                }
            }
            else
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Globe, "打开网站"))
                    Util.OpenLink(plugin.WebsiteUri.ToString());

                ImGui.SameLine();
                if (plugin.DalamudRepositoryUri != null)
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Code, "打开代码库"))
                        Util.OpenLink(plugin.DalamudRepositoryUri.ToString());
                }
                else
                {
                    ImGui.AlignTextToFramePadding();
                    ImGuiComponents.HelpMarker("在官方 Dalamud 插件库中找到");
                }
            }
        }
    }

    private IExposedPlugin? FindInstalledPlugin(PluginInfo pluginInfo)
    {
        return _pluginInterface.InstalledPlugins.FirstOrDefault(x =>
            x.InternalName == pluginInfo.InternalName && x.IsLoaded);
    }

    private sealed record PluginInfo(
        string DisplayName,
        string InternalName,
        string Details,
        Uri WebsiteUri,
        Uri? DalamudRepositoryUri,
        string? ConfigCommand = null,
        List<PluginDetailInfo>? DetailsToCheck = null);

    private sealed record PluginDetailInfo(string DisplayName, string Details, Func<bool> Predicate);
}
