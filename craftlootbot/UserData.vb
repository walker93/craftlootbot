

Module UserData

    'Salva Equipaggiamento
    Sub saveEquip(items As List(Of Integer), UserID As Integer)
        Dim text As New Text.StringBuilder
        Dim path = "equip/" + UserID.ToString + ".txt"
        For Each id In items
            Dim it = ItemIds(id)
            Dim type = it.getEquipType
            text.Append(type.ToString).Append(":").AppendLine(id)
        Next
        IO.File.WriteAllText(path, text.ToString)
    End Sub

    'Ottiene equipaggiamento
    Function getEquip(UserID As Long) As Integer()
        Dim path = "equip/" + UserID.ToString + ".txt"
        Dim lines = IO.File.ReadAllLines(path)
        Dim res As Integer()
        For Each line In lines
            res.Add(line.Split(":")(1))
        Next
        Return res
    End Function

    'Controllo se possiede equip
    Function HasEquip(UserID As Long) As Boolean
        Dim path = "equip/" + UserID.ToString + ".txt"
        If IO.File.Exists(path) Then Return True
        Return False
    End Function

    'controllo se possiede zaino
    Function HasZaino(UserID As Long) As Boolean
        Dim path = "zaini/" + UserID.ToString + ".txt"
        If IO.File.Exists(path) Then Return True
        Return False
    End Function

    'ottengo zaino e aggiungo l'equipaggiamento
    Function getZaino(UserID As Long) As Dictionary(Of Item, Integer)
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

    'ottengo gli alias personali dell'utente
    Function getPersonalAlias(UserID As Long) As Dictionary(Of String, String)
        Dim alias_path As String = "alias/" + UserID.ToString + ".txt"
        Dim PersonalAlias As New Dictionary(Of String, String)
        If IO.File.Exists(alias_path) Then
            Dim lines = IO.File.ReadAllLines(alias_path)
            For Each line In lines
                PersonalAlias.Add(line.Split("==")(0), line.Split("==")(2))
            Next
        End If
        Return PersonalAlias
    End Function

    'Aggiunge un alias personale 
    Function AddPersonalAlias(UserID As Long, keyword As String, items As String) As String
        Dim alias_path As String = "alias/" + UserID.ToString + ".txt"
        Dim PersonalAlias As Dictionary(Of String, String) = getPersonalAlias(UserID)
        Dim GlobalAlias = getGlobalAlias()
        Dim result As String = "OK"
        If PersonalAlias.ContainsKey(keyword) Then result = "Un alias con lo stesso nome è già presente. Scegline un altro." : Return result
        If GlobalAlias.ContainsKey(keyword) Then result = "Esiste un Alias globale con lo stesso nome. Scegline un altro." : Return result
        IO.File.AppendAllLines(alias_path, {keyword + "==" + items})
        Return result
    End Function

    'Rimuove un alias personale 
    Function DeletePersonalAlias(UserID As Long, keyword As String) As String
        Dim alias_path As String = "alias/" + UserID.ToString + ".txt"
        Dim PersonalAlias As Dictionary(Of String, String) = getPersonalAlias(UserID)
        Dim result As String = "OK"
        If Not PersonalAlias.ContainsKey(keyword) Then result = "L'alias specificato non è presente" : Return result
        PersonalAlias.Remove(keyword)
        Dim contents() As String
        For Each PA In PersonalAlias
            contents.Add(PA.Key + "==" + PA.Value)
        Next
        IO.File.WriteAllLines(alias_path, contents)
        Return result
    End Function

    'carica cronologia inline
    Sub read_inlineHistory()
        If Not IO.File.Exists(Inlinehistory_file) Then IO.File.WriteAllText(Inlinehistory_file, "")
        Dim Global_history As String() = IO.File.ReadAllLines(Inlinehistory_file)
        For Each line In Global_history
            Dim User_ID As Integer = Integer.Parse(line.Split("=")(0))
            Dim item_ids() As String = line.Split("=")(1).Split(";")
            Dim q As New Queue(Of String)(item_ids)

            inline_history.Add(User_ID, q)
            StampaDebug("lettura cronologia inline: " + line)
        Next
    End Sub

    'salva cronologia inline
    Sub salva_inlineHistory()
        While True
            Threading.Thread.Sleep(stat_timeout * 60 * 1000) 'ogni X minuti, X è salvato nelle impostazioni
            Dim history_builder As New Text.StringBuilder
            For Each user In inline_history
                Dim ids_string As String = ""
                ids_string = String.Join(";", user.Value)
                history_builder.AppendLine(user.Key.ToString + "=" + ids_string)
            Next
            IO.File.WriteAllText(Inlinehistory_file, history_builder.ToString)
            StampaDebug("Salvata cronologia inline!")
        End While
    End Sub

    'Thread Cancellazione Vecchi Dati
    Sub vecchizaini()
        While True
            Try
                For Each file In IO.Directory.GetFiles("zaini/")
                    If DateDiff(DateInterval.Day, IO.File.GetLastWriteTime(file), Date.Now) > olderZaini_limit Then
                        IO.File.Delete(file)
                        StampaDebug(file + " Cancellato!")
                    End If
                Next
                For Each file In IO.Directory.GetFiles("crafts/")
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
