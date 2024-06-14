using System;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;

namespace Questionable.Model.V1;

internal static class QuestStepExtensions
{
    /// <summary>
    /// Positive values: Must be set to this value; will wait for the step to have these set.
    /// Negative values: Will skip if set to this value, won't wait for this to be set.
    /// </summary>
    public static unsafe bool MatchesQuestVariables(this QuestStep step, QuestWork questWork, bool forSkip)
    {
        if (step.CompletionQuestVariablesFlags.Count != 6)
            return false;

        for (int i = 0; i < 6; ++i)
        {
            short? check = step.CompletionQuestVariablesFlags[i];
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
}
