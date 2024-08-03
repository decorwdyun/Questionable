using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

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
    public float? CombatDelaySecondsAtStart { get; set; }

    public JumpDestination? JumpDestination { get; set; }
    public uint? ContentFinderConditionId { get; set; }
    public SkipConditions? SkipConditions { get; set; }

    public List<List<QuestWorkValue>?> RequiredQuestVariables { get; set; } = new();
    public List<GatheredItem> RequiredGatheredItems { get; set; } = [];
    public IList<QuestWorkValue?> CompletionQuestVariablesFlags { get; set; } = new List<QuestWorkValue?>();
    public IList<DialogueChoice> DialogueChoices { get; set; } = new List<DialogueChoice>();
    public IList<uint> PointMenuChoices { get; set; } = new List<uint>();

    // TODO: Not implemented
    [JsonConverter(typeof(IdConverter))]
    public IId? PickUpQuestId { get; set; }

    [JsonConverter(typeof(IdConverter))]
    public IId? TurnInQuestId { get; set; }

    [JsonConverter(typeof(IdConverter))]
    public IId? NextQuestId { get; set; }

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
