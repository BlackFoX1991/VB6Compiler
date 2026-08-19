VERSION 5.00
Object = "{3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0"; "RICHTX32.OCX"
Begin VB.UserControl CodeEdit 
   BackColor       =   &H00C8D0D4&
   ClientHeight    =   1950
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   3150
   ScaleHeight     =   1950
   ScaleWidth      =   3150
   Begin VB.PictureBox picLineNumbers 
      Appearance      =   0  '2D
      AutoRedraw      =   -1  'True
      BackColor       =   &H00C8D0D4&
      BorderStyle     =   0  'Kein
      ForeColor       =   &H80000008&
      Height          =   1695
      Left            =   135
      ScaleHeight     =   1695
      ScaleWidth      =   330
      TabIndex        =   1
      TabStop         =   0   'False
      Top             =   135
      Visible         =   0   'False
      Width           =   330
      Begin VB.Line linNumbers 
         BorderColor     =   &H009AA6A7&
         X1              =   315
         X2              =   315
         Y1              =   -45
         Y2              =   1665
      End
   End
   Begin RichTextLib.RichTextBox RTB 
      Height          =   1695
      Left            =   630
      TabIndex        =   0
      Top             =   135
      Width           =   2175
      _ExtentX        =   3836
      _ExtentY        =   2990
      _Version        =   393217
      BorderStyle     =   0
      HideSelection   =   0   'False
      ScrollBars      =   3
      DisableNoScroll =   -1  'True
      BulletIndent    =   1
      Appearance      =   0
      RightMargin     =   1e7
      TextRTF         =   $"CodeEdit.ctx":0000
      BeginProperty Font {0BE35203-8F91-11CE-9DE3-00AA004BB851} 
         Name            =   "Courier New"
         Size            =   9.75
         Charset         =   0
         Weight          =   400
         Underline       =   0   'False
         Italic          =   0   'False
         Strikethrough   =   0   'False
      EndProperty
   End
   Begin VB.Shape shp4RTB 
      BorderColor     =   &H009AA6A7&
      Height          =   1680
      Left            =   540
      Top             =   45
      Visible         =   0   'False
      Width           =   2490
   End
End
Attribute VB_Name = "CodeEdit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
'---------------------------------------------------------------------------------------
' Module    : CodeEdit Active-X Control
' Date      : Dec 2002
' Author    : Karl Durrance (KDurrance@Hotmail.com)
' Purpose   : CodeEdit Control
'             Provides Efficient syntax colouring and basic editor functions
'---------------------------------------------------------------------------------------
' This Control is FreeWare, and may be freely used and distributed in your own projects

' You may not publish any part of this Source code
' without giving credit to me for the parts that I have created..

' The control is 'As-is', use it at your own risk

' Using this control means that you understand that I will not be responsible
' for any damage this may cause to any file you open with this control.
'---------------------------------------------------------------------------------------

Option Explicit

'General API Declarations
Private Declare Function SendMessage Lib "user32" Alias "SendMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, lParam As Any) As Long
Private Declare Function LockWindowUpdate Lib "user32" (ByVal hwndLock As Long) As Long

'Default Property Values:
Const m_def_ColourStrings = &HC000C0    ' purple
Const m_def_ColourOperator = &HFF&      ' red
Const m_def_ColourKeyWord = &HFF0000    ' blue
Const m_def_ColourComment = &H8000&     ' green

Const COMMENT_IDENTIFER = "//"           ' comment line char

Const m_def_ceBoldWords = ""
Const m_def_ceOperators = ""
Const m_def_ceKeyWords = ""

Const m_def_NormaliseCase = True
Const m_def_ForeColor = 0
Const m_def_BackStyle = 0
Const m_def_SyntaxColouring = True
Const m_def_ProcessStrings = True
Const m_def_ItalicComments = True
Const m_def_BoldSelectedKeyWords = False
Const m_def_WordWrap = False
Const m_def_LineNumbers = False
Const m_def_SelStart = 0
Const m_def_SelLength = 0
Const m_def_SelText = ""

' Subclassing constants
Const WM_NULL = &H0
Const WM_CREATE = &H1
Const WM_DESTROY = &H2
Const WM_MOVE = &H3
Const WM_SIZE = &H5
Const WM_ACTIVATE = &H6
Const WM_SETFOCUS = &H7
Const WM_KILLFOCUS = &H8
Const WM_ENABLE = &HA
Const WM_SETREDRAW = &HB
Const WM_SETTEXT = &HC
Const WM_GETTEXT = &HD
Const WM_GETTEXTLENGTH = &HE
Const WM_PAINT = &HF
Const WM_CLOSE = &H10
Const WM_QUERYENDSESSION = &H11
Const WM_QUIT = &H12
Const EM_SETSEL = 177
Const WM_QUERYOPEN = &H13
Const WM_ERASEBKGND = &H14
Const WM_SYSCOLORCHANGE = &H15
Const WM_ENDSESSION = &H16
Const WM_SHOWWINDOW = &H18
Const WM_SETTINGCHANGE = &H1A
Const WM_DEVMODECHANGE = &H1B
Const WM_ACTIVATEAPP = &H1C
Const WM_FONTCHANGE = &H1D
Const WM_TIMECHANGE = &H1E
Const WM_CANCELMODE = &H1F
Const WM_SETCURSOR = &H20
Const WM_MOUSEACTIVATE = &H21
Const WM_CHILDACTIVATE = &H22
Const WM_QUEUESYNC = &H23
Const WM_GETMINMAXINFO = &H24
Const WM_PAINTICON = &H26
Const WM_ICONERASEBKGND = &H27
Const WM_NEXTDLGCTL = &H28
Const WM_SPOOLERSTATUS = &H2A
Const WM_DRAWITEM = &H2B
Const WM_MEASUREITEM = &H2C
Const WM_DELETEITEM = &H2D
Const WM_VKEYTOITEM = &H2E
Const WM_CHARTOITEM = &H2F
Const WM_SETFONT = &H30
Const WM_GETFONT = &H31
Const WM_SETHOTKEY = &H32
Const WM_GETHOTKEY = &H33
Const WM_QUERYDRAGICON = &H37
Const WM_COMPAREITEM = &H39
Const WM_COMPACTING = &H41
Const WM_WINDOWPOSCHANGING = &H46
Const WM_WINDOWPOSCHANGED = &H47
Const WM_POWER = &H48
Const WM_COPYDATA = &H4A
Const WM_CANCELJOURNAL = &H4B
Const WM_NCCREATE = &H81
Const WM_NCDESTROY = &H82
Const WM_NCCALCSIZE = &H83
Const WM_NCHITTEST = &H84
Const WM_NCPAINT = &H85
Const WM_NCACTIVATE = &H86
Const WM_GETDLGCODE = &H87
Const WM_NCMOUSEMOVE = &HA0
Const WM_NCLBUTTONDOWN = &HA1
Const WM_NCLBUTTONUP = &HA2
Const WM_NCLBUTTONDBLCLK = &HA3
Const WM_NCRBUTTONDOWN = &HA4
Const WM_NCRBUTTONUP = &HA5
Const WM_NCRBUTTONDBLCLK = &HA6
Const WM_NCMBUTTONDOWN = &HA7
Const WM_NCMBUTTONUP = &HA8
Const WM_NCMBUTTONDBLCLK = &HA9
Const WM_KEYFIRST = &H100
Const WM_KEYDOWN = &H100
Const WM_KEYUP = &H101
Const WM_CHAR = &H102
Const WM_DEADCHAR = &H103
Const WM_SYSKEYDOWN = &H104
Const WM_SYSKEYUP = &H105
Const WM_SYSCHAR = &H106
Const WM_SYSDEADCHAR = &H107
Const WM_KEYLAST = &H108
Const WM_INITDIALOG = &H110
Const WM_COMMAND = &H111
Const WM_SYSCOMMAND = &H112
Const WM_TIMER = &H113
Const WM_HSCROLL = &H114
Const WM_VSCROLL = &H115
Const WM_INITMENU = &H116
Const WM_INITMENUPOPUP = &H117
Const WM_MENUSELECT = &H11F
Const WM_MENUCHAR = &H120
Const WM_ENTERIDLE = &H121
Const WM_CTLCOLORMSGBOX = &H132
Const WM_CTLCOLOREDIT = &H133
Const WM_CTLCOLORLISTBOX = &H134
Const WM_CTLCOLORBTN = &H135
Const WM_CTLCOLORDLG = &H136
Const WM_CTLCOLORSCROLLBAR = &H137
Const WM_CTLCOLORSTATIC = &H138
Const WM_MOUSEFIRST = &H200
Const WM_MOUSEMOVE = &H200
Const WM_LBUTTONDOWN = &H201
Const WM_LBUTTONUP = &H202
Const WM_LBUTTONDBLCLK = &H203
Const WM_RBUTTONDOWN = &H204
Const WM_RBUTTONUP = &H205
Const WM_RBUTTONDBLCLK = &H206
Const WM_MBUTTONDOWN = &H207
Const WM_MBUTTONUP = &H208
Const WM_MBUTTONDBLCLK = &H209
Const WM_MOUSELAST = &H209
Const WM_PARENTNOTIFY = &H210
Const WM_ENTERMENULOOP = &H211
Const WM_EXITMENULOOP = &H212
Const WM_MDICREATE = &H220
Const WM_MDIDESTROY = &H221
Const WM_MDIACTIVATE = &H222
Const WM_MDIRESTORE = &H223
Const WM_MDINEXT = &H224
Const WM_MDIMAXIMIZE = &H225
Const WM_MDITILE = &H226
Const WM_MDICASCADE = &H227
Const WM_MDIICONARRANGE = &H228
Const WM_MDIGETACTIVE = &H229
Const WM_MDISETMENU = &H230
Const WM_DROPFILES = &H233
Const WM_MDIREFRESHMENU = &H234
Const WM_CUT = &H300
Const WM_COPY = &H301
Const WM_PASTE = &H302
Const WM_CLEAR = &H303
Const WM_UNDO = &H304
Const WM_RENDERFORMAT = &H305
Const WM_RENDERALLFORMATS = &H306
Const WM_DESTROYCLIPBOARD = &H307
Const WM_DRAWCLIPBOARD = &H308
Const WM_PAINTCLIPBOARD = &H309
Const WM_VSCROLLCLIPBOARD = &H30A
Const WM_SIZECLIPBOARD = &H30B
Const WM_ASKCBFORMATNAME = &H30C
Const WM_CHANGECBCHAIN = &H30D
Const WM_HSCROLLCLIPBOARD = &H30E
Const WM_QUERYNEWPALETTE = &H30F
Const WM_PALETTEISCHANGING = &H310
Const WM_PALETTECHANGED = &H311
Const WM_HOTKEY = &H312
Const WM_PENWINFIRST = &H380
Const WM_PENWINLAST = &H38F
Const WM_USER = &H400

