Imports Telegram.Bot
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.Enums
Imports Newtonsoft
Imports System.Net
Imports System.Text.RegularExpressions

Module Module1
    Dim WithEvents api As TelegramBotClient

    Dim time_start As Date = Date.UtcNow
    Public ItemIds As New Dictionary(Of Integer, Item)
    Dim items() As Item
    Dim stati As New Dictionary(Of ULong, Integer)
    Dim zaini As New Dictionary(Of ULong, String)
    Dim confronti As New Dictionary(Of ULong, String)
    Dim thread_inline As New Dictionary(Of Integer, Threading.CancellationTokenSource)
    Dim from_inline_query As New Dictionary(Of ULong, String) 'ID_utente, Query

    'STATI:
    '0: Di default

    '10: Attendo messaggi zaino dopo comando /salvazaino
    '---->Ricevo salva o annulla, torno a 0

    '100: Attendo lista cerca dopo comando /confronta
    '---->Ricevo lista vado a 110
    '110: attendo zaino dopo aver ricevuto lista cerca
    '---->Ricevo zaino, mostro risultato torno a 0

    Sub Main()
        initializeVariables()
        api = New TelegramBotClient(token.token)
        Try
            Dim bot = api.GetMeAsync.Result
            Console.WriteLine(bot.Username & ": " & bot.Id)
        Catch ex As AggregateException
            Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
        Catch ex As Exception
            Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.Message)
        End Try
        Dim t As New Threading.Thread(New Threading.ThreadStart(AddressOf Database.initialize_Dictionary))
        t.Start()
        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start()
        Dim stats_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf salvaStats))
        stats_thread.Start()
    End Sub

    'Sub aggiorno_dictionary()
    '    Console.WriteLine("Aggiorno database")
    '    Dim handler As New Http.HttpClientHandler
    '    If handler.SupportsAutomaticDecompression() Then
    '        handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
    '    End If
    '    Dim client As New Http.HttpClient(handler)
    '    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
    '    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
    '    Dim res
    '    Dim jsonres
    '    'aggiungo rifugi
    '    res = getRifugiItemsJSON()
    '    jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
    '    ItemIds.Clear()
    '    items = {}
    '    Dim rif() As Item = jsonres.res
    '    For Each it As Item In rif
    '        ItemIds.Add(it.id, it)
    '        items.Add(it)
    '    Next

    '    res = client.GetStringAsync(ITEM_URL).Result
    '    jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
    '    Dim res_items = jsonres.res
    '    For Each it As Item In res_items
    '        ItemIds.Add(it.id, it)
    '        items.Add(it)
    '    Next
    '    Console.WriteLine("Numero di oggetti: " + items.Length.ToString)
    '    Console.WriteLine("Terminato aggiornamento")
    'End Sub

    'Sub initialize_Dictionary()
    '    While True
    '        Try
    '            aggiorno_dictionary()
    '            Threading.Thread.Sleep(update_db_timeout * 60 * 60 * 1000) 'aggiorno ogni 12 ore
    '        Catch e As AggregateException
    '            Console.WriteLine("Errori durante l'aggiornamento: " + e.InnerException.Message)
    '            Threading.Thread.Sleep(60 * 1000) 'Aggiorno ogni minuto         
    '        Catch e As Exception
    '            Console.WriteLine("Errori durante l'aggiornamento: " + e.Message)
    '            Threading.Thread.Sleep(60 * 1000) 'Aggiorno ogni minuto
    '        End Try
    '    End While
    'End Sub

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
                            Dim ct As New Threading.CancellationTokenSource
                            Dim t As New Task(Of Boolean)(Function() process_query(up.InlineQuery, ct.Token), ct.Token)
                            Dim from_id = up.InlineQuery.From.Id
                            If thread_inline.ContainsKey(from_id) Then
                                thread_inline.Item(from_id).Cancel()
                                thread_inline.Remove(from_id)
                            End If
                            t.Start()
                            thread_inline.Add(from_id, ct)
                        Case UpdateType.ChosenInlineResultUpdate
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_ChosenQuery))
                            t.Start(up.ChosenInlineResult)
                        Case UpdateType.CallbackQueryUpdate
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_callbackData))
                            t.Start(up.CallbackQuery)
                    End Select
                    offset = up.Id + 1
                Next
            Catch ex As AggregateException
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
            End Try
        End While
    End Sub

#Region "callbackdata"
    Sub process_callbackData(callback As CallbackQuery)
        Try
            Dim result As String = process_help(callback.Data)
            Dim e = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, result, ParseMode.Markdown,, creaHelpKeyboard()).Result
            Dim a = api.AnswerCallbackQueryAsync(callback.Id,,,, 0).Result
        Catch e As AggregateException
            Console.WriteLine(e.InnerException.Message)
        Catch e As Exception
            Console.WriteLine(e.Message)
        Finally
            Dim a = api.AnswerCallbackQueryAsync(callback.Id,,,, 0).Result
        End Try
    End Sub
#End Region

