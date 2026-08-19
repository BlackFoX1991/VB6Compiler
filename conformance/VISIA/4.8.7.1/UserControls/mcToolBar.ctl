VERSION 5.00
Begin VB.UserControl McToolBar 
   Alignable       =   -1  'True
   Appearance      =   0  '2D
   AutoRedraw      =   -1  'True
   BackColor       =   &H8000000A&
   ClientHeight    =   3090
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   3840
   ClipBehavior    =   0  'Keine
   FillColor       =   &H00FA9712&
   FillStyle       =   0  'Ausgefüllt
   MouseIcon       =   "mcToolBar.ctx":0000
   ScaleHeight     =   206
   ScaleMode       =   3  'Pixel
   ScaleWidth      =   256
   ToolboxBitmap   =   "mcToolBar.ctx":030A
End
Attribute VB_Name = "McToolBar"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False

'$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$
'$^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^$
'$^^^Gtech Creations^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^$
'$^^^¶¶¶^^^^¶¶¶^^^^^^^^¶¶¶¶¶¶^^^^^^^^^^^^^^^^^¶¶^¶¶¶¶¶¶^^^^^^^^^^^^^^^^^^^^^^^¶¶^^^^^^^^¶¶¶¶¶^^^^^$
'$^^^¶¶¶^^^^¶¶¶^^^^^^^^^^¶¶^^^^^^^^^^^^^^^^^^^¶¶^¶¶^^^¶¶^^^^^^^^^^^^^^^^^^^^¶¶¶¶^^^^^^^¶^^^^¶¶^^^^$
'$^^^¶^¶¶^^¶^¶¶^^¶¶¶¶^^^^¶¶^^^^¶¶¶¶¶^^^¶¶¶¶¶^^¶¶^¶¶^^^¶¶^^^¶¶¶¶^^¶¶^¶¶^^^^^^^^¶¶^^^^^^^¶^^^^¶¶^^^^$
'$^^^¶^¶¶^^¶^¶¶^¶¶^^^¶^^^¶¶^^^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^¶¶^^^¶¶^^¶^^^¶¶^¶¶¶¶¶^^^^^^^^¶¶^^^^^^^^^^^^¶¶^^^^$
'$^^^¶^^¶¶¶^^¶¶^¶¶^^^^^^^¶¶^^^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^¶¶¶¶¶¶^^^^^^^¶¶^¶¶^^^^^^^^^^^¶¶^^^^^^^^^^^¶¶^^^^^$
'$^^^¶^^¶¶¶^^¶¶^¶¶^^^^^^^¶¶^^^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^¶¶^^^¶¶^^¶¶¶¶¶¶^¶¶^^^^^^^^^^^¶¶^^^^^^^^^^¶¶^^^^^^$
'$^^^¶^^^¶^^^¶¶^¶¶^^^^^^^¶¶^^^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^^^^^^^^^^^¶¶^^^^^^^^^¶¶^^^^^^^$
'$^^^¶^^^¶^^^¶¶^¶¶^^^¶^^^¶¶^^^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^¶¶^^^¶¶^¶¶^^^¶¶^¶¶^^^^^^^^^^^¶¶^^^^¶¶^^¶¶^^^^^^^^$
'$^^^¶^^^^^^^¶¶^^¶¶¶¶^^^^¶¶^^^^¶¶¶¶¶^^^¶¶¶¶¶^^¶¶^¶¶¶¶¶¶^^^¶¶¶¶¶¶^¶¶^^^^^^^^^¶¶¶¶¶¶^^¶¶^¶¶¶¶¶¶¶^^^^$
'$^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^$
'$^^^By Jim Jose^^^^^^^^email : jimjosev33@yahoo.com^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^$
'$^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^$
'$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$

'-----------------------------------------------------------------------------------------------------------
' SourceCode : McToolBar 1.2
' Auther     : Jim Jose
' Email      : jimjosev33@yahoo.com
' Date       : 3-10-2005
' Purpose    : An advanced XP style toolbar
' CopyRight  : JimJose © Gtech Creations - 2005
'-----------------------------------------------------------------------------------------------------------

'-----------------------------------------------------------------------------------------------------------
' About :
' ------
'       McToolBar is an advanced, ownerdraw Xp style toolbar control with
' Hover effect, cutom tool tip, unicode support, gradient effects... After
' all this is very cute! (?)
'
'       As I mentioned earlier, my primary aim was to build an XP style toolbar
' control with hover effect. The control is fully self contained. It uses
' no property pages to Adding,Editing,Removing the Button captions, icons,
' and tooltips.

'       This technique ( was already shown i my prev control McImageList )
' gives a full flexibility by using the vb's property window for all these
' critical operations.
'
'       The conrol also offers Customized tooltip to each ToolButtons. It has
' balloon style and rectangular tooltips. Each Button can give seperate ToolTip
' icon!
'
'       No heavy work is done on it!. All the way to the completion of this
' project was very smooth. I built/designed this control according to my
' needs. If you want any additional functionality or u found any bugs, please
' contact me!
'
'-----------------------------------------------------------------------------------------------------------
' Features :
' --------
'       1  Single file'd
'       2. Owner drawn
'       3. XP style
'       4. Custom tooltip with balloon and rectangular style! for each button
'       5. Unicode support
'       6. Hover effects with custom colors'
'       7. Gradient effects
'       8. Tiled background
'       9. Highly flexible and avoids the use of property pages
'
'-----------------------------------------------------------------------------------------------------------
' Credits/Thanks :
' --------------
'
'    Paul Carton    -   For his unbeatable self subclasser!
'    Gary Noble     -   For his ColorBlending code!
'    Carls P.V      -   For his excellent DIB-gradient+tile routine!
'    Fred.cpp       -   For his most flexible tooltip code!
'    Dana Seaman    -   The Master of Unicode support!
'    All PSC members -  For the inspiration and lots of comments!
'
'-----------------------------------------------------------------------------------------------------------
' How To :
' ------
'       At the time when u create a new control, "Button_Count" will be one
' and "ButtonsPerRow" is 3. It will be in "WarpSize" mode ( control over uc
' size) and "Autosize" ( fit to uc width) is flase!
'
' 1. Create Buttons :
'       In the vb prop window, u can found "Button_Count" ( by default 1).
'    Change this to the number of buttons u need. That much controls will
'    be created instantly with default properties!
'
' 2. AccessButtons  :
'       Access each control by setting the property "Button_Index". All the
'    properties of this button will be loaded into the window.
'    It includes...
'
'        -  ButtonCaption
'        -  ButtonIcon
'        -  ButtonToolTipText
'        -  ButtonToolTipIcon
'
'        U can see the default values. To channe it just use the property
'    window. To move to next button, just set the Button_Index
'    [ For the ease of editing, all these property name starts with "Button"
'    and are avialable in continues manner ]
'
' 3. Remove buttons :
'        In the property window u can see "ButtonRemove". Change this to "Yes".
'    The currently loaded button will be removed
'
' 4.  Move Buttons [ Change index ] :
'       In the property window u can see "ButtonMove". Change this to the new
'     button index. The currently loaded button will be moved to new index!
'
' 5. General :
'       To change the appearance and behaviour, change...
'
'           - Warp Size
'           - AutoSize
'           - HoverColor
'           - Buttons per row
'
'-----------------------------------------------------------------------------------------------------------
' History:
'   3/10/2005   - Initial submission to PSC
'
' Version 1.4 :
'
'   [User Comments/feedbacks]
'   -------------------------
'   - ["invalid m_Button_Index in CreateTooltip routine (>-1)"] >> From Carles P.V.
'     This issue is cleared with a simple check for m_Button_Index in the
'     same routine
'
'   - ["allow user full control of rendering.... and related information.. "] >> From Carles P.V.
'       Added   "Public Event OnRedrawing(ByVal ButtonIndex As Long)"
'               "Public Event OnButtonHover(ByVal ButtonIndex As Long)"
'
'   - ["When the style is XP, you could add a shade to the image"] >> From "Heriberto Mantilla Santamaria"
'       Yeah, Button shadow effect is added, which can be activatd in any style (xp or nomal)
'       by the property "HoverIconShadow". Thanks a lot to Heriberto for the Support code!
'
'   - ["when the top (Horizontal) Toolbar is dragged, the application crashes"] >> From The_One
'       I tried to track this, and made some modifications. May its ok now!
'
'   - ["urgent features are: Enabled (whole toolbar) and ButtonEnabled()"] >> From Carles P.V.
'       Added both 1)Enabled 2)ButtonEnabled
'
'   - ["Just add somes states for buttons like : tbrUnpressed...."] >> From tr0piiic
'       The property "ButtonPressed" is added. Set it to True if
'       the button should show the state "Pressed!"
'
'   [Other modifications]
'   ---------------------
'   - I don't know any of u noticed... the ToolTip was not displaying when it
'     runs from a copiled exe. The problem solved by the LoadLibrary API call.
'
'   - "IconAlignment" option is added with ALN_Top, ALN_Bottom, ALN_Left,
'     ALN_Right options. Each button can have different "IconAlignment".
'
'   - New style "Style_Win9X" is added which will draw raised border to
'     all the buttons (as in MS toolbar)
'
'-----------------------------------------------------------------------------------------------------------

Option Explicit

'[APIs]
Private Declare Function GetVersionEx Lib "kernel32" Alias "GetVersionExA" (lpVersionInformation As OSVERSIONINFO) As Long
Private Declare Function DrawTextA Lib "user32.dll" (ByVal hdc As Long, ByVal lpStr As String, ByVal nCount As Long, ByRef lpRect As RECT, ByVal wFormat As Long) As Long
Private Declare Function DrawTextW Lib "user32.dll" (ByVal hdc As Long, ByVal lpStr As Long, ByVal nCount As Long, ByRef lpRect As RECT, ByVal wFormat As Long) As Long
Private Declare Function LoadLibrary Lib "kernel32" Alias "LoadLibraryA" (ByVal lpLibFileName As String) As Long
Private Declare Function FreeLibrary Lib "kernel32" (ByVal hLibModule As Long) As Long
Private Declare Function DrawFocusRect Lib "user32.dll" (ByVal hdc As Long, ByRef lpRect As RECT) As Long
Private Declare Function Rectangle Lib "gdi32.dll" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Long
Private Declare Function OleTranslateColor Lib "oleaut32.dll" (ByVal lOleColor As Long, ByVal lHPalette As Long, lColorRef As Long) As Long
Private Declare Function DeleteObject Lib "gdi32.dll" (ByVal hObject As Long) As Long
Private Declare Function DrawState Lib "user32.dll" Alias "DrawStateA" (ByVal hdc As Long, ByVal hBrush As Long, ByVal lpDrawStateProc As Long, ByVal lParam As Long, ByVal wParam As Long, ByVal n1 As Long, ByVal n2 As Long, ByVal n3 As Long, ByVal n4 As Long, ByVal un As Long) As Long
Private Declare Function CreateSolidBrush Lib "gdi32.dll" (ByVal crColor As Long) As Long
Private Declare Function DeleteDC Lib "gdi32.dll" (ByVal hdc As Long) As Long
Private Declare Function GetIconInfo Lib "user32.dll" (ByVal hIcon As Long, ByRef piconinfo As ICONINFO) As Long
Private Declare Function ReleaseDC Lib "user32" (ByVal hwnd As Long, ByVal hdc As Long) As Long

Private Declare Function RoundRect Lib "gdi32.dll" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long, ByVal X3 As Long, ByVal Y3 As Long) As Long


' for Carles P.V DIB solutions
Private Declare Function StretchDIBits Lib "gdi32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long, ByVal dx As Long, ByVal dy As Long, ByVal SrcX As Long, ByVal SrcY As Long, ByVal wSrcWidth As Long, ByVal wSrcHeight As Long, lpBits As Any, lpBitsInfo As Any, ByVal wUsage As Long, ByVal dwRop As Long) As Long
Private Declare Function GetObjectType Lib "gdi32" (ByVal hgdiobj As Long) As Long
Private Declare Function FillRect Lib "user32" (ByVal hdc As Long, lpRect As RECT, ByVal hBrush As Long) As Long
Private Declare Function SetBrushOrgEx Lib "gdi32" (ByVal hdc As Long, ByVal nXOrg As Long, ByVal nYOrg As Long, lppt As POINTAPI) As Long
Private Declare Function SetRect Lib "user32" (lpRect As RECT, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Long
Private Declare Function CreateDIBPatternBrushPt Lib "gdi32" (lpPackedDIB As Any, ByVal iUsage As Long) As Long
Private Declare Function SelectObject Lib "gdi32" (ByVal hdc As Long, ByVal hObject As Long) As Long
Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (ByRef lpDest As Any, ByRef lpSource As Any, ByVal iLen As Long)
Private Declare Function GetObject Lib "gdi32" Alias "GetObjectA" (ByVal hObject As Long, ByVal nCount As Long, lpObject As Any) As Long
Private Declare Function CreateCompatibleDC Lib "gdi32" (ByVal hdc As Long) As Long
Private Declare Function GetDIBits Lib "gdi32" (ByVal aHDC As Long, ByVal hBitmap As Long, ByVal nStartScan As Long, ByVal nNumScans As Long, lpBits As Any, lpBI As BITMAPINFOHEADER, ByVal wUsage As Long) As Long
Private Declare Function GetPixel Lib "gdi32.dll" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long) As Long
Private Declare Function SetPixel Lib "gdi32.dll" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long, ByVal crColor As Long) As Long

' for subclassing
Private Declare Sub RtlMoveMemory Lib "kernel32" (Destination As Any, Source As Any, ByVal Length As Long)
Private Declare Function GetModuleHandleA Lib "kernel32" (ByVal lpModuleName As String) As Long
Private Declare Function GetProcAddress Lib "kernel32" (ByVal hModule As Long, ByVal lpProcName As String) As Long
Private Declare Function GlobalAlloc Lib "kernel32" (ByVal wFlags As Long, ByVal dwBytes As Long) As Long
Private Declare Function GlobalFree Lib "kernel32" (ByVal hMem As Long) As Long
Private Declare Function SetWindowLongA Lib "user32" (ByVal hwnd As Long, ByVal nIndex As Long, ByVal dwNewLong As Long) As Long
Private Declare Function LoadLibraryA Lib "kernel32" (ByVal lpLibFileName As String) As Long
Private Declare Function TrackMouseEvent Lib "user32" (lpEventTrack As TRACKMOUSEEVENT_STRUCT) As Long
Private Declare Function TrackMouseEventComCtl Lib "Comctl32" Alias "_TrackMouseEvent" (lpEventTrack As TRACKMOUSEEVENT_STRUCT) As Long

