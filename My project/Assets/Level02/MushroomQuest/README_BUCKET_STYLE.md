# Secchio — aggiornamento grafico

## Installazione

1. Esci da Play e salva la scena.
2. Importa Level02_Bucket_Visual_Update.unitypackage e lascia selezionati entrambi gli script.
3. Aspetta la compilazione e riprova il minigioco dell'acqua.

Non serve eseguire Configure. Il pacchetto mantiene il GUID del tuo ForestQuest.cs e aggiunge ForestBucketArt.cs. Non modifica NpcDialogue, la scena o gli script della missione del ponte. Le posizioni e i collegamenti già salvati restano validi.

## Aspetto nuovo

- Sfondo illustrato con fiume, montagna, pini e sassi.
- Secchio in legno con manico e fasce metalliche, rappresentato in sezione per mostrare l'acqua.
- Livello dell'acqua ondulato mentre il cursore si muove.
- Riquadro con percentuale grande, intervallo trasportabile e risultato.
- Barra graduata con zona verde, cursore a goccia e colori più leggibili.
- Pulsanti con bordi smussati e contrasto tra azione principale e ritorno.
- Segno di conferma sul secchio quando il livello è corretto.

## Regole conservate

Il cursore oscilla automaticamente. STOP lo blocca. Fuori dall'intervallo puoi riprovare; nell'intervallo ritiri il secchio. Minimum Water, Maximum Water e Bucket Travel Seconds restano quelli dell'Inspector. Gli estremi del range sono inclusi.

La missione passa a ReturnWater solo con Take the bucket. Il successivo ritorno da Peter e l'annaffiatura restano necessari per sbloccare Noah.

## Codice

ForestQuest.cs: aggiorna soltanto la presentazione del secchio e il relativo aggiornamento grafico; le altre quest, le interazioni e la logica del cursore restano presenti.
ForestBucketArt.cs: disegna le illustrazioni e l'onda tramite mesh UI; include esplicitamente CanvasRenderer e non richiede texture o materiali esterni.

Per cambiare la disposizione: metodo OpenBucket in ForestQuest.cs.
Per cambiare colori e forme: metodi Riverbank e Bucket in ForestBucketArt.cs.

Verifica in Unity: qui non sono disponibili compilazione e Play. Controlla visibilità delle illustrazioni, lettura della percentuale, STOP sotto/sopra/dentro il range, Try again, Take the bucket e ritorno dei controlli chiudendo la UI. I progressi si azzerano uscendo da Play.
