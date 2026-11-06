using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class QTEAcqua : MonoBehaviour
{
    [Header("Regole")]
    public int goccieMax = 12;
    public int scivolateMax = 3;
    public int fangoMax = 3;
    public float acquaPerGoccia = 16f;
    public float acquaPerFango = 8f;
    public float acquaPersaScivolata = 20f;

    [Header("Difficoltà (da primo a ultimo turno)")]
    public float tempoPassoIniziale = 1.4f;
    public float tempoPassoFinale = 0.9f;
    public float velocitaIniziale = 0.9f;   // barre al secondo
    public float velocitaFinale = 1.8f;
    public float zonaPulitaIniziale = 0.18f; // frazione della barra
    public float zonaPulitaFinale = 0.10f;
    public float margineFango = 0.12f;
    public float tempoMaxGoccia = 5f;

    [Header("Testi")]
    [TextArea(3, 6)]
    public string ricordoNonno =
        "Dal taccuino del nonno:\n«Mi fermai alla baita di una giovane del posto. Mi offrì un pranzo abbondante, ma rifiutai: mi bastò l'acqua del torrente, freschissima e abbondante, bevuta a grandi sorsi.»";
    [TextArea(3, 6)]
    public string presente =
        "Oggi il torrente è un rigagnolo fangoso. Riempi il secchio goccia dopo goccia, senza scivolare.";
    [TextArea(3, 6)]
    public string istruzioni =
        "[A] o [D] = passo sulla roccia (premi il tasto indicato in tempo)\n[SPAZIO] = raccogli la goccia quando l'indicatore è nell'azzurro\nAttenzione al fango!";

    [Header("Riferimenti")]
    public PlayerFPS player;

    [Header("Eventi")]
    public UnityEvent onVittoria;
    public UnityEvent onSconfitta;

    public bool InCorso { get { return canvasGO != null && canvasGO.activeSelf; } }

    enum Fase { Fermo, Intro, Passo, Goccia, Pausa, Fine }
    Fase fase = Fase.Fermo;

    // Scala tra le coordinate del disegno e i pixel della UI
    const float S = 1.25f;

    // UI
    GameObject canvasGO;
    Transform introGroup, giocoGroup, passoGroup, barraGroup;
    GameObject btnRiprova, btnEsci;
    Text introTesto, prompt, messaggio, gocciaTesto;
    PoligonoUI passoFill, zonaFango, zonaAcqua, marker;
    PoligonoUI acquaSecchio, fillAcqua, fillFango;
    PoligonoUI[] pipScivolate, pipFango;
    Font font;

    // Stato
    float acquaChiara, acquaTorbida;
    int fango, scivolate, round;
    KeyCode tastoRichiesto;
    float timerFase, durataFase;
    float markerT, velocita, zonaStart, zonaW;

    static readonly Color acquaLimpida = new Color(0.357f, 0.722f, 0.902f);   // #5BB8E6
    static readonly Color acquaFango = new Color(0.545f, 0.42f, 0.29f);       // #8B6B4A
    static readonly Color terracotta = new Color(0.788f, 0.475f, 0.31f);      // #C9794F
    static readonly Color pipVuoto = new Color(0.239f, 0.275f, 0.298f);       // #3D464C

    float Totale { get { return acquaChiara + acquaTorbida; } }

    // ------------------------------------------------------------
    // AVVIO / CHIUSURA
    // ------------------------------------------------------------
    public void Avvia()
    {
        if (fase != Fase.Fermo) return;

        if (canvasGO == null) CostruisciUI();

        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        if (player == null && Camera.main != null)
            player = Camera.main.GetComponentInParent<PlayerFPS>();

        canvasGO.SetActive(true);
        if (player != null) player.canMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ResetPartita();
        MostraIntro();
    }

    void Chiudi()
    {
        fase = Fase.Fermo;
        StopAllCoroutines();
        if (canvasGO != null) canvasGO.SetActive(false);
        if (player != null) player.canMove = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ResetPartita()
    {
        acquaChiara = 0f;
        acquaTorbida = 0f;
        fango = 0;
        scivolate = 0;
        round = 0;
        AggiornaHUD();
    }

    void MostraIntro()
    {
        fase = Fase.Intro;
        introTesto.text = "Il torrente quasi in secca\n\n" + ricordoNonno + "\n\n" + presente + "\n\n" +
                          istruzioni + "\n\n[SPAZIO] per iniziare";
        introGroup.gameObject.SetActive(true);
        giocoGroup.gameObject.SetActive(false);
        btnRiprova.SetActive(false);
        btnEsci.SetActive(false);
        messaggio.text = "";
    }

    // ------------------------------------------------------------
    // CICLO DI GIOCO
    // ------------------------------------------------------------
    void Update()
    {
        switch (fase)
        {
            case Fase.Intro:
                if (Input.GetKeyDown(KeyCode.Space)) IniziaRound();
                break;
            case Fase.Passo:
                UpdatePasso();
                break;
            case Fase.Goccia:
                UpdateGoccia();
                break;
        }
    }

    float Progresso()
    {
        return Mathf.Clamp01((round - 1) / (float)Mathf.Max(1, goccieMax - 1));
    }

    void IniziaRound()
    {
        round++;
        durataFase = Mathf.Lerp(tempoPassoIniziale, tempoPassoFinale, Progresso());
        timerFase = durataFase;
        tastoRichiesto = Random.value < 0.5f ? KeyCode.A : KeyCode.D;

        fase = Fase.Passo;
        introGroup.SetActive(false);
        giocoGroup.SetActive(true);
        passoGroup.gameObject.SetActive(true);
        barraGroup.gameObject.SetActive(false);

        prompt.text = "Passo sulla roccia: premi  " + tastoRichiesto;
        messaggio.text = "";
        ImpostaPasso(1f);
        AggiornaHUD();
    }

    void UpdatePasso()
    {
        timerFase -= Time.deltaTime;
        ImpostaPasso(Mathf.Clamp01(timerFase / durataFase));

        if (Input.GetKeyDown(tastoRichiesto))
        {
            IniziaGoccia();
            return;
        }

        KeyCode altro = tastoRichiesto == KeyCode.A ? KeyCode.D : KeyCode.A;
        if (Input.GetKeyDown(altro) || timerFase <= 0f)
            Scivolata();
    }

    void IniziaGoccia()
    {
        fase = Fase.Goccia;
        passoGroup.SetActive(false);
        barraGroup.SetActive(true);

        float k = Progresso();
        zonaW = Mathf.Lerp(zonaPulitaIniziale, zonaPulitaFinale, k);
        velocita = Mathf.Lerp(velocitaIniziale, velocitaFinale, k);
        zonaStart = Random.Range(margineFango + 0.02f, 1f - zonaW - margineFango - 0.02f);
        markerT = Random.value * 2f;
        timerFase = tempoMaxGoccia;

        prompt.text = "Premi SPAZIO quando l'indicatore è nell'azzurro";
        DisegnaZone();
        MuoviMarker(Mathf.PingPong(markerT, 1f));
    }

    void UpdateGoccia()
    {
        markerT += Time.deltaTime * velocita;
        float p = Mathf.PingPong(markerT, 1f);
        MuoviMarker(p);

        timerFase -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ValutaGoccia(p);
        }
        else if (timerFase <= 0f)
        {
            messaggio.text = "Troppo lento: la goccia è caduta.";
            fase = Fase.Pausa;
            StartCoroutine(FineRound());
        }
    }

    void ValutaGoccia(float p)
    {
        bool pulita = p >= zonaStart && p <= zonaStart + zonaW;
        bool torbida = !pulita &&
                       p >= zonaStart - margineFango &&
                       p <= zonaStart + zonaW + margineFango;

        if (pulita)
        {
            acquaChiara += acquaPerGoccia;
            messaggio.text = "Acqua limpida! Una goccia in più.";
        }
        else if (torbida)
        {
            acquaTorbida += acquaPerFango;
            fango++;
            messaggio.text = "Hai preso del fango...";
        }
        else
        {
            messaggio.text = "Mancata: solo un filo d'acqua che scivola via.";
        }

        float t = Totale;
        if (t > 100f)
        {
            float k = 100f / t;
            acquaChiara *= k;
            acquaTorbida *= k;
        }

        fase = Fase.Pausa;
        StartCoroutine(FineRound());
    }

    void Scivolata()
    {
        scivolate++;

        float t = Totale;
        if (t > 0f)
        {
            float nuovo = Mathf.Max(0f, t - acquaPersaScivolata);
            float k = nuovo / t;
            acquaChiara *= k;
            acquaTorbida *= k;
        }

        messaggio.text = "Il piede scivola! Parte dell'acqua si rovescia.";
        fase = Fase.Pausa;
        StartCoroutine(FineRound());
    }

    IEnumerator FineRound()
    {
        AggiornaHUD();
        yield return new WaitForSeconds(1.1f);

        if (fango >= fangoMax)
        {
            Fine("L'acqua è troppo torbida: non si può bere.");
            yield break;
        }

        if (Totale >= 99.9f)
        {
            fase = Fase.Fine;
            messaggio.text = "Il secchio è pieno! Non è molta, ma è acqua pulita. Per lei vale oro.";
            passoGroup.SetActive(false);
            barraGroup.SetActive(false);
            prompt.text = "";
            yield return new WaitForSeconds(3f);
            Chiudi();
            if (onVittoria != null) onVittoria.Invoke();
            yield break;
        }

        if (scivolate >= scivolateMax)
        {
            Fine("Le rocce sono troppo scivolose: devi fermarti a riprendere fiato.");
            yield break;
        }

        if (round >= goccieMax)
        {
            Fine("Hai finito le forze prima di riempire il secchio.");
            yield break;
        }

        IniziaRound();
    }

    void Fine(string motivo)
    {
        fase = Fase.Fine;
        messaggio.text = motivo + " Vuoi riprovare?";
        passoGroup.SetActive(false);
        barraGroup.SetActive(false);
        prompt.text = "Per oggi basta così";
        btnRiprova.SetActive(true);
        btnEsci.SetActive(true);
    }

    void Riprova()
    {
        StopAllCoroutines();
        btnRiprova.SetActive(false);
        btnEsci.SetActive(false);
        ResetPartita();
        IniziaRound();
    }

    void Esci()
    {
        Chiudi();
        if (onSconfitta != null) onSconfitta.Invoke();
    }

    // ------------------------------------------------------------
    // AGGIORNAMENTO GRAFICA
    // ------------------------------------------------------------
    void AggiornaHUD()
    {
        gocciaTesto.text = Mathf.Max(1, Mathf.Min(round, goccieMax)) + "/" + goccieMax;

        for (int i = 0; i < pipScivolate.Length; i++)
            pipScivolate[i].color = i < scivolate ? terracotta : pipVuoto;
        for (int i = 0; i < pipFango.Length; i++)
            pipFango[i].color = i < fango ? acquaFango : pipVuoto;

        float f1 = Mathf.Clamp01(acquaChiara / 100f);
        float f2 = Mathf.Clamp01(Totale / 100f);
        ImpostaFill(fillAcqua, 0f, f1);
        ImpostaFill(fillFango, f1, f2);

        acquaSecchio.gameObject.SetActive(f2 > 0.001f);
        acquaSecchio.color = Color.Lerp(acquaLimpida, acquaFango, acquaTorbida / Mathf.Max(1f, Totale));
    }

    void ImpostaFill(PoligonoUI p, float a, float b)
    {
        bool visibile = b - a > 0.002f;
        p.gameObject.SetActive(visibile);
        if (!visibile) return;
        float xt0 = 70f + 100f * a, xt1 = 70f + 100f * b;
        float xb0 = 74f + 92f * a, xb1 = 74f + 92f * b;
        p.Imposta(Punti(new float[] { xt0, 366, xt1, 366, xb1, 378, xb0, 378 }));
    }

    float[] BarraPoly(float u1, float u2)
    {
        float xt1 = 190f + 300f * u1, xt2 = 190f + 300f * u2;
        float xb1 = 196f + 288f * u1, xb2 = 196f + 288f * u2;
        return new float[] { xt1, 104, xt2, 104, xb2, 134, xb1, 134 };
    }

    void ImpostaPasso(float r)
    {
        bool visibile = r > 0.002f;
        passoFill.gameObject.SetActive(visibile);
        if (visibile) passoFill.Imposta(Punti(BarraPoly(0f, r)));
    }

    void DisegnaZone()
    {
        float a = zonaStart, b = zonaStart + zonaW;
        zonaFango.Imposta(Punti(BarraPoly(Mathf.Clamp01(a - margineFango), Mathf.Clamp01(b + margineFango))));
        zonaAcqua.Imposta(Punti(BarraPoly(a, b)));
    }

    void MuoviMarker(float p)
    {
        float xt = 190f + 300f * p;
        float xb = 196f + 288f * p;
        marker.Imposta(Punti(new float[] { xt - 3, 98, xt + 3, 98, xb + 3, 140, xb - 3, 140 }));
    }

    // ------------------------------------------------------------
    // COSTRUZIONE UI
    // ------------------------------------------------------------
    void CostruisciUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasGO = new GameObject("QTEAcquaCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        RectTransform dim = Crea("Sfondo", canvasGO.transform, new Vector2(4000, 3000), Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        RectTransform pan = Crea("Pannello", canvasGO.transform, new Vector2(900, 640), Vector2.zero);
        pan.gameObject.AddComponent<Image>().color = Hex("#D6DEE2");

        RectTransform cornice = Crea("Cornice", pan, new Vector2(808, 483), new Vector2(0, 50));
        cornice.gameObject.AddComponent<Image>().color = Hex("#1F2A30");

        RectTransform scena = Crea("Scena", pan, Vector2.one, new Vector2(0, 50));
        CostruisciScena(scena);

        // ---------- Gioco ----------
        giocoGroup = Gruppo("Gioco", scena);

        // Secchio: acqua dentro, etichetta e barra di riempimento
        acquaSecchio = Poly(giocoGroup, acquaLimpida, 88, 263, 152, 263, 150, 276, 90, 276);
        TestoScena(giocoGroup, 120, 360, 100, 20, 15, TextAnchor.MiddleCenter, Hex("#F1F3EC")).text = "Secchio";
        Poly(giocoGroup, "#4D4A48", 70, 366, 170, 366, 166, 378, 74, 378);
        fillAcqua = Poly(giocoGroup, acquaLimpida, 70, 366, 100, 366, 96, 378, 74, 378);
        fillFango = Poly(giocoGroup, acquaFango, 100, 366, 118, 366, 114, 378, 96, 378);

        // Targa della barra
        Placca(giocoGroup, new Color(0.12f, 0.16f, 0.19f, 0.88f), 170, 52, 510, 162, 6);
        prompt = TestoScena(giocoGroup, 340, 82, 330, 24, 16, TextAnchor.MiddleCenter, Hex("#F2F5F7"));
        prompt.fontStyle = FontStyle.Bold;

        // Barra di sfondo (grigio topo)
        Poly(giocoGroup, "#8C8987", 190, 104, 490, 104, 484, 134, 196, 134);

        // Fase passo
        passoGroup = Gruppo("Passo", giocoGroup);
        passoFill = Poly(passoGroup, terracotta, 190, 104, 490, 104, 484, 134, 196, 134);

        // Fase goccia
        barraGroup = Gruppo("Barra", giocoGroup);
        zonaFango = Poly(barraGroup, acquaFango, 260, 104, 350, 104, 342, 134, 268, 134);
        zonaAcqua = Poly(barraGroup, acquaLimpida, 284, 104, 326, 104, 322, 134, 288, 134);
        marker = Poly(barraGroup, "#F2F5F7", 330, 98, 337, 98, 335, 140, 332, 140);

        Color legendaCol = Hex("#C7D2D8");
        Poly(barraGroup, acquaLimpida, 200, 146, 212, 146, 210, 156, 198, 156);
        TestoScena(barraGroup, 218, 155, 60, 18, 15, TextAnchor.MiddleLeft, legendaCol).text = "acqua";
        Poly(barraGroup, acquaFango, 290, 146, 302, 146, 300, 156, 288, 156);
        TestoScena(barraGroup, 308, 155, 60, 18, 15, TextAnchor.MiddleLeft, legendaCol).text = "fango";
        Poly(barraGroup, "#8C8987", 380, 146, 392, 146, 390, 156, 378, 156);
        TestoScena(barraGroup, 398, 155, 70, 18, 15, TextAnchor.MiddleLeft, legendaCol).text = "niente";

        // HUD a destra
        Placca(giocoGroup, new Color(0.12f, 0.16f, 0.19f, 0.88f), 526, 44, 650, 168, 6);
        TestoScena(giocoGroup, 540, 68, 70, 20, 15, TextAnchor.MiddleLeft, legendaCol).text = "Goccia";
        gocciaTesto = TestoScena(giocoGroup, 636, 68, 60, 22, 18, TextAnchor.MiddleRight, Hex("#F2F5F7"));
        TestoScena(giocoGroup, 540, 98, 90, 20, 15, TextAnchor.MiddleLeft, legendaCol).text = "Scivolate";
        TestoScena(giocoGroup, 540, 134, 90, 20, 15, TextAnchor.MiddleLeft, legendaCol).text = "Fango";

        int ns = Mathf.Clamp(scivolateMax, 1, 5);
        pipScivolate = new PoligonoUI[ns];
        for (int i = 0; i < ns; i++) pipScivolate[i] = Rombo(giocoGroup, 548 + i * 18, 110, 6, pipVuoto);

        int nf = Mathf.Clamp(fangoMax, 1, 5);
        pipFango = new PoligonoUI[nf];
        for (int i = 0; i < nf; i++) pipFango[i] = Rombo(giocoGroup, 548 + i * 18, 146, 6, pipVuoto);

        // ---------- Intro ----------
        introGroup = Gruppo("Intro", scena);
        Placca(introGroup, new Color(0.12f, 0.16f, 0.19f, 0.93f), 110, 50, 570, 350, 8);
        introTesto = TestoScena(introGroup, 340, 200, 430, 270, 19, TextAnchor.MiddleCenter, Hex("#F2F5F7"));

        // ---------- Messaggio e pulsanti (fuori dalla scena) ----------
        messaggio = CreaTesto("Messaggio", pan, new Vector2(820, 60), new Vector2(0, -225), 22,
                              TextAnchor.MiddleCenter, new Color(0.15f, 0.2f, 0.23f));
        btnRiprova = CreaBottone("Riprova", pan, "Riprova", new Vector2(-110, -288), Riprova);
        btnEsci = CreaBottone("Esci", pan, "Esci", new Vector2(110, -288), Esci);

        canvasGO.SetActive(false);
    }

    // Scena fissa: cielo, montagne, prato, alberi, torrente, rocce, secchio
    void CostruisciScena(Transform r)
    {
        // Cielo
        Poly(r, "#BFD7E8", 20, 10, 660, 10, 660, 210, 20, 210);
        Poly(r, "#B3CFE3", 20, 10, 340, 10, 250, 120, 20, 160);
        Poly(r, "#C9DEEC", 340, 10, 660, 10, 660, 140, 470, 100);
        Poly(r, "#BAD3E6", 250, 120, 340, 10, 470, 100, 400, 170);

        // Sole e nuvole
        Poly(r, "#F3F1E8", 54, 32, 70, 22, 86, 32, 86, 52, 70, 62, 54, 52);
        Poly(r, "#E2DFD2", 54, 32, 70, 22, 70, 42);
        Poly(r, "#E9F1F7", 190, 40, 200, 28, 218, 26, 232, 34, 240, 40);
        Poly(r, "#D5E3ED", 200, 28, 218, 26, 214, 40, 196, 40);
        Poly(r, "#E9F1F7", 400, 34, 410, 24, 426, 22, 438, 30, 446, 34);
        Poly(r, "#D5E3ED", 410, 24, 426, 22, 422, 34, 406, 34);

        // Montagne con poca neve
        Poly(r, "#7F8E9A", 20, 210, 120, 100, 230, 210);
        Poly(r, "#6A7985", 120, 100, 165, 210, 230, 210);
        Poly(r, "#EEF2F5", 120, 100, 100, 128, 120, 122, 138, 132);
        Poly(r, "#8796A2", 200, 210, 300, 80, 420, 210);
        Poly(r, "#707F8B", 300, 80, 350, 210, 420, 210);
        Poly(r, "#E4EAEE", 300, 80, 285, 104, 300, 100, 314, 108);
        Poly(r, "#7D8C98", 400, 210, 520, 120, 640, 210);
        Poly(r, "#66757F", 520, 120, 570, 210, 640, 210);

        // Prato (salvia e grigio-verde)
        Poly(r, "#7E8A6E", 20, 200, 660, 200, 660, 390, 20, 390);
        Poly(r, "#748064", 20, 200, 250, 200, 150, 290, 20, 330);
        Poly(r, "#8D9879", 250, 200, 450, 200, 560, 260, 420, 300);
        Poly(r, "#74806A", 560, 260, 660, 200, 660, 330, 580, 330);
        Poly(r, "#6B775D", 20, 330, 150, 290, 250, 390, 20, 390);
        Poly(r, "#85917A", 450, 300, 560, 260, 580, 330, 520, 390, 380, 390);

        // Pini
        Pino(r, 52, 160, 28, 14, 172, 28, 18);
        Pino(r, 86, 176, 20, 10, 186, 22, 14);
        Pino(r, 548, 176, 24, 12, 188, 24, 16);

        // Torrente: fango, filo d'acqua, crepe
        Poly(r, "#8F7A58", 322, 200, 352, 200, 336, 250, 384, 312, 350, 390, 262, 390, 296, 318, 304, 252);
        Poly(r, "#6DB6DB", 330, 212, 342, 212, 330, 250, 374, 312, 344, 370, 310, 370, 318, 320, 314, 254);
        Poly(r, "#5AA3C8", 330, 212, 342, 212, 336, 250, 336, 300, 318, 320, 314, 254);
        Linea(r, "#6F5A3E", 1.6f, 288, 372, 300, 360);
        Linea(r, "#6F5A3E", 1.6f, 300, 360, 296, 346);
        Linea(r, "#6F5A3E", 1.6f, 300, 360, 312, 364);

        // Rocce
        Poly(r, "#8C8C8F", 180, 350, 190, 322, 215, 312, 240, 328, 246, 352);
        Poly(r, "#A5A5A8", 190, 322, 215, 312, 218, 336, 196, 342);
        Poly(r, "#707073", 218, 336, 240, 328, 246, 352, 214, 352);
        Poly(r, "#8C8C8F", 392, 368, 400, 338, 428, 328, 452, 346, 456, 368);
        Poly(r, "#A5A5A8", 400, 338, 428, 328, 430, 350, 408, 356);
        Poly(r, "#707073", 430, 350, 452, 346, 456, 368, 424, 368);
        Poly(r, "#85858A", 480, 346, 488, 326, 508, 322, 522, 338, 520, 350);
        Poly(r, "#A0A0A5", 488, 326, 508, 322, 506, 338, 492, 342);
        Poly(r, "#8C8C8F", 520, 380, 530, 360, 552, 356, 570, 372, 572, 384);
        Poly(r, "#A5A5A8", 530, 360, 552, 356, 552, 370, 534, 374);

        // Cespugli secchi e sassolini
        Poly(r, "#5F6B4C", 420, 306, 428, 290, 444, 286, 458, 298, 460, 308);
        Poly(r, "#76835D", 428, 290, 444, 286, 442, 298, 432, 300);
        Poly(r, "#5F6B4C", 28, 358, 34, 342, 48, 338, 60, 348, 62, 360);
        Poly(r, "#76835D", 34, 342, 48, 338, 46, 350, 38, 352);
        Poly(r, "#7A7775", 240, 374, 246, 367, 254, 369, 257, 377, 247, 379);
        Poly(r, "#7A7775", 360, 350, 366, 344, 374, 346, 376, 354, 366, 356);

        // Secchio (manico, facce, bordo)
        Poly(r, "#3E2E1E", 86, 258, 92, 238, 110, 228, 130, 228, 148, 238, 154, 258, 149, 258, 144, 242, 128, 234, 112, 234, 96, 242, 91, 258);
        Poly(r, "#6B5238", 80, 255, 110, 255, 108, 345, 92, 345);
        Poly(r, "#8A6A48", 110, 255, 130, 255, 128, 345, 108, 345);
        Poly(r, "#5A4430", 130, 255, 160, 255, 148, 345, 128, 345);
        Poly(r, "#3E2E1E", 80, 255, 160, 255, 150, 263, 90, 263);
    }

    // Un pino: due triangoli sovrapposti con la faccia in ombra, più il tronco
    void Pino(Transform r, float cx, float y1, float h1, float w1, float y2, float h2, float w2)
    {
        float b1 = y1 + h1, b2 = y2 + h2;
        Poly(r, "#4F6B57", cx, y1, cx - w1, b1, cx + w1, b1);
        Poly(r, "#4F6B57", cx, y2, cx - w2, b2, cx + w2, b2);
        Poly(r, "#3F5A49", cx, y1, cx + w1, b1, cx, b1);
        Poly(r, "#3F5A49", cx, y2, cx + w2, b2, cx, b2);
        Poly(r, "#4A3A2C", cx - 3, b2, cx + 3, b2, cx + 3, b2 + 9, cx - 3, b2 + 9);
    }

    // ------------------------------------------------------------
    // HELPER GRAFICI
    // ------------------------------------------------------------
    static Color Hex(string h)
    {
        Color c = Color.white;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
    }

    // Coordinate del disegno -> coordinate locali della scena
    static Vector2 M(float x, float y)
    {
        return new Vector2((x - 340f) * S, -(y - 200f) * S);
    }

    static Vector2[] Punti(float[] xy)
    {
        Vector2[] v = new Vector2[xy.Length / 2];
        for (int i = 0; i < v.Length; i++) v[i] = M(xy[2 * i], xy[2 * i + 1]);
        return v;
    }

    PoligonoUI Poly(Transform parent, string hex, params float[] xy)
    {
        return Poly(parent, Hex(hex), xy);
    }

    PoligonoUI Poly(Transform parent, Color col, params float[] xy)
    {
        GameObject go = new GameObject("Poly", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.anchoredPosition = Vector2.zero;

        PoligonoUI p = go.AddComponent<PoligonoUI>();
        p.raycastTarget = false;
        p.color = col;
        p.canvasRenderer.cullTransparentMesh = false;
        p.Imposta(Punti(xy));
        return p;
    }
    PoligonoUI Rombo(Transform parent, float cx, float cy, float r, Color col)
    {
        return Poly(parent, col, cx, cy - r, cx + r, cy, cx, cy + r, cx - r, cy);
    }

    // Rettangolo con gli angoli smussati
    PoligonoUI Placca(Transform parent, Color col, float x0, float y0, float x1, float y1, float c)
    {
        return Poly(parent, col,
            x0 + c, y0, x1 - c, y0, x1, y0 + c, x1, y1 - c,
            x1 - c, y1, x0 + c, y1, x0, y1 - c, x0, y0 + c);
    }

    // Segmento sottile
    void Linea(Transform parent, string hex, float spessore, float x1, float y1, float x2, float y2)
    {
        Vector2 d = new Vector2(x2 - x1, y2 - y1).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * (spessore / 2f);
        Poly(parent, hex, x1 + n.x, y1 + n.y, x2 + n.x, y2 + n.y, x2 - n.x, y2 - n.y, x1 - n.x, y1 - n.y);
    }

    Transform Gruppo(string nome, Transform parent)
    {
        return Crea(nome, parent, Vector2.one, Vector2.zero);
    }

    Text TestoScena(Transform parent, float sx, float sy, float w, float h, int fontSize, TextAnchor anchor, Color col)
    {
        Text t = CreaTesto("Testo", parent, new Vector2(w * S, h * S), Vector2.zero, fontSize, anchor, col);
        RectTransform rt = t.rectTransform;

        float px = 0.5f;
        if (anchor == TextAnchor.MiddleLeft) px = 0f;
        else if (anchor == TextAnchor.MiddleRight) px = 1f;

        rt.pivot = new Vector2(px, 0.5f);
        rt.anchoredPosition = M(sx, sy - 5f);
        return t;
    }

    RectTransform Crea(string nome, Transform parent, Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    Text CreaTesto(string nome, Transform parent, Vector2 size, Vector2 pos, int fontSize, TextAnchor anchor, Color col)
    {
        RectTransform rt = Crea(nome, parent, size, pos);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = col;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    GameObject CreaBottone(string nome, Transform parent, string etichetta, Vector2 pos, UnityAction azione)
    {
        RectTransform rt = Crea(nome, parent, new Vector2(180, 50), pos);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(azione);

        Text t = CreaTesto("Testo", rt, new Vector2(180, 50), Vector2.zero, 24, TextAnchor.MiddleCenter, Color.white);
        t.text = etichetta;
        return rt.gameObject;
    }
}
public static class TransformExtensions
{
    public static void SetActive(this Transform t, bool attivo)
    {
        t.gameObject.SetActive(attivo);
    }
}