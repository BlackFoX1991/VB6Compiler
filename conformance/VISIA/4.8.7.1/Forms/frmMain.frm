VERSION 5.00
Object = "{F9043C88-F6F2-101A-A3C9-08002B2F49FB}#1.2#0"; "COMDLG32.OCX"
Object = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.1#0"; "MSCOMCTL.OCX"
Begin VB.Form frmMain 
   AutoRedraw      =   -1  'True
   BackColor       =   &H00C8D0D4&
   Caption         =   "Visia Compiler"
   ClientHeight    =   6585
   ClientLeft      =   165
   ClientTop       =   345
   ClientWidth     =   10515
   Icon            =   "frmMain.frx":0000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6585
   ScaleWidth      =   10515
   StartUpPosition =   3  'Windows Default
   Begin VB.PictureBox conInTab 
      BackColor       =   &H00C8D0D4&
      BorderStyle     =   0  'None
      Height          =   3795
      Left            =   2070
      ScaleHeight     =   3795
      ScaleWidth      =   4965
      TabIndex        =   24
      TabStop         =   0   'False
      Top             =   1170
      Width           =   4965
      Begin Visia.CodeEdit Code 
         Height          =   1635
         Left            =   0
         TabIndex        =   32
         Top             =   450
         Visible         =   0   'False
         Width           =   3480
         _ExtentX        =   6138
         _ExtentY        =   2884
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "Courier New"
            Size            =   9.75
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         BorderStyle     =   1
         LineNumbers     =   -1  'True
         ColourOperator  =   242037
         ColourKeyWord   =   12215086
         ColourComment   =   242037
         ColourStrings   =   8421504
      End
      Begin MSComctlLib.ImageCombo cmbParent 
         Height          =   300
         Left            =   1755
         TabIndex        =   25
         TabStop         =   0   'False
         Top             =   0
         Visible         =   0   'False
         Width           =   1680
         _ExtentX        =   2963
         _ExtentY        =   529
         _Version        =   393216
         ForeColor       =   8421504
         BackColor       =   -2147483643
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "Lucida Console"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Locked          =   -1  'True
         ImageList       =   "ilObjects"
      End
      Begin MSComctlLib.ImageCombo cmbObject 
         Height          =   300
         Left            =   0
         TabIndex        =   26
         TabStop         =   0   'False
         Top             =   0
         Visible         =   0   'False
         Width           =   1860
         _ExtentX        =   3281
         _ExtentY        =   529
         _Version        =   393216
         ForeColor       =   8421504
         BackColor       =   -2147483643
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "Lucida Console"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Locked          =   -1  'True
         ImageList       =   "ilObjects"
      End
      Begin Visia.DesignForm Designer 
         Height          =   3570
         Left            =   45
         TabIndex        =   27
         TabStop         =   0   'False
         Top             =   0
         Visible         =   0   'False
         Width           =   4605
         _ExtentX        =   8123
         _ExtentY        =   6297
      End
      Begin VB.Line linBetweenCode 
         BorderColor     =   &H00808080&
         Visible         =   0   'False
         X1              =   -1530
         X2              =   1215
         Y1              =   360
         Y2              =   360
      End
   End
   Begin VB.PictureBox picMnu 
      Align           =   1  'Align Top
      AutoRedraw      =   -1  'True
      BackColor       =   &H00C0C0C0&
      BorderStyle     =   0  'None
      Height          =   375
      Left            =   0
      ScaleHeight     =   375
      ScaleWidth      =   10515
      TabIndex        =   14
      TabStop         =   0   'False
      Top             =   0
      Width           =   10515
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "&Help"
         Height          =   195
         Index           =   5
         Left            =   3330
         TabIndex        =   20
         Top             =   90
         Width           =   525
      End
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "E&xtras"
         Height          =   195
         Index           =   4
         Left            =   2655
         TabIndex        =   19
         Top             =   90
         Width           =   600
      End
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "&Compile"
         Height          =   195
         Index           =   3
         Left            =   1890
         TabIndex        =   18
         Top             =   90
         Width           =   690
      End
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "&Search"
         Height          =   195
         Index           =   2
         Left            =   1170
         TabIndex        =   17
         Top             =   90
         Width           =   645
      End
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "&Edit"
         Height          =   195
         Index           =   1
         Left            =   630
         TabIndex        =   16
         Top             =   90
         Width           =   465
      End
      Begin VB.Label lblMnu 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "&File"
         Height          =   195
         Index           =   0
         Left            =   135
         TabIndex        =   15
         Top             =   90
         Width           =   465
      End
      Begin VB.Shape shpMenu 
         BackColor       =   &H0000D5F2&
         BackStyle       =   1  'Opaque
         BorderColor     =   &H00006A79&
         Height          =   285
         Left            =   135
         Top             =   45
         Visible         =   0   'False
         Width           =   465
      End
      Begin VB.Image Image1 
         Height          =   225
         Left            =   20
         Picture         =   "frmMain.frx":2CFA
         Top             =   60
         Width           =   135
      End
   End
   Begin VB.PictureBox picToolB 
      Align           =   1  'Align Top
      AutoRedraw      =   -1  'True
      BackColor       =   &H00C0C0C0&
      BorderStyle     =   0  'None
      Height          =   375
      Left            =   0
      ScaleHeight     =   375
      ScaleWidth      =   10515
      TabIndex        =   11
      TabStop         =   0   'False
      Top             =   375
      Width           =   10515
      Begin Visia.McToolBar mcTB 
         Height          =   375
         Left            =   135
         Negotiate       =   -1  'True
         TabIndex        =   12
         TabStop         =   0   'False
         Top             =   0
         Width           =   8190
         _ExtentX        =   14446
         _ExtentY        =   661
         Appearance      =   1
         BackColor       =   13160660
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "MS Sans Serif"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   16777215
         Button_Count    =   18
         ButtonsWidth    =   25
         ButtonsHeight   =   25
         ButtonsPerRow   =   100
         HoverColor      =   16737843
         TooTipStyle     =   1
         BackGradient    =   3
         BackGradientCol =   15922421
         ButtonsStyle    =   2
         BorderColor     =   54770
         HoverIconShadow =   0   'False
         ButtonIcon0     =   "frmMain.frx":2EE0
         ButtonToolTipText0=   "New Project"
         ButtonIconAllignment0=   4
         ButtonIcon1     =   "frmMain.frx":327A
         ButtonToolTipText1=   "Add Items"
         ButtonIconAllignment1=   4
         ButtonIcon2     =   "frmMain.frx":3614
         ButtonToolTipText2=   "Open Project"
         ButtonIconAllignment2=   4
         ButtonIcon3     =   "frmMain.frx":39AE
         ButtonToolTipText3=   "Save Project"
         ButtonIconAllignment3=   4
         ButtonIcon4     =   "frmMain.frx":3D48
         ButtonToolTipText4=   "Save Project As.."
         ButtonIconAllignment4=   4
         Button_Type5    =   1
         ButtonIcon6     =   "frmMain.frx":40E2
         ButtonToolTipText6=   "Cut"
         ButtonIconAllignment6=   4
         ButtonIcon7     =   "frmMain.frx":4AF4
         ButtonToolTipText7=   "Copy"
         ButtonIconAllignment7=   4
         ButtonIcon8     =   "frmMain.frx":5506
         ButtonToolTipText8=   "Paste"
         ButtonIconAllignment8=   4
         Button_Type9    =   1
         ButtonIcon10    =   "frmMain.frx":5F18
         ButtonToolTipText10=   "Undo"
         ButtonIconAllignment10=   4
         ButtonIcon11    =   "frmMain.frx":62B2
         ButtonToolTipText11=   "Redo"
         ButtonIconAllignment11=   4
         Button_Type12   =   1
         ButtonIcon13    =   "frmMain.frx":664C
         ButtonToolTipText13=   "Run Project"
         ButtonIconAllignment13=   4
         ButtonIcon14    =   "frmMain.frx":69E6
         ButtonToolTipText14=   "Stop Project"
         ButtonIconAllignment14=   4
         Button_Type15   =   1
         ButtonIcon16    =   "frmMain.frx":6D80
         ButtonToolTipText16=   "Show/Hide Toolbars"
         ButtonIconAllignment16=   4
         ButtonIcon17    =   "frmMain.frx":711A
         ButtonIconAllignment17=   4
      End
      Begin VB.Image tbRight 
         Height          =   375
         Left            =   8325
         Picture         =   "frmMain.frx":74B4
         Top             =   0
         Width           =   255
      End
      Begin VB.Image tbLeft 
         Height          =   375
         Left            =   0
         Picture         =   "frmMain.frx":7A0A
         Top             =   0
         Width           =   150
      End
   End
   Begin MSComctlLib.ImageList ilDesigner 
      Left            =   4590
      Top             =   5400
      _ExtentX        =   1005
      _ExtentY        =   1005
      BackColor       =   -2147483643
      ImageWidth      =   16
      ImageHeight     =   16
      MaskColor       =   12632256
      _Version        =   393216
      BeginProperty Images {2C247F25-8591-11D1-B16A-00C0F0283628} 
         NumListImages   =   18
         BeginProperty ListImage1 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":7D6C
            Key             =   ""
         EndProperty
         BeginProperty ListImage2 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":8106
            Key             =   ""
         EndProperty
         BeginProperty ListImage3 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":84A0
            Key             =   ""
         EndProperty
         BeginProperty ListImage4 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":883A
            Key             =   ""
         EndProperty
         BeginProperty ListImage5 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":8BD4
            Key             =   ""
         EndProperty
         BeginProperty ListImage6 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":8F6E
            Key             =   ""
         EndProperty
         BeginProperty ListImage7 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":9308
            Key             =   ""
         EndProperty
         BeginProperty ListImage8 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":96A2
            Key             =   ""
         EndProperty
         BeginProperty ListImage9 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":9A3C
            Key             =   ""
         EndProperty
         BeginProperty ListImage10 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":9DD6
            Key             =   ""
         EndProperty
         BeginProperty ListImage11 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":A170
            Key             =   ""
         EndProperty
         BeginProperty ListImage12 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":A50A
            Key             =   ""
         EndProperty
         BeginProperty ListImage13 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":A8A4
            Key             =   ""
         EndProperty
         BeginProperty ListImage14 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":AC3E
            Key             =   ""
         EndProperty
         BeginProperty ListImage15 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":AFD8
            Key             =   ""
         EndProperty
         BeginProperty ListImage16 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":B372
            Key             =   ""
         EndProperty
         BeginProperty ListImage17 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":B70C
            Key             =   ""
         EndProperty
         BeginProperty ListImage18 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":BAA6
            Key             =   ""
         EndProperty
      EndProperty
   End
   Begin MSComctlLib.ImageList ilObjects 
      Left            =   5220
      Top             =   5400
      _ExtentX        =   1005
      _ExtentY        =   1005
      BackColor       =   -2147483643
      ImageWidth      =   16
      ImageHeight     =   16
      MaskColor       =   12632256
      _Version        =   393216
      BeginProperty Images {2C247F25-8591-11D1-B16A-00C0F0283628} 
         NumListImages   =   15
         BeginProperty ListImage1 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":BE40
            Key             =   "Object"
         EndProperty
         BeginProperty ListImage2 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":C1DA
            Key             =   ""
         EndProperty
         BeginProperty ListImage3 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":C574
            Key             =   "Parent"
         EndProperty
         BeginProperty ListImage4 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":C90E
            Key             =   ""
         EndProperty
         BeginProperty ListImage5 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":CCA8
            Key             =   ""
         EndProperty
         BeginProperty ListImage6 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":D042
            Key             =   ""
         EndProperty
         BeginProperty ListImage7 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":D3DC
            Key             =   ""
         EndProperty
         BeginProperty ListImage8 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":D776
            Key             =   ""
         EndProperty
         BeginProperty ListImage9 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":DB10
            Key             =   "Parent2"
         EndProperty
         BeginProperty ListImage10 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":DEAA
            Key             =   ""
         EndProperty
         BeginProperty ListImage11 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":E244
            Key             =   "Entry"
         EndProperty
         BeginProperty ListImage12 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":E5DE
            Key             =   ""
         EndProperty
         BeginProperty ListImage13 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":E978
            Key             =   ""
         EndProperty
         BeginProperty ListImage14 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":ED12
            Key             =   "Exports"
         EndProperty
         BeginProperty ListImage15 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":F0AC
            Key             =   ""
         EndProperty
      EndProperty
   End
   Begin MSComDlg.CommonDialog cDExport 
      Left            =   6480
      Top             =   5520
      _ExtentX        =   847
      _ExtentY        =   847
      _Version        =   393216
   End
   Begin VB.PictureBox conProject 
      Align           =   4  'Align Right
      AutoRedraw      =   -1  'True
      BackColor       =   &H00C8D0D4&
      BorderStyle     =   0  'None
      ForeColor       =   &H00000000&
      Height          =   5550
      Left            =   8340
      ScaleHeight     =   5550
      ScaleWidth      =   2175
      TabIndex        =   1
      TabStop         =   0   'False
      Top             =   750
      Width           =   2175
      Begin VB.PictureBox imgProjectBar 
         AutoRedraw      =   -1  'True
         BorderStyle     =   0  'None
         Height          =   375
         Left            =   90
         ScaleHeight     =   375
         ScaleWidth      =   2040
         TabIndex        =   30
         TabStop         =   0   'False
         Top             =   225
         Width           =   2040
         Begin VB.Image imgAppView 
            Height          =   240
            Index           =   3
            Left            =   1755
            Picture         =   "frmMain.frx":11DB6
            ToolTipText     =   "Delete Selected Item"
            Top             =   45
            Width           =   240
         End
         Begin VB.Image imgAppView 
            Height          =   240
            Index           =   2
            Left            =   405
            Picture         =   "frmMain.frx":12140
            ToolTipText     =   "Collapse"
            Top             =   45
            Width           =   240
         End
         Begin VB.Image imgAppView 
            Height          =   240
            Index           =   1
            Left            =   765
            Picture         =   "frmMain.frx":124CA
            ToolTipText     =   "Add New Item"
            Top             =   45
            Width           =   240
         End
         Begin VB.Image imgAppView 
            Height          =   240
            Index           =   0
            Left            =   45
            Picture         =   "frmMain.frx":12854
            ToolTipText     =   "Expand & Sort"
            Top             =   45
            Width           =   240
         End
         Begin VB.Line Line1 
            BorderColor     =   &H00808080&
            X1              =   -45
            X2              =   2025
            Y1              =   360
            Y2              =   360
         End
         Begin VB.Shape shpSelectAppView 
            BorderColor     =   &H00695A50&
            FillColor       =   &H00C88059&
            FillStyle       =   0  'Solid
            Height          =   315
            Left            =   0
            Top             =   15
            Visible         =   0   'False
            Width           =   315
         End
      End
      Begin VB.PictureBox conProperties 
         Appearance      =   0  'Flat
         BackColor       =   &H00FFFFFF&
         BorderStyle     =   0  'None
         ForeColor       =   &H80000008&
         Height          =   2040
         Left            =   100
         ScaleHeight     =   2040
         ScaleWidth      =   2010
         TabIndex        =   6
         TabStop         =   0   'False
         Top             =   3195
         Width           =   2010
         Begin VB.TextBox txtChange 
            BorderStyle     =   0  'None
            Enabled         =   0   'False
            ForeColor       =   &H00404040&
            Height          =   285
            Left            =   720
            TabIndex        =   7
            TabStop         =   0   'False
            Top             =   40
            Width           =   1185
         End
         Begin VB.Shape Shape3 
            BackColor       =   &H0003B175&
            BorderColor     =   &H0003B175&
            Height          =   330
            Left            =   0
            Top             =   30
            Width           =   2055
         End
         Begin VB.Label lblName 
            Alignment       =   2  'Center
            Appearance      =   0  'Flat
            BackColor       =   &H0003B175&
            Caption         =   "Name:"
            ForeColor       =   &H00FFFFFF&
            Height          =   300
            Left            =   0
            TabIndex        =   8
            Top             =   45
            Width           =   645
         End
      End
      Begin MSComctlLib.TreeView tvProject 
         Height          =   2265
         Left            =   105
         TabIndex        =   2
         TabStop         =   0   'False
         Top             =   630
         Width           =   2010
         _ExtentX        =   3545
         _ExtentY        =   3995
         _Version        =   393217
         HideSelection   =   0   'False
         Indentation     =   35
         LabelEdit       =   1
         LineStyle       =   1
         Style           =   7
         HotTracking     =   -1  'True
         ImageList       =   "ilToolBar"
         Appearance      =   0
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "MS Sans Serif"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
      End
      Begin VB.Label imgProjectClose 
         BackStyle       =   0  'Transparent
         Caption         =   "X"
         BeginProperty Font 
            Name            =   "Lucida Console"
            Size            =   8.25
            Charset         =   0
            Weight          =   700
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00FFFFFF&
         Height          =   150
         Left            =   1935
         TabIndex        =   28
         Top             =   45
         Width           =   150
      End
      Begin VB.Label Label2 
         BackStyle       =   0  'Transparent
         Caption         =   "Project View"
         BeginProperty Font 
            Name            =   "Arial"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00FFFFFF&
         Height          =   195
         Left            =   180
         TabIndex        =   4
         Top             =   0
         UseMnemonic     =   0   'False
         Width           =   1545
      End
      Begin VB.Shape shp4Properties 
         BorderColor     =   &H00808080&
         Height          =   915
         Left            =   90
         Top             =   3240
         Width           =   1410
      End
      Begin VB.Shape shp4Project 
         BorderColor     =   &H00808080&
         Height          =   600
         Left            =   90
         Top             =   225
         Width           =   735
      End
      Begin VB.Line linEdgeProject 
         BorderColor     =   &H00808080&
         X1              =   0
         X2              =   0
         Y1              =   5850
         Y2              =   0
      End
      Begin VB.Label lblProp 
         BackStyle       =   0  'Transparent
         Caption         =   "Properties"
         BeginProperty Font 
            Name            =   "Arial"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00FFFFFF&
         Height          =   195
         Left            =   180
         TabIndex        =   13
         Top             =   2925
         UseMnemonic     =   0   'False
         Width           =   1275
      End
      Begin VB.Shape shpProp 
         BackColor       =   &H80000003&
         BackStyle       =   1  'Opaque
         BorderColor     =   &H00808080&
         FillColor       =   &H00FFFFFF&
         Height          =   240
         Left            =   90
         Top             =   2925
         Width           =   2040
      End
      Begin VB.Shape shpTitleProject 
         BackColor       =   &H80000003&
         BackStyle       =   1  'Opaque
         BorderColor     =   &H00808080&
         FillColor       =   &H00FFFFFF&
         Height          =   240
         Left            =   90
         Top             =   0
         Width           =   2040
      End
   End
   Begin VB.Timer tmrApplicationRuntime 
      Enabled         =   0   'False
      Interval        =   100
      Left            =   3690
      Top             =   5715
   End
   Begin MSComctlLib.ImageList ilToolBar 
      Left            =   5850
      Top             =   5400
      _ExtentX        =   1005
      _ExtentY        =   1005
      BackColor       =   -2147483643
      ImageWidth      =   16
      ImageHeight     =   16
      MaskColor       =   12632256
      _Version        =   393216
      BeginProperty Images {2C247F25-8591-11D1-B16A-00C0F0283628} 
         NumListImages   =   21
         BeginProperty ListImage1 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":12BDE
            Key             =   "Entr"
         EndProperty
         BeginProperty ListImage2 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":12F78
            Key             =   "Folder"
         EndProperty
         BeginProperty ListImage3 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":13312
            Key             =   "Modulex"
         EndProperty
         BeginProperty ListImage4 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":136AC
            Key             =   ""
         EndProperty
         BeginProperty ListImage5 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":14386
            Key             =   ""
         EndProperty
         BeginProperty ListImage6 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":153D8
            Key             =   ""
         EndProperty
         BeginProperty ListImage7 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1642A
            Key             =   "Module2"
         EndProperty
         BeginProperty ListImage8 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":167C4
            Key             =   "Module4"
         EndProperty
         BeginProperty ListImage9 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":16B5E
            Key             =   ""
         EndProperty
         BeginProperty ListImage10 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":16EF8
            Key             =   "Module1"
         EndProperty
         BeginProperty ListImage11 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":17292
            Key             =   "Settings"
         EndProperty
         BeginProperty ListImage12 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":182E4
            Key             =   ""
         EndProperty
         BeginProperty ListImage13 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1867E
            Key             =   ""
         EndProperty
         BeginProperty ListImage14 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":19358
            Key             =   "Dialog"
         EndProperty
         BeginProperty ListImage15 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":196F2
            Key             =   "s"
         EndProperty
         BeginProperty ListImage16 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":19A8C
            Key             =   "fdccc"
         EndProperty
         BeginProperty ListImage17 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":19E26
            Key             =   "Entry"
         EndProperty
         BeginProperty ListImage18 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1A1C0
            Key             =   "Entry4"
         EndProperty
         BeginProperty ListImage19 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1A55A
            Key             =   ""
         EndProperty
         BeginProperty ListImage20 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1A8F4
            Key             =   ""
         EndProperty
         BeginProperty ListImage21 {2C247F27-8591-11D1-B16A-00C0F0283628} 
            Picture         =   "frmMain.frx":1AC8E
            Key             =   "Module"
         EndProperty
      EndProperty
   End
   Begin VB.PictureBox conToolbox 
      Align           =   3  'Align Left
      AutoRedraw      =   -1  'True
      BackColor       =   &H00C8D0D4&
      BorderStyle     =   0  'None
      Height          =   5550
      Left            =   0
      ScaleHeight     =   5550
      ScaleWidth      =   1800
      TabIndex        =   0
      TabStop         =   0   'False
      Top             =   750
      Width           =   1800
      Begin MSComctlLib.TreeView tvTools 
         Height          =   5055
         Left            =   45
         TabIndex        =   10
         TabStop         =   0   'False
         Top             =   240
         Visible         =   0   'False
         Width           =   1590
         _ExtentX        =   2805
         _ExtentY        =   8916
         _Version        =   393217
         HideSelection   =   0   'False
         Indentation     =   2
         LabelEdit       =   1
         Style           =   7
         HotTracking     =   -1  'True
         ImageList       =   "ilDesigner"
         Appearance      =   0
         BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
            Name            =   "MS Sans Serif"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
      End
      Begin VB.Shape shp4Tools 
         BorderColor     =   &H00808080&
         Height          =   5415
         Left            =   0
         Top             =   0
         Visible         =   0   'False
         Width           =   1725
      End
      Begin VB.Label imgClsToolBox 
         BackStyle       =   0  'Transparent
         Caption         =   "X"
         BeginProperty Font 
            Name            =   "Lucida Console"
            Size            =   8.25
            Charset         =   0
            Weight          =   700
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00FFFFFF&
         Height          =   150
         Left            =   1530
         TabIndex        =   29
         Top             =   45
         Width           =   150
      End
      Begin VB.Line linEdgeTool 
         BorderColor     =   &H00808080&
         X1              =   1785
         X2              =   1785
         Y1              =   5670
         Y2              =   0
      End
      Begin VB.Label lNoItems 
         Alignment       =   2  'Center
         BackStyle       =   0  'Transparent
         Caption         =   "Currently no useable items to view."
         BeginProperty Font 
            Name            =   "Verdana"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00000000&
         Height          =   825
         Left            =   45
         TabIndex        =   5
         Top             =   360
         Width           =   1635
      End
      Begin VB.Label Label1 
         BackStyle       =   0  'Transparent
         Caption         =   "Toolbox"
         BeginProperty Font 
            Name            =   "Arial"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00FFFFFF&
         Height          =   195
         Left            =   90
         TabIndex        =   3
         Top             =   0
         UseMnemonic     =   0   'False
         Width           =   960
      End
      Begin VB.Shape shpTitleToolBox 
         BackColor       =   &H80000003&
         BackStyle       =   1  'Opaque
         BorderColor     =   &H00808080&
         FillColor       =   &H00FFFFFF&
         Height          =   240
         Left            =   -90
         Top             =   0
         Width           =   1815
      End
   End
   Begin MSComDlg.CommonDialog cDExe 
      Left            =   7440
      Top             =   5520
      _ExtentX        =   847
      _ExtentY        =   847
      _Version        =   393216
   End
   Begin MSComDlg.CommonDialog cD 
      Left            =   6960
      Top             =   5520
      _ExtentX        =   847
      _ExtentY        =   847
      _Version        =   393216
   End
   Begin VB.TextBox txtInfoSel 
      Alignment       =   2  'Center
      Appearance      =   0  'Flat
      BackColor       =   &H8000000F&
      BorderStyle     =   0  'None
      Enabled         =   0   'False
      BeginProperty Font 
         Name            =   "Verdana"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      ForeColor       =   &H80000010&
      Height          =   285
      Left            =   4275
      TabIndex        =   9
      TabStop         =   0   'False
      Text            =   "Currently no item selected."
      Top             =   3285
      Width           =   2355
   End
   Begin VB.PictureBox sbMain 
      Align           =   2  'Align Bottom
      AutoRedraw      =   -1  'True
      BorderStyle     =   0  'None
      Height          =   285
      Left            =   0
      ScaleHeight     =   285
      ScaleWidth      =   10515
      TabIndex        =   22
      TabStop         =   0   'False
      Top             =   6300
      Width           =   10515
      Begin VB.Image imgReIDE 
         Height          =   180
         Left            =   10305
         Picture         =   "frmMain.frx":1B028
         Top             =   80
         Width           =   195
      End
      Begin VB.Label Panels 
         BackStyle       =   0  'Transparent
         Caption         =   "Ready ..."
         BeginProperty Font 
            Name            =   "Courier New"
            Size            =   8.25
            Charset         =   0
            Weight          =   400
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         Height          =   195
         Left            =   90
         TabIndex        =   23
         Top             =   45
         Width           =   8925
      End
   End
   Begin Visia.GpTabStrip ucTab 
      Height          =   4380
      Left            =   1845
      TabIndex        =   21
      Top             =   810
      Visible         =   0   'False
      Width           =   5865
      _ExtentX        =   10345
      _ExtentY        =   7726
      ForeColor       =   8421504
      HotTracking     =   0   'False
      MultiRow        =   0   'False
      TabBorderColor  =   13160660
      BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
         Name            =   "Arial"
         Size            =   8.25
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
      Begin VB.Label lblTabClose 
         BackStyle       =   0  'Transparent
         Caption         =   "X"
         BeginProperty Font 
            Name            =   "Lucida Console"
            Size            =   8.25
            Charset         =   0
            Weight          =   700
            Underline       =   0   'False
            Italic          =   0   'False
            Strikethrough   =   0   'False
         EndProperty
         ForeColor       =   &H00695A50&
         Height          =   150
         Left            =   180
         TabIndex        =   31
         Top             =   45
         Width           =   150
      End
   End
   Begin VB.Line linEdgeBelow 
      BorderColor     =   &H00808080&
      X1              =   2070
      X2              =   8100
      Y1              =   6210
      Y2              =   6210
   End
   Begin VB.Line linEdgeTop 
      BorderColor     =   &H00808080&
      X1              =   1980
      X2              =   7965
      Y1              =   765
      Y2              =   765
   End
   Begin VB.Menu mnuFile 
      Caption         =   "File"
      Visible         =   0   'False
      Begin VB.Menu cmdFile 
         Caption         =   "FileContent"
         Index           =   0
      End
   End
   Begin VB.Menu mnuEdit 
      Caption         =   "Edit"
      Visible         =   0   'False
      Begin VB.Menu cmdEdit 
         Caption         =   "EditContent"
         Index           =   0
      End
   End
   Begin VB.Menu mnuSearch 
      Caption         =   "Search"
      Visible         =   0   'False
      Begin VB.Menu cmdFind 
         Caption         =   "Find"
      End
   End
   Begin VB.Menu mnuCompile 
      Caption         =   "Compile"
      Visible         =   0   'False
      Begin VB.Menu cmdCompile 
         Caption         =   "CompileContent"
         Index           =   0
      End
   End
   Begin VB.Menu mnuExtra 
      Caption         =   "Extra"
      Visible         =   0   'False
      Begin VB.Menu cmdExtra 
         Caption         =   "ExtraContent"
         Index           =   0
      End
   End
   Begin VB.Menu mnuHelp 
      Caption         =   "Help"
      Visible         =   0   'False
      Begin VB.Menu cmdHelp 
         Caption         =   "HelpContent"
         Index           =   0
      End
   End
   Begin VB.Menu cmdAddToProject 
      Caption         =   "Add"
      Visible         =   0   'False
      Begin VB.Menu sMnuAddModule 
         Caption         =   "Add New Module"
         Begin VB.Menu cmdAddModule 
            Caption         =   "Create Module .."
         End
         Begin VB.Menu cmdImportModule 
            Caption         =   "Import Module .."
         End
      End
      Begin VB.Menu cmdAddResource 
         Caption         =   "Add New Resource"
         Enabled         =   0   'False
         Begin VB.Menu cmdAddResDialog 
            Caption         =   "Resource Dialog .."
         End
      End
      Begin VB.Menu SepProjectExp1 
         Caption         =   "-"
      End
      Begin VB.Menu cmdExportFileAsText 
         Caption         =   "Export To File"
      End
      Begin VB.Menu SepProjectExp2 
         Caption         =   "-"
      End
      Begin VB.Menu cmdDelTreeView 
         Caption         =   "Delete Selected"
      End
   End
   Begin VB.Menu mnuTab 
      Caption         =   "Tab"
      Visible         =   0   'False
      Begin VB.Menu cmdCloseSelTab 
         Caption         =   "Close Selected Tab .."
         Visible         =   0   'False
      End
      Begin VB.Menu cmdCloseAllTabs 
         Caption         =   "Close All Tabs .."
      End
   End
