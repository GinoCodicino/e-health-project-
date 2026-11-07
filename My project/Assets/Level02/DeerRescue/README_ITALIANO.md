# Terza missione — Help the trapped deer

Aggiunta separata per il livello 2. Non contiene copie o sostituzioni di ForestQuest, NpcDialogue o BridgeQuestController: riutilizza i personaggi e i collegamenti già esistenti. Tutti i testi di gioco sono in inglese.

## 1. Importazione e creazione

1. Esci da Play, salva la scena e fai un commit o una copia del progetto.
2. Importa Level02_DeerRescue.unitypackage con Assets > Import Package > Custom Package. Lascia selezionati tutti i nuovi file.
3. Aspetta la compilazione.
4. Scegli Tools > Level 02 > Deer rescue > Configure from existing bridge quest.
5. Il comando trova Level02_BridgeQuest e crea Level02_DeerRescue con i nuovi punti e oggetti. Non devi selezionare il ponte.
6. Controlla e sposta i gruppi sotto elencati, poi salva con Cmd+S.

Il comando non duplica una missione già creata. Non usare Configure selected bridge e non rigenerare le missioni precedenti.

## 2. Posizionamento nella tua mappa

Il comando ricava il lato di partenza dal punto di riparazione del ponte e propone le posizioni sull'altra riva. Sono posizioni iniziali: la ricerca automatica del terreno non conosce gli ostacoli né il percorso effettivamente accessibile nella tua scena.

### Encounter_POINT_AFTER_BRIDGE

Mettilo sul sentiero oltre il ponte, sulla riva di destinazione. Ha come figlio NPC_Ranger_MOVE_TO_FAR_BANK: spostare il punto muove anche la posizione iniziale del personaggio.

In Play, quando entri entro Encounter Radius dal punto, Mia si avvicina al protagonista e apre la richiesta d'aiuto. Il movimento è un breve avvicinamento in linea retta, senza NavMesh o una nuova animazione di corsa. Lascia uno spazio libero e abbastanza piano tra punto e personaggio.

### Fishing_POINT_MOVE_TO_SAFE_BANK

Mettilo sulla riva raggiungibile tornando da Rowan. Non in acqua. Contiene panchina e cartello Fishing spot: muovendo il gruppo si spostano anche questi oggetti. Controlla che la panchina e il cartello poggino sul terreno.

### DeerRescue_AREA_MOVE_TO_TRAIL_END

Spostalo verso il fondo del percorso, oltre il ponte. Contiene il capriolo, i rami e Deer_INTERACTION_POINT. Muovendo l'intero gruppo rimane tutto collegato. Il punto d'interazione è circa due unità davanti all'animale: deve restare su suolo raggiungibile.

Il capriolo è un modello provvisorio costruito con forme low poly; non è un animale animato professionale. Puoi sostituire AnimalVisual con un vostro modello, assegnando il nuovo Transform in Deer Rescue View e riposizionando la benda. In questa versione l'animale passa dalla posa sdraiata a quella in piedi, poi rimane sul posto: non viene aggiunto un percorso di fuga.

## 3. Controllo dei riferimenti

Seleziona Level02_DeerRescue. Nel componente Deer Rescue Controller controlla:
- Player: il protagonista con playercontroller.
- Mushroom Quest: NpcDialogue di NPC_funghi (eredita ForestQuest).
- Bridge Quest: il controller del ponte.
- Bridge Condition: il componente che gestisce ponte rotto/riparato.
- Encounter Point e Ranger: punto oltre il ponte e Mia.
- Fishing Point: il gruppo del punto pesca.
- Deer Point: il punto d'interazione davanti all'animale.
- Deer View: il componente sul gruppo del capriolo.
- Fishing Game: River Fishing Game sullo stesso gestore.

Il comando prova a compilare tutti questi campi. Se rinomini Rowan o Peter, alcuni testi dei marcatori usano ancora quei nomi come stringhe: cerca Rowan e Peter in DeerRescueController.cs per adeguarli.

## 4. Quando parte la terza missione

