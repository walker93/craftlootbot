Public Class ItemResponse
    Public Property code As Integer
        Get
            Return m_code
        End Get
        Set(value As Integer)
            m_code = value
        End Set
    End Property

    Public res() As Item
    Private m_code As Integer
End Class

Public Class SingleItemResponse
    Public Property code As Integer
    Public res As Item
End Class

Public Class PlayerResponse
    Public Property code As Integer
    Public Property res As Nickname()
End Class

Public Class Nickname
    Public Property nickname As String
End Class

Public Class row
    Public Property id As Integer
    Public Property name As String
    Public Property rarity As String
    Public Property craftable As Integer
End Class

Public Class CraftResponse
    Public Property code As Integer
    Public Property item As String
    Public Property res As row()
End Class

Public Class CraftTable
    Public code As Integer
    Public res() As IDCraft
End Class

Public Class IDCraft
    Public Property id As Integer
    Public Property material_1 As Integer
    Public Property material_2 As Integer
    Public Property material_3 As Integer
    Public Property material_result As Integer
End Class