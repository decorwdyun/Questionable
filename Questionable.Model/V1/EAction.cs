using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(ActionConverter))]
public enum EAction
{
    Esuna = 7568,
    RedGulal = 29382,
    YellowGulal = 29383,
    BlueGulal = 29384,
}

public static class EActionExtensions
{
    public static bool RequiresMount(this EAction action)
    {
        return action is EAction.RedGulal or EAction.YellowGulal or EAction.BlueGulal;
    }
}
