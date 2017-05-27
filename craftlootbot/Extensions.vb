﻿Imports System.Globalization
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports Newtonsoft
Imports Telegram.Bot.Types

Public Module MyExtensions
    'Aggiunge elementi ad un array
    <Extension()>
    Public Sub Add(Of T)(ByRef arr As T(), item As T)
        If arr IsNot Nothing Then
            Array.Resize(arr, arr.Length + 1)
            arr(arr.Length - 1) = item
        Else
            ReDim arr(0)
            arr(0) = item
        End If
    End Sub

    'Se il debug è attivo mostra in console il testo in input
    Sub StampaDebug(text As String)
        If debug Then Console.WriteLine(text)
    End Sub

    'Restituisce stringa soldi formattata
    Function prettyCurrency(value As Integer) As String
        Dim nfi As NumberFormatInfo = DirectCast(CultureInfo.InvariantCulture.NumberFormat.Clone(), NumberFormatInfo)
        nfi.NumberGroupSeparator = "'"
        nfi.NumberDecimalDigits = 0
        Return value.ToString("n", nfi) + "§"
    End Function

    'Ottiene nome file da timestamp corrente (usato per spedire alberi)
    Function getFileName() As String
        Dim uTime As ULong
        uTime = (Date.UtcNow - New DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds
        Dim filename As String = uTime.ToString + ".txt"
        Return filename
    End Function

    'Crea file e lo prepara ad essere caricato
    Function prepareFile(filename As String, text As String, Item As String) As FileToSend
        IO.File.WriteAllText(filename, text)
        Dim file As New FileToSend
        file.Content = IO.File.Open(filename, IO.FileMode.Open)
        file.Filename = Item + ".txt"
        Return file
    End Function

    'ottiene oggetti necessari al craft
    Function requestCraft(id As Integer) As Integer()
        Dim craft As IDCraft = CraftIds(id)
        Dim required_ids() As Integer = {craft.material_1, craft.material_2, craft.material_3}
        Return required_ids
    End Function

    'Invia richiesta API e ottiene info oggetti
    Function requestItems(name As String) As Item()
        'Dim res
        Dim arr As New List(Of Item)

        ' If rifugiMatch.Contains(name.ToLower) Then
        'Dim allrifugi() = Json.JsonConvert.DeserializeObject(Of ItemResponse)(getRifugiItemsJSON()).res
        'For Each rif In allrifugi
        '    arr.Add(rif)
        '    If rif.name.ToLower.Equals(name.ToLower.Trim) Then
        '        arr.Clear()
        '        arr.Add(rif)
        '        Return arr.ToArray
        '    End If
        'Next
        'Return arr.ToArray
        ' Else
        'Dim handler As New Http.HttpClientHandler
        'If handler.SupportsAutomaticDecompression() Then
        '    handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
        'End If
        'Dim client As New Http.HttpClient(handler)
        'client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
        'client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
        'Try
        '    res = client.GetStringAsync(getItemUrl(name)).Result
        'Catch ex As Exception
        '    Return arr.ToArray()
        'End Try
        ' End If
        Dim matching_items = ItemIds.Select(Function(p) p.Value).Where(Function(p) p.craftable AndAlso p.name.ToLower.Contains(name.ToLower))
        Try
            'Dim jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
            'If jsonres.code = 200 Then
            '    arr.AddRange(jsonres.res)
            'End If
            arr.AddRange(matching_items)
        Catch
            'Dim jsonres = Json.JsonConvert.DeserializeObject(Of SingleItemResponse)(res)
            'If jsonres.res IsNot Nothing Then arr.Add(jsonres.res)
        End Try
        Return arr.ToArray
    End Function

    'Crea tastiera per salvataggio zaino
    Function creaZainoKeyboard() As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboard As New ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboardbuttons()() As KeyboardButton
        Dim button1 As New KeyboardButton("Salva")
        Dim button2 As New KeyboardButton("Annulla")
        Dim row1() As KeyboardButton
        Dim row2() As KeyboardButton

        row1.Add(button1)
        row2.Add(button2)
        keyboardbuttons.Add(row1)
        keyboardbuttons.Add(row2)
        keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera per confronta
    Function creaConfrontaKeyboard(Optional withzaino As Boolean = False) As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboard As New ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboardbuttons()() As KeyboardButton
        Dim button1 As New KeyboardButton("Annulla")
        Dim row1() As KeyboardButton
        row1.Add(button1)
        If withzaino Then
            Dim button2 As New KeyboardButton("Utilizza il mio zaino")
            Dim row2() As KeyboardButton
            row2.Add(button2)
            keyboardbuttons.Add(row2)
        End If
        keyboardbuttons.Add(row1)
        keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera per salvataggio zaino inline
    Function creaInlineKeyboard(query_text As String) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim button1 As New InlineKeyboardButton("Torna Inline")
        Dim row1() As InlineKeyboardButton
        button1.SwitchInlineQuery = query_text
        button1.CallbackData = Nothing
        row1.Add(button1)
        keyboardbuttons.Add(row1)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera con oggetti per /info
    Function creaInfoKeyboard(ids() As Integer) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)
        'Dim rows As New List(Of InlineKeyboardButton())

        For Each i In ids
            Dim button As New InlineKeyboardButton(ItemIds(i).name, "info_" + i.ToString)
            row.Add(button)
            If row.Count = 2 Then
                keyboardbuttons.Add(row.ToArray)
                row.Clear()
            End If
            If ids.Last = i Then keyboardbuttons.Add(row.ToArray)
        Next
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera vuota
    Function creaNULLKeyboard() As ReplyMarkups.ReplyKeyboardRemove
        Dim keyboard As New ReplyMarkups.ReplyKeyboardRemove
        keyboard.RemoveKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Controlla esistenza giocatore
    Function checkPlayer(User As String) As Boolean
        Dim handler As New Http.HttpClientHandler
        If handler.SupportsAutomaticDecompression() Then
            handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
        End If
        Dim client As New Http.HttpClient(handler)
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
        Dim res = client.GetStringAsync(getPlayerUrl(User)).Result
        Dim jres = Json.JsonConvert.DeserializeObject(Of PlayerResponse)(res)

        If jres.res.Length > 0 Then
            For Each nick In jres.res
                If nick.nickname.ToLower = User.Trim.ToLower Then Return True
            Next
        End If
        Return False
    End Function

    'Ottiene informazioni su file e riga che hanno fatto l'eccezione
    Function getFrame(Exception As Exception) As String
        Dim trace As New StackTrace(Exception)
        Dim frame As StackFrame = trace.GetFrame(1)
        Return frame.GetFileName + "riga: " + frame.GetFileLineNumber.ToString
    End Function

    'controllo se il testo matcha uno zaino
    Function isZaino(text As String) As Boolean
        Dim rex As New Regex("\> ([A-z 0-9òàèéìù'-]+)\(([0-9]+)\)")
        Return rex.IsMatch(text)
    End Function

    'controllo se il testo matcha un elenco cerco inline
    Function isInlineCerco(text As String) As Boolean
        Dim rex As New Regex("\> ([0-9]+) di ([A-z òàèéìù'-]+) \(([A-Z]+)\)")
        Return rex.IsMatch(text)
    End Function

    'controllo se il testo match un elenco prezzi negozio
    Function isPrezziNegozi(text As String) As Boolean
        Dim rex As New Regex("^([A-z 0-9òàèéìù'-]+):([0-9]+)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    'Controllo se il testo matcha un'apertura scrigni di loot
    Function isAperturaScrigno(text As String) As Boolean
        Dim rex As New Regex("\> ([0-9]+)x ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    Function isListaoVendi(text As String) As Boolean
        Dim rex As New Regex("\> (([0-9]+) su )?([0-9]+) di ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    'dati due dizionari(item, quantità) restituisce un nuovo dizionario contenente gli oggetti del secondo che sono contenuti nel primo
    Function ConfrontaDizionariItem(zaino As Dictionary(Of Item, Integer), cerco As Dictionary(Of Item, Integer)) As Dictionary(Of Item, Integer)
        Dim res As New Dictionary(Of Item, Integer)
        For Each it In cerco
            If zaino.ContainsKey(it.Key) Then
                res.Add(it.Key, zaino.Item(it.Key))
            End If
        Next
        Return res
    End Function

    Function SottrazioneDizionariItem(Dic1 As Dictionary(Of Item, Integer), Dic2 As Dictionary(Of Item, Integer)) As Dictionary(Of Item, Integer)
        Dim res As New Dictionary(Of Item, Integer)
        For Each it In Dic1
            If Dic2.ContainsKey(it.Key) Then
                Dim diff = Dic1(it.Key) - Dic2(it.Key)
                If diff > 0 Then
                    res.Add(it.Key, diff)
                End If
            Else
                res.Add(it.Key, Dic1(it.Key))
            End If
        Next
        Return res
    End Function

    Function getRifugiItemsJSON() As String
        If Not IO.File.Exists("rifugi/items.json") Then Return ""
        Return IO.File.ReadAllText("rifugi/items.json")
    End Function

    Function getRifugiCraftsJSON() As String
        If Not IO.File.Exists("rifugi/crafts.json") Then Return ""
        Return IO.File.ReadAllText("rifugi/crafts.json")
    End Function

End Module