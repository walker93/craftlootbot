Module Statistiche
    Public stats_file As String = "stats.dat"
    Public personal_file As String = "personal_stats.dat"
    Public Inlinehistory_file As String = "inline_recents.dat"
    Public stat_timeout As Integer = 20 'minuti, tra un salvataggio e l'altro delle statistiche
    Public stats As New Dictionary(Of String, Tuple(Of String, ULong))
    '                                 Chiave, (Testo stampato, valore)
    Public delta_stats As New Dictionary(Of String, Tuple(Of String, Integer))
    Public PersonalStats As New Dictionary(Of String, ULong) 'Conteggio usaggio utenti
    Public inline_history As New Dictionary(Of Integer, Queue(Of String)) 'ricerche inline recenti degli utenti
    'Inizializzo statistiche a 0, prima di leggerle dal file.
    Sub init_stats()
        If Not IO.File.Exists(stats_file) Then IO.File.WriteAllText(stats_file, "")
        If Not IO.File.Exists(personal_file) Then IO.File.WriteAllText(personal_file, "")
        stats.Add("lista", New Tuple(Of String, ULong)("Liste inviate", 0))
        stats.Add("albero", New Tuple(Of String, ULong)("Alberi generati", 0))
        stats.Add("rinascita", New Tuple(Of String, ULong)("Liste scambi rinascita effettuate", 0))
        stats.Add("inline", New Tuple(Of String, ULong)("Comandi inline inviati", 0))
        stats.Add("confronta", New Tuple(Of String, ULong)("Confronti eseguiti", 0))
        stats.Add("craft", New Tuple(Of String, ULong)("Liste Craft inviate", 0))
        stats.Add("base", New Tuple(Of String, ULong)("Liste oggetti base inviate", 0))
        stats.Add("vendi", New Tuple(Of String, ULong)("Liste oggetti non necessari", 0))
        stats.Add("creanegozi", New Tuple(Of String, ULong)("Creazioni di negozi ricevute", 0))
        stats.Add("info", New Tuple(Of String, ULong)("Informazioni oggetti inviate", 0))
        stats.Add("stima", New Tuple(Of String, ULong)("Stime di oggetti valutate", 0))
        stats.Add("xml", New Tuple(Of String, ULong)("Alberi in formato XML inviati", 0))
        stats.Add("html", New Tuple(Of String, ULong)("Alberi in formato HTML inviati", 0))
        stats.Add("totale", New Tuple(Of String, ULong)("Totale comandi processati", 0))
        stats.Add("zaini", New Tuple(Of String, ULong)("Zaini attualmente salvati", 0))

        delta_stats = stats.ToList.ToDictionary(Function(p) p.Key, Function(p) New Tuple(Of String, Integer)(p.Value.Item1, p.Value.Item2))
        delta_stats("zaini") = New Tuple(Of String, Integer)("Nuovi zaini salvati", 0)
    End Sub

    'Leggo statistiche da file
    Sub read_stats()
        Dim stats_lines As String() = IO.File.ReadAllLines(stats_file)
        For Each line In stats_lines
            Dim split As String() = line.Split("=")
            If stats.ContainsKey(split(0).ToLower) Then
                stats.Item(split(0).ToLower) = New Tuple(Of String, ULong)(stats.Item(split(0).ToLower).Item1, split(1))
            End If
            StampaDebug("lettura statistica: " + line)
        Next

        'leggo statistiche personali
        Dim personal_lines As String() = IO.File.ReadAllLines(personal_file)
        For Each line In personal_lines
            Dim split As String() = line.Split("=")
            PersonalStats.Add(split(0), split(1))
        Next
    End Sub
    Sub read_stats(ByRef stats As Dictionary(Of String, Tuple(Of String, Integer)), Optional def As Boolean = False)
        Dim stats_lines As String() = IO.File.ReadAllLines(stats_file)
        For Each line In stats_lines
            Dim split As String() = line.Split("=")
            If stats.ContainsKey(split(0).ToLower) Then
                stats.Item(split(0).ToLower) = New Tuple(Of String, Integer)(stats.Item(split(0).ToLower).Item1, If(def, 0, split(1)))
            End If
        Next
    End Sub

    'Salva le statistiche su file periodicamente
    Sub salvaStats()
        While True
            Threading.Thread.Sleep(stat_timeout * 60 * 1000) 'ogni X minuti, X è salvato nelle impostazioni
            Dim settings As String() = IO.File.ReadAllLines(settings_file)
            For Each line In settings
                Dim split As String() = line.Split("=")
                Select Case split(0)
                    Case "stat_timeout"
                        stat_timeout = split(1)
                End Select
            Next
            'Leggo statistiche dal file
            read_stats(delta_stats)
            For i = 0 To delta_stats.Count - 1
                Dim key = delta_stats.Keys(i)
                delta_stats.Item(key) = New Tuple(Of String, Integer)(stats.Values(i).Item1, stats.Values(i).Item2 - delta_stats.Values(i).Item2)
            Next
            'Sovrascrivo vecchie statistiche nel file
            Dim builder As New Text.StringBuilder
            stats.Item("zaini") = New Tuple(Of String, ULong)(stats.Item("zaini").Item1, IO.Directory.GetFiles("zaini/").Length)
            For Each stat In stats
                builder.Append(stat.Key + "=" + stat.Value.Item2.ToString).Append(vbCrLf)
            Next
            IO.File.WriteAllText(stats_file, builder.ToString)
            StampaDebug("Salvate statistiche!")

            'salvo statistiche personali
            Dim personal_builder As New Text.StringBuilder
            For Each us In PersonalStats
                personal_builder.AppendLine(us.Key + "=" + us.Value.ToString)
            Next
            IO.File.WriteAllText(personal_file, personal_builder.ToString)
            StampaDebug("Salvate statistiche personali!")
        End While
    End Sub

    'Incremento statistiche tramite comando inviato
    Sub aggiornastats(text As String, userID As String)
        Dim comando As String = text.Split(" ")(0).Trim.ToLower.Replace("/", "")
        If stats.ContainsKey(comando) Then
            stats.Item(comando) = New Tuple(Of String, ULong)(stats(comando).Item1, stats(comando).Item2 + 1)
            stats("totale") = New Tuple(Of String, ULong)(stats("totale").Item1, stats("totale").Item2 + 1)

            'Conteggio statistiche personali
            If PersonalStats.ContainsKey(userID) Then
                PersonalStats(userID) += 1
            Else
                PersonalStats.Add(userID, 1)
            End If
        End If
    End Sub

End Module