' SendMessage RTB constants
Const EM_GETLINE = &HC4
Const EM_GETLINECOUNT = &HBA
Const EM_LINELENGTH = &HC1
Const EM_LINEINDEX = &HBB
Const EM_LINEFROMCHAR = &HC9
Const EM_GETFIRSTVISIBLELINE = &HCE

'Property Variables:
Dim m_ColourStrings         As OLE_COLOR
Dim m_ColourOperator        As OLE_COLOR
Dim m_ColourKeyWord         As OLE_COLOR
Dim m_ColourComment         As OLE_COLOR
Dim m_ProcessStrings        As Boolean
Dim m_ItalicComments        As Boolean
Dim m_BoldSelectedKeyWords  As Boolean
Dim m_WordWrap              As Boolean
Dim m_LineNumbers           As Boolean
Dim m_SelStart              As Long
Dim m_SelLength             As Long
Dim m_SelText               As String
Dim m_ceBoldWords           As String
Dim m_ceOperators           As String
Dim m_ceKeyWords            As String
Dim m_NormaliseCase         As Boolean
Dim m_ForeColor             As Long
Dim m_BackStyle             As Integer
Dim m_SyntaxColouring       As Boolean
Dim bDirty                  As Boolean
Dim stexttmp                As String

'rgb Values for the long to rgb conversion
Dim RGBRed1                 As Long
Dim RGBBlue1                As Long
Dim RGBGreen1               As Long
Dim RGBRed2                 As Long
Dim RGBBlue2                As Long
Dim RGBGreen2               As Long
Dim RGBRed3                 As Long
Dim RGBBlue3                As Long
Dim RGBGreen3               As Long
Dim RGBRed4                 As Long
Dim RGBBlue4                As Long
Dim RGBGreen4               As Long
Dim RGBRed5                 As Long
Dim RGBBlue5                As Long
Dim RGBGreen5               As Long

' other private variables
Private RaiseEvents         As Boolean
Private lLineTracker        As Long
Private mWndProcOrg         As Long
Private mHWndSubClassed     As Long
Private bScrolling          As Boolean

'Event Declarations:
Event VScroll()
Event HScroll()
Event Change() 'MappingInfo=RTB,RTB,-1,Change
Event SelChange() 'MappingInfo=RTB,RTB,-1,SelChange
Event Click() 'MappingInfo=RTB,RTB,-1,Click
Event DblClick() 'MappingInfo=RTB,RTB,-1,DblClick
Event KeyDown(KeyCode As Integer, Shift As Integer) 'MappingInfo=RTB,RTB,-1,KeyDown
Event KeyPress(KeyAscii As Integer) 'MappingInfo=RTB,RTB,-1,KeyPress
Event KeyUp(KeyCode As Integer, Shift As Integer) 'MappingInfo=RTB,RTB,-1,KeyUp
Event MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single) 'MappingInfo=RTB,RTB,-1,MouseDown
Event MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single) 'MappingInfo=RTB,RTB,-1,MouseMove
Event MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single) 'MappingInfo=RTB,RTB,-1,MouseUp

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,BackColor
Public Property Get BackColor() As OLE_COLOR
    BackColor = RTB.BackColor
End Property

Public Property Let BackColor(ByVal New_BackColor As OLE_COLOR)
    RTB.BackColor() = New_BackColor
    PropertyChanged "BackColor"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=8,0,0,0
Public Property Get ForeColor() As Long
    ForeColor = m_ForeColor
End Property

Public Property Let ForeColor(ByVal New_ForeColor As Long)
    m_ForeColor = New_ForeColor
    PropertyChanged "ForeColor"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,Enabled
Public Property Get Enabled() As Boolean
    Enabled = RTB.Enabled
End Property

Public Property Let Enabled(ByVal New_Enabled As Boolean)
    RTB.Enabled() = New_Enabled
    PropertyChanged "Enabled"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,Font
Public Property Get Font() As Font
    Set Font = RTB.Font
End Property

Public Property Set Font(ByVal New_Font As Font)
    Set RTB.Font = New_Font
    PropertyChanged "Font"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=7,0,0,0
Public Property Get BackStyle() As Integer
    BackStyle = m_BackStyle
End Property

Public Property Let BackStyle(ByVal New_BackStyle As Integer)
    m_BackStyle = New_BackStyle
    PropertyChanged "BackStyle"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,BorderStyle
Public Property Get BorderStyle() As BorderStyleConstants
    BorderStyle = RTB.BorderStyle
End Property

Public Sub Undo()
SendMessage RTB.hwnd, WM_UNDO, 0, ByVal 0
End Sub

Public Sub CopyM()
SendMessage RTB.hwnd, WM_COPY, 0, ByVal 0
End Sub

Public Sub Cut()
SendMessage RTB.hwnd, WM_CUT, 0, ByVal 0
End Sub

Public Sub Paste()
SendMessage RTB.hwnd, WM_PASTE, 0, ByVal 0
End Sub

Public Sub Delete()
SendMessage RTB.hwnd, WM_CLEAR, 0, ByVal 0
End Sub

Public Sub SelAll()
SendMessage RTB.hwnd, EM_SETSEL, 0, ByVal -1
End Sub

Public Property Let BorderStyle(ByVal New_BorderStyle As BorderStyleConstants)
    RTB.BorderStyle() = New_BorderStyle
    PropertyChanged "BorderStyle"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,Refresh
Public Sub Refresh()
    RTB.Refresh
End Sub


Private Sub picLineNumbers_Resize()
    linNumbers.Y2 = picLineNumbers.Height
End Sub

Private Sub RTB_Click()
   
    RaiseEvent Click
    
End Sub

Private Sub RTB_DblClick()

    RaiseEvent DblClick

End Sub

Public Sub RTB_KeyDown(KeyCode As Integer, Shift As Integer)

' Original code by ChiefRedBull from www.VisualBasicForum.com

On Error Resume Next

