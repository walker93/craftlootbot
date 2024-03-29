﻿Imports Telegram.Bot
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.Enums
Imports System.Net
Imports System.Text.RegularExpressions
Imports craftlootbot

Module Module1
    Dim WithEvents api As TelegramBotClient

    Dim time_start As Date = Date.UtcNow
    Public ItemIds As New Dictionary(Of Integer, Item) 'Dizionario degli oggetti di Loot (ID, oggetto)
    Dim items() As Item
    Dim stati As New Dictionary(Of ULong, Integer) 'stato utenti
    Dim zaini As New Concurrent.ConcurrentDictionary(Of ULong, String) 'salvataggio zaini in corso
    Dim confronti As New Dictionary(Of ULong, String) 'confronti in corso
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
        'test token
        Try
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls12
            Dim bot = api.GetMeAsync.Result
            Console.WriteLine(bot.Username & ": " & bot.Id)
        Catch ex As AggregateException
            Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
        Catch ex As Exception
            Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.Message)
        End Try

        'Esecuzione dei thread infiniti
        Dim Database_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf Database.initialize_Dictionary))
        Database_thread.Start()
        Dim stats_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf salvaStats))
        stats_thread.Start()
        Dim zaini_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf vecchizaini))
        zaini_thread.Start()
        Dim inlineHistory_thread As New Threading.Thread(New Threading.ThreadStart(AddressOf salva_inlineHistory))
        inlineHistory_thread.Start()

        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start() 'Thread principale per long polling ricezione Updates
    End Sub

    'Long polling ricezione update
    Sub run()
        Dim updates() As Update
        Dim offset As Integer = 0
        While True
            Try
                updates = api.GetUpdatesAsync(offset,, 20).Result
                For Each up As Update In updates
                    Select Case up.Type
                        Case UpdateType.Message
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_Message))
                            t.Start(up.Message)
                        Case UpdateType.InlineQuery
                            Dim ct As New Threading.CancellationTokenSource
                            Dim t As New Task(Of Boolean)(Function() process_query(up.InlineQuery, ct.Token).Result, ct.Token)
                            Dim from_id = up.InlineQuery.From.Id
                            If thread_inline.ContainsKey(from_id) Then
                                thread_inline.Item(from_id).Cancel()
                                thread_inline.Remove(from_id)
                            End If
                            t.Start()
                            thread_inline.Add(from_id, ct)
                        Case UpdateType.ChosenInlineResult
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_ChosenQuery))
                            t.Start(up.ChosenInlineResult)
                        Case UpdateType.CallbackQuery
                            Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf process_callbackData))
                            t.Start(up.CallbackQuery)
                    End Select
                    offset = up.Id + 1
                Next
            Catch ex As AggregateException
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("{0} {1} FATAL Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
            Catch ex As Exception
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("{0} {1} FATAL Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.Message)
            End Try
        End While
    End Sub

#Region "callbackdata"
    'risposta a Update di tipo CallBackQuery
    Async Sub process_callbackData(callback As CallbackQuery)
        Try
            'gestione tastiera /info
            If callback.Data.StartsWith("info_") Then
                Dim i As Integer = Integer.Parse(callback.Data.Replace("info_", ""))
                Dim result As String = ItemIds(i).ToString
                Dim related = ItemIds(i).getRelatedItemsIDs
                Dim e = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, result, ParseMode.Markdown,,, If(IsNothing(related), Nothing, creaInfoKeyboard(related))).Result
                Await api.AnswerCallbackQueryAsync(callback.Id,,,, 0)

            ElseIf callback.Data.StartsWith("craftF_") Then
#Region "/craft File"
                Dim text As String = ""
                Try
                    text = IO.File.ReadAllText("crafts/" + callback.Data.Replace("craftF_", ""))
                Catch e As IO.FileNotFoundException
                    Dim edit = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "La lista craft non è più disponibile!").Result
                End Try
                Dim ids = text.Split("_")
                Dim item_ids = ids.Select(Function(id) Integer.Parse(id))

                If item_ids.Count = 0 Then Exit Sub
                Dim prof As Integer = -1
                Dim CraftTree As New List(Of KeyValuePair(Of Item, Integer))
                Dim gia_possiedi As New Dictionary(Of Item, Integer)
                Dim spesa As Integer
                Dim zainoDic = getZaino(callback.From.Id)
                Dim zainoDic_copy = zainoDic
                For Each i In item_ids
                    prof = -1
                    getCraftItemsTree(i, prof, CraftTree, zainoDic_copy, gia_possiedi)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                Next
                Await api.SendChatActionAsync(callback.Message.Chat.Id, ChatAction.UploadDocument)
                Dim name As String = getFileName()
                Dim b = api.SendDocumentAsync(callback.Message.Chat.Id, prepareFile(name, getcraftText(CraftTree, gia_possiedi, item_ids.ToArray), "Lista Craft"))
                Dim a = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "File in arrivo...").Result

                Dim c = b.Result
                IO.File.Delete(name)
                Await api.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId)
                IO.File.Delete("crafts/" + callback.Data.Replace("craftF_", ""))
#End Region
            ElseIf callback.Data.StartsWith("craftM_") Then
#Region "/craft Messaggio"
                Dim text As String = ""
                Try
                    text = IO.File.ReadAllText("crafts/" + callback.Data.Replace("craftM_", ""))
                Catch e As IO.FileNotFoundException
                    Dim b = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "La lista craft non è più disponibile!").Result
                End Try
                Dim ids = text.Split("_")
                Dim item_ids = ids.Select(Function(id) Integer.Parse(id))
                If item_ids.Count = 0 Then Exit Sub
                Dim prof As Integer = -1
                Dim CraftTree As New List(Of KeyValuePair(Of Item, Integer))
                Dim gia_possiedi As New Dictionary(Of Item, Integer)
                Dim spesa As Integer
                Dim zainoDic = getZaino(callback.From.Id)
                Dim zainoDic_copy = zainoDic

                For Each i In item_ids
                    prof = -1
                    getCraftItemsTree(i, prof, CraftTree, zainoDic_copy, gia_possiedi)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                Next
                Dim text_result As String = getcraftText(CraftTree, gia_possiedi, item_ids.ToArray, True)
                answerTooEntities(text_result, callback.Message.Chat.Id, ParseMode.Markdown)
                Await api.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId)
                IO.File.Delete("crafts/" + callback.Data.Replace("craftM_", ""))
#End Region
            ElseIf callback.Data = "err_reset" Then
#Region "Ripristino dati"
                Dim UserID As Long = callback.From.Id
                Console.WriteLine("Ripristino richiesto per l'utente {0}", userID)
                Dim report As New Text.StringBuilder("Ripristino eseguito!")
                report.AppendLine()
                If zaini.TryRemove(userID, "") Then report.AppendLine("Salvataggio zaino interrotto.")
                If stati.ContainsKey(userID) Then stati.Remove(userID) : report.AppendLine("Stato utente ripristinato.")
                If confronti.ContainsKey(userID) Then confronti.Remove(userID) : report.AppendLine("Confronto interrotto.")
                If IO.File.Exists("zaini/" + userID.ToString + ".txt") Then IO.File.Delete("zaini/" + userID.ToString + ".txt") : report.AppendLine("Zaino utente cancellato!")
                If IO.File.Exists("equip/" + userID.ToString + ".txt") Then IO.File.Delete("equip/" + userID.ToString + ".txt") : report.AppendLine("Equipaggiamento utente cancellato!")
                If IO.File.Exists("prezzi/" + userID.ToString + ".txt") Then IO.File.Delete("prezzi/" + userID.ToString + ".txt") : report.AppendLine("Prezzi utente cancellati!")
                If IO.File.Exists("alias/" + userID.ToString + ".txt") Then IO.File.Delete("alias/" + userID.ToString + ".txt") : report.AppendLine("Alias utente cancellati!")
                For Each file In IO.Directory.GetFiles("crafts")
                    Dim info As New IO.FileInfo(file)
                    If info.Name.StartsWith(userID) Then IO.File.Delete(file) : report.AppendLine("File '" + info.Name + "' cancellato!")
                Next
                Await api.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId)
                Await api.SendTextMessageAsync(callback.Message.Chat.Id, report.ToString,,,,, ,, creaNULLKeyboard)
