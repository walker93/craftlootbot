Imports Telegram.Bot
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.Enums
Imports Newtonsoft
Imports System.Net
Imports System.Text.RegularExpressions

Module Module1

    Dim WithEvents api As TelegramBotClient
    Dim flush As Boolean = False
    Dim time_start As Date = Date.UtcNow
    Dim ItemIds As New Dictionary(Of Integer, Item)
    Dim items() As Item
    Dim stati As New List(Of KeyValuePair(Of ULong, Integer))
    Dim zaini As New Dictionary(Of ULong, String)

    Sub Main(ByVal args() As String)
        initializeVariables()
        If args.Length > 0 Then
            flush = args(0).Contains("flush")
        End If
        api = New TelegramBotClient(token.token)
        Try
            Dim bot = api.GetMeAsync.Result
            Console.WriteLine(bot.Username & ": " & bot.Id)
        Catch ex As Exception
            Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.Message)
        End Try
        Dim t As New Threading.Thread(New Threading.ThreadStart(AddressOf initialize_Dictionary))
        t.Start()
        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start()
        Dim stats_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf salvaStats))
        stats_thread.Start()
    End Sub

    Sub initialize_Dictionary()
        While True
            Console.WriteLine("Aggiorno database")
            Dim handler As New Http.HttpClientHandler
            If handler.SupportsAutomaticDecompression() Then
                handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
            End If
            Dim client As New Http.HttpClient(handler)
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
            Try
                Dim res = client.GetStringAsync(ITEM_URL).Result
                Dim jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
                items = jsonres.res
                Console.WriteLine("Numero di oggetti: " + items.Length.ToString)
                ItemIds.Clear()
                For Each it As Item In items
                    ItemIds.Add(it.id, it)
                Next
                Console.WriteLine("Terminato aggiornamento")
                Threading.Thread.Sleep(12 * 60 * 60 * 1000) 'aggiorno ogni 12 ore
            Catch
                Console.WriteLine("Errori durante l'aggiornamento")
                Threading.Thread.Sleep(60 * 1000) 'Aggiorno ogni minuto
            End Try
        End While
    End Sub

    Sub run()
        Dim updates() As Update
        Dim offset As Integer = 0

        While True
            Try
                updates = api.GetUpdatesAsync(offset,, 20).Result
                For Each up As Update In updates
                    Select Case up.Type
                        Case UpdateType.MessageUpdate
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_Message))
                            t.Start(up.Message)
                        Case UpdateType.InlineQueryUpdate
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_query))
                            t.Start(up.InlineQuery)
                        Case UpdateType.ChosenInlineResultUpdate
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_ChosenQuery))
                            t.Start(up.ChosenInlineResult)
                    End Select
                    offset = up.Id + 1
                Next
            Catch ex As AggregateException
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
            End Try
        End While
    End Sub

