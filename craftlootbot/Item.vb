Imports System.Text

Public Class Item
    Public Property id As Integer
    Public Property name As String
    Public Property rarity As String
    Public Property rarity_name As String
    Public Property value As Integer
    Public Property craftable As Integer
    Public Property reborn As Integer
    Public Property power As Integer
    Public Property power_armor As Integer
    Public Property power_shield As Integer
    Public Property dragon_power As Integer
    Public Property critical As Integer

    Public Overrides Function ToString() As String
        Dim builder As New StringBuilder
        builder.AppendLine(name).AppendLine()
        builder.Append("Rarità: ").AppendLine(rarity_name + " (" + rarity + ")")
        builder.Append("Prezzo base: ").AppendLine(value)
        builder.Append("Craftabile: ").AppendLine(If(craftable = 1, "Si", "No"))
        Return builder.ToString
    End Function

    Public Class ItemComparer
        Implements IComparer(Of Item)
        Dim rarity_string As New List(Of String)
        Dim a() As String = {"C", "NC", "R", "UR", "L", "E", "UE", "S", "U", "X"}
        Public Function Compare(x As Item, y As Item) As Integer Implements IComparer(Of Item).Compare
            If rarity_string.Count = 0 Then rarity_string = a.ToList
            If rarity_string.IndexOf(x.rarity) < rarity_string.IndexOf(y.rarity) Then Return -1
            If rarity_string.IndexOf(x.rarity) > rarity_string.IndexOf(y.rarity) Then Return 1
            If rarity_string.IndexOf(x.rarity) = rarity_string.IndexOf(y.rarity) Then
                If (x.id < y.id) Then Return -1
                If x.id > y.id Then Return 1
            End If
            Return 0
        End Function
    End Class
End Class


