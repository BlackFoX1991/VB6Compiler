using VB6.Runtime.WinForms;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: vb6winforms <generated-assembly> [arguments]");
    return 1;
}

return GeneratedApplicationRunner.Run(args[0], args.Skip(1).ToArray());
