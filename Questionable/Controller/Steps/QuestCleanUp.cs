using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal static class QuestCleanUp
{
    private static readonly Dictionary<ushort, MountConfiguration> AlliedSocietyMountConfiguration = new()
    {
        { 66, new(1016093, EAetheryteLocation.SeaOfCloudsOkZundu) },
        { 79, new(1017031, EAetheryteLocation.DravanianForelandsAnyxTrine) },
        { 369, new(1051798, EAetheryteLocation.KozamaukaDockPoga) },
    };

    internal sealed class CheckAlliedSocietyMount(GameFunctions gameFunctions, AetheryteData aetheryteData, ILogger<CheckAlliedSocietyMount> logger) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (sequence.Sequence == 0)
                return null;

            // if you are on a allied society mount
            if (gameFunctions.GetMountId() is { } mountId &&
                AlliedSocietyMountConfiguration.TryGetValue(mountId, out var mountConfiguration))
            {
                logger.LogInformation("We are on a known allied society mount with id = {MountId}", mountId);

                // it doesn't particularly matter if we teleport to the same aetheryte twice in the same quest step, as
                // the second (normal) teleport instance should detect that we're within range and not do anything
                var targetAetheryte = step.AetheryteShortcut ?? mountConfiguration.ClosestAetheryte;
                var teleportTask = new AetheryteShortcut.Task(null, quest.Id, targetAetheryte, aetheryteData.TerritoryIds[targetAetheryte]);

                // turn-in step can never be done while mounted on an allied society mount
                if (sequence.Sequence == 255)
                {
                    logger.LogInformation("Mount can't be used to finish quest, teleporting to {Aetheryte}", mountConfiguration.ClosestAetheryte);
                    return teleportTask;
                }

                // if the quest uses no mount actions, that's not a mount quest
                if (!quest.AllSteps().Any(x => x.Step.Action is { } action && action.RequiresMount()))
                {
                    logger.LogInformation("Quest doesn't use any mount actions, teleporting to {Aetheryte}", mountConfiguration.ClosestAetheryte);
                    return teleportTask;
                }

                // have any of the previous sequences interacted with the issuer?
                var previousSequences =
                    quest.AllSequences()
                        .Where(x => x.Sequence > 0 // quest accept doesn't ever put us into a mount
                                    && x.Sequence < sequence.Sequence)
                        .ToList();
                if (previousSequences.SelectMany(x => x.Steps).All(x => x.DataId != mountConfiguration.IssuerDataId))
                {
                    // this quest hasn't given us a mount yet
                    logger.LogInformation("Haven't talked to mount NPC for this allied society quest; {Aetheryte}", mountConfiguration.ClosestAetheryte);
                    return teleportTask;
                }
            }

            return null;
        }
    }

    private sealed record MountConfiguration(uint IssuerDataId, EAetheryteLocation ClosestAetheryte);
}