#Region "Inline Query"

    Function process_query(InlineQuery As InlineQuery, ct As Threading.CancellationToken) As Boolean
        StampaDebug(String.Format("{0} {1} From: {2}-{3} ID:{4} TEXT: {5}", Now.ToShortDateString, Now.ToShortTimeString, InlineQuery.From.Id, InlineQuery.From.FirstName, InlineQuery.Id, InlineQuery.Query))
        Dim query_text As String = InlineQuery.Query.Trim.ToLower
        Dim path As String = "zaini/" + InlineQuery.From.Id.ToString + ".txt"
        Dim hasZaino As Boolean = IO.File.Exists(path)
        Dim results As New List(Of InlineQueryResults.InlineQueryResult)

        Try
            If hasZaino Then
                'lo zaino è salvato, procedo ad elaborare
                If query_text.Length < 4 Then
                    api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Return True
                End If
                Dim matching_items() As Item = requestItems(query_text)
                If matching_items Is Nothing OrElse matching_items.Length = 0 Then
                    api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Return True
                End If
                Dim limit As Integer = If(matching_items.Length > 50, 50, matching_items.Length - 1)
                Dim res() As InlineQueryResults.InlineQueryResultArticle
                Dim message_text As String = ""
                Dim Tasks(limit) As Task(Of KeyValuePair(Of String, Integer))
                For i = 0 To limit
                    Dim it As Item = matching_items(i)
                    If Not isCraftable(it.id) Then Continue For
                    Tasks(i) = Task.Factory.StartNew(Function() As KeyValuePair(Of String, Integer)
                                                         Return task_getMessageText(it, InlineQuery.From.Id, ct)
                                                     End Function)
                Next
                For i = 0 To limit
                    Dim content As New InputMessageContents.InputTextMessageContent
                    If Tasks(i) Is Nothing Then Continue For
                    Dim result = Tasks(i).Result
                    content.MessageText = result.Key
                    Dim article = New InlineQueryResults.InlineQueryResultArticle
                    article.Id = matching_items(i).id
                    article.InputMessageContent = content
                    article.Title = "Cerco per " + matching_items(i).name
                    article.Description = If(content.MessageText.Contains("Possiedo"), "Hai già tutti gli oggetti.", "Hai bisogno di " + result.Value.ToString + If(result.Value = 1, " oggetto.", " oggetti."))
                    res.Add(article)
                Next
                If res IsNot Nothing Then results.AddRange(res)
                Dim success = api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 5, True,, "Aggiorna zaino salvato", "inline_" + query_text.Replace(" ", "-")).Result
            Else
                'Non ha zaino, propongo di salvarlo
                api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True,, "Cliccami e salva lo zaino per questa funzione", "inline_" + query_text)
            End If
        Catch e As AggregateException
            For Each v In e.InnerExceptions
                If TypeOf v Is TaskCanceledException Then
                    Console.WriteLine("   TaskCanceledException: Task {0}",
                                      DirectCast(v, TaskCanceledException).Task.Id)
                Else
                    Console.WriteLine("   Exception: {0} - {1}", v.GetType().Name, v.Message)
                End If
            Next
        Catch e As Exception
            If TypeOf e Is Threading.ThreadAbortException Then
                Console.WriteLine("   ThreadAbortException")
            Else
                Try
                    Console.WriteLine(e.Message)
                    sendReport(e, InlineQuery)
                Catch
                End Try
            End If
        Finally
            thread_inline.Remove(InlineQuery.From.Id)
        End Try
        thread_inline.Remove(InlineQuery.From.Id)
        Return True
    End Function

    Function task_getMessageText(item As Item, id As Integer, ct As Threading.CancellationToken) As KeyValuePair(Of String, Integer)
        If ct.IsCancellationRequested Then
            StampaDebug("Cancellazione richiesta per " + Threading.Thread.CurrentThread.ManagedThreadId.ToString)
            Try
                Threading.Thread.CurrentThread.Abort()
                ct.ThrowIfCancellationRequested()
            Catch
                Return New KeyValuePair(Of String, Integer)("", 0)
            End Try
        End If
        StampaDebug(String.Format("Starting {1} in '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId, item.name))
        Dim CraftList As New List(Of Item)
        Dim zainoDic_copy As New Dictionary(Of Item, Integer)
        Dim gia_possiedi As New Dictionary(Of Item, Integer)
        Dim spesa As Integer = 0
        Dim zainoDic As New Dictionary(Of Item, Integer)
        Dim it As New Item
        Dim zaino As String = ""
        zaino = IO.File.ReadAllText("zaini/" + id.ToString + ".txt")
        zainoDic = parseZaino(zaino)
        zainoDic_copy = zainoDic

        task_getNeededItemsList(item.id, CraftList, zainoDic_copy, gia_possiedi, ct)
        If CraftList.Count <> 0 Then
            StampaDebug(String.Format("returning from '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
            Return getCercoText(createCraftCountList(CraftList), zainoDic)
        End If
        StampaDebug(String.Format("Terminating '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
        Return New KeyValuePair(Of String, Integer)("", 0)
    End Function

    Sub process_ChosenQuery(ChosenResult As ChosenInlineResult)
        aggiornastats("inline")
    End Sub

    'Creo testo con lista
    Function getCercoText(dic As Dictionary(Of Item, Integer), zaino As Dictionary(Of Item, Integer)) As KeyValuePair(Of String, Integer)
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
        Dim num_richiesti As Integer
        Dim numero_righe As Integer = 1
        Dim first_10_richiesti As Integer
        For Each it In sortedDictionary
            tot_necessari = sortedDictionary.Item(it.Key)
            With buildernecessari
                necessari = If(zaino.ContainsKey(it.Key), tot_necessari - zaino.Item(it.Key), tot_necessari)
                If necessari > 0 Then
                    If numero_righe < inline_message_row_limit Then
                        numero_righe += 1
                        first_10_richiesti += necessari
                    End If
                    num_richiesti += necessari
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
        Dim result As String = ""
        Dim rows() As String = buildernecessari.ToString.Split(Environment.NewLine)
        If rows.Length > inline_message_row_limit Then
            Dim restanti As Integer = num_richiesti - first_10_richiesti
            For Each row In rows.Take(inline_message_row_limit).ToArray
                result &= row.Replace(vbCrLf, "").Replace(vbLf, "") & vbCrLf
            Next
            result &= If(restanti = 1, "E un altro oggetto.", "E altri " + restanti.ToString + " oggetti.")
        Else
            result = buildernecessari.ToString
        End If
        Return New KeyValuePair(Of String, Integer)(result, num_richiesti)
    End Function

    Sub task_getNeededItemsList(id As Integer, ByRef CraftList As List(Of Item), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef ct As Threading.CancellationToken)
        If ct.IsCancellationRequested Then
            StampaDebug("Cancellazione richiesta per " + Threading.Thread.CurrentThread.ManagedThreadId.ToString)
            Try
                Threading.Thread.CurrentThread.Abort()
                ct.ThrowIfCancellationRequested()
            Catch
                Exit Sub
            End Try
        End If
        Dim rows As Integer() = requestCraft(id)
        'StampaDebug("Craft oggetto: " + ItemIds.Item(id).name)
        If rows Is Nothing Then Exit Sub
        Dim item As Item
        For Each row In rows
            item = ItemIds(row)
            If isCraftable(item.id) Then
                If Not zaino.ContainsKey(item) Then
                    task_getNeededItemsList(item.id, CraftList, zaino, possiedi, ct)
                Else
                    zaino.Item(item) -= 1
                    If zaino.Item(item) = 0 Then zaino.Remove(item)
                    If Not possiedi.ContainsKey(item) Then
                        possiedi.Add(item, 1)
                    Else
                        possiedi.Item(item) += 1
                    End If
                End If
            Else
                CraftList.Add(item)
            End If
        Next
    End Sub

#End Region

#Region "Text Message"

    Private Sub process_Message(message As Message)
        Try
            If flush Then 'controllo flush, se attivo ignoro il messaggio
                If message.Date < time_start Then Exit Sub
            End If
            If message.Type <> MessageType.TextMessage And message.Type <> MessageType.DocumentMessage Then Exit Sub
            Dim a As Message
            Dim CraftList As New List(Of Item)
            Dim zainoDic As New Dictionary(Of Item, Integer)
            Dim CraftCount As New Dictionary(Of Item, Integer)
            Dim CraftTree As New List(Of KeyValuePair(Of Item, Integer))
            Dim gia_possiedi As New Dictionary(Of Item, Integer)
            Dim item As String
            Dim id As Integer
            Dim spesa As Integer
            Dim it As New Item
            'Dim lootbot_id As ULong = 171514820
            Dim kill As Boolean = False

            If message.Type = MessageType.DocumentMessage Then
                'è un documento prezzi, lo scarico e lo salvo
                If message.Document.MimeType <> "text/plain" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Formato file non consentito, invia solamente file di testo.").Result
                    Exit Sub
                End If
                Dim f As File = api.GetFileAsync(message.Document.FileId).Result
                Dim c As New Http.HttpClient
                Dim text = c.GetStringAsync(String.Format("https://api.telegram.org/file/bot{0}/{1}", token.token, f.FilePath)).Result
                If isPrezziNegozi(text) Then
                    IO.File.WriteAllText("prezzi/" + message.From.Id.ToString + ".txt", text)
                    Console.WriteLine("Salvati prezzi di ID: " + message.From.Id.ToString)
                    a = api.SendTextMessageAsync(message.Chat.Id, "I tuoi prezzi sono stati salvati!").Result
                    Exit Sub
                Else
                    Dim builder As New Text.StringBuilder("Non sono stati riconosciuti prezzi all'interno del file.")
                    builder.AppendLine().AppendLine("Utilizza il formato:")
                    builder.AppendLine("Rame:400").AppendLine("Sabbia:400").AppendLine("Vetro:400")
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
                    Exit Sub
                End If
            End If
            If isZaino(message.Text) Then
                IO.Directory.CreateDirectory("zaini")
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) Then
                    'sta inviando più parti di zaino
                    zaini.Item(message.From.Id) += message.Text
                    StampaDebug("Zaino diviso ricevuto.")
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                    'sta inviando zaino per confronto
                    Dim zaino = parseZaino(message.Text)
                    Dim cercotext = parseCerca(confronti.Item(message.From.Id))
                    Dim result = ConfrontaDizionariItem(zaino, cercotext)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(cercotext, result),,,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                Else
                    'sta inviando un singolo messaggio
                    Try
                        IO.File.WriteAllText("zaini/" + message.From.Id.ToString + ".txt", message.Text)
                        Console.WriteLine("Salvato zaino di ID: " + message.From.Id.ToString)
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!").Result
                    Catch e As IO.IOException
                        If e.Message.ToLower.StartsWith("sharing violation") Then
                            a = api.SendTextMessageAsync(message.Chat.Id, "Per inviare lo zaino diviso in più messaggi devi utilizzare il comando '/salvazaino'.").Result
                        End If
                    End Try
                End If
            ElseIf isInlineCerco(message.Text) Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) Then
                    confronti.Item(message.From.Id) = message.Text
                    stati.Item(message.From.Id) = 110
                    a = api.SendTextMessageAsync(message.Chat.Id, "Invia ora lo zaino nel quale cercare gli oggetti che stai cercando." + vbCrLf + "Se ne hai uno salvato, puoi toccare 'Utilizza il mio zaino' per utilizzarlo.",,,, creaConfrontaKeyboard(True)).Result
                End If
            ElseIf isPrezziNegozi(message.Text) Then
                IO.Directory.CreateDirectory("prezzi")
                IO.File.WriteAllText("prezzi/" + message.From.Id.ToString + ".txt", message.Text)
                Console.WriteLine("Salvati prezzi di ID: " + message.From.Id.ToString)
                a = api.SendTextMessageAsync(message.Chat.Id, "I tuoi prezzi sono stati salvati!").Result
            ElseIf message.Text.ToLower.Trim.Equals("utilizza il mio zaino") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                    Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                    Dim zaino As String = ""
                    If IO.File.Exists(path) Then
                        zaino = IO.File.ReadAllText(path)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Per utilizzare il tuo zaino, devi averne uno salvato." + vbCrLf + "Inoltra il tuo zaino di seguito.").Result
                        Exit Sub
                    End If
                    zainoDic = parseZaino(zaino)
                    Dim cercotext = parseCerca(confronti.Item(message.From.Id))
                    Dim result = ConfrontaDizionariItem(zainoDic, cercotext)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(cercotext, result),,,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai effettuando il confronto.",,,, creaNULLKeyboard).Result
                End If
            Else
                Console.WriteLine("{0} {1} {2} from: {3}", Now.ToShortDateString, Now.ToShortTimeString, message.Text, message.From.Username)
            End If
            If message.Text.ToLower.StartsWith("/salvazaino") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Stai già salvando lo zaino, inoltralo di seguito.").Result
                    Exit Sub
                End If
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Stai già usando /confronta, annulla prima di eseguire /salvazaino.").Result
                    Exit Sub
                End If
                stati.Add(message.From.Id, 10) 'entro nello stato 10, ovvero salvataggio zaino
                zaini.Add(message.From.Id, "")
                If message.Chat.Type = ChatType.Group Or message.Chat.Type = ChatType.Supergroup Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inoltra i messaggi in privato").Result
                End If
                a = api.SendTextMessageAsync(message.From.Id, "Inoltra di seguito il tuo zaino diviso in più messaggi. " + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,, creaZainoKeyboard).Result
            ElseIf message.Text.ToLower.Equals("salva") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then
                    IO.File.WriteAllText("zaini/" + message.From.Id.ToString + ".txt", zaini.Item(message.From.Id))
                    stati.Remove(message.From.Id)
                    zaini.Remove(message.From.Id)
                    If from_inline_query.ContainsKey(message.From.Id) Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!",,,, creaInlineKeyboard(from_inline_query.Item(message.From.Id))).Result
                        from_inline_query.Remove(message.From.Id)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!",,,, creaNULLKeyboard).Result
                    End If
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai salvando uno zaino, utilizza /salvazaino per iniziare il salvataggio.",,,, creaNULLKeyboard).Result
                End If
            ElseIf message.Text.ToLower.Equals("annulla") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then
                    stati.Remove(message.From.Id)
                    zaini.Remove(message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai annullato il salvataggio dello zaino",,,, creaNULLKeyboard).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) Or stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai annullato il confronto.",,,, creaNULLKeyboard).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non hai azioni in sospeso.",,,, creaNULLKeyboard).Result
                End If
            ElseIf message.Text.ToLower.StartsWith("/base") Then
#Region "Base"
                Dim rarity = message.Text.Replace("/base", "").Trim
                If rarity = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci la rarità.").Result
                    Exit Sub
                End If
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                zainoDic = parseZaino(zaino)
                a = api.SendTextMessageAsync(message.Chat.Id, getBaseText(rarity, zainoDic)).Result
#End Region
            ElseIf message.Text.ToLower.Trim.Equals("/confronta") Then
#Region "confronta"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) _
                    Or stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai già usato '/confronta'.").Result
                    Exit Sub
                End If
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Stai già usando /salvazaino, salva o annulla prima di eseguire il confronto.").Result
                    Exit Sub
                End If
                'entro nello stato di confronto
                stati.Add(message.From.Id, 100)
                a = api.SendTextMessageAsync(message.From.Id, "Invia l'elenco 'Cerco:' generato dal comando inline del bot.",,,, creaConfrontaKeyboard).Result
#End Region
            ElseIf message.Text.ToLower.StartsWith("/lista") Then
#Region "lista"
                Dim item_ids = checkInputItems(message.Text.ToLower.Trim, message.Chat.Id, "/lista")
                If item_ids.Count = 0 Then Exit Sub
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                zainoDic = parseZaino(zaino)
                Dim zainoDic_copy = zainoDic
                For Each i In item_ids
                    getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                Next
                Dim result As String = getCraftListText(createCraftCountList(CraftList), item_ids.ToArray, zainoDic, gia_possiedi, spesa)
                answerLongMessage(result, message.Chat.Id)
#End Region
            ElseIf message.Text.ToLower.StartsWith("/albero") Then
#Region "albero"
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
                    If Not isCraftable(id) Then
                        Dim e = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è craftabile").Result
                        Exit Sub
                    End If
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                    zainoDic = parseZaino(zaino)
                    getNeededItemsTree(id, prof, CraftTree, zainoDic, gia_possiedi, spesa)
                    If rarity_value.ContainsKey(ItemIds.Item(id).rarity) Then spesa += rarity_value.Item(ItemIds.Item(id).rarity)
                    Dim name As String = getFileName()
                    a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, getCraftTreeText(CraftTree, gia_possiedi, spesa), item),,, message.MessageId).Result
                    IO.File.Delete(name)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/craft") Then
#Region "craft"
                Dim item_ids = checkInputItems(message.Text.ToLower.Trim, message.Chat.Id, "/craft")
                If item_ids.Count = 0 Then Exit Sub
                Dim prof As Integer = -1
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                zainoDic = parseZaino(zaino)
                Dim zainoDic_copy = zainoDic
                For Each i In item_ids
                    prof = -1
                    getCraftItemsTree(i, prof, CraftTree, zainoDic_copy, gia_possiedi)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                Next
                Dim name As String = getFileName()
                a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, getcraftText(CraftTree, gia_possiedi), "Lista Craft"),,, message.MessageId).Result
                IO.File.Delete(name)
