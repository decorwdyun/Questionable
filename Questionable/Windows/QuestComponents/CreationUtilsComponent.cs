using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Text.ReadOnly;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using Questionable.Windows.Utils;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Questionable.Windows.QuestComponents;

internal sealed class CreationUtilsComponent(
    QuestController questController,
    MovementController movementController,
    GameFunctions gameFunctions,
    QuestRegistry questRegistry,
    QuestFunctions questFunctions,
    CameraFunctions cameraFunctions,
    TerritoryData territoryData,
    QuestData questData,
    QuestSelectionWindow questSelectionWindow,
    PriorityWindow priorityWindow,
    IClientState clientState,
    IObjectTable objectTable,
    //IPlayerState playerState,
    ITargetManager targetManager,
    ICondition condition,
    IGameGui gameGui,
    Configuration configuration,
    ILogger<CreationUtilsComponent> logger)
{
    private readonly QuestController _questController = questController;
    private readonly MovementController _movementController = movementController;
    private readonly GameFunctions _gameFunctions = gameFunctions;
    private readonly QuestRegistry _questRegistry = questRegistry;
    private readonly QuestFunctions _questFunctions = questFunctions;
    private readonly CameraFunctions _cameraFunctions = cameraFunctions;
    private readonly TerritoryData _territoryData = territoryData;
    private readonly QuestData _questData = questData;
    private readonly QuestSelectionWindow _questSelectionWindow = questSelectionWindow;
    private readonly PriorityWindow _priorityWindow = priorityWindow;
    private readonly IClientState _clientState = clientState;
    private readonly IObjectTable _objectTable = objectTable;
    //private readonly IPlayerState _playerState;
    private readonly ITargetManager _targetManager = targetManager;
    private readonly ICondition _condition = condition;
    private readonly IGameGui _gameGui = gameGui;
    private readonly Configuration _configuration = configuration;
    private readonly ILogger<CreationUtilsComponent> _logger = logger;
    private readonly RedoUtil _redoUtil = new RedoUtil();

    public void Draw()
    {
        if (_objectTable[0] == null) return;

        string territoryName = _territoryData.GetNameAndId(_clientState.TerritoryType);
        ImGui.Text(territoryName);

        if (_gameFunctions.IsFlyingUnlockedInCurrentZone())
        {
            ImGui.SameLine();
            ImGui.Text(SeIconChar.BotanistSprout.ToIconString());
        }

        if (_configuration.Advanced.AdditionalStatusInformation)
        {
            ImGui.Separator();
            QuestReference q = _questFunctions.GetCurrentQuest();
            ImGui.Text($"QST prio: {q.CurrentQuest} → {q.Sequence}");
            var simQ = _questController.SimulatedQuest?.Quest;
            if (simQ != null)
            {
                ImGui.Text($"Sim: {simQ.Id} → {_questController.SimulatedQuest?.Sequence}");
            }
            unsafe
            {
                if (_configuration.Advanced.ShowNewGamePlus)
                {
                    var qid = (uint)(q.CurrentQuest?.Value ?? 0) + 65536;
                    if (simQ != null)
                        qid = (uint)simQ.Id.Value + 65536;
                    var chapter = _redoUtil.GetChapter(qid);
                    string isSim = simQ != null ? " (sim)" : "";
                    if (!chapter.Item1.IsEmpty)
                        ImGui.Text($"NG+{isSim}: {chapter.Item1} (#{chapter.Item2+1})");
                }
                if (_configuration.Advanced.ShowDailies || _configuration.Advanced.ShowTracked)
                {
                    var questManager = QuestManager.Instance();
                    if (questManager != null)
                    {
                        if (_configuration.Advanced.ShowTracked)
                        {
                            for (int i = questManager->TrackedQuests.Length - 1; i >= 0; --i)
                            {
                                var trackedQuest = questManager->TrackedQuests[i];
                                switch (trackedQuest.QuestType)
                                {
                                    default:
                                        if (trackedQuest.QuestType != 0 || trackedQuest.Index != 0)
                                            ImGui.Text($"Tracked Quest {i}: {trackedQuest.QuestType}, {trackedQuest.Index}");
                                        break;

                                    case 1:
                                        //_questRegistry.TryGetQuest(questManager->NormalQuests[trackedQuest.Index].QuestId,
                                        //    out var quest);
                                        ImGui.Text(
                                            $"Tracked Quest: {questManager->NormalQuests[trackedQuest.Index].QuestId} → {questManager->NormalQuests[trackedQuest.Index].Sequence}");
                                        break;

                                    case 2:
                                        break;
                                }
                            }
                        }
                        if (_configuration.Advanced.ShowDailies)
                        {
                            for (int i = 0; i < questManager->DailyQuests.Length; ++i)
                            {
                                var dailyQuest = questManager->DailyQuests[i];
                                if (dailyQuest.QuestId != 0 && !dailyQuest.IsCompleted)
                                {
                                    ImGui.Text($"Daily Quest {i}: {dailyQuest.QuestId}, C:{dailyQuest.IsCompleted}");
                                    if (_questRegistry.TryGetQuest(new QuestId(dailyQuest.QuestId), out var quest))
                                    {
                                        if (ImGui.IsItemHovered())
                                            ImGui.SetTooltip($"{quest.Info.Name} ({quest.Info.AlliedSociety})");

                                        if (ImGui.IsItemClicked())
                                        {
                                            _questController.AddQuestPriority(quest.Id);
                                            if (!_priorityWindow.IsOpen)
                                                _priorityWindow.ToggleOrUncollapse();
                                            _priorityWindow.BringToFront();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (_configuration.Advanced.ShowDirector)
                {
                    var director = UIState.Instance()->DirectorTodo.Director;
                    if (director != null)
                    {
                        ImGui.Separator();
                        ImGui.Text($"Director: {director->ContentId}");
                        ImGui.Text($"Seq: {director->Sequence}");
                        ImGui.Text($"Ico: {director->IconId}");
                        if (director->EventHandlerInfo != null)
                        {
                            ImGui.Text($"  EHI CI: {director->Info.EventId.ContentId}");
                            ImGui.Text($"  EHI EI: {director->Info.EventId.Id}");
                            ImGui.Text($"  EHI EEI: {director->Info.EventId.EntryId}");
                            ImGui.Text($"  EHI F: {director->Info.Flags}");
                        }
                    }
                }
                if (_configuration.Advanced.ShowActionManager)
                {
                    ImGui.Separator();
                    var actionManager = ActionManager.Instance();
                    ImGui.Text(
                        $"A1: {actionManager->CastActionId} ({actionManager->LastUsedActionSequence} → {actionManager->LastHandledActionSequence})");
                    ImGui.Text($"A2: {actionManager->CastTimeElapsed} / {actionManager->CastTimeTotal}");
                    ImGui.Text($"PC: {_questController.TaskQueue.CurrentTaskExecutor?.ProgressContext}");
                }
            }
        }

        if (_targetManager.Target != null)
        {
            DrawTargetDetails(_targetManager.Target);
            DrawInteractionButtons(_targetManager.Target);
            ImGui.SameLine();
            DrawCopyButton(_targetManager.Target);
        }
        else
        {
            ImGui.Separator();
            DrawCopyButton();
        }

        ulong hoveredItemId = _gameGui.HoveredItem;
        if (hoveredItemId != 0)
        {
            ImGui.Separator();
            ImGui.Text($"Hovered Item: {hoveredItemId}");
        }
    }

    private unsafe void DrawTargetDetails(IGameObject target)
    {
        string nameId = string.Empty;
        if (target is ICharacter { NameId: > 0 } character)
            nameId = $"; n={character.NameId}";

        ImGui.Separator();
        ImGui.Text(string.Create(CultureInfo.InvariantCulture,
            $"目标: {target.Name}  ({target.ObjectKind}; {GameFunctions.GetBaseID(target)}{nameId})"));

        if (_objectTable[0] != null)
        {
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"距离: {(target.Position - _objectTable[0]!.Position).Length():F2}"));
            ImGui.SameLine();

            float verticalDistance = target.Position.Y - _objectTable[0]!.Position.Y;
            string verticalDistanceText = string.Create(CultureInfo.InvariantCulture, $"Y: {verticalDistance:F2}");
            if (Math.Abs(verticalDistance) >= MovementController.DefaultVerticalInteractionDistance)
                ImGui.TextColored(ImGuiColors.DalamudOrange, verticalDistanceText);
            else
                ImGui.Text(verticalDistanceText);

            ImGui.SameLine();
        }

        GameObject* gameObject = (GameObject*)target.Address;
        ImGui.Text($"QM: {gameObject->NamePlateIconId}");
    }

    private unsafe void DrawInteractionButtons(IGameObject target)
    {
        ImGui.BeginDisabled(!_movementController.IsNavmeshReady || _gameFunctions.IsOccupied());
        if (!_movementController.IsPathfinding)
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Bullseye, "前往目标位置"))
            {
                _movementController.NavigateTo(EMovementType.DebugWindow, GameFunctions.GetBaseID(target),
                    target.Position,
                    fly: _condition[ConditionFlag.Mounted] && _gameFunctions.IsFlyingUnlockedInCurrentZone(),
                    sprint: true);
            }
        }
        else
        {
            if (ImGui.Button("取消寻路"))
                _movementController.ResetPathfinding();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(!_questData.IsIssuerOfAnyQuest(GameFunctions.GetBaseID(target)));
        bool showQuests = ImGuiComponents.IconButton(FontAwesomeIcon.MapMarkerAlt);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("显示当前目标可接取的全部任务.");
        if (showQuests)
            _questSelectionWindow.OpenForTarget(_targetManager.Target);

        ImGui.EndDisabled();

        ImGui.BeginDisabled(_gameFunctions.IsOccupied());
        ImGui.SameLine();
        bool interact = ImGuiComponents.IconButton(FontAwesomeIcon.MousePointer);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("和当前目标进行交互.");
        if (interact)
        {
            _cameraFunctions.Face(target.Position);
            ulong result = TargetSystem.Instance()->InteractWithObject(
                (GameObject*)target.Address, false);
            _logger.LogInformation("XXXXX Interaction Result: {Result}", result);
        }

        ImGui.EndDisabled();
    }

    private string GetCurrentQuestInfoAsString()
    {
        var q = _questFunctions.GetCurrentQuest();
        string qw;
        if (q.CurrentQuest is QuestId)
        {
            var progressInfo = _questFunctions.GetQuestProgressInfo(q.CurrentQuest);
            qw = progressInfo != null ? progressInfo.ToString() : "QW: -";
        }
        else
        {
            return "No active quest";
        }
        return $"{q.CurrentQuest} → {q.Sequence} - {qw}";
    }

    private unsafe void DrawCopyButton(IGameObject target)
    {
        GameObject* gameObject = (GameObject*)target.Address;
        bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "左键点击：将目标位置复制为 JSON。\n右键点击：将目标位置复制为 C# 代码。");
        if (copy)
        {
            if (target.ObjectKind == ObjectKind.GatheringPoint)
            {
                ImGui.SetClipboardText($$"""
                                         "DataId": {{GameFunctions.GetBaseID(target)}},
                                                   "Position": {
                                                     "X": {{target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                                     "Y": {{target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                                     "Z": {{target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                                   }
                                         """);
            }
            else
            {
                string interactionType = gameObject->NamePlateIconId switch
                {
                    71201 or 71211 or 71221 or 71231 or 71341 or 71351 => "AcceptQuest",
                    71202 or 71212 or 71222 or 71232 or 71342 or 71352 => "AcceptQuest", // repeatable
                    71205 or 71215 or 71225 or 71235 or 71345 or 71355 => "CompleteQuest",
                    _ => "Interact",
                };
                ImGui.SetClipboardText($$"""
                                         "DataId": {{GameFunctions.GetBaseID(target)}},
                                                   "Position": {
                                                     "X": {{target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                                     "Y": {{target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                                     "Z": {{target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                                   },
                                                   "TerritoryId": {{_clientState.TerritoryType}},
                                                   "InteractionType": "{{interactionType}}"
                                         """);
            }
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            if (target.ObjectKind == ObjectKind.Aetheryte)
            {
                EAetheryteLocation location = (EAetheryteLocation)GameFunctions.GetBaseID(target);
                ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                    $"{{EAetheryteLocation.{location}, new({target.Position.X}f, {target.Position.Y}f, {target.Position.Z}f)}},"));
            }
            else
                ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                    $"new({target.Position.X}f, {target.Position.Y}f, {target.Position.Z}f)"));
        }
    }

    private void DrawCopyButton()
    {
        if (_objectTable[0] == null)
            return;

        bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "左键点击：将当前位置复制为 JSON。\n右键点击：将当前位置复制为 C# 代码。");
        if (copy)
        {
            ImGui.SetClipboardText($$"""
                                     "Position": {
                                                 "X": {{_objectTable[0]!.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                                 "Y": {{_objectTable[0]!.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                                 "Z": {{_objectTable[0]!.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                               },
                                               "TerritoryId": {{_clientState.TerritoryType}},
                                               "InteractionType": ""
                                     """);
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            Vector3 position = _objectTable[0]!.Position;
            ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                $"new({position.X}f, {position.Y}f, {position.Z}f)"));
        }
    }
}
