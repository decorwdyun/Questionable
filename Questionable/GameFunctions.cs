﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Lumina.Excel.CustomSheets;
using Lumina.Excel.GeneratedSheets2;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Model.V1;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ContentFinderCondition = Lumina.Excel.GeneratedSheets.ContentFinderCondition;
using ContentTalk = Lumina.Excel.GeneratedSheets.ContentTalk;
using Emote = Lumina.Excel.GeneratedSheets.Emote;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;
using Quest = Questionable.Model.Quest;
using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

namespace Questionable;

internal sealed unsafe class GameFunctions
{
    private static class Signatures
    {
        internal const string SendChat = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9";
        internal const string SanitiseString = "E8 ?? ?? ?? ?? EB 0A 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8D";
    }

    private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

    private readonly ProcessChatBoxDelegate _processChatBox;
    private readonly delegate* unmanaged<Utf8String*, int, IntPtr, void> _sanitiseString;
    private readonly ReadOnlyDictionary<ushort, byte> _territoryToAetherCurrentCompFlgSet;
    private readonly ReadOnlyDictionary<EEmote, string> _emoteCommands;
    private readonly ReadOnlyDictionary<uint, ushort> _contentFinderConditionToContentId;

    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly QuestRegistry _questRegistry;
    private readonly IGameGui _gameGui;
    private readonly Configuration _configuration;
    private readonly ILogger<GameFunctions> _logger;

