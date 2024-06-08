using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;
using Questionable.Model.V1.Converter;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;
    private readonly ILogger<QuestController> _logger;
    private readonly ICondition _condition;
    private readonly IChatGui _chatGui;
    private readonly IFramework _framework;
    private readonly AetheryteData _aetheryteData;
    private readonly LifestreamIpc _lifestreamIpc;
    private readonly TerritoryData _territoryData;
    private readonly QuestRegistry _questRegistry;

    public QuestController(IClientState clientState, GameFunctions gameFunctions, MovementController movementController,
        ILogger<QuestController> logger, ICondition condition, IChatGui chatGui, IFramework framework,
        AetheryteData aetheryteData, LifestreamIpc lifestreamIpc, TerritoryData territoryData,
        QuestRegistry questRegistry)
    {
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _movementController = movementController;
        _logger = logger;
        _condition = condition;
        _chatGui = chatGui;
        _framework = framework;
        _aetheryteData = aetheryteData;
        _lifestreamIpc = lifestreamIpc;
        _territoryData = territoryData;
        _questRegistry = questRegistry;
    }


    public QuestProgress? CurrentQuest { get; set; }
    public string? DebugState { get; private set; }
    public string? Comment { get; private set; }

    public void Reload()
    {
        CurrentQuest = null;
        DebugState = null;

        _questRegistry.Reload();
    }

    public void Update()
    {
        DebugState = null;

        (ushort currentQuestId, byte currentSequence) = _gameFunctions.GetCurrentQuest();
        if (currentQuestId == 0)
        {
            if (CurrentQuest != null)
                CurrentQuest = null;
        }
        else if (CurrentQuest == null || CurrentQuest.Quest.QuestId != currentQuestId)
        {
            if (_questRegistry.TryGetQuest(currentQuestId, out var quest))
                CurrentQuest = new QuestProgress(quest, currentSequence, 0);
            else if (CurrentQuest != null)
                CurrentQuest = null;
        }

        if (CurrentQuest == null)
        {
            DebugState = "No quest active";
            Comment = null;
            return;
        }

        if (_condition[ConditionFlag.Occupied] || _condition[ConditionFlag.Occupied30] ||
            _condition[ConditionFlag.Occupied33] || _condition[ConditionFlag.Occupied38] ||
            _condition[ConditionFlag.Occupied39] || _condition[ConditionFlag.OccupiedInEvent] ||
            _condition[ConditionFlag.OccupiedInQuestEvent] || _condition[ConditionFlag.OccupiedInCutSceneEvent] ||
            _condition[ConditionFlag.Casting] || _condition[ConditionFlag.Unknown57])
        {
            DebugState = "Occupied";
            return;
        }

        if (!_movementController.IsNavmeshReady)
        {
            DebugState = "Navmesh not ready";
            return;
        }
        else if (_movementController.IsPathfinding || _movementController.IsPathRunning)
        {
            DebugState = "Path is running";
            return;
        }

        if (CurrentQuest.Sequence != currentSequence)
            CurrentQuest = CurrentQuest with { Sequence = currentSequence, Step = 0 };

        var q = CurrentQuest.Quest;
        var sequence = q.FindSequence(CurrentQuest.Sequence);
        if (sequence == null)
        {
            DebugState = "Sequence not found";
            Comment = null;
            return;
        }

        if (CurrentQuest.Step == 255)
        {
            DebugState = "Step completed";
            Comment = null;
            return;
        }

        if (CurrentQuest.Step >= sequence.Steps.Count)
        {
            DebugState = "Step not found";
            Comment = null;
            return;
        }

        var step = sequence.Steps[CurrentQuest.Step];
        DebugState = null;
        Comment = step.Comment ?? sequence.Comment ?? q.Data.Comment;
    }

    public (QuestSequence? Sequence, QuestStep? Step) GetNextStep()
    {
        if (CurrentQuest == null)
            return (null, null);

        var q = CurrentQuest.Quest;
        var seq = q.FindSequence(CurrentQuest.Sequence);
        if (seq == null)
            return (null, null);

        if (CurrentQuest.Step >= seq.Steps.Count)
            return (null, null);

        return (seq, seq.Steps[CurrentQuest.Step]);
    }

    public void IncreaseStepCount()
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Unable to retrieve next quest step, not increasing step count");
            return;
        }

        if (CurrentQuest.Step + 1 < seq.Steps.Count)
        {
            CurrentQuest = CurrentQuest with
            {
                Step = CurrentQuest.Step + 1,
                StepProgress = new()
            };
        }
        else
        {
            CurrentQuest = CurrentQuest with
            {
                Step = 255,
                StepProgress = new()
            };
        }
    }

    public void IncreaseDialogueChoicesSelected()
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Unable to retrieve next quest step, not increasing dialogue choice count");
            return;
        }

        CurrentQuest = CurrentQuest with
        {
            StepProgress = CurrentQuest.StepProgress with
            {
                DialogueChoicesSelected = CurrentQuest.StepProgress.DialogueChoicesSelected + 1
            }
        };

        if (CurrentQuest.StepProgress.DialogueChoicesSelected >= step.DialogueChoices.Count)
            IncreaseStepCount();
    }

    public unsafe void ExecuteNextStep()
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Could not retrieve next quest step, not doing anything");
            return;
        }

        if (step.Disabled)
        {
            _logger.LogInformation("Skipping step, as it is disabled");
            IncreaseStepCount();
            return;
        }

        if (!CurrentQuest.StepProgress.AetheryteShortcutUsed && step.AetheryteShortcut != null)
        {
            bool skipTeleport = false;
            ushort territoryType = _clientState.TerritoryType;
            if (step.TerritoryId == territoryType)
            {
                Vector3 pos = _clientState.LocalPlayer!.Position;
                if (_aetheryteData.CalculateDistance(pos, territoryType, step.AetheryteShortcut.Value) < 11 ||
                    (step.AethernetShortcut != null &&
                     (_aetheryteData.CalculateDistance(pos, territoryType, step.AethernetShortcut.From) < 20 ||
                      _aetheryteData.CalculateDistance(pos, territoryType, step.AethernetShortcut.To) < 20)))
                {
                    _logger.LogInformation("Skipping aetheryte teleport");
                    skipTeleport = true;
                }
            }

            if (skipTeleport)
            {
                _logger.LogInformation("Marking aetheryte shortcut as used");
                CurrentQuest = CurrentQuest with
                {
                    StepProgress = CurrentQuest.StepProgress with { AetheryteShortcutUsed = true }
                };
            }
            else
            {
                if (!_gameFunctions.IsAetheryteUnlocked(step.AetheryteShortcut.Value))
                {
                    _logger.LogError("Aetheryte {Aetheryte} is not unlocked.", step.AetheryteShortcut.Value);
                    _chatGui.Print($"[Questionable] Aetheryte {step.AetheryteShortcut.Value} is not unlocked.");
                }
                else if (_gameFunctions.TeleportAetheryte(step.AetheryteShortcut.Value))
                {
                    _logger.LogInformation("Travelling via aetheryte...");
                    CurrentQuest = CurrentQuest with
                    {
                        StepProgress = CurrentQuest.StepProgress with { AetheryteShortcutUsed = true }
                    };
                }
                else
                {
                    _logger.LogWarning("Unable to teleport to aetheryte");
                    _chatGui.Print("[Questionable] Unable to teleport to aetheryte.");
                }

                return;
            }
        }

        if (!step.SkipIf.Contains(ESkipCondition.Never))
        {
            _logger.LogInformation("Checking skip conditions; {ConfiguredConditions}", string.Join(",", step.SkipIf));

            if (step.SkipIf.Contains(ESkipCondition.FlyingUnlocked) &&
                _gameFunctions.IsFlyingUnlocked(step.TerritoryId))
            {
                _logger.LogInformation("Skipping step, as flying is unlocked");
                IncreaseStepCount();
                return;
            }

            if (step.SkipIf.Contains(ESkipCondition.FlyingLocked) &&
                !_gameFunctions.IsFlyingUnlocked(step.TerritoryId))
            {
                _logger.LogInformation("Skipping step, as flying is locked");
                IncreaseStepCount();
                return;
            }

            if (step is
                {
                    DataId: not null,
                    InteractionType: EInteractionType.AttuneAetheryte or EInteractionType.AttuneAethernetShard
                } &&
                _gameFunctions.IsAetheryteUnlocked((EAetheryteLocation)step.DataId.Value))
            {
                _logger.LogInformation("Skipping step, as aetheryte/aethernet shard is unlocked");
                IncreaseStepCount();
                return;
            }

            if (step is { DataId: not null, InteractionType: EInteractionType.AttuneAetherCurrent } &&
                _gameFunctions.IsAetherCurrentUnlocked(step.DataId.Value))
            {
                _logger.LogInformation("Skipping step, as current is unlocked");
                IncreaseStepCount();
                return;
            }

            QuestWork? questWork = _gameFunctions.GetQuestEx(CurrentQuest.Quest.QuestId);
            if (questWork != null && step.MatchesQuestVariables(questWork.Value))
            {
                _logger.LogInformation("Skipping step, as quest variables match");
                IncreaseStepCount();
                return;
            }
        }

        if (!CurrentQuest.StepProgress.AethernetShortcutUsed && step.AethernetShortcut != null)
        {
            if (_gameFunctions.IsAetheryteUnlocked(step.AethernetShortcut.From) &&
                _gameFunctions.IsAetheryteUnlocked(step.AethernetShortcut.To))
            {
                EAetheryteLocation from = step.AethernetShortcut.From;
                EAetheryteLocation to = step.AethernetShortcut.To;
                ushort territoryType = _clientState.TerritoryType;
                Vector3 playerPosition = _clientState.LocalPlayer!.Position;

                // closer to the source
                if (_aetheryteData.CalculateDistance(playerPosition, territoryType, from) <
                    _aetheryteData.CalculateDistance(playerPosition, territoryType, to))
                {
                    if (_aetheryteData.CalculateDistance(playerPosition, territoryType, from) < 11)
                    {
                        _logger.LogInformation("Using lifestream to teleport to {Destination}", to);
                        _lifestreamIpc.Teleport(to);
                        CurrentQuest = CurrentQuest with
                        {
                            StepProgress = CurrentQuest.StepProgress with { AethernetShortcutUsed = true }
                        };
                    }
                    else
                    {
                        _logger.LogInformation("Moving to aethernet shortcut");
                        _movementController.NavigateTo(EMovementType.Quest, (uint)from, _aetheryteData.Locations[from],
                            false, true,
                            AetheryteConverter.IsLargeAetheryte(from) ? 10.9f : 6.9f);
                    }

                    return;
                }
            }
            else
                _logger.LogWarning(
                    "Aethernet shortcut not unlocked (from: {FromAetheryte}, to: {ToAetheryte}), walking manually",
                    step.AethernetShortcut.From, step.AethernetShortcut.To);
        }

        if (step.TargetTerritoryId.HasValue && step.TerritoryId != step.TargetTerritoryId &&
            step.TargetTerritoryId == _clientState.TerritoryType)
        {
            // we assume whatever e.g. interaction, walkto etc. we have will trigger the zone transition
            _logger.LogInformation("Zone transition, skipping rest of step");
            IncreaseStepCount();
            return;
        }

        if (step.InteractionType == EInteractionType.Jump && step.JumpDestination != null &&
            (_clientState.LocalPlayer!.Position - step.JumpDestination.Position).Length() <=
            (step.JumpDestination.StopDistance ?? 1f))
        {
            _logger.LogInformation("We're at the jump destination, skipping movement");
        }
        else if (step.Position != null)
        {
            float distance;
            if (step.InteractionType == EInteractionType.WalkTo)
                distance = step.StopDistance ?? 0.25f;
            else
                distance = step.StopDistance ?? MovementController.DefaultStopDistance;

            var position = _clientState.LocalPlayer?.Position ?? new Vector3();
            float actualDistance = (position - step.Position.Value).Length();

            if (step.Mount == true && !_gameFunctions.HasStatusPreventingSprintOrMount())
            {
                _logger.LogInformation("Step explicitly wants a mount, trying to mount...");
                if (!_condition[ConditionFlag.Mounted] && !_condition[ConditionFlag.InCombat] &&
                    _territoryData.CanUseMount(_clientState.TerritoryType))
                {
                    _gameFunctions.Mount();
                    return;
                }
            }
            else if (step.Mount == false)
            {
                _logger.LogInformation("Step explicitly wants no mount, trying to unmount...");
                if (_condition[ConditionFlag.Mounted])
                {
                    _gameFunctions.Unmount();
                    return;
                }
            }

            if (!step.DisableNavmesh)
            {
                if (step.Mount != false && actualDistance > 30f && !_condition[ConditionFlag.Mounted] &&
                    !_condition[ConditionFlag.InCombat] && _territoryData.CanUseMount(_clientState.TerritoryType) &&
                    !_gameFunctions.HasStatusPreventingSprintOrMount())
                {
                    _gameFunctions.Mount();
                    return;
                }

                if (actualDistance > distance)
                {
                    _movementController.NavigateTo(EMovementType.Quest, step.DataId, step.Position.Value,
                        fly: step.Fly == true && _gameFunctions.IsFlyingUnlockedInCurrentZone(),
                        sprint: step.Sprint != false,
                        stopDistance: distance);
                    return;
                }
            }
            else
            {
                // navmesh won't move close enough
                if (actualDistance > distance)
                {
                    _movementController.NavigateTo(EMovementType.Quest, step.DataId, [step.Position.Value],
                        fly: step.Fly == true && _gameFunctions.IsFlyingUnlockedInCurrentZone(),
                        sprint: step.Sprint != false,
                        stopDistance: distance);
                    return;
                }
            }
        }
        else if (step.DataId != null && step.StopDistance != null)
        {
            GameObject? gameObject = _gameFunctions.FindObjectByDataId(step.DataId.Value);
            if (gameObject == null ||
                (gameObject.Position - _clientState.LocalPlayer!.Position).Length() > step.StopDistance)
            {
                _logger.LogWarning("Object not found or too far away, no position so we can't move");
                return;
            }
        }

        _logger.LogInformation("Running logic for {InteractionType}", step.InteractionType);
        switch (step.InteractionType)
        {
            case EInteractionType.Interact:
                if (step.DataId != null)
                {
                    GameObject? gameObject = _gameFunctions.FindObjectByDataId(step.DataId.Value);
                    if (gameObject == null)
                    {
                        _logger.LogWarning("No game object with dataId {DataId}", step.DataId);
                        return;
                    }

                    if (!gameObject.IsTargetable && _condition[ConditionFlag.Mounted])
                    {
                        _gameFunctions.Unmount();
                        return;
                    }

                    _gameFunctions.InteractWith(step.DataId.Value);

                    // if we have any dialogue, that is handled in GameUiController
                    if (step.DialogueChoices.Count == 0)
                        IncreaseStepCount();
                }
                else
                    _logger.LogWarning("Not interacting on current step, DataId is null");

                break;

            case EInteractionType.AttuneAethernetShard:
                if (step.DataId != null)
                {
                    if (!_gameFunctions.IsAetheryteUnlocked((EAetheryteLocation)step.DataId.Value))
                        _gameFunctions.InteractWith(step.DataId.Value);

                    IncreaseStepCount();
                }

                break;

            case EInteractionType.AttuneAetheryte:
                if (step.DataId != null)
                {
                    if (!_gameFunctions.IsAetheryteUnlocked((EAetheryteLocation)step.DataId.Value))
                        _gameFunctions.InteractWith(step.DataId.Value);

                    IncreaseStepCount();
                }

                break;

            case EInteractionType.AttuneAetherCurrent:
                if (step.DataId != null)
                {
                    _logger.LogInformation(
                        "{AetherCurrentId} is unlocked = {Unlocked}",
                        step.AetherCurrentId,
                        _gameFunctions.IsAetherCurrentUnlocked(step.AetherCurrentId.GetValueOrDefault()));
                    if (step.AetherCurrentId == null ||
                        !_gameFunctions.IsAetherCurrentUnlocked(step.AetherCurrentId.Value))
                        _gameFunctions.InteractWith(step.DataId.Value);

                    IncreaseStepCount();
                }

                break;

            case EInteractionType.WalkTo:
                IncreaseStepCount();
                break;

            case EInteractionType.UseItem:
                if (_gameFunctions.Unmount())
                    return;

                if (step is { DataId: not null, ItemId: not null, GroundTarget: true })
                {
                    _gameFunctions.UseItemOnGround(step.DataId.Value, step.ItemId.Value);
                    IncreaseStepCount();
                }
                else if (step is { DataId: not null, ItemId: not null })
                {
                    _gameFunctions.UseItem(step.DataId.Value, step.ItemId.Value);
                    IncreaseStepCount();
                }
                else if (step.ItemId != null)
                {
                    _gameFunctions.UseItem(step.ItemId.Value);
                    IncreaseStepCount();
                }

                break;

            case EInteractionType.Combat:
                if (_gameFunctions.Unmount())
                    return;

                if (step.EnemySpawnType != null)
                {
                    if (step is { DataId: not null, EnemySpawnType: EEnemySpawnType.AfterInteraction })
                        _gameFunctions.InteractWith(step.DataId.Value);

                    if (step is { DataId: not null, ItemId: not null, EnemySpawnType: EEnemySpawnType.AfterItemUse })
                        _gameFunctions.UseItem(step.DataId.Value, step.ItemId.Value);

                    // next sequence should trigger automatically
                    IncreaseStepCount();
                }

                break;

            case EInteractionType.Emote:
                if (step is { DataId: not null, Emote: not null })
                {
                    _gameFunctions.UseEmote(step.DataId.Value, step.Emote.Value);
                    IncreaseStepCount();
                }
                else if (step.Emote != null)
                {
                    _gameFunctions.UseEmote(step.Emote.Value);
                    IncreaseStepCount();
                }

                break;

            case EInteractionType.Say:
                if (_condition[ConditionFlag.Mounted])
                {
                    _gameFunctions.Unmount();
                    return;
                }

                if (step.ChatMessage != null)
                {
                    string? excelString = _gameFunctions.GetDialogueText(CurrentQuest.Quest,
                        step.ChatMessage.ExcelSheet,
                        step.ChatMessage.Key);
                    if (excelString == null)
                        return;

                    _gameFunctions.ExecuteCommand($"/say {excelString}");
                    IncreaseStepCount();
                }

                break;

            case EInteractionType.WaitForObjectAtPosition:
                if (step is { DataId: not null, Position: not null } &&
                    !_gameFunctions.IsObjectAtPosition(step.DataId.Value, step.Position.Value))
                {
                    return;
                }

                IncreaseStepCount();
                break;

            case EInteractionType.WaitForManualProgress:
                // something needs to be done manually, the next sequence will be picked up automatically
                break;

            case EInteractionType.Duty:
                if (step.ContentFinderConditionId != null)
                    _gameFunctions.OpenDutyFinder(step.ContentFinderConditionId.Value);

                break;

            case EInteractionType.SinglePlayerDuty:
                // TODO: Disable YesAlready, interact with NPC to open dialog, restore YesAlready
                // TODO: also implement check for territory blacklist
                break;

            case EInteractionType.Jump:
                if (step.JumpDestination != null && !_condition[ConditionFlag.Jumping])
                {
                    float stopDistance = step.JumpDestination.StopDistance ?? 1f;
                    if ((_clientState.LocalPlayer!.Position - step.JumpDestination.Position).Length() <= stopDistance)
                        IncreaseStepCount();
                    else
                    {
                        _movementController.NavigateTo(EMovementType.Quest, step.DataId,
                            [step.JumpDestination.Position], false, false,
                            step.JumpDestination.StopDistance ?? stopDistance);
                        _framework.RunOnTick(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2),
                            TimeSpan.FromSeconds(step.JumpDestination.DelaySeconds ?? 0.5f));
                    }
                }

                break;

            case EInteractionType.ShouldBeAJump:
            case EInteractionType.Instruction:
                // Need to manually forward
                break;

            default:
                _logger.LogWarning("Action '{InteractionType}' is not implemented", step.InteractionType);
                break;
        }
    }

    public sealed record QuestProgress(
        Quest Quest,
        byte Sequence,
        int Step,
        StepProgress StepProgress)
    {
        public QuestProgress(Quest quest, byte sequence, int step)
            : this(quest, sequence, step, new StepProgress())
        {
        }
    }

    public sealed record StepProgress(
        bool AetheryteShortcutUsed = false,
        bool AethernetShortcutUsed = false,
        int DialogueChoicesSelected = 0);
}
