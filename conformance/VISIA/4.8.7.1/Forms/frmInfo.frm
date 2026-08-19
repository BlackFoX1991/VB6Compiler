VERSION 5.00
Object = "{3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0"; "RICHTX32.OCX"
Begin VB.Form frmInfo 
   ClientHeight    =   4020
   ClientLeft      =   60
   ClientTop       =   60
   ClientWidth     =   6105
   ClipControls    =   0   'False
   ControlBox      =   0   'False
   Icon            =   "frmInfo.frx":0000
   LinkTopic       =   "Form1"
   ScaleHeight     =   4020
   ScaleWidth      =   6105
   StartUpPosition =   1  'Fenstermitte
   Begin VB.CommandButton cmdAlwaysBack 
      Caption         =   "Back.."
      BeginProperty Font 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Height          =   555
      Left            =   2430
      TabIndex        =   4
      Top             =   3375
      Visible         =   0   'False
      Width           =   1770
   End
   Begin RichTextLib.RichTextBox rtfSummary 
      Height          =   2580
      Left            =   45
      TabIndex        =   2
      TabStop         =   0   'False
      Top             =   675
      Width           =   6000
      _ExtentX        =   10583
      _ExtentY        =   4551
      _Version        =   393217
      ReadOnly        =   -1  'True
      ScrollBars      =   3
      DisableNoScroll =   -1  'True
      Appearance      =   0
      TextRTF         =   $"frmInfo.frx":2CFA
      BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
   End
   Begin VB.CommandButton cmdAction 
      Caption         =   "Action"
      BeginProperty Font 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Height          =   555
      Left            =   4275
      TabIndex        =   0
      Top             =   3375
      Width           =   1770
   End
   Begin VB.Label lblNumErrors 
      Alignment       =   1  'Rechts
      BackStyle       =   0  'Transparent
      Caption         =   "0 Errors.."
      BeginProperty Font 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   700
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Height          =   240
      Left            =   4320
      TabIndex        =   3
      Top             =   180
      Width           =   1500
   End
   Begin VB.Image Image1 
      Height          =   480
      Left            =   135
      Picture         =   "frmInfo.frx":2D7A
      Top             =   90
      Width           =   480
   End
   Begin VB.Label Label1 
      BackStyle       =   0  'Transparent
      Caption         =   "Summary:"
      BeginProperty Font 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   700
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Height          =   240
      Left            =   720
      TabIndex        =   1
      Top             =   180
      Width           =   915
   End
   Begin VB.Shape shpHead 
      BackColor       =   &H00FFFFFF&
      BackStyle       =   1  'Undurchsichtig
      BorderColor     =   &H00000000&
      Height          =   645
      Left            =   45
      Top             =   0
      Width           =   6000
   End
End
Attribute VB_Name = "frmInfo"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False

Private Sub cmdAction_Click()
    Select Case cmdAction.Caption
        Case "Back..": frmMain.RunEnabled = False: Unload Me
        Case "Run.."
        Dim ProgramID As Long
        sFileToRun = """" & sFileToRun & """"
        ProgramID = Shell(sFileToRun, vbNormalFocus)
        hWndProg = OpenProcess(PROCESS_ALL_ACCESS, False, ProgramID)
        Unload Me
    End Select
End Sub

Private Sub cmdAlwaysBack_Click()
    frmMain.RunEnabled = False
    Unload Me
End Sub

Private Sub Form_Resize()
    On Error Resume Next
    cmdAction.Left = frmInfo.Width - cmdAction.Width - 200
    cmdAction.Top = frmInfo.Height - cmdAction.Height - 200
    cmdAlwaysBack.Left = 50
    cmdAlwaysBack.Top = frmInfo.Height - cmdAlwaysBack.Height - 200
    rtfSummary.Width = frmInfo.Width - 200
    rtfSummary.Height = frmInfo.Height - 1600
    shpHead.Width = frmInfo.Width - 200
End Sub
