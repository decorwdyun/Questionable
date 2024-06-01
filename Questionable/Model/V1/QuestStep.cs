using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public class QuestStep
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
    public bool Fly { get; set; }
    public string? Comment { get; set; }

    public EAetheryteLocation? AetheryteShortcut { get; set; }

    public AethernetShortcut? AethernetShortcut { get; set; }
    public uint? AetherCurrentId { get; set; }

    public uint? ItemId { get; set; }
    public bool? GroundTarget { get; set; }

    public EEmote? Emote { get; set; }
    public string? ChatMessage { get; set; }

    public EEnemySpawnType? EnemySpawnType { get; set; }

    public IList<uint> KillEnemyDataIds { get; set; } = new List<uint>();
    public JumpDestination? JumpDestination { get; set; }
    public uint? ContentFinderConditionId { get; set; }

    public IList<ESkipCondition> SkipIf { get; set; } = new List<ESkipCondition>();
}
