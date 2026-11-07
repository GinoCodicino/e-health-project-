using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Una sola macchina a stati decide quali azioni sono consentite.
// Lo stato dura per la sessione corrente: non viene salvato su disco.
public class ForestQuest : MonoBehaviour
{
    public enum Stage { Introduction, FindMushrooms, ReportMushrooms, FetchWater, ReturnWater, WaterGarden, Complete }
    [Header("Collegamenti")]
    public playercontroller player;
    public Transform riverPoint;
    public Transform gardenPoint;
    [Header("Personalizzazione")]
    public string speakerName = "Peter";
    public float interactionDistance = 3f;
    [Range(0f, 0.1f)] public float typingSpeed = 0.025f;
    [Range(0.2f, 0.95f)] public float dialogueOpacity = 0.72f;
    [Range(3, 8)] public int minimumMushrooms = 3;
    [Range(3, 8)] public int maximumMushrooms = 5;
    [Range(0f, 1f)] public float minimumWater = 0.50f;
    [Range(0f, 1f)] public float maximumWater = 0.75f;
    public Sprite forestBackground;
    [Header("Trova i funghi - scena")]
    [Tooltip("Sprite del fungo da trovare (opzionale). Se vuoto usa il fungo generato da codice. Lo sfondo si imposta in Forest Background.")]
    public Sprite mushroomSprite;
    [Tooltip("Sprite di alberi, cespugli, sassi, case... sparsi a caso nella scena. Se vuoto usa foglie e sassi generati da codice.")]
    public Sprite[] clutterSprites;
    [Tooltip("Quante volte la scena è più grande del riquadro (si sposta col mouse e si ingrandisce con la rotellina).")]
    [Range(1f, 3f)] public float sceneScale = 2f;
    [Header("Trova i funghi - difficoltà")]
    [Tooltip("Dimensione media dei funghi in pixel (più piccoli = più difficile).")]
    [Range(30f, 110f)] public float mushroomSize = 62f;
    [Tooltip("0 = colori vivaci, 1 = colori spenti e simili alle foglie.")]
    [Range(0f, 1f)] public float camouflage = 0.55f;
    [Tooltip("Foglie e sassi extra sparsi sopra lo sfondo (se lo sfondo è già molto ricco ne bastano pochi).")]
    [Range(0, 600)] public int decorations = 120;
    [Tooltip("Una foglia o un sasso copre in parte ogni fungo.")]
    public bool coverMushrooms = true;
    [Header("Secchio - prova di tempismo")]
    [Tooltip("Secondi da vuoto a pieno. Il ritorno richiede altrettanto tempo.")]
    [Range(1f, 8f)] public float bucketTravelSeconds = 2.5f;
    [Header("Segnale del fiume")]
    public bool showRiverMarker = true;
    public Color riverMarkerColor = new Color(0.15f, 0.8f, 1f);
    [Range(0.3f, 3f)] public float riverMarkerRadius = 1.2f;
    [Range(1f, 6f)] public float riverMarkerHeight = 2.5f;
    public Stage CurrentStage { get; private set; }
    public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }
    public int FoundCount { get { return found; } }

    GameObject uiRoot, page;
    RectTransform canvas;
    TMP_Text speech, counter, hint;
    ForestBucketArt bucketArt;
    TMP_Text bucketStatus;
    RectTransform bucketCursor;
    Button bucketAction;
    TMP_Text bucketActionCaption;
    bool bucketOpen, bucketRunning, bucketSucceeded;
    float bucketElapsed;
    GameObject riverMarker;
    LineRenderer riverRing, riverBeam;
    TextMeshPro riverLabel;
    Material riverMarkerMaterial;
    Coroutine typing;
    bool isTyping, oldCursorVisible;
    CursorLockMode oldCursorLock;
    int found, target, lineIndex;
    float water;
    string[] lines;
    Action afterLines;
    readonly HashSet<int> collected = new HashSet<int>();
    readonly List<Vector2> slots = new List<Vector2>();
    int roundSeed, misses;
    bool replaying;
    RectTransform view, world;
    float zoom = 1f, dragDist;
    bool dragging, dragMoved;
    Vector2 dragLast;

    protected virtual void Awake()
    {
        // Disattiva la vecchia raccolta e il quiz; non elimina asset condivisi.
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component != null && (component.GetType().Name == "FindMushroomsGame" || component.GetType().Name == "MushroomLearningQuest"))
                component.enabled = false;
        if (player == null) player = FindFirstObjectByType<playercontroller>();
        minimumMushrooms = Mathf.Clamp(minimumMushrooms, 3, 8);
        maximumMushrooms = Mathf.Clamp(maximumMushrooms, minimumMushrooms, 8);
        minimumWater = Mathf.Clamp(minimumWater, 0.05f, 0.95f);
        maximumWater = Mathf.Clamp(maximumWater, minimumWater, 1f);
    }

    protected virtual void Update()
    {
        UpdateRiverMarker();
        UpdateMushroomView();
        // Il cursore non dipende dal movimento del giocatore o da Time.timeScale.
        if (bucketOpen && bucketRunning && IsOpen)
        {
            bucketElapsed += Time.unscaledDeltaTime;
            water = Mathf.PingPong(bucketElapsed / Mathf.Max(1f, bucketTravelSeconds), 1f);
            UpdateWater();
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || player == null) return;
        if (IsOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) { CloseDialogue(); return; }
            // Spazio completa la riga; non attiva anche un pulsante selezionato.
            if (isTyping && keyboard.spaceKey.wasPressedThisFrame) ShowAll();
            return;
        }
        if (!keyboard.eKey.wasPressedThisFrame) return;
        if (CurrentStage == Stage.FetchWater && Near(riverPoint)) OpenBucket();
        else if (CurrentStage == Stage.WaterGarden && Near(gardenPoint))
        {
            CurrentStage = Stage.Complete;
            Speak(new[] { "Thank you! This water helps our little garden.",
                "Caring for the whole forest takes much more. Your grandfather would be proud of you." }, CloseDialogue);
        }
        else if (Near(transform)) OpenDialogue(player);
    }

    bool Near(Transform point) { return point != null && Vector3.Distance(player.transform.position, point.position) <= interactionDistance; }
    void OpenUI()
    {
        if (uiRoot == null)
        {
            canvas = MushroomUI.Canvas("ForestQuest_UI");
            uiRoot = canvas.gameObject;
            uiRoot.SetActive(false);
        }
        if (!uiRoot.activeSelf)
        {
            oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
            player.SetUiMode(this, true);
            uiRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
    void ClearPage(bool fullScreen)
    {
        OpenUI();
        world = null; view = null; dragging = false;
        bucketOpen = false; bucketRunning = false;
        if (typing != null) StopCoroutine(typing);
        typing = null; isTyping = false;
        if (page != null) { page.SetActive(false); Destroy(page); }
        Image panel = MushroomUI.Image("Page", canvas, new Color(0.025f, 0.065f, 0.04f, fullScreen ? 1f : dialogueOpacity));
        page = panel.gameObject;
        if (fullScreen) MushroomUI.Place(panel.rectTransform, 0, 0, 1200, 680);
        else
        {
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f, 0);
            panel.rectTransform.pivot = new Vector2(.5f, 0);
            panel.rectTransform.anchoredPosition = new Vector2(0, 20);
            panel.rectTransform.sizeDelta = new Vector2(1200, 300);
        }
    }
    TMP_Text Label(string text, float x, float y, float w, float h, float size = 24)
    {
        TMP_Text t = MushroomUI.Text("Text", page.transform, text, size, Color.white);
        // x indica il bordo SINISTRO del testo, rispetto al centro del pannello.
        // Con pivot centrale il testo iniziava mezza larghezza troppo a sinistra.
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        MushroomUI.Place(t.rectTransform, x, y, w, h);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Truncate;
        return t;
    }
    Button Choice(string text, float x, float y, float width, Action action)
    {
        TMP_Text caption;
        Button b = MushroomUI.Button("Choice", page.transform, text, new Color(.20f, .35f, .23f), out caption);
        caption.color = Color.white; caption.fontSize = 20;
        MushroomUI.Place((RectTransform)b.transform, x, y, width, 46);
        b.onClick.AddListener(() => { if (!isTyping) action(); });
        return b;
    }
    public void OpenDialogue(playercontroller who)
    {
        if (who != null) player = who;
        if (player == null || IsOpen) return;
        switch (CurrentStage)
        {
            case Stage.Introduction: Greeting(); break;
            case Stage.FindMushrooms:
                Speak(new[] { "Ready to look for the mushrooms I showed you?" }, SearchOffer); break;
            case Stage.ReportMushrooms:
                Speak(new[] { "You found them all! Thank you.",
                    "Could you bring some river water for my little garden? Take this bucket.",
                    "Follow the blue marker to the river. Stop the moving pointer in the green zone to fill the bucket." },
                    () => { CurrentStage = Stage.FetchWater; CloseDialogue(); }); break;
            case Stage.FetchWater:
                Speak(new[] { "Follow the blue marker to the river. Press E inside the blue ring to fill the bucket." }, CloseDialogue); break;
            case Stage.ReturnWater:
                Speak(new[] { "Just the right amount! Please water the little garden beside me." },
                    () => { CurrentStage = Stage.WaterGarden; CloseDialogue(); }); break;
            case Stage.WaterGarden:
                Speak(new[] { "Press E beside the little garden to water it." }, CloseDialogue); break;
            default:
                Speak(new[] { "Thank you for helping. Enjoy the rest of the trail!" }, CloseDialogue); break;
        }
    }
    void Greeting()
    {
        Speak(new[] { "Good morning, traveler! What brings you here?" }, () => { });
        // Le scelte sono visibili, ma diventano utilizzabili dopo la scrittura.
        Choice("I am your old friend's grandchild.", -390, -103, 390, () => Branch(0));
        Choice("Why are there so few mushrooms?", 10, -103, 390, () => Branch(1));
        Choice("Have you lived here long?", 410, -103, 370, () => Branch(2));
        // Rimuove il Next inutile dalla schermata iniziale.
        nextButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(false);
    }
    Button nextButton, closeButton;
    void Branch(int branch)
    {
        string[][] branches = {
            new [] { "Your grandfather! Sixty years ago, I walked this trail with him. I was just a child.",
                "You: Did you pick mushrooms together?", "Yes! We found many. These days the ground often stays too dry." },
            new [] { "We have found fewer lately. Long, hot, dry spells make it harder for mushrooms to appear.",
                "You: Could climate change be part of the reason?", "Yes. It can change the moisture and temperatures they need." },
            new [] { "Since I was a child! I often came here with your grandfather, sixty years ago.",
                "You: What was the forest like then?", "We found many mushrooms. The bridge was whole, and a giant pine stood by the trail." }
        };
        List<string> text = new List<string>(branches[branch]);
        text.Add("You: How can we help?");
        text.Add("We can protect the forest floor and avoid trampling it. Will you help me collect the mushrooms I point out?");
        Speak(text.ToArray(), QuestOffer);
    }
    void QuestOffer()
    {
        ClearPage(false);
        Label(speakerName, -550, 116, 1000, 30, 22);
        Label("Will you help me find the mushrooms?", -550, 35, 1100, 80);
        Choice("Yes, I will help!", -285, -100, 530, () => { CurrentStage = Stage.FindMushrooms; BeginMushroomGame(); });
        Choice("I will come back later.", 285, -100, 530, CloseDialogue);
    }
    void SearchOffer()
    {
        ClearPage(false);
        Label(speakerName, -550, 116, 1000, 30, 22);
        Label("Ready to continue your search?", -550, 35, 1100, 80);
        Choice("Find mushrooms", -285, -100, 530, BeginMushroomGame);
        Choice("Later", 285, -100, 530, CloseDialogue);
    }
    void Speak(string[] text, Action done)
    {
        ClearPage(false);
        lines = text; lineIndex = 0; afterLines = done;
        Label(speakerName, -550, 116, 1000, 30, 22);
        speech = Label("", -550, 30, 1100, 110, 25);
        nextButton = Choice("Next / Show all", 270, -103, 520, Next);
        // Questo pulsante deve funzionare anche durante l'effetto di scrittura.
        nextButton.onClick.RemoveAllListeners(); nextButton.onClick.AddListener(Next);
        closeButton = Choice("Close", -390, -103, 300, CloseDialogue);
        TypeLine();
    }
    void TypeLine()
    {
        speech.text = lines[lineIndex]; speech.maxVisibleCharacters = 0;
        speech.ForceMeshUpdate();
        typing = StartCoroutine(TypeText(speech.textInfo.characterCount));
    }
    IEnumerator TypeText(int length)
    {
        isTyping = true;
        for (int i = 0; i <= length; i++)
        {
            speech.maxVisibleCharacters = i;
            if (typingSpeed > 0) yield return new WaitForSecondsRealtime(typingSpeed);
        }
        isTyping = false; typing = null;
    }
    public void ShowAll()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null; isTyping = false;
        if (speech != null) speech.maxVisibleCharacters = int.MaxValue;
    }
    void Next()
    {
        if (isTyping) { ShowAll(); return; }
        if (++lineIndex < lines.Length) TypeLine(); else afterLines?.Invoke();
    }
    const float ViewW = 1100f, ViewH = 430f, MaxZoom = 2.5f;
    class HiddenItem { public Vector2 pos; public float sortY, h, rot; public bool flip; public int kind, variant, mushroom = -1; }
    // kind: 0 = oggetto di scena, 2 = oggetto che copre in parte un fungo, 3 = fungo da trovare (tutti i funghi visibili sono cliccabili)

    public void BeginMushroomGame()
    {
        // replaying: permette di rigiocare anche dopo aver finito la missione, senza cambiare lo stato della quest.
        if ((CurrentStage != Stage.FindMushrooms && !replaying) || player == null) return;
        ClearPage(true);
        Label("FIND THE MUSHROOMS", -540, 295, 850, 45, 30);
        counter = Label("", -540, 247, 1000, 35);
        Image field = MushroomUI.Image("MountainPicture", page.transform, new Color(.22f, .29f, .15f));
        MushroomUI.Place(field.rectTransform, 0, -5, ViewW, ViewH);
        field.gameObject.AddComponent<RectMask2D>();
        view = field.rectTransform;

        // La scena è più grande del riquadro: si trascina per spostarla e si usa la rotellina per lo zoom.
        float worldW = ViewW * sceneScale, worldH = ViewH * sceneScale;
        Image worldImage = MushroomUI.Image("World", field.transform, new Color(1, 1, 1, 0));
        worldImage.raycastTarget = false;
        world = worldImage.rectTransform;
        MushroomUI.Place(world, 0, 0, worldW, worldH);
        zoom = 1f / sceneScale;                       // si parte con la panoramica di tutta la scena
        world.localScale = new Vector3(zoom, zoom, 1);
        if (forestBackground != null)
        {
            // Effetto "cover": l'immagine riempie la scena senza deformarsi, il resto viene tagliato.
            Image picture = MushroomUI.Image("Picture", world, Color.white);
            picture.sprite = forestBackground; picture.raycastTarget = false;
            Vector2 s = forestBackground.rect.size;
            float k = Mathf.Max(worldW / s.x, worldH / s.y);
            MushroomUI.Place(picture.rectTransform, 0, 0, s.x * k, s.y * k);
        }
        // Un clic a vuoto (o su un fungo esca) conta come errore.
        Button wrong = field.gameObject.AddComponent<Button>();
        wrong.targetGraphic = field; wrong.transition = Selectable.Transition.None;
        Navigation none = wrong.navigation; none.mode = Navigation.Mode.None; wrong.navigation = none;
        wrong.onClick.AddListener(() => { if (!dragMoved && found < target) { misses++; RefreshCounter(); } });

        if (target == 0) NewRound();

        // Tutti gli oggetti vengono generati con un seed: se esci e rientri la scena è identica,
        // a ogni nuova partita il seed cambia e cambia tutto.
        float hx = worldW * .5f - 60f, hy = worldH * .5f - 60f;
        List<HiddenItem> items = new List<HiddenItem>();
        System.Random deco = new System.Random(roundSeed);
        for (int i = 0; i < decorations; i++)
        {
            HiddenItem it = new HiddenItem();
            it.pos = new Vector2((float)((deco.NextDouble() * 2 - 1) * (hx + 40)), (float)((deco.NextDouble() * 2 - 1) * (hy + 40)));
            it.variant = deco.Next(0, 1000); it.h = deco.Next(45, 110); it.rot = deco.Next(-20, 21); it.flip = deco.Next(0, 2) == 0;
            it.sortY = it.pos.y;
            if (NearMushroom(it.pos, 32f)) continue;       // niente oggetti a caso sopra un fungo
            items.Add(it);
        }
        for (int i = 0; i < target; i++)
        {
            if (!collected.Contains(i))
                items.Add(new HiddenItem { kind = 3, mushroom = i, pos = slots[i], sortY = slots[i].y });
            if (coverMushrooms)
            {
                // Una foglia, un sasso (o uno dei tuoi sprite) copre circa metà del fungo.
                System.Random r = new System.Random(roundSeed + 500 + i);
                float side = r.Next(0, 2) == 0 ? -1f : 1f;
                HiddenItem c = new HiddenItem { kind = 2, variant = r.Next(0, 1000), h = r.Next(60, 90), rot = r.Next(-30, 31), flip = side < 0 };
                c.pos = slots[i] + new Vector2(side * r.Next(20, 32), r.Next(-14, 10));
                c.sortY = slots[i].y - 2f;                   // sempre disegnato sopra al suo fungo
                items.Add(c);
            }
        }
        // Come in un disegno prospettico: gli oggetti più in basso stanno davanti a quelli più in alto.
        items.Sort((a, b) => b.sortY.CompareTo(a.sortY));
        foreach (HiddenItem it in items)
        {
            if (it.kind == 3) SpawnMushroom(world, it);
            else SpawnClutter(world, it);
        }

        hint = Label("Drag to move, scroll to zoom. Click each hidden mushroom once. No time limit.", -540, -260, 1080, 45, 21);
        RefreshCounter();
        Choice("Back to the forest", -250, -310, 460, CloseDialogue);
        Choice("New random search", 250, -310, 460, NewSearch);
    }
    // Genera una nuova partita: numero e posizioni dei funghi cambiano ogni volta.
    void NewRound()
    {
        found = 0; misses = 0; collected.Clear(); slots.Clear();
        roundSeed = UnityEngine.Random.Range(1, int.MaxValue);
        target = UnityEngine.Random.Range(minimumMushrooms, maximumMushrooms + 1);
        float hx = ViewW * sceneScale * .5f - 60f, hy = ViewH * sceneScale * .5f - 60f;
        float minDistance = 110f + 50f * sceneScale;
        int attempts = 0;
        while (slots.Count < target)
        {
            Vector2 p = new Vector2(UnityEngine.Random.Range(-hx, hx), UnityEngine.Random.Range(-hy, hy));
            bool free = true;
            foreach (Vector2 other in slots) if (Vector2.Distance(p, other) < minDistance) { free = false; break; }
            // Dopo troppi tentativi accetta comunque il punto, per non bloccarsi con tanti funghi.
            if (free || ++attempts > 200) slots.Add(p);
        }
    }
    void NewSearch()
    {
        replaying = CurrentStage != Stage.FindMushrooms;
        NewRound();
        BeginMushroomGame();
    }
    void FinishSearch()
    {
        if (CurrentStage == Stage.FindMushrooms)
        {
            CurrentStage = Stage.ReportMushrooms;
            hint.text = "All found! Return to Peter to receive your next task, or play again with new positions.";
        }
        else hint.text = "All found again! Press \"New random search\" to play another round.";
    }
    bool NearMushroom(Vector2 point, float radius)
    {
        foreach (Vector2 s in slots) if (Vector2.Distance(point, s) < radius) return true;
        return false;
    }
    void SpawnMushroom(Transform parent, HiddenItem it)
    {
        int index = it.mushroom;
        System.Random r = new System.Random(roundSeed + index);
        float size = mushroomSize * (0.85f + 0.3f * (float)r.NextDouble());
        Image hit = MushroomUI.Image("Mushroom_" + index, parent, new Color(1, 1, 1, 0));
        MushroomUI.Place(hit.rectTransform, it.pos.x, it.pos.y, size, size);
        hit.rectTransform.localRotation = Quaternion.Euler(0, 0, r.Next(-15, 16));
        Button b = hit.gameObject.AddComponent<Button>(); b.targetGraphic = hit;
        b.transition = Selectable.Transition.None;
        MushroomVisual(hit.transform, r.Next(0, 4));
        b.onClick.AddListener(() =>
        {
            if (dragMoved || !collected.Add(index)) return;   // un trascinamento non è un clic
            found++; hit.gameObject.SetActive(false); RefreshCounter();
            if (found == target) FinishSearch();
        });
    }
    void MushroomVisual(Transform parent, int variant)
    {
        // Fungo in stile cartoon con contorno, come lo sfondo; oppure il tuo sprite, se assegnato.
        Image img = MushroomUI.Image("Mushroom", parent, Color.Lerp(Color.white, new Color(.72f, .66f, .58f), camouflage * .6f));
        img.sprite = mushroomSprite != null ? mushroomSprite : ForestToonArt.Mushroom(variant);
        img.preserveAspect = true; img.raycastTarget = false;
        MushroomUI.Fill(img.rectTransform);
    }
    void SpawnClutter(Transform parent, HiddenItem it)
    {
        // Oggetti non cliccabili: il clic passa attraverso e arriva al fungo che sta sotto.
        if (clutterSprites != null && clutterSprites.Length > 0)
        {
            Sprite sp = clutterSprites[it.variant % clutterSprites.Length];
            if (sp != null)
            {
                Image img = MushroomUI.Image("Clutter", parent, Color.white);
                img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
                MushroomUI.Place(img.rectTransform, it.pos.x, it.pos.y, it.h * sp.rect.width / sp.rect.height, it.h);
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, it.rot * .4f);
                img.rectTransform.localScale = new Vector3(it.flip ? -1f : 1f, 1f, 1f);
                return;
            }
        }
        bool rock = it.variant % 5 == 0;
        Image g = MushroomUI.Image("ForestObject", parent, Color.white);
        g.sprite = rock ? ForestToonArt.Rock() : ForestToonArt.Leaf(it.variant);
        g.preserveAspect = true; g.raycastTarget = false;
        float size = it.h * (rock ? 1.0f : 0.85f);
        MushroomUI.Place(g.rectTransform, it.pos.x, it.pos.y, size, size);
        g.rectTransform.localRotation = Quaternion.Euler(0, 0, rock ? it.rot : (it.variant * 7) % 360);
    }
    // Trascinamento col tasto sinistro per spostarsi, rotellina per lo zoom (centrato sul cursore).
    void UpdateMushroomView()
    {
        if (world == null || view == null || !IsOpen || !view.gameObject.activeInHierarchy) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Canvas c = view.GetComponentInParent<Canvas>();
        Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(view, mouse.position.ReadValue(), cam, out local);
        bool inside = view.rect.Contains(local);
        if (mouse.leftButton.wasPressedThisFrame && inside) { dragging = true; dragMoved = false; dragDist = 0f; dragLast = local; }
        if (dragging)
        {
            if (!mouse.leftButton.isPressed) dragging = false;
            else
            {
                Vector2 delta = local - dragLast; dragLast = local;
                dragDist += delta.magnitude; if (dragDist > 8f) dragMoved = true;
                world.anchoredPosition += delta;
            }
        }
        float scroll = mouse.scroll.ReadValue().y;
        if (inside && Mathf.Abs(scroll) > 0.01f)
        {
            float minZoom = 1f / sceneScale;
            float newZoom = Mathf.Clamp(zoom * Mathf.Exp(Mathf.Clamp(scroll, -120f, 120f) * 0.002f), minZoom, MaxZoom);
            Vector2 under = (local - world.anchoredPosition) / zoom;     // punto della scena sotto il cursore
            zoom = newZoom; world.localScale = new Vector3(zoom, zoom, 1);
            world.anchoredPosition = local - under * zoom;
        }
        // La scena non esce mai dal riquadro.
        Vector2 limit = (world.rect.size * zoom - view.rect.size) * .5f;
        Vector2 pos = world.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -Mathf.Max(0, limit.x), Mathf.Max(0, limit.x));
        pos.y = Mathf.Clamp(pos.y, -Mathf.Max(0, limit.y), Mathf.Max(0, limit.y));
        world.anchoredPosition = pos;
    }
    void RefreshCounter() { counter.text = "Mushrooms remaining: " + (target - found) + "     Wrong clicks: " + misses; }
    ForestBucketArt BucketIllustration(string name, Transform parent, ForestBucketArt.Picture picture, float x, float y, float w, float h)
    {
        ForestBucketArt art = MushroomUI.Rect(name, parent).gameObject.AddComponent<ForestBucketArt>();
        art.picture = picture; art.raycastTarget = false;
        MushroomUI.Place(art.rectTransform, x, y, w, h); art.SetVerticesDirty();
        return art;
    }
    void StyleBucketButton(Button button, Color tint)
    {
        button.GetComponent<Image>().color = new Color(1, 1, 1, 0);
        ForestBucketArt background = BucketIllustration("Button artwork", button.transform, ForestBucketArt.Picture.Panel, 0, 0,
            ((RectTransform)button.transform).sizeDelta.x, ((RectTransform)button.transform).sizeDelta.y);
        background.color = tint; background.transform.SetAsFirstSibling();
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .22f); shadow.effectDistance = new Vector2(0, -3);
        button.targetGraphic = background;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(); text.fontStyle = FontStyles.Bold;
    }
    void OpenBucket()
    {
        if (CurrentStage != Stage.FetchWater) return;
        ClearPage(true);
        bucketOpen = true;
        page.GetComponent<Image>().color = new Color(.055f, .12f, .105f);
        // Illustrazione di sfondo e pannello dati vengono disegnati prima dei testi.
        BucketIllustration("Riverbank", page.transform, ForestBucketArt.Picture.Riverbank, 0, 44, 1100, 330);
        ForestBucketArt card = BucketIllustration("Information card", page.transform, ForestBucketArt.Picture.Panel, 355, 44, 310, 278);
        card.color = new Color(.045f, .105f, .095f, .94f);
        TMP_Text chapter = Label("FOREST TRAIL  /  RIVER WATER", -540, 310, 1080, 22, 15);
        chapter.color = new Color(.71f, .80f, .65f); chapter.characterSpacing = 2;
        TMP_Text title = Label("A LITTLE WATER, A LITTLE CARE", -540, 267, 1080, 48, 32);
        title.fontStyle = FontStyles.Bold;
        Label("Stop the moving pointer in the green zone to fill your bucket.", -540, 224, 1080, 34, 21);

        bucketArt = BucketIllustration("Wooden bucket", page.transform, ForestBucketArt.Picture.Bucket, -100, 43, 330, 330);
        TMP_Text tag = Label("WATER LEVEL", 230, 150, 250, 28, 17);
        tag.color = new Color(.67f, .80f, .73f); tag.characterSpacing = 2;
        counter = Label("0%", 230, 97, 250, 72, 54); counter.fontStyle = FontStyles.Bold;
        TMP_Text range = Label("CARRY ZONE  " + Mathf.RoundToInt(minimumWater * 100) + "-" + Mathf.RoundToInt(maximumWater * 100) + "%", 230, 34, 250, 32, 18);
        range.color = new Color(.73f, .91f, .65f);
        bucketStatus = Label("", 230, -28, 250, 66, 21);

        ForestBucketArt frame = BucketIllustration("Meter frame", page.transform, ForestBucketArt.Picture.Panel, 0, -165, 1000, 64);
        frame.color = new Color(.025f, .075f, .065f);
        Image track = MushroomUI.Image("TimingTrack", page.transform, new Color(.27f, .39f, .40f));
        MushroomUI.Place(track.rectTransform, 0, -165, 960, 28); track.raycastTarget = false;
        Image heavy = MushroomUI.Image("Too much water", track.transform, new Color(.61f, .36f, .23f));
        heavy.rectTransform.anchorMin = new Vector2(maximumWater, 0); heavy.rectTransform.anchorMax = Vector2.one;
        heavy.rectTransform.offsetMin = heavy.rectTransform.offsetMax = Vector2.zero; heavy.raycastTarget = false;
        Image safe = MushroomUI.Image("AcceptableWater", track.transform, new Color(.35f, .66f, .43f));
        safe.rectTransform.anchorMin = new Vector2(minimumWater, 0); safe.rectTransform.anchorMax = new Vector2(maximumWater, 1);
        safe.rectTransform.offsetMin = safe.rectTransform.offsetMax = Vector2.zero; safe.raycastTarget = false;
        for (int i = 0; i <= 10; i++)
        {
            Image tick = MushroomUI.Image("Scale mark", track.transform, new Color(1, 1, 1, .20f));
            tick.raycastTarget = false;
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(i / 10f, 0);
            tick.rectTransform.pivot = new Vector2(.5f, 0); tick.rectTransform.anchoredPosition = Vector2.zero;
            tick.rectTransform.sizeDelta = new Vector2(2, i % 5 == 0 ? 10 : 5);
        }
        Image pointer = MushroomUI.Image("MovingPointer", track.transform, new Color(.94f, .96f, .83f));
        bucketCursor = pointer.rectTransform; bucketCursor.sizeDelta = new Vector2(4, 48); pointer.raycastTarget = false;
        ForestBucketArt drop = BucketIllustration("Pointer droplet", pointer.transform, ForestBucketArt.Picture.Droplet, 0, 36, 18, 25);
        drop.color = new Color(.85f, .94f, .87f);
        Label("NO WATER", -480, -212, 240, 26, 16).color = new Color(.72f, .83f, .82f);
        float safeCenter = -480f + (minimumWater + maximumWater) * .5f * 960f;
        TMP_Text good = Label("GOOD AMOUNT", safeCenter - 120, -212, 240, 26, 16);
        good.alignment = TextAlignmentOptions.Center; good.color = new Color(.75f, .93f, .70f);
        TMP_Text high = Label("TOO MUCH", 240, -212, 240, 26, 16);
        high.alignment = TextAlignmentOptions.MidlineRight; high.color = new Color(.91f, .72f, .56f);
        hint = Label("", -540, -251, 1080, 32, 20); hint.alignment = TextAlignmentOptions.Center;
        bucketAction = Choice("STOP", -260, -302, 480, BucketAction);
        bucketActionCaption = bucketAction.GetComponentInChildren<TMP_Text>();
        StyleBucketButton(bucketAction, new Color(.30f, .52f, .36f));
        Navigation nav = bucketAction.navigation; nav.mode = Navigation.Mode.None; bucketAction.navigation = nav;
        Button back = Choice("Back to the forest", 260, -302, 480, CloseDialogue);
        StyleBucketButton(back, new Color(.14f, .26f, .22f));
        StartBucketAttempt();
    }
    void StartBucketAttempt()
    {
        bucketSucceeded = false;
        bucketElapsed = 0f;
        water = 0f;
        bucketRunning = true;
        bucketActionCaption.text = "STOP";
        hint.text = "Aim for the green zone. You can retry if you miss.";
        hint.color = Color.white;
        UpdateWater();
    }
    void BucketAction()
    {
        if (!bucketOpen || !IsOpen) return;
        if (bucketSucceeded)
        {
            // La missione avanza solo ritirando il secchio dopo il successo.
            CurrentStage = Stage.ReturnWater;
            CloseDialogue();
            return;
        }
        if (!bucketRunning) { StartBucketAttempt(); return; }
        // Usa lo stesso valore mostrato nell'ultimo frame: niente spostamenti dopo Stop.
        bucketRunning = false;
        if (water < minimumWater)
        {
            hint.text = "Too little water! Try again.";
            hint.color = new Color(1f, .76f, .43f);
            bucketActionCaption.text = "Try again";
        }
        else if (water > maximumWater)
        {
            hint.text = "Too much water! The bucket is too heavy. Try again.";
            hint.color = new Color(1f, .76f, .43f);
            bucketActionCaption.text = "Try again";
        }
        else
        {
            bucketSucceeded = true;
            hint.text = "Just right! Take the bucket back to " + speakerName + ".";
            hint.color = new Color(.55f, 1f, .61f);
            bucketActionCaption.text = "Take the bucket";
        }
        UpdateWater();
    }
    void UpdateWater()
    {
        if (bucketArt != null) bucketArt.SetWater(water, bucketRunning, bucketSucceeded);
        if (counter != null) counter.text = Mathf.RoundToInt(water * 100) + "%";
        if (bucketStatus != null)
        {
            bucketStatus.text = bucketRunning ? "Find the right moment.\nPress STOP on green." :
                bucketSucceeded ? "Just right!\nReady to carry." :
                water < minimumWater ? "Not enough water.\nGive it another try." : "Too heavy to carry.\nGive it another try.";
            bucketStatus.color = bucketSucceeded ? new Color(.75f, .93f, .65f) : new Color(.86f, .91f, .83f);
        }
        if (bucketAction != null)
            bucketAction.targetGraphic.color = bucketSucceeded ? new Color(.40f, .62f, .37f) : new Color(.30f, .52f, .36f);
        if (bucketCursor != null)
        {
            bucketCursor.anchorMin = bucketCursor.anchorMax = new Vector2(water, .5f);
            bucketCursor.anchoredPosition = Vector2.zero;
        }
    }

    // Segnale generato soltanto durante Play. Non aggiunge collider o asset alla scena.
    void UpdateRiverMarker()
    {
        bool visible = showRiverMarker && riverPoint != null && player != null
            && CurrentStage == Stage.FetchWater && !IsOpen;
        if (!visible) { if (riverMarker != null) riverMarker.SetActive(false); return; }
        if (riverMarker == null) CreateRiverMarker();
        riverMarker.SetActive(true);
        riverMarker.transform.position = riverPoint.position;
        float distance = Vector3.Distance(player.transform.position, riverPoint.position);
        riverLabel.text = "WATER COLLECTION\n" + Mathf.CeilToInt(distance) + " m"
            + (Near(riverPoint) ? "\n[E] Fill the bucket" : "");
        riverLabel.transform.localPosition = Vector3.up * (riverMarkerHeight + .35f);
        riverLabel.color = riverMarkerColor;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48;
            riverRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * riverMarkerRadius, .10f, Mathf.Sin(angle) * riverMarkerRadius));
        }
        riverBeam.SetPosition(0, Vector3.up * .15f);
        riverBeam.SetPosition(1, Vector3.up * riverMarkerHeight);
        riverRing.startColor = riverRing.endColor = riverMarkerColor;
        riverBeam.startColor = riverBeam.endColor = riverMarkerColor;
    }
    void CreateRiverMarker()
    {
        riverMarker = new GameObject("Quest_WaterMarker_Runtime");
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            riverMarkerMaterial = new Material(shader);
            riverMarkerMaterial.color = riverMarkerColor;
        }
        riverRing = MarkerLine("GroundRing", 48, .08f, true);
        riverBeam = MarkerLine("Beacon", 2, .07f, false);
        GameObject text = new GameObject("WaterPointLabel");
        text.transform.SetParent(riverMarker.transform, false);
        riverLabel = text.AddComponent<TextMeshPro>();
        if (MushroomUI.Font != null) riverLabel.font = MushroomUI.Font;
        riverLabel.text = "WATER COLLECTION";
        riverLabel.fontSize = 3f;
        riverLabel.alignment = TextAlignmentOptions.Center;
        riverLabel.rectTransform.sizeDelta = new Vector2(6, 2);
    }
    LineRenderer MarkerLine(string name, int count, float width, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(riverMarker.transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false; line.positionCount = count; line.loop = loop;
        line.widthMultiplier = width;
        if (riverMarkerMaterial != null) line.sharedMaterial = riverMarkerMaterial;
        line.startColor = line.endColor = riverMarkerColor;
        return line;
    }
    protected virtual void LateUpdate()
    {
        if (riverMarker == null || !riverMarker.activeSelf || riverLabel == null) return;
        Camera camera = Camera.main;
        if (camera != null) riverLabel.transform.rotation = camera.transform.rotation;
    }
    public void CloseDialogue()
    {
        bucketOpen = false; bucketRunning = false;
        if (typing != null) StopCoroutine(typing);
        typing = null; isTyping = false;
        replaying = false;
        if (!IsOpen) return;
        uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
        Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
    }
    void OnGUI()
    {
        if (IsOpen || player == null) return;
        if (CurrentStage == Stage.FetchWater && riverPoint != null)
        {
            float distance = Vector3.Distance(player.transform.position, riverPoint.position);
            GUI.Box(new Rect(20, 20, 420, 58), "OBJECTIVE: Collect river water\nFollow the blue marker - " + Mathf.CeilToInt(distance) + " m");
            // Etichetta sullo schermo per localizzare il punto anche a distanza.
            Camera camera = Camera.main;
            if (camera != null && !Near(riverPoint))
            {
                Vector3 screen = camera.WorldToScreenPoint(riverPoint.position + Vector3.up * riverMarkerHeight);
                if (screen.z > 0)
                {
                    float x = Mathf.Clamp(screen.x, 110, Screen.width - 110);
                    float y = Mathf.Clamp(Screen.height - screen.y, 95, Screen.height - 115);
                    GUI.Box(new Rect(x - 100, y - 20, 200, 40), "WATER - " + Mathf.CeilToInt(distance) + " m");
                }
                else GUI.Box(new Rect(Screen.width / 2 - 160, 90, 320, 35), "Turn around to find the river marker");
            }
        }
        string text = CurrentStage == Stage.FetchWater && Near(riverPoint) ? "E - Fill the bucket" :
            CurrentStage == Stage.WaterGarden && Near(gardenPoint) ? "E - Water the garden" :
            Near(transform) ? "E - Talk to " + speakerName : "";
        if (text != "") GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height - 85, 340, 42), text);
    }
    protected virtual void OnDisable()
    {
        CloseDialogue();
        if (riverMarker != null) riverMarker.SetActive(false);
    }
    protected virtual void OnDestroy()
    {
        if (uiRoot != null) Destroy(uiRoot);
        if (riverMarker != null) Destroy(riverMarker);
        if (riverMarkerMaterial != null) Destroy(riverMarkerMaterial);
    }
}

