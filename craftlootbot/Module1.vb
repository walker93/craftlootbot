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
        Dim zaini_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf vecchizaini))
        zaini_thread.Start()
        Dim inlineHistory_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf salva_inlineHistory))
        inlineHistory_thread.Start()
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
            If callback.Data.StartsWith("info_") Then
                Dim i As Integer = Integer.Parse(callback.Data.Replace("info_", ""))
                Dim result As String = ItemIds(i).ToString
                Dim related = ItemIds(i).getRelatedItemsIDs
                Dim e = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, result, ParseMode.Markdown,, If(IsNothing(related), Nothing, creaInfoKeyboard(related))).Result
                Dim a = api.AnswerCallbackQueryAsync(callback.Id,,,, 0).Result
            Else
                Dim result As String = process_help(callback.Data)
                Dim e = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, result, ParseMode.Markdown,, creaHelpKeyboard()).Result
                Dim a = api.AnswerCallbackQueryAsync(callback.Id,,,, 0).Result
            End If
        Catch e As AggregateException
            Console.WriteLine(e.InnerException.Message)
        Catch e As Exception
            Console.WriteLine(e.Message)
        Finally
            'Dim a = api.AnswerCallbackQueryAsync(callback.Id,,,, 0).Result
        End Try
    End Sub
#End Region