#End Region
            ElseIf message.Text.ToLower.StartsWith("/vendi") Then
#Region "vendi"
                Dim item_ids = checkInputItems(message.Text.ToLower.Trim, message.Chat.Id, "/vendi")
                If item_ids.Count = 0 Then Exit Sub
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Per utilizzare questa funzione devi prima salvare il tuo zaino.").Result
                    Exit Sub
                End If
                api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                zainoDic = parseZaino(zaino)
                Dim zainoDic_copy = zainoDic
                For Each i In item_ids
                    getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa)
                Next
                Dim res = SottrazioneDizionariItem(zainoDic_copy, createCraftCountList(CraftList))
                Dim result As String = getVendiText(res, zainoDic, item_ids.ToArray)
                answerLongMessage(result, message.Chat.Id)
#End Region
            ElseIf message.Text.ToLower.StartsWith("/svuota") Then
#Region "Svuota"
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                If IO.File.Exists(path) Then
                    IO.File.Delete(path)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Lo zaino è stato svuotato.").Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino non è salvato").Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/cancellaprezzi") Then
#Region "CancellaPrezzi"
                Dim path As String = "prezzi/" + message.From.Id.ToString + ".txt"
                If IO.File.Exists(path) Then
                    IO.File.Delete(path)
                    a = api.SendTextMessageAsync(message.Chat.Id, "I prezzi sono stati cancellati.").Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non hai prezzi salvati al momento.").Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/rinascita") Then
