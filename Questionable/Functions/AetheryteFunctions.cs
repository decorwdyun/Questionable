using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using Action = Lumina.Excel.Sheets.Action;

namespace Questionable.Functions;

internal sealed unsafe class AetheryteFunctions
{
    private const uint TeleportAction = 5;
    private const uint ReturnAction = 6;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AetheryteFunctions> _logger;
    private readonly IDataManager _dataManager;

    public AetheryteFunctions(IServiceProvider serviceProvider, ILogger<AetheryteFunctions> logger,
        IDataManager dataManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dataManager = dataManager;
    }

    public DateTime ReturnRequestedAt { get; set; } = DateTime.MinValue;

    public bool IsAetheryteUnlocked(uint aetheryteId, out byte subIndex)
    {
        subIndex = 0;

        var uiState = UIState.Instance();
        return uiState != null && uiState->IsAetheryteUnlocked(aetheryteId);
    }

    public bool IsAetheryteUnlocked(EAetheryteLocation aetheryteLocation)
    {
        if (aetheryteLocation.IsFirmamentAetheryte())
            return _serviceProvider.GetRequiredService<QuestFunctions>().IsQuestComplete(new QuestId(3672));
        return IsAetheryteUnlocked((uint)aetheryteLocation, out _);
    }

    public bool CanTeleport(EAetheryteLocation aetheryteLocation)
    {
        if ((ushort)aetheryteLocation == PlayerState.Instance()->HomeAetheryteId &&
            ActionManager.Instance()->GetActionStatus(ActionType.Action, ReturnAction) == 0)
            return true;

        return ActionManager.Instance()->GetActionStatus(ActionType.Action, TeleportAction) == 0;
    }

    public bool IsTeleportUnlocked()
    {
        uint unlockLink = _dataManager.GetExcelSheet<Action>()
            .GetRow(5)
            .UnlockLink
            .RowId;
        return UIState.Instance()->IsUnlockLinkUnlocked(unlockLink);
    }

    public bool TeleportAetheryte(uint aetheryteId)
    {
        _logger.LogDebug("Attempting to teleport to aetheryte {AetheryteId}", aetheryteId);
        if (IsAetheryteUnlocked(aetheryteId, out var subIndex))
        {
            if (aetheryteId == PlayerState.Instance()->HomeAetheryteId &&
                ActionManager.Instance()->GetActionStatus(ActionType.Action, ReturnAction) == 0)
            {
                ReturnRequestedAt = DateTime.Now;
                if (ActionManager.Instance()->UseAction(ActionType.Action, ReturnAction))
                {
                    _logger.LogInformation("Using 'return' for home aetheryte");
                    return true;
                }
            }

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, TeleportAction) == 0)
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
}
