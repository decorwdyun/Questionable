using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

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
    };
}
