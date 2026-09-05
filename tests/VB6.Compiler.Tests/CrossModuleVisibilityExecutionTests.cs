namespace VB6.Compiler.Tests;

/// <summary>
/// What one standard module of a project can see in another.
///
/// A <c>Public Property Get</c> in a <c>.bas</c> is project-wide, exactly like a
/// <c>Public Function</c> — and it was not, because the project-wide lookup table is keyed by name
/// and Get, Let and Set share one. Building the accessors once for the whole project fixes both
/// halves of that: they become visible, and they keep a single identity, so the symbol a call in
/// another module resolves to is the one the declaring body was bound to. Two instances would have
/// left the caller pointing at a procedure with no body.
///
/// The negative cases matter as much: <c>Private</c> has to stay private, or the fix would have
/// made every module helper callable from everywhere.
/// </summary>
[TestClass]
public sealed class CrossModuleVisibilityExecutionTests
{
    [TestMethod]
    public void EmitManagedProject_ReachesPublicMembersOfAnotherModule()
    {
        RunInProject(
            store: """
                Option Explicit

                Private Backing As Long
                Public Shared As Long

                Public Property Get Level() As Long
                    Level = Backing
                End Property

                Public Property Let Level(ByVal newValue As Long)
                    Backing = newValue * 2
                End Property

                Public Function Doubled(ByVal value As Long) As Long
                    Doubled = value * 2
                End Function
                """,
            main: """
                Option Explicit

                Sub Main()
                    Shared = 3
                    Debug.Print "public-var|" & Shared
                    Debug.Print "public-fn|" & Doubled(4)
                    Debug.Print "qualified|" & Store.Doubled(5)

                    Level = 5
                    Debug.Print "property|" & Level
                End Sub
                """,
            expected: new[] { "public-var|3", "public-fn|8", "qualified|10", "property|10" });
    }

    [TestMethod]
    public void AnalyzeManagedProject_KeepsAPrivateModulePropertyOutOfOtherModules()
    {
        // A bare name that resolves to nothing is a variable reference, so the miss is VB6S0001.
        AssertRejected(
            "VB6S0001",
            store: """
                Option Explicit

                Private Backing As Long

                Private Property Get Hidden() As Long
                    Hidden = Backing
                End Property
                """,
            main: """
                Option Explicit

                Sub Main()
                    Debug.Print Hidden
                End Sub
                """);
    }

    [TestMethod]
    public void AnalyzeManagedProject_KeepsAPrivateFunctionOutOfOtherModules()
    {
        // A call with parentheses is a procedure reference, so the miss is VB6S0005 instead.
        AssertRejected(
            "VB6S0005",
            store: """
                Option Explicit

                Private Function Hidden() As Long
                    Hidden = 1
                End Function
                """,
            main: """
                Option Explicit

                Sub Main()
                    Debug.Print Hidden()
                End Sub
                """);
    }

    private static void RunInProject(string store, string main, string[] expected)
    {
        WithProject(store, main, projectPath =>
        {
            var output = VB6TestProgram.RunProjectLines(projectPath);
            CollectionAssert.AreEqual(expected, output);
        });
    }

    private static void AssertRejected(string code, string store, string main)
    {
        WithProject(store, main, projectPath =>
        {
            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Diagnostics.Any(diagnostic => diagnostic.Code == code),
                $"Erwartet {code}, gemeldet: " + (analysis.Diagnostics.Length == 0
                    ? "gar keine Diagnose"
                    : string.Join(", ", analysis.Diagnostics.Select(diagnostic => diagnostic.Code))));
        });
    }

    private static void WithProject(string store, string main, Action<string> body)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerCrossModuleTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "CrossModule.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="CrossModule"
                Module=Store; Store.bas
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Store.bas"), store);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), main);
            body(projectPath);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