Dim lCursor             As Long
Dim lSelectLen          As Long
Dim lStart              As Long
Dim lFinish             As Long
Dim lLocalTracker       As Long

    lLocalTracker = CurrentLineNumber

    Call WriteLineNumbers

    ' check for Ctrl+Y, this is the delete line shortcut
    If KeyCode = vbKeyY And Shift = 2 Then
        ' delete the current line...
        DeleteCurrentLine
        ' null the keypress to get rid of any 'Y' characters..
        KeyCode = 0
    End If
    
    ' to handle delete being pressed...
    If KeyCode = 8 Then
        If lLocalTracker <> lLineTracker Then
            lLineTracker = CurrentLineNumber
            bDirty = False
        End If
    End If
    
    ' reset the line tracker after the del check
    lLineTracker = CurrentLineNumber
    
    If KeyCode = vbKeyTab Then
        'RTB.SelText = vbTab
    End If
    
    ' check for text being pasted into the box
    ' with Ctrl-V.. we also call the same sub when a WM_Paste message
    ' has been send to the control...
    If KeyCode = vbKeyV And Shift = 2 Then
         Call DoPaste
         ' null the keypress so we don't get any 'V' characters
         KeyCode = 0
    End If
    
    If KeyCode = 13 Or _
         KeyCode = vbKeyUp Or _
            KeyCode = vbKeyDown Or _
               KeyCode = 33 Or KeyCode = 34 Then
    
        ' only color this line if it's been changed
        If bDirty Or KeyCode = 13 And Shift <> 2 Then
        
            ' store the current cursor pos
            ' and current selection if there is any
            lCursor = RTB.SelStart
            lSelectLen = RTB.SelLength
            
            ' sure we need to colour the line.. but lets reset its colour first
            ' to be sure we don't screw the colours up..
            Call ResetColours(CurrentLineNumber - 1)
            
            ' lock the window and lets colour the line
            LockWindowUpdate RTB.hwnd
            
            lStart = CurrentLineNumber - 1
            lFinish = CurrentLineNumber - 1
            
            ColourSelection lStart, lFinish
            
            ' reset the properties
            RTB.SelStart = lCursor
            RTB.SelLength = lSelectLen
            RTB.SelColor = vbBlack
            RTB.SelBold = False
            RTB.SelItalic = False
            
            ' reset the flag and release the window
            bDirty = False
            LockWindowUpdate 0&
            
        End If
        
    ElseIf Not IsControlKey(KeyCode) And Shift <> 2 Then
        
        ' this section resets the current lines colour to black
        ' once we are finished, then the above section re-colours the line..
        If bDirty = False Then
            ' reset the colours for this line only!
            Call ResetColours(CurrentLineNumber - 1)
            bDirty = True
        End If
                
    End If
    
    RaiseEvent KeyDown(KeyCode, Shift)
    
End Sub

Private Sub RTB_KeyPress(KeyAscii As Integer)
    
    ' don't reset colours on ctrl-c
    If KeyAscii <> 3 Then
        RTB.SelColor = vbBlack
        RTB.SelBold = False
        RTB.SelItalic = False
    End If

    RaiseEvent KeyPress(KeyAscii)

End Sub

Private Sub RTB_KeyUp(KeyCode As Integer, Shift As Integer)
    Call WriteLineNumbers
    RaiseEvent KeyUp(KeyCode, Shift)
End Sub

Private Sub RTB_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    Call WriteLineNumbers
    If RTB.SelColor = vbRed Then
        Me.RTB_KeyDown 13, 0
    End If
    Call WriteLineNumbers
    RaiseEvent MouseDown(Button, Shift, X, Y)
End Sub

Private Sub RTB_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    RaiseEvent MouseMove(Button, Shift, X, Y)
End Sub

Private Sub RTB_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    RaiseEvent MouseUp(Button, Shift, X, Y)
End Sub

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,True
Public Property Get SyntaxColouring() As Boolean
    SyntaxColouring = m_SyntaxColouring
End Property

Public Property Let SyntaxColouring(ByVal New_SyntaxColouring As Boolean)
    m_SyntaxColouring = New_SyntaxColouring
    PropertyChanged "SyntaxColouring"
End Property


Private Sub Timer1_Timer()

End Sub

Private Sub UserControl_Initialize()
    RaiseEvents = True

    subclassControl RTB
End Sub

'Initialize Properties for User Control
Private Sub UserControl_InitProperties()
    m_ForeColor = m_def_ForeColor
    m_BackStyle = m_def_BackStyle
    m_SyntaxColouring = m_def_SyntaxColouring
    bDirty = True
    m_NormaliseCase = m_def_NormaliseCase
    m_ceBoldWords = m_def_ceBoldWords
    m_ceOperators = m_def_ceOperators
    m_ceKeyWords = m_def_ceKeyWords
    m_SelStart = m_def_SelStart
    m_SelLength = m_def_SelLength
    m_SelText = m_def_SelText
    m_LineNumbers = m_def_LineNumbers
    m_WordWrap = m_def_WordWrap
    m_BoldSelectedKeyWords = m_def_BoldSelectedKeyWords
    m_ItalicComments = m_def_ItalicComments
    m_ProcessStrings = m_def_ProcessStrings
    m_ColourOperator = m_def_ColourOperator
    m_ColourKeyWord = m_def_ColourKeyWord
    m_ColourComment = m_def_ColourComment
    m_ColourStrings = m_def_ColourStrings
End Sub

'Load property Values from storage
Private Sub UserControl_ReadProperties(PropBag As PropertyBag)

    RTB.BackColor = PropBag.ReadProperty("BackColor", &H80000005)
    m_ForeColor = PropBag.ReadProperty("ForeColor", m_def_ForeColor)
    RTB.Enabled = PropBag.ReadProperty("Enabled", True)
    Set RTB.Font = PropBag.ReadProperty("Font", Ambient.Font)
    m_BackStyle = PropBag.ReadProperty("BackStyle", m_def_BackStyle)
    RTB.BorderStyle = PropBag.ReadProperty("BorderStyle", 0)
    m_SyntaxColouring = PropBag.ReadProperty("SyntaxColouring", m_def_SyntaxColouring)
    RTB.Text = PropBag.ReadProperty("Text", "")
    m_NormaliseCase = PropBag.ReadProperty("NormaliseCase", m_def_NormaliseCase)
    m_ceBoldWords = PropBag.ReadProperty("ceBoldWords", m_def_ceBoldWords)
    m_ceOperators = PropBag.ReadProperty("ceOperators", m_def_ceOperators)
    m_ceKeyWords = PropBag.ReadProperty("ceKeyWords", m_def_ceKeyWords)
    m_SelStart = PropBag.ReadProperty("SelStart", m_def_SelStart)
    m_SelLength = PropBag.ReadProperty("SelLength", m_def_SelLength)
    m_SelText = PropBag.ReadProperty("SelText", m_def_SelText)
    m_LineNumbers = PropBag.ReadProperty("LineNumbers", m_def_LineNumbers)
    m_WordWrap = PropBag.ReadProperty("WordWrap", m_def_WordWrap)
    RTB.HideSelection = PropBag.ReadProperty("HideSelection", False)
    m_BoldSelectedKeyWords = PropBag.ReadProperty("BoldSelectedKeyWords", m_def_BoldSelectedKeyWords)
    m_ItalicComments = PropBag.ReadProperty("ItalicComments", m_def_ItalicComments)
    m_ProcessStrings = PropBag.ReadProperty("ProcessStrings", m_def_ProcessStrings)
    m_ColourOperator = PropBag.ReadProperty("ColourOperator", m_def_ColourOperator)
    m_ColourKeyWord = PropBag.ReadProperty("ColourKeyWord", m_def_ColourKeyWord)
    m_ColourComment = PropBag.ReadProperty("ColourComment", m_def_ColourComment)
    m_ColourStrings = PropBag.ReadProperty("ColourStrings", m_def_ColourStrings)

    picLineNumbers.Visible = m_LineNumbers
    Call UserControl_Resize
    
    ' split the long Values to rgb sub vals
    SplitRGB m_ColourStrings, RGBRed4, RGBGreen4, RGBBlue4
    SplitRGB m_ColourOperator, RGBRed2, RGBGreen2, RGBBlue2
    SplitRGB m_ColourKeyWord, RGBRed1, RGBGreen1, RGBBlue1
    SplitRGB m_ColourComment, RGBRed5, RGBGreen5, RGBBlue5

End Sub

Private Sub UserControl_Resize()
On Error Resume Next
    With RTB
        
        .Height = UserControl.ScaleHeight
        .Top = UserControl.ScaleTop
        If m_LineNumbers = True Then
            .Left = UserControl.ScaleLeft + picLineNumbers.ScaleWidth
        Else
            .Left = UserControl.ScaleLeft
        End If
        If m_LineNumbers = True Then
            .Width = UserControl.ScaleWidth - picLineNumbers.Width
        Else
            .Width = UserControl.ScaleWidth
        End If
        
    End With
    
    With shp4RTB
        .Left = 0
        .Top = 0
        .Width = RTB.Left + RTB.Width + 10
        .Height = RTB.Height + 20
    End With
    
    With picLineNumbers
    
        .Height = UserControl.ScaleHeight - 50
        .Top = UserControl.ScaleTop + 10
        .Left = UserControl.ScaleLeft + 10
    
    End With
    
    'Call WriteLineNumbers

