using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.Data;
using Questionable.External;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;
    private readonly IPluginLog _pluginLog;
    private readonly ICondition _condition;
    private readonly IChatGui _chatGui;
    private readonly AetheryteData _aetheryteData;
    private readonly LifestreamIpc _lifestreamIpc;
    private readonly TerritoryData _territoryData;
    private readonly Dictionary<ushort, Quest> _quests = new();

    public QuestController(DalamudPluginInterface pluginInterface, IDataManager dataManager, IClientState clientState,
        GameFunctions gameFunctions, MovementController movementController, IPluginLog pluginLog, ICondition condition,
        IChatGui chatGui, AetheryteData aetheryteData, LifestreamIpc lifestreamIpc)
    {
        _pluginInterface = pluginInterface;
        _dataManager = dataManager;
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _movementController = movementController;
        _pluginLog = pluginLog;
        _condition = condition;
        _chatGui = chatGui;
        _aetheryteData = aetheryteData;
        _lifestreamIpc = lifestreamIpc;
        _territoryData = new TerritoryData(dataManager);

        Reload();
        _gameFunctions.QuestController = this;
    }


    public QuestProgress? CurrentQuest { get; set; }
    public string? DebugState { get; private set; }
    public string? Comment { get; private set; }

    public void Reload()
    {
        _quests.Clear();

        CurrentQuest = null;
        DebugState = null;

#if false
        LoadFromEmbeddedResources();
#endif
        LoadFromDirectory(new DirectoryInfo(@"E:\ffxiv\Questionable\Questionable\QuestPaths"));
        LoadFromDirectory(_pluginInterface.ConfigDirectory);

        foreach (var (questId, quest) in _quests)
        {
            var questData =
                _dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Quest>()!.GetRow((uint)questId + 0x10000);
            if (questData == null)
                continue;

            quest.Name = questData.Name.ToString();
        }
    }

#if false
    private void LoadFromEmbeddedResources()
    {
        foreach (string resourceName in typeof(Questionable).Assembly.GetManifestResourceNames())
        {
            if (resourceName.EndsWith(".json"))
            {
                var (questId, name) = ExtractQuestDataFromName(resourceName);
                Quest quest = new Quest
                {
                    QuestId = questId,
                    Name = name,
                    Data = JsonSerializer.Deserialize<QuestData>(
                        typeof(Questionable).Assembly.GetManifestResourceStream(resourceName)!)!,
                };
                _quests[questId] = quest;
            }
        }
    }
