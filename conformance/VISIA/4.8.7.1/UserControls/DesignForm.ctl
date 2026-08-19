VERSION 5.00
Begin VB.UserControl DesignForm 
   BackColor       =   &H80000010&
   ClientHeight    =   4185
   ClientLeft      =   0
   ClientTop       =   0
   ClientWidth     =   5490
   ScaleHeight     =   4185
   ScaleWidth      =   5490
   Begin VB.Shape shpRndDes 
      Height          =   375
      Left            =   0
      Top             =   0
      Width           =   375
   End
End
Attribute VB_Name = "DesignForm"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Public IsParent As Long

Public Function hwnd() As Long
    hwnd = UserControl.hwnd
End Function

Private Sub UserControl_Initialize()
    On Error GoTo NotParent
    IsParent = 0
    GridSize = 100 'GetSetting("Visia", "Settings", "GridSize", 100)
    'frmDesign.Show: DoEvents
    'IsParent = SetParent(frmDesign.hWnd, UserControl.hWnd)
NotParent:
End Sub

Private Sub UserControl_Resize()
    With shpRndDes
        .Width = UserControl.Width
        .Height = UserControl.Height
    End With
End Sub

Private Sub UserControl_Terminate()
    'If IsParent <> 0 Then Unload frmDesign
End Sub
