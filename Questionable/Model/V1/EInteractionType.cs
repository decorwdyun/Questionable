using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(InteractionTypeConverter))]
public enum EInteractionType
{
    Interact,
    WalkTo,
    AttuneAethernetShard,
    AttuneAetheryte,
    AttuneAetherCurrent,
    Combat,
    UseItem,
    Say,
    Emote,
    WaitForObjectAtPosition,
    WaitForManualProgress,
    Duty,
    SinglePlayerDuty,

    Jump,

    /// <summary>
    /// Needs to be manually continued.
    /// </summary>
    Instruction,
}