#Region "rinascita"
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
#End Region
            ElseIf message.Text.ToLower.StartsWith("/creanegozi") Then
#Region "creanegozi"
                Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                Dim zaino As String = ""
                Dim result As New Dictionary(Of Item, Integer)
                If IO.File.Exists(path) Then
                    zaino = IO.File.ReadAllText(path)
                End If
                If zaino = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Devi salvare lo zaino prima di usare questa funzione.").Result
                    Exit Sub
                End If
                zainoDic = parseZaino(zaino)
                Dim item_ids = checkInputItems(message.Text.Trim.ToLower, message.Chat.Id, "/creanegozi")
                If item_ids.Count = 0 Then
                    'Uso lo zaino per creare i negozi
                    result = zainoDic
                Else
                    'Uso il /vendi per creare i negozi
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                    Dim zainoDic_copy = zainoDic
                    For Each i In item_ids
                        getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa)
                    Next
                    result = SottrazioneDizionariItem(zainoDic_copy, createCraftCountList(CraftList))
                End If
                Dim prezzi_dic As Dictionary(Of Item, Integer)
                Dim prezzi As String
                If IO.File.Exists("prezzi/" + message.From.Id.ToString + ".txt") Then
                    prezzi = IO.File.ReadAllText("prezzi/" + message.From.Id.ToString + ".txt")
                    prezzi_dic = parsePrezzoNegozi(prezzi)
                End If
                Dim negozi = getNegoziText(result, prezzi_dic)
                For Each negozio In negozi
                    a = api.SendTextMessageAsync(message.Chat.Id, negozio).Result
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, "/privacy tutti").Result
#End Region
            ElseIf message.Text.ToLower.StartsWith("/ottieniprezzi") Then
