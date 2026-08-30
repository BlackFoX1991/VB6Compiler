using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ImplicitVariantAnalysisTests
{
    [TestMethod]
    public void Analyze_DefaultsUntypedDeclarationsToVariant()
    {
        var analysis = VBCompilation.Create("""
            Public Current

            Sub Main()
                Dim first, second As Long
                Dim values(1 To 2)
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0020"));

        var current = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
        Assert.AreSame(TypeSymbol.Variant, current.Symbol.Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "first").Type);
        Assert.AreSame(TypeSymbol.Long, main.Locals.Single(local => local.Name == "second").Type);

        var values = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type;
        Assert.AreSame(TypeSymbol.Variant, values.ElementType);
        Assert.AreEqual(1, values.Rank);
    }

    [TestMethod]
    public void Analyze_AppliesDefTypeDefaultsToDeclarationsParametersAndReturns()
    {
        var analysis = VBCompilation.Create("""
            DefInt A-Z
            Dim moduleValue

            Sub Main(argumentValue)
                Dim localValue
            End Sub

            Function BuildValue()
            End Function

            Property Get Answer()
            End Property
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.AreSame(
            TypeSymbol.Integer,
            analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "moduleValue").Symbol.Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Integer, main.Symbol.Parameters.Single().Type);
        Assert.AreSame(TypeSymbol.Integer, main.Locals.Single(local => local.Name == "localValue").Type);
        Assert.AreSame(
            TypeSymbol.Integer,
            analysis.SemanticModel.Procedures
                .Single(procedure => procedure.Symbol.Name == "BuildValue")
                .Symbol.ReturnType);
        Assert.AreSame(
            TypeSymbol.Integer,
            analysis.SemanticModel.Properties.Single(property => property.Name == "Answer").Type);
    }

    [TestMethod]
    public void Analyze_DefTypeDefaultsYieldToExplicitTypesAndSuffixes()
    {
        var analysis = VBCompilation.Create("""
            DefInt A-Z
            Dim explicitValue As String
            Dim suffixValue$

            Sub Main()
                Dim explicitLocal As String
                Dim suffixLocal$
            End Sub

            Function BuildValue$()
            End Function
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.AreSame(
            TypeSymbol.String,
            analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "explicitValue").Symbol.Type);
        Assert.AreSame(
            TypeSymbol.String,
            analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "suffixValue").Symbol.Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.String, main.Locals.Single(local => local.Name == "explicitLocal").Type);
        Assert.AreSame(TypeSymbol.String, main.Locals.Single(local => local.Name == "suffixLocal").Type);
        Assert.AreSame(
            TypeSymbol.String,
            analysis.SemanticModel.Procedures
                .Single(procedure => procedure.Symbol.Name == "BuildValue")
                .Symbol.ReturnType);
    }

    [TestMethod]
    public void Analyze_AppliesDefTypeToImplicitAssignmentAndNameVariables()
    {
        var analysis = VBCompilation.Create("""
            DefInt A-Z

            Sub Main()
                assignedValue = 42
                Debug.Print expressionValue
                suffixedValue$ = "ok"
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Integer, main.Locals.Single(local => local.Name == "assignedValue").Type);
        Assert.AreSame(TypeSymbol.Integer, main.Locals.Single(local => local.Name == "expressionValue").Type);
        Assert.AreSame(TypeSymbol.String, main.Locals.Single(local => local.Name == "suffixedValue").Type);
    }

    [TestMethod]
    public void Analyze_RejectsOverlappingDefTypeRanges()
    {
        var analysis = VBCompilation.Create("""
            DefInt A-M
            DefStr M-Z
            """, "test.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        var diagnostic = analysis.Diagnostics.Single(item => item.Code == "VB6S0070");
        StringAssert.Contains(diagnostic.Message, "overlaps");
    }

    [TestMethod]
    public void Analyze_AcceptsAdjacentDefTypeRanges()
    {
        var analysis = VBCompilation.Create("""
            DefInt A-M
            DefStr N-Z

            Sub Main()
                integerValue = 1
                stringValue = "ok"
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Integer, main.Locals.Single(local => local.Name == "integerValue").Type);
        Assert.AreSame(TypeSymbol.String, main.Locals.Single(local => local.Name == "stringValue").Type);
    }

    [TestMethod]
    public void Analyze_StaticProcedureDimUsesPersistentStorage()
    {
        var analysis = VBCompilation.Create("""
            Static Function NextValue() As Long
                Dim count As Long
                count = count + 1
                NextValue = count
            End Function

            Function OrdinaryValue() As Long
                Dim count As Long
                count = count + 1
                OrdinaryValue = count
            End Function
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);

        var staticProcedure = analysis.SemanticModel.Procedures.Single(
            procedure => procedure.Symbol.Name == "NextValue");
        var ordinaryProcedure = analysis.SemanticModel.Procedures.Single(
            procedure => procedure.Symbol.Name == "OrdinaryValue");
        Assert.AreEqual(0, staticProcedure.Locals.Length);
        Assert.AreEqual(1, ordinaryProcedure.Locals.Length);
        Assert.AreEqual(1, analysis.SemanticModel.StaticVariables.Length);
        Assert.AreEqual("__static_test.bas_NextValue_count", analysis.SemanticModel.StaticVariables[0].Symbol.Name);
    }

    [TestMethod]
    public void EmitManagedApplication_StaticProcedureRetainsDimValuesAcrossCalls()
    {
        var output = VB6TestProgram.RunLines("""
            Static Function NextValue() As Long
                Dim count As Long
                count = count + 1
                NextValue = count
            End Function

            Function OrdinaryValue() As Long
                Dim count As Long
                count = count + 1
                OrdinaryValue = count
            End Function

            Sub Main()
                Debug.Print NextValue()
                Debug.Print NextValue()
                Debug.Print OrdinaryValue()
                Debug.Print OrdinaryValue()
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "2", "1", "1" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_StaticProcedureRetainsDimArrayAcrossCalls()
    {
        var output = VB6TestProgram.RunLines("""
            Static Sub KeepArray()
                Dim values(1 To 2) As Long
                values(1) = values(1) + 1
                Debug.Print values(1)
            End Sub

            Sub Main()
                KeepArray
                KeepArray
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "2" }, output);
    }

    [TestMethod]
    public void Analyze_DefaultsUntypedStaticToVariantWithPersistentLifetime()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Static cached
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0020"));

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, analysis.SemanticModel.StaticVariables.Single().Symbol.Type);
        Assert.AreEqual(0, main.Locals.Length);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesImplicitVariantStorageAndArrays()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim value
                Dim values(1 To 2)
                value = 42
                values(1) = value
                Debug.Print values(1)
            End Sub
            """, "test.bas");

        var main = VB6TestIr.Procedures(program).Single(procedure => procedure.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "value").Type);
        Assert.AreSame(
            TypeSymbol.Variant,
            ((ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type).ElementType);

        // An untyped declaration has to survive all the way into a running program, not just into
        // the type table: Variant storage is where a wrong element type shows up as a crash.
        Assert.AreEqual("42", VB6TestProgram.Run("""
            Sub Main()
                Dim value
                Dim values(1 To 2)
                value = 42
                values(1) = value
                Debug.Print values(1)
            End Sub
            """, "test.bas").Trim());
    }
}