#End Region
            ElseIf callback.Data = "DelMess" Then
                Await api.DeleteMessageAsync(callback.Message.Chat.Id, callback.Message.MessageId)
            Else
                'gestione tastiera Help
                Dim result As String = process_help(callback.Data)
                Dim e = api.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, result, ParseMode.Markdown,, True, creaHelpKeyboard()).Result
                Await api.AnswerCallbackQueryAsync(callback.Id,,,, 0)
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

    'risposta a Update di tipo messaggio
    Async Function process_query(InlineQuery As InlineQuery, ct As Threading.CancellationToken) As Task(Of Boolean)
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
                    Await api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True)
                    Return True
                End If

                Dim limit As Integer = If(matching_items.Length > 50, 50, matching_items.Length - 1)
                Dim res() As InlineQueryResults.InlineQueryResultArticle
                Dim message_text As String = ""
                Dim Tasks(limit) As Task(Of KeyValuePair(Of String, Integer))
                For i = 0 To limit
                    Dim pair = matching_items(i)
                    If Not isCraftable(pair.Key.id) Then Continue For

                    Tasks(i) = Task.Factory.StartNew(Function() As KeyValuePair(Of String, Integer)
                                                         Return task_getMessageText(pair.Key, InlineQuery.From.Id, ct, pair.Value)
                                                     End Function)
                Next
                For i = 0 To limit
                    If Tasks(i) Is Nothing Then Continue For
                    Dim result = Tasks(i).Result
                    Dim content As New InlineQueryResults.InputTextMessageContent(If(result.Key <> "", result.Key, "Possiedo già tutti gli oggetti necessari"))
                    Dim f = matching_items(i).Value
                    Dim costo As Integer = If(rarity_value.ContainsKey(matching_items(i).Key.rarity), rarity_value(matching_items(i).Key.rarity), 0)
                    Dim punti As Integer = If(rarity_craft.ContainsKey(matching_items(i).Key.rarity), rarity_craft(matching_items(i).Key.rarity), 0)
                    Dim costoBase As Integer = 0 'matching_items(i).value
                    Dim oggbase As Integer = 0
                    Dim costoScrigni As Integer = 0
                    Dim article = New InlineQueryResults.InlineQueryResultArticle(f + matching_items(i).Key.id.ToString,
                                                                                  "Cerco per " + matching_items(i).Key.name,
                                                                                  content)

                    matching_items(i).Key.contaCosto(matching_items(i).Key.id, costo, punti, costoBase, oggbase, costoScrigni)

                    article.Description = If(content.MessageText.Contains("Possiedo"), "Hai già tutti gli oggetti" &
                        If(IsNothing(f), "", " " & f).ToUpper, "Hai bisogno di " + result.Value.ToString &
                        If(result.Value = 1, " oggetto", " oggetti") &
                        If(IsNothing(f), "", " " & f).ToUpper) & " su " & oggbase.ToString + " totali."

                    article.Description += vbCrLf + "Costo craft: " + prettyCurrency(costo) + ", Punti craft: " + punti.ToString
                    res.Add(article)
                Next
                If res IsNot Nothing Then results.AddRange(res)
                Await api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 5, True,, "Aggiorna zaino salvato", "inline_" + query_text.Replace(" ", "-"))
            Else
                'Non ha zaino, propongo di salvarlo
                Await api.AnswerInlineQueryAsync(InlineQuery.Id, results.ToArray, 0, True,, "Cliccami e salva lo zaino per questa funzione", "inline_" + query_text.Replace(" ", "-"))
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

        Dim zainoDic As Dictionary(Of Item, Integer)

        zainoDic = getZaino(id)
        zainoDic_copy = zainoDic

        task_getNeededItemsList(item.id, CraftList, zainoDic_copy, gia_possiedi, ct)
        If CraftList.Count <> 0 Then
            StampaDebug(String.Format("returning from '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
            Return getCercoText(createCraftCountList(CraftList), zainoDic, fil)
        End If
        StampaDebug(String.Format("Terminating '{0}'.", Threading.Thread.CurrentThread.ManagedThreadId))
        Return New KeyValuePair(Of String, Integer)("", 0)
    End Function

    'Salvataggio cronologia inline
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
    'risposta a Update di tipo messaggio
    Private Sub process_Message(message As Message)
        Try
            If flush Then 'controllo flush, se attivo ignoro il messaggio
                If message.Date < time_start Then Exit Sub
            End If
            If message.Type <> MessageType.Text And message.Type <> MessageType.Document Then Exit Sub 'ignoro messagi che non siano testo o documenti
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
            'Dim lootbot_id As ULong = 171514820 <-- ID Bot Lootbot
            Dim kill As Boolean = False

#Region "File prezzi"
            If message.Type = MessageType.Document Then
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
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso message.Chat.Type = ChatType.Private Then
                    'sta inviando più parti di zaino
                    zaini.Item(message.From.Id) += message.Text + vbNewLine
                    StampaDebug("Zaino diviso ricevuto.") ' ID:" + message.MessageId.ToString + " tempo: " + ((Now.Ticks - cron(message.MessageId)) / 10000).ToString)
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    'sta inviando zaino per confronto
                    Dim zaino = parseZaino(message.Text)
                    Dim cercotext = parseCerca(confronti.Item(message.From.Id))
                    If cercotext.Count = 0 Then cercotext = parseListaoVendi(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(zaino, cercotext)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(cercotext, result),,,,, ,, creaNULLKeyboard).Result
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
                    a = api.SendTextMessageAsync(message.Chat.Id, "Invia ora lo zaino o il /vendi nel quale cercare gli oggetti che stai cercando." + vbCrLf + "Se ne hai uno salvato, puoi toccare 'Utilizza il mio zaino' per utilizzarlo.",,,,, ,, creaConfrontaKeyboard(True)).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    Dim VendiDic = parseListaoVendi(message.Text)
                    Dim ListaDic = parseListaoVendi(confronti(message.From.Id))
                    If ListaDic.Count = 0 Then ListaDic = parseCerca(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(VendiDic, ListaDic)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(ListaDic, result),,,,, ,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                End If
#End Region
            ElseIf isListaoVendi(message.Text) Then
#Region "lista o vendi ricevuto"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) AndAlso message.Chat.Type = ChatType.Private Then
                    confronti.Item(message.From.Id) = message.Text
                    stati.Item(message.From.Id) = 110
                    a = api.SendTextMessageAsync(message.Chat.Id, "Invia ora lo zaino o il /vendi nel quale cercare gli oggetti che stai cercando." + vbCrLf + "Se hai uno zaino salvato, puoi toccare 'Utilizza il mio zaino' per utilizzarlo.",,,, , ,, creaConfrontaKeyboard(True)).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    Dim VendiDic = parseListaoVendi(message.Text)
                    Dim ListaDic = parseListaoVendi(confronti(message.From.Id))
                    If ListaDic.Count = 0 Then ListaDic = parseCerca(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(VendiDic, ListaDic)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(ListaDic, result),,,,, ,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                Else
                    Dim ParsedDic = parseListaoVendi(message.Text)
                    Dim commands = getRicercaText(ParsedDic)
                    For Each ricerca In commands
                        a = api.SendTextMessageAsync(message.Chat.Id, ricerca).Result
                    Next
                End If
#End Region
            ElseIf isPrezziNegozi(message.Text) Then
#Region "prezzi ricevuti"
                IO.File.WriteAllText("prezzi/" + message.From.Id.ToString + ".txt", message.Text)
                Console.WriteLine("Salvati prezzi di ID: " + message.From.Id.ToString)
                a = api.SendTextMessageAsync(message.Chat.Id, "I tuoi prezzi sono stati salvati!").Result
#End Region
            ElseIf message.Text.ToLower.Trim.Equals("utilizza il mio zaino") Then
#Region "utilizza zaino ricevuto"
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) AndAlso message.Chat.Type = ChatType.Private Then
                    Dim path As String = "zaini/" + message.From.Id.ToString + ".txt"
                    'Dim zaino As String = ""
                    If HasZaino(message.From.Id) Then
                        'zaino = IO.File.ReadAllText(path)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Per utilizzare il tuo zaino, devi averne uno salvato." + vbCrLf + "Inoltra il tuo zaino di seguito.").Result
                        Exit Sub
                    End If
                    zainoDic = getZaino(message.From.Id)
                    Dim cercotext = parseCerca(confronti.Item(message.From.Id))
                    If cercotext.Count = 0 Then cercotext = parseListaoVendi(confronti(message.From.Id))
                    Dim result = ConfrontaDizionariItem(zainoDic, cercotext)
                    a = api.SendTextMessageAsync(message.Chat.Id, getConfrontoText(cercotext, result),,,,, ,, creaNULLKeyboard).Result
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai effettuando il confronto.",,,, , ,, creaNULLKeyboard).Result
                End If
#End Region
            ElseIf isAperturaScrigno(message.Text) Then
#Region "Apertura scrigno"
                Dim rex As New Regex("\> ([0-9.]+)x ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
                Dim matches As MatchCollection = rex.Matches(message.Text)
                Dim res = rex.Replace(message.Text, "> $2 ($1)")
                a = api.SendTextMessageAsync(message.Chat.Id, res).Result
#End Region
            ElseIf isDungeon(message.Text) AndAlso team_members.Contains(message.From.Username) Then
#Region "Parola dungeon"
                Dim start_index = message.Text.IndexOf("ultima di una parola di Lootia:") + 1
                Dim end_index = message.Text.IndexOf("Puoi fare un tentativo per cercare di fuggire") - 1
                Dim input = message.Text.Substring(start_index + "ultima di una parola di Lootia:".Length, end_index - (start_index + "ultima di una parola di Lootia:".Length)).Trim
                Dim matching = getDungeonItems(input)
                If matching.Count = 0 Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Nessun risultato trovato :(").Result
                    Exit Sub
                ElseIf matching.Count > 15 Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Troppi risultati, affina la ricerca").Result
                    Exit Sub
                End If
                Dim o As String = ""
                For Each i In matching
                    o &= "`" + i.Value.name + "`" + vbCrLf
                Next
                answerLongMessage(o, message.Chat.Id, ParseMode.Markdown)
#End Region
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
                If Not zaini.TryAdd(message.From.Id, "") Then Console.WriteLine("Impossibile creare elemento in dictionary zaini, l'elemento esiste già.")
                If message.Chat.Type = ChatType.Group Or message.Chat.Type = ChatType.Supergroup Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inoltra i messaggi in privato").Result
                End If
                a = api.SendTextMessageAsync(message.From.Id, "Inoltra di seguito il tuo zaino diviso in più messaggi. " + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,,, ,, creaZainoKeyboard).Result
            ElseIf message.Text.ToLower.Equals("salva") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then
                    IO.File.WriteAllText("zaini/" + message.From.Id.ToString + ".txt", zaini.Item(message.From.Id))
                    stati.Remove(message.From.Id)
                    If Not zaini.TryRemove(message.From.Id, "") Then Console.WriteLine("Impossibile eliminare l'elemento dal dictionary zaini.")
                    If from_inline_query.ContainsKey(message.From.Id) Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!",,,, , ,, creaInlineKeyboard(from_inline_query.Item(message.From.Id))).Result
                        from_inline_query.Remove(message.From.Id)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il tuo zaino è stato salvato!",,,, , ,, creaNULLKeyboard).Result
                    End If
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non stai salvando uno zaino, utilizza /salvazaino per iniziare il salvataggio.",,,,, ,, creaNULLKeyboard).Result
                End If
            ElseIf message.Text.ToLower.Equals("annulla") Then
                If stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 10)) AndAlso zaini.ContainsKey(message.From.Id) Then
                    stati.Remove(message.From.Id)
                    If Not zaini.TryRemove(message.From.Id, "") Then Console.WriteLine("Impossibile eliminare l'elemento dal dictionary zaini.")
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai annullato il salvataggio dello zaino",,,, , ,, creaNULLKeyboard).Result
                ElseIf stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 100)) Or stati.Contains(New KeyValuePair(Of ULong, Integer)(message.From.Id, 110)) Then
                    stati.Remove(message.From.Id)
                    confronti.Remove(message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Hai annullato il confronto.",,,,, ,, creaNULLKeyboard).Result
                Else
                    a = api.SendTextMessageAsync(message.Chat.Id, "Non hai azioni in sospeso.",,,,, ,, creaNULLKeyboard).Result
                End If
#End Region
            ElseIf message.Text.ToLower.StartsWith("/base") Then
#Region "Base"
                Dim rarity = message.Text.Replace("/base", "").Trim
                If rarity = "" Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci la rarità.").Result
                    Exit Sub
                End If
                zainoDic = getZaino(message.From.Id)
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
                a = api.SendTextMessageAsync(message.From.Id, "Invia l'elenco 'Cerco:' generato dal comando inline del bot o la lista degli oggetti necessari generata dal comando /lista.",,,,, ,, creaConfrontaKeyboard).Result
#End Region
            ElseIf message.Text.ToLower.StartsWith("/lista") Then
#Region "lista"
                Dim item_ids As New List(Of Integer)
                checkInputItems(message.Text.Trim, message.Chat.Id, message.From.Id, "/lista", item_ids)
                If item_ids.Count = 0 Then Exit Sub
                api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                zainoDic = getZaino(message.From.Id)
                Dim zainoDic_copy = zainoDic
                For Each i In item_ids
                    'If Not zainoDic_copy.ContainsKey(ItemIds(i)) Then
                    getNeededItemsList(i, CraftList, zainoDic_copy, gia_possiedi, spesa, punti_craft)
                    If rarity_value.ContainsKey(ItemIds.Item(i).rarity) Then spesa += rarity_value.Item(ItemIds.Item(i).rarity)
                    If rarity_craft.ContainsKey(ItemIds.Item(i).rarity) Then punti_craft += rarity_craft.Item(ItemIds.Item(i).rarity)
                    'Else
                    'gia_possiedi.Add(ItemIds(i), 1)
                    'End If
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
                If id <> -1 Then
                    If Not isCraftable(id) Then
                        Dim e = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è craftabile").Result
                        Exit Sub
                    End If
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                    zainoDic = getZaino(message.From.Id)
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
                Dim item_ids As New List(Of Integer)
                checkInputItems(message.Text.Trim, message.Chat.Id, message.From.Id, "/craft", item_ids)
                If item_ids.Count = 0 Then Exit Sub
                Dim prof As Integer = -1

                a = api.SendTextMessageAsync(message.Chat.Id, "Scegli come ottenere la lista craft:",,,,, ,, creaCraftKeyboard(item_ids.ToArray, message.From.Id)).Result
#End Region
            ElseIf message.Text.ToLower.StartsWith("/vendi") Then
#Region "vendi"
                Dim item_ids As New List(Of Integer)
                checkInputItems(message.Text.Trim, message.Chat.Id, message.From.Id, "/vendi", item_ids)
                If item_ids.Count = 0 Then Exit Sub
                If Not HasZaino(message.From.Id) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Per utilizzare questa funzione devi prima salvare il tuo zaino.").Result
                    Exit Sub
                End If
                api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                zainoDic = getZaino(message.From.Id)
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
                If HasZaino(message.From.Id) Then
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
            ElseIf message.Text.ToLower.StartsWith("/creanegozi") Then
#Region "creanegozi"
                Dim result As New Dictionary(Of Item, Integer)
                If Not HasZaino(message.From.Id) Then
                    a = api.SendTextMessageAsync(message.Chat.Id, "Devi salvare lo zaino prima di usare questa funzione.").Result
                    Exit Sub
                End If
                zainoDic = getZaino(message.From.Id)
                Dim item_ids As New List(Of Integer)
                Dim items_string = message.Text.Trim.Replace(If(message.Text.Trim.ToLower.Contains("@craftlootbot"), "/creanegozi" + "@craftlootbot", "/creanegozi"), "").Trim

                Dim reg As New Regex("^(C|NC|R|UR|L|E|UE|S|U|X|RF2|RF3|RF4|RF5|RF6)?(\*)? {1}", RegexOptions.IgnoreCase)
                Dim filter As Match
                Dim new_message As String = message.Text.Trim
                If items_string <> "" AndAlso reg.IsMatch(items_string & " ") Then
                    filter = reg.Match(items_string & " ")
                    new_message = new_message.Replace(filter.Value.Trim, "").Trim
                End If
                checkInputItems(new_message, message.Chat.Id, message.From.Id, "/creanegozi", item_ids)
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
                    Dim negozi = getNegoziText(result, prezzi_dic, filter)
                    For Each negozio In negozi
                        a = api.SendTextMessageAsync(message.Chat.Id, negozio).Result
                    Next
                    a = api.SendTextMessageAsync(message.Chat.Id, "/privacy tutti").Result
#End Region
                ElseIf message.Text.ToLower.StartsWith("/ottieniprezzi") Then
#Region "ottieniprezzi"
                    If IO.File.Exists("prezzi/" + message.From.Id.ToString + ".txt") Then
                        Dim prezzi_text = IO.File.ReadAllText("prezzi/" + message.From.Id.ToString + ".txt")
                        Dim link = checkLink(prezzi_text)
                        If Not isPrezziNegozi(prezzi_text) AndAlso Not IsNothing(link) Then
                            prezzi_text = "I prezzi sono scaricati autonomamente dal link:" + vbCrLf + link.AbsoluteUri + vbCrLf + vbCrLf + "Il contenuto è:"
                        a = api.SendTextMessageAsync(message.Chat.Id, prezzi_text, , ,, True).Result
                        prezzi_text = getPrezziStringFromURL(link)
                        End If

                        If prezzi_text.Split(vbLf).Length > 15 Then
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
                            If Not zaini.TryAdd(message.From.Id, "") Then Console.WriteLine("Impossibile creare elemento in dictionary zaini, l'elemento esiste già.")
                        End If
                        If Not from_inline_query.ContainsKey(message.From.Id) Then
                            from_inline_query.Add(message.From.Id, message.Text.ToLower.Trim.Replace("/start inline_", "").Replace("-", " "))
                        Else
                            from_inline_query(message.From.Id) = message.Text.ToLower.Trim.Replace("/start inline_", "").Replace("-", " ")
                        End If
                    a = api.SendTextMessageAsync(message.From.Id, "Inoltra o incolla di seguito il tuo zaino, può essere in più messaggi." + vbCrLf + "Premi 'Salva' quando hai terminato, o 'Annulla' per non salvare.",,,,, ,, creaZainoKeyboard).Result
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
                a = api.SendTextMessageAsync(message.Chat.Id, help_builder.ToString, ParseMode.Markdown,,, True,,, creaHelpKeyboard).Result
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
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString, ParseMode.Markdown).Result
#End Region
                ElseIf message.Text.ToLower.StartsWith("/db") Then
#Region "Aggiorno DB"
                    Dim builder As New Text.StringBuilder
                    builder.AppendLine(download_items())
                    builder.AppendLine(download_crafts())
                    builder.AppendLine(Leggo_Items())
                    builder.AppendLine(Leggo_Crafts())
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString).Result
#End Region
                ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/classifica") Then
                    StampaClassificaUtilizzoBot()
                ElseIf message.Text.ToLower.StartsWith("/info") Then
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
                    a = api.SendTextMessageAsync(message.Chat.Id, builder.ToString, ParseMode.Markdown,,,, ,, If(IsNothing(related), Nothing, creaInfoKeyboard(related))).Result
                Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                    End If