End
Attribute VB_Name = "frmMain"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Public isDirty As Boolean
Public lModuleID As Long
Public lResourceID As Long
Public cNodeKey As Long
Public lLastTab As String
Public AutoTemplates As Boolean
Public RunEnabled As Boolean
Public NoBeforeClickEvent As Boolean
Public LastMnuIndex As Long
Public LastMnuOpenIndex As Long

Public Sub cmbObject_Click()
    On Error Resume Next
    LastMnuOpenIndex = -2
    AddScanObjectsToCombo cmbObject.SelectedItem.Text
End Sub

Private Sub cmbObject_Dropdown()
    CodeScan
End Sub

Private Sub cmbParent_Click()
    Dim i As Long
    LastMnuOpenIndex = -2
    For i = 1 To UBound(ScanObjects)
        If ScanObjects(i).Parent = cmbParent.Text Then
            Code.SetCarretPos ScanObjects(i).Position
            Code.SetFocus
        End If
    Next i
End Sub

Private Sub cmdAddModule_Click()
    lModuleID = 1
    While VirtualFileExists("Module" & lModuleID): lModuleID = lModuleID + 1: DoEvents: Wend
    If FindProjectFolder("Modules") = False Then tvProject.Nodes.Add , , "Mod", "Modules", "Folder": tvProject.Nodes(tvProject.Nodes.Count).Expanded = True
    tvProject.Nodes.Add "Mod", tvwChild, "Module" & lModuleID, "Module" & lModuleID, "Module"
    CreateVirtualFile "Module" & lModuleID, EX_MODULE, ""
