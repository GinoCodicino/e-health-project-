# Aggiornamento dopo le modifiche di Elena — 8 novembre 2026

Questo pacchetto conserva la versione caricata di ForestQuest: funghi con sfondo, zoom, trascinamento e cespugli per recuperare le travi del tetto. Cambia solo il premio finale della prima missione. Dialoghi e istruzioni del gioco sono in inglese.

## Installazione passo passo

1. Esci dal Play e salva una copia del progetto o fai un commit.
2. Importa `Level02_Rescue_Elena_Update.unitypackage` con Assets > Import Package > Custom Package. Aggiorna gli script esistenti: non creare seconde copie con nomi diversi.
3. Aspetta che Unity finisca la compilazione. Questo pacchetto richiede i precedenti script del progetto: MushroomUI, playercontroller, playerinputcontroller, ForestQuest/NpcDialogue e BridgeQuestController.
4. Se il salvataggio è già configurato, usa **Tools > Level 02 > Deer rescue > Update existing rescue - Elena version**. Il comando collega i nuovi giochi e imposta 10 pesci / 90 secondi, senza ricreare gli oggetti o spostarli.
5. Solo se non hai mai configurato la terza missione: usa prima **Configure from existing bridge quest**, poi il comando di aggiornamento.
6. Seleziona Level02_DeerRescue. Controlla i riferimenti Player, Mushroom Quest, Bridge Quest, Encounter Point, Ranger, Fishing Point, Deer Point e Deer View. Mantieni i punti già posizionati da voi su terreno raggiungibile.
7. Nel campo **Woodcutter Axe** trascina l'oggetto separato dell'accetta aggiunto da Elena. Quando Rowan presta l'accetta, quell'oggetto viene nascosto. Non assegnare l'intero NPC o una mesh contenente anche il personaggio.
8. Salva la scena fuori dal Play. Non rilanciare la configurazione del primo NPC: i suoi riferimenti ai cespugli e alla casa sono già quelli di Elena.

La scena aggiornata di Elena non è inclusa: il pacchetto contiene script, istruzioni e test. Non sostituisce né casa, né ambiente del boscaiolo, né fiume.

## Sequenza

- Funghi → recupero travi nei cespugli → ritorno alla casa e riparazione tetto.
- Peter ringrazia e consegna **rete e kit**. Sono due proprietà di ForestQuest; il controller del salvataggio registra i premi una sola volta nella partita.
- Noah → Rowan → legname → puzzle del ponte → conclusione del dialogo con Noah.
- Oltre il ponte Mia chiede aiuto → Rowan chiede 10 pesci → pesca con la rete di Peter → ritorno da Rowan per l'accetta.
- Capriolo: puzzle della struttura di legno caduta → kit già disponibile sul posto → cura. Nessun ritorno da Peter per le bende.

Gli stati ripartono all'avvio del Play, come nei giochi precedenti. Non è stato aggiunto un salvataggio permanente fra sessioni.

## Giochi nuovi

### Pesca

Un pesce ogni quattro oggetti: gli altri sono sassi, rami, bottiglie o scarponi. Gli ostacoli bloccano brevemente la rete, senza cancellare i pesci raccolti. Obiettivo predefinito 10 pesci, tempo 90 secondi; A/D o frecce muovono la rete. Gli oggetti continuano a cadere anche se un pesce viene perso: non serve un percorso perfetto.

### Liberazione, ispirata ai puzzle delle viti

È una versione **2D con strati e animazioni**, non una simulazione fisica 3D. Cinque assi formano i resti di una vecchia recinzione caduta insieme al pino: questo spiega la presenza delle viti. Le assi si sollevano quando togli entrambe le viti. I livelli sopra bloccano quelli sotto.

Le viti del colore della scatola vanno direttamente nella scatola; gli altri colori occupano tre posti temporanei. Le scatole si svuotano automaticamente quando complete. Se la vaschetta è piena, una vite del colore sbagliato viene rifiutata e puoi riprovare con un'altra oppure riavviare. Nessun timer. Le viti ruotano e le assi escono lateralmente senza urtare il capriolo.

Per controllare il puzzle: gli identificativi sono `asse*2 + estremità`; una soluzione è **8, 9, 6, 7, 4, 2, 5, 3, 0, 1**. I colori e le dipendenze sono in ScrewPuzzle.cs.

### Kit arcade

Sei microbi, due colpi di disinfettante ciascuno. Il primo colpo libera una scheggia infetta: seleziona la pinzetta fucsia e rimuovila prima di dare il secondo colpo. I microbi lanciano anche altre schegge. Seleziona gli strumenti con i pulsanti oppure 1 e 2. Cinque errori o schegge sfuggite richiedono di ripetere la sfida. Il kit resta disponibile in caso di errore.

La battaglia è una rappresentazione fantastica all'interno del kit. Alla vittoria Mia medica la zampa e il modello del capriolo si rialza con la benda. Non è una simulazione di cure veterinarie.

## Dove modificare cosa

Tutti i percorsi sotto sono relativi ad Assets/Level02/.