#Region "Inline Query"

    Sub process_query(InlineQuery As InlineQuery)
        StampaDebug(String.Format("{0} {1} From: {2}-{3} ID:{4} TEXT: {5}", Now.ToShortDateString, Now.ToShortTimeString, InlineQuery.From.Id, InlineQuery.From.FirstName, InlineQuery.Id, InlineQuery.Query))
        Dim query_text As String = InlineQuery.Query.Trim.ToLower
        Dim path As String = "zaini/" + InlineQuery.From.Id.ToString + ".txt"
        Dim hasZaino As Boolean = IO.File.Exists(path)
        Dim results As New List(Of InlineQueryResults.InlineQueryResult)
        Dim res As InlineQueryResults.InlineQueryResultArticle
        Try
            If hasZaino Then
                'lo zaino è salvato, procedo ad elaborare
                If query_text = "" Then
                    api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Exit Sub
                End If
                Dim matching_items() As Item = requestItems(query_text)
                If matching_items Is Nothing OrElse matching_items.Length = 0 Then
                    api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Exit Sub
                End If

                For Each item In matching_items
                    Dim CraftList As New List(Of Item)
                    Dim zainoDic_copy As New Dictionary(Of Item, Integer)
                    Dim gia_possiedi As New Dictionary(Of Item, Integer)
                    Dim spesa As Integer = 0
                    Dim zainoDic As New Dictionary(Of Item, Integer)
                    Dim it As New Item
                    Dim zaino As String = ""
                    zaino = IO.File.ReadAllText(path)
                    zainoDic = parseZaino(zaino)
                    zainoDic_copy = zainoDic
                    If Not isCraftable(item.id) Then Continue For
                    getNeededItemsList(item.id, CraftList, zainoDic_copy, gia_possiedi, spesa)
                    If CraftList.Count <> 0 Then
                        it = item
                        res = New InlineQueryResults.InlineQueryResultArticle
                        Dim content As New InputMessageContents.InputTextMessageContent
                        content.MessageText = getCercoText(createCraftCountList(CraftList), zainoDic)
                        res.InputMessageContent = content
                        res.Title = "Cerco per " + it.name
                        res.Description = If(content.MessageText.Contains("Possiedo"), "Hai già tutti gli oggetti.", Nothing)
                        res.Id = it.id
                        results.Add(res)
                    End If
                Next
                api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 5, True)
            Else
                'Non ha zaino, propongo di salvarlo
                api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True,, "Cliccami e salva lo zaino per questa funzione", "zaino")
            End If
        Catch e As Exception
            Try
                Console.WriteLine(e.Message)
                sendReport(e, InlineQuery)
            Catch
            End Try
        End Try
    End Sub

    Sub process_ChosenQuery(ChosenResult As ChosenInlineResult)
        stats(stats_params(3)) += 1
        stats.Item(stats_params(4)) = stats.Item(stats_params(0)) + stats.Item(stats_params(1)) + stats.Item(stats_params(2)) + stats.Item(stats_params(3))
    End Sub

    'Creo testo con lista
    Function getCercoText(dic As Dictionary(Of Item, Integer), zaino As Dictionary(Of Item, Integer)) As String
        Dim sor As New SortedDictionary(Of Item, Integer)(New Item.ItemComparer)
        For Each pair In dic
            sor.Add(pair.Key, pair.Value)
        Next
        Dim sortedDictionary As Dictionary(Of Item, Integer) = sor.Reverse.ToDictionary(Function(p) p.Key, Function(p) p.Value)
        Dim buildernecessari As New Text.StringBuilder()
        Dim intestazione As String = "Cerco:"
        buildernecessari.AppendLine(intestazione)
        Dim tot_necessari As Integer
        Dim necessari As Integer
        For Each it In sortedDictionary
            tot_necessari = sortedDictionary.Item(it.Key)
            With buildernecessari
                necessari = If(zaino.ContainsKey(it.Key), tot_necessari - zaino.Item(it.Key), tot_necessari)
                If necessari > 0 Then
                    .Append("> ") 'inizio riga
                    .Append(necessari.ToString)
                    .Append(" di ")
                    .Append(it.Key.name) 'Nome oggetto
                    .Append(" (" + it.Key.rarity + ")") 'Rarità
                    .AppendLine()
                End If
            End With
        Next
        If buildernecessari.ToString = (intestazione + Environment.NewLine) Then buildernecessari.Clear.AppendLine("Possiedo già tutti gli oggetti necessari")
        Dim result = buildernecessari.ToString
        Return result
    End Function

#End Region

