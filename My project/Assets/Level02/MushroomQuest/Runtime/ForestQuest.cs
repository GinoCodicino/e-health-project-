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
    [Range(3,5)] public int minimumMushrooms = 3;
    [Range(3,5)] public int maximumMushrooms = 5;
    [Range(0f,1f)] public float minimumWater = 0.50f;
    [Range(0f,1f)] public float maximumWater = 0.75f;
    public Sprite forestBackground;
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

    protected virtual void Awake()
    {
        // Disattiva la vecchia raccolta e il quiz; non elimina asset condivisi.
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component != null && (component.GetType().Name == "FindMushroomsGame" || component.GetType().Name == "MushroomLearningQuest"))
                component.enabled = false;
        if (player == null) player = FindFirstObjectByType<playercontroller>();
        minimumMushrooms = Mathf.Clamp(minimumMushrooms,3,5);
        maximumMushrooms = Mathf.Clamp(maximumMushrooms,minimumMushrooms,5);
        minimumWater = Mathf.Clamp(minimumWater,0.05f,0.95f);
        maximumWater = Mathf.Clamp(maximumWater,minimumWater,1f);
    }

    protected virtual void Update()
    {
        UpdateRiverMarker();
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
            Speak(new [] { "Thank you! This water helps our little garden.",
                "Caring for the whole forest takes much more. Your grandfather would be proud of you." }, CloseDialogue);
        }
        else if (Near(transform)) OpenDialogue(player);
    }

    bool Near(Transform point) { return point != null && Vector3.Distance(player.transform.position,point.position) <= interactionDistance; }
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
            player.SetUiMode(this,true);
            uiRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
    void ClearPage(bool fullScreen)
    {
        OpenUI();
        bucketOpen = false; bucketRunning = false;
        if (typing != null) StopCoroutine(typing);
        typing = null; isTyping = false;
        if (page != null) { page.SetActive(false); Destroy(page); }
        Image panel = MushroomUI.Image("Page", canvas, new Color(0.025f,0.065f,0.04f,fullScreen ? 1f : dialogueOpacity));
        page = panel.gameObject;
        if (fullScreen) MushroomUI.Place(panel.rectTransform,0,0,1200,680);
        else
        {
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f,0);
            panel.rectTransform.pivot = new Vector2(.5f,0);
            panel.rectTransform.anchoredPosition = new Vector2(0,20);
            panel.rectTransform.sizeDelta = new Vector2(1200,300);
        }
    }
    TMP_Text Label(string text,float x,float y,float w,float h,float size=24)
    {
        TMP_Text t = MushroomUI.Text("Text",page.transform,text,size,Color.white);
        // x indica il bordo SINISTRO del testo, rispetto al centro del pannello.
        // Con pivot centrale il testo iniziava mezza larghezza troppo a sinistra.
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        MushroomUI.Place(t.rectTransform,x,y,w,h);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Truncate;
        return t;
    }
    Button Choice(string text,float x,float y,float width,Action action)
    {
        TMP_Text caption;
        Button b = MushroomUI.Button("Choice",page.transform,text,new Color(.20f,.35f,.23f),out caption);
        caption.color = Color.white; caption.fontSize = 20;
        MushroomUI.Place((RectTransform)b.transform,x,y,width,46);
        b.onClick.AddListener(()=> { if (!isTyping) action(); });
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
                Speak(new [] { "Ready to look for the mushrooms I showed you?" }, SearchOffer); break;
            case Stage.ReportMushrooms:
                Speak(new [] { "You found them all! Thank you.",
                    "Could you bring some river water for my little garden? Take this bucket.",
                    "Follow the blue marker to the river. Stop the moving pointer in the green zone to fill the bucket." },
                    ()=> { CurrentStage = Stage.FetchWater; CloseDialogue(); }); break;
            case Stage.FetchWater:
                Speak(new [] { "Follow the blue marker to the river. Press E inside the blue ring to fill the bucket." },CloseDialogue); break;
            case Stage.ReturnWater:
                Speak(new [] { "Just the right amount! Please water the little garden beside me." },
                    ()=> { CurrentStage = Stage.WaterGarden; CloseDialogue(); }); break;
            case Stage.WaterGarden:
                Speak(new [] { "Press E beside the little garden to water it." },CloseDialogue); break;
            default:
                Speak(new [] { "Thank you for helping. Enjoy the rest of the trail!" },CloseDialogue); break;
        }
    }
    void Greeting()
    {
        Speak(new [] { "Good morning, traveler! What brings you here?" },()=>{});
        // Le scelte sono visibili, ma diventano utilizzabili dopo la scrittura.
        Choice("I am your old friend's grandchild.",-390,-103,390,()=>Branch(0));
        Choice("Why are there so few mushrooms?",10,-103,390,()=>Branch(1));
        Choice("Have you lived here long?",410,-103,370,()=>Branch(2));
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
        Speak(text.ToArray(),QuestOffer);
    }
    void QuestOffer()
    {
        ClearPage(false);
        Label(speakerName,-550,116,1000,30,22);
        Label("Will you help me find the mushrooms?",-550,35,1100,80);
        Choice("Yes, I will help!",-285,-100,530,()=> { CurrentStage = Stage.FindMushrooms; BeginMushroomGame(); });
        Choice("I will come back later.",285,-100,530,CloseDialogue);
    }
    void SearchOffer()
    {
        ClearPage(false);
        Label(speakerName,-550,116,1000,30,22);
        Label("Ready to continue your search?",-550,35,1100,80);
        Choice("Find mushrooms",-285,-100,530,BeginMushroomGame);
        Choice("Later",285,-100,530,CloseDialogue);
    }
    void Speak(string[] text,Action done)
    {
        ClearPage(false);
        lines = text; lineIndex = 0; afterLines = done;
        Label(speakerName,-550,116,1000,30,22);
        speech = Label("",-550,30,1100,110,25);
        nextButton = Choice("Next / Show all",270,-103,520,Next);
        // Questo pulsante deve funzionare anche durante l'effetto di scrittura.
        nextButton.onClick.RemoveAllListeners(); nextButton.onClick.AddListener(Next);
        closeButton = Choice("Close",-390,-103,300,CloseDialogue);
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
        for (int i=0;i<=length;i++)
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
    public void BeginMushroomGame()
    {
        if (CurrentStage != Stage.FindMushrooms || player == null) return;
        ClearPage(true);
        Label("FIND THE MUSHROOMS",-540,295,850,45,30);
        counter = Label("",-540,247,1000,35);
        Image field = MushroomUI.Image("ForestFloor",page.transform,new Color(.22f,.29f,.15f));
        MushroomUI.Place(field.rectTransform,0,-5,1100,430);
        if (forestBackground != null) { field.sprite = forestBackground; field.color = Color.white; }
        field.gameObject.AddComponent<RectMask2D>();
        if (target == 0)
        {
            target = UnityEngine.Random.Range(minimumMushrooms,maximumMushrooms+1);
            List<Vector2> available = new List<Vector2>();
            for(int row=0;row<3;row++) for(int col=0;col<6;col++) available.Add(new Vector2(-450+col*180,-135+row*135));
            while(slots.Count < target)
            {
                int i = UnityEngine.Random.Range(0,available.Count);
                slots.Add(available[i]+new Vector2(UnityEngine.Random.Range(-25,25),UnityEngine.Random.Range(-15,15)));
                available.RemoveAt(i);
            }
        }
        // Decorazione ripetibile: non cambia riprendendo una ricerca interrotta.
        System.Random rng = new System.Random(1937);
        for(int i=0;i<32;i++)
        {
            MushroomIcon deco = MushroomUI.Rect("ForestObject",field.transform).gameObject.AddComponent<MushroomIcon>();
            deco.picture = i%3 == 0 ? MushroomIcon.Picture.Rock : MushroomIcon.Picture.Leaf;
            deco.raycastTarget = false;
            MushroomUI.Place(deco.rectTransform,rng.Next(-490,490),rng.Next(-170,170),rng.Next(40,95),65);
        }
        for(int i=0;i<target;i++)
        {
            if (collected.Contains(i)) continue;
            int index = i;
            Image hit = MushroomUI.Image("Mushroom_"+i,field.transform,new Color(1,1,1,0));
            MushroomUI.Place(hit.rectTransform,slots[i].x,slots[i].y,75,75);
            Button b = hit.gameObject.AddComponent<Button>(); b.targetGraphic = hit;
            MushroomIcon icon = MushroomUI.Rect("Mushroom",hit.transform).gameObject.AddComponent<MushroomIcon>();
            MushroomUI.Fill(icon.rectTransform); icon.picture = MushroomIcon.Picture.Mushroom;
            icon.spots = false; icon.capColor = new Color(.66f,.40f,.20f); icon.raycastTarget = false;
            b.onClick.AddListener(()=>
            {
                if (!collected.Add(index)) return;
                found++; hit.gameObject.SetActive(false); RefreshCounter();
                if (found == target)
                {
                    CurrentStage = Stage.ReportMushrooms;
                    hint.text = "All found! Return to Peter to receive your next task.";
                }
            });
        }
        hint = Label("Click each mushroom once. No time limit.",-540,-260,1080,45,21);
        RefreshCounter();
        Choice("Back to the forest",0,-310,450,CloseDialogue);
    }
    void RefreshCounter() { counter.text = "Mushrooms remaining: " + (target-found); }
    ForestBucketArt BucketIllustration(string name,Transform parent,ForestBucketArt.Picture picture,float x,float y,float w,float h)
    {
        ForestBucketArt art=MushroomUI.Rect(name,parent).gameObject.AddComponent<ForestBucketArt>();
        art.picture=picture; art.raycastTarget=false;
        MushroomUI.Place(art.rectTransform,x,y,w,h); art.SetVerticesDirty();
        return art;
    }
    void StyleBucketButton(Button button,Color tint)
    {
        button.GetComponent<Image>().color=new Color(1,1,1,0);
        ForestBucketArt background=BucketIllustration("Button artwork",button.transform,ForestBucketArt.Picture.Panel,0,0,
            ((RectTransform)button.transform).sizeDelta.x,((RectTransform)button.transform).sizeDelta.y);
        background.color=tint; background.transform.SetAsFirstSibling();
        var shadow=background.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(0,0,0,.22f); shadow.effectDistance=new Vector2(0,-3);
        button.targetGraphic=background;
        TMP_Text text=button.GetComponentInChildren<TMP_Text>(); text.fontStyle=FontStyles.Bold;
    }
    void OpenBucket()
    {
        if (CurrentStage != Stage.FetchWater) return;
        ClearPage(true);
        bucketOpen=true;
        page.GetComponent<Image>().color=new Color(.055f,.12f,.105f);
        // Illustrazione di sfondo e pannello dati vengono disegnati prima dei testi.
        BucketIllustration("Riverbank",page.transform,ForestBucketArt.Picture.Riverbank,0,44,1100,330);
        ForestBucketArt card=BucketIllustration("Information card",page.transform,ForestBucketArt.Picture.Panel,355,44,310,278);
        card.color=new Color(.045f,.105f,.095f,.94f);
        TMP_Text chapter=Label("FOREST TRAIL  /  RIVER WATER",-540,310,1080,22,15);
        chapter.color=new Color(.71f,.80f,.65f); chapter.characterSpacing=2;
        TMP_Text title=Label("A LITTLE WATER, A LITTLE CARE",-540,267,1080,48,32);
        title.fontStyle=FontStyles.Bold;
        Label("Stop the moving pointer in the green zone to fill your bucket.",-540,224,1080,34,21);

        bucketArt=BucketIllustration("Wooden bucket",page.transform,ForestBucketArt.Picture.Bucket,-100,43,330,330);
        TMP_Text tag=Label("WATER LEVEL",230,150,250,28,17);
        tag.color=new Color(.67f,.80f,.73f); tag.characterSpacing=2;
        counter=Label("0%",230,97,250,72,54); counter.fontStyle=FontStyles.Bold;
        TMP_Text range=Label("CARRY ZONE  "+Mathf.RoundToInt(minimumWater*100)+"-"+Mathf.RoundToInt(maximumWater*100)+"%",230,34,250,32,18);
        range.color=new Color(.73f,.91f,.65f);
        bucketStatus=Label("",230,-28,250,66,21);

        ForestBucketArt frame=BucketIllustration("Meter frame",page.transform,ForestBucketArt.Picture.Panel,0,-165,1000,64);
        frame.color=new Color(.025f,.075f,.065f);
        Image track=MushroomUI.Image("TimingTrack",page.transform,new Color(.27f,.39f,.40f));
        MushroomUI.Place(track.rectTransform,0,-165,960,28); track.raycastTarget=false;
        Image heavy=MushroomUI.Image("Too much water",track.transform,new Color(.61f,.36f,.23f));
        heavy.rectTransform.anchorMin=new Vector2(maximumWater,0); heavy.rectTransform.anchorMax=Vector2.one;
        heavy.rectTransform.offsetMin=heavy.rectTransform.offsetMax=Vector2.zero; heavy.raycastTarget=false;
        Image safe=MushroomUI.Image("AcceptableWater",track.transform,new Color(.35f,.66f,.43f));
        safe.rectTransform.anchorMin=new Vector2(minimumWater,0); safe.rectTransform.anchorMax=new Vector2(maximumWater,1);
        safe.rectTransform.offsetMin=safe.rectTransform.offsetMax=Vector2.zero; safe.raycastTarget=false;
        for(int i=0;i<=10;i++)
        {
            Image tick=MushroomUI.Image("Scale mark",track.transform,new Color(1,1,1,.20f));
            tick.raycastTarget=false;
            tick.rectTransform.anchorMin=tick.rectTransform.anchorMax=new Vector2(i/10f,0);
            tick.rectTransform.pivot=new Vector2(.5f,0); tick.rectTransform.anchoredPosition=Vector2.zero;
            tick.rectTransform.sizeDelta=new Vector2(2,i%5==0 ? 10 : 5);
        }
        Image pointer=MushroomUI.Image("MovingPointer",track.transform,new Color(.94f,.96f,.83f));
        bucketCursor=pointer.rectTransform; bucketCursor.sizeDelta=new Vector2(4,48); pointer.raycastTarget=false;
        ForestBucketArt drop=BucketIllustration("Pointer droplet",pointer.transform,ForestBucketArt.Picture.Droplet,0,36,18,25);
        drop.color=new Color(.85f,.94f,.87f);
        Label("NO WATER",-480,-212,240,26,16).color=new Color(.72f,.83f,.82f);
        float safeCenter=-480f+(minimumWater+maximumWater)*.5f*960f;
        TMP_Text good=Label("GOOD AMOUNT",safeCenter-120,-212,240,26,16);
        good.alignment=TextAlignmentOptions.Center; good.color=new Color(.75f,.93f,.70f);
        TMP_Text high=Label("TOO MUCH",240,-212,240,26,16);
        high.alignment=TextAlignmentOptions.MidlineRight; high.color=new Color(.91f,.72f,.56f);
        hint=Label("",-540,-251,1080,32,20); hint.alignment=TextAlignmentOptions.Center;
        bucketAction=Choice("STOP",-260,-302,480,BucketAction);
        bucketActionCaption=bucketAction.GetComponentInChildren<TMP_Text>();
        StyleBucketButton(bucketAction,new Color(.30f,.52f,.36f));
        Navigation nav=bucketAction.navigation; nav.mode=Navigation.Mode.None; bucketAction.navigation=nav;
        Button back=Choice("Back to the forest",260,-302,480,CloseDialogue);
        StyleBucketButton(back,new Color(.14f,.26f,.22f));
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
            hint.color = new Color(1f,.76f,.43f);
            bucketActionCaption.text = "Try again";
        }
        else if (water > maximumWater)
        {
            hint.text = "Too much water! The bucket is too heavy. Try again.";
            hint.color = new Color(1f,.76f,.43f);
            bucketActionCaption.text = "Try again";
        }
        else
        {
            bucketSucceeded = true;
            hint.text = "Just right! Take the bucket back to " + speakerName + ".";
            hint.color = new Color(.55f,1f,.61f);
            bucketActionCaption.text = "Take the bucket";
        }
        UpdateWater();
    }
    void UpdateWater()
    {
        if(bucketArt!=null) bucketArt.SetWater(water,bucketRunning,bucketSucceeded);
        if(counter!=null) counter.text=Mathf.RoundToInt(water*100)+"%";
        if(bucketStatus!=null)
        {
            bucketStatus.text=bucketRunning ? "Find the right moment.\nPress STOP on green." :
                bucketSucceeded ? "Just right!\nReady to carry." :
                water<minimumWater ? "Not enough water.\nGive it another try." : "Too heavy to carry.\nGive it another try.";
            bucketStatus.color=bucketSucceeded ? new Color(.75f,.93f,.65f) : new Color(.86f,.91f,.83f);
        }
        if(bucketAction!=null)
            bucketAction.targetGraphic.color=bucketSucceeded ? new Color(.40f,.62f,.37f) : new Color(.30f,.52f,.36f);
        if(bucketCursor!=null)
        {
            bucketCursor.anchorMin=bucketCursor.anchorMax=new Vector2(water,.5f);
            bucketCursor.anchoredPosition=Vector2.zero;
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
        float distance = Vector3.Distance(player.transform.position,riverPoint.position);
        riverLabel.text = "WATER COLLECTION\n" + Mathf.CeilToInt(distance) + " m"
            + (Near(riverPoint) ? "\n[E] Fill the bucket" : "");
        riverLabel.transform.localPosition = Vector3.up * (riverMarkerHeight + .35f);
        riverLabel.color = riverMarkerColor;
        for (int i=0;i<48;i++)
        {
            float angle = i * Mathf.PI * 2f / 48;
            riverRing.SetPosition(i,new Vector3(Mathf.Cos(angle)*riverMarkerRadius,.10f,Mathf.Sin(angle)*riverMarkerRadius));
        }
        riverBeam.SetPosition(0,Vector3.up*.15f);
        riverBeam.SetPosition(1,Vector3.up*riverMarkerHeight);
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
        riverRing = MarkerLine("GroundRing",48,.08f,true);
        riverBeam = MarkerLine("Beacon",2,.07f,false);
        GameObject text = new GameObject("WaterPointLabel");
        text.transform.SetParent(riverMarker.transform,false);
        riverLabel = text.AddComponent<TextMeshPro>();
        if (MushroomUI.Font != null) riverLabel.font = MushroomUI.Font;
        riverLabel.text = "WATER COLLECTION";
        riverLabel.fontSize = 3f;
        riverLabel.alignment = TextAlignmentOptions.Center;
        riverLabel.rectTransform.sizeDelta = new Vector2(6,2);
    }
    LineRenderer MarkerLine(string name,int count,float width,bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(riverMarker.transform,false);
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
        typing = null; isTyping=false;
        if (!IsOpen) return;
        uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this,false);
        Cursor.lockState=oldCursorLock; Cursor.visible=oldCursorVisible;
    }
    void OnGUI()
    {
        if (IsOpen || player == null) return;
        if (CurrentStage == Stage.FetchWater && riverPoint != null)
        {
            float distance = Vector3.Distance(player.transform.position,riverPoint.position);
            GUI.Box(new Rect(20,20,420,58),"OBJECTIVE: Collect river water\nFollow the blue marker - " + Mathf.CeilToInt(distance) + " m");
            // Etichetta sullo schermo per localizzare il punto anche a distanza.
            Camera camera = Camera.main;
            if (camera != null && !Near(riverPoint))
            {
                Vector3 screen = camera.WorldToScreenPoint(riverPoint.position + Vector3.up*riverMarkerHeight);
                if (screen.z > 0)
                {
                    float x = Mathf.Clamp(screen.x,110,Screen.width-110);
                    float y = Mathf.Clamp(Screen.height-screen.y,95,Screen.height-115);
                    GUI.Box(new Rect(x-100,y-20,200,40),"WATER - " + Mathf.CeilToInt(distance)+" m");
                }
                else GUI.Box(new Rect(Screen.width/2-160,90,320,35),"Turn around to find the river marker");
            }
        }
        string text = CurrentStage == Stage.FetchWater && Near(riverPoint) ? "E - Fill the bucket" :
            CurrentStage == Stage.WaterGarden && Near(gardenPoint) ? "E - Water the garden" :
            Near(transform) ? "E - Talk to " + speakerName : "";
        if (text != "") GUI.Box(new Rect(Screen.width/2-170,Screen.height-85,340,42),text);
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
