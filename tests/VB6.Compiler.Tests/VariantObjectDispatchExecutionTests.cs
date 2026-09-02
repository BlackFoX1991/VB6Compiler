namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantObjectDispatchExecutionTests
{
    [TestMethod]
    public void EmitManagedProject_DispatchesVariantPropertiesAndMethods()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantObjectDispatchTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantObjectDispatch.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantObjectDispatch"
                Class=Widget; Widget.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Widget.cls"), """
                Option Explicit

                Private current As Long

                Private Sub Class_Initialize()
                    current = 7
                End Sub

                Public Property Get Value() As Long
                    Value = current
                End Property

                Public Property Let Value(ByVal newValue As Long)
                    current = newValue
                End Property

                Public Function Add(ByVal amount As Long) As Long
                    current = current + amount
                    Add = current
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim value As Variant
                    Set value = New Widget

                    Debug.Print value.Value
                    value.Value = 10
                    Debug.Print value.Add(5)
                    Debug.Print value.Value
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "7", "15", "15" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_UsesItemDefaultPropertyThroughVariant()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantDefaultItemTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantDefaultItem.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantDefaultItem"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal index As Long) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal index As Long, ByVal value As String)
                    stored = value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value(2) = "through Variant"
                    Debug.Print value(2)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "through Variant" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_WritesBackByRefThroughVariantDefaultProperty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantDefaultByRefTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantDefaultByRef.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantDefaultByRef"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As Variant

                Public Property Get Item(ByVal index As Long) As Variant
                    Item = stored
                End Property

                Public Property Let Item(ByVal index As Long, ByVal value As Variant)
                    stored = value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Private calls As Long

                Private Function NextIndex() As Long
                    calls = calls + 1
                    NextIndex = calls
                End Function

                Private Function Replace(ByRef target As Variant) As String
                    target = "changed"
                    Replace = "returned"
                End Function

                Public Sub Main()
                    Dim bucket As Variant
                    Set bucket = New Bucket
                    bucket(1) = "before"
                    Debug.Print Replace(bucket(NextIndex()))
                    Debug.Print calls
                    Debug.Print bucket(1)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "returned", "1", "changed" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_PreservesStringKeysForVariantDefaultProperties()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantStringKeyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantStringKey.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantStringKey"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal key As String) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value("legacy") = "default"
                    Debug.Print value("legacy")
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "legacy:default" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_DispatchesRealComAutomationObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM compiler E2E test requires Windows.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerComObjectDispatchTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ComObjectDispatch.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ComObjectDispatch"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim dictionary As Object
                    Set dictionary = CreateObject("Scripting.Dictionary")
                    dictionary.Add "answer", 41
                    Debug.Print dictionary.Count
                    dictionary("answer") = 42
                    Debug.Print dictionary("answer")

                    Dim keys() As Variant
                    keys = dictionary.Keys
                    Debug.Print UBound(keys)
                    Debug.Print keys(0)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "1", "42", "0", "answer" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_UsesDefaultPropertyThroughObject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerObjectDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ObjectDefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ObjectDefaultProperty"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal key As String) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Object
                    Set value = New Bucket
                    value("object") = "default"
                    Debug.Print value("object")
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "object:default" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_UsesNamedDefaultPropertyThroughVariant()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantNamedDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantNamedDefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantNamedDefaultProperty"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Bucket"
                Attribute Text.VB_UserMemId = 0

                Private stored As String

                Public Property Get Text(ByVal key As String) As String
                    Text = stored
                End Property

                Public Property Let Text(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value("named") = "default"
                    Debug.Print value("named")
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "named:default" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_ResolvesNoArgumentDefaultPropertyForVariantValues()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantObjectDefaultValueTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantObjectDefaultValue.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantObjectDefaultValue"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Box"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As Long
                    Value = 7
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Box

                    Debug.Print VarType(value)
                    Debug.Print value + 1
                    Debug.Print value * 2
                    Debug.Print value & "x"
                    Debug.Print value = 7
                    Debug.Print CInt(value)
                    Debug.Print CStr(value)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "3", "8", "14", "7x", "True", "7", "7" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_ResolvesDefaultValuesAcrossVariantIntrinsics()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantIntrinsicDefaultTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantIntrinsicDefault.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantIntrinsicDefault"
                Class=Box; Box.cls
                Class=DateBox; DateBox.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Box"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As Long
                    Value = 7
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "DateBox.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "DateBox"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As String
                    Value = "April 28, 2014"
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Box

                    Debug.Print Len(value)
                    Debug.Print LenB(value)
                    Debug.Print Format(value, "0")
                    Debug.Print "[" & Str(value) & "]"
                    Debug.Print IsNumeric(value)
                    Debug.Print value Like "7"
                    Debug.Print Val(value)

                    Dim dateValue As Variant
                    Set dateValue = New DateBox
                    Debug.Print IsDate(dateValue)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "4", "4", "7", "[ 7]", "True", "True", "7", "True" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_ResolvesDefaultValuesForVariantMath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantMathDefaultTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantMathDefault.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantMathDefault"
                Class=Box; Box.cls
                Class=ErrorBox; ErrorBox.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Box"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As Double
                    Value = -1.75
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "ErrorBox.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "ErrorBox"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As Long
                    Value = 2001
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Box

                    Debug.Print Abs(value)
                    Debug.Print Sgn(value)
                    Debug.Print Fix(value)
                    Debug.Print Round(value, 1)
                    Debug.Print Int(value)

                    Dim errorValue As Variant
                    Set errorValue = New ErrorBox
                    Debug.Print TypeName(CVErr(errorValue))
                    Debug.Print CVErr(errorValue)
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "1.75", "-1", "-1", "-1.8", "-2", "Error", "Error 2001" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedProject_ResolvesDefaultValuesInBooleanContexts()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantBooleanDefaultTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantBooleanDefault.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantBooleanDefault"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Box"
                Attribute Value.VB_UserMemId = 0

                Public Property Get Value() As Long
                    Value = 1
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Box

                    If value Then Debug.Print "if-true"
                    Debug.Print IIf(value, "iif-true", "iif-false")
                    Debug.Print Switch(value, "switch-true", False, "switch-false")
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "if-true", "iif-true", "switch-true" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesVariantObjectIdentityAndNothingNullDistinction()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim b As Variant
                Dim n As Variant

                Set a = New Collection
                Set b = a
                Debug.Print (a Is b)
                Set b = New Collection
                Debug.Print (a Is b)
                Debug.Print VarType(a)
                Debug.Print TypeName(a)

                Set a = Nothing
                n = Null
                Debug.Print (a Is Nothing)
                Debug.Print IsObject(a)
                Debug.Print IsNull(a)
                Debug.Print IsObject(n)
                Debug.Print IsNull(n)
                Debug.Print TypeName(a)
                Debug.Print TypeName(n)
            End Sub
            """);

        // TypeName nennt das Standardobjekt "Collection", nicht den CLR-Typnamen der
        // Runtime. Nothing bleibt ein Objektwert (IsObject True, IsNull False) und ist
        // damit von Null unterschieden, das genau umgekehrt antwortet.
        CollectionAssert.AreEqual(
            new[]
            {
                "True", "False", "9", "Collection",
                "True", "True", "False", "False", "True", "Nothing", "Null"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ResolvesLateBoundCollectionMembersAndDefaultMember()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim c As Variant
                Dim o As Object

                Set c = New Collection
                c.Add "erst"
                c.Add "zweit", "k2"
                Debug.Print c.Count
                Debug.Print c(1)
                Debug.Print c("k2")
                Debug.Print c.Item(2)

                Set o = New Collection
                o.Add "nur eins"
                Debug.Print o.Count

                c.Add "dritt"
                Debug.Print c(2.6)
                Debug.Print c(2.5)
                Debug.Print c(CCur(2))

                On Error Resume Next
                Debug.Print c("fehlt")
                Debug.Print Err.Number
            End Sub
            """);

        // Der Index wird gerundet wie ueberall sonst -- 2.6 auf 3, 2.5 auf 2 (Banker's
        // Rounding) -- und Currency konvertiert; ein String bleibt dagegen ein Key und
        // fuehrt bei "fehlt" zu 5.
        //
        // Add hat in VB6 drei optionale Parameter. Der spaet gebundene Pfad muss den
        // Aufruf mit einem und mit zwei Argumenten annehmen -- ohne [Optional] lehnte er
        // ihn ab, waehrend der typisierte Pfad immer alle vier uebergibt.
        CollectionAssert.AreEqual(
            new[] { "2", "erst", "zweit", "zweit", "1", "dritt", "zweit", "zweit", "5" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsVariantArrayBoundsAndElementSubtypes()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Fill(ByRef target As Variant)
                target(1) = CCur(9.5)
            End Sub

            Sub Main()
                Dim v As Variant
                Dim r() As Variant

                v = Array(CInt(1), "zwei", CDbl(3.5))
                Debug.Print LBound(v)
                Debug.Print UBound(v)
                Debug.Print VarType(v)
                Debug.Print IsArray(v)
                Debug.Print TypeName(v)
                Debug.Print VarType(v(0))
                Debug.Print VarType(v(1))
                Debug.Print VarType(v(2))

                v = Array(CInt(1), CInt(2))
                Fill v
                Debug.Print v(1)
                Debug.Print VarType(v(1))

                ReDim r(1 To 3)
                r(1) = "a"
                r(3) = CInt(7)
                ReDim Preserve r(1 To 5)
                Debug.Print LBound(r)
                Debug.Print UBound(r)
                Debug.Print r(1)
                Debug.Print VarType(r(3))
                Debug.Print VarType(r(5))
            End Sub
            """);

        // 8204 ist vbArray + vbVariant. Der Subtyp jedes Elements ueberlebt sowohl den
        // ByRef-Rueckweg (Currency, 6) als auch ReDim Preserve (Integer, 2); neue Plaetze
        // sind Empty (0).
        CollectionAssert.AreEqual(
            new[]
            {
                "0", "2", "8204", "True", "Variant()", "2", "8", "5",
                "9.5", "6",
                "1", "5", "a", "2", "0"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_FailsExplicitlyOnUnsupportedVariantShapes()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant
                Dim c As Variant

                v = Array(1, 2)
                Set c = New Collection

                On Error Resume Next
                Debug.Print v(5)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print c.GibtEsNicht
                Debug.Print Err.Number
                Err.Clear
                c.AuchNichtAlsMethode 1
                Debug.Print Err.Number
            End Sub
            """);

        // Ein Zugriff ausserhalb der Grenzen meldet 9 (Subscript out of range), ein nicht
        // vorhandenes Mitglied 438 -- nicht das pauschale 5, unter dem beide vorher
        // ununterscheidbar waren.
        CollectionAssert.AreEqual(new[] { "9", "438", "438" }, output);
    }
}