End Sub

Private Sub cmdAddResDialog_Click()
    lResourceID = lResourceID + 1
    If FindProjectFolder("Resources") = False Then tvProject.Nodes.Add , , "Res", "Resources", "Folder": tvProject.Nodes(tvProject.Nodes.Count).Expanded = True
    tvProject.Nodes.Add "Res", tvwChild, "Dialog" & lResourceID, "Dialog" & lResourceID, "Dialog"
    CreateVirtualFile "Dialog" & lResourceID, EX_DIALOG, ""
End Sub

Private Sub cmdCloseAllTabs_Click()
    Dim i As Long
    Dim tCount As Long
    tCount = ucTab.Tabs.Count
    For i = 1 To tCount
       FindOrCreateTab ucTab.Tabs.Item(i).Caption
    Next i
    Code.Visible = False: ToolBarCodeEditDisable: Designer.Visible = False: tvTools.Visible = False: shp4Tools.Visible = False
    ucTab.Tabs.Clear
    lblTabClose.Visible = False
End Sub

Private Sub cmdCloseSelTab_Click()
    ucTab_BeforeClick False
    Code.Visible = False: ToolBarCodeEditDisable: Designer.Visible = False: tvTools.Visible = False: shp4Tools.Visible = False
    ucTab.Tabs.Clear
