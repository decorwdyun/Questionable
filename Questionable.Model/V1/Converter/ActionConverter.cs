using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class ActionConverter() : EnumConverter<EAction>(Values)
{
    private static readonly Dictionary<EAction, string> Values = new()
    {
        { EAction.Esuna, "Esuna" },
        { EAction.SiphonSnout, "Siphon Snout" },
        { EAction.RedGulal, "Red Gulal" },
        { EAction.YellowGulal, "Yellow Gulal" },
        { EAction.BlueGulal, "Blue Gulal" },
    };
}
