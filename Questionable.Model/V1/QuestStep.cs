using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public sealed class QuestStep
{
    public const float DefaultStopDistance = 3f;

    public EInteractionType InteractionType { get; set; }

    public uint? DataId { get; set; }

    [JsonConverter(typeof(VectorConverter))]
    public Vector3? Position { get; set; }

    public float? StopDistance { get; set; }
    public float? NpcWaitDistance { get; set; }
    public ushort TerritoryId { get; set; }
    public ushort? TargetTerritoryId { get; set; }
    public float? DelaySecondsAtStart { get; set; }

    public bool Disabled { get; set; }
    public bool DisableNavmesh { get; set; }
    public bool? Mount { get; set; }
    public bool? Fly { get; set; }
    public bool? Land { get; set; }
    public bool? Sprint { get; set; }
    public bool? IgnoreDistanceToObject { get; set; }
    public string? Comment { get; set; }

    /// <summary>
    /// Only used when attuning to an aetheryte.
    /// </summary>
    public EAetheryteLocation? Aetheryte { get; set; }

    /// <summary>
    /// Only used when attuning to an aethernet shard.
    /// </summary>
    [JsonConverter(typeof(AethernetShardConverter))]
    public EAetheryteLocation? AethernetShard { get; set; }

    public EAetheryteLocation? AetheryteShortcut { get; set; }

    public AethernetShortcut? AethernetShortcut { get; set; }
    public uint? AetherCurrentId { get; set; }

    public uint? ItemId { get; set; }
    public bool? GroundTarget { get; set; }

    public EEmote? Emote { get; set; }
    public ChatMessage? ChatMessage { get; set; }
    public EAction? Action { get; set; }

    public EEnemySpawnType? EnemySpawnType { get; set; }
    public IList<uint> KillEnemyDataIds { get; set; } = new List<uint>();
    public IList<ComplexCombatData> ComplexCombatData { get; set; } = new List<ComplexCombatData>();

    public JumpDestination? JumpDestination { get; set; }
    public uint? ContentFinderConditionId { get; set; }

    public IList<ESkipCondition> SkipIf { get; set; } = new List<ESkipCondition>();
    public List<List<QuestWorkValue>?> RequiredQuestVariables { get; set; } = new();
    public IList<short?> CompletionQuestVariablesFlags { get; set; } = new List<short?>();
    public IList<DialogueChoice> DialogueChoices { get; set; } = new List<DialogueChoice>();
    public IList<uint> PointMenuChoices { get; set; } = new List<uint>();

    // TODO: Not implemented
    public ushort? PickUpQuestId { get; set; }

    public ushort? TurnInQuestId { get; set; }
    public ushort? NextQuestId { get; set; }

    [JsonConstructor]
    public QuestStep()
    {
    }

    public QuestStep(EInteractionType interactionType, uint? dataId, Vector3? position, ushort territoryId)
    {
        InteractionType = interactionType;
        DataId = dataId;
        Position = position;
        TerritoryId = territoryId;
    }

    public float CalculateActualStopDistance()
    {
        if (InteractionType == EInteractionType.WalkTo)
            return StopDistance ?? 0.25f;
        if (InteractionType == EInteractionType.AttuneAetheryte)
            return StopDistance ?? 10f;
        else
            return StopDistance ?? DefaultStopDistance;
    }
}
