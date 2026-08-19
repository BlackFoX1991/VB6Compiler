VERSION 5.00
Begin VB.UserControl GpTabStrip 
   AutoRedraw      =   -1  'True
   ClientHeight    =   1275
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   2235
   ClipBehavior    =   0  'Keine
   ControlContainer=   -1  'True
   EditAtDesignTime=   -1  'True
   ForwardFocus    =   -1  'True
   PaletteMode     =   1  '1 -ZReihenfolge
   PropertyPages   =   "GpTabs.ctx":0000
   ScaleHeight     =   85
   ScaleMode       =   3  'Pixel
   ScaleWidth      =   149
   ToolboxBitmap   =   "GpTabs.ctx":0014
   Begin VB.Label lblFont 
      AutoSize        =   -1  'True
      Height          =   180
      Left            =   120
      TabIndex        =   0
      Top             =   240
      Visible         =   0   'False
      Width           =   90
   End
End
Attribute VB_Name = "GpTabStrip"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Attribute VB_Ext_KEY = "PropPageWizardRun" ,"Yes"
Option Explicit
'
' ***************************************************************************************
' * Project  | GpTabStrip                                                               *
' *----------|--------------------------------------------------------------------------*
' * Version  | V1.0                                                                     *
' *----------|--------------------------------------------------------------------------*
' * Author   | Genghis Khan(GuangJian Guo)                                              *
' *----------|--------------------------------------------------------------------------*
' * WebSite  | http://www.itkhan.com                                                    *
' *----------|--------------------------------------------------------------------------*
' * MailTo   | webmaster@itkhan.com                                                     *
' *----------|--------------------------------------------------------------------------*
' * Date     | 13 April 2003                                                            *
' ***************************************************************************************

' ======================================================================================
' Constants
' ======================================================================================

Private Const MODULE_NAME = "GpTabs"
Private Const XPBorderColor = &H808080       '&H733C00  ' XP风格边框的颜色
Private Const XPFlatBorderColor = &HE0A193
Private Const XPFlatTabColor = &HFFC3B3
'Private Const XPFlatTabColorActive = vbWhite
Private Const XPFlatTabColorActive = &HEAEAEA
Private Const XPFlatTabColorHover = &HFFA49B
Private Const TabsInterval = 2          ' 每个Tab之间的间隔距离
Private Const RoundRectSize = 1         ' 圆角的大小
Private Const DiscrepancyHeight = 2     ' 选中的Tab与没有选中的Tab的高度差
Private Const InflateFontHeight = 6     ' 与Tab的Caption在当前字体的实际高度相加的得Tab的默认高度
Private Const InflateFontWidth = 4      ' 与Tab的Caption在当前字体的实际宽度相加的得Tab的默认宽度
Private Const InflateIconHeight = 2     ' 与Tab的Icon的实际高度相加的得Tab的默认高度
Private Const InflateIconWidth = 0      ' 与Tab的Icon的实际宽度相加的得Tab的默认宽度

Private Declare Function GetComputerName Lib "kernel32" Alias "GetComputerNameA" (ByVal lpBuffer As String, nSize As Long) As Long

#Const DEBUGMODE = 0

'Set of bit flags that indicate which common control classes will be loaded
'from the DLL. The dwICC value of tagINITCOMMONCONTROLSEX can
'be a combination of the following:
 Const ICC_LISTVIEW_CLASSES = &H1          '/* listview, header
 Const ICC_TREEVIEW_CLASSES = &H2          '/* treeview, tooltips
 Const ICC_BAR_CLASSES = &H4               '/* toolbar, statusbar, trackbar, tooltips
 Const ICC_TAB_CLASSES = &H8               '/* tab, tooltips
 Const ICC_UPDOWN_CLASS = &H10             '/* updown
 Const ICC_PROGRESS_CLASS = &H20           '/* progress
 Const ICC_HOTKEY_CLASS = &H40             '/* hotkey
 Const ICC_ANIMATE_CLASS = &H80            '/* animate
 Const ICC_WIN95_CLASSES = &HFF            '/* loads everything above
 Const ICC_DATE_CLASSES = &H100            '/* month picker, date picker, time picker, updown
 Const ICC_USEREX_CLASSES = &H200          '/* ComboEx
 Const ICC_COOL_CLASSES = &H400            '/* Rebar (coolbar) control


' 指定窗口的结构中取得信息，用于GetWindowLong、SetWindowLong函数
 Const GWL_EXSTYLE = (-20)                 '/* 扩展窗口样式 */
 Const GWL_HINSTANCE = (-6)                '/* 拥有窗口的实例的句柄 */
 Const GWL_HWNDPARENT = (-8)               '/* 该窗口之父的句柄。不要用SetWindowWord来改变这个值 */
 Const GWL_ID = (-12)                      '/* 对话框中一个子窗口的标识符 */
 Const GWL_STYLE = (-16)                   '/* 窗口样式 */
 Const GWL_USERDATA = (-21)                '/* 含义由应用程序规定 */
 Const GWL_WNDPROC = (-4)                  '/* 该窗口的窗口函数的地址 */
 Const DWL_DLGPROC = 4                     '/* 这个窗口的对话框函数地址 */
 Const DWL_MSGRESULT = 0                   '/* 在对话框函数中处理的一条消息返回的值 */
 Const DWL_USER = 8                        '/* 含义由应用程序规定 */


' GetDeviceCaps索引表，用于GetDeviceCaps函数
 Const DRIVERVERSION = 0                   '/* 备驱动程序版本
 Const BITSPIXEL = 12                      '/*
 Const LOGPIXELSX = 88                     '/*  Logical pixels/inch in X
 Const LOGPIXELSY = 90                     '/*  Logical pixels/inch in Y

' Windows对象常数表，函数GetSysColor
 Const COLOR_ACTIVEBORDER = 10             '/* 活动窗口的边框
 Const COLOR_ACTIVECAPTION = 2             '/* 活动窗口的标题
 Const COLOR_ADJ_MAX = 100                 '/*
 Const COLOR_ADJ_MIN = -100                '/*
 Const COLOR_APPWORKSPACE = 12             '/* MDI桌面的背景
 Const COLOR_BACKGROUND = 1                '/*
 Const COLOR_BTNDKSHADOW = 21              '/*
 Const COLOR_BTNLIGHT = 22                 '/*
 Const COLOR_BTNFACE = 15                  '/* 按钮
 Const COLOR_BTNHIGHLIGHT = 20             '/* 按钮的3D加亮区
 Const COLOR_BTNSHADOW = 16                '/* 按钮的3D阴影
 Const COLOR_BTNTEXT = 18                  '/* 按钮文字
 Const COLOR_CAPTIONTEXT = 9               '/* 窗口标题中的文字
 Const COLOR_GRAYTEXT = 17                 '/* 灰色文字；如使用了抖动技术则为零
 Const COLOR_HIGHLIGHT = 13                '/* 选定的项目背景
 Const COLOR_HIGHLIGHTTEXT = 14            '/* 选定的项目文字
 Const COLOR_INACTIVEBORDER = 11           '/* 不活动窗口的边框
 Const COLOR_INACTIVECAPTION = 3           '/* 不活动窗口的标题
 Const COLOR_INACTIVECAPTIONTEXT = 19      '/* 不活动窗口的文字
 Const COLOR_MENU = 4                      '/* 菜单
 Const COLOR_MENUTEXT = 7                  '/* 菜单正文
 Const COLOR_SCROLLBAR = 0                 '/* 滚动条
 Const COLOR_WINDOW = 5                    '/* 窗口背景
 Const COLOR_WINDOWFRAME = 6               '/* 窗框
 Const COLOR_WINDOWTEXT = 8                '/* 窗口正文
Const COLORONCOLOR = 3

' 函数CombineRgn的返回值，类型Long
 Const COMPLEXREGION = 3                   '/* 区域有互相交叠的边界 */
 Const SIMPLEREGION = 2                    '/* 区域边界没有互相交叠 */
 Const NULLREGION = 1                      '/* 区域为空 */
 Const ERRORAPI = 0                        '/* 不能创建组合区域 */

' 组合两区域的方法，函数CombineRgn的的参数nCombineMode所使用的常数
 Const RGN_AND = 1                         '/* hDestRgn被设置为两个源区域的交集 */
 Const RGN_COPY = 5                        '/* hDestRgn被设置为hSrcRgn1的拷贝 */
 Const RGN_DIFF = 4                        '/* hDestRgn被设置为hSrcRgn1中与hSrcRgn2不相交的部分 */
 Const RGN_OR = 2                          '/* hDestRgn被设置为两个区域的并集 */
 Const RGN_XOR = 3                         '/* hDestRgn被设置为除两个源区域OR之外的部分 */

' Missing Draw State constants declarations，参看DrawState函数
'/* Image type */
 Const DST_COMPLEX = &H0                   '/* 绘图在由lpDrawStateProc参数指定的回调函数期间执行。lParam和wParam会传递给回调事件
 Const DST_TEXT = &H1                      '/* lParam代表文字的地址（可使用一个字串别名），wParam代表字串的长度
 Const DST_PREFIXTEXT = &H2                '/* 与DST_TEXT类似，只是 & 字符指出为下各字符加上下划线
 Const DST_ICON = &H3                      '/* lParam包括图标句柄
 Const DST_BITMAP = &H4                    '/* lParam中的句柄
' /* State type */
 Const DSS_NORMAL = &H0                    '/* 普通图象
 Const DSS_UNION = &H10                    '/* 图象进行抖动处理
 Const DSS_DISABLED = &H20                 '/* 图象具有浮雕效果
 Const DSS_MONO = &H80                     '/* 用hBrush描绘图象
 Const DSS_RIGHT = &H8000                  '/*

' Built in ImageList drawing methods:
 Const ILD_NORMAL = 0&
 Const ILD_TRANSPARENT = 1&
 Const ILD_BLEND25 = 2&
 Const ILD_SELECTED = 4&
 Const ILD_FOCUS = 4&
 Const ILD_MASK = &H10&
 Const ILD_IMAGE = &H20&
 Const ILD_ROP = &H40&
 Const ILD_OVERLAYMASK = 3840&
 Const ILC_MASK = &H1&
 Const ILCF_MOVE = &H0&
 Const ILCF_SWAP = &H1&

 Const CLR_DEFAULT = -16777216
 Const CLR_HILIGHT = -16777216
 Const CLR_NONE = -1

' General windows messages:
 Const WM_COMMAND = &H111
 Const WM_KEYDOWN = &H100
 Const WM_KEYUP = &H101
 Const WM_CHAR = &H102
 Const WM_SETFOCUS = &H7
 Const WM_KILLFOCUS = &H8
 Const WM_SETFONT = &H30
 Const WM_GETTEXT = &HD
 Const WM_GETTEXTLENGTH = &HE
 Const WM_SETTEXT = &HC
 Const WM_NOTIFY = &H4E&

' Show window styles
 Const SW_SHOWNORMAL = 1
 Const SW_ERASE = &H4
 Const SW_HIDE = 0
 Const SW_INVALIDATE = &H2
 Const SW_MAX = 10
 Const SW_MAXIMIZE = 3
 Const SW_MINIMIZE = 6
 Const SW_NORMAL = 1
 Const SW_OTHERUNZOOM = 4
 Const SW_OTHERZOOM = 2
 Const SW_PARENTCLOSING = 1
 Const SW_RESTORE = 9
 Const SW_PARENTOPENING = 3
 Const SW_SHOW = 5
 Const SW_SCROLLCHILDREN = &H1
 Const SW_SHOWDEFAULT = 10
 Const SW_SHOWMAXIMIZED = 3
 Const SW_SHOWMINIMIZED = 2
 Const SW_SHOWMINNOACTIVE = 7
 Const SW_SHOWNA = 8
 Const SW_SHOWNOACTIVATE = 4

' 常见的光栅操作代码
 Const BLACKNESS = &H42                    '/* 表示使用与物理调色板的索引0相关的色彩来填充目标矩形区域，（对缺省的物理调色板而言，该颜色为黑色）。
 Const DSTINVERT = &H550009                '/* 表示使目标矩形区域颜色取反。
 Const MERGECOPY = &HC000CA                '/* 表示使用布尔型的AND（与）操作符将源矩形区域的颜色与特定模式组合一起。
 Const MERGEPAINT = &HBB0226               '/* 通过使用布尔型的OR（或）操作符将反向的源矩形区域的颜色与目标矩形区域的颜色合并。
 Const NOTSRCCOPY = &H330008               '/* 将源矩形区域颜色取反，于拷贝到目标矩形区域。
 Const NOTSRCERASE = &H1100A6              '/* 使用布尔类型的OR（或）操作符组合源和目标矩形区域的颜色值，然后将合成的颜色取反。
 Const PATCOPY = &HF00021                  '/* 将特定的模式拷贝到目标位图上。
 Const PATINVERT = &H5A0049                '/* 通过使用布尔OR（或）操作符将源矩形区域取反后的颜色值与特定模式的颜色合并。然后使用OR（或）操作符将该操作的结果与目标矩形区域内的颜色合并。
 Const PATPAINT = &HFB0A09                 '/* 通过使用XOR（异或）操作符将源和目标矩形区域内的颜色合并。
 Const SRCAND = &H8800C6                   '/* 通过使用AND（与）操作符来将源和目标矩形区域内的颜色合并
 Const SRCCOPY = &HCC0020                  '/* 将源矩形区域直接拷贝到目标矩形区域。
 Const SRCERASE = &H440328                 '/* 通过使用AND（与）操作符将目标矩形区域颜色取反后与源矩形区域的颜色值合并。
 Const SRCINVERT = &H660046                '/* 通过使用布尔型的XOR（异或）操作符将源和目标矩形区域的颜色合并。
 Const SRCPAINT = &HEE0086                 '/* 通过使用布尔型的OR（或）操作符将源和目标矩形区域的颜色合并。
 Const WHITENESS = &HFF0062                '/* 使用与物理调色板中索引1有关的颜色填充目标矩形区域。（对于缺省物理调色板来说，这个颜色就是白色）。

'--- for mouse_event
 Const MOUSE_MOVED = &H1
 Const MOUSEEVENTF_ABSOLUTE = &H8000       '/*
 Const MOUSEEVENTF_LEFTDOWN = &H2          '/* 模拟鼠标左键按下
Const MOUSEEVENTF_LEFTUP = &H4            '/* 模拟鼠标左键抬起
 Const MOUSEEVENTF_MIDDLEDOWN = &H20       '/* 模拟鼠标中键按下
 Const MOUSEEVENTF_MIDDLEUP = &H40         '/* 模拟鼠标中键按下
 Const MOUSEEVENTF_MOVE = &H1              '/* 移动鼠标 */
 Const MOUSEEVENTF_RIGHTDOWN = &H8         '/* 模拟鼠标右键按下
 Const MOUSEEVENTF_RIGHTUP = &H10          '/* 模拟鼠标右键按下
Const MOUSETRAILS = 39                    '/*

 Const BMP_MAGIC_COOKIE = 19778            '/* this is equivalent to ascii string "BM" */
' constants for the biCompression field
 Const BI_RGB = 0&
 Const BI_RLE4 = 2&
 Const BI_RLE8 = 1&
 Const BI_BITFIELDS = 3&
' Const BITSPIXEL = 12                     '/* Number of bits per pixel
' DIB color table identifiers
 Const DIB_PAL_COLORS = 1                  '/* 在颜色表中装载一个16位所以数组，它们与当前选定的调色板有关 color table in palette indices
 Const DIB_PAL_INDICES = 2                 '/* No color table indices into surf palette
 Const DIB_PAL_LOGINDICES = 4              '/* No color table indices into DC palette
 Const DIB_PAL_PHYSINDICES = 2             '/* No color table indices into surf palette
 Const DIB_RGB_COLORS = 0                  '/* 在颜色表中装载RGB颜色

' BLENDFUNCTION AlphaFormat-Konstante
 Const AC_SRC_ALPHA = &H1
' BLENDFUNCTION BlendOp-Konstante
 Const AC_SRC_OVER = &H0

' ======================================================================================
' Methods
' ======================================================================================
' 函数SetBkModen参数BkMode
 Enum KhanBackStyles
    TRANSPARENT = 1                              '/* 透明处理，即不作上述填充 */
    OPAQUE = 2                                   '/* 用当前的背景色填充虚线画笔、阴影刷子以及字符的空隙 */
    NEWTRANSPARENT = 3                           '/* NT4: Uses chroma-keying upon BitBlt. Undocumented feature that is not working on Windows 2000/XP.
End Enum

' 多边形的填充模式
 Enum KhanPolyFillModeFalgs
    ALTERNATE = 1                                '/* 交替填充
    WINDING = 2                                  '/* 根据绘图方向填充
End Enum

' DrawIconEx
 Enum KhanDrawIconExFlags
    DI_MASK = &H1                                '/* 绘图时使用图标的MASK部分（如单独使用，可获得图标的掩模）
    DI_IMAGE = &H2                               '/* 绘图时使用图标的XOR部分（即图标没有透明区域）
    DI_NORMAL = &H3                              '/* 用常规方式绘图（合并 DI_IMAGE 和 DI_MASK）
    DI_COMPAT = &H4                              '/* 描绘标准的系统指针，而不是指定的图象
    DI_DEFAULTSIZE = &H8                         '/* 忽略cxWidth和cyWidth设置，并采用原始的图标大小
End Enum

'指定被装载图像类型,LoadImage,CopyImage
 Enum KhanImageTypes
    IMAGE_BITMAP = 0
    IMAGE_ICON = 1
    IMAGE_CURSOR = 2
    IMAGE_ENHMETAFILE = 3
End Enum

 Enum KhanImageFalgs
    LR_COLOR = &H2                               '/*
    LR_COPYRETURNORG = &H4                       '/* 表示创建一个图像的精确副本，而忽略参数cxDesired和cyDesired
    LR_COPYDELETEORG = &H8                       '/* 表示创建一个副本后删除原始图像。
    LR_CREATEDIBSECTION = &H2000                 '/* 当参数uType指定为IMAGE_BITMAP时，使得函数返回一个DIB部分位图，而不是一个兼容的位图。这个标志在装载一个位图，而不是映射它的颜色到显示设备时非常有用。
    LR_DEFAULTCOLOR = &H0                        '/* 以常规方式载入图象
    LR_DEFAULTSIZE = &H40                        '/* 若 cxDesired或cyDesired未被设为零，使用系统指定的公制值标识光标或图标的宽和高。如果这个参数不被设置且cxDesired或cyDesired被设为零，函数使用实际资源尺寸。如果资源包含多个图像，则使用第一个图像的大小。
    LR_LOADFROMFILE = &H10                       '/* 根据参数lpszName的值装载图像。若标记未被给定，lpszName的值为资源名称。
    LR_LOADMAP3DCOLORS = &H1000                  '/* 将图象中的深灰(Dk Gray RGB（128，128，128）)、灰(Gray RGB（192，192，192）)、以及浅灰(Gray RGB（223，223，223）)像素都替换成COLOR_3DSHADOW，COLOR_3DFACE以及COLOR_3DLIGHT的当前设置
    LR_LOADTRANSPARENT = &H20                    '/* 若fuLoad包括LR_LOADTRANSPARENT和LR_LOADMAP3DCOLORS两个值，则LRLOADTRANSPARENT优先。但是，颜色表接口由COLOR_3DFACE替代，而不是COLOR_WINDOW。
    LR_MONOCHROME = &H1                          '/* 将图象转换成单色
    LR_SHARED = &H8000                           '/* 若图像将被多次装载则共享。如果LR_SHARED未被设置，则再向同一个资源第二次调用这个图像是就会再装载以便这个图像且返回不同的句柄。
    LR_COPYFROMRESOURCE = &H4000                 '/*
End Enum

 Enum KhanDrawTextStyles
    DT_BOTTOM = &H8&                             '/* 必须同时指定DT_SINGLE。指示文本对齐格式化矩形的底边
    DT_CALCRECT = &H400&                         '/* 象下面这样计算格式化矩形：多行绘图时矩形的底边根据需要进行延展，以便容下所有文字；单行绘图时，延展矩形的右侧。不描绘文字。由lpRect参数指定的矩形会载入计算出来的值
    DT_CENTER = &H1&                             '/* 文本垂直居中
    DT_EXPANDTABS = &H40&                        '/* 描绘文字的时候，对制表站进行扩展。默认的制表站间距是8个字符。但是，可用DT_TABSTOP标志改变这项设定
    DT_EXTERNALLEADING = &H200&                  '/* 计算文本行高度的时候，使用当前字体的外部间距属性（the external leading attribute）
    DT_INTERNAL = &H1000&                        '/* Uses the system font to calculate text metrics
    DT_LEFT = &H0&                               '/* 文本左对齐
    DT_NOCLIP = &H100&                           '/* 描绘文字时不剪切到指定的矩形，DrawTextEx is somewhat faster when DT_NOCLIP is used.
    DT_NOPREFIX = &H800&                         '/* 通常，函数认为 & 字符表示应为下一个字符加上下划线。该标志禁止这种行为
    DT_RIGHT = &H2&                              '/* 文本右对齐
    DT_SINGLELINE = &H20&                        '/* 只画单行
    DT_TABSTOP = &H80&                           '/* 指定新的制表站间距，采用这个整数的高8位
    DT_TOP = &H0&                                '/* 必须同时指定DT_SINGLE。指示文本对齐格式化矩形的底边
    DT_VCENTER = &H4&                            '/* 必须同时指定DT_SINGLE。指示文本对齐格式化矩形的中部
    DT_WORDBREAK = &H10&                         '/* 进行自动换行。如用SetTextAlign函数设置了TA_UPDATECP标志，这里的设置则无效
' #if(WINVER >= =&H0400)
    DT_EDITCONTROL = &H2000&                     '/* 对一个多行编辑控件进行模拟。不显示部分可见的行
    DT_END_ELLIPSIS = &H8000&                    '/* 倘若字串不能在矩形里全部容下，就在末尾显示省略号
    DT_PATH_ELLIPSIS = &H4000&                   '/* 如字串包含了 \ 字符，就用省略号替换字串内容，使其能在矩形中全部容下。例如，一个很长的路径名可能换成这样显示——c:\windows\...\doc\readme.txt
    DT_MODIFYSTRING = &H10000                    '/* 如指定了DT_ENDELLIPSES 或 DT_PATHELLIPSES，就会对字串进行修改，使其与实际显示的字串相符
    DT_RTLREADING = &H20000                      '/* 如选入设备场景的字体属于希伯来或阿拉伯语系，就从右到左描绘文字
    DT_WORD_ELLIPSIS = &H40000                   '/* Truncates any word that does not fit in the rectangle and adds ellipses. Compare with DT_END_ELLIPSIS and DT_PATH_ELLIPSIS.
