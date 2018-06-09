Imports System.Globalization
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports Google.Apis.Drive.v3
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
    Function prepareFile(filename As String, text As String, Item As String, Optional Extension As String = ".txt") As InputFiles.InputOnlineFile
        IO.File.WriteAllText(filename, text)
        Dim stream = IO.File.Open(filename, IO.FileMode.Open)
        Dim file As New InputFiles.InputOnlineFile(stream)
        file.FileName = Item + Extension
        StampaDebug("Item: " + Item + ", Filename: " + file.FileName)
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
        Dim filename = UserID.ToString + "_" + getFileName()
        IO.File.WriteAllText("crafts/" + filename, text)
        Return filename
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

    'dati due dizionari(item, quantità) restituisce un nuovo dizionario contenente la quantità degli oggetti nel primo meno la quantità nel secondo
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

    'restituisce oggetti possibili nella stanza dungeon
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

    'salva il team su file
    Sub saveTeamMembers()
        Dim team_file = "team.dat"
        IO.File.WriteAllLines(team_file, team_members)
    End Sub

    'restituisce l'id del file gdrive
    Function getdriveFileID(url As Uri) As String
        'FORMATO GDRIVE  drive.google.com/open?id=0BwncXt4cfJK8d2Q3cVJURml2Szg
        Dim q = url.Query.Split("=")
        If q(0) = "?id" Then Return q(1)
        Return Nothing
    End Function

    'converte link Dropbox in file scaricabile
    Function convertDropboxLink(url As Uri) As Uri
        Try
            Return New Uri(url.AbsoluteUri.Replace("dl=0", "dl=1"))
        Catch ex As Exception
            Return url
        End Try
    End Function

    'ottengo i prezzi salvati nel file puntato dall'url
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

    'controllo il link
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

    'creo l'intestazione del messaggio di risposta
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

    'data una lista di ID, restituisce la stringa nel formato "<nome oggetto>:<quantità>,"
    Function ItemIdListToInputString(item_ids As List(Of Integer)) As String
        Dim count As New Dictionary(Of Integer, Integer)
        For Each it In item_ids
            If count.ContainsKey(it) Then
                count(it) += 1
            Else
                count.Add(it, 1)
            End If
        Next
        Dim result As String = ""
        For Each it In count
            result += ItemIds(it.Key).name + ":" + it.Value.ToString + If(count.Keys.Last = it.Key, "", ",")
        Next
        Return result
    End Function

    'ottengo gli alias globali
    Function getGlobalAlias() As Dictionary(Of String, String)
        Dim alias_path As String = "alias/GLOBAL.txt"
        Dim GlobalAlias As New Dictionary(Of String, String)
        If IO.File.Exists(alias_path) Then
            Dim lines = IO.File.ReadAllLines(alias_path)
            For Each line In lines
                GlobalAlias.Add(line.Split("==")(0), line.Split("==")(2))
            Next
        End If
        Return GlobalAlias
    End Function

    'Unisco Alias personali e Globali, quindi restituisco valore
    Function GetAliasValue(UserID As Long, keyword As String) As String
        Dim result As String = keyword
        Dim PersonalAlias = getPersonalAlias(UserID)
        Dim union = PersonalAlias.Union(getGlobalAlias).ToDictionary(Function(k) k.Key, Function(v) v.Value)
        If union.ContainsKey(keyword) Then result = union(keyword)
        Return result
    End Function
End Module