using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using Questionable.Controller;
using Questionable.External;
using Questionable.Utils;
using static Questionable.Utils.LocalizeShortcut;
namespace Questionable.Windows.ConfigComponents;

internal sealed class PluginConfigComponent
(
    IDalamudPluginInterface pluginInterface,
    Configuration configuration,
    CombatController combatController,
    UiUtils uiUtils,
    ICommandManager commandManager,
    AutomatonIpc automatonIpc,
    PandorasBoxIpc pandorasBoxIpc) : ConfigComponent(pluginInterface, configuration)
{
    private static readonly IReadOnlyList<PluginInfo> RequiredPlugins =
    [
        new("vnavmesh",
            "vnavmesh",
            _L("""
            vnavmesh 处理寻路、导航，负责将你的角色移动到下一个与任务相关的目的地。
            """),
            new("https://github.com/awgil/ffxiv_navmesh/"),
            new("https://puni.sh/api/repository/veyn"),
            "/vnav"),
        new("Lifestream",
            "Lifestream",
            _L("""
            用于在城市小型以太之光间传送。
            """),
            new("https://github.com/NightmareXIV/Lifestream"),
            new("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json"),
            "/lifestream"),
        new("TextAdvance",
            "TextAdvance",
            _L("""
            自动接受并交付任务，跳过过场动画和对话。
            """),
            new("https://github.com/NightmareXIV/TextAdvance"),
            new("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json"),
            "/at c")
    ];

    private static readonly ReadOnlyDictionary<Configuration.ECombatModule, PluginInfo> CombatPlugins =
        new Dictionary<Configuration.ECombatModule, PluginInfo>
        {
            {
                Configuration.ECombatModule.BossMod,
                new("Boss Mod (VBM)",
                    "BossMod",
                    string.Empty,
                    new("https://github.com/awgil/ffxiv_bossmod"),
                    new("https://puni.sh/api/repository/veyn"),
                    "/vbm")
            },
            {
                Configuration.ECombatModule.WrathCombo,
                new("Wrath Combo",
                    "WrathCombo",
                    string.Empty,
                    new("https://github.com/PunishXIV/WrathCombo"),
                    new("https://puni.sh/api/plugins"),
                    "/wrath")
            },
                        {
                Configuration.ECombatModule.RotationSolverReborn,
                new("Rotation Solver Reborn",
                    "RotationSolver",
                    string.Empty,
                    new("https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
                    new(
                        "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"),
                    "/rsr")
            },
            {
                Configuration.ECombatModule.AEAssist,
                new("AEAssist",
                    "AEAssistV3",
                    "如果你希望使用 AEAssist，请自行寻找安装方式",
                    new("https://github.com/FFXIV-CombatReborn/AEAssist"),
                    null)
            }

        }.AsReadOnly();
    private readonly CombatController _combatController = combatController;
    private readonly ICommandManager _commandManager = commandManager;

    private readonly Configuration _configuration = configuration;
    private readonly IDalamudPluginInterface _pluginInterface = pluginInterface;

    private readonly IReadOnlyList<PluginInfo> _recommendedPlugins =
    [
        new("CBT (formerly known as Automaton)",
            "Automaton",
            _L("""
            Automaton 是一组自动化相关的功能合集。
            """),
            new("https://github.com/Jaksuhn/Automaton"),
            new("https://puni.sh/api/repository/croizat"),
            "/cbt",
            [
                new(_L("已启用 'Sniper no sniping'"),
                    _L("自动完成红莲版本加入的狙击小游戏任务"),
                    () => automatonIpc.IsAutoSnipeEnabled)
            ]),
        new("Pandora's Box",
            "PandorasBox",
            _L("""
            Pandora's Box 是一组便捷功能合集。
            """),
            new("https://github.com/PunishXIV/PandorasBox"),
            new("https://puni.sh/api/plugins"),
            "/pandora",
            [
                new(_L("已启用 'Auto Active Time Maneuver'"),
                    _L("""
                    自动完成单人任务、副本和大型任务中的 Active Time Maneuver。
                    """),
                    () => pandorasBoxIpc.IsAutoActiveTimeManeuverEnabled)
            ]),
        new("Artisan",
            "Artisan",
            _L("""
            全自动生产插件（自动制作）。
            """),
            new("https://github.com/PunishXIV/Artisan"),
            new("https://puni.sh/api/plugins"),
            "/artisan"),
        new("NotificationMaster",
            "NotificationMaster",
            _L("""
            Sends a configurable out-of-game notification if a quest
            requires manual actions.
            """),
            new Uri("https://github.com/NightmareXIV/NotificationMaster"),
            new("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json"),
            "/pnotify"),
        new("AutoDuty",
            "AutoDuty",
            _L("自动完成副本"),
            new("https://github.com/erdelf/AutoDuty"),
            new("https://puni.sh/api/repository/erdelf"),
            "/ad"),
        new("Stylist",
            "Stylist",
            _L("""
            装备管理器
            """),
            new("https://github.com/NightmareXIV/Stylist"),
            new("https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json"),
            "/stylist c")
    ];
    private readonly UiUtils _uiUtils = uiUtils;

    public override void DrawTab()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("插件依赖") + "###Plugins");
        if (!tab)
            return;

        Draw(out bool allRequiredInstalled);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (allRequiredInstalled)
            ImGui.TextColored(ImGuiColors.ParsedGreen, _L("所有需要的插件都已安装"));
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudRed,
                _L("缺少必需的插件，Questionable 可能无法正常工作。"));
        }
    }

    public void Draw(out bool allRequiredInstalled)
    {
        float checklistPadding;
        using (_pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            checklistPadding = ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X +
                               ImGui.GetStyle().ItemSpacing.X;
        }

        allRequiredInstalled = true;
        ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        if (ImGui.CollapsingHeader(_L("必需的插件:")))
        {
            using (ImRaii.PushIndent())
            {
                foreach (PluginInfo plugin in RequiredPlugins)
                    allRequiredInstalled &= DrawPlugin(plugin, checklistPadding);
            }
        }

        if (ImGui.CollapsingHeader(_L("自动输出/循环插件:")))
        {
            using (ImRaii.Disabled(_combatController.IsRunning))
            {
                using (ImRaii.PushIndent())
                {
                    if (ImGui.RadioButton(_L("不使用自动输出/循环插件（战斗必须手动进行）"),
                        _configuration.General.CombatModule == Configuration.ECombatModule.None))
                    {
                        _configuration.General.CombatModule = Configuration.ECombatModule.None;
                        _pluginInterface.SavePluginConfig(_configuration);
                    }

                    allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.BossMod, checklistPadding);
                    allRequiredInstalled &= DrawCombatPlugin(Configuration.ECombatModule.WrathCombo, checklistPadding);
                }

                ImGui.Text(_L("以下自动输出/循环插件仅用于兼容性和测试："));
                using (ImRaii.PushIndent())
                {
                    allRequiredInstalled &=
                        DrawCombatPlugin(Configuration.ECombatModule.RotationSolverReborn, checklistPadding);
                    allRequiredInstalled &=
                        DrawCombatPlugin(Configuration.ECombatModule.AEAssist, checklistPadding);
                }
            }
        }

        if (ImGui.CollapsingHeader(_L("推荐/小众插件:")))
        {
            using (ImRaii.PushIndent())
            {
                foreach (PluginInfo plugin in _recommendedPlugins)
                    DrawPlugin(plugin, checklistPadding);
            }
        }
    }

    private void AddConfigClickable(IExposedPlugin? installedPlugin, PluginInfo plugin)
    {
        if (installedPlugin != null && plugin.ConfigCommand != null && plugin.ConfigCommand.StartsWith('/'))
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_L("打开设置"));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
                _commandManager.ProcessCommand(plugin.ConfigCommand);

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

            ImGui.BeginGroup();
            if (installedPlugin != null && installedPlugin.InternalName.Equals("vnavmesh", StringComparison.Ordinal) && (installedPlugin.Manifest.Author.Contains("AtmoOmen")))
                plugin = new(
                    plugin.DisplayName,
                    plugin.InternalName,
                    plugin.Details,
                    new("https://github.com/AtmoOmen/ffxiv_navmesh-cn"),
                    new("https://gh.atmoomen.top/DalamudPlugins/main/pluginmaster.json"),
                    plugin.ConfigCommand
                );
            _uiUtils.ChecklistItem(label, isInstalled);

            DrawPluginDetails(plugin, checklistPadding, isInstalled);
            ImGui.EndGroup();
            AddConfigClickable(installedPlugin, plugin);
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
                Vector4 iconColor = isInstalled ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;
                FontAwesomeIcon icon = isInstalled ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;

                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(iconColor, icon.ToIconString());
            }
            AddConfigClickable(installedPlugin, plugin);

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
                foreach (PluginDetailInfo detail in plugin.DetailsToCheck)
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
                    ImRaii.ColorDisposable? color = null;
                    if (!allDetailsOk)
                        color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                    if (ImGuiComponentsLocal.IconButton(FontAwesomeIcon.Cog))
                        _commandManager.ProcessCommand(plugin.ConfigCommand);
                    color?.Dispose();
                }
            }
            else
            {
                if (ImGuiComponentsLocal.IconButtonWithText(FontAwesomeIcon.Globe, _L("打开网站")))
                    Util.OpenLink(plugin.WebsiteUri.ToString());

                ImGui.SameLine();
                if (plugin.DalamudRepositoryUri != null)
                {
                    if (ImGuiComponentsLocal.IconButtonWithText(FontAwesomeIcon.Code, _L("打开仓库")))
                        Util.OpenLink(plugin.DalamudRepositoryUri.ToString());
                }
                else
                {
                    ImGui.AlignTextToFramePadding();
                    ImGuiComponents.HelpMarker(_L("可在官方 Dalamud 插件库中找到"));
                }
            }
        }
    }

    private static bool PluginImageButton(PluginInfo plugin, float size, bool isInstalled, bool isActive)
    {
        string url = $"https://qstxiv.github.io/icons/{plugin.InternalName}.png";
        if (ThreadLoadImageHandler.TryGetTextureWrap(url, out IDalamudTextureWrap? logo))
        {
            return ImGui.ImageButton(
                logo.Handle,
                new(size.Scale(), size.Scale()),
                2,
                isInstalled ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed,
                isActive ? Vector4.One : new(0.5f, 0.5f, 0.5f, 1f)
            );
        }

        return false;
    }

    private IExposedPlugin? FindInstalledPlugin(PluginInfo pluginInfo)
    {
        return _pluginInterface.InstalledPlugins.FirstOrDefault(x =>
            x.InternalName == pluginInfo.InternalName && x.IsLoaded);
    }

    private sealed record PluginInfo
    (
        string DisplayName,
        string InternalName,
        string Details,
        Uri WebsiteUri,
        Uri? DalamudRepositoryUri,
        string? ConfigCommand = null,
        List<PluginDetailInfo>? DetailsToCheck = null);

    private sealed record PluginDetailInfo(string DisplayName, string Details, Func<bool> Predicate);
}