Serve completare la missione del ponte, COMPRESO il dialogo finale di ritorno da Noah. Non basta inserire la terza asse e attraversare prima di parlargli.

Dopo aver chiuso il dialogo finale di Noah, attraversa il ponte ed entra nel punto Encounter. Mia si avvicina e racconta del capriolo sotto i rami del vecchio pino caduto dopo le forti piogge.

Accettando la richiesta parte l'obiettivo del taglialegna. Se chiudi o rimandi, puoi parlare nuovamente con Mia premendo E quando sei vicino.

## 5. Percorso completo

1. Mia chiede aiuto oltre il ponte.
2. Segui il marcatore fino a Rowan e premi E. Il dialogo chiede 3 pesci (o 4 se cambi Fish Goal).
3. Raggiungi il punto Fishing spot e premi E.
4. Completa il minigioco e premi Keep the fish.
5. Torna da Rowan e completa il dialogo: consegni i pesci e ricevi l'accetta.
6. Segui il marcatore al capriolo e premi E.
7. Libera l'animale rimuovendo i tre rami.
8. Raggiungi Peter alla casa rossa e chiedi le bende con E.
9. Torna al capriolo, premi E e completa la medicazione.
10. Mia conclude il dialogo e l'animale si rialza lentamente.

Un solo obiettivo della terza quest è segnalato alla volta. Accetta, pesci e bende sono oggetti logici mostrati nel contatore; non vengono aggiunti modelli in mano al personaggio.

## 6. Minigioco della pesca

La schermata mostra il fondale di un fiume di montagna e una rete in basso.

- Premi Start per avviare il timer.
- Sposta la rete con A/D oppure freccia sinistra/destra. Il personaggio del mondo resta fermo mentre la schermata è aperta.
- I pesci sono colorati e hanno pinne e coda. Sassi, rami, scarponi e bottiglie sono ostacoli.
- Un pesce raccolto aumenta il contatore.
- Un oggetto sbagliato blocca la rete per 0,65 secondi; non cancella i pesci già presi.
- Mentre la rete è bloccata, anche un pesce che la raggiunge può essere perso.
- Appaiono pesci con regolarità: non sono tutti oggetti estratti casualmente senza garanzie.
- Devi raccogliere 3 pesci entro 45 secondi. Fish Goal può essere impostato a 4 nell'Inspector.
- Se fallisci, Try again azzera conteggio, ostacoli e tempo.
- Se vinci, Keep the fish conferma il risultato e aggiorna la quest. Back o Esc senza confermare non assegnano i pesci.
- Uscire da una prova in corso annulla il tentativo.

### Prova veloce senza ripetere tutto il livello

Dopo aver creato la missione, entra in Play SENZA iniziare ancora il salvataggio. Usa Tools > Level 02 > Deer rescue > Test fishing in Play.
Si apre soltanto la pesca: puoi provarla anche prima di completare Peter e il ponte. La prova non assegna ricompense alla quest.
Il comando non apre un'altra schermata se sei già in un dialogo o hai già iniziato il salvataggio.

## 7. Liberazione del capriolo

La schermata mostra il capriolo dietro tre rami sovrapposti. Non c'è un timer.

1. Seleziona il ramo superiore.
2. Compare CUT sull'estremità sinistra, lontano dal corpo dell'animale.
3. Clicca CUT tre volte.
4. Il taglio si approfondisce e il ramo viene rimosso.
5. Ripeti con gli altri due rami.

Un ramo bloccato da quello superiore non conta. I colpi e i rami rimossi si conservano chiudendo la schermata nella stessa sessione. I rami vengono tolti anche dalla rappresentazione 3D nella scena. Terminata la liberazione si scopre che l'animale ha bisogno delle bende.

## 8. Bende e medicazione

Peter dà le bende in riconoscenza dell'aiuto ricevuto nella prima missione. Questo richiede ForestQuest.Stage.Complete, cioè anche l'annaffiatura completata.

