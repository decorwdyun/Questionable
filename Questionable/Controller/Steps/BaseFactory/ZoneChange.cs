using System;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.BaseFactory;

internal static class ZoneChange
{
    internal sealed class Factory : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            return null;
        }
    }

    internal sealed class WaitForZone : ITask
    {
        /* TODO: Unsure when this would evne be needed again, this should probably be moved to AFTER walkTo/interacting

        if (step.TargetTerritoryId.HasValue && step.TerritoryId != step.TargetTerritoryId &&
            step.TargetTerritoryId == _clientState.TerritoryType)
        {
            // we assume whatever e.g. interaction, walkto etc. we have will trigger the zone transition
            _logger.LogInformation("Zone transition, skipping rest of step");
            IncreaseStepCount();
            return;
        }
         */
        public bool Start() => throw new NotImplementedException();

        public ETaskResult Update() => throw new NotImplementedException();

        public override string ToString() => "WaitForZone";
    }
}
