using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(EnemySpawnTypeConverter))]
internal enum EEnemySpawnType
{
    None = 0,
    AfterInteraction,
    AfterItemUse,
    AutoOnEnterArea,
    OverworldEnemies,
}
