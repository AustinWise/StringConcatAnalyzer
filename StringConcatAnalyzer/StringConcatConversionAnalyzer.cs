using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace StringConcatAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringConcatConversionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "StringConcatConversion";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(csaContext =>
            {
                var iformatProviderType = csaContext.Compilation.GetTypeByMetadataName("System.IFormatProvider");
                var cultureInfoType = csaContext.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");
                var invariantCultureProperty = cultureInfoType?.GetMembers("InvariantCulture").OfType<IPropertySymbol>().FirstOrDefault();
                if (iformatProviderType == null || cultureInfoType == null || invariantCultureProperty == null)
                {
                    return;
                }

                var objectType = csaContext.Compilation.GetSpecialType(SpecialType.System_Object);
                var stringType = csaContext.Compilation.GetSpecialType(SpecialType.System_String);
                var obsoleteAttributeType = csaContext.Compilation.GetTypeByMetadataName(typeof(System.ObsoleteAttribute).FullName);


                csaContext.RegisterOperationAction(oaContext =>
                {
                    var op = (IConversionOperation)oaContext.Operation;
                    if (!op.IsImplicit || !op.Conversion.Exists || !(op.Type.Equals(objectType) || op.Type.Equals(stringType)))
                        return;

                    if (op.Parent is IBinaryOperation parentBinOp)
                    {
                        if (parentBinOp.OperatorKind != BinaryOperatorKind.Add || !parentBinOp.Type.Equals(stringType))
                            return;
                    }
                    else if (op.Parent is ICompoundAssignmentOperation compoundOp)
                    {
                        if (compoundOp.OperatorKind != BinaryOperatorKind.Add || !compoundOp.Type.Equals(stringType))
                            return;
                    }
                    else
                    {
                        return;
                    }

                    IEnumerable<IMethodSymbol> canidateToStringMethods =
                        op.Operand.Type.GetMembers("ToString")
                                       .OfType<IMethodSymbol>()
                                       .Where(m => !m.GetAttributes().Any(a => a.AttributeClass.Equals(obsoleteAttributeType)))
                                       .ToList();
                    foreach (var m in canidateToStringMethods)
                    {
                        if (m.Parameters.Any(p => p.Type.Equals(iformatProviderType)))
                        {
                            Location loc = op.Syntax.GetLocation();
                            if (!loc.IsInSource)
                                loc = null;

                            var diag = Diagnostic.Create(Rule, loc, m, op.Operand.Type.Name);
                            oaContext.ReportDiagnostic(diag);
                            break;
                        }
                    }
                }, OperationKind.Conversion);
            });
        }
    }
}