#Region "Inline Query"
    Dim filter As String = Nothing
    Function process_query(InlineQuery As InlineQuery, ct As Threading.CancellationToken) As Boolean
        StampaDebug(String.Format("{0} {1} From: {2}-{3} ID:{4} TEXT: {5}", Now.ToShortDateString, Now.ToShortTimeString, InlineQuery.From.Id, InlineQuery.From.FirstName, InlineQuery.Id, InlineQuery.Query))
        Dim unfiltered_query As String = InlineQuery.Query.Trim.ToLower
        Dim query_text As String
        Dim path As String = "zaini/" + InlineQuery.From.Id.ToString + ".txt"
        Dim hasZaino As Boolean = IO.File.Exists(path)
        Dim results As New List(Of InlineQueryResults.InlineQueryResult)
        Dim user_history() As KeyValuePair(Of String, Integer) = getUserHistory(InlineQuery.From.Id)
        Try
            Dim reg As New Regex("^(C|NC|R|UR|L|E|UE|U){1} ", RegexOptions.IgnoreCase)
            If reg.IsMatch(unfiltered_query) Then
                filter = reg.Match(unfiltered_query).Value.Trim
                query_text = unfiltered_query.Substring(filter.Length).Trim
            Else
                filter = Nothing
                query_text = unfiltered_query
            End If

            If hasZaino Then
                'lo zaino è salvato, procedo ad elaborare
                Dim matching_items() As KeyValuePair(Of Item, String)
                If query_text.Length < 4 Then
                    For Each it In user_history.Reverse
                        matching_items.Add(New KeyValuePair(Of Item, String)(ItemIds(it.Value), it.Key))
                    Next
                Else
                    For Each i In requestItems(query_text)
                        matching_items.Add(New KeyValuePair(Of Item, String)(i, filter))
                    Next
                End If
                If matching_items Is Nothing OrElse matching_items.Length = 0 Then
                    api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Return True
                End If

                Dim limit As Integer = If(matching_items.Length > 50, 50, matching_items.Length - 1)
                Dim res() As InlineQueryResults.InlineQueryResultArticle
                Dim message_text As String = ""
                Dim Tasks(limit) As Task(Of KeyValuePair(Of String, Integer))
                For i = 0 To limit
                    Dim pair = matching_items(i)
                    If Not isCraftable(pair.key.id) Then Continue For

                    Tasks(i) = Task.Factory.StartNew(Function() As KeyValuePair(Of String, Integer)
                                                         Return task_getMessageText(pair.Key, InlineQuery.From.Id, ct, pair.Value)
                                                     End Function)
                Next
                For i = 0 To limit
                    Dim content As New InputMessageContents.InputTextMessageContent
                    If Tasks(i) Is Nothing Then Continue For
                    Dim result = Tasks(i).Result
                    content.MessageText = result.Key
                    Dim f = matching_items(i).Value
                    Dim article = New InlineQueryResults.InlineQueryResultArticle
                    Dim costo As Integer = If(rarity_value.ContainsKey(matching_items(i).Key.rarity), rarity_value(matching_items(i).Key.rarity), 0)
                    Dim punti As Integer = If(rarity_craft.ContainsKey(matching_items(i).Key.rarity), rarity_craft(matching_items(i).Key.rarity), 0)
                    Dim costoBase As Integer = 0 'matching_items(i).value
                    article.Id = f + matching_items(i).Key.id.ToString
                    article.InputMessageContent = content
                    article.Title = "Cerco per " + matching_items(i).Key.name
                    matching_items(i).Key.contaCosto(matching_items(i).Key.id, costo, punti, costoBase)

                    article.Description = If(content.MessageText.Contains("Possiedo"), "Hai già tutti gli oggetti " + If(IsNothing(f), "", f).ToUpper, "Hai bisogno di " + result.Value.ToString + If(result.Value = 1, " oggetto ", " oggetti ") + If(IsNothing(f), "", f).ToUpper)
                    article.Description += vbCrLf + "Costo craft: " + prettyCurrency(costo) + ", Punti craft: " + punti.ToString
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

    Private Function getUserHistory(User_id As Integer) As KeyValuePair(Of String, Integer)()
        Dim user_history As New List(Of KeyValuePair(Of String, Integer))
        Dim filter As String = ""
        Dim reg As New Regex("^(C|NC|R|UR|L|E|UE|U){0,1}([0-9]+)", RegexOptions.IgnoreCase)
        If inline_history.ContainsKey(User_id) Then
            For Each t In inline_history(User_id)
                Dim id As Integer
                If reg.IsMatch(t) Then
                    Dim m As Match = reg.Match(t)
                    filter = If(m.Groups(1).Success, m.Groups(1).Value, Nothing)
                    id = m.Groups(2).Value
                    user_history.Add(New KeyValuePair(Of String, Integer)(filter, id))
                End If
            Next
        End If
        Return user_history.ToArray
    End Function

    Function task_getMessageText(item As Item, id As Integer, ct As Threading.CancellationToken, Optional fil As String = Nothing) As KeyValuePair(Of String, Integer)
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
        If IO.File.Exists("zaini/" + id.ToString + ".txt") Then
            zaino = IO.File.ReadAllText("zaini/" + id.ToString + ".txt")
        Else
            Return New KeyValuePair(Of String, Integer)("", -1)
        End If
        zainoDic = parseZaino(zaino)
        zainoDic_copy = zainoDic

        task_getNeededItemsList(item.id, CraftList, zainoDic_copy, gia_possiedi, ct)
        If CraftList.Count <> 0 Then
            StampaDebug(String.Format("returning from '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
            Return getCercoText(createCraftCountList(CraftList), zainoDic, fil)
        End If
        StampaDebug(String.Format("Terminating '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
        Return New KeyValuePair(Of String, Integer)("", 0)
    End Function

    Sub process_ChosenQuery(ChosenResult As ChosenInlineResult)
        aggiornastats("inline", ChosenResult.From.Username)
        Dim user_id = ChosenResult.From.Id
        If inline_history.ContainsKey(user_id) Then
            If inline_history(user_id).Contains(ChosenResult.ResultId.Trim) Then
                Dim list = inline_history(user_id).ToList
                list.Remove(ChosenResult.ResultId.Trim)
                inline_history(user_id) = New Queue(Of String)(list)
            End If
            inline_history(user_id).Enqueue(ChosenResult.ResultId.Trim)
            If inline_history(user_id).Count > inline_history_limit Then inline_history(user_id).Dequeue()
        Else
            Dim id() As String
            id.Add(ChosenResult.ResultId.Trim)
            inline_history.Add(user_id, New Queue(Of String)(id))
        End If
    End Sub

    'Creo testo con lista
    Function getCercoText(dic As Dictionary(Of Item, Integer), zaino As Dictionary(Of Item, Integer), Optional filter As String = Nothing) As KeyValuePair(Of String, Integer)
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
            If Not IsNothing(filter) Then
                If it.Key.rarity.ToLower <> filter.ToLower Then Continue For
            End If
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
            Dim punti_craft As Integer
            Dim it As New Item
            'Dim lootbot_id As ULong = 171514820
            Dim kill As Boolean = False
#Region "File prezzi"
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
                    builder.AppendLine("Piombo:2400").AppendLine("Sabbia:400").AppendLine("Vetro:150")
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
                    Exit Sub
                End If
            End If
#End Region
            If isZaino(message.Text) Then
#Region "Zaino Ricevuto"
                IO.Directory.CreateDirectory("zaini")
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso message.Chat.Type = ChatType.Private Then
                    'sta inviando più parti di zaino
                    zaini.Item(message.From.Id) += message.Text
                    StampaDebug("Zaino diviso ricevuto.")
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    'sta inviando zaino per confronto
                    Dim zaino = parseZaino(message.Text)
                    Dim cercotext = parseCerca(confronti.Item(message.From.Id))
                    If cercotext.Count = 0 Then cercotext = parseListaoVendi(confronti(message.From.Id))
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
#End Region
            ElseIf isInlineCerco(message.Text) Then
#Region "Cerco Inline ricevuto"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) AndAlso message.Chat.Type = ChatType.Private Then
                    confronti.Item(message.From.Id) = message.Text
                    stati.Item(message.From.Id) = 110
                    a = api.SendTextMessageAsync(message.Chat.Id, "Invia ora lo zaino o il /vendi nel quale cercare gli oggetti che stai cercando." + vbCrLf + "Se ne hai uno salvato, puoi toccare 'Utilizza il mio zaino' per utilizzarlo.",,,, creaConfrontaKeyboard(True)).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    Dim VendiDic = parseListaoVendi(message.Text)
                    Dim ListaDic = parseListaoVendi(confronti(message.From.Id))
                    If ListaDic.Count = 0 Then ListaDic = parseCerca(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(VendiDic, ListaDic)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(ListaDic, result),,,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                End If
#End Region
            ElseIf isListaoVendi(message.Text) Then
#Region "lista o vendi ricevuto"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) AndAlso message.Chat.Type = ChatType.Private Then
                    confronti.Item(message.From.Id) = message.Text
                    stati.Item(message.From.Id) = 110
                    a = api.SendTextMessageAsync(message.Chat.Id, "Invia ora lo zaino o il /vendi nel quale cercare gli oggetti che stai cercando." + vbCrLf + "Se hai uno zaino salvato, puoi toccare 'Utilizza il mio zaino' per utilizzarlo.",,,, creaConfrontaKeyboard(True)).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    Dim VendiDic = parseListaoVendi(message.Text)
                    Dim ListaDic = parseListaoVendi(confronti(message.From.Id))
                    If ListaDic.Count = 0 Then ListaDic = parseCerca(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(VendiDic, ListaDic)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(ListaDic, result),,,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                End If
#End Region
            ElseIf isPrezziNegozi(message.Text) Then
#Region "prezzi ricevuti"
                IO.Directory.CreateDirectory("prezzi")
                IO.File.WriteAllText("prezzi/" + message.From.Id.ToString + ".txt", message.Text)
                Console.WriteLine("Salvati prezzi di ID: " + message.From.Id.ToString)
                a = api.SendTextMessageAsync(message.Chat.Id, "I tuoi prezzi sono stati salvati!").Result
#End Region
            ElseIf message.Text.ToLower.Trim.Equals("utilizza il mio zaino") Then
#Region "utilizza zaino ricevuto"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
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
#End Region
            ElseIf isAperturaScrigno(message.Text) Then
                Dim rex As New Regex("\> ([0-9]+)x ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
                Dim matches As MatchCollection = rex.Matches(message.Text)
                Dim res = rex.Replace(message.Text, "> $2 ($1)")
                a = api.SendTextMessageAsync(message.Chat.Id, res).Result
            ElseIf isDungeon(message.Text) Then
                Dim start_index = message.Text.IndexOf("ultima di una parola di Lootia:") + 1
                Dim end_index = message.Text.IndexOf("Puoi fare un tentativo per cercare di fuggire") - 1
                Dim input = message.Text.Substring(start_index + "ultima di una parola di Lootia:".Length, end_index - (start_index + "ultima di una parola di Lootia:".Length)).Trim
                Dim matching = getDungeonItems(input)
                Dim o As String = ""
                For Each i In matching
                    o &= "`" + i.Value.name + "`" + vbCrLf
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, o,,,,, ParseMode.Markdown).Result
            ElseIf isIspezione(message.Text) Then
                Dim start_index = message.Text.IndexOf("🗝") + 1
                Dim end_index = message.Text.IndexOf("Esplora") - 1
                Dim input = message.Text.Substring(start_index + 1, end_index - start_index).Trim
                Dim matching = getIspezioneWords(input)
                Dim o As String = ""
                For Each i In matching
                    o &= "`" + i.ToLower + "`" + vbCrLf
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, o,,,,, ParseMode.Markdown).Result
            Else
                Console.WriteLine("{0} {1} {2} from: {3}", Now.ToShortDateString, Now.ToShortTimeString, message.Text, message.From.Username)
            End If
            'COMANDI
            If message.Text.ToLower.StartsWith("/salvazaino") Then
#Region "salvazaino"
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
#End Region
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
                If message.Chat.Type = ChatType.Group Or message.Chat.Type = ChatType.Supergroup Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inoltra i messaggi in privato").Result
                End If
                a = api.SendTextMessageAsync(message.From.Id, "Invia l'elenco 'Cerco:' generato dal comando inline del bot o la lista degli oggetti necessari generata dal comando /lista.",,,, creaConfrontaKeyboard).Result
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
                    getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa, punti_craft)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                    If rarity_craft.ContainsKey(ItemIds.Item(i).rarity) Then punti_craft += rarity_craft.Item(ItemIds.Item(i).rarity)
                Next
                Dim result As String = getCraftListText(createCraftCountList(CraftList), item_ids.ToArray, zainoDic, gia_possiedi, spesa, punti_craft)
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
                a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, getcraftText(CraftTree, gia_possiedi, item_ids.ToArray), "Lista Craft"),,, message.MessageId).Result
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
                    getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa, punti_craft)
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
                        getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa, punti_craft)
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
            ElseIf message.Text.ToLower.StartsWith(If(message.Chat.Type = ChatType.Private, "/help", "/help@craftlootbot")) Then
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
            ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/classifica") Then
                notificaPremio()
            ElseIf message.Text.StartsWith("/info") Then
