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
    Public Property rows As Nickname()
End Class

Public Class Nickname
    Public Property nickname As String
End Class

Public Class CraftResponse
    Public Property code As Integer
    Public Property item As String
    Public Property rows As Row()
End Class

Public Class Row
    Public Property id As Integer
    Public Property name As String
    Public Property rarity As String
    Public Property craftable As Integer
End Class
