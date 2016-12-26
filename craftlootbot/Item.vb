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
        Public Function Compare(x As Item, y As Item) As Integer Implements IComparer(Of Item).Compare
            Select Case x.rarity
                Case "C"
                    Select Case y.rarity
                        Case "C"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "NC"
                            Return -1
                        Case "R"
                            Return -1
                        Case "UR"
                            Return -1
                        Case "L"
                            Return -1
                        Case "E"
                            Return -1
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "NC"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "R"
                            Return -1
                        Case "UR"
                            Return -1
                        Case "L"
                            Return -1
                        Case "E"
                            Return -1
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "R"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "UR"
                            Return -1
                        Case "L"
                            Return -1
                        Case "E"
                            Return -1
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "UR"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "L"
                            Return -1
                        Case "E"
                            Return -1
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "L"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "E"
                            Return -1
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "E"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            Return 1
                        Case "E"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "UE"
                            Return -1
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "UE"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            Return 1
                        Case "E"
                            Return 1
                        Case "UE"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "S"
                            Return -1
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "S"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            Return 1
                        Case "E"
                            Return 1
                        Case "UE"
                            Return 1
                        Case "S"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "U"
                            Return -1
                        Case "X"
                            Return -1
                    End Select
                Case "U"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            Return 1
                        Case "E"
                            Return 1
                        Case "UE"
                            Return 1
                        Case "S"
                            Return 1
                        Case "U"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                        Case "X"
                            Return -1
                    End Select
                Case "X"
                    Select Case y.rarity
                        Case "C"
                            Return 1
                        Case "NC"
                            Return 1
                        Case "R"
                            Return 1
                        Case "UR"
                            Return 1
                        Case "L"
                            Return 1
                        Case "E"
                            Return 1
                        Case "UE"
                            Return 1
                        Case "S"
                            Return 1
                        Case "U"
                            Return 1
                        Case "X"
                            If (x.id < y.id) Then
                                Return -1
                            ElseIf x.id > y.id Then
                                Return 1
                            Else
                                Return 0
                            End If
                    End Select
            End Select

            Return 0

        End Function
    End Class
End Class