#Region "/info"
                item = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/info" + "@craftlootbot", "/info"), "").Trim
                If item = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto su cui avere informazioni").Result
                    Exit Sub
                End If
                id = getItemId(item)
                If id <> -1 Then
                    Dim builder As New Text.StringBuilder
                    Dim related = ItemIds(id).getRelatedItemsIDs
                    builder.AppendLine(ItemIds(id).ToString)
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString,,,, If(IsNothing(related), Nothing, creaInfoKeyboard(related)), ParseMode.Markdown).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If
#End Region
            ElseIf message.Text.StartsWith("/stima") Then
#Region "/stima"
                item = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/stima" + "@craftlootbot", "/stima"), "").Trim
                If item = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto dopo il comando: '/stima spada antimateria'").Result
                    Exit Sub
                End If
                id = getItemId(item)

                If id <> -1 Then
                    If Not isCraftable(id) Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto inserito deve essere craftabile").Result
                        Exit Sub
                    End If
                    Dim builder As New Text.StringBuilder
                    Dim rarity = ItemIds(id).rarity
                    Dim s_tot As Integer = If(rarity_value.ContainsKey(rarity), rarity_value(rarity), 0)
                    Dim pc_tot As Integer = If(rarity_craft.ContainsKey(rarity), rarity_craft(rarity), 0)
                    Dim costoBase As Integer = 0

                    Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                    Dim zaino As String = ""
                    If IO.File.Exists(path) Then
                        zaino = IO.File.ReadAllText(path)
                    End If
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                    zainoDic = parseZaino(zaino)
                    Dim zainoDic_copy = zainoDic
                    getNeededItemsList(id, CraftList, zainoDic_copy, gia_possiedi, spesa, punti_craft)
                    If rarity_value.ContainsKey(ItemIds.Item(id).rarity) Then spesa += rarity_value.Item(ItemIds.Item(id).rarity)
                    If rarity_craft.ContainsKey(ItemIds.Item(id).rarity) Then punti_craft += rarity_craft.Item(ItemIds.Item(id).rarity)

                    Dim prezzi_dic As Dictionary(Of Item, Integer)
                    Dim prezzi As String
                    If IO.File.Exists("prezzi/" + message.From.Id.ToString + ".txt") Then
                        prezzi = IO.File.ReadAllText("prezzi/" + message.From.Id.ToString + ".txt")
                        prezzi_dic = parsePrezzoNegozi(prezzi)
                    End If
                    ItemIds(id).contaCosto(id, s_tot, pc_tot, costoBase, prezzi_dic)
                    builder.AppendLine(ItemIds(id).name + ":").AppendLine()
                    builder.Append("Punti craft mancanti / totali: ").AppendLine(punti_craft.ToString + "/" + pc_tot.ToString)
                    builder.Append("Costo craft mancante / totale: ").AppendLine(prettyCurrency(spesa) + "/" + prettyCurrency(s_tot))
                    builder.Append("Valore stimato con i prezzi salvati: ").AppendLine(prettyCurrency(costoBase))
                    builder.Append("Valore corrente stimato: ").AppendLine(prettyCurrency(ItemIds(id).estimate))
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                End If
#End Region
            ElseIf message.Text.StartsWith("/creabili") Then
