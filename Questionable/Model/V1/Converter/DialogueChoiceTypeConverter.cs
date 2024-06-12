using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

internal sealed class DialogueChoiceTypeConverter() : EnumConverter<EDialogChoiceType>(Values)
{
    private static readonly Dictionary<EDialogChoiceType, string> Values = new()
    {
        { EDialogChoiceType.YesNo, "YesNo" },
        { EDialogChoiceType.List, "List" },
    };
}