#Region "Text Message"

    Private Sub process_Message(message As Message)
        Try
            If flush Then 'controllo flush, se attivo ignoro il messaggio
                If message.Date < time_start Then Exit Sub
            End If
            If message.Type <> MessageType.TextMessage Then Exit Sub
            Dim CraftList As New List(Of Item)
            Dim zainoDic As New Dictionary(Of Item, Integer)
            Dim CraftCount As New Dictionary(Of Item, Integer)
            Dim CraftTree As New List(Of KeyValuePair(Of Item, Integer))
            Dim gia_possiedi As New Dictionary(Of Item, Integer)
            Dim a As Message
            Dim item As String
            Dim id As Integer
            Dim spesa As Integer
            Dim it As New Item
            Dim lootbot_id As ULong = 171514820
            Dim kill As Boolean = False

            If isZaino(message.Text) Then
                IO.Directory.CreateDirectory("zaini")
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) Then
                    'sta inviando più parti di zaino
                    zaini.Item(message.From.Id) += message.Text
                Else
                    'sta inviando un singolo messaggio
                    IO.File.WriteAllText("zaini/" + message.From.Id.ToString + ".txt", message.Text)
                    Console.WriteLine("Salvato zaino di ID: " + message.From.Id.ToString)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!").Result
                End If
            Else
                Console.WriteLine("{0} {1} {2} from: {3}", Now.ToShortDateString, Now.ToShortTimeString, message.Text, message.From.Username)
            End If
            If message.Text.ToLower.StartsWith("/zaino") Then
                stati.Add(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) 'entro nello stato 10, ovvero salvataggio zaino
                zaini.Add(message.From.Id, "")
                If message.Chat.Type = ChatType.Group Or message.Chat.Type = ChatType.Supergroup Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inoltra i messaggi in privato").Result
                End If
                a = api.SendTextMessageAsync(message.From.Id, "Inoltra di seguito il tuo zaino diviso in più messaggi. " + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,, creaKeyboard).Result
            ElseIf message.Text.ToLower.Equals("salva") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then
                    IO.File.WriteAllText("zaini/" + message.From.Id.ToString + ".txt", zaini.Item(message.From.Id))
                    stati.Remove(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10))
                    zaini.Remove(message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!",,,, New ReplyMarkups.ReplyKeyboardHide).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai salvando uno zaino, utilizza /zaino per iniziare il salvataggio.",,,, New ReplyMarkups.ReplyKeyboardHide).Result
                End If
            ElseIf message.Text.ToLower.Equals("annulla") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then

                    stati.Remove(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10))
                    zaini.Remove(message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai annullato il salvataggio dello zaino",,,, New ReplyMarkups.ReplyKeyboardHide).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai salvando uno zaino, utilizza /zaino per iniziare il salvataggio.",,,, New ReplyMarkups.ReplyKeyboardHide).Result
                End If
            ElseIf message.Text.ToLower.StartsWith("/lista") Then
                item = message.Text.Replace("/lista", "").Trim
                If item = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto che vuoi ottenere").Result
                    Exit Sub
                End If
                id = getItemId(item)
                ItemIds.TryGetValue(id, it)
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                If id <> -1 Then
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                    zainoDic = parseZaino(zaino)
                    Dim zainoDic_copy = zainoDic
                    getNeededItemsList(id, CraftList, zainoDic_copy, gia_possiedi, spesa)

                    If CraftList.Count = 0 Then
                        Dim e = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è craftabile").Result
                    Else
                        If rarity_value.ContainsKey(ItemIds.Item(id).rarity) Then spesa += rarity_value.Item(ItemIds.Item(id).rarity)
                        a = api.SendTextMessageAsync(message.Chat.Id, getCraftListText(createCraftCountList(CraftList), it.name, zainoDic, gia_possiedi, spesa),,, message.MessageId).Result
                    End If
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If

            ElseIf message.Text.ToLower.StartsWith("/albero") Then
                item = message.Text.Replace("/albero", "").Trim
                If item = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto che vuoi ottenere").Result
                    Exit Sub
                End If
                id = getItemId(item)
                Dim prof As Integer = -1
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                If id <> -1 Then
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                    zainoDic = parseZaino(zaino)
                    getNeededItemsTree(id, prof, CraftTree, zainoDic, gia_possiedi, spesa)
                    If CraftTree.Count = 0 Then
                        Dim e = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è craftabile").Result
                    Else
                        If rarity_value.ContainsKey(ItemIds.Item(id).rarity) Then spesa += rarity_value.Item(ItemIds.Item(id).rarity)
                        Dim name As String = getFileName()
                        a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, getCraftTreeText(CraftTree, gia_possiedi, spesa), item),,, message.MessageId).Result
                        IO.File.Delete(name)
                    End If
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If
            ElseIf message.Text.ToLower.StartsWith("/svuota") Then
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                If IO.File.Exists(path) Then
                    IO.File.Delete(path)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Lo zaino è stato svuotato",,, message.MessageId).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino non è salvato",,, message.MessageId).Result
                End If
            ElseIf message.Text.ToLower.StartsWith("/item") Then
                item = message.Text.Replace("/item", "").Trim
                id = getItemId(item)
                If id <> -1 Then ItemIds.TryGetValue(id, it)
                a = api.SendTextMessageAsync(message.Chat.Id, it.ToString).Result
            ElseIf message.Text.ToLower.StartsWith("/rinascita") Then
                Dim args() As String = message.Text.Split(" ")
                If args.Length < 3 Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci tutti i parametri richiesti!").Result
                    Exit Sub
                End If
                Dim user As String = args(1)
                If Not checkPlayer(user) Or user.ToLower.Trim = message.From.Username.ToLower.Trim Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'utente inserito non è valido.").Result
                    Exit Sub
                End If
                Dim oggetto As String = message.Text.Substring(message.Text.LastIndexOf(user) + user.Length + 1).Trim
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                id = getItemId(oggetto)
                If id <> -1 Then
                    ItemIds.TryGetValue(id, it)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                If zaino = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Devi salvare lo zaino prima di usare questa funzione.").Result
                    Exit Sub
                End If
                zainoDic = parseZaino(zaino)
                If zainoDic.ContainsKey(it) Then
                    zainoDic.Remove(it)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non possiedi l'oggetto di scambio inserito.").Result
                    Exit Sub
                End If
                Dim name As String = getFileName()
                a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, creaScambi(zainoDic, oggetto, user), "Lista Scambi"),,, message.MessageId).Result
                IO.File.Delete(name)
            ElseIf message.Text.ToLower.StartsWith("/start") Then
                If message.Text.Contains("zaino") Then
                    'Proviene da inline propongo zaino
                    stati.Add(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) 'entro nello stato 10, ovvero salvataggio zaino
                    zaini.Add(message.From.Id, "")
                    a = api.SendTextMessageAsync(message.From.Id, "Inoltra o incolla di seguito il tuo zaino, può essere in più messaggi." + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,, creaKeyboard).Result
                Else
                    'Avvio normale
                    Dim builder As New Text.StringBuilder()
                    builder.AppendLine("Benvenuto in Craft Lootbot!")
                    builder.AppendLine("Questo bot permette di riceve la lista dei materiali necessari al craft oppure l'albero dei craft di un oggetto.")
                    builder.AppendLine("Usa /help per la guida ai comandi")
                    builder.AppendLine("Provami! Non ti deluderò!")
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
                End If

            ElseIf message.Text.ToLower.StartsWith("/help") Then
                Dim builder As New Text.StringBuilder
                Dim args() As String = message.Text.Split(" ")
                If args.Length = 1 Then builder.Append(help_builder)
                If args.Length >= 2 Then
                    Select Case args(1).ToLower.Trim.Replace("/", "")
                        Case "lista"
                            builder.Append(lista_builder.ToString)
                        Case "albero"
                            builder.Append(Albero_builder.ToString)
                        Case "zaino"
                            builder.Append(Zaino_builder.ToString)
                        Case "rinascita"
                            builder.Append(Rinascita_buider.ToString)
                        Case Else
                            builder.Append("Comando non riconosciuto.")
                    End Select
                End If
                a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString,,,,, ParseMode.Markdown).Result
            ElseIf message.Text.ToLower.StartsWith("/stats") Then
                Dim builder As New Text.StringBuilder("*STATISTICHE COMPLESSIVE:*")
                builder.AppendLine().AppendLine()
                stats.Item(stats_params(stats_params.Length - 1)) = IO.Directory.GetFiles("zaini/").Length
                For Each stat In stats
                    builder.Append("*" + stat.Key + ":* " + stat.Value.ToString).Append(vbCrLf)
                Next
                builder.AppendLine().AppendLine("*Negli ultimi " + stat_timeout.ToString + " minuti:*")
                For Each stat In delta_stats
                    builder.Append("*" + stat.Key + ":* " + stat.Value.ToString).Append(vbCrLf)
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString,,,,, ParseMode.Markdown).Result
            ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/kill") Then
                kill = True
                Dim ex As New Exception("PROCESSO TERMINATO SU RICHIESTA")
                Throw ex
            End If

            aggiornastats(message.Text)

        Catch e As Exception
            If e.Message = "PROCESSO TERMINATO SU RICHIESTA" Then
                Throw New Exception("PROCESSO TERMINATO SU RICHIESTA")
            End If
            Try
                Console.WriteLine(e.Message)
                Dim a
                sendReport(e, message)
                a = api.SendTextMessageAsync(message.Chat.Id, "Si è verificato un errore, riprova tra qualche istante." + vbCrLf + "Una segnalazione è stata inviata automaticamente allo sviluppatore, potrebbe contattarti per avere più informazioni.").Result
            Catch

            End Try
        End Try
    End Sub

    Sub getNeededItemsList(id As Integer, ByRef CraftList As List(Of Item), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer)
        Dim rows As Row() = requestCraft(id)
        'StampaDebug("Craft oggetto: " + ItemIds.Item(id).name)
        If rows Is Nothing Then Exit Sub
        Dim item As Item
        For Each row In rows
            item = RowToItem(row)
            If isCraftable(row.id) Then
                If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)

                If Not zaino.ContainsKey(item) Then
                    getNeededItemsList(row.id, CraftList, zaino, possiedi, spesa)
                Else
                    zaino.Item(item) -= 1
                    If zaino.Item(item) = 0 Then zaino.Remove(item)
                    If Not possiedi.ContainsKey(item) Then
                        possiedi.Add(item, 1) 'zaino.Item(item))
                    Else
                        possiedi.Item(item) += 1
                    End If
                End If
            Else
                CraftList.Add(ItemIds.Item(row.id))
            End If
        Next
    End Sub

    Sub getNeededItemsTree(id As Integer, ByRef prof As Integer, ByRef CraftTree As List(Of KeyValuePair(Of Item, Integer)), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer)
        Dim rows As Row() = requestCraft(id)
        prof += 1
        If rows Is Nothing Then Exit Sub
        CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(id), prof))
        Dim item As Item
        For Each row In rows
            item = RowToItem(row)
            If Not possiedi.ContainsKey(item) And zaino.ContainsKey(item) Then possiedi.Add(item, zaino.Item(item))
            If isCraftable(row.id) Then
                Dim prev_spesa = spesa
                If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)
                getNeededItemsTree(row.id, prof, CraftTree, zaino, possiedi, spesa)
                prof -= 1
            Else
                CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(row.id), prof + 1))
            End If
        Next
    End Sub

    Function creaScambi(zaino As Dictionary(Of Item, Integer), oggetto_scambio As String, user As String) As String
        Dim builder As New Text.StringBuilder
        Dim sorted = From pair In zaino
                     Order By pair.Value Descending
        Dim zainolist = sorted.ToList

        Dim totbuilder As New Text.StringBuilder
        For x = zainolist.Count - 1 To 1 Step -1
            x = zainolist.Count - 1
            Dim item = zainolist(x)
            Dim next_item = zainolist(x - 1)
            For i = 1 To If(item.Value > next_item.Value, next_item.Value, item.Value)
                builder = New Text.StringBuilder
                With builder
                    .Append("Scambia ")
                    .Append(item.Key.name).Append(",")
                    .Append(next_item.Key.name).Append(",")
                    .Append(oggetto_scambio).Append(",")
                    .Append(oggetto_scambio).Append(",,,")
                    .Append(user).Append(",")
                    .Append(9)
                    .Append(vbCrLf)
                End With
                totbuilder.Append(builder.ToString)
                zainolist(x) = New KeyValuePair(Of Item, Integer)(item.Key, zainolist(x).Value - 1)
                zainolist(x - 1) = New KeyValuePair(Of Item, Integer)(next_item.Key, zainolist(x - 1).Value - 1)
                zainolist.RemoveAll(New Predicate(Of KeyValuePair(Of Item, Integer))(Function(a) a.Value = 0))
            Next
        Next
        Return totbuilder.ToString
    End Function

    'Creo testo con lista
    Function getCraftListText(dic As Dictionary(Of Item, Integer), oggetto As String, zaino As Dictionary(Of Item, Integer), ByRef gia_possiedi As Dictionary(Of Item, Integer), spesa As Integer) As String

        Dim sor As New SortedDictionary(Of Item, Integer)(New Item.ItemComparer)
        For Each pair In dic
            sor.Add(pair.Key, pair.Value)
        Next
        Dim sortedDictionary As Dictionary(Of Item, Integer) = sor.Reverse.ToDictionary(Function(p) p.Key, Function(p) p.Value)
        Dim buildernecessari As New Text.StringBuilder()
        Dim builderposseduti As New Text.StringBuilder()
        builderposseduti.AppendLine()
        If zaino.Count > 0 Then builderposseduti.AppendLine("Già possiedi: ")
        For Each pos In gia_possiedi
            For i = 1 To pos.Value
                If rarity_value.ContainsKey(pos.Key.rarity) Then spesa -= rarity_value.Item(pos.Key.rarity)
            Next
            With builderposseduti
                .Append("> ")
                .Append(pos.Key.name)
                .Append(" (" + pos.Key.rarity + ", " + pos.Value.ToString + ")")
                .AppendLine()
            End With
        Next
        Dim intestazione As String = "Lista oggetti necessari per " + oggetto + ": "
        buildernecessari.AppendLine(intestazione)
        Dim tot_necessari As Integer
        Dim necessari As Integer
        For Each it In sortedDictionary
            tot_necessari = sortedDictionary.Item(it.Key)
            With buildernecessari
                If zaino.Count > 0 Then
                    If zaino.ContainsKey(it.Key) Then
                        necessari = tot_necessari - zaino.Item(it.Key)
                        If necessari > 0 Then
                            .Append("> ") 'inizio riga
                            .Append(necessari) 'necessari
                        Else
                            With builderposseduti
                                .Append("> ")
                                .Append(zaino.Item(it.Key).ToString + " su ")
                                .Append(tot_necessari.ToString)
                                .Append(" di ")
                                .Append(it.Key.name)
                                .Append(" (" + it.Key.rarity + ")") 'Rarità
                                .AppendLine()
                            End With
                            Continue For
                        End If
                        '.Append(If(necessari > 0, necessari, 0)) 
                    Else
                        .Append("> ") 'inizio riga
                        .Append(tot_necessari.ToString)
                    End If
                    .Append(" su ")
                End If
                .Append(tot_necessari.ToString) 'Totale oggetti necessari
                .Append(" di ")
                .Append(it.Key.name) 'Nome oggetto
                .Append(" (" + it.Key.rarity + ")") 'Rarità
                .AppendLine()
            End With
        Next
        If builderposseduti.ToString = "Già possiedi: " + Environment.NewLine Then builderposseduti.AppendLine().AppendLine("Nessuno")
        If buildernecessari.ToString = (intestazione + Environment.NewLine) Then buildernecessari.AppendLine().AppendLine("Nessuno")
        Dim result = buildernecessari.ToString + builderposseduti.ToString
        result += vbCrLf + "Per eseguire i craft spenderai: " + prettyCurrency(spesa) + If(zaino.Count > 0, vbCrLf + " (Escludendo oggetti già craftati)", "")
        Return result
    End Function

    'Creo testo albero
    Function getCraftTreeText(list As List(Of KeyValuePair(Of Item, Integer)), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer) As String
        Dim builder As New Text.StringBuilder("Albero craft per:  " + list(0).Key.name)
        For Each pos In possiedi
            For i = 1 To pos.Value
                If isCraftable(pos.Key.id) Then
                    If rarity_value.ContainsKey(pos.Key.rarity) Then spesa -= rarity_value.Item(pos.Key.rarity)
                End If
            Next
        Next
        builder.Append(vbCrLf).Append("Per eseguire i craft spenderai: " + prettyCurrency(spesa) + If(possiedi.Count > 0, " (Escludendo oggetti già craftati)", ""))
        builder.Append(vbCrLf)
        For Each item In list
            For i = 1 To item.Value
                builder.Append("     ")
            Next
            builder.Append(item.Value.ToString + "> ").Append(item.Key.name)
            If possiedi.ContainsKey(item.Key) Then builder.Append(" (" + possiedi.Item(item.Key).ToString + ")")
            builder.Append(vbCrLf)
        Next
        Return builder.ToString
    End Function


