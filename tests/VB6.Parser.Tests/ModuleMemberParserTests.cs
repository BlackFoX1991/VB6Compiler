using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ModuleMemberParserTests
{
    private static CompilationUnitSyntax Parse(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        return result.Root;
    }

    [TestMethod]
    public void Parse_SkipsAttributeLines()
    {
        var root = Parse("""
            Attribute VB_Name = "modMain"
            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.AreEqual(2, root.Members.Length);
        Assert.IsInstanceOfType<AttributeSyntax>(root.Members[0]);
        Assert.IsInstanceOfType<SubDeclarationSyntax>(root.Members[1]);
    }

    [TestMethod]
    public void Parse_KeepsAttributeUsableAsAnIdentifier()
    {
        // 'Attribute' is not reserved in VB6, so it must still work as a variable name.
        var root = Parse("""
            Sub Main()
                Dim Attribute As Integer
                Attribute = 1
            End Sub
            """);

        Assert.AreEqual(1, root.Members.Length);
        Assert.IsInstanceOfType<SubDeclarationSyntax>(root.Members[0]);
    }

    [TestMethod]
    public void Parse_AcceptsVisibilityModifiersOnProcedures()
    {
        var root = Parse("""
            Public Sub Main()
                Debug.Print 1
            End Sub

            Private Function Twice(ByVal value As Integer) As Integer
                Twice = value
            End Function
            """);

        var sub = (SubDeclarationSyntax)root.Members[0];
        var function = (FunctionDeclarationSyntax)root.Members[1];
        Assert.AreEqual("Public", sub.VisibilityKeyword!.Text);
        Assert.AreEqual("Private", function.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_AcceptsStaticSubDeclaration()
    {
        var root = Parse("""
            Static Sub Count()
            End Sub
            """);

        var sub = (SubDeclarationSyntax)root.Members.Single();
        Assert.IsNull(sub.VisibilityKeyword);
        Assert.AreEqual("Static", sub.StaticKeyword!.Text);
    }

    [TestMethod]
    public void Parse_PreservesVisibilityBeforeStaticFunctionModifier()
    {
        var root = Parse("""
            Private Static Function NextValue() As Long
            End Function
            """);

        var function = (FunctionDeclarationSyntax)root.Members.Single();
        Assert.AreEqual("Private", function.VisibilityKeyword!.Text);
        Assert.AreEqual("Static", function.StaticKeyword!.Text);
        Assert.AreEqual("Long", function.ReturnTypeToken!.Text);
    }

    [TestMethod]
    public void Parse_ReadsModuleVariableDeclarations()
    {
        var root = Parse("""
            Public Source As String
            Private Position As Long
            Dim Counter As Integer

            Sub Main()
                Debug.Print Counter
            End Sub
            """);

        Assert.AreEqual(4, root.Members.Length);
        var declarations = root.Members.OfType<ModuleVariableDeclarationSyntax>().ToArray();
        Assert.AreEqual(3, declarations.Length);
        Assert.AreEqual("Source", declarations[0].Identifier.Text);
        Assert.AreEqual("Public", declarations[0].VisibilityKeyword!.Text);
        Assert.AreEqual("String", declarations[0].TypeToken.Text);
        Assert.AreEqual("Dim", declarations[2].VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_AcceptsDimWithEventsModuleVariable()
    {
        var root = Parse("""
            Dim WithEvents source As Counter
            """);

        var declaration = root.Members.OfType<ModuleVariableDeclarationSyntax>().Single();
        Assert.AreEqual("Dim", declaration.VisibilityKeyword!.Text);
        Assert.AreEqual("WithEvents", declaration.WithEventsKeyword!.Text);
        Assert.AreEqual("source", declaration.Identifier.Text);
        Assert.AreEqual("Counter", declaration.TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_DoesNotTreatDeclareAsModuleVariable()
    {
        var root = Parse("""
            Private Declare Function GetTickCount Lib "kernel32" () As Long
            """);

        Assert.AreEqual(0, root.Members.OfType<ModuleVariableDeclarationSyntax>().Count());
        Assert.AreEqual(1, root.Members.OfType<DeclareDeclarationSyntax>().Count());
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityDeclarationsInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                Public exported As Long
                If True Then
                    Private hidden As Long
                End If
                Global sharedValue As Long
                Dim localValue As Long
                Debug.Print localValue
            End Sub
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(5, procedure.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[0]);
        var nestedIf = (IfStatementSyntax)procedure.Statements[1];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[2]);
        Assert.IsInstanceOfType<DimStatementSyntax>(procedure.Statements[3]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(procedure.Statements[4]);
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityConstantsInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                Public Const exported As Long = 1
                If True Then
                    Private Const hidden As Long = 2
                End If
                Global Const sharedValue As Long = 3
                Const localValue As Long = 4
                Debug.Print localValue
            End Sub
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(5, procedure.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[0]);
        var nestedIf = (IfStatementSyntax)procedure.Statements[1];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[2]);
        Assert.IsInstanceOfType<ConstStatementSyntax>(procedure.Statements[3]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(procedure.Statements[4]);
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityProceduresInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Public Sub Nested()
                Debug.Print 1
            End Sub

            Sub Second()
                Private Function NestedFunction()
                Debug.Print 2
            End Sub

            Sub Third()
                If True Then
                    Global Sub NestedGlobal()
                End If
                Debug.Print 3
            End Sub

            Public Sub Exported()
            End Sub
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(4, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(second.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var third = (SubDeclarationSyntax)result.Root.Members[2];
        Assert.AreEqual(2, third.Statements.Length);
        var nestedIf = (IfStatementSyntax)third.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(third.Statements[1]);

        var exported = (SubDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual("Public", exported.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityTypesInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Public Enum Nested
                Debug.Print 1
            End Sub

            Sub Second()
                Private Type NestedRecord
                Debug.Print 2
            End Sub

            Sub Third()
                If True Then
                    Global Enum NestedGlobal
                End If
                Debug.Print 3
            End Sub

            Public Enum Exported
                Value = 1
            End Enum

            Private Type Record
                Value As Long
            End Type
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(5, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(second.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var third = (SubDeclarationSyntax)result.Root.Members[2];
        Assert.AreEqual(2, third.Statements.Length);
        var nestedIf = (IfStatementSyntax)third.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(third.Statements[1]);

        var exportedEnum = (EnumDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual("Public", exportedEnum.VisibilityKeyword!.Text);
        var exportedType = (TypeDeclarationSyntax)result.Root.Members[4];
        Assert.AreEqual("Private", exportedType.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityDeclareInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Public Declare Function Nested Lib "kernel32" () As Long
                Debug.Print 1
            End Sub

            Sub Second()
                Private Declare Sub NestedSub Lib "kernel32" ()
                Debug.Print 2
            End Sub

            Sub Third()
                If True Then
                    Global Declare Function NestedGlobal Lib "kernel32" () As Long
                End If
                Debug.Print 3
            End Sub

            Public Declare Function Exported Lib "kernel32" () As Long
            Private Declare Sub Hidden Lib "kernel32" ()
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(5, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(second.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var third = (SubDeclarationSyntax)result.Root.Members[2];
        Assert.AreEqual(2, third.Statements.Length);
        var nestedIf = (IfStatementSyntax)third.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(third.Statements[1]);

        var exported = (DeclareDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual("Public", exported.VisibilityKeyword!.Text);
        var hidden = (DeclareDeclarationSyntax)result.Root.Members[4];
        Assert.AreEqual("Private", hidden.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_RejectsModuleVisibilityPropertyAndEventInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Public Property Get Nested() As Long
                Debug.Print 1
            End Sub

            Sub Second()
                Private Event NestedChanged(ByVal value As Long)
                Debug.Print 2
            End Sub

            Sub Third()
                If True Then
                    Global Property Let NestedValue(ByVal value As Long)
                End If
                Debug.Print 3
            End Sub

            Public Property Get Exported() As Long
                Exported = 1
            End Property

            Private Event Hidden(ByVal value As Long)
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(3, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(5, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(second.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var third = (SubDeclarationSyntax)result.Root.Members[2];
        Assert.AreEqual(2, third.Statements.Length);
        var nestedIf = (IfStatementSyntax)third.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(third.Statements[1]);

        var exported = (PropertyDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual("Public", exported.VisibilityKeyword!.Text);
        Assert.IsTrue(exported.IsGet);
        var hidden = (EventDeclarationSyntax)result.Root.Members[4];
        Assert.AreEqual("Private", hidden.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_RejectsWithEventsDeclarationsInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Public WithEvents publicSource As Counter
                Debug.Print 1
            End Sub

            Sub Second()
                Dim WithEvents localSource As Counter
                Debug.Print 2
            End Sub

            Sub Third()
                Private WithEvents privateSource As Counter
                Debug.Print 3
            End Sub

            Sub Fourth()
                If True Then
                    Global WithEvents globalSource As Counter
                End If
                Debug.Print 4
            End Sub

            Public WithEvents exported As Counter
            Dim WithEvents dimExported As Counter
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(4, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(6, result.Root.Members.Length);
        for (var index = 0; index < 3; index++)
        {
            var procedure = (SubDeclarationSyntax)result.Root.Members[index];
            Assert.AreEqual(2, procedure.Statements.Length);
            Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[0]);
            Assert.IsInstanceOfType<DebugPrintStatementSyntax>(procedure.Statements[1]);
        }

        var fourth = (SubDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual(2, fourth.Statements.Length);
        var nestedIf = (IfStatementSyntax)fourth.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(fourth.Statements[1]);

        var exported = (ModuleVariableDeclarationSyntax)result.Root.Members[4];
        Assert.AreEqual("Public", exported.VisibilityKeyword!.Text);
        Assert.AreEqual("WithEvents", exported.WithEventsKeyword!.Text);
        var dimExported = (ModuleVariableDeclarationSyntax)result.Root.Members[5];
        Assert.AreEqual("Dim", dimExported.VisibilityKeyword!.Text);
        Assert.AreEqual("WithEvents", dimExported.WithEventsKeyword!.Text);
    }

    [TestMethod]
    public void Parse_RejectsImplementsDeclarationsInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Implements IWorker
                Debug.Print 1
            End Sub

            Sub Second()
                If True Then
                    Implements INested
                End If
                Debug.Print 2
            End Sub

            Implements IWorker
            """, "test.cls")).ParseCompilationUnit();

        Assert.AreEqual(2, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(3, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        var nestedIf = (IfStatementSyntax)second.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var moduleImplements = (ImplementsStatementSyntax)result.Root.Members[2];
        Assert.AreEqual("Implements", moduleImplements.ImplementsKeyword.Text);
        Assert.AreEqual("IWorker", moduleImplements.TypeToken.Text);
    }

    [TestMethod]
    public void Parse_RejectsOptionDirectivesInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Option Explicit
                Debug.Print 1
            End Sub

            Sub Second()
                Option Base 1
                Debug.Print 2
            End Sub

            Sub Third()
                Option Compare Text
                Debug.Print 3
            End Sub

            Sub Fourth()
                If True Then
                    Option Private Module
                End If
                Debug.Print 4
            End Sub

            Option Explicit
            Option Base 1
            Option Compare Text
            Option Private Module
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(4, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001", "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(8, result.Root.Members.Length);
        for (var index = 0; index < 3; index++)
        {
            var procedure = (SubDeclarationSyntax)result.Root.Members[index];
            Assert.AreEqual(2, procedure.Statements.Length);
            Assert.IsInstanceOfType<SkippedStatementSyntax>(procedure.Statements[0]);
            Assert.IsInstanceOfType<DebugPrintStatementSyntax>(procedure.Statements[1]);
        }

        var fourth = (SubDeclarationSyntax)result.Root.Members[3];
        Assert.AreEqual(2, fourth.Statements.Length);
        var nestedIf = (IfStatementSyntax)fourth.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(fourth.Statements[1]);

        Assert.IsInstanceOfType<OptionExplicitSyntax>(result.Root.Members[4]);
        Assert.IsInstanceOfType<OptionBaseSyntax>(result.Root.Members[5]);
        Assert.IsInstanceOfType<OptionCompareSyntax>(result.Root.Members[6]);
        Assert.IsInstanceOfType<OptionPrivateModuleSyntax>(result.Root.Members[7]);
    }

    [TestMethod]
    public void Parse_RejectsAttributeLinesInsideProcedure()
    {
        var result = new ParserType(SourceText.From("""
            Sub First()
                Attribute Foo = "bar"
                Debug.Print 1
            End Sub

            Sub Second()
                If True Then
                    Attribute Bar = "baz"
                End If
                Debug.Print 2
            End Sub

            Attribute VB_Name = "test"
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(2, result.Diagnostics.Length);
        CollectionAssert.AreEquivalent(
            new[] { "VB6P0001", "VB6P0001" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        Assert.AreEqual(3, result.Root.Members.Length);
        var first = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(2, first.Statements.Length);
        Assert.IsInstanceOfType<SkippedStatementSyntax>(first.Statements[0]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(first.Statements[1]);

        var second = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual(2, second.Statements.Length);
        var nestedIf = (IfStatementSyntax)second.Statements[0];
        Assert.IsInstanceOfType<SkippedStatementSyntax>(nestedIf.Statements.Single());
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(second.Statements[1]);

        var moduleAttribute = (AttributeSyntax)result.Root.Members[2];
        Assert.AreEqual("Attribute", moduleAttribute.AttributeKeyword.Text);
    }
}
