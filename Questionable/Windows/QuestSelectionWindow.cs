using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using LLib.GameUI;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;

namespace Questionable.Windows;

internal sealed class QuestSelectionWindow : LWindow
{
    private const string WindowId = "###QuestionableQuestSelection";
    private readonly QuestData _questData;
    private readonly IGameGui _gameGui;
    private readonly IChatGui _chatGui;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly IDalamudPluginInterface _pluginInterface;

    private List<QuestInfo> _quests = [];
    private List<QuestInfo> _offeredQuests = [];
    private bool _onlyAvailableQuests = true;

    public QuestSelectionWindow(QuestData questData, IGameGui gameGui, IChatGui chatGui, GameFunctions gameFunctions,
        QuestController questController, QuestRegistry questRegistry, IDalamudPluginInterface pluginInterface)
        : base($"Quest Selection{WindowId}")
    {
        _questData = questData;
        _gameGui = gameGui;
        _chatGui = chatGui;
        _gameFunctions = gameFunctions;
        _questController = questController;
        _questRegistry = questRegistry;
        _pluginInterface = pluginInterface;

        Size = new Vector2(500, 200);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 200),
        };
    }

    public uint TargetId { get; private set; }
    public string TargetName { get; private set; } = string.Empty;

    public unsafe void Open(IGameObject? gameObject)
    {
        if (gameObject != null)
        {
            TargetId = gameObject.DataId;
            TargetName = gameObject.Name.ToString();
            WindowName = $"Quests starting with {TargetName} [{TargetId}]{WindowId}";

            _quests = _questData.GetAllByIssuerDataId(TargetId);
            if (_gameGui.TryGetAddonByName<AddonSelectIconString>("SelectIconString", out var addonSelectIconString))
            {
                var answers = GameUiController.GetChoices(addonSelectIconString);
                _offeredQuests = _quests
                    .Where(x => answers.Any(y => GameUiController.GameStringEquals(x.Name, y)))
                    .ToList();
            }
            else
                _offeredQuests = [];
        }
        else
        {
            _quests = [];
            _offeredQuests = [];
        }

        IsOpen = _quests.Count > 0;
    }

    public override void Draw()
    {
        if (_offeredQuests.Count != 0)
            ImGui.Checkbox("Only show quests currently offered", ref _onlyAvailableQuests);

        using var table = ImRaii.Table("QuestSelection", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table)
        {
            ImGui.Text("Not table");
            return;
        }

        ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 18);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 200);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableHeadersRow();

        foreach (QuestInfo quest in (_offeredQuests.Count != 0 && _onlyAvailableQuests) ? _offeredQuests : _quests)
        {
            ImGui.TableNextRow();

            string questId = quest.QuestId.ToString(CultureInfo.InvariantCulture);
            bool isKnownQuest = _questRegistry.TryGetQuest(quest.QuestId, out var knownQuest);

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(questId);
            }

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                using var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();

                FontAwesomeIcon icon;
                Vector4 color;
                if (_gameFunctions.IsQuestAccepted(quest.QuestId))
                {
                    color = ImGuiColors.DalamudYellow;
                    icon = FontAwesomeIcon.Running;
                }
                else if (_gameFunctions.IsQuestAcceptedOrComplete(quest.QuestId))
                {
                    color = ImGuiColors.ParsedGreen;
                    icon = FontAwesomeIcon.Check;
                }
                else
                {
                    color = ImGuiColors.DalamudRed;
                    icon = FontAwesomeIcon.Times;
                }

                if (isKnownQuest)
                    ImGui.TextColored(color, icon.ToIconString());
                else
                    ImGui.TextColored(ImGuiColors.DalamudGrey, icon.ToIconString());
            }

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(quest.Name);
            }

            if (ImGui.TableNextColumn())
            {
                using var id = ImRaii.PushId(questId);

                bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Copy as file name");
                if (copy)
                    CopyToClipboard(quest, true);
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    CopyToClipboard(quest, false);

                ImGui.SameLine();

                if (knownQuest != null && !_gameFunctions.IsQuestAccepted(quest.QuestId) &&
                    (quest.IsRepeatable || !_gameFunctions.IsQuestAcceptedOrComplete(quest.QuestId)))
                {
                    ImGui.BeginDisabled(_questController.NextQuest != null || _questController.SimulatedQuest != null);

                    bool startNextQuest = ImGuiComponents.IconButton(FontAwesomeIcon.Play);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Start as next quest");
                    if (startNextQuest)
                    {
                        _questController.SetNextQuest(knownQuest);
                        _questController.ExecuteNextStep(true);
                    }

                    ImGui.SameLine();

                    bool setNextQuest = ImGuiComponents.IconButton(FontAwesomeIcon.AngleDoubleRight);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Set as next quest");
                    if (setNextQuest)
                        _questController.SetNextQuest(knownQuest);

                    ImGui.EndDisabled();
                }
            }
        }
    }

    private void CopyToClipboard(QuestInfo quest, bool suffix)
    {
        string fileName = $"{quest.QuestId}_{quest.SimplifiedName}{(suffix ? ".json" : "")}";
        ImGui.SetClipboardText(fileName);
        _chatGui.Print($"Copied '{fileName}' to clipboard");
    }
}
