using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using LLib.GameUI;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Controller.Steps.Interactions;
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
    private readonly Configuration _configuration;
    private readonly IDataManager _dataManager;
    private readonly IClientState _clientState;
    private readonly IGameGui _gameGui;
    private readonly ILogger<QuestFunctions> _logger;

    public QuestFunctions(
        QuestRegistry questRegistry,
        QuestData questData,
        AetheryteFunctions aetheryteFunctions,
        Configuration configuration,
        IDataManager dataManager,
        IClientState clientState,
        IGameGui gameGui,
        ILogger<QuestFunctions> logger)
    {
        _questRegistry = questRegistry;
        _questData = questData;
        _aetheryteFunctions = aetheryteFunctions;
        _configuration = configuration;
        _dataManager = dataManager;
        _clientState = clientState;
        _gameGui = gameGui;
        _logger = logger;
    }

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

    public (ElementId? CurrentQuest, byte Sequence) GetCurrentQuestInternal()
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
                        if (_questRegistry.IsKnownQuest(currentQuest))
                            return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
                        break;

                    case 2: // leve
                        currentQuest = new LeveId(questManager->LeveQuests[trackedQuest.Index].LeveId);
                        if (_questRegistry.IsKnownQuest(currentQuest))
                            return (currentQuest, questManager->GetLeveQuestById(currentQuest.Value)->Sequence);
                        break;
                }

                if (_questRegistry.IsKnownQuest(currentQuest))
                    return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
            }

            ElementId? priorityQuest = GetNextPriorityQuestThatCanBeAccepted();
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
            return default;

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

        // if we're not at a high enough level to continue, we also ignore it
        var currentLevel = _clientState.LocalPlayer?.Level ?? 0;
        if (currentLevel != 0 &&
            _questRegistry.TryGetQuest(currentQuest, out Quest? quest)
            && quest.Info.Level > currentLevel)
            return default;

        return (currentQuest, QuestManager.GetQuestSequence(currentQuest.Value));
    }

    public QuestProgressInfo? GetQuestProgressInfo(ElementId elementId)
    {
        if (elementId is QuestId questId)
        {
            QuestWork* questWork = QuestManager.Instance()->GetQuestById(questId.Value);
            return questWork != null ? new QuestProgressInfo(*questWork) : null;
        }
        else if (elementId is LeveId leveId)
        {
            LeveWork* leveWork = QuestManager.Instance()->GetLeveQuestById(leveId.Value);
            return leveWork != null ? new QuestProgressInfo(*leveWork) : null;
        }
        else
            return null;
    }

    public ElementId? GetNextPriorityQuestThatCanBeAccepted()
    {
        // all priority quests assume we're able to teleport to the beginning (and for e.g. class quests, the end)
        // ideally without having to wait 15m for Return.
        if (!_aetheryteFunctions.IsTeleportUnlocked())
            return null;

        // ideally, we'd also be able to afford *some* teleports
        // this implicitly makes sure we're not starting one of the lv1 class quests if we can't afford to teleport back
        //
        // Of course, they can still be accepted manually.
        InventoryManager* inventoryManager = InventoryManager.Instance();
        int gil = inventoryManager->GetItemCountInContainer(1, InventoryType.Currency);

        return GetPriorityQuestsThatCanBeAccepted()
            .Where(x =>
            {
                if (!_questRegistry.TryGetQuest(x, out Quest? quest))
                    return false;

                var firstStep = quest.FindSequence(0)?.FindStep(0);
                if (firstStep == null)
                    return false;

                if (firstStep.AetheryteShortcut != null)
                    return true;

                if (firstStep is
                    { InteractionType: EInteractionType.UseItem, ItemId: UseItem.VesperBayAetheryteTicket })
                    return true;

                return false;
            })
            .FirstOrDefault(x =>
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
            });
    }

    private static int EstimateTeleportCosts(Quest quest)
    {
        /*
        if (quest.Info.Expansion == EExpansionVersion.ARealmReborn)
            return 300 * quest.AllSteps().Count(x => x.Step.AetheryteShortcut != null);
        else
            return 1000 * quest.AllSteps().Count(x => x.Step.AetheryteShortcut != null);
            */
        return 0;
    }

    private List<ElementId> GetPriorityQuestsThatCanBeAccepted()
    {
        List<ElementId> priorityQuests =
        [
            new QuestId(1157), // Garuda (Hard)
            new QuestId(1158), // Titan (Hard)
            ..QuestData.CrystalTowerQuests
        ];

        EClassJob classJob = (EClassJob?)_clientState.LocalPlayer?.ClassJob.Id ?? EClassJob.Adventurer;
        ushort[] shadowbringersRoleQuestChapters = QuestData.AllRoleQuestChapters.Select(x => x[0]).ToArray();
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
            .Where(IsReadyToAcceptQuest)
            .ToList();
    }

    public bool IsReadyToAcceptQuest(ElementId questId)
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

    public bool IsQuestAcceptedOrComplete(ElementId elementId)
    {
        return IsQuestComplete(elementId) || IsQuestAccepted(elementId);
    }

    public bool IsQuestAccepted(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestAccepted(questId);
        else if (elementId is LeveId leveId)
            return IsQuestAccepted(leveId);
        else if (elementId is SatisfactionSupplyNpcId)
            return false;
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    public bool IsQuestAccepted(QuestId questId)
    {
        QuestManager* questManager = QuestManager.Instance();
        return questManager->IsQuestAccepted(questId.Value);
    }

    public bool IsQuestAccepted(LeveId leveId)
    {
        QuestManager* questManager = QuestManager.Instance();
        foreach (var leveQuest in questManager->LeveQuests)
        {
            if (leveQuest.LeveId == leveId.Value)
                return true;
        }

        return false;
    }

    public bool IsQuestComplete(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return IsQuestComplete(questId);
        else if (elementId is LeveId leveId)
            return IsQuestComplete(leveId);
        else if (elementId is SatisfactionSupplyNpcId)
            return false;
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    [SuppressMessage("Performance", "CA1822")]
    public bool IsQuestComplete(QuestId questId)
    {
        return QuestManager.IsQuestComplete(questId.Value);
    }

    public bool IsQuestComplete(LeveId leveId)
    {
        return QuestManager.Instance()->IsLevequestComplete(leveId.Value);
    }

    public bool IsQuestLocked(ElementId elementId, ElementId? extraCompletedQuest = null)
    {
        if (elementId is QuestId questId)
            return IsQuestLocked(questId, extraCompletedQuest);
        else if (elementId is LeveId leveId)
            return IsQuestLocked(leveId);
        else if (elementId is SatisfactionSupplyNpcId)
            return false;
        else
            throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    public bool IsQuestLocked(QuestId questId, ElementId? extraCompletedQuest = null)
    {
        var questInfo = (QuestInfo)_questData.GetQuestInfo(questId);
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

        if (_questData.GetLockedClassQuests().Contains(questId))
            return true;

        return !HasCompletedPreviousQuests(questInfo, extraCompletedQuest) || !HasCompletedPreviousInstances(questInfo);
    }

    public bool IsQuestLocked(LeveId leveId)
    {
        // this only checks for the current class
        IQuestInfo questInfo = _questData.GetQuestInfo(leveId);
        if (!questInfo.ClassJobs.Contains((EClassJob)_clientState.LocalPlayer!.ClassJob.Id) ||
            questInfo.Level > _clientState.LocalPlayer.Level)
            return true;

        return !IsQuestAccepted(leveId) && QuestManager.Instance()->NumLeveAllowances == 0;
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

    public bool IsClassJobUnlocked(EClassJob classJob)
    {
        var classJobRow = _dataManager.GetExcelSheet<ClassJob>()!.GetRow((uint)classJob)!;
        var questId = (ushort)classJobRow.UnlockQuest.Row;
        if (questId != 0)
            return IsQuestComplete(new QuestId(questId));

        PlayerState* playerState = PlayerState.Instance();
        return playerState != null && playerState->ClassJobLevels[classJobRow.ExpArrayIndex] > 0;
    }

    public bool IsJobUnlocked(EClassJob classJob)
    {
        var classJobRow = _dataManager.GetExcelSheet<ClassJob>()!.GetRow((uint)classJob)!;
        return IsClassJobUnlocked((EClassJob)classJobRow.ClassJobParent.Row);
    }

    public GrandCompany GetGrandCompany()
    {
        return (GrandCompany)PlayerState.Instance()->GrandCompany;
    }
}
