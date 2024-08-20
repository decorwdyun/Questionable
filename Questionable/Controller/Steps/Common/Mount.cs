using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Common.Math;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Functions;

namespace Questionable.Controller.Steps.Common;

internal static class Mount
{
    internal sealed class Factory(
        GameFunctions gameFunctions,
        ICondition condition,
        TerritoryData territoryData,
        IClientState clientState,
        ILoggerFactory loggerFactory)
    {
        public ITask Mount(ushort territoryId, EMountIf mountIf, Vector3? position = null)
        {
            if (mountIf == EMountIf.AwayFromPosition)
                ArgumentNullException.ThrowIfNull(position);

            return new MountTask(territoryId, mountIf, position, gameFunctions, condition, territoryData, clientState,
                loggerFactory.CreateLogger<MountTask>());
        }

        public ITask Unmount()
        {
            return new UnmountTask(condition, loggerFactory.CreateLogger<UnmountTask>(), gameFunctions);
        }
    }

    private sealed class MountTask(
        ushort territoryId,
        EMountIf mountIf,
        Vector3? position,
        GameFunctions gameFunctions,
        ICondition condition,
        TerritoryData territoryData,
        IClientState clientState,
        ILogger<MountTask> logger) : ITask
    {
        private bool _mountTriggered;
        private DateTime _retryAt = DateTime.MinValue;

        public bool Start()
        {
            if (condition[ConditionFlag.Mounted])
                return false;

            if (!territoryData.CanUseMount(territoryId))
            {
                logger.LogInformation("Can't use mount in current territory {Id}", territoryId);
                return false;
            }

            if (gameFunctions.HasStatusPreventingMount())
            {
                logger.LogInformation("Can't mount due to status preventing sprint or mount");
                return false;
            }

            if (mountIf == EMountIf.AwayFromPosition)
            {
                Vector3 playerPosition = clientState.LocalPlayer?.Position ?? Vector3.Zero;
                float distance = System.Numerics.Vector3.Distance(playerPosition, position.GetValueOrDefault());
                if (territoryId == clientState.TerritoryType && distance < 30f && !Conditions.IsDiving)
                {
                    logger.LogInformation("Not using mount, as we're close to the target");
                    return false;
                }

                logger.LogInformation(
                    "Want to use mount if away from destination ({Distance} yalms), trying (in territory {Id})...",
                    distance, territoryId);
            }
            else
                logger.LogInformation("Want to use mount, trying (in territory {Id})...", territoryId);

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
    }

    private sealed class UnmountTask(ICondition condition, ILogger<UnmountTask> logger, GameFunctions gameFunctions)
        : ITask
    {
        private bool _unmountTriggered;
        private DateTime _continueAt = DateTime.MinValue;

        public bool Start()
        {
            if (!condition[ConditionFlag.Mounted])
                return false;

            logger.LogInformation("Step explicitly wants no mount, trying to unmount...");
            if (condition[ConditionFlag.InFlight])
            {
                gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(1);
                return true;
            }

            _unmountTriggered = gameFunctions.Unmount();
            _continueAt = DateTime.Now.AddSeconds(1);
            return true;
        }

        public ETaskResult Update()
        {
            if (_continueAt >= DateTime.Now)
                return ETaskResult.StillRunning;

            if (!_unmountTriggered)
            {
                // if still flying, we still need to land
                if (condition[ConditionFlag.InFlight])
                    gameFunctions.Unmount();
                else
                    _unmountTriggered = gameFunctions.Unmount();

                _continueAt = DateTime.Now.AddSeconds(1);
                return ETaskResult.StillRunning;
            }

            if (condition[ConditionFlag.Mounted] && condition[ConditionFlag.InCombat])
            {
                _unmountTriggered = gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(1);
                return ETaskResult.StillRunning;
            }

            return condition[ConditionFlag.Mounted]
                ? ETaskResult.StillRunning
                : ETaskResult.TaskComplete;
        }

        public override string ToString() => "Unmount";
    }

    public enum EMountIf
    {
        Always,
        AwayFromPosition,
    }
}