End Enum

 Enum KhanDrawFrameControlType
    DFC_CAPTION = 1                              '/* Title bar.
    DFC_MENU = 2                                 '/* Menu bar.
    DFC_SCROLL = 3                               '/* Scroll bar.
    DFC_BUTTON = 4                               '/* Standard button.
    DFC_POPUPMENU = 5                            '/* <b>Windows 98/Me, Windows 2000 or later:</b> Popup menu item.
End Enum

 Enum KhanDrawFrameControlStyle
    DFCS_BUTTONCHECK = &H0                       '/* Check box.
    DFCS_BUTTONRADIOIMAGE = &H1                  '/* Image for radio button (nonsquare needs image).
    DFCS_BUTTONRADIOMASK = &H2                   '/* Mask for radio button (nonsquare needs mask).
    DFCS_BUTTONRADIO = &H4                       '/* Radio button.
    DFCS_BUTTON3STATE = &H8                      '/* Three-state button.
    DFCS_BUTTONPUSH = &H10                       '/* Push button.
    DFCS_CAPTIONCLOSE = &H0                      '/* <b>Close</b> button.
    DFCS_CAPTIONMIN = &H1                        '/* <b>Minimize</b> button.
    DFCS_CAPTIONMAX = &H2                        '/* <b>Maximize</b> button.
    DFCS_CAPTIONRESTORE = &H3                    '/* <b>Restore</b> button.
    DFCS_CAPTIONHELP = &H4                       '/* <b>Help</b> button.
    DFCS_MENUARROW = &H0                         '/* Submenu arrow.
    DFCS_MENUCHECK = &H1                         '/* Check mark.
    DFCS_MENUBULLET = &H2                        '/* Bullet.
    DFCS_MENUARROWRIGHT = &H4                    '/* Submenu arrow pointing left. This is used for the right-to-left cascading menus used with right-to-left languages such as Arabic or Hebrew.
    DFCS_SCROLLUP = &H0                          '/* Up arrow of scroll bar.
    DFCS_SCROLLDOWN = &H1                        '/* Down arrow of scroll bar.
    DFCS_SCROLLLEFT = &H2                        '/* Left arrow of scroll bar.
    DFCS_SCROLLRIGHT = &H3                       '/* Right arrow of scroll bar.
    DFCS_SCROLLCOMBOBOX = &H5                    '/* Combo box scroll bar.
    DFCS_SCROLLSIZEGRIP = &H8                    '/* Size grip in bottom-right corner of window.
    DFCS_SCROLLSIZEGRIPRIGHT = &H10              '/* Size grip in bottom-left corner of window. This is used with right-to-left languages such as Arabic or Hebrew.
    DFCS_INACTIVE = &H100                        '/* Button is inactive (grayed).
    DFCS_PUSHED = &H200                          '/* Button is pushed.
    DFCS_CHECKED = &H400                         '/* Button is checked.
    DFCS_TRANSPARENT = &H800                     '/* <b>Windows 98/Me, Windows 2000 or later:</b> The background remains untouched.
    DFCS_HOT = &H1000                            '/* <b>Windows 98/Me, Windows 2000 or later:</b> Button is hot-tracked.
    DFCS_ADJUSTRECT = &H2000                     '/* Bounding rectangle is adjusted to exclude the surrounding edge of the push button.
    DFCS_FLAT = &H4000                           '/* Button has a flat border.
    DFCS_MONO = &H8000                           '/* Button has a monochrome border.
End Enum

' 指定画笔样式，函数CreatePen的参数CreatePen所使用的常数
 Enum KhanPenStyles
    ' CreatePen，ExtCreatePen
    ' 画笔的样式
    PS_SOLID = 0                                 '/* 画笔画出的是实线 */
    PS_DASH = 1                                  '/* 画笔画出的是虚线（nWidth必须是1） */
    PS_DOT = 2                                   '/* 画笔画出的是点线（nWidth必须是1） */
    PS_DASHDOT = 3                               '/* 画笔画出的是点划线（nWidth必须是1） */
    PS_DASHDOTDOT = 4                            '/* 画笔画出的是点-点-划线（nWidth必须是1） */
    PS_NULL = 5                                  '/* 画笔不能画图 */
    PS_INSIDEFRAME = 6                           '/* 画笔在由椭圆、矩形、圆角矩形、饼图以及弦等生成的封闭对象框中画图。如指定的准确RGB颜色不存在，就进行抖动处理 */
    ' ExtCreatePen
    ' 画笔的样式
    PS_USERSTYLE = 7                             '/* <b>Windows NT/2000:</b> The pen uses a styling array supplied by the user.
    PS_ALTERNATE = 8                             '/* <b>Windows NT/2000:</b> The pen sets every other pixel. (This style is applicable only for cosmetic pens.)
    ' 画笔的笔尖
    PS_ENDCAP_ROUND = &H0                        '/* End caps are round.
    PS_ENDCAP_SQUARE = &H100                     '/* End caps are square.
    PS_ENDCAP_FLAT = &H200                       '/* End caps are flat.
    PS_ENDCAP_MASK = &HF00                       '/* Mask for previous PS_ENDCAP_XXX values.
    ' 在图形中连接线段或在路径中连接直线的方式
    PS_JOIN_ROUND = &H0                          '/* Joins are beveled.
    PS_JOIN_BEVEL = &H1000                       '/* Joins are mitered when they are within the current limit set by the SetMiterLimit function. If it exceeds this limit, the join is beveled.
    PS_JOIN_MITER = &H2000                       '/* Joins are round.
    PS_JOIN_MASK = &HF000                        '/* Mask for previous PS_JOIN_XXX values.
    ' 画笔的类型
    PS_COSMETIC = &H0                            '/* The pen is cosmetic.
    PS_GEOMETRIC = &H10000                       '/* The pen is geometric.
    '
    PS_STYLE_MASK = &HF                          '/* Mask for previous PS_XXX values.
    PS_TYPE_MASK = &HF0000                       '/* Mask for previous PS_XXX (pen type).
End Enum

 Enum KhanBrushStyle
    BS_SOLID = 0                                 '/* Solid brush.
    BS_HOLLOW = 1                                '/* Hollow brush.
    BS_NULL = 1                                  '/* Same as BS_HOLLOW.
    BS_HATCHED = 2                               '/* Hatched brush.
    BS_PATTERN = 3                               '/* Pattern brush defined by a memory bitmap.
    BS_INDEXED = 4                               '/*
    BS_DIBPATTERN = 5                            '/* A pattern brush defined by a device-independent bitmap (DIB) specification.
    BS_DIBPATTERNPT = 6                          '/* A pattern brush defined by a device-independent bitmap (DIB) specification. If <b>lbStyle</b> is BS_DIBPATTERNPT, the <b>lbHatch</b> member contains a pointer to a packed DIB.
    BS_PATTERN8X8 = 7                            '/* Same as BS_PATTERN.
    BS_DIBPATTERN8X8 = 8                         '/* Same as BS_DIBPATTERN.
    BS_MONOPATTERN = 9                           '/* The brush is a monochrome (black & white) bitmap.
End Enum

 Enum KhanHatchStyles
    HS_HORIZONTAL = 0                            '/* Horizontal hatch.
    HS_VERTICAL = 1                              '/* Vertical hatch.
    HS_FDIAGONAL = 2                             '/* A 45-degree downward, left-to-right hatch.
    HS_BDIAGONAL = 3                             '/* A 45-degree upward, left-to-right hatch.
    HS_CROSS = 4                                 '/* Horizontal and vertical cross-hatch.
    HS_DIAGCROSS = 5                             '/* A 45-degree crosshatch.
End Enum

' DrawEdge
 Enum KhanBorderStyles
    BDR_RAISEDOUTER = &H1                        '/* Raised outer edge.
    BDR_SUNKENOUTER = &H2                        '/* Sunken outer edge.
    BDR_RAISEDINNER = &H4                        '/* Raised inner edge.
    BDR_SUNKENINNER = &H8                        '/* Sunken inner edge.
    BDR_OUTER = &H3                              '/* (BDR_RAISEDOUTER Or BDR_SUNKENOUTER)
    BDR_INNER = &HC                              '/* (BDR_RAISEDINNER Or BDR_SUNKENINNER)
    BDR_RAISED = &H5
    BDR_SUNKEN = &HA
    EDGE_BUMP = (BDR_RAISEDOUTER Or BDR_SUNKENINNER)
    EDGE_ETCHED = (BDR_SUNKENOUTER Or BDR_RAISEDINNER)
    EDGE_RAISED = (BDR_RAISEDOUTER Or BDR_RAISEDINNER)
    EDGE_SUNKEN = (BDR_SUNKENOUTER Or BDR_SUNKENINNER)
End Enum

 Enum KhanBorderFlags
    BF_LEFT = &H1                                '/* Left side of border rectangle.
    BF_TOP = &H2                                 '/* Top of border rectangle.
    BF_RIGHT = &H4                               '/* Right side of border rectangle.
    BF_BOTTOM = &H8                              '/* Bottom of border rectangle.
    BF_TOPLEFT = (BF_TOP Or BF_LEFT)
    BF_TOPRIGHT = (BF_TOP Or BF_RIGHT)
    BF_BOTTOMLEFT = (BF_BOTTOM Or BF_LEFT)
    BF_BOTTOMRIGHT = (BF_BOTTOM Or BF_RIGHT)
    BF_RECT = (BF_LEFT Or BF_TOP Or BF_RIGHT Or BF_BOTTOM)
    BF_DIAGONAL = &H10                           '/* Diagonal border.
    BF_DIAGONAL_ENDTOPRIGHT = (BF_DIAGONAL Or BF_TOP Or BF_RIGHT)
    BF_DIAGONAL_ENDTOPLEFT = (BF_DIAGONAL Or BF_TOP Or BF_LEFT)
    BF_DIAGONAL_ENDBOTTOMLEFT = (BF_DIAGONAL Or BF_BOTTOM Or BF_LEFT)
    BF_DIAGONAL_ENDBOTTOMRIGHT = (BF_DIAGONAL Or BF_BOTTOM Or BF_RIGHT)
    BF_MIDDLE = &H800                            '/* Fill in the middle.
    BF_SOFT = &H1000                             '/* Use for softer buttons.
    BF_ADJUST = &H2000                           '/* Calculate the space left over.
    BF_FLAT = &H4000                             '/* For flat rather than 3-D borders.
    BF_MONO = &H8000&                            '/* For monochrome borders
End Enum

' 窗口指定一个新位置和状态，用于SetWindowPos函数
 Enum KhanSetWindowPosStyles
    HWND_BOTTOM = 1                              '/* 将窗口置于窗口列表底部 */
    HWND_NOTOPMOST = -2                          '/* 将窗口置于列表顶部，并位于任何最顶部窗口的后面 */
    HWND_TOP = 0                                 '/* 将窗口置于Z序列的顶部；Z序列代表在分级结构中，窗口针对一个给定级别的窗口显示的顺序 */
    HWND_TOPMOST = -1                            '/* 将窗口置于列表顶部，并位于任何最顶部窗口的前面 */
    SWP_SHOWWINDOW = &H40                        '/* 显示窗口 */
    SWP_HIDEWINDOW = &H80                        '/* 隐藏窗口 */
    SWP_FRAMECHANGED = &H20                      '/* 强迫一条WM_NCCALCSIZE消息进入窗口，即使窗口的大小没有改变 */
    SWP_NOACTIVATE = &H10                        '/* 不激活窗口 */
    SWP_NOCOPYBITS = &H100                       '
    SWP_NOMOVE = &H2                             '/* 保持当前位置（x和y设定将被忽略） */
    SWP_NOOWNERZORDER = &H200                    '/* Don't do owner Z ordering */
    SWP_NOREDRAW = &H8                           '/* 窗口不自动重画 */
    SWP_NOREPOSITION = SWP_NOOWNERZORDER         '
    SWP_NOSIZE = &H1                             '/* 保持当前大小（cx和cy会被忽略） */
    SWP_NOZORDER = &H4                           '/* 保持窗口在列表的当前位置（hWndInsertAfter将被忽略） */
    SWP_DRAWFRAME = SWP_FRAMECHANGED             '/* 围绕窗口画一个框 */
'    HWND_BROADCAST = &HFFFF&
'    HWND_DESKTOP = 0
End Enum

' 指定创建窗口的风格
 Enum KhanCreateWindowSytles
    ' CreateWindow
    WS_BORDER = &H800000                         '/* 创建一个单边框的窗口。
    WS_CAPTION = &HC00000                        '/* 创建一个有标题框的窗口（包括WS_BODER风格）。
    WS_CHILD = &H40000000                        '/* 创建一个子窗口。这个风格不能与WS_POPVP风格合用。
    WS_CHILDWINDOW = (WS_CHILD)                  '/* 与WS_CHILD相同。
    WS_CLIPCHILDREN = &H2000000                  '/* 当在父窗口内绘图时，排除子窗口区域。在创建父窗口时使用这个风格。
    WS_CLIPSIBLINGS = &H4000000                  '/* 排除子窗口之间的相对区域，也就是，当一个特定的窗口接收到WM_PAINT消息时，WS_CLIPSIBLINGS 风格将所有层叠窗口排除在绘图之外，只重绘指定的子窗口。如果未指定WS_CLIPSIBLINGS风格，并且子窗口是层叠的，则在重绘子窗口的客户区时，就会重绘邻近的子窗口。
    WS_DISABLED = &H8000000                      '/* 创建一个初始状态为禁止的子窗口。一个禁止状态的窗日不能接受来自用户的输人信息。
    WS_DLGFRAME = &H400000                       '/* 创建一个带对话框边框风格的窗口。这种风格的窗口不能带标题条。
    WS_GROUP = &H20000                           '/* 指定一组控制的第一个控制。这个控制组由第一个控制和随后定义的控制组成，自第二个控制开始每个控制，具有WS_GROUP风格，每个组的第一个控制带有WS_TABSTOP风格，从而使用户可以在组间移动。用户随后可以使用光标在组内的控制间改变键盘焦点。
    WS_HSCROLL = &H100000                        '/* 创建一个有水平滚动条的窗口。
    WS_MAXIMIZE = &H1000000                      '/* 创建一个具有最大化按钮的窗口。该风格不能与WS_EX_CONTEXTHELP风格同时出现，同时必须指定WS_SYSMENU风格。
    WS_MAXIMIZEBOX = &H10000                     '/*
    WS_MINIMIZE = &H20000000                     '/* 创建一个初始状态为最小化状态的窗口。
    WS_ICONIC = WS_MINIMIZE                      '/* 创建一个初始状态为最小化状态的窗口。与WS_MINIMIZE风格相同。
    WS_MINIMIZEBOX = &H20000                     '/*
    WS_OVERLAPPED = &H0&                         '/* 产生一个层叠的窗口。一个层叠的窗口有一个标题条和一个边框。与WS_TILED风格相同
    WS_POPUP = &H80000000                        '/* 创建一个弹出式窗口。该风格不能与WS_CHLD风格同时使用。
    WS_SYSMENU = &H80000                         '/* 创建一个在标题条上带有窗口菜单的窗口，必须同时设定WS_CAPTION风格。
    WS_TABSTOP = &H10000                         '/* 创建一个控制，这个控制在用户按下Tab键时可以获得键盘焦点。按下Tab键后使键盘焦点转移到下一具有WS_TABSTOP风格的控制。
    WS_THICKFRAME = &H40000                      '/* 创建一个具有可调边框的窗口。
    WS_SIZEBOX = WS_THICKFRAME                   '/* 与WS_THICKFRAME风格相同
    WS_TILED = WS_OVERLAPPED                     '/* 产生一个层叠的窗口。一个层叠的窗口有一个标题和一个边框。与WS_OVERLAPPED风格相同。
    WS_VISIBLE = &H10000000                      '/* 创建一个初始状态为可见的窗口。
    WS_VSCROLL = &H200000                        '/* 创建一个有垂直滚动条的窗口。
    WS_OVERLAPPEDWINDOW = (WS_OVERLAPPED Or WS_CAPTION Or WS_SYSMENU Or WS_THICKFRAME Or WS_MINIMIZEBOX Or WS_MAXIMIZEBOX)
    WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW         '/* 创建一个具有WS_OVERLAPPED，WS_CAPTION，WS_SYSMENU MS_THICKFRAME．
    WS_POPUPWINDOW = (WS_POPUP Or WS_BORDER Or WS_SYSMENU) '/* 创建一个具有WS_BORDER，WS_POPUP,WS_SYSMENU风格的窗口，WS_CAPTION和WS_POPUPWINDOW必须同时设定才能使窗口某单可见。
    ' CreateWindowEx
    WS_EX_ACCEPTFILES = &H10&                    '/* 指定以该风格创建的窗口接受一个拖拽文件。
    WS_EX_APPWINDOW = &H40000                    '/* 当窗口可见时，将一个顶层窗口放置到任务条上。
    WS_EX_CLIENTEDGE = &H200                     '/* 指定窗口有一个带阴影的边界。
    WS_EX_CONTEXTHELP = &H400                    '/* 在窗口的标题条包含一个问号标志。当用户点击了问号时，鼠标光标变为一个问号的指针、如果点击了一个子窗口，则子窗日接收到WM_HELP消息。子窗口应该将这个消息传递给父窗口过程，父窗口再通过HELP_WM_HELP命令调用WinHelp函数。这个Help应用程序显示一个包含子窗口帮助信息的弹出式窗口。 WS_EX_CONTEXTHELP不能与WS_MAXIMIZEBOX和WS_MINIMIZEBOX同时使用。
    WS_EX_CONTROLPARENT = &H10000                '/* 允许用户使用Tab键在窗口的子窗口间搜索。
    WS_EX_DLGMODALFRAME = &H1&                   '/* 创建一个带双边的窗口；该窗口可以在dwStyle中指定WS_CAPTION风格来创建一个标题栏。
    WS_EX_LEFT = &H0                             '/* 窗口具有左对齐属性，这是缺省设置的。
    WS_EX_LEFTSCROLLBAR = &H4000                 '/* 如果外壳语言是如Hebrew，Arabic，或其他支持reading order alignment的语言，则标题条（如果存在）则在客户区的左部分。若是其他语言，在该风格被忽略并且不作为错误处理。
    WS_EX_LTRREADING = &H0                       '/* 窗口文本以LEFT到RIGHT（自左向右）属性的顺序显示。这是缺省设置的。
    WS_EX_MDICHILD = &H40                        '/* 创建一个MDI子窗口。
    WS_EX_NOACTIVATE = &H8000000                 '/*
    WS_EX_NOPATARENTNOTIFY = &H4&                '/* 指明以这个风格创建的窗口在被创建和销毁时不向父窗口发送WM_PARENTNOTFY消息。
    WS_EX_OVERLAPPEDWINDOW = &H300               '/*
    WS_EX_PALETTEWINDOW = &H188                  '/* WS_EX_WINDOWEDGE, WS_EX_TOOLWINDOW和WS_WX_TOPMOST风格的组合WS_EX_RIGHT:窗口具有普通的右对齐属性，这依赖于窗口类。只有在外壳语言是如Hebrew,Arabic或其他支持读顺序对齐（reading order alignment）的语言时该风格才有效，否则，忽略该标志并且不作为错误处理。
    WS_EX_RIGHT = &H1000                         '/*
    WS_EX_RIGHTSCROLLBAR = &H0                   '/* 垂直滚动条在窗口的右边界。这是缺省设置的。
    WS_EX_RTLREADING = &H2000                    '/* 如果外壳语言是如Hebrew，Arabic，或其他支持读顺序对齐（reading order alignment）的语言，则窗口文本是一自左向右）RIGHT到LEFT顺序的读出顺序。若是其他语言，在该风格被忽略并且不作为错误处理。
    WS_EX_STATICEDGE = &H20000                   '/* 为不接受用户输入的项创建一个3一维边界风格。
    WS_EX_TOOLWINDOW = &H80                      '/*
    WS_EX_TOPMOST = &H8&                         '/* 指明以该风格创建的窗口应放置在所有非最高层窗口的上面并且停留在其L，即使窗口未被激活。使用函数SetWindowPos来设置和移去这个风格。
    WS_EX_TRANSPARENT = &H20&                    '/* 指定以这个风格创建的窗口在窗口下的同属窗口已重画时，该窗口才可以重画。
    WS_EX_WINDOWEDGE = &H100
End Enum

