using System;
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
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.V1;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;

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

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly ICondition _condition;
    private readonly IPluginLog _pluginLog;

    public GameFunctions(IDataManager dataManager, IObjectTable objectTable, ISigScanner sigScanner,
        ITargetManager targetManager, ICondition condition, IPluginLog pluginLog)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _condition = condition;
        _pluginLog = pluginLog;
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
    }

    public (ushort CurrentQuest, byte Sequence) GetCurrentQuest()
    {
        var scenarioTree = AgentScenarioTree.Instance();
        if (scenarioTree == null)
        {
            //ImGui.Text("Scenario tree is null.");
            return (0, 0);
        }

        if (scenarioTree->Data == null)
        {
            //ImGui.Text("Scenario tree data is null.");
            return (0, 0);
        }

        uint currentQuest = scenarioTree->Data->CurrentScenarioQuest;
        if (currentQuest == 0)
        {
            //ImGui.Text("Current quest is 0.");
            return (0, 0);
        }

        //ImGui.Text($"Current Quest: {currentQuest}");
        //ImGui.Text($"Progress: {QuestManager.GetQuestSequence(currentQuest)}");
        return ((ushort)currentQuest, QuestManager.GetQuestSequence(currentQuest));
    }

    public bool IsAetheryteUnlocked(uint aetheryteId, out byte subIndex)
    {
        var telepo = Telepo.Instance();
        if (telepo == null || telepo->UpdateAetheryteList() == null)
        {
            subIndex = 0;
            return false;
        }

        for (ulong i = 0; i < telepo->TeleportList.Size(); ++i)
        {
            var data = telepo->TeleportList.Get(i);
            if (data.AetheryteId == aetheryteId)
            {
                subIndex = data.SubIndex;
                return true;
            }
        }

        subIndex = 0;
        return false;
    }

    public bool IsAetheryteUnlocked(EAetheryteLocation aetheryteLocation)
        => IsAetheryteUnlocked((uint)aetheryteLocation, out _);

    public bool TeleportAetheryte(uint aetheryteId)
    {
        var status = ActionManager.Instance()->GetActionStatus(ActionType.Action, 5);
        if (status != 0)
            return false;

        if (IsAetheryteUnlocked(aetheryteId, out var subIndex))
        {
            return Telepo.Instance()->Teleport(aetheryteId, subIndex);
        }

        return false;
    }

    public bool TeleportAetheryte(EAetheryteLocation aetheryteLocation)
        => TeleportAetheryte((uint)aetheryteLocation);

    public bool IsFlyingUnlocked(ushort territoryId)
    {
        var playerState = PlayerState.Instance();
        return playerState != null &&
               _territoryToAetherCurrentCompFlgSet.TryGetValue(territoryId, out byte aetherCurrentCompFlgSet) &&
               playerState->IsAetherCurrentZoneComplete(aetherCurrentCompFlgSet);
    }

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

    private GameObject? FindObjectByDataId(uint dataId)
    {
        foreach (var gameObject in _objectTable)
        {
            if (gameObject.DataId == dataId)
            {
                return gameObject;
            }
        }

        return null;
    }

    public void InteractWith(uint dataId)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = null;
            _targetManager.Target = gameObject;

            TargetSystem.Instance()->InteractWithObject(
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address, false);
        }
    }

    public void UseItem(uint dataId, uint itemId)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = gameObject;
            AgentInventoryContext.Instance()->UseItem(itemId);
        }
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

    public bool IsObbjectAtPosition(uint dataId, Vector3 position)
    {
        GameObject? gameObject = FindObjectByDataId(dataId);
        return gameObject != null && (gameObject.Position - position).Length() < 0.05f;
    }

    public bool HasStatusPreventingSprintOrMount()
    {
        var gameObject = GameObjectManager.GetGameObjectByIndex(0);
        if (gameObject != null && gameObject->ObjectKind == 1)
        {
            var battleChara = (BattleChara*)gameObject;
            StatusManager* statusManager = battleChara->GetStatusManager;
            return statusManager->HasStatus(565);
        }

        return false;
    }

    public bool Unmount()
    {
        if (_condition[ConditionFlag.Mounted])
        {
            if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 23) == 0)
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);

            return true;
        }

        return false;
    }
}
