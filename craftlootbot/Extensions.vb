﻿Imports System.Globalization
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports Google.Apis.Drive.v3
Imports Newtonsoft
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.InlineKeyboardButtons

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
    Function prepareFile(filename As String, text As String, Item As String, Optional Extension As String = ".txt") As FileToSend
        IO.File.WriteAllText(filename, text)
        Dim file As New FileToSend
        file.Content = IO.File.Open(filename, IO.FileMode.Open)
        file.Filename = Item + Extension
        StampaDebug("Item: " + Item + ", Filename: " + file.Filename)
        Return file
    End Function

    'ottiene oggetti necessari al craft
    Function requestCraft(id As Integer) As Integer()
        If Not CraftIds.ContainsKey(id) Then Return {}
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

    'Creo File per Salvataggio Callback di /craft
    Function createfile(text As String, UserID As Long)
        IO.Directory.CreateDirectory("crafts")
        Dim filename = UserID.ToString + "_" + getFileName()
        IO.File.WriteAllText("crafts/" + filename, text)
        Return filename
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
        Dim button1 As New SwitchInlineButton("Torna Inline", query_text)
        Dim row1() As SwitchInlineButton
        'button1.SwitchInlineQuery =
        'button1.CallbackData = Nothing
        row1.Add(button1)
        keyboardbuttons.Add(row1)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera con oggetti per /info
    Function creaCraftKeyboard(ids() As Integer, UserID As Long) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)
        Dim filename = createfile(String.Join("_", ids), UserID)
        Dim button As New CallbackInlineButton("File di testo", "craftF_" + filename)
        Dim button2 As New CallbackInlineButton("Messaggio", "craftM_" + filename)

        row.Add(button)
        row.Add(button2)

        keyboardbuttons.Add(row.ToArray)

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
            Dim button As New CallbackInlineButton(ItemIds(i).name, "info_" + i.ToString)
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

    Function isDungeon(text As String) As Boolean
        If text.Contains("senti tremare il pavimento ed una gabbia ti circonda lentamente") Then
            If text.Contains(" _ ") Then
                Return True
            End If
        End If
        Return False
    End Function

    Function isIspezione(text As String) As Boolean
        If text.Contains("Sul portone del rifugio") Then
            If text.Contains(" _ ") Then
                Return True
            End If
        End If
        Return False
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

    Function getHalloweenItemsJSON() As String
        If Not IO.File.Exists("halloween.json") Then Return ""
        Return IO.File.ReadAllText("halloween.json")
    End Function

    Function getRifugiCraftsJSON() As String
        If Not IO.File.Exists("rifugi/crafts.json") Then Return ""
        Return IO.File.ReadAllText("rifugi/crafts.json")
    End Function

    Function getDungeonItems(input As String) As IEnumerable(Of KeyValuePair(Of Integer, Item))
        Dim first_letter = input.First
        Dim last_letter = input.Last
        Dim words() = input.Split("-")
        Dim pattern = "[A-z0-9òàèéìù'-]"
        Dim reg As String = "^" + first_letter
        For Each w In words
            reg += pattern + "{" + w.Where(Function(s) s.Equals("_"c)).Count.ToString + "} "
        Next
        reg = reg.Remove(reg.Length - 1)
        reg += last_letter + "$"
        Dim regex As New Regex(reg)
        Return ItemIds.Where(Function(p) regex.IsMatch(p.Value.name))
    End Function

    'Function getIspezioneWords(input As String) As IEnumerable(Of String)
    '    Dim letters = alphabet.ToList
    '    For Each cha In input
    '        If letters.Contains(cha.ToString.ToUpper) Then
    '            letters.Remove(cha.ToString.ToUpper)
    '        End If
    '    Next
    '    Dim pattern = "[" + String.Join("", letters) + "]{1}"
    '    input = input.ToUpper.Replace(" ", "").Replace("_", pattern)
    '    Dim reg As String = "^" + input + "$"
    '    Dim regex As New Regex(reg, RegexOptions.IgnoreCase)

    '    Return Italian_dictionary.Where(Function(p) regex.IsMatch(p))
    'End Function

    Sub saveTeamMembers()
        Dim team_file = "team.dat"
        IO.File.WriteAllLines(team_file, team_members)
    End Sub

    Function getdriveFileID(url As Uri) As String
        'FORMATO GDRIVE  drive.google.com/open?id=0BwncXt4cfJK8d2Q3cVJURml2Szg
        Dim q = url.Query.Split("=")
        If q(0) = "?id" Then Return q(1)
        Return Nothing
    End Function

    Function convertDropboxLink(url As Uri) As Uri
        Try
            Return New Uri(url.AbsoluteUri.Replace("dl=0", "dl=1"))
        Catch ex As Exception
            Return url
        End Try
    End Function

    Function getPrezziStringFromURL(url As Uri) As String
        Try
            Dim resultText As String
            If url.Host.Contains("drive.google.com") Then
                Dim init As New Google.Apis.Services.BaseClientService.Initializer With {
                    .ApiKey = Drive_API,
                    .ApplicationName = "Craftlootbot"}
                Dim s As New DriveService(init)
                Dim f = s.Files.Get(getdriveFileID(url))
                Dim stream As New IO.MemoryStream
                Dim d = f.DownloadAsync(stream).Result
                If d.Status <> Google.Apis.Download.DownloadStatus.Completed Then
                    Dim export = s.Files.Export(getdriveFileID(url), "text/plain")
                    stream = New IO.MemoryStream
                    Dim e = export.DownloadAsync(stream).Result
                    If e.Status <> Google.Apis.Download.DownloadStatus.Completed Then Return ""
                End If
                stream.Position = 0
                Dim sr As New IO.StreamReader(stream)
                resultText = sr.ReadToEnd
            Else
                If url.Host.Contains("dropbox.com") Then url = convertDropboxLink(url)
                Dim handler As New Http.HttpClientHandler
                If handler.SupportsAutomaticDecompression() Then
                    handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
                End If
                Dim client As New Http.HttpClient(handler)
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html")
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/plain")
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
                resultText = client.GetStringAsync(url).Result
            End If

            If isPrezziNegozi(resultText) Then Return resultText
            Return ""
        Catch ex As Exception
            Return ""
        End Try
    End Function

    'Function isPrezziStringFromURL(url As Uri) As Boolean
    '    Dim handler As New Http.HttpClientHandler
    '    If handler.SupportsAutomaticDecompression() Then
    '        handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
    '    End If
    '    Dim client As New Http.HttpClient(handler)
    '    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html")
    '    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/plain")
    '    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
    '    Try
    '        Dim resultText = client.GetStringAsync(url).Result
    '        If isPrezziNegozi(resultText) Then Return True
    '        Return False
    '    Catch ex As Exception
    '        Return False
    '    End Try
    'End Function

    Function checkLink(link As String) As Uri
        Dim URL As Uri
        Try
            URL = New Uri(link, UriKind.Absolute)
            Return URL
        Catch ex As Exception
            Return Nothing
        End Try
        Return Nothing
    End Function

    Sub saveEquip(items As List(Of Integer), UserID As Integer)
        IO.Directory.CreateDirectory("equip")
        Dim text As New Text.StringBuilder
        Dim path = "equip/" + UserID.ToString + ".txt"
        For Each id In items
            Dim it = ItemIds(id)
            Dim type = it.getEquipType
            text.Append(type.ToString).Append(":").AppendLine(id)
        Next
        IO.File.WriteAllText(path, text.ToString)
    End Sub

    Function getEquip(UserID As Integer) As Integer()
        Dim path = "equip/" + UserID.ToString + ".txt"
        Dim lines = IO.File.ReadAllLines(path)
        Dim res As Integer()
        For Each line In lines
            res.Add(line.Split(":")(1))
        Next
        Return res
    End Function

    Function HasEquip(UserID As Integer) As Boolean
        Dim path = "equip/" + UserID.ToString + ".txt"
        If IO.File.Exists(path) Then Return True
        Return False
    End Function

    Function HasZaino(UserID As Integer) As Boolean
        Dim path = "zaini/" + UserID.ToString + ".txt"
        If IO.File.Exists(path) Then Return True
        Return False
    End Function
    'message.From.id
    'ottengo zaino e aggiungo l'equipaggiamento
    Function getZaino(UserID As Integer) As Dictionary(Of Item, Integer)
        Dim Zaino_path As String = "zaini/" + UserID.ToString + ".txt"
        Dim Equip_path As String = "equip/" + UserID.ToString + ".txt"
        Dim zaino As String = ""
        If HasZaino(UserID) Then
            zaino = IO.File.ReadAllText(Zaino_path)
        End If
        Dim zainoDic = parseZaino(zaino)
        If IO.File.Exists(Equip_path) Then
            Dim equip = getEquip(UserID)
            If equip.Length > 0 Then
                For Each eq In equip
                    If zainoDic.ContainsKey(ItemIds(eq)) Then
                        zainoDic(ItemIds(eq)) += 1
                    Else
                        zainoDic.Add(ItemIds(eq), 1)
                    End If
                Next
            End If
        End If
        Return zainoDic
    End Function

    Function getIntestazioneMessaggio(static_intestazione As String, oggetti() As Integer) As String

        Dim ogg_string As New Dictionary(Of String, Integer)
        For Each i In oggetti
            If ogg_string.ContainsKey(ItemIds(i).name) Then
                ogg_string(ItemIds(i).name) += 1
            Else
                ogg_string.Add(ItemIds(i).name, 1)
            End If
        Next
        Dim intestazione As String = static_intestazione

        For Each it In ogg_string
            intestazione += it.Value.ToString + "x " + it.Key + If(ogg_string.Keys.Last = it.Key, "", ", ")
        Next
        'If intestazione.Last = " " Then intestazione = intestazione.Substring(0, intestazione.Length - 2)
        intestazione += ": "

        Return intestazione
    End Function

End Module