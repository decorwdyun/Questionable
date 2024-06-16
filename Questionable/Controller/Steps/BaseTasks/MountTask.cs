using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Data;

namespace Questionable.Controller.Steps.BaseTasks;

internal sealed class MountTask(
    GameFunctions gameFunctions,
    ICondition condition,
    TerritoryData territoryData,
    IClientState clientState,
    ILogger<MountTask> logger) : ITask
{
    private ushort _territoryId;
    private EMountIf _mountIf;
    private Vector3? _position;

    private bool _mountTriggered;
    private DateTime _retryAt = DateTime.MinValue;

    public ITask With(ushort territoryId, EMountIf mountIf, Vector3? position = null)
    {
        _territoryId = territoryId;
        _mountIf = mountIf;
        _position = position;

        if (_mountIf == EMountIf.AwayFromPosition)
            ArgumentNullException.ThrowIfNull(position);
        return this;
    }

    public bool Start()
    {
        if (condition[ConditionFlag.Mounted])
            return false;

        if (!territoryData.CanUseMount(_territoryId))
        {
            logger.LogInformation("Can't use mount in current territory {Id}", _territoryId);
            return false;
        }

        if (gameFunctions.HasStatusPreventingMount())
        {
            logger.LogInformation("Can't mount due to status preventing sprint or mount");
            return false;
        }

        if (_mountIf == EMountIf.AwayFromPosition)
        {
            Vector3 playerPosition = clientState.LocalPlayer?.Position ?? Vector3.Zero;
            float distance = (playerPosition - _position.GetValueOrDefault()).Length();
            if (_territoryId == clientState.TerritoryType && distance < 30f)
            {
                logger.LogInformation("Not using mount, as we're close to the target");
                return false;
            }

            logger.LogInformation("Want to use mount if away from destination ({Distance} yalms), trying (in territory {Id})...", distance, _territoryId);
        }
        else
            logger.LogInformation("Want to use mount, trying (in territory {Id})...", _territoryId);

        if (!condition[ConditionFlag.InCombat])
        {
            _retryAt = DateTime.Now.AddSeconds(0.5);
            return true;
        }

        return false;
    }

    public ETaskResult Update()
    {
        if (_mountTriggered && !condition[ConditionFlag.Mounted] && DateTime.Now > _retryAt)
        {
            logger.LogInformation("Not mounted, retrying...");
            _mountTriggered = false;
            _retryAt = DateTime.MaxValue;
        }

        if (!_mountTriggered)
        {
            if (gameFunctions.HasStatusPreventingMount())
            {
                logger.LogInformation("Can't mount due to status preventing sprint or mount");
                return ETaskResult.TaskComplete;
            }

            _mountTriggered = gameFunctions.Mount();
            _retryAt = DateTime.Now.AddSeconds(5);
            return ETaskResult.StillRunning;
        }

        return condition[ConditionFlag.Mounted]
            ? ETaskResult.TaskComplete
            : ETaskResult.StillRunning;
    }

    public override string ToString() => "Mount";

    public enum EMountIf
    {
        Always,
        AwayFromPosition,
    }
}
