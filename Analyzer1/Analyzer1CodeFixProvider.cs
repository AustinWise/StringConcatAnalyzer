using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Editing;

namespace Analyzer1
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Analyzer1CodeFixProvider)), Shared]
    public class Analyzer1CodeFixProvider : CodeFixProvider
    {
        private const string title = "Call ToString(CultureInfo.InvarientCulture)";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(Analyzer1Analyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return Task.CompletedTask;
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => MakeUppercaseAsync(context.Document, context.Span, c),
                    equivalenceKey: title),
                diagnostic);

            return Task.CompletedTask;
        }

        private async Task<Document> MakeUppercaseAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var conversionNode = root.FindNode(span, getInnermostNodeForTie: true);
            if (conversionNode == null)
            {
                return document;
            }

            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;

            var cultureInfoType = editor.SemanticModel.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");

            var toStringAccess = generator.MemberAccessExpression(conversionNode, "ToString");
            var cultureInfoTypeExpr = generator.TypeExpression(cultureInfoType, addImport: true);
            var invarientCultureExpr = generator.MemberAccessExpression(cultureInfoTypeExpr, "InvariantCulture");
            var toStringInvoke = generator.InvocationExpression(toStringAccess, invarientCultureExpr);

            editor.ReplaceNode(conversionNode, toStringInvoke);

            return editor.GetChangedDocument();
        }
    }
}
