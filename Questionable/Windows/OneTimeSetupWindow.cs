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
            vnavmesh 处理区域内的寻路，将你的角色移动到下一个与任务相关的目标。
            """,
            new Uri("https://github.com/awgil/ffxiv_navmesh/"),
            new Uri("https://puni.sh/api/repository/veyn")),
        new("Lifestream",
            "Lifestream",
            """
            用于在城市中传送到小型以太之光。
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
        {
            Configuration.ECombatModule.RotationSolverReborn,
            new("Rotation Solver Reborn",
                "RotationSolver",
                string.Empty,
                new Uri("https://github.com/FFXIV-CombatReborn/RotationSolverReborn"),
                new Uri(
                    "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"))
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
           new("NotificationMaster",
                "NotificationMaster",
                """
                当任务需要手动操作时，发送可配置的游戏外通知。
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

        ImGui.Text("Questionable 需要以下插件才能正常工作：");
        ImGui.TextColored(ImGuiColors.DalamudRed, "注意下方提供的所有链接均为国际服原始库，如果无法使用，请自行寻找国服特供。");
        ImGui.TextColored(ImGuiColors.DalamudRed, "注意下方提供的所有链接均为国际服原始库，如果无法使用，请自行寻找国服特供。");
        ImGui.TextColored(ImGuiColors.DalamudRed, "注意下方提供的所有链接均为国际服原始库，如果无法使用，请自行寻找国服特供。");
        bool allRequiredInstalled = true;
        using (ImRaii.PushIndent())
        {
            foreach (var plugin in RequiredPlugins)
                allRequiredInstalled &= DrawPlugin(plugin, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Questionable 支持多个自动输出/循环插件，请选择你想要使用的:");

        using (ImRaii.PushIndent())
        {
            if (ImGui.RadioButton("不使用自动输出/循环插件（战斗必须手动进行）",
                    _configuration.General.CombatModule == Configuration.ECombatModule.None))
            {
                _configuration.General.CombatModule = Configuration.ECombatModule.None;
                _pluginInterface.SavePluginConfig(_configuration);
            }

            DrawCombatPlugin(Configuration.ECombatModule.BossMod, checklistPadding);
            DrawCombatPlugin(Configuration.ECombatModule.WrathCombo, checklistPadding);
            DrawCombatPlugin(Configuration.ECombatModule.RotationSolverReborn, checklistPadding);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("以下插件是推荐的，但不是必需的：");
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
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "完成设置"))
                {
                    _logger.LogInformation("标记设置完成");
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
                    ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "缺少必需的插件");
            }
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "关闭窗口并不启用 Questionable"))
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
                ImGuiComponents.HelpMarker("可在官方 Dalamud 插件库中找到");
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