#End Region

    'incremento contatori statistiche
    Sub aggiornastats(text As String)
        Dim comando As String = text.Split(" ")(0).Trim.ToLower
        Select Case comando
            Case "/lista"
                stats.Item(stats_params(0)) += 1
            Case "/albero"
                stats.Item(stats_params(1)) += 1
            Case "/rinascita"
                stats.Item(stats_params(2)) += 1
        End Select
        stats.Item(stats_params(4)) = stats.Item(stats_params(0)) + stats.Item(stats_params(1)) + stats.Item(stats_params(2)) + stats.Item(stats_params(3))
    End Sub

    'controllo se il testo matcha uno zaino
    Function isZaino(text As String) As Boolean
        Dim rex As New Regex("\> ([A-z òàèéìù'-]+)\(([0-9]+)\)")
        Return rex.IsMatch(text)
    End Function

    'controllo se l'item è craftabile
    Function isCraftable(id As Integer) As Boolean
        Return If(ItemIds.Item(id).craftable = 0, False, True)
    End Function

    'Data una lista di oggetti ripetuti creo dizionario(oggetto, quantità)
    Function createCraftCountList(ByVal craftlist As List(Of Item)) As Dictionary(Of Item, Integer)
        Dim CraftCount As New Dictionary(Of Item, Integer)
        For Each it In craftlist
            If CraftCount.ContainsKey(it) Then
                CraftCount.Item(it) += 1
            Else
                CraftCount.Add(it, 1)
            End If
        Next
        Return CraftCount
    End Function

    'Dato il nome ottengo l'id
    Function getItemId(ByVal name As String) As Integer
        For Each it In items
            If it.name.ToLower = name.ToLower.Trim Then Return it.id
        Next
        Return -1
    End Function

    'Dato uno zaino in stringa restituisco dizionario(oggetto, quantità)
    Function parseZaino(text As String) As Dictionary(Of Item, Integer)
        Dim quantità As New Dictionary(Of Item, Integer)
        Dim rex As New Regex("\> ([A-z òàèéìù'-]+)\(([0-9]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        For Each match As Match In matches
            Try
                quantità.Add(ItemIds.Item(getItemId(match.Groups(1).Value)), (Integer.Parse(match.Groups(2).Value)))
            Catch e As Exception
                Continue For
            End Try
        Next
        Return quantità
    End Function

    'Converto oggetto craft a oggetto normale
    Public Function RowToItem(row As Row) As Item
        Dim it As New Item
        ItemIds.TryGetValue(row.id, it)
        Return it
    End Function

    'invio report di errore allo sviluppatore
    Sub sendReport(ex As Exception, message As Message)
        Dim reportBuilder As New Text.StringBuilder
        Dim hasZaino As String = If(IO.File.Exists("zaini/" + message.From.Id.ToString + ".txt"), "Si", "No")

        With reportBuilder
            .AppendLine("Comando: " + message.Text)
            .AppendLine("Da: " + message.From.Username)
            .AppendLine("ID: " + message.From.Id.ToString)
            .AppendLine("Chat: " + [Enum].GetName(GetType(ChatType), message.Chat.Type))
            .AppendLine("Zaino salvato: " + hasZaino)
            .AppendLine("Metodo: " + If(ex.TargetSite.Name, "Sconosciuto") + ", " + If(ex.Source, "Sconosciuta"))
            .AppendLine("Eccezione: " + ex.Message)
        End With
        Dim a = api.SendTextMessageAsync(1265775, reportBuilder.ToString,, True).Result
    End Sub
    'invio report di errore allo sviluppatore
    Sub sendReport(ex As Exception, query As InlineQuery)
        Dim reportBuilder As New Text.StringBuilder
        Dim hasZaino As String = If(IO.File.Exists("zaini/" + query.From.Id.ToString + ".txt"), "Si", "No")
        With reportBuilder
            .AppendLine("Testo query: " + query.Query)
            .AppendLine("Da: " + query.From.Username)
            .AppendLine("ID: " + query.From.Id.ToString)
            .AppendLine("Zaino salvato: " + hasZaino)
            .AppendLine("Metodo: " + If(ex.TargetSite.Name, "Sconosciuto") + ", " + If(ex.Source, "Sconosciuta"))
            .AppendLine("Eccezione: " + ex.Message)
        End With
        Dim a = api.SendTextMessageAsync(1265775, reportBuilder.ToString,, True).Result
    End Sub
End Module