End Sub

Private Sub cmdCompile_Click(Index As Integer)
    Dim CompilePath As String
    
    Call Me.ucTab_BeforeClick(False): LastMnuOpenIndex = -2
    
    Select Case cmdCompile(Index).Caption
        Case "Link && Run":
            If cD.FileName = "" Then
                If MsgBox("File is not saved. Do you want to save it now?", _
                          vbInformation + vbYesNo, "Visia Compiler") = vbYes Then
                    If (SaveProject(True) = False) Then
                        GoTo CannotCompileNotSaved
                    End If
                Else
                    GoTo CannotCompileNotSaved
                End If
            End If
           
            Dim i As Integer
            
            RunEnabled = True
            tmrApplicationRuntime.Enabled = True:
            frmMain.Caption = Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) & " - Visia Compiler 4.8.7 [Compiling..]"
            mcTB.SetButtonValue 14, BTN_Enabled, True
            mcTB.SetButtonValue 13, BTN_Enabled, False
            Screen.MousePointer = 13
           
            CompilePath = Left(cD.FileName, Len(cD.FileName) - Len(Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare))))
            Compile CompilePath & Switch(Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) <> "", Mid$(Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)), 1, InStr(1, Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)), ".", vbTextCompare)) & "exe", Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) = "", "noname.exe"), True
            Screen.MousePointer = vbNormal
            mcTB.SetButtonValue 13, BTN_Enabled, False
            AppType = 0 'because stop button checks this
            'RunEnabled = False
        Case "Create Executable (*.exe|*.dll)"
            MakeExecuteable
    End Select

CannotCompileNotSaved:
End Sub

Private Sub cmdDelTreeView_Click()
    On Error GoTo NoDeleteIs
    If MsgBox("Are you sure you want to delete '" & tvProject.Nodes(tvProject.SelectedItem.Index).Text & "' ?", vbInformation + vbYesNo, "Visia Compiler 4.8.7") = vbYes Then
        If GetVirtualFileExtension(tvProject.Nodes(tvProject.SelectedItem.Index).Text) = EX_MODULE Then
            DeleteVirtualFile tvProject.Nodes(tvProject.SelectedItem.Index).Text
            tvProject.Nodes.Remove tvProject.SelectedItem.Index
            
            Dim i As Long
            Dim DummyVF() As TYPE_VIRTUAL_FILE
            ReDim DummyVF(0) As TYPE_VIRTUAL_FILE
            
            For i = 1 To UBound(VirtualFiles)
                If VirtualFiles(i).Used = True Then
                    ReDim Preserve DummyVF(UBound(DummyVF) + 1) As TYPE_VIRTUAL_FILE
                    DummyVF(UBound(DummyVF)) = VirtualFiles(i)
                End If
            Next i
            
            ReDim VirtualFiles(0) As TYPE_VIRTUAL_FILE
            
            For i = 1 To UBound(DummyVF)
                ReDim Preserve VirtualFiles(UBound(VirtualFiles) + 1) As TYPE_VIRTUAL_FILE
                VirtualFiles(UBound(VirtualFiles)) = DummyVF(i)
            Next i
            
        Else
            MsgBox "'" & tvProject.Nodes(tvProject.SelectedItem.Index).Text & "' could not be deleted.", vbInformation
        End If
    End If
NoDeleteIs:
End Sub

Private Sub cmdEdit_Click(Index As Integer)
    Call Me.ucTab_BeforeClick(False): LastMnuOpenIndex = -2
    Select Case cmdEdit(Index).Caption
        Case "Undo": Code.Undo
        Case "Redo": Code.Undo
        Case "Cut":  Code.Cut
        Case "Copy": Code.CopyM
        Case "Paste":  Code.Paste
        Case "Delete": Code.Delete
        Case "Select All": Code.SelAll
    End Select
End Sub

Private Sub cmdExportFileAsText_Click()
    If GetVirtualFileExtension(tvProject.Nodes(tvProject.SelectedItem.Index).Text) = EX_MODULE Or _
       GetVirtualFileExtension(tvProject.Nodes(tvProject.SelectedItem.Index).Text) = EX_ENTRY Then
        On Error GoTo ExportFailed
        cDExport.CancelError = True
        cDExport.Filter = "Include Files (*.inc)|*.inc|Plain Text (*.txt)|*.txt|All Files (*.*)|*.*"
        cDExport.ShowSave
        If Trim(cDExport.FileName) <> "" Then
            Open cDExport.FileName For Output As #1
                Print #1, GetVirtualFileContent(tvProject.Nodes(tvProject.SelectedItem.Index).Text)
            Close #1
            Exit Sub
        End If
    End If
ExportFailed:
    MsgBox "Could not export file."
    Close #1
End Sub

Private Sub cmdExtra_Click(Index As Integer)
    Call Me.ucTab_BeforeClick(False): LastMnuOpenIndex = -2
    Select Case cmdExtra(Index).Caption
        'Case "Specify FASM Path..": DeclareFasmPath
        Case "Set Grid Size..":
            On Error Resume Next
            'GridSize = InputBox("Set New Grid Size", "Grid Size", GridSize)
            'If GridSize < 50 Then MsgBox "Grid Size must be greater than 50. GridSize was set to 50.": GridSize = 50
        Case "Show Summary.."
            If cmdExtra(2).Checked = True Then cmdExtra(2).Checked = False Else: cmdExtra(2).Checked = True
            SaveSetting "Visia", "Settings", "Summary", cmdExtra(2).Checked
            ShowSummary = cmdExtra(2).Checked
        Case "Use Auto Templates.."
            If cmdExtra(3).Checked = True Then cmdExtra(3).Checked = False Else: cmdExtra(3).Checked = True
            SaveSetting "Visia", "Settings", "AutoTemplates", cmdExtra(3).Checked
            AutoTemplates = cmdExtra(3).Checked
    End Select
End Sub

Private Sub cmdHelp_Click(Index As Integer)
    Call Me.ucTab_BeforeClick(False): LastMnuOpenIndex = -2
    Select Case cmdHelp(Index).Caption
        Case "About": frmAbout.Show
    End Select
End Sub

Private Sub cmdImportModule_Click()
        On Error GoTo ImportFailed
        cDExport.CancelError = True
        cDExport.Filter = "Plain Text (*.*)|*.*"
        cDExport.ShowOpen
        
        Dim ImportContent As String
        GetVirtualFileContent (tvProject.Nodes(tvProject.SelectedItem.Index).Text)
        If Trim(cDExport.FileName) <> "" Then
            Open cDExport.FileName For Binary As #1
                ImportContent = Space(LOF(1))
                Get #1, , ImportContent
            Close #1
                lModuleID = 1
                While VirtualFileExists("Module" & lModuleID): lModuleID = lModuleID + 1: DoEvents: Wend
                If FindProjectFolder("Modules") = False Then tvProject.Nodes.Add , , "Mod", "Modules", "Folder": tvProject.Nodes(tvProject.Nodes.Count).Expanded = True
                tvProject.Nodes.Add "Mod", tvwChild, "Mod" & lModuleID, "Module" & lModuleID, "Module"
                CreateVirtualFile "Module" & lModuleID, EX_MODULE, ImportContent
            Exit Sub
        End If
ImportFailed:
    MsgBox "Could not import file."
    Close #1
End Sub

Private Sub Code_Change()
    isDirty = True
    'Line introduced by Wilksey! (2014) for real time syntax highlighting
    Code.ColourEntireRTB
End Sub


Sub SelectObjectParent(ObjName As String)
    Dim i As Long
    For i = 1 To cmbParent.ComboItems.Count
        If Trim(ObjName) = cmbParent.ComboItems(i).Text Then
            cmbParent.ComboItems(i).Selected = True
        End If
    Next i
End Sub

Sub SelectObjectObject(ObjName As String)
    Dim i As Long
    For i = 1 To cmbObject.ComboItems.Count
        If Trim(ObjName) = cmbObject.ComboItems(i).Text Then
            cmbObject.ComboItems(i).Selected = True
            cmbObject_Click
        End If
    Next i
End Sub

Function GetLineNumberByCarret(CarretPosition As Long)
    Dim ActualLine As Integer
    Dim CodeSource As String
    Dim i As Long
    CodeSource = Code.Text
    
    ActualLine = 1
    For i = 1 To CarretPosition
        If Mid$(CodeSource, i, 2) = vbCrLf Then
            ActualLine = ActualLine + 1
        End If
    Next i
    GetLineNumberByCarret = ActualLine
End Function

