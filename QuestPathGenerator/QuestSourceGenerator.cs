using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Questionable.Model.V1;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Questionable.QuestPathGenerator.RoslynShortcuts;

namespace Questionable.QuestPathGenerator;

/// <summary>
/// A sample source generator that creates C# classes based on the text file (in this case, Domain Driven Design ubiquitous language registry).
/// When using a simple text file as a baseline, we can create a non-incremental source generator.
/// </summary>
[Generator]
[SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008")]
public class QuestSourceGenerator : ISourceGenerator
{
    private static readonly DiagnosticDescriptor InvalidJson = new("QSG0001",
        "Invalid JSON",
        "Invalid quest file {0}",
        nameof(QuestSourceGenerator),
        DiagnosticSeverity.Error,
        true);

    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required for this generator.
    }

    public void Execute(GeneratorExecutionContext context)
    {
        List<(ushort, QuestRoot)> quests = [];

        // Find schema definition
        AdditionalText jsonSchemaFile =
            context.AdditionalFiles.Single(x => Path.GetFileName(x.Path) == "quest-v1.json");
        var questSchema = JsonSchema.FromText(jsonSchemaFile.GetText()!.ToString());

        // Go through all files marked as an Additional File in file properties.
        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile == null || additionalFile == jsonSchemaFile)
                continue;

            if (Path.GetExtension(additionalFile.Path) != ".json")
                continue;

            string name = Path.GetFileName(additionalFile.Path);
            if (!name.Contains('_'))
                continue;

            ushort id = ushort.Parse(name.Substring(0, name.IndexOf('_')));

            var text = additionalFile.GetText();
            if (text == null)
                continue;

            var questNode = JsonNode.Parse(text.ToString());
            var evaluationResult = questSchema.Evaluate(questNode, new EvaluationOptions
            {
                Culture = CultureInfo.InvariantCulture,
                OutputFormat = OutputFormat.List
            });
            if (!evaluationResult.IsValid)
            {
                var error = Diagnostic.Create(InvalidJson,
                    null,
                    Path.GetFileName(additionalFile.Path));
                context.ReportDiagnostic(error);
            }

            var quest = questNode.Deserialize<QuestRoot>()!;
            if (quest.Disabled)
            {
                quest.Author = [];
                quest.QuestSequence = [];
                quest.TerritoryBlacklist = [];
            }

            quests.Add((id, quest));
        }

        if (quests.Count == 0)
            return;

        var partitionedQuests = quests
            .OrderBy(x => x.Item1)
            .GroupBy(x => $"LoadQuests{x.Item1 / 50}")
            .ToList();

        List<MethodDeclarationSyntax> methods =
        [
            MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier("LoadQuests"))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PrivateKeyword),
                        Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    Block(
                        partitionedQuests
                            .Select(x =>
                                ExpressionStatement(
                                    InvocationExpression(
                                        IdentifierName(x.Key))))))
        ];

        foreach (var partition in partitionedQuests)
        {
            methods.Add(MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier(partition.Key))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PrivateKeyword),
                        Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    Block(CreateInitializer(partition.ToList()))));
        }

        var code =
            CompilationUnit()
                .WithUsings(
                    List(
                        new[]
                        {
                            UsingDirective(
                                IdentifierName("System")),
                            UsingDirective(
                                QualifiedName(
                                    IdentifierName("System"),
                                    IdentifierName("Numerics"))),
                            UsingDirective(
                                QualifiedName(
                                    IdentifierName("System"),
                                    IdentifierName("IO"))),
                            UsingDirective(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"), IdentifierName("Collections")),
                                    IdentifierName("Generic"))),
                            UsingDirective(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("Questionable"),
                                        IdentifierName("Model")),
                                    IdentifierName("V1")))
                        }))
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        FileScopedNamespaceDeclaration(
                                QualifiedName(
                                    IdentifierName("Questionable"),
                                    IdentifierName("QuestPaths")))
                            .WithMembers(
                                SingletonList<MemberDeclarationSyntax>(
                                    ClassDeclaration("AssemblyQuestLoader")
                                        .WithModifiers(
                                            TokenList(Token(SyntaxKind.PartialKeyword)))
                                        .WithMembers(List<MemberDeclarationSyntax>(methods))))))
                .NormalizeWhitespace();

        // Add the source code to the compilation.
        context.AddSource("AssemblyQuestLoader.g.cs", code.ToFullString());
    }

    private static StatementSyntax[] CreateInitializer(List<(ushort QuestId, QuestRoot Root)> quests)
    {
        List<StatementSyntax> statements = [];

        foreach (var quest in quests)
        {
            statements.Add(
                ExpressionStatement(
                    InvocationExpression(
                            IdentifierName("AddQuest"))
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]
                                    {
                                        Argument(
                                            LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                                Literal(quest.QuestId))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(CreateQuestRootExpression(quest.QuestId, quest.Root))
                                    })))));
        }

        return statements.ToArray();
    }

    private static ObjectCreationExpressionSyntax CreateQuestRootExpression(ushort questId, QuestRoot quest)
    {
        try
        {
            return ObjectCreationExpression(
                    IdentifierName(nameof(QuestRoot)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                AssignmentList(nameof(QuestRoot.Author), quest.Author)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(QuestRoot.Comment), quest.Comment, null)
                                    .AsSyntaxNodeOrToken(),
                                AssignmentList(nameof(QuestRoot.TerritoryBlacklist),
                                    quest.TerritoryBlacklist).AsSyntaxNodeOrToken(),
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(nameof(QuestRoot.QuestSequence)),
                                    CreateQuestSequence(quest.QuestSequence))))));
        }
        catch (Exception e)
        {
            throw new Exception($"QuestGen[{questId}]: {e.Message}", e);
        }
    }

    private static ExpressionSyntax CreateQuestSequence(List<QuestSequence> sequences)
    {
        return CollectionExpression(
            SeparatedList<CollectionElementSyntax>(
                sequences.SelectMany(sequence => new SyntaxNodeOrToken[]
                {
                    ExpressionElement(
                        ObjectCreationExpression(
                                IdentifierName(nameof(QuestSequence)))
                            .WithInitializer(
                                InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SeparatedList<ExpressionSyntax>(
                                        SyntaxNodeList(
                                            Assignment<int?>(nameof(QuestSequence.Sequence), sequence.Sequence, null)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestSequence.Comment), sequence.Comment, null)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                IdentifierName(nameof(QuestSequence.Steps)),
                                                CreateQuestSteps(sequence.Steps))))))),
                    Token(SyntaxKind.CommaToken),
                }.ToArray())));
    }

    private static ExpressionSyntax CreateQuestSteps(List<QuestStep> steps)
    {
        QuestStep emptyStep = new();
        return CollectionExpression(
            SeparatedList<CollectionElementSyntax>(
                steps.SelectMany(step => new SyntaxNodeOrToken[]
                {
                    ExpressionElement(
                        ObjectCreationExpression(
                                IdentifierName(nameof(QuestStep)))
                            .WithArgumentList(
                                ArgumentList(
                                    SeparatedList<ArgumentSyntax>(
                                        new SyntaxNodeOrToken[]
                                        {
                                            Argument(LiteralValue(step.InteractionType)),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(LiteralValue(step.DataId)),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(LiteralValue(step.Position)),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(LiteralValue(step.TerritoryId))
                                        })))
                            .WithInitializer(
                                InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SeparatedList<ExpressionSyntax>(
                                        SyntaxNodeList(
                                            Assignment(nameof(QuestStep.StopDistance), step.StopDistance,
                                                    emptyStep.StopDistance)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.NpcWaitDistance), step.NpcWaitDistance,
                                                    emptyStep.NpcWaitDistance)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.TargetTerritoryId), step.TargetTerritoryId,
                                                    emptyStep.TargetTerritoryId)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.DelaySecondsAtStart), step.DelaySecondsAtStart,
                                                    emptyStep.DelaySecondsAtStart)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Disabled), step.Disabled, emptyStep.Disabled)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.DisableNavmesh), step.DisableNavmesh,
                                                    emptyStep.DisableNavmesh)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Mount), step.Mount, emptyStep.Mount)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Fly), step.Fly, emptyStep.Fly)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Land), step.Land, emptyStep.Land)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Sprint), step.Sprint, emptyStep.Sprint)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.IgnoreDistanceToObject),
                                                    step.IgnoreDistanceToObject, emptyStep.IgnoreDistanceToObject)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Comment), step.Comment, emptyStep.Comment)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Aetheryte), step.Aetheryte, emptyStep.Aetheryte)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.AethernetShard), step.AethernetShard,
                                                    emptyStep.AethernetShard)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.AetheryteShortcut), step.AetheryteShortcut,
                                                    emptyStep.AetheryteShortcut)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.AethernetShortcut), step.AethernetShortcut,
                                                    emptyStep.AethernetShortcut)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.AetherCurrentId), step.AetherCurrentId,
                                                    emptyStep.AetherCurrentId)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.ItemId), step.ItemId, emptyStep.ItemId)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.GroundTarget), step.GroundTarget,
                                                    emptyStep.GroundTarget)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Emote), step.Emote, emptyStep.Emote)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.ChatMessage), step.ChatMessage,
                                                    emptyStep.ChatMessage)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.Action), step.Action, emptyStep.Action)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.EnemySpawnType), step.EnemySpawnType,
                                                    emptyStep.EnemySpawnType)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.KillEnemyDataIds), step.KillEnemyDataIds)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.ComplexCombatData), step.ComplexCombatData)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.CombatDelaySecondsAtStart),
                                                    step.CombatDelaySecondsAtStart,
                                                    emptyStep.CombatDelaySecondsAtStart)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.JumpDestination), step.JumpDestination,
                                                    emptyStep.JumpDestination)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.ContentFinderConditionId),
                                                    step.ContentFinderConditionId, emptyStep.ContentFinderConditionId)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.SkipIf), step.SkipIf)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.RequiredQuestVariables),
                                                    step.RequiredQuestVariables)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.CompletionQuestVariablesFlags),
                                                    step.CompletionQuestVariablesFlags)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.DialogueChoices), step.DialogueChoices)
                                                .AsSyntaxNodeOrToken(),
                                            AssignmentList(nameof(QuestStep.PointMenuChoices), step.PointMenuChoices)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.PickUpQuestId), step.PickUpQuestId,
                                                    emptyStep.PickUpQuestId)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.TurnInQuestId), step.TurnInQuestId,
                                                    emptyStep.TurnInQuestId)
                                                .AsSyntaxNodeOrToken(),
                                            Assignment(nameof(QuestStep.NextQuestId), step.NextQuestId,
                                                    emptyStep.NextQuestId)
                                                .AsSyntaxNodeOrToken()))))),
                    Token(SyntaxKind.CommaToken),
                }.ToArray())));
    }
}
