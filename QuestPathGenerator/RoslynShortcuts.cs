using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Questionable.Model.Common;
using Questionable.Model.Gathering;
using Questionable.Model.Questing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Questionable.QuestPathGenerator;

public static class RoslynShortcuts
{
    public static IEnumerable<SyntaxNodeOrToken> SyntaxNodeList(params SyntaxNodeOrToken?[] nodes)
    {
        nodes = nodes.Where(x => x != null && x.Value.RawKind != 0).ToArray();
        if (nodes.Length == 0)
            return [];

        List<SyntaxNodeOrToken> list = new();
        for (int i = 0; i < nodes.Length; ++i)
        {
            if (i > 0)
                list.Add(Token(SyntaxKind.CommaToken));
            list.Add(nodes[i].GetValueOrDefault());
        }

        return list;
    }

    public static ExpressionSyntax LiteralValue<T>(T? value)
    {
        try
        {
            if (value is string s)
                return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s));
            else if (value is bool b)
                return LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
            else if (value is short i16)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i16));
            else if (value is int i32)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i32));
            else if (value is byte u8)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(u8));
            else if (value is ushort u16)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(u16));
            else if (value is uint u32)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(u32));
            else if (value is float f)
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(f));
            else if (value != null && value.GetType().IsEnum)
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(value.GetType().Name),
                    IdentifierName(value.GetType().GetEnumName(value)!));
            else if (value is QuestId questId)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(QuestId)))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(LiteralValue(questId.Value)))));
            }
            else if (value is LeveId leveId)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(LeveId)))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(LiteralValue(leveId.Value)))));
            }
            else if (value is SatisfactionSupplyNpcId satisfactionSupplyNpcId)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(SatisfactionSupplyNpcId)))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(LiteralValue(satisfactionSupplyNpcId.Value)))));
            }
            else if (value is Vector3 vector)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(Vector3)))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]
                                {
                                    Argument(LiteralValue(vector.X)),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(LiteralValue(vector.Y)),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(LiteralValue(vector.Z))
                                })));
            }
            else if (value is AethernetShortcut aethernetShortcut)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(AethernetShortcut)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment<EAetheryteLocation?>(nameof(AethernetShortcut.From),
                                            aethernetShortcut.From,
                                            null)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment<EAetheryteLocation?>(nameof(AethernetShortcut.To), aethernetShortcut.To,
                                            null)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is ChatMessage chatMessage)
            {
                ChatMessage emptyMessage = new();
                return ObjectCreationExpression(
                        IdentifierName(nameof(ChatMessage)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(ChatMessage.ExcelSheet), chatMessage.ExcelSheet,
                                            emptyMessage.ExcelSheet)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(ChatMessage.Key), chatMessage.Key,
                                            emptyMessage.Key)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is DialogueChoice dialogueChoice)
            {
                DialogueChoice emptyChoice = new();
                return ObjectCreationExpression(
                        IdentifierName(nameof(DialogueChoice)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment<EDialogChoiceType?>(nameof(DialogueChoice.Type), dialogueChoice.Type,
                                            null)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.ExcelSheet), dialogueChoice.ExcelSheet,
                                            emptyChoice.ExcelSheet)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.Prompt), dialogueChoice.Prompt, emptyChoice.Prompt)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.Yes), dialogueChoice.Yes, emptyChoice.Yes)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.Answer), dialogueChoice.Answer, emptyChoice.Answer)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.AnswerIsRegularExpression),
                                            dialogueChoice.AnswerIsRegularExpression,
                                            emptyChoice.AnswerIsRegularExpression)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(DialogueChoice.DataId), dialogueChoice.DataId, emptyChoice.DataId)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is JumpDestination jumpDestination)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(JumpDestination)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment<Vector3?>(nameof(JumpDestination.Position), jumpDestination.Position,
                                            null)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(JumpDestination.StopDistance), jumpDestination.StopDistance, null)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(JumpDestination.DelaySeconds), jumpDestination.DelaySeconds, null)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(JumpDestination.Type), jumpDestination.Type, default)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is ExcelRef excelRef)
            {
                if (excelRef.Type == ExcelRef.EType.Key)
                {
                    return ObjectCreationExpression(
                            IdentifierName(nameof(ExcelRef)))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(LiteralValue(excelRef.AsKey())))));
                }
                else if (excelRef.Type == ExcelRef.EType.RowId)
                {
                    return ObjectCreationExpression(
                            IdentifierName(nameof(ExcelRef)))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(LiteralValue(excelRef.AsRowId())))));
                }
                else
                    throw new Exception($"Unsupported ExcelRef type {excelRef.Type}");
            }
            else if (value is ComplexCombatData complexCombatData)
            {
                var emptyData = new ComplexCombatData();
                return ObjectCreationExpression(
                        IdentifierName(nameof(ComplexCombatData)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(ComplexCombatData.DataId), complexCombatData.DataId,
                                            emptyData.DataId)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(ComplexCombatData.MinimumKillCount),
                                            complexCombatData.MinimumKillCount, emptyData.MinimumKillCount)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(ComplexCombatData.RewardItemId), complexCombatData.RewardItemId,
                                            emptyData.RewardItemId)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(ComplexCombatData.RewardItemCount),
                                            complexCombatData.RewardItemCount,
                                            emptyData.RewardItemCount)
                                        .AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(ComplexCombatData.CompletionQuestVariablesFlags),
                                        complexCombatData.CompletionQuestVariablesFlags),
                                    Assignment(nameof(ComplexCombatData.IgnoreQuestMarker),
                                            complexCombatData.IgnoreQuestMarker,
                                            emptyData.IgnoreQuestMarker)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is QuestWorkValue qwv)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(QuestWorkValue)))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]
                                {
                                    Argument(LiteralValue(qwv.High)),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(LiteralValue(qwv.Low)),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(LiteralValue(qwv.Mode))
                                })));
            }
            else if (value is List<QuestWorkValue> list)
            {
                return CollectionExpression(
                    SeparatedList<CollectionElementSyntax>(
                        SyntaxNodeList(list.Select(x => ExpressionElement(
                            LiteralValue(x)).AsSyntaxNodeOrToken()).ToArray())));
            }
            else if (value is SkipConditions skipConditions)
            {
                var emptySkip = new SkipConditions();
                return ObjectCreationExpression(
                        IdentifierName(nameof(SkipConditions)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(SkipConditions.StepIf), skipConditions.StepIf, emptySkip.StepIf)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipConditions.AetheryteShortcutIf),
                                            skipConditions.AetheryteShortcutIf, emptySkip.AetheryteShortcutIf)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(skipConditions.AethernetShortcutIf),
                                            skipConditions.AethernetShortcutIf, emptySkip.AethernetShortcutIf)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is SkipStepConditions skipStepConditions)
            {
                var emptyStep = new SkipStepConditions();
                return ObjectCreationExpression(
                        IdentifierName(nameof(SkipStepConditions)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(SkipStepConditions.Never), skipStepConditions.Never,
                                            emptyStep.Never)
                                        .AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(SkipStepConditions.CompletionQuestVariablesFlags),
                                            skipStepConditions.CompletionQuestVariablesFlags)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipStepConditions.Flying), skipStepConditions.Flying,
                                            emptyStep.Flying)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipStepConditions.Chocobo), skipStepConditions.Chocobo,
                                            emptyStep.Chocobo)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipStepConditions.NotTargetable),
                                            skipStepConditions.NotTargetable, emptyStep.NotTargetable)
                                        .AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(SkipStepConditions.InTerritory),
                                        skipStepConditions.InTerritory).AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(SkipStepConditions.NotInTerritory),
                                        skipStepConditions.NotInTerritory).AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipStepConditions.Item), skipStepConditions.Item, emptyStep.Item)
                                        .AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(SkipStepConditions.QuestsAccepted),
                                        skipStepConditions.QuestsAccepted).AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(SkipStepConditions.QuestsCompleted),
                                        skipStepConditions.QuestsCompleted).AsSyntaxNodeOrToken(),
                                    Assignment(nameof(SkipStepConditions.ExtraCondition),
                                            skipStepConditions.ExtraCondition, emptyStep.ExtraCondition)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is SkipItemConditions skipItemCondition)
            {
                var emptyItem = new SkipItemConditions();
                return ObjectCreationExpression(
                        IdentifierName(nameof(SkipItemConditions)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(SkipItemConditions.NotInInventory),
                                        skipItemCondition.NotInInventory,
                                        emptyItem.NotInInventory)))));
            }
            else if (value is SkipAetheryteCondition skipAetheryteCondition)
            {
                var emptyAetheryte = new SkipAetheryteCondition();
                return ObjectCreationExpression(
                        IdentifierName(nameof(SkipAetheryteCondition)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(SkipAetheryteCondition.Never), skipAetheryteCondition.Never,
                                        emptyAetheryte.Never),
                                    Assignment(nameof(SkipAetheryteCondition.InSameTerritory),
                                        skipAetheryteCondition.InSameTerritory, emptyAetheryte.InSameTerritory),
                                    AssignmentList(nameof(SkipAetheryteCondition.InTerritory),
                                        skipAetheryteCondition.InTerritory)))));
            }
            else if (value is GatheredItem gatheredItem)
            {
                var emptyItem = new GatheredItem();
                return ObjectCreationExpression(
                        IdentifierName(nameof(GatheredItem)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(GatheredItem.ItemId), gatheredItem.ItemId, emptyItem.ItemId)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheredItem.ItemCount), gatheredItem.ItemCount,
                                            emptyItem.ItemCount)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheredItem.Collectability), gatheredItem.Collectability,
                                            emptyItem.Collectability)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheredItem.ClassJob), gatheredItem.ClassJob,
                                            emptyItem.ClassJob)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is GatheringNodeGroup nodeGroup)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(GatheringNodeGroup)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    AssignmentList(nameof(GatheringNodeGroup.Nodes), nodeGroup.Nodes)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is GatheringNode nodeLocation)
            {
                var emptyLocation = new GatheringNode();
                return ObjectCreationExpression(
                        IdentifierName(nameof(GatheringNode)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(GatheringNode.DataId), nodeLocation.DataId,
                                            emptyLocation.DataId)
                                        .AsSyntaxNodeOrToken(),
                                    AssignmentList(nameof(GatheringNode.Locations), nodeLocation.Locations)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is GatheringLocation location)
            {
                var emptyLocation = new GatheringLocation();
                return ObjectCreationExpression(
                        IdentifierName(nameof(GatheringLocation)))
                    .WithInitializer(
                        InitializerExpression(
                            SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(
                                SyntaxNodeList(
                                    Assignment(nameof(GatheringLocation.Position), location.Position,
                                        emptyLocation.Position).AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheringLocation.MinimumAngle), location.MinimumAngle,
                                        emptyLocation.MinimumAngle).AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheringLocation.MaximumAngle), location.MaximumAngle,
                                        emptyLocation.MaximumAngle).AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheringLocation.MinimumDistance),
                                            location.MinimumDistance, emptyLocation.MinimumDistance)
                                        .AsSyntaxNodeOrToken(),
                                    Assignment(nameof(GatheringLocation.MaximumDistance),
                                            location.MaximumDistance, emptyLocation.MaximumDistance)
                                        .AsSyntaxNodeOrToken()))));
            }
            else if (value is null)
                return LiteralExpression(SyntaxKind.NullLiteralExpression);
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to handle literal [{value}]: {e.StackTrace}", e);
        }

        throw new Exception($"Unsupported data type {value.GetType()} = {value}");
    }

    public static AssignmentExpressionSyntax? Assignment<T>(string name, T? value, T? defaultValue)
    {
        try
        {
            if (value == null && defaultValue == null)
                return null;

            if (value != null && defaultValue != null && value.Equals(defaultValue))
                return null;

            return AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(name),
                LiteralValue(value));
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to handle assignment [{name}]: {e.Message}", e);
        }
    }

    public static AssignmentExpressionSyntax? AssignmentList<T>(string name, IEnumerable<T>? value)
    {
        try
        {
            if (value == null)
                return null;

            IEnumerable<T> list = value.ToList();
            if (!list.Any())
                return null;

            return AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(name),
                CollectionExpression(
                    SeparatedList<CollectionElementSyntax>(
                        SyntaxNodeList(list.Select(x => ExpressionElement(
                            LiteralValue(x)).AsSyntaxNodeOrToken()).ToArray())
                    )));
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to handle list [{name}]: {e.StackTrace}", e);
        }
    }

    public static SyntaxNodeOrToken? AsSyntaxNodeOrToken(this SyntaxNode? node)
    {
        if (node == null)
            return null;

        return node;
    }
}