' for tooltip
Private Declare Function CreateWindowEx Lib "user32" Alias "CreateWindowExA" (ByVal dwExStyle As Long, ByVal lpClassName As String, ByVal lpWindowName As String, ByVal dwStyle As Long, ByVal X As Long, ByVal Y As Long, ByVal nWidth As Long, ByVal nHeight As Long, ByVal hWndParent As Long, ByVal hMenu As Long, ByVal hInstance As Long, lpParam As Any) As Long
Private Declare Function SendMessage Lib "user32" Alias "SendMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, lParam As Any) As Long
Private Declare Function DestroyWindow Lib "user32" (ByVal hwnd As Long) As Long
Private Declare Function GetClientRect Lib "user32" (ByVal hwnd As Long, lpRect As RECT) As Long
Private Declare Function KillTimer Lib "user32" (ByVal hwnd As Long, ByVal nIDEvent As Long) As Long
Private Declare Function SetTimer Lib "user32" (ByVal hwnd As Long, ByVal nIDEvent As Long, ByVal uElapse As Long, ByVal lpTimerFunc As Long) As Long
Private Declare Function SetWindowPos Lib "user32.dll" (ByVal hwnd As Long, ByVal hWndInsertAfter As Long, ByVal X As Long, ByVal Y As Long, ByVal cx As Long, ByVal cy As Long, ByVal wFlags As Long) As Long

'[APIConstants]
Private Const DIB_RGB_ColS      As Long = 0
Private Const VER_PLATFORM_WIN32_NT  As Long = 2
Private Const DSS_DISABLED As Long = &H20
Private Const DSS_MONO As Long = &H80
Private Const DST_BITMAP As Long = &H4
Private Const DST_ICON As Long = &H3
Private Const DST_COMPLEX As Long = &H0

Private Const DT_CENTER As Long = &H1
Private Const DT_LEFT As Long = &H0
Private Const DT_RIGHT As Long = &H2


' for subclassing
Private Const WM_GETMINMAXINFO      As Long = &H24
Private Const WM_WINDOWPOSCHANGED   As Long = &H47
Private Const WM_WINDOWPOSCHANGING  As Long = &H46
Private Const WM_LBUTTONDOWN        As Long = &H201
Private Const WM_SIZE               As Long = &H5
Private Const WM_LBUTTONDBLCLK      As Long = &H203
Private Const WM_RBUTTONDOWN        As Long = &H204
Private Const WM_MOUSEMOVE          As Long = &H200
Private Const WM_SETFOCUS           As Long = &H7
Private Const WM_KILLFOCUS          As Long = &H8
Private Const WM_MOVE               As Long = &H3
Private Const WM_TIMER              As Long = &H113
Private Const WM_MOUSELEAVE         As Long = &H2A3
Private Const WM_MOUSEWHEEL         As Long = &H20A
Private Const WM_MOUSEHOVER         As Long = &H2A1

Private Const ALL_MESSAGES           As Long = -1                                       'All messages added or deleted
Private Const GMEM_FIXED             As Long = 0                                        'Fixed memory GlobalAlloc flag
Private Const GWL_WNDPROC            As Long = -4                                       'Get/SetWindow offset to the WndProc procedure address
Private Const PATCH_04               As Long = 88                                       'Table B (before) address patch offset
Private Const PATCH_05               As Long = 93                                       'Table B (before) entry count patch offset
Private Const PATCH_08               As Long = 132                                      'Table A (after) address patch offset
Private Const PATCH_09               As Long = 137                                      'Table A (after) entry count patch offset

Private sc_aSubData()                As tSubData
Private bTrack                       As Boolean
Private bTrackUser32                 As Boolean
Private bInCtrl                      As Boolean

'Tooltip Window Constants
Private Const WM_USER                   As Long = &H400
Private Const TTS_NOPREFIX              As Long = &H2
Private Const TTF_TRANSPARENT           As Long = &H100
Private Const TTF_CENTERTIP             As Long = &H2
Private Const TTM_ADDTOOLA              As Long = (WM_USER + 4)
Private Const TTM_ADDTOOLW              As Long = (WM_USER + 50)
Private Const TTM_DELTOOLA              As Long = (WM_USER + 5)
Private Const TTM_DELTOOLW              As Long = (WM_USER + 51)
Private Const TTM_ACTIVATE              As Long = WM_USER + 1
Private Const TTM_UPDATETIPTEXTA        As Long = (WM_USER + 12)
Private Const TTM_SETMAXTIPWIDTH        As Long = (WM_USER + 24)
Private Const TTM_SETTIPBKCOLOR         As Long = (WM_USER + 19)
Private Const TTM_SETTIPTEXTCOLOR       As Long = (WM_USER + 20)
Private Const TTM_SETTITLE              As Long = (WM_USER + 32)
Private Const TTM_SETTITLEW             As Long = (WM_USER + 33)
Private Const TTS_BALLOON               As Long = &H40
Private Const TTS_ALWAYSTIP             As Long = &H1
Private Const TTF_SUBCLASS              As Long = &H10
Private Const TOOLTIPS_CLASSA           As String = "tooltips_class32"
Private Const CW_USEDEFAULT             As Long = &H80000000
Private Const TTM_SETMARGIN             As Long = (WM_USER + 26)

Private Const SWP_FRAMECHANGED          As Long = &H20
Private Const SWP_DRAWFRAME             As Long = SWP_FRAMECHANGED
Private Const SWP_HIDEWINDOW            As Long = &H80
Private Const SWP_NOACTIVATE            As Long = &H10
Private Const SWP_NOCOPYBITS            As Long = &H100
Private Const SWP_NOMOVE                As Long = &H2
Private Const SWP_NOOWNERZORDER         As Long = &H200
Private Const SWP_NOREDRAW              As Long = &H8
Private Const SWP_NOREPOSITION          As Long = SWP_NOOWNERZORDER
Private Const SWP_NOSIZE                As Long = &H1
Private Const SWP_NOZORDER              As Long = &H4
Private Const HWND_TOPMOST              As Long = -&H1

'[Types]
Private Type ToolButton
    TB_Caption          As String
    TB_Icon             As Picture
    TB_Enabled          As Boolean
    TB_Type             As ButtonTypeEnum
    TB_ToolTipText      As String
    TB_ToolTipIcon      As ToolTipIconEnum
    TB_Pressed          As Boolean
    TB_IconAllignment   As IconAllignmentEnum
End Type

Private Type RECT
    Left    As Long
    Top     As Long
    Right   As Long
    Bottom  As Long
End Type

Private Type BITMAPINFOHEADER
    biSize          As Long
    biWidth         As Long
    biHeight        As Long
    biPlanes        As Integer
    biBitCount      As Integer
    biCompression   As Long
    biSizeImage     As Long
    biXPelsPerMeter As Long
    biYPelsPerMeter As Long
    biClrUsed       As Long
    biClrImportant  As Long
End Type

Private Type ICONINFO
    fIcon As Long
    xHotspot As Long
    yHotspot As Long
    hbmMask As Long
    hbmColor As Long
End Type

Private Type BITMAP
   bmType       As Long
   bmWidth      As Long
   bmHeight     As Long
   bmWidthBytes As Long
   bmPlanes     As Integer
   bmBitsPixel  As Integer
   bmBits       As Long
End Type

Private Type POINTAPI
   X As Long
   Y As Long
End Type

Private Type OSVERSIONINFO
   dwOSVersionInfoSize  As Long
   dwMajorVersion       As Long
   dwMinorVersion       As Long
   dwBuildNumber        As Long
   dwPlatformId         As Long
   szCSDVersion         As String * 128 ' Maintenance string
End Type

Private Type tSubData                                                                   'Subclass data type
    hwnd          As Long                                            'Handle of the window being subclassed
    nAddrSub      As Long                                            'The address of our new WndProc (allocated memory).
    nAddrOrig     As Long                                            'The address of the pre-existing WndProc
    nMsgCntA      As Long                                            'Msg after table entry count
    nMsgCntB      As Long                                            'Msg before table entry count
    aMsgTblA()    As Long                                            'Msg after table array
    aMsgTblB()    As Long                                            'Msg Before table array
End Type
                                
Private Type TRACKMOUSEEVENT_STRUCT
  cbSize          As Long
  dwFlags         As TRACKMOUSEEVENT_FLAGS
  hwndTrack       As Long
  dwHoverTime     As Long
End Type

'Tooltip Window Types
Private Type TOOLINFO
    lSize           As Long
    lFlags          As Long
    lhWnd           As Long
    lId             As Long
    lpRect          As RECT
    hInstance       As Long
    lpStr           As Long
    lParam          As Long
End Type

'[Enums]
Public Enum ButtonPropertyEnum
    [BTN_Type] = 0
    [BTN_Caption] = 1
    [BTN_Enabled] = 2
    [BTN_Icon] = 3
    [BTN_IconAlignment] = 4
    [BTN_Pressed] = 5
    [BTN_Tooltip] = 6
    [BTN_ToolTipIcon] = 7
End Enum

Public Enum IconAllignmentEnum
    [ALN_Top] = 0
    [ALN_Bottom] = 1
    [ALN_Left] = 2
    [ALN_Right] = 3
    [ALN_Center] = 4
End Enum

Public Enum ButtonTypeEnum
    [TYP_Button] = 0
    [TYP_Seperator] = 1
End Enum

Public Enum ButtonsStyleEnum
    [Style_Flat] = 0
    [Style_Win9X] = 1
    [Style_OfficeXP] = 2
    [Style_WinXP] = 3
    [Style_Solid] = 4
End Enum

Public Enum UserOptionEnum
    [No!] = -1
    [Yes!] = 1
End Enum

Public Enum GradientDirectionEnum
    [Fill_None] = 0
    [Fill_Horizontal] = 1
    [Fill_HorizontalMiddleOut] = 2
    [Fill_Vertical] = 3
    [Fill_VerticalMiddleOut] = 4
    [Fill_DownwardDiagonal] = 5
    [Fill_UpwardDiagonal] = 6
End Enum

Public Enum TooTipStyleEnum
    [Tip_Normal] = 1
    [Tip_Balloon] = 2
End Enum

Public Enum ToolTipIconEnum
    [Icon_None] = 0
    [Icon_Info] = 1
    [Icon_Warning] = 2
    [Icon_Error] = 3
End Enum

Public Enum TB_AppearanceEnum
    [Flat] = 0
    [3D] = 1
End Enum

Public Enum BorderStyleEnum
    [BDR_None] = 0
    [BDR_RAISED] = 1
    [BDR_InSet] = 2
End Enum

' for subclassing
Private Enum eMsgWhen
    MSG_AFTER = 1                                                                         'Message calls back after the original (previous) WndProc
    MSG_BEFORE = 2                                                                        'Message calls back before the original (previous) WndProc
    MSG_BEFORE_AND_AFTER = MSG_AFTER Or MSG_BEFORE                                        'Message calls back before and after the original (previous) WndProc
End Enum

Private Enum TRACKMOUSEEVENT_FLAGS
    TME_HOVER = &H1&
    TME_LEAVE = &H2&
    TME_QUERY = &H40000000
    TME_CANCEL = &H80000000
End Enum

'[Local Variables]
Private m_bIsNT             As Boolean
Private m_BtnWidth          As Double
Private m_BtnHeight         As Double
Private m_BtnRows           As Long
Private m_Pressed           As Boolean
Private m_MouseX            As Long
Private m_MouseY            As Long
Private m_hMode             As Long
Private m_BackDrawn         As Boolean


Private m_TimerElsp         As Long
Private m_ToolTipHwnd       As Long
Private m_ToolTipInfo       As TOOLINFO
Private m_TooTipStyle       As TooTipStyleEnum
Private m_ToolTipBackCol    As OLE_COLOR
Private m_ToolTipForeCol    As OLE_COLOR

'[Data Storage]
Private m_ButtonItem() As ToolButton

'Property Variables:
Private m_Button_Count   As Long
Private m_Button_Index  As Long
Private m_Appearance    As Integer
Private m_BackColor     As OLE_COLOR
Private m_BorderStyle   As Integer
Private m_Enabled       As Boolean
Private m_Font          As Font
Private m_ForeColor     As OLE_COLOR
Private m_BackGround    As Picture
Private m_ButtonsWidth  As Long
Private m_ButtonsHeight As Long
Private m_ButtonsPerRow As Long
Private m_HoverColor    As OLE_COLOR
Private m_ShowSeperator As Boolean
Private m_BackGradient  As GradientDirectionEnum
Private m_ButtonsStyle    As ButtonsStyleEnum
Private m_BorderColor   As OLE_COLOR
Private m_HoverIconShadow   As Boolean
Private m_BackGradientCol   As OLE_COLOR
Private m_ButtonsSeperatorWidth As Long


'Default Property Values:
Private Const m_def_Button_Count = 1
Private Const m_def_Button_Index = 0
Private Const m_def_Appearance = 0
Private Const m_def_BackColor = &H8000000F
Private Const m_def_BorderStyle = 0
Private Const m_def_Enabled = True
Private Const m_def_ForeColor = 0
Private Const m_def_ButtonCaption = ""
Private Const m_def_ButtonsWidth = 32
Private Const m_def_ButtonsHeight = 32
Private Const m_def_ButtonsPerRow = 3
Private Const m_def_HoverColor = &H8000000F
Private Const m_def_WarpSize = True
Private Const m_def_ButtonToolTip = ""
Private Const m_def_TooTipStyle = Tip_Balloon
Private Const m_def_ToolTipBackCol = &HE6FDFD
Private Const m_def_ToolTipForeCol = &H0&
Private Const m_def_ButtonToolTipIcon = 0
Private Const m_def_BackGradient = Fill_None
Private Const m_def_BackGradientCol = &HC0C0FF
Private Const m_def_ButtonsStyle = 0
Private Const m_def_BorderColor = &H8000000A
Private Const m_def_ButtonEnabled = True
Private Const m_def_HoverIconShadow = True
Private Const m_def_ButtonPressed = False
Private Const m_def_ButtonIconAllignment = ALN_Top
Private Const m_def_ButtonsSeperatorWidth = 10
Private Const m_def_ShowSeperator = True

