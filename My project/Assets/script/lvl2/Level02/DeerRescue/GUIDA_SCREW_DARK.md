# Tronchi casuali e difficoltà moderatamente maggiore

## Installazione

Questa versione aggiorna il puzzle scuro già installato. Esci dal Play, salva e importa **Level02_Screw_RandomChallenge.unitypackage**, selezionando tutti i file. I due script principali sono aggiornati insieme e completi: non incollare altre proprietà NextColour. Non serve riconfigurare gli NPC o assegnare immagini.

Apri la prova da Tools > Level 02 > Deer rescue > Test screw puzzle in Play. Premi Start challenge. Restart e Try again generano una nuova partita.

## Cosa cambia davvero

- **Sei tronchi e dodici punti di taglio**, anziché cinque tronchi e dieci punti.
- Due cataste di tre tronchi, con almeno due tronchi inizialmente accessibili: puoi scegliere da quale iniziare e come alternare i colori.
- Posizioni, inclinazioni, lunghezze e leggere variazioni della tinta del legno cambiano a ogni tentativo, entro limiti che mantengono i bersagli grandi e dentro il pannello.
- I dodici colori sono rimescolati liberamente, quattro pezzi per colore: non sono soltanto il vecchio schema con i colori rinominati.
- Il generatore cerca una sequenza che porti alla vittoria con tre posti d'attesa. Scarta gli schemi per cui non trova una soluzione e quelli risolvibili usando un solo posto d'attesa.
- Dopo un numero limitato di tentativi di generazione, esiste un ripiego deterministico verificato, per non bloccare il gioco o mostrare una catasta impossibile.

Il tema scuro, la texture del legno, l'illustrazione del capriolo e le animazioni rimangono quelli della versione precedente.

## Regole mantenute

Il contenitore richiede due pezzi del colore attivo. Gli altri colori aspettano nei tre spazi sottostanti; quando diventano il colore richiesto, entrano automaticamente nel contenitore.

Un quarto pezzo non richiesto nell'attesa piena, tre tagli su tronchi coperti o la fine del tempo fanno perdere il tentativo. Si può riprovare senza perdere l'ascia. Il timer resta a 60 secondi dall'avvio; non inizia durante la generazione o la lettura delle istruzioni. Rimane modificabile in **Screw Rescue Game > Time Limit Seconds**.

Le cataste seguono l'ordine visibile degli strati: i tronchi superiori bloccano quelli della stessa colonna sotto di loro. I segni scuri indicano i tronchi coperti. I colori e le due cataste impongono più scelte rispetto al precedente ordine quasi obbligato.

## File e modifiche future

I percorsi partono da Assets/Level02/DeerRescue/.

| File | Responsabilità |
| --- | --- |
| Logic/ScrewPuzzle.cs | Generazione dei sei tronchi, colori, dipendenze degli strati, controllo delle soluzioni e regole di vittoria/sconfitta. |
| Runtime/ScrewRescueGame.cs | Posizioni, inclinazioni, misure dei tronchi, UI scura, animazioni e timer. Le dimensioni si adattano a BoardCount e TotalCuts. |
| Tests/Editor/ScrewChallengeTests.cs | Test NUnit per 100 semi, riproducibilità, vittoria, sconfitta e ripetizione. |
| Resources/DeerRescueArt/Timber.png | Texture del legno precedente, mantenuta. |

Il numero usato per inizializzare la generazione è disponibile come proprietà `Seed`. Lo stesso valore riproduce colori e colonne; la UI usa lo stesso valore per la disposizione. Questo permette di ricreare una partita quando si verifica un problema, senza mostrarne il codice al giocatore.

`GetSolution()` restituisce una copia della soluzione trovata dal generatore, per i test; non viene mostrata nella partita. `TrySolve()` effettua la ricerca con memoria degli stati già visitati e un limite di lavoro. La generazione non continua senza limite.

Il costruttore con valore zero conserva il vecchio schema a cinque tronchi soltanto per mantenere i test precedenti. La partita normale usa sempre un valore casuale non nullo e sei tronchi.

## Verifica

Controlli eseguiti qui: struttura dei file, proprietà NextColour unica, corrispondenza delle API e simulazione di riferimento su 200 semi. La simulazione ha prodotto 200 schemi distinti e risolvibili, senza usare il ripiego.

La simulazione è scritta separatamente in Python: non equivale alla compilazione o all'esecuzione del codice Unity. I test NUnit sono inclusi ma non eseguiti qui. Unity non è disponibile in questo ambiente.

Nel progetto prova alcune partite con Restart e verifica che cambino colori, posizione e inclinazione; completa una partita; verifica una sconfitta e Try again. Controlla anche che la nuova disposizione resti leggibile nella risoluzione Game che userete per la consegna.
