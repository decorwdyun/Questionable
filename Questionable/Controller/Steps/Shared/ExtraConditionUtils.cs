using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Microsoft.Extensions.Logging;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal sealed class ExtraConditionUtils
{
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly ILogger<ExtraConditionUtils> _logger;

    public ExtraConditionUtils(
        Configuration configuration,
        IClientState clientState,
        ILogger<ExtraConditionUtils> logger)
    {
        _configuration = configuration;
        _clientState = clientState;
        _logger = logger;
    }

    public bool MatchesExtraCondition(EExtraSkipCondition skipCondition)
    {
        var position = _clientState.LocalPlayer?.Position;
        return position != null &&
               _clientState.TerritoryType != 0 &&
               MatchesExtraCondition(skipCondition, position.Value, _clientState.TerritoryType);
    }

    public bool MatchesExtraCondition(EExtraSkipCondition skipCondition, Vector3 position, ushort territoryType)
    {
        return skipCondition switch
        {
            EExtraSkipCondition.WakingSandsMainArea => territoryType == 212 && position.X < 24,
            EExtraSkipCondition.WakingSandsSolar => territoryType == 212 && position.X >= 24,
            EExtraSkipCondition.RisingStonesSolar => territoryType == 351 && position.Z <= -28,
            EExtraSkipCondition.RoguesGuild => territoryType == 129 && position.Y <= -115,
            EExtraSkipCondition.NotRoguesGuild => territoryType == 129 && position.Y > -115,
            EExtraSkipCondition.DockStorehouse => territoryType == 137 && position.Y <= -20,
            EExtraSkipCondition.SkipFreeFantasia => ShouldSkipFreeFantasia(),
            _ => throw new ArgumentOutOfRangeException(nameof(skipCondition), skipCondition, null)
        };
    }

    private unsafe bool ShouldSkipFreeFantasia()
    {
        if (!_configuration.General.PickUpFreeFantasia)
        {
            _logger.LogInformation("Skipping fantasia step, as free fantasia is disabled in the configuration");
            return true;
        }

        bool foundFestival = false;
        for (int i = 0; i < GameMain.Instance()->ActiveFestivals.Length; ++i)
        {
            if (GameMain.Instance()->ActiveFestivals[i].Id == 160)
            {
                foundFestival = true;
                break;
            }
        }

        if (!foundFestival)
        {
            _logger.LogInformation("Skipping fantasia step, as free fantasia moogle is not available");
            return true;
        }

        UIState* uiState = UIState.Instance();
        if (uiState != null && uiState->IsUnlockLinkUnlocked(505))
        {
            _logger.LogInformation("Already picked up free fantasia");
            return true;
        }

        return false;
    }
}