#Region "ottieniprezzi"
                If IO.File.Exists("prezzi/" + message.From.Id.ToString + ".txt") Then
                    Dim prezzi_text = IO.File.ReadAllText("prezzi/" + message.From.Id.ToString + ".txt")
                    If prezzi_text.Length > 4096 Then
                        'invio file
                        Dim name As String = getFileName()
                        a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, prezzi_text, "Prezzi")).Result
                        IO.File.Delete(name)
                    Else
                        answerLongMessage(prezzi_text, message.Chat.Id)
                    End If
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non hai prezzi salvati al momento").Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/start") Then
#Region "start"
                If message.Text.Contains("inline_") Then
                    If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) _
                            Or stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                        stati.Remove(message.From.Id)  'entro nello stato 10, ovvero salvataggio zaino
                        confronti.Remove(message.From.Id)
                    End If
                    'Proviene da inline propongo zaino
                    If Not stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) Then
                        stati.Add(message.From.Id, 10) 'entro nello stato 10, ovvero salvataggio zaino
                        zaini.Add(message.From.Id, "")
                    End If
                    If Not from_inline_query.ContainsKey(message.From.Id) Then
                        from_inline_query.Add(message.From.Id, message.Text.ToLower.Trim.Replace("/start inline_", "").Replace("-", " "))
                    Else
                        from_inline_query(message.From.Id) = message.Text.ToLower.Trim.Replace("/start inline_", "").Replace("-", " ")
                    End If
                    a = api.SendTextMessageAsync(message.From.Id, "Inoltra o incolla di seguito il tuo zaino, può essere in più messaggi." + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,, creaZainoKeyboard).Result
                Else
                    'Avvio normale
                    Dim builder As New Text.StringBuilder()
                    builder.AppendLine("Benvenuto in Craft Lootbot!")
                    builder.AppendLine("Questo bot permette di riceve la lista dei materiali necessari al craft, l'albero dei craft di un oggetto e molto altro.")
                    builder.AppendLine("Usa /help per la guida ai comandi.")
                    builder.AppendLine("Segui il canale @CraftLootBotNews per le news e aggiornamenti, contatta @AlexCortinovis per malfunzionamenti o dubbi")
                    builder.AppendLine("Provami! Non ti deluderò!")
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/help") Then
                a = api.SendTextMessageAsync(message.Chat.Id, help_builder.ToString,,,, creaHelpKeyboard, ParseMode.Markdown).Result
            ElseIf message.Text.ToLower.StartsWith("/stats") Then
