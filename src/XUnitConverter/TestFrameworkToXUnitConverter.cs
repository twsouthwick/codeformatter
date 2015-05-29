// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitConverter
{
    public abstract class TestFrameworkToXUnitConverter : ConverterBase
    {
        protected abstract ICollection<string> TestNamespaces { get; }

        protected abstract string TestMethodName { get; }

        protected abstract IEnumerable<string> AttributesToRemove { get; }

        protected override async Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var root = syntaxNode as CompilationUnitSyntax;

            if (root == null)
            {
                return document.Project.Solution;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var result = Process(semanticModel, new CompilationUnitSyntaxConverter(root as CompilationUnitSyntax));

            return document.WithSyntaxRoot(result.Node).Project.Solution;
        }

        private IConverterSyntax Process(SemanticModel semanticModel, IConverterSyntax usings)
        {
            var newUsings = new List<UsingDirectiveSyntax>();
            bool needsChanges = false;

            foreach (var usingSyntax in usings.Usings)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(usingSyntax.Name);
                if (symbolInfo.Symbol != null)
                {
                    string namespaceDocID = symbolInfo.Symbol.GetDocumentationCommentId();
                    if (TestNamespaces.Contains(namespaceDocID))
                    {
                        needsChanges = true;
                    }
                    else
                    {
                        newUsings.Add(usingSyntax);
                    }
                }
                else
                {
                    newUsings.Add(usingSyntax);
                }
            }

            // Iterate through all members and process any namespace declarations
            foreach (var member in usings.Members)
            {
                if (member.IsKind(SyntaxKind.NamespaceDeclaration))
                {
                    var result = Process(semanticModel, new NamespaceSyntaxConverter(member as NamespaceDeclarationSyntax));

                    usings = usings.ReplaceNode(member, result.Node);
                }
            }

            if (!needsChanges)
            {
                return usings;
            }

            TransformationTracker transformationTracker = new TransformationTracker();
            RemoveAttributes(usings.Node, semanticModel, transformationTracker);
            ChangeTestMethodAttributesToFact(usings.Node, semanticModel, transformationTracker);
            ChangeAssertCalls(usings.Node, semanticModel, transformationTracker);
            usings = transformationTracker.TransformRoot(usings);

            //  Remove compiler directives before the first member of the file (e.g. an #endif after the using statements)
            var firstMember = usings.Members.FirstOrDefault();
            if (firstMember != null)
            {
                if (firstMember.HasLeadingTrivia)
                {
                    var newLeadingTrivia = firstMember.GetLeadingTrivia();
                    usings = usings.ReplaceNode(firstMember, firstMember.WithLeadingTrivia(newLeadingTrivia));
                }
            }

            //  Apply trivia from original last using statement to new last using statement
            var lastUsing = usings.Usings.Last();
            var xUnitUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Xunit"))
                .NormalizeWhitespace()
                .WithTrailingTrivia(lastUsing.GetTrailingTrivia())
                .WithLeadingTrivia(lastUsing.GetLeadingTrivia());

            newUsings.Add(xUnitUsing);

            return usings.WithUsings(SyntaxFactory.List(newUsings));
        }
        
        private void RemoveAttributes(SyntaxNode root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            foreach (var attribute in AttributesToRemove)
            {
                RemoveTestAttributes(root, semanticModel, transformationTracker, attribute);
            }
        }

        private void RemoveTestAttributes(SyntaxNode root, SemanticModel semanticModel, TransformationTracker transformationTracker, string attributeName)
        {
            List<AttributeSyntax> nodesToRemove = new List<AttributeSyntax>();

            foreach (var attributeListSyntax in root.DescendantNodes().OfType<AttributeListSyntax>())
            {
                var attributesToRemove = attributeListSyntax.Attributes.Where(attributeSyntax =>
                {
                    var typeInfo = semanticModel.GetTypeInfo(attributeSyntax);
                    if (typeInfo.Type != null)
                    {
                        string attributeTypeDocID = typeInfo.Type.GetDocumentationCommentId();
                        if (IsTestNamespaceType(attributeTypeDocID, attributeName))
                        {
                            return true;
                        }
                    }
                    return false;
                }).ToList();

                nodesToRemove.AddRange(attributesToRemove);
            }

            transformationTracker.AddTransformation(nodesToRemove, (transformationRoot, rewrittenNodes, originalNodeMap) =>
            {
                foreach (AttributeSyntax rewrittenNode in rewrittenNodes)
                {
                    var attributeListSyntax = (AttributeListSyntax)rewrittenNode.Parent;
                    var newSyntaxList = attributeListSyntax.Attributes.Remove(rewrittenNode);
                    if (newSyntaxList.Any())
                    {
                        transformationRoot = transformationRoot.ReplaceNode(attributeListSyntax, attributeListSyntax.WithAttributes(newSyntaxList));
                    }
                    else
                    {
                        transformationRoot = transformationRoot.RemoveNode(attributeListSyntax, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                }
                return transformationRoot;
            });
        }

        private void ChangeTestMethodAttributesToFact(SyntaxNode root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            List<AttributeSyntax> nodesToReplace = new List<AttributeSyntax>();

            foreach (var attributeSyntax in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var typeInfo = semanticModel.GetTypeInfo(attributeSyntax);
                if (typeInfo.Type != null)
                {
                    string attributeTypeDocID = typeInfo.Type.GetDocumentationCommentId();
                    if (IsTestNamespaceType(attributeTypeDocID, TestMethodName))
                    {
                        nodesToReplace.Add(attributeSyntax);
                    }
                }
            }

            transformationTracker.AddTransformation(nodesToReplace, (transformationRoot, rewrittenNodes, originalNodeMap) =>
            {
                return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                {
                    return ((AttributeSyntax)rewrittenNode).WithName(SyntaxFactory.ParseName("Fact")).NormalizeWhitespace();
                });
            });
        }

        private void ChangeAssertCalls(SyntaxNode root, SemanticModel semanticModel, TransformationTracker transformationTracker)
        {
            Dictionary<string, string> assertMethodsToRename = new Dictionary<string, string>()
            {
                { "AreEqual", "Equal" },
                { "AreNotEqual", "NotEqual" },
                { "IsNull", "Null" },
                { "IsNotNull", "NotNull" },
                { "AreSame", "Same" },
                { "AreNotSame", "NotSame" },
                { "IsTrue", "True" },
                { "IsFalse", "False" },
                { "IsInstanceOfType", "IsAssignableFrom" },
            };

            Dictionary<SimpleNameSyntax, string> nameReplacementsForNodes = new Dictionary<SimpleNameSyntax, string>();
            List<InvocationExpressionSyntax> methodCallsToReverseArguments = new List<InvocationExpressionSyntax>();

            foreach (var methodCallSyntax in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var expressionSyntax = methodCallSyntax.Expression;
                var expressionTypeInfo = semanticModel.GetTypeInfo(expressionSyntax);
                if (expressionTypeInfo.Type != null)
                {
                    string expressionDocID = expressionTypeInfo.Type.GetDocumentationCommentId();
                    if (IsTestNamespaceType(expressionDocID, "Assert"))
                    {
                        string newMethodName;
                        if (assertMethodsToRename.TryGetValue(methodCallSyntax.Name.Identifier.Text, out newMethodName))
                        {
                            nameReplacementsForNodes.Add(methodCallSyntax.Name, newMethodName);

                            if (newMethodName == "IsAssignableFrom" && methodCallSyntax.Parent is InvocationExpressionSyntax)
                            {
                                //  Parameter order is reversed between MSTest Assert.IsInstanceOfType and xUnit Assert.IsAssignableFrom
                                methodCallsToReverseArguments.Add((InvocationExpressionSyntax)methodCallSyntax.Parent);
                            }
                        }
                    }
                }
            }

            if (nameReplacementsForNodes.Any())
            {
                transformationTracker.AddTransformation(nameReplacementsForNodes.Keys, (transformationRoot, rewrittenNodes, originalNodeMap) =>
                {
                    return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                    {
                        var realOriginalNode = (SimpleNameSyntax)originalNodeMap[originalNode];
                        string newName = nameReplacementsForNodes[realOriginalNode];
                        return SyntaxFactory.ParseName(newName);
                    });
                });

                transformationTracker.AddTransformation(methodCallsToReverseArguments, (transformationRoot, rewrittenNodes, originalNodeMap) =>
                {
                    return transformationRoot.ReplaceNodes(rewrittenNodes, (originalNode, rewrittenNode) =>
                    {
                        var invocationExpression = (InvocationExpressionSyntax)rewrittenNode;
                        var oldArguments = invocationExpression.ArgumentList.Arguments;
                        var newArguments = new SeparatedSyntaxList<ArgumentSyntax>().AddRange(new[] { oldArguments[1], oldArguments[0] });

                        return invocationExpression.WithArgumentList(invocationExpression.ArgumentList.WithArguments(newArguments));
                    });
                });
            }
        }

        private bool IsTestNamespaceType(string docID, string simpleTypeName)
        {
            if (docID == null)
            {
                return false;
            }

            int lastPeriod = docID.LastIndexOf('.');
            if (lastPeriod < 0)
            {
                return false;
            }

            string simpleTypeNameFromDocID = docID.Substring(lastPeriod + 1);
            if (simpleTypeNameFromDocID != simpleTypeName)
            {
                return false;
            }

            string namespaceDocID = "N" + docID.Substring(1, lastPeriod - 1);
            return TestNamespaces.Contains(namespaceDocID);
        }

        private class TransformationTracker
        {
            private Dictionary<SyntaxAnnotation, Func<IConverterSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, IConverterSyntax>> _annotationToTransformation = new Dictionary<SyntaxAnnotation, Func<IConverterSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, IConverterSyntax>>();
            private Dictionary<SyntaxNode, List<SyntaxAnnotation>> _nodeToAnnotations = new Dictionary<SyntaxNode, List<SyntaxAnnotation>>();
            private Dictionary<SyntaxAnnotation, SyntaxNode> _originalNodeLookup = new Dictionary<SyntaxAnnotation, SyntaxNode>();

            public void AddTransformation(IEnumerable<SyntaxNode> nodesToTransform, Func<IConverterSyntax, IEnumerable<SyntaxNode>, Dictionary<SyntaxNode, SyntaxNode>, IConverterSyntax> transformerFunc)
            {
                var annotation = new SyntaxAnnotation();
                _annotationToTransformation[annotation] = transformerFunc;

                foreach (var node in nodesToTransform)
                {
                    List<SyntaxAnnotation> annotationsForNode;
                    if (!_nodeToAnnotations.TryGetValue(node, out annotationsForNode))
                    {
                        annotationsForNode = new List<SyntaxAnnotation>();
                        _nodeToAnnotations[node] = annotationsForNode;
                    }
                    annotationsForNode.Add(annotation);

                    var originalNodeAnnotation = new SyntaxAnnotation();
                    _originalNodeLookup[originalNodeAnnotation] = node;
                    annotationsForNode.Add(originalNodeAnnotation);
                }
            }

            public IConverterSyntax TransformRoot(IConverterSyntax usings)
            {
                usings = usings.ReplaceNodes(_nodeToAnnotations.Keys, (originalNode, rewrittenNode) =>
                {
                    var ret = rewrittenNode.WithAdditionalAnnotations(_nodeToAnnotations[originalNode]);

                    return ret;
                });

                foreach (var kvp in _annotationToTransformation)
                {
                    Dictionary<SyntaxNode, SyntaxNode> originalNodeMap = new Dictionary<SyntaxNode, SyntaxNode>();
                    foreach (var originalNodeKvp in _originalNodeLookup)
                    {
                        var annotatedNodes = usings.GetAnnotatedNodes(originalNodeKvp.Key).ToList();
                        SyntaxNode annotatedNode = annotatedNodes.SingleOrDefault();
                        if (annotatedNode != null)
                        {
                            originalNodeMap[annotatedNode] = originalNodeKvp.Value;
                        }
                    }

                    var syntaxAnnotation = kvp.Key;
                    var transformation = kvp.Value;
                    var nodesToTransform = usings.GetAnnotatedNodes(syntaxAnnotation);
                    usings = transformation(usings, nodesToTransform, originalNodeMap);
                }

                return usings;
            }
        }
    }
}
