using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ProjectUserDefinedTypeDeclarationBinderTests
{
    [TestMethod]
    public void Bind_ResolvesPublicTypeAcrossModules()
    {
        var result = BindProject(
            """
            Public Type Point
                X As Long
            End Type
            """,
            """
            Private Type Container
                Position As Point
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        var point = result.PublicTypes["Point"];
        Assert.AreSame(point, result.Modules[1].Types["Point"]);
        var container = result.Modules[1].Types["Container"];
        Assert.IsTrue(container.TryGetMember("Position", out var position));
        Assert.AreSame(point, position.Type);
    }

    [TestMethod]
    public void Bind_DefaultTypeVisibilityIsPublic()
    {
        var result = BindProject(
            """
            Type Point
                X As Long
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        Assert.IsTrue(result.PublicTypes.ContainsKey("point"));
        Assert.AreSame(result.PublicTypes["Point"], result.Modules[0].Types["Point"]);
    }

    [TestMethod]
    public void Bind_AllowsSamePrivateTypeNameInDifferentModules()
    {
        var result = BindProject(
            """
            Private Type LocalRecord
                A As Long
            End Type
            """,
            """
            Private Type LocalRecord
                B As Integer
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        Assert.AreNotSame(
            result.Modules[0].Types["LocalRecord"],
            result.Modules[1].Types["LocalRecord"]);
    }

    [TestMethod]
    public void Bind_PrivateTypeCanShadowPublicTypeFromAnotherModule()
    {
        var result = BindProject(
            """
            Public Type Point
                X As Long
            End Type
            """,
            """
            Private Type Point
                X As Integer
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        Assert.AreNotSame(result.PublicTypes["Point"], result.Modules[1].Types["Point"]);
    }

    [TestMethod]
    public void Bind_DiagnosesDuplicatePublicTypeAcrossModules()
    {
        var result = BindProject(
            """
            Public Type Point
                X As Long
            End Type
            """,
            """
            Public Type POINT
                Y As Long
            End Type
            """);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0045"));
    }

    private static ProjectUserDefinedTypeDeclarationResult BindProject(params string[] sources)
    {
        var modules = sources.Select((source, index) =>
        {
            var text = SourceText.From(source, $"module{index + 1}.bas");
            var parse = new ParserType(text).ParseCompilationUnit();
            Assert.AreEqual(
                0,
                parse.Diagnostics.Length,
                string.Join(Environment.NewLine, parse.Diagnostics.Select(diagnostic => diagnostic.ToString())));
            return new UserDefinedTypeModuleInput(text, parse.Root);
        });

        return new ProjectUserDefinedTypeDeclarationBinder().Bind(modules);
    }

    private static string FormatDiagnostics(ProjectUserDefinedTypeDeclarationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
