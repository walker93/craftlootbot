Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.ReplyMarkups

Module Keyboards
    'Crea tastiera per salvataggio zaino
    Function creaZainoKeyboard() As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboardbuttons()() As KeyboardButton = New KeyboardButton()() {New KeyboardButton() {("Salva")}, New KeyboardButton() {("Annulla")}}
        Dim keyboard As New ReplyMarkups.ReplyKeyboardMarkup(keyboardbuttons)

        'keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera per confronta
    Function creaConfrontaKeyboard(Optional withzaino As Boolean = False) As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboardbuttons()() As KeyboardButton
        Dim button1 As New KeyboardButton("Annulla")
        Dim row1() As KeyboardButton
        row1.Add(button1)
        If withzaino Then
            Dim button2 As New KeyboardButton("Utilizza il mio zaino")
            Dim row2() As KeyboardButton
            row2.Add(button2)
            keyboardbuttons.Add(row2)
        End If
        keyboardbuttons.Add(row1)
        Dim keyboard As New ReplyKeyboardMarkup(keyboardbuttons)
        'keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera inline
    Function creaInlineKeyboard(query_text As String) As InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim button1 As New InlineKeyboardButton("Torna Inline") With {.SwitchInlineQuery = query_text}

        Dim row1() As InlineKeyboardButton
        row1.Add(button1)
        keyboardbuttons.Add(row1)

        Dim keyboard As New InlineKeyboardMarkup(keyboardbuttons)
        Return keyboard
    End Function

    'Crea tastiera per messaggio errore
    Function creaErrorInlineKeyboard() As InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim button1 As New InlineKeyboardButton("CANCELLA DATI") With {.CallbackData = "err_reset"}
        Dim button2 As New InlineKeyboardButton("Non ripristinare i dati") With {.CallbackData = "DelMess"}
        Dim row1() As InlineKeyboardButton
        Dim row2() As InlineKeyboardButton
        row1.Add(button1)
        row2.Add(button2)
        keyboardbuttons.Add(row1)
        keyboardbuttons.Add(row2)

        Dim keyboard As New InlineKeyboardMarkup(keyboardbuttons)
        Return keyboard
    End Function

    'Crea tastiera per /craft
    Function creaCraftKeyboard(ids() As Integer, UserID As Long) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)
        Dim filename = createfile(String.Join("_", ids), UserID)
        Dim button As New InlineKeyboardButton("File di testo") With {.CallbackData = "craftF_" + filename}
        Dim button2 As New InlineKeyboardButton("Messaggio") With {.CallbackData = "craftM_" + filename}

        row.Add(button)
        row.Add(button2)

        keyboardbuttons.Add(row.ToArray)

        Dim keyboard As New InlineKeyboardMarkup(keyboardbuttons)
        Return keyboard
    End Function

    'Crea tastiera con oggetti per /info
    Function creaInfoKeyboard(ids() As Integer) As InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)


        For Each i In ids
            Dim button As New InlineKeyboardButton(ItemIds(i).name) With {.CallbackData = "info_" + i.ToString}
            row.Add(button)
            If row.Count = 2 Then
                keyboardbuttons.Add(row.ToArray)
                row.Clear()
            End If
            If ids.Last = i Then keyboardbuttons.Add(row.ToArray)
        Next

        Dim keyboard As New InlineKeyboardMarkup(keyboardbuttons)
        Return keyboard
    End Function

    'Crea tastiera vuota
    Function creaNULLKeyboard() As ReplyKeyboardRemove
        Dim keyboard As New ReplyKeyboardRemove
        keyboard.Selective = True
        Return keyboard
    End Function
End Module
