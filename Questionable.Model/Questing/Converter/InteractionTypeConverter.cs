using System.Collections.Generic;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing.Converter;

public sealed class InteractionTypeConverter() : EnumConverter<EInteractionType>(Values)
{
    private static readonly Dictionary<EInteractionType, string> Values = new()
    {
        { EInteractionType.Interact, "Interact" },
        { EInteractionType.WalkTo, "WalkTo" },
        { EInteractionType.AttuneAethernetShard, "AttuneAethernetShard" },
        { EInteractionType.AttuneAetheryte, "AttuneAetheryte" },
        { EInteractionType.AttuneAetherCurrent, "AttuneAetherCurrent" },
        { EInteractionType.Combat, "Combat" },
        { EInteractionType.UseItem, "UseItem" },
        { EInteractionType.EquipItem, "EquipItem" },
        { EInteractionType.Say, "Say" },
        { EInteractionType.Emote, "Emote" },
        { EInteractionType.Action, "Action" },
        { EInteractionType.WaitForObjectAtPosition, "WaitForNpcAtPosition" },
        { EInteractionType.WaitForManualProgress, "WaitForManualProgress" },
        { EInteractionType.Duty, "Duty" },
        { EInteractionType.SinglePlayerDuty, "SinglePlayerDuty" },
        { EInteractionType.Jump, "Jump" },
        { EInteractionType.Dive, "Dive" },
        { EInteractionType.Instruction, "Instruction" },
        { EInteractionType.AcceptQuest, "AcceptQuest" },
        { EInteractionType.CompleteQuest, "CompleteQuest" },
    };
}
