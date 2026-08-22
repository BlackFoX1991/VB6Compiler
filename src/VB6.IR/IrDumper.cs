using System.Text;

namespace VB6.IR;

public static class IrDumper
{
    public static string Dump(IrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var builder = new StringBuilder();

        foreach (var module in program.Modules)
        {
            builder.Append("module ").AppendLine(module.Name);
            foreach (var procedure in module.Procedures)
            {
                builder.Append("  proc ").Append(procedure.Name).Append('(')
                    .Append(string.Join(", ", procedure.Parameters.Select(p => p.Name)))
                    .AppendLine(")");

                foreach (var block in procedure.Blocks)
                {
                    builder.Append("    ").Append(block.Label).AppendLine(":");
                    foreach (var instruction in block.Instructions)
                    {
                        builder.Append("      ").AppendLine(FormatInstruction(instruction));
                    }

                    builder.Append("      ").AppendLine(FormatTerminator(block.Terminator));
                }
            }
        }

        foreach (var @class in program.ClassDefinitions)
        {
            builder.Append("class ").AppendLine(@class.Name);
            foreach (var procedure in @class.Methods)
            {
                builder.Append("  proc ").Append(procedure.Name).Append('(')
                    .Append(string.Join(", ", procedure.Parameters.Select(p => p.Name)))
                    .AppendLine(")");
            }
        }

        return builder.ToString();
    }

    private static string FormatInstruction(IrInstruction instruction) => instruction switch
    {
        IrStoreInstruction store => $"store {FormatPlace(store.Target)}, {FormatExpression(store.Value)}",
        IrStoreAddressInstruction address => $"store-address %{address.AddressLocal.Name}, {FormatExpression(address.Address)}",
        IrEvaluateInstruction evaluate => $"eval {FormatExpression(evaluate.Expression)}",
        IrBaseFinalizeInstruction => "base-finalize",
        IrSubscribeEventInstruction subscribe => $"subscribe {subscribe.Event.Name} -> {subscribe.Handler.Name}",
        _ => "nop"
    };

    private static string FormatTerminator(IrTerminator terminator) => terminator switch
    {
        IrGotoTerminator go => $"br block_{go.TargetBlockId}",
        IrConditionalTerminator conditional =>
            $"brtrue {FormatExpression(conditional.Condition)} block_{conditional.TrueBlockId} block_{conditional.FalseBlockId}",
        IrGoSubTerminator goSub => $"gosub block_{goSub.TargetBlockId} return_{goSub.ReturnIndex}",
        IrGoSubReturnTerminator goSubReturn =>
            $"gosub-return [{string.Join(", ", goSubReturn.ReturnTargetBlockIds.Select(id => $"block_{id}"))}]",
        IrOnGoToTerminator onGoTo =>
            $"on-goto {FormatExpression(onGoTo.Index)} [{string.Join(", ", onGoTo.TargetBlockIds.Select(id => $"block_{id}"))}] default block_{onGoTo.DefaultBlockId}",
        IrOnGoSubTerminator onGoSub =>
            $"on-gosub {FormatExpression(onGoSub.Index)} [{string.Join(", ", onGoSub.TargetBlockIds.Select(id => $"block_{id}"))}] return_{onGoSub.ReturnIndex} default block_{onGoSub.DefaultBlockId}",
        IrReturnTerminator ret when ret.Value is not null => $"ret {FormatExpression(ret.Value)}",
        IrReturnTerminator => "ret",
        _ => "ret"
    };

    private static string FormatPlace(IrPlace place) => place switch
    {
        IrLocalPlace local => $"%{local.Local.Name}",
        IrParameterPlace parameter => $"@{parameter.Parameter.Name}",
        IrGlobalPlace global => $"global::{global.Global.Name}",
        IrFieldPlace field => $"{FormatPlace(field.Receiver)}.{field.Field.Name}",
        IrArrayElementPlace element => $"{FormatExpression(element.Array)}[...]",
        IrIndirectPlace indirect => $"*{FormatExpression(indirect.Address)}",
        IrThisPlace thisPlace => $"this<{thisPlace.ClassType.Name}>",
        IrAccessorPlace => "<accessor>",
        _ => "<place>"
    };

    private static string FormatExpression(IrExpression expression) => expression switch
    {
        IrConstantExpression constant => constant.Value?.ToString() ?? "null",
        IrDefaultExpression => "default",
        IrLoadExpression load => $"load {FormatPlace(load.Place)}",
        IrAddressExpression address => $"addr {FormatPlace(address.Place)}",
        IrLocalAddressExpression address => $"addr-local %{address.Local.Name}",
        IrRuntimeCallExpression call => $"runtime::{call.Method}(...) ",
        IrProcedureCallExpression call => $"call {call.Procedure.Name}(...) ",
        IrSyntheticCallExpression call => $"call {call.Procedure.Name}(...) ",
        IrNewVBArrayExpression => "new VBArray(...) ",
        IrNewClassExpression @new => $"new {@new.ClassType.Name}() ",
        IrTypeOfExpression typeOf => $"typeof({typeOf.TargetType.Name}, ...) ",
        IrEnsureArrayExpression ensure => $"ensure-array {FormatPlace(ensure.Storage)}",
        IrCopyArrayExpression copy => $"copy-array {FormatExpression(copy.Source)}",
        IrReDimPreserveExpression => "redim-preserve(...) ",
        _ => "<expr>"
    };
}
