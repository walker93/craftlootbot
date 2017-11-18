Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.InlineKeyboardButtons

Module Keyboards
    'Crea tastiera per salvataggio zaino
    Function creaZainoKeyboard() As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboard As New ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboardbuttons()() As KeyboardButton = New KeyboardButton()() {New KeyboardButton() {("Salva")}, New KeyboardButton() {("Annulla")}}
        'Dim button1 As New KeyboardButton("Salva")
        'Dim button2 As New KeyboardButton("Annulla")
        'Dim row1() As KeyboardButton
        'Dim row2() As KeyboardButton

        'row1.Add(button1)
        'row2.Add(button2)
        'keyboardbuttons.Add(row1)
        'keyboardbuttons.Add(row2)
        keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera per confronta
    Function creaConfrontaKeyboard(Optional withzaino As Boolean = False) As ReplyMarkups.ReplyKeyboardMarkup
        Dim keyboard As New ReplyMarkups.ReplyKeyboardMarkup
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
        keyboard.Keyboard = keyboardbuttons
        keyboard.OneTimeKeyboard = True
        keyboard.ResizeKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function

    'Crea tastiera per salvataggio zaino inline
    Function creaInlineKeyboard(query_text As String) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim button1 As New SwitchInlineButton("Torna Inline", query_text)
        Dim row1() As SwitchInlineButton
        'button1.SwitchInlineQuery =
        'button1.CallbackData = Nothing
        row1.Add(button1)
        keyboardbuttons.Add(row1)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera per messaggio errore
    Function creaErrorInlineKeyboard() As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim button1 As New CallbackInlineButton("CANCELLA DATI", "err_reset")
        Dim button2 As New CallbackInlineButton("Non ripristinare i dati", "DelMess")
        Dim row1() As CallbackInlineButton
        Dim row2() As CallbackInlineButton
        row1.Add(button1)
        row2.Add(button2)
        keyboardbuttons.Add(row1)
        keyboardbuttons.Add(row2)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera per /craft
    Function creaCraftKeyboard(ids() As Integer, UserID As Long) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)
        Dim filename = createfile(String.Join("_", ids), UserID)
        Dim button As New CallbackInlineButton("File di testo", "craftF_" + filename)
        Dim button2 As New CallbackInlineButton("Messaggio", "craftM_" + filename)

        row.Add(button)
        row.Add(button2)

        keyboardbuttons.Add(row.ToArray)

        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera con oggetti per /info
    Function creaInfoKeyboard(ids() As Integer) As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim row As New List(Of InlineKeyboardButton)
        'Dim rows As New List(Of InlineKeyboardButton())

        For Each i In ids
            Dim button As New CallbackInlineButton(ItemIds(i).name, "info_" + i.ToString)
            row.Add(button)
            If row.Count = 2 Then
                keyboardbuttons.Add(row.ToArray)
                row.Clear()
            End If
            If ids.Last = i Then keyboardbuttons.Add(row.ToArray)
        Next
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

    'Crea tastiera vuota
    Function creaNULLKeyboard() As ReplyMarkups.ReplyKeyboardRemove
        Dim keyboard As New ReplyMarkups.ReplyKeyboardRemove
        keyboard.RemoveKeyboard = True
        keyboard.Selective = True
        Return keyboard
    End Function
End Module
