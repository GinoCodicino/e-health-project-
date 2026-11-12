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
    public Stage CurrentStage { get; private set; }
    public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }
    public int FoundCount { get { return found; } }

    GameObject uiRoot, page;
    RectTransform canvas;
    TMP_Text speech, counter, hint;
    Image waterFill;
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
        if (Near(transform)) OpenDialogue(player);
        else if (CurrentStage == Stage.FetchWater && Near(riverPoint)) OpenBucket();
        else if (CurrentStage == Stage.WaterGarden && Near(gardenPoint))
        {
            CurrentStage = Stage.Complete;
            Speak(new [] { "Thank you! This water helps our little garden.",
                "Caring for the whole forest takes much more. Your grandfather would be proud of you." }, CloseDialogue);
        }
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
        MushroomUI.Place(t.rectTransform,x,y,w,h);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.overflowMode = TextOverflowModes.Overflow;
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
                    "Fill it enough to water the garden, but keep it light enough to carry." },
                    ()=> { CurrentStage = Stage.FetchWater; CloseDialogue(); }); break;
            case Stage.FetchWater:
                Speak(new [] { "Take the bucket to the river. Press E at the water collection point." },CloseDialogue); break;
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
        Label(speakerName,-460,116,240,30,22);
        Label("Will you help me find the mushrooms?",-550,35,1100,80);
        Choice("Yes, I will help!",-285,-100,530,()=> { CurrentStage = Stage.FindMushrooms; BeginMushroomGame(); });
        Choice("I will come back later.",285,-100,530,CloseDialogue);
    }
    void SearchOffer()
    {
        ClearPage(false);
        Label(speakerName,-460,116,240,30,22);
        Label("Ready to continue your search?",-550,35,1100,80);
        Choice("Find mushrooms",-285,-100,530,BeginMushroomGame);
        Choice("Later",285,-100,530,CloseDialogue);
    }
    void Speak(string[] text,Action done)
    {
        ClearPage(false);
        lines = text; lineIndex = 0; afterLines = done;
        Label(speakerName,-460,116,240,30,22);
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
    void OpenBucket()
    {
        ClearPage(true);
        Label("FILL THE BUCKET",-540,290,1000,50,32);
        Label("Choose a useful amount you can still carry.",-540,225,1080,50);
        Image bucket = MushroomUI.Image("Bucket",page.transform,new Color(.45f,.40f,.32f));
        MushroomUI.Place(bucket.rectTransform,0,10,300,310);
        waterFill = MushroomUI.Image("Water",bucket.transform,new Color(.20f,.65f,.85f));
        waterFill.rectTransform.anchorMin = Vector2.zero; waterFill.rectTransform.anchorMax = new Vector2(1,water);
        waterFill.rectTransform.offsetMin = new Vector2(8,8); waterFill.rectTransform.offsetMax = new Vector2(-8,-8);
        counter = Label("",-150,-180,600,40);
        hint = Label("",-540,-230,1080,45,21);
        // Slider compatibile anche con trackpad e tastiera, senza tempi di reazione.
        Slider slider = MushroomUI.Image("WaterSlider",page.transform,new Color(.6f,.6f,.5f)).gameObject.AddComponent<Slider>();
        MushroomUI.Place((RectTransform)slider.transform,0,-115,800,24);
        Image handle = MushroomUI.Image("Handle",slider.transform,Color.white);
        handle.rectTransform.sizeDelta = new Vector2(30,44);
        slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue=0; slider.maxValue=1; slider.value=water;
        slider.onValueChanged.AddListener(value=> { water=value; UpdateWater(); });
        Choice("Confirm",-270,-300,470,()=>
        {
            if (water < minimumWater) hint.text = "Too little water. Add some more.";
            else if (water > maximumWater) hint.text = "Too heavy! Pour a little out.";
            else { CurrentStage=Stage.ReturnWater; CloseDialogue(); }
        });
        Choice("Back",270,-300,470,CloseDialogue);
        UpdateWater();
    }
    void UpdateWater()
    {
        waterFill.rectTransform.anchorMax = new Vector2(1,water);
        // Altezza proporzionale senza invertire il rettangolo quando e vuoto.
        waterFill.rectTransform.offsetMin = new Vector2(8,0);
        waterFill.rectTransform.offsetMax = new Vector2(-8,0);
        counter.text = "Bucket: " + Mathf.RoundToInt(water*100)+"%";
    }
    public void CloseDialogue()
    {
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
        string text = Near(transform) ? "E - Talk to " + speakerName :
            CurrentStage == Stage.FetchWater && Near(riverPoint) ? "E - Fill the bucket" :
            CurrentStage == Stage.WaterGarden && Near(gardenPoint) ? "E - Water the garden" : "";
        if (text != "") GUI.Box(new Rect(Screen.width/2-170,Screen.height-85,340,42),text);
    }
    protected virtual void OnDisable() { CloseDialogue(); }
    protected virtual void OnDestroy() { if(uiRoot != null) Destroy(uiRoot); }
}
