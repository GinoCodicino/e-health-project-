using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class MemoryMinigame : MonoBehaviour
{
    [System.Serializable]
    public class Animale
    {
        public string nome;
        public Color colore = Color.white;
        public Sprite sprite;            // opzionale: se vuoto mostra solo colore + nome
        public bool vitellino;
        [TextArea(1, 3)] public string frase;
    }

    [Header("Regole")]
    public int colonne = 4;
    public int righe = 3;
    public float tempoTotale = 120f;
    public float tempoErrore = 0.9f;

    [Header("Retri Carte (Opzionali)")]
    public Sprite spriteRoccia;          // Trascina qui lo sprite della roccia
    public Sprite spriteCespuglio;       // Trascina qui lo sprite del cespuglio
    
    [Header("Sfondo")]
    public Sprite immagineSfondo;
    
    [Header("Animali (uno per coppia: 6 per la griglia 4x3)")]
    public Animale[] animali;

    [Header("Riferimenti")]
    public PlayerFPS player;

    [Header("Eventi")]
    public UnityEvent onVittoria;
    public UnityEvent onSconfitta;

    public bool InCorso { get { return canvasGO != null && canvasGO.activeSelf; } }

    class Carta
    {
        public int id;
        public RectTransform rt;
        public Image sfondo, immagine;
        public Text testo;
        public CanvasGroup gruppo;
        public bool scoperta, risolta;
        public string retro;
        public Color retroColore;
        public Sprite retroSprite;       // Riferimento allo sprite del retro per questa carta
    }

    GameObject canvasGO;
    Image pannello;
    Text titolo, timerTesto, messaggio;
    GameObject btnRiprova, btnEsci;
    Font font;

    readonly List<Carta> carte = new List<Carta>();
    Carta primo;
    bool attivo, bloccato;
    float tempoRimasto;

    static readonly Color cieloFresco = new Color(0.78f, 0.88f, 0.95f);
    static readonly Color cieloCaldo = new Color(0.95f, 0.5f, 0.28f);

    // ------------------------------------------------------------
    // Valori di default (compilati quando aggiungi il componente)
    // ------------------------------------------------------------
    void Reset() { animali = Predefiniti(); }

    static Animale[] Predefiniti()
    {
        return new Animale[]
        {
            new Animale { nome = "Vitellino", colore = new Color(0.96f, 0.82f, 0.5f), vitellino = true,
                frase = "Il vitellino! È debole, ma sta bene." },
            new Animale { nome = "Marmotta", colore = new Color(0.68f, 0.52f, 0.36f),
                frase = "La marmotta scappa: era lontana dalla sua tana, in cerca d'ombra." },
            new Animale { nome = "Volpe", colore = new Color(0.92f, 0.5f, 0.2f),
                frase = "La volpe scappa: è scesa fin qui perché in alto non trova più cibo." },
            new Animale { nome = "Stambecco", colore = new Color(0.62f, 0.62f, 0.62f),
                frase = "Lo stambecco scappa: sale sempre più su per fuggire il caldo." },
            new Animale { nome = "Camoscio", colore = new Color(0.55f, 0.42f, 0.28f),
                frase = "Il camoscio scappa: il suo pascolo è secco da settimane." },
            new Animale { nome = "Aquila", colore = new Color(0.4f, 0.4f, 0.5f),
                frase = "L'aquila vola via: le correnti non sono più quelle di una volta." },
        };
    }

    // ------------------------------------------------------------
    // AVVIO / CHIUSURA
    // ------------------------------------------------------------
    public void Avvia()
    {
        if (attivo) return;

        if (animali == null || animali.Length == 0) animali = Predefiniti();
        if (animali.Length * 2 != colonne * righe)
        {
            Debug.LogError("MemoryMinigame: servono " + (colonne * righe / 2) +
                           " animali per una griglia " + colonne + "x" + righe);
            return;
        }

        if (canvasGO == null) CostruisciUI();

        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        if (player == null && Camera.main != null)
            player = Camera.main.GetComponentInParent<PlayerFPS>();

        PreparaCarte();

        btnRiprova.SetActive(false);
        btnEsci.SetActive(false);
        titolo.text = "Chi si nasconde tra le rocce?";
        messaggio.text = "Trova le coppie di animali. Da qualche parte c'è un vitellino smarrito...";

        canvasGO.SetActive(true);

        if (player != null) player.canMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        tempoRimasto = tempoTotale;
        primo = null;
        bloccato = false;
        attivo = true;
    }

    void Chiudi()
    {
        attivo = false;
        if (canvasGO != null) canvasGO.SetActive(false);
        if (player != null) player.canMove = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ------------------------------------------------------------
    // LOGICA
    // ------------------------------------------------------------
    void Update()
    {
        if (!attivo) return;

        tempoRimasto -= Time.deltaTime;

        int s = Mathf.CeilToInt(Mathf.Max(0f, tempoRimasto));
        timerTesto.text = (s / 60) + ":" + (s % 60).ToString("00");
        timerTesto.color = tempoRimasto < 20f ? new Color(0.75f, 0.1f, 0.1f) : new Color(0.15f, 0.15f, 0.15f);

        float caldo = 1f - Mathf.Clamp01(tempoRimasto / tempoTotale);
        pannello.color = Color.Lerp(cieloFresco, cieloCaldo, caldo);

        if (tempoRimasto <= 0f) TempoScaduto();
    }

    void ClickCarta(Carta c)
    {
        if (!attivo || bloccato || c.scoperta || c.risolta) return;

        StartCoroutine(Gira(c, true));

        if (primo == null)
        {
            primo = c;
            return;
        }

        Carta a = primo;
        primo = null;
        StartCoroutine(Verifica(a, c));
    }

    IEnumerator Verifica(Carta a, Carta b)
    {
        bloccato = true;
        yield return new WaitForSeconds(0.4f);

        if (a.id == b.id)
        {
            a.risolta = b.risolta = true;
            Animale an = animali[a.id];

            if (an.vitellino)
            {
                yield return Vittoria(a, b);
                yield break;
            }

            messaggio.text = an.frase;
            yield return Fuga(a, b);
        }
        else
        {
            yield return new WaitForSeconds(tempoErrore);
            StartCoroutine(Gira(a, false));
            StartCoroutine(Gira(b, false));
            yield return new WaitForSeconds(0.35f);
        }

        bloccato = false;
    }

    IEnumerator Vittoria(Carta a, Carta b)
    {
        attivo = false;
        a.sfondo.color = b.sfondo.color = new Color(0.6f, 0.95f, 0.6f);
        titolo.text = "Trovato!";
        messaggio.text = "Hai trovato il vitellino! Il caldo non l'ha piegato, per fortuna.";

        yield return new WaitForSeconds(3f);

        Chiudi();
        if (onVittoria != null) onVittoria.Invoke();
    }

    void TempoScaduto()
    {
        attivo = false;
        StopAllCoroutines();
        bloccato = false;

        titolo.text = "Il sole è troppo alto";
        timerTesto.text = "0:00";
        messaggio.text = "Il caldo ha disperso tutti gli animali. Vuoi riprovare?";
        btnRiprova.SetActive(true);
        btnEsci.SetActive(true);
    }

    void Riprova()
    {
        attivo = false;
        Avvia();
    }

    void Esci()
    {
        Chiudi();
        if (onSconfitta != null) onSconfitta.Invoke();
    }

    // ------------------------------------------------------------
    // ANIMAZIONI E GESTIONE SPRITE
    // ------------------------------------------------------------
    IEnumerator Gira(Carta c, bool scopri)
    {
        float d = 0.15f;
        for (float t = 0; t < d; t += Time.deltaTime)
        {
            c.rt.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / d), 1f, 1f);
            yield return null;
        }

        c.scoperta = scopri;
        AggiornaFaccia(c);

        for (float t = 0; t < d; t += Time.deltaTime)
        {
            c.rt.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / d), 1f, 1f);
            yield return null;
        }
        c.rt.localScale = Vector3.one;
    }

    IEnumerator Fuga(Carta a, Carta b)
    {
        Vector2 pa = a.rt.anchoredPosition, pb = b.rt.anchoredPosition;
        float d = 0.7f;

        for (float t = 0; t < d; t += Time.deltaTime)
        {
            float k = t / d;
            a.gruppo.alpha = b.gruppo.alpha = 1f - k;
            a.rt.anchoredPosition = pa + new Vector2(-60f, 40f) * k;
            b.rt.anchoredPosition = pb + new Vector2(60f, 40f) * k;
            yield return null;
        }

        a.rt.gameObject.SetActive(false);
        b.rt.gameObject.SetActive(false);
    }

    void AggiornaFaccia(Carta c)
    {
        Animale an = animali[c.id];

        if (c.scoperta)
        {
            c.sfondo.color = an.colore;
            if (an.sprite != null)
            {
                c.immagine.sprite = an.sprite;
                c.immagine.enabled = true;
                c.testo.text = ""; // Nasconde il nome se c'è l'immagine
            }
            else
            {
                c.immagine.enabled = false;
                c.testo.text = an.nome;
            }
        }
        else
        {
            c.sfondo.color = c.retroColore;
            if (c.retroSprite != null)
            {
                c.immagine.sprite = c.retroSprite;
                c.immagine.enabled = true;
                c.testo.text = ""; // Nasconde "Roccia" o "Cespuglio" se c'è l'immagine
            }
            else
            {
                c.immagine.enabled = false;
                c.testo.text = c.retro;
            }
        }
    }

    // ------------------------------------------------------------
    // COSTRUZIONE UI
    // ------------------------------------------------------------
    void PreparaCarte()
    {
        foreach (Carta c in carte)
            if (c.rt != null) Destroy(c.rt.gameObject);
        carte.Clear();

        // Mazzo: ogni animale due volte, poi mescolato
        List<int> mazzo = new List<int>();
        for (int i = 0; i < animali.Length; i++) { mazzo.Add(i); mazzo.Add(i); }
        for (int i = mazzo.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = mazzo[i]; mazzo[i] = mazzo[j]; mazzo[j] = tmp;
        }

        Transform parent = pannello.transform;
        float cw = 180f, ch = 140f, sp = 20f, centroY = -25f;

        for (int i = 0; i < mazzo.Count; i++)
        {
            int col = i % colonne;
            int riga = i / colonne;
            float x = (col - (colonne - 1) / 2f) * (cw + sp);
            float y = ((righe - 1) / 2f - riga) * (ch + sp) + centroY;

            RectTransform rt = Crea("Carta" + i, parent, new Vector2(cw, ch), new Vector2(x, y));
            Image img = rt.gameObject.AddComponent<Image>();
            CanvasGroup cg = rt.gameObject.AddComponent<CanvasGroup>();
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            RectTransform sprRt = Crea("Sprite", rt, new Vector2(cw - 30f, ch - 60f), new Vector2(0, 12f));
            Image spr = sprRt.gameObject.AddComponent<Image>();
            spr.preserveAspect = true;
            spr.raycastTarget = false;
            spr.enabled = false;

            Text t = CreaTesto("Testo", rt, new Vector2(cw - 10f, 36f), new Vector2(0, -ch / 2f + 24f),
                               22, TextAnchor.MiddleCenter, Color.white);
            t.rectTransform.anchoredPosition = Vector2.zero;
            t.rectTransform.sizeDelta = new Vector2(cw - 10f, ch - 10f);

            bool roccia = (i % 2 == 0);
            Carta carta = new Carta
            {
                id = mazzo[i],
                rt = rt,
                sfondo = img,
                immagine = spr,
                testo = t,
                gruppo = cg,
                retro = roccia ? "Roccia" : "Cespuglio",
                retroColore = roccia ? new Color(0.45f, 0.45f, 0.47f) : new Color(0.62f, 0.6f, 0.3f),
                retroSprite = roccia ? spriteRoccia : spriteCespuglio // Assegna lo sprite personalizzato al retro
            };

            AggiornaFaccia(carta);
            btn.onClick.AddListener(() => ClickCarta(carta));
            carte.Add(carta);
        }
    }

    void CostruisciUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasGO = new GameObject("MemoryCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Sfondo a tutto schermo
        RectTransform dim = Crea("Sfondo", canvasGO.transform, new Vector2(4000, 3000), Vector2.zero);
        Image sfondoImg = dim.gameObject.AddComponent<Image>();

        if (immagineSfondo != null)
        {
            sfondoImg.sprite = immagineSfondo;
            sfondoImg.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Leggermente scurito per far leggere i testi
        }
        else
        {
            sfondoImg.color = new Color(0, 0, 0, 0.8f);
        }

        // Pannello centrale
        RectTransform pan = Crea("Pannello", canvasGO.transform, new Vector2(900, 640), Vector2.zero);
        pannello = pan.gameObject.AddComponent<Image>();

        // Se hai messo l'immagine, rendiamo il pannello dietro le carte trasparente
        pannello.color = immagineSfondo != null ? new Color(0, 0, 0, 0.3f) : cieloFresco;

        Color scuro = new Color(0.15f, 0.15f, 0.15f);
        titolo = CreaTesto("Titolo", pan, new Vector2(800, 50), new Vector2(0, 285), 32, TextAnchor.MiddleCenter, scuro);
        titolo.fontStyle = FontStyle.Bold;
        timerTesto = CreaTesto("Timer", pan, new Vector2(300, 50), new Vector2(0, 232), 40, TextAnchor.MiddleCenter, scuro);
        timerTesto.fontStyle = FontStyle.Bold;
        messaggio = CreaTesto("Messaggio", pan, new Vector2(800, 60), new Vector2(0, -288), 22, TextAnchor.MiddleCenter, scuro);

        btnRiprova = CreaBottone("Riprova", pan, "Riprova", new Vector2(-110, -240), Riprova);
        btnEsci = CreaBottone("Esci", pan, "Esci", new Vector2(110, -240), Esci);

        canvasGO.SetActive(false);
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