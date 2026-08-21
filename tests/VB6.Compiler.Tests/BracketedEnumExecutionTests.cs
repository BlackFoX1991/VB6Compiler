namespace VB6.Compiler.Tests;

[TestClass]
public sealed class BracketedEnumExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesBracketedEnumMembers()
    {
        const string source = """
            Public Enum GradientDirection
                [GR_Fill_None] = -1
                [gr_Fill_Horizontal] = 0
                [GR_Fill_Vertical] = 1
            End Enum

            Sub Main()
                Debug.Print [GR_Fill_None]
                Debug.Print [GR_Fill_Vertical]
            End Sub
            """;

        var standardOutput = VB6TestProgram.Run(VBCompilation.Create(source, "Module1.bas"));
        CollectionAssert.AreEqual(
            new[] { "-1", "1" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
