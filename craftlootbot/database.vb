Imports System.Net
Imports Newtonsoft
Module Database
    Sub initialize_Dictionary()
        While True
            Try
                download_items()
                download_crafts()
            Catch e As AggregateException
                Console.WriteLine("Errori durante l'aggiornamento (Download): " + e.InnerException.Message)
            Catch e As Exception
                Console.WriteLine("Errori durante l'aggiornamento (Download): " + e.Message)
            End Try
            Try
                Leggo_Items()
                Leggo_Crafts()
                Threading.Thread.Sleep(update_db_timeout * 60 * 60 * 1000)
            Catch e As AggregateException
                Console.WriteLine("Errori durante l'aggiornamento (Lettura): " + e.InnerException.Message)
                Threading.Thread.Sleep(60 * 1000)
            Catch e As Exception
                Console.WriteLine("Errori durante l'aggiornamento (Lettura): " + e.Message)
                Threading.Thread.Sleep(60 * 1000)
            End Try
        End While
    End Sub

    Function download_items() As String
        Dim handler As New Http.HttpClientHandler
        If handler.SupportsAutomaticDecompression() Then
            handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
        End If
        Dim client As New Http.HttpClient(handler)
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
        Dim res
        res = client.GetStringAsync(ITEM_URL).Result
        Dim jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
        IO.File.WriteAllText("items.json", res)
        Return "Scaricati Items."
    End Function
    Function download_crafts() As String
        Dim handler As New Http.HttpClientHandler
        If handler.SupportsAutomaticDecompression() Then
            handler.AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
        End If
        Dim client As New Http.HttpClient(handler)
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json")
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate")
        Dim res
        res = client.GetStringAsync(CRAFT_URL + "id").Result
        Dim jsonres = Json.JsonConvert.DeserializeObject(Of CraftTable)(res)
        IO.File.WriteAllText("crafts.json", res)
        Return "Scaricati Crafts."
    End Function

    Function Leggo_Items() As String
        Console.WriteLine("Aggiorno Items")
        Dim res
        Dim jsonres
        ItemIds.Clear()
        'aggiungo rifugi
        res = getRifugiItemsJSON()
        jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
        Dim rif() As Item = jsonres.res
        For Each it As Item In rif
            ItemIds.Add(it.id, it)
        Next
        'aggiungo halloween
        res = getHalloweenItemsJSON()
        jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
        Dim hall() As Item = jsonres.res
        For Each it As Item In hall
            ItemIds.Add(it.id, it)
        Next
        'Aggiorno items
        res = IO.File.ReadAllText("items.json")
        jsonres = Json.JsonConvert.DeserializeObject(Of ItemResponse)(res)
        Dim res_items = jsonres.res
        For Each it As Item In res_items
            ItemIds.Add(it.id, it)
        Next
        Console.WriteLine("Numero di oggetti: " + ItemIds.Count.ToString)
        Console.WriteLine("Terminato aggiornamento")
        Return "Numero di oggetti: " + ItemIds.Count.ToString
    End Function
    Function Leggo_Crafts() As String
        Console.WriteLine("Aggiorno Crats")
        Dim res
        Dim jsonres
        CraftIds.Clear()
        'aggiungo rifugi
        res = getRifugiCraftsJSON()
        jsonres = Json.JsonConvert.DeserializeObject(Of CraftTable)(res)
        Dim rif() As IDCraft = jsonres.res
        For Each it As IDCraft In rif
            CraftIds.Add(it.id, it)
        Next
        'Aggiorno crafts
        res = IO.File.ReadAllText("crafts.json")
        jsonres = Json.JsonConvert.DeserializeObject(Of CraftTable)(res)
        Dim res_items = jsonres.res
        For Each it As IDCraft In res_items
            CraftIds.Add(it.material_result, it)
        Next
        Console.WriteLine("Numero di crafts: " + CraftIds.Count.ToString)
        Console.WriteLine("Terminato aggiornamento")
        Return "Numero di crafts: " + CraftIds.Count.ToString
    End Function

    Sub Salvo_Crafts()
        Dim table As New CraftTable With {.code = 200, .res = CraftIds.Values.ToArray}
        Dim j = Json.JsonConvert.SerializeObject(table)
        IO.File.WriteAllText("crafts.json", j)
    End Sub
End Module