#End Region
                ElseIf message.Text.ToLower.StartsWith("/stima") Then
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
                        Dim oggBase As Integer = 0
                        Dim costoScrigni As Integer = 0
                        api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                        zainoDic = getZaino(message.From.Id)
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
                        ItemIds(id).contaCosto(id, s_tot, pc_tot, costoBase, oggBase, costoScrigni, prezzi_dic)
                        builder.AppendLine(ItemIds(id).name + ":").AppendLine()
                        builder.Append("Punti craft mancanti / totali: ").AppendLine(punti_craft.ToString + "/" + pc_tot.ToString)
                        builder.Append("Costo craft mancante / totale: ").AppendLine(prettyCurrency(spesa) + "/" + prettyCurrency(s_tot))
                        builder.Append("Valore stimato con i prezzi salvati: ").AppendLine(prettyCurrency(costoBase))
                        builder.Append("Valore scrigni degli oggetti base + costo craft: ").AppendLine(prettyCurrency(spesa + costoScrigni))
                        builder.Append("Valore corrente stimato: ").AppendLine(prettyCurrency(ItemIds(id).estimate))
                        builder.Append("Totale oggetti base necessario al craft: ").AppendLine(oggBase)
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
                ElseIf message.Text.ToLower.StartsWith("/xml") Then