Public Sub MenuShortCuts(KeyCode As Integer, Shift As Integer)

    If (Shift And vbAltMask) And KeyCode = vbKeyF Then
            lblMnu_MouseMove 0, 1, 0, 1, 1
            SendKeys "{DOWN}"
            lblMnu_MouseUp 0, 0, 0, 1, 1
    ElseIf (Shift And vbAltMask) And KeyCode = vbKeyE Then
            lblMnu_MouseMove 1, 0, 0, 1, 1
            SendKeys "{DOWN}"
            lblMnu_MouseUp 1, 0, 0, 1, 1
    ElseIf (Shift And vbAltMask) And KeyCode = vbKeyS Then
            frmMain.lblMnu_MouseMove 2, 0, 0, 1, 1
            SendKeys "{DOWN}"
            frmMain.lblMnu_MouseUp 2, 0, 0, 1, 1
    ElseIf (Shift And vbAltMask) And KeyCode = vbKeyC Then
            frmMain.lblMnu_MouseMove 3, 0, 0, 1, 1
            SendKeys "{DOWN}"
            frmMain.lblMnu_MouseUp 3, 0, 0, 1, 1
    ElseIf (Shift And vbAltMask) And KeyCode = vbKeyX Then
            lblMnu_MouseMove 4, 0, 0, 1, 1
            SendKeys "{DOWN}"
            lblMnu_MouseUp 4, 0, 0, 1, 1
    ElseIf (Shift And vbAltMask) And KeyCode = vbKeyH Then
            lblMnu_MouseMove 5, 0, 0, 1, 1
            SendKeys "{DOWN}"
            lblMnu_MouseUp 5, 0, 0, 1, 1
    End If
End Sub

Private Sub Code_KeyDown(KeyCode As Integer, Shift As Integer)
    
    On Error GoTo NoTemplate
    Dim i As Long
    Dim CountTabs As Long
    Dim CountTabsLine As String
    'MsgBox KeyCode

    Dim mnuID As Integer
    
    MenuShortCuts KeyCode, Shift
    
    If AutoTemplates = True Then
        If KeyCode = 188 Then
            If Mid$(Code.Text, Code.GetCarretPos + 1, 1) = Chr(34) Then
                Code.SetCarretPos Code.GetCarretPos + 1
            End If
        End If
        
        'MsgBox Chr(34) & Mid$(Code.Text, Code.GetCarretPos - 1, 3) & Chr(34)
        If Shift = 1 And KeyCode = 56 Then
            If UCase(Mid$(Code.Text, Code.GetCarretPos, 1)) >= "A" And _
                UCase(Mid$(Code.Text, Code.GetCarretPos, 1)) <= "Z" Or _
                UCase(Mid$(Code.Text, Code.GetCarretPos - 1, 2)) >= "A " And _
                UCase(Mid$(Code.Text, Code.GetCarretPos - 1, 2)) <= "Z " Then
                If Mid$(Code.Text, Code.GetCarretPos - 1, 2) = "if" Or _
                   Mid$(Code.Text, Code.GetCarretPos - 2, 3) = "for" Or _
                   Mid$(Code.Text, Code.GetCarretPos - 4, 5) = "while" Then
                    KeyCode = 0
                    Code.AddCharacter " ()"
                    Code.SetCarretPos Code.GetCarretPos + 2
                ElseIf Mid$(Code.Text, Code.GetCarretPos - 2, 3) = "if " Or _
                       Mid$(Code.Text, Code.GetCarretPos - 3, 4) = "for " Or _
                       Mid$(Code.Text, Code.GetCarretPos - 5, 6) = "while " Then
                    Code.AddCharacter ")"
                Else
                    'MsgBox Chr(34) & Mid$(Code.Text, Code.GetCarretPos + 1, 1) & Chr(34)
                    If Mid$(Code.Text, Code.GetCarretPos + 1, 2) = vbCrLf Or _
                       Mid$(Code.Text, Code.GetCarretPos + 1, 1) = vbTab Or _
                       Mid$(Code.Text, Code.GetCarretPos + 1, 1) = " " Then
                        Code.AddCharacter ");"
                    End If
                End If
            End If
        End If
        
        If Shift = 1 And KeyCode = 50 Then Code.AddCharacter Chr(34)
    
        If KeyCode = 13 Then
            If Mid$(Code.Text, Code.GetCarretPos + 1, 2) = ");" Then
                Code.SetCarretPos Code.GetCarretPos + 2
                KeyCode = 0
            ElseIf Mid$(Code.Text, Code.GetCarretPos + 1, 1) = ")" Then
                Code.SetCarretPos Code.GetCarretPos + 1
                Code.AddCharacter " "
                Code.SetCarretPos Code.GetCarretPos + 1
                KeyCode = 0
            End If
        End If
    End If
    
    If KeyCode = 13 Then
        Dim CutCode As String
        CutCode = Mid$(Code.Text, Code.GetCarretPos - 50, 50)
        If InStrRev(CutCode, "entry", -1, vbTextCompare) <> 0 Then CodeScan
        If InStrRev(CutCode, "frame", -1, vbTextCompare) <> 0 Then CodeScan
        If InStrRev(CutCode, "export", -1, vbTextCompare) <> 0 Then CodeScan
        If InStrRev(CutCode, "import", -1, vbTextCompare) <> 0 Then CodeScan
    End If
NoTemplate:
End Sub


Private Sub Code_KeyUp(KeyCode As Integer, Shift As Integer)
    'MsgBox KeyCode

    SelectObjectByScan
End Sub

Private Sub Code_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    LastMnuOpenIndex = -2
    If Button = 2 Then frmMain.PopupMenu mnuEdit
End Sub

Private Sub Command1_Click()
End Sub

Private Sub Code_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    SelectObjectByScan
End Sub

Public Sub CheckForAssociation()
    Dim IsAssigned As String
    If InStr(1, CheckFileAssociation("via"), "4.8.7", vbTextCompare) = 0 Then
        DeleteFileAssociation "via"
    End If
    If CheckFileAssociation("via") <> "" Then Exit Sub
    MakeFileAssociation "via", App.Path & "\", App.EXEName, "", "Associate.ico"
    SHChangeNotify SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0
End Sub

Public Sub SelectEntryFile()
    On Error Resume Next
    frmMain.tvProject_NodeClick frmMain.tvProject.Nodes.Item(2)
    frmMain.Code.SetFocus
End Sub

Private Sub conInTab_Resize()
    linBetweenCode.X2 = conInTab.Width
    
End Sub

Private Sub conProject_Resize()
    SmoothGradient conProject.hdc, &HC8D0D4, vbWhite, 0, 0, conProject.Width, conProject.Height, gr_Fill_Horizontal, False, 2
    conProject.Refresh
End Sub

Private Sub conProperties_GotFocus()
    shpProp.BackColor = &H80000002
End Sub

Private Sub conProperties_LostFocus()
    shpProp.BackColor = &H80000003
End Sub

Private Sub conProperties_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    LastMnuOpenIndex = -2
End Sub

Private Sub conToolbox_GotFocus()
    'If tvTools.Visible = True Then shpTitleToolBox.BackColor = &H80000002
End Sub

Private Sub conToolbox_LostFocus()
    shpTitleToolBox.BackColor = &H80000003
End Sub

Private Sub conToolbox_Resize()
    SmoothGradient conToolbox.hdc, vbWhite, &HC8D0D4, 0, 0, conToolbox.Width, conToolbox.Height, gr_Fill_Horizontal, False, 2
    conToolbox.Refresh
End Sub

Private Sub Form_KeyDown(KeyCode As Integer, Shift As Integer)
    MenuShortCuts KeyCode, Shift
End Sub

Private Sub Form_Load()
    
    isDirty = False: RunEnabled = True
    
    'SetOfficeBorder Code, True, True
    'AddOfficeBorders frmMain, ctPictureBox, False
    
    With ucTab
         .BorderStyle = GpTabBorderStyle3DThin
         .Style = GpTabStyleWinXP
         .Placement = GpTabPlacementTopRight
         .TabStyle = GpTabTrapezoid
         .TabWidthStyle = GpTabFixed
         .TabFixedHeight = 280
         .TabFixedWidth = 1100
         .HotTracking = False
         .AutoBackColor = False
    End With
    
    ucTab.Tabs.Clear
    DoEvents
    
    CheckForAssociation
    
    tvTools.Nodes.Add , , "Root", "Objects", 18
    tvTools.Nodes.Add "Root", tvwChild, "Button", "Button", 1
    tvTools.Nodes.Add "Root", tvwChild, "Edit", "Edit", 2
    tvTools.Nodes.Add "Root", tvwChild, "Picture", "Picture", 3
    tvTools.Nodes.Add "Root", tvwChild, "Frame", "Frame", 4
    tvTools.Nodes.Add "Root", tvwChild, "Label", "Label", 5
    tvTools.Nodes.Add "Root", tvwChild, "Check", "Check", 6
    tvTools.Nodes.Add "Root", tvwChild, "Radio", "Radio", 7
    tvTools.Nodes.Add "Root", tvwChild, "Combo", "Combo", 8
    tvTools.Nodes.Add "Root", tvwChild, "List", "List", 9
    tvTools.Nodes.Add "Root", tvwChild, "Progress", "Progress", 10
    tvTools.Nodes.Add "Root", tvwChild, "HScroll", "HScroll", 11
    tvTools.Nodes.Add "Root", tvwChild, "VScroll", "VScroll", 12
    tvTools.Nodes.Add "Root", tvwChild, "Slider", "Slider", 13
    tvTools.Nodes.Add "Root", tvwChild, "Tabs", "Tabs", 14
    tvTools.Nodes.Add "Root", tvwChild, "TreeView", "TreeView", 15
    tvTools.Nodes.Add "Root", tvwChild, "ToolBar", "ToolBar", 16
    tvTools.Nodes.Add "Root", tvwChild, "StatusBar", "StatusBar", 17
    tvTools.Nodes(1).Expanded = True
    
    GenerateMainMenus
    
    If GetSetting("Visia", "Settings", "Summary", False) = True Then ShowSummary = True: cmdExtra(2).Checked = True Else: ShowSummary = False: cmdExtra(2).Checked = False
    
    AutoTemplates = GetSetting("Visia", "Settings", "AutoTemplates", True)
    
    If AutoTemplates Then cmdExtra(3).Checked = True Else: cmdExtra(3).Checked = False
    
    If GetSetting("Visia", "Settings", "Maximized", False) = True Then
        frmMain.WindowState = vbMaximized
    ElseIf GetSetting("Visia", "Settings", "Minimized", False) = True Then
        'Nothing
    Else
        frmMain.Move GetSetting("Visia", "Settings", "Left", 400), _
                     GetSetting("Visia", "Settings", "Top", 400), _
                     GetSetting("Visia", "Settings", "Width", 11000), _
                     GetSetting("Visia", "Settings", "Height", 7200)
    End If
                 
    'FasmPath = VBA.GetSetting("Visia", "Settings", "FasmPath", FasmPath)
    
    Code.ceKeyWords = "*application*entry*bitmap*icon*library*export*include*import*ascii*unicode*frame*end*alias*for*while*lib*const*bool*boolean*byte*word*dword*string*single*if*else*type*iassembler*return*goto*label*add*sub*mul*div*mod*shr*shl*or*xor*and*direct*address*as*with*loop*up*down*property*set*get*class*object*"
    Code.ceOperators = "*local*GUI*CUI*PE*DLL*TRUE*FALSE*NULL*signed*unsigned*ubound*lbound*destroy*reserve*preserve*"

    frmMain.Show: DoEvents
    
    If Command$ <> "" And Not Mid(Command$, 1, 2) = "/c" Then
        cD.FileName = Command$: DoEvents
        OpenProject True
        SelectEntryFile
    Else
        NewProject True
        SelectEntryFile
    End If

    SetDesignerParent
    