#Region "/creabili"
                Dim params = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/creabili" + "@craftlootbot", "/creabili"), "").Trim
                Dim param_array = params.Split(" ")
                Dim rarity = If(param_array.Length > 1, param_array(0).Trim, "")
                Dim offset As Integer
                If Not Integer.TryParse(If(param_array.Length = 1, param_array(0).Trim, param_array(1).Trim), offset) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci un numero valido dopo la rarità").Result
                End If
                Dim reg As New Regex("^(C|NC|R|UR|L|E|UE|U|S|X){1}", RegexOptions.IgnoreCase)
                Dim fil = Nothing
                If reg.IsMatch(rarity) Then fil = reg.Match(params).Value.Trim.ToUpper
                Dim filtered_items = ItemIds.Where(Function(i) isCraftable(i.Value.id) AndAlso If(IsNothing(fil), True, i.Value.rarity = fil))
                Dim limit = filtered_items.Count
                Dim Tasks(limit) As Task(Of KeyValuePair(Of String, Integer))
                Dim ct As New Threading.CancellationTokenSource
                For i = 0 To limit - 1
                    Dim ite As Item = filtered_items(i).Value
                    'If Not isCraftable(ite.id) Then Continue For
                    Tasks(i) = Task.Factory.StartNew(Function() As KeyValuePair(Of String, Integer)
                                                         Return task_getMessageText(ite, message.From.Id, ct.Token)
                                                     End Function)
                Next
                Dim builder As New Text.StringBuilder
                For i = 0 To limit - 1
                    If Tasks(i) Is Nothing Then Continue For
                    Dim result = Tasks(i).Result
                    If result.Value >= 0 And result.Value <= offset Then
                        If result.Value = 0 Then
                            builder.AppendLine("`/craft " + filtered_items(i).Value.name + "`")
                        Else
                            builder.Append("`/lista " + filtered_items(i).Value.name + "` ").AppendLine(result.Value.ToString + " mancanti")
                        End If

                    End If
                Next
                If builder.ToString = "" Then builder.AppendLine("Nessun oggetto craftabile con l'offset specificato")
                answerLongMessage(builder.ToString, message.Chat.Id)
