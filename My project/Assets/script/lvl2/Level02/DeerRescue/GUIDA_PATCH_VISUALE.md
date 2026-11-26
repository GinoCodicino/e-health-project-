# Patch inventario e salvataggio del capriolo

## Come installarla sulla versione già funzionante

1. Esci dal Play e salva la scena. Fai un commit o conserva una copia del progetto.
2. Importa **Level02_Rescue_VisualPatch.unitypackage** tramite Assets > Import Package > Custom Package.
3. Attendi la compilazione. Il pacchetto aggiorna gli script già presenti mantenendo i loro GUID. Non contiene ForestQuest.cs: il file corretto resta quello unico in Assets/Level02/ForestQuest.cs. Non reimportare il vecchio pacchetto completo, che aveva aggiunto un duplicato in un altro percorso.
4. Non serve rifare la configurazione. Fishing Game, Release Game, Care Game e Woodcutter Axe mantengono i collegamenti già assegnati nella scena.
5. Premi Play. Puoi usare Tools > Level 02 > Deer rescue > Test screw puzzle in Play per la prova separata.

Questa patch richiede il precedente aggiornamento Elena già installato. Non modifica funghi, cespugli, casa, fiume, posizioni degli NPC o punti della missione.

## Inventario

Un solo pannello, nell'angolo superiore destro, mostra gli oggetti realmente disponibili, uno per riga:

- Axe
- Aid kit
- Net

Quando necessario compaiono anche Fish e Planks con la quantità. La vecchia box separata delle assi del ponte viene disattivata mentre il nuovo inventario è attivo. L'elenco si nasconde durante dialoghi e minigiochi.

Gli strumenti vengono rimossi solo a operazione riuscita:

| Oggetto | Quando sparisce |
| --- | --- |
| Net | Dopo aver raggiunto l'obiettivo di pesca e premuto Keep the fish. |
| Axe | Dopo aver liberato tutti i tronchi e confermato Continue to the aid kit. |
| Aid kit | Dopo la vittoria della cura e la conferma Help the deer. |

Fallire, uscire o riprovare non consuma lo strumento. I premi di Peter vengono assegnati una sola volta nella partita, quindi gli oggetti consumati non ricompaiono al frame successivo.

## Il nuovo puzzle, spiegato senza viti

La meccanica resta ispirata ai giochi con strati e colori, ma i bersagli sono **X dipinte sul legno**. Cliccando su una X accessibile compare l'ascia e parte il taglio. Un piccolo pezzo colorato vola verso il contenitore o verso l'area di attesa. Dopo aver tagliato entrambi i punti di un tronco, il tronco scivola via e scopre lo strato sotto.

1. Guarda **COLLECT CORAL / BLUE / GOLD** a destra: è il colore richiesto adesso. I due spazi bordati sono i due pezzi da raccogliere.
2. Un taglio di quel colore riempie uno spazio. Quando entrambi sono pieni, si passa automaticamente al colore successivo, anticipato dalla riga **NEXT**.
3. Se tagli un altro colore, il pezzo va in uno dei tre spazi numerati sotto **WAITING AREA**.
4. Appena quel colore diventa quello richiesto, i suoi pezzi in attesa vengono spostati automaticamente nel contenitore. Non devi trascinarli.
5. Se i tre spazi di attesa sono pieni, puoi ancora tagliare il colore richiesto. Gli altri tagli vengono rifiutati senza perdere pezzi. Se non c'è una mossa utile, usa Restart.

Esempio: il contenitore richiede CORAL e tagli BLUE. Il pezzo blu aspetta sotto. Quando la scatola passa a BLUE, quel pezzo entra da solo e conta già come uno dei due necessari.

Le X luminose sono accessibili; quelle scure appartengono a tronchi ancora coperti. La rimozione richiede anche di scegliere l'ordine dei colori, non soltanto cliccare su tutti i punti.

La schermata è una rappresentazione 2D a strati con animazioni, non il modello 3D ruotabile del riferimento. Il vecchio disegno procedurale del capriolo viene sostituito con un'illustrazione trasparente più curata, usata anche nelle schermate del kit. Il modello 3D presente nel bosco rimane quello della vostra scena.

## File aggiornati e responsabilità

I percorsi partono da Assets/Level02/.

| File | Cosa gestisce |
| --- | --- |
| DeerRescue/Runtime/QuestEquipmentHud.cs | Unico pannello dell'inventario, elenco e impaginazione. |
| DeerRescue/Runtime/DeerRescueController.cs | Fornisce i dati all'inventario e coordina i passaggi della missione. |
| DeerRescue/Logic/RescueProgress.cs | Consumo rete, ascia e kit solo dopo successo; impedisce premi duplicati. |
| BridgeQuest/Runtime/BridgeQuestController.cs | Espone il numero di assi e permette di nascondere la vecchia box. La logica del ponte e del taglialegna resta invariata. |
| DeerRescue/Runtime/ScrewRescueGame.cs | Nuova schermata, contenitore, attesa, movimento dell'ascia e dei pezzi. Il nome del file resta uguale per conservare il collegamento Release Game. |
| DeerRescue/Logic/ScrewPuzzle.cs | Regole degli strati e dei colori. Aggiunta l'anteprima del colore successivo. |
| DeerRescue/Runtime/RescueArcadeArt.cs | Disegno delle X, dell'ascia, dei pezzi e del legno con bordi e ombreggiatura. |
| DeerRescue/Runtime/RescueGamePanel.cs | Pannelli arrotondati e caricamento della nuova illustrazione. |
| DeerRescue/Resources/DeerRescueArt/RoeDeer.png | Illustrazione del capriolo. Il nome e il percorso sono usati da Resources.Load. |
| DeerRescue/Tests/Editor/RescueProgressTests.cs | Test aggiornati per verificare che gli oggetti consumati non tornino. |

Puoi sostituire RoeDeer.png con un'altra illustrazione, mantenendo nome e percorso. Il codice crea lo sprite a runtime: non serve assegnarlo manualmente nell'Inspector.

## Prove da fare

- In Play, dopo la prima missione, verifica Net e Aid kit su righe separate. Durante la riparazione del ponte Planks deve comparire nello stesso elenco, senza la vecchia box sovrapposta.
- Fallisci una pesca: Net resta. Vinci e conferma: Net sparisce e compaiono i pesci.
- Scambia i pesci: compare Axe. Completa il salvataggio e conferma: Axe sparisce, Aid kit resta.
- Fallisci la cura: Aid kit resta. Vinci e conferma: Aid kit sparisce.
- Nel puzzle prova un colore diverso da quello attivo: osserva il pezzo nell'area di attesa. Completa una scatola e osserva il passaggio automatico.
- Premi Restart anche durante un'animazione: la schermata deve ripartire pulita. Esc chiude il gioco; la prova dal menu non assegna ricompense alla missione.

Controlli effettuati qui: confronto delle modifiche, controlli statici e soluzione di riferimento del puzzle. Unity non è disponibile in questo ambiente: compilazione, NUnit e Play devono essere verificati nel vostro progetto.

## Asset illustrato

Nuova immagine creata con generazione integrata. Prompt: una femmina adulta di capriolo europeo sdraiata, corpo intero, tre quarti verso destra, zampe ripiegate naturalmente, mantello marrone caldo e ventre chiaro, orecchie grandi, senza palchi, espressione calma, illustrazione rifinita per un gioco boschivo, sfondo trasparente, nessun testo, sangue, ferita o oggetto.