End Sub

Sub SetDesignerParent()
    SetParent frmDesign.hwnd, Designer.hwnd
    frmDesign.Show: DoEvents
End Sub

Sub MakeInitControls(Optional OpenProject As Boolean)
    InitVirtualFiles
    InitScanObjects
    
    ucTab.Visible = True
        
    tvProject.Nodes.Clear
    ucTab.Tabs.Clear
    
    mcTB.SetButtonValue 14, BTN_Enabled, False
    mcTB.SetButtonValue 13, BTN_Enabled, True
    
    tvProject.Nodes.Add , , "App", "Application", "Folder"
    
    If Not OpenProject Then tvProject.Nodes.Add "App", tvwChild, "Entry", "Entry Point", "Entry"
    If Not OpenProject Then CreateVirtualFile "Entry Point", EX_ENTRY, ""
    
    tvProject.Nodes(1).Expanded = True
       
    On Error Resume Next
    If Not OpenProject Then Code.SetFocus
    Code.Visible = False: ToolBarCodeEditDisable
    Designer.Visible = False
    lLastTab = 200
    tvTools.Visible = False: shp4Tools.Visible = False

End Sub

Sub ToolBarCodeEditDisable()
    mcTB.SetButtonValue 6, BTN_Enabled, False
    mcTB.SetButtonValue 7, BTN_Enabled, False
    mcTB.SetButtonValue 8, BTN_Enabled, False
    mcTB.SetButtonValue 9, BTN_Enabled, False
    mcTB.SetButtonValue 11, BTN_Enabled, False
    mcTB.SetButtonValue 12, BTN_Enabled, False
    cmbObject.Visible = False: cmbParent.Visible = False
    linBetweenCode.Visible = False
End Sub

Sub ToolBarCodeEditEnable()
    mcTB.SetButtonValue 6, BTN_Enabled, True
    mcTB.SetButtonValue 7, BTN_Enabled, True
    mcTB.SetButtonValue 8, BTN_Enabled, True
    mcTB.SetButtonValue 9, BTN_Enabled, True
    mcTB.SetButtonValue 11, BTN_Enabled, True
    mcTB.SetButtonValue 12, BTN_Enabled, True
    cmbObject.Visible = True: cmbParent.Visible = True
    linBetweenCode.Visible = True
End Sub

Public Sub Form_Resize()
   
    On Error Resume Next
    
    With conInTab
        .Left = conToolbox.Width + 20
        .Height = frmMain.Height + picMnu.Height - .Top - 1120
        .Width = frmMain.Width - conProject.Width - conToolbox.Width - 160
    End With
    
    With Code
        .Top = 380
        .Left = 40
        .Height = conInTab.Height - 410
        .Width = conInTab.Width - 80
    End With
    
   
    With Designer
        .Height = frmMain.Height + picMnu.Height - .Top - 2320
        .Width = frmMain.Width - conProject.Width - conToolbox.Width - 220
    End With
    
    With ucTab
        .Left = conToolbox.Width - 20
        .Height = frmMain.Height + picMnu.Height - .Top - 1070
        .Width = frmMain.Width - conProject.Width - conToolbox.Width - 90
    End With
    
    txtInfoSel.Left = conToolbox.Width + ucTab.Width / 3
    txtInfoSel.Top = picMnu.Height + picToolB.Height + ucTab.Height / 2
    tvProject.Height = frmMain.Height / 2.5 - tvProject.Top - 200
    conProperties.Top = tvProject.Top + tvProject.Height + 300
    conProperties.Height = frmMain.Height - tvProject.Height - 2400
    
    lblProp.Top = tvProject.Height + 690
    shpProp.Top = tvProject.Height + 690

    cmbObject.Left = 40
    cmbObject.Width = ucTab.Width / 2
    cmbParent.Left = cmbObject.Left + cmbObject.Width + 50
    cmbParent.Width = ucTab.Width / 2 - 190
    
    With tvTools
        .Left = 10
        .Width = conToolbox.Width - 100
        .Height = frmMain.Height - 1700
    End With
    
    With mcTB
       .Width = frmMain.ScaleWidth - .Left - 250
    End With
    tbLeft.Left = 20
    tbRight.Left = frmMain.Width - tbRight.Width - 170
    
    linEdgeTool.Y1 = frmMain.Height
    linEdgeProject.Y1 = frmMain.Height
    linEdgeTop.X1 = conToolbox.Width
    linEdgeTop.X2 = frmMain.Width - conProject.Width
    
    linEdgeBelow.X1 = conToolbox.Width
    linEdgeBelow.X2 = frmMain.Width - conProject.Width
    linEdgeBelow.Y1 = frmMain.Height + picMnu.Height - sbMain.Height - 800
    linEdgeBelow.Y2 = frmMain.Height + picMnu.Height - sbMain.Height - 800
    
    With shp4Project
        .Left = tvProject.Left - 30
        .Top = 0
        .Height = tvProject.Height + 660
        .Width = tvProject.Width + 60
    End With
    
    With shp4Properties
        .Left = conProperties.Left - 10
        .Top = conProperties.Top - 20
        .Height = conProperties.Height + 40
        .Width = conProperties.Width + 30
    End With
    
    With shp4Tools
        .Left = tvTools.Left - 10
        .Top = 0
        .Height = tvTools.Height + 260
        .Width = tvTools.Width + 20
    End With
    
End Sub
Private Sub cmdFile_Click(Index As Integer)
    Me.ucTab_BeforeClick (False)
    Select Case cmdFile(Index).Caption
        Case "New Project": NewProject True
        Case "Open Project": OpenProject
        Case "Save Project": SaveProject False
        Case "Save Project As..": SaveProject True
        Case "Exit": ucTab_BeforeClick False: DoEvents: Unload Me
        Case Else
    End Select
    
End Sub

Public Function FindProjectFolder(FolderName As String) As Boolean
    Dim i As Integer
    For i = 1 To tvProject.Nodes.Count
        If tvProject.Nodes(i).Image = "Folder" And tvProject.Nodes(i).Text = FolderName Then
            FindProjectFolder = True
            Exit Function
        End If
    Next i
End Function

Public Sub OpenProject(Optional NoCD As Boolean)
    On Error GoTo CancelOpen
    
    If NoCD = True Then GoTo lNoCD
    
    If CheckUnsaved = True Then Exit Sub
    
    
    With cD
        .Filter = "Visia Project Files (*.via)|*.via|Linley Project Files (*.lnl)|*.lnl"
        .FileName = ""
        .CancelError = True
        .ShowOpen
    End With
    
lNoCD:
    If cD.FileName = "" Then Exit Sub
    If InStr(1, cD.FileName, Chr$(34)) <> 0 Then
        cD.FileName = Mid$(cD.FileName, InStr(1, cD.FileName, Chr$(34)) + 1, InStr(InStr(1, cD.FileName, Chr$(34)) + 1, cD.FileName, Chr$(34)) - InStr(1, cD.FileName, Chr$(34)) - 1)
    End If
    frmMain.Caption = Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) & " - Visia Compiler 4.8.7"
    'Open File
    
    Dim i As Long: Dim Ident As String: Dim FileNum As Long: Dim NumberOfItems As Long
    
    On Error GoTo InvalidFileType

    FileNum = FreeFile
    
    MakeInitControls True
    InitVirtualFiles
    
    Open cD.FileName For Binary As #FileNum
        Get #FileNum, , NumberOfItems
        ReDim VirtualFiles(UBound(VirtualFiles) + NumberOfItems) As TYPE_VIRTUAL_FILE
        For i = 1 To NumberOfItems
            Get #FileNum, , VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i)
            If VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Extension = EX_MODULE Then
                If FindProjectFolder("Modules") = False Then tvProject.Nodes.Add , , "Mod", "Modules", "Folder": tvProject.Nodes(tvProject.Nodes.Count).Expanded = True
                tvProject.Nodes.Add "Mod", tvwChild, _
                    VirtualFiles(UBound(VirtualFiles) - 1 - NumberOfItems + i).Name, _
                    VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Name, "Module"
            ElseIf VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Extension = EX_ENTRY Then
                tvProject.Nodes.Add "App", tvwChild, _
                    "Entry", _
                    VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Name, "Entry"
            ElseIf VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Extension = EX_DIALOG Then
                If FindProjectFolder("Resources") = False Then tvProject.Nodes.Add , , "Res", "Resources", "Folder": tvProject.Nodes(tvProject.Nodes.Count).Expanded = True
                tvProject.Nodes.Add "Res", tvwChild, _
                    VirtualFiles(UBound(VirtualFiles) - 1 - NumberOfItems + i).Name, _
                    VirtualFiles(UBound(VirtualFiles) - NumberOfItems + i).Name, "Dialog"
            End If
        Next i
    Close #1
    
    isDirty = False
    frmMain.SelectEntryFile
    Exit Sub
InvalidFileType:
    MsgBox "Error while loading file '" & cD.FileName & "'", vbCritical, "Visia Compiler 4.8.7"
