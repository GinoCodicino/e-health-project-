# Seconda missione — aggiornamento 2

Aggiornamento degli script già presenti: conserva GUID, riferimenti nella scena, posizioni dei due NPC e stati grafici del ponte. Il primo gioco non viene modificato.

## 1. Aggiornare la scena già configurata

1. Esci da Play e salva la scena. Conserva un commit o una copia del progetto.
2. Importa il nuovo Level02_BridgeQuest.unitypackage con Assets > Import Package > Custom Package. Importa gli aggiornamenti degli stessi script: non creare altre copie a mano.
3. Attendi la compilazione.
4. Scegli Tools > Level 02 > Bridge quest > Apply update to existing quest.
5. Seleziona Level02_BridgeQuest nella Hierarchy e controlla il nuovo campo Mushroom Quest: deve puntare al componente NpcDialogue di NPC_funghi, che eredita ForestQuest.
6. Require Mushroom Quest deve essere attivo. Il comando lo attiva e limita il numero di tronchi a 3–4.
7. Salva la scena con Cmd+S.

NON usare di nuovo Configure selected bridge: per questa modifica non devi rigenerare né spostare ponte o NPC. Il comando Apply update non crea oggetti e conserva le posizioni già scelte.
Se Mushroom Quest resta vuoto, trascina NPC_funghi dalla Hierarchy nel campo, oppure usa il selettore del campo e scegli il suo NpcDialogue.

## 2. Noah si sblocca dopo Peter

Fino al completamento della prima missione non vengono mostrati il marcatore Talk to Noah né il suo comando E. Anche la seconda missione non può essere avviata passando direttamente dal taglialegna.

Lo sblocco avviene quando ForestQuest raggiunge Stage.Complete: dopo aver raccolto i funghi, portato l'acqua a Peter e annaffiato l'aiuola. Mentre il dialogo finale di Peter è aperto, il giocatore resta occupato; chiudendolo compare l'obiettivo Noah.

La prima missione non ha salvataggio permanente: dopo essere uscita da Play devi rifarla per verificare lo sblocco. Per una prova veloce dei soli minigiochi puoi temporaneamente disattivare Require Mushroom Quest nell'Inspector, poi riattivarlo fuori da Play prima di salvare la versione finale.

## 3. Nuovo minigioco del legno

Il dialogo di Rowan conduce alle istruzioni. Start fa partire una singola prova a tempo per tutta la mappa.

- Compaiono 3 o 4 tronchi su una piccola mappa illustrata del bosco.
- Scegli un tronco cliccandolo.
- Si apre il dettaglio di QUEL tronco.
- Ogni colpo fa oscillare l'ascia, tremare il tronco, comparire schegge e approfondire il taglio.
- Dopo 5 colpi il tronco è tagliato e torni automaticamente alla mappa.
- I tronchi completati rimangono visibili con la scritta CUT e non si possono contare due volte.
- Puoi usare Back to the map anche a metà taglio: il tronco mantiene i colpi ricevuti e puoi sceglierne un altro.
- Il timer continua durante selezione e cambio schermata: tornare alla mappa non lo azzera e non lo mette in pausa.
- Finire tutti i tronchi entro il limite sblocca il dialogo di ricompensa e le tre assi.
- Try again dopo un fallimento rigenera la prova azzerando i tronchi e il tempo.
- Leave challenge o Esc durante la mappa o il dettaglio annullano il tentativo. Alla ripresa ricominci.

Il valore iniziale del tempo resta quello già configurato sul tuo oggetto, normalmente 30 secondi. Puoi alzare Time Limit Seconds a 40–45 se vuoi rendere più comoda la selezione con il trackpad.
Minimum Logs e Maximum Logs sono ora limitati all'intervallo 3–4. Hits Per Log resta modificabile e parte da 5.

Le illustrazioni sono disegnate dalla UI: tronco con corteccia e anelli, tagli, schegge, ascia e alberi della mappa. Non serve importare immagini esterne e non vengono abbattuti alberi reali della scena.

## 4. Nuovo puzzle del ponte

La schermata mostra due rive, il fiume, le estremità del ponte e tre varchi di lunghezza diversa. Le assi disponibili sono nel vassoio in basso.

