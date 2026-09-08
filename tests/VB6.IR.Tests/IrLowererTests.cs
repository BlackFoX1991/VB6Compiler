using VB6.Compiler;
using VB6.Semantics;

namespace VB6.IR.Tests;

[TestClass]
public sealed class IrLowererTests
{
    [TestMethod]
    public void Lower_IfUsesExplicitBasicBlocks()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Long
                x = 1
                If x = 1 Then
                    Debug.Print 10
                Else
                    Debug.Print 20
                End If
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Blocks.Any(block => block.Terminator is IrConditionalTerminator));
        Assert.IsFalse(main.Blocks.SelectMany(block => block.Instructions)
            .Any(instruction => instruction.GetType().Name.Contains("If", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Lower_ByRefExpressionMaterializesTemporaryAddress()
    {
        var analysis = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Bump 1 + 2
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Locals.Any(local => local.IsCompilerGenerated && local.Name.Contains("byref", StringComparison.Ordinal)));
        var call = main.Blocks.SelectMany(block => block.Instructions)
            .OfType<IrEvaluateInstruction>()
            .Select(instruction => instruction.Expression)
            .OfType<IrProcedureCallExpression>()
            .Single();
        Assert.AreEqual(IrCallArgumentKind.Address, call.Arguments.Single().Kind);
        Assert.IsInstanceOfType<IrAddressExpression>(call.Arguments.Single().Expression);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalLongCreatesOneAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Long
                Dim pointer As Long
                value = 7
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(1, main.AddressableCells.Count);
        var pair = main.AddressableCells.Single();
        Assert.AreEqual(TypeSymbol.Long, pair.Key.Type);
        Assert.AreEqual(TypeSymbol.Variant, pair.Value.Type);

        var pointer = main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .OfType<IrAddressablePointerExpression>()
            .Single();
        Assert.AreSame(pair.Key, pointer.Local);
        Assert.AreSame(pair.Value, pointer.Cell);
        Assert.AreEqual(TypeSymbol.Long, pointer.ResultType);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalIntegerCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Integer
                Dim pointer As Long
                value = 7
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        var pair = main.AddressableCells.Single();
        Assert.AreEqual(TypeSymbol.Integer, pair.Key.Type);
        Assert.AreEqual(TypeSymbol.Variant, pair.Value.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalByteCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Byte
                Dim pointer As Long
                value = 7
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Byte, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalBooleanCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Boolean
                Dim pointer As Long
                value = True
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Boolean, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalSingleCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Single
                Dim pointer As Long
                value = 1.5
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Single, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalDoubleCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Double
                Dim pointer As Long
                value = 1.5
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Double, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalDateCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Date
                Dim pointer As Long
                value = CDate(1.5)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Date, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalCurrencyCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As Currency
                Dim pointer As Long
                value = CCur(1.5)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.Currency, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalLongLongCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As LongLong
                Dim pointer As Long
                value = 72623859790382856
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.LongLong, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalLongPtrCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As LongPtr
                Dim pointer As Long
                value = CLngPtr(7)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.LongPtr, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalUShortCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As UShort
                Dim pointer As Long
                value = CUShort(7)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.UShort, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalUIntegerCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As UInteger
                Dim pointer As Long
                value = CUInt(7)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.UInteger, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForLocalULongCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As ULong
                Dim pointer As Long
                value = CULng(7)
                pointer = VarPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.ULong, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredStrPtrForLocalStringCreatesAnAddressableCell()
    {
        var program = Lower("""
            Sub Main()
                Dim value As String
                Dim pointer As Long
                value = "abc"
                pointer = StrPtr(value)
            End Sub
            """);
        var main = program.EntryPoint!;

        Assert.IsNotNull(main.AddressableCells);
        Assert.AreEqual(TypeSymbol.String, main.AddressableCells.Single().Key.Type);
        Assert.IsInstanceOfType<IrAddressablePointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressablePointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForAModuleVariableRecordsAnAddressableGlobal()
    {
        var program = Lower("""
            Private total As Long

            Sub Main()
                Dim pointer As Long
                total = 7
                pointer = VarPtr(total)
            End Sub
            """);
        var main = program.EntryPoint!;

        // Die Zelle einer Modulvariablen ist kein Local -- sie gehoert dem Programm, weil der
        // Emitter sie als statisches Feld anlegt.
        Assert.IsNull(main.AddressableCells);
        var global = program.AddressableGlobals.Single();
        Assert.AreEqual(TypeSymbol.Long, global.Type);

        var pointer = main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .OfType<IrAddressableGlobalPointerExpression>()
            .Single();
        Assert.AreSame(global, pointer.Global);
        Assert.AreEqual(TypeSymbol.Long, pointer.ResultType);
    }

    [TestMethod]
    public void Lower_StrPtrForAModuleStringRecordsAnAddressableGlobal()
    {
        var program = Lower("""
            Private caption As String

            Sub Main()
                Dim pointer As Long
                caption = "abc"
                pointer = StrPtr(caption)
            End Sub
            """);

        Assert.AreEqual(TypeSymbol.String, program.AddressableGlobals.Single().Type);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForAByValParameterCreatesOneAddressableCell()
    {
        var program = Lower("""
            Sub Zeige(ByVal wert As Long)
                Dim pointer As Long
                pointer = VarPtr(wert)
            End Sub

            Sub Main()
                Zeige 7
            End Sub
            """);
        var show = program.Modules
            .SelectMany(module => module.Procedures)
            .Single(procedure => procedure.Name == "__vb6_Zeige");

        // Die Kopie eines ByVal-Parameters gehoert der Prozedur, ihre Zelle also auch: ein Local,
        // kein statisches Feld wie bei einer Modulvariablen.
        Assert.IsNull(show.AddressableCells);
        Assert.IsNotNull(show.AddressableParameterCells);
        var pair = show.AddressableParameterCells.Single();
        Assert.AreEqual(TypeSymbol.Long, pair.Key.Type);
        Assert.AreEqual(TypeSymbol.Variant, pair.Value.Type);

        var pointer = show.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .OfType<IrAddressableParameterPointerExpression>()
            .Single();
        Assert.AreSame(pair.Key, pointer.Parameter);
        Assert.AreSame(pair.Value, pointer.Cell);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForAByRefParameterRecordsTheCallerAliasContract()
    {
        var program = Lower("""
            Sub Zeige(ByRef wert As Long)
                Dim pointer As Long
                pointer = VarPtr(wert)
            End Sub

            Sub Main()
                Dim wert As Long
            Zeige wert
            End Sub
            """);

        var show = program.Modules
            .SelectMany(module => module.Procedures)
            .Single(procedure => procedure.Name == "__vb6_Zeige");

        // Die Markierung gehoert dem Programm, weil erst der Emitter mit allen IR-Aufrufstellen
        // entscheiden kann, ob die Zelle des Aufrufers sicher durchgereicht werden darf. Im
        // Aufgerufenen entsteht weiterhin keine zweite Parameterzelle.
        Assert.AreSame(show.Parameters.Single(), program.AddressableByRefParameters.Single());
        foreach (var procedure in program.Modules.SelectMany(module => module.Procedures))
        {
            Assert.IsNull(procedure.AddressableParameterCells, procedure.Name);
        }

        var pointer = show.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .OfType<IrAddressableByRefParameterPointerExpression>()
            .Single();
        Assert.AreSame(show.Parameters.Single(), pointer.Parameter);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForARecordMemberPointsIntoTheRecordCell()
    {
        var program = Lower("""
            Private Type Innen
                A As Long
                B As Long
            End Type

            Private Type Punkt
                X As Long
                Tief As Innen
            End Type

            Sub Main()
                Dim p As Punkt
                Dim ganz As Long
                Dim tief As Long
                ganz = VarPtr(p)
                tief = VarPtr(p.Tief.B)
            End Sub
            """);
        var main = program.EntryPoint!;

        // Eine Zelle, nicht zwei: Der Datensatz traegt sie, der Memberzeiger zeigt nur hinein.
        // Sonst koennten VarPtr(p) + Offset und VarPtr(p.Tief.B) auseinanderlaufen.
        Assert.AreEqual(1, main.AddressableCells!.Count);

        var pointers = main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .OfType<IrAddressablePointerExpression>()
            .ToArray();
        Assert.AreEqual(2, pointers.Length);
        Assert.AreSame(pointers[0].Cell, pointers[1].Cell);
        Assert.IsNull(pointers[0].MemberPath);
        Assert.AreEqual("Tief.B", pointers[1].MemberPath);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForARecordWithItsOwnStorageStaysUnaddressable()
    {
        // Ein Arraymember besitzt Speicher neben dem Datensatz; sein Layout ist ein eigener
        // Vertrag und nicht der, den der Marshaller fuer einen flachen Block kennt.
        var program = Lower("""
            Private Type MitFeld
                Werte(3) As Long
            End Type

            Sub Main()
                Dim f As MitFeld
                Dim zeiger As Long
                zeiger = VarPtr(f)
            End Sub
            """);

        Assert.IsNull(program.EntryPoint!.AddressableCells);
    }

    [TestMethod]
    public void Lower_StoredVarPtrForAnArrayElementNeedsNoCell()
    {
        var program = Lower("""
            Sub Main()
                Dim feld(3) As Long
                Dim zeiger As Long
                feld(1) = 7
                zeiger = VarPtr(feld(1))
            End Sub
            """);
        var main = program.EntryPoint!;

        // Keine Zelle: Das Array besitzt den einzigen Speicher, und die Runtime macht ihn
        // unbeweglich. Ein Abbild daneben wuerde veralten, sobald jemand ueber eine zweite
        // Referenz auf das Array schreibt -- und eine Arrayreferenz reist.
        Assert.IsNull(main.AddressableCells);
        Assert.AreEqual(0, program.AddressableGlobals.Length);
        Assert.IsInstanceOfType<IrAddressableArrayPointerExpression>(main.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Select(instruction => instruction.Value)
            .Single(value => value is IrAddressableArrayPointerExpression));
    }

    [TestMethod]
    public void Lower_StoredVarPtrForARectangularArrayElementStaysUnaddressable()
    {
        // Bei mehr als einer Dimension ist die physische Reihenfolge hier eine andere als die,
        // die ein VB6-SAFEARRAY ablaeuft. Ein Zeiger darauf waere ueber den Block irrefuehrend.
        var program = Lower("""
            Sub Main()
                Dim feld(2, 2) As Long
                Dim zeiger As Long
                zeiger = VarPtr(feld(1, 1))
            End Sub
            """);

        Assert.IsFalse(program.EntryPoint!.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<IrStoreInstruction>()
            .Any(instruction => instruction.Value is IrAddressableArrayPointerExpression));
    }

    [TestMethod]
    public void Lower_ForAndExitUseBranchesInsteadOfStructuredLoop()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim i As Long
                For i = 1 To 10
                    If i = 3 Then Exit For
                Next i
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Blocks.Count(block => block.Terminator is IrGotoTerminator) >= 2);
        Assert.IsTrue(main.Blocks.Any(block => block.Label.Contains("for_exit", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Lower_DebugAssertElidesTheConditionExpression()
    {
        var analysis = VBCompilation.Create("""
            Function Mark() As Boolean
                Mark = True
            End Function

            Sub Main()
                Debug.Assert Mark()
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsFalse(main.Blocks.SelectMany(block => block.Instructions)
            .OfType<IrEvaluateInstruction>()
            .Select(instruction => instruction.Expression)
            .OfType<IrProcedureCallExpression>()
            .Any(call => call.Procedure.Name.Equals("Mark", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Lower_CrossUdtLSetUsesManagedDestinationAddressForScalarLayouts()
    {
        var program = Lower("""
            Type SourceRecord
                Prefix As Byte
                Value As Long
            End Type

            Type TargetRecord
                Value As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord
                LSet target = source
            End Sub
            """);

        var lsetCalls = RuntimeCalls(program)
            .Where(call => call.Method == IrRuntimeMethod.MemoryLSet)
            .ToArray();

        Assert.AreEqual(1, lsetCalls.Length);
        var transfer = lsetCalls.Single();
        Assert.AreEqual(TypeSymbol.Error, transfer.ResultType);
        var descriptor = transfer.Arguments[0].Expression;
        Assert.IsInstanceOfType<IrAddressExpression>(
            descriptor);
        Assert.AreEqual(
            IrCallArgumentKind.Address,
            transfer.Arguments[0].Kind);
    }

    [TestMethod]
    public void Lower_CrossUdtLSetKeepsReferenceLayoutsOnGuardedRuntimePath()
    {
        var program = Lower("""
            Type SourceRecord
                Value As String
            End Type

            Type TargetRecord
                Value As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord
                LSet target = source
            End Sub
            """);

        var call = RuntimeCalls(program)
            .Single(runtime => runtime.Method == IrRuntimeMethod.MemoryLSet);
        Assert.AreEqual(TypeSymbol.Error, call.ResultType);
        Assert.IsInstanceOfType<IrLoadExpression>(call.Arguments[0].Expression);
        Assert.IsInstanceOfType<IrLoadExpression>(call.Arguments[1].Expression);
    }

    private static IrProgram Lower(string source)
    {
        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        return IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
    }

    private static IEnumerable<IrRuntimeCallExpression> RuntimeCalls(IrProgram program)
    {
        foreach (var expression in program.EntryPoint!.Blocks
                     .SelectMany(block => block.Instructions)
                     .OfType<IrEvaluateInstruction>()
                     .Select(instruction => instruction.Expression))
        {
            foreach (var call in RuntimeCalls(expression))
            {
                yield return call;
            }
        }
    }

    private static IEnumerable<IrRuntimeCallExpression> RuntimeCalls(IrExpression expression)
    {
        if (expression is IrRuntimeCallExpression call)
        {
            yield return call;
            foreach (var argument in call.Arguments)
            {
                foreach (var nested in RuntimeCalls(argument.Expression))
                {
                    yield return nested;
                }
            }
        }
    }
}
