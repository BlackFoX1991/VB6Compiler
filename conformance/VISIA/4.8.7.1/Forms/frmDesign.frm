VERSION 5.00
Begin VB.Form frmDesign 
   AutoRedraw      =   -1  'True
   Caption         =   "Dialog"
   ClientHeight    =   3525
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   5715
   Icon            =   "frmDesign.frx":0000
   LinkTopic       =   "Form1"
   Moveable        =   0   'False
   NegotiateMenus  =   0   'False
   ScaleHeight     =   3525
   ScaleWidth      =   5715
   ShowInTaskbar   =   0   'False
   Begin VB.Frame ctlFrame 
      Caption         =   "Frame"
      Height          =   690
      Left            =   3015
      TabIndex        =   5
      Top             =   45
      Visible         =   0   'False
      Width           =   690
   End
   Begin VB.PictureBox ctlPicture 
      Height          =   690
      Left            =   1485
      ScaleHeight     =   630
      ScaleWidth      =   630
      TabIndex        =   3
      Top             =   45
      Visible         =   0   'False
      Width           =   690
   End
   Begin VB.PictureBox picResizer 
      Appearance      =   0  '2D
      BackColor       =   &H0000D5F2&
      ForeColor       =   &H80000008&
      Height          =   105
      Left            =   5220
      ScaleHeight     =   75
      ScaleWidth      =   75
      TabIndex        =   2
      Top             =   2970
      Visible         =   0   'False
      Width           =   105
   End
   Begin VB.TextBox ctlEdit 
      Height          =   690
      Index           =   0
      Left            =   45
      TabIndex        =   1
      Text            =   "Edit"
      Top             =   45
      Visible         =   0   'False
      Width           =   690
   End
   Begin VB.CommandButton ctlButton 
      Caption         =   "Button"
      Height          =   690
      Index           =   0
      Left            =   765
      TabIndex        =   0
      Top             =   45
      Visible         =   0   'False
      Width           =   690
   End
   Begin VB.Label ctlLabel 
      Caption         =   "Label"
      Height          =   690
      Left            =   2250
      TabIndex        =   4
      Top             =   45
      Visible         =   0   'False
      Width           =   690
   End
   Begin VB.Shape shpRect 
      BackColor       =   &H8000000F&
      BorderColor     =   &H00BF7D35&
      BorderWidth     =   2
      Height          =   690
      Left            =   4410
      Top             =   2475
      Visible         =   0   'False
      Width           =   1005
   End
   Begin VB.Menu mnuDesigner 
      Caption         =   "Designer"
      Visible         =   0   'False
      Begin VB.Menu cmdViewCode 
         Caption         =   "View Code"
      End
      Begin VB.Menu Seperator1 
         Caption         =   "-"
      End
      Begin VB.Menu cmdDelete 
         Caption         =   "Delete Object"
      End
   End
End
Attribute VB_Name = "frmDesign"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Public SizeShape As Boolean
Public DragX As Long, DragY As Long
Public AssignedObject As Object