CancelOpen:
    MakeInitControls
End Sub

Function SaveProject(SaveAs As Boolean) As Boolean
    
    On Error GoTo SaveCancelError
    
    SaveProject = False
    
    If SaveAs = False Then If cD.FileName <> "" Then GoTo SaveNow
    
    With cD
    .Filter = "Visia Project Files (*.via)|*.via|Linley Project Files (*.lnl)|*.lnl"
    .CancelError = True
    .ShowSave
    End With
    
    If Dir(cD.FileName) <> "" Then
        If MsgBox("File already exist! Overwrite?", vbYesNo + vbCritical) = vbNo Then
            Exit Function
        End If
    End If
    
SaveNow:
    
    ucTab_BeforeClick False
    DoEvents
    Code.Visible = True: ToolBarCodeEditEnable
    
    Dim i As Long: Dim FileNum As Long
    
    FileNum = FreeFile
    
    If Dir(cD.FileName) <> "" Then Kill cD.FileName
    
    Open cD.FileName For Binary As #FileNum
        
        Put #FileNum, , UBound(VirtualFiles)
        For i = 1 To UBound(VirtualFiles): Put #FileNum, , VirtualFiles(i): Next i
    
    Close #FileNum
    
    frmMain.Caption = Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) & " - Visia Compiler 4.8.7"
    isDirty = False
    SaveProject = True
    Exit Function
SaveCancelError:
    SaveProject = False
End Function

Public Sub MakeExecuteable()

    On Error GoTo ExecCancelError
    
    With cDExe
    .Filter = "Executable Files|*.exe"
    .CancelError = True
    .ShowSave
    End With
    
    If Dir(cDExe.FileName) <> "" Then
        If MsgBox("File already exists! Overwrite?", vbYesNo + vbCritical) = vbNo Then
            Exit Sub
        End If
    End If
    
    Compile cDExe.FileName, False
    isDirty = False
ExecCancelError:

End Sub

Sub NewProject(CreateNew As Boolean)
    If CheckUnsaved = True Then Exit Sub
    Code.Text = ""
    cD.FileName = ""
    frmMain.Caption = "Unsaved - Visia Compiler 4.8.7"
    'NewTemplate
    frmNew.Show 1
    isDirty = False
End Sub

Function CheckUnsaved() As Boolean
    If isDirty = True Then
        Select Case MsgBox("Project has been changed do you want to save your current project?", vbInformation + vbYesNoCancel)
            Case vbYes: SaveProject False
            Case vbCancel: CheckUnsaved = True
        End Select
    End If
End Function
Sub GenerateMainMenus()
    
    Code.RemoveBorder
    
    Dim i As Integer
    
    For i = 1 To 6: Load cmdFile(i): Next i
    For i = 1 To 8: Load cmdEdit(i): Next i
    For i = 1 To 2: Load cmdCompile(i): Next i
    For i = 1 To 3: Load cmdExtra(i): Next i
    For i = 1 To 2: Load cmdHelp(i): Next i
    
    'File Menu
    cmdFile(0).Caption = "New Project"
    cmdFile(1).Caption = "Open Project"
    cmdFile(2).Caption = "-"
    cmdFile(3).Caption = "Save Project"
    cmdFile(4).Caption = "Save Project As.."
    cmdFile(5).Caption = "-"
    cmdFile(6).Caption = "Exit"
    
    'Edit Menu
    cmdEdit(0).Caption = "Undo"
    cmdEdit(1).Caption = "Redo"
    cmdEdit(2).Caption = "-"
    cmdEdit(3).Caption = "Cut"
    cmdEdit(4).Caption = "Copy"
    cmdEdit(5).Caption = "Paste"
    cmdEdit(6).Caption = "Delete"
    cmdEdit(7).Caption = "-"
    cmdEdit(8).Caption = "Select All"
    
    'Compile Menu
    cmdCompile(0).Caption = "Link && Run"
    cmdCompile(1).Caption = "-"
    cmdCompile(2).Caption = "Create Executable (*.exe|*.dll)"
    
    'Extra Menu
    cmdExtra(0).Caption = "Set Grid Size.."
    cmdExtra(1).Caption = "Set FASM Path.."
    cmdExtra(2).Caption = "Show Summary.."
    cmdExtra(3).Caption = "Use Auto Templates.."
    
    'Help Menu
    cmdHelp(0).Caption = "Contents"
    cmdHelp(1).Caption = "-"
    cmdHelp(2).Caption = "About"
    
End Sub

Private Sub Form_Unload(Cancel As Integer)
    ucTab_BeforeClick False
    'SaveSetting "Visia", "Settings", "GridSize", GridSize
    SaveSetting "Visia", "Settings", "AutoTemplates", AutoTemplates
    If frmMain.WindowState = vbMaximized Then
        SaveSetting "Visia", "Settings", "Maximized", True
    ElseIf frmMain.WindowState = vbMinimized Then
        SaveSetting "Visia", "Settings", "vbMinimized", True
    Else
        SaveSetting "Visia", "Settings", "Maximized", False
        SaveSetting "Visia", "Settings", "Left", frmMain.Left
        SaveSetting "Visia", "Settings", "Top", frmMain.Top
        SaveSetting "Visia", "Settings", "Width", frmMain.Width
        SaveSetting "Visia", "Settings", "Height", frmMain.Height
    End If
    If CheckUnsaved = True Then Cancel = True: Exit Sub
    Unload frmDesign
    End
End Sub


Private Sub imgAppView_Click(Index As Integer)
    Dim i As Integer
    imgProjectBar.SetFocus
    Select Case Index
        Case 0
            tvProject.Nodes(1).Expanded = True
            For i = 1 To tvProject.Nodes.Count
                If tvProject.Nodes(i).Image = "Folder" And tvProject.Nodes(i).Text = "Modules" Then
                    tvProject.Nodes(i).Expanded = True
                End If
            Next i
            tvProject.Sorted = True
        Case 2:
            tvProject.Nodes(1).Expanded = False
            For i = 1 To tvProject.Nodes.Count
                If tvProject.Nodes(i).Image = "Folder" And tvProject.Nodes(i).Text = "Modules" Then
                    tvProject.Nodes(i).Expanded = False
                End If
            Next i
        Case 1: PopupMenu cmdAddToProject
        Case 3: Call cmdDelTreeView_Click
    End Select
    shpSelectAppView.Visible = False
End Sub

