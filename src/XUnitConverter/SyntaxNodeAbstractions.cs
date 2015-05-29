using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace XUnitConverter
{
    internal interface IConverterSyntax
    {
        SyntaxNode Node { get; }
        SyntaxList<UsingDirectiveSyntax> Usings { get; }
        SyntaxList<MemberDeclarationSyntax> Members { get; }
        SyntaxNode WithUsing(SyntaxList<UsingDirectiveSyntax> usings);
        IEnumerable<SyntaxNode> GetAnnotatedNodes(SyntaxAnnotation annotation);
        IConverterSyntax ReplaceNodes<TNode>(IEnumerable<TNode> nodes, Func<TNode, TNode, SyntaxNode> computeReplacementNode)
            where TNode : SyntaxNode;
        IConverterSyntax ReplaceNode(SyntaxNode oldNode, SyntaxNode newNode);
        IConverterSyntax WithUsings(SyntaxList<UsingDirectiveSyntax> usings);
        IConverterSyntax RemoveNode(SyntaxNode node, SyntaxRemoveOptions options);
    }

    internal class NamespaceSyntaxConverter : IConverterSyntax
    {
        private NamespaceDeclarationSyntax _node;

        public NamespaceSyntaxConverter(NamespaceDeclarationSyntax namespaceDeclarationSyntax)
        {
            _node = namespaceDeclarationSyntax;
        }

        public SyntaxNode Node => _node;

        public SyntaxList<UsingDirectiveSyntax> Usings => _node.Usings;

        public SyntaxList<MemberDeclarationSyntax> Members => _node.Members;

        public SyntaxNode WithUsing(SyntaxList<UsingDirectiveSyntax> usings)
        {
            return _node.WithUsings(usings);
        }

        public IConverterSyntax ReplaceNodes<TNode>(IEnumerable<TNode> nodes, Func<TNode, TNode, SyntaxNode> computeReplacementNode) where TNode : SyntaxNode
        {
            return new NamespaceSyntaxConverter(_node.ReplaceNodes(nodes, computeReplacementNode));
        }

        public IEnumerable<SyntaxNode> GetAnnotatedNodes(SyntaxAnnotation annotation)
        {
            return _node.GetAnnotatedNodes(annotation);
        }

        public IConverterSyntax WithUsings(SyntaxList<UsingDirectiveSyntax> usings)
        {
            return new NamespaceSyntaxConverter(_node.WithUsings(usings));
        }

        public IConverterSyntax ReplaceNode(SyntaxNode oldNode, SyntaxNode newNode)
        {
            return new NamespaceSyntaxConverter(_node.ReplaceNode(oldNode, newNode));
        }

        public IConverterSyntax RemoveNode(SyntaxNode node, SyntaxRemoveOptions options)
        {
            return new NamespaceSyntaxConverter(_node.RemoveNode(node, options));
        }
    }

    internal class CompilationUnitSyntaxConverter : IConverterSyntax
    {
        private readonly CompilationUnitSyntax _node;

        public CompilationUnitSyntaxConverter(CompilationUnitSyntax root)
        {
            _node = root;
        }

        public SyntaxNode Node => _node;

        public SyntaxList<UsingDirectiveSyntax> Usings => _node.Usings;

        public SyntaxList<MemberDeclarationSyntax> Members => _node.Members;

        public SyntaxNode WithUsing(SyntaxList<UsingDirectiveSyntax> usings)
        {
            return _node.WithUsings(usings);
        }

        public IConverterSyntax ReplaceNodes<TNode>(IEnumerable<TNode> nodes, Func<TNode, TNode, SyntaxNode> computeReplacementNode) where TNode : SyntaxNode
        {
            return new CompilationUnitSyntaxConverter(_node.ReplaceNodes(nodes, computeReplacementNode));
        }

        public IEnumerable<SyntaxNode> GetAnnotatedNodes(SyntaxAnnotation annotation)
        {
            return _node.GetAnnotatedNodes(annotation);
        }

        public IConverterSyntax WithUsings(SyntaxList<UsingDirectiveSyntax> usings)
        {
            return new CompilationUnitSyntaxConverter(_node.WithUsings(usings));
        }

        public IConverterSyntax ReplaceNode(SyntaxNode oldNode, SyntaxNode newNode)
        {
            return new CompilationUnitSyntaxConverter(_node.ReplaceNode(oldNode, newNode));
        }

        public IConverterSyntax RemoveNode(SyntaxNode node, SyntaxRemoveOptions options)
        {
            return new CompilationUnitSyntaxConverter(_node.RemoveNode(node, options));
        }
    }
}
