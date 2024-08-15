using System.Collections.Generic;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing.Converter;

public sealed class EmoteConverter() : EnumConverter<EEmote>(Values)
{
    private static readonly Dictionary<EEmote, string> Values = new()
    {
        { EEmote.Stretch, "stretch" },
        { EEmote.Wave, "wave" },
        { EEmote.Rally, "rally" },
        { EEmote.Deny, "deny" },
        { EEmote.Pray, "pray" },
        { EEmote.Slap, "slap" },
        { EEmote.Doubt, "doubt" },
        { EEmote.Psych, "psych" },
        { EEmote.Cheer, "cheer" },
        { EEmote.Happy, "happy" },
        { EEmote.Poke, "poke" },
        { EEmote.Flex, "flex" },
        { EEmote.Soothe, "soothe" },
        { EEmote.Me, "me" },
        { EEmote.Welcome, "welcome" },
        { EEmote.ImperialSalute, "imperialsalute" },
        { EEmote.Pet, "pet" },
        { EEmote.Dance, "dance" },
        { EEmote.Respect, "respect" },
        { EEmote.Lookout, "lookout" },
        { EEmote.Kneel, "kneel" },
        { EEmote.Bow, "bow" },
        { EEmote.Uchiwasshoi, "uchiwasshoi" },
        { EEmote.Clap, "clap" },
        { EEmote.VictoryPose, "victorypose" },
    };
}
