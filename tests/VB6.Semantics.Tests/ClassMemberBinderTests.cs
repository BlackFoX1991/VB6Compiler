using System.Collections.Immutable;
using VB6.Parser;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ClassMemberBinderTests
{
    [TestMethod]
    public void Bind_RegistersPropertyAccessorsAndEvents()
    {
        var source = """
            Public Event Changed(ByVal value As Long)

            Property Get Value() As Long
                Value = 1
            End Property

            Property Let Value(ByVal newValue As Long)
            End Property

            Property Set Child(ByVal value As Variant)
            End Property
            """;
        var text = SourceText.From(source, "ClassMembers.cls");
        var root = new ParserType(text).ParseCompilationUnit().Root;

        var model = new Binder(text).BindCompilationUnit(root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        Assert.AreEqual(3, model.Properties.Length);
        Assert.AreEqual(PropertyAccessorKind.Get, model.Properties[0].Accessor);
        Assert.AreEqual(PropertyAccessorKind.Let, model.Properties[1].Accessor);
        Assert.AreEqual(PropertyAccessorKind.Set, model.Properties[2].Accessor);
        Assert.AreEqual(TypeSymbol.Long, model.Properties[0].Type);
        Assert.AreEqual(TypeSymbol.Long, model.Properties[1].Type);
        Assert.AreEqual(TypeSymbol.Variant, model.Properties[2].Type);
        Assert.AreEqual(1, model.Events.Length);
        Assert.AreEqual("Changed", model.Events[0].Name);
        Assert.AreEqual(TypeSymbol.Long, model.Events[0].Parameters[0].Type);
    }

    [TestMethod]
    public void Bind_ResolvesClassConstructionPropertiesMethodsAndTypeOf()
    {
        var classText = SourceText.From("""
            Property Get Value() As Long
                Value = 1
            End Property

            Function GetValue() As Long
            End Function
            """, "Widget.cls");
        var classRoot = new ParserType(classText).ParseCompilationUnit().Root;
        var classType = new ClassTypeSymbol("Widget", classText.FilePath);
        Assert.IsTrue(classType.TryDefineMembers(
            classRoot.Members.OfType<FunctionDeclarationSyntax>().Select(Binder.CreateProcedureSymbol),
            classRoot.Members.OfType<PropertyDeclarationSyntax>().Select(Binder.CreatePropertySymbol),
            Enumerable.Empty<EventSymbol>(),
            out _));

        var sourceText = SourceText.From("""
            Sub Use()
                Dim value As Widget
                Set value = New Widget
                Debug.Print value.Value
                Debug.Print value.GetValue()
                If TypeOf value Is Widget Then
                End If
            End Sub
            """, "Use.bas");
        var root = new ParserType(sourceText).ParseCompilationUnit().Root;
        var aliases = ImmutableDictionary.CreateBuilder<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        aliases.Add("Widget", classType);
        using var scope = UserDefinedTypeLookupScope.PushAliases(aliases.ToImmutable());

        var model = new Binder(sourceText).BindCompilationUnit(
            root,
            new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase));

        Assert.AreEqual(0, model.Diagnostics.Length, string.Join(Environment.NewLine, model.Diagnostics));
    }

    [TestMethod]
    public void Bind_ModelsArrayFunctionReturnType()
    {
        var text = SourceText.From("Function Names() As String()\nEnd Function\n", "module.bas");
        var root = new ParserType(text).ParseCompilationUnit().Root;

        var symbol = Binder.CreateProcedureSymbol((FunctionDeclarationSyntax)root.Members.Single());

        var returnType = symbol.ReturnType as ArrayTypeSymbol;
        Assert.IsNotNull(returnType);
        Assert.AreEqual(TypeSymbol.String, returnType!.ElementType);
        Assert.IsFalse(returnType.HasKnownRank);
    }
}
