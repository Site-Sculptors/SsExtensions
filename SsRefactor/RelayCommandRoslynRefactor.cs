using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SsRefactor
{
    public static class RelayCommandRoslynRefactor
    {
        // Given the document text and selection span, returns the updated text with Command replaced by RelayCommand
        public static async Task<string> RefactorCommandToRelayCommandAsync(string documentText, int selectionStart, int selectionLength)
        {
            var tree = CSharpSyntaxTree.ParseText(documentText);
            var root = await tree.GetRootAsync();
            var span = new TextSpan(selectionStart, selectionLength);

            // Find the node at the selection
            var node = root.FindNode(span, getInnermostNodeForTie: true);
            var classNode = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classNode == null)
                return documentText;

            // Find all field/property/assignment related to ICommand/Command
            // 1. If selection is a property, get its backing field and assignment
            // 2. If selection is an assignment, get the field and property

            // Find all ICommand fields
            var iCommandFields = classNode.Members.OfType<FieldDeclarationSyntax>()
                .Where(f => f.Declaration.Type.ToString().Contains("ICommand"))
                .ToList();

            // Find all ICommand properties
            var iCommandProperties = classNode.Members.OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Type.ToString().Contains("ICommand"))
                .ToList();

            // Find all assignments in constructors
            var constructors = classNode.Members.OfType<ConstructorDeclarationSyntax>();
            var assignments = constructors
                .SelectMany(ctor => ctor.Body?.Statements.OfType<ExpressionStatementSyntax>() ?? Enumerable.Empty<ExpressionStatementSyntax>())
                .Select(stmt => stmt.Expression as AssignmentExpressionSyntax)
                .Where(assign => assign != null && assign.Right.ToString().Contains("new Command"))
                .ToList();

            // Replace 'new Command' with 'new RelayCommand' in assignments
            var newRoot = root;
            foreach (var assign in assignments)
            {
                var newRight = SyntaxFactory.ParseExpression(assign.Right.ToString().Replace("new Command", "new RelayCommand"));
                newRoot = newRoot.ReplaceNode(assign.Right, newRight);
            }

            // Replace 'new Command' with 'new RelayCommand' in property initializers
            foreach (var prop in iCommandProperties)
            {
                if (prop.ExpressionBody != null && prop.ExpressionBody.Expression.ToString().Contains("new Command"))
                {
                    var newExpr = SyntaxFactory.ParseExpression(prop.ExpressionBody.Expression.ToString().Replace("new Command", "new RelayCommand"));
                    var newProp = prop.WithExpressionBody(prop.ExpressionBody.WithExpression(newExpr));
                    newRoot = newRoot.ReplaceNode(prop, newProp);
                }
            }

            return newRoot.ToFullString();
        }
    }
}