#End Region
            ElseIf message.Text.StartsWith("/dungeon") Then
#Region "Dungeon"
                Dim input = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/dungeon" + "@craftlootbot", "/dungeon"), "").Trim
                input = input.Replace(" ", "")
                If input = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci il pattern dopo il comando. Es: '/dungeon S _ _ _ _ - _ _ _ _ _ e'").Result
                    Exit Sub
                End If
                Dim o As String = ""
                Dim matching = getDungeonItems(input)
                For Each i In matching
                    o &= "`" + i.Value.name + "`" + vbCrLf
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, o,,,,, ParseMode.Markdown).Result
#End Region
            ElseIf message.Text.StartsWith("/ispezione") Then
#Region "ispezione"
                Dim input = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/ispezione" + "@craftlootbot", "/ispezione"), "").Trim
                If input = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci il pattern dopo il comando. Es: '/ispezione _ o _ a _ d o'").Result
                    Exit Sub
                End If
                Dim matching = getIspezioneWords(input)
                Dim o As String = ""
                For Each i In matching
                    o &= "`" + i.ToLower + "`" + vbCrLf
                Next
                a = api.SendTextMessageAsync(message.Chat.Id, o,,,,, ParseMode.Markdown).Result
#End Region
            ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/kill") Then
                kill = True
                Dim ex As New Exception("PROCESSO TERMINATO SU RICHIESTA")
                Throw ex
            End If
            aggiornastats(message.Text, message.From.Username)
        Catch e As Exception
