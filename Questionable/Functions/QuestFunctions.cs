using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using LLib.GameUI;
using Lumina.Excel.Sheets;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;
using Quest = Questionable.Model.Quest;

namespace Questionable.Functions;

internal sealed unsafe class QuestFunctions
{
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly AetheryteFunctions _aetheryteFunctions;
    private readonly AlliedSocietyQuestFunctions _alliedSocietyQuestFunctions;
    private readonly AlliedSocietyData _alliedSocietyData;
    private readonly Configuration _configuration;
    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;
    private readonly IGameGui _gameGui;

    public QuestFunctions(
        QuestRegistry questRegistry,
        QuestData questData,
        AetheryteFunctions aetheryteFunctions,
        AlliedSocietyQuestFunctions alliedSocietyQuestFunctions,
        AlliedSocietyData alliedSocietyData,
        Configuration configuration,
        IDataManager dataManager,
        IClientState clientState,
        IGameGui gameGui)
    {
        _questRegistry = questRegistry;
        _questData = questData;
        _aetheryteFunctions = aetheryteFunctions;
        _alliedSocietyQuestFunctions = alliedSocietyQuestFunctions;
        _alliedSocietyData = alliedSocietyData;
        _configuration = configuration;
        _dataManager = dataManager;
        _clientState = clientState;
        _gameGui = gameGui;
    }