1. Tieni premuto su un'asse e trascinala sul varco della stessa lunghezza.
2. Rilascia dentro il varco: se corrisponde, l'asse si aggancia.
3. Se il varco è sbagliato o rilasci fuori dai varchi, l'asse torna nel vassoio e non viene consumata.
4. Un'asse agganciata non è più trascinabile e non può essere contata due volte.
5. Chiudere e riaprire il puzzle nella stessa sessione mantiene gli inserimenti corretti.
6. Alla terza asse torna attivo il ponte originale 3D con il suo collider; ponte rotto e cartello vengono nascosti.

Con il trackpad usa clic mantenuto e trascinamento. Il semplice clic seguito dal clic sul varco non è più il comando del puzzle.

## 5. Cosa è cambiato nei file

| File | Modifica |
|---|---|
| BridgeQuestController.cs | Sblocco dopo ForestQuest, mappa dei tronchi, dettaglio, animazioni e puzzle con rive |
| BridgeQuestProgress.cs | Mantiene separati i colpi di ogni tronco e accetta selezioni in ordine libero |
| TimberPicture.cs | Illustrazioni più ricche, ascia, danni, tremolio e schegge |
| BridgeQuestSetup.cs | Comando Apply update per migrare la scena esistente senza ricrearla |
| BridgePlankDrag.cs | Unico nuovo script: riceve gli eventi di trascinamento e passa al controller il rilascio |
| BridgeQuestProgressTests.cs | Test aggiornati per 3–4 tronchi, danni separati e timer condiviso |

BridgeCondition e BridgeQuestMarker mantengono il loro ruolo. ForestQuest.cs e NpcDialogue.cs non vengono sovrascritti.

### Dove cambiare le cose in futuro

- Tempi, colpi, nomi e distanza: Inspector di Level02_BridgeQuest.
- Collegamento a Peter: Mushroom Quest sullo stesso oggetto.
- Frasi: TalkFisher, FisherBranch, TalkWoodcutter e OfferPlanks in BridgeQuestController.
- Posizioni dei tronchi sulla mappa: array positions in OpenLogMap.
- Durata della breve animazione finale: ReturnAfterCut.
- Colori e forme: TimberPicture e DrawRiverAndBanks.
- Lunghezze dei tre pezzi: array lengths in OpenPuzzle. L'ordine dei pezzi nel vassoio è nell'array order.
- Regole per assegnare le assi, evitare doppioni e concludere la prova: BridgeQuestProgress.

## 6. Se importi questa versione per la prima volta

Se non hai ancora creato la missione, importa il pacchetto e seleziona Footbridge_1 nella Hierarchy. Usa Configure selected bridge, poi verifica i collegamenti e sposta i due NPC e BridgeRepair_POINT_MOVE_TO_BANK su terreno raggiungibile dalla riva di partenza.

La versione danneggiata viene costruita usando le dimensioni locali della mesh. Il ponte originale viene conservato e nascosto. Controlla l'allineamento fuori da Play usando Preview broken bridge e Preview repaired bridge. Un collider al centro interrompe il passaggio mentre il ponte è rotto. Questo non modifica i collider di Terrain e Water: verifica che non si possa aggirare la missione camminando sotto il ponte nel resto del livello.

## 7. Verifica in Unity

Qui non è disponibile l'Editor Unity: non sono stati eseguiti compilazione, Test Runner o Play. Sono stati controllati struttura del codice, API del giocatore ricevute, riferimenti e integrità del pacchetto. I test inclusi sono da eseguire nel progetto con Window > General > Test Runner > EditMode > Run All.

Controlli in Play:
1. Prima di finire Peter: nessun marcatore o interazione con Noah.
2. Dopo l'annaffiatura e la chiusura del dialogo: compare Noah.
3. La mappa contiene soltanto 3–4 tronchi.
4. Dai due colpi a un tronco, torna alla mappa, seleziona un altro e poi riapri il primo: deve mantenere due colpi.
5. Il timer non riparte tornando alla mappa e una prova scaduta non assegna assi.
6. Ogni colpo mostra movimento e danno. Un tronco CUT non è più selezionabile.
7. Trascina un'asse fuori dal ponte: torna al vassoio. Trascinala nel varco sbagliato: non viene consumata.
8. Nel varco corretto si aggancia. Esc e riapertura conservano l'asse agganciata.
9. Dopo il terzo pezzo puoi attraversare il ponte originale e tornare da Noah.
10. Il gioco dei funghi e del secchio resta funzionante.

Tutti i testi di gioco restano in inglese. Gli stati delle missioni durano per la sessione corrente, senza salvataggio su disco.
