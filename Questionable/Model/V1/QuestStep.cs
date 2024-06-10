using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json.Serialization;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using JetBrains.Annotations;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class QuestStep
{
    public EInteractionType InteractionType { get; set; }

    public uint? DataId { get; set; }

    [JsonConverter(typeof(VectorConverter))]
    public Vector3? Position { get; set; }

    public float? StopDistance { get; set; }
    public ushort TerritoryId { get; set; }
    public ushort? TargetTerritoryId { get; set; }

    public bool Disabled { get; set; }
    public bool DisableNavmesh { get; set; }
    public bool? Mount { get; set; }
    public bool? Fly { get; set; }
    public bool? Sprint { get; set; }
    public string? Comment { get; set; }

    public EAetheryteLocation? AetheryteShortcut { get; set; }

    public AethernetShortcut? AethernetShortcut { get; set; }
    public uint? AetherCurrentId { get; set; }

    public uint? ItemId { get; set; }
    public bool? GroundTarget { get; set; }

    public EEmote? Emote { get; set; }
    public ChatMessage? ChatMessage { get; set; }

    public EEnemySpawnType? EnemySpawnType { get; set; }

    public IList<uint> KillEnemyDataIds { get; set; } = new List<uint>();
    public JumpDestination? JumpDestination { get; set; }
    public uint? ContentFinderConditionId { get; set; }

    public IList<ESkipCondition> SkipIf { get; set; } = new List<ESkipCondition>();
    public IList<short?> CompletionQuestVariablesFlags { get; set; } = new List<short?>();
    public IList<DialogueChoice> DialogueChoices { get; set; } = new List<DialogueChoice>();

    /// <summary>
    /// Positive values: Must be set to this value; will wait for the step to have these set.
    /// Negative values: Will skip if set to this value, won't wait for this to be set.
    /// </summary>
    public unsafe bool MatchesQuestVariables(QuestWork questWork, bool forSkip)
    {
        if (CompletionQuestVariablesFlags.Count != 6)
            return false;

        for (int i = 0; i < 6; ++i)
        {
            short? check = CompletionQuestVariablesFlags[i];
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
