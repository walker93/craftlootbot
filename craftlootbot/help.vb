Imports System.Text
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.InlineKeyboardButtons

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
    Public info_builder As New StringBuilder
    Public stima_builder As New StringBuilder
    Public xmlhtml_builder As New StringBuilder
    Public setequip_builder As New StringBuilder

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
            Case "info"
                builder.Append(info_builder.ToString)
            Case "stima"
                builder.Append(stima_builder.ToString)
            Case "html-xml"
                builder.Append(xmlhtml_builder.ToString)
            Case "setequip"
                builder.Append(setequip_builder.ToString)
            Case Else
                builder.Append("Comando non riconosciuto.")
        End Select

        Return builder.ToString
    End Function

    Sub init_help()
        With help_builder
            .AppendLine("*GUIDA COMANDI:*")
            .AppendLine("> '/lista <oggetto/i>' per ricevere la lista dei materiali base necessari al craft degli oggetti inseriti.")
            .AppendLine("> '/albero <oggetto>' per ricevere un file di testo contenente l'albero dei craft dell'oggetto inserito.")
            .AppendLine("> '/salvazaino' per salvare lo zaino diviso in più messaggi, copia o inoltralo senza nessun comando se è un singolo messaggio.")
            .AppendLine("> '/rinascita <Username> <oggetto>' per ricevere un file di testo con le stringhe scambio da copiare e incollare in lootbot.")
            .AppendLine("> '/confronta' per ricevere un elenco di oggetti in comune tra una lista di oggetti in vendita e gli oggetti che stai cercando.")
            .AppendLine("> '/craft <oggetto/i>' per ricevere un file di testo contenente stringhe da copiare e incollare, per craftare tutti gli oggetti necessari fino agli oggetti inseriti.")
            .AppendLine("> '/base <rarità>' per ricevere un elenco di tutti gli oggetti base per la rarità inserita, per ogni oggetto è indicata la quantità che possiedi.")
            .AppendLine("> '/vendi <oggetto/i>' per ottenere una lista di oggetti che puoi vendere in quanto non necessari per craftare gli oggetti inseriti.")
            .AppendLine("> '/creanegozi' o '/creanegozi <oggetto/i>' per ricevere dei comandi /negozio da inoltrare a @lootplusbot")
            .AppendLine("> '/info <oggetto>' per ricevere informazioni utili sull'oggetto inserito.")
            .AppendLine("> '/stats' per ottenere statistiche sull'utilizzo del bot.")
            .AppendLine("> '/stima <oggetto>' per ricevere info su costi, punti craft e valore di un oggetto tenendo presente il vostro zaino.")
            .AppendLine("> '/xml <oggetto>' o '/html <oggetto>' per ricevere un file XML o HTML con la struttura ad albero dei craft dell'oggetto inserito.")
            .AppendLine("> '/setequip <oggetto/i>' per impostare il proprio equipaggiamento.")
            .AppendLine("> '@craftlootbot <rarità> <oggetto>' in qualsiasi chat o gruppo per inviare rapidamente la lista dei materiali che stai cercando, la rarità è opzionale.")
            .AppendLine()
            .AppendLine("*GUIDA INOLTRI:*")
            .AppendLine("> Se inoltri uno zaino, verrà salvato.")
            .AppendLine("> Se inoltri un messaggio prezzi per il comando '/creanegozi', verranno salvati.")
            .AppendLine("> Se inoltri un messaggio di apertura scrigni, verrà convertito in uno zaino pronto per essere salvato.")
            .AppendLine("> Se inoltri un messaggio ottenuto da /lista o /vendi, il bot restituirà i comandi /ricerca da inoltrare a @lootplusbot per conoscerne il prezzo.")
            .AppendLine()
            .AppendLine("Per specificare più oggetti esclusivamente nei comandi che lo supportano è necessario separarli con una virgola.")
            .AppendLine("I comandi /lista, /craft, /creanegozi e /vendi supportano anche la quantità degli oggetti inseriti. Ad esempio '/lista tavolino:10' fornirà la lista per degli oggetti per 10 tavolini.")

            .AppendLine("*Premi sui bottoni qui sotto per vedere maggiori informazioni su uno specifico comando.*")
            .AppendLine()
            .AppendLine("Segui il canale @CraftLootBotNews per le news e aggiornamenti, contatta @AlexCortinovis per malfunzionamenti o dubbi")
        End With
        With lista_builder
            .AppendLine("*Lista:*")
            .AppendLine("Usa '/lista <oggetto/i>' per ricevere la lista dei materiali base necessari al craft degli oggetti inseriti.")
            .AppendLine("Ad esempio _'/lista rivestimento elastico'_")
            .AppendLine("Quando hai salvato il tuo zaino nel bot, la lista mostrerà gli oggetti necessari (che non possiedi) seguito dal totale.")
            .AppendLine("Subito dopo ci sarà invece l'elenco degli oggetti in tuo possesso, compresi item già craftati.")
            .AppendLine("Infine il bot darà un indicazione sul costo richiesto per eseguire i craft e i punti craft che si guadagnano, escludendo item già craftati.")
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
            .AppendLine("Permette di confrontare un elenco 'Cerco:' dal comando inline o una /lista con uno zaino o un comando /vendi restituendo gli oggetti che sono presenti in entrambi.")
            .AppendLine("Torna molto utile quando qualcuno invia lo zaino perchè vende tutto (ad esempio in vista della rinascita), e tu puoi subito sapere quali oggetti nel suo zaino sono nel tuo elenco 'Cerco:' o nella tua lista di oggetti necessari.")
            .AppendLine("Torna utile anche quando tu invii l'elenco 'Cerco:' dal comando inline o la tua /lista di oggetti che cerchi in un gruppo permettendo alle persone di confrontare immediatamente il loro zaino o la loro lista /vendi di oggetti non necessari con il tuo elenco e dirti cosa possiedono.")
            .AppendLine("Dopo aver inviato '/confronta', invia l'elenco 'Cerco:' o la /lista successivamente invia lo zaino o il /vendi nel quale cercare gli oggetti.")
            .AppendLine("Se hai uno zaino salvato, puoi toccare il pulsante 'Utilizza il mio zaino' per utilizzarlo.")
            .AppendLine("In qualsiasi momento puoi premere annulla per annullare il confronto, gli zaini inviati tramite questo comando non sono salvati.")
        End With
        With craft_builder
            .AppendLine("*Lista Craft:*")
            .AppendLine("Permette di ricevere un file di testo o un messaggio contenente stringhe da copiare e incollare in lootbot, per craftare tutti gli oggetti necessari fino agli oggetti inseriti.")
            .AppendLine("Se hai lo zaino salvato, gli oggetti necessari che hai già craftato verranno esclusi dall'elenco.")
            .AppendLine("La lista è in ordine decrescente, dall'oggetto più semplice al più complesso, in questo modo puoi eseguire tutti i craft in sequenza uno alla volta.")
        End With
        With Inline_builder
            .AppendLine("*Comandi inline:*")
            .AppendLine("Puoi anche usare il bot per inviare rapidamente in qualsiasi chat o gruppo i materiali che stai cercando attraverso comandi inline.")
            .Append("Man mano che stai scrivendo l'oggetto, il bot ti mostrerà una lista di item che contengono la parola che stai scrivendo ")
            .AppendLine("(questo ti permette di avere molteplici risultati senza dover completare il nome dell'oggetto inserito).")
            .AppendLine("Puoi specificare una rarità inserendone la sigla prima dell'oggetto, la lista sarà filtrata e verrà inviato l'elenco contenente solo gli oggetti di quella rarità.")
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
            .AppendLine("Tutta la guida su come si possono salvare e utilizzare i prezzi è disponibile qui: http://telegra.ph/Guida-impostazione-prezzi-per-craftlootbot-07-11")
        End With
        With base_builder
            .AppendLine("*Lista oggetti base:*")
            .AppendLine("Con questo comando verrà mostrata una lista di tutti gli oggetti base per la rarità inserita.")
            .AppendLine("Se hai lo zaino salvato, tra parentesi viene mostrata la quantità che possiedi per quell'oggetto.")
        End With
        With info_builder
            .AppendLine("*Informazioni oggetti:*")
            .AppendLine("Con questo comando sarà possibile visualizzare alcune informazioni utili sugli oggetti, molte delle quali disponibili anche in lootbot, altre invece esclusive di craftlootbot.")
            .AppendLine("Ad esempio sarà visualizzato il costo di craft, i punti craft guadagnati craftandolo, e il numero di utilizzi all'interno del set necro.")
        End With
        With stima_builder
            .AppendLine("*Stima oggetti:*")
            .AppendLine("Con questo comando sarà possibile visualizzare alcune informazioni come i punti craft, il costo per il craft, il valore dell'oggetto ecc.")
        End With
        With xmlhtml_builder
            .AppendLine("*XML - HTML:*")
            .AppendLine("Con questi comandi sarà possibile ottenere un file rispettivamente .xml o .html contenente l'albero dei craft dell'oggetto inserito.")
            .AppendLine("Il comando HTML dispone anche di una semplice interfaccia grafica che permette di aprire o chiudere i vari craft per facilitare la lettura. Sia da PC che da smartphone.")
            .AppendLine("E' possibile fare lo stesso con il comando /xml all'interno di un editor testuale avanzato come Notepad++")
        End With
        With setequip_builder
            .AppendLine("*SetEquip:*")
            .AppendLine("Con questo comando è possibile impostare il proprio equipaggiamento in modo da non doverlo aggiungere allo zaino manualmente o disequipaggiarlo prima di salvare lo zaino.")
            .AppendLine("E' possibile inserire tutto l'equipaggiamento o solo una parte, nell'ordine che si preferisce.")
            .AppendLine("Se viene inviato il comando senza oggetti di seguito verrà cancellato l'equipaggiamento attualmente salvato.")
            .AppendLine("Usando questo comando il vecchio equipaggiamento viene sovrascritto.")
        End With
    End Sub

    Function creaHelpKeyboard() As ReplyMarkups.InlineKeyboardMarkup
        Dim keyboard As New ReplyMarkups.InlineKeyboardMarkup
        Dim keyboardbuttons()() As InlineKeyboardButton
        Dim lista_button As New CallbackInlineButton("📜 Lista 📜", "lista")
        Dim albero_button As New CallbackInlineButton("🌲 Albero 🌲", "albero")
        'Dim rinascita_button As New InlineKeyboardButton("👼🏼 Rinascita 👼🏼", "rinascita")
        Dim inline_button As New CallbackInlineButton("🔍 Inline 🔍", "inline")
        Dim zaino_button As New CallbackInlineButton("🎒 Zaino 🎒", "zaino")
        Dim craft_button As New CallbackInlineButton("🛠 Craft 🛠", "craft")
        Dim confronta_button As New CallbackInlineButton("📊 Confronta 📊", "confronta")
        Dim base_button As New CallbackInlineButton("🔤 Base 🔤", "base")
        Dim vendi_button As New CallbackInlineButton("🏪 Vendi 🏪", "vendi")
        Dim creanegozi_button As New CallbackInlineButton("💸 CreaNegozi 💸", "creanegozi")
        Dim info_button As New CallbackInlineButton("ℹ️ Info ℹ️", "info")
        Dim stima_button As New CallbackInlineButton("📈 Stima 📈", "stima")
        Dim xmlHtml_button As New CallbackInlineButton("🌐 XML / HTML 🌐", "html-xml")
        Dim setequip_button As New CallbackInlineButton("🗡 SetEquip 🗡", "setequip")
        Dim riepilogo_button As New CallbackInlineButton("⬅️ Riepilogo ⬅️", "riepilogo")

        Dim row1() As InlineKeyboardButton
        Dim row2() As InlineKeyboardButton
        Dim row3() As InlineKeyboardButton
        Dim row4() As InlineKeyboardButton
        Dim row5() As InlineKeyboardButton
        Dim row6() As InlineKeyboardButton
        Dim row7() As InlineKeyboardButton
        'Dim row8() As InlineKeyboardButton

        row1.Add(lista_button)
        row1.Add(albero_button)

        row2.Add(zaino_button)
        'row2.Add(rinascita_button)
        row2.Add(craft_button)

        row3.Add(confronta_button)
        row3.Add(inline_button)

        row4.Add(base_button)
        row4.Add(vendi_button)

        row5.Add(creanegozi_button)
        row5.Add(info_button)

        row6.Add(stima_button)
        row6.Add(xmlHtml_button)

        row7.Add(setequip_button)
        row7.Add(riepilogo_button)

        keyboardbuttons.Add(row1)
        keyboardbuttons.Add(row2)
        keyboardbuttons.Add(row3)
        keyboardbuttons.Add(row4)
        keyboardbuttons.Add(row5)
        keyboardbuttons.Add(row6)
        keyboardbuttons.Add(row7)
        'keyboardbuttons.Add(row8)
        keyboard.InlineKeyboard = keyboardbuttons
        Return keyboard
    End Function

End Module