#Region "/XML"
                    item = message.Text.ToLower.Replace(If(message.Text.Contains("@craftlootbot"), "/xml" + "@craftlootbot", "/xml"), "").Trim
                    If item = "" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto che vuoi ottenere").Result
                        Exit Sub
                    End If
                    id = getItemId(item)
                    If id <> -1 Then
                        api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                        Dim name As String = getFileName()
                        Dim xml As String = "<?xml version='1.0' encoding='UTF-8'?>" + vbNewLine + ItemToXML(id)
                        a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, xml, item, ".xml"),,, message.MessageId).Result
                        IO.File.Delete(name)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                    End If
#End Region
                ElseIf message.Text.ToLower.StartsWith("/html") Then
#Region "/HTML"
                    item = message.Text.ToLower.Replace(If(message.Text.Contains("@craftlootbot"), "/html" + "@craftlootbot", "/html"), "").Trim
                    If item = "" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto che vuoi ottenere").Result
                        Exit Sub
                    End If
                    Dim Header As String = "<head><style>body{padding:20px}div{margin-bottom:5px;margin-left:8px;border-left:1px solid #000;padding:5px}label.leaf{border:none;cursor: default;font-weight: normal}input:checked+label+div{display:none}input:checked+label:before{content:'+ '}input+label:before{content:'- '}label{font-size:16px;padding:2px 5px;cursor:pointer;display:block;font-weight:700}</style></head>"
                    id = getItemId(item)
                    If id <> -1 Then
                        api.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument)
                        Dim name As String = getFileName()
                        Dim counter = 0
                        Dim html As String = Header + vbNewLine + ItemToHTML(id, counter) + "</body>"
                        a = api.SendDocumentAsync(message.Chat.Id, prepareFile(name, html, item, ".html"),,, message.MessageId).Result
                        IO.File.Delete(name)
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto specificato non è stato riconosciuto").Result
                    End If
#End Region
                ElseIf message.Text.ToLower.StartsWith("/setprezzi") Then
