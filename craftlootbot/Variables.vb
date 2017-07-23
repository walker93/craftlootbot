Public Module Variables
    Public CRAFT_URL As String
    Public ITEM_URL As String
    Public PLAYER_URL As String
    Public BASE_URL As String
    Public rarity_value As New Dictionary(Of String, Integer)
    Public rarity_craft As New Dictionary(Of String, Integer)
    Public settings_file As String = "settings.dat"
    Public debug As Boolean
    Public update_db_timeout As Integer = 3 'ore
    Public inline_message_row_limit As Integer = 15 'righe massime messaggio risposta query inline
    Public rifugiMatch() As String
    Public prezzoScrigni As New Dictionary(Of String, Integer)
    Public flush As Boolean = True
    Public CraftIds As New Dictionary(Of Integer, IDCraft)
    Public olderZaini_limit As Integer = 30 'giorni
    Public inline_history_limit As Integer = 20 'numero massimo di cronologia inline da salvare per ogni utente
    Public alphabet As String() = {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"}
    Public Italian_dictionary As String()
    Public team_members As New List(Of String)

    Public Enum Equip
        ARMA = 0
        ARMATURA = 1
        SCUDO = 2
        ARTIGLI = 3
        SELLA = 4

        NONE = -1
    End Enum

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
                    stat_timeout = split(1)
                Case "update_db_timeout"
                    update_db_timeout = split(1)
                Case "inline_message_row_limit"
                    inline_message_row_limit = split(1)
                Case "flush"
                    flush = If(split(1) = 1, True, False)
                Case "olderzaini_limit"
                    olderZaini_limit = split(1)
                Case "inline_history_limit"
                    inline_history_limit = split(1)
            End Select
        Next
        ITEM_URL = BASE_URL + "items/"
        CRAFT_URL = BASE_URL + "crafts/"
        PLAYER_URL = BASE_URL + "players/"
        token.token = If(debug, debug_token, release_token)
        init_rarity_value()
        init_rarity_craft()
        init_help()

        init_stats()
        read_stats()

        read_inlineHistory()

        init_rifugiMatch()
        init_prezzoscrigni()

        init_teamMembers()

        Italian_dictionary = IO.File.ReadAllText("dictionary.txt").Split(" "c, vbLf)
    End Sub

    Sub init_prezzoscrigni()
        prezzoScrigni.Add("C", 450)
        prezzoScrigni.Add("NC", 750)
        prezzoScrigni.Add("R", 1500)
        prezzoScrigni.Add("UR", 2500)
        prezzoScrigni.Add("L", 5000)
        prezzoScrigni.Add("E", 10000)
    End Sub

    'inizializza punti craft rarità
    Sub init_rarity_craft()
        rarity_craft.Add("UR", 2)
        rarity_craft.Add("L", 3)
        rarity_craft.Add("E", 5)
        rarity_craft.Add("UE", 25)
        rarity_craft.Add("U", 35)
        rarity_craft.Add("X", 50)
    End Sub

    'Inizializza costo craft rarità
    Sub init_rarity_value()
        rarity_value.Add("C", 0)
        rarity_value.Add("NC", 0)
        rarity_value.Add("R", 0)
        rarity_value.Add("UR", 500)
        rarity_value.Add("L", 750)
        rarity_value.Add("E", 1000)
        rarity_value.Add("UE", 10000)
        rarity_value.Add("S", 0)
        rarity_value.Add("U", 50000)
        rarity_value.Add("X", 100000)
        rarity_value.Add("RF2", 1500)
        rarity_value.Add("RF3", 3000)
        rarity_value.Add("RF4", 4500)
        rarity_value.Add("RF5", 7000)
        rarity_value.Add("RF6", 10000)
    End Sub

    Sub init_rifugiMatch()
        rifugiMatch.Add("rif")
        rifugiMatch.Add("rifu")
        rifugiMatch.Add("rifug")
        rifugiMatch.Add("rifugi")
        rifugiMatch.Add("rifugio")
        rifugiMatch.Add("rifugio ")
        rifugiMatch.Add("rifugio 2")
        rifugiMatch.Add("rifugio 3")
        rifugiMatch.Add("rifugio 4")
        rifugiMatch.Add("rifugio 5")
        rifugiMatch.Add("rifugio 6")
    End Sub

    Sub init_teamMembers()
        Dim team_file = "team.dat"
        If Not IO.File.Exists(team_file) Then IO.File.WriteAllText(team_file, "")
        team_members.AddRange(IO.File.ReadAllLines(team_file))
    End Sub

#Region "Deprecated"
    ''restituisce url craft per un determinato ID
    'Function getCraftUrl(id As Integer)
    '    Return CRAFT_URL + id.ToString + "/needed"
    'End Function

    ''restituisce url info per un determinato ID
    'Function getItemUrl(id As Integer)
    '    Return ITEM_URL + id.ToString
    'End Function
    ''restituisce url info per un determinato nome
    'Function getItemUrl(name As String)
    '    Return ITEM_URL + name
    'End Function
#End Region

    'restituisce url giocatore per un determinato username
    Function getPlayerUrl(User As String) As String
        Return PLAYER_URL + User
    End Function

End Module