' Windows环境有关的信息，用于GetSystemMetrics函数
 Enum KhanSystemMetricsFlags
    SM_CXSCREEN = 0                              '/* 屏幕大小 */
    SM_CYSCREEN = 1                              '/* 屏幕大小 */
    SM_CXVSCROLL = 2                             '/* 垂直滚动条中的箭头按钮的大小 */
    SM_CYHSCROLL = 3                             '/* 水平滚动条上的箭头大小 */
    SM_CYCAPTION = 4                             '/* 窗口标题的高度 */
    SM_CXBORDER = 5                              '/* 尺寸不可变边框的大小 */
    SM_CYBORDER = 6                              '/* 尺寸不可变边框的大小 */
    SM_CXDLGFRAME = 7                            '/* 对话框边框的大小 */
    SM_CYDLGFRAME = 8                            '/* 对话框边框的大小 */
    SM_CYVTHUMB = 9                              '/* 滚动块在水平滚动条上的大小 */
    SM_CXHTHUMB = 10                             '/* 滚动块在水平滚动条上的大小 */
    SM_CXICON = 11                               '/* 标准图标的大小 */
    SM_CYICON = 12                               '/* 标准图标的大小 */
    SM_CXCURSOR = 13                             '/* 标准指针大小 */
    SM_CYCURSOR = 14                             '/* 标准指针大小 */
    SM_CYMENU = 15                               '/* 菜单高度 */
    SM_CXFULLSCREEN = 16                         '/* 最大化窗口客户区的大小 */
    SM_CYFULLSCREEN = 17                         '/* 最大化窗口客户区的大小 */
    SM_CYKANJIWINDOW = 18                        '/* Kanji窗口的大小（Height of Kanji window） */
    SM_MOUSEPRESENT = 19                         '/* 如安装了鼠标则为TRUE */
    SM_CYVSCROLL = 20                            '/* 垂直滚动条中的箭头按钮的大小 */
    SM_CXHSCROLL = 21                            '/* 水平滚动条上的箭头大小 */
    SM_DEBUG = 22                                '/* 如windows的调试版正在运行，则为TRUE */
    SM_SWAPBUTTON = 23
    SM_RESERVED1 = 24
    SM_RESERVED2 = 25
    SM_RESERVED3 = 26
    SM_RESERVED4 = 27
    SM_CXMIN = 28                                '/* 窗口的最小尺寸 */
    SM_CYMIN = 29                                '/* 窗口的最小尺寸 */
    SM_CXSIZE = 30                               '/* 标题栏位图的大小 */
    SM_CYSIZE = 31                               '/* 标题栏位图的大小 */
    SM_CXFRAME = 32                              '/* 尺寸可变边框的大小（在win95和nt 4.0中使用SM_C?FIXEDFRAME） */
    SM_CYFRAME = 33                              '/* 尺寸可变边框的大小 */
    SM_CXMINTRACK = 34                           '/* 窗口的最小轨迹宽度 */
    SM_CYMINTRACK = 35                           '/* 窗口的最小轨迹宽度 */
    SM_CXDOUBLECLK = 36                          '/* 双击区域的大小（指定屏幕上一个特定的显示区域，只有在这个区域内连续进行两次鼠标单击，才有可能被当作双击事件处理） */
    SM_CYDOUBLECLK = 37                          '/* 双击区域的大小 */
    SM_CXICONSPACING = 38                        '/* 桌面图标之间的间隔距离。在win95和nt 4.0中是指大图标的间距 */
    SM_CYICONSPACING = 39                        '/* 桌面图标之间的间隔距离。在win95和nt 4.0中是指大图标的间距 */
    SM_MENUDROPALIGNMENT = 40                    '/* 如弹出式菜单对齐菜单栏项目的左侧，则为零 */
    SM_PENWINDOWS = 41                           '/* 如装载了支持笔窗口的DLL，则表示笔窗口的句柄 */
    SM_DBCSENABLED = 42                          '/* 如支持双字节则为TRUE */
    SM_CMOUSEBUTTONS = 43                        '/* 鼠标按钮（按键）的数量。如没有鼠标，就为零 */
    SM_CMETRICS = 44                             '/* 可用系统环境的数量 */
End Enum

' SetMapMode
 Enum KhanMapModeStyles
    MM_ANISOTROPIC = 8                           '/* 逻辑单位转换成具有任意比例轴的任意单位，用SetWindowExtEx和SetViewportExtEx函数可指定单位、方向和比例。
    MM_HIENGLISH = 5                             '/* 每个逻辑单位转换为0.001inch(英寸)，X的正方面向右，Y的正方向向上
    MM_HIMETRIC = 3                              '/* 每个逻辑单位转换为0.01millimeter(毫米)，X正方向向右，Y的正方向向上。
    MM_ISOTROPIC = 7                             '/* 视口和窗口范围任意，只是x和y逻辑单元尺寸要相同
    MM_LOENGLISH = 4                             '/* 每个逻辑单位转换为英寸，X正方向向右，Y正方向向上。
    MM_LOMETRIC = 2                              '/* 每个逻辑单位转换为毫米，X正方向向右，Y正方向向上。
    MM_TEXT = 1                                  '/* 每个逻辑单位转换为一个设置备素，X正方向向右，Y正方向向下。
    MM_TWIPS = 6                                 '/* 每个逻辑单位转换为1 twip (1/1440 inch)，X正方向向右，Y方向向上。
End Enum

' GetROP2,SetROP2
 Enum EnumDrawModeFlags
    R2_BLACK = 1                                 '/* 黑色
    R2_COPYPEN = 13                              '/* 画笔颜色
    R2_LAST = 16
    R2_MASKNOTPEN = 3                            '/* 画笔颜色的反色与显示颜色进行AND运算
    R2_MASKPEN = 9                               '/* 显示颜色与画笔颜色进行AND运算
    R2_MASKPENNOT = 5                            '/* 显示颜色的反色与画笔颜色进行AND运算
    R2_MERGENOTPEN = 12                          '/* 画笔颜色的反色与显示颜色进行OR运算
    R2_MERGEPEN = 15                             '/* 画笔颜色与显示颜色进行OR运算
    R2_MERGEPENNOT = 14                          '/* 显示颜色的反色与画笔颜色进行OR运算
    R2_NOP = 11                                  '/* 不变
    R2_NOT = 6                                   '/* 当前显示颜色的反色
    R2_NOTCOPYPEN = 4                            '/* R2_COPYPEN的反色
    R2_NOTMASKPEN = 8                            '/* R2_MASKPEN的反色
    R2_NOTMERGEPEN = 2                           '/* R2_MERGEPEN的反色
    R2_NOTXORPEN = 10                            '/* R2_XORPEN的反色
    R2_WHITE = 16                                '/* 白色
    R2_XORPEN = 7                                '/* 显示颜色与画笔颜色进行异或运算
End Enum

' ======================================================================================
' Types
' ======================================================================================

Private Type tagINITCOMMONCONTROLSEX              '/* icc
   dwSize                   As Long              '/* size of this structure
   dwICC                    As Long              '/* flags indicating which classes to be initialized.
End Type

Private Type POINTAPI
   X                        As Long
   Y                        As Long
End Type

 Private Type RECT
   Left                     As Long
   Top                      As Long
   Right                    As Long
   Bottom                   As Long
End Type

Private Type LOGPEN
    lopnStyle               As Long
    lopnWidth               As POINTAPI
    lopnColor               As Long
End Type

Private Type LOGBRUSH
   lbStyle                  As Long
   lbColor                  As Long
   lbHatch                  As Long
End Type

' 这个结构包含了附加的绘图参数，函数DrawTextEx
Private Type DRAWTEXTPARAMS
    cbSize                  As Long              '/* Specifies the structure size, in bytes */
    iTabLength              As Long              '/* Specifies the size of each tab stop, in units equal to the average character width */
    iLeftMargin             As Long              '/* Specifies the left margin, in units equal to the average character width */
    iRightMargin            As Long              '/* Specifies the right margin, in units equal to the average character width */
    uiLengthDrawn           As Long              '/* Receives the number of characters processed by DrawTextEx, including white-space characters. */
                                                 '/* The number can be the length of the string or the index of the first line that falls below the drawing area. */
                                                 '/* Note that DrawTextEx always processes the entire string if the DT_NOCLIP formatting flag is specified */
End Type

Private Const LF_FACESIZE   As Long = 32
 Private Type LOGFONT
   lfHeight                 As Long              '/* The font size (see below) */
   lfWidth                  As Long              '/* Normally you don't set this, just let Windows create the Default */
   lfEscapement             As Long              '/* The angle, in 0.1 degrees, of the font */
   lfOrientation            As Long              '/* Leave as default */
   lfWeight                 As Long              '/* Bold, Extra Bold, Normal etc */
   lfItalic                 As Byte              '/* As it says */
   lfUnderline              As Byte              '/* As it says */
   lfStrikeOut              As Byte              '/* As it says */
   lfCharSet                As Byte              '/* As it says */
   lfOutPrecision           As Byte              '/* Leave for default */
   lfClipPrecision          As Byte              '/* Leave for defaultv
   lfQuality                As Byte              '/* Leave for default */
   lfPitchAndFamily         As Byte              '/* Leave for default */
   lfFaceName(LF_FACESIZE)  As Byte              '/* The font name converted to a byte array */
End Type

Private Type ICONINFO
   fIcon                    As Long
   xHotspot                 As Long
   yHotspot                 As Long
   hbmMask                  As Long
   hbmColor                 As Long
End Type

Private Type IMAGEINFO
    hBitmapImage            As Long
    hBitmapMask             As Long
    cPlanes                 As Long
    cBitsPerPixel           As Long
    rcImage                 As RECT
End Type

'/* DIB 的文件大小及架构讯息 */
Private Type BITMAPFILEHEADER
    bfType                  As Integer           '/* 指定文件类型，必须 BM("magic cookie" - must be "BM" (19778)) */
    bfSize                  As Long              '/* 指定位图文件大小，以位元组为单位 */
    bfReserved1             As Integer           '/* 保留，必须设为0 */
    bfReserved2             As Integer           '/* 同上 */
    bfOffBits               As Long              '/* 从此架构到位图数据位的位元组偏移量 */
End Type

'/* 设备无关位图 (DIB)的大小及颜色信息  (它位于 bmp 文件的开头处) 40 bytes */
 Private Type BITMAPINFOHEADER
    biSize                  As Long              '/* 结构长度 */
    biWidth                 As Long              '/* 指定位图的宽度，以像素为单位 */
    biHeight                As Long              '/* 指定位图的高度，以像素为单位 */
    biPlanes                As Integer           '/* 指定目标设备的级数(必须为 1 ) */
    biBitCount              As Integer           '/* 位图的颜色位数,每一个像素的位(1，4，8，16，24，32) */
    biCompression           As Long              '/* 指定压缩类型(BI_RGB 为不压缩) */
    biSizeImage             As Long              '/* 图象的大小,以字节为单位,当用BI_RGB格式是,可设置为0 */
    biXPelsPerMeter         As Long              '/* 指定设备水准分辨率，以每米的像素为单位 */
    biYPelsPerMeter         As Long              '/* 垂直分辨率，其他同上 */
    biClrUsed               As Long              '/* 说明位图实际使用的彩色表中的颜色索引数,设为0的话,说明使用所有调色板项 */
    biClrImportant          As Long              '/* 说明对图象显示有重要影响的颜色索引的数目，如果是0，表示都重要 */
End Type

'/* 描述了由红、绿、蓝组成的颜色组合 */
 Private Type RGBQUAD
    rgbBlue                 As Byte
    rgbGreen                As Byte
    rgbRed                  As Byte
    rgbReserved             As Byte              '/* '保留，必须为 0 */
End Type

Private Type BITMAPINFO
    bmiHeader               As BITMAPINFOHEADER
    bmiColors               As RGBQUAD
End Type

 Private Type BITMAPINFO_1BPP
   bmiHeader                As BITMAPINFOHEADER
   bmiColors(0 To 1)        As RGBQUAD
End Type

 Private Type BITMAPINFO_4BPP
   bmiHeader                As BITMAPINFOHEADER
   bmiColors(0 To 15)       As RGBQUAD
End Type

 Private Type BITMAPINFO_8BPP
   bmiHeader                As BITMAPINFOHEADER
   bmiColors(0 To 255)      As RGBQUAD
End Type

 Private Type BITMAPINFO_ABOVE8
   bmiHeader                As BITMAPINFOHEADER
End Type

 Private Type BITMAP
    bmType                  As Long              '/* Type of bitmap */
    bmWidth                 As Long              '/* Pixel width */
    bmHeight                As Long              '/* Pixel height */
    bmWidthBytes            As Long              '/* Byte width = 3 x Pixel width */
    bmPlanes                As Integer           '/* Color depth of bitmap */
    bmBitsPixel             As Integer           '/* Bits per pixel, must be 16 or 24 */
    bmBits                  As Long              '/* This is the pointer to the bitmap data */
End Type

' AlphaBlend
 Private Type BLENDFUNCTION
   BlendOp                  As Byte
   BlendFlags               As Byte
   SourceConstantAlpha      As Byte
   AlphaFormat              As Byte
End Type

Private Const FW_NORMAL = 400
Private Const FW_BOLD = 700
Private Const FF_DONTCARE = 0
Private Const DEFAULT_QUALITY = 0
Private Const DEFAULT_PITCH = 0
Private Const DEFAULT_CHARSET = 1

Private Const MAX_PATH = 260

' ======================================================================================
' Types:
' ======================================================================================

Private Type PICTDESC
    cbSizeofStruct  As Long
    picType         As Long
    hImage          As Long
    xExt            As Long
    yExt            As Long
End Type

Private Type Guid
    Data1           As Long
    Data2           As Integer
    Data3           As Integer
    Data4(0 To 7)   As Byte
End Type

' ======================================================================================
' API declares:
' ======================================================================================

Private Declare Function GetTempFileName Lib "kernel32" Alias "GetTempFileNameA" (ByVal lpszPath As String, ByVal lpPrefixString As String, ByVal wUnique As Long, ByVal lpTempFileName As String) As Long
Private Declare Function GetTempPath Lib "kernel32" Alias "GetTempPathA" (ByVal nBufferLength As Long, ByVal lpBuffer As String) As Long
Private Declare Function lstrlen Lib "kernel32" Alias "lstrlenA" (ByVal lpString As String) As Long
Private Declare Function OleCreatePictureIndirect Lib "olepro32.dll" (lpPictDesc As PICTDESC, riid As Guid, ByVal fPictureOwnsHandle As Long, IPic As IPicture) As Long
' 设置指定设备场景的绘图模式。与vb的DrawMode属性完全一致
Private Declare Function SetROP2 Lib "gdi32" (ByVal hdc As Long, ByVal nDrawMode As Long) As Long
Private Declare Function MulDiv Lib "kernel32" (ByVal nNumber As Long, ByVal nNumerator As Long, ByVal nDenominator As Long) As Long

' ======================================================================================
' API declares:
' ======================================================================================

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃-----------------------------消息函数和消息列队函数---------------------------------┃
'┃                                                                                    ┃
'
' 调用一个窗口的窗口函数，将一条消息发给那个窗口。除非消息处理完毕，否则该函数不会返回。
' SendMessageBynum， SendMessageByString是该函数的“类型安全”声明形式
 Private Declare Function SendMessage Lib "user32" Alias "SendMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, lParam As Any) As Long
 Private Declare Function SendMessageByString Lib "user32" Alias "SendMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As String) As Long
 Private Declare Function SendMessageByLong Lib "user32" Alias "SendMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