    public (ElementId? CurrentQuest, byte Sequence) GetCurrentQuest(bool allowNewMsq = true)
    {
        var (currentQuest, sequence) = GetCurrentQuestInternal(allowNewMsq);
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
                GrandCompany.ImmortalFlames => (new QuestId(682), 0),
                _ => default
            };
        }
        else if (currentQuest.Value == 3856 && !playerState->IsMountUnlocked(1)) // we come in peace
        {
            ushort chocoboQuest = (GrandCompany)playerState->GrandCompany switch
            {
                GrandCompany.TwinAdder => 700,
                GrandCompany.Maelstrom => 701,
                GrandCompany.ImmortalFlames => 702,
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

    public (ElementId? CurrentQuest, byte Sequence) GetCurrentQuestInternal(bool allowNewMsq)
    {
        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            // always prioritize accepting MSQ quests, to make sure we don't turn in one MSQ quest and then go off to do
            // side quests until the end of time.
            var msqQuest = GetMainScenarioQuest();
            if (msqQuest.CurrentQuest != null && !_questRegistry.IsKnownQuest(msqQuest.CurrentQuest))
                msqQuest = default;

            if (msqQuest.CurrentQuest != null && !IsQuestAccepted(msqQuest.CurrentQuest))
            {
                if (allowNewMsq)
                    return msqQuest;
                else
                    msqQuest = default;
            }

            // Use the quests in the same order as they're shown in the to-do list, e.g. if the MSQ is the first item,
            // do the MSQ; if a side quest is the first item do that side quest.
            //
            // If no quests are marked as 'priority', accepting a new quest adds it to the top of the list.
            List<(ElementId Quest, byte Sequence)> trackedQuests = [];
            for (int i = questManager->TrackedQuests.Length - 1; i >= 0; --i)
            {
                ElementId currentQuest;
                var trackedQuest = questManager->TrackedQuests[i];
                switch (trackedQuest.QuestType)
                {
                    case 1: // normal quest
                        currentQuest = new QuestId(questManager->NormalQuests[trackedQuest.Index].QuestId);
                        if (_questRegistry.IsKnownQuest(currentQuest))
                            trackedQuests.Add((currentQuest, QuestManager.GetQuestSequence(currentQuest.Value)));
                        break;

                    case 2: // leve
                        break;
                }
            }

            if (trackedQuests.Count > 0)
            {
                // if we have multiple quests to turn in for an allied society, try and complete all of them
                var (firstTrackedQuest, firstTrackedSequence) = trackedQuests.First();
                EAlliedSociety firstTrackedAlliedSociety =
                    _alliedSocietyData.GetCommonAlliedSocietyTurnIn(firstTrackedQuest);
                if (firstTrackedAlliedSociety != EAlliedSociety.None)
                {
                    var alliedQuestsForSameSociety = trackedQuests.Skip(1)
                        .Where(quest =>
                            _alliedSocietyData.GetCommonAlliedSocietyTurnIn(quest.Quest) == firstTrackedAlliedSociety)
                        .ToList();
                    if (alliedQuestsForSameSociety.Count > 0)
                    {
                        if (firstTrackedSequence == 255)
                        {
                            foreach (var (quest, sequence) in alliedQuestsForSameSociety)
                            {
                                // only if the other quest isn't ready to be turned in
                                if (sequence != 255)
                                    return (quest, sequence);
                            }
                        }
                        else if (!IsOnAlliedSocietyMount())
                        {
                            // a few of the vanu quests require you to talk to one of the npcs near the issuer, so we
                            // give priority to those

                            // also include the first quest in the list for those
                            alliedQuestsForSameSociety.Insert(0, (firstTrackedQuest, firstTrackedSequence));

                            _alliedSocietyData.GetCommonAlliedSocietyNpcs(firstTrackedAlliedSociety,
                                out uint[]? normalNpcs,
                                out _);

                            if (normalNpcs.Length > 0)
                            {
                                var talkToNormalNpcs = alliedQuestsForSameSociety
                                    .Where(x => x.Sequence < 255)
                                    .Where(x => IsInteractSequence(x.Quest, x.Sequence, normalNpcs))
                                    .Cast<(ElementId, byte)?>()
                                    .FirstOrDefault();
                                if (talkToNormalNpcs != null)
                                    return talkToNormalNpcs.Value;
                            }

                            /*
                             * TODO: If you have e.g. a mount quest in the middle of 3, it should temporarily make you
                             *       do that quest first, even if it isn't the first in the list. Otherwise, the logic
                             *       here won't make much sense.
                             *
                             * TODO: This also won't work if two or three daily quests use a mount.
                            if (mountNpcs.Length > 0)
                            {
                                var talkToMountNpc = alliedQuestsForSameSociety
                                    .Where(x => x.Sequence < 255)
                                    .Where(x => IsInteractStep(x.Quest, x.Sequence, mountNpcs))
                                    .Cast<(ElementId, byte)?>()
                                    .FirstOrDefault();
                                if (talkToMountNpc != null)
                                    return talkToMountNpc.Value;
                            }
                            */
                        }
                    }
                }

                return (firstTrackedQuest, firstTrackedSequence);
            }

            ElementId? priorityQuest = GetNextPriorityQuestsThatCanBeAccepted().FirstOrDefault();
            if (priorityQuest != null)
            {
                // if we have an accepted msq quest, and know of no quest of those currently in the to-do list...
                // (1) try and find a priority quest to do
                return (priorityQuest, QuestManager.GetQuestSequence(priorityQuest.Value));
            }
            else if (msqQuest.CurrentQuest != null)
            {
                // (2) just do a normal msq quest
                return msqQuest;
            }
        }

        return default;
    }

    private (QuestId? CurrentQuest, byte Sequence) GetMainScenarioQuest()
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
        {
            if (IsQuestComplete(_questData.LastMainScenarioQuestId))
                return default;

            // fallback lookup; find a quest which isn't completed but where all prequisites are met
            // excluding branching quests

            var playerState = PlayerState.Instance();
            var potentialQuests = _questData.MainScenarioQuests
                .Where(x => x.StartingCity == 0 || x.StartingCity == playerState->StartTown)
                .Where(q => IsReadyToAcceptQuest(q.QuestId, true))
                .ToList();
            if (potentialQuests.Count == 0)
                return default;
            else if (potentialQuests.Count > 1)
            {
                // for all of these (except the GC quests), questionable normally auto-picks the next quest based on the
                // agent data. This should (hopefully) pick the exact same quest when the agent does not know for a bit.
                if (potentialQuests.All(x => x.QuestId.Value is 680 or 681 or 682))
                    currentQuest = new QuestId(681); // The Company You Keep; actual quest will be resolved later in GetCurrentQuest
                else if (potentialQuests.Any(x => x.QuestId.Value == 1583))
                    currentQuest = new QuestId(1583); // HW: Over the Wall vs. Onwards and Upwards
                else if (potentialQuests.Any(x => x.QuestId.Value == 2451))
                    currentQuest = new QuestId(2451); // SB: A Friend of a Friend in Need vs. A Familiar Face Forgotten
                else if (potentialQuests.Any(x => x.QuestId.Value == 3282))
                    currentQuest = new QuestId(3282); // ShB: In Search of Alphinaud vs. In Search of Alisaie
                else if (potentialQuests.Any(x => x.QuestId.Value == 4359))
                    currentQuest = new QuestId(4359); // EW: Hitting the Books vs. For Thavnair Bound
                else if (potentialQuests.Any(x => x.QuestId.Value == 4865))
                    currentQuest = new QuestId(4865); // DT: To Kozama'uk vs. To Urqopacha
                if (potentialQuests.Count != 1)
                    return default;
            }
            else
                currentQuest = (QuestId)potentialQuests.Single().QuestId;
        }

        // if the MSQ is hidden, we generally ignore it
        QuestManager* questManager = QuestManager.Instance();
        if (IsQuestAccepted(currentQuest) && questManager->GetQuestById(currentQuest.Value)->IsHidden)
            return default;

        // it can sometimes happen (although this isn't reliably reproducible) that the quest returned here
        // is one you've just completed. We return 255 as sequence here, since that is the end of said quest;
        // but this is just really hoping that this breaks nothing.
        if (IsQuestComplete(currentQuest))
            return (currentQuest, 255);
        else if (!IsReadyToAcceptQuest(currentQuest))
            return default;

        var currentLevel = _clientState.LocalPlayer?.Level;

        // are we in a loading screen?
        if (currentLevel == null)
            return default;

        // if we're not at a high enough level to continue, we also ignore it
        if (_questRegistry.TryGetQuest(currentQuest, out Quest? quest)
            && quest.Info.Level > currentLevel)
            return default;

        return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
    }

    private bool IsOnAlliedSocietyMount()
    {
        BattleChara* battleChara = (BattleChara*)(_clientState.LocalPlayer?.Address ?? 0);
        return battleChara != null &&
               battleChara->Mount.MountId != 0 &&
               _alliedSocietyData.Mounts.ContainsKey(battleChara->Mount.MountId);
    }

    private bool IsInteractSequence(ElementId questId, byte sequenceNo, uint[] dataIds)
    {
        if (_questRegistry.TryGetQuest(questId, out var quest))
        {
            QuestSequence? sequence = quest.FindSequence(sequenceNo);
            return sequence != null &&
                   sequence.Steps.All(x =>
                       x is { InteractionType: EInteractionType.WalkTo } ||
                       (x is { InteractionType: EInteractionType.Interact, DataId: { } dataId } &&
                        dataIds.Contains(dataId)));
        }

        return false;
    }

    public QuestProgressInfo? GetQuestProgressInfo(ElementId elementId)
    {
        if (elementId is QuestId questId)
        {
            QuestWork* questWork = QuestManager.Instance()->GetQuestById(questId.Value);
            return questWork != null ? new QuestProgressInfo(*questWork) : null;
        }
        else
            return null;
    }

    public List<ElementId> GetNextPriorityQuestsThatCanBeAccepted()
    {
        // all priority quests assume we're able to teleport to the beginning (and for e.g. class quests, the end)
        // ideally without having to wait 15m for Return.
        if (!_aetheryteFunctions.IsTeleportUnlocked())
            return [];

        // ideally, we'd also be able to afford *some* teleports
        // this implicitly makes sure we're not starting one of the lv1 class quests if we can't afford to teleport back
        //
        // Of course, they can still be accepted manually.
        InventoryManager* inventoryManager = InventoryManager.Instance();
        int gil = inventoryManager->GetItemCountInContainer(1, InventoryType.Currency);

        return GetPriorityQuests()
            .Where(x => IsReadyToAcceptQuest(x))
            .Where(x =>
            {
                if (!_questRegistry.TryGetQuest(x, out Quest? quest))
                    return false;

                var firstStep = quest.FindSequence(0)?.FindStep(0);
                if (firstStep == null)
                    return false;

                return firstStep.IsTeleportableForPriorityQuests();
            })
            .Where(x =>
            {
                if (!_questRegistry.TryGetQuest(x, out Quest? quest))
                    return false;

                if (gil < EstimateTeleportCosts(quest))
                    return false;

                return quest.AllSteps().All(y =>
                {
                    if (y.Step.AetheryteShortcut is { } aetheryteShortcut &&
                        !_aetheryteFunctions.IsAetheryteUnlocked(aetheryteShortcut))
                    {
                        if (y.Step.SkipConditions?.AetheryteShortcutIf?.AetheryteLocked == aetheryteShortcut)
                        {
                            // _logger.LogTrace("Checking priority quest {QuestId}: aetheryte locked, but is listed as skippable", quest.Id);
                        }
                        else return false;
                    }

                    if (y.Step.AethernetShortcut is { } aethernetShortcut &&
                        (!_aetheryteFunctions.IsAetheryteUnlocked(aethernetShortcut.From) ||
                         !_aetheryteFunctions.IsAetheryteUnlocked(aethernetShortcut.To)))
                        return false;

                    return true;
                });
            })
            .ToList();
    }

    private static int EstimateTeleportCosts(Quest quest)
    {
        if (quest.Info.Expansion == EExpansionVersion.ARealmReborn)
            return 300 * quest.AllSteps().Count(x => x.Step.AetheryteShortcut != null);
        else
            return 1000 * quest.AllSteps().Count(x => x.Step.AetheryteShortcut != null);
    }

    public List<ElementId> GetPriorityQuests(bool onlyClassAndRoleQuests = false)
    {
        List<ElementId> priorityQuests = [];
        if (!onlyClassAndRoleQuests)
        {
            priorityQuests.Add(new QuestId(1157)); // Garuda (Hard)
            priorityQuests.Add(new QuestId(1158)); // Titan (Hard)
            priorityQuests.AddRange(QuestData.CrystalTowerQuests);
        }

        EClassJob classJob = (EClassJob?)_clientState.LocalPlayer?.ClassJob.RowId ?? EClassJob.Adventurer;
        uint[] shadowbringersRoleQuestChapters = QuestData.AllRoleQuestChapters.Select(x => x[0]).ToArray();
        if (classJob != EClassJob.Adventurer)
        {
            priorityQuests.AddRange(_questRegistry.GetKnownClassJobQuests(classJob)
                .Where(x =>
                {
                    if (!_questRegistry.TryGetQuest(x.QuestId, out Quest? quest) ||
                        quest.Info is not QuestInfo questInfo)
                        return false;

                    // if no shadowbringers role quest is complete, (at least one) is required
                    if (shadowbringersRoleQuestChapters.Contains(questInfo.NewGamePlusChapter))
                        return !QuestData.FinalShadowbringersRoleQuests.Any(IsQuestComplete);

                    // ignore all other role quests
                    if (QuestData.AllRoleQuestChapters.Any(y => y.Contains(questInfo.NewGamePlusChapter)))
                        return false;

                    // even job quests for the later expacs (after role quests were introduced) might have skills locked
                    // behind them, e.g. reaper and sage

                    return true;
                })
                .Select(x => x.QuestId));
        }

        return priorityQuests
            .Where(_questRegistry.IsKnownQuest)
            .ToList();
    }

    public bool IsReadyToAcceptQuest(ElementId questId, bool ignoreLevel = false)
    {
        _questRegistry.TryGetQuest(questId, out var quest);
        if (quest is { Info.IsRepeatable: true })
        {
            if (IsQuestAccepted(questId))
                return false;

            if (questId is QuestId qId && IsDailyAlliedSocietyQuest(qId))
            {
                if (QuestManager.Instance()->IsDailyQuestCompleted(questId.Value))
                    return false;

                if (!IsDailyAlliedSocietyQuestAndAvailableToday(qId))
                    return false;
            }
            else
            {
                if (IsQuestComplete(questId))
                    return false;
            }
        }
        else
        {
            if (IsQuestAcceptedOrComplete(questId))
                return false;
        }

        if (IsQuestLocked(questId))
            return false;

        if (!ignoreLevel)
        {
            // if we're not at a high enough level to continue, we also ignore it
            var currentLevel = _clientState.LocalPlayer?.Level ?? 0;
            if (currentLevel != 0 && quest != null && quest.Info.Level > currentLevel)
                return false;
        }

        return true;
    }

    public bool IsQuestAcceptedOrComplete(ElementId elementId)
    {
        return IsQuestComplete(elementId) || IsQuestAccepted(elementId);
    }

    public bool IsQuestAccepted(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestAccepted(questId);
        else if (elementId is SatisfactionSupplyNpcId)
            return false;
        else if (elementId is AlliedSocietyDailyId)
            return false;
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
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
        else if (elementId is SatisfactionSupplyNpcId)
            return false;
        else if (elementId is AlliedSocietyDailyId)
            return false;
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
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
        else if (elementId is SatisfactionSupplyNpcId satisfactionSupplyNpcId)
            return IsQuestLocked(satisfactionSupplyNpcId);
        else if (elementId is AlliedSocietyDailyId alliedSocietyDailyId)
            return IsQuestLocked(alliedSocietyDailyId);
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    private bool IsQuestLocked(QuestId questId, ElementId? extraCompletedQuest = null)
    {
        if (IsQuestUnobtainable(questId, extraCompletedQuest))
            return true;

        var questInfo = (QuestInfo)_questData.GetQuestInfo(questId);
        if (questInfo.GrandCompany != GrandCompany.None && questInfo.GrandCompany != GetGrandCompany())
            return true;

        if (questInfo.AlliedSociety != EAlliedSociety.None && questInfo.IsRepeatable)
            return !IsDailyAlliedSocietyQuestAndAvailableToday(questId);

        if (questInfo.IsMoogleDeliveryQuest)
        {
            byte currentDeliveryLevel = PlayerState.Instance()->DeliveryLevel;
            if (extraCompletedQuest != null &&
                _questData.TryGetQuestInfo(extraCompletedQuest, out IQuestInfo? extraQuestInfo) &&
                extraQuestInfo is QuestInfo { IsMoogleDeliveryQuest: true })
                currentDeliveryLevel++;

            if (questInfo.MoogleDeliveryLevel > currentDeliveryLevel)
                return true;
        }

        return !HasCompletedPreviousQuests(questInfo, extraCompletedQuest) || !HasCompletedPreviousInstances(questInfo);
    }

    private bool IsQuestLocked(SatisfactionSupplyNpcId satisfactionSupplyNpcId)
    {
        SatisfactionSupplyInfo questInfo = (SatisfactionSupplyInfo)_questData.GetQuestInfo(satisfactionSupplyNpcId);
        return !HasCompletedPreviousQuests(questInfo, null);
    }

    private bool IsQuestLocked(AlliedSocietyDailyId alliedSocietyDailyId)
    {
        PlayerState* playerState = PlayerState.Instance();
        byte currentRank = playerState->GetBeastTribeRank(alliedSocietyDailyId.AlliedSociety);
        return currentRank == 0 || currentRank < alliedSocietyDailyId.Rank;
    }

    public bool IsDailyAlliedSocietyQuest(QuestId questId)
    {
        var questInfo = (QuestInfo)_questData.GetQuestInfo(questId);
        return questInfo.AlliedSociety != EAlliedSociety.None && questInfo.IsRepeatable;
    }

    public bool IsDailyAlliedSocietyQuestAndAvailableToday(QuestId questId)
    {
        if (!IsDailyAlliedSocietyQuest(questId))
            return false;

        var questInfo = (QuestInfo)_questData.GetQuestInfo(questId);
        return _alliedSocietyQuestFunctions.GetAvailableAlliedSocietyQuests(questInfo.AlliedSociety).Contains(questId);
    }

    public bool IsQuestUnobtainable(ElementId elementId, ElementId? extraCompletedQuest = null)
    {
        if (elementId is QuestId questId)
            return IsQuestUnobtainable(questId, extraCompletedQuest);
        else
            return false;
    }

    public bool IsQuestUnobtainable(QuestId questId, ElementId? extraCompletedQuest = null)
    {
        var questInfo = (QuestInfo)_questData.GetQuestInfo(questId);
        if (questInfo.Expansion > (EExpansionVersion)PlayerState.Instance()->MaxExpansion)
            return true;

        if (questInfo.QuestLocks.Count > 0)
        {
            var completedQuests = questInfo.QuestLocks.Count(x => IsQuestComplete(x) || x.Equals(extraCompletedQuest));
            if (questInfo.QuestLockJoin == EQuestJoin.All && questInfo.QuestLocks.Count == completedQuests)
                return true;
            else if (questInfo.QuestLockJoin == EQuestJoin.AtLeastOne && completedQuests > 0)
                return true;
        }

        if (_questData.GetLockedClassQuests().Contains(questId))
            return true;

        var startingCity = PlayerState.Instance()->StartTown;
        if (questInfo.StartingCity > 0 && questInfo.StartingCity != startingCity)
            return true;

        if (questId.Value == 674 && startingCity == 3)
            return true;
        if (questId.Value == 673 && startingCity != 3)
            return true;

        Dictionary<ushort, EClassJob> closeToHomeQuests = new()
        {
            { 108, EClassJob.Marauder },
            { 109, EClassJob.Arcanist },
            { 85, EClassJob.Lancer },
            { 123, EClassJob.Archer },
            { 124, EClassJob.Conjurer },
            { 568, EClassJob.Gladiator },
            { 569, EClassJob.Pugilist },
            { 570, EClassJob.Thaumaturge }
        };

        // The starting class experience is a bit confusing. If you start in Gridania, the MSQ next quest data will
        // always select 'Close to Home (Lancer)' even if starting as Conjurer/Archer. However, if we always mark the
        // Lancer quest as unobtainable, it'll not get picked up as Conjurer/Archer, and thus will stop questing.
        //
        // While the NPC offers all 3 quests, there's no manual selection, and interacting will automatically select the
        // quest for your current class, then switch you from a dead-ish intro zone to the actual starting city
        // (so that you can't come back later to pick up another quest).
        if (closeToHomeQuests.TryGetValue(questId.Value, out EClassJob neededStartingClass) &&
            closeToHomeQuests.Any(x => IsQuestAcceptedOrComplete(new QuestId(x.Key))))
        {
            EClassJob actualStartingClass = (EClassJob)PlayerState.Instance()->FirstClass;
            if (actualStartingClass != neededStartingClass)
                return true;
        }

        if (IsQuestRemoved(questId))
            return true;

        return false;
    }

    public bool IsQuestRemoved(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestRemoved(questId);
        else
            return false;
    }

    [SuppressMessage("Performance", "CA1822")]
    private bool IsQuestRemoved(QuestId questId)
    {
        return questId.Value is 487 or 1428 or 1429;
    }

    private bool HasCompletedPreviousQuests(IQuestInfo questInfo, ElementId? extraCompletedQuest)
    {
        if (questInfo.PreviousQuests.Count == 0)
            return true;

        var completedQuests = questInfo.PreviousQuests.Count(x =>
            HasEnoughProgressOnPreviousQuest(x) || x.QuestId.Equals(extraCompletedQuest));
        if (questInfo.PreviousQuestJoin == EQuestJoin.All &&
            questInfo.PreviousQuests.Count == completedQuests)
            return true;
        else if (questInfo.PreviousQuestJoin == EQuestJoin.AtLeastOne && completedQuests > 0)
            return true;
        else
            return false;
    }

    private bool HasEnoughProgressOnPreviousQuest(PreviousQuestInfo previousQuestInfo)
    {
        if (IsQuestComplete(previousQuestInfo.QuestId))
            return true;

        if (previousQuestInfo.Sequence != 0 && IsQuestAccepted(previousQuestInfo.QuestId))
        {
            var progress = GetQuestProgressInfo(previousQuestInfo.QuestId);
            return progress != null && progress.Sequence >= previousQuestInfo.Sequence;
        }

        return false;
    }

    private static bool HasCompletedPreviousInstances(QuestInfo questInfo)
    {
        if (questInfo.PreviousInstanceContent.Count == 0)
            return true;

        var completedInstances = questInfo.PreviousInstanceContent.Count(x => UIState.IsInstanceContentCompleted(x));
        if (questInfo.PreviousInstanceContentJoin == EQuestJoin.All &&
            questInfo.PreviousInstanceContent.Count == completedInstances)
            return true;
        else if (questInfo.PreviousInstanceContentJoin == EQuestJoin.AtLeastOne && completedInstances > 0)
            return true;
        else
            return false;
    }

    public bool IsClassJobUnlocked(EClassJob classJob)
    {
        var classJobRow = _dataManager.GetExcelSheet<ClassJob>().GetRow((uint)classJob);
        var questId = (ushort)classJobRow.UnlockQuest.RowId;
        if (questId != 0)
            return IsQuestComplete(new QuestId(questId));

        PlayerState* playerState = PlayerState.Instance();
        return playerState != null && playerState->ClassJobLevels[classJobRow.ExpArrayIndex] > 0;
    }

    public bool IsJobUnlocked(EClassJob classJob)
    {
        var classJobRow = _dataManager.GetExcelSheet<ClassJob>().GetRow((uint)classJob);
        return IsClassJobUnlocked((EClassJob)classJobRow.ClassJobParent.RowId);
    }

    public GrandCompany GetGrandCompany()
    {
        return (GrandCompany)PlayerState.Instance()->GrandCompany;
    }
}