#Region "exception handling"
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
#End Region
    End Sub

    Sub getNeededItemsList(id As Integer, ByRef CraftList As List(Of Item), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer, ByRef punti_craft As Integer)
        Dim rows As Integer() = requestCraft(id)
        If rows Is Nothing Then Exit Sub
        Dim item As Item
        For Each ids In rows
            item = ItemIds(ids)
            If isCraftable(item.id) Then
                If Not zaino.ContainsKey(item) Then
                    If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)
                    If rarity_craft.ContainsKey(item.rarity) Then punti_craft += rarity_craft.Item(item.rarity)
                    StampaDebug(String.Format("Oggetto: {0}, +{1}={2}", item.name, If(rarity_craft.ContainsKey(item.rarity), rarity_craft.Item(item.rarity).ToString, "0"), punti_craft.ToString))

                    getNeededItemsList(item.id, CraftList, zaino, possiedi, spesa, punti_craft)
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

    Function getCraftListText(dic As Dictionary(Of Item, Integer), oggetti() As Integer, zaino As Dictionary(Of Item, Integer), ByRef gia_possiedi As Dictionary(Of Item, Integer), spesa As Integer, punti_craft As Integer) As String

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
            'For i = 1 To pos.Value
            '    If rarity_value.ContainsKey(pos.Key.rarity) Then spesa -= rarity_value.Item(pos.Key.rarity)

            'Next
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
        Dim result As New Text.StringBuilder(buildernecessari.ToString + builderposseduti.ToString)
        result.AppendLine()
        result.Append("Per eseguire i craft spenderai: ")
        result.AppendLine(prettyCurrency(spesa))
        result.Append("Guadagnerai inoltre ").Append(punti_craft).AppendLine(" punti craft")
        If zaino.Count > 0 Then
            result.AppendLine("(Escludendo oggetti già craftati)")
        End If
        Return result.ToString
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

    Function getcraftText(list As List(Of KeyValuePair(Of Item, Integer)), ByRef possiedi As Dictionary(Of Item, Integer), oggetti() As Integer) As String
        Dim builder As New Text.StringBuilder
        Dim ogg_string()
        For Each i In oggetti
            ogg_string.Add(ItemIds(i).name)
        Next
        Dim intestazione As String = "Lista craft per " + String.Join(", ", ogg_string) + ": "

        builder.Append(intestazione)
        builder.Append(vbCrLf)
        'Dim sorted = From pair In list
        'Order By pair.Value Descending
        Dim sorted = list.OrderByDescending(Of Integer)(Function(x) x.Value).ThenBy(Of String)(Function(p) p.Key.name).Select(Of KeyValuePair(Of Item, Integer))(Function(o) o)
        'Dim sortedDictionary = sorted.ToList()
        For Each craft In sorted
            If isCraftable(craft.Key.id) Then
                builder.Append("Crea " + craft.Key.name)
                builder.Append(vbCrLf)
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
        If Filtro_zaino.Count = 0 Then
            builder.Clear.Append("Nessun oggetto può essere venduto.")
            res.Add(builder.ToString)
            Return res
        End If
        Dim prev_rarity As String = Filtro_zaino.FirstOrDefault.Key.rarity
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
            Dim list = ItemIds.Where(Function(i) i.Value.name.ToLower = name.ToLower.Trim)
            If list.Count > 0 Then Return list.First.Key
            'For Each it In ItemIds
            '    If it.Value.name.ToLower = name.ToLower.Trim Then Return it.Key
            'Next
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

    'dato una /list o un /vendi restituisco dizionario(oggetto, quantità necessaria)
    Function parseListaoVendi(text As String) As Dictionary(Of Item, Integer)
        Dim listadic As New Dictionary(Of Item, Integer)
        Dim rex As New Regex("\> (([0-9]+) su )?([0-9]+) di ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        For Each match As Match In matches
            Try
                If text.Contains("Già possiedi:") AndAlso match.Index > text.IndexOf("Già possiedi:") Then Return listadic
                Dim necessario = match.Groups(2).Value
                If necessario = "" Then necessario = match.Groups(3).Value
                listadic.Add(ItemIds.Item(getItemId(match.Groups(4).Value)), (Integer.Parse(necessario)))
            Catch e As Exception
                Continue For
            End Try
        Next
        Return listadic
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
            .AppendLine("Inner Exception: " + If(IsNothing(ex.InnerException), "Nessuna", ex.InnerException.Message))
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

    Function answerLongMessage(result As String, chatID As Long, Optional parse As ParseMode = ParseMode.Markdown) As Message
        Dim a
        If result.Length > 4096 Then
            Dim valid_substring As String
            Do
                Dim substring = result.Substring(0, If(result.Length > 4096, 4096, result.Length))
                valid_substring = substring.Substring(0, substring.LastIndexOf(Environment.NewLine))
                result = result.Substring(substring.LastIndexOf(Environment.NewLine))
                If valid_substring.Trim <> "" And valid_substring <> Environment.NewLine Then
                    a = api.SendTextMessageAsync(chatID, valid_substring,,,,, parse).Result
                Else
                    a = api.SendTextMessageAsync(chatID, result,,,,, parse).Result
                    result = ""
                End If
            Loop While result <> "" And result <> Environment.NewLine

        Else
            a = api.SendTextMessageAsync(chatID, result,,,,, parse).Result
        End If
        Return a
    End Function

    Function checkInputItems(message_text As String, chat_id As Long, comando As String, Optional checkCraftable As Boolean = True) As List(Of Integer)

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
            If checkCraftable Then
                If Not isCraftable(temp_id) Then
                    Dim e = api.SendTextMessageAsync(chat_id, "L'oggetto " + i + " non è craftabile, verrà saltato.").Result
                    Continue For
                End If
            End If
            item_ids.Add(temp_id)
        Next
        If item_ids.Count = 0 Then
            a = api.SendTextMessageAsync(chat_id, "Nessuno degli oggetti inseriti è valido, riprova.").Result
        End If
        Return item_ids
    End Function

    Sub notificaPremio()
        Dim builder As New Text.StringBuilder()
        builder.AppendLine()
        Dim i = 1
        For Each user In PersonalStats.OrderByDescending(Function(p) p.Value).Take(50)
            builder.AppendLine(i.ToString + "° " + user.Key + ": " + user.Value.ToString)
            i += 1
        Next
        answerLongMessage("La classifica è: " + builder.ToString, 1265775, ParseMode.Default)
    End Sub

    Sub vecchizaini()
        While True
            Try
                For Each file In IO.Directory.GetFiles("zaini/")
                    If DateDiff(DateInterval.Day, IO.File.GetLastWriteTime(file), Date.Now) > olderZaini_limit Then
                        IO.File.Delete(file)
                        StampaDebug(file + " Cancellato!")
                    End If
                Next
                Threading.Thread.Sleep(24 * 60 * 60 * 1000) 'aspetto 1 giorno
            Catch ex As Exception
                Console.WriteLine("Errore cancellazione file.")
                Threading.Thread.Sleep(24 * 60 * 60 * 1000)
            End Try
        End While
    End Sub

End Module
