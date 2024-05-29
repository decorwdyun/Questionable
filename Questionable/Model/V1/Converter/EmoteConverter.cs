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
    };

}
