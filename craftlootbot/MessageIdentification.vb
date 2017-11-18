Module MessageIdentification

    'controllo se il testo matcha uno zaino
    Function isZaino(text As String) As Boolean
        Dim rex As New Regex("\> ([A-z 0-9òàèéìù'-]+)\(([0-9]+)\)")
        Return rex.IsMatch(text)
    End Function

    'controllo se il testo matcha un elenco cerco inline
    Function isInlineCerco(text As String) As Boolean
        Dim rex As New Regex("\> ([0-9]+) di ([A-z òàèéìù'-]+) \(([A-Z]+)\)")
        Return rex.IsMatch(text)
    End Function

    'controllo se il testo match un elenco prezzi negozio
    Function isPrezziNegozi(text As String) As Boolean
        Dim rex As New Regex("^([A-z 0-9òàèéìù'-]+):([0-9]+)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    'Controllo se il testo matcha un'apertura scrigni di loot
    Function isAperturaScrigno(text As String) As Boolean
        Dim rex As New Regex("\> ([0-9]+)x ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    'Controllo se il testo matcha una /lista o un /vendi di craftbot
    Function isListaoVendi(text As String) As Boolean
        Dim rex As New Regex("\> (([0-9]+) su )?([0-9]+) di ([A-z 0-9òàèéìù'-]+) \(([A-Z]+)\)")
        Dim matches As MatchCollection = rex.Matches(text)
        Return rex.IsMatch(text)
    End Function

    'Controllo se il testo matcha la stanza della gabbia
    Function isDungeon(text As String) As Boolean
        If text.Contains("senti tremare il pavimento ed una gabbia ti circonda lentamente") Then
            If text.Contains(" _ ") Then
                Return True
            End If
        End If
        Return False
    End Function

    'Controllo se il testo matcha l'ispezione con gnomo
    Function isIspezione(text As String) As Boolean
        If text.Contains("Sul portone del rifugio") Then
            If text.Contains(" _ ") Then
                Return True
            End If
        End If
        Return False
    End Function

End Module