End Sub

Public Sub RemoveBorder()
    On Error Resume Next
    RTB.BorderStyle = 0
    RTB.Appearance = 0
End Sub

'Write property Values to storage
Private Sub UserControl_WriteProperties(PropBag As PropertyBag)

    Call PropBag.WriteProperty("BackColor", RTB.BackColor, &H80000005)
    Call PropBag.WriteProperty("ForeColor", m_ForeColor, m_def_ForeColor)
    Call PropBag.WriteProperty("Enabled", RTB.Enabled, True)
    Call PropBag.WriteProperty("Font", RTB.Font, Ambient.Font)
    Call PropBag.WriteProperty("BackStyle", m_BackStyle, m_def_BackStyle)
    Call PropBag.WriteProperty("BorderStyle", RTB.BorderStyle, 0)
    Call PropBag.WriteProperty("SyntaxColouring", m_SyntaxColouring, m_def_SyntaxColouring)
    Call PropBag.WriteProperty("Text", RTB.Text, "")
    Call PropBag.WriteProperty("NormaliseCase", m_NormaliseCase, m_def_NormaliseCase)
    Call PropBag.WriteProperty("ceBoldWords", m_ceBoldWords, m_def_ceBoldWords)
    Call PropBag.WriteProperty("ceOperators", m_ceOperators, m_def_ceOperators)
    Call PropBag.WriteProperty("ceKeyWords", m_ceKeyWords, m_def_ceKeyWords)
    Call PropBag.WriteProperty("SelStart", m_SelStart, m_def_SelStart)
    Call PropBag.WriteProperty("SelLength", m_SelLength, m_def_SelLength)
    Call PropBag.WriteProperty("SelText", m_SelText, m_def_SelText)
    Call PropBag.WriteProperty("LineNumbers", m_LineNumbers, m_def_LineNumbers)
    Call PropBag.WriteProperty("WordWrap", m_WordWrap, m_def_WordWrap)
    Call PropBag.WriteProperty("HideSelection", RTB.HideSelection, False)
    Call PropBag.WriteProperty("BoldSelectedKeyWords", m_BoldSelectedKeyWords, m_def_BoldSelectedKeyWords)
    Call PropBag.WriteProperty("ItalicComments", m_ItalicComments, m_def_ItalicComments)
    Call PropBag.WriteProperty("ProcessStrings", m_ProcessStrings, m_def_ProcessStrings)
    Call PropBag.WriteProperty("ColourOperator", m_ColourOperator, m_def_ColourOperator)
    Call PropBag.WriteProperty("ColourKeyWord", m_ColourKeyWord, m_def_ColourKeyWord)
    Call PropBag.WriteProperty("ColourComment", m_ColourComment, m_def_ColourComment)
    Call PropBag.WriteProperty("ColourStrings", m_ColourStrings, m_def_ColourStrings)

    picLineNumbers.Visible = m_LineNumbers
    Call UserControl_Resize
        
    ' split the long Values to rgb sub vals
    SplitRGB m_ColourStrings, RGBRed4, RGBGreen4, RGBBlue4
    SplitRGB m_ColourOperator, RGBRed2, RGBGreen2, RGBBlue2
    SplitRGB m_ColourKeyWord, RGBRed1, RGBGreen1, RGBBlue1
    SplitRGB m_ColourComment, RGBRed5, RGBGreen5, RGBBlue5

End Sub


Private Sub RTB_Change()
    If RaiseEvents Then
        'Call WriteLineNumbers
        RaiseEvent Change
    End If
End Sub

Private Sub RTB_SelChange()
    If RaiseEvents Then
        'Call WriteLineNumbers
        RaiseEvent SelChange
    End If
End Sub

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,Text
Public Property Get Text() As String
    Text = RTB.Text
End Property

Public Property Let Text(ByVal New_Text As String)
    RTB.Text() = New_Text
    PropertyChanged "Text"
End Property
'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,True
Public Property Get NormaliseCase() As Boolean
    NormaliseCase = m_NormaliseCase
End Property

Public Property Let NormaliseCase(ByVal New_NormaliseCase As Boolean)
    m_NormaliseCase = New_NormaliseCase
    PropertyChanged "NormaliseCase"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=13,0,0,0
Public Property Get ceBoldWords() As String
    ceBoldWords = m_ceBoldWords
End Property

Public Property Let ceBoldWords(ByVal New_ceBoldWords As String)
    m_ceBoldWords = New_ceBoldWords
    PropertyChanged "ceBoldWords"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=13,0,0,0
Public Property Get ceOperators() As String
    ceOperators = m_ceOperators
End Property

Public Property Let ceOperators(ByVal New_ceOperators As String)
    m_ceOperators = New_ceOperators
    PropertyChanged "ceOperators"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=13,0,0,0
Public Property Get ceKeyWords() As String
    ceKeyWords = m_ceKeyWords
End Property

Public Property Let ceKeyWords(ByVal New_ceKeyWords As String)
    m_ceKeyWords = New_ceKeyWords
    PropertyChanged "ceKeyWords"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=8,0,2,0
Public Property Get SelStart() As Long
    SelStart = m_SelStart
End Property

Public Property Let SelStart(ByVal New_SelStart As Long)
    If Ambient.UserMode = False Then Err.Raise 387
    m_SelStart = New_SelStart
    PropertyChanged "SelStart"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=8,0,2,0
Public Property Get SelLength() As Long
    SelLength = m_SelLength
End Property

Public Property Let SelLength(ByVal New_SelLength As Long)
    If Ambient.UserMode = False Then Err.Raise 387
    m_SelLength = New_SelLength
    PropertyChanged "SelLength"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=13,0,2,0
Public Property Get SelText() As String
    SelText = m_SelText
End Property

Public Property Let SelText(ByVal New_SelText As String)
    If Ambient.UserMode = False Then Err.Raise 387
    m_SelText = New_SelText
    PropertyChanged "SelText"
End Property

Public Sub InsertString(InsertString As String)

    RTB.SelText = InsertString

End Sub

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,False
Public Property Get LineNumbers() As Boolean
    LineNumbers = m_LineNumbers
End Property

Public Property Let LineNumbers(ByVal New_LineNumbers As Boolean)
    m_LineNumbers = New_LineNumbers
    PropertyChanged "LineNumbers"
    picLineNumbers.Visible = m_LineNumbers
    Call UserControl_Resize
End Property
'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,False
Public Property Get WordWrap() As Boolean
    WordWrap = m_WordWrap
End Property

Public Property Let WordWrap(ByVal New_WordWrap As Boolean)
    m_WordWrap = New_WordWrap
    PropertyChanged "WordWrap"
    If m_WordWrap = True Then
        RTB.RightMargin = RTB.Width - 250
    Else
        RTB.RightMargin = 999999
    End If
    'Call WriteLineNumbers
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=RTB,RTB,-1,HideSelection
Public Property Get HideSelection() As Boolean
    HideSelection = RTB.HideSelection
End Property

Public Property Let HideSelection(ByVal New_HideSelection As Boolean)
    RTB.HideSelection() = New_HideSelection
    PropertyChanged "HideSelection"
End Property
'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,False
Public Property Get BoldSelectedKeyWords() As Boolean
    BoldSelectedKeyWords = m_BoldSelectedKeyWords
End Property

Public Property Let BoldSelectedKeyWords(ByVal New_BoldSelectedKeyWords As Boolean)
    m_BoldSelectedKeyWords = New_BoldSelectedKeyWords
    PropertyChanged "BoldSelectedKeyWords"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,True
Public Property Get ItalicComments() As Boolean
    ItalicComments = m_ItalicComments
End Property

Public Property Let ItalicComments(ByVal New_ItalicComments As Boolean)
    m_ItalicComments = New_ItalicComments
    PropertyChanged "ItalicComments"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=0,0,0,True
Public Property Get ProcessStrings() As Boolean
    ProcessStrings = m_ProcessStrings
End Property

Public Property Let ProcessStrings(ByVal New_ProcessStrings As Boolean)
    m_ProcessStrings = New_ProcessStrings
    PropertyChanged "ProcessStrings"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=10,0,0,0
Public Property Get ColourOperator() As OLE_COLOR
    ColourOperator = m_ColourOperator
End Property

Public Property Let ColourOperator(ByVal New_ColourOperator As OLE_COLOR)
    m_ColourOperator = New_ColourOperator
    PropertyChanged "ColourOperator"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=10,0,0,0
