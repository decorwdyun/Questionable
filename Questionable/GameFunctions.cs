using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Lumina.Excel.CustomSheets;
using Lumina.Excel.GeneratedSheets2;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using Action = Lumina.Excel.GeneratedSheets2.Action;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ContentFinderCondition = Lumina.Excel.GeneratedSheets.ContentFinderCondition;
using ContentTalk = Lumina.Excel.GeneratedSheets.ContentTalk;
using EventPathMove = Lumina.Excel.GeneratedSheets.EventPathMove;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using Quest = Questionable.Model.Quest;
using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

namespace Questionable;

internal sealed unsafe class GameFunctions
{
    private readonly ReadOnlyDictionary<ushort, byte> _territoryToAetherCurrentCompFlgSet;
    private readonly ReadOnlyDictionary<uint, ushort> _contentFinderConditionToContentId;

    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly IGameGui _gameGui;
    private readonly Configuration _configuration;
    private readonly ILogger<GameFunctions> _logger;

    public GameFunctions(IDataManager dataManager,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        IClientState clientState,
        QuestRegistry questRegistry,
        QuestData questData,
        IGameGui gameGui,
        Configuration configuration,
        ILogger<GameFunctions> logger)
    {
        _dataManager = dataManager;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _condition = condition;
        _clientState = clientState;
        _questRegistry = questRegistry;
        _questData = questData;
        _gameGui = gameGui;
        _configuration = configuration;
        _logger = logger;

        _territoryToAetherCurrentCompFlgSet = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.Unknown32 > 0)
            .ToDictionary(x => (ushort)x.RowId, x => x.Unknown32)
            .AsReadOnly();
        _contentFinderConditionToContentId = dataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(x => x.RowId > 0 && x.Content > 0)
            .ToDictionary(x => x.RowId, x => x.Content)
            .AsReadOnly();
    }

    public DateTime ReturnRequestedAt { get; set; } = DateTime.MinValue;

    public (ElementId? CurrentQuest, byte Sequence) GetCurrentQuest()
    {
        var (currentQuest, sequence) = GetCurrentQuestInternal();
        PlayerState* playerState = PlayerState.Instance();

        if (currentQuest == null || currentQuest.Value == 0)
        {
            if (_clientState.TerritoryType == 181) // Starting in Limsa
                return (new QuestId(107), 0);
            if (_clientState.TerritoryType == 182) // Starting in Ul'dah
                return (new QuestId(594), 0);
            if (_clientState.TerritoryType == 183) // Starting in Gridania
                return (new QuestId(39), 0);
            return default;
        }
        else if (currentQuest.Value == 681)
        {
            // if we have already picked up the GC quest, just return the progress for it
            if (IsQuestAccepted(currentQuest) || IsQuestComplete(currentQuest))
                return (currentQuest, sequence);

            // The company you keep...
            return _configuration.General.GrandCompany switch
            {
                GrandCompany.TwinAdder => (new QuestId(680), 0),
                GrandCompany.Maelstrom => (new QuestId(681), 0),
                _ => default
            };
        }
        else if (currentQuest.Value == 3856 && !playerState->IsMountUnlocked(1)) // we come in peace
        {
            ushort chocoboQuest = (GrandCompany)playerState->GrandCompany switch
            {
                GrandCompany.TwinAdder => 700,
                GrandCompany.Maelstrom => 701,
                _ => 0
            };

            if (chocoboQuest != 0 && !QuestManager.IsQuestComplete(chocoboQuest))
                return (new QuestId(chocoboQuest), QuestManager.GetQuestSequence(chocoboQuest));
        }
        else if (currentQuest.Value == 801)
        {
            // skeletons in her closet, finish 'broadening horizons' to unlock the white wolf gate
            QuestId broadeningHorizons = new QuestId(802);
            if (IsQuestAccepted(broadeningHorizons))
                return (broadeningHorizons, QuestManager.GetQuestSequence(broadeningHorizons.Value));
        }

        return (currentQuest, sequence);
    }

    public (ElementId? CurrentQuest, byte Sequence) GetCurrentQuestInternal()
    {
        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            // always prioritize accepting MSQ quests, to make sure we don't turn in one MSQ quest and then go off to do
            // side quests until the end of time.
            var msqQuest = GetMainScenarioQuest(questManager);
            if (msqQuest.CurrentQuest is { Value: not 0 } && _questRegistry.IsKnownQuest(msqQuest.CurrentQuest))
                return msqQuest;

            // Use the quests in the same order as they're shown in the to-do list, e.g. if the MSQ is the first item,
            // do the MSQ; if a side quest is the first item do that side quest.
            //
            // If no quests are marked as 'priority', accepting a new quest adds it to the top of the list.
            for (int i = questManager->TrackedQuests.Length - 1; i >= 0; --i)
            {
                ElementId currentQuest;
                var trackedQuest = questManager->TrackedQuests[i];
                switch (trackedQuest.QuestType)
                {
                    default:
                        continue;

                    case 1: // normal quest
                        currentQuest = new QuestId(questManager->NormalQuests[trackedQuest.Index].QuestId);
                        break;
                }

                if (_questRegistry.IsKnownQuest(currentQuest))
                    return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
            }

            // if we know no quest of those currently in the to-do list, just do MSQ
            return msqQuest;
        }

        return default;
    }

    private (QuestId? CurrentQuest, byte Sequence) GetMainScenarioQuest(QuestManager* questManager)
    {
        if (QuestManager.IsQuestComplete(3759)) // Memories Rekindled
        {
            AgentInterface* questRedoHud = AgentModule.Instance()->GetAgentByInternalId(AgentId.QuestRedoHud);
            if (questRedoHud != null && questRedoHud->IsAgentActive())
            {
                // there's surely better ways to check this, but the one in the OOB Plugin was even less reliable
                if (_gameGui.TryGetAddonByName<AtkUnitBase>("QuestRedoHud", out var addon) &&
                    addon->AtkValuesCount == 4 &&
                    // 0 seems to be active,
                    // 1 seems to be paused,
                    // 2 is unknown, but it happens e.g. before the quest 'Alzadaal's Legacy'
                    // 3 seems to be having /ng+ open while active,
                    // 4 seems to be when (a) suspending the chapter, or (b) having turned in a quest
                    addon->AtkValues[0].UInt is 0 or 2 or 3 or 4)
                {
                    // redoHud+44 is chapter
                    // redoHud+46 is quest
                    ushort questId = MemoryHelper.Read<ushort>((nint)questRedoHud + 46);
                    return (new QuestId(questId), QuestManager.GetQuestSequence(questId));
                }
            }
        }

        var scenarioTree = AgentScenarioTree.Instance();
        if (scenarioTree == null)
            return default;

        if (scenarioTree->Data == null)
            return default;

        QuestId currentQuest = new QuestId(scenarioTree->Data->CurrentScenarioQuest);
        if (currentQuest.Value == 0)
            return default;

        // if the MSQ is hidden, we generally ignore it
        if (IsQuestAccepted(currentQuest) && questManager->GetQuestById(currentQuest.Value)->IsHidden)
            return default;

        // it can sometimes happen (although this isn't reliably reproducible) that the quest returned here
        // is one you've just completed.
        if (!IsReadyToAcceptQuest(currentQuest))
            return default;

        // if we're not at a high enough level to continue, we also ignore it
        var currentLevel = _clientState.LocalPlayer?.Level ?? 0;
        if (currentLevel != 0 &&
            _questRegistry.TryGetQuest(currentQuest, out Quest? quest)
            && quest.Info.Level > currentLevel)
            return default;

        return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
    }

    public QuestWork? GetQuestEx(QuestId questId)
    {
        QuestWork* questWork = QuestManager.Instance()->GetQuestById(questId.Value);
        return questWork != null ? *questWork : null;
    }

    public bool IsReadyToAcceptQuest(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsReadyToAcceptQuest(questId);
        return false;
    }

    public bool IsReadyToAcceptQuest(QuestId questId)
    {
        _questRegistry.TryGetQuest(questId, out var quest);
        if (quest is { Info.IsRepeatable: true })
        {
            if (IsQuestAccepted(questId))
                return false;
        }
        else
        {
            if (IsQuestAcceptedOrComplete(questId))
                return false;
        }

        if (IsQuestLocked(questId))
            return false;

        // if we're not at a high enough level to continue, we also ignore it
        var currentLevel = _clientState.LocalPlayer?.Level ?? 0;
        if (currentLevel != 0 && quest != null && quest.Info.Level > currentLevel)
            return false;

        return true;
    }

    public bool IsQuestAcceptedOrComplete(ElementId questElementId)
    {
        return IsQuestComplete(questElementId) || IsQuestAccepted(questElementId);
    }

    public bool IsQuestAccepted(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestAccepted(questId);
        return false;
    }

    public bool IsQuestAccepted(QuestId questId)
    {
        QuestManager* questManager = QuestManager.Instance();
        return questManager->IsQuestAccepted(questId.Value);
    }

    public bool IsQuestComplete(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestComplete(questId);
        return false;
    }

    [SuppressMessage("Performance", "CA1822")]
    public bool IsQuestComplete(QuestId questId)
    {
        return QuestManager.IsQuestComplete(questId.Value);
    }

    public bool IsQuestLocked(ElementId elementId, ElementId? extraCompletedQuest = null)
    {
        if (elementId is QuestId questId)
            return IsQuestLocked(questId, extraCompletedQuest);
        return false;
    }

    public bool IsQuestLocked(QuestId questId, ElementId? extraCompletedQuest = null)
    {
        var questInfo = _questData.GetQuestInfo(questId);
        if (questInfo.QuestLocks.Count > 0)
        {
            var completedQuests = questInfo.QuestLocks.Count(x => IsQuestComplete(x) || x.Equals(extraCompletedQuest));
            if (questInfo.QuestLockJoin == QuestInfo.QuestJoin.All && questInfo.QuestLocks.Count == completedQuests)
                return true;
            else if (questInfo.QuestLockJoin == QuestInfo.QuestJoin.AtLeastOne && completedQuests > 0)
                return true;
        }

        if (questInfo.GrandCompany != GrandCompany.None && questInfo.GrandCompany != GetGrandCompany())
            return true;

        return !HasCompletedPreviousQuests(questInfo, extraCompletedQuest) || !HasCompletedPreviousInstances(questInfo);
    }

    private bool HasCompletedPreviousQuests(QuestInfo questInfo, ElementId? extraCompletedQuest)
    {
        if (questInfo.PreviousQuests.Count == 0)
            return true;

        var completedQuests = questInfo.PreviousQuests.Count(x => IsQuestComplete(x) || x.Equals(extraCompletedQuest));
        if (questInfo.PreviousQuestJoin == QuestInfo.QuestJoin.All &&
            questInfo.PreviousQuests.Count == completedQuests)
            return true;
        else if (questInfo.PreviousQuestJoin == QuestInfo.QuestJoin.AtLeastOne && completedQuests > 0)
            return true;
        else
            return false;
    }

    private static bool HasCompletedPreviousInstances(QuestInfo questInfo)
    {
        if (questInfo.PreviousInstanceContent.Count == 0)
            return true;

        var completedInstances = questInfo.PreviousInstanceContent.Count(x => UIState.IsInstanceContentCompleted(x));
        if (questInfo.PreviousInstanceContentJoin == QuestInfo.QuestJoin.All &&
            questInfo.PreviousInstanceContent.Count == completedInstances)
            return true;
        else if (questInfo.PreviousInstanceContentJoin == QuestInfo.QuestJoin.AtLeastOne && completedInstances > 0)
            return true;
        else
            return false;
    }

    public bool IsAetheryteUnlocked(uint aetheryteId, out byte subIndex)
    {
        subIndex = 0;

        var uiState = UIState.Instance();
        return uiState != null && uiState->IsAetheryteUnlocked(aetheryteId);
    }

    public bool IsAetheryteUnlocked(EAetheryteLocation aetheryteLocation)
        => IsAetheryteUnlocked((uint)aetheryteLocation, out _);

    public bool CanTeleport(EAetheryteLocation aetheryteLocation)
    {
        if ((ushort)aetheryteLocation == PlayerState.Instance()->HomeAetheryteId &&
            ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 8) == 0)
            return true;

        return ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0;
    }

    public bool TeleportAetheryte(uint aetheryteId)
    {
        _logger.LogDebug("Attempting to teleport to aetheryte {AetheryteId}", aetheryteId);
        if (IsAetheryteUnlocked(aetheryteId, out var subIndex))
        {
            if (aetheryteId == PlayerState.Instance()->HomeAetheryteId &&
                ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 8) == 0)
            {
                ReturnRequestedAt = DateTime.Now;
                if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8))
                {
                    _logger.LogInformation("Using 'return' for home aetheryte");
                    return true;
                }
            }

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0)
            {
                // fallback if return isn't available or (more likely) on a different aetheryte
                _logger.LogInformation("Teleporting to aetheryte {AetheryteId}", aetheryteId);
                return Telepo.Instance()->Teleport(aetheryteId, subIndex);
            }
        }

        return false;
    }

    public bool TeleportAetheryte(EAetheryteLocation aetheryteLocation)
        => TeleportAetheryte((uint)aetheryteLocation);

    public bool IsFlyingUnlocked(ushort territoryId)
    {
        if (_configuration.Advanced.NeverFly)
            return false;

        if (IsQuestAccepted(new QuestId(3304)) && _condition[ConditionFlag.Mounted])
        {
            BattleChara* battleChara = (BattleChara*)(_clientState.LocalPlayer?.Address ?? 0);
            if (battleChara != null && battleChara->Mount.MountId == 198) // special quest amaro, not the normal one
                return true;
        }

        var playerState = PlayerState.Instance();
        return playerState != null &&
               _territoryToAetherCurrentCompFlgSet.TryGetValue(territoryId, out byte aetherCurrentCompFlgSet) &&
               playerState->IsAetherCurrentZoneComplete(aetherCurrentCompFlgSet);
    }

    public bool IsFlyingUnlockedInCurrentZone() => IsFlyingUnlocked(_clientState.TerritoryType);

    public bool IsAetherCurrentUnlocked(uint aetherCurrentId)
    {
        var playerState = PlayerState.Instance();
        return playerState != null &&
               playerState->IsAetherCurrentUnlocked(aetherCurrentId);
    }

    public IGameObject? FindObjectByDataId(uint dataId, ObjectKind? kind = null, bool targetable = false)
    {
        foreach (var gameObject in _objectTable)
        {
            if (targetable && !gameObject.IsTargetable)
                continue;

            if (gameObject.ObjectKind is ObjectKind.Player or ObjectKind.Companion or ObjectKind.MountType
                or ObjectKind.Retainer or ObjectKind.Housing)
                continue;

            if (gameObject.DataId == dataId && (kind == null || kind.Value == gameObject.ObjectKind))
            {
                return gameObject;
            }
        }

        _logger.LogWarning("Could not find GameObject with dataId {DataId}", dataId);
        return null;
    }

    public bool InteractWith(uint dataId, ObjectKind? kind = null)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId, kind);
        if (gameObject != null)
            return InteractWith(gameObject);

        _logger.LogDebug("Game object is null");
        return false;
    }

    public bool InteractWith(IGameObject gameObject)
    {
        _logger.LogInformation("Setting target with {DataId} to {ObjectId}", gameObject.DataId, gameObject.EntityId);
        _targetManager.Target = null;
        _targetManager.Target = gameObject;

        if (gameObject.ObjectKind == ObjectKind.GatheringPoint)
        {
            TargetSystem.Instance()->OpenObjectInteraction((GameObject*)gameObject.Address);
            _logger.LogInformation("Interact result: (none) for GatheringPoint");
            return true;
        }
        else
        {
            long result = (long)TargetSystem.Instance()->InteractWithObject((GameObject*)gameObject.Address, false);

            _logger.LogInformation("Interact result: {Result}", result);
            return result != 7 && result > 0;
        }
    }

    public bool UseItem(uint itemId)
    {
        long result = AgentInventoryContext.Instance()->UseItem(itemId);
        _logger.LogInformation("UseItem result: {Result}", result);

        return result == 0;
    }

    public bool UseItem(uint dataId, uint itemId)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = gameObject;
            long result = AgentInventoryContext.Instance()->UseItem(itemId);

            _logger.LogInformation("UseItem result on {DataId}: {Result}", dataId, result);

            // TODO is 1 a generally accepted result?
            return result == 0 || (itemId == 2002450 && result == 1);
        }

        return false;
    }

    public bool UseItemOnGround(uint dataId, uint itemId)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            Vector3 position = gameObject.Position;
            return ActionManager.Instance()->UseActionLocation(ActionType.KeyItem, itemId, location: &position);
        }

        return false;
    }

    public bool UseItemOnPosition(Vector3 position, uint itemId)
    {
        return ActionManager.Instance()->UseActionLocation(ActionType.KeyItem, itemId, location: &position);
    }

    public bool UseAction(EAction action)
    {
        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, (uint)action) == 0)
        {
            bool result = ActionManager.Instance()->UseAction(ActionType.Action, (uint)action);
            _logger.LogInformation("UseAction {Action} result: {Result}", action, result);

            return result;
        }

        return false;
    }

    public bool UseAction(IGameObject gameObject, EAction action)
    {
        var actionRow = _dataManager.GetExcelSheet<Action>()!.GetRow((uint)action)!;
        if (!ActionManager.CanUseActionOnTarget((uint)action, (GameObject*)gameObject.Address))
        {
            _logger.LogWarning("Can not use action {Action} on target {Target}", action, gameObject);
            return false;
        }

        _targetManager.Target = gameObject;
        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, (uint)action, gameObject.GameObjectId) == 0)
        {
            bool result;
            if (actionRow.TargetArea)
            {
                Vector3 position = gameObject.Position;
                result = ActionManager.Instance()->UseActionLocation(ActionType.Action, (uint)action,
                    location: &position);
                _logger.LogInformation("UseAction {Action} on target area {Target} result: {Result}", action,
                    gameObject,
                    result);
            }
            else
            {
                result = ActionManager.Instance()->UseAction(ActionType.Action, (uint)action, gameObject.GameObjectId);
                _logger.LogInformation("UseAction {Action} on target {Target} result: {Result}", action, gameObject,
                    result);
            }

            return result;
        }

        return false;
    }

    public bool IsObjectAtPosition(uint dataId, Vector3 position, float distance)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        return gameObject != null && (gameObject.Position - position).Length() < distance;
    }

    public bool HasStatusPreventingMount()
    {
        if (_condition[ConditionFlag.Swimming] && !IsFlyingUnlockedInCurrentZone())
            return true;

        // company chocobo is locked
        var playerState = PlayerState.Instance();
        if (playerState != null && !playerState->IsMountUnlocked(1))
            return true;

        var localPlayer = _clientState.LocalPlayer;
        if (localPlayer == null)
            return false;

        var battleChara = (BattleChara*)localPlayer.Address;
        StatusManager* statusManager = battleChara->GetStatusManager();
        if (statusManager->HasStatus(1151))
            return true;

        return HasCharacterStatusPreventingMountOrSprint();
    }

    public bool HasStatusPreventingSprint() => HasCharacterStatusPreventingMountOrSprint();

    private bool HasCharacterStatusPreventingMountOrSprint()
    {
        var localPlayer = _clientState.LocalPlayer;
        if (localPlayer == null)
            return false;

        var battleChara = (BattleChara*)localPlayer.Address;
        StatusManager* statusManager = battleChara->GetStatusManager();
        return statusManager->HasStatus(565) ||
               statusManager->HasStatus(404) ||
               statusManager->HasStatus(416) ||
               statusManager->HasStatus(2729) ||
               statusManager->HasStatus(2730);
    }

    public bool Mount()
    {
        if (_condition[ConditionFlag.Mounted])
            return true;

        var playerState = PlayerState.Instance();
        if (playerState != null && _configuration.General.MountId != 0 &&
            playerState->IsMountUnlocked(_configuration.General.MountId))
        {
            if (ActionManager.Instance()->GetActionStatus(ActionType.Mount, _configuration.General.MountId) == 0)
            {
                _logger.LogDebug("Attempting to use preferred mount...");
                if (ActionManager.Instance()->UseAction(ActionType.Mount, _configuration.General.MountId))
                {
                    _logger.LogInformation("Using preferred mount");
                    return true;
                }

                return false;
            }
        }
        else
        {
            if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) == 0)
            {
                _logger.LogDebug("Attempting to use mount roulette...");
                if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9))
                {
                    _logger.LogInformation("Using mount roulette");
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    public bool Unmount()
    {
        if (!_condition[ConditionFlag.Mounted])
            return true;

        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 23) == 0)
        {
            _logger.LogDebug("Attempting to unmount...");
            if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23))
            {
                _logger.LogInformation("Unmounted");
                return true;
            }

            return false;
        }
        else
        {
            _logger.LogWarning("Can't unmount right now?");
            return false;
        }
    }

    public void OpenDutyFinder(uint contentFinderConditionId)
    {
        if (_contentFinderConditionToContentId.TryGetValue(contentFinderConditionId, out ushort contentId))
        {
            if (UIState.IsInstanceContentUnlocked(contentId))
                AgentContentsFinder.Instance()->OpenRegularDuty(contentFinderConditionId);
            else
                _logger.LogError(
                    "Trying to access a locked duty (cf: {ContentFinderId}, content: {ContentId})",
                    contentFinderConditionId, contentId);
        }
        else
            _logger.LogError("Could not find content for content finder condition (cf: {ContentFinderId})",
                contentFinderConditionId);
    }

    public string? GetDialogueText(Quest currentQuest, string? excelSheetName, string key)
    {
        if (excelSheetName == null)
        {
            var questRow =
                _dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets2.Quest>()!.GetRow((uint)currentQuest.QuestElementId.Value +
                    0x10000);
            if (questRow == null)
            {
                _logger.LogError("Could not find quest row for {QuestId}", currentQuest.QuestElementId);
                return null;
            }

            excelSheetName = $"quest/{(currentQuest.QuestElementId.Value / 100):000}/{questRow.Id}";
        }

        var excelSheet = _dataManager.Excel.GetSheet<QuestDialogueText>(excelSheetName);
        if (excelSheet == null)
        {
            _logger.LogError("Unknown excel sheet '{SheetName}'", excelSheetName);
            return null;
        }

        return excelSheet.FirstOrDefault(x => x.Key == key)?.Value?.ToDalamudString().ToString();
    }

    public string? GetDialogueTextByRowId(string? excelSheet, uint rowId)
    {
        if (excelSheet == "GimmickYesNo")
        {
            var questRow = _dataManager.GetExcelSheet<GimmickYesNo>()!.GetRow(rowId);
            return questRow?.Unknown0?.ToString();
        }
        else if (excelSheet == "Warp")
        {
            var questRow = _dataManager.GetExcelSheet<Warp>()!.GetRow(rowId);
            return questRow?.Name?.ToString();
        }
        else if (excelSheet is "Addon")
        {
            var questRow = _dataManager.GetExcelSheet<Addon>()!.GetRow(rowId);
            return questRow?.Text?.ToString();
        }
        else if (excelSheet is "EventPathMove")
        {
            var questRow = _dataManager.GetExcelSheet<EventPathMove>()!.GetRow(rowId);
            return questRow?.Unknown10?.ToString();
        }
        else if (excelSheet is "ContentTalk" or null)
        {
            var questRow = _dataManager.GetExcelSheet<ContentTalk>()!.GetRow(rowId);
            return questRow?.Text?.ToString();
        }
        else
            throw new ArgumentOutOfRangeException(nameof(excelSheet), $"Unsupported excel sheet {excelSheet}");
    }

    public bool IsOccupied()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return true;

        if (IsLoadingScreenVisible())
            return true;

        return _condition[ConditionFlag.Occupied] || _condition[ConditionFlag.Occupied30] ||
               _condition[ConditionFlag.Occupied33] || _condition[ConditionFlag.Occupied38] ||
               _condition[ConditionFlag.Occupied39] || _condition[ConditionFlag.OccupiedInEvent] ||
               _condition[ConditionFlag.OccupiedInQuestEvent] || _condition[ConditionFlag.OccupiedInCutSceneEvent] ||
               _condition[ConditionFlag.Casting] || _condition[ConditionFlag.Unknown57] ||
               _condition[ConditionFlag.BetweenAreas] || _condition[ConditionFlag.BetweenAreas51] ||
               _condition[ConditionFlag.Jumping61] || _condition[ConditionFlag.Gathering42];
    }

    public bool IsLoadingScreenVisible()
    {
        return _gameGui.TryGetAddonByName("FadeMiddle", out AtkUnitBase* fade) &&
               LAddon.IsAddonReady(fade) &&
               fade->IsVisible;
    }

    public GrandCompany GetGrandCompany()
    {
        return (GrandCompany)PlayerState.Instance()->GrandCompany;
    }
}