| File | Responsabilità / modifiche frequenti |
| --- | --- |
| MushroomQuest/Runtime/ForestQuest.cs | Gioco di Elena, dialogo di ringraziamento, HasFishingNet e HasMedicalKit al completamento del tetto. |
| DeerRescue/Runtime/DeerRescueController.cs | Ordine delle missioni, dialoghi di Mia/Rowan, collegamenti, prestito accetta, passaggio diretto dalla liberazione alla cura. |
| DeerRescue/Runtime/RiverFishingGame.cs | Schermata pesca, frequenza oggetti, colori e movimento. Dall'Inspector: Fish Goal, Time Limit Seconds, Net Speed, Fall Speed. |
| DeerRescue/Logic/RescueProgress.cs | Controlli sui premi e sui passaggi della missione; impedisce di ricevere di nuovo il kit consumato. |
| DeerRescue/Logic/ScrewPuzzle.cs | Colori, strati, tre posti temporanei, validità delle mosse. Cambiare i colori richiede ricontrollare che esista una soluzione. |
| DeerRescue/Runtime/ScrewRescueGame.cs | Disegno della schermata e animazioni di viti e assi. |
| DeerRescue/Logic/CareBattle.cs | Sei microbi, due colpi, limite cinque errori. |
| DeerRescue/Runtime/BacteriaBattleGame.cs | Bersagli, schegge, selezione strumenti, ritmo. Dall'Inspector: Splinter Interval, Splinter Speed, Spray Cooldown. |
| DeerRescue/Runtime/RescueArcadeArt.cs | Disegni procedurali di viti, assi, microbi, pinzetta, spray e schegge. |
| DeerRescue/Runtime/RescueGamePanel.cs | Creazione delle finestre e blocco/rilascio dei comandi del giocatore. |
| DeerRescue/Runtime/RescueArtwork.cs | Disegni precedenti del fiume, rete e capriolo, mantenuti. |
| DeerRescue/Runtime/DeerRescueView.cs | Modello 3D del capriolo: rami rimossi, benda e animazione finale. |
| BridgeQuest/Runtime/BridgeQuestMarker.cs | Etichetta e distanza della destinazione, ora collega anche il segnale luminoso. |
| BridgeQuest/Runtime/QuestBeacon.cs | Anello luminoso e raggio verticale; percorso opzionale con waypoint. |
| DeerRescue/Editor/DeerRescueSetup.cs | Comandi Tools, configurazione e prove separate. Non gira nella build. |
| DeerRescue/Tests/Editor/RescueProgressTests.cs | Test NUnit dei premi, sequenza, pesca, soluzione del puzzle e regole del kit. |

RescueDressingDrag.cs e le vecchie API sono conservati per non rompere riferimenti esistenti; il nuovo gioco non usa più il trascinamento della garza.

## Segnali e scia luminosa

Il segnale segue soltanto la destinazione della missione attiva: Noah, Rowan, ponte, pesca o capriolo. L'anello e il raggio sono disegnati a runtime, senza collider e senza modificare gli script del fiume.

Per una vera scia sul terreno, aggiungi **QuestBeacon** al GameObject che contiene BridgeQuestMarker e assegna **Route Waypoints** in ordine lungo un percorso sicuro. Usa Empty GameObject appena sopra il sentiero, evitando segmenti che attraversino l'acqua. L'ultimo punto deve essere entro quattro metri dalla destinazione: la scia si nasconde quando cambia l'obiettivo, così non suggerisce una strada sbagliata. Questo aggiornamento non inventa waypoint nella vostra scena: per un percorso completo occorre controllare la scena attuale.

## Verifica in Unity

Questo aggiornamento è stato controllato staticamente e la soluzione delle viti è stata simulata come riferimento. Qui non sono disponibili Unity o un compilatore C#: **compilazione, test NUnit e Play vanno eseguiti nel vostro progetto**.

1. Console: nessun errore rosso dopo l'importazione.
2. In Play usa i menu **Test fishing in Play**, **Test screw puzzle in Play**, **Test kit challenge in Play**, con tutte le altre finestre chiuse. Le prove non assegnano ricompense della missione.
3. Controlla che 9 pesci non completino il gioco e 10 sì. Prova tempo scaduto e nuovo tentativo.
4. Nel puzzle prova una vaschetta piena, Restart, Esc anche durante l'animazione, poi la soluzione completa.
5. Nel kit prova strumento sbagliato, scheggia sfuggita, fallimento e ripetizione, poi vittoria.
6. Gioca la sequenza intera: premi soltanto dopo il tetto; Noah sbloccato dopo la prima missione; niente ritorno da Peter dopo la liberazione; accetta nascosta al prestito; capriolo medicato alla vittoria.
7. Window > General > Test Runner > EditMode: esegui Level02.DeerRescue.Tests se Unity Test Framework è installato.

Se il vostro NpcDialogue non eredita più da ForestQuest oppure gli script della terza missione sono stati modificati dopo il pacchetto precedente, serve confrontare anche quelle versioni prima di importare. Non sostituire altri script del progetto con vecchie copie.
