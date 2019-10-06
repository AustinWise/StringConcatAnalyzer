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
using System.Reflection;
using Microsoft.CodeAnalysis.Host;

namespace Analyzer1
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Analyzer1CodeFixProvider)), Shared]
    public class Analyzer1CodeFixProvider : CodeFixProvider
    {
        private const string title = "Call ToString(CultureInfo.InvarientCulture)";

        static readonly Func<Document, CancellationToken, Task<Document>> sAddImportsAsync;

        static Analyzer1CodeFixProvider()
        {
            var workspaceModule = typeof(ExtensionOrderAttribute).Module;

            var codeGenType = workspaceModule.GetType("Microsoft.CodeAnalysis.CodeGeneration.ICodeGenerationService", throwOnError: false, ignoreCase: false);
            if (codeGenType == null)
                return;

            var codeGenerationOptionsType = workspaceModule.GetType("Microsoft.CodeAnalysis.CodeGeneration.CodeGenerationOptions", throwOnError: false, ignoreCase: false);
            if (codeGenerationOptionsType == null)
                return;

            var addImportsMethod = codeGenType.GetMethod("AddImportsAsync",
                new Type[] { typeof(Document), codeGenerationOptionsType, typeof(CancellationToken) });
            if (addImportsMethod == null)
                return;

            var defaultProp = codeGenerationOptionsType.GetField("Default", BindingFlags.Static | BindingFlags.Public);
            if (defaultProp == null)
                return;
            object defaultValue = defaultProp.GetValue(null);

            var getServiceFunction = typeof(HostLanguageServices).GetMethod(nameof(HostLanguageServices.GetService));
            if (getServiceFunction == null)
                return;

            var getGenerationService = getServiceFunction.MakeGenericMethod(codeGenType);

            sAddImportsAsync = (doc, ct) =>
            {
                var codeGenService = getGenerationService.Invoke(doc.Project.LanguageServices, null);
                if (codeGenService == null)
                    return Task.FromResult(doc);
                return (Task<Document>)addImportsMethod.Invoke(codeGenService, new object[] { doc, defaultValue, ct });
            };
        }

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

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var cultureInfoType = semanticModel.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);

            //add the ToString(CultureInfo.InvarientCulture)
            var toStringAccess = generator.MemberAccessExpression(conversionNode, "ToString");
            var cultureInfoTypeExpr = generator.TypeExpression(cultureInfoType, addImport: true);
            var invarientCultureExpr = generator.MemberAccessExpression(cultureInfoTypeExpr, "InvariantCulture");
            var toStringInvoke = generator.InvocationExpression(toStringAccess, invarientCultureExpr);
            root = root.ReplaceNode(conversionNode, toStringInvoke);
            document = document.WithSyntaxRoot(root);

            //add using to the top of the file if it is not there already
            if (sAddImportsAsync != null)
                document = await sAddImportsAsync(document, cancellationToken).ConfigureAwait(false);

            return document;
        }
    }
}
