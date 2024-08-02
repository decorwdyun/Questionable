using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

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
    EquipItem,
    Say,
    Emote,
    Action,
    WaitForObjectAtPosition,
    WaitForManualProgress,
    Duty,
    SinglePlayerDuty,
    Jump,
    Dive,

    /// <summary>
    /// Needs to be manually continued.
    /// </summary>
    Instruction,

    AcceptQuest,
    CompleteQuest,
}
