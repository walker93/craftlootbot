Imports System.Text
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
        builder.Append("Rinascita richiesta: ").AppendLine(If(reborn - 1 = 0, "Base", getstars()))
        builder.Append("Prezzo base: ").AppendLine(prettyCurrency(value))
        If Not IsNothing(estimate) Then builder.Append("Valore corrente stimato: ").AppendLine(prettyCurrency(estimate))
        builder.Append("Craftabile: ").AppendLine(If(craftable = 1, "Si", "No"))

        builder.Append("Numero di usi per il set Necro: ").AppendLine(countNecro)
        If craftable AndAlso CraftIds.ContainsKey(id) Then
            Dim spesa As Integer = If(rarity_value.ContainsKey(rarity), rarity_value(rarity), 0)
            Dim punti_craft As Integer = If(rarity_craft.ContainsKey(rarity), rarity_craft(rarity), 0)
            Dim costoBase As Integer = 0
            Dim oggBase As Integer = 0
            contaCosto(id, spesa, punti_craft, costoBase, oggBase)
            builder.Append("Costo per il Craft: ").AppendLine(prettyCurrency(spesa))
            builder.Append("Valore oggetti base + costo craft: ").AppendLine(prettyCurrency(costoBase + spesa))
            builder.Append("Punti craft guadagnati: ").AppendLine(punti_craft)
            builder.Append("Numero oggetti base necessari: ").AppendLine(oggBase)
        End If
        If power > 0 Then
                builder.Append("Danno: +").AppendLine(power)
                builder.Append("Critico: ").Append(critical).AppendLine("%")
            End If
            If power_armor < 0 Then
                builder.Append("Difesa: ").AppendLine(power_armor)
                builder.Append("Critico: ").Append(critical).AppendLine("%")
            End If
            If power_shield < 0 Then
                builder.Append("Difesa: ").AppendLine(power_shield)
                builder.Append("Critico: ").Append(critical).AppendLine("%")
            End If
            If dragon_power <> 0 Then builder.Append("Danno/Difesa: ").AppendLine(If(dragon_power > 0, "+" + dragon_power.ToString, "-" + dragon_power.ToString))
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
            Case Else
                If power > 0 Then builder.Append("Categoria: ").AppendLine("Arma")
                If power_armor < 0 Then builder.Append("Categoria: ").AppendLine("Armatura")
                If power_shield < 0 Then builder.Append("Categoria: ").AppendLine("Scudo")
                If dragon_power > 0 Then builder.Append("Categoria: ").AppendLine("Equipaggiamento Drago")
        End Select
        If category > 0 Then builder.Append("Descrizione: ").AppendLine(description)
        If craftable AndAlso CraftIds.ContainsKey(id) Then
            builder.AppendLine.AppendLine("Necessari:")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_1).name).AppendLine(" (" + ItemIds(CraftIds(id).material_1).rarity + ")")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_2).name).AppendLine(" (" + ItemIds(CraftIds(id).material_2).rarity + ")")
            builder.Append("> ").Append(ItemIds(CraftIds(id).material_3).name).AppendLine(" (" + ItemIds(CraftIds(id).material_3).rarity + ")")
        End If
        builder.AppendLine.Append(stampaUsi)
            Return builder.ToString
    End Function

    'restituisce ID necessari e usi
    Function getRelatedItemsIDs() As Integer()
        Dim items As Integer()
        'necessari
        If craftable AndAlso CraftIds.ContainsKey(id) Then
            items.Add(ItemIds(CraftIds(id).material_1).id)
            items.Add(ItemIds(CraftIds(id).material_2).id)
            items.Add(ItemIds(CraftIds(id).material_3).id)
        End If
        'usi
        For Each craft In CraftIds
            Dim required_ids() As Integer = {craft.Value.material_1, craft.Value.material_2, craft.Value.material_3}
            If required_ids.Contains(id) Then
                items.Add(craft.Value.material_result)
            End If
        Next
        Return items
    End Function

    'restituisce stringa Stelle rinascita
    Function getstars()
        Dim res As String = ""
        For i = 1 To reborn - 1
            res += "⭐️"
        Next
        Return res
    End Function

    'restituisce stringa oggetti in cui si può usare l'item
    Function stampaUsi() As String
        Dim usi_builder As New StringBuilder("Usato per:")
        usi_builder.AppendLine()
        Dim usi As Integer = 0
        For Each craft In CraftIds
            Dim required_ids() As Integer = {craft.Value.material_1, craft.Value.material_2, craft.Value.material_3}
            If required_ids.Contains(id) Then
                usi += 1
                usi_builder.Append("> ").Append(ItemIds(craft.Value.material_result).name).AppendLine(" (" + ItemIds(craft.Value.material_result).rarity + ")")
            End If
        Next
        If usi = 0 Then Return ""
        Return usi_builder.ToString
    End Function

    'restituisce conteggio nel craft necro
    Function countNecro() As Integer
        Dim count As Integer
        contaUsi(221, count)
        contaUsi(577, count)
        contaUsi(600, count)
        Return count
    End Function

    'ricorsione per conteggio necro
    Sub contaUsi(necro As Integer, ByRef count As Integer)
        Dim required_ids() As Integer = requestCraft(necro)
        If required_ids.Contains(id) Then count += 1
        For Each i In required_ids
            If isCraftable(i) Then
                contaUsi(i, count)
            End If
        Next
    End Sub

    'ricorsione per punti craft e costo
    Sub contaCosto(item_id As Integer, ByRef spesa As Integer, ByRef punticraft As Integer, ByRef costoBase As Integer, ByRef oggBase As Integer, Optional prezzi_dic As Dictionary(Of Item, Integer) = Nothing)
        Dim required_ids() As Integer = requestCraft(item_id)
        Dim ite As Item
        For Each i In required_ids
            ite = ItemIds(i)
            If isCraftable(i) Then
                If rarity_value.ContainsKey(ite.rarity) Then spesa += rarity_value.Item(ite.rarity)
                If rarity_craft.ContainsKey(ite.rarity) Then punticraft += rarity_craft.Item(ite.rarity)
                contaCosto(i, spesa, punticraft, costoBase, oggBase)
            Else
                If Not IsNothing(prezzi_dic) AndAlso prezzi_dic.ContainsKey(ite) AndAlso prezzi_dic(ite) > 0 Then
                    costoBase += prezzi_dic(ite)
                Else
                    costoBase += ite.value
                End If
                oggBase += 1
            End If
        Next
    End Sub

    Public Class ItemComparer
        Implements IComparer(Of Item)
        Implements IComparer(Of String)
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
        Public Function RarityCompare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            If rarity_string.Count = 0 Then rarity_string = a.ToList
            If rarity_string.IndexOf(x) < rarity_string.IndexOf(y) Then Return -1
            If rarity_string.IndexOf(x) > rarity_string.IndexOf(y) Then Return 1
            If rarity_string.IndexOf(x) = rarity_string.IndexOf(y) Then Return 0
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