#Region "stats"
                Dim builder As New Text.StringBuilder("*STATISTICHE COMPLESSIVE:*")
                builder.AppendLine().AppendLine()
                stats.Item("zaini") = New Tuple(Of String, ULong)(stats.Item("zaini").Item1, IO.Directory.GetFiles("zaini/").Length)
                For Each stat In stats
                    builder.Append("*" + stat.Value.Item1 + ":* " + stat.Value.Item2.ToString).Append(vbCrLf)
                Next
                builder.AppendLine().AppendLine("*Negli ultimi " + stat_timeout.ToString + " minuti:*")
                For Each delta_stat In delta_stats
                    builder.Append("*" + delta_stat.Value.Item1 + ":* " + delta_stat.Value.Item2.ToString).Append(vbCrLf)
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString,,,,, ParseMode.Markdown).Result
#End Region
            ElseIf message.Text.ToLower.StartsWith("/db") Then
                Dim builder As New Text.StringBuilder
                builder.AppendLine(download_items())
                builder.AppendLine(download_crafts())
                builder.AppendLine(Leggo_Items())
                builder.AppendLine(Leggo_Crafts())
                a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
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
            If TypeOf e Is KeyNotFoundException Then
                Try
                    download_items()
                    download_crafts()
                    Leggo_Items()
                    Leggo_Crafts()
                Catch
                    Console.WriteLine("Impossibile aggiornare dizionario")
                    Exit Sub
                End Try
            End If
            Try
                Console.WriteLine(e.Message)
                Dim a
                sendReport(e, message)
                a = api.SendTextMessageAsync(message.Chat.Id, "Si è verificato un errore, riprova tra qualche istante." + vbCrLf + "Una segnalazione è stata inviata automaticamente allo sviluppatore, potrebbe contattarti per avere più informazioni.",,,, creaNULLKeyboard).Result
            Catch
            End Try
        End Try
    End Sub

    Sub getNeededItemsList(id As Integer, ByRef CraftList As List(Of Item), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer)
        Dim rows As Integer() = requestCraft(id)
        If rows Is Nothing Then Exit Sub
        Dim item As Item
        For Each ids In rows
            item = ItemIds(ids)
            If isCraftable(item.id) Then
                If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)
                StampaDebug(String.Format("Oggetto: {0}, +{1}={2}", item.name, If(rarity_value.ContainsKey(item.rarity), rarity_value.Item(item.rarity).ToString, "0"), spesa.ToString))
                If Not zaino.ContainsKey(item) Then
                    getNeededItemsList(item.id, CraftList, zaino, possiedi, spesa)
                Else
                    zaino.Item(item) -= 1
                    If zaino.Item(item) = 0 Then zaino.Remove(item)
                    If Not possiedi.ContainsKey(item) Then
                        possiedi.Add(item, 1)
                    Else
                        possiedi.Item(item) += 1
                    End If
                End If
            Else
                CraftList.Add(item)
            End If
        Next
    End Sub

    Sub getNeededItemsTree(id As Integer, ByRef prof As Integer, ByRef CraftTree As List(Of KeyValuePair(Of Item, Integer)), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer)
        Dim rows As Integer() = requestCraft(id)
        prof += 1
        If rows Is Nothing Then Exit Sub
        CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(id), prof))
        Dim item As Item
        For Each row In rows
            item = ItemIds(row)
            If Not possiedi.ContainsKey(item) And zaino.ContainsKey(item) Then possiedi.Add(item, zaino.Item(item))
            If isCraftable(item.id) Then
                Dim prev_spesa = spesa
                If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)
                getNeededItemsTree(item.id, prof, CraftTree, zaino, possiedi, spesa)
                prof -= 1
            Else
                CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(item.id), prof + 1))
            End If
        Next
    End Sub

    Sub getCraftItemsTree(id As Integer, ByRef prof As Integer, ByRef CraftTree As List(Of KeyValuePair(Of Item, Integer)), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer))
        Dim rows As Integer() = requestCraft(id)
        prof += 1
        If rows Is Nothing Then Exit Sub
        CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(id), prof))
        Dim item As Item
        For Each row In rows
            item = ItemIds(row)
            'If Not possiedi.ContainsKey(item) And zaino.ContainsKey(item) Then possiedi.Add(item, zaino.Item(item))
            If isCraftable(item.id) Then
                If Not zaino.ContainsKey(item) Then
                    getCraftItemsTree(item.id, prof, CraftTree, zaino, possiedi)
                    prof -= 1
                Else
                    StampaDebug(item.name + " presente nello zaino")
                    zaino.Item(item) -= 1
                    If zaino.Item(item) = 0 Then zaino.Remove(item)
                    If Not possiedi.ContainsKey(item) Then
                        possiedi.Add(item, 1)
                    Else
                        possiedi.Item(item) += 1
                    End If
                End If
            Else
                CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(item.id), prof + 1))
            End If
        Next
    End Sub

    'Creazione testi
    Function creaScambi(zaino As Dictionary(Of Item, Integer), oggetto_scambio As String, user As String) As String
        Dim builder As New Text.StringBuilder
        Dim sorted = From pair In zaino
                     Order By pair.Value Descending
        Dim comparer As New Item.ZainoComparer

        Dim zainolist = zaino.OrderByDescending(Function(p) p.Value).ThenByDescending(Function(p) p, comparer).ToList 'sorted.ToList
        Dim prev_count
        Dim totbuilder As New Text.StringBuilder
        Dim item, next_item
        Dim diff = 0
        For x = zainolist.Count - 1 To 1 Step -1
            x = zainolist.Count - 1 - diff
            diff = 0
            'If zainolist(x).Value <> zainolist(x - 1).Value Then diff = 1
            item = zainolist(x - diff)
            next_item = zainolist(x - 1 - diff)

            For i = 1 To If(item.Value > next_item.Value, next_item.Value, item.Value)
                prev_count = zainolist.Count
                builder = New Text.StringBuilder
                With builder
                    .Append("Scambia ")
                    .Append(item.Key.name).Append(",")
                    .Append(next_item.Key.name).Append(",")
                    .Append(oggetto_scambio).Append(",")
                    .Append(oggetto_scambio).Append(",,,")
                    .Append(user).Append(",")
                    .Append(5)
                    .Append(vbCrLf)
                End With
                totbuilder.Append(builder.ToString)
                zainolist(x - diff) = New KeyValuePair(Of Item, Integer)(item.Key, zainolist(x - diff).Value - 1)
                zainolist(x - 1 - diff) = New KeyValuePair(Of Item, Integer)(next_item.Key, zainolist(x - 1 - diff).Value - 1)
                zainolist.RemoveAll(New Predicate(Of KeyValuePair(Of Item, Integer))(Function(a) a.Value = 0))
                'If zainolist.Count < prev_count Then zainolist = zainolist.OrderByDescending(Of Integer)(Function(p) p.Value).ToList '.ThenByDescending(Of KeyValuePair(Of Item, Integer))(Function(p) p, comparer).ToList
            Next
        Next
        Return totbuilder.ToString
    End Function

    Function getBilanciaText(dic As Dictionary(Of Item, Integer)) As String
        Dim a = From pair In dic
                Order By pair.Value Descending
                Group By pair.Key.rarity Into Rarita = Group, Count()
        Dim list = a.ToList
        Dim builder As New Text.StringBuilder
        For Each rar In list
            builder.Append(rar.rarity).Append(" (" + rar.Count.ToString + ")").AppendLine(":")

            For Each It In rar.Rarita
                builder.Append("   > ").Append(It.Value).Append(" ").Append(It.Key.name).AppendLine()
            Next
        Next
        Return builder.ToString
    End Function

    Function getCraftListText(dic As Dictionary(Of Item, Integer), oggetti() As Integer, zaino As Dictionary(Of Item, Integer), ByRef gia_possiedi As Dictionary(Of Item, Integer), spesa As Integer) As String

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
        Dim ogg_string()
        For Each i In oggetti
            ogg_string.Add(ItemIds(i).name)
        Next
        Dim intestazione As String = "Lista oggetti necessari per " + String.Join(", ", ogg_string) + ": "
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

    Function getcraftText(list As List(Of KeyValuePair(Of Item, Integer)), ByRef possiedi As Dictionary(Of Item, Integer)) As String
        Dim builder As New Text.StringBuilder("Lista craft per:  " + list(0).Key.name)
        builder.Append(vbCrLf)
        Dim sorted = From pair In list
                     Order By pair.Value Descending
        Dim sortedDictionary = sorted.ToList()
        For Each craft In sortedDictionary
            If isCraftable(craft.Key.id) Then
                'If Not possiedi.ContainsKey(craft.Key) Then
                builder.Append("Crea " + craft.Key.name)
                    builder.Append(vbCrLf)
                    'Else
                    '    possiedi(craft.Key) -= 1
                    '    If possiedi(craft.Key) = 0 Then possiedi.Remove(craft.Key)
                    'End If
                End If
        Next
        Return builder.ToString
    End Function

    Function getConfrontoText(cerco As Dictionary(Of Item, Integer), presenti As Dictionary(Of Item, Integer)) As String
        Dim builder As New Text.StringBuilder()
        Dim intestazione As String = "Nello zaino sono presenti questi oggetti che cerchi:"
        builder.AppendLine(intestazione)

        For Each row In presenti
            With builder
                .Append("> ")
                .Append(row.Value)
                .Append(" su ")
                .Append(cerco.Item(row.Key))
                .Append(" di ")
                .Append(row.Key.name)
                .Append(" (" + row.Key.rarity + ")") 'Rarità
                .AppendLine()
            End With
        Next
        If builder.ToString = intestazione + Environment.NewLine Then builder.Clear.AppendLine("Non sono presenti oggetti che cerchi nello zaino.")
        Return builder.ToString
    End Function

    Function getBaseText(rarity As String, zaino As Dictionary(Of Item, Integer)) As String
        Dim builder As New Text.StringBuilder
        rarity = rarity.ToUpper
        Dim intestazione As String = String.Format("Ecco gli oggetti base di rarità {0}:", rarity)
        If Not rarity_value.ContainsKey(rarity) Then
            builder.AppendLine("La rarità specificata non è stata riconosciuta.")
        Else
            builder.AppendLine(intestazione)
        End If
        For Each item In ItemIds
            If Not isCraftable(item.Key) Then
                If item.Value.rarity = rarity Then
                    builder.Append("> ")
                    builder.Append(item.Value.name)
                    If zaino.Count > 0 Then
                        builder.Append(" (")
                        builder.Append(If(zaino.ContainsKey(item.Value), zaino.Item(item.Value), 0))
                        builder.Append(")")
                    End If
                    builder.AppendLine()
                End If
            End If
        Next
        If builder.ToString = intestazione + Environment.NewLine Then builder.AppendLine("Non ci sono oggetti base per questa rarità.")
        Return builder.ToString
    End Function

    Function getVendiText(vendi As Dictionary(Of Item, Integer), zaino As Dictionary(Of Item, Integer), oggetti() As Integer) As String
        Dim builder As New Text.StringBuilder()
        Dim sor As New SortedDictionary(Of Item, Integer)(New Item.ItemComparer)
        Dim ogg_string()
        For Each i In oggetti
            ogg_string.Add(ItemIds(i).name)
        Next
        For Each pair In vendi
            sor.Add(pair.Key, pair.Value)
        Next
        Dim sortedDictionary As Dictionary(Of Item, Integer) = sor.Reverse.ToDictionary(Function(p) p.Key, Function(p) p.Value)
        If sortedDictionary.Count > 0 Then
            builder.AppendLine(String.Format("Ecco la lista degli oggetti non necessari per craftare {0}:", String.Join(", ", ogg_string)))
            For Each it In sortedDictionary
                With builder
                    .Append("> ")
                    .Append(vendi.Item(it.Key))
                    .Append(" su ")
                    .Append(zaino.Item(it.Key))
                    .Append(" di ")
                    .Append(it.Key.name)
                    .Append(" (" + it.Key.rarity + ")") 'Rarità
                    .AppendLine()
                End With
            Next
        Else
            builder.AppendLine("Non puoi vendere alcun oggetto. Ti servono tutti per craftare l'oggetto specificato.")
        End If
        Return builder.ToString
    End Function

    Function getNegoziText(zaino As Dictionary(Of Item, Integer), prezzi As Dictionary(Of Item, Integer)) As List(Of String)
        Dim res As New List(Of String)
        Dim builder As New Text.StringBuilder("/negozio ")
        Dim i_counter = 1
        Dim Filtro_zaino = zaino.Where(Function(p) prezzoScrigni.ContainsKey(p.Key.rarity) AndAlso Not isCraftable(p.Key.id))
        Dim prev_rarity As String = Filtro_zaino.First.Key.rarity
        Dim prezzo = ""
        For Each it In Filtro_zaino
            prezzo = ""
            If prezzi IsNot Nothing AndAlso prezzi.ContainsKey(it.Key) Then
                If prezzi(it.Key) <= 0 Then Continue For
                prezzo = prezzi(it.Key).ToString
            Else
                prezzo = it.Key.value.ToString
            End If
            If it.Key.rarity <> prev_rarity Then
                i_counter = 1
                If builder(builder.Length - 1) = "," Then builder.Remove(builder.Length - 1, 1)
                'builder.Append("#")
                If Not builder.ToString.Trim = "/negozio" Then res.Add(builder.ToString)
                builder.Clear().Append("/negozio ")
                builder.Append(it.Key.name).Append(":")
                builder.Append(prezzo).Append(":")
                builder.Append(it.Value)
                builder.Append(",")
            ElseIf i_counter >= 10 Then
                i_counter = 1
                builder.Append(it.Key.name).Append(":")
                builder.Append(prezzo).Append(":")
                builder.Append(it.Value)
                'builder.Append("#")
                If Not builder.ToString.Trim = "/negozio" Then res.Add(builder.ToString)
                builder.Clear().Append("/negozio ")
            Else
                builder.Append(it.Key.name).Append(":")
                builder.Append(prezzo).Append(":")
                builder.Append(it.Value)
                builder.Append(",")
            End If
            i_counter += 1
            prev_rarity = it.Key.rarity
        Next
        builder.Remove(builder.Length - 1, 1)
        'builder.Append("#")
        If Not builder.ToString.Trim = "/negozio" Then res.Add(builder.ToString)
        Return res
    End Function