#Region "/setprezzi"
                    api.SendChatActionAsync(message.Chat.Id, ChatAction.Typing)
                    'Dim link '= message.Text.Replace(If(message.Text.ToLower.Contains("@craftlootbot"), "/setprezzi" + "@craftlootbot", "/setprezzi"), "").Trim
                    Dim args = message.Text.Split(" ")
                    If args.Length <= 1 Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci un link.").Result
                        Exit Sub
                    End If
                    Dim URLPrezzi = checkLink(args(1))
                    If IsNothing(URLPrezzi) Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Il link inserito non è valido").Result
                    Else
                        If getPrezziStringFromURL(URLPrezzi) <> "" Then
                            IO.File.WriteAllText("prezzi/" + message.From.Id.ToString + ".txt", URLPrezzi.ToString)
                            Console.WriteLine("Salvato link prezzi di ID: " + message.From.Id.ToString)
                            a = api.SendTextMessageAsync(message.Chat.Id, "Il link è stato salvato!").Result
                        Else
                            a = api.SendTextMessageAsync(message.Chat.Id, "Non ho trovato prezzi validi al link specificato.").Result
                        End If
                    End If
#End Region
                ElseIf message.Text.ToLower.StartsWith("/setequip") Then
#Region "/setequip"
                    Dim message_text = message.Text.ToLower.Trim
                    Dim comando = "/setequip"
                    Dim items = message_text.Replace(If(message_text.Contains("@craftlootbot"), comando + "@craftlootbot", comando), "").Split(",").Where(Function(p) p <> "").ToList
                    Dim item_ids As New List(Of Integer)
                    If items.Count = 0 Then
                        If HasEquip(message.From.Id) Then
                            IO.File.Delete("equip/" + message.From.Id.ToString + ".txt")
                            a = api.SendTextMessageAsync(message.Chat.Id, "Equipaggiamento eliminato").Result
                        Else
                            a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci l'oggetto o gli oggetti che vuoi impostare equipaggiati, al momento non hai impostato nulla").Result
                        End If
                        Exit Sub
                    End If
                    For Each i In items
                        i = i.Trim
                        Dim temp_id = getItemId(i)
                        If temp_id = -1 Then
                            a = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto " + i + " non è stato riconosciuto, verrà saltato.").Result
                            Continue For
                        End If
                        If ItemIds(temp_id).getEquipType < 0 Then
                            Dim e = api.SendTextMessageAsync(message.Chat.Id, "L'oggetto " + i + " non è equipaggiabile, verrà saltato.").Result
                            Continue For
                        End If
                        item_ids.Add(temp_id)
                    Next
                    If item_ids.Count = 0 Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Nessuno degli oggetti inseriti è valido, riprova.").Result
                        Exit Sub
                    End If

                    'ora posso salvare
                    saveEquip(item_ids, message.From.Id)
                    a = api.SendTextMessageAsync(message.Chat.Id, "Equipaggiamento salvato").Result
#End Region
                ElseIf message.Text.ToLower.StartsWith("/aggiungialias") Then
#Region "/aggiungialias"
                    Dim split() = message.Text.Split({"=="}, StringSplitOptions.RemoveEmptyEntries)
                    Dim keyword As String = split(0).Remove(0, split(0).Split(" ")(0).Length).Trim 'split(0).Replace(If(message.Text.ToLower.Contains("@craftlootbot"), "/aggiungialias" + "@craftlootbot", "/aggiungialias"), "").Trim
                    If keyword = "" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'alias non può essere vuoto.").Result
                        Exit Sub
                    End If
                    If Not split.Length = 2 Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "L'alias " + keyword + " non è inserito nel formato corretto.").Result
                        Exit Sub
                    End If

                    Dim items As New List(Of Integer)
                    checkInputItems(split(1), message.Chat.Id, message.From.Id, "/aggiungialias", items)
                    If items.Count = 0 Then Exit Sub
                    Dim result = AddPersonalAlias(message.From.Id, keyword, ItemIdListToInputString(items))
                    If result <> "OK" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, result).Result
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Alias " + keyword + " aggiunto!").Result
                    End If
#End Region
                ElseIf message.Text.ToLower.StartsWith("/elencaalias") Then
#Region "/elencaalias"
                    Dim PersonalAlias = getPersonalAlias(message.From.Id)
                    Dim GlobalAlias = getGlobalAlias()
                    Dim builder As New Text.StringBuilder("*I tuoi alias salvati sono:*")
                    builder.AppendLine()
                    If PersonalAlias.Count = 0 Then builder.Clear.AppendLine("Non hai alias salvati al momento.")
                    For Each PA In PersonalAlias
                        builder.AppendLine("`" + PA.Key + "` == " + PA.Value)
                    Next
                    builder.AppendLine.AppendLine("*Alias Globali:*")
                    For Each GA In GlobalAlias
                        builder.AppendLine("`" + GA.Key + "` == " + GA.Value)
                    Next
                    answerTooEntities(builder.ToString, message.Chat.Id, ParseMode.Markdown)
#End Region
                ElseIf message.Text.ToLower.StartsWith("/cancellaalias") Then
#Region "/cancellaalias"
                    Dim input = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/cancellaalias" + "@craftlootbot", "/cancellaalias"), "").Trim
                    Dim result = DeletePersonalAlias(message.From.Id, input)
                    If result <> "OK" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, result).Result
                    Else
                        a = api.SendTextMessageAsync(message.Chat.Id, "Alias " + input + " rimosso!").Result
                    End If
#End Region
                ElseIf team_members.Contains(message.From.Username) AndAlso message.Text.StartsWith("/dungeon") Then
