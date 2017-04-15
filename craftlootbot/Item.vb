﻿Imports System.Text
Imports craftlootbot

Public Class Item
    Public Property id As Integer
    Public Property name As String
    Public Property rarity As String
    Public Property description As String
    Public Property value As Integer
    Public Property estimate As Integer
    Public Property craftable As Integer
    Public Property searchable As Integer
    Public Property reborn As Integer
    Public Property power As Integer
    Public Property power_armor As Integer
    Public Property power_shield As Integer
    Public Property dragon_power As Integer
    Public Property critical As Integer
    Public Property category As Integer
    Public Property rarity_name As String

    Public Overrides Function ToString() As String
        Dim builder As New StringBuilder
        builder.AppendLine("*" + name.ToString + "*")
        builder.Append("ID oggetto: ").AppendLine(id)
        builder.Append("Rarità: ").AppendLine(rarity_name + " (" + rarity + ")")
        builder.Append("Rinascita richiesta: ").AppendLine(If(reborn - 1 = 0, "Base", "R" + (reborn - 1).ToString))
        builder.Append("Prezzo base: ").AppendLine(prettyCurrency(value))
        If Not IsNothing(estimate) Then builder.Append("Valore corrente stimato: ").AppendLine(prettyCurrency(estimate))
        builder.Append("Craftabile: ").AppendLine(If(craftable = 1, "Si", "No"))
        Select Case category
            Case 1
                builder.Append("Categoria: ")
                builder.AppendLine("Pozioni")
            Case 2
                builder.Append("Categoria: ")
                builder.AppendLine("Talismani")
            Case 3
                builder.Append("Categoria: ")
                builder.AppendLine("Oggetti speciali")
            Case 4
                builder.Append("Categoria: ")
                builder.AppendLine("Consumabili")
        End Select
        If category > 0 Then builder.Append("Descrizione: ").AppendLine(description)
        builder.Append("Numero di usi per il set Necro: ").AppendLine(countNecro)
        If craftable Then
            Dim spesa As Integer = If(rarity_value.ContainsKey(rarity), rarity_value(rarity), 0)
            Dim punti_craft As Integer = If(rarity_craft.ContainsKey(rarity), rarity_craft(rarity), 0)
            contaCosto(id, spesa, punti_craft)
            builder.Append("Costo per il Craft: ").AppendLine(prettyCurrency(spesa))
            builder.Append("Punti craft guadagnati: ").AppendLine(punti_craft)
        End If
        If power > 0 Then
            builder.Append("Danno (Arma): ").AppendLine(power)
            builder.Append("Critico: ").Append(critical).AppendLine("%")
        End If
        If power_armor < 0 Then
            builder.Append("Difesa (Armatura): ").AppendLine(power_armor)
            builder.Append("Critico: ").Append(critical).AppendLine("%")
        End If
        If power_shield < 0 Then
            builder.Append("Difesa (Scudo): ").AppendLine(power_shield)
            builder.Append("Critico: ").Append(critical).AppendLine("%")
        End If
        If dragon_power <> 0 Then builder.Append("Danno/Difesa: ").AppendLine(dragon_power)


        If craftable Then
            builder.AppendLine.AppendLine("Necessari:")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_1).name).AppendLine(" (" + ItemIds(CraftIds(id).material_1).rarity + ")")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_2).name).AppendLine(" (" + ItemIds(CraftIds(id).material_2).rarity + ")")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_3).name).AppendLine(" (" + ItemIds(CraftIds(id).material_3).rarity + ")")
        End If

        Return builder.ToString
    End Function

    Function countNecro() As Integer
        Dim count As Integer
        contaUsi(221, count)
        contaUsi(577, count)
        contaUsi(600, count)
        Return count
    End Function

    Sub contaUsi(necro As Integer, ByRef count As Integer)
        Dim craft As IDCraft = CraftIds(necro)
        Dim required_ids() As Integer = {craft.material_1, craft.material_2, craft.material_3}
        For Each i In required_ids
            If i = id Then count += 1
            If isCraftable(i) Then
                contaUsi(i, count)
            End If
        Next
    End Sub

    Sub contaCosto(item_id As Integer, ByRef spesa As Integer, ByRef punticraft As Integer)
        Dim craft As IDCraft = CraftIds(item_id)
        Dim required_ids() As Integer = {craft.material_1, craft.material_2, craft.material_3}
        Dim ite As Item
        For Each i In required_ids
            ite = ItemIds(i)
            If isCraftable(i) Then
                If rarity_value.ContainsKey(ite.rarity) Then spesa += rarity_value.Item(ite.rarity)
                If rarity_craft.ContainsKey(ite.rarity) Then punticraft += rarity_craft.Item(ite.rarity)
                contaCosto(i, spesa, punticraft)
            End If
        Next
    End Sub

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

    Public Class ZainoComparer
        Implements IComparer(Of KeyValuePair(Of Item, Integer))
        Dim itemcomparer As New ItemComparer
        Public Function Compare(x As KeyValuePair(Of Item, Integer), y As KeyValuePair(Of Item, Integer)) As Integer Implements IComparer(Of KeyValuePair(Of Item, Integer)).Compare
            Return itemcomparer.Compare(x.Key, y.Key)
        End Function
    End Class
End Class


