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

        return builder.ToString();
    }

    private static string FormatInstruction(IrInstruction instruction) => instruction switch
    {
        IrStoreInstruction store => $"store {FormatPlace(store.Target)}, {FormatExpression(store.Value)}",
        IrStoreAddressInstruction address => $"store-address %{address.AddressLocal.Name}, {FormatExpression(address.Address)}",
        IrEvaluateInstruction evaluate => $"eval {FormatExpression(evaluate.Expression)}",
        _ => "nop"
    };

    private static string FormatTerminator(IrTerminator terminator) => terminator switch
    {
        IrGotoTerminator go => $"br block_{go.TargetBlockId}",
        IrConditionalTerminator conditional =>
            $"brtrue {FormatExpression(conditional.Condition)} block_{conditional.TrueBlockId} block_{conditional.FalseBlockId}",
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
        IrEnsureArrayExpression ensure => $"ensure-array {FormatPlace(ensure.Storage)}",
        IrCopyArrayExpression copy => $"copy-array {FormatExpression(copy.Source)}",
        IrReDimPreserveExpression => "redim-preserve(...) ",
        _ => "<expr>"
    };
}
