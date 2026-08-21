using VB6.IR;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class UserDefinedTypeAnalysisTests
{
    [TestMethod]
    public void Analyze_ExposesBoundUserDefinedTypes()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Y As Long
            End Type
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.UserDefinedTypes);
        Assert.IsTrue(analysis.UserDefinedTypes.Types.ContainsKey("point"));
        Assert.AreEqual(2, analysis.UserDefinedTypes.Types["Point"].Members.Length);
    }

    [TestMethod]
    public void Analyze_BindsUdtIdentityIntoValuesAndArrays()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Y As Long
            End Type

            Public Current As Point

            Function Echo(ByRef value As Point) As Point
                Dim local As Point
                Echo = value
            End Function

            Sub Main()
                Dim points(1 To 2) As Point
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.UserDefinedTypes);
        Assert.IsNotNull(analysis.SemanticModel);
        var point = analysis.UserDefinedTypes.Types["Point"];

        var current = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
        Assert.AreSame(point, current.Symbol.Type);

        var echo = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Echo");
        Assert.AreSame(point, echo.Symbol.ReturnType);
        Assert.AreSame(point, echo.Symbol.Parameters.Single().Type);
        Assert.AreSame(point, echo.Locals.Single(local => local.Name == "local").Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var points = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "points").Type;
        Assert.AreSame(point, points.ElementType);
        Assert.AreEqual(1, points.Rank);

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    /// <summary>
    /// A UDT becomes its own type definition with one field per member, and a variable of that
    /// type holds the type itself - not a Variant, which is what an unresolved member type would
    /// silently degrade into.
    /// </summary>
    [TestMethod]
    public void Lower_EmitsManagedUdtStorage()
    {
        var program = VB6TestIr.Lower("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim value As Point
            End Sub
            """, "test.bas");

        var point = program.TypeDefinitions.Single(type => type.Symbol.Name == "Point");
        var field = point.Fields.Single(item => item.Name == "X");
        Assert.AreSame(TypeSymbol.Long, field.Type);

        var main = VB6TestIr.Procedures(program).Single(procedure => procedure.Name == "Main");
        Assert.AreSame(point.Symbol, main.Locals.Single(local => local.Name == "value").Type);
    }

    [TestMethod]
    public void Lower_AllowsFixedPrimitiveArrayMemberLayout()
    {
        var program = VB6TestIr.Lower("""
            Type Buffer
                Values(0 To 3) As Long
            End Type

            Sub Main()
                Dim value As Buffer
            End Sub
            """, "test.bas");

        var member = program.TypeDefinitions
            .Single(type => type.Symbol.Name == "Buffer")
            .Symbol.Members.Single(item => item.Name == "Values");

        Assert.AreSame(TypeSymbol.Long, ((ArrayTypeSymbol)member.Type).ElementType);
        CollectionAssert.AreEqual(
            new[] { new UserDefinedTypeArrayBound(0, 3) },
            member.ArrayBounds.ToArray());
    }

    /// <summary>
    /// A <c>String * n</c> member keeps its declared width in both directions: reading an
    /// untouched one yields n spaces, and a stored value is truncated or padded to n.
    /// </summary>
    [TestMethod]
    public void Lower_EmitsFixedStringMemberLayout()
    {
        var program = VB6TestIr.Lower("""
            Type Record
                Name As String * 16
            End Type

            Sub Main()
                Dim value As Record
                value.Name = "hello"
                Debug.Print value.Name
            End Sub
            """, "test.bas");

        var member = program.TypeDefinitions
            .Single(type => type.Symbol.Name == "Record")
            .Symbol.Members.Single(item => item.Name == "Name");
        Assert.AreEqual(16, ((FixedLengthStringTypeSymbol)member.Type).Length);

        CollectionAssert.IsSubsetOf(
            new[] { IrRuntimeMethod.FixedStringRead, IrRuntimeMethod.FixedStringWrite },
            VB6TestIr.RuntimeCalls(program).ToArray());
    }

    [TestMethod]
    public void Lower_StopsOnInvalidUserDefinedTypeMember()
    {
        var lowering = VBCompilation.Create("""
            Type Broken
                Value As MissingType
            End Type
            """, "test.bas").Lower();

        Assert.IsFalse(lowering.Success);
        Assert.IsNull(lowering.Program);
        Assert.IsTrue(lowering.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
    }
}