// Sprite in stile cartoon (colori piatti + contorno scuro) generati da codice: nessun asset da importare.
// Servono a far somigliare funghi, foglie e sassi all'illustrazione di sfondo.
static class ForestToonArt
{
    const int Size = 128, Outline = 4;
    static readonly Color Ink = new Color(.09f, .07f, .05f);
    static readonly Color[] caps = { new Color(.80f, .24f, .14f), new Color(.88f, .52f, .16f), new Color(.62f, .36f, .18f), new Color(.60f, .34f, .55f) };
    static readonly Color[] leaves = { new Color(.93f, .55f, .16f), new Color(.88f, .76f, .20f), new Color(.45f, .62f, .22f), new Color(.62f, .34f, .14f), new Color(.75f, .25f, .12f) };
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Mushroom(int variant)
    {
        int i = Mathf.Abs(variant) % caps.Length;
        bool spots = i == 0 || i == 3;
        return Get("mushroom" + i, () => Build((u, v) => MushroomRegion(u * .9f, v * .9f - .125f, spots),
            new Color(.94f, .86f, .68f), caps[i], new Color(.98f, .94f, .82f), true));
    }
    public static Sprite Leaf(int variant)
    {
        int i = Mathf.Abs(variant) % leaves.Length;
        return Get("leaf" + i, () => Build(LeafRegion, leaves[i], leaves[i], Color.Lerp(leaves[i], Ink, .35f), false));
    }
    public static Sprite Rock()
    {
        return Get("rock", () => Build(RockRegion, new Color(.63f, .65f, .60f), Color.clear, new Color(.78f, .80f, .74f), false));
    }
    static Sprite Get(string key, Func<Sprite> make)
    {
        Sprite s;
        if (!cache.TryGetValue(key, out s) || s == null) { s = make(); cache[key] = s; }
        return s;
    }

