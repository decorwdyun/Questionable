using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Model.V1;

namespace Questionable.Controller.Utils;

internal static class QuestWorkUtils
{
    public static bool HasCompletionFlags(IList<short?> completionQuestVariablesFlags)
    {
        return completionQuestVariablesFlags.Count == 6 && completionQuestVariablesFlags.Any(x => x != null);
    }

    /// <summary>
    /// Positive values: Must be set to this value; will wait for the step to have these set.
    /// Negative values: Will skip if set to this value, won't wait for this to be set.
    /// </summary>
    public static bool MatchesQuestWork(IList<short?> completionQuestVariablesFlags, QuestWork questWork, bool forSkip)
    {
        if (!HasCompletionFlags(completionQuestVariablesFlags))
            return false;

        for (int i = 0; i < 6; ++i)
        {
            short? check = completionQuestVariablesFlags[i];
            if (check == null)
                continue;

            byte actualValue = questWork.Variables[i];
            byte checkByte = check > 0 ? (byte)check : (byte)-check;
            if (forSkip)
            {
                byte expectedValue = (byte)Math.Abs(check.Value);
                if ((actualValue & checkByte) != expectedValue)
                    return false;
            }
            else if (!forSkip && check > 0)
            {
                byte expectedValue = check > 0 ? (byte)check : (byte)0;
                if ((actualValue & checkByte) != expectedValue)
                    return false;
            }
        }

        return true;
    }

    public static bool MatchesRequiredQuestWorkConfig(List<List<QuestWorkValue>?> requiredQuestVariables,
        QuestWork questWork, ILogger<SkipCondition.CheckSkip> logger)
    {
        if (requiredQuestVariables.Count != 6 || requiredQuestVariables.All(x => x == null || x.Count == 0))
        {
            logger.LogInformation("No RQW defined");
            return true;
        }

        for (int i = 0; i < 6; ++i)
        {
            if (requiredQuestVariables[i] == null)
            {
                logger.LogInformation("No RQW {Index} defined", i);
                continue;
            }

            byte high = (byte)(questWork.Variables[i] >> 4);
            byte low = (byte)(questWork.Variables[i] & 0xF);

            foreach (QuestWorkValue expectedValue in requiredQuestVariables[i]!)
            {
                logger.LogInformation("H: {ExpectedHigh} - {ActualHigh}, L: {ExpectedLow} - {ActualLow}",
                    expectedValue.High, high, expectedValue.Low, low);
                if (expectedValue.High != null && expectedValue.High != high)
                    continue;

                if (expectedValue.Low != null && expectedValue.Low != low)
                    continue;

                return true;
            }
        }

        logger.LogInformation("Should execute step");
        return false;
    }
}