'Event Declarations:
Public Event MouseEnter()
Public Event MouseLeave()
Public Event Click(ByVal ButtonIndex As Long)
Public Event DblClick(ByVal ButtonIndex As Long)
Public Event OnRedrawing(ByVal ButtonIndex As Long)
Public Event OnButtonHover(ByVal ButtonIndex As Long)
Public Event KeyDown(ByVal ButtonIndex As Long, KeyCode As Integer, Shift As Integer)
Public Event KeyUp(ByVal ButtonIndex As Long, KeyCode As Integer, Shift As Integer)
Public Event MouseUp(ByVal ButtonIndex As Long, Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event MouseDown(ByVal ButtonIndex As Long, Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event MouseMove(ByVal ButtonIndex As Long, Button As Integer, Shift As Integer, X As Single, Y As Single)


'[ Subclassed events receiver ]
'------------------------------------------------------------------------------------------
Public Sub zSubclass_Proc(ByVal bBefore As Boolean, ByRef bHandled As Boolean, ByRef lReturn As Long, ByRef lng_hWnd As Long, ByRef uMsg As Long, ByRef wParam As Long, ByRef lParam As Long)
 
    Select Case uMsg

        Case WM_MOUSEMOVE
        
            If m_MouseX = WordLo(lParam) And m_MouseY = WordHi(lParam) Then Exit Sub
            m_MouseX = WordLo(lParam)
            m_MouseY = WordHi(lParam)
    
             ' Set timer for tooltip generation
            SetTimer hwnd, 1, 1, 0
            m_TimerElsp = 0
            
            If Not bInCtrl Then
                'debug.Print "Mouse Enter"
                bInCtrl = True
                Call TrackMouseLeave(lng_hWnd)
                RaiseEvent MouseEnter
            End If

            ' Remove the tooltip on mouse move
            RemoveToolTip
            
        Case WM_MOUSELEAVE
            'debug.Print "Mouse leave"
            bInCtrl = False
            m_Pressed = False
            m_Button_Index = -1
            RemoveToolTip
            RedrawControl
            RaiseEvent MouseLeave
            
        ' The timer callback
        Case WM_TIMER
            m_TimerElsp = m_TimerElsp + 1
            If m_TimerElsp = 5 Then ' After 1/2 Sec
                KillTimer hwnd, 1
                If bInCtrl Then CreateToolTip
            End If
            
    End Select
    
End Sub


Public Property Get Appearance() As TB_AppearanceEnum
    Appearance = m_Appearance
End Property

Public Property Let Appearance(ByVal New_Appearance As TB_AppearanceEnum)
    m_Appearance = New_Appearance
    PropertyChanged "Appearance"
    RedrawControl
End Property


Public Property Get BackColor() As OLE_COLOR
    BackColor = m_BackColor
End Property

Public Property Let BackColor(ByVal New_BackColor As OLE_COLOR)
    m_BackColor = New_BackColor
    PropertyChanged "BackColor"
    m_BackDrawn = False
    RedrawControl
End Property


Public Property Get BorderStyle() As BorderStyleEnum
    BorderStyle = m_BorderStyle
End Property

Public Property Let BorderStyle(ByVal New_BorderStyle As BorderStyleEnum)
    m_BorderStyle = New_BorderStyle
    PropertyChanged "BorderStyle"
    RedrawControl
End Property


Public Property Get Enabled() As Boolean
    Enabled = m_Enabled
End Property

Public Property Let Enabled(ByVal New_Enabled As Boolean)
    m_Enabled = New_Enabled
    PropertyChanged "Enabled"
    RedrawControl
End Property


Public Property Get Font() As Font
    Set Font = m_Font
End Property

Public Property Set Font(ByVal New_Font As Font)
    Set m_Font = New_Font
    PropertyChanged "Font"
    RedrawControl
End Property


Public Property Get ForeColor() As OLE_COLOR
    ForeColor = m_ForeColor
End Property

Public Property Let ForeColor(ByVal New_ForeColor As OLE_COLOR)
    m_ForeColor = New_ForeColor
    PropertyChanged "ForeColor"
    RedrawControl
End Property


Public Property Get ButtonIcon() As Picture
    If Not m_Button_Index = -1 Then
        Set ButtonIcon = m_ButtonItem(m_Button_Index).TB_Icon
    End If
End Property

Public Property Set ButtonIcon(ByVal New_ButtonIcon As Picture)
    If Not m_Button_Index = -1 Then
        Set m_ButtonItem(m_Button_Index).TB_Icon = New_ButtonIcon
        PropertyChanged "ButtonIcon"
        RedrawControl
    End If
End Property


Public Property Get ButtonsHeight() As Long
    ButtonsHeight = m_ButtonsHeight
End Property

Public Property Let ButtonsHeight(ByVal New_ButtonsHeight As Long)
    m_ButtonsHeight = New_ButtonsHeight
    PropertyChanged "ButtonsHeight"
    RedrawControl
End Property


Public Property Get ButtonsPerRow() As Long
    ButtonsPerRow = m_ButtonsPerRow
End Property

Public Property Let ButtonsPerRow(ByVal New_ButtonsPerRow As Long)
    m_ButtonsPerRow = New_ButtonsPerRow
    PropertyChanged "ButtonsPerRow"
    RedrawControl
End Property


Public Property Get ButtonsWidth() As Long
    ButtonsWidth = m_ButtonsWidth
End Property

Public Property Let ButtonsWidth(ByVal New_ButtonsWidth As Long)
    m_ButtonsWidth = New_ButtonsWidth
    PropertyChanged "ButtonsWidth"
    RedrawControl
End Property


Public Property Get ButtonCaption() As String
    If Not m_Button_Index = -1 Then
        ButtonCaption = m_ButtonItem(m_Button_Index).TB_Caption
    End If
End Property

Public Property Let ButtonCaption(ByVal New_ButtonCaption As String)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_Caption = New_ButtonCaption
        PropertyChanged "ButtonCaption"
        RedrawControl
    End If
End Property


Public Property Get Button_Type() As ButtonTypeEnum
    If Not m_Button_Index = -1 Then
        Button_Type = m_ButtonItem(m_Button_Index).TB_Type
    End If
End Property

Public Property Let Button_Type(ByVal New_Button_Type As ButtonTypeEnum)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_Type = New_Button_Type
        PropertyChanged "Button_Type"
        RedrawControl
    End If
End Property


Public Property Get ButtonsSeperatorWidth() As Long
    ButtonsSeperatorWidth = m_ButtonsSeperatorWidth
End Property

Public Property Let ButtonsSeperatorWidth(ByVal New_ButtonsSeperatorWidth As Long)
    m_ButtonsSeperatorWidth = New_ButtonsSeperatorWidth
    PropertyChanged "ButtonsSeperatorWidth"
    RedrawControl
End Property


Public Property Get BackGround() As Picture
    Set BackGround = m_BackGround
End Property

Public Property Set BackGround(ByVal New_BackGround As Picture)
    Set m_BackGround = New_BackGround
    PropertyChanged "BackGround"
    m_BackDrawn = False
    RedrawControl
End Property


Public Property Get Button_Count() As Long
    Button_Count = m_Button_Count
End Property

Public Property Let Button_Count(ByVal New_Button_Count As Long)
Dim nPrev As Long
Dim X As Long

    If Not New_Button_Count = m_Button_Count And New_Button_Count >= 1 Then
        
        ' Create new array size
        nPrev = m_Button_Count
        m_Button_Count = New_Button_Count
        ReDim Preserve m_ButtonItem(m_Button_Count - 1)
        
        ' Assign default caption
        If m_Button_Count > nPrev Then
            For X = nPrev + 1 To m_Button_Count
                m_ButtonItem(X - 1).TB_Enabled = m_def_Enabled
                m_ButtonItem(X - 1).TB_IconAllignment = m_def_ButtonIconAllignment
                m_ButtonItem(X - 1).TB_Pressed = m_def_ButtonPressed
                m_ButtonItem(X - 1).TB_ToolTipIcon = m_def_ButtonToolTipIcon
                m_ButtonItem(X - 1).TB_ToolTipText = m_def_ButtonToolTip
            Next X
        End If
        
        PropertyChanged "Button_Count"
        RedrawControl
    End If
    
End Property


Public Property Get Button_Index() As Long
    Button_Index = m_Button_Index
End Property

Public Property Let Button_Index(ByVal New_Button_Index As Long)
    
'    If New_Button_Index < 0 Then New_Button_Index = 0
'    If New_Button_Index >= m_Button_Count Then New_Button_Index = m_Button_Count - 1
    
    If New_Button_Index < 0 Or New_Button_Index >= m_Button_Count Then
        Err.Raise 33, , "Index out or range!!"
        Exit Property
    End If
    
    If Not New_Button_Index = m_Button_Index Then
        m_Button_Index = New_Button_Index
        PropertyChanged "Button_Index"
        RedrawControl
    End If
    
End Property


Public Property Get HoverColor() As OLE_COLOR
    HoverColor = m_HoverColor
End Property

Public Property Let HoverColor(ByVal New_HoverColor As OLE_COLOR)
    m_HoverColor = New_HoverColor
    PropertyChanged "HoverColor"
    RedrawControl
End Property


Public Property Get ButtonToolTip() As String
    If Not m_Button_Index = -1 Then
        ButtonToolTip = m_ButtonItem(m_Button_Index).TB_ToolTipText
    End If
End Property

Public Property Let ButtonToolTip(ByVal New_ButtonToolTip As String)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_ToolTipText = New_ButtonToolTip
        PropertyChanged "ButtonToolTip"
    End If
End Property


Public Property Get ToolTipBackCol() As OLE_COLOR
    ToolTipBackCol = m_ToolTipBackCol
End Property

Public Property Let ToolTipBackCol(ByVal New_ToolTipBackCol As OLE_COLOR)
    m_ToolTipBackCol = New_ToolTipBackCol
    PropertyChanged "ToolTipBackCol"
End Property


Public Property Get ToolTipForeCol() As OLE_COLOR
    ToolTipForeCol = m_ToolTipForeCol
End Property

Public Property Let ToolTipForeCol(ByVal New_ToolTipForeCol As OLE_COLOR)
    m_ToolTipForeCol = New_ToolTipForeCol
    PropertyChanged "ToolTipForeCol"
End Property


Public Property Get TooTipStyle() As TooTipStyleEnum
    TooTipStyle = m_TooTipStyle
End Property

Public Property Let TooTipStyle(ByVal New_TooTipStyle As TooTipStyleEnum)
    m_TooTipStyle = New_TooTipStyle
    PropertyChanged "TooTipStyle"
End Property


Public Property Get ButtonToolTipIcon() As ToolTipIconEnum
    If Not m_Button_Index = -1 Then
        ButtonToolTipIcon = m_ButtonItem(m_Button_Index).TB_ToolTipIcon
    End If
End Property

Public Property Let ButtonToolTipIcon(ByVal New_ButtonToolTipIcon As ToolTipIconEnum)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_ToolTipIcon = New_ButtonToolTipIcon
        PropertyChanged "ButtonToolTipIcon"
    End If
End Property


Public Property Get BackGradient() As GradientDirectionEnum
    BackGradient = m_BackGradient
End Property

Public Property Let BackGradient(ByVal New_BackGradient As GradientDirectionEnum)
    m_BackGradient = New_BackGradient
    PropertyChanged "BackGradient"
    m_BackDrawn = False
    RedrawControl
End Property


Public Property Get BackGradientCol() As OLE_COLOR
    BackGradientCol = m_BackGradientCol
End Property

Public Property Let BackGradientCol(ByVal New_BackGradientCol As OLE_COLOR)
    m_BackGradientCol = New_BackGradientCol
    PropertyChanged "BackGradientCol"
    m_BackDrawn = False
    RedrawControl
End Property


Public Property Get ButtonsStyle() As ButtonsStyleEnum
    ButtonsStyle = m_ButtonsStyle
End Property

Public Property Let ButtonsStyle(ByVal New_ButtonsStyle As ButtonsStyleEnum)
    m_ButtonsStyle = New_ButtonsStyle
    ApplyTheme New_ButtonsStyle
    m_BackDrawn = False
    PropertyChanged "ButtonsStyle"
    RedrawControl
End Property


Public Property Get ButtonEnabled() As Boolean
    If Not m_Button_Index = -1 Then
        ButtonEnabled = m_ButtonItem(m_Button_Index).TB_Enabled
    End If
End Property

Public Property Let ButtonEnabled(ByVal New_ButtonEnabled As Boolean)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_Enabled = New_ButtonEnabled
        PropertyChanged "ButtonEnabled"
        RedrawControl
    End If
End Property


Public Property Get BorderColor() As OLE_COLOR
    BorderColor = m_BorderColor
End Property

Public Property Let BorderColor(ByVal New_BorderColor As OLE_COLOR)
    m_BorderColor = New_BorderColor
    PropertyChanged "BorderColor"
    RedrawControl
End Property

Public Property Get HoverIconShadow() As Boolean
    HoverIconShadow = m_HoverIconShadow
End Property

Public Property Let HoverIconShadow(ByVal New_HoverIconShadow As Boolean)
    m_HoverIconShadow = New_HoverIconShadow
    PropertyChanged "HoverIconShadow"
    RedrawControl
End Property


Public Property Get ButtonPressed() As Boolean
    If Not m_Button_Index = -1 Then
        ButtonPressed = m_ButtonItem(m_Button_Index).TB_Pressed
    End If
End Property

Public Property Let ButtonPressed(ByVal New_ButtonPressed As Boolean)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_Pressed = New_ButtonPressed
        PropertyChanged "ButtonPressed"
        RedrawControl
    End If
End Property


Public Property Get ButtonIconAllignment() As IconAllignmentEnum
    If Not m_Button_Index = -1 Then
        ButtonIconAllignment = m_ButtonItem(m_Button_Index).TB_IconAllignment
    End If
End Property

Public Property Let ButtonIconAllignment(ByVal New_ButtonIconAllignment As IconAllignmentEnum)
    If Not m_Button_Index = -1 Then
        m_ButtonItem(m_Button_Index).TB_IconAllignment = New_ButtonIconAllignment
        PropertyChanged "ButtonIconAllignment"
        RedrawControl
    End If
End Property


Public Property Get ShowSeperator() As Boolean
    ShowSeperator = m_ShowSeperator
End Property

Public Property Let ShowSeperator(ByVal New_ShowSeperator As Boolean)
    m_ShowSeperator = New_ShowSeperator
    PropertyChanged "ShowSeperator"
    RedrawControl
End Property


' Remove Button
Public Property Get ButtonRemove() As UserOptionEnum
    ButtonRemove = -1
End Property

Public Property Let ButtonRemove(ByVal vNewValue As UserOptionEnum)
    
    If vNewValue = 1 Then
        RemoveButton m_Button_Index
        If m_Button_Index >= m_Button_Count Then m_Button_Index = m_Button_Count - 1
        RedrawControl
    End If
    
End Property


' Move Button Index
Public Property Get ButtonMoveTo() As Long
    ButtonMoveTo = -1
End Property

Public Property Let ButtonMoveTo(ByVal vNewValue As Long)

    MoveButtonTo m_Button_Index, vNewValue
    m_Button_Index = vNewValue
    RedrawControl
    
End Property

' Insert to Button Index
Public Property Get ButtonInsertTo() As Long
    ButtonInsertTo = -1
End Property

Public Property Let ButtonInsertTo(ByVal vNewValue As Long)

    InsertButtonTo m_Button_Index, vNewValue
    m_Button_Index = vNewValue
    RedrawControl
        
End Property


Private Sub UserControl_Click()
    RaiseEvent Click(m_Button_Index)
End Sub

Private Sub UserControl_DblClick()
    m_Pressed = True
    RedrawControl
    RaiseEvent DblClick(m_Button_Index)
End Sub

Private Sub UserControl_Initialize()
    'debug.Print "----------------------------------------"
    'debug.Print "INITIALIZED!"
    'debug.Print "----------------------------------------"
    m_hMode = LoadLibrary("shell32.dll")
    m_bIsNT = IsNT
End Sub

'Initialize Properties for User Control
Private Sub UserControl_InitProperties()
    m_Appearance = m_def_Appearance
    m_BackColor = m_def_BackColor
    m_BorderStyle = m_def_BorderStyle
    m_Enabled = m_def_Enabled
    Set m_Font = Ambient.Font
    m_ForeColor = m_def_ForeColor
    m_Button_Count = m_def_Button_Count
    m_Button_Index = m_def_Button_Index
    Set m_BackGround = LoadPicture("")
    m_ButtonsWidth = m_def_ButtonsWidth
    m_ButtonsHeight = m_def_ButtonsHeight
    m_ButtonsPerRow = m_def_ButtonsPerRow
    m_HoverColor = m_def_HoverColor
    m_BackGradient = m_def_BackGradient
    m_BackGradientCol = m_def_BackGradientCol
    m_ToolTipBackCol = m_def_ToolTipBackCol
    m_ToolTipForeCol = m_def_ToolTipForeCol
    m_ButtonsStyle = m_def_ButtonsStyle
    m_BorderColor = m_def_BorderColor
    m_HoverIconShadow = m_def_HoverIconShadow
    m_ButtonsSeperatorWidth = m_def_ButtonsSeperatorWidth
    m_ShowSeperator = m_def_ShowSeperator
    
    ReDim m_ButtonItem(0)
    m_ButtonItem(0).TB_Caption = m_def_ButtonCaption & "0"
    m_ButtonItem(0).TB_ToolTipText = m_def_ButtonToolTip
    m_ButtonItem(0).TB_Enabled = m_def_Enabled
    m_ButtonItem(0).TB_IconAllignment = m_def_ButtonIconAllignment
    m_ButtonItem(0).TB_Pressed = m_def_ButtonPressed
    m_ButtonItem(0).TB_ToolTipIcon = m_def_ButtonToolTipIcon

End Sub

Private Sub UserControl_KeyDown(KeyCode As Integer, Shift As Integer)
    m_Pressed = True
    RaiseEvent KeyDown(m_Button_Index, KeyCode, Shift)
End Sub

Private Sub UserControl_KeyUp(KeyCode As Integer, Shift As Integer)
    m_Pressed = False
    RaiseEvent KeyUp(m_Button_Index, KeyCode, Shift)
End Sub

Private Sub UserControl_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Not m_Button_Index = -1 Then
        m_Pressed = True
        RedrawControl
        RaiseEvent MouseDown(m_Button_Index, Button, Shift, X, Y)
    End If
End Sub

Private Sub UserControl_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
Dim nIndex As Long

    On Error GoTo handle
    ' Calculate the Hover item
    nIndex = GetButtonFromXY(X, Y)
    
    ' Check the value
    If Int(X / m_ButtonsWidth) >= m_ButtonsPerRow Or nIndex >= m_Button_Count Or nIndex < 0 Then
        nIndex = -1
        MousePointer = vbNormal
    Else
        If m_ButtonItem(nIndex).TB_Enabled = False Or m_ButtonItem(nIndex).TB_Type = TYP_Seperator Then
            nIndex = -1
            MousePointer = vbNormal
        Else
            MousePointer = vbCustom
        End If
    End If

    ' Redraw if necessary
    If Not nIndex = m_Button_Index Then
        m_Button_Index = nIndex
        RaiseEvent OnButtonHover(m_Button_Index)
        RedrawControl
    End If
    
handle:
    RaiseEvent MouseMove(m_Button_Index, Button, Shift, X, Y)
    
End Sub

Private Sub UserControl_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    m_Pressed = False
    If Not m_Button_Index = -1 Then
        RedrawControl
        RaiseEvent MouseUp(m_Button_Index, Button, Shift, X, Y)
    End If
End Sub

'Load property values from storage
Private Sub UserControl_ReadProperties(PropBag As PropertyBag)
    
    'debug.Print "Reading properties..."
    m_Appearance = PropBag.ReadProperty("Appearance", m_def_Appearance)
    m_BackColor = PropBag.ReadProperty("BackColor", m_def_BackColor)
    m_BorderStyle = PropBag.ReadProperty("BorderStyle", m_def_BorderStyle)
    m_Enabled = PropBag.ReadProperty("Enabled", m_def_Enabled)
    m_ForeColor = PropBag.ReadProperty("ForeColor", m_def_ForeColor)
    m_Button_Count = PropBag.ReadProperty("Button_Count", m_def_Button_Count)
    m_Button_Index = PropBag.ReadProperty("Button_Index", m_def_Button_Index)
    m_ButtonsWidth = PropBag.ReadProperty("ButtonsWidth", m_def_ButtonsWidth)
    m_ButtonsHeight = PropBag.ReadProperty("ButtonsHeight", m_def_ButtonsHeight)
    m_ButtonsPerRow = PropBag.ReadProperty("ButtonsPerRow", m_def_ButtonsPerRow)
    m_HoverColor = PropBag.ReadProperty("HoverColor", m_def_HoverColor)
    m_TooTipStyle = PropBag.ReadProperty("TooTipStyle", m_def_TooTipStyle)
    m_ToolTipBackCol = PropBag.ReadProperty("ToolTipBackCol", m_def_ToolTipBackCol)
    m_ToolTipForeCol = PropBag.ReadProperty("ToolTipForeCol", m_def_ToolTipForeCol)
    m_BackGradient = PropBag.ReadProperty("BackGradient", m_def_BackGradient)
    m_BackGradientCol = PropBag.ReadProperty("BackGradientCol", m_def_BackGradientCol)
    m_ButtonsStyle = PropBag.ReadProperty("ButtonsStyle", m_def_ButtonsStyle)
    m_BorderColor = PropBag.ReadProperty("BorderColor", m_def_BorderColor)
    m_HoverIconShadow = PropBag.ReadProperty("HoverIconShadow", m_def_HoverIconShadow)
    m_ButtonsSeperatorWidth = PropBag.ReadProperty("ButtonsSeperatorWidth", m_def_ButtonsSeperatorWidth)
    m_ShowSeperator = PropBag.ReadProperty("ShowSeperator", m_def_ShowSeperator)

    Set m_Font = PropBag.ReadProperty("Font", Ambient.Font)
    Set m_BackGround = PropBag.ReadProperty("BackGround", Nothing)
    
    Dim X  As Long
    ReDim m_ButtonItem(m_Button_Count - 1)
    For X = 0 To m_Button_Count - 1
        m_ButtonItem(X).TB_Caption = PropBag.ReadProperty("ButtonCaption" & X, m_def_ButtonCaption)
        Set m_ButtonItem(X).TB_Icon = PropBag.ReadProperty("ButtonIcon" & X, Nothing)
        m_ButtonItem(X).TB_ToolTipText = PropBag.ReadProperty("ButtonToolTipText" & X, vbNullString)
        m_ButtonItem(X).TB_ToolTipIcon = PropBag.ReadProperty("ButtonToolTipIcon" & X, 0)
        m_ButtonItem(X).TB_Enabled = PropBag.ReadProperty("ButtonEnabled" & X, m_def_ButtonEnabled)
        m_ButtonItem(X).TB_Pressed = PropBag.ReadProperty("ButtonPressed" & X, m_def_ButtonPressed)
        m_ButtonItem(X).TB_IconAllignment = PropBag.ReadProperty("ButtonIconAllignment" & X, m_def_ButtonIconAllignment)
        m_ButtonItem(X).TB_Type = PropBag.ReadProperty("Button_Type" & X, 0)
    Next X
    
    'debug.Print "Completed reading properties!"
    
    If Ambient.UserMode Then m_Button_Index = -1 Else m_Button_Index = 0
    InitializeSubClassing
    RedrawControl

End Sub

Private Sub UserControl_Resize()
    m_BackDrawn = False
    RedrawControl
End Sub

Private Sub UserControl_Terminate()
On Error GoTo Catch
    'Stop all subclassing
    Call Subclass_Stop(hwnd)
    Call Subclass_StopAll
    FreeLibrary m_hMode
Catch:
End Sub

'Write property values to storage
Private Sub UserControl_WriteProperties(PropBag As PropertyBag)

    Call PropBag.WriteProperty("Appearance", m_Appearance, m_def_Appearance)
    Call PropBag.WriteProperty("BackColor", m_BackColor, m_def_BackColor)
    Call PropBag.WriteProperty("BorderStyle", m_BorderStyle, m_def_BorderStyle)
    Call PropBag.WriteProperty("Enabled", m_Enabled, m_def_Enabled)
    Call PropBag.WriteProperty("Font", m_Font, Ambient.Font)
    Call PropBag.WriteProperty("ForeColor", m_ForeColor, m_def_ForeColor)
    Call PropBag.WriteProperty("Button_Count", m_Button_Count, m_def_Button_Count)
    Call PropBag.WriteProperty("Button_Index", m_Button_Index, m_def_Button_Index)
    Call PropBag.WriteProperty("BackGround", m_BackGround, Nothing)
    Call PropBag.WriteProperty("ButtonsWidth", m_ButtonsWidth, m_def_ButtonsWidth)
    Call PropBag.WriteProperty("ButtonsHeight", m_ButtonsHeight, m_def_ButtonsHeight)
    Call PropBag.WriteProperty("ButtonsPerRow", m_ButtonsPerRow, m_def_ButtonsPerRow)
    Call PropBag.WriteProperty("HoverColor", m_HoverColor, m_def_HoverColor)
    Call PropBag.WriteProperty("TooTipStyle", m_TooTipStyle, m_def_TooTipStyle)
    Call PropBag.WriteProperty("ToolTipBackCol", m_ToolTipBackCol, m_def_ToolTipBackCol)
    Call PropBag.WriteProperty("ToolTipForeCol", m_ToolTipForeCol, m_def_ToolTipForeCol)
    Call PropBag.WriteProperty("BackGradient", m_BackGradient, m_def_BackGradient)
    Call PropBag.WriteProperty("BackGradientCol", m_BackGradientCol, m_def_BackGradientCol)
    Call PropBag.WriteProperty("ButtonsStyle", m_ButtonsStyle, m_def_ButtonsStyle)
    Call PropBag.WriteProperty("BorderColor", m_BorderColor, m_def_BorderColor)
    Call PropBag.WriteProperty("HoverIconShadow", m_HoverIconShadow, m_def_HoverIconShadow)
    Call PropBag.WriteProperty("ButtonsSeperatorWidth", m_ButtonsSeperatorWidth, m_def_ButtonsSeperatorWidth)
    Call PropBag.WriteProperty("ShowSeperator", m_ShowSeperator, m_def_ShowSeperator)

    Dim X As Long
    For X = 0 To m_Button_Count - 1
        Call PropBag.WriteProperty("ButtonCaption" & X, m_ButtonItem(X).TB_Caption, m_def_ButtonCaption)
        Call PropBag.WriteProperty("ButtonIcon" & X, m_ButtonItem(X).TB_Icon, Nothing)
        Call PropBag.WriteProperty("ButtonToolTipText" & X, m_ButtonItem(X).TB_ToolTipText, vbNullString)
        Call PropBag.WriteProperty("ButtonToolTipIcon" & X, m_ButtonItem(X).TB_ToolTipIcon, 0)
        Call PropBag.WriteProperty("ButtonEnabled" & X, m_ButtonItem(X).TB_Enabled, m_def_ButtonEnabled)
        Call PropBag.WriteProperty("ButtonPressed" & X, m_ButtonItem(X).TB_Pressed, m_def_ButtonPressed)
        Call PropBag.WriteProperty("ButtonIconAllignment" & X, m_ButtonItem(X).TB_IconAllignment, m_def_ButtonIconAllignment)
        Call PropBag.WriteProperty("Button_Type" & X, m_ButtonItem(X).TB_Type, 0)
    Next X
    
End Sub

Private Sub InitializeSubClassing()
On Error GoTo handle
    
    ' Subclass in runtime
    If Ambient.UserMode Then
    
    bTrack = True
    bTrackUser32 = IsFunctionExported("TrackMouseEvent", "User32")
  
    If Not bTrackUser32 Then
      If Not IsFunctionExported("_TrackMouseEvent", "Comctl32") Then
        bTrack = False
      End If
    End If
    
    If Not bTrack Then Exit Sub
    
        With UserControl
            
            ' Start subclassing our calendar
            Call Subclass_Start(.hwnd)
            
            ' Adding the messages we need to track
            Call Subclass_AddMsg(.hwnd, WM_MOUSEMOVE, MSG_AFTER)
            Call Subclass_AddMsg(.hwnd, WM_MOUSELEAVE, MSG_AFTER)
            Call Subclass_AddMsg(.hwnd, WM_TIMER, MSG_AFTER)
            
        End With
    
    End If
    
handle:
End Sub


'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : SplitToLines
' Auther    : Jim Jose
' Input     : Object, Text to split an parameters
' OutPut    : Splitted text array
' Purpose   : Split a string into lines by length!
'------------------------------------------------------------------------------------------------------------------------------------------

Private Function SplitToLines(ByVal sText As String, _
                                ByVal lLength As Long, _
                                Optional ByVal bFilterLines As Boolean = True) As String()
 Dim mArray() As String
 Dim mChar As String
 Dim mLine As String
 Dim lnCount As Long
 Dim xMax As String
 Dim mPos As Long
 Dim X As Long
 Dim lDone As Long
 Dim xStart As Long
    
    
    If bFilterLines Then sText = Replace(sText, vbNewLine, vbNullString)
    xMax = Len(sText)
    If TextWidth(sText) < lLength Then
        mLine = sText
        xStart = xMax - 1
    End If
    
    For X = xStart + 1 To xMax
    
        mChar = Mid(sText, X, 1)

        If IsDelim(mChar) Then mPos = X - (lDone + 1)
        If TextWidth(mLine & mChar) >= lLength Or X = xMax Then
            If mPos = 0 Then mPos = X - (lDone + 1)
            ReDim Preserve mArray(lnCount)
            mArray(lnCount) = RTrim(LTrim(Mid(mLine, 1, mPos)))
            mLine = Mid(mLine, mPos + 1, Len(mLine) - mPos)
            lDone = lDone + mPos: mPos = 0
            lnCount = lnCount + 1
        End If
        
        mLine = mLine & mChar
        
    Next X

    mArray(lnCount - 1) = mArray(lnCount - 1) & mChar
Complete:
    SplitToLines = mArray
    
End Function


'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : IsDelim
' Auther    : Rde
' Input     : Char
' OutPut    : IsDelim?
' Purpose   : Check if the input char is a Delimiter or not!
'------------------------------------------------------------------------------------------------------------------------------------------

Private Function IsDelim(Char As String) As Boolean
    Select Case Asc(Char) ' Upper/Lowercase letters,Underscore Not delimiters
    Case 65 To 90, 95, 97 To 122
        IsDelim = False
    Case Else: IsDelim = True ' Another Character Is delimiter
    End Select
End Function


'------------------------------------------------------------------------------------------
' Procedure  : IsThere
' Auther     : Jim Jose
' Input      : None
' OutPut     : None
' Purpose    : To check if the Picture is loaded
'------------------------------------------------------------------------------------------

Private Function IsThere(vPicture As StdPicture) As Boolean
On Error GoTo handle
     IsThere = Not vPicture Is Nothing
handle:
End Function


'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : IsNT
' Auther    : Dana Seaman
' Input     : None
' OutPut    : NT?
' Purpose   : Check for the NT Platform
'------------------------------------------------------------------------------------------------------------------------------------------

Private Function IsNT() As Boolean

  Dim udtVer     As OSVERSIONINFO
  On Error Resume Next
    udtVer.dwOSVersionInfoSize = Len(udtVer)
    If GetVersionEx(udtVer) Then
      m_bIsNT = udtVer.dwPlatformId = VER_PLATFORM_WIN32_NT
    End If
  On Error GoTo 0
   
End Function

' -------------------------------------------------------------------------------------
' Procedure : BlendColor
' Type      : Property
' DateTime  : 03/02/2005
' Author    : Gary Noble [ Modified by CodeFixer4! ]
' Purpose   : Blends Two Colours Together
' Returns   : Long
' -------------------------------------------------------------------------------------

Private Function BlendColor(ByVal oColorFrom As OLE_COLOR, _
                               ByVal oColorTo As OLE_COLOR, _
                               Optional ByVal Alpha As Long = 128) As Long
Dim lCFrom As Long
Dim lCTo   As Long
    lCFrom = TranslateColor(oColorFrom)
    lCTo = TranslateColor(oColorTo)
    BlendColor = RGB((((lCFrom And &HFF) * Alpha) / 255) + (((lCTo And &HFF) * (255 - Alpha)) / 255), ((((lCFrom And &HFF00&) \ &H100&) * Alpha) / 255) + ((((lCTo And &HFF00&) \ &H100&) * (255 - Alpha)) / 255), ((((lCFrom And &HFF0000) \ &H10000) * Alpha) / 255) + ((((lCTo And &HFF0000) \ &H10000) * (255 - Alpha)) / 255))

End Function

' -------------------------------------------------------------------------------------
' Procedure : TranslateColor
' Type      : Function
' DateTime  : 03/02/2005
' Author    : Roger
' Purpose   : Convert Automation color to Windows color
' Returns   : Long
' -------------------------------------------------------------------------------------

Private Function TranslateColor(ByVal oClr As OLE_COLOR, _
                               Optional hPal As Long = 0) As Long

    OleTranslateColor oClr, hPal, TranslateColor

End Function


'[Important. If not included, tooltips don't change when you try to set the toltip text]
Private Sub RemoveToolTip()
   Dim lR As Long
   If m_ToolTipHwnd <> 0 Then
      lR = SendMessage(m_ToolTipInfo.lhWnd, TTM_DELTOOLW, 0, m_ToolTipInfo)
      DestroyWindow m_ToolTipHwnd
      m_ToolTipHwnd = 0
   End If
End Sub

'-------------------------------------------------------------------------------------------------------------------------
' Procedure : CreateToolTip
' Auther    : Fred.cpp
' Modified  : Jim Jose
' Upgraded  : Dana Seaman, for unicode support
' Purpose   : Simple and efficient tooltip generation with baloon style
'-------------------------------------------------------------------------------------------------------------------------

Private Sub CreateToolTip()
Dim lpRect As RECT
Dim lWinStyle As Long

    'Remove previous ToolTip
    RemoveToolTip
    
    If m_Button_Index = -1 Then Exit Sub
    If m_ButtonItem(m_Button_Index).TB_ToolTipText = vbNullString Then Exit Sub
    'debug.Print "Creating new Tooltip!"

    ''create baloon style if desired
    If m_TooTipStyle = Tip_Normal Then
        lWinStyle = TTS_ALWAYSTIP Or TTS_NOPREFIX
    Else
        lWinStyle = TTS_ALWAYSTIP Or TTS_NOPREFIX Or TTS_BALLOON
    End If
        
    m_ToolTipHwnd = CreateWindowEx(0&, _
                TOOLTIPS_CLASSA, _
                vbNullString, _
                lWinStyle, _
                CW_USEDEFAULT, _
                CW_USEDEFAULT, _
                CW_USEDEFAULT, _
                CW_USEDEFAULT, _
                hwnd, _
                0&, _
                App.hInstance, _
                0&)
                
    ''make our tooltip window a topmost window
    SetWindowPos m_ToolTipHwnd, _
        HWND_TOPMOST, _
        0&, _
        0&, _
        0&, _
        0&, _
        SWP_NOACTIVATE Or SWP_NOSIZE Or SWP_NOMOVE
    
    ''get the rect of the parent control
    GetClientRect hwnd, lpRect
    
    ''now set our tooltip info structure
    With m_ToolTipInfo
        .lSize = Len(m_ToolTipInfo)
        .lFlags = TTF_SUBCLASS
        .lhWnd = hwnd
        .lId = 0
        .hInstance = App.hInstance
        .lpStr = StrPtr(m_ButtonItem(m_Button_Index).TB_ToolTipText)
        .lpRect = lpRect
    End With
    
    ''add the tooltip structure
    SendMessage m_ToolTipHwnd, TTM_ADDTOOLW, 0&, m_ToolTipInfo

    ''if we want a title or we want an icon
    SendMessage m_ToolTipHwnd, TTM_SETTIPTEXTCOLOR, TranslateColor(m_ToolTipForeCol), 0&
    SendMessage m_ToolTipHwnd, TTM_SETTIPBKCOLOR, TranslateColor(m_ToolTipBackCol), 0&
    SendMessage m_ToolTipHwnd, TTM_SETTITLEW, m_ButtonItem(m_Button_Index).TB_ToolTipIcon, ByVal StrPtr(m_ButtonItem(m_Button_Index).TB_Caption)
    
Exit Sub
handle:
   'debug.Print "Error " & Err.Description
End Sub


'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : FillGradient
' Auther    : Jim Jose
' Input     : Hdc + Parameters
' OutPut    : None
' Purpose   : Middleout Gradients with Carls's DIB solution
'------------------------------------------------------------------------------------------------------------------------------------------

Private Sub FillGradient(ByVal hdc As Long, _
                         ByVal X As Long, _
                         ByVal Y As Long, _
                         ByVal Width As Long, _
                         ByVal Height As Long, _
                         ByVal Col1 As Long, _
                         ByVal Col2 As Long, _
                         ByVal GradientDirection As GradientDirectionEnum, _
                         Optional Right2Left As Boolean = True)
                         
Dim tmpCol  As Long
  
    ' Exit if needed
    If GradientDirection = Fill_None Then Exit Sub
    
    ' Right-To-Left
    If Right2Left Then
        tmpCol = Col1
        Col1 = Col2
        Col2 = tmpCol
    End If
    
    ' Translate system colors
    Col1 = TranslateColor(Col1)
    Col2 = TranslateColor(Col2)
    
    Select Case GradientDirection
        Case Fill_HorizontalMiddleOut
            DIBGradient hdc, X, Y, Width / 2, Height, Col1, Col2, Fill_Horizontal
            DIBGradient hdc, X + Width / 2 - 1, Y, Width / 2, Height, Col2, Col1, Fill_Horizontal

        Case Fill_VerticalMiddleOut
            DIBGradient hdc, X, Y, Width, Height / 2, Col1, Col2, Fill_Vertical
            DIBGradient hdc, X, Y + Height / 2 - 1, Width, Height / 2 + 1, Col2, Col1, Fill_Vertical

        Case Else
            DIBGradient hdc, X, Y, Width, Height, Col1, Col2, GradientDirection
    End Select
    
End Sub

'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : DIBGradient
' Auther    : Carls P.V.
' Input     : Hdc + Parameters
' OutPut    : None
' Purpose   : DIB solution for fast gradients
'------------------------------------------------------------------------------------------------------------------------------------------

Private Sub DIBGradient(ByVal hdc As Long, _
                         ByVal X As Long, _
                         ByVal Y As Long, _
                         ByVal vWidth As Long, _
                         ByVal vHeight As Long, _
                         ByVal Col1 As Long, _
                         ByVal Col2 As Long, _
                         ByVal GradientDirection As GradientDirectionEnum)

  Dim uBIH    As BITMAPINFOHEADER
  Dim lBits() As Long
  Dim lGrad() As Long
  
  Dim R1      As Long
  Dim G1      As Long
  Dim B1      As Long
  Dim R2      As Long
  Dim G2      As Long
  Dim B2      As Long
  Dim dR      As Long
  Dim dG      As Long
  Dim dB      As Long
  
  Dim Scan    As Long
  Dim i       As Long
  Dim iEnd    As Long
  Dim iOffset As Long
  Dim j       As Long
  Dim jEnd    As Long
  Dim iGrad   As Long
  
    '-- A minor check
    If (vWidth < 1 Or vHeight < 1) Then Exit Sub
    
    '-- Decompose Cols'
    R1 = (Col1 And &HFF&)
    G1 = (Col1 And &HFF00&) \ &H100&
    B1 = (Col1 And &HFF0000) \ &H10000
    R2 = (Col2 And &HFF&)
    G2 = (Col2 And &HFF00&) \ &H100&
    B2 = (Col2 And &HFF0000) \ &H10000

    '-- Get Col distances
    dR = R2 - R1
    dG = G2 - G1
    dB = B2 - B1
    
    '-- Size gradient-Cols array
    Select Case GradientDirection
        Case [Fill_Horizontal]
            ReDim lGrad(0 To vWidth - 1)
        Case [Fill_Vertical]
            ReDim lGrad(0 To vHeight - 1)
        Case Else
            ReDim lGrad(0 To vWidth + vHeight - 2)
    End Select
    
    '-- Calculate gradient-Cols
    iEnd = UBound(lGrad())
    If (iEnd = 0) Then
        '-- Special case (1-pixel wide gradient)
        lGrad(0) = (B1 \ 2 + B2 \ 2) + 256 * (G1 \ 2 + G2 \ 2) + 65536 * (R1 \ 2 + R2 \ 2)
      Else
        For i = 0 To iEnd
            lGrad(i) = B1 + (dB * i) \ iEnd + 256 * (G1 + (dG * i) \ iEnd) + 65536 * (R1 + (dR * i) \ iEnd)
        Next i
    End If
    
    '-- Size DIB array
    ReDim lBits(vWidth * vHeight - 1) As Long
    iEnd = vWidth - 1
    jEnd = vHeight - 1
    Scan = vWidth
    
    '-- Render gradient DIB
    Select Case GradientDirection
        
        Case [Fill_Horizontal]
        
            For j = 0 To jEnd
                For i = iOffset To iEnd + iOffset
                    lBits(i) = lGrad(i - iOffset)
                Next i
                iOffset = iOffset + Scan
            Next j
        
        Case [Fill_Vertical]
        
            For j = jEnd To 0 Step -1
                For i = iOffset To iEnd + iOffset
                    lBits(i) = lGrad(j)
                Next i
                iOffset = iOffset + Scan
            Next j
            
        Case [Fill_DownwardDiagonal]
            
            iOffset = jEnd * Scan
            For j = 1 To jEnd + 1
                For i = iOffset To iEnd + iOffset
                    lBits(i) = lGrad(iGrad)
                    iGrad = iGrad + 1
                Next i
                iOffset = iOffset - Scan
                iGrad = j
            Next j
            
        Case [Fill_UpwardDiagonal]
            
            iOffset = 0
            For j = 1 To jEnd + 1
                For i = iOffset To iEnd + iOffset
                    lBits(i) = lGrad(iGrad)
                    iGrad = iGrad + 1
                Next i
                iOffset = iOffset + Scan
                iGrad = j
            Next j
    End Select
    
    '-- Define DIB header
    With uBIH
        .biSize = 40
        .biPlanes = 1
        .biBitCount = 32
        .biWidth = vWidth
        .biHeight = vHeight
    End With
    
    '-- Paint it!
    Call StretchDIBits(hdc, X, Y, vWidth, vHeight, 0, 0, vWidth, vHeight, lBits(0), uBIH, DIB_RGB_ColS, vbSrcCopy)

End Sub


'------------------------------------------------------------------------------------------------------------------------------------------
' Procedure : TileBitmap
' Auther    : Carls P.V.
' Input     : Hdc + Parameters
' OutPut    : None
' Purpose   : Draw tiled picture to a DC
'------------------------------------------------------------------------------------------------------------------------------------------

Private Function TileBitmap(Picture As StdPicture, ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Boolean

 Dim tBI          As BITMAP
 Dim tBIH         As BITMAPINFOHEADER
 Dim Buff()       As Byte 'Packed DIB
 Dim lhDC         As Long
 Dim lhOldBmp     As Long
 Dim TileRect     As RECT
 Dim PtOrg        As POINTAPI
 Dim m_hBrush     As Long

   If (GetObjectType(Picture) = 7) Then

'     -- Get image info
      GetObject Picture, Len(tBI), tBI

'     -- Prepare DIB header and redim. Buff() array
      With tBIH
         .biSize = Len(tBIH) '40
         .biPlanes = 1
         .biBitCount = 24
         .biWidth = tBI.bmWidth
         .biHeight = tBI.bmHeight
         .biSizeImage = ((.biWidth * 3 + 3) And &HFFFFFFFC) * .biHeight
      End With
      ReDim Buff(1 To Len(tBIH) + tBIH.biSizeImage) '[Header + Bits]

'     -- Create DIB brush
      lhDC = CreateCompatibleDC(0)
      If (lhDC <> 0) Then
         lhOldBmp = SelectObject(lhDC, Picture)

'        -- Build packed DIB:
'        - Merge Header
         CopyMemory Buff(1), tBIH, Len(tBIH)
'        - Get and merge DIB Bits
         GetDIBits lhDC, Picture, 0, tBI.bmHeight, Buff(Len(tBIH) + 1), tBIH, 0

         SelectObject lhDC, lhOldBmp
         DeleteDC lhDC

'        -- Create brush from packed DIB
         m_hBrush = CreateDIBPatternBrushPt(Buff(1), 0)
      End If

   End If

   If (m_hBrush <> 0) Then
   
      SetRect TileRect, X1, Y1, X2, Y2
      SetBrushOrgEx hdc, X1, Y1, PtOrg
'     -- Tile image
      FillRect hdc, TileRect, m_hBrush

      DeleteObject m_hBrush
      m_hBrush = 0
   
   End If
   
End Function


'---------------------------------------------------------------------------------------------------------------------------------------------
' The following bytes are donated exclusively for Paul Caton's Subclassing
' We need this to track the movement information of the m_picCalendar and
' sizing/positioning of parent form
'---------------------------------------------------------------------------------------------------------------------------------------------
' Auther    : Paul Caton
' Purpose   : Advanced subclassing for UserControls (Self subclasser)
' Comment   : Thanks a Billion for this ever green piece of code on subclassing!!!
'---------------------------------------------------------------------------------------------------------------------------------------------

'Add a message to the table of those that will invoke a callback. You should Subclass_Start first and then add the messages
Private Sub Subclass_AddMsg(ByVal lng_hWnd As Long, ByVal uMsg As Long, Optional ByVal When As eMsgWhen = MSG_AFTER)
'Parameters:
  'lng_hWnd  - The handle of the window for which the uMsg is to be added to the callback table
  'uMsg      - The message number that will invoke a callback. NB Can also be ALL_MESSAGES, ie all messages will callback
  'When      - Whether the msg is to callback before, after or both with respect to the the default (previous) handler
  With sc_aSubData(zIdx(lng_hWnd))
    If When And eMsgWhen.MSG_BEFORE Then
      Call zAddMsg(uMsg, .aMsgTblB, .nMsgCntB, eMsgWhen.MSG_BEFORE, .nAddrSub)
    End If
    If When And eMsgWhen.MSG_AFTER Then
      Call zAddMsg(uMsg, .aMsgTblA, .nMsgCntA, eMsgWhen.MSG_AFTER, .nAddrSub)
    End If
  End With
End Sub

'Delete a message from the table of those that will invoke a callback.
Private Sub Subclass_DelMsg(ByVal lng_hWnd As Long, ByVal uMsg As Long, Optional ByVal When As eMsgWhen = MSG_AFTER)
'Parameters:
  'lng_hWnd  - The handle of the window for which the uMsg is to be removed from the callback table
  'uMsg      - The message number that will be removed from the callback table. NB Can also be ALL_MESSAGES, ie all messages will callback
  'When      - Whether the msg is to be removed from the before, after or both callback tables
  With sc_aSubData(zIdx(lng_hWnd))
    If When And eMsgWhen.MSG_BEFORE Then
      Call zDelMsg(uMsg, .aMsgTblB, .nMsgCntB, eMsgWhen.MSG_BEFORE, .nAddrSub)
    End If
    If When And eMsgWhen.MSG_AFTER Then
      Call zDelMsg(uMsg, .aMsgTblA, .nMsgCntA, eMsgWhen.MSG_AFTER, .nAddrSub)
    End If
  End With
End Sub

'Return whether we're running in the IDE.
Private Function Subclass_InIDE() As Boolean
  'debug.Assert zSetTrue(Subclass_InIDE)
End Function

'Start subclassing the passed window handle
Private Function Subclass_Start(ByVal lng_hWnd As Long) As Long
'Parameters:
  'lng_hWnd  - The handle of the window to be subclassed
'Returns;
  'The sc_aSubData() index
  Const CODE_LEN              As Long = 200                                             'Length of the machine code in bytes
  Const FUNC_CWP              As String = "CallWindowProcA"                             'We use CallWindowProc to call the original WndProc
  Const FUNC_EBM              As String = "EbMode"                                      'VBA's EbMode function allows the machine code thunk to know if the IDE has stopped or is on a breakpoint
  Const FUNC_SWL              As String = "SetWindowLongA"                              'SetWindowLongA allows the cSubclasser machine code thunk to unsubclass the subclasser itself if it detects via the EbMode function that the IDE has stopped
  Const MOD_USER              As String = "user32"                                      'Location of the SetWindowLongA & CallWindowProc functions
  Const MOD_VBA5              As String = "vba5"                                        'Location of the EbMode function if running VB5
  Const MOD_VBA6              As String = "vba6"                                        'Location of the EbMode function if running VB6
  Const PATCH_01              As Long = 18                                              'Code buffer offset to the location of the relative address to EbMode
  Const PATCH_02              As Long = 68                                              'Address of the previous WndProc
  Const PATCH_03              As Long = 78                                              'Relative address of SetWindowsLong
  Const PATCH_06              As Long = 116                                             'Address of the previous WndProc
  Const PATCH_07              As Long = 121                                             'Relative address of CallWindowProc
  Const PATCH_0A              As Long = 186                                             'Address of the owner object
  Static aBuf(1 To CODE_LEN)  As Byte                                                   'Static code buffer byte array
  Static pCWP                 As Long                                                   'Address of the CallWindowsProc
  Static pEbMode              As Long                                                   'Address of the EbMode IDE break/stop/running function
  Static pSWL                 As Long                                                   'Address of the SetWindowsLong function
  Dim i                       As Long                                                   'Loop index
  Dim j                       As Long                                                   'Loop index
  Dim nSubIdx                 As Long                                                   'Subclass data index
  Dim sHex                    As String                                                 'Hex code string
  
'If it's the first time through here..
  If aBuf(1) = 0 Then
  
'The hex pair machine code representation.
    sHex = "5589E583C4F85731C08945FC8945F8EB0EE80000000083F802742185C07424E830000000837DF800750AE838000000E84D00" & _
           "00005F8B45FCC9C21000E826000000EBF168000000006AFCFF7508E800000000EBE031D24ABF00000000B900000000E82D00" & _
           "0000C3FF7514FF7510FF750CFF75086800000000E8000000008945FCC331D2BF00000000B900000000E801000000C3E33209" & _
           "C978078B450CF2AF75278D4514508D4510508D450C508D4508508D45FC508D45F85052B800000000508B00FF90A4070000C3"

'Convert the string from hex pairs to bytes and store in the static machine code buffer
    i = 1
    Do While j < CODE_LEN
      j = j + 1
      aBuf(j) = Val("&H" & Mid$(sHex, i, 2))                                            'Convert a pair of hex characters to an eight-bit value and store in the static code buffer array
      i = i + 2
    Loop                                                                                'Next pair of hex characters
    
'Get API function addresses
    If Subclass_InIDE Then                                                              'If we're running in the VB IDE
      aBuf(16) = &H90                                                                   'Patch the code buffer to enable the IDE state code
      aBuf(17) = &H90                                                                   'Patch the code buffer to enable the IDE state code
      pEbMode = zAddrFunc(MOD_VBA6, FUNC_EBM)                                           'Get the address of EbMode in vba6.dll
      If pEbMode = 0 Then                                                               'Found?
        pEbMode = zAddrFunc(MOD_VBA5, FUNC_EBM)                                         'VB5 perhaps
      End If
    End If
    
    pCWP = zAddrFunc(MOD_USER, FUNC_CWP)                                                'Get the address of the CallWindowsProc function
    pSWL = zAddrFunc(MOD_USER, FUNC_SWL)                                                'Get the address of the SetWindowLongA function
    ReDim sc_aSubData(0 To 0) As tSubData                                               'Create the first sc_aSubData element
  Else
    nSubIdx = zIdx(lng_hWnd, True)
    If nSubIdx = -1 Then                                                                'If an sc_aSubData element isn't being re-cycled
      nSubIdx = UBound(sc_aSubData()) + 1                                               'Calculate the next element
      ReDim Preserve sc_aSubData(0 To nSubIdx) As tSubData                              'Create a new sc_aSubData element
    End If
    
    Subclass_Start = nSubIdx
  End If

  With sc_aSubData(nSubIdx)
    .hwnd = lng_hWnd                                                                    'Store the hWnd
    .nAddrSub = GlobalAlloc(GMEM_FIXED, CODE_LEN)                                       'Allocate memory for the machine code WndProc
    .nAddrOrig = SetWindowLongA(.hwnd, GWL_WNDPROC, .nAddrSub)                          'Set our WndProc in place
    Call RtlMoveMemory(ByVal .nAddrSub, aBuf(1), CODE_LEN)                              'Copy the machine code from the static byte array to the code array in sc_aSubData
    Call zPatchRel(.nAddrSub, PATCH_01, pEbMode)                                        'Patch the relative address to the VBA EbMode api function, whether we need to not.. hardly worth testing
    Call zPatchVal(.nAddrSub, PATCH_02, .nAddrOrig)                                     'Original WndProc address for CallWindowProc, call the original WndProc
    Call zPatchRel(.nAddrSub, PATCH_03, pSWL)                                           'Patch the relative address of the SetWindowLongA api function
    Call zPatchVal(.nAddrSub, PATCH_06, .nAddrOrig)                                     'Original WndProc address for SetWindowLongA, unsubclass on IDE stop
    Call zPatchRel(.nAddrSub, PATCH_07, pCWP)                                           'Patch the relative address of the CallWindowProc api function
    Call zPatchVal(.nAddrSub, PATCH_0A, ObjPtr(Me))                                     'Patch the address of this object instance into the static machine code buffer
  End With
End Function

'Stop all subclassing
Private Sub Subclass_StopAll()
  Dim i As Long
  
  i = UBound(sc_aSubData())                                                             'Get the upper bound of the subclass data array
  Do While i >= 0                                                                       'Iterate through each element
    With sc_aSubData(i)
      If .hwnd <> 0 Then                                                                'If not previously Subclass_Stop'd
        Call Subclass_Stop(.hwnd)                                                       'Subclass_Stop
      End If
    End With
    
    i = i - 1                                                                           'Next element
  Loop
End Sub

'Stop subclassing the passed window handle
Private Sub Subclass_Stop(ByVal lng_hWnd As Long)
'Parameters:
  'lng_hWnd  - The handle of the window to stop being subclassed
  With sc_aSubData(zIdx(lng_hWnd))
    Call SetWindowLongA(.hwnd, GWL_WNDPROC, .nAddrOrig)                                 'Restore the original WndProc
    Call zPatchVal(.nAddrSub, PATCH_05, 0)                                              'Patch the Table B entry count to ensure no further 'before' callbacks
    Call zPatchVal(.nAddrSub, PATCH_09, 0)                                              'Patch the Table A entry count to ensure no further 'after' callbacks
    Call GlobalFree(.nAddrSub)                                                          'Release the machine code memory
    .hwnd = 0                                                                           'Mark the sc_aSubData element as available for re-use
    .nMsgCntB = 0                                                                       'Clear the before table
    .nMsgCntA = 0                                                                       'Clear the after table
    Erase .aMsgTblB                                                                     'Erase the before table
    Erase .aMsgTblA                                                                     'Erase the after table
  End With
End Sub

'Worker sub for Subclass_AddMsg
Private Sub zAddMsg(ByVal uMsg As Long, ByRef aMsgTbl() As Long, ByRef nMsgCnt As Long, ByVal When As eMsgWhen, ByVal nAddr As Long)
  Dim nEntry  As Long                                                                   'Message table entry index
  Dim nOff1   As Long                                                                   'Machine code buffer offset 1
  Dim nOff2   As Long                                                                   'Machine code buffer offset 2
  
  If uMsg = ALL_MESSAGES Then                                                           'If all messages
    nMsgCnt = ALL_MESSAGES                                                              'Indicates that all messages will callback
  Else                                                                                  'Else a specific message number
    Do While nEntry < nMsgCnt                                                           'For each existing entry. NB will skip if nMsgCnt = 0
      nEntry = nEntry + 1
      
      If aMsgTbl(nEntry) = 0 Then                                                       'This msg table slot is a deleted entry
        aMsgTbl(nEntry) = uMsg                                                          'Re-use this entry
        Exit Sub                                                                        'Bail
      ElseIf aMsgTbl(nEntry) = uMsg Then                                                'The msg is already in the table!
        Exit Sub                                                                        'Bail
      End If
    Loop                                                                                'Next entry

    nMsgCnt = nMsgCnt + 1                                                               'New slot required, bump the table entry count
    ReDim Preserve aMsgTbl(1 To nMsgCnt) As Long                                        'Bump the size of the table.
    aMsgTbl(nMsgCnt) = uMsg                                                             'Store the message number in the table
  End If

  If When = eMsgWhen.MSG_BEFORE Then                                                    'If before
    nOff1 = PATCH_04                                                                    'Offset to the Before table
    nOff2 = PATCH_05                                                                    'Offset to the Before table entry count
  Else                                                                                  'Else after
    nOff1 = PATCH_08                                                                    'Offset to the After table
    nOff2 = PATCH_09                                                                    'Offset to the After table entry count
  End If

  If uMsg <> ALL_MESSAGES Then
    Call zPatchVal(nAddr, nOff1, VarPtr(aMsgTbl(1)))                                    'Address of the msg table, has to be re-patched because Redim Preserve will move it in memory.
  End If
  Call zPatchVal(nAddr, nOff2, nMsgCnt)                                                 'Patch the appropriate table entry count
End Sub

'Return the memory address of the passed function in the passed dll
Private Function zAddrFunc(ByVal sDLL As String, ByVal sProc As String) As Long
  zAddrFunc = GetProcAddress(GetModuleHandleA(sDLL), sProc)
  'debug.Assert zAddrFunc                                                                'You may wish to comment out this line if you're using vb5 else the EbMode GetProcAddress will stop here everytime because we look for vba6.dll first
End Function

'Worker sub for Subclass_DelMsg
Private Sub zDelMsg(ByVal uMsg As Long, ByRef aMsgTbl() As Long, ByRef nMsgCnt As Long, ByVal When As eMsgWhen, ByVal nAddr As Long)
  Dim nEntry As Long
  
  If uMsg = ALL_MESSAGES Then                                                           'If deleting all messages
    nMsgCnt = 0                                                                         'Message count is now zero
    If When = eMsgWhen.MSG_BEFORE Then                                                  'If before
      nEntry = PATCH_05                                                                 'Patch the before table message count location
    Else                                                                                'Else after
      nEntry = PATCH_09                                                                 'Patch the after table message count location
    End If
    Call zPatchVal(nAddr, nEntry, 0)                                                    'Patch the table message count to zero
  Else                                                                                  'Else deleteting a specific message
    Do While nEntry < nMsgCnt                                                           'For each table entry
      nEntry = nEntry + 1
      If aMsgTbl(nEntry) = uMsg Then                                                    'If this entry is the message we wish to delete
        aMsgTbl(nEntry) = 0                                                             'Mark the table slot as available
        Exit Do                                                                         'Bail
      End If
    Loop                                                                                'Next entry
  End If
End Sub

'Get the sc_aSubData() array index of the passed hWnd
Private Function zIdx(ByVal lng_hWnd As Long, Optional ByVal bAdd As Boolean = False) As Long
'Get the upper bound of sc_aSubData() - If you get an error here, you're probably Subclass_AddMsg-ing before Subclass_Start
  zIdx = UBound(sc_aSubData)
  Do While zIdx >= 0                                                                    'Iterate through the existing sc_aSubData() elements
    With sc_aSubData(zIdx)
      If .hwnd = lng_hWnd Then                                                          'If the hWnd of this element is the one we're looking for
        If Not bAdd Then                                                                'If we're searching not adding
          Exit Function                                                                 'Found
        End If
      ElseIf .hwnd = 0 Then                                                             'If this an element marked for reuse.
        If bAdd Then                                                                    'If we're adding
          Exit Function                                                                 'Re-use it
        End If
      End If
    End With
    zIdx = zIdx - 1                                                                     'Decrement the index
  Loop
  
  If Not bAdd Then
    'debug.Assert False                                                                  'hWnd not found, programmer error
  End If

'If we exit here, we're returning -1, no freed elements were found
End Function

'Patch the machine code buffer at the indicated offset with the relative address to the target address.
Private Sub zPatchRel(ByVal nAddr As Long, ByVal nOffset As Long, ByVal nTargetAddr As Long)
  Call RtlMoveMemory(ByVal nAddr + nOffset, nTargetAddr - nAddr - nOffset - 4, 4)
End Sub

'Patch the machine code buffer at the indicated offset with the passed value
Private Sub zPatchVal(ByVal nAddr As Long, ByVal nOffset As Long, ByVal nValue As Long)
  Call RtlMoveMemory(ByVal nAddr + nOffset, nValue, 4)
End Sub

'Worker function for Subclass_InIDE
Private Function zSetTrue(ByRef bValue As Boolean) As Boolean
  zSetTrue = True
  bValue = True
End Function

'Return the upper 16 bits of the passed 32 bit value
Private Function WordHi(lngValue As Long) As Long
  If (lngValue And &H80000000) = &H80000000 Then
    WordHi = ((lngValue And &H7FFF0000) \ &H10000) Or &H8000&
  Else
    WordHi = (lngValue And &HFFFF0000) \ &H10000
  End If
End Function

'Return the lower 16 bits of the passed 32 bit value
Private Function WordLo(lngValue As Long) As Long
  WordLo = (lngValue And &HFFFF&)
End Function

'Determine if the passed function is supported
Private Function IsFunctionExported(ByVal sFunction As String, ByVal sModule As String) As Boolean
  Dim hMod        As Long
  Dim bLibLoaded  As Boolean

  hMod = GetModuleHandleA(sModule)

  If hMod = 0 Then
    hMod = LoadLibraryA(sModule)
    If hMod Then
      bLibLoaded = True
    End If
  End If

  If hMod Then
    If GetProcAddress(hMod, sFunction) Then
      IsFunctionExported = True
    End If
  End If

  If bLibLoaded Then
    Call FreeLibrary(hMod)
  End If
End Function

'Track the mouse leaving the indicated window
Private Sub TrackMouseLeave(ByVal lng_hWnd As Long)
  Dim tme As TRACKMOUSEEVENT_STRUCT
  
  If bTrack Then
    With tme
      .cbSize = Len(tme)
      .dwFlags = TME_LEAVE
      .hwndTrack = lng_hWnd
    End With

    If bTrackUser32 Then
      Call TrackMouseEvent(tme)
    Else
      Call TrackMouseEventComCtl(tme)
    End If
  End If
End Sub


Private Sub RedrawControl()
Dim X As Long
Dim xMax As Long
Dim mArray As Boolean

    On Error GoTo handle
    'debug.Print "Redrawing..."
    xMax = Me.Button_Count
    UserControl.Cls
    UserControl.BackColor = m_BackColor
    Set UserControl.Font = m_Font
    
    If m_BorderStyle = BDR_None Then
        Height = m_ButtonsHeight * ((m_Button_Count - 1) \ m_ButtonsPerRow + 1) * 15
    Else
        Height = (m_ButtonsHeight * ((m_Button_Count - 1) \ m_ButtonsPerRow + 1) + 7) * 15
    End If

    'Draw the background only once and save it as picture
    'This can reduse redrawing effort/time in a large margin
    If Not m_BackDrawn Then
        Set UserControl.Picture = Nothing
        If IsThere(m_BackGround) And m_BackGradient = 0 Then
            TileBitmap m_BackGround, hdc, 0, 0, ScaleWidth, ScaleHeight
        ElseIf Not m_BackGradient = 0 And Not m_BackColor = m_BackGradientCol Then
            FillGradient hdc, 0, 0, ScaleWidth, ScaleHeight, m_BackColor, m_BackGradientCol, m_BackGradient, True
        End If
        UserControl.Picture = UserControl.Image
        m_BackDrawn = True
    End If
    
    If m_BorderStyle = BDR_InSet Then
        DrawBorder_Win98 m_BackColor, 0, 0, ScaleWidth, ScaleHeight, True, 0
    ElseIf m_BorderStyle = BDR_RAISED Then
        DrawBorder_Win98 m_BackColor, 0, 0, ScaleWidth, ScaleHeight, False, 0
    End If
    
    'Draw each button
    For X = 0 To xMax - 1
        With m_ButtonItem(X)
            If .TB_Type = TYP_Button Then
                If X = m_Button_Index And Not .TB_Pressed Then
                    DrawButton X, .TB_Icon, .TB_Caption, m_Pressed, .TB_Enabled
                Else
                    DrawButton X, .TB_Icon, .TB_Caption, .TB_Pressed, .TB_Enabled
                End If
            Else
                DrawSeperator X
            End If
        End With
    Next X
    
handle:
End Sub


Private Sub DrawButton(ByVal btnIndex As Long, _
                            ByRef mIcon As StdPicture, _
                            ByRef sCaption As String, _
                            ByVal bPressed As Boolean, _
                            ByVal bEnabled As Boolean)
 Dim lLeft As Long
 Dim lTop As Long
 Dim icnHeight As Long
 Dim icnWidth As Long
 Dim lStyle As Long
 Dim bHasIcon  As Boolean
 Dim icnLift As Long
 
        bHasIcon = IsThere(mIcon)
        GetButtonXY btnIndex, lLeft, lTop
        
        If bHasIcon Then
            icnHeight = ScaleY(mIcon.Height) + 5
            icnWidth = ScaleX(mIcon.Width) + 5
        End If
        If bEnabled Then lStyle = 1
        
        Select Case m_ButtonsStyle
            Case Style_Solid
                DrawBorder_Win98 m_BackColor, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight, bPressed, 1
            Case Style_Win9X
                If Not bPressed Or bPressed Then DrawBorder_Win98 m_BackColor, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight, bPressed, 2
            Case Style_Flat
                If btnIndex = m_Button_Index Or bPressed Then DrawBorder_Win98 m_BackColor, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight, bPressed, 0
            Case Style_OfficeXP
                If btnIndex = m_Button_Index Then
                    'changed
                    DrawBorder_OfficeXP m_HoverColor, lLeft + 1, lTop + 1, m_ButtonsWidth - 1, m_ButtonsHeight - 1, m_Pressed
                    'DrawBorder_WinXP m_HoverColor, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight, (btnIndex = m_Button_Index), m_Pressed And (m_Button_Index = btnIndex)
                    If Not m_Pressed Then icnLift = 0
                End If
            Case Style_WinXP
                DrawBorder_WinXP m_BackColor, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight, (btnIndex = m_Button_Index), m_Pressed And (m_Button_Index = btnIndex)
        End Select
        
        Select Case m_ButtonItem(btnIndex).TB_IconAllignment
            Case ALN_Bottom     'Icon at bottom
                If bHasIcon Then DrawIcon mIcon, lLeft - icnLift, lTop + m_ButtonsHeight - icnHeight - icnLift, m_ButtonsWidth, icnHeight - 10, lStyle
                DrawCaption sCaption, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight - icnHeight
            Case ALN_Left       'Icon at left
                If bHasIcon Then DrawIcon mIcon, lLeft - icnLift, lTop - icnLift, icnWidth + 10, m_ButtonsHeight, lStyle
                DrawCaption sCaption, lLeft + icnWidth, lTop, m_ButtonsWidth - icnWidth, m_ButtonsHeight
            Case ALN_Right      'Icon at Right
                If bHasIcon Then DrawIcon mIcon, lLeft + m_ButtonsWidth - icnWidth - icnLift, lTop - icnLift, icnWidth - 10, m_ButtonsHeight, lStyle
                DrawCaption sCaption, lLeft, lTop, m_ButtonsWidth - icnWidth, m_ButtonsHeight
            Case ALN_Top        'Icon On top
                If bHasIcon Then DrawIcon mIcon, lLeft - icnLift, lTop - icnLift, m_ButtonsWidth, icnHeight + 10, lStyle
                DrawCaption sCaption, lLeft, lTop + icnHeight, m_ButtonsWidth, m_ButtonsHeight - icnHeight
            Case ALN_Center     'Both Icon and caption at center
                If bHasIcon Then DrawIcon mIcon, lLeft - icnLift, lTop - icnLift, m_ButtonsWidth, m_ButtonsHeight, lStyle
                DrawCaption sCaption, lLeft, lTop, m_ButtonsWidth, m_ButtonsHeight
        End Select

    DrawButtonIndex lLeft, lTop, btnIndex
    
End Sub


Private Function GetButtonXY(ByVal btnIndex As Long, _
                                ByRef lLeft As Long, _
                                ByRef lTop As Long)
 Dim X As Long
 
    If Not m_BorderStyle = BDR_None Then lTop = 5: lLeft = 2
    For X = 0 To btnIndex - 1
        If m_ButtonItem(X).TB_Type = TYP_Button Then
            lLeft = lLeft + m_ButtonsWidth
        Else
            lLeft = lLeft + m_ButtonsSeperatorWidth
        End If
        If ((X + 1) / m_ButtonsPerRow) = ((X + 1) \ m_ButtonsPerRow) Then
            If Not X = 0 Then lTop = lTop + m_ButtonsHeight
            lLeft = IIf(m_BorderStyle = BDR_None, 0, 2)
        End If
    Next X
    
End Function


Private Sub DrawCaption(ByVal sCaption As String, _
                            ByVal lLeft As Long, _
                            ByVal lTop As Long, _
                            ByVal lWidth As Long, _
                            ByVal lHeight As Long, _
                            Optional bEnabled As Boolean = True)
    
 Dim X As Long
 Dim xMax As Long
 Dim Rct As RECT
 Dim sArray() As String
 Dim mHeight As Long
    
    On Error GoTo handle
    mHeight = TextHeight("A")
    sArray = SplitToLines(sCaption, lWidth - 5)
    xMax = UBound(sArray) + 1
    Rct.Top = lTop + (lHeight - mHeight * xMax) / 2
    Rct.Left = lLeft
    Rct.Right = lLeft + lWidth
    Rct.Bottom = lTop + lHeight
    UserControl.ForeColor = IIf(bEnabled, m_ForeColor, TranslateColor(vbGrayText))
    
    For X = 0 To xMax - 1
        ' Draw the text
        If m_bIsNT Then
            DrawTextW hdc, StrPtr(sArray(X)), -1, Rct, 1
        Else
           DrawTextA hdc, sArray(X), -1, Rct, 1
        End If
        Rct.Top = Rct.Top + mHeight
    Next X

handle:
End Sub



Private Sub DrawIcon(ByRef mIcon As StdPicture, _
                            ByVal lLeft As Long, _
                            ByVal lTop As Long, _
                            ByVal lWidth As Long, _
                            ByVal lHeight As Long, _
                            Optional lStyle As Long = 1)
 Dim iWidth As Long
 Dim iHeight As Long
 Dim hBrush As Long
 Dim lFlags As Long

    iWidth = ScaleX(mIcon.Width)
    iHeight = ScaleY(mIcon.Height)
    lLeft = lLeft + (lWidth - iWidth) / 2
    lTop = lTop + (lHeight - iHeight) / 2
    
    Select Case lStyle
        Case 1:     ' Paint the normal picture
            PaintPicture mIcon, lLeft, lTop, iWidth, iHeight
            
        Case -1, 0: ' Paint disabled picture
            Select Case mIcon.Type
                Case vbPicTypeBitmap
                    lFlags = DST_BITMAP
                Case vbPicTypeIcon
                    lFlags = DST_ICON
                Case Else
                    lFlags = DST_COMPLEX
            End Select
            ' Create brush and paint disabled state!
            hBrush = CreateSolidBrush(RGB(128, 128, 128))
            DrawState hdc, hBrush, 0, mIcon, 0, lLeft, lTop, iWidth, iHeight, lFlags Or DSS_MONO
            DeleteObject hBrush
    End Select
            
End Sub


Private Sub DrawBorder_Win98(ByVal lnCol As Long, _
                                ByVal X As Long, _
                                ByVal Y As Long, _
                                ByVal lWidth As Long, _
                                ByVal lHeight As Long, _
                                Optional bPressed As Boolean, _
                                Optional lStyle As Long)
 
 Dim lCol1 As Long
 Dim lCol2 As Long
 Dim lCol3 As Long
 Dim lCol4 As Long
 Dim tmpCol As Long

    'lStyle=0 [Flat]  lStyle=1[Solid!]   lStyle=2[Win98]
    lWidth = lWidth - 1
    lHeight = lHeight - 1
    UserControl.FillStyle = vbTransparent
    
    lCol2 = IIf(bPressed, BlendColor(lnCol, vbWhite), BlendColor(lnCol, vbBlack))
    lCol1 = IIf(bPressed, BlendColor(lnCol, vbBlack), BlendColor(lnCol, vbWhite))
    lCol3 = IIf(bPressed Or lStyle = 1, vbBlack, lnCol)
    lCol4 = IIf(bPressed Or lStyle = 1, lnCol, vbBlack)

    If lStyle = 0 Then
        Line (X, Y)-(X + lWidth, Y), lCol1
        Line (X, Y)-(X, Y + lHeight), lCol1
        Line (X + lWidth, Y)-(X + lWidth, Y + lHeight + 1), lCol2
        Line (X, Y + lHeight)-(X + lWidth + 1, Y + lHeight), lCol2
    Else
        Line (X, Y)-(X + lWidth, Y), lCol3
        Line (X, Y)-(X, Y + lHeight), lCol3
        Line (X + lWidth, Y)-(X + lWidth, Y + lHeight + 1), lCol4
        Line (X, Y + lHeight)-(X + lWidth + 1, Y + lHeight), lCol4
        Line (X + 1, Y + 1)-(X + lWidth - 1, Y + 1), lCol1
        Line (X + 1, Y + 1)-(X + 1, Y + lHeight - 1), lCol1
        Line (X + lWidth - 1, Y + 1)-(X + lWidth - 1, Y + lHeight), lCol2
        Line (X + 1, Y + lHeight - 1)-(X + lWidth, Y + lHeight - 1), lCol2
    End If
    
End Sub


Private Sub DrawBorder_OfficeXP(ByVal lnCol As Long, _
                                    ByVal X As Long, _
                                    ByVal Y As Long, _
                                    ByVal lWidth As Long, _
                                    ByVal lHeight As Long, _
                                    Optional bPressed As Boolean)
 Dim lCol1 As Long
 Dim lCol2 As Long

    lWidth = lWidth - 1
    lHeight = lHeight - 1
    lCol1 = BlendColor(lnCol, vbBlack)
    lCol2 = IIf(bPressed, lnCol, BlendColor(lnCol, vbWhite))
    UserControl.FillStyle = vbSolid
    UserControl.FillColor = lCol2
    UserControl.ForeColor = lCol1
    Rectangle hdc, X, Y, X + lWidth, Y + lHeight
    UserControl.FillStyle = vbTransparent
    
End Sub


Private Sub DrawBorder_WinXP(ByVal lnCol As Long, _
                                ByVal X As Long, _
                                ByVal Y As Long, _
                                ByVal lWidth As Long, _
                                ByVal lHeight As Long, _
                                Optional bHover As Boolean, _
                                Optional bPressed As Boolean)
 Dim lCol1 As Long
 Dim lCurv As Long
 Dim lShift As Long
 
    lCurv = 6
    lCol1 = BlendColor(lnCol, vbBlack)
    UserControl.ForeColor = lCol1
    If bHover And Not m_Pressed Then
        UserControl.FillStyle = vbSolid
        UserControl.FillColor = lnCol
        RoundRect hdc, X, Y, X + lWidth, Y + lHeight, lCurv, lCurv
        lShift = 1.5
    Else
        UserControl.FillColor = m_BackColor
        RoundRect hdc, X, Y, X + lWidth, Y + lHeight, lCurv, lCurv
        lShift = 2
    End If
    lnCol = IIf(bPressed, lCol1, lnCol)
    FillGradient hdc, X + lShift, Y + lShift, lWidth - lShift * 2, lHeight - lShift * 2, lnCol, m_BackGradientCol, Fill_Vertical

End Sub


Private Function GetButtonFromXY(ByVal X1 As Long, ByVal Y1 As Long)
Dim lLeft As Long
Dim lTop As Long
Dim X As Long
Dim xMax As Long
Dim xPrev As Long
Dim yPrev As Long

    xMax = Me.Button_Count
    If Not m_BorderStyle = BDR_None Then lTop = 5: lLeft = 2
    
    For X = 0 To xMax - 1
        If X / m_ButtonsPerRow = X \ m_ButtonsPerRow Then
            yPrev = lTop
            lTop = lTop + m_ButtonsHeight
            lLeft = 0
        End If
        xPrev = lLeft
        If m_ButtonItem(X).TB_Type = TYP_Button Then
            lLeft = lLeft + m_ButtonsWidth
        Else
            lLeft = lLeft + m_ButtonsSeperatorWidth
        End If

        If X1 > xPrev And X1 < lLeft Then
            If Y1 > yPrev And Y1 < lTop Then
                GetButtonFromXY = X
                Exit Function
            End If
        End If
    Next
    GetButtonFromXY = -1
End Function

Private Sub ApplyTheme(ByVal mTheme As ButtonsStyleEnum)
Select Case mTheme
    Case Style_Flat, Style_Solid, Style_Win9X
        m_BackColor = vbButtonFace
        m_HoverColor = vbButtonFace
        m_BackGradientCol = vbButtonFace
        m_BackGradient = Fill_None
        m_BorderStyle = IIf(mTheme = Style_Flat, BDR_None, m_BorderStyle)
    Case Style_OfficeXP, Style_WinXP
        m_BackColor = BlendColor(vbButtonFace, vbWhite)
        m_HoverColor = IIf(mTheme = Style_OfficeXP, &HFF8080, &HCFF0FF)
        m_BackGradient = Fill_None
        m_BorderStyle = IIf(mTheme = Style_OfficeXP, BDR_None, m_BorderStyle)
End Select
End Sub

Private Sub DrawSeperator(ByVal btnIndex As Long)
Dim lLeft As Long
Dim lTop As Long
Dim X As Long
Dim Y As Long

    GetButtonXY btnIndex, X, Y
    If m_ShowSeperator Then
        lLeft = X + m_ButtonsSeperatorWidth / 2
        lTop = Y + 2
        Line (lLeft, lTop)-(lLeft, lTop + m_ButtonsHeight - 6), BlendColor(m_BackColor, vbBlack) ' RGB(128, 128, 128)
        Line (lLeft + 1, lTop)-(lLeft + 1, lTop + m_ButtonsHeight - 6), BlendColor(m_BackColor, vbWhite) ' RGB(230, 230, 230)
    End If
    
    DrawButtonIndex X, Y, btnIndex
    
End Sub

Private Sub DrawButtonIndex(X As Long, Y As Long, btnIndex As Long)
Dim lWidth As Long

    If Not Ambient.UserMode Then
        CurrentX = X
        CurrentY = Y + 2
        
        If btnIndex = m_Button_Index Then
            Select Case m_ButtonsStyle
                Case Style_Flat, Style_Win9X, Style_Solid
                    lWidth = IIf(m_ButtonItem(btnIndex).TB_Type = TYP_Button, m_ButtonsWidth, m_ButtonsSeperatorWidth)
                    FillStyle = vbSolid
                    Rectangle hdc, X, Y, X + lWidth, Y + 3
            End Select
        End If
        
        Print btnIndex
        
    End If
    
End Sub


' Accessing all the button properties without selecting
' a particular button... Needed when altering the button
' values from code (not from the property window)
' NB : This will not alter the Button_Index
' ---------------------------------------------------------------------------------------------------

Public Function GetButtonValue(ByVal ButtonIndex As Long, _
                            ByVal PropertyID As ButtonPropertyEnum) As Variant
    Select Case PropertyID
        Case BTN_Caption
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_Caption
        Case BTN_Enabled
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_Enabled
        Case BTN_Icon
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_Icon
        Case BTN_IconAlignment
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_IconAllignment
        Case BTN_Pressed
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_Pressed
        Case BTN_Tooltip
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_ToolTipText
        Case BTN_ToolTipIcon
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_ToolTipIcon
        Case BTN_Type
            GetButtonValue = m_ButtonItem(ButtonIndex).TB_Type
    End Select
    
End Function

Public Sub SetButtonValue(ByVal ButtonIndex As Long, _
                            ByVal PropertyID As ButtonPropertyEnum, _
                            ByVal newValue As Variant)
                            
    Select Case PropertyID
        Case BTN_Caption
            m_ButtonItem(ButtonIndex).TB_Caption = newValue
        Case BTN_Enabled
            m_ButtonItem(ButtonIndex).TB_Enabled = newValue
        Case BTN_Icon
            Set m_ButtonItem(ButtonIndex).TB_Icon = newValue
        Case BTN_IconAlignment
            m_ButtonItem(ButtonIndex).TB_IconAllignment = newValue
        Case BTN_Pressed
            m_ButtonItem(ButtonIndex).TB_Pressed = newValue
        Case BTN_Tooltip
            m_ButtonItem(ButtonIndex).TB_ToolTipText = newValue
        Case BTN_ToolTipIcon
            m_ButtonItem(ButtonIndex).TB_ToolTipIcon = newValue
        Case BTN_Type
            m_ButtonItem(ButtonIndex).TB_Type = newValue
    End Select
    RedrawControl
    
End Sub



' Some useful public routines... Also used by the control !!
' ---------------------------------------------------------------------------------------------------

Public Sub RemoveButton(ByVal ButtonIndex As Long)
Dim mNewItems() As ToolButton
Dim mPos As Long
Dim X As Long

    If ButtonIndex < 0 Or ButtonIndex >= m_Button_Count Then
        Err.Raise 33, , "Index out or range!!"
        Exit Sub
    End If
    
    If m_Button_Count = 1 Then Exit Sub
    
    ReDim mNewItems(m_Button_Count - 2)
    
    For X = 0 To m_Button_Count - 1
        If Not X = ButtonIndex Then
            mNewItems(mPos) = m_ButtonItem(X)
            mPos = mPos + 1
        End If
    Next X
    
    m_ButtonItem = mNewItems
    m_Button_Count = m_Button_Count - 1
    RedrawControl
    
End Sub


Public Sub InsertButtonTo(ByVal ButtonIndex As Long, ByVal NewIndex As Long)
Dim X As Long
Dim mCurButton As ToolButton

    If ButtonIndex < 0 Or ButtonIndex >= m_Button_Count Or NewIndex < 0 Or NewIndex >= m_Button_Count Then
        Err.Raise 33, , "Index out or range!!"
        Exit Sub
    End If
    
    If NewIndex < ButtonIndex Then
    
        mCurButton = m_ButtonItem(ButtonIndex)
        For X = ButtonIndex To NewIndex + 1 Step -1
            m_ButtonItem(X) = m_ButtonItem(X - 1)
        Next X
        m_ButtonItem(NewIndex) = mCurButton
        
    ElseIf NewIndex > ButtonIndex Then
    
        mCurButton = m_ButtonItem(ButtonIndex)
        For X = ButtonIndex To NewIndex - 1
            m_ButtonItem(X) = m_ButtonItem(X + 1)
        Next X
        m_ButtonItem(NewIndex) = mCurButton

    End If
    
    RedrawControl
    
End Sub


Public Sub MoveButtonTo(ByVal ButtonIndex As Long, ByVal NewIndex As Long)
Dim mTmpItem As ToolButton

    If ButtonIndex < 0 Or ButtonIndex >= m_Button_Count Or NewIndex < 0 Or NewIndex >= m_Button_Count Then
        Err.Raise 33, , "Index out or range!!"
        Exit Sub
    End If
    
    mTmpItem = m_ButtonItem(NewIndex)
    m_ButtonItem(NewIndex) = m_ButtonItem(ButtonIndex)
    m_ButtonItem(ButtonIndex) = mTmpItem
    RedrawControl
    
End Sub


