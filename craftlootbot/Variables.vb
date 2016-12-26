Module Variables
    Public CRAFT_URL As String
    Public ITEM_URL As String
    Public PLAYER_URL As String
    Public BASE_URL As String
    Public rarity_value As New Dictionary(Of String, Integer)
    Public settings_file As String = "settings.dat"
    Public debug As Boolean
    Public update_db_timeout As Integer = 3 'ore
    Public inline_message_row_limit As Integer = 15 'righe massime messaggio risposta query inline

    'Legge da file impostazioni e inizializza variabili
    Sub initializeVariables()
        If Not IO.File.Exists(settings_file) Then
            IO.File.WriteAllText(settings_file, "base_url=http://fenixweb.net:3300/api/v1/")
        End If
        Dim settings As String() = IO.File.ReadAllLines(settings_file)
        For Each line In settings
            Dim split As String() = line.Split("=")
            Select Case split(0)
                Case "base_url"
                    BASE_URL = split(1)
                Case "debug"
                    debug = If(split(1) = 1, True, False)
                Case "stat_timeout"
                    stat_timeout = split(1) 'se debug ogni 2 minuti
                Case "update_db_timeout"
                    update_db_timeout = split(1)
                Case "inline_message_row_limit"
                    inline_message_row_limit = split(1)
            End Select
        Next
        ITEM_URL = BASE_URL + "items/"
        CRAFT_URL = BASE_URL + "crafts/"
        PLAYER_URL = BASE_URL + "players/"
        token.token = If(debug, debug_token, release_token)
        init_rarity()
        init_help()

        init_stats()
        read_stats()

    End Sub

    'Inizializza valori craft rarità
    Sub init_rarity()
        rarity_value.Add("C", 0)
        rarity_value.Add("NC", 0)
        rarity_value.Add("R", 0)
        rarity_value.Add("UR", 500)
        rarity_value.Add("L", 750)
        rarity_value.Add("E", 1000)
        rarity_value.Add("UE", 10000)
        rarity_value.Add("S", 0)
        rarity_value.Add("U", 0)
        rarity_value.Add("X", 100000)
    End Sub

    'restituisce url craft per un determinato ID
    Function getCraftUrl(id As Integer)
        Return CRAFT_URL + id.ToString + "/needed"
    End Function

    'restituisce url info per un determinato ID
    Function getItemUrl(id As Integer)
        Return ITEM_URL + id.ToString
    End Function
    'restituisce url info per un determinato nome
    Function getItemUrl(name As String)
        Return ITEM_URL + name
    End Function

    'restituisce url giocatore per un determinato username
    Function getPlayerUrl(User As String) As String
        Return PLAYER_URL + User
    End Function

End Module
