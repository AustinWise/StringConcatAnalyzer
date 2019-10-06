using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using Analyzer1;

namespace Analyzer1.Test
{
    [TestClass]
    public class StringConcatConversionUnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void TestNoDiagnoisticsOnNoSource()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestConcatWithExistingGlobalzationUsing()
        {
            var test = @"
using System;
using System.Globalization;

class Program
{
    string Main(int val)
    {
        return ""hi: "" + val;
    }
}
";
            var expected = new DiagnosticResult
            {
                Id = StringConcatConversionAnalyzer.DiagnosticId,
                Message = "Call method 'int.ToString(System.IFormatProvider)' on type 'Int32'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 25)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Globalization;

class Program
{
    string Main(int val)
    {
        return ""hi: "" + val.ToString(CultureInfo.InvariantCulture);
    }
}
";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void TestConcatAndAddNamespaceUsing()
        {
            var test = @"
using System;

class Program
{
    string Main(int val)
    {
        return ""hi: "" + val;
    }
}
";
            var expected = new DiagnosticResult
            {
                Id = StringConcatConversionAnalyzer.DiagnosticId,
                Message = "Call method 'int.ToString(System.IFormatProvider)' on type 'Int32'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 8, 25)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Globalization;

class Program
{
    string Main(int val)
    {
        return ""hi: "" + val.ToString(CultureInfo.InvariantCulture);
    }
}
";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void TestCompounndAssignmentStringConcat()
        {
            var test = @"
using System;
using System.Globalization;

class Program
{
    string Main(int val)
    {
        string s = ""asdf"";
        s += val;
        return s;
    }
}
";
            var expected = new DiagnosticResult
            {
                Id = StringConcatConversionAnalyzer.DiagnosticId,
                Message = "Call method 'int.ToString(System.IFormatProvider)' on type 'Int32'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 14)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Globalization;

class Program
{
    string Main(int val)
    {
        string s = ""asdf"";
        s += val.ToString(CultureInfo.InvariantCulture);
        return s;
    }
}
";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new StringConcatConversionCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new StringConcatConversionAnalyzer();
        }
    }
}