Public Property Get ColourKeyWord() As OLE_COLOR
    ColourKeyWord = m_ColourKeyWord
End Property

Public Property Let ColourKeyWord(ByVal New_ColourKeyWord As OLE_COLOR)
    m_ColourKeyWord = New_ColourKeyWord
    PropertyChanged "ColourKeyWord"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=10,0,0,0
Public Property Get ColourComment() As OLE_COLOR
    ColourComment = m_ColourComment
End Property

Public Property Let ColourComment(ByVal New_ColourComment As OLE_COLOR)
    m_ColourComment = New_ColourComment
    PropertyChanged "ColourComment"
End Property

'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MemberInfo=10,0,0,0
Public Property Get ColourStrings() As OLE_COLOR
    ColourStrings = m_ColourStrings
End Property

Public Property Let ColourStrings(ByVal New_ColourStrings As OLE_COLOR)
    m_ColourStrings = New_ColourStrings
    PropertyChanged "ColourStrings"
End Property
'
'WARNING! DO NOT REMOVE OR MODIFY THE FOLLOWING COMMENTED LINES!
'MappingInfo=UserControl,UserControl,-1,hWnd
Public Property Get hwnd() As Long
    hwnd = UserControl.hwnd
End Property

Public Sub ColourSelection(lStartLine As Long, lEndLine As Long)

' go thru the rtb line by line, instead of the traditional way of selecting
' each keyword individually, we will select the entire line, then write
' back to the SelRTF property..

' this does not need to be 'as' fast as the ColourEntireRTB sub, but..
' still needs to be reasonable as this will process blocks of code
' for WM_Paste messages in the RTB..

' Karl Durrance, Dec 2002

' the lStartLine and lEndLine Values are zero based..

