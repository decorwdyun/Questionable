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
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.ConfigComponents;

internal sealed class DutyConfigComponent : ConfigComponent
{
    private const string DutyClipboardPrefix = "qst:duty:";

    private readonly QuestRegistry _questRegistry;
    private readonly AutoDutyIpc _autoDutyIpc;
    private readonly Dictionary<EExpansionVersion, List<DutyInfo>> _contentFinderConditionNames;

    public DutyConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        IDataManager dataManager,
        QuestRegistry questRegistry,
        AutoDutyIpc autoDutyIpc,
        TerritoryData territoryData)
        : base(pluginInterface, configuration)
    {
        _questRegistry = questRegistry;
        _autoDutyIpc = autoDutyIpc;

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
                    .Select(y => new DutyInfo(y.CfcId, y.TerritoryId, $"{FormatLevel(y.Level)} {y.Name}"))
                    .ToList());
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("副本###Duties");
        if (!tab)
            return;

        bool runInstancedContentWithAutoDuty = Configuration.Duties.RunInstancedContentWithAutoDuty;
        if (ImGui.Checkbox("使用 AutoDuty 和 BossMod 自动通过副本", ref runInstancedContentWithAutoDuty))
        {
            Configuration.Duties.RunInstancedContentWithAutoDuty = runInstancedContentWithAutoDuty;
            Save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "此功能使用的战斗模块由 AutoDuty 配置，将忽略在 Questionable 的\"通用\"设置中所做的选择。");

        ImGui.Separator();

        using (ImRaii.Disabled(!runInstancedContentWithAutoDuty))
        {
            ImGui.Text(
                "Questionable 包含一个默认的副本列表，如果安装了 AutoDuty 和 BossMod，副本任务就会自动进行。");

            ImGui.Text(
                "此副本列表可能会随着每次更新而变化，并基于以下表格：:");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.GlobeEurope, "Open AutoDuty spreadsheet"))
                Util.OpenLink(
                    "https://docs.google.com/spreadsheets/d/151RlpqRcCpiD_VbQn6Duf-u-S71EP7d0mx3j1PDNoNA/edit?pli=1#gid=0");

            ImGui.Separator();
            ImGui.Text("您可以覆盖每个副本/试炼的设置：");

            DrawConfigTable(runInstancedContentWithAutoDuty);

            DrawClipboardButtons();
            ImGui.SameLine();
            DrawResetButton();
        }
    }

    private void DrawConfigTable(bool runInstancedContentWithAutoDuty)
    {
        using var child = ImRaii.Child("DutyConfiguration", new Vector2(650, 400), true);
        if (!child)
            return;

        foreach (EExpansionVersion expansion in Enum.GetValues<EExpansionVersion>())
        {
            if (ImGui.CollapsingHeader(expansion.ToFriendlyString()))
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
                            if (_questRegistry.TryGetDutyByContentFinderConditionId(cfcId, out DutyOptions? dutyOptions))
                            {
                                ImGui.TableNextRow();

                                string[] labels = dutyOptions.Enabled
                                    ? SupportedCfcOptions
                                    : UnsupportedCfcOptions;
                                int value = 0;
                                if (Configuration.Duties.WhitelistedDutyCfcIds.Contains(cfcId))
                                    value = 1;
                                if (Configuration.Duties.BlacklistedDutyCfcIds.Contains(cfcId))
                                    value = 2;

                                if (ImGui.TableNextColumn())
                                {
                                    ImGui.AlignTextToFramePadding();
                                    ImGui.TextUnformatted(name);
                                    if (ImGui.IsItemHovered() &&
                                        Configuration.Advanced.AdditionalStatusInformation)
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
                                        ImGuiComponents.HelpMarker("尚未支持此副本或 AutoDuty 插件未启用",
                                            FontAwesomeIcon.Times, ImGuiColors.DalamudRed);
                                    else if (dutyOptions.Notes.Count > 0)
                                        DrawNotes(dutyOptions.Enabled, dutyOptions.Notes);
                                }

                                if (ImGui.TableNextColumn())
                                {
                                    using var _ = ImRaii.PushId($"##Dungeon{cfcId}");
                                    ImGui.SetNextItemWidth(200);
                                    if (ImGui.Combo(string.Empty, ref value, labels, labels.Length))
                                    {
                                        Configuration.Duties.WhitelistedDutyCfcIds.Remove(cfcId);
                                        Configuration.Duties.BlacklistedDutyCfcIds.Remove(cfcId);

                                        if (value == 1)
                                            Configuration.Duties.WhitelistedDutyCfcIds.Add(cfcId);
                                        else if (value == 2)
                                            Configuration.Duties.BlacklistedDutyCfcIds.Add(cfcId);

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

    private void DrawClipboardButtons()
    {
        using (ImRaii.Disabled(Configuration.Duties.WhitelistedDutyCfcIds.Count +
                   Configuration.Duties.BlacklistedDutyCfcIds.Count == 0))
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, "Export to clipboard"))
            {
                var whitelisted =
                    Configuration.Duties.WhitelistedDutyCfcIds.Select(x => $"{DutyWhitelistPrefix}{x}");
                var blacklisted =
                    Configuration.Duties.BlacklistedDutyCfcIds.Select(x => $"{DutyBlacklistPrefix}{x}");
                string text = DutyClipboardPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    string.Join(DutyClipboardSeparator, whitelisted.Concat(blacklisted))));
                ImGui.SetClipboardText(text);
            }
        }

        ImGui.SameLine();

        string? clipboardText = GetClipboardText();
        using (ImRaii.Disabled(clipboardText == null ||
                               !clipboardText.StartsWith(DutyClipboardPrefix, StringComparison.InvariantCulture)))
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Paste, "Import from Clipboard"))
            {
                clipboardText = clipboardText!.Substring(DutyClipboardPrefix.Length);
                string text = Encoding.UTF8.GetString(Convert.FromBase64String(clipboardText));

                Configuration.Duties.WhitelistedDutyCfcIds.Clear();
                Configuration.Duties.BlacklistedDutyCfcIds.Clear();
                foreach (string part in text.Split(DutyClipboardSeparator))
                {
                    if (part.StartsWith(DutyWhitelistPrefix, StringComparison.InvariantCulture) &&
                        uint.TryParse(part.AsSpan(DutyWhitelistPrefix.Length), CultureInfo.InvariantCulture,
                            out uint whitelistedCfcId))
                        Configuration.Duties.WhitelistedDutyCfcIds.Add(whitelistedCfcId);

                    if (part.StartsWith(DutyBlacklistPrefix, StringComparison.InvariantCulture) &&
                        uint.TryParse(part.AsSpan(DutyBlacklistPrefix.Length), CultureInfo.InvariantCulture,
                            out uint blacklistedCfcId))
                        Configuration.Duties.WhitelistedDutyCfcIds.Add(blacklistedCfcId);
                }
            }
        }
    }

    private void DrawResetButton()
    {
        using (ImRaii.Disabled(!ImGui.IsKeyDown(ImGuiKey.ModCtrl)))
        {
            if (ImGui.Button("重置为默认设置"))
            {
                Configuration.Duties.WhitelistedDutyCfcIds.Clear();
                Configuration.Duties.BlacklistedDutyCfcIds.Clear();
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住 CTRL 启用此按钮");
    }

    private sealed record DutyInfo(uint CfcId, uint TerritoryId, string Name);
}
