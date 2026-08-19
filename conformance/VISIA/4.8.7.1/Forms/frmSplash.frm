VERSION 5.00
Object = "{F9043C88-F6F2-101A-A3C9-08002B2F49FB}#1.2#0"; "Comdlg32.ocx"
Begin VB.Form frmSplash 
   BackColor       =   &H00FFFFFF&
   BorderStyle     =   0  'Kein
   ClientHeight    =   5280
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   8160
   Icon            =   "frmSplash.frx":0000
   LinkTopic       =   "Form1"
   Picture         =   "frmSplash.frx":2CFA
   ScaleHeight     =   5280
   ScaleWidth      =   8160
   ShowInTaskbar   =   0   'False
   StartUpPosition =   2  'Bildschirmmitte
   Begin MSComDlg.CommonDialog cdCmd 
      Left            =   6930
      Top             =   3555
      _ExtentX        =   847
      _ExtentY        =   847
      _Version        =   393216
   End
   Begin VB.Timer Timer1 
      Interval        =   2000
      Left            =   5490
      Top             =   2970
   End
   Begin VB.Label Label4 
      BackStyle       =   0  'Transparent
      Caption         =   "Version:"
      BeginProperty Font 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   700
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   &H00FFFFFF&
      Height          =   255
      Left            =   180
      TabIndex        =   3
      Top             =   90
      Width           =   915
   End
   Begin VB.Label Label5 
      Alignment       =   1  'Rechts
      BackStyle       =   0  'Transparent
      Caption         =   "4 . 8 . 7 "
      BeginProperty Font 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   &H00FFFFFF&
      Height          =   255
      Left            =   1035
      TabIndex        =   2
      Top             =   90
      Width           =   825
   End
   Begin VB.Label Label3 
      BackStyle       =   0  'Transparent
      Caption         =   "http://www.kidev.com"
      BeginProperty Font 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   -1  'True
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   &H00FFFFFF&
      Height          =   255
      Left            =   6120
      TabIndex        =   1
      Top             =   4950
      Width           =   1935
   End
   Begin VB.Label Label1 
      BackStyle       =   0  'Transparent
      Caption         =   "This computer program is protected by copyright law."
      BeginProperty Font 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   &H00959595&
      Height          =   285
      Left            =   135
      TabIndex        =   0
      Top             =   4950
      Width           =   4830
   End
   Begin VB.Shape Shape1 
      BorderColor     =   &H00404040&
      Height          =   5280
      Left            =   0
      Top             =   0
      Width           =   8160
   End
   Begin VB.Shape Shape2 
      BackColor       =   &H00404040&
      BackStyle       =   1  'Undurchsichtig
      BorderColor     =   &H00404040&
      Height          =   420
      Left            =   0
      Top             =   4860
      Width           =   8160
   End
End
Attribute VB_Name = "frmSplash"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private Sub Form_Load()
    Dim CmdParams As Variant
    Dim SourceCon As String
    Dim SourFile As String
    Dim DestFile As String

    If Command() <> "" Then
        CmdParams = Split(Command(), " ")
        If UBound(CmdParams) = 1 Then
            Timer1.Enabled = False
            If CmdParams(0) = "/c" Then
                SourFile = CmdParams(1)
                InitVirtualFiles
                If Not Dir(CmdParams(1)) <> "" Then MsgBox "File '" & CmdParams(1) & "' does not exist.": End
                Open SourFile For Binary As #1
                    SourceCon = Space(LOF(1))
                    Get #1, , SourceCon
                Close #1
                CreateVirtualFile "Entry Point", EX_ENTRY, SourceCon
            
                On Error GoTo CmdExeError
                With cdCmd
                .Filter = "Executable Files|*.exe"
                .CancelError = True
                .ShowSave
                End With
                
                If Dir(cdCmd.FileName) <> "" Then
                    If MsgBox("File already exists! Overwrite?", vbYesNo + vbCritical) = vbNo Then
                        Exit Sub
                    End If
                End If
                IsCmdCompile = True
                Compile cdCmd.FileName, False
CmdExeError:
                End
            Else
                MsgBox "Usage: /c source.* destination.*"
            End If
        End If
        Unload Me
        frmMain.Show
    End If
End Sub

Private Sub Label3_Click()
    Dim ie As Variant
    Set ie = CreateObject("INTERNETEXPLORER.APPLICATION")
    ie.Navigate "http://www.kidev.com"
    ie.Visible = True
End Sub

Private Sub Timer1_Timer()
    Unload Me
    frmMain.Show
End Sub