Dim X                       As Long
Dim i                       As Long
Dim lCurLineStart           As Long
Dim lCurLineEnd             As Long
Dim sLineText               As String
Dim sLineTextRTF            As String
Dim lnglength               As Long
Dim nQuoteEnd               As Long
Dim sCurrentWord            As String
Dim sChar                   As String
Dim nWordPos                As Long
Dim lColour                 As Long
Dim lLastBreak              As Long
Dim sBoldStart              As String
Dim sBoldEnd                As String
Dim bDone                   As Boolean
Dim lLineOffset             As Long
Dim lStartRTFCode           As Long
Dim stmpstring              As String

    With RTB

        For i = lStartLine To lEndLine
        
            ' get the details for this line
            lCurLineStart = SendMessage(.hwnd, EM_LINEINDEX, i, 0&)
            lnglength = SendMessage(.hwnd, EM_LINELENGTH, lCurLineStart, 0)
            
            ' if the line actually has some data in it then we'll process it..
            If lCurLineStart >= 0 And lnglength > 0 Then
            
                ' select the entire line
                .SelStart = lCurLineStart
                .SelLength = lnglength
                
                If lCurLineStart = 1 Then lCurLineStart = 0
                    
                    ' get the selected text.. assign to a variable
                    sLineText = .SelText
                    
                    ' fix up any rtf problems now.. like "\{}"..
                    If InStr(1, sLineText, "\") Or InStr(1, sLineText, "{") Or InStr(1, sLineText, "}") Then
                        sLineText = Replace$(sLineText, "\", "\\")
                        sLineText = Replace$(sLineText, "{", "\{")
                        sLineText = Replace$(sLineText, "}", "\}")
                        lnglength = Len(sLineText)
                    End If
                    
                    ' check for comment Identifier at the start of the line
                    If Left$(LTrim$(sLineText), 2) = "//" Then
                        ' colour the lines that are complete comments like this
                        ' beats messing around with the RTB codes..
                        ' there is no speed loss since the line is already selected..
                        .SelColor = m_ColourComment
                        If m_ItalicComments = True Then
                            .SelItalic = True
                        End If
                    Else
                        
                        lLastBreak = 1
                        For X = 1 To Len(sLineText)
                        
                            sChar = Mid$(sLineText, X, 1)
                            bDone = False
                            
                            Select Case sChar
                        
                                Case COMMENT_IDENTIFER
                                    
                                    ' write the colours now!
                                    If Len(sLineTextRTF) > 0 Then
                                    
                                        .SelRTF = "{{\colortbl;\red" & RGBRed1 & "\green" & _
                                                    RGBGreen1 & "\blue" & RGBBlue1 & ";\red" & RGBRed2 & _
                                                    "\green" & RGBGreen2 & "\blue" & RGBBlue2 & ";\red" & _
                                                    RGBRed3 & "\green" & RGBGreen3 & "\blue" & RGBBlue3 & _
                                                    ";\red" & RGBRed4 & "\green" & RGBGreen4 & "\blue" & _
                                                    RGBBlue4 & ";\red" & RGBRed5 & "\green" & RGBGreen5 _
                                                    & "\blue" & RGBBlue5 & ";}" & sLineTextRTF & "\I0\B0}\par"
                                    
                                    End If
                                
                                    ' comment, colour the rest of the line
                                    ' these can be done the slower way..
                                    ' with no real time loss..
                                    ' these are rarer than standard comments...
                                    .SelStart = lCurLineStart + X - 1
                                    .SelLength = (lnglength + 2) - X
                                    .SelColor = m_ColourComment
                                    If m_ItalicComments = True Then
                                        .SelItalic = True
                                    End If
                                    ' set the flag so we don't colour the line again
                                    bDone = True
                                    Exit For
                            
                                Case Chr(34)
                                
                                    ' Find the end and reset the for loop
                                    nQuoteEnd = InStr(X + 1, sLineText, Chr(34), vbBinaryCompare)
                                    If nQuoteEnd = 0 Then nQuoteEnd = Len(sLineText)
                                
                                    If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                    
                                    If m_ProcessStrings = True Then
                                    
                                        ' assign the colour codes to the string..
                                        stmpstring = "{\cf4" & Mid$(sLineText, X, (nQuoteEnd - X) + 1) & "\cf0}"
                                        sLineTextRTF = Replace$(sLineTextRTF, Mid$(sLineText, X, (nQuoteEnd - X) + 1), "{\cf4" & Mid$(sLineText, X, (nQuoteEnd - X) + 1) & "\cf0}")
                                        lLineOffset = lLineOffset + 10
                                    
                                    End If
                                    
                                    X = nQuoteEnd
                               
                                Case "a" To "z", "A" To "Z", "_"
                                    ' alphanumeric, non string or comment..
                                    sCurrentWord = sCurrentWord & sChar
                                    ' if we are at the end of a line with no vbCrLf then
                                    ' call the colour routine directly so we don't miss
                                    ' the last word in the line...
                                    If X = Len(sLineText) Then GoTo ColourWord
                                Case "0" To "9"
                                    If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                    sCurrentWord = sChar
                                    GoTo ColourNumber
                                Case "$"
                                    If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                    sCurrentWord = sChar
                                    GoTo ColourNumber
                                Case "-"
                                    If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                    sCurrentWord = sChar
                                    GoTo ColourNumber
                                Case Else
                                    ' should be a word sep char.. so we could have a word!
                                    
ColourWord:
                                    
                                    If sCurrentWord <> "" Then
                                    
                                        nWordPos = InStr(1, m_ceKeyWords & m_ceOperators, "*" & sCurrentWord & "*", vbTextCompare)
                                        
                                        If nWordPos > 0 Then
                                            ' this word is a keyword, set the colour
                                            If nWordPos > Len(m_ceKeyWords) Then
                                                lColour = 2
                                            Else
                                                lColour = 1
                                            End If
                                            
                                            ' check if we need to bold the word..
                                            If m_BoldSelectedKeyWords = True Then
                                                If InStr(1, m_ceBoldWords, "*" & sCurrentWord & "*", vbTextCompare) Then
                                                    sBoldStart = "\b1"
                                                    sBoldEnd = "\b0"
                                                Else
                                                    sBoldStart = ""
                                                    sBoldEnd = ""
                                                End If
                                            End If
                                            
                                            ' reset the case of the keyword if required...
                                            If m_NormaliseCase = True Then
                                                sCurrentWord = Mid$(m_ceKeyWords & m_ceOperators, InStr(1, LCase$(m_ceKeyWords & m_ceOperators), "*" & LCase$(sCurrentWord) & "*", vbBinaryCompare) + 1, Len(sCurrentWord))
                                            End If
                                            
                                            ' now colour the word with the rtf codes
                                            ' use the custom replaceword function, start at the last breakpoint
                                            ' only colour one copy of the word..
                                            If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                            sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & lColour & sBoldStart & sCurrentWord & sBoldEnd & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                            'assign the offset because of the RTF codes..
                                            lLineOffset = lLineOffset + 10 + IIf(Len(sBoldStart) > 0, 6, 0)
                                        GoTo ResetWord
                                        End If

ColourNumber:
                                        If IsNumeric(sCurrentWord) = True Or _
                                            sCurrentWord = "," Or _
                                            sCurrentWord = "$" Then
                                            If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                            sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & 5 & " " & sCurrentWord & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                            'assign the offset because of the RTF codes..
                                            lLineOffset = lLineOffset + 11 + IIf(Len(sBoldStart) > 0, 6, 0)
                                        End If
ColourComma:
                                        If sCurrentWord = "," Or _
                                           sCurrentWord = ";" Or _
                                           sCurrentWord = "<" Or _
                                           sCurrentWord = ">" Or _
                                           sCurrentWord = "=" Or _
                                           sCurrentWord = "!" Or _
                                           sCurrentWord = "@" Then
                                            If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                            sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & 2 & sCurrentWord & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                            'assign the offset because of the RTF codes..
                                            lLineOffset = lLineOffset + 11 + IIf(Len(sBoldStart) > 0, 6, 0)
                                        End If
ResetWord:
                            
                                        ' reset the word to nothing
                                        sCurrentWord = ""
                                    
                                    End If
                                    
                                    lLastBreak = X
                                    
                            End Select
                        
                        Next X
                        
                        If sLineTextRTF <> "" And bDone = False Then
                            
                            .SelRTF = "{{\colortbl;\red" & RGBRed1 & "\green" & _
                                        RGBGreen1 & "\blue" & RGBBlue1 & ";\red" & RGBRed2 & _
                                        "\green" & RGBGreen2 & "\blue" & RGBBlue2 & ";\red" & _
                                        RGBRed3 & "\green" & RGBGreen3 & "\blue" & RGBBlue3 & _
                                        ";\red" & RGBRed4 & "\green" & RGBGreen4 & "\blue" & _
                                        RGBBlue4 & ";\red" & RGBRed5 & "\green" & RGBGreen5 _
                                        & "\blue" & RGBBlue5 & ";}" & sLineTextRTF & "\I0\B0}\par"
                        
                        End If
                        
                        sLineTextRTF = ""
                        lLineOffset = 0
                    End If
            
            End If
        
        Next i

    End With

End Sub

Public Sub ColourEntireRTB()

' This is for an entire colour of the RTB.. like on load..
' this out performs the line by line methods because we process
' the entire script in memory..

' the structure is basically the same as the ColourSelection sub
' but we write to the TextRTF property at the end instead..
' and do all the line processing in memory

' this obviously clears the entire contents of the rtb..

' Karl Durrance Dec 2002

Dim X                       As Long
Dim i                       As Long
Dim lCurLineStart           As Long
Dim lCurLineEnd             As Long
Dim sLineText               As String
Dim sLineTextRTF            As String
Dim sAllTextRTF             As String
Dim lnglength               As Long
Dim nQuoteEnd               As Long
Dim sCurrentWord            As String
Dim sChar                   As String
Dim nWordPos                As Long
Dim lColour                 As Long
Dim lLastBreak              As Long
Dim sBoldStart              As String
Dim sBoldEnd                As String
Dim sItalicStart            As String
Dim sItalicEnd              As String
Dim bDone                   As Boolean
Dim lLineOffset             As Long
Dim lStartRTFCode           As Long
Dim stmpstring              As String
Dim sBuffer                 As String
Dim asBuffer()              As String
Dim bForce                  As Boolean
Dim objAllRTFString         As New cString
Dim objFinalConcat          As New cString
Dim sTextRTF                As String
Dim oldstart                As Long

    oldstart = RTB.SelStart
    With RTB

        If m_ItalicComments = True Then
            ' set the RTF italic code because we have it turned on..
            sItalicStart = "\I1"
            sItalicEnd = "\I0"
        End If

        sBuffer = .Text
        asBuffer = Split(sBuffer, vbCrLf)
        
        ' set the text buffer for the CString class..
        ' we'll set the size initially to triple the size of the script
        ' in plain text.. this is pretty big, but will speed up execution
        ' because memory won't need to be reallocated during load..
        ' we will release the extra memory at the end by resetting the buffer..
        objAllRTFString.SetBufferSize Len(sBuffer) * 3
        objFinalConcat.SetBufferSize Len(sBuffer) * 3

        For i = LBound(asBuffer) To UBound(asBuffer)
        
                ' get the selected text.. assign to a variable for readability
                sLineText = asBuffer(i)
                
                ' fix up any rtf problems now.. like "\{}"..
                If InStr(1, sLineText, "\") Or InStr(1, sLineText, "{") Or InStr(1, sLineText, "}") Then
                    sLineText = Replace$(sLineText, "\", "\\")
                    sLineText = Replace$(sLineText, "{", "\{")
                    sLineText = Replace$(sLineText, "}", "\}")
                End If
                
                ' check for comment Identifier at the start of the line
                If Left$(LTrim$(sLineText), 2) = "//" Then
                    sLineTextRTF = "{\cf5" & sItalicStart & sLineText & "\cf0" & sItalicEnd & "}"
                    objAllRTFString.Append sLineTextRTF & "\par" & vbCrLf
                    ' reset the variables now.. we are done for this line..
                    sLineTextRTF = ""
                    lLineOffset = 0
                Else
                    
                    lLastBreak = 1
                    For X = 1 To Len(sLineText)
                    
                        sChar = Mid$(sLineText, X, 1)
                        bDone = False
                        
                        Select Case sChar
                    
                            Case COMMENT_IDENTIFER
                                
                                If sLineTextRTF = "" Then sLineTextRTF = sLineText
                            
                                ' comment, colour the rest of the line
                                sLineTextRTF = Mid$(sLineTextRTF, 1, (X + lLineOffset) - 1) & "{\cf5" & sItalicStart & Mid$(sLineTextRTF, X + lLineOffset) & "\cf0" & sItalicEnd & "}"
                                Exit For
                        
                            Case Chr(34)
                            
                                ' Find the end and reset the for loop
                                nQuoteEnd = InStr(X + 1, sLineText, Chr(34), vbBinaryCompare)
                                If nQuoteEnd = 0 Then nQuoteEnd = Len(sLineText)
                            
                                If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                
                                If m_ProcessStrings = True Then
                                
                                    ' assign the colour codes to the string..
                                    stmpstring = "{\cf4" & Mid$(sLineText, X, (nQuoteEnd - X) + 1) & "\cf0}"
                                    sLineTextRTF = Replace$(sLineTextRTF, Mid$(sLineText, X, (nQuoteEnd - X) + 1), "{\cf4" & Mid$(sLineText, X, (nQuoteEnd - X) + 1) & "\cf0}")
                                    lLineOffset = lLineOffset + 10
                                
                                End If
                                
                                X = nQuoteEnd
                           
                            Case "a" To "z", "A" To "Z", "_"
                                ' alphanumeric, non string or comment..
                                sCurrentWord = sCurrentWord & sChar
                                ' if we are at the end of a line with no vbCrLf then
                                ' call the colour routine directly so we don't miss
                                ' the last word in the line...
                                If X = Len(sLineText) Then GoTo ColourWord
                            Case "0" To "9"
                                If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                sCurrentWord = sChar
                                GoTo ColourNumber
                            Case "$"
                                If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                sCurrentWord = sChar
                                GoTo ColourNumber
                            Case "-"
                                If Len(sCurrentWord) > 1 Then GoTo ColourWord
                                sCurrentWord = sChar
                                GoTo ColourNumber
                            'Case ",", ";", "<", ">", "=", "!", "@"
                            '    sCurrentWord = sChar
                            '    GoTo ColourComma
                            Case Else
                                ' should be a word sep char.. so we could have a word!

' this tag is basically to handle the last word on a line
' just incase it needs colouring we call the ColourWord tag directly..
ColourWord:
                                
                                If sCurrentWord <> "" Then
                                
                                    nWordPos = InStr(1, m_ceKeyWords & m_ceOperators, "*" & sCurrentWord & "*", vbTextCompare)
                                    
                                    
                                    If nWordPos > 0 Then
                                        ' this word is a keyword, set the colour
                                        
                                        
                                        If nWordPos > Len(m_ceKeyWords) Then
                                            lColour = 2
                                        Else
                                            lColour = 1
                                        End If
                                        
                                        ' check if we need to bold the word..
                                        If m_BoldSelectedKeyWords = True Then
                                            If InStr(1, m_ceBoldWords, "*" & sCurrentWord & "*", vbTextCompare) Then
                                                sBoldStart = "\b1"
                                                sBoldEnd = "\b0"
                                            Else
                                                sBoldStart = ""
                                                sBoldEnd = ""
                                            End If
                                        End If
                                        
                                        ' reset the case of the keyword if required...
                                        If m_NormaliseCase = True Then
                                            sCurrentWord = Mid$(m_ceKeyWords & m_ceOperators, InStr(1, LCase$(m_ceKeyWords & m_ceOperators), "*" & LCase$(sCurrentWord) & "*", vbBinaryCompare) + 1, Len(sCurrentWord))
                                        End If
                                        
                                        ' now colour the word with the rtf codes
                                        ' use the custom replaceword function, start at the last breakpoint
                                        ' only colour one copy of the word..
                                        If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                        sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & lColour & sBoldStart & sCurrentWord & sBoldEnd & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                        'assign the offset because of the RTF codes..
                                        lLineOffset = lLineOffset + 10 + IIf(Len(sBoldStart) > 0, 6, 0)
                                    
                                    End If
                                    
ColourNumber:
                                        If IsNumeric(sCurrentWord) = True Or _
                                            sCurrentWord = "," Or _
                                            sCurrentWord = "$" Then
                                            If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                            sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & 5 & " " & sCurrentWord & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                            'assign the offset because of the RTF codes..
                                            lLineOffset = lLineOffset + 11 + IIf(Len(sBoldStart) > 0, 6, 0)
                                        End If
ColourComma:
                                        If sCurrentWord = "," Or _
                                           sCurrentWord = ";" Or _
                                           sCurrentWord = "<" Or _
                                           sCurrentWord = ">" Or _
                                           sCurrentWord = "=" Or _
                                           sCurrentWord = "!" Or _
                                           sCurrentWord = "@" Then
                                            lColour = 1
                                            If sLineTextRTF = "" Then sLineTextRTF = sLineText
                                            sLineTextRTF = ReplaceFullWord$(sLineTextRTF, sCurrentWord, "{\cf" & 2 & " " & sCurrentWord & "\cf0}", lLastBreak + lLineOffset, 1, vbTextCompare)
                                            'assign the offset because of the RTF codes..
                                            lLineOffset = lLineOffset + 11 + IIf(Len(sBoldStart) > 0, 6, 0)
                                        End If
                                    ' reset the word to nothing
                                    sCurrentWord = ""
                                
                                End If
                                
                                lLastBreak = X
                                
                        End Select
                    
                    Next X
                    
                    If sLineTextRTF = "" Then sLineTextRTF = sLineText

                    ' for LARGE strings, concatenation is a pain..
                    ' so we will replace with the fast CString class
                    objAllRTFString.Append sLineTextRTF & "\par" & vbCrLf
                    
                    sLineTextRTF = ""
                    lLineOffset = 0
                    
                End If
            
        Next i
        
        sAllTextRTF = objAllRTFString.Value
        
        ' once again, use the faster CString class
        ' for BIG scripts, this can save up to a second!!
        
        objFinalConcat.Append "{{\colortbl;\red" & RGBRed1 & "\green" & RGBGreen1 & _
                            "\blue" & RGBBlue1 & ";\red" & RGBRed2 & "\green" & RGBGreen2 & "\blue" & _
                            RGBBlue2 & ";\red" & RGBRed3 & "\green" & RGBGreen3 & "\blue" & RGBBlue3 _
                            & ";\red" & RGBRed4 & "\green" & RGBGreen4 & "\blue" & RGBBlue4 & ";\red" _
                            & RGBRed5 & "\green" & RGBGreen5 & "\blue" & RGBBlue5 & ";}"
        
        objFinalConcat.Append sAllTextRTF
        objFinalConcat.Append "\I0\B0}\par"
        
        ' reset the buffer size to the amount of characters.
        objFinalConcat.SetBufferSize objFinalConcat.Length
        
        ' clear the buffer to release memory now..
        objAllRTFString.SetBufferSize 0, True
        
        sTextRTF = objFinalConcat.Value
        
        ' clear the buffer to release memory now..
        objFinalConcat.SetBufferSize 0, True
        
        ' we are finished...write the full set of RTF to the TextRTF property of the RTB!!
        .TextRTF = "" ' clear the rtb box of all contents before writing the the Value.
        .TextRTF = sTextRTF

    End With
    
    RTB.SelStart = oldstart
    Set objFinalConcat = Nothing
    Set objAllRTFString = Nothing
    
End Sub

Private Function ReplaceFullWord(Source As String, Find As String, ReplaceStr As String, _
    Optional ByVal Start As Long = 1, Optional Count As Long = -1, _
    Optional Compare As VbCompareMethod = vbBinaryCompare) As String

Dim findLen             As Long
Dim replaceLen          As Long
Dim Index               As Long
Dim counter             As Long
Dim charcode            As Long
Dim replaceIt           As Boolean
    
    findLen = Len(Find)
    replaceLen = Len(ReplaceStr)
    
    ' this prevents an endless loop
    If findLen = 0 Then Err.Raise 5
    
    If Start < 1 Then Start = 1
    Index = Start
    
    ' let's start by assigning the Source to the result
    ReplaceFullWord = Source
    
    Do
        Index = InStr(Index, ReplaceFullWord, Find, Compare)
        If Index = 0 Then Exit Do
        
        replaceIt = False
        ' check that it is preceded by a punctuation symbol
        If Index > 1 Then
            charcode = Asc(UCase$(Mid$(ReplaceFullWord, Index - 1, 1)))
        Else
            charcode = 32
        End If
        If charcode < 65 Or charcode > 90 Then
            ' check that it is followed by a punctuation symbol
            charcode = Asc(UCase$(Mid$(ReplaceFullWord, Index + Len(Find), _
                1)) & " ")
            If charcode < 65 Or charcode > 90 Then
                replaceIt = True
            End If
        End If
        
        If replaceIt Then
            ' do the replacement
            ReplaceFullWord = Left$(ReplaceFullWord, Index - 1) & ReplaceStr & Mid$ _
                (ReplaceFullWord, Index + findLen)
            ' skip over the string just added
            Index = Index + replaceLen
            ' increment the replacement counter
            counter = counter + 1
        Else
            ' skip over this false match
            Index = Index + findLen
        End If
        
        ' Note that the Loop Until test will always fail if Count = -1
    Loop Until counter = Count
    
End Function

Public Sub SelectCurrentLine()

Dim lStart      As Long
Dim lFinish     As Long

    ' get the line start and end
    lStart = SendMessage(RTB.hwnd, EM_LINEINDEX, CurrentLineNumber - 1, 0&)
    lFinish = SendMessage(RTB.hwnd, EM_LINELENGTH, lStart, 0)
    
    RTB.SelStart = lStart
    RTB.SelLength = lFinish
    
End Sub

Public Sub DeleteCurrentLine()

Dim lStart      As Long
Dim lFinish     As Long
    
    LockWindowUpdate RTB.hwnd
    
    ' select the entire line, then delete the text
    SelectCurrentLine
    RTB.SelText = ""
    
    ' take the risk.. delete the line with sendkeys.. YUK!
    RTB.SetFocus
    SendKeys "{DEL}", True
    
    LockWindowUpdate 0&
    
End Sub

Private Sub ResetColours(lLine As Long)

'lLine is zero based!

Dim lStart      As Long
Dim lFinish     As Long
Dim lCursor     As Long
Dim lSelectLen  As Long

    LockWindowUpdate RTB.hwnd
        
    ' get the line start and end
    lStart = SendMessage(RTB.hwnd, EM_LINEINDEX, lLine, 0&)
    lFinish = SendMessage(RTB.hwnd, EM_LINELENGTH, lStart, 0)
    
    lCursor = RTB.SelStart
    lSelectLen = RTB.SelLength
    
    RTB.SelStart = lStart
    RTB.SelLength = lFinish
    RTB.SelColor = vbBlack
    RTB.SelBold = False
    RTB.SelItalic = False
    
    RTB.SelStart = lCursor
    RTB.SelLength = lSelectLen
    
    LockWindowUpdate 0&

End Sub

Private Sub WriteLineNumbers()
Exit Sub
' write the line numbers in the picture box..
' nice and quick way with the Print method.., ie.. no fancy crap, this works nicely.
' only print from the bounds of the top of the page to the bottom.. this way it
' takes no time at all!!

Dim X           As Long
Dim lStart      As Long
Dim FontHeight  As Long
Dim lFinish     As Long
Dim colCount    As Long

    lStart = SendMessage(RTB.hwnd, EM_GETFIRSTVISIBLELINE, 0, 0) + 1
    
    picLineNumbers.Cls
    picLineNumbers.Font = RTB.Font.Name
    picLineNumbers.FontSize = RTB.Font.Size
    picLineNumbers.BackColor = &HC8D0D4
    picLineNumbers.ForeColor = vbWhite
    FontHeight = picLineNumbers.TextHeight("1")
    
    Exit Sub
    
    lFinish = (RTB.Height / FontHeight) + lStart
    If lFinish > LineCount Then lFinish = LineCount
    
    ' loop from the first visible line in the rtb to the end of the page
    For X = lStart To lFinish
        picLineNumbers.Print X & " "
    Next X
    
End Sub

Private Sub DoPaste()

' Original code by ChiefRedBull from www.VisualBasicForum.com

Dim lCursor         As Long
Dim lStart          As Long
Dim lFinish         As Long
Dim sText           As String

    Screen.MousePointer = vbHourglass

    lCursor = RTB.SelStart
    LockWindowUpdate RTB.hwnd
    sText = Clipboard.GetText
        
    ' the starting line is the line we are currently on..
    lStart = CurrentLineNumber - 1
    
    RTB.SelText = sText
    lFinish = RTB.GetLineFromChar(RTB.SelStart + RTB.SelLength)
    
    ColourSelection lStart, lFinish
    
    ' restore the original Values
    RTB.SelStart = lCursor + Len(sText)
    RTB.SelColor = vbBlack
    
    LockWindowUpdate 0&
    
    Screen.MousePointer = vbNormal
    RTB.Refresh

End Sub

Private Function IsControlKey(ByVal KeyCode As Long) As Boolean

' Code by ChiefRedBull from www.VisualBasicForum.com

    ' check if the key is a control key
    Select Case KeyCode
        Case vbKeyLeft, vbKeyRight, vbKeyHome, _
             vbKeyEnd, vbKeyPageUp, vbKeyPageDown, _
             vbKeyShift, vbKeyControl
            IsControlKey = True
        Case Else
            IsControlKey = False
    End Select
End Function

Public Sub LoadFile(sFilePath As String)
On Error Resume Next
' Original Code by ChiefRedBull from www.VisualBasicForum.com

Dim FileNum     As Long

    Screen.MousePointer = vbHourglass
    
    'lock the window update so we don't get flicker
    LockWindowUpdate RTB.hwnd
    RaiseEvents = False
    
    ' load the file
    FileNum = FreeFile
    Open sFilePath For Input As FileNum
        RTB.Text = Input(LOF(FileNum), FileNum)
    Close FileNum

    ' Call the colouring routine
    ' this is destructive!!!
    ColourEntireRTB
    
    ' reset the cursor postion to the top of the rtb
    RTB.SelStart = 0
    
    ' write the line numbers
    Call WriteLineNumbers
    
    ' update the controls view
    RaiseEvents = True
    LockWindowUpdate 0&
    
    Screen.MousePointer = vbNormal
    
End Sub

Public Function GetLine(lngLine As Long) As String

Dim sAllText    As String
Dim lngindex    As Long
Dim lnglength   As Long
Dim X           As Long
Dim stemp       As String
Dim sChar       As Long

    sAllText = RTB.Text

    'get the current lines text..
    lngindex = SendMessage(RTB.hwnd, EM_LINEINDEX, lngLine - 1, 0)
    lnglength = SendMessage(RTB.hwnd, EM_LINELENGTH, lngindex, 0) + 2
    
    stemp = Mid$(sAllText, lngindex + 1, lnglength)
    
    ' strip any line feed characters as they are going to stuff us up..
    For X = 1 To Len(stemp)
    
        sChar = Asc(Mid$(stemp, X, 1))
        
        If Not sChar = 10 And Not sChar = 13 Then
            GetLine = GetLine & Mid$(stemp, X, 1)
        End If
    
    Next X
    
    
End Function

Public Function CurrentWord() As String

' get the current word being typed from bound to bound.

Dim BreakChrs       As String
Dim sLineText       As String
Dim X               As Long
Dim lStart          As Long
Dim lLineStart      As Long

    sLineText = GetLine(CurrentLineNumber)
    lStart = CurrentColumnNumber

    ' set the break character criteria for the words..
    BreakChrs = " ,.()<>[]\|:;=/*-+" & _
                    Chr$(32) & _
                    Chr$(13) & _
                    Chr$(10) & _
                    Chr$(9) & _
                    Chr$(39)
    
    For X = lStart To 1 Step -1
    
        If InStr(1, BreakChrs, Mid$(sLineText, X, 1)) Then
            CurrentWord = Mid$(sLineText, X + 1, lStart - X)
            Exit For
        End If
        
    Next X
    
    If CurrentWord = "" Then CurrentWord = Mid$(sLineText, 1, lStart)
        
End Function

Public Function CurrentLineNumber() As Long

    ' return the current line number in the code window
    CurrentLineNumber = SendMessage(RTB.hwnd, EM_LINEFROMCHAR, ByVal -1, 0&) + 1

End Function

Public Function CurrentColumnNumber() As Long

Dim lCurLine As Long
    ' Current Line
    lCurLine = 1 + RTB.GetLineFromChar(RTB.SelStart)
    ' Column
    CurrentColumnNumber = SendMessage(RTB.hwnd, EM_LINEINDEX, ByVal lCurLine - 1, 0&)
    CurrentColumnNumber = (RTB.SelStart) - CurrentColumnNumber

End Function

Public Function LineCount() As Long

    ' return the total line count of the code window
    LineCount = SendMessage(RTB.hwnd, EM_GETLINECOUNT, 0, 0)

End Function

Public Function SaveFile(sFilePath As String)

    RTB.SaveFile sFilePath, rtfText
    
End Function

Private Sub SplitRGB(ByVal lColor As Long, _
                    ByRef lRed As Long, _
                    ByRef lGreen As Long, _
                    ByRef lBlue As Long)
    
    lRed = lColor And &HFF
    lGreen = (lColor And &HFF00&) \ &H100&
    lBlue = (lColor And &HFF0000) \ &H10000
    
End Sub

Friend Sub SubclassedMessage(ByVal uMsg As Long, ByVal wParam As Long, ByVal lParam As Long)

    ' SubClassing Sub constructed from example provided by Garrett Sever (The Hand)
    ' on www.VisualBasicForum.com

    ' this sub captures the messages and allows us to process them..
    
Dim lCurCursor      As Long
Dim lFirstLine      As Long

    On Local Error Resume Next
    
    If uMsg = WM_VSCROLL Then
        ' write the line numbers on the Vertical Scroll..
        Call WriteLineNumbers
        ' raise the custom scroll event
        RaiseEvent VScroll
    End If
    
    If uMsg = WM_HSCROLL Then
        ' raise the custom scroll event
        RaiseEvent HScroll
    End If
    
    ' now be basically need to capture the times we move off a line
    ' and its not coloured.. ie.. on click on the form, scroll etc..
    ' this will only call if the rtb has the dirty flag..
    If uMsg = WM_LBUTTONDOWN Or uMsg = WM_RBUTTONDOWN Or _
                uMsg = WM_VSCROLL Or uMsg = WM_HSCROLL Then
        
        If bDirty = True Then
        
            lCurCursor = RTB.SelStart
            LockWindowUpdate RTB.hwnd
            ' colour the dirty line now
            ColourSelection lLineTracker - 1, lLineTracker - 1
            LockWindowUpdate 0&
            ' reset the flag to false
            bDirty = False
            
            ' reset the caret pos to the place we clicked or left the cursor
            If lCurCursor > 0 Then
                RTB.SelStart = lCurCursor
            End If
            
        End If
        
    End If
    
    ' when text is being pasted into the control call DoPaste..
    ' not by ctrl-v, but by a msg being sent to the control by SendMessage..
    If uMsg = WM_PASTE Then
        Call DoPaste
    End If
    
End Sub

Public Function ErrorSelectLineByposition(SourcePosition As Long)
    On Error Resume Next
    If Mid$(Source, SourcePosition, 2) = vbCrLf Then SourcePosition = SourcePosition + 2
    RTB.SelLength = 0
    RTB.SelStart = SourcePosition
    RTB.Visible = False
    SelectCurrentLine
    RTB.SelColor = vbRed
    RTB.Visible = True
    RTB.SelLength = 0
    RTB.SelStart = SourcePosition
End Function

Public Function SetCarretPos(SourcePosition As Long)
    On Error Resume Next
    RTB.SelLength = 0
    RTB.SelStart = SourcePosition
End Function

Public Function GetCarretPos() As Long
    On Error Resume Next
    GetCarretPos = RTB.SelStart
End Function

Public Function AddCharacter(Char As String)
    RTB.SelLength = 0
    RTB.SelText = Char
    RTB.SelStart = RTB.SelStart - Len(Char)
End Function