Private Sub imgAppView_MouseMove(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    shpSelectAppView.Visible = True
    shpSelectAppView.Left = imgAppView(Index).Left - 30
End Sub

Private Sub imgClsToolBox_Click()
    conToolbox.Width = 1
    conToolbox.Visible = False
    frmMain.Form_Resize
End Sub

Private Sub imgProjectBar_GotFocus()
    shpTitleProject.BackColor = &H80000002
End Sub

Private Sub imgProjectBar_LostFocus()
    shpTitleProject.BackColor = &H80000003
    shpSelectAppView.Visible = False
End Sub

Private Sub imgProjectBar_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    shpSelectAppView.Visible = False
End Sub

Private Sub imgProjectBar_Paint()
    SmoothGradient imgProjectBar.hdc, vbWhite, &HC8D0D4, 0, 0, imgProjectBar.Width, imgProjectBar.Height, GR_Fill_Vertical, True, 10
    imgProjectBar.Refresh
End Sub

Private Sub imgProjectClose_Click()
    conProject.Width = 1
    conProject.Visible = False
    frmMain.Form_Resize
End Sub


Public Sub lblMnu_MouseDown(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    shpMenu.BackColor = &H3B175
End Sub

Public Sub lblMnu_MouseMove(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    If shpMenu.Visible = False Then
        shpMenu.Left = lblMnu(Index).Left
        shpMenu.Width = lblMnu(Index).Width
        shpMenu.Visible = True
    ElseIf LastMnuIndex <> Index Then
        shpMenu.Visible = False
    End If
    LastMnuIndex = Index
End Sub

Public Sub lblMnu_MouseUp(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Not Button = 0 Then If LastMnuOpenIndex = Index Then LastMnuOpenIndex = -1: GoTo NoMnuPop
    shpMenu.BackColor = &HBF7D35: lblMnu(Index).ForeColor = vbWhite
    LastMnuOpenIndex = Index
    Select Case Index
        Case 0: PopEye mnuFile
        Case 1: PopEye mnuEdit
        Case 2: PopEye mnuSearch
        Case 3: PopEye mnuCompile
        Case 4: PopEye mnuExtra
        Case 5: PopEye mnuHelp
    End Select
NoMnuPop:
    If LastMnuOpenIndex = -2 Then LastMnuOpenIndex = -1
    shpMenu.Visible = False
    shpMenu.BackColor = &HD5F2&
    lblMnu(Index).ForeColor = vbBlack
End Sub

Sub PopEye(Mnue As Menu)
    PopupMenu Mnue, , shpMenu.Left, shpMenu.Top + shpMenu.Height
End Sub

Private Sub lblTabClose_Click()
    Call cmdCloseAllTabs_Click
    
End Sub

Private Sub mcTB_Click(ByVal vButton_Index As Long)
    LastMnuOpenIndex = -2
    Select Case vButton_Index
        Case 0: cmdFile_Click 0
        Case 1: 'Additem
        Case 2: cmdFile_Click 1
        Case 3: cmdFile_Click 3
        Case 4: cmdFile_Click 4
        Case 11: Code.Undo
        Case 12: Code.Undo
        Case 6: Code.Cut
        Case 7: Code.CopyM
        Case 8: Code.Delete
        Case 9: Code.Paste
        Case 13: cmdCompile_Click 0
        Case 14
            If AppType <> 0 Then
                ErrMessage "compiling process aborted by user."
                RunEnabled = False
            Else
                TerminateProcess hWndProg, 0
            End If
        Case 16
            If conProject.Visible = True Or conToolbox.Visible = True Then
                conProject.Visible = False: conToolbox.Visible = False
                conProject.Width = 1: conToolbox.Width = 1
                Form_Resize
            Else
                conProject.Visible = True: conToolbox.Visible = True
                conProject.Width = 2175: conToolbox.Width = 1800
                Form_Resize
            End If
    End Select
End Sub

Private Sub Timer1_Timer()

End Sub



Private Sub picMnu_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    LastMnuOpenIndex = -2
End Sub

Private Sub picMnu_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If shpMenu.Visible = True Then shpMenu.Visible = False
End Sub

Private Sub picMnu_Resize()
    SmoothGradient picMnu.hdc, vbWhite, &HC8D0D4, 0, 0, frmMain.Width, picMnu.Height, gr_Fill_Horizontal, False, 15
    picMnu.Refresh
    SmoothGradient picToolB.hdc, vbWhite, &HC8D0D4, 0, 0, frmMain.Width, picToolB.Height, gr_Fill_Horizontal, False, 15
    picToolB.Refresh
    SmoothGradient imgProjectBar.hdc, vbWhite, &HC8D0D4, 0, 0, imgProjectBar.Width, imgProjectBar.Height, GR_Fill_Vertical, True, 10
    imgProjectBar.Refresh
End Sub

Private Sub Picture1_Click()

End Sub

Private Sub sbMain_Resize()
    SmoothGradient sbMain.hdc, vbWhite, &HC8D0D4, 0, 0, sbMain.Width, sbMain.Height, gr_Fill_Horizontal, False, 15
    sbMain.Refresh
    imgReIDE.Left = frmMain.Width - 350
End Sub

Private Sub tmrApplicationRuntime_Timer()
    Dim i As Long
    
    GetExitCodeProcess hWndProg, ExitCode

    If ExitCode = STILL_ACTIVE& Then
        If RunEnabled = True Then
            frmMain.Caption = Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) & " - Visia Compiler 4.8.7 [Running]"
            Code.Enabled = False: Code.BackColor = &H8000000F: DoEvents
            mcTB.SetButtonValue 14, BTN_Enabled, True
            For i = 0 To 13: mcTB.SetButtonValue i, BTN_Enabled, False: Next i
            For i = 15 To mcTB.Button_Count - 1: mcTB.SetButtonValue i, BTN_Enabled, False: Next i
            mnuCompile.Enabled = False: mnuExtra.Enabled = False: mnuFile.Enabled = False: mnuHelp.Enabled = False: mnuEdit.Enabled = False: mnuSearch.Enabled = False
            cmbObject.Enabled = False: cmbParent.Enabled = False
            cmbObject.BackColor = &H8000000F: cmbParent.BackColor = &H8000000F

            txtChange.Locked = True
            RunEnabled = False
        End If
    Else
        If RunEnabled = False Then
            frmMain.Caption = Right(cD.FileName, Len(cD.FileName) - InStrRev(cD.FileName, "\", -1, vbTextCompare)) & " - Visia Compiler 4.8.7"
            Code.Enabled = True: Code.BackColor = &H80000005
            mcTB.SetButtonValue 14, BTN_Enabled, False
            For i = 0 To 13: mcTB.SetButtonValue i, BTN_Enabled, True: Next i
            For i = 15 To mcTB.Button_Count - 1: mcTB.SetButtonValue i, BTN_Enabled, True: Next i
            If hWndProg <> 0 Then CloseHandle hWndProg: hWndProg = 0
            mnuCompile.Enabled = True: mnuExtra.Enabled = True: mnuFile.Enabled = True: mnuHelp.Enabled = True: mnuEdit.Enabled = True: mnuSearch.Enabled = True
            cmbObject.Enabled = True: cmbParent.Enabled = True
            cmbObject.BackColor = &H80000005: cmbParent.BackColor = &H80000005:
            txtChange.Locked = False
            RunEnabled = True
            frmMain.Panels.Caption = "Ready .."
            On Error Resume Next
            frmMain.SetFocus: Code.SetFocus
            tmrApplicationRuntime.Enabled = False
        End If
    End If
End Sub

Private Sub tvProject_Click()
    On Error Resume Next
    tvProject.Nodes(1).Sorted = True
    If tvProject.Nodes.Count >= 2 Then tvProject.Nodes(2).Sorted = True
    If tvProject.Nodes.Count >= 3 Then tvProject.Nodes(3).Sorted = True
End Sub

Private Sub tvProject_GotFocus()
    shpTitleProject.BackColor = &H80000002
End Sub

Private Sub tvProject_KeyDown(KeyCode As Integer, Shift As Integer)
    MenuShortCuts KeyCode, Shift
End Sub

Private Sub tvProject_LostFocus()
    shpTitleProject.BackColor = &H80000003
End Sub

Private Sub tvProject_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    'LastMnuOpenIndex = -2
End Sub

Private Sub tvProject_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Button = 2 Then frmMain.PopupMenu frmMain.cmdAddToProject
End Sub

Public Sub tvProject_NodeClick(ByVal Node As MSComctlLib.Node)
    'Dim i As Integer
    
    If Node.Key <> "App" And _
       Node.Key <> "Mod" And _
       Node.Key <> "Entry" And _
       Node.Key <> "Res" Then
        txtChange = Node.Text
        cNodeKey = Node.Index
        txtChange.Enabled = True
        FindOrCreateTab Node.Text
    ElseIf Node.Key = "Entry" Then
        txtChange.Enabled = False
        txtChange.Text = "[Entry]"
        FindOrCreateTab Node.Text
    '    FindOrCreateTab Node.Text
    Else
        txtChange.Enabled = False
        txtChange.Text = "[Folder]"
    End If
    
    'If ucTab.Tabs.Count > 4 Then
    '    ucTab.Visible = False: DoEvents
    '    Call cmdCloseAllTabs_Click
    '    ucTab.Visible = True
    '    tvProject_NodeClick Node
    'End If
    
End Sub

Sub FindOrCreateTab(NodeText As String)
    Dim i As Integer
    Dim tFound As Boolean
    ucTab_BeforeClick False
    For i = 1 To ucTab.Tabs.Count
        If ucTab.Tabs.Item(i).Caption = NodeText Then
            NoBeforeClickEvent = False
            'ucTab.Tabs.Item(i).Selected = True
             ucTab.SelectTab i
            'ucTab.Refresh
            tFound = True
        End If
    Next i
    If tFound = False Then ucTab.Tabs.Add ucTab.Tabs.Count + 1, NodeText, NodeText: NoBeforeClickEvent = True: ucTab.Tabs.Item(ucTab.Tabs.Count).Selected = True
    Code.Text = GetVirtualFileContent(NodeText)
    Code.ColourEntireRTB
    ucTab_Click
    If GetVirtualFileExtension(NodeText) = EX_ENTRY Or _
       GetVirtualFileExtension(NodeText) = EX_MODULE Then
       Code.Visible = True: ToolBarCodeEditEnable
    ElseIf GetVirtualFileExtension(NodeText) = EX_DIALOG Then
        Designer.Visible = True
        tvTools.Visible = True: shp4Tools.Visible = True
    Else
        Designer.Visible = False
        tvTools.Visible = False: shp4Tools.Visible = False
        
        Code.Visible = False: ToolBarCodeEditDisable
    End If
    If tFound = False Then FindOrCreateTab NodeText
End Sub

Private Sub tvTools_KeyDown(KeyCode As Integer, Shift As Integer)
    MenuShortCuts KeyCode, Shift
End Sub

Private Sub tvTools_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    LastMnuOpenIndex = -2
    shpTitleToolBox.BackColor = &H80000002
End Sub

Private Sub tvTools_NodeClick(ByVal Node As MSComctlLib.Node)
    DoCreateObject = True
End Sub

Private Sub txtChange_GotFocus()
    shpProp.BackColor = &H80000002
End Sub

Private Sub txtChange_KeyDown(KeyCode As Integer, Shift As Integer)
    Dim i As Integer
    If KeyCode = 27 Then txtChange.Text = tvProject.Nodes(cNodeKey).Text
    If KeyCode = 13 Then
        If ChangeVirtualFileName(tvProject.Nodes(cNodeKey).Text, txtChange.Text) = True Then
            For i = 1 To ucTab.Tabs.Count
                If ucTab.Tabs.Item(i).Caption = tvProject.Nodes(cNodeKey).Text Then
                    ucTab.Tabs.Remove i
                    ucTab.Tabs.Add i, txtChange.Text, txtChange.Text
                End If
            Next i
            tvProject.Nodes(cNodeKey).Text = txtChange.Text
        Else
            txtChange.Text = tvProject.Nodes(cNodeKey).Text
        End If
    End If
    tvProject.Nodes(1).Sorted = True
    tvProject.Nodes(2).Sorted = True
    tvProject.Nodes(3).Sorted = True
End Sub

Public Sub ucTab_BeforeClick(Cancel As Integer)
    On Error Resume Next
    
    If Not NoBeforeClickEvent Then
        If GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_ENTRY Or _
           GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_MODULE Then
            'Save Code Text
            SetVirtualFileContent ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption, Code.Text
        ElseIf GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_DIALOG Then
            'Save FormDesigner Form
            SetVirtualFileContent ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption, frmDesign.SaveDialog
        End If
    End If
    NoBeforeClickEvent = False
End Sub

Private Sub txtChange_LostFocus()
    shpProp.BackColor = &H80000003
End Sub

Public Sub ucTab_Click()
    On Error GoTo NoTabExists
    If ucTab.Tabs.Count > 0 Then lblTabClose.Visible = True
    
    If lLastTab = ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption Then Exit Sub
    
    Code.Visible = False: ToolBarCodeEditDisable
    Designer.Visible = False
    tvTools.Visible = False: shp4Tools.Visible = False
    
    If GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_ENTRY Or _
       GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_MODULE Then
        Code.Text = GetVirtualFileContent(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption)
        Code.Visible = True: ToolBarCodeEditEnable
        Code.ColourEntireRTB
    ElseIf GetVirtualFileExtension(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption) = EX_DIALOG Then
        Designer.Visible = True
        tvTools.Visible = True: shp4Tools.Visible = True
        'Designer.Load GetVirtualFileContent...
        frmDesign.LoadDialog GetVirtualFileContent(ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption)
    Else
        Designer.Visible = False
        tvTools.Visible = False: shp4Tools.Visible = False
        Code.Visible = False: ToolBarCodeEditDisable
    End If
    lLastTab = ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption
    CodeScan
    cmbObject_Click
NoTabExists:
End Sub

Private Sub ucTab_LostFocus()
    On Error GoTo NoSet
    SetVirtualFileContent ucTab.Tabs.Item(ucTab.SelectTabItem.Index).Caption, Code.Text
NoSet:
End Sub

Private Sub ucTab_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    LastMnuOpenIndex = -2
End Sub

Private Sub ucTab_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Button = 2 Then PopupMenu mnuTab
End Sub

