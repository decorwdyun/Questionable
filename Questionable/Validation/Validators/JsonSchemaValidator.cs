using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Json.Schema;
using Questionable.Model;
using Questionable.QuestPaths;

namespace Questionable.Validation.Validators;

internal sealed class JsonSchemaValidator : IQuestValidator
{
    private readonly Dictionary<ushort, JsonNode> _questNodes = new();
    private JsonSchema? _questSchema;

    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        _questSchema ??= JsonSchema.FromStream(AssemblyQuestLoader.QuestSchema).AsTask().Result;

        if (_questNodes.TryGetValue(quest.QuestId, out JsonNode? questNode))
        {
            var evaluationResult = _questSchema.Evaluate(questNode, new EvaluationOptions
            {
                Culture = CultureInfo.InvariantCulture,
                OutputFormat = OutputFormat.List
            });
            if (!evaluationResult.IsValid)
            {
                yield return new ValidationIssue
                {
                    QuestId = quest.QuestId,
                    Sequence = null,
                    Step = null,
                    Severity = EIssueSeverity.Error,
                    Description = "JSON Validation failed"
                };
            }
        }

    }

    public void Enqueue(ushort questId, JsonNode questNode) => _questNodes[questId] = questNode;

    public void Reset() => _questNodes.Clear();
}