' 将一条消息投递到指定窗口的消息队列。投递的消息会在Windows事件处理过程中得到处理。
' 在那个时候，会随同投递的消息调用指定窗口的窗口函数。特别适合那些不需要立即处理的窗口消息的发送
 Private Declare Function PostMessage Lib "user32" Alias "PostMessageA" (ByVal hwnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃--------------------------------窗口函数(Window)------------------------------------┃
'┃                                                                                    ┃
'
' Creating new windows:
 Private Declare Function CreateWindowEx Lib "user32" Alias "CreateWindowExA" (ByVal dwExStyle As Long, ByVal lpClassName As String, ByVal lpWindowName As String, ByVal dwStyle As Long, ByVal X As Long, ByVal Y As Long, ByVal nWidth As Long, ByVal nHeight As Long, ByVal hWndParent As Long, ByVal hMenu As Long, ByVal hInstance As Long, lpParam As Any) As Long
' 最小化指定的窗口。窗口不会从内存中清除
 Private Declare Function CloseWindow Lib "user32" (ByVal hwnd As Long) As Long
' 破坏（即清除）指定的窗口以及它的所有子窗口
 Private Declare Function DestroyWindow Lib "user32" (ByVal hwnd As Long) As Long
' 在指定的窗口里允许或禁止所有鼠标及键盘输入
 Private Declare Function EnableWindow Lib "user32" (ByVal hwnd As Long, ByVal fEnable As Long) As Long
' 在窗口列表中寻找与指定条件相符的第一个子窗口
 Private Declare Function FindWindowEx Lib "user32" Alias "FindWindowExA" (ByVal hWnd1 As Long, ByVal hWnd2 As Long, ByVal lpsz1 As String, ByVal lpsz2 As String) As Long
' 判断指定窗口的父窗口
 Private Declare Function GetParent Lib "user32" (ByVal hwnd As Long) As Long
' 指定一个窗口的新父（在vb里使用：利用这个函数，vb可以多种形式支持子窗口。
' 例如，可将控件从一个容器移至窗体中的另一个。用这个函数在窗体间移动控件是相当冒险的，
' 但却不失为一个有效的办法。如真的这样做，请在关闭任何一个窗体之前，注意用SetParent将控件的父设回原来的那个）
 Private Declare Function SetParent Lib "user32" (ByVal hWndChild As Long, ByVal hWndNewParent As Long) As Long
' 锁定指定窗口，禁止它更新。同时只能有一个窗口处于锁定状态
 Private Declare Function LockWindowUpdate Lib "user32" (ByVal hwndLock As Long) As Long
' 强制立即更新窗口，窗口中以前屏蔽的所有区域都会重画
' 在vb里使用：如vb窗体或控件的任何部分需要更新，可考虑直接使用refresh方法
 Private Declare Function UpdateWindow Lib "user32" (ByVal hwnd As Long) As Long
' 判断一个窗口句柄是否有效
 Private Declare Function IsWindow Lib "user32" (ByVal hwnd As Long) As Long
' 控制窗口的可见性
 Private Declare Function ShowWindow Lib "user32" (ByVal hwnd As Long, ByVal nCmdShow As Long) As Long
' 改变指定窗口的位置和大小。顶级窗口可能受最大或最小尺寸的限制，那些尺寸优先于这里设置的参数
 Private Declare Function MoveWindow Lib "user32" (ByVal hwnd As Long, ByVal X As Long, ByVal Y As Long, ByVal nWidth As Long, ByVal nHeight As Long, ByVal bRepaint As Long) As Long
' 这个函数能为窗口指定一个新位置和状态。它也可改变窗口在内部窗口列表中的位置。
' 该函数与DeferWindowPos函数相似，只是它的作用是立即表现出来的
' 在vb里使用：针对vb窗体，如它们在win32下屏蔽或最小化，则需重设最顶部状态。
' 如有必要，请用一个子类处理模块来重设最顶部状态)
' 参数
' hwnd             欲定位的窗口
' hWndInsertAfter  窗口句柄。在窗口列表中，窗口hwnd会置于这个窗口句柄的后面，参看本模块枚举KhanSetWindowPosStyles
' x                窗口新的x坐标。如hwnd是一个子窗口，则x用父窗口的客户区坐标表示
' y                窗口新的y坐标。如hwnd是一个子窗口，则y用父窗口的客户区坐标表示
' cx               指定新的窗口宽度
' cy               指定新的窗口高度
' wFlags           包含了旗标的一个整数，参看本模块枚举KhanSetWindowPosStyles
 Private Declare Function SetWindowPos Lib "user32" (ByVal hwnd As Long, ByVal hWndInsertAfter As Long, ByVal X As Long, ByVal Y As Long, ByVal cx As Long, ByVal cy As Long, ByVal wFlags As Long) As Long
' 从指定窗口的结构中取得信息，nIndex参数参看本模块常量声明
 Private Declare Function GetWindowLong Lib "user32" Alias "GetWindowLongA" (ByVal hwnd As Long, ByVal nIndex As Long) As Long
' 在窗口结构中为指定的窗口设置信息，nIndex参数参看本模块常量声明
 Private Declare Function SetWindowLong Lib "user32" Alias "SetWindowLongA" (ByVal hwnd As Long, ByVal nIndex As Long, ByVal dwNewLong As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃------------------------------窗口类函数(Window Class)------------------------------┃
'┃                                                                                    ┃
'
' 为指定的窗口取得类名
 Private Declare Function GetClassName Lib "user32" Alias "GetClassNameA" (ByVal hwnd As Long, ByVal lpClassName As String, ByVal nMaxCount As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃-----------------------------鼠标输入函数(Mouse Input)------------------------------┃
'
' 获得一个窗口的句柄，这个窗口位于当前输入线程，且拥有鼠标捕获（鼠标活动由它接收）
 Private Declare Function GetCapture Lib "user32" () As Long
' 将鼠标捕获设置到指定的窗口。在鼠标按钮按下的时候，这个窗口会为当前应用程序或整个系统接收所有鼠标输入
 Private Declare Function SetCapture Lib "user32" (ByVal hwnd As Long) As Long
' 为当前的应用程序释放鼠标捕获
 Private Declare Function ReleaseCapture Lib "user32" () As Long
' 可以模拟一次鼠标事件，比如左键单击、双击和右键单击等
 Private Declare Sub mouse_event Lib "user32" (ByVal dwFlags As Long, ByVal dx As Long, ByVal dy As Long, ByVal cButtons As Long, ByVal dwExtraInfo As Long)
' 这个函数判断指定的点是否位于矩形lpRect内部
' Private Declare Function PtInRect Lib "user32" (lpRect As RECT, pt As POINTAPI) As Long
Private Declare Function PtInRect Lib "user32" (lpRect As RECT, ByVal X As Long, ByVal Y As Long) As Long

'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃-----------------------------键盘输入函数(Mouse Input)------------------------------┃
'
' 获得拥有输入焦点的窗口的句柄
 Private Declare Function GetFocus Lib "user32" () As Long
' 输入焦点设到指定的窗口
 Private Declare Function SetFocus Lib "user32" (ByVal hwnd As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃----------------坐标空间与变换函数(Coordinate Space Transtormation)-----------------┃
'
' 判断窗口内以客户区坐标表示的一个点的屏幕坐标
Private Declare Function ClientToScreen Lib "user32" (ByVal hwnd As Long, lpPoint As POINTAPI) As Long
' 判断屏幕上一个指定点的客户区坐标
Private Declare Function ScreenToClient Lib "user32" (ByVal hwnd As Long, lpPoint As POINTAPI) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃---------------------------设备场景函数(Device Context)-----------------------------┃
'
' 创建一个与特定设备场景一致的内存设备场景。在绘制之前，先要为该设备场景选定一个位图。
' 不再需要时，该设备场景可用DeleteDC函数删除。删除前，其所有对象应回复初始状态
 Private Declare Function CreateCompatibleDC Lib "gdi32" (ByVal hdc As Long) As Long
' 为专门设备创建设备场景
 Private Declare Function CreateDCAsNull Lib "gdi32" Alias "CreateDCA" (ByVal lpDriverName As String, lpDeviceName As Any, lpOutput As Any, lpInitData As Any) As Long
' 获取指定窗口的设备场景，用本函数获取的设备场景一定要用ReleaseDC函数释放，不能用DeleteDC
 Private Declare Function GetDC Lib "user32" (ByVal hwnd As Long) As Long
' 释放由调用GetDC或GetWindowDC函数获取的指定设备场景。它对类或私有设备场景无效（但这样的调用不会造成损害）
 Private Declare Function ReleaseDC Lib "user32" (ByVal hwnd As Long, ByVal hdc As Long) As Long
' 删除专用设备场景或信息场景，释放所有相关窗口资源。不要将它用于GetDC函数取回的设备场景
 Private Declare Function DeleteDC Lib "gdi32" (ByVal hdc As Long) As Long
' 每个设备场景都可能有选入其中的图形对象。其中包括位图、刷子、字体、画笔以及区域等等。
' 一次选入设备场景的只能有一个对象。选定的对象会在设备场景的绘图操作中使用。
' 例如，当前选定的画笔决定了在设备场景中描绘的线段颜色及样式
' 返回值通常用于获得选入DC的对象的原始值。
' 绘图操作完成后，原始的对象通常选回设备场景。在清除一个设备场景前，务必注意恢复原始的对象
 Private Declare Function SelectObject Lib "gdi32" (ByVal hdc As Long, ByVal hObject As Long) As Long
' 用这个函数删除GDI对象，比如画笔、刷子、字体、位图、区域以及调色板等等。对象使用的所有系统资源都会被释放
' 不要删除一个已选入设备场景的画笔、刷子或位图。如删除以位图为基础的阴影（图案）刷子，
' 位图不会由这个函数删除——只有刷子被删掉
 Private Declare Function DeleteObject Lib "gdi32" (ByVal hObject As Long) As Long
'根据指定设备场景代表的设备的功能返回信息
 Private Declare Function GetDeviceCaps Lib "gdi32" (ByVal hdc As Long, ByVal nIndex As Long) As Long
' 取得对指定对象进行说明的一个结构
' lpObject 任何类型，用于容纳对象数据的结构。
' 针对画笔，通常是一个LOGPEN结构；针对扩展画笔，通常是EXTLOGPEN；
' 针对字体是LOGBRUSH；针对位图是BITMAP；针对DIBSection位图是DIBSECTION；
' 针对调色板，应指向一个整型变量，代表调色板中的条目数量
 Private Declare Function GetObjectAPI Lib "gdi32" Alias "GetObjectA" (ByVal hObject As Long, ByVal nCount As Long, lpObject As Any) As Long
' 在窗口（由设备场景代表）中水平和（或）垂直滚动矩形
Private Declare Function ScrollDC Lib "user32" (ByVal hdc As Long, ByVal dx As Long, ByVal dy As Long, lprcScroll As RECT, lprcClip As RECT, ByVal hrgnUpdate As Long, lprcUpdate As RECT) As Long
' 将两个区域组合为一个新区域
Private Declare Function CombineRgn Lib "gdi32" (ByVal hDestRgn As Long, ByVal hSrcRgn1 As Long, ByVal hSrcRgn2 As Long, ByVal nCombineMode As Long) As Long
' 创建一个由点X1，Y1和X2，Y2描述的矩形区域，不用时一定要用DeleteObject函数删除该区域
Private Declare Function CreateRectRgn Lib "gdi32" (ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Long
' 创建一个由lpRect确定的矩形区域
Private Declare Function CreateRectRgnIndirect Lib "gdi32" (lpRect As RECT) As Long
' 创建一个圆角矩形，该矩形由X1，Y1-X2，Y2确定，并由X3，Y3确定的椭圆描述圆角弧度
' 用该函数创建的区域与用RoundRect API函数画的圆角矩形不完全相同，因为本矩形的右边和下边不包括在区域之内
 Private Declare Function CreateRoundRectRgn Lib "gdi32" (ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long, ByVal X3 As Long, ByVal Y3 As Long) As Long
' 用指定刷子填充指定区域
 Private Declare Function FillRgn Lib "gdi32" (ByVal hdc As Long, ByVal hRgn As Long, ByVal hBrush As Long) As Long
' 用指定刷子围绕指定区域画一个外框
 Private Declare Function FrameRgn Lib "gdi32" (ByVal hdc As Long, ByVal hRgn As Long, ByVal hBrush As Long, ByVal nWidth As Long, ByVal nHeight As Long) As Long
 Private Declare Function GetMapMode Lib "gdi32" (ByVal hdc As Long) As Long
Private Declare Function SetMapMode Lib "gdi32" (ByVal hdc As Long, ByVal nMapMode As Long) As Long
' 这是那些很难有人注意到的对编程者来说是个巨大的宝藏的隐含的API函数中的一个。本函数允许您改变窗口的区域。
' 通常所有窗口都是矩形的——窗口一旦存在就含有一个矩形区域。本函数允许您放弃该区域。
' 这意味着您可以创建圆的、星形的窗口，也可以将它分为两个或许多部分——实际上可以是任何形状
 Private Declare Function SetWindowRgn Lib "user32" (ByVal hwnd As Long, ByVal hRgn As Long, ByVal bRedraw As Long) As Long
' 该函数选择一个区域作为指定设备环境的当前剪切区域
 Private Declare Function SelectClipRgn Lib "gdi32" (ByVal hdc As Long, ByVal hRgn As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃---------------------------------位图函数(Bitmap)-----------------------------------┃
'
' 该函数用来显示透明或半透明像素的位图。
 Private Declare Function AlphaBlend Lib "msimg32.dll" (ByVal hdcDest As Long, ByVal xDest As Long, ByVal yDest As Long, ByVal WidthDest As Long, ByVal HeightDest As Long, ByVal hdcSrc As Long, ByVal xSrc As Long, ByVal ySrc As Long, ByVal WidthSrc As Long, ByVal HeightSrc As Long, ByVal Blendfunc As Long) As Long
' 将一幅位图从一个设备场景复制到另一个。源和目标DC相互间必须兼容
' 在NT环境下，如在一次世界传输中要求在源设备场景中进行剪切或旋转处理，这个函数的执行会失败
' 如目标和源DC的映射关系要求矩形中像素的大小必须在传输过程中改变，
' 那么这个函数会根据需要自动伸缩、旋转、折叠、或切断，以便完成最终的传输过程
' dwRop：指定光栅操作代码。这些代码将定义源矩形区域的颜色数据，如何与目标矩形区域的颜色数据组合以完成最后的颜色。
 Private Declare Function BitBlt Lib "gdi32" (ByVal hDestDC As Long, ByVal X As Long, ByVal Y As Long, ByVal nWidth As Long, ByVal nHeight As Long, ByVal hSrcDC As Long, ByVal xSrc As Long, ByVal ySrc As Long, ByVal dwRop As Long) As Long
' 创建一幅与设备有关位图
Private Declare Function CreateBitmapIndirect Lib "gdi32" (lpBitmap As BITMAP) As Long
' 创建一幅与设备有关位图，它与指定的设备场景兼容
' 内存设备场景即与彩色位图兼容，也与单色位图兼容。这个函数的作用是创建一幅与当前选入hdc中的场景兼容。
' 对一个内存场景来说，默认的位图是单色的。倘若内存设备场景有一个DIBSection选入其中，
' 这个函数就会返回DIBSection的一个句柄。如hdc是一幅设备位图，
' 那么结果生成的位图就肯定兼容于设备（也就是说，彩色设备生成的肯定是彩色位图）
' 如果nWidth和nHeight为零，返回的位图就是一个1×1的单色位图
' 一旦位图不再需要，一定用DeleteObject函数释放它占用的内存及资源
 Private Declare Function CreateCompatibleBitmap Lib "gdi32" (ByVal hdc As Long, ByVal nWidth As Long, ByVal nHeight As Long) As Long
' 该函数由与设备无关的位图（DIB）创建与设备有关的位图（DDB），并且有选择地为位图置位。
Private Declare Function CreateDIBitmap Lib "gdi32" (ByVal hdc As Long, lpInfoHeader As BITMAPINFOHEADER, ByVal dwUsage As Long, lpInitBits As Any, lpInitInfo As BITMAPINFO, ByVal wUsage As Long) As Long
' 该函数创建应用程序可以直接写入的、与设备无关的位图（DIB）。
' 该函数提供一个指针，该指针指向位图位数据值的地方。
' 可以给文件映射对象提供句柄，函数使用文件映射对象来创建位图，或者让系统为位图分配内存。
Private Declare Function CreateDIBSection Lib "gdi32" (ByVal hdc As Long, pBitmapInfo As BITMAPINFO, ByVal un As Long, ByVal lplpVoid As Long, ByVal Handle As Long, ByVal dw As Long) As Long
' 复制位图、图标或指针，同时在复制过程中进行一些转换工作
 Private Declare Function CopyImage Lib "user32" (ByVal Handle As Long, ByVal un1 As Long, ByVal n1 As Long, ByVal n2 As Long, ByVal un2 As Long) As Long
' 载入一个位图、图标或指针
 Private Declare Function LoadImage Lib "user32" Alias "LoadImageA" (ByVal hInst As Long, ByVal lpsz As String, ByVal un1 As Long, ByVal n1 As Long, ByVal n2 As Long, ByVal un2 As Long) As Long
 Private Declare Function LoadImageLong Lib "user32" Alias "LoadImageA" (ByVal hInst As Long, ByVal lpsz As Long, ByVal un1 As Long, ByVal n1 As Long, ByVal n2 As Long, ByVal un2 As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃----------------------------------图标函数(Icon)------------------------------------┃
'
' 制作指定图标或鼠标指针的一个副本。这个副本从属于发出调用的应用程序
 Private Declare Function CopyIcon Lib "user32" (ByVal hIcon As Long) As Long
' 创建一个图标
Private Declare Function CreateIconIndirect Lib "user32" (piconinfo As ICONINFO) As Long
' 该函数清除图标和释放任何被图标占用的存储空间。
 Private Declare Function DestroyIcon Lib "user32" (ByVal hIcon As Long) As Long
' 该函数在限定的设备上下文窗口的客户区域绘制图标
 Private Declare Function DrawIcon Lib "user32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long, ByVal hIcon As Long) As Long
' 该函数在限定的设备上下文窗口的客户区域绘制图标，执行限定的光栅操作，并按特定要求伸长或压缩图标或光标。
 Private Declare Function DrawIconEx Lib "user32" (ByVal hdc As Long, ByVal xLeft As Long, ByVal yTop As Long, ByVal hIcon As Long, ByVal cxWidth As Long, ByVal cyWidth As Long, ByVal istepIfAniCur As Long, ByVal hbrFlickerFreeDraw As Long, ByVal diFlags As Long) As Boolean
' 取得与图标有关的信息
Private Declare Function GetIconInfo Lib "user32" (ByVal hIcon As Long, piconinfo As ICONINFO) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃---------------------------------光标函数(Cursor)-----------------------------------┃
'
 Private Declare Function CopyCursor Lib "user32" (ByVal hcur As Long) As Long
' 从指定的模块或应用程序实例中载入一个鼠标指针。LoadCursorBynum是LoadCursor函数的类型安全声明
 Private Declare Function LoadCursor Lib "user32" Alias "LoadCursorA" (ByVal hInstance As Long, ByVal lpCursorName As Long) As Long
' 该函数销毁一个光标并释放它占用的任何内存，不要使用该函数去消毁一个共享光标。
 Private Declare Function DestroyCursor Lib "user32" (ByVal hCursor As Long) As Long
' 获取鼠标指针的当前位置
Private Declare Function GetCursorPos Lib "user32" (lpPoint As POINTAPI) As Long
' 该函数把光标移到屏幕的指定位置。如果新位置不在由 ClipCursor函数设置的屏幕矩形区域之内，
' 则系统自动调整坐标，使得光标在矩形之内。
 Private Declare Function SetCursorPos Lib "user32" (ByVal X As Long, ByVal Y As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃-----------------------------笔刷函数(Pen and Brush)---------------------------------┃
'
' 用指定的样式、宽度和颜色创建一个画笔，用DeleteObject函数将其删除
 Private Declare Function CreatePen Lib "gdi32" (ByVal nPenStyle As Long, ByVal nWidth As Long, ByVal crColor As Long) As Long
' 根据指定的LOGPEN结构创建一个画笔
Private Declare Function CreatePenIndirect Lib "gdi32" (lpLogPen As LOGPEN) As Long
' 创建一个扩展画笔（装饰或几何）
Private Declare Function ExtCreatePen Lib "gdi32" (ByVal dwPenStyle As Long, ByVal dwWidth As Long, lplb As LOGBRUSH, ByVal dwStyleCount As Long, lpStyle As Long) As Long
' 在一个LOGBRUSH数据结构的基础上创建一个刷子
Private Declare Function CreateBrushIndirect Lib "gdi32" (lpLogBrush As LOGBRUSH) As Long
' 该函数可以创建一个具有指定阴影模式和颜色的逻辑刷子。
 Private Declare Function CreateHatchBrush Lib "gdi32" (ByVal nIndex As Long, ByVal crColor As Long) As Long
' 该函数可以创建具有指定位图模式的逻辑刷子，该位图不能是DIB类型的位图，DIB位图是由CreateDIBSection函数创建的。
 Private Declare Function CreatePatternBrush Lib "gdi32" (ByVal hBitmap As Long) As Long
' 用纯色创建一个刷子，一旦刷子不再需要，就用DeleteObject函数将其删除
 Private Declare Function CreateSolidBrush Lib "gdi32" (ByVal crColor As Long) As Long
' 为任何一种标准系统颜色取得一个刷子，不要用DeleteObject函数删除这些刷子。
' 它们是由系统拥有的固有对象。不要将这些刷子指定成一种窗口类的默认刷子
 Private Declare Function GetSysColorBrush Lib "user32" (ByVal nIndex As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃---------------------------字体和正文函数(Font and Text)-----------------------------┃
'
' 用指定的属性创建一种逻辑字体，VB的字体属性在选择字体的时候显得更有效
Private Declare Function CreateFontIndirect Lib "gdi32" Alias "CreateFontIndirectA" (lpLogFont As LOGFONT) As Long
' 将文本描绘到指定的矩形中，wFormat标志常数参看KhanDrawTextStyles
Private Declare Function DrawText Lib "user32" Alias "DrawTextA" (ByVal hdc As Long, ByVal lpStr As String, ByVal nCount As Long, lpRect As RECT, ByVal wFormat As Long) As Long
Private Declare Function DrawTextEx Lib "user32" Alias "DrawTextExA" (ByVal hdc As Long, ByVal lpsz As String, ByVal n As Long, lpRect As RECT, ByVal un As Long, lpDrawTextParams As DRAWTEXTPARAMS) As Long
' 该函数取得指定设备环境的当前正文颜色。
 Private Declare Function GetTextColor Lib "gdi32" (ByVal hdc As Long) As Long
' 设置当前文本颜色。这种颜色也称为“前景色”，如改变了这个设置，注意恢复VB窗体或控件原始的文本颜色
 Private Declare Function SetTextColor Lib "gdi32" (ByVal hdc As Long, ByVal crColor As Long) As Long
'┃                                                                                    ┃
'┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

'┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
'┃------------------------------------绘图函数----------------------------------------┃
'
' 该函数画一段圆弧，圆弧是由一个椭圆和一条线段（称之为割线）相交限定的闭合区域。
' 此弧由当前的画笔画轮廓，由当前的画刷填充。
 Private Declare Function Chord Lib "gdi32" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long, ByVal X3 As Long, ByVal Y3 As Long, ByVal X4 As Long, ByVal Y4 As Long) As Long
' 用指定的样式描绘一个矩形的边框。利用这个函数，我们没有必要再使用许多3D边框和面板。
' 所以就资源和内存的占用率来说，这个函数的效率要高得多。它可在一定程度上提升性能
' hdc      要在其中绘图的设备场景
' qrc      要为其描绘边框的矩形
' edge     带有前缀BDR_的两个常数的组合。一个指定内部边框是上凸还是下凹；另一个则指定外部边框。有时能换用带EDGE_前缀的常数。
' grfFlags 带有BF_前缀的常数的组合
Private Declare Function DrawEdge Lib "user32" (ByVal hdc As Long, qrc As RECT, ByVal edge As Long, ByVal grfFlags As Long) As Long
' 画一个焦点矩形。这个矩形是在标志焦点的样式中通过异或运算完成的（焦点通常用一个点线表示）
' 如用同样的参数再次调用这个函数，就表示删除焦点矩形
Private Declare Function DrawFocusRect Lib "user32" (ByVal hdc As Long, lpRect As RECT) As Long
' 这个函数用于描绘一个标准控件
Private Declare Function DrawFrameControl Lib "user32" (ByVal hdc As Long, lpRect As RECT, ByVal un1 As Long, ByVal un2 As Long) As Long
' 这个函数可为一幅图象或绘图操作应用各式各样的效果
 Private Declare Function DrawState Lib "user32" Alias "DrawStateA" (ByVal hdc As Long, ByVal hBrush As Long, ByVal lpDrawStateProc As Long, ByVal lParam As Long, ByVal wParam As Long, ByVal X As Long, ByVal Y As Long, ByVal cx As Long, ByVal cy As Long, ByVal fuFlags As Long) As Long
' 该函数用于画一个椭圆，椭圆的中心是限定矩形的中心，使用当前画笔画椭圆，用当前的画刷填充椭圆。
 Private Declare Function Ellipse Lib "gdi32" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Long
' 用指定的刷子填充一个矩形，矩形的右边和底边不会描绘
Private Declare Function FillRect Lib "user32" (ByVal hdc As Long, lpRect As RECT, ByVal hBrush As Long) As Long
' 用指定的刷子围绕一个矩形画一个边框（组成一个帧），边框的宽度是一个逻辑单位
 Private Declare Function FrameRect Lib "user32" (ByVal hdc As Long, lpRect As RECT, ByVal hBrush As Long) As Long
' 取得指定设备场景当前的背景颜色
 Private Declare Function GetBkColor Lib "gdi32" (ByVal hdc As Long) As Long
' 针对指定的设备场景，取得当前的背景填充模式
 Private Declare Function GetBkMode Lib "gdi32" (ByVal hdc As Long) As Long
' 为指定的设备场景设置背景颜色。背景颜色用于填充阴影刷子、虚线画笔以及字符（如背景模式为OPAQUE）中的空隙。
' 也在位图颜色转换期间使用。背景实际是设备能够显示的最接近于 crColor 的颜色
 Private Declare Function SetBkColor Lib "gdi32" (ByVal hdc As Long, ByVal crColor As Long) As Long
' 指定阴影刷子、虚线画笔以及字符中的空隙的填充方式，背景模式不会影响用扩展画笔描绘的线条
 Private Declare Function SetBkMode Lib "gdi32" (ByVal hdc As Long, ByVal nBkMode As Long) As Long
' 在指定的设备场景中取得一个像素的RGB值
 Private Declare Function GetPixel Lib "gdi32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long) As Long
' 在指定的设备场景中设置一个像素的RGB值
 Private Declare Function SetPixel Lib "gdi32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long, ByVal crColor As Long) As Long
' 将来自一幅位图的二进制位复制到一幅与设备无关的位图里
' Private Declare Function GetDIBits Lib "gdi32" ( ByVal aHDC As Long, ByVal hBitmap As Long, ByVal nStartScan As Long, ByVal nNumScans As Long, lpBits As Any, lpBI As BITMAPINFO, ByVal wUsage As Long) As Long
 Private Declare Function GetDIBits Lib "gdi32" (ByVal aHDC As Long, ByVal hBitmap As Long, ByVal nStartScan As Long, ByVal nNumScans As Long, lpBits As Any, lpBI As Any, ByVal wUsage As Long) As Long
' 将来自与设备无关位图的二进制位复制到一幅与设备有关的位图里
 Private Declare Function SetDIBits Lib "gdi32" (ByVal hdc As Long, ByVal hBitmap As Long, ByVal nStartScan As Long, ByVal nNumScans As Long, lpBits As Any, lpBI As Any, ByVal wUsage As Long) As Long
' 针对指定的设备场景，获得多边形填充模式。
 Private Declare Function GetPolyFillMode Lib "gdi32" (ByVal hdc As Long) As Long
' 设置多边形的填充模式
 Private Declare Function SetPolyFillMode Lib "gdi32" (ByVal hdc As Long, ByVal nPolyFillMode As Long) As Long
' 针对指定的设备场景，取得当前的绘图模式。这样可定义绘图操作如何与正在显示的图象合并起来
' 这个函数只对光栅设备有效
 Private Declare Function GetROP2 Lib "gdi32" (ByVal hdc As Long) As Long
' 设置指定设备场景的绘图模式。

' 用当前画笔画一条线，从当前位置连到一个指定的点。这个函数调用完毕，当前位置变成x,y点
 Private Declare Function LineTo Lib "gdi32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long) As Long
' 为指定的设备场景指定一个新的当前画笔位置。前一个位置保存在lpPoint中
 Private Declare Function MoveToEx Lib "gdi32" (ByVal hdc As Long, ByVal X As Long, ByVal Y As Long, lpPoint As POINTAPI) As Long
' 该函数画一个由椭圆和两条半径相交闭合而成的饼状楔形图，此饼图由当前画笔画轮廓，由当前画刷填充。
 Private Declare Function Pie Lib "gdi32" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long, ByVal X3 As Long, ByVal Y3 As Long, ByVal X4 As Long, ByVal Y4 As Long) As Long
' 该函数画一个由直线相闻的两个以上顶点组成的多边形，用当前画笔画多边形轮廓，
' 用当前画刷和多边形填充模式填充多边形。
 Private Declare Function Polygon Lib "gdi32" (ByVal hdc As Long, lpPoint As POINTAPI, ByVal nCount As Long) As Long
' 用当前画笔描绘一系列线段。使用PolylineTo函数时，当前位置会设为最后一条线段的终点。
' 它不会由Polyline函数改动
 Private Declare Function Polyline Lib "gdi32" (ByVal hdc As Long, lpPoint As POINTAPI, ByVal nCount As Long) As Long
 Private Declare Function PolyPolygon Lib "gdi32" (ByVal hdc As Long, lpPoint As POINTAPI, lpPolyCounts As Long, ByVal nCount As Long) As Long
 Private Declare Function PolyPolyline Lib "gdi32" (ByVal hdc As Long, lppt As POINTAPI, lpdwPolyPoints As Long, ByVal cCount As Long) As Long
' 该函数画一个矩形，用当前的画笔画矩形轮廓，用当前画刷进行填充。
 Private Declare Function Rectangle Lib "gdi32" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long) As Long
' 函数画一个带圆角的矩形，此矩形由当前画笔画轮廊，由当前画刷填充。
 Private Declare Function RoundRect Lib "gdi32" (ByVal hdc As Long, ByVal X1 As Long, ByVal Y1 As Long, ByVal X2 As Long, ByVal Y2 As Long, ByVal X3 As Long, ByVal Y3 As Long) As Long
' 这个函数用于增大或减小一个矩形的大小。
' x加在右侧区域，并从左侧区域减去；如x为正，则能增大矩形的宽度；如x为负，则能减小它。
' y对顶部与底部区域产生的影响是是类似的
 Private Declare Function InflateRect Lib "user32" (lpRect As RECT, ByVal X As Long, ByVal Y As Long) As Long
' 该函数通过应用一个指定的偏移，从而让矩形移动起来。
' x会添加到右侧和左侧区域。y添加到顶部和底部区域。
' 偏移方向则取决于参数是正数还是负数，以及采用的是什么坐标系统
 Private Declare Function OffsetRect Lib "user32" (lpRect As RECT, ByVal X As Long, ByVal Y As Long) As Long
' 返回与windows环境有关的信息，nIndex值参看本模块的常量声明
 Private Declare Function GetSystemMetrics Lib "user32" (ByVal nIndex As Long) As Long
' 获得整个窗口的范围矩形，窗口的边框、标题栏、滚动条及菜单等都在这个矩形内
 Private Declare Function GetWindowRect Lib "user32" (ByVal hwnd As Long, lpRect As RECT) As Long
' 返回指定窗口客户区矩形的大小
 Private Declare Function GetClientRect Lib "user32" (ByVal hwnd As Long, lpRect As RECT) As Long
' 这个函数屏蔽一个窗口客户区的全部或部分区域。这会导致窗口在事件期间部分重画
 Private Declare Function InvalidateRect Lib "user32" (ByVal hwnd As Long, lpRect As RECT, ByVal bErase As Long) As Long
' 判断指定windows显示对象的颜色，颜色对象看本模块声明
 Private Declare Function GetSysColor Lib "user32" (ByVal nIndex As Long) As Long


 Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (lpvDest As Any, lpvSource As Any, ByVal cbCopy As Long)
 Private Declare Function OleTranslateColor Lib "olepro32.dll" (ByVal OLE_COLOR As Long, ByVal hPalette As Long, pccolorref As Long) As Long

'Initializes the entire common control dynamic-link library.
'Exported by all versions of Comctl32.dll.
 Private Declare Sub InitCommonControls Lib "Comctl32" ()
'Initializes specific common controls classes from the common
'control dynamic-link library.
'Returns TRUE (non-zero) if successful, or FALSE otherwise.
'Began being exported with Comctl32.dll version 4.7 (IE3.0 & later).
 Private Declare Function InitCommonControlsEx Lib "Comctl32" (lpInitCtrls As tagINITCOMMONCONTROLSEX) As Boolean
 Private Declare Function ImageList_GetBkColor Lib "Comctl32" (ByVal hImageList As Long) As Long
 Private Declare Function ImageList_ReplaceIcon Lib "Comctl32" (ByVal hImageList As Long, ByVal i As Long, ByVal hIcon As Long) As Long
 Private Declare Function ImageList_Convert Lib "Comctl32" Alias "ImageList_Draw" (ByVal hImageList As Long, ByVal ImgIndex As Long, ByVal hdcDest As Long, ByVal X As Long, ByVal Y As Long, ByVal Flags As Long) As Long
 Private Declare Function ImageList_Create Lib "Comctl32" (ByVal MinCx As Long, ByVal MinCy As Long, ByVal Flags As Long, ByVal cInitial As Long, ByVal cGrow As Long) As Long
 Private Declare Function ImageList_AddMasked Lib "Comctl32" (ByVal hImageList As Long, ByVal hbmImage As Long, ByVal crMask As Long) As Long
 Private Declare Function ImageList_Replace Lib "Comctl32" (ByVal hImageList As Long, ByVal ImgIndex As Long, ByVal hbmImage As Long, ByVal hbmMask As Long) As Long
 Private Declare Function ImageList_Add Lib "Comctl32" (ByVal hImageList As Long, ByVal hbmImage As Long, hbmMask As Long) As Long
 Private Declare Function ImageList_Remove Lib "Comctl32" (ByVal hImageList As Long, ByVal ImgIndex As Long) As Long
 Private Declare Function ImageList_GetImageInfo Lib "COMCTL32.DLL" (ByVal hIml As Long, ByVal i As Long, pImageInfo As IMAGEINFO) As Long
 Private Declare Function ImageList_AddIcon Lib "Comctl32" (ByVal hIml As Long, ByVal hIcon As Long) As Long
 Private Declare Function ImageList_GetIcon Lib "Comctl32" (ByVal hImageList As Long, ByVal ImgIndex As Long, ByVal fuFlags As Long) As Long
 Private Declare Function ImageList_SetImageCount Lib "Comctl32" (ByVal hImageList As Long, uNewCount As Long)
 Private Declare Function ImageList_GetImageCount Lib "Comctl32" (ByVal hImageList As Long) As Long
 Private Declare Function ImageList_Destroy Lib "Comctl32" (ByVal hImageList As Long) As Long
 Private Declare Function ImageList_GetIconSize Lib "Comctl32" (ByVal hImageList As Long, cx As Long, cy As Long) As Long
 Private Declare Function ImageList_SetIconSize Lib "Comctl32" (ByVal hImageList As Long, cx As Long, cy As Long) As Long
 Private Declare Function ImageList_Draw Lib "COMCTL32.DLL" (ByVal hIml As Long, ByVal i As Long, ByVal hdcDst As Long, ByVal X As Long, ByVal Y As Long, ByVal fStyle As Long) As Long
' Draw an item in an ImageList with more control over positioning and colour:
 Private Declare Function ImageList_DrawEx Lib "COMCTL32.DLL" (ByVal hIml As Long, ByVal i As Long, ByVal hdcDst As Long, ByVal X As Long, ByVal Y As Long, ByVal dx As Long, ByVal dy As Long, ByVal rgbBk As Long, ByVal rgbFg As Long, ByVal fStyle As Long) As Long
 Private Declare Function ImageList_GetImageRect Lib "COMCTL32.DLL" (ByVal hIml As Long, ByVal i As Long, prcImage As RECT) As Long
 Private Declare Function ImageList_LoadImage Lib "Comctl32" Alias "ImageList_LoadImageA" (ByVal hInst As Long, ByVal lpbmp As String, ByVal cx As Long, ByVal cGrow As Long, ByVal crMask As Long, ByVal uType As Long, ByVal uFlags As Long)
 Private Declare Function ImageList_SetBkColor Lib "Comctl32" (ByVal hImageList As Long, ByVal clrBk As Long) As Long
 Private Declare Function ImageList_Copy Lib "Comctl32" (ByVal himlDst As Long, ByVal iDst As Long, ByVal himlSrc As Long, ByVal iSrc As Long, ByVal uFlags As Long) As Long

' ======================================================================================
' Enums
' ======================================================================================

' 边框样式
Public Enum GPTAB_BORDERSTYLE_METHOD
   GpTabBorderStyleNone = 0               ' 没有边框
   GpTabBorderStyle3D = 1                 ' 3D
   GpTabBorderStyle3DThin = 2             ' 3DThin
End Enum

' 样式
Public Enum GPTAB_STYLE_METHOD
    GpTabStyleStandard = 0                '/* Win32 风格
    GpTabStyleWinXP = 1                   '/* XP 风格
End Enum

' 选项卡布局
Public Enum GPTAB_PLACEMENT_METHOD
    GpTabPlacementTopleft = 0
    GpTabPlacementTopRight = 1
    GpTabPlacementBottomLeft = 2
    GpTabPlacementBottomRight = 3
    GpTabPlacementLeftTop = 4
    GpTabPlacementLeftBottom = 5
    GpTabPlacementRightTop = 6
    GpTabPlacementRightBottom = 7
End Enum

' 选项卡样式
Public Enum GPTAB_TABSTYLE_METHOD
    GpTabRectangle = 0                 '
    GpTabRoundRect = 1                 '
    GpTabTrapezoid = 2                 '
End Enum

' 选项卡宽度
Public Enum GPTAB_TABWIDTHSTYLE_METHOD
    GpTabJustified = 0                 '
    GpTabnonJustified = 1              '
    GpTabFixed = 2                     '
End Enum

Public Enum GPTAB_XPCOLORSCHEME_METHOD
    GpTabUseWindows = 0
    GpTabCustom = 1
End Enum

' ======================================================================================
' Types
' ======================================================================================

' 便于标志扩展
Private Type TabState
    Index As Long       ' 用于存放Tab的Index
End Type

Private Type TabListType
    list() As TabState  ' 取得每行中Tab的Index
    Count As Long       ' 一行中Tab的个数
End Type

' ======================================================================================
' Private variables:
' ======================================================================================

' Icons:
Private m_hIml                    As Long
Private m_lIconSizeX              As Long
Private m_lIconSizeY              As Long
Private m_lngFontHeight           As Long
Private m_lngDefaultTabHeight     As Long

Private m_lngXPFaceColor         As Long
Private m_oleBackColor As OLE_COLOR      ' 控件的背景颜色
Private m_oleTabColor As OLE_COLOR       ' 选项卡不可选时颜色
Private m_oleTabColorActive As OLE_COLOR ' 选项卡激活时颜色
Private m_oleTabColorHover As OLE_COLOR  ' 选项卡热跟踪时颜色
Private m_oleTabBorderColor As OLE_COLOR ' XP风格,GpTabBorderStyleNone控件边框的颜色
Private m_blnAutoBackColor As Boolean ' 判断控件的背景颜色是否随父窗体的背景颜色改变而改变
Private m_blnUserMode As Boolean ' 控件运行在设计阶段?运行阶段?
Private m_blnEnabled As Boolean ' Enable
Private m_blnHotTracking As Boolean ' 热跟踪
Private m_blnMultiRow As Boolean

Private m_lngTabFixedHeight As Long     ' 定制Tab的高度
Private m_lngTabFixedWidth As Long      ' 定制Tab的宽度

Private m_udtMainRect         As RECT     ' 主区域
Private m_lngCurrentList        As Long  ' 当前Tab列表的索引
Private m_lngListCount         As Long  ' Tab有几行
Private m_aryTabList()       As TabListType

Private m_udtBorderStyle As GPTAB_BORDERSTYLE_METHOD
Private m_udtXPColorScheme As GPTAB_XPCOLORSCHEME_METHOD
Private m_udtPlacement As GPTAB_PLACEMENT_METHOD
Private m_udtStyle As GPTAB_STYLE_METHOD
Private m_udtTabStyle As GPTAB_TABSTYLE_METHOD
Private m_udtTabWidthStyle As GPTAB_TABWIDTHSTYLE_METHOD
Private m_udtDrawTextParams           As DRAWTEXTPARAMS
Private m_clsSelectTab As cTabItem
Private m_clsHoverTab As cTabItem
Private WithEvents m_clsTabs As cTabItems
Attribute m_clsTabs.VB_VarHelpID = -1

' ======================================================================================
' Events
' ======================================================================================
Public Event Click()
Public Event KeyDown(KeyCode As Integer, Shift As Integer)
Public Event KeyPress(KeyAscii As Integer)
Public Event KeyUp(KeyCode As Integer, Shift As Integer)
Public Event MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event OLECompleteDrag(Effect As Long)
Public Event OLEDragDrop(Data As DataObject, Effect As Long, Button As Integer, Shift As Integer, X As Single, Y As Single)
Public Event OLEDragOver(Data As DataObject, Effect As Long, Button As Integer, Shift As Integer, X As Single, Y As Single, State As Integer)
Public Event OLEGiveFeedback(Effect As Long, DefaultCursors As Boolean)
Public Event OLESetData(Data As DataObject, DataFormat As Integer)
Public Event OLEStartDrag(Data As DataObject, AllowedEffects As Long)
Public Event TabClick()

Public Property Get AutoBackColor() As Boolean
    AutoBackColor = m_blnAutoBackColor
End Property

Public Property Let AutoBackColor(ByVal New_AutoBackColor As Boolean)
    m_blnAutoBackColor = New_AutoBackColor
    PropertyChanged "AutoBackColor"
    Call pvDraw
End Property

'/* 背景颜色（默认为-1，随父窗体的颜色而改变） */
Public Property Get BackColor() As OLE_COLOR
    BackColor = m_oleBackColor
End Property

Public Property Let BackColor(ByVal New_BackColor As OLE_COLOR)
    m_oleBackColor = VerifyColor(New_BackColor)
    If Not m_blnAutoBackColor Then UserControl.BackColor = m_oleBackColor
    PropertyChanged "BackColor"
    Call pvDraw
End Property

Public Property Get BorderStyle() As GPTAB_BORDERSTYLE_METHOD
    BorderStyle = m_udtBorderStyle
End Property

Public Property Let BorderStyle(ByVal New_BorderStyle As GPTAB_BORDERSTYLE_METHOD)
    m_udtBorderStyle = New_BorderStyle
    PropertyChanged "BorderStyle"
    Call pvCalculateSize
    Call pvDraw
End Property

Public Property Get Enable() As Boolean
    Enable = m_blnEnabled
End Property

Public Property Let Enable(ByVal New_Enable As Boolean)
    m_blnEnabled = New_Enable
    PropertyChanged "Enable"
    Call pvDraw
End Property

Public Property Get Font() As StdFont
    Set Font = UserControl.Font
End Property

Public Property Set Font(ByVal New_Font As StdFont)
    Dim udtFont As LOGFONT
     
    Set UserControl.Font = New_Font
    Set lblFont.Font = New_Font
    '/* 取得当前字体下文字的高度 */
    m_lngFontHeight = lblFont.Height + 1
    If m_lngFontHeight > m_lIconSizeY Then
       m_lngDefaultTabHeight = m_lngFontHeight + InflateFontHeight
    Else
       m_lngDefaultTabHeight = m_lIconSizeY + InflateIconHeight
    End If
'    If m_lngFontHeight > m_lIconSizeY Then
'       m_cListItems.DefaultListitemHeight = m_lngFontHeight
'    Else
'       m_cListItems.DefaultListitemHeight = m_lIconSizeY
'    End If
    
'    If m_lngFontHeight > m_lngColumnIconHeight Then
'       If m_lngFontHeight > m_cHeader.Height Then
'          m_cHeader.Height = m_lngFontHeight
'       End If
'    Else
'       If m_lngColumnIconHeight > m_cHeader.Height Then
'          m_cHeader.Height = m_lngColumnIconHeight
'       End If
'    End If
    PropertyChanged "Font"
    Call pvDraw
End Property

Public Property Get ForeColor() As OLE_COLOR
   ForeColor = UserControl.ForeColor
End Property

Public Property Let ForeColor(ByVal New_ForeColor As OLE_COLOR)
   UserControl.ForeColor = VerifyColor(New_ForeColor)
   PropertyChanged "ForeColor"
    Call pvDraw
End Property

Public Property Get hdc() As Long
    hdc = UserControl.hdc
End Property

Public Function HitTest(ByVal X As Single, ByVal Y As Single) As cTabItem
    Dim lngI As Long
    Dim lngY As Long
    Dim tR As RECT
    Const ProcName = "HitTest"
    
    On Error GoTo ErrorHandle
    
    ' 判断鼠标是否出界
    If X < 0 Or X > UserControl.ScaleWidth Or Y < 0 Or Y > UserControl.ScaleHeight Then
       Set HitTest = Nothing
       Exit Function
    End If
    ' 判断鼠标是否在主区域内
    If Y >= m_udtMainRect.Top Then
       Set HitTest = Nothing
       Exit Function
    End If
    If m_clsTabs Is Nothing Or m_clsTabs.Count <= 0 Then
       Set HitTest = Nothing
       Exit Function
    End If
    Select Case m_udtTabStyle
           Case GpTabRectangle, GpTabRoundRect
             For lngI = 1 To m_clsTabs.Count
                 With m_clsTabs.Item(lngI)
                      tR.Top = .Top
                      tR.Left = .Left
                      tR.Right = .Left + .Width
                      tR.Bottom = .Top + .Height
                 End With
                 If PtInRect(tR, X, Y) <> 0 Then
                    Set HitTest = m_clsTabs.Item(lngI)
                    Exit Function
                 End If
             Next lngI
           Case GpTabTrapezoid
             For lngI = 1 To m_clsTabs.Count
                 With m_clsTabs.Item(lngI)
                      tR.Top = .Top
                      tR.Left = .Left + .Height
                      tR.Right = .Left + .Width
                      tR.Bottom = .Top + .Height
                 End With
                 If PtInRect(tR, X, Y) <> 0 Then
                    Set HitTest = m_clsTabs.Item(lngI)
                    Exit Function
                 End If
                 
'                 With m_clsTabs.Item(lngI)
'                      For lngY = 1 To .Height
'                          If Y = lngY And X >= .Left - .Height - 1 Then
'                             Set HitTest = m_clsTabs.Item(lngI)
'                             Exit Function
'                          End If
'                      Next lngY
'                 End With
             Next lngI
    End Select
    Exit Function
ErrorHandle:
    Select Case ShowError(ProcName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
    End Select
End Function

Public Property Get HotTracking() As Boolean
    HotTracking = m_blnHotTracking
End Property

Public Property Let HotTracking(ByVal New_HotTracking As Boolean)
    m_blnHotTracking = New_HotTracking
    PropertyChanged "HotTracking"
End Property

Public Property Get hwnd() As Long
    hwnd = UserControl.hwnd
End Property

Public Property Let ImageList(New_ImageList As Variant)
    Dim hIml As Long
    
    ' Set the ImageList handle property either from a VB
    ' image list or directly:
    If VarType(New_ImageList) = vbObject Then
       ' Assume VB ImageList control.  Note that unless
       ' some call has been made to an object within a
       ' VB ImageList the image list itself is not
       ' created.  Therefore hImageList returns error. So
       ' ensure that the ImageList has been initialised by
       ' drawing into nowhere:
       On Error Resume Next
       ' Get the image list initialised..
       New_ImageList.ListImages(1).Draw 0, 0, 0, 1
       hIml = New_ImageList.hImageList
       If (Err.Number <> 0) Then
           hIml = 0
       End If
       On Error GoTo 0
    ElseIf VarType(New_ImageList) = vbLong Then
       ' Assume ImageList handle:
       hIml = New_ImageList
    Else
       Err.Raise vbObjectError + 1049, "GpTabs." & App.EXEName, "ImageList property expects ImageList object or long hImageList handle."
    End If
    
    ' If we have a valid image list, then associate it with the control:
    If (hIml <> 0) Then
       m_hIml = hIml
       Call ImageList_GetIconSize(m_hIml, m_lIconSizeX, m_lIconSizeY)
       m_lIconSizeY = m_lIconSizeY + 2
       If m_lngFontHeight > m_lIconSizeY Then
          m_lngDefaultTabHeight = m_lngFontHeight + InflateFontHeight
       Else
          m_lngDefaultTabHeight = m_lIconSizeY + InflateIconHeight
       End If
    End If
End Property

' 鼠标Icon
Public Property Get MouseIcon() As StdPicture
    Set MouseIcon = UserControl.MouseIcon
End Property

Public Property Set MouseIcon(ByVal New_MouseIcon As StdPicture)
    Set UserControl.MouseIcon = New_MouseIcon
    PropertyChanged "MouseIcon"
End Property

' 鼠标样式
Public Property Get MousePointer() As MousePointerConstants
    MousePointer = UserControl.MousePointer
End Property

Public Property Let MousePointer(ByVal New_MousePointer As MousePointerConstants)
    UserControl.MousePointer() = New_MousePointer
    PropertyChanged "MousePointer"
End Property

' 多列显示
Public Property Get MultiRow() As Boolean
    MultiRow = m_blnMultiRow
End Property

Public Property Let MultiRow(ByVal New_MultiRow As Boolean)
    m_blnMultiRow = New_MultiRow
    PropertyChanged "MultiRow"
    Call pvDraw
End Property

' 选项卡布局
Public Property Get Placement() As GPTAB_PLACEMENT_METHOD
    Placement = m_udtPlacement
End Property

Public Property Let Placement(ByVal New_Placement As GPTAB_PLACEMENT_METHOD)
    m_udtPlacement = New_Placement
    PropertyChanged "Placement"
    Call pvDraw
End Property

' 计算构成主区域各个点的坐标
Private Sub pvCalculateRect(ByRef DstPoint() As POINTAPI, _
                            ByVal Count As Long, _
                            ByVal Top As Long, _
                            ByVal Left As Long, _
                            ByVal Right As Long, _
                            ByVal Bottom As Long, _
                            ByVal LeftTop As Boolean, _
                            ByVal LeftBottom As Boolean, _
                            ByVal RightBottom As Boolean, _
                            ByVal RightTop As Boolean)
    Dim lngindex            As Long
    
    ReDim DstPoint(Count - 1)
    lngindex = 0
    If LeftTop Then
       With DstPoint(lngindex)
            .X = Left + RoundRectSize
            .Y = Top
       End With
       lngindex = lngindex + 1
       With DstPoint(lngindex)
            .X = Left
            .Y = Top + RoundRectSize
       End With
       lngindex = lngindex + 1
    Else
       With DstPoint(lngindex)
            .X = Left
            .Y = Top
       End With
       lngindex = lngindex + 1
    End If
    
    If LeftBottom Then
       With DstPoint(lngindex)
            .X = Left
            .Y = Bottom - RoundRectSize
       End With
       lngindex = lngindex + 1
       With DstPoint(lngindex)
            .X = Left + RoundRectSize
            .Y = Bottom
       End With
       lngindex = lngindex + 1
    Else
       With DstPoint(lngindex)
            .X = Left
            .Y = Bottom
       End With
       lngindex = lngindex + 1
    End If
    If RightBottom Then
       With DstPoint(lngindex)
            .X = Right - RoundRectSize
            .Y = Bottom
       End With
       lngindex = lngindex + 1
       With DstPoint(lngindex)
            .X = Right
            .Y = Bottom - RoundRectSize
       End With
       lngindex = lngindex + 1
    Else
       With DstPoint(lngindex)
            .X = Right
            .Y = Bottom
       End With
       lngindex = lngindex + 1
    End If
    If RightTop Then
       With DstPoint(lngindex)
            .X = Right
            .Y = Top + RoundRectSize
       End With
       lngindex = lngindex + 1
       With DstPoint(lngindex)
            .X = Right - RoundRectSize
            .Y = Top
       End With
       lngindex = lngindex + 1
    Else
       With DstPoint(lngindex)
            .X = Right
            .Y = Top
       End With
       lngindex = lngindex + 1
    End If
    
    If LeftTop Then
       With DstPoint(lngindex)
            .X = Left + RoundRectSize
            .Y = Top
       End With
    Else
       With DstPoint(lngindex)
            .X = Left
            .Y = Top
       End With
    End If
End Sub

Private Sub pvCalculateRoundPoint(ByRef DstPoint() As POINTAPI, _
                                  ByVal Count As Long, _
                                  ByVal Top As Long, _
                                  ByVal Left As Long, _
                                  ByVal Right As Long, _
                                  ByVal Bottom As Long)
    Dim lngindex As Long
    
    lngindex = 0
    ReDim DstPoint(Count - 1)
    With DstPoint(lngindex)
         .X = Left
         .Y = Bottom
    End With
    lngindex = lngindex + 1
    With DstPoint(lngindex)
         .X = Left
         .Y = Top + RoundRectSize
    End With
    lngindex = lngindex + 1
    With DstPoint(lngindex)
         .X = Left + RoundRectSize
         .Y = Top
    End With
    lngindex = lngindex + 1
    With DstPoint(lngindex)
         .X = Right - RoundRectSize
         .Y = Top
    End With
    lngindex = lngindex + 1
    With DstPoint(lngindex)
         .X = Right
         .Y = Top + RoundRectSize
    End With
    lngindex = lngindex + 1
    With DstPoint(lngindex)
         .X = Right
         .Y = Bottom
    End With
End Sub

Private Sub pvCalculateSize()
    Dim lngI                 As Long  ' 循环记数
    Dim lngY                 As Long
    Dim lngTabCount          As Long  ' Tab的总个数
    Dim lngAllWidth          As Long  ' 所有的Tab的宽度
    Dim lngListIndex         As Long  ' 没行Tab的索引
    Dim lngListTabIndex      As Long  ' 一行Tab的索引
    Dim lngListWidth         As Long  ' 累加一行Tab的宽度
    Dim lngManualWidth       As Long  ' 定制Tab的宽度
    Dim lngManualHeight      As Long  ' 定制Tab的高度
    Dim lngWidth             As Long  ' 控件的宽度
    Dim lngHeight            As Long  ' 控件的高度
    Dim lngDiscrepancy       As Long
    Dim tR                   As RECT
    
    If m_udtBorderStyle = GpTabBorderStyleNone Then
       lngDiscrepancy = 0
    Else
       lngDiscrepancy = DiscrepancyHeight
    End If
    Const ProcName = "pvCalculateSize"
    
    On Error GoTo ErrorHandle
    
    lngWidth = UserControl.ScaleWidth - 1
    lngHeight = UserControl.ScaleHeight - 1
    With m_udtMainRect
         .Top = 0
         .Left = 0
         .Right = lngWidth
         .Bottom = lngHeight
    End With
    If m_clsTabs Is Nothing Then
       m_udtMainRect.Top = m_lngDefaultTabHeight
       Exit Sub
    End If
    
    m_lngListCount = 0
    Erase m_aryTabList
    lngManualWidth = m_lngTabFixedWidth \ Screen.TwipsPerPixelX
    lngManualHeight = m_lngTabFixedHeight \ Screen.TwipsPerPixelY
    With m_clsTabs
         lngTabCount = m_clsTabs.Count
         ' 计算每个Tab的实际最小宽度
         For lngI = 1 To lngTabCount
             Call DrawTextEx(UserControl.hdc, .Item(lngI).Caption & vbNullChar, -1, tR, _
                             DT_CALCRECT Or DT_SINGLELINE Or DT_VCENTER Or DT_CENTER, _
                             m_udtDrawTextParams)
             If m_udtTabStyle = GpTabTrapezoid Then
                .Item(lngI).DefaultWidth = tR.Right - tR.Left + InflateFontWidth + m_lngDefaultTabHeight
             Else
                .Item(lngI).DefaultWidth = tR.Right - tR.Left + InflateFontWidth
             End If
             lngAllWidth = lngAllWidth + .Item(lngI).DefaultWidth
         Next lngI
         Select Case m_udtPlacement
                Case GpTabPlacementTopleft
                  If lngTabCount <= 0 Then
                     m_udtMainRect.Top = m_lngDefaultTabHeight + lngDiscrepancy
                     Exit Sub
                  End If
                  Select Case m_udtTabWidthStyle
                         Case GpTabJustified
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = m_lngDefaultTabHeight
                               .Item(lngI).Width = .Item(lngI).DefaultWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                              ' 初试数组
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(0)
                              ' 分行,并设置每个Tab的坐标
                              m_lngListCount = 0
                              lngListTabIndex = 0
                              lngListWidth = .Item(1).Width
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Top = m_lngDefaultTabHeight + lngDiscrepancy
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Top = lngDiscrepancy
                                  If lngI > 1 Then
                                     .Item(lngI).Left = .Item(lngI - 1).Left + .Item(lngI - 1).Width + TabsInterval
                                  Else
                                     .Item(lngI).Left = 0
                                  End If
                              Next lngI
                           End If
                         Case GpTabnonJustified
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = m_lngDefaultTabHeight
                               .Item(lngI).Width = .Item(lngI).DefaultWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                              ' 初试数组
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(0)
                              ' 分行,并设置每个Tab的坐标
                              m_lngListCount = 0
                              lngListTabIndex = 0
                              lngListWidth = .Item(1).Width
                              For lngI = 1 To lngTabCount
                                  ReDim Preserve m_aryTabList(m_lngListCount).list(lngListTabIndex)
                                  With m_aryTabList(m_lngListCount).list(lngListTabIndex)
                                       .Index = lngI
                                  End With
                                  If lngListTabIndex > 0 Then
                                     .Item(lngI).Left = .Item(lngI - 1).Left + .Item(lngI - 1).Width + TabsInterval
                                  Else
                                     .Item(lngI).Left = 0
                                  End If
                                  If lngI + 1 <= lngTabCount Then
                                     lngListWidth = lngListWidth + .Item(lngI + 1).Width
                                  End If
                                  lngListTabIndex = lngListTabIndex + 1
                                  If lngListWidth > lngWidth Then
                                     ' 存储每行中Tab的个数
                                     m_aryTabList(m_lngListCount).Count = lngListTabIndex
                                     lngListTabIndex = 0
                                     m_lngListCount = m_lngListCount + 1
                                     ReDim Preserve m_aryTabList(m_lngListCount)
                                     ReDim Preserve m_aryTabList(m_lngListCount).list(0)
                                     m_aryTabList(m_lngListCount).Count = 1
                                     lngListWidth = .Item(lngI + 1).Width
                                  End If
                              Next lngI
                              ' 设置每行的高度
                              For lngI = m_lngListCount To 0 Step -1
                                  For lngY = 0 To m_aryTabList(lngI).Count - 1
                                      m_clsTabs.Item(m_aryTabList(lngI).list(lngY).Index).Top = (m_lngListCount - lngI) * m_lngDefaultTabHeight + lngDiscrepancy
                                  Next lngY
                              Next lngI
                              m_lngListCount = m_lngListCount + 1
                              ' 设置主区域的顶点
                              m_udtMainRect.Top = m_lngListCount * m_lngDefaultTabHeight + lngDiscrepancy
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Top = m_lngDefaultTabHeight + lngDiscrepancy
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Top = lngDiscrepancy
                                  If lngI > 1 Then
                                     .Item(lngI).Left = .Item(lngI - 1).Left + .Item(lngI - 1).Width + TabsInterval
                                  Else
                                     .Item(lngI).Left = 0
                                  End If
                              Next lngI
                           End If
                         Case GpTabFixed
                           lngAllWidth = lngManualWidth * lngTabCount
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = lngManualHeight
                               .Item(lngI).Width = lngManualWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                              ' 计算Tab的行数
                              m_lngListCount = CLng(lngAllWidth \ lngWidth)
                              If lngAllWidth Mod lngWidth > 0 Then m_lngListCount = m_lngListCount + 1
                              m_udtMainRect.Top = m_lngListCount * lngManualHeight + lngDiscrepancy
                              ' 初试数组
                              ReDim m_aryTabList(m_lngListCount - 1)
                              For lngI = 0 To m_lngListCount - 1
                                  ReDim m_aryTabList(lngI).list(0)
                              Next lngI
                              ' 分行,并设置每个Tab的坐标
                              lngListIndex = 0
                              lngListTabIndex = 0
                              lngListWidth = .Item(1).Width
                              For lngI = 1 To lngTabCount
                                  ReDim Preserve m_aryTabList(lngListIndex).list(lngListTabIndex)
                                  With m_aryTabList(lngListIndex).list(lngListTabIndex)
                                       .Index = lngI
                                  End With
                                  .Item(lngI).Top = (m_lngListCount - lngListIndex - 1) * m_lngDefaultTabHeight + 1
                                  If lngListTabIndex > 0 Then
                                     .Item(lngI).Left = .Item(lngI - 1).Left + .Item(lngI - 1).Width + TabsInterval
                                  Else
                                     .Item(lngI).Left = 0
                                  End If
                                  If lngI + 1 <= lngTabCount Then
                                     lngListWidth = lngListWidth + .Item(lngI + 1).Width
                                  End If
                                  If lngListWidth > lngWidth Then
                                     lngListIndex = lngListIndex + 1
                                     lngListTabIndex = 0
                                  Else
                                     lngListTabIndex = lngListTabIndex + 1
                                  End If
                                  ' 存储每行中Tab的个数
                                  If lngListIndex <= m_lngListCount - 1 Then m_aryTabList(lngListIndex).Count = lngListTabIndex
                              Next lngI
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Top = lngManualHeight + lngDiscrepancy
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Top = lngDiscrepancy
                                  If lngI > 1 Then
                                     .Item(lngI).Left = (lngManualWidth + TabsInterval) * (lngI - 1)
                                  Else
                                     .Item(lngI).Left = 0
                                  End If
                              Next lngI
                           End If
                  End Select
                Case GpTabPlacementTopRight
                  If lngTabCount <= 0 Then
                     m_udtMainRect.Top = m_lngDefaultTabHeight + lngDiscrepancy
                     Exit Sub
                  End If
                  Select Case m_udtTabWidthStyle
                         Case GpTabJustified
                         Case GpTabnonJustified
                         Case GpTabFixed
                           lngAllWidth = lngManualWidth * lngTabCount
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = lngManualHeight
                               .Item(lngI).Width = lngManualWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Top = lngManualHeight + lngDiscrepancy
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Top = lngDiscrepancy
                                  If lngI > 1 Then
                                     .Item(lngI).Left = m_udtMainRect.Right - 10 - (lngManualWidth + TabsInterval) * lngI
                                  Else
                                     .Item(lngI).Left = m_udtMainRect.Right - 10 - .Item(lngI).Width
                                  End If
                              Next lngI
                           End If
                  End Select
                Case GpTabPlacementLeftTop
                  If lngTabCount <= 0 Then
                     m_udtMainRect.Left = lngManualWidth
                     Exit Sub
                  End If
                  Select Case m_udtTabWidthStyle
                         Case GpTabJustified
                         Case GpTabnonJustified
                         Case GpTabFixed
                           lngAllWidth = lngManualWidth * lngTabCount
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = lngManualHeight
                               .Item(lngI).Width = lngManualWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Left = lngManualWidth
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Left = 0
                                  If lngI > 1 Then
                                     .Item(lngI).Top = m_udtMainRect.Top + lngManualHeight * (lngI - 1)
                                  Else
                                     .Item(lngI).Top = 0
                                  End If
                              Next lngI
                           End If
                  End Select
                Case GpTabPlacementLeftBottom
                Case GpTabPlacementBottomLeft
                Case GpTabPlacementBottomRight
                Case GpTabPlacementRightTop
                Case GpTabPlacementRightBottom
                  If lngTabCount <= 0 Then
                     m_udtMainRect.Right = m_udtMainRect.Right - lngManualWidth
                     Exit Sub
                  End If
                  Select Case m_udtTabWidthStyle
                         Case GpTabJustified
                         Case GpTabnonJustified
                         Case GpTabFixed
                           lngAllWidth = lngManualWidth * lngTabCount
                           ' 设置每个Tab的高度和宽度
                           For lngI = 1 To lngTabCount
                               .Item(lngI).Height = lngManualHeight
                               .Item(lngI).Width = lngManualWidth
                           Next lngI
                           If lngAllWidth > lngWidth Then
                           Else
                              ' 设置主区域的顶部
                              m_udtMainRect.Left = lngManualWidth
                              m_lngCurrentList = 0
                              ReDim m_aryTabList(0)
                              ReDim m_aryTabList(0).list(lngTabCount - 1)
                              m_aryTabList(0).Count = lngTabCount
                              ' 设置每个Tab的坐标
                              For lngI = 1 To lngTabCount
                                  With m_aryTabList(0).list(lngI - 1)
                                       .Index = lngI - 1
                                  End With
                                  .Item(lngI).Left = m_udtMainRect.Right
                                  If lngI > 1 Then
                                     .Item(lngI).Top = m_udtMainRect.Top + lngManualHeight * (lngI - 1)
                                  Else
                                     .Item(lngI).Top = 0
                                  End If
                              Next lngI
                           End If
                  End Select
         End Select
    End With
    Exit Sub
ErrorHandle:
    Select Case ShowError(ProcName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
    End Select
End Sub

Private Sub pvCalculateTrapezoidPoint(ByRef DstPoint() As POINTAPI, _
                                      ByVal Count As Long, _
                                      ByVal Top As Long, _
                                      ByVal Left As Long, _
                                      ByVal Right As Long, _
                                      ByVal Bottom As Long)
    Dim lngindex As Long
    
    Select Case m_udtPlacement
           Case GpTabPlacementTopleft
             lngindex = 0
             ReDim DstPoint(Count - 1)
             With DstPoint(lngindex)
                  .X = Left
                  .Y = Bottom
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Left
                  .Y = Top + RoundRectSize
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Left + RoundRectSize
                  .Y = Top
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right - (Bottom - Top)
                  .Y = Top
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right
                  .Y = Bottom
             End With
           Case GpTabPlacementTopRight
             lngindex = 0
             ReDim DstPoint(Count - 1)
             With DstPoint(lngindex)
                  .X = Left
                  .Y = Bottom
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Left + Bottom - Top
                  .Y = Top
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right - RoundRectSize
                  .Y = Top
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right
                  .Y = Top + RoundRectSize
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right
                  .Y = Bottom
             End With
           Case GpTabPlacementBottomLeft
           Case GpTabPlacementBottomRight
           Case GpTabPlacementLeftTop
           Case GpTabPlacementLeftBottom
           Case GpTabPlacementRightTop
           Case GpTabPlacementRightBottom
             lngindex = 0
             ReDim DstPoint(Count - 1)
             With DstPoint(lngindex)
                  .X = Left
                  .Y = Top
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right
                  .Y = Top + Right - Left
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right
                  .Y = Bottom - RoundRectSize
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Right - RoundRectSize
                  .Y = Bottom
             End With
             lngindex = lngindex + 1
             With DstPoint(lngindex)
                  .X = Left
                  .Y = Bottom
             End With
    End Select
End Sub

' 绘制控件界面
Private Sub pvDraw()
    Dim lngI                As Long
    Dim lngTop1             As Long
    Dim lngTop2             As Long
    Dim lngBottom1          As Long
    Dim lngBottom2          As Long
    Dim lngHeight           As Long
    Dim lngPointCount       As Long
    Dim lngBrush            As Long
    Dim lngPen              As Long
    Dim lngXPColor          As Long
    Dim lngStepXP           As Single
    Dim blnHover            As Boolean
    Dim blnLeftTop          As Boolean
    Dim blnLeftBottom       As Boolean
    Dim blnRightBottom      As Boolean
    Dim blnRightTop         As Boolean
    Dim tBR                 As RECT
    Dim udtPointA()         As POINTAPI
    Dim udtPointB()         As POINTAPI
    Const ProcName = "pvDraw"
    
    On Error GoTo ErrorHandle
    If m_lngDefaultTabHeight <= 0 Then Exit Sub
    With UserControl
         ' 设置控件背景颜色
         If m_blnAutoBackColor Then
            .BackColor = .Ambient.BackColor
         Else
            .BackColor = m_oleBackColor
         End If
         ' 清除
         .Cls
    End With
    
    ' 控件Win32风格
    If m_udtStyle = GpTabStyleStandard Then
       ' 创建刷子
       lngBrush = CreateSolidBrush(TranslateColor(m_oleTabColorActive))
       ' 填充
       Call FillRect(UserControl.hdc, m_udtMainRect, lngBrush)
       ' 删除画刷
       Call DeleteObject(lngBrush): lngBrush = 0
       If m_udtBorderStyle = GpTabBorderStyle3D Then
          Call DrawEdge(UserControl.hdc, m_udtMainRect, EDGE_RAISED, BF_RECT)
       ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
          Call DrawEdge(UserControl.hdc, m_udtMainRect, BDR_RAISEDINNER, BF_RECT)
       End If
    ' 控件WinXP风格
    ElseIf m_udtStyle = GpTabStyleWinXP Then
       If m_udtBorderStyle = GpTabBorderStyleNone Then
          ' 创建刷子
          lngBrush = CreateSolidBrush(TranslateColor(XPFlatTabColorActive))
          ' 填充
          Call FillRect(UserControl.hdc, m_udtMainRect, lngBrush)
          ' 删除画刷
          Call DeleteObject(lngBrush): lngBrush = 0
          With m_udtMainRect
               tBR.Top = .Top
               tBR.Left = .Left
               tBR.Right = .Right
               tBR.Bottom = .Top + 3
          End With
          ' 创建刷子
          lngBrush = CreateSolidBrush(TranslateColor(XPFlatBorderColor))
          ' 填充
          Call FillRect(UserControl.hdc, tBR, lngBrush)
          ' 删除画刷
          Call DeleteObject(lngBrush): lngBrush = 0
       Else
          blnLeftTop = False
          blnLeftBottom = True
          blnRightBottom = True
          blnRightTop = True
          lngPointCount = 8
          ' 设置当前画笔,边框颜色
          'lngPen = CreatePen(PS_SOLID, 1, TranslateColor(XPBorderColor))
          'Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
          With m_udtMainRect
               Call pvCalculateRect(udtPointA, lngPointCount, .Top, .Left, .Right, .Bottom, _
                                    blnLeftTop, blnLeftBottom, blnRightBottom, blnRightTop)
               lngHeight = .Bottom - .Top
               lngStepXP = 25 / lngHeight
               lngTop1 = 1
               lngTop2 = 2
               lngBottom1 = lngHeight - 1
               lngBottom2 = lngHeight
               For lngI = 1 To lngHeight
                   Select Case lngI
                            'HERE
                          Case lngTop1
                            lngXPColor = vbWhite 'BrightnessColor(m_lngXPFaceColor, -lngStepXP * lngI)
                            'Call DrawLine(UserControl.hdc, IIf(blnLeftTop, .Left + 2, .Left), .Top, _
                                          IIf(blnRightTop, .Right - 2, .Right), .Top, _
                                          lngXPColor)
                          Case lngTop2
                            'Call DrawLine(UserControl.hdc, IIf(blnLeftTop, .Left + 1, .Left), .Top + 1, _
                                          IIf(blnRightTop, .Right - 1, .Right), .Top + 1, _
                                          BrightnessColor(m_lngXPFaceColor, -lngStepXP * lngI))
                          Case lngBottom1
                            'Call DrawLine(UserControl.hdc, IIf(blnLeftBottom, .Left + 2, .Left), .Bottom - 1, _
                                          IIf(blnRightBottom, .Right - 2, .Right), .Bottom - 1, _
                                          BrightnessColor(m_lngXPFaceColor, -lngStepXP * lngI))
                          Case lngBottom2
                            'Call DrawLine(UserControl.hdc, IIf(blnLeftBottom, .Left + 1, .Left), .Bottom - 2, _
                                          IIf(blnRightBottom, .Right - 1, .Right), .Bottom - 2, _
                                          BrightnessColor(m_lngXPFaceColor, -lngStepXP * lngI))
                          Case Else
                            'Call DrawLine(UserControl.hdc, .Left, lngI + .Top - 1, .Right, lngI + .Top - 1, _
                                          BrightnessColor(m_lngXPFaceColor, -lngStepXP * lngI))
                   End Select
               Next lngI
               Call Polyline(UserControl.hdc, udtPointA(0), lngPointCount)
          End With
       End If
    End If
    If m_clsTabs.Count <= 0 Then
       Call pvDrawTab("", 0, DiscrepancyHeight, 60, m_lngDefaultTabHeight, m_oleTabColorActive, lngXPColor, True, False)
    Else
       For lngI = m_clsTabs.Count To 1 Step -1
           blnHover = False
           With m_clsTabs.Item(lngI)
                If Not (m_clsHoverTab Is Nothing) Then
                   If m_clsHoverTab.Index = lngI Then blnHover = True
                End If
                Call pvDrawTab(.Caption, .Left, .Top, .Width, .Height, m_oleTabColorActive, lngXPColor, .Selected, blnHover)
           End With
       Next lngI
    End If
    
    Exit Sub
ErrorHandle:
    Select Case ShowError(ProcName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
    End Select
End Sub

Private Sub pvDrawTab(ByVal Caption As String, _
                      ByVal Left As Long, _
                      ByVal Top As Long, _
                      ByVal Width As Long, _
                      ByVal Height As Long, _
                      ByVal TabColor As Long, _
                      ByVal TranXPColor As Long, _
                      ByVal Selected As Boolean, _
                      ByVal Hover As Boolean)
    Dim lngTop1             As Long
    Dim lngTop2             As Long
    Dim lngEdgeStyle        As Long
    Dim lngEdgeFlag         As Long
    Dim lngPen              As Long
    Dim lngOldBrush         As Long
    Dim lngXPBorderBrush    As Long
    Dim lngTabBorderBrush   As Long
    
    Dim lngFlatBrush        As Long
    Dim lngFlatActiveBrush  As Long
    Dim lngFlatHoverBrush   As Long
    Dim lngFlatBorderBrush  As Long
    
    Dim lngI                As Long
    Dim lngLightColor       As Long
    Dim lngHighLightColor   As Long
    Dim lngShadowColor      As Long
    Dim lngDarkShadowColor  As Long
    Dim lngXPColor          As Long
    Dim lngStepXP           As Single
    Dim udtPointA()         As POINTAPI
    Dim udtPointB()         As POINTAPI
    Dim udtTabRect          As RECT
    Dim udtCaptionRect      As RECT
    Const ProcName = "pvDrawTab"
    
    On Error GoTo ErrorHandle
    
    ' 取得系统显示对象的颜色
    lngShadowColor = GetSysColor(COLOR_BTNSHADOW)
    lngLightColor = GetSysColor(COLOR_BTNLIGHT)
    lngDarkShadowColor = GetSysColor(COLOR_BTNDKSHADOW)
    lngHighLightColor = GetSysColor(COLOR_BTNHIGHLIGHT)
    
    ' 建立画刷
    lngXPBorderBrush = CreateSolidBrush(TranslateColor(XPBorderColor))
    
    lngFlatBrush = CreateSolidBrush(TranslateColor(XPFlatTabColor))
    lngFlatActiveBrush = CreateSolidBrush(TranslateColor(XPFlatTabColorActive))
    lngFlatHoverBrush = CreateSolidBrush(TranslateColor(XPFlatTabColorHover))
    lngFlatBorderBrush = CreateSolidBrush(TranslateColor(XPFlatBorderColor))
    
    lngTabBorderBrush = CreateSolidBrush(TranslateColor(TabColor))
    lngOldBrush = SelectObject(UserControl.hdc, lngTabBorderBrush)
    With udtTabRect
         .Left = Left
         .Top = Top
         .Right = Left + Width
         .Bottom = Top + Height
    End With
    
    ' 控件Win32风格
    If m_udtStyle = GpTabStyleStandard Then
       Select Case m_udtTabStyle
              Case GpTabRectangle
                If m_udtBorderStyle = GpTabBorderStyle3D Then
                   lngEdgeStyle = EDGE_RAISED
                   'lngEdgeStyle = BDR_RAISEDINNER
                   If Selected Then udtTabRect.Bottom = Top + Height + 2
                ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
                   lngEdgeStyle = BDR_RAISEDINNER
                   If Selected Then udtTabRect.Bottom = Top + Height + 1
                End If
                Call FillRect(UserControl.hdc, udtTabRect, lngTabBorderBrush)
                If m_udtBorderStyle <> GpTabBorderStyleNone And Selected = True Then Call DrawEdge(UserControl.hdc, udtTabRect, lngEdgeStyle, BF_LEFT Or BF_TOP Or BF_RIGHT)
              Case GpTabRoundRect
                ReDim udtPointB(5)
                With udtTabRect
                     If m_udtBorderStyle = GpTabBorderStyle3D Then
                        .Left = .Left + 1
                        .Bottom = .Bottom + 2
                     ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
                        .Bottom = .Bottom + 1
                     End If
                     ' 取得圆角的各个点的坐标
                     Call pvCalculateRoundPoint(udtPointA, 6, .Top, _
                                                .Left, .Right, _
                                                .Bottom)
                     ' 填充
                     lngPen = CreatePen(PS_SOLID, 1, lngHighLightColor)
                     Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                     Call SelectObject(UserControl.hdc, lngTabBorderBrush)
                     Call Polygon(UserControl.hdc, udtPointA(0), 6)
                     lngPen = CreatePen(PS_SOLID, 1, lngLightColor)
                     Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                     Call pvInflatePoint(udtPointB(0), udtPointA(0), 1, 0)
                     Call pvInflatePoint(udtPointB(1), udtPointA(1), 1, 1)
                     Call pvInflatePoint(udtPointB(2), udtPointA(2), 1, 1)
                     Call pvInflatePoint(udtPointB(3), udtPointA(3), -1, 1)
                     Call pvInflatePoint(udtPointB(4), udtPointA(4), -1, 1)
                     Call pvInflatePoint(udtPointB(5), udtPointA(5), -1, 0)
                     Call Polyline(UserControl.hdc, udtPointB(0), 6)
                     If m_udtBorderStyle = GpTabBorderStyle3D Then
                        ' 去底边
                        Call DrawLine(UserControl.hdc, .Left + 1, .Bottom, .Right, .Bottom, TranslateColor(m_oleTabColorActive))
                        Call DrawLine(UserControl.hdc, .Left + 1, .Bottom + 1, .Right, .Bottom + 1, TranslateColor(m_oleTabColorActive))
                        ' 加阴影
                        Call DrawLine(UserControl.hdc, .Right - 2, .Top, .Right, .Top + 2, TranslateColor(lngDarkShadowColor))
                        Call DrawLine(UserControl.hdc, .Right - 3, Top, .Right - 1, .Top + 3, TranslateColor(lngShadowColor))
                        Call DrawLine(UserControl.hdc, .Right, .Top + 2, .Right, .Bottom, TranslateColor(lngDarkShadowColor))
                        Call DrawLine(UserControl.hdc, .Right - 1, .Top + 2, .Right - 1, .Bottom - 1, TranslateColor(lngShadowColor))
                     ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
                        ' 去底边
                        Call DrawLine(UserControl.hdc, .Left + 2, .Bottom, .Right, .Bottom, TranslateColor(m_oleTabColorActive))
                        Call DrawLine(UserControl.hdc, .Left + 2, .Bottom + 1, .Right, .Bottom + 1, TranslateColor(m_oleTabColorActive))
                        ' 加阴影
                        Call DrawLine(UserControl.hdc, .Right - 2, .Top, .Right, .Top + 2, TranslateColor(lngShadowColor))
                        Call DrawLine(UserControl.hdc, .Right, .Top + 2, .Right, .Bottom, TranslateColor(lngShadowColor))
                     End If
                End With
              Case GpTabTrapezoid
                ReDim udtPointB(4)
                With udtTabRect
                     If m_udtBorderStyle = GpTabBorderStyleNone Then
                        ' 取得圆角的各个点的坐标
                        Call pvCalculateTrapezoidPoint(udtPointA, 5, .Top, .Left, .Right, .Bottom)
                        ' 填充
                        lngPen = CreatePen(PS_SOLID, 1, lngHighLightColor)
                        Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                        Call SelectObject(UserControl.hdc, lngTabBorderBrush)
                        Call Polygon(UserControl.hdc, udtPointA(0), 5)
                     Else
                     If m_udtBorderStyle = GpTabBorderStyle3D Then
                        .Left = .Left + 1
                        .Bottom = .Bottom + 2
                     ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
                        .Bottom = .Bottom + 1
                     End If
                     ' 取得圆角的各个点的坐标
                     Call pvCalculateTrapezoidPoint(udtPointA, 5, .Top, .Left, .Right, .Bottom)
                     ' 填充
                     lngPen = CreatePen(PS_SOLID, 1, lngHighLightColor)
                     Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                     Call SelectObject(UserControl.hdc, lngTabBorderBrush)
                     Call Polygon(UserControl.hdc, udtPointA(0), 5)
                     lngPen = CreatePen(PS_SOLID, 1, lngLightColor)
                     Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                     Call pvInflatePoint(udtPointB(0), udtPointA(0), 1, 0)
                     Call pvInflatePoint(udtPointB(1), udtPointA(1), 1, 1)
                     Call pvInflatePoint(udtPointB(2), udtPointA(2), -1, 1)
                     Call pvInflatePoint(udtPointB(3), udtPointA(3), -1, 1)
                     Call pvInflatePoint(udtPointB(4), udtPointA(4), -1, 0)
                     Call Polyline(UserControl.hdc, udtPointB(0), 5)
                     If m_udtBorderStyle = GpTabBorderStyle3D Then
                        ' 去底边
                        Call DrawLine(UserControl.hdc, .Left + 1, .Bottom, .Right, .Bottom, TranslateColor(m_oleTabColorActive))
                        Call DrawLine(UserControl.hdc, .Left + 1, .Bottom + 1, .Right, .Bottom + 1, TranslateColor(m_oleTabColorActive))
                        ' 加阴影
                        Call DrawLine(UserControl.hdc, .Right - 2, .Top, .Right, .Top + 2, TranslateColor(lngDarkShadowColor))
                        Call DrawLine(UserControl.hdc, .Right - 3, .Top, .Right - 1, .Top + 3, TranslateColor(lngShadowColor))
                        Call DrawLine(UserControl.hdc, .Right, .Top + 2, .Right, .Bottom, TranslateColor(lngDarkShadowColor))
                        Call DrawLine(UserControl.hdc, .Right - 1, .Top + 2, .Right - 1, .Bottom - 1, TranslateColor(lngShadowColor))
                     ElseIf m_udtBorderStyle = GpTabBorderStyle3DThin Then
                        ' 去底边
                        Call DrawLine(UserControl.hdc, .Left + 2, .Bottom, .Right, .Bottom, TranslateColor(m_oleTabColorActive))
                        Call DrawLine(UserControl.hdc, .Left + 2, .Bottom + 1, .Right, .Bottom + 1, TranslateColor(m_oleTabColorActive))
                        ' 加阴影
                        Call DrawLine(UserControl.hdc, .Right - 2, .Top, .Right, .Top + 2, TranslateColor(lngShadowColor))
                        Call DrawLine(UserControl.hdc, .Right, .Top + 2, .Right, .Bottom, TranslateColor(lngShadowColor))
                     End If
                     End If
                End With
       End Select
    ' 控件WinXP风格
    'LAST HERE -> LOOK FOR THE BLUE EDGE ON TAB HOW TO REMOVE
    ElseIf m_udtStyle = GpTabStyleWinXP Then
       Select Case m_udtTabStyle
              Case GpTabRectangle
                If m_udtBorderStyle = GpTabBorderStyleNone Then
                Else
                   With udtTabRect
                        ' 填充
                        lngStepXP = 25 / Height
                        For lngI = Height To 1 Step -1
                            Call DrawLine(UserControl.hdc, .Left + 1, lngI + .Top, .Right, lngI + .Top, _
                                          BrightnessColor(TranXPColor, lngStepXP * lngI))
                        Next lngI
                        lngXPColor = BrightnessColor(TranXPColor, lngStepXP * 1)
                        Call SelectObject(UserControl.hdc, lngXPBorderBrush)
                        .Right = .Right + 1  ' 使间距变小
                        .Bottom = Top + Height + 1
                        ' 画边框
                        Call FrameRect(UserControl.hdc, udtTabRect, lngXPBorderBrush)
                        If Selected Then
                           ' 去顶线
                           'Call DrawLine(UserControl.hdc, .Left, .Top, .Right, .Top, lngXPColor)
                           ' 画焦点线
                           'Call DrawLine(UserControl.hdc, .Left + 2, Top - 2, .Right - 2, Top - 2, &HFF6633)
                           'Call DrawLine(UserControl.hdc, .Left + 1, Top - 1, .Right - 1, Top - 1, &HFF855D)
                           'Call DrawLine(UserControl.hdc, .Left, Top, .Right, Top, &HFEA588)
                           'Call DrawLine(UserControl.hdc, .Left - 1, Top + 1, .Right - 1, Top + 1, &HFFC5B2)
                           ' 当选中时去底边
                           'Call DrawLine(UserControl.hdc, .Left + 1, .Bottom - 1, .Right - 1, .Bottom - 1, TranXPColor)
                        Else
                           If Hover Then
                              ' 去顶线
                              'Call DrawLine(UserControl.hdc, .Left, .Top, .Right, .Top, lngXPColor)
                              ' Hover line
                              'Call DrawLine(UserControl.hdc, .Left + 2, Top - 2, .Right - 2, Top - 2, &H138DEB)
                              'Call DrawLine(UserControl.hdc, .Left + 1, Top - 1, .Right - 1, Top - 1, &H3399FF)
                              'Call DrawLine(UserControl.hdc, .Left, Top, .Right, Top, &H66CCFF)
                              'Call DrawLine(UserControl.hdc, .Left - 1, Top + 1, .Right - 1, Top + 1, &H9DDBFF)
                           End If
                        End If
                   End With
                End If
              Case GpTabRoundRect
                If m_udtBorderStyle = GpTabBorderStyleNone Then
                Else
                   lngStepXP = 25 / Height
                   With udtTabRect
                        ' 填充
                        lngTop1 = .Top
                        lngTop2 = .Top + 1
                        For lngI = Height To 1 Step -1
                            If lngI = lngTop1 Then
                               Call DrawLine(UserControl.hdc, .Left + 2, lngI + .Top, .Right - 2, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                            ElseIf lngI = lngTop2 Then
                               Call DrawLine(UserControl.hdc, .Left + 1, lngI + .Top, .Right - 1, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                            Else
                               Call DrawLine(UserControl.hdc, .Left, lngI + .Top, .Right, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                            End If
                        Next lngI
                        lngXPColor = BrightnessColor(TranXPColor, lngStepXP * .Top)
                        ' 取得圆角的各个点的坐标
                        Call pvCalculateRoundPoint(udtPointA, 6, .Top, .Left, .Right, .Bottom)
                        Call SelectObject(UserControl.hdc, lngXPBorderBrush)
                        ' 画边框
                        Call Polyline(UserControl.hdc, udtPointA(0), 6)
                        If Selected Then
                           ' 去顶线
                           'Call DrawLine(UserControl.hdc, .Left + 2, .Top, .Right - 2, .Top, lngXPColor)
                           ' 画焦点线
                           'Call DrawLine(UserControl.hdc, .Left + 2, Top - 2, .Right - 1, Top - 2, &HFF6633)
                           'Call DrawLine(UserControl.hdc, .Left + 1, Top - 1, .Right, Top - 1, &HFF855D)
                           'Call DrawLine(UserControl.hdc, .Left, Top, .Right + 1, Top, &HFEA588)
                           'Call DrawLine(UserControl.hdc, .Left - 1, Top + 1, .Right, Top + 1, &HFFC5B2)
                        Else
                           If Hover Then
                              ' 去顶线
                              'Call DrawLine(UserControl.hdc, .Left + 2, .Top, .Right - 2, .Top, lngXPColor)
                              ' Hover line
                              'Call DrawLine(UserControl.hdc, .Left + 2, Top - 2, .Right - 1, Top - 2, &H138DEB)
                              'Call DrawLine(UserControl.hdc, .Left + 1, Top - 1, .Right, Top - 1, &H3399FF)
                              'Call DrawLine(UserControl.hdc, .Left, Top, .Right + 1, Top, &H66CCFF)
                              'Call DrawLine(UserControl.hdc, .Left + 1, Top + 1, .Right, Top + 1, &H9DDBFF)
                           End If
                           ' 没有选中时加底边
                           Call DrawLine(UserControl.hdc, .Left + 1, .Bottom, .Right, .Bottom, XPBorderColor)
                        End If
                   End With
                End If
              Case GpTabTrapezoid
                If m_udtBorderStyle = GpTabBorderStyleNone Then
                   ReDim udtPointB(4)
                   With udtTabRect
                        ' 取得圆角的各个点的坐标
                        Call pvCalculateTrapezoidPoint(udtPointA, 5, .Top, .Left, .Right, .Bottom - 1)
                        ' 填充
                        If Selected Then
                          
                           lngPen = CreatePen(PS_SOLID, 1, vbWhite)
                           Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                           Call SelectObject(UserControl.hdc, vbWhite)
                           Call pvInflatePoint(udtPointB(0), udtPointA(0), 2, 2)
                           Call pvInflatePoint(udtPointB(1), udtPointA(1), 2, 2)
                           Call pvInflatePoint(udtPointB(2), udtPointA(2), -2, 2)
                           Call pvInflatePoint(udtPointB(3), udtPointA(3), -2, 2)
                           Call pvInflatePoint(udtPointB(4), udtPointA(4), -2, 2)
                           Call Polygon(UserControl.hdc, udtPointB(0), 5)
                           
                           'Call DrawLine(UserControl.hdc, .Left, .Bottom + 2, .Right, .Bottom + 2, XPFlatTabColorActive)
                        Else
                           lngPen = CreatePen(PS_SOLID, 0, &H808080)
                           Call DeleteObject(SelectObject(UserControl.hdc, lngPen))
                           Call SelectObject(UserControl.hdc, &H808080)
                           Call Polygon(UserControl.hdc, udtPointA(0), 5)
                        End If
                   End With
                Else
                   lngStepXP = 25 / Height
                   With udtTabRect
                        ' 填充
                        lngTop1 = .Top
                        lngTop2 = .Top + 1
                        For lngI = Height To 1 Step -1
                            If lngI = lngTop1 Then
                                If Selected Then
                               Call DrawLine(UserControl.hdc, .Left + Height, lngI + .Top, .Right - 2, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                                End If
                            ElseIf lngI = lngTop2 Then
                                If Selected Then
                               Call DrawLine(UserControl.hdc, .Left + Height - 1, lngI + .Top, .Right - 1, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                                End If
                            Else
                                If Selected Then
                               Call DrawLine(UserControl.hdc, .Left + (.Bottom - lngI), lngI + .Top, .Right, lngI + .Top, _
                                             BrightnessColor(TranXPColor, lngStepXP * lngI))
                                End If
                            End If
                        Next lngI
                        ' 取得圆角的各个点的坐标
                        Call pvCalculateTrapezoidPoint(udtPointA, 5, .Top, .Left, .Right, .Bottom)
                        Call SelectObject(UserControl.hdc, lngXPBorderBrush)
                        ' 画边框
                        Call Polyline(UserControl.hdc, udtPointA(0), 5)
                        If Selected Then
                           ' 去顶线
                           'HERE
                           'Call DrawLine(UserControl.hdc, .Left + 2, .Top, .Right - 2, .Top, lngXPColor)
                           ' 画焦点线
                           'Call DrawLine(UserControl.hdc, .Left + 2, Top, .Right - 1, Top, &HFF6633)
                           'Call DrawLine(UserControl.hdc, .Left + 1, Top + 1, .Right, Top + 1, &HFF855D)
                           'Call DrawLine(UserControl.hdc, .Left, Top + 2, .Right + 1, Top + 2, &HFEA588)
                           'Call DrawLine(UserControl.hdc, .Left - 1, Top + 3, .Right, Top + 3, &HFFC5B2)
                        Else
                           If Hover Then
                              ' 去顶线
                              'Call DrawLine(UserControl.hdc, .Left + 2, .Top, .Right - 2, .Top, lngXPColor)
                              ' Hover line
                              'Call DrawLine(UserControl.hdc, .Left + 2, Top, .Right - 1, Top, &H138DEB)
                              'Call DrawLine(UserControl.hdc, .Left + 1, Top + 1, .Right, Top + 1, &H3399FF)
                              'Call DrawLine(UserControl.hdc, .Left, Top + 2, .Right + 1, Top + 2, &H66CCFF)
                              'Call DrawLine(UserControl.hdc, .Left + 1, Top + 3, .Right, Top + 3, &H9DDBFF)
                           End If
                           ' 没有选中时加底边
                           Call DrawLine(UserControl.hdc, .Left + 1, .Bottom, .Right, .Bottom, XPBorderColor)
                        End If
                   End With
                End If
       End Select
    End If
    
    If m_udtTabStyle = GpTabTrapezoid Then
       With udtTabRect
            .Left = .Left + m_lngDefaultTabHeight
       End With
    End If
    ' Draw Caption
    Call DrawTextEx(UserControl.hdc, Caption & vbNullString, -1, udtTabRect, _
                    pvGetCaptionFlags(GpTabCaptionRight), m_udtDrawTextParams)
    If lngOldBrush <> 0 Then Call SelectObject(UserControl.hdc, lngOldBrush): lngOldBrush = 0
    Call DeleteObject(lngXPBorderBrush)
    Call DeleteObject(lngTabBorderBrush)
    Call DeleteObject(lngFlatBrush)
    Call DeleteObject(lngFlatActiveBrush)
    Call DeleteObject(lngFlatHoverBrush)
    Call DeleteObject(lngFlatBorderBrush)
    Exit Sub
ErrorHandle:
    Select Case ShowError(ProcName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
           Case vbAbort
             If lngOldBrush <> 0 Then Call SelectObject(UserControl.hdc, lngOldBrush)
             Call DeleteObject(lngXPBorderBrush)
             Call DeleteObject(lngTabBorderBrush)
             Call DeleteObject(lngFlatBrush)
             Call DeleteObject(lngFlatActiveBrush)
             Call DeleteObject(lngFlatHoverBrush)
             Call DeleteObject(lngFlatBorderBrush)
    End Select
End Sub

Private Function pvGetCaptionFlags(ByVal Alignment As GPTAB_ALIGNMENT_METHOD) As Long
    Select Case Alignment
           Case GpTabCaptionLeft
             pvGetCaptionFlags = DT_SINGLELINE Or DT_WORD_ELLIPSIS Or DT_LEFT Or DT_VCENTER
           Case GpTabCaptionRight
             pvGetCaptionFlags = DT_SINGLELINE Or DT_WORD_ELLIPSIS Or DT_RIGHT Or DT_VCENTER
           Case GpTabCaptionCenter
             pvGetCaptionFlags = DT_SINGLELINE Or DT_WORD_ELLIPSIS Or DT_CENTER Or DT_VCENTER
    End Select
End Function

Private Sub pvInflatePoint(ByRef DstPoint As POINTAPI, ByRef SrcPoint As POINTAPI, ByVal X As Long, ByVal Y As Long)
    With DstPoint
         .X = SrcPoint.X + X
         .Y = SrcPoint.Y + Y
    End With
End Sub

Private Sub pvSetColor()
    If m_udtXPColorScheme = GpTabUseWindows Then
       m_lngXPFaceColor = BrightnessColor(GetSysColor(COLOR_BTNFACE), &H30)
    Else
       m_lngXPFaceColor = BrightnessColor(m_oleTabColorActive, &H30)
    End If
End Sub

' 刷新
Public Sub Refresh()
    Call pvDraw
End Sub

Public Property Get SelectTabItem() As cTabItem
    If m_clsSelectTab Is Nothing Then
       Set SelectTabItem = Nothing
    Else
       Set SelectTabItem = m_clsSelectTab
    End If
End Property

Public Property Get Style() As GPTAB_STYLE_METHOD
    Style = m_udtStyle
End Property

Public Property Let Style(ByVal New_Style As GPTAB_STYLE_METHOD)
    m_udtStyle = New_Style
    PropertyChanged "Style"
    Call pvDraw
End Property

Public Property Get TabBorderColor() As OLE_COLOR
    TabBorderColor = m_oleTabBorderColor
End Property

Public Property Let TabBorderColor(ByVal New_TabBorderColor As OLE_COLOR)
    m_oleTabBorderColor = New_TabBorderColor
    PropertyChanged "TabBorderColor"
End Property

Public Property Get TabColor() As OLE_COLOR
    TabColor = m_oleTabColor
End Property

Public Property Let TabColor(ByVal New_TabColor As OLE_COLOR)
    m_oleTabColor = New_TabColor
    PropertyChanged "TabColor"
    Call pvDraw
End Property

Public Property Get TabColorActive() As OLE_COLOR
    TabColorActive = m_oleTabColorActive
End Property

Public Property Let TabColorActive(ByVal New_TabColorActive As OLE_COLOR)
    m_oleTabColorActive = New_TabColorActive
    PropertyChanged "TabColorActive"
    Call pvSetColor
    Call pvDraw
End Property

Public Property Get TabColorHover() As OLE_COLOR
    TabColorHover = m_oleTabColorHover
End Property

Public Property Let TabColorHover(ByVal New_TabColorHover As OLE_COLOR)
    m_oleTabColorHover = New_TabColorHover
    PropertyChanged "TabColorHover"
End Property

Public Property Get TabFixedHeight() As Long
    TabFixedHeight = m_lngTabFixedHeight
End Property

Public Property Let TabFixedHeight(ByVal New_TabFixedHeight As Long)
    m_lngTabFixedHeight = New_TabFixedHeight
    PropertyChanged "TabFixedHeight"
    Call pvCalculateSize
    Call pvDraw
End Property

Public Property Get TabFixedWidth() As Long
    TabFixedWidth = m_lngTabFixedWidth
End Property

Public Property Let TabFixedWidth(ByVal New_TabFixedWidth As Long)
    m_lngTabFixedWidth = New_TabFixedWidth
    PropertyChanged "TabFixedWidth"
    Call pvCalculateSize
    Call pvDraw
End Property

Public Property Get Tabs() As cTabItems
    Set Tabs = m_clsTabs
End Property

Public Property Get TabStyle() As GPTAB_TABSTYLE_METHOD
    TabStyle = m_udtTabStyle
End Property

Public Property Let TabStyle(ByVal New_TabStyle As GPTAB_TABSTYLE_METHOD)
    m_udtTabStyle = New_TabStyle
    PropertyChanged "TabStyle"
    Call pvCalculateSize
    Call pvDraw
End Property

Public Property Get TabWidthStyle() As GPTAB_TABWIDTHSTYLE_METHOD
    TabWidthStyle = m_udtTabWidthStyle
End Property

Public Property Let TabWidthStyle(ByVal New_TabWidthStyle As GPTAB_TABWIDTHSTYLE_METHOD)
    m_udtTabWidthStyle = New_TabWidthStyle
    PropertyChanged "TabWidthStyle"
    Call pvCalculateSize
    Call pvDraw
End Property

Public Property Get XPColorScheme() As GPTAB_XPCOLORSCHEME_METHOD
    XPColorScheme = m_udtXPColorScheme
End Property

Public Property Let XPColorScheme(ByVal New_XPColorScheme As GPTAB_XPCOLORSCHEME_METHOD)
    m_udtXPColorScheme = New_XPColorScheme
    PropertyChanged "XPColorScheme"
    Call pvSetColor
    Call pvDraw
End Property

Private Sub m_clsTabs_TabAddNew()
    If m_clsSelectTab Is Nothing Then
       Set m_clsSelectTab = m_clsTabs.Item(1)
       m_clsSelectTab.Selected = True
    End If
    Call pvCalculateSize
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabAlignmentChanged(ByVal Index As Long)
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabCaptionChanged(ByVal Index As Long)
    Call pvCalculateSize
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabIconAlignChanged(ByVal Index As Long)
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabIconChanged(ByVal Index As Long)
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabRemove()
    Call pvCalculateSize
    Call pvDraw
End Sub

Private Sub m_clsTabs_TabSelectedChanged(ByVal Index As Long)
    '
End Sub

Private Sub UserControl_Click()
    If m_blnEnabled Then RaiseEvent Click
End Sub

Private Sub UserControl_Initialize()
    Set m_clsTabs = New cTabItems
    With m_udtDrawTextParams
         .iLeftMargin = 1
         .iRightMargin = 1
         .iTabLength = 1
         .cbSize = Len(m_udtDrawTextParams)
    End With
    m_blnAutoBackColor = True
    m_blnEnabled = True
    m_blnHotTracking = True
    m_blnMultiRow = True
    m_lngTabFixedHeight = 0
    m_lngTabFixedWidth = 0
    m_udtXPColorScheme = GpTabUseWindows
    m_udtBorderStyle = GpTabBorderStyle3D
    m_udtPlacement = GpTabPlacementTopleft
    m_udtStyle = GpTabStyleStandard
    m_oleTabBorderColor = vbWhite
    m_oleTabColor = vbButtonShadow
    m_oleTabColorActive = vbButtonFace
    m_oleTabColorHover = vbHighlight
    m_udtTabStyle = GpTabRectangle
    m_udtTabWidthStyle = GpTabJustified
    Call pvSetColor
End Sub

Private Sub UserControl_InitProperties()
'    Me.AutoBackColor = True
    Call pvCalculateSize
    Me.BackColor = UserControl.Ambient.BackColor
'    Me.BorderStyle = GpTabBorderStyle3D
    Set Me.Font = UserControl.Ambient.Font
'    Me.ForeColor = vbWindowText
'    Me.Enable = True
'    Me.MultiRow = True
'    Me.Placement = GpTabPlacementTopLeft
'    Me.Style = GpTabStyleStandard
'    Me.TabFixedHeight = 0
'    Me.TabFixedWidth = 0
'    Me.TabStyle = GpTabStandard
'    Me.TabWidthStyle = GpTabJustified
End Sub

Public Sub SelectTab(Index As Integer)
    Dim lngI As Long
    Dim clsTemp As cTabItem
    
    'On Error GoTo ErrorHandle
    
        '
        For lngI = 1 To m_clsTabs.Count
            m_clsTabs.Item(lngI).Selected = False
        Next lngI
        
        m_clsTabs.Item(Index).Selected = True
        'clsTemp = m_clsTabs.Item(Index)
        Set m_clsSelectTab = m_clsTabs.Item(Index)
        m_clsSelectTab.Selected = True
        Call pvDraw
        'Set clsTemp = Nothing
    Exit Sub
ErrorHandle:
    Select Case ShowError("SelectTab", MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
           Case vbAbort: Set clsTemp = Nothing
    End Select
End Sub

Private Sub UserControl_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    Dim lngI As Long
    Dim clsTemp As cTabItem
    Const ProcName = "UserControl_MouseDown"
    On Error GoTo ErrorHandle
    DoEvents
    If m_blnEnabled Then RaiseEvent MouseDown(Button, Shift, X, Y)
    If Button = vbLeftButton Then
       Set clsTemp = HitTest(X, Y)
       If Not (clsTemp Is Nothing) Then
          If Not clsTemp Is m_clsSelectTab Then
             For lngI = 1 To m_clsTabs.Count
             DoEvents
                 m_clsTabs.Item(lngI).Selected = False
             Next lngI
             Set m_clsSelectTab = clsTemp
             m_clsSelectTab.Selected = True
             Call pvDraw
             RaiseEvent TabClick
          End If
       End If
    End If
    Set clsTemp = Nothing
    frmMain.Code.SetFocus
   Exit Sub
ErrorHandle:
DoEvents
   'Select Case ShowError(ProcName, MODULE_NAME)
'           Case vbRetry: Resume
'           Case vbIgnore: Resume Next
'           Case vbAbort: Set clsTemp = Nothing
'    End Select
End Sub

Private Sub UserControl_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    Dim clsTemp As cTabItem
    Const ProcName = "UserControl_MouseMove"
    
    On Error GoTo ErrorHandle
    If m_blnEnabled Then RaiseEvent MouseMove(Button, Shift, X, Y)
    Set clsTemp = HitTest(X, Y)
    If Not clsTemp Is m_clsHoverTab Then
       Set m_clsHoverTab = clsTemp
       If m_blnHotTracking Then Call pvDraw
    End If
    ' 鼠标捕获
    If X >= 0 And X < UserControl.ScaleWidth And Y >= 0 And Y < UserControl.ScaleHeight And Button = 0 Then
        'If GetCapture() <> UserControl.hWnd Then Call SetCapture(UserControl.hWnd)
    Else
        'If GetCapture() = UserControl.hWnd And Button = 0 Then Call ReleaseCapture
    End If
    'If GetCapture() = UserControl.hWnd And Button = 0 Then Call ReleaseCapture
    Set clsTemp = Nothing
    Exit Sub
ErrorHandle:
    Select Case ShowError(ProcName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
           Case vbAbort: Set clsTemp = Nothing
    End Select
End Sub

Private Sub UserControl_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If m_blnEnabled Then RaiseEvent MouseUp(Button, Shift, X, Y)
End Sub

Private Sub UserControl_Paint()
    UserControl.AutoRedraw = True
    Call pvDraw
End Sub

Private Sub UserControl_ReadProperties(PropBag As PropertyBag)
    Dim clsFont As New StdFont
    
    Call pvCalculateSize
    m_blnUserMode = UserControl.Ambient.UserMode
    With PropBag
         Me.AutoBackColor = .ReadProperty("AutoBackColor", True)
         Me.BackColor = .ReadProperty("BackColor", UserControl.Ambient.BackColor)
         Me.BorderStyle = .ReadProperty("BorderStyle", GpTabBorderStyle3D)
         Me.Enable = .ReadProperty("Enable", True)
         Me.ForeColor = .ReadProperty("ForeColor", vbWindowText)
         Me.HotTracking = .ReadProperty("HotTracking", True)
         Set Me.MouseIcon = .ReadProperty("MouseIcon", Nothing)
         Me.MousePointer = .ReadProperty("MousePointer", 0)
         Me.MultiRow = .ReadProperty("MultiRow", True)
         Me.Placement = .ReadProperty("Placement", GpTabPlacementTopleft)
         Me.Style = .ReadProperty("Style", GpTabStyleStandard)
         Me.TabBorderColor = .ReadProperty("TabBorderColor", vbWhite)
         Me.TabColor = .ReadProperty("TabColor", vbButtonShadow)
         Me.TabColorActive = .ReadProperty("TabColorActive", vbButtonFace)
         Me.TabColorHover = .ReadProperty("TabColorHover", vbHighlight)
         Me.TabFixedHeight = .ReadProperty("TabFixedHeight", 0)
         Me.TabFixedWidth = .ReadProperty("TabFixedWidth", 0)
         Me.TabStyle = .ReadProperty("TabStyle", GpTabRectangle)
         Me.TabWidthStyle = .ReadProperty("TabWidthStyle", GpTabJustified)
         Me.XPColorScheme = .ReadProperty("XPColorScheme", GpTabUseWindows)
         With clsFont
              .Name = "MS Sans Serif"
              .Size = 8
         End With
         Set Me.Font = .ReadProperty("Font", clsFont)
    End With
    Set clsFont = Nothing
    Call pvSetColor
End Sub

Private Sub UserControl_Resize()
    Call pvCalculateSize
    Call pvDraw
End Sub

Public Sub Redraw()
    Call pvCalculateSize
    Call pvDraw
End Sub

Private Sub UserControl_Terminate()
    Set m_clsSelectTab = Nothing
    Set m_clsHoverTab = Nothing
    Set m_clsTabs = Nothing
End Sub

Private Sub UserControl_WriteProperties(PropBag As PropertyBag)
    Dim clsFont As New StdFont
    
    With PropBag
         .WriteProperty "AutoBackColor", m_blnAutoBackColor, True
         .WriteProperty "BackColor", m_oleBackColor, UserControl.Ambient.BackColor
         .WriteProperty "BorderStyle", m_udtBorderStyle, GpTabBorderStyle3D
         .WriteProperty "Enable", m_blnEnabled, True
         .WriteProperty "ForeColor", Me.ForeColor, vbWindowText
         .WriteProperty "HotTracking", m_blnHotTracking, True
         .WriteProperty "MouseIcon", Me.MouseIcon, Nothing
         .WriteProperty "MousePointer", UserControl.MousePointer, 0
         .WriteProperty "MultiRow", m_blnMultiRow, True
         .WriteProperty "Placement", m_udtPlacement, GpTabPlacementTopleft
         .WriteProperty "Style", m_udtStyle, GpTabStyleStandard
         .WriteProperty "TabBorderColor", m_oleTabBorderColor, vbWhite
         .WriteProperty "TabColor", Me.TabColor, vbButtonShadow
         .WriteProperty "TabColorActive", Me.TabColorActive, vbButtonFace
         .WriteProperty "TabColorHover", Me.TabColorHover, vbHighlight
         .WriteProperty "TabFixedHeight", m_lngTabFixedHeight, 0
         .WriteProperty "TabFixedWidth", m_lngTabFixedWidth, 0
         .WriteProperty "TabStyle", m_udtTabStyle, GpTabRectangle
         .WriteProperty "TabWidthStyle", m_udtTabWidthStyle, GpTabJustified
         .WriteProperty "XPColorScheme", m_udtXPColorScheme, GpTabUseWindows
         With clsFont
              .Name = "MS Sans Serif"
              .Size = 8
         End With
         .WriteProperty "Font", Me.Font, clsFont
    End With
    Set clsFont = Nothing
End Sub

Private Function ShowError(ByVal strFunc As String, ByVal strModule As String) As VbMsgBoxResult
    Dim lngErrNumber             As Long
    Dim strErrDescription        As String
    Dim strErrSource             As String
    
    lngErrNumber = Err.Number
    strErrDescription = Err.Description
    strErrSource = IIf(Len(strModule) > 0, _
                            "[\\" & ErrComputerName() & "] " & _
                            App.EXEName & "." & _
                            strModule & "." & _
                            strFunc & _
                            IIf(Erl <> 0, "(" & Erl & ")", ""), "") & "--" & Err.Source
    ShowError = MsgBox( _
            strErrDescription & vbCrLf & vbCrLf & _
            "Error: 0x" & Hex(lngErrNumber) & vbCrLf & vbCrLf & _
            "Call stack:" & vbCrLf & _
            strErrSource, vbCritical Or vbAbortRetryIgnore, "Error")
End Function

Private Function ErrComputerName() As String
    Static sName        As String
        
    If Len(sName) = 0 Then
        sName = String(256, 0)
        GetComputerName sName, Len(sName)
        sName = Left$(sName, InStr(sName, Chr(0)) - 1)
    End If
    ErrComputerName = sName
End Function

Public Function BitmapToPicture(ByVal hBmp As Long) As IPicture
    Dim IGuid    As Guid
    Dim NewPic   As Picture
    Dim tPicConv As PICTDESC
    
    If (hBmp = 0) Then Exit Function
   
    ' Fill PictDesc structure with necessary parts:
    With tPicConv
         .cbSizeofStruct = Len(tPicConv)
         .picType = vbPicTypeBitmap
         .hImage = hBmp
    End With
    
    ' Fill in IDispatch Interface ID
    With IGuid
         .Data1 = &H20400
         .Data4(0) = &HC0
         .Data4(7) = &H46
    End With
   
    ' Create a picture object:
    OleCreatePictureIndirect tPicConv, IGuid, True, NewPic
   
    ' Return it:
    Set BitmapToPicture = NewPic
End Function

Public Function BrightnessColor(ByVal ColorValue As Long, ByVal Increment As Long) As Long
    Dim R, g, b As Long
    
    b = ((ColorValue \ &H10000) Mod &H100): b = b + ((b * Increment) \ &HC0)
    g = ((ColorValue \ &H100) Mod &H100) + Increment
    R = (ColorValue And &HFF) + Increment
    If R < 0 Then R = 0
    If R > 255 Then R = 255
    If g < 0 Then g = 0
    If g > 255 Then g = 255
    If b < 0 Then b = 0
    If b > 255 Then b = 255
    BrightnessColor = RGB(R, g, b)
End Function

Private Sub DrawDragImage(ByRef rcNew As RECT, _
                         ByVal bFirst As Boolean, _
                         ByVal bLast As Boolean)
    Static rcCurrent     As RECT
    Dim hdc              As Long
    Dim lngReturn        As Long
    
    On Error Resume Next
    ' First get the Desktop DC:
    hdc = CreateDCAsNull("DISPLAY", ByVal 0&, ByVal 0&, ByVal 0&)
    ' Set the draw mode to XOR:
    lngReturn = SetROP2(hdc, R2_NOTXORPEN)
    '// Draw over and erase the old rectangle
    If Not (bFirst) Then
       lngReturn = Rectangle(hdc, rcCurrent.Left, rcCurrent.Top, rcCurrent.Right, rcCurrent.Bottom)
    End If
    If Not (bLast) Then
       '// Draw the new rectangle
       lngReturn = Rectangle(hdc, rcNew.Left, rcNew.Top, rcNew.Right, rcNew.Bottom)
    End If
    ' Store this position so we can erase it next time:
    LSet rcCurrent = rcNew
    ' Free the reference to the Desktop DC we got (make sure you do this!)
    lngReturn = DeleteDC(hdc)
End Sub

Public Sub DrawImage(ByVal hIml As Long, _
                     ByVal iIndex As Long, _
                     ByVal hdc As Long, _
                     ByVal xPixels As Integer, _
                     ByVal yPixels As Integer, _
                     ByVal lIconSizeX As Long, _
                     ByVal lIconSizeY As Long, _
                     Optional ByVal bSelected = False, _
                     Optional ByVal bCut = False, _
                     Optional ByVal bDisabled = False, _
                     Optional ByVal oCutDitherColour As OLE_COLOR = vbWindowBackground, _
                     Optional ByVal hExternalIml As Long = 0)
    Dim hIcon        As Long
    Dim lFlags       As Long
    Dim lhIml        As Long
    Dim lColor       As Long
    Dim iImgIndex    As Long
    Dim lngReturn    As Long
    
    ' Draw the image at 1 based index or key supplied in vKey.
    ' on the hDC at xPixels,yPixels with the supplied options.
    ' You can even draw an ImageList from another ImageList control
    ' if you supply the handle to hExternalIml with this function.
    On Error Resume Next
    iImgIndex = iIndex
    If (iImgIndex > -1) Then
       If (hExternalIml <> 0) Then
          lhIml = hExternalIml
       Else
          lhIml = hIml
       End If
       lFlags = ILD_TRANSPARENT
       If (bSelected) Or (bCut) Then
          lFlags = lFlags Or ILD_SELECTED
       End If
       If (bCut) Then
          ' Draw dithered:
          lColor = TranslateColor(oCutDitherColour)
          If (lColor = -1) Then lColor = TranslateColor(vbWindowBackground)
          lngReturn = ImageList_DrawEx(lhIml, iImgIndex, hdc, xPixels, yPixels, 0, 0, CLR_NONE, lColor, lFlags)
       ElseIf (bDisabled) Then
          ' extract a copy of the icon:
          hIcon = ImageList_GetIcon(hIml, iImgIndex, 0)
          ' Draw it disabled at x,y:
          lngReturn = DrawState(hdc, 0, 0, hIcon, 0, xPixels, yPixels, lIconSizeX, lIconSizeY, DST_ICON Or DSS_DISABLED)
          ' Clear up the icon:
          lngReturn = DestroyIcon(hIcon)
       Else
          ' Standard draw:
          lngReturn = ImageList_Draw(lhIml, iImgIndex, hdc, xPixels, yPixels, lFlags)
       End If
    End If
End Sub

Public Sub DrawLine(ByVal hdc As Long, _
                    ByVal X1 As Long, _
                    ByVal Y1 As Long, _
                    ByVal X2 As Long, _
                    ByVal Y2 As Long, _
                    ByVal Color As Long, _
                    Optional Width As Long = 1)
    Dim lngPen      As Long
    Dim lngPenOld   As Long
    Dim pt          As POINTAPI
    Const FuncName = "DrawLine"
    
    On Error GoTo ErrorHandle
    
    '/* 创建一个画笔 */
    lngPen = CreatePen(PS_SOLID, Width, Color)
    If lngPen <> 0 Then lngPenOld = SelectObject(hdc, lngPen)
    '/* 指定一个新的当前画笔位置X1,Y1。前一个位置保存在pt中 */
    MoveToEx hdc, X1, Y1, pt
    '/* 画一条线 */
    LineTo hdc, X2, Y2
    If lngPenOld <> 0 Then SelectObject hdc, lngPenOld
    lngPenOld = 0
    If lngPen <> 0 Then DeleteObject lngPen
    Exit Sub
ErrorHandle:
    Select Case ShowError(FuncName, MODULE_NAME)
           Case vbRetry: Resume
           Case vbIgnore: Resume Next
           Case vbAbort
             If lngPenOld <> 0 Then SelectObject hdc, lngPenOld
             lngPenOld = 0
             If lngPen <> 0 Then DeleteObject lngPen
    End Select
End Sub

Public Function TranslateColor(ByVal oClr As OLE_COLOR, Optional hPal As Long = 0) As Long
    ' Convert Automation color to Windows color
    If OleTranslateColor(oClr, hPal, TranslateColor) Then TranslateColor = CLR_NONE
End Function

' Returns Color as long, accepts SystemColorConstants
Public Function VerifyColor(ByVal ColorVal As Long) As Long
    VerifyColor = ColorVal
    If ColorVal > &HFFFFFF Or ColorVal < 0 Then VerifyColor = GetSysColor(ColorVal And &HFFFFFF)
End Function


