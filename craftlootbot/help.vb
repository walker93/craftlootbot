Imports System.Text
Imports Telegram.Bot.Types

Module help
    'Inizializza Testo help
    Public help_builder As New StringBuilder
    Public lista_builder As New StringBuilder
    Public Albero_builder As New StringBuilder
    Public Zaino_builder As New StringBuilder
    Public Rinascita_buider As New StringBuilder
    Public Inline_builder As New StringBuilder
    Public confronta_builder As New StringBuilder
    Public craft_builder As New StringBuilder
    Public base_builder As New StringBuilder
    Public vendi_bilder As New StringBuilder
    Public creanegozi_builder As New StringBuilder
    Function process_help(text As String) As String
        Dim builder As New StringBuilder
        Select Case text.ToLower.Trim
            Case "lista"
                builder.Append(lista_builder.ToString)
            Case "albero"
                builder.Append(Albero_builder.ToString)
            Case "zaino"
                builder.Append(Zaino_builder.ToString)
            Case "rinascita"
                builder.Append(Rinascita_buider.ToString)
            Case "inline"
                builder.Append(Inline_builder.ToString)
            Case "confronta"
                builder.Append(confronta_builder.ToString)
            Case "craft"
                builder.Append(craft_builder.ToString)
            Case "riepilogo"
                builder.Append(help_builder.ToString)
            Case "base"
                builder.Append(base_builder.ToString)
            Case "vendi"
                builder.Append(vendi_bilder.ToString)
            Case "creanegozi"
                builder.Append(creanegozi_builder.ToString)
            Case Else
                builder.Append("Comando non riconosciuto.")
        End Select

        Return builder.ToString
    End Function

    Sub init_help()
        With help_builder
            .AppendLine("*GUIDA:*")
            .AppendLine("Usa '/lista <oggetto/i>' per ricevere la lista dei materiali base necessari al craft degli oggetti inseriti.")
            .AppendLine("Usa '/albero <oggetto>' per ricevere un file di testo contenente l'albero dei craft dell'oggetto inserito.")
            .AppendLine("Usa '/salvazaino' per salvare lo zaino diviso in più messaggi, copia o inoltralo senza nessun comando se è un singolo messaggio.")
            .AppendLine("Usa '/rinascita <Username> <oggetto>' per ricevere un file di testo con le stringhe scambio da copiare e incollare in lootbot.")
            .AppendLine("Usa '/confronta' per ricevere un elenco di oggetti in comune tra uno zaino e un elenco 'Cerco:' dal comando inline.")
            .AppendLine("Usa '/craft <oggetto/i>' per ricevere un file di testo contenente stringhe da copiare e incollare, per craftare tutti gli oggetti necessari fino agli oggetti inseriti.")
            .AppendLine("Usa '/base <rarità>' per ricevere un elenco di tutti gli oggetti base per la rarità inserita, per ogni oggetto è indicata la quantità che possiedi.")
            .AppendLine("Usa '/vendi <oggetto/i>' per ottenere una lista di oggetti che puoi vendere in quanto non necessari per craftare gli oggetti inseriti.")
            .AppendLine("Usa '/creanegozi' o '/creanegozi <oggetto/i>' per ricevere dei comandi /negozio da inoltrare a @lootbotplus")
            .AppendLine("Usa '/info <oggetto/i>' per ricevere informazioni utili sugli oggetti inseriti.")
            .AppendLine("Usa '@craftlootbot <rarità> <oggetto>' in qualsiasi chat o gruppo per inviare rapidamente la lista dei materiali che stai cercando, la rarità è opzionale.")
            .AppendLine("Per specificare più oggetti esclusivamente nei comandi che lo supportano è necessario separarli con una virgola.")

            .AppendLine("Premi sui bottoni qui sotto per vedere maggiori informazioni su uno specifico comando.")
            .AppendLine()
            .AppendLine("Segui il canale @CraftLootBotNews per le news e aggiornamenti, contatta @AlexCortinovis per malfunzionamenti o dubbi")
        End With
        With lista_builder
            .AppendLine("*Lista:*")
            .AppendLine("Usa '/lista <oggetto/i>' per ricevere la lista dei materiali base necessari al craft degli oggetti inseriti.")
            .AppendLine("Ad esempio _'/lista rivestimento elastico'_")
            .AppendLine("Quando hai salvato il tuo zaino nel bot, la lista mostrerà gli oggetti necessari (che non possiedi) seguito dal totale.")
            .AppendLine("Subito dopo ci sarà invece l'elenco degli oggetti in vostro possesso, compresi item già craftati.")
            .AppendLine("Infine il bot darà un indicazione sul costo richiesto per eseguire i craft, escludendo item già craftati.")
        End With
        With Albero_builder
            .AppendLine("*Albero craft:*")
            .AppendLine("Usa '/albero <nome oggetto>' per ricevere un file di testo contenente l'albero dei craft dell'oggetto inserito.")
            .AppendLine("Ad esempio _'/albero rivestimento elastico'_")
            .AppendLine("In alto il bot darà un indicazione sul costo richiesto per eseguire i craft, escludendo item già craftati in tuo possesso.")
            .AppendLine("Il numero prima dell'oggetto indica la sua profondità, questo torna utile in alberi complessi, dove tra due item della stessa profondità possono esserci molte righe.")
            .AppendLine("Se hai salvato il tuo zaino, il numero tra parentesi dopo l'oggetto indica la quantità che possedi di quell'oggetto.")
        End With
        With Zaino_builder
            .AppendLine("*Zaino:*")
            .AppendLine("Puoi salvare il tuo zaino nel bot in modo da escludere gli oggetti che già possiedi dalla lista o per mostrarli nell'albero.")
            .AppendLine("Se lo zaino è un singolo messaggio copia o inoltra lo zaino, se invece è diviso in più messaggi perchè troppo lungo usa '/salvazaino' per iniziare a salvarlo.")
            .AppendLine("Per aggiornare lo zaino salvato, basta ripetere la procedura, quello precedente verrà sovrascritto.")
            .AppendLine("Usa '/svuota' per eliminare il tuo zaino se lo desideri.")
        End With
        With Rinascita_buider
            .AppendLine("*Rinascita:*")
            .AppendLine("Quando hai il tuo zaino salvato, e stai per fare la rinascita puoi chiedere al bot di creare in automatico le stringhe rapide per scambiare gli oggetti a un tuo compagno.")
            .AppendLine("Usa '/rinascita <Username> <oggetto>' per ricevere un file di testo con le stringhe da copiare e incollare in lootbot")
            .AppendLine("<Username> è il nome utente del giocatore al quale invierai i tuoi oggetti.")
            .AppendLine("<oggetto> è l'item base che verrà usato tra di voi per eseguire gli scambi, *è importante che entrambi abbiate almeno un'unità di quell'oggetto nello zaino*.")
            .AppendLine("Un esempio di stringa è:" + vbCrLf + "_'Scambia Bronzo,Titanio,<oggetto>,<oggetto>,,,<Username>,9'_")
            .AppendLine("Il bot tiene conto della quantità degli oggetti che hai nello zaino.")
        End With
        With confronta_builder
            .AppendLine("*Confronta:*")
            .AppendLine("Permette di confrontare un elenco 'Cerco:' dal comando inline e uno zaino restituendo gli oggetti che sono presenti in entrambi.")
            .AppendLine("Torna molto utile quando qualcuno invia lo zaino perchè vende tutto (ad esempio in vista della rinascita), e tu puoi subito sapere quali oggetti nel suo zaino sono nel tuo elenco 'Cerco:'.")
            .AppendLine("Torna utile anche quando tu invii l'elenco 'Cerco:' dal comando inline in un gruppo permettendo alle persone di confrontare immediatamente il loro zaino con il tuo elenco e dirti cosa possiedono.")
            .AppendLine("Dopo aver inviato '/confronta', invia l'elenco 'Cerco:' e infine lo zaino nel quale cercare gli oggetti.")
            .AppendLine("Se hai uno zaino salvato, puoi toccare il pulsante 'Utilizza il mio zaino' per utilizzarlo.")
            .AppendLine("In qualsiasi momento puoi premere annulla per annullare il confronto, gli zaini inviati tramite questo comando non sono salvati.")
        End With
        With craft_builder
            .AppendLine("*Lista Craft:*")
            .AppendLine("Permette di ricevere un file di testo contenente stringhe da copiare e incollare in lootbot, per craftare tutti gli oggetti necessari fino agli oggetti inseriti.")
            .AppendLine("Se hai lo zaino salvato, gli oggetti necessari che hai già craftato verranno esclusi dall'elenco.")
            .AppendLine("La lista è in ordine decrescente, dall'oggetto più semplice al più complesso, in questo modo puoi eseguire tutti i craft in sequenza uno alla volta.")
        End With
        With Inline_builder
            .AppendLine("*Comandi inline:*")
            .AppendLine("Puoi anche usare il bot per inviare rapidamente in qualsiasi chat o gruppo i materiali che stai cercando attraverso comandi inline.")
            .Append("Man mano che stai scrivendo l'oggetto, il bot ti mostrerà una lista di item che contengono la parola che stai scrivendo ")
            .AppendLine("(questo ti permette di avere molteplici risultati senza dover completare il nome dell'oggetto inserito).")
            .AppendLine()
            .Append("Nella lista per ogni risultato appare anche il totale degli oggetti di cui hai bisogno. ")
            .AppendLine("Premendo un oggetto in lista, verrà inviato un messaggio nella chat con l'elenco dei materiali di cui hai bisogno per craftarlo.")
            .AppendLine("Se l'elenco dei materiali fosse eventualmente troppo lungo, sarà troncato ai primi 15 oggetti di rarità maggiore.")
            .AppendLine()
            .AppendLine("Per utilizzare questa funzione devi aver salvato lo zaino, in qualsiasi momento il bot avrà sopra ai risultati una scorciatoia per aggiornarlo rapidamente.")
        End With
        With vendi_bilder
            .AppendLine("*Lista oggetti non necessari:*")
            .AppendLine("Permette di ricevere una lista di oggetti contenuti nel proprio zaino che non sono richiesti per il crafting degli oggetti specificati.")
            .AppendLine("Risulta particolarmente utile per sapere immediatamente cosa è possibile vendere perchè non necessario oppure perchè posseduto in abbondanza.")
            .AppendLine("Per utilizzare questa funzione devi avere lo zaino salvato.")
        End With
        With creanegozi_builder
            .AppendLine("*Creazione Negozi:*")
            .AppendLine("Permette di creare negozi velocemente partendo dal proprio zaino oppure, specificando uno o più oggetti, dalla lista oggetti non necessari per quegli oggetti.")
            .AppendLine("I negozi creati saranno impostati come privati. Il bot dopo che avrà inviato tutti i negozi, invierà anche il comando per cambiare la privacy a tutti i negozi, nel caso si volesse utilizzare.")
            .AppendLine("Il prezzo inserito di default è il prezzo base, è possibile inviare un messaggio per impostare il prezzo che si vuole agli oggetti. I prezzi saranno salvati per non doverli inviare ogni volta.")
            .AppendLine("Se si imposta un prezzo minore o uguale a 0, quell'oggetto verrà escluso dalla vendita. Gli oggetti craftati oppure di rarità UE e superiore sono automaticamente esclusi.")
            .AppendLine("Il formato da utilizzare è: ")
            .AppendLine("Vetro:100")
            .AppendLine("Pozione Piccola:0")
            .AppendLine("Acqua:150")
            .AppendLine("E' possibile farsi inviare la lista dei prezzi salvati tramite il comando /ottieniprezzi.")
            .AppendLine("E' possibile cancellare i prezzi salvati tramite il comando /cancellaprezzi.")
        End With
        With base_builder
            .AppendLine("*Lista oggetti base:*")
            .AppendLine("Con questo comando verrà mostrata una lista di tutti gli oggetti base per la rarità inserita.")
            .AppendLine("Se hai lo zaino salvato, tra parentesi viene mostrata la quantità che possiedi per quell'oggetto.")
        End With

    End Sub

    Function creaHelpKeyboard() As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim lista_button As New InlineKeyboardButton("📜 Lista 📜", "lista")
        Dim albero_button As New InlineKeyboardButton("🌲 Albero 🌲", "albero")
        Dim rinascita_button As New InlineKeyboardButton("👼🏼 Rinascita 👼🏼", "rinascita")
        Dim inline_button As New InlineKeyboardButton("🔍 Inline 🔍", "inline")
        Dim zaino_button As New InlineKeyboardButton("🎒 Zaino 🎒", "zaino")
        Dim craft_button As New InlineKeyboardButton("🛠 Craft 🛠", "craft")
        Dim confronta_button As New InlineKeyboardButton("📊 Confronta 📊", "confronta")
        Dim base_button As New InlineKeyboardButton("🔤 Base 🔤", "base")
        Dim vendi_button As New InlineKeyboardButton("🏪 Vendi 🏪", "vendi")
        Dim creanegozi_button As New InlineKeyboardButton("💸 CreaNegozi 💸", "creanegozi")
        Dim riepilogo_button As New InlineKeyboardButton("⬅️ Riepilogo ⬅️", "riepilogo")
        Dim row1() As InlineKeyboardButton
        Dim row2() As InlineKeyboardButton
        Dim row3() As InlineKeyboardButton
        Dim row4() As InlineKeyboardButton
        Dim row5() As InlineKeyboardButton
        Dim row6() As InlineKeyboardButton

        row1.Add(lista_button)
        row1.Add(albero_button)

        row2.Add(zaino_button)
        row2.Add(rinascita_button)

        row3.Add(craft_button)
        row3.Add(confronta_button)

        row4.Add(inline_button)
        row4.Add(base_button)

        row5.Add(vendi_button)
        row5.Add(creanegozi_button)

        row6.Add(riepilogo_button)

        keyboardbuttons.Add(row1)
        keyboardbuttons.Add(row2)
        keyboardbuttons.Add(row3)
        keyboardbuttons.Add(row4)
        keyboardbuttons.Add(row5)
        keyboardbuttons.Add(row6)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

End Module