#Region "Dungeon"
                    Dim input = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/dungeon" + "@craftlootbot", "/dungeon"), "").Trim
                    input = input.Replace(" ", "")
                    If input = "" Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Inserisci il pattern dopo il comando. Es: '/dungeon S _ _ _ _ - _ _ _ _ _ e'").Result
                        Exit Sub
                    End If
                    Dim o As String = ""
                    Dim matching = getDungeonItems(input)
                    If matching.Count = 0 Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Nessun risultato trovato :(").Result
                        Exit Sub
                    ElseIf matching.Count > 15 Then
                        a = api.SendTextMessageAsync(message.Chat.Id, "Troppi risultati, affina la ricerca").Result
                        Exit Sub
                    End If
                    For Each i In matching
                        o &= "`" + i.Value.name + "`" + vbCrLf
                    Next
                    answerLongMessage(o, message.Chat.Id)
#End Region

                ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/addmember") Then
#Region "/addmember"
                    Dim member = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/addmember" + "@craftlootbot", "/addmember"), "").Trim
                    If member = "" Then
                        If Not IsNothing(message.ReplyToMessage) Then
                            If team_members.Contains(message.ReplyToMessage.From.Username) Then
                                a = api.SendTextMessageAsync(message.Chat.Id, "Il giocatore " + message.ReplyToMessage.From.Username + "  è già nel team!").Result
                                Exit Sub
                            End If
                            If checkPlayer(message.ReplyToMessage.From.Username) Then
                                team_members.Add(message.ReplyToMessage.From.Username)
                                a = api.SendTextMessageAsync(message.Chat.Id, "Il giocatore " + message.ReplyToMessage.From.Username + "  è stato aggiunto!").Result
                            Else
                                a = api.SendTextMessageAsync(message.Chat.Id, "Il giocatore " + message.ReplyToMessage.From.Username + " non è un giocatore di Loot bot!").Result
                            End If
                        Else
                            a = api.SendTextMessageAsync(message.Chat.Id, "Rispondi a un messaggio o specifica un username").Result
                        End If
                    Else
                        Dim lines() = member.Split(vbLf)
                        Dim reply As String = ""
                        For Each m In lines
                            If team_members.Contains(m) Then
                                reply += "Già presente: " + m + vbCrLf
                                Continue For
                            End If
                            If checkPlayer(m) Then
                                team_members.Add(m)
                                reply += "Aggiunto: " + m + vbCrLf
                            Else
                                reply += m + " non è un giocatore di Loot bot" + vbCrLf
                            End If
                        Next
                        a = api.SendTextMessageAsync(message.Chat.Id, reply).Result
                    End If
                    saveTeamMembers()
#End Region
                ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/removemember") Then
#Region "/removemember"
                    Dim member = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/removemember" + "@craftlootbot", "/removemember"), "").Trim
                    If member = "" Then
                        If Not IsNothing(message.ReplyToMessage) Then
                            If team_members.Contains(message.ReplyToMessage.From.Username) Then
                                team_members.Remove(message.ReplyToMessage.From.Username)
                                a = api.SendTextMessageAsync(message.Chat.Id, "Il giocatore " + message.ReplyToMessage.From.Username + "  è stato rimosso!").Result
                            Else
                                a = api.SendTextMessageAsync(message.Chat.Id, "Il giocatore " + message.ReplyToMessage.From.Username + " non è del team!").Result
                            End If
                        Else
                            a = api.SendTextMessageAsync(message.Chat.Id, "Rispondi a un messaggio o specifica un username").Result
                        End If
                    Else
                        Dim lines() = member.Split(vbCrLf)
                        Dim reply As String = ""
                        For Each m In lines
                            If team_members.Contains(m) Then
                                team_members.Remove(m)
                                reply += "Rimosso: " + m + vbCrLf
                            Else
                                reply += m + " non è un membro del team" + vbCrLf
                            End If
                        Next
                        a = api.SendTextMessageAsync(message.Chat.Id, reply).Result
                    End If
                    saveTeamMembers()
#End Region
                ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/getmembers") Then
#Region "/getmembers"
                    Dim res As String = ""
                    For Each member In team_members
                        res &= member + vbCrLf
                    Next
                    a = api.SendTextMessageAsync(message.Chat.Id, res).Result
#End Region

                ElseIf message.From.Id = 1265775 AndAlso message.Text.ToLower.StartsWith("/ripristino") Then
#Region "/ripristino"
                    Dim member = message.Text.Replace(If(message.Text.Contains("@craftlootbot"), "/ripristino" + "@craftlootbot", "/ripristino"), "").Trim
                Dim userID As ULong = ULong.Parse(member)
                If Not zaini.TryRemove(userID, "") Then Console.WriteLine("Impossibile eliminare elemento da dictionary zaini")
                    If stati.ContainsKey(userID) Then stati.Remove(userID)
                    If confronti.ContainsKey(userID) Then confronti.Remove(userID)
                    If IO.File.Exists("zaini/" + userID.ToString + ".txt") Then IO.File.Delete("zaini/" + userID.ToString + ".txt")
                    If IO.File.Exists("equip/" + userID.ToString + ".txt") Then IO.File.Delete("equip/" + userID.ToString + ".txt")
                    If IO.File.Exists("prezzi/" + userID.ToString + ".txt") Then IO.File.Delete("prezzi/" + userID.ToString + ".txt")
                    If IO.File.Exists("alias/" + userID.ToString + ".txt") Then IO.File.Delete("alias/" + userID.ToString + ".txt")
                    For Each file In IO.Directory.GetFiles("crafts")
                        Dim info As New IO.FileInfo(file)
                        If info.Name.StartsWith(userID) Then IO.File.Delete(file)
                    Next
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
                    Console.WriteLine("Impossibile aggiornare database")
                    Exit Sub
                End Try
            ElseIf TypeOf e Is ArithmeticException Then
                Try
                    Console.WriteLine(e.Message)
                    Dim a
                    'sendReport(e, message)
                    Dim mess_err As String = ""
                    mess_err += "L'operazione richiesta richiede calcoli troppo elevati per essere soddisfatta. Ti invito a riprovare abbassando la richiesta."
                    a = api.SendTextMessageAsync(message.Chat.Id, mess_err).Result
                    Exit Sub
                Catch
                End Try
            End If
            Try
                Console.WriteLine(e.Message)
                Dim a
                sendReport(e, message)
                Dim mess_err As String = ""
                mess_err += "Si è verificato un errore, riprova tra qualche istante." + vbCrLf
                mess_err += "Una segnalazione è stata inviata automaticamente allo sviluppatore, potrebbe contattarti per avere più informazioni." + vbCrLf
                mess_err += "Se l'errore persiste, può essere utile ripristinare i tuoi dati. Perderai tutti i tuoi dati salvati su craftlootbot, assicurati di poterli reperire nuovamente." + vbCrLf
                mess_err += "Ripristinare?"
                a = api.SendTextMessageAsync(message.Chat.Id, mess_err,,,, , ,, creaErrorInlineKeyboard).Result
            Catch
            End Try
        End Try
#End Region
    End Sub

    'calcolo oggetti lista
    Sub getNeededItemsList(id As Integer, ByRef CraftList As List(Of Item), ByRef zaino As Dictionary(Of Item, Integer), ByRef possiedi As Dictionary(Of Item, Integer), ByRef spesa As Integer, ByRef punti_craft As Integer)

        'Ricorsione sui figlio
        Dim rows As Integer() = requestCraft(id)
        If rows Is Nothing Then Exit Sub
        Dim item As Item
        For Each ids In rows
            item = ItemIds(ids)
            If isCraftable(item.id) Then
                If Not zaino.ContainsKey(item) Then
                    If rarity_value.ContainsKey(item.rarity) Then spesa += rarity_value.Item(item.rarity)
                    If rarity_craft.ContainsKey(item.rarity) Then punti_craft += rarity_craft.Item(item.rarity)
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

    'calcolo oggetti albero
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

    'calcolo oggetti craft
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
                    StampaDebug(item.name + " presente nello zaino con profondità " & prof + 1)
                    zaino.Item(item) -= 1
                    If zaino.Item(item) = 0 Then zaino.Remove(item)
                    If Not possiedi.ContainsKey(item) Then
                        possiedi.Add(item, 1)
                    Else
                        possiedi.Item(item) += 1
                    End If
                    'CraftTree.Add(New KeyValuePair(Of Item, Integer)(item, prof + 1))
                End If
            Else
                CraftTree.Add(New KeyValuePair(Of Item, Integer)(ItemIds.Item(item.id), prof + 1))
            End If
        Next
    End Sub

#Region "Creazione testi"
    'Testo /rinascita
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

    'Testo /lista
    Function getCraftListText(dic As Dictionary(Of Item, Integer), oggetti() As Integer, zaino As Dictionary(Of Item, Integer), ByRef gia_possiedi As Dictionary(Of Item, Integer), spesa As Integer, punti_craft As Integer) As String
        Dim sortedDictionary = dic.OrderByDescending(Of String)(Function(x) x.Key.rarity, New Item.ItemComparer).ThenBy(Function(x) x.Key.name).ToDictionary(Of Item, Integer)(Function(o) o.Key, Function(n) n.Value)
        Dim buildernecessari As New Text.StringBuilder()
        Dim builderposseduti As New Text.StringBuilder()
        builderposseduti.AppendLine()
        If zaino.Count > 0 OrElse gia_possiedi.Count > 0 Then builderposseduti.AppendLine("Già possiedi: ")
        For Each pos In gia_possiedi
            With builderposseduti
                .Append("> ")
                .Append(pos.Key.name)
                .Append(" (" + pos.Key.rarity + ", " + pos.Value.ToString + ")")
                .AppendLine()
            End With
        Next
        Dim intestazione As String = getIntestazioneMessaggio("Lista oggetti necessari per ", oggetti)

        buildernecessari.AppendLine(intestazione)
        Dim tot_necessari As Integer
        Dim necessari As Integer
        For Each it In sortedDictionary
            tot_necessari = sortedDictionary.Item(it.Key)
            With buildernecessari
                '.Append("> ") 'inizio riga
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
                Else
                    .Append("> ") 'inizio riga
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

    'testo /albero
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

    'testo /craft
    Function getcraftText(list As List(Of KeyValuePair(Of Item, Integer)), ByRef possiedi As Dictionary(Of Item, Integer), oggetti() As Integer, Optional tick As Boolean = False) As String
        Dim builder As New Text.StringBuilder
        Dim intestazione As String = getIntestazioneMessaggio("Lista craft per ", oggetti)
        builder.Append(intestazione)
        builder.Append(vbCrLf)
        Dim sorted = list.Where(Function(z) isCraftable(z.Key.id)).OrderByDescending(Function(x) x.Value).ThenBy(Function(p) p.Key.name).Select(Function(o) o).ToList
        Dim newSorted As New Dictionary(Of Item, Integer)
        'TODO: rimuovi "NonCraftare" sia in tick che non.

        If tick Then
            For Each sort In sorted
                'If sort.Key.name.StartsWith("Generatore di Massa") Then StampaDebug("Generatore di massa: " & sort.Value)
                'If sort.Key.name.StartsWith("Bronzo") Then StampaDebug("Bronzo: " & sort.Value)
                'If sort.Key.name.StartsWith("Carica Positiva") Then StampaDebug("Carica Positiva: " & sort.Value)

                If newSorted.ContainsKey(sort.Key) Then
                    newSorted(sort.Key) += 1
                Else
                    newSorted.Add(sort.Key, 1)
                End If
            Next
            For Each craft In newSorted
                Dim NonCraftare As Integer '= If(possiedi.ContainsKey(craft.Key), possiedi(craft.Key), 0)
                Dim volte As Integer = Math.Truncate((craft.Value - NonCraftare) / 3)
                Dim modulo As Integer = (craft.Value - NonCraftare) Mod 3
                If volte > 0 Then
                    builder.Append("`" + "Crea " + craft.Key.name + ",3`" + If(volte > 1, " (x" + volte.ToString + ")", ""))
                    builder.Append(vbCrLf)
                End If
                If modulo <> 0 Then builder.Append("`" + "Crea " + craft.Key.name + If(modulo > 1, "," + modulo.ToString, "") + "`").Append(vbCrLf)
            Next
        Else
            For Each sort In sorted
                If newSorted.ContainsKey(sort.Key) Then
                    newSorted(sort.Key) += 1
                Else
                    newSorted.Add(sort.Key, 1)
                End If
            Next
            For Each craft In newSorted
                Dim NonCraftare As Integer '= If(possiedi.ContainsKey(craft.Key), possiedi(craft.Key), 0)
                Dim volte As Integer = Math.Truncate((craft.Value - NonCraftare) / 3)
                Dim modulo As Integer = (craft.Value - NonCraftare) Mod 3
                For i = 1 To volte
                    builder.Append("Crea " + craft.Key.name + ",3").Append(vbCrLf)
                Next
                If modulo <> 0 Then builder.Append("Crea " + craft.Key.name + If(modulo > 1, "," + modulo.ToString, "")).Append(vbCrLf)
            Next
        End If
        Return builder.ToString
    End Function

    'testo /confronta
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

    'testo /base
    Function getBaseText(rarity As String, zaino As Dictionary(Of Item, Integer)) As String
        Dim builder As New Text.StringBuilder
        rarity = rarity.ToUpper
        Dim intestazione As String = String.Format("Ecco gli oggetti base di rarità {0}:", rarity)
        If Not rarity_value.ContainsKey(rarity) Then
            builder.AppendLine("La rarità specificata non è stata riconosciuta.")
        Else
            builder.AppendLine(intestazione)
        End If
        Dim sublist = ItemIds.Where(Function(x) x.Value.rarity = rarity AndAlso Not isCraftable(x.Value.id)).OrderBy(Function(x) x.Value.name)
        For Each item In sublist
            builder.Append("> ")
            builder.Append(item.Value.name)
            If zaino.Count > 0 Then
                builder.Append(" (")
                builder.Append(If(zaino.ContainsKey(item.Value), zaino.Item(item.Value), 0))
                builder.Append(")")
            End If
            builder.AppendLine()
        Next
        If builder.ToString = intestazione + Environment.NewLine Then builder.AppendLine("Non ci sono oggetti base per questa rarità.")
        Return builder.ToString
    End Function

    'testo /vendi
    Function getVendiText(vendi As Dictionary(Of Item, Integer), zaino As Dictionary(Of Item, Integer), oggetti() As Integer) As String
        Dim builder As New Text.StringBuilder()
        Dim sortedDictionary = vendi.OrderByDescending(Of String)(Function(x) x.Key.rarity, New Item.ItemComparer).ThenBy(Function(x) x.Key.name).ToDictionary(Of Item, Integer)(Function(o) o.Key, Function(n) n.Value)
        If sortedDictionary.Count > 0 Then
            builder.AppendLine(getIntestazioneMessaggio("Ecco la lista degli oggetti non necessari per craftare ", oggetti))
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

    'testo /creanegozi
    Function getNegoziText(zaino As Dictionary(Of Item, Integer), prezzi As Dictionary(Of Item, Integer), filter As Match) As List(Of String)
        Dim res As New List(Of String)
        Dim builder As New Text.StringBuilder("/negozio ")
        Dim i_counter = 1
        Dim Filtro_zaino As Dictionary(Of Item, Integer) = zaino
        If Not IsNothing(filter) Then
            If filter.Groups(1).Value <> "" Then Filtro_zaino = Filtro_zaino.Where(Function(p) p.Key.rarity.Equals(filter.Groups(1).Value)).ToDictionary(Function(x) x.Key, Function(x) x.Value)
            If filter.Groups(2).Value <> "" Then Filtro_zaino = Filtro_zaino.Where(Function(p) isCraftable(p.Key.id)).ToDictionary(Function(x) x.Key, Function(x) x.Value)
        End If
        'zaino.Where(Function(p) prezzoScrigni.ContainsKey(p.Key.rarity) AndAlso Not isCraftable(p.Key.id))
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

    'testo /ricerca plusbot
    Function getRicercaText(parsedDic As Dictionary(Of Item, Integer)) As List(Of String)
        Dim res As New List(Of String)
        Dim builder As New Text.StringBuilder("/ricerca ")
        Dim i_counter = 1
        For Each it In parsedDic
            If i_counter >= 3 Then
                i_counter = 0
                builder.Append(it.Key.name) '.Append(", ")
                If Not builder.ToString.Trim = "/ricerca" Then res.Add(builder.ToString)
                builder.Clear.Append("/ricerca ")
            Else
                builder.Append(it.Key.name).Append(", ")
            End If
            i_counter += 1
        Next
        builder.Remove(builder.Length - 2, 2)
        If Not builder.ToString.Trim = "/ricerc" Then res.Add(builder.ToString)
        Return res
    End Function
#End Region
#End Region

    'controllo se l'item è craftabile
    Function isCraftable(id As Integer) As Boolean
        Try
            Return If(ItemIds.Item(id).craftable = 0, False, True)
        Catch e As Exception
            Return False
        End Try
    End Function

    Function isCraftable(item As Item) As Boolean
        Return isCraftable(item.id)
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
        Dim rex As New Regex("\> ([A-z 0-9òàèéìù'-]+)\(([0-9.]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        Dim nfi As Globalization.NumberFormatInfo = DirectCast(Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone(), Globalization.NumberFormatInfo)
        nfi.NumberGroupSeparator = "."
        nfi.NumberGroupSizes = {3}
        nfi.NumberDecimalDigits = 0

        For Each match As Match In matches
            Try
                quantità.Add(ItemIds.Item(getItemId(match.Groups(1).Value)), (Integer.Parse(match.Groups(2).Value, Globalization.NumberStyles.AllowThousands, nfi)))
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
        If Not isPrezziNegozi(text) Then
            Dim link = checkLink(text)
            If Not IsNothing(link) Then text = getPrezziStringFromURL(link)
        End If
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
        Dim userID = message.From.Id
        Dim hasZaino As String = If(IO.File.Exists("zaini/" + userID.ToString + ".txt"), "Si", "No")
        Dim pending_actions As String = "Nessuna"
        If zaini.ContainsKey(userID) Then pending_actions = "salvataggio zaino "
        If stati.ContainsKey(userID) Then pending_actions += "stato corrente = " + stati(userID).ToString + " "
        If confronti.ContainsKey(userID) Then pending_actions += "confronto"
        With reportBuilder
            .AppendLine("Comando: " + message.Text)
            .AppendLine("Da: " + message.From.Username)
            .AppendLine("ID: " + userID.ToString)
            .AppendLine("Chat: " + [Enum].GetName(GetType(ChatType), message.Chat.Type))
            .AppendLine("Zaino salvato: " + hasZaino)
            .AppendLine("Azioni in corso: " + pending_actions)
            .AppendLine("Metodo: " + If(ex.TargetSite.Name, "Sconosciuto") + ", " + If(ex.Source, "Sconosciuta"))
            .AppendLine("Eccezione: " + ex.Message)
            .AppendLine("Inner Exception: " + If(IsNothing(ex.InnerException), "Nessuna", ex.InnerException.Message))
        End With
        Dim a = api.SendTextMessageAsync(1265775, reportBuilder.ToString,, ,, True).Result
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
        Dim a = api.SendTextMessageAsync(1265775, reportBuilder.ToString,, ,, True).Result
    End Sub

    'Invia messaggi multipli se messaggio troppo lungo
    Function answerLongMessage(result As String, chatID As Long, Optional parse As ParseMode = ParseMode.Markdown, Optional replyMarckup As ReplyMarkups.IReplyMarkup = Nothing) As Message
        Dim a
        If result.Length > 4096 Then
            Dim valid_substring As String
            Do
                Dim substring = result.Substring(0, If(result.Length > 4096, 4096, result.Length))
                Dim lenght = substring.LastIndexOf(Environment.NewLine)
                valid_substring = substring.Substring(0, lenght)
                result = result.Substring(lenght)
                If valid_substring.Trim <> "" And valid_substring <> Environment.NewLine Then
                    a = api.SendTextMessageAsync(chatID, valid_substring, parse).Result
                Else
                    a = api.SendTextMessageAsync(chatID, result, parse).Result
                    result = ""
                End If
            Loop While result <> "" And result <> Environment.NewLine

        Else
            a = api.SendTextMessageAsync(chatID, result, parse,,,,,, replyMarckup).Result
        End If

        Return a
    End Function

    'Invia messaggi multipli se messaggio ha troppe entità
    Function answerTooEntities(result As String, chatID As Long, parse As ParseMode, Optional replymarckup As ReplyMarkups.IReplyMarkup = Nothing) As Message
        Dim lines = result.Split(vbCrLf)
        Dim a
        Dim iterations = 0
        Dim sublines As IEnumerable(Of String)
        Do
            sublines = lines.Skip(99 * iterations).Take(99)
            Dim text As String = String.Join("", sublines.ToArray).ToString
            If text.Length > 4096 Then
                answerLongMessage(sublines(0), chatID, parse, replymarckup)
                text = String.Join("", sublines.Skip(1).Take(99)).ToString
            End If
            a = api.SendTextMessageAsync(chatID, text, parse,,,,,, replymarckup).Result
            iterations += 1
        Loop Until sublines.Count < 99
        Return a
    End Function

    'Controllo oggetti/quantità/alias in input
    Sub checkInputItems(message_text As String, chat_id As Long, user_id As Long, comando As String, ByRef item_ids As List(Of Integer), Optional checkCraftable As Boolean = True)
        Dim items_string = message_text.Replace(If(message_text.ToLower.Contains("@craftlootbot"), comando + "@craftlootbot", comando), "")
        Dim items = items_string.Split(",").Where(Function(p) p <> "").ToList
        Dim a
        If items.Count = 0 Then
            If comando <> "/creanegozi" Then a = api.SendTextMessageAsync(chat_id, "Inserisci l'oggetto o gli oggetti che vuoi ottenere").Result
            Exit Sub
        End If
        For Each i In items
            Dim x = GetAliasValue(user_id, i.Trim).Trim
            If x <> i.Trim Then
                checkInputItems(x, chat_id, user_id, comando, item_ids, checkCraftable)
            Else
                'controllo e gestisco craft multipli
                Dim split() = x.Split(":")
                Dim name As String = split(0)
                Dim quantity As Integer = 1
                If split.Length > 2 Then
                    a = api.SendTextMessageAsync(chat_id, "L'oggetto " + name + " non è inserito nel formato corretto, verrà saltato.").Result
                    Continue For
                End If
                If split.Length = 2 Then
                    If Not Integer.TryParse(split(1), quantity) Then
                        a = api.SendTextMessageAsync(chat_id, "La quantità " + split(1) + " è troppo elevata, l'oggetto " + name + " verrà saltato.").Result
                        Continue For
                    End If
                End If

                'Check prima dell'aggiunta
                Dim temp_id = getItemId(name)
                If temp_id = -1 Then
                    a = api.SendTextMessageAsync(chat_id, "L'oggetto " + name + " non è stato riconosciuto, verrà saltato.").Result
                    Continue For
                End If
                If checkCraftable Then
                    If Not isCraftable(temp_id) Then
                        Dim e = api.SendTextMessageAsync(chat_id, "L'oggetto " + name + " non è craftabile, verrà saltato.").Result
                        Continue For
                    End If
                End If
                For y = 1 To quantity
                    item_ids.Add(temp_id)
                Next
            End If
        Next
        If item_ids.Count = 0 Then
            a = api.SendTextMessageAsync(chat_id, "Nessuno degli oggetti inseriti è valido, riprova.").Result
        End If

    End Sub

    'restituisco classifica utilizzo bot
    Sub StampaClassificaUtilizzoBot()
        Dim builder As New Text.StringBuilder()
        builder.AppendLine()
        Dim i = 1
        For Each user In PersonalStats.OrderByDescending(Function(p) p.Value).Take(50)
            builder.AppendLine(i.ToString + "° " + user.Key + ": " + user.Value.ToString)
            i += 1
        Next
        answerLongMessage("La classifica è: " + builder.ToString, 1265775, ParseMode.Html)
    End Sub

    'creazione testo XML
    Function ItemToXML(id As Integer) As String
        Dim it = ItemIds(id)
        Dim builder As New Text.StringBuilder
        Dim prop() = GetType(Item).GetProperties
        builder.Append("<item name=""")
        builder.Append(it.name).Append("""" + ">")
        If isCraftable(id) Then
            builder.AppendLine().AppendLine("<required_items>")
            Dim craft = requestCraft(id)
            For Each c In craft
                builder.Append(ItemToXML(c))
            Next
            builder.AppendLine("</required_items>")
        End If
        builder.AppendLine("</item>")
        Return builder.ToString
    End Function

    'creazione testo HTML
    Function ItemToHTML(id As Integer, ByRef counter As Integer) As String
        Dim it = ItemIds(id)
        Dim input As String = "<input type='checkbox' style='display: none' id=CBXX>"
        Dim label As String = "<label FOR_INPUT CLASS_NAME>ITEM_NAME</label>"
        Dim builder As New Text.StringBuilder
        If isCraftable(id) Then
            builder.AppendLine(input.Replace("XX", counter.ToString))
            builder.AppendLine(label.Replace("FOR_INPUT", "for=CB" & counter.ToString).Replace("ITEM_NAME", it.name).Replace("CLASS_NAME", ""))
            builder.AppendLine("<div>")
            Dim craft = requestCraft(id)
            For Each c In craft
                counter += 1
                builder.Append(ItemToHTML(c, counter))
            Next
            builder.AppendLine("</div>")
        Else
            builder.AppendLine(label.Replace("FOR_INPUT", "").Replace("ITEM_NAME", it.name).Replace("CLASS_NAME", "class='leaf'"))
        End If
        Return builder.ToString
    End Function
End Module