    // Regioni: 0 = vuoto, 1 = corpo/gambo, 2 = cappello, 3 = dettaglio (macchie, nervatura, luce).
    static int MushroomRegion(float u, float v, bool spots)
    {
        bool cap = v >= -0.02f && (u * u) / (0.95f * 0.95f) + (Mathf.Max(v, 0f) * Mathf.Max(v, 0f)) / (0.70f * 0.70f) <= 1f;
        if (cap)
        {
            if (spots)
            {
                if (Spot(u, v, -0.45f, 0.28f, 0.12f) || Spot(u, v, 0.12f, 0.45f, 0.10f) ||
                    Spot(u, v, 0.50f, 0.20f, 0.11f) || Spot(u, v, -0.05f, 0.14f, 0.07f)) return 3;
            }
            return 2;
        }
        float half = 0.23f + 0.07f * Mathf.Clamp01(-v);
        if (v < -0.95f || v > 0.05f) return 0;
        if (v < -0.80f)
        {
            float a = u / half, b = (v + 0.80f) / 0.15f;
            return a * a + b * b <= 1f ? 1 : 0;
        }
        return Mathf.Abs(u) <= half ? 1 : 0;
    }
    static bool Spot(float u, float v, float cu, float cv, float r) { return (u - cu) * (u - cu) + (v - cv) * (v - cv) <= r * r; }
    static int LeafRegion(float u, float v)
    {
        float t = 1f - Mathf.Abs(v) / 0.92f;
        if (t <= 0f) return 0;
        float half = 0.50f * Mathf.Pow(t, 0.7f);
        float uu = u - 0.10f * v * v;                        // leggera curvatura
        if (Mathf.Abs(uu) > half) return 0;
        if (Mathf.Abs(uu) < 0.04f && v < 0.55f) return 3;   // nervatura
        return 1;
    }
    static int RockRegion(float u, float v)
    {
        float a = Mathf.Atan2(v * 1.5f, u);
        float rad = 1f + 0.10f * Mathf.Sin(a * 5f + 1f) + 0.06f * Mathf.Sin(a * 3f);
        float d = (u * u) / (0.92f * 0.92f) + (v * v) / (0.62f * 0.62f);
        if (d > rad * rad) return 0;
        if (v > 0.12f && d < 0.55f && u < 0.2f) return 3;
        return 1;
    }

    static Sprite Build(Func<float, float, int> region, Color body, Color cap, Color detail, bool separate)
    {
        int[] map = new int[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                map[y * Size + x] = region((x + .5f) / Size * 2.4f - 1.2f, (y + .5f) / Size * 2.4f - 1.2f);
        Color[] px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int r = map[y * Size + x];
                Color c = r == 1 ? body : r == 2 ? cap : r == 3 ? detail : Color.clear;
                // Contorno esterno (spessore Outline) e linea di separazione tra gambo e cappello.
                if (r == 0 || (separate && r == 1))
                {
                    int radius = r == 0 ? Outline : 2;
                    bool edge = false;
                    for (int dy = -radius; dy <= radius && !edge; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy > radius * radius) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                            int n = map[ny * Size + nx];
                            if (r == 0 ? n != 0 : n == 2) { edge = true; break; }
                        }
                    if (edge) c = Ink;
                }
                px[y * Size + x] = c;
            }
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
    }
}