#endif

    public bool IsKnownQuest(ushort questId) => _quests.ContainsKey(questId);

    private void LoadFromDirectory(DirectoryInfo configDirectory)
    {
        foreach (FileInfo fileInfo in configDirectory.GetFiles("*.json"))
        {
            try
            {
                using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                var (questId, name) = ExtractQuestDataFromName(fileInfo.Name);
                Quest quest = new Quest
                {
                    FilePath = fileInfo.FullName,
                    QuestId = questId,
                    Name = name,
                    Data = JsonSerializer.Deserialize<QuestData>(stream)!,
                };
                _quests[questId] = quest;
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Unable to load file {fileInfo.FullName}", e);
            }
        }

        foreach (DirectoryInfo childDirectory in configDirectory.GetDirectories())
            LoadFromDirectory(childDirectory);
    }

    private static (ushort QuestId, string Name) ExtractQuestDataFromName(string resourceName)
    {
        string name = resourceName.Substring(0, resourceName.Length - ".json".Length);
        name = name.Substring(name.LastIndexOf('.') + 1);

        string[] parts = name.Split('_', 2);
        return (ushort.Parse(parts[0], CultureInfo.InvariantCulture), parts[1]);
    }

    public void Update()
    {
        Comment = null;
        DebugState = null;

        (ushort currentQuestId, byte currentSequence) = _gameFunctions.GetCurrentQuest();
        if (currentQuestId == 0)
        {
            if (CurrentQuest != null)
                CurrentQuest = null;
        }
        else if (CurrentQuest == null || CurrentQuest.Quest.QuestId != currentQuestId)
        {
            if (_quests.TryGetValue(currentQuestId, out var quest))
                CurrentQuest = new QuestProgress(quest, currentSequence, 0);
            else if (CurrentQuest != null)
                CurrentQuest = null;
        }

        if (CurrentQuest == null)
        {
            DebugState = "No quest active";
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
            return;
        }

        if (CurrentQuest.Step == 255)
        {
            DebugState = "Step completed";
            return;
        }

        if (CurrentQuest.Step >= sequence.Steps.Count)
        {
            DebugState = "Step not found";
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
        if (seq == null || step == null)
            return;

        Debug.Assert(CurrentQuest != null, nameof(CurrentQuest) + " != null");
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

    public unsafe void ExecuteNextStep()
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (seq == null || step == null)
            return;

        Debug.Assert(CurrentQuest != null, nameof(CurrentQuest) + " != null");
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
                    skipTeleport = true;
                }
            }

            if (skipTeleport)
            {
                CurrentQuest = CurrentQuest with
                {
                    StepProgress = CurrentQuest.StepProgress with { AetheryteShortcutUsed = true }
                };
            }
            else
            {
                if (step.AetheryteShortcut != null)
                {
                    if (!_gameFunctions.IsAetheryteUnlocked(step.AetheryteShortcut.Value))
                        _chatGui.Print($"[Questionable] Aetheryte {step.AetheryteShortcut.Value} is not unlocked.");
                    else if (_gameFunctions.TeleportAetheryte(step.AetheryteShortcut.Value))
                        CurrentQuest = CurrentQuest with
                        {
                            StepProgress = CurrentQuest.StepProgress with { AetheryteShortcutUsed = true }
                        };
                    else
                        _chatGui.Print("[Questionable] Unable to teleport to aetheryte.");
                }
                else
                    _chatGui.Print("[Questionable] No aetheryte for teleport set.");

                return;
            }
        }

        if (!CurrentQuest.StepProgress.AethernetShortcutUsed)
        {
            if (step.AethernetShortcut != null &&
                _gameFunctions.IsAetheryteUnlocked(step.AethernetShortcut.From) &&
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
                        _lifestreamIpc.Teleport(to);
                        CurrentQuest = CurrentQuest with
                        {
                            StepProgress = CurrentQuest.StepProgress with { AethernetShortcutUsed = true }
                        };
                    }
                    else
                        _movementController.NavigateTo(EMovementType.Quest, null, _aetheryteData.Locations[from], false,
                            6.9f);

                    return;
                }
            }
        }

        if (step.TargetTerritoryId == _clientState.TerritoryType)
        {
            _pluginLog.Information("Skipping any movement");
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
                if (!_condition[ConditionFlag.Mounted] && _territoryData.CanUseMount(_clientState.TerritoryType))
                {
                    if (ActionManager.Instance()->GetActionStatus(ActionType.Mount, 71) == 0)
                        ActionManager.Instance()->UseAction(ActionType.Mount, 71);
                    return;
                }
            }
            else if (step.Mount == false)
            {
                if (_condition[ConditionFlag.Mounted])
                {
                    _gameFunctions.Unmount();
                    return;
                }
            }

            if (!step.DisableNavmesh)
            {
                if (step.Mount != false && actualDistance > 30f && !_condition[ConditionFlag.Mounted] &&
                    _territoryData.CanUseMount(_clientState.TerritoryType))
                {
                    if (ActionManager.Instance()->GetActionStatus(ActionType.Mount, 71) == 0)
                        ActionManager.Instance()->UseAction(ActionType.Mount, 71);
                    return;
                }

                if (actualDistance > distance)
                {
                    _movementController.NavigateTo(EMovementType.Quest, step.DataId, step.Position.Value,
                        step.Fly && _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType), distance);
                    return;
                }
            }
            else
            {
                if (actualDistance > distance)
                {
                    _movementController.NavigateTo(EMovementType.Quest, step.DataId, [step.Position.Value],
                        step.Fly && _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType), distance);
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
                return;
            }
        }

        switch (step.InteractionType)
        {
            case EInteractionType.Interact:
                if (step.DataId != null)
                {
                    GameObject? gameObject = _gameFunctions.FindObjectByDataId(step.DataId.Value);
                    if (gameObject == null)
                        return;

                    if (!gameObject.IsTargetable && _condition[ConditionFlag.Mounted])
                    {
                        _gameFunctions.Unmount();
                        return;
                    }

                    _gameFunctions.InteractWith(step.DataId.Value);
                    IncreaseStepCount();
                }

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
                    _pluginLog.Information(
                        $"{step.AetherCurrentId} → {_gameFunctions.IsAetherCurrentUnlocked(step.AetherCurrentId.GetValueOrDefault())}");
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
                    if (step.DataId != null && step.EnemySpawnType == EEnemySpawnType.AfterInteraction)
                        _gameFunctions.InteractWith(step.DataId.Value);

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

                if (!string.IsNullOrEmpty(step.ChatMessage))
                {
                    _gameFunctions.ExecuteCommand($"/say {step.ChatMessage}");
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

            default:
                _pluginLog.Warning($"Action '{step.InteractionType}' is not implemented");
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
        bool AethernetShortcutUsed = false);
}
