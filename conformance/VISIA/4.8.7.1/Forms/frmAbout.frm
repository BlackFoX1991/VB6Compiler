VERSION 5.00
Begin VB.Form frmAbout 
   BorderStyle     =   1  'Fixed Single
   Caption         =   "About Visia Compiler"
   ClientHeight    =   3555
   ClientLeft      =   45
   ClientTop       =   330
   ClientWidth     =   6345
   Icon            =   "frmAbout.frx":0000
   LinkTopic       =   "frmMain"
   MaxButton       =   0   'False
   MinButton       =   0   'False
   ScaleHeight     =   3555
   ScaleWidth      =   6345
   StartUpPosition =   1  'CenterOwner
   Begin VB.Frame Frame1 
      Caption         =   "Kinex Development Visia Compiler"
      BeginProperty Font 
         Name            =   "Courier New"
         Size            =   9
         Charset         =   0
         Weight          =   700
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Height          =   3525
      Left            =   1575
      TabIndex        =   1
      Top             =   0
      Width           =   4740
      Begin VB.CommandButton cmdOK 
         Cancel          =   -1  'True
         Caption         =   "OK"
         Default         =   -1  'True
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Height          =   345
         Left            =   3150
         TabIndex        =   6
         Tag             =   "OK"
         Top             =   2880
         Width           =   1467
      End
      Begin VB.Frame Frame2 
         Height          =   135
         Left            =   135
         TabIndex        =   5
         Top             =   2430
         Width           =   4455
      End
      Begin VB.TextBox txtCredits 
         BackColor       =   &H80000010&
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Height          =   1455
         Left            =   135
         MultiLine       =   -1  'True
         ScrollBars      =   3  'Both
         TabIndex        =   2
         Top             =   945
         Width           =   4470
      End
      Begin VB.Label lblDisclaimer 
         Caption         =   "Warning: This program is protected by copyright laws."
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00000000&
         Height          =   705
         Left            =   135
         TabIndex        =   8
         Tag             =   "Warning"
         Top             =   2655
         Width           =   2625
      End
      Begin VB.Label Label2 
         Caption         =   "Website"
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   -1  'True
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00000000&
         Height          =   255
         Left            =   3780
         TabIndex        =   7
         Top             =   585
         Width           =   795
      End
      Begin VB.Label lblVersion 
         Alignment       =   1  'Right Justify
         Caption         =   "x.xx"
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Height          =   240
         Left            =   4005
         TabIndex        =   4
         Top             =   270
         Width           =   510
      End
      Begin VB.Label Label1 
         Caption         =   "Version Info:"
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Height          =   240
         Left            =   135
         TabIndex        =   3
         Top             =   270
         Width           =   1410
      End
      Begin VB.Label lblDescription 
         Caption         =   "32-Bit Application Development"
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   9
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00000000&
         Height          =   210
         Left            =   135
         TabIndex        =   9
         Tag             =   "Anwendungsbeschreibung"
         Top             =   585
         Width           =   3735
      End
   End
   Begin VB.PictureBox Picture1 
      Height          =   3555
      Left            =   0
      Picture         =   "frmAbout.frx":08CA
      ScaleHeight     =   3495
      ScaleWidth      =   1470
      TabIndex        =   0
      Top             =   0
      Width           =   1530
   End
End
Attribute VB_Name = "frmAbout"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub cmdOK_Click()
    Unload Me
End Sub

Private Sub Form_Load()
    lblVersion.Caption = "4.8.7.1"
    txtCredits = "Thanks:" & vbNewLine & _
    "Mark Chipman" & vbNewLine & _
    "Mordred" & vbNewLine & _
    "Tommy Lillehagen" & vbNewLine & _
    "Jordi Enguídanos"
    
End Sub

Private Sub Label2_Click()
    Dim ie As Object
    Set ie = CreateObject("INTERNETEXPLORER.APPLICATION")
    ie.Visible = True
    ie.Navigate "http://www.planet-source-code.com/vb/scripts/showcode.asp?txtCodeId=75096&lngWid=1"
End Sub

