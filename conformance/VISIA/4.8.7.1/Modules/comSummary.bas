Attribute VB_Name = "comSummary"
Option Explicit

Public Declare Function TerminateProcess Lib "kernel32" (ByVal hProcess As Long, ByVal uExitCode As Long) As Long

Public Errors As Long
Public pError As Boolean
Public Summary As String
Public ShowSummary As Boolean
Public sFileToRun As String
Public LenIncludes As Long
Public LenProcModules As Long
        
Sub InitSummary()
    Summary = ""
    Errors = 0
    pError = False
    LenIncludes = 0
End Sub

Sub ErrMessage(Text As String)
    pError = True: Errors = Errors + 1
    Dim i As Integer
    LenProcModules = 0
    For i = 1 To UBound(VirtualFiles)
        If VirtualFiles(i).Name = CurrentModule Then
            LenProcModules = LenProcModules + Len("module " & Chr(34) & VirtualFiles(i).Name & Chr(34) & ";" & vbNewLine)
            Exit For
        Else
            LenProcModules = LenProcModules + Len(VirtualFiles(i).Content) + Len("module " & Chr(34) & VirtualFiles(i).Name & Chr(34) & ";" & vbNewLine)
            End If
    Next i
    
    Summary = Summary & "-> " & Text & " [" & CurrentModule & ".Line:" & GetLineNumber(Position - 1 - LenIncludes - LenProcModules) & "]" & vbCrLf
    If Not IsCmdCompile Then frmMain.FindOrCreateTab CurrentModule
    If Not IsCmdCompile Then frmMain.Code.ErrorSelectLineByposition Position - 1 - LenIncludes - LenProcModules
End Sub

Sub InfMessage(Text As String)
    If Not IsCmdCompile Then frmMain.Panels.Caption = Replace(Text, vbNewLine, "")
    Summary = Summary & Text & vbCrLf
End Sub

Sub WriteSummary(Text As String)
    If ShowSummary = False And pError = False And IsDLL = False And bLibrary = False Then 'ShellExecute frmMain.hwnd, "open", sFileToRun, "", App.Path & "\Binary", 1: Exit Sub
        Dim ProgramID As Long
        sFileToRun = """" & sFileToRun & """"
        ProgramID = Shell(sFileToRun, vbNormalFocus)
        hWndProg = OpenProcess(PROCESS_ALL_ACCESS, False, ProgramID)
        Exit Sub
    End If
    With frmInfo
        .rtfSummary = Text
        .lblNumErrors.Caption = CStr(Errors & " Errors..")
        If pError = True Or IsDLL = True Then .cmdAction.Caption = "Back..": .cmdAlwaysBack.Visible = False Else .cmdAction.Caption = "Run..": .cmdAlwaysBack.Visible = True
        
        If bLibrary = True Then .cmdAction.Caption = "Back..":
        If Errors > 0 Then
            .rtfSummary.SelStart = 0
            .rtfSummary.SelLength = Len(.rtfSummary.Text)
            .rtfSummary.SelColor = vbRed
            .rtfSummary.SelLength = 0
        End If
        If Not IsCmdCompile Then .Show 1, frmMain
    End With
End Sub

Function GetLineNumber(CurrentPosition As Long)
    Dim ActualLine As Integer
    Dim i As Long
    
    ActualLine = 1
    For i = 1 To CurrentPosition
        If Mid$(Source, i, 2) = vbCrLf Then
            ActualLine = ActualLine + 1
        End If
    Next i
    GetLineNumber = ActualLine
End Function