    public GameFunctions(IDataManager dataManager, IObjectTable objectTable, ISigScanner sigScanner,
        ITargetManager targetManager, ICondition condition, IClientState clientState, QuestRegistry questRegistry,
        IGameGui gameGui, Configuration configuration, ILogger<GameFunctions> logger)
    {
        _dataManager = dataManager;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _condition = condition;
        _clientState = clientState;
        _questRegistry = questRegistry;
        _gameGui = gameGui;
        _configuration = configuration;
        _logger = logger;
        _processChatBox =
            Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(sigScanner.ScanText(Signatures.SendChat));
        _sanitiseString =
            (delegate* unmanaged<Utf8String*, int, IntPtr, void>)sigScanner.ScanText(Signatures.SanitiseString);

        _territoryToAetherCurrentCompFlgSet = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.Unknown32 > 0)
            .ToDictionary(x => (ushort)x.RowId, x => x.Unknown32)
            .AsReadOnly();
        _emoteCommands = dataManager.GetExcelSheet<Emote>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.TextCommand != null && x.TextCommand.Value != null)
            .Select(x => (x.RowId, Command: x.TextCommand.Value!.Command?.ToString()))
            .Where(x => x.Command != null && x.Command.StartsWith('/'))
            .ToDictionary(x => (EEmote)x.RowId, x => x.Command!)
            .AsReadOnly();
        _contentFinderConditionToContentId = dataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(x => x.RowId > 0 && x.Content > 0)
            .ToDictionary(x => x.RowId, x => x.Content)
            .AsReadOnly();
    }

    public DateTime ReturnRequestedAt { get; set; } = DateTime.MinValue;

    public (ushort CurrentQuest, byte Sequence) GetCurrentQuest()
    {
        var (currentQuest, sequence) = GetCurrentQuestInternal();
        QuestManager* questManager = QuestManager.Instance();
        PlayerState* playerState = PlayerState.Instance();

        if (currentQuest == 0)
        {
            if (_clientState.TerritoryType == 181) // Starting in Limsa
                return (107, 0);
            if (_clientState.TerritoryType == 183) // Starting in Gridania
                return (39, 0);
            return default;
        }
        else if (currentQuest == 681)
        {
            // if we have already picked up the GC quest, just return the progress for it
            if (questManager->IsQuestAccepted(currentQuest) || QuestManager.IsQuestComplete(currentQuest))
                return (currentQuest, sequence);

            // The company you keep...
            return _configuration.General.GrandCompany switch
            {
                GrandCompany.TwinAdder => (680, 0),
                GrandCompany.Maelstrom => (681, 0),
                _ => default
            };
        }
        else if (currentQuest == 3856 && !playerState->IsMountUnlocked(1)) // we come in peace
        {
            ushort chocoboQuest = (GrandCompany)playerState->GrandCompany switch
            {
                GrandCompany.TwinAdder => 700,
                GrandCompany.Maelstrom => 701,
                _ => 0
            };

            if (chocoboQuest != 0 && !QuestManager.IsQuestComplete(chocoboQuest))
                return (chocoboQuest, QuestManager.GetQuestSequence(chocoboQuest));
        }

        return (currentQuest, sequence);
    }

    public (ushort CurrentQuest, byte Sequence) GetCurrentQuestInternal()
    {
        ushort currentQuest;

        // if any quest that is currently tracked (i.e. in the to-do list) exists as mapped quest, we use that
        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            foreach (var tracked in questManager->TrackedQuestsSpan)
            {
                switch (tracked.QuestType)
                {
                    default:
                        continue;

                    case 1: // normal quest
                        currentQuest = questManager->NormalQuestsSpan[tracked.Index].QuestId;
                        break;
                }

                if (_questRegistry.IsKnownQuest(currentQuest))
                    return (currentQuest, QuestManager.GetQuestSequence(currentQuest));
            }
        }

        var scenarioTree = AgentScenarioTree.Instance();
        if (scenarioTree == null)
            return default;

        if (scenarioTree->Data == null)
            return default;

        currentQuest = scenarioTree->Data->CurrentScenarioQuest;
        if (currentQuest == 0)
            return default;

        return (currentQuest, QuestManager.GetQuestSequence(currentQuest));
    }

    public QuestWork? GetQuestEx(ushort questId)
    {
        QuestWork* questWork = QuestManager.Instance()->GetQuestById(questId);
        return questWork != null ? *questWork : null;
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


        if (IsAetheryteUnlocked(aetheryteId, out var subIndex))
        {
            if (aetheryteId == PlayerState.Instance()->HomeAetheryteId &&
                ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 8) == 0)
            {
                ReturnRequestedAt = DateTime.Now;
                if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8))
                    return true;
            }

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0)
                // fallback if return isn't available or (more likely) on a different aetheryte
                return Telepo.Instance()->Teleport(aetheryteId, subIndex);
        }

        return false;
    }

    public bool TeleportAetheryte(EAetheryteLocation aetheryteLocation)
        => TeleportAetheryte((uint)aetheryteLocation);

    public bool IsFlyingUnlocked(ushort territoryId)
    {
        if (_configuration.Advanced.NeverFly)
            return false;

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

    public void ExecuteCommand(string command)
    {
        if (!command.StartsWith('/'))
            return;

        SendMessage(command);
    }

    #region SendMessage

    /// <summary>
    /// <para>
    /// Send a given message to the chat box. <b>This can send chat to the server.</b>
    /// </para>
    /// <para>
    /// <b>This method is unsafe.</b> This method does no checking on your input and
    /// may send content to the server that the normal client could not. You must
    /// verify what you're sending and handle content and length to properly use
    /// this.
    /// </para>
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
    private void SendMessageUnsafe(byte[] message)
    {
        var uiModule = (IntPtr)Framework.Instance()->GetUiModule();

        using var payload = new ChatPayload(message);
        var mem1 = Marshal.AllocHGlobal(400);
        Marshal.StructureToPtr(payload, mem1, false);

        _processChatBox(uiModule, mem1, IntPtr.Zero, 0);

        Marshal.FreeHGlobal(mem1);
    }

    /// <summary>
    /// <para>
    /// Send a given message to the chat box. <b>This can send chat to the server.</b>
    /// </para>
    /// <para>
    /// This method is slightly less unsafe than <see cref="SendMessageUnsafe"/>. It
    /// will throw exceptions for certain inputs that the client can't normally send,
    /// but it is still possible to make mistakes. Use with caution.
    /// </para>
    /// </summary>
    /// <param name="message">message to send</param>
    /// <exception cref="ArgumentException">If <paramref name="message"/> is empty, longer than 500 bytes in UTF-8, or contains invalid characters.</exception>
    /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
    public void SendMessage(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length == 0)
        {
            throw new ArgumentException("message is empty", nameof(message));
        }

        if (bytes.Length > 500)
        {
            throw new ArgumentException("message is longer than 500 bytes", nameof(message));
        }

        if (message.Length != SanitiseText(message).Length)
        {
            throw new ArgumentException("message contained invalid characters", nameof(message));
        }

        SendMessageUnsafe(bytes);
    }

    /// <summary>
    /// <para>
    /// Sanitises a string by removing any invalid input.
    /// </para>
    /// <para>
    /// The result of this method is safe to use with
    /// <see cref="SendMessage"/>, provided that it is not empty or too
    /// long.
    /// </para>
    /// </summary>
    /// <param name="text">text to sanitise</param>
    /// <returns>sanitised text</returns>
    /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
    public string SanitiseText(string text)
    {
        var uText = Utf8String.FromString(text);

        _sanitiseString(uText, 0x27F, IntPtr.Zero);
        var sanitised = uText->ToString();

        uText->Dtor();
        IMemorySpace.Free(uText);

        return sanitised;
    }

    [StructLayout(LayoutKind.Explicit)]
    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    private readonly struct ChatPayload : IDisposable
    {
        [FieldOffset(0)] private readonly IntPtr textPtr;

        [FieldOffset(16)] private readonly ulong textLen;

        [FieldOffset(8)] private readonly ulong unk1;

        [FieldOffset(24)] private readonly ulong unk2;

        internal ChatPayload(byte[] stringBytes)
        {
            textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
            Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
            Marshal.WriteByte(textPtr + stringBytes.Length, 0);

            textLen = (ulong)(stringBytes.Length + 1);

            unk1 = 64;
            unk2 = 0;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(textPtr);
        }
    }

    #endregion

    public GameObject? FindObjectByDataId(uint dataId)
    {
        foreach (var gameObject in _objectTable)
        {
            if (gameObject.DataId == dataId)
            {
                return gameObject;
            }
        }

        _logger.LogWarning("Could not find GameObject with dataId {DataId}", dataId);
        return null;
    }

    public bool InteractWith(uint dataId)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _logger.LogInformation("Setting target with {DataId} to {ObjectId}", dataId, gameObject.ObjectId);
            _targetManager.Target = null;
            _targetManager.Target = gameObject;

            long result = (long)TargetSystem.Instance()->InteractWithObject(
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address, false);

            _logger.LogInformation("Interact result: {Result}", result);
            return result > 0;
        }

        _logger.LogDebug("Game object is null");
        return false;
    }

    public bool UseItem(uint itemId)
    {
        long result = AgentInventoryContext.Instance()->UseItem(itemId);
        _logger.LogInformation("UseItem result: {Result}", result);

        return result == 0;
    }

    public bool UseItem(uint dataId, uint itemId)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = gameObject;
            long result = AgentInventoryContext.Instance()->UseItem(itemId);

            _logger.LogInformation("UseItem result on {DataId}: {Result}", dataId, result);
            return result == 0;
        }

        return false;
    }

    public bool UseItemOnGround(uint dataId, uint itemId)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            var position = (FFXIVClientStructs.FFXIV.Common.Math.Vector3)gameObject.Position;
            return ActionManager.Instance()->UseActionLocation(ActionType.KeyItem, itemId, location: &position);
        }

        return false;
    }

    public void UseEmote(uint dataId, EEmote emote)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = gameObject;
            ExecuteCommand($"{_emoteCommands[emote]} motion");
        }
    }

    public void UseEmote(EEmote emote)
    {
        ExecuteCommand($"{_emoteCommands[emote]} motion");
    }

    public bool IsObjectAtPosition(uint dataId, Vector3 position, float distance)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
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

        return HasCharacterStatusPreventingMountOrSprint();
    }

    public bool HasStatusPreventingSprint() => HasCharacterStatusPreventingMountOrSprint();

    private bool HasCharacterStatusPreventingMountOrSprint()
    {
        var gameObject = GameObjectManager.GetGameObjectByIndex(0);
        if (gameObject != null && gameObject->ObjectKind == 1)
        {
            var battleChara = (BattleChara*)gameObject;
            StatusManager* statusManager = battleChara->GetStatusManager;
            return statusManager->HasStatus(565) || statusManager->HasStatus(404) || statusManager->HasStatus(2730);
        }

        return false;
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
            _logger.LogInformation("Unmounting...");
            return ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
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
                _dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets2.Quest>()!.GetRow((uint)currentQuest.QuestId +
                    0x10000);
            if (questRow == null)
            {
                _logger.LogError("Could not find quest row for {QuestId}", currentQuest.QuestId);
                return null;
            }

            excelSheetName = $"quest/{(currentQuest.QuestId / 100):000}/{questRow.Id}";
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
        if (IsLoadingScreenVisible())
            return true;

        return _condition[ConditionFlag.Occupied] || _condition[ConditionFlag.Occupied30] ||
               _condition[ConditionFlag.Occupied33] || _condition[ConditionFlag.Occupied38] ||
               _condition[ConditionFlag.Occupied39] || _condition[ConditionFlag.OccupiedInEvent] ||
               _condition[ConditionFlag.OccupiedInQuestEvent] || _condition[ConditionFlag.OccupiedInCutSceneEvent] ||
               _condition[ConditionFlag.Casting] || _condition[ConditionFlag.Unknown57] ||
               _condition[ConditionFlag.BetweenAreas] || _condition[ConditionFlag.BetweenAreas51] ||
               _condition[ConditionFlag.Jumping61];
    }

    public bool IsLoadingScreenVisible()
    {
        return _gameGui.TryGetAddonByName("FadeMiddle", out AtkUnitBase* fade) &&
               LAddon.IsAddonReady(fade) &&
               fade->IsVisible;
    }
}
