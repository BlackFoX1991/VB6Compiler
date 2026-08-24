using VB6.IR;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectCompilationTests
{
    [TestMethod]
    public void Analyze_CombinesStandardModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory);
            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(2, analysis.Units.Length);
            Assert.IsNotNull(analysis.SemanticModel);
            Assert.AreEqual(4, analysis.SemanticModel!.Procedures.Length);

            var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
            var update = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Update");
            var observe = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Observe");
            var add = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Add");
            var invocations = main.Body.Statements.OfType<BoundInvocationStatement>().ToArray();
            var addAssignment = main.Body.Statements.OfType<BoundAssignmentStatement>().Last();
            var addInvocation = (BoundInvocationExpression)addAssignment.Expression;

            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundForStatement));
            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundWhileStatement));
            Assert.IsTrue(main.Body.Statements.Count(statement => statement is BoundDoStatement) >= 2);
            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundSelectCaseStatement));
            Assert.IsTrue(main.Body.Statements.Count(statement => statement is BoundIfStatement) >= 3);
            Assert.AreEqual(update.Symbol, invocations[0].Procedure);
            Assert.AreEqual(observe.Symbol, invocations[1].Procedure);
            Assert.AreEqual(add.Symbol, addInvocation.Procedure);
            Assert.AreEqual(ParameterPassingMode.ByRef, update.Symbol.Parameters.Single().PassingMode);
            Assert.AreEqual(ParameterPassingMode.ByVal, observe.Symbol.Parameters.Single().PassingMode);
            Assert.AreEqual(TypeSymbol.Integer, add.Symbol.ReturnType);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReadsWindowsAnsiEncodedProjectSources()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AnsiProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Module=Main; Main.bas
                """);
            File.WriteAllBytes(
                Path.Combine(directory, "Main.bas"),
                System.Text.Encoding.Latin1.GetBytes("Sub Main()\r\n    Debug.Print \"Grüße\"\r\nEnd Sub\r\n"));

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(0, analysis.Diagnostics.Length);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_EvaluatesConditionalCompilationInProjectSources()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ConditionalProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                CondComp=UseNew = 1
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                #If UseNew Then
                    Public Sub Main()
                        Debug.Print 2
                    End Sub
                #Else
                    Public Sub Main()
                        Debug.Print 1
                    End Sub
                #End If
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);

            Assert.AreEqual("2", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesControlFlowCrossModuleCallsAndFunction()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory);
            var standardOutput = VB6TestProgram.RunProject(projectPath);
            Assert.AreEqual("12", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_HandlesProjectConstantsInAllGlobalScopes()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Constants.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Constants"
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Private Const QTHRESH As Long = 42

                Public Sub Main()
                    Debug.Print QTHRESH
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);

            Assert.AreEqual("42", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_HandlesStaticLocalsInClassModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "StaticClass.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="StaticClass"
                Module=Main; Main.bas
                Class=Worker; Worker.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Public Sub Main()
                    Dim worker As Worker
                    Set worker = New Worker
                    worker.Touch
                    Debug.Print worker.Value
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Worker.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Worker"

                Private m_Value As Long

                Public Sub Touch()
                    Const Limit As Long = 1
                    Static calls(1 To Limit) As Long
                    calls(1) = calls(1) + 1
                    m_Value = calls(1)
                End Sub

                Public Property Get Value() As Long
                    Value = m_Value
                End Property
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);

            Assert.AreEqual("1", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ResolvesScopedDeclareSymbolsAcrossProjectModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ScopedDeclare.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ScopedDeclare"
                Module=Main; Main.bas
                Class=Worker; Worker.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Public Sub Main()
                    Dim worker As Worker
                    Set worker = New Worker
                    worker.Touch
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Worker.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Worker"

                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Public Sub Touch()
                    Dim source As Long
                    Dim destination As Long
                    source = 16909060
                    CopyMemory destination, source, 4
                    Debug.Print destination
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);

            Assert.AreEqual("16909060", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateProceduresAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Duplicate.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Duplicate"
                Module=First; First.bas
                Module=Second; Second.bas
                """);
            File.WriteAllText(Path.Combine(directory, "First.bas"), """
                Sub Helper()
                    Debug.Print 1
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Second.bas"), """
                Sub Helper()
                    Debug.Print 2
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0003"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateModuleVariablesAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DuplicateVariable.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="DuplicateVariable"
                Module=First; First.bas
                Module=Second; Second.bas
                """);
            File.WriteAllText(Path.Combine(directory, "First.bas"), """
                Public Counter As Long

                Sub Main()
                    Debug.Print 1
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Second.bas"), """
                Public Counter As Long
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0006"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsMissingReferencedVbpWithResolvedPath()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Consumer.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#..\Shared\Shared.vbp#Shared
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            var diagnostic = analysis.ProjectDiagnostics.Single(diagnostic => diagnostic.Code == "VB6PRJ0016");
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(directory, "..", "Shared", "Shared.vbp")),
                diagnostic.FilePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_BindsQualifiedActiveXTypesFromProjectObjects()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Controls.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.1#0; MSCOMCTL.OCX
                Object={3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0; RICHTX32.OCX
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Dim tree As MSComctlLib.TreeView
                    Dim node As MSComctlLib.Node
                    Dim editor As RichTextLib.RichTextBox
                    Set tree = Nothing
                    Set node = Nothing
                    Set editor = Nothing
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ImportsTypesFromWindowsTypeLibraryReference()
    {
        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "stdole2.tlb");
        if (!OperatingSystem.IsWindows() || !File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The Windows stdole2.tlb test fixture is not available.");
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "TypeLibrary.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Reference=*\G00020430-0000-0000-C000-000000000046#2.0#0#{typeLibraryPath}#stdole
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Dim picture As stdole.IPicture
                    Dim font As stdole.IFont
                    Dim standardFont As stdole.StdFont
                    Dim state As stdole.OLE_TRISTATE
                    state = stdole.OLE_TRISTATE.Checked
                    Set picture = Nothing
                    Set font = Nothing
                    Set standardFont = Nothing
                    Debug.Print picture.Handle
                    Debug.Print font.Name
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            var main = analysis.Units
                .Single(unit => string.Equals(unit.Item.Name, "Main", StringComparison.OrdinalIgnoreCase))
                .Analysis
                .SemanticModel!
                .Procedures
                .Single(procedure => string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));
            var stateAssignment = main.Body.Statements
                .OfType<BoundAssignmentStatement>()
                .Single(statement => string.Equals(statement.Variable.Name, "state", StringComparison.OrdinalIgnoreCase));
            var stateExpression = stateAssignment.Expression;
            while (stateExpression is BoundConversionExpression conversion)
            {
                stateExpression = conversion.Expression;
            }
            Assert.IsInstanceOfType<BoundLiteralExpression>(stateExpression);

            var standardFont = main.Locals.Single(local =>
                string.Equals(local.Name, "standardFont", StringComparison.OrdinalIgnoreCase));
            Assert.IsInstanceOfType<ClassTypeSymbol>(standardFont.Type);
            var standardFontType = (ClassTypeSymbol)standardFont.Type;
            Assert.IsTrue(standardFontType.TryGetEvent("FontChanged", out var fontChanged));
            Assert.AreEqual(1, fontChanged.Parameters.Length);
            Assert.IsTrue(fontChanged.ComInterfaceId.HasValue);
            Assert.IsTrue(fontChanged.ComDispId.HasValue);

            var stateConstant = analysis.SemanticModel!.ModuleVariables
                .Single(variable => string.Equals(variable.Symbol.Name, "Checked", StringComparison.OrdinalIgnoreCase));
            var stateLiteral = stateConstant.Initializer as BoundLiteralExpression;
            Assert.IsNotNull(stateLiteral);
            Assert.AreEqual(1L, stateLiteral!.Value);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ImportsRecordFieldsFromWindowsTypeLibraryReference()
    {
        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "stdole2.tlb");
        if (!OperatingSystem.IsWindows() || !File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The Windows stdole2.tlb test fixture is not available.");
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "TypeLibraryRecords.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Reference=*\G00020430-0000-0000-C000-000000000046#2.0#0#{typeLibraryPath}#stdole
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Dim identifier As stdole.GUID
                    identifier.Data1 = 1
                    identifier.Data2 = 2
                    identifier.Data3 = 3
                    Debug.Print identifier.Data1
                    Dim color As stdole.OLE_COLOR
                    color = 3
                    Dim exceptionInfo As stdole.EXCEPINFO
                    exceptionInfo.scode = 5
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            var main = analysis.Units
                .Single(unit => string.Equals(unit.Item.Name, "Main", StringComparison.OrdinalIgnoreCase))
                .Analysis
                .SemanticModel!
                .Procedures
                .Single(procedure => string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));
            var identifier = main.Locals.Single(local =>
                string.Equals(local.Name, "identifier", StringComparison.OrdinalIgnoreCase));
            Assert.IsInstanceOfType<UserDefinedTypeSymbol>(identifier.Type);
            var guid = (UserDefinedTypeSymbol)identifier.Type;
            Assert.AreEqual(4, guid.Members.Length);
            Assert.AreEqual(TypeSymbol.UInteger, guid.Members.Single(member => member.Name == "Data1").Type);
            Assert.AreEqual(TypeSymbol.UShort, guid.Members.Single(member => member.Name == "Data2").Type);
            Assert.AreEqual(TypeSymbol.UShort, guid.Members.Single(member => member.Name == "Data3").Type);
            Assert.AreSame(VBStandardTypes.Object, guid.Members.Single(member => member.Name == "Data4").Type);

            var color = main.Locals.Single(local =>
                string.Equals(local.Name, "color", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(TypeSymbol.UInteger, color.Type);

            var exceptionInfo = main.Locals.Single(local =>
                string.Equals(local.Name, "exceptionInfo", StringComparison.OrdinalIgnoreCase));
            Assert.IsInstanceOfType<UserDefinedTypeSymbol>(exceptionInfo.Type);
            var excepInfo = (UserDefinedTypeSymbol)exceptionInfo.Type;
            Assert.AreEqual(TypeSymbol.String, excepInfo.Members.Single(member => member.Name == "bstrSource").Type);
            Assert.AreEqual(TypeSymbol.UInteger, excepInfo.Members.Single(member => member.Name == "dwHelpContext").Type);

            Assert.AreEqual("1", VB6TestProgram.RunProject(projectPath).Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_BindsDesignerActiveXControlContracts()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DesignerControls.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="DesignerControls"
                Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.1#0; MSCOMCTL.OCX
                Object={3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0; RICHTX32.OCX
                Object={A0E7BF60-0D59-11D2-8E2F-00A0C9EAF7A1}#1.0#0; COMDLG32.OCX
                Form=Main.frm
                Module=Entry; Entry.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Entry.bas"), """
                Sub Main()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Main.frm"), """
                VERSION 5.00
                Begin VB.Form Main
                   Begin MSComctlLib.TreeView tree
                   End
                   Begin RichTextLib.RichTextBox editor
                   End
                   Begin MSComDlg.CommonDialog dialog
                   End
                   Begin MSComctlLib.ImageList images
                   End
                   Begin MSComctlLib.ImageCombo combo
                   End
                End
                Attribute VB_Name = "Main"
                Attribute VB_PredeclaredId = True
                Option Explicit

                Private Sub UseControls()
                    Dim node As MSComctlLib.Node
                    Set node = tree.Nodes.Add(, , "root", "Root")
                    Debug.Print node.Text
                    Debug.Print tree.Nodes.Count
                    Debug.Print tree.Nodes(1).Index
                    editor.SelText = "text"
                    editor.BackColor() = 1
                    editor.HideSelection() = True
                    dialog.Filter = "Text (*.txt)|*.txt"
                    dialog.ShowSave
                    Dim image As MSComctlLib.ListImage
                    Set image = images.ListImages.Add(1, "key")
                    Debug.Print image.Index
                    Dim item As MSComctlLib.ComboItem
                    Set item = combo.ComboItems.Add(, "key", "text")
                    item.Selected = True
                    combo.ComboItems(1).Selected = True
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            var form = analysis.SemanticModel!.ClassTypes.Single(type => type.Name == "Main");
            Assert.IsTrue(form.TryGetProperty("tree", PropertyAccessorKind.Get, out var tree));
            Assert.AreSame(VBStandardTypes.ExternalTreeView, tree!.Type);
            Assert.IsTrue(form.TryGetProperty("editor", PropertyAccessorKind.Get, out var editor));
            Assert.AreSame(VBStandardTypes.ExternalRichTextBox, editor!.Type);
            Assert.IsTrue(form.TryGetProperty("dialog", PropertyAccessorKind.Get, out var dialog));
            Assert.AreSame(VBStandardTypes.ExternalCommonDialog, dialog!.Type);
            Assert.IsTrue(form.TryGetProperty("images", PropertyAccessorKind.Get, out var images));
            Assert.AreSame(VBStandardTypes.ExternalImageList, images!.Type);
            Assert.IsTrue(form.TryGetProperty("combo", PropertyAccessorKind.Get, out var combo));
            Assert.AreSame(VBStandardTypes.ExternalImageCombo, combo!.Type);

            var result = compilation.EmitManagedApplication(Path.Combine(directory, "DesignerControls.dll"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Lowering.Analysis));
            Assert.IsTrue(File.Exists(result.AssemblyPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_BindsNestedIntrinsicControlArraysFromDesigner()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ControlArray.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Form=Main.frm
                Module=Entry; Entry.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Entry.bas"), """
                Sub Main()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Main.frm"), """
                VERSION 5.00
                Begin VB.Form Main
                   Begin VB.Frame HostFrame
                      Begin VB.CommandButton Buttons
                         Index = 0
                         Caption = "First"
                      End
                   End
                End
                Attribute VB_Name = "Main"
                Option Explicit

                Private Sub UseButtons()
                    Buttons(0).Caption = "Changed"
                    Debug.Print Buttons.UBound
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(1, analysis.Designers.Length);
            var form = analysis.SemanticModel!.ClassTypes.Single(type => type.Name == "Main");
            Assert.IsTrue(form.TryGetProperty("Buttons", PropertyAccessorKind.Get, out var buttons));
            var buttonArray = buttons!.Type as ArrayTypeSymbol;
            Assert.IsNotNull(buttonArray);
            Assert.AreSame(VBStandardTypes.Control, buttonArray!.ElementType);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_EmitsFormStartupProject()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "FormApp.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Splash"
                Name="FormApp"
                Form=Splash.frm
                """);
            File.WriteAllText(Path.Combine(directory, "Splash.frm"), """
                VERSION 5.00
                Begin VB.Form Splash
                   Caption = "Splash"
                   Begin VB.Frame Frame1
                      Begin VB.CommandButton StartButton
                         Caption = "Start"
                      End
                   End
                End
                Attribute VB_Name = "Splash"
                Attribute VB_PredeclaredId = True
                Option Explicit

                Private Sub Form_Load()
                End Sub
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "FormApp.dll"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Lowering.Analysis));
            Assert.IsNotNull(result.Lowering.Program);
            Assert.AreEqual("Main", result.Lowering.Program!.EntryPoint!.Name);
            var startupInstructions = result.Lowering.Program.EntryPoint.Blocks
                .Single()
                .Instructions;
            CollectionAssert.AreEquivalent(
                new[]
                {
                    IrRuntimeMethod.InteractionLoad,
                    IrRuntimeMethod.InteractionShow
                },
                startupInstructions
                    .OfType<IrEvaluateInstruction>()
                    .Select(instruction => instruction.Expression)
                    .OfType<IrRuntimeCallExpression>()
                    .Select(call => call.Method)
                    .ToArray());

            var formClass = result.Lowering.Program.ClassDefinitions
                .Single(classDefinition => classDefinition.Name == "Splash");
            var constructor = formClass.Methods.Single(method => method.Name == ".ctor");
            Assert.IsTrue(
                constructor.Blocks
                    .SelectMany(block => block.Instructions)
                    .OfType<IrStoreInstruction>()
                    .Select(store => store.Value)
                    .OfType<IrRuntimeCallExpression>()
                    .Any(call => call.Method == IrRuntimeMethod.InteractionCreateControl));
            var controlNames = constructor.Blocks
                .SelectMany(block => block.Instructions)
                .OfType<IrStoreInstruction>()
                .Select(store => store.Value)
                .OfType<IrRuntimeCallExpression>()
                .Where(call => call.Method == IrRuntimeMethod.InteractionCreateControl)
                .Select(call => ((IrConstantExpression)call.Arguments[1].Expression).Value)
                .Cast<string>()
                .ToArray();
            CollectionAssert.Contains(controlNames, "Frame1");
            CollectionAssert.Contains(controlNames, "Frame1.StartButton");
            var designerInitializers = constructor.Blocks
                .SelectMany(block => block.Instructions)
                .OfType<IrEvaluateInstruction>()
                .Select(instruction => instruction.Expression)
                .OfType<IrRuntimeCallExpression>()
                .Where(call => call.Method == IrRuntimeMethod.InteractionSetMember)
                .ToArray();
            Assert.AreEqual(2, designerInitializers.Length);
            CollectionAssert.AreEquivalent(
                new[] { "Splash", "Start" },
                designerInitializers
                    .Select(call => ((IrConstantExpression)call.Arguments[2].Expression).Value)
                    .Cast<string>()
                    .ToArray());
            foreach (var initializer in designerInitializers)
            {
                Assert.AreEqual("Caption", ((IrConstantExpression)initializer.Arguments[1].Expression).Value);
            }
            Assert.IsTrue(File.Exists(result.AssemblyPath));
            Assert.AreEqual(string.Empty, VB6TestProgram.RunProject(projectPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_EmitsShapeAndLineDesignerValues()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DrawingApp.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Splash"
                Name="DrawingApp"
                Form=Splash.frm
                """);
            File.WriteAllText(Path.Combine(directory, "Splash.frm"), """
                VERSION 5.00
                Begin VB.Form Splash
                   Begin VB.Shape Oval
                      BackColor = &H000000FF&
                      BorderColor = &H00000000&
                      BorderWidth = 2
                      Height = 1440
                      Shape = 2
                      Width = 1440
                   End
                   Begin VB.Line Diagonal
                      BorderColor = &H00FF0000&
                      BorderWidth = 2
                      X1 = 0
                      X2 = 1440
                      Y1 = 0
                      Y2 = 1440
                   End
                   Begin VB.Menu FileMenu
                      Caption = "File"
                      Begin VB.Menu OpenMenu
                         Caption = "Open"
                      End
                   End
                End
                Attribute VB_Name = "Splash"
                Attribute VB_PredeclaredId = True
                Option Explicit
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "DrawingApp.dll"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Lowering.Analysis));
            var formClass = result.Lowering.Program!.ClassDefinitions
                .Single(classDefinition => classDefinition.Name == "Splash");
            var constructor = formClass.Methods.Single(method => method.Name == ".ctor");
            var initializers = constructor.Blocks
                .SelectMany(block => block.Instructions)
                .OfType<IrEvaluateInstruction>()
                .Select(instruction => instruction.Expression)
                .OfType<IrRuntimeCallExpression>()
                .Where(call => call.Method == IrRuntimeMethod.InteractionSetMember)
                .Select(call => ((IrConstantExpression)call.Arguments[1].Expression).Value)
                .Cast<string>()
                .ToArray();

            var controlTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var call in constructor.Blocks
                .SelectMany(block => block.Instructions)
                .OfType<IrStoreInstruction>()
                .Select(store => store.Value)
                .OfType<IrRuntimeCallExpression>()
                .Where(call => call.Method == IrRuntimeMethod.InteractionCreateControl))
            {
                var controlName = ((IrConstantExpression)call.Arguments[1].Expression).Value as string
                    ?? throw new InvalidOperationException("Designer control name was not emitted as a string.");
                var controlType = ((IrConstantExpression)call.Arguments[2].Expression).Value as string
                    ?? throw new InvalidOperationException("Designer control type was not emitted as a string.");
                controlTypes.Add(controlName, controlType);
            }

            Assert.AreEqual("Shape", controlTypes["Oval"]);
            Assert.AreEqual("Line", controlTypes["Diagonal"]);
            Assert.AreEqual("Menu", controlTypes["FileMenu"]);
            Assert.AreEqual("Menu", controlTypes["FileMenu.OpenMenu"]);
            CollectionAssert.Contains(initializers, "BorderColor");
            CollectionAssert.Contains(initializers, "BorderWidth");
            CollectionAssert.Contains(initializers, "Shape");
            CollectionAssert.Contains(initializers, "X1");
            CollectionAssert.Contains(initializers, "Y2");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedLibrary_CompilesPropertyPageAndUserDocumentSources()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "LegacyControl.vbp");
            File.WriteAllText(projectPath, """
                Type=Control
                Name="LegacyControl"
                PropertyPage=Options.pag
                UserDocument=Document.dob
                """);
            File.WriteAllText(Path.Combine(directory, "Options.pag"), """
                VERSION 5.00
                Begin VB.PropertyPage Options
                End
                Attribute VB_Name = "Options"
                Option Explicit

                Public Function Value() As Long
                    Value = 1
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Document.dob"), """
                VERSION 5.00
                Begin VB.UserDocument Document
                End
                Attribute VB_Name = "Document"
                Option Explicit

                Public Function Value() As Long
                    Value = 2
                End Function
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "LegacyControl.dll"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Lowering.Analysis));
            CollectionAssert.AreEquivalent(
                new[] { "Options", "Document" },
                result.Lowering.Analysis.SemanticModel!.ClassTypes.Select(type => type.Name).ToArray());
            Assert.IsTrue(File.Exists(result.AssemblyPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedLibrary_CompilesLegacyDesignerSources()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "LegacyData.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name="LegacyData"
                Designer=MSDataEnvironment; DataEnvironment1.dsr
                """);
            File.WriteAllText(Path.Combine(directory, "DataEnvironment1.dsr"), """
                VERSION 5.00
                Begin MSDataEnvironment DataEnvironment1
                End
                Attribute VB_Name = "DataEnvironment1"
                Option Explicit

                Public Function Value() As Long
                    Value = 3
                End Function
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "LegacyData.dll"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Lowering.Analysis));
            Assert.AreEqual(1, result.Lowering.Analysis.Designers.Length);
            CollectionAssert.Contains(
                result.Lowering.Analysis.SemanticModel!.ClassTypes.Select(type => type.Name).ToArray(),
                "DataEnvironment1");
            Assert.IsTrue(File.Exists(result.AssemblyPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_BindsClassTypesFromReferencedVbp()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var libraryPath = Path.Combine(directory, "Shared.vbp");
            File.WriteAllText(libraryPath, """
                Type=OleDll
                Name=Shared
                Class=Customer; Customer.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Customer.cls"), """
                Public Function Value() As Long
                    Value = 7
                End Function
                """);

            var consumerPath = Path.Combine(directory, "Consumer.vbp");
            File.WriteAllText(consumerPath, """
                Type=Exe
                Startup="Sub Main"
                Name=Consumer
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Dim customer As Shared.Customer
                    Set customer = New Shared.Customer
                    Debug.Print customer.Value
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(consumerPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsNotNull(analysis.SemanticModel);
            var main = analysis.SemanticModel!.Procedures.Single(procedure =>
                string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));
            var customer = main.Locals.Single(variable => variable.Name == "customer");
            Assert.AreEqual("Customer", customer.Type.Name);

            var libraryEmit = VBProjectCompilation.Create(libraryPath)
                .EmitManagedApplication(Path.Combine(directory, "Shared.dll"));
            Assert.IsTrue(libraryEmit.Success, FormatDiagnostics(libraryEmit.Lowering.Analysis));

            var consumerEmit = VBProjectCompilation.Create(consumerPath)
                .EmitManagedApplication(Path.Combine(directory, "Consumer.dll"));
            Assert.IsTrue(consumerEmit.Success, FormatDiagnostics(consumerEmit.Lowering.Analysis));
            Assert.IsTrue(File.Exists(consumerEmit.AssemblyPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsProjectReferenceCycles()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "First.vbp"), """
                Type=OleDll
                Name=First
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Second.vbp#Second
                """);
            File.WriteAllText(Path.Combine(directory, "Second.vbp"), """
                Type=OleDll
                Name=Second
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#First.vbp#First
                """);

            var analysis = VBProjectCompilation.Create(Path.Combine(directory, "First.vbp")).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0017"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// A module with a syntax error still declares its procedures. The parser is fault-tolerant on
    /// purpose, so a procedure whose own header parsed is a real declaration - and hiding it turns
    /// one parser gap into a "not declared" error at every call site. In the conformance corpus a
    /// single syntax error suppressed one procedure and produced 30 such errors across seven files.
    /// </summary>
    [TestMethod]
    public void Analyze_DeclaresProceduresFromModulesThatStillHaveSyntaxErrors()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Partial.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Partial"
                Module=Broken; Broken.bas
                Module=Caller; Caller.bas
                """);

            // The helper parses cleanly; only the statement below it does not.
            File.WriteAllText(Path.Combine(directory, "Broken.bas"), """
                Sub Helper()
                    Debug.Print 1
                End Sub

                Sub Damaged()
                    ReDim Item(0).Field(0)
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Caller.bas"), """
                Sub Main()
                    Helper
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(
                analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0005"),
                "Helper is declared, so calling it must not be reported as undeclared.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "MultiModule.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="MultiModule"
            Module=MainModule; MainModule.bas
            Module=HelperModule; HelperModule.bas
            """);
        File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
            Option Explicit

            Sub Main()
                Dim x As Integer
                Dim i As Integer
                Dim flag As Boolean
                x = 0

                For i = 1 To 5
                    x = x + 1
                    If i = 3 Then
                        Exit For
                    End If
                Next i

                While x < 5
                    x = x + 1
                Wend

                Do
                    x = x + 1
                    If x = 6 Then
                        Exit Do
                    End If
                Loop

                Do
                    x = x + 1
                Loop Until x = 7

                Select Case x
                    Case 1 To 6
                        x = 100
                    Case 7, 8
                        x = x
                    Case Is > 8
                        x = 200
                    Case Else
                        x = 300
                End Select

                If x < 0 Then
                    x = 100
                ElseIf x = 7 Then
                    x = 8
                Else
                    x = 200
                End If

                If x = 8 Then x = 9 Else x = 300

                flag = True
                If flag And Not False And (True Xor False) And (True Eqv True) And (False Imp True) Then
                    x = x
                Else
                    x = 300
                End If

                Call Update(x)
                Call Observe(x)
                x = Add(x, 2)
                Debug.Print x
            End Sub
            """);
        File.WriteAllText(Path.Combine(directory, "HelperModule.bas"), """
            Option Explicit

            Sub Update(value As Integer)
                value = 10
            End Sub

            Sub Observe(ByVal value As Integer)
                value = 20
            End Sub

            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function
            """);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis)
    {
        var projectDiagnostics = analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString());
        var sourceDiagnostics = analysis.Diagnostics.Select(diagnostic => diagnostic.ToString());
        return string.Join(Environment.NewLine, projectDiagnostics.Concat(sourceDiagnostics));
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
