using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class RaccoltaLegnetti : MonoBehaviour
{
    [Header("Immagini (PNG importati come Sprite)")]
    public Sprite sfondo;
    public Sprite cesta;
    public Sprite[] legnetti = new Sprite[3];
    public Sprite pigna;

    [Header("Regole")]
    public int legnettiDaRaccogliere = 12;   // la catasta ne mostra al massimo 12
    public int vite = 3;
    [Range(0f, 0.6f)] public float probabilitaPigna = 0.25f;

    [Header("Velocità (pixel al secondo, bassa di proposito)")]
    public float cadutaIniziale = 150f;
    public float cadutaFinale = 240f;
    public float intervalloIniziale = 1.5f;
    public float intervalloFinale = 1.0f;
    public float velocitaCesta = 520f;

    [Header("Dimensioni (pixel)")]
    public float larghezzaCesta = 150f;
    public float dimensioneOggetto = 76f;

    [Header("Testi")]
    [TextArea(3, 6)]
    public string ricordoNonno =
        "Dal taccuino del nonno:\n«Su un sentiero incontrai un bambino che passava i pomeriggi nel bosco a osservare più specie possibile: scoiattoli, picchi, ghiri. Ogni giorno ne scopriva una nuova.»";
    [TextArea(3, 6)]
    public string presente =
        "Oggi il bosco ha perso molti alberi e la fauna si è ritirata. Quel bambino è diventato un adulto che costruisce rifugi per animali: cataste di legno che fanno ombra e tana.";
    [TextArea(3, 6)]
    public string istruzioni =
        "[← →] oppure [A] [D] = muovi la cesta\nRaccogli i legnetti per costruire il rifugio. Evita le pigne: non servono!";

    [Header("Riferimenti")]
    public PlayerFPS player;

    [Header("Eventi")]
    public UnityEvent onVittoria;
    public UnityEvent onSconfitta;

    public bool InCorso { get { return canvasGO != null && canvasGO.activeSelf; } }

    enum Fase { Fermo, Intro, Gioco, Fine }
    Fase fase = Fase.Fermo;

    const float campoW = 740f;
    const float campoH = 560f;
    const float colonnaX = 385f;
    const float catastaY = -150f;
    const float catastaS = 46f;

    class Oggetto
    {
        public RectTransform rt;
        public bool pigna;
        public Sprite sprite;
        public Color fallback;
        public float vel, rotVel, size;
    }

    GameObject canvasGO, introGroup, btnRiprova, btnEsci;
    RectTransform pan, campo, oggettiRoot, cestaRt;
    Image flash;
    Text introTesto, contatore, messaggio;
    Image[] pipVite;
    Font font;

    readonly List<Oggetto> oggetti = new List<Oggetto>();
    readonly List<GameObject> catasta = new List<GameObject>();

    int raccolti, viteRimaste, pignaDiFila;
    float cestaX, cestaW, cestaH, cestaY, scossa, timerSpawn;

    static readonly Color terracotta = new Color(0.788f, 0.475f, 0.31f);
    static readonly Color pipVuoto = new Color(0.239f, 0.275f, 0.298f);

    int Obiettivo { get { return Mathf.Clamp(legnettiDaRaccogliere, 1, 12); } }
    float Progresso() { return Mathf.Clamp01(raccolti / (float)Obiettivo); }

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

    [ContextMenu("Prova (solo in Play)")]
    void Prova()
    {
        if (Application.isPlaying) Avvia();
    }

    void Chiudi()
    {
        fase = Fase.Fermo;
        StopAllCoroutines();
        PulisciOggetti();
        if (canvasGO != null) canvasGO.SetActive(false);
        if (player != null) player.canMove = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ResetPartita()
    {
        StopAllCoroutines();
        PulisciOggetti();
        foreach (GameObject g in catasta) if (g != null) Destroy(g);
        catasta.Clear();

        raccolti = 0;
        viteRimaste = Mathf.Max(1, vite);
        pignaDiFila = 0;
        cestaX = 0f;
        scossa = 0f;
        timerSpawn = 0.8f;
        flash.color = new Color(0.79f, 0.2f, 0.15f, 0f);
        messaggio.text = "Muovi la cesta con le frecce.";
        btnRiprova.SetActive(false);
        btnEsci.SetActive(false);
        AggiornaHUD();
    }

    void MostraIntro()
    {
        fase = Fase.Intro;
        introTesto.text = ricordoNonno + "\n\n" + presente + "\n\n" + istruzioni + "\n\n[SPAZIO] per iniziare";
        introGroup.SetActive(true);
    }

    void PulisciOggetti()
    {
        foreach (Oggetto o in oggetti)
            if (o.rt != null) Destroy(o.rt.gameObject);
        oggetti.Clear();
    }

    // ------------------------------------------------------------
    // CICLO DI GIOCO
    // ------------------------------------------------------------
    void Update()
    {
        if (fase == Fase.Intro)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                introGroup.SetActive(false);
                fase = Fase.Gioco;
            }
            return;
        }

        if (fase != Fase.Gioco) return;

        MuoviCesta();
        AggiornaOggetti();
        if (fase != Fase.Gioco) return;

        timerSpawn -= Time.deltaTime;
        if (timerSpawn <= 0f)
        {
            Genera();
            timerSpawn = Mathf.Lerp(intervalloIniziale, intervalloFinale, Progresso());
        }
    }

    void MuoviCesta()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float limite = campoW / 2f - cestaW / 2f;
        cestaX = Mathf.Clamp(cestaX + h * velocitaCesta * Time.deltaTime, -limite, limite);
        cestaRt.anchoredPosition = new Vector2(cestaX + scossa, cestaY);
    }

    void AggiornaOggetti()
    {
        float dt = Time.deltaTime;

        for (int i = oggetti.Count - 1; i >= 0; i--)
        {
            Oggetto o = oggetti[i];
            Vector2 p = o.rt.anchoredPosition;
            p.y -= o.vel * dt;
            o.rt.anchoredPosition = p;
            o.rt.Rotate(0f, 0f, o.rotVel * dt);

            float bocca = cestaY + cestaH * 0.25f;
            bool altezza = (p.y - o.size * 0.3f) <= bocca && p.y > cestaY - cestaH * 0.2f;
            bool larghezza = Mathf.Abs(p.x - cestaX) < cestaW * 0.45f;

            if (altezza && larghezza)
            {
                oggetti.RemoveAt(i);
                Destroy(o.rt.gameObject);
                Presa(o);
                if (fase != Fase.Gioco) return;
                continue;
            }

            if (p.y < -campoH / 2f - o.size)
            {
                oggetti.RemoveAt(i);
                Destroy(o.rt.gameObject);
            }
        }
    }

    void Presa(Oggetto o)
    {
        if (o.pigna)
        {
            viteRimaste--;
            messaggio.text = "Una pigna! Non serve per il rifugio.";
            StartCoroutine(Scuoti());
            StartCoroutine(Lampo());
            AggiornaHUD();

            if (viteRimaste <= 0) Fine("Troppe pigne nella cesta.");
            return;
        }

        raccolti++;
        AggiungiAllaCatasta(o.sprite, o.fallback);
        messaggio.text = "Un legnetto in più per il rifugio.";
        AggiornaHUD();

        if (raccolti >= Obiettivo) StartCoroutine(Vittoria());
    }

    IEnumerator Vittoria()
    {
        fase = Fase.Fine;
        PulisciOggetti();
        messaggio.text = "Il rifugio è pronto! Ora gli animali avranno ombra e una tana.";
        yield return new WaitForSeconds(3f);
        Chiudi();
        if (onVittoria != null) onVittoria.Invoke();
    }

    void Fine(string motivo)
    {
        fase = Fase.Fine;
        PulisciOggetti();
        messaggio.text = motivo + " Vuoi riprovare?";
        btnRiprova.SetActive(true);
        btnEsci.SetActive(true);
    }

    void Riprova()
    {
        ResetPartita();
        introGroup.SetActive(false);
        fase = Fase.Gioco;
    }

    void Esci()
    {
        Chiudi();
        if (onSconfitta != null) onSconfitta.Invoke();
    }

    IEnumerator Scuoti()
    {
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            scossa = Mathf.Sin(t * 70f) * 10f * (1f - t / 0.3f);
            yield return null;
        }
        scossa = 0f;
    }

    IEnumerator Lampo()
    {
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            flash.color = new Color(0.79f, 0.2f, 0.15f, 0.4f * (1f - t / 0.35f));
            yield return null;
        }
        flash.color = new Color(0.79f, 0.2f, 0.15f, 0f);
    }

    // ------------------------------------------------------------
    // OGGETTI CHE CADONO
    // ------------------------------------------------------------
    void Genera()
    {
        bool eUnaPigna = Random.value < probabilitaPigna;
        if (eUnaPigna && pignaDiFila >= 2) eUnaPigna = false;
        pignaDiFila = eUnaPigna ? pignaDiFila + 1 : 0;

        Oggetto o = new Oggetto();
        o.pigna = eUnaPigna;
        o.size = eUnaPigna ? dimensioneOggetto * 0.8f : dimensioneOggetto;

        Sprite s = null;
        Color fb;
        if (eUnaPigna)
        {
            s = pigna;
            fb = Hex("#5C4A32");
        }
        else
        {
            int tipo = Random.Range(0, 3);
            if (legnetti != null && legnetti.Length > 0)
                s = legnetti[tipo % legnetti.Length];
            fb = tipo == 0 ? Hex("#C9B79A") : (tipo == 1 ? Hex("#A07F5A") : Hex("#8A6A48"));
        }
        o.sprite = s;
        o.fallback = fb;

        GameObject go = new GameObject(eUnaPigna ? "Pigna" : "Legnetto", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(oggettiRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        if (s != null)
        {
            img.sprite = s;
            img.preserveAspect = true;
            rt.sizeDelta = new Vector2(o.size, o.size);
        }
        else
        {
            img.color = fb;
            rt.sizeDelta = eUnaPigna ? new Vector2(o.size * 0.6f, o.size * 0.75f) : new Vector2(o.size, o.size * 0.4f);
        }

        float margine = o.size * 0.6f;
        float x = Random.Range(-campoW / 2f + margine, campoW / 2f - margine);
        rt.anchoredPosition = new Vector2(x, campoH / 2f + o.size);
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-30f, 30f));

        o.rt = rt;
        o.vel = Mathf.Lerp(cadutaIniziale, cadutaFinale, Progresso()) * Random.Range(0.9f, 1.1f);
        o.rotVel = Random.Range(-40f, 40f);
        oggetti.Add(o);
    }

    // ------------------------------------------------------------
    // CATASTA (il rifugio che cresce)
    // ------------------------------------------------------------
    void AggiungiAllaCatasta(Sprite sprite, Color fallback)
    {
        int[] perRiga = { 5, 4, 3 };
        int resto = catasta.Count;
        int riga = 0;
        while (riga < perRiga.Length && resto >= perRiga[riga])
        {
            resto -= perRiga[riga];
            riga++;
        }
        if (riga >= perRiga.Length) return;

        int n = perRiga[riga];
        float x = colonnaX + (resto - (n - 1) / 2f) * catastaS;
        float y = catastaY + riga * catastaS * 0.78f;

        GameObject go = new GameObject("Tronco", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(pan, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-8f, 8f));

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
            rt.sizeDelta = new Vector2(catastaS, catastaS);
        }
        else
        {
            img.color = fallback;
            rt.sizeDelta = new Vector2(catastaS, catastaS * 0.5f);
        }

        catasta.Add(go);
    }

    void AggiornaHUD()
    {
        contatore.text = "Legnetti raccolti: " + raccolti + "/" + Obiettivo;
        for (int i = 0; i < pipVite.Length; i++)
            pipVite[i].color = i < viteRimaste ? terracotta : pipVuoto;
    }

    // ------------------------------------------------------------
    // COSTRUZIONE UI
    // ------------------------------------------------------------
    void CostruisciUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasGO = new GameObject("RaccoltaLegnettiCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Color scuro = new Color(0.15f, 0.2f, 0.23f);

        RectTransform dim = Crea("Sfondo", canvasGO.transform, new Vector2(4000, 3000), Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        pan = Crea("Pannello", canvasGO.transform, new Vector2(1100, 640), Vector2.zero);
        pan.gameObject.AddComponent<Image>().color = Hex("#D6DEE2");

        // Campo di gioco
        campo = Crea("Campo", pan, new Vector2(campoW, campoH), new Vector2(-165, -10));
        Image campoImg = campo.gameObject.AddComponent<Image>();
        campoImg.raycastTarget = false;
        if (sfondo != null)
        {
            campoImg.sprite = sfondo;
            campoImg.color = Color.white;
        }
        else
        {
            campoImg.color = Hex("#BFD7E8");
            RectTransform terreno = Crea("Terreno", campo, new Vector2(campoW, 110), new Vector2(0, -campoH / 2f + 55f));
            Image t = terreno.gameObject.AddComponent<Image>();
            t.color = Hex("#7E8A6E");
            t.raycastTarget = false;
        }
        campo.gameObject.AddComponent<RectMask2D>();

        oggettiRoot = Crea("Oggetti", campo, Vector2.zero, Vector2.zero);

        // Cesta
        cestaW = larghezzaCesta;
        cestaH = cesta != null && cesta.rect.width > 0f
                 ? cestaW * cesta.rect.height / cesta.rect.width
                 : cestaW * 0.6f;
        cestaY = -campoH / 2f + cestaH / 2f + 10f;

        cestaRt = Crea("Cesta", campo, new Vector2(cestaW, cestaH), new Vector2(0, cestaY));
        Image cestaImg = cestaRt.gameObject.AddComponent<Image>();
        cestaImg.raycastTarget = false;
        if (cesta != null) { cestaImg.sprite = cesta; cestaImg.preserveAspect = true; }
        else cestaImg.color = Hex("#A07F5A");

        // Lampo rosso quando prendi una pigna
        RectTransform fl = Crea("Lampo", campo, new Vector2(campoW, campoH), Vector2.zero);
        flash = fl.gameObject.AddComponent<Image>();
        flash.color = new Color(0.79f, 0.2f, 0.15f, 0f);
        flash.raycastTarget = false;

        // Colonna di destra
        Text titolo = Testo(pan, new Vector2(colonnaX, 275), new Vector2(300, 40), 26, TextAnchor.MiddleCenter, scuro);
        titolo.fontStyle = FontStyle.Bold;
        titolo.text = "Rifugio per animali";

        contatore = Testo(pan, new Vector2(colonnaX, 235), new Vector2(300, 30), 20, TextAnchor.MiddleCenter, scuro);

        Testo(pan, new Vector2(colonnaX - 60, 195), new Vector2(80, 28), 20, TextAnchor.MiddleCenter, scuro).text = "Vite";
        int nv = Mathf.Clamp(vite, 1, 6);
        pipVite = new Image[nv];
        for (int i = 0; i < nv; i++)
        {
            RectTransform pr = Crea("Vita", pan, new Vector2(22, 22), new Vector2(colonnaX + i * 30 - 10, 195));
            pr.localEulerAngles = new Vector3(0, 0, 45);
            pipVite[i] = pr.gameObject.AddComponent<Image>();
            pipVite[i].raycastTarget = false;
        }

        messaggio = Testo(pan, new Vector2(colonnaX, 80), new Vector2(290, 120), 20, TextAnchor.MiddleCenter, scuro);

        RectTransform terra = Crea("Terra", pan, new Vector2(260, 6), new Vector2(colonnaX, catastaY - catastaS / 2f - 2f));
        terra.gameObject.AddComponent<Image>().color = Hex("#6B775D");

        btnRiprova = CreaBottone("Riprova", pan, "Riprova", new Vector2(colonnaX, -228), Riprova);
        btnEsci = CreaBottone("Esci", pan, "Esci", new Vector2(colonnaX, -285), Esci);

        // Introduzione (copre tutto il pannello)
        introGroup = Crea("Intro", pan, Vector2.one, Vector2.zero).gameObject;
        RectTransform plate = Crea("Targa", introGroup.transform, new Vector2(1040, 580), Vector2.zero);
        plate.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.16f, 0.19f, 0.96f);
        introTesto = Testo(introGroup.transform, Vector2.zero, new Vector2(900, 500), 22, TextAnchor.MiddleCenter, Hex("#F2F5F7"));

        canvasGO.SetActive(false);
    }

    // ------------------------------------------------------------
    // HELPER
    // ------------------------------------------------------------
    static Color Hex(string h)
    {
        Color c = Color.white;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
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

    Text Testo(Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color col)
    {
        RectTransform rt = Crea("Testo", parent, size, pos);
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
        RectTransform rt = Crea(nome, parent, new Vector2(180, 46), pos);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(azione);

        Text t = Testo(rt, Vector2.zero, new Vector2(180, 46), 24, TextAnchor.MiddleCenter, Color.white);
        t.text = etichetta;
        return rt.gameObject;
    }
}