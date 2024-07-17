using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Questionable.Model;

namespace Questionable.Validation;

internal sealed class QuestValidator
{
    private readonly IReadOnlyList<IQuestValidator> _validators;
    private readonly ILogger<QuestValidator> _logger;

    private List<ValidationIssue> _validationIssues = new();

    public QuestValidator(IEnumerable<IQuestValidator> validators, ILogger<QuestValidator> logger)
    {
        _validators = validators.ToList();
        _logger = logger;

        _logger.LogInformation("Validators: {Validators}",
            string.Join(", ", _validators.Select(x => x.GetType().Name)));
    }

    public IReadOnlyList<ValidationIssue> Issues => _validationIssues;
    public int IssueCount => _validationIssues.Count;
    public int ErrorCount => _validationIssues.Count(x => x.Severity == EIssueSeverity.Error);

    public void ClearIssues() => _validationIssues.Clear();

    public void Validate(IEnumerable<Quest> quests)
    {
        Task.Run(() =>
        {
            foreach (var quest in quests)
            {
                foreach (var validator in _validators)
                {
                    foreach (var issue in validator.Validate(quest))
                    {
                        var level = issue.Severity == EIssueSeverity.Error ? LogLevel.Warning : LogLevel.Information;
                        _logger.Log(level,
                            "Validation failed: {QuestId} ({QuestName}) / {QuestSequence} / {QuestStep} - {Description}",
                            issue.QuestId, quest.Info.Name, issue.Sequence, issue.Step, issue.Description);
                        _validationIssues.Add(issue);
                    }
                }
            }

            _validationIssues = _validationIssues.OrderBy(x => x.QuestId)
                .ThenBy(x => x.Sequence)
                .ThenBy(x => x.Step)
                .ThenBy(x => x.Description)
                .ToList();
        });
    }

    public void AddIssue(ValidationIssue issue) => _validationIssues.Add(issue);
}
