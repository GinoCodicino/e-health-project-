using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Una sola macchina a stati decide quali azioni sono consentite.
// Lo stato dura per la sessione corrente: non viene salvato su disco.
public class ForestQuest : MonoBehaviour
{
    public enum Stage { Introduction, FindMushrooms, ReportMushrooms, FindRoofPieces, RepairRoof, Complete }
    [Header("Collegamenti")]
    public playercontroller player;
    [Tooltip("Centro dell'area dove si nascondono i pezzi di tetto: davanti alla casa, dal lato opposto del path. Si crea con Tools > Level 02 > Create roof search area and repair point.")]
    public Transform roofSearchArea;
    [Tooltip("Punto davanti alla casa dove si ripara il tetto (si trova da solo se esiste Quest_RedHouse_...).")]
    public Transform housePoint;
    // Campi della vecchia missione dell'acqua: non più usati, restano nascosti solo per non rompere altri script.
    [HideInInspector] public Transform riverPoint;
    [HideInInspector] public Transform gardenPoint;
    [Header("Personalizzazione")]
    public string speakerName = "Peter";
    public float interactionDistance = 3f;
    [Range(0f, 0.1f)] public float typingSpeed = 0.025f;
    [Range(0.2f, 0.95f)] public float dialogueOpacity = 0.72f;
    [Range(3, 8)] public int minimumMushrooms = 3;
    [Range(3, 8)] public int maximumMushrooms = 5;
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
    [Header("Pezzi di tetto")]
    [Tooltip("Quanti pezzi di tetto sono nascosti.")]
    [Range(1, 6)] public int piecesToFind = 3;
    [Tooltip("Raggio dell'area di ricerca intorno a Roof Search Area (in metri).")]
    [Range(3f, 20f)] public float searchRadius = 8f;
    [Tooltip("Quanti cespugli vengono creati nell'area (3 nascondono un pezzo, gli altri sono vuoti).")]
    [Range(5, 20)] public int bushCount = 10;
    [Tooltip("Opzionale: cespugli già presenti nella scena. Se ne assegni almeno 4 usa quelli invece di crearne di nuovi.")]
    public Transform[] existingBushes;
    [Tooltip("A che distanza il cespuglio giusto trema e brilla.")]
    [Range(3f, 15f)] public float hintDistance = 7f;
    [Tooltip("Quante pressioni di Spazio servono per scostare i rami.")]
    [Range(4, 20)] public int pushesNeeded = 10;
    [Header("Segnale della missione")]
    public bool showMarker = true;
    public Color markerColor = new Color(1f, 0.72f, 0.2f);
    [Range(0.3f, 3f)] public float markerRadius = 1.5f;
    [Range(1f, 8f)] public float markerHeight = 4f;
    public Stage CurrentStage { get; private set; }
    // Premi disponibili soltanto dopo funghi E riparazione del tetto.
    public bool HasFishingNet { get { return CurrentStage == Stage.Complete; } }
    public bool HasMedicalKit { get { return CurrentStage == Stage.Complete; } }
    public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }
    public int FoundCount { get { return found; } }

    GameObject uiRoot, page;
    RectTransform canvas;
    TMP_Text speech, counter, hint;
    GameObject marker;
    LineRenderer markerRing, markerBeam;
    TextMeshPro markerLabel;
    Material markerMaterial;
    int piecesFound, activeBush = -1;
    bool bushOpen;
    float bushProgress, bushKick, bushLockUntil;
    RectTransform bushFill;
    string toast;
    float toastUntil;
    Coroutine typing;
    bool isTyping, oldCursorVisible;
    CursorLockMode oldCursorLock;
    int found, target, lineIndex;
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
        if (housePoint == null) housePoint = FindHousePoint();
        if (roofSearchArea == null) roofSearchArea = FallbackSearchArea();
    }

    protected virtual void Update()
    {
        UpdateMarker();
        UpdateBushes();
        UpdateMushroomView();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || player == null) return;
        if (IsOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) { CloseDialogue(); return; }
            if (bushOpen) { UpdateBushGame(keyboard); return; }
            // Spazio completa la riga; non attiva anche un pulsante selezionato.
            if (isTyping && keyboard.spaceKey.wasPressedThisFrame) ShowAll();
            return;
        }
        Mouse mouse = Mouse.current;
        bool eKey = keyboard.eKey.wasPressedThisFrame;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        // E o clic vicino a un cespuglio: scosta i rami.
        if (CurrentStage == Stage.FindRoofPieces && (eKey || click) && Time.unscaledTime >= bushLockUntil)
        {
            int bush = NearestBush();
            if (bush >= 0) { OpenBush(bush); return; }
        }
        if (!eKey) return;
        if (CurrentStage == Stage.RepairRoof && Near(housePoint))
        {
            CurrentStage = Stage.Complete;
            Speak(new[] { "Thank you! With these planks the roof will keep the rain out again.",
                "Your grandfather would be proud of you. Take this fishing net and this first-aid kit as a thank-you.",
                "The net may be useful by the river. Keep the kit with you in case an animal needs help." }, CloseDialogue);
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
        bushOpen = false;
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
                    "Now I have another problem. The long rains and the strong wind tore a section of my roof apart.",
                    "Three wooden planks flew away and landed among the bushes in front of the house, on the other side of the path.",
                    "Follow the marker to the area. When you get close, the right bush trembles and sparkles. Press E or click to push the branches aside.",
                    "Bring all three planks back to the house so I can repair the roof." },
                    () => { CurrentStage = Stage.FindRoofPieces; SetupBushes(); CloseDialogue(); }); break;
            case Stage.FindRoofPieces:
                Speak(new[] { "Roof pieces found: " + piecesFound + " of " + Mathf.Clamp(piecesToFind,1,6) + ".",
                    "Look for bushes that tremble and sparkle, in front of the house on the other side of the path." }, CloseDialogue); break;
            case Stage.RepairRoof:
                Speak(new[] { "You have all the pieces! Please bring them to the front of the house.",
                    "Press E there to repair the roof." }, CloseDialogue); break;
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
    // ---------- Pezzi di tetto nascosti tra i cespugli ----------
    class Bush { public Transform t; public bool hasPiece, done, generated; public Vector3 basePos; public Quaternion baseRot; public GameObject sparkle; }
    List<Bush> bushes = new List<Bush>();
    Material[] bushMaterials;
    static Mesh blobMesh;
    readonly List<Material> runtimeMaterials = new List<Material>();

    Material Mat(Color color, bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find(unlit ? "Sprites/Default" : "Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material m = new Material(shader); m.color = color;
        runtimeMaterials.Add(m);
        return m;
    }
    // Icosaedro a facce piatte: l'aspetto "low poly" dei cespugli e delle scintille.
    static Mesh LowPolyBlob()
    {
        if (blobMesh != null) return blobMesh;
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v = { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
            new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
            new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        Vector3[] verts = new Vector3[60]; int[] tris = new int[60];
        for (int i = 0; i < 60; i += 3)
        {
            Vector3 a = v[f[i]].normalized * .5f, b = v[f[i + 1]].normalized * .5f, c = v[f[i + 2]].normalized * .5f;
            // Unity disegna il lato "orario": se la faccia guarda verso l'interno, inverte l'ordine.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
            verts[i] = a; verts[i + 1] = b; verts[i + 2] = c;
            tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
        }
        Mesh m = new Mesh(); m.name = "LowPolyBlob";
        m.vertices = verts; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
        blobMesh = m;
        return m;
    }
    Transform BuildBush(Vector3 position)
    {
        if (bushMaterials == null)
            bushMaterials = new[] { Mat(new Color(.20f, .45f, .16f), false), Mat(new Color(.27f, .55f, .20f), false), Mat(new Color(.15f, .36f, .14f), false) };
        GameObject root = new GameObject("Quest_RoofBush_Runtime");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
        int blobs = UnityEngine.Random.Range(3, 6);
        for (int i = 0; i < blobs; i++)
        {
            GameObject g = new GameObject("Leaves");
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = new Vector3(UnityEngine.Random.Range(-.55f, .55f), UnityEngine.Random.Range(.35f, .7f), UnityEngine.Random.Range(-.55f, .55f));
            float s = UnityEngine.Random.Range(1.1f, 1.7f);
            g.transform.localScale = new Vector3(s, s * .8f, s);
            g.transform.localRotation = UnityEngine.Random.rotation;
            g.AddComponent<MeshFilter>().sharedMesh = LowPolyBlob();
            g.AddComponent<MeshRenderer>().sharedMaterial = bushMaterials[UnityEngine.Random.Range(0, bushMaterials.Length)];
        }
        return root.transform;
    }
    // Piccole scintille low poly che compaiono sopra il cespuglio giusto quando ti avvicini.
    GameObject BuildSparkle(Transform parent)
    {
        GameObject root = new GameObject("Sparkle");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0, 2.1f, 0);
        Material m = Mat(new Color(1f, .88f, .30f), true);
        for (int i = 0; i < 3; i++)
        {
            GameObject g = new GameObject("Spark");
            g.transform.SetParent(root.transform, false);
            float a = i * Mathf.PI * 2f / 3f;
            g.transform.localPosition = new Vector3(Mathf.Cos(a) * .45f, i * .18f, Mathf.Sin(a) * .45f);
            g.transform.localScale = Vector3.one * .2f;
            g.AddComponent<MeshFilter>().sharedMesh = LowPolyBlob();
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        root.SetActive(false);
        return root;
    }
    Vector3 GroundAt(Vector3 p, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 60f, Vector3.down, 150f, ~0, QueryTriggerInteraction.Ignore);
        float y = fallbackY; bool terrainFound = false;
        foreach (RaycastHit h in hits) if (h.collider is TerrainCollider) { y = h.point.y; terrainFound = true; break; }
        if (!terrainFound && hits.Length > 0)
        {
            y = float.MaxValue;
            foreach (RaycastHit h in hits) y = Mathf.Min(y, h.point.y);
        }
        return new Vector3(p.x, y, p.z);
    }
    Vector3 PickBushPosition(List<Vector3> placed)
    {
        Vector3 c = roofSearchArea.position, best = c;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * searchRadius;
            Vector3 p = GroundAt(new Vector3(c.x + r.x, c.y, c.z + r.y), c.y);
            best = p;
            bool free = true;
            foreach (Vector3 q in placed) if ((q - p).sqrMagnitude < 2.5f * 2.5f) { free = false; break; }
            // Evita di finire dentro un albero o un sasso.
            if (free && Physics.CheckSphere(p + Vector3.up * 1.4f, .7f, ~0, QueryTriggerInteraction.Ignore)) free = false;
            if (free) return p;
        }
        return best;
    }
    Transform FallbackSearchArea()
    {
        Debug.LogWarning("Roof Search Area non assegnata: uso una posizione provvisoria. Usa Tools > Level 02 > Create roof search area and repair point e spostala davanti alla casa, dal lato opposto del path.", this);
        GameObject go = new GameObject("Quest_RoofSearchArea_TEMP");
        go.transform.position = transform.position + transform.right * 12f;
        return go.transform;
    }
    Transform FindHousePoint()
    {
        GameObject house = GameObject.Find("Quest_RedHouse_" + gameObject.name);
        if (house == null) { Debug.LogWarning("Casa rossa non trovata: assegna House Point sull'NPC.", this); return null; }
        Transform existing = house.transform.Find("Quest_RoofRepair_POINT");
        if (existing != null) return existing;
        GameObject p = new GameObject("Quest_RoofRepair_POINT");
        p.transform.SetParent(house.transform, false);
        p.transform.localPosition = new Vector3(0, 0, 4.2f);      // davanti alla porta
        return p.transform;
    }
    // Crea i cespugli (o usa i tuoi) e sceglie a caso quali nascondono un pezzo: cambia a ogni partita.
    void SetupBushes()
    {
        ClearBushes();
        piecesFound = 0;
        if (roofSearchArea == null) roofSearchArea = FallbackSearchArea();
        int pieces = Mathf.Clamp(piecesToFind, 1, 6);
        List<Bush> list = new List<Bush>();
        int usable = 0;
        if (existingBushes != null) foreach (Transform t in existingBushes) if (t != null) usable++;
        if (usable >= pieces + 1)
        {
            foreach (Transform t in existingBushes) if (t != null) list.Add(new Bush { t = t });
        }
        else
        {
            List<Vector3> placed = new List<Vector3>();
            int total = Mathf.Max(bushCount, pieces + 3);
            for (int i = 0; i < total; i++)
            {
                Vector3 p = PickBushPosition(placed);
                placed.Add(p);
                list.Add(new Bush { t = BuildBush(p), generated = true });
            }
        }
        List<int> order = new List<int>();
        for (int i = 0; i < list.Count; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }
        for (int k = 0; k < pieces; k++) list[order[k]].hasPiece = true;
        foreach (Bush b in list)
        {
            b.basePos = b.t.position; b.baseRot = b.t.rotation;
            if (b.hasPiece) b.sparkle = BuildSparkle(b.t);
        }
        bushes = list;
    }
    void ClearBushes()
    {
        foreach (Bush b in bushes)
        {
            if (b.sparkle != null) Destroy(b.sparkle);
            if (b.t == null) continue;
            if (b.generated) Destroy(b.t.gameObject);
            else { b.t.position = b.basePos; b.t.rotation = b.baseRot; }
        }
        bushes.Clear();
    }
    int NearestBush()
    {
        if (player == null) return -1;
        int best = -1; float bestDistance = interactionDistance;
        for (int i = 0; i < bushes.Count; i++)
        {
            if (bushes[i].done || bushes[i].t == null) continue;
            float d = Vector3.Distance(player.transform.position, bushes[i].basePos);
            if (d <= bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }
    // Il cespuglio giusto trema e brilla quando sei vicino; gli altri restano fermi.
    void UpdateBushes()
    {
        bushKick = Mathf.MoveTowards(bushKick, 0f, Time.unscaledDeltaTime * 4f);
        if (CurrentStage != Stage.FindRoofPieces || player == null) return;
        if (bushes.Count == 0) SetupBushes();
        for (int i = 0; i < bushes.Count; i++)
        {
            Bush b = bushes[i];
            if (b.t == null) continue;
            float d = Vector3.Distance(player.transform.position, b.basePos);
            bool hint = b.hasPiece && !b.done && d <= hintDistance;
            float shake = 0f;
            if (bushOpen && i == activeBush) shake = bushKick * .12f;
            else if (hint) shake = (0.03f + 0.06f * (1f - d / hintDistance)) * Mathf.Clamp01(Mathf.Sin(Time.time * 2.4f + i * 1.7f) * 4f);
            Vector3 o = new Vector3(Mathf.Sin(Time.time * 45f + i), 0f, Mathf.Cos(Time.time * 41f + i)) * shake;
            b.t.position = b.basePos + o;
            b.t.rotation = b.baseRot * Quaternion.Euler(o.z * 30f, 0f, -o.x * 30f);
            if (b.sparkle != null)
            {
                if (b.sparkle.activeSelf != hint) b.sparkle.SetActive(hint);
                if (hint)
                {
                    b.sparkle.transform.Rotate(0f, 200f * Time.deltaTime, 0f);
                    Vector3 lp = b.sparkle.transform.localPosition; lp.y = 2.1f + Mathf.Sin(Time.time * 3f + i) * .15f;
                    b.sparkle.transform.localPosition = lp;
                }
            }
        }
    }

    // ---------- Mini-interazione: scostare i rami ----------
    float BushRequired() { return activeBush >= 0 && activeBush < bushes.Count && bushes[activeBush].hasPiece ? 1f : 0.35f; }
    void OpenBush(int index)
    {
        if (CurrentStage != Stage.FindRoofPieces || index < 0 || index >= bushes.Count || bushes[index].done || player == null) return;
        ClearPage(false);
        activeBush = index; bushProgress = 0f; bushKick = 0f;
        bushOpen = true;
        Label("SEARCHING THE BUSH", -550, 116, 1000, 30, 22);
        Label("Push the branches aside! Press SPACE (or click the button) again and again.", -550, 64, 1100, 44, 22);
        Image track = MushroomUI.Image("PushTrack", page.transform, new Color(.12f, .20f, .14f));
        MushroomUI.Place(track.rectTransform, 0, 6, 900, 26); track.raycastTarget = false;
        Image fill = MushroomUI.Image("PushFill", track.transform, new Color(.45f, .72f, .38f));
        bushFill = fill.rectTransform; fill.raycastTarget = false;
        bushFill.anchorMin = new Vector2(0, 0); bushFill.anchorMax = new Vector2(0, 1);
        bushFill.offsetMin = bushFill.offsetMax = Vector2.zero;
        Button push = Choice("Push the branches (Space)", -285, -103, 530, Push);
        Navigation nav = push.navigation; nav.mode = Navigation.Mode.None; push.navigation = nav;
        Choice("Step back", 285, -103, 530, CloseDialogue);
        UpdateBushFill();
    }
    void Push()
    {
        if (!bushOpen || !IsOpen) return;
        bushProgress = Mathf.Min(1f, bushProgress + 1f / Mathf.Max(3, pushesNeeded));
        bushKick = 1f;
    }
    void UpdateBushFill()
    {
        if (bushFill != null) bushFill.anchorMax = new Vector2(Mathf.Clamp01(bushProgress / BushRequired()), 1f);
    }
    void UpdateBushGame(Keyboard keyboard)
    {
        if (activeBush < 0 || activeBush >= bushes.Count) { CloseDialogue(); return; }
        // Nessun pulsante resta selezionato, così Spazio non lo preme una seconda volta.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (keyboard.spaceKey.wasPressedThisFrame) Push();
        // Prima controlla se è completa, poi applica il calo: altrimenti la barra arriva a ~99,7% e non finisce mai.
        if (bushProgress >= BushRequired() - 0.02f) { FinishBush(); return; }
        bushProgress = Mathf.Max(0f, bushProgress - 0.20f * Time.unscaledDeltaTime);
        UpdateBushFill();
    }
    void FinishBush()
    {
        Bush b = bushes[activeBush];
        b.done = true;
        bushLockUntil = Time.unscaledTime + 0.6f;
        if (b.sparkle != null) b.sparkle.SetActive(false);
        b.t.position = b.basePos; b.t.rotation = b.baseRot;
        CloseDialogue();
        if (b.hasPiece)
        {
            piecesFound++;
            StartCoroutine(PlankPop(b.basePos));
            if (piecesFound >= Mathf.Clamp(piecesToFind, 1, 6))
            {
                CurrentStage = Stage.RepairRoof;
                Toast("Roof plank found! All pieces collected - bring them back to the house.", 4f);
            }
            else Toast("Roof plank found! " + piecesFound + "/" + Mathf.Clamp(piecesToFind, 1, 6), 3f);
        }
        else Toast("Nothing here but leaves. Try another bush.", 2.5f);
    }
    IEnumerator PlankPop(Vector3 start)
    {
        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = "Quest_RoofPlank_Runtime";
        Collider col = plank.GetComponent<Collider>(); if (col != null) Destroy(col);
        plank.transform.localScale = new Vector3(.95f, .07f, .28f);
        plank.GetComponent<Renderer>().sharedMaterial = Mat(new Color(.55f, .34f, .16f), false);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.9f;
            Vector3 end = player != null ? player.transform.position + Vector3.up * 1.2f : start + Vector3.up * 2f;
            plank.transform.position = Vector3.Lerp(start + Vector3.up * .9f, end, t * t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * .9f;
            plank.transform.rotation = Quaternion.Euler(t * 360f, t * 540f, 0f);
            yield return null;
        }
        Destroy(plank);
    }
    void Toast(string text, float seconds) { toast = text; toastUntil = Time.unscaledTime + seconds; }

    // ---------- Segnale in scena (anello, raggio di luce, distanza) ----------
    // Generato soltanto durante Play. Non aggiunge collider o asset alla scena.
    Transform MarkerTarget()
    {
        if (CurrentStage == Stage.FindRoofPieces) return roofSearchArea;
        if (CurrentStage == Stage.RepairRoof) return housePoint;
        return null;
    }
    bool InMarkerRange(Transform target)
    {
        if (target == null || player == null) return false;
        if (CurrentStage == Stage.FindRoofPieces) return Vector3.Distance(player.transform.position, target.position) <= searchRadius;
        return Near(target);
    }
    void UpdateMarker()
    {
        Transform target = MarkerTarget();
        bool visible = showMarker && target != null && player != null && !IsOpen;
        if (!visible) { if (marker != null) marker.SetActive(false); return; }
        if (marker == null) CreateMarker();
        marker.SetActive(true);
        marker.transform.position = target.position;
        float distance = Vector3.Distance(player.transform.position, target.position);
        float radius = CurrentStage == Stage.FindRoofPieces ? Mathf.Max(searchRadius, markerRadius) : markerRadius;
        if (CurrentStage == Stage.FindRoofPieces)
            markerLabel.text = "ROOF PIECES " + piecesFound + "/" + Mathf.Clamp(piecesToFind, 1, 6) + "\n"
                + (InMarkerRange(target) ? "Search the bushes" : Mathf.CeilToInt(distance) + " m");
        else
            markerLabel.text = "REPAIR THE ROOF\n" + Mathf.CeilToInt(distance) + " m" + (Near(target) ? "\n[E] Repair" : "");
        markerLabel.transform.localPosition = Vector3.up * (markerHeight + .35f);
        markerLabel.color = markerColor;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48;
            markerRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .10f, Mathf.Sin(angle) * radius));
        }
        markerBeam.SetPosition(0, Vector3.up * .15f);
        markerBeam.SetPosition(1, Vector3.up * markerHeight);
        markerRing.startColor = markerRing.endColor = markerColor;
        markerBeam.startColor = markerBeam.endColor = markerColor;
    }
    void CreateMarker()
    {
        marker = new GameObject("Quest_Marker_Runtime");
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            markerMaterial = new Material(shader);
            markerMaterial.color = markerColor;
        }
        markerRing = MarkerLine("GroundRing", 48, .08f, true);
        markerBeam = MarkerLine("Beacon", 2, .07f, false);
        GameObject text = new GameObject("MarkerLabel");
        text.transform.SetParent(marker.transform, false);
        markerLabel = text.AddComponent<TextMeshPro>();
        if (MushroomUI.Font != null) markerLabel.font = MushroomUI.Font;
        markerLabel.text = "";
        markerLabel.fontSize = 3f;
        markerLabel.alignment = TextAlignmentOptions.Center;
        markerLabel.rectTransform.sizeDelta = new Vector2(8, 3);
    }
    LineRenderer MarkerLine(string name, int count, float width, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(marker.transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false; line.positionCount = count; line.loop = loop;
        line.widthMultiplier = width;
        if (markerMaterial != null) line.sharedMaterial = markerMaterial;
        line.startColor = line.endColor = markerColor;
        return line;
    }
    protected virtual void LateUpdate()
    {
        if (marker == null || !marker.activeSelf || markerLabel == null) return;
        Camera camera = Camera.main;
        if (camera != null) markerLabel.transform.rotation = camera.transform.rotation;
    }
    public void CloseDialogue()
    {
        bushOpen = false;
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
        if (player == null) return;
        if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            GUI.Box(new Rect(Screen.width / 2 - 230, Screen.height / 2 - 150, 460, 44), toast);
        if (IsOpen) return;
        Transform target = MarkerTarget();
        if (target != null)
        {
            float distance = Vector3.Distance(player.transform.position, target.position);
            string objective = CurrentStage == Stage.FindRoofPieces
                ? "OBJECTIVE: Find the roof pieces (" + piecesFound + "/" + Mathf.Clamp(piecesToFind, 1, 6) + ")\nFollow the marker - " + Mathf.CeilToInt(distance) + " m"
                : "OBJECTIVE: Repair the roof\nReturn to the house - " + Mathf.CeilToInt(distance) + " m";
            GUI.Box(new Rect(20, 20, 420, 58), objective);
            // Etichetta sullo schermo per localizzare il punto anche a distanza.
            Camera camera = Camera.main;
            if (camera != null && !InMarkerRange(target))
            {
                Vector3 screen = camera.WorldToScreenPoint(target.position + Vector3.up * markerHeight);
                if (screen.z > 0)
                {
                    float x = Mathf.Clamp(screen.x, 110, Screen.width - 110);
                    float y = Mathf.Clamp(Screen.height - screen.y, 95, Screen.height - 115);
                    GUI.Box(new Rect(x - 100, y - 20, 200, 40), (CurrentStage == Stage.FindRoofPieces ? "ROOF PIECES - " : "HOUSE - ") + Mathf.CeilToInt(distance) + " m");
                }
                else GUI.Box(new Rect(Screen.width / 2 - 160, 90, 320, 35), "Turn around to find the marker");
            }
        }
        string text = CurrentStage == Stage.FindRoofPieces && NearestBush() >= 0 ? "E / Click - Search the bush" :
            CurrentStage == Stage.RepairRoof && Near(housePoint) ? "E - Repair the roof" :
            Near(transform) ? "E - Talk to " + speakerName : "";
        if (text != "") GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height - 85, 340, 42), text);
    }
    // Mostra nell'editor l'area di ricerca quando selezioni l'NPC.
    void OnDrawGizmosSelected()
    {
        if (roofSearchArea == null) return;
        Gizmos.color = new Color(1f, .75f, .2f);
        Vector3 c = roofSearchArea.position;
        for (int i = 0; i < 48; i++)
        {
            float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), .1f, Mathf.Sin(a0)) * searchRadius, c + new Vector3(Mathf.Cos(a1), .1f, Mathf.Sin(a1)) * searchRadius);
        }
    }
    protected virtual void OnDisable()
    {
        CloseDialogue();
        if (marker != null) marker.SetActive(false);
    }
    protected virtual void OnDestroy()
    {
        ClearBushes();
        if (uiRoot != null) Destroy(uiRoot);
        if (marker != null) Destroy(marker);
        if (markerMaterial != null) Destroy(markerMaterial);
        foreach (Material m in runtimeMaterials) if (m != null) Destroy(m);
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
