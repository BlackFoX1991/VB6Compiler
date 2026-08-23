using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectUserDefinedTypeAnalysisTests
{
    [TestMethod]
    public void Analyze_ExposesPublicAndPrivateUserDefinedTypeScopes()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
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

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsNotNull(analysis.UserDefinedTypes);
            Assert.AreEqual(1, analysis.UserDefinedTypes.PublicTypes.Count);
            Assert.AreEqual(2, analysis.UserDefinedTypes.Modules.Length);

            var point = analysis.UserDefinedTypes.PublicTypes["Point"];
            var container = analysis.UserDefinedTypes.Modules[1].Types["Container"];
            Assert.IsTrue(container.TryGetMember("Position", out var position));
            Assert.AreSame(point, position.Type);

            Assert.IsNotNull(analysis.Units[0].Analysis.UserDefinedTypes);
            Assert.AreSame(point, analysis.Units[0].Analysis.UserDefinedTypes!.Types["Point"]);
            Assert.IsNotNull(analysis.Units[1].Analysis.UserDefinedTypes);
            Assert.AreSame(container, analysis.Units[1].Analysis.UserDefinedTypes!.Types["Container"]);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_BindsPublicUdtValuesAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Type Point
                    X As Long
                End Type

                Public Origin As Point
                """,
                """
                Sub UsePoint(ByRef value As Point)
                    Dim local As Point
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsNotNull(analysis.UserDefinedTypes);
            Assert.IsNotNull(analysis.SemanticModel);
            var point = analysis.UserDefinedTypes.PublicTypes["Point"];

            var origin = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Origin");
            Assert.AreSame(point, origin.Symbol.Type);

            var usePoint = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "UsePoint");
            Assert.AreSame(point, usePoint.Symbol.Parameters.Single().Type);
            Assert.AreSame(point, usePoint.Locals.Single(local => local.Name == "local").Type);

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_UsesPrivateUdtIdentityInOriginModule()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Type Point
                    X As Long
                End Type

                Sub UsePublic(ByRef value As Point)
                End Sub
                """,
                """
                Private Type Point
                    X As Integer
                End Type

                Sub UsePrivate(ByRef value As Point)
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsNotNull(analysis.UserDefinedTypes);
            Assert.IsNotNull(analysis.SemanticModel);
            var publicPoint = analysis.UserDefinedTypes.PublicTypes["Point"];
            var privatePoint = analysis.UserDefinedTypes.Modules[1].Types["Point"];
            Assert.AreNotSame(publicPoint, privatePoint);

            var publicParameter = analysis.SemanticModel.Procedures
                .Single(procedure => procedure.Symbol.Name == "UsePublic")
                .Symbol.Parameters.Single();
            var privateParameter = analysis.SemanticModel.Procedures
                .Single(procedure => procedure.Symbol.Name == "UsePrivate")
                .Symbol.Parameters.Single();

            Assert.AreSame(publicPoint, publicParameter.Type);
            Assert.AreSame(privatePoint, privateParameter.Type);
            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_BindsPrivateUdtArrayParametersInClassModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClassUdt.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ClassUdt"
                Module=Main; Main.bas
                Class=Widget; Widget.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Widget.cls"), """
                Private Type Point
                    X As Long
                End Type

                Private Sub Fill(ByRef values() As Point)
                    ReDim values(0)
                    With values(0)
                        .X = 1
                    End With
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            var fill = analysis.SemanticModel!.Procedures.Single(procedure => procedure.Symbol.Name == "Fill");
            var arrayType = fill.Symbol.Parameters.Single().Type as ArrayTypeSymbol;
            Assert.IsNotNull(arrayType);
            Assert.IsInstanceOfType<UserDefinedTypeSymbol>(arrayType!.ElementType);
            Assert.AreEqual("Point", arrayType.ElementType.Name);
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Lower_UsesDistinctStorageTypesForPrivateUdtShadowing()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Type Point
                    X As Long
                End Type

                Sub Main()
                    Dim value As Point
                    UsePublic value
                End Sub

                Sub UsePublic(ByRef value As Point)
                End Sub
                """,
                """
                Private Type Point
                    X As Integer
                End Type

                Sub UsePrivate(ByRef value As Point)
                End Sub
                """);

            var program = VB6TestIr.LowerProject(projectPath);

            // Both declarations are named Point but are different types, so each one needs its own
            // storage: a single shared definition would silently give one module the other's
            // member widths.
            var points = program.TypeDefinitions.Where(type => type.Symbol.Name == "Point").ToArray();
            Assert.AreEqual(2, points.Length);
            Assert.AreNotSame(points[0].Symbol, points[1].Symbol);
            Assert.AreEqual(2, points.Select(type => type.Name).Distinct(StringComparer.Ordinal).Count());

            var usePublic = VB6TestIr.Procedures(program).Single(procedure => procedure.Symbol?.Name == "UsePublic");
            var usePrivate = VB6TestIr.Procedures(program).Single(procedure => procedure.Symbol?.Name == "UsePrivate");
            Assert.AreNotSame(usePublic.Parameters.Single().Type, usePrivate.Parameters.Single().Type);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicatePublicUserDefinedTypesAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
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

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0045"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory, string firstSource, string secondSource)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "UdtProject.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="UdtProject"
            Module=First; First.bas
            Module=Second; Second.bas
            """);
        File.WriteAllText(Path.Combine(directory, "First.bas"), firstSource);
        File.WriteAllText(Path.Combine(directory, "Second.bas"), secondSource);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis)
    {
        var projectDiagnostics = analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString());
        var sourceDiagnostics = analysis.Diagnostics.Select(diagnostic => diagnostic.ToString());
        return string.Join(Environment.NewLine, projectDiagnostics.Concat(sourceDiagnostics));
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectUdtTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
