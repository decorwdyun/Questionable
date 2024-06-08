﻿using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

internal sealed class InteractionTypeConverter() : EnumConverter<EInteractionType>(Values)
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
        { EInteractionType.Say, "Say" },
        { EInteractionType.Emote, "Emote" },
        { EInteractionType.WaitForObjectAtPosition, "WaitForNpcAtPosition" },
        { EInteractionType.WaitForManualProgress, "WaitForManualProgress" },
        { EInteractionType.Duty, "Duty" },
        { EInteractionType.SinglePlayerDuty, "SinglePlayerDuty" },
        { EInteractionType.Jump, "Jump" },
        { EInteractionType.ShouldBeAJump, "ShouldBeAJump" },
        { EInteractionType.Instruction, "Instruction" },
    };
}
