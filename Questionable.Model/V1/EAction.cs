using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(ActionConverter))]
public enum EAction
{
    Cure = 120,
    Esuna = 7568,
    Physick = 190,
    Buffet = 4931,
    Fumigate = 5872,
    SiphonSnout = 18187,
    RedGulal = 29382,
    YellowGulal = 29383,
    BlueGulal = 29384,
}

public static class EActionExtensions
{
    public static bool RequiresMount(this EAction action)
    {
        return action
            is EAction.Buffet
            or EAction.Fumigate
            or EAction.SiphonSnout
            or EAction.RedGulal
            or EAction.YellowGulal
            or EAction.BlueGulal;
    }
}