Private Sub ctlButton_MouseDown(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    Set AssignedObject = ctlButton(Index)
    DragX = X: DragY = Y
    picResizer.Visible = False
End Sub

Private Sub ctlButton_MouseMove(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Button = 1 Then
        AssignedObject.Move AlignToGrid(AssignedObject.Left + X - DragX), AlignToGrid(AssignedObject.Top + Y - DragY)
    End If
End Sub

Private Sub ctlButton_MouseUp(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    SetResizePoints AssignedObject
End Sub

Private Sub ctlEdit_MouseDown(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    Set AssignedObject = ctlEdit(Index)
    DragX = X: DragY = Y
    picResizer.Visible = False
End Sub

Private Sub ctlEdit_MouseMove(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Button = 1 Then
        AssignedObject.Move AlignToGrid(AssignedObject.Left + X - DragX), AlignToGrid(AssignedObject.Top + Y - DragY)
    End If
End Sub

Private Sub ctlEdit_MouseUp(Index As Integer, Button As Integer, Shift As Integer, X As Single, Y As Single)
    SetResizePoints AssignedObject
End Sub

Private Sub Form_Load()
    Me.Left = 100
    Me.Top = 100
End Sub

Private Sub Form_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If Button = 2 Then DoCreateObject = False
    If Button = 1 Then
        Set AssignedObject = frmDesign
        If DoCreateObject = True Then
            SizeShape = True
            shpRect.Left = AlignToGrid(CDbl(X))
            shpRect.Top = AlignToGrid(CDbl(Y))
            shpRect.Width = 0: shpRect.Height = 0
            shpRect.Visible = True
        End If
    End If
End Sub

Sub CreateObject()
    Dim GlobalObject As Object
    Select Case frmMain.tvTools.SelectedItem.Text
        Case "Button"
            Load ctlButton(ctlButton.UBound + 1)
            Set GlobalObject = ctlButton(ctlButton.UBound)
        Case "Edit"
            Load ctlEdit(ctlEdit.UBound + 1)
            Set GlobalObject = ctlEdit(ctlEdit.UBound)
        Case Else
            MsgBox "Could not create object!", vbInformation, "Visia Compiler 4.8.7"
            DoCreateObject = False
            Exit Sub
    End Select
    DoCreateObject = False
    Me.MousePointer = vbArrow
    
    GlobalObject.Visible = True
    GlobalObject.Left = shpRect.Left
    GlobalObject.Top = shpRect.Top
    GlobalObject.Width = shpRect.Width
    GlobalObject.Height = shpRect.Height
    On Error GoTo DoText
    GlobalObject.Caption = GlobalObject.Caption & GlobalObject.Index
    Exit Sub
DoText:
    GlobalObject.Text = GlobalObject.Text & GlobalObject.Index
End Sub

Private Sub Form_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If DoCreateObject = True Then
        If Me.MousePointer <> vbCrosshair Then
            Me.MousePointer = vbCrosshair
        End If
        If SizeShape = True Then
            On Error Resume Next
            shpRect.Width = AlignToGrid(X - shpRect.Left)
            shpRect.Height = AlignToGrid(Y - shpRect.Top)
        End If
    Else
        If Me.MousePointer = vbCrosshair Then
            Me.MousePointer = vbArrow
        End If
    End If
End Sub

Private Sub Form_MouseUp(Button As Integer, Shift As Integer, X As Single, Y As Single)
    If SizeShape = True Then CreateObject: SizeShape = False: shpRect.Visible = False
End Sub

Private Sub Form_Paint()
    Dim X As Integer
    Dim Y As Integer
    For X = 0 To frmDesign.ScaleWidth Step 100
        For Y = 0 To frmDesign.ScaleHeight Step 100
            frmDesign.PSet (X, Y), vbBlack
        Next
    Next
End Sub

Private Sub Form_Resize()
    SmoothGradient Me.hdc, vbWhite, &HC8D0D4, 0, 0, Me.Width, Me.Height, gr_Fill_Horizontal, False, 5
    Me.Refresh
    Form_Paint
    If Me.WindowState = vbMaximized Then Me.WindowState = vbNormal
    'frmMain.Designer.AlignForm
End Sub

Sub SetResizePoints(ToObj As Object)
    picResizer.Visible = True
    With picResizer
        .Left = ToObj.Left + ToObj.Width
        .Top = ToObj.Top + ToObj.Height
        .Visible = True
    End With
End Sub

Function AlignToGrid(Value As Double) As Double

    Dim i As Long
    
    For i = 0 To Value + GridSize Step GridSize
        If i > Value - (GridSize / 2) Then AlignToGrid = i: Exit Function
        If i > Value Then AlignToGrid = i - GridSize: Exit Function
    Next i
    
End Function

Sub LoadDialog(Content As String)
    Dim i As Long
    Dim NumCtl As Long
    Dim Lines As Variant
    Dim Seperated As Variant
    On Error Resume Next
    
    picResizer.Visible = False
    
    Lines = Split(Content, vbNewLine)
    
    'MsgBox Content
    'Delete all existing controls
    For Each Control In frmDesign
        If Control.Index > 0 Then
            If Control.Name = "ctlButton" Or _
               Control.Name = "ctlEdit" Then
                Unload Control(Control.Index)
            End If
        End If
    Next
    
    'Load the new controls
    For i = 0 To UBound(Lines) - 1
        Seperated = Split(Lines(i), "|")
        If i = 0 Then
            NumCtl = Seperated(0)
        ElseIf i = 1 Then
            frmDesign.Width = Seperated(0)
            frmDesign.Height = Seperated(1)
        Else
            If Seperated(0) = "ctlButton" Then
                Load ctlButton(ctlButton.UBound + 1)
                ctlButton(ctlButton.UBound).Left = Seperated(1)
                ctlButton(ctlButton.UBound).Left = Seperated(2)
                ctlButton(ctlButton.UBound).Left = Seperated(3)
                ctlButton(ctlButton.UBound).Left = Seperated(4)
                ctlButton(ctlEdit.UBound).Visible = True
            ElseIf Seperated(0) = "ctlEdit" Then
                Load ctlEdit(ctlButton.UBound + 1)
                ctlEdit(ctlEdit.UBound).Left = Seperated(1)
                ctlEdit(ctlEdit.UBound).Left = Seperated(2)
                ctlEdit(ctlEdit.UBound).Left = Seperated(3)
                ctlEdit(ctlEdit.UBound).Left = Seperated(4)
                ctlEdit(ctlEdit.UBound).Visible = True
                
            End If
        End If
    Next i
End Sub

Function SaveDialog() As String
    Dim i As Long
    
    SaveDialog = ""
    SaveDialog = SaveDialog & frmDesign.Width & "|"
    SaveDialog = SaveDialog & frmDesign.Height & vbNewLine
    
    Dim CountCtl As Long
    
    For Each Control In frmDesign
            If Control.Name = "ctlButton" Then
                If Control.Index > 0 Then
                    SaveDialog = SaveDialog & Control.Name & "|"
                    SaveDialog = SaveDialog & Control.Left & "|"
                    SaveDialog = SaveDialog & Control.Top & "|"
                    SaveDialog = SaveDialog & Control.Width & "|"
                    SaveDialog = SaveDialog & Control.Height & vbNewLine
                    CountCtl = CountCtl + 1
                End If
            ElseIf Control.Name = "ctlEdit" Then
                If Control.Index > 0 Then
                    SaveDialog = SaveDialog & Control.Name & "|"
                    SaveDialog = SaveDialog & Control.Left & "|"
                    SaveDialog = SaveDialog & Control.Top & "|"
                    SaveDialog = SaveDialog & Control.Width & "|"
                    SaveDialog = SaveDialog & Control.Height & vbNewLine
                    CountCtl = CountCtl + 1
                End If
            End If
NextObjecta:
    Next
    
    SaveDialog = CountCtl & "|" & vbNewLine & SaveDialog
    
End Function

Private Sub picResizer_MouseDown(Button As Integer, Shift As Integer, X As Single, Y As Single)
    DragX = X: DragY = Y
End Sub

Private Sub picResizer_MouseMove(Button As Integer, Shift As Integer, X As Single, Y As Single)
    On Error Resume Next
    If Button = 1 Then
        picResizer.Move AlignToGrid(picResizer.Left + X - DragX), AlignToGrid(picResizer.Top + Y - DragY)
        AssignedObject.Width = picResizer.Left - AssignedObject.Left
        AssignedObject.Height = picResizer.Top - AssignedObject.Top
    End If
End Sub