La medicazione è un piccolo puzzle illustrato:
1. Trascina la medicazione sulla zona della zampa evidenziata.
2. Clicca i punti 1, 2, 3 in ordine per completare la benda.
3. La quest finisce e il modello si rialza.

Non vengono mostrati sangue o ferite realistiche. La posizione evidenziata della zampa è riferita all'illustrazione UI, non alle ossa del modello 3D. La benda 3D è una forma provvisoria, modificabile sul modello.

## 9. Come convivono i vecchi e i nuovi dialoghi

I vecchi script non vengono sostituiti. Dopo l'accettazione del salvataggio il nuovo controller disabilita temporaneamente, solo durante Play, BridgeQuestController: in questo modo Rowan non apre anche il dialogo precedente.

Quando devi chiedere le bende, prende temporaneamente in carico anche ForestQuest di Peter. I componenti mantengono il loro stato e vengono riabilitati quando non servono più o disattivi il nuovo gestore.

Per questo, durante Play, potresti vedere il vecchio componente con la casella Enabled deselezionata. È intenzionale: non correggerla manualmente durante la prova. Le modifiche non vengono salvate nella scena fuori da Play.

## 10. Script e modifiche future

| Percorso sotto Assets/Level02/DeerRescue | Funzione |
|---|---|
| Runtime/DeerRescueController.cs | Sequenza, nuovo dialogo di Rowan/Peter, marcatori e schermate del capriolo |
| Runtime/RiverFishingGame.cs | Rete, controlli, comparsa/caduta degli oggetti e interfaccia della pesca |
| Runtime/RescueArtwork.cs | Disegni di pesci, rete, ostacoli, capriolo e benda; CanvasRenderer incluso |
| Runtime/DeerRescueView.cs | Rami 3D, posa sdraiata, benda visibile e animazione per rialzarsi |
| Runtime/RescueDressingDrag.cs | Trascinamento della medicazione |
| Logic/RescueProgress.cs | Stati e regole; contiene RescueProgress e FishingRound |
| Editor/DeerRescueSetup.cs | Creazione della missione e comando di prova rapida |
| Tests/Editor | Test della logica |

Nell'Inspector di River Fishing Game puoi cambiare Fish Goal, Time Limit Seconds, Net Speed e Fall Speed. Nel controller puoi cambiare nome di Mia, distanze, velocità d'avvicinamento e velocità del testo.
Per cambiare la sequenza o i dialoghi cerca Intro, TalkWoodcutter, TalkPeter e Wrap. Per il disegno del fondale e degli oggetti modifica RescueArtwork. Non rimuovere RequireComponent(typeof(CanvasRenderer)).

## 11. Verifica e limiti

Qui non è disponibile Unity: non sono stati eseguiti compilazione, Test Runner e Play. Sono stati controllati la struttura dei file, i riferimenti alle API degli script ricevuti e il contenuto del pacchetto. I test automatici sono inclusi ma vanno eseguiti nel tuo progetto: Window > General > Test Runner > EditMode > Run All.

Controlla in Play:
- I primi due giochi funzionano ancora.
- Mia non interviene prima della conclusione di Noah.
- La rete prende soltanto i pesci e gli ostacoli provocano il blocco breve.
- Una prova fallita non assegna pesci; la consegna non assegna più accette.
- Il nuovo dialogo di Rowan/Peter si apre da solo, senza un secondo pannello sovrapposto.
- Esc chiude le schermate e restituisce i controlli.
- I rami si rimuovono nell'ordine previsto e il capriolo non viene liberato prima del terzo ramo.
- Le bende arrivano soltanto dopo la liberazione e l'aiuto iniziale a Peter.
- La medicazione richiede il trascinamento e i punti nell'ordine giusto.
- I punti sono raggiungibili e panchina, NPC e capriolo poggiano sul terreno.

Lo stato dura per la sessione: uscire da Play o ricaricare la scena ricomincia le missioni. Le nuove posizioni proposte richiedono il controllo nella tua mappa. Il modello del capriolo, la benda 3D e l'avvicinamento di Mia sono rappresentazioni semplici da rifinire con gli asset del gruppo.
