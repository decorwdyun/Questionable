using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class ActionConverter() : EnumConverter<EAction>(Values)
{
    private static readonly Dictionary<EAction, string> Values = new()
    {
        { EAction.Esuna, "Esuna" },
    };
}
