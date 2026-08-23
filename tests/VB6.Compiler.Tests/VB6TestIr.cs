using System.Collections.Immutable;
using VB6.IR;

namespace VB6.Compiler.Tests;

/// <summary>
/// Lowers a program and walks the result.
///
/// Tests that used to assert on generated C# text assert on the IR instead: it is the compiler's
/// own representation, it is typed, and unlike a source string it cannot pass by accident because
/// an unrelated line happened to contain the expected substring.
/// </summary>
internal static class VB6TestIr
{
    /// <summary>Lowers one source file, failing the test with the diagnostics if it cannot.</summary>
    public static IrProgram Lower(string source, string fileName = "Module1.bas")
    {
        var lowering = VBCompilation.Create(source, fileName).Lower();
        Assert.IsTrue(
            lowering.Success,
            string.Join(Environment.NewLine, lowering.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return lowering.Program!;
    }

    /// <summary>Lowers a project, failing the test with the diagnostics if it cannot.</summary>
    public static IrProgram LowerProject(string projectPath)
    {
        var lowering = VBProjectCompilation.Create(projectPath).Lower();
        Assert.IsTrue(
            lowering.Success,
            string.Join(
                Environment.NewLine,
                lowering.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                    .Concat(lowering.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));
        return lowering.Program!;
    }

    /// <summary>Every runtime operation the program calls, in no particular order.</summary>
    public static IReadOnlyCollection<IrRuntimeMethod> RuntimeCalls(IrProgram program) =>
        Expressions(program).OfType<IrRuntimeCallExpression>().Select(call => call.Method).ToArray();

    /// <summary>Every array operation the program performs, in no particular order.</summary>
    public static IReadOnlyCollection<IrArrayOperation> ArrayCalls(IrProgram program) =>
        Expressions(program).OfType<IrArrayCallExpression>().Select(call => call.Operation).ToArray();

    /// <summary>Every procedure of the program, including the methods of its type definitions.</summary>
    public static IEnumerable<IrProcedure> Procedures(IrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return program.Modules.SelectMany(module => module.Procedures)
            .Concat(program.TypeDefinitions.SelectMany(type => type.Methods))
            .Concat(program.ClassDefinitions.SelectMany(@class => @class.Methods));
    }

    /// <summary>Every expression anywhere in the program, parents before children.</summary>
    public static IReadOnlyCollection<IrExpression> Expressions(IrProgram program)
    {
        var collected = new List<IrExpression>();
        foreach (var block in Procedures(program).SelectMany(procedure => procedure.Blocks))
        {
            foreach (var instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IrStoreInstruction store:
                        Visit(store.Target, collected);
                        Visit(store.Value, collected);
                        break;
                    case IrVariantArraySetInstruction set:
                        Visit(set.Array, collected);
                        foreach (var argument in set.Arguments)
                        {
                            Visit(argument, collected);
                        }

                        Visit(set.Value, collected);
                        break;
                    case IrStoreAddressInstruction storeAddress:
                        Visit(storeAddress.Address, collected);
                        break;
                    case IrEvaluateInstruction evaluate:
                        Visit(evaluate.Expression, collected);
                        break;
                }
            }

            switch (block.Terminator)
            {
                case IrConditionalTerminator conditional:
                    Visit(conditional.Condition, collected);
                    break;
                case IrReturnTerminator { Value: { } value }:
                    Visit(value, collected);
                    break;
            }
        }

        return collected;
    }

    private static void Visit(IrExpression expression, List<IrExpression> collected)
    {
        collected.Add(expression);
        switch (expression)
        {
            case IrLoadExpression load:
                Visit(load.Place, collected);
                break;
            case IrAddressExpression address:
                Visit(address.Place, collected);
                break;
            case IrRuntimeCallExpression call:
                VisitArguments(call.Arguments, collected);
                break;
            case IrProcedureCallExpression call:
                if (call.Receiver is not null)
                {
                    Visit(call.Receiver, collected);
                }

                VisitArguments(call.Arguments, collected);
                break;
            case IrSyntheticCallExpression call:
                if (call.Receiver is not null)
                {
                    Visit(call.Receiver, collected);
                }

                VisitArguments(call.Arguments, collected);
                break;
            case IrArrayCallExpression call:
                Visit(call.Array, collected);
                foreach (var argument in call.Arguments)
                {
                    Visit(argument, collected);
                }

                break;
            case IrNewVBArrayExpression newArray:
                VisitBounds(newArray.Bounds, collected);
                break;
            case IrReDimPreserveExpression preserve:
                Visit(preserve.Array, collected);
                VisitBounds(preserve.Bounds, collected);
                break;
            case IrTypeOfExpression typeOf:
                Visit(typeOf.Expression, collected);
                break;
            case IrEnsureArrayExpression ensure:
                Visit(ensure.Storage, collected);
                VisitBounds(ensure.Bounds, collected);
                break;
            case IrCopyArrayExpression copy:
                Visit(copy.Source, collected);
                VisitBounds(copy.Bounds, collected);
                break;
        }
    }

    private static void Visit(IrPlace place, List<IrExpression> collected)
    {
        switch (place)
        {
            case IrFieldPlace field:
                Visit(field.Receiver, collected);
                break;
            case IrArrayElementPlace element:
                Visit(element.Array, collected);
                foreach (var index in element.Indices)
                {
                    Visit(index, collected);
                }

                break;
            case IrArrayFlatElementPlace element:
                Visit(element.Array, collected);
                Visit(element.Index, collected);
                break;
            case IrIndirectPlace indirect:
                Visit(indirect.Address, collected);
                break;
            case IrAccessorPlace { Receiver: { } receiver }:
                Visit(receiver, collected);
                break;
        }
    }

    private static void VisitArguments(ImmutableArray<IrCallArgument> arguments, List<IrExpression> collected)
    {
        foreach (var argument in arguments)
        {
            Visit(argument.Expression, collected);
        }
    }

    private static void VisitBounds(ImmutableArray<IrArrayBound> bounds, List<IrExpression> collected)
    {
        foreach (var bound in bounds)
        {
            Visit(bound.Lower, collected);
            Visit(bound.Upper, collected);
        }
    }
}
