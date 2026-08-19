VERSION 5.00
Begin VB.Form frmNew 
   BorderStyle     =   1  'Fest Einfach
   Caption         =   "New Project"
   ClientHeight    =   3390
   ClientLeft      =   45
   ClientTop       =   330
   ClientWidth     =   6615
   ControlBox      =   0   'False
   Icon            =   "frmNew.frx":0000
   MaxButton       =   0   'False
   MinButton       =   0   'False
   ScaleHeight     =   3390
   ScaleWidth      =   6615
   StartUpPosition =   1  'Fenstermitte
   Begin VB.Frame Frame1 
      Height          =   60
      Left            =   90
      TabIndex        =   3
      Top             =   2655
      Width           =   6450
   End
   Begin Visia.McToolBar McToolBar1 
      Height          =   2505
      Left            =   90
      TabIndex        =   2
      Top             =   90
      Width           =   6435
      _ExtentX        =   11351
      _ExtentY        =   4419
      BackColor       =   16777215
      BorderStyle     =   2
      BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   4210752
      Button_Count    =   4
      Button_Index    =   3
      ButtonsWidth    =   90
      ButtonsHeight   =   160
      ButtonsPerRow   =   4
      HoverColor      =   10520954
      TooTipStyle     =   0
      BackGradient    =   5
      BackGradientCol =   12549429
      ButtonsStyle    =   2
      BorderColor     =   16777215
      ButtonCaption0  =   "Windows GUI"
      ButtonIcon0     =   "frmNew.frx":038A
      ButtonCaption1  =   "Dynamic Link Library"
      ButtonIcon1     =   "frmNew.frx":1064
      ButtonCaption2  =   "Windows Console"
      ButtonIcon2     =   "frmNew.frx":1D3E
      ButtonCaption3  =   "Blank Project"
      ButtonIcon3     =   "frmNew.frx":2A18
   End
   Begin VB.CommandButton cmdExist 
      Caption         =   "Open Existing .."
      Height          =   510
      Left            =   90
      TabIndex        =   1
      Top             =   2790
      Width           =   2085
   End
   Begin VB.CommandButton cmdCancel 
      Caption         =   "Cancel"
      Height          =   510
      Left            =   4995
      TabIndex        =   0
      Top             =   2790
      Width           =   1545
   End
End
Attribute VB_Name = "frmNew"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub cmdCancel_Click()
    frmMain.MakeInitControls
    Unload Me
End Sub

Private Sub cmdExist_Click()
    frmMain.MakeInitControls
    frmMain.OpenProject
    Unload Me
End Sub

Sub TemplateGUI()
    frmMain.MakeInitControls
    SetVirtualFileContent "Entry Point", _
                "application PE GUI;" & vbCrLf & vbCrLf & _
                "import MessageBox ascii lib ""USER32.DLL"",4;" & vbCrLf & vbCrLf & _
                "entry" & vbCrLf & vbCrLf & _
                vbTab & "MessageBox(0,""Hello World!"",""Visia GUI"",$20);" & vbCrLf & _
                vbCrLf & _
                "end."
    frmMain.SelectEntryFile
End Sub

Sub TemplateDLL()
    frmMain.MakeInitControls
    SetVirtualFileContent "Entry Point", _
                "application PE GUI DLL;" & vbCrLf & vbCrLf & _
                "export IsInitialized();" & vbCrLf & _
                vbTab & "return(TRUE);" & vbCrLf & _
                "end;" & vbCrLf
    frmMain.SelectEntryFile
End Sub

Sub TemplateCUI()
    frmMain.MakeInitControls
    SetVirtualFileContent "Entry Point", _
                "application PE CUI;" & vbCrLf & vbCrLf & _
                "include ""Windows.inc"", ""Console.inc"";" & vbCrLf & vbCrLf & _
                "entry" & vbCrLf & vbCrLf & _
                vbTab & "Console.Init(""Visia Console"");" & vbCrLf & _
                vbTab & "Console.Write(""Hello World!"");" & vbCrLf & _
                vbTab & "Console.Read();" & vbCrLf & _
                vbCrLf & _
                "end."
    frmMain.SelectEntryFile
End Sub


Private Sub Form_Load()
    frmMain.ucTab.Tabs.Clear
End Sub

Private Sub McToolBar1_Click(ByVal vButton_Index As Long)
    Select Case vButton_Index
        Case 0: TemplateGUI
        Case 1: TemplateDLL
        Case 2: TemplateCUI
        Case 3: frmMain.MakeInitControls: frmMain.SelectEntryFile
        Case Else: Exit Sub
    End Select
    Unload Me
End Sub