#End Region

    'controllo se l'item è craftabile
    Function isCraftable(id As Integer) As Boolean
        Try
            Return If(ItemIds.Item(id).craftable = 0, False, True)
        Catch e As Exception
            Return False
        End Try
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
        Try
            For Each it In ItemIds
                If it.Value.name.ToLower = name.ToLower.Trim Then Return it.Key
            Next
        Catch ex As Exception
            download_crafts()
            download_items()
            Leggo_Crafts()
            Leggo_Items()
            Console.WriteLine("Impossibile ottenere l'ID per " + name + ". Riscaricati Item e craft")
        End Try
        Return -1
    End Function

    'Dato uno zaino in stringa restituisco dizionario(oggetto, quantità)
    Function parseZaino(text As String) As Dictionary(Of Item, Integer)
        Dim quantità As New Dictionary(Of Item, Integer)
        Dim rex As New Regex("\> ([A-z 0-9òàèéìù'-]+)\(([0-9]+)\)")
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

    'dato una elenco generato tramite comando inline restituisco dizionario(oggetto, quantità)
    Function parseCerca(text As String) As Dictionary(Of Item, Integer)
        Dim cercodic As New Dictionary(Of Item, Integer)
        Dim rex As New Regex("\> ([0-9]+) di ([A-z òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        For Each match As Match In matches
            Try
                cercodic.Add(ItemIds.Item(getItemId(match.Groups(2).Value)), (Integer.Parse(match.Groups(1).Value)))
            Catch e As Exception
                Continue For
            End Try
        Next
        Return cercodic
    End Function

    'Date le impostazioni prezzi per i negozi, restituisco un dizionario(Oggetto, prezzo)
    Function parsePrezzoNegozi(text As String) As Dictionary(Of Item, Integer)
        Dim prezzo_dic As New Dictionary(Of Item, Integer)
        Dim rex As New Regex("([A-z 0-9òàèéìù'-]+)\:([0-9]+)")
        Dim matches As MatchCollection = rex.Matches(text)
        For Each match As Match In matches
            Try
                prezzo_dic.Add(ItemIds.Item(getItemId(match.Groups(1).Value)), (Integer.Parse(match.Groups(2).Value)))
            Catch ex As Exception
                StampaDebug("Oggetto " + match.Groups(1).Value + " Saltato!")
                Continue For
            End Try
        Next
        Return prezzo_dic
    End Function

    'Converto oggetto craft a oggetto normale
    'Public Function RowToItem(row As Row) As Item
    '    Dim it As New Item
    '    ItemIds.TryGetValue(row.id, it)
    '    Return it
    'End Function

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

    Function answerLongMessage(result As String, chatID As Long) As Message
        Dim a
        If result.Length > 4096 Then
            Dim valid_substring As String
            Do
                Dim substring = result.Substring(0, If(result.Length > 4096, 4096, result.Length))
                valid_substring = substring.Substring(0, substring.LastIndexOf(Environment.NewLine))
                result = result.Substring(substring.LastIndexOf(Environment.NewLine))
                If valid_substring.Trim <> "" And valid_substring <> Environment.NewLine Then
                    a = api.SendTextMessageAsync(chatID, valid_substring,,,,, ParseMode.Markdown).Result
                Else
                    a = api.SendTextMessageAsync(chatID, result,,,,, ParseMode.Markdown).Result
                    result = ""
                End If
            Loop While result <> "" And result <> Environment.NewLine

        Else
            a = api.SendTextMessageAsync(chatID, result,,,,, ParseMode.Markdown).Result
        End If
        Return a
    End Function

    Function checkInputItems(message_text As String, chat_id As Long, comando As String) As List(Of Integer)

        Dim items = message_text.Replace(If(message_text.Contains("@craftlootbot"), comando + "@craftlootbot", comando), "").Split(",").Where(Function(p) p <> "").ToList
        Dim item_ids As New List(Of Integer)
        Dim a
        If items.Count = 0 Then
            If comando <> "/creanegozi" Then a = api.SendTextMessageAsync(chat_id, "Inserisci l'oggetto o gli oggetti che vuoi ottenere").Result
            Return item_ids
        End If
        For Each i In items
            i = i.Trim
            Dim temp_id = getItemId(i)
            If temp_id = -1 Then
                a = api.SendTextMessageAsync(chat_id, "L'oggetto " + i + " non è stato riconosciuto, verrà saltato.").Result
                Continue For
            End If
            If Not isCraftable(temp_id) Then
                Dim e = api.SendTextMessageAsync(chat_id, "L'oggetto " + i + " non è craftabile, verrà saltato.").Result
                Continue For
            End If
            item_ids.Add(temp_id)
        Next
        If item_ids.Count = 0 Then
            a = api.SendTextMessageAsync(chat_id, "Nessuno degli oggetti inseriti è valido, riprova.").Result
        End If
        Return item_ids
    End Function
End Module
