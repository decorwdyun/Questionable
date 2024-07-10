using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(ActionConverter))]
public enum EAction
{
    Esuna = 7568,
}
