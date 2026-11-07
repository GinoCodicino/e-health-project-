using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Level02.BridgeMission
{
    public sealed class BridgeQuestController : MonoBehaviour
    {
        [Header("Collegamenti scena")]
        public playercontroller player;
        public Transform fisher;
        public Transform woodcutter;
        public Transform bridgeInteraction;
        public BridgeCondition bridge;
        [Header("Sblocco dopo la prima missione")]
        public ForestQuest mushroomQuest;
        public bool requireMushroomQuest = true;
        public bool IsUnlocked { get { return !requireMushroomQuest ||
            (mushroomQuest != null && mushroomQuest.CurrentStage == ForestQuest.Stage.Complete); } }
        [Header("Dialoghi")]
        public string fisherName="Noah";
        public string woodcutterName="Rowan";
        public float interactionDistance=3f;
        [Range(0f,.1f)] public float typingSpeed=.025f;
        [Range(.2f,.95f)] public float dialogueOpacity=.78f;
        [Header("Prova del taglialegna")]
        [Range(3,4)] public int minimumLogs=3;
        [Range(3,4)] public int maximumLogs=4;
        [Min(1)] public int hitsPerLog=5;
        [Min(5)] public float timeLimitSeconds=30f;

        readonly BridgeQuestProgress progress=new BridgeQuestProgress();
        public BridgeStage CurrentStage { get { return progress.Stage; } }
        public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }
        enum ScreenMode { None, Dialogue, ChopInstructions, LogMap, Chopping, Puzzle }
        ScreenMode mode;
        GameObject uiRoot,page;
        RectTransform canvas;
        TMP_Text speech,timerText,chopCount,feedback,hitCaption,puzzleFeedback;
        Button chopButton,next;
        TimberPicture activeLogPicture;
        Coroutine returnToMap;
        readonly Button[] logButtons = new Button[4];
        readonly TMP_Text[] logCaptions = new TMP_Text[4];
        readonly RectTransform[] gapRects = new RectTransform[3];
        readonly BridgePlankDrag[] draggablePlanks = new BridgePlankDrag[3];
        int lineIndex;
        string[] lines;
        Action afterLines;
        Coroutine typing;
        bool isTyping;
        int closeFrame=-1;
        BridgeQuestMarker marker;
        Camera view;
        static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

        void Awake()
        {
            if(player==null) player=FindFirstObjectByType<playercontroller>();
            if(player!=null) view=player.GetComponentInChildren<Camera>(true);
            if(view==null) view=Camera.main;
            marker=GetComponent<BridgeQuestMarker>();
            if(marker==null) marker=gameObject.AddComponent<BridgeQuestMarker>();
            FindMushroomQuest();
            minimumLogs=Mathf.Clamp(minimumLogs,3,4);
            maximumLogs=Mathf.Clamp(maximumLogs,minimumLogs,4);
            hitsPerLog=Mathf.Max(1,hitsPerLog); timeLimitSeconds=Mathf.Max(5,timeLimitSeconds);
        }
        void Update()
        {
            if(player==null) return;
            if (!IsUnlocked)
            {
                if (IsOpen) Close();
                marker.Hide(); return;
            }
            if(IsOpen)
            {
                marker.Hide();
                if ((mode==ScreenMode.LogMap || mode==ScreenMode.Chopping) && progress.Attempt==ChopResult.Running)
                {
                    progress.Tick(Now);
                    if (progress.Attempt==ChopResult.Failed) ShowChopResult();
                    else RefreshChopDisplay();
                }
                if(playerinputcontroller.ConsumeEscape()) { Close(); return; }
                if(isTyping && Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame) ShowAll();
                return;
            }
            if(player.IsGameplayBlocked || closeFrame==Time.frameCount) { marker.Hide(); return; }
            UpdateObjective();
            if(Keyboard.current==null || !Keyboard.current.eKey.wasPressedThisFrame) return;
            if(progress.Stage==BridgeStage.RepairBridge && Near(bridgeInteraction)) OpenPuzzle();
            else
            {
                float a=Distance(fisher),b=Distance(woodcutter);
                if(a<=interactionDistance && a<=b) TalkFisher();
                else if(b<=interactionDistance) TalkWoodcutter();
            }
        }
        // Preferisce il personaggio Peter per evitare di collegarsi ad altri ForestQuest.
        public void FindMushroomQuest()
        {
            if (mushroomQuest != null) return;
            GameObject peter = GameObject.Find("NPC_funghi");
            if (peter != null) mushroomQuest = peter.GetComponent<ForestQuest>();
            if (mushroomQuest == null) mushroomQuest = FindFirstObjectByType<ForestQuest>();
        }
        float Distance(Transform point) { return point==null ? float.PositiveInfinity : Vector3.Distance(player.transform.position,point.position); }
        bool Near(Transform point) { return Distance(point)<=interactionDistance; }
        void UpdateObjective()
        {
            Transform target=null; string text="";
            switch(progress.Stage)
            {
                case BridgeStage.MeetFisher: target=fisher; text="Talk to "+fisherName; break;
                case BridgeStage.FindWoodcutter:
                case BridgeStage.PrepareWood: target=woodcutter; text="Find "+woodcutterName+", the woodcutter"; break;
                case BridgeStage.RepairBridge: target=bridgeInteraction; text="Repair the bridge"; break;
                case BridgeStage.ReportRepair: target=fisher; text="Return to "+fisherName; break;
            }
            marker.Show(target,text,view);
        }
        void OpenPage(bool full)
        {
            if(uiRoot==null)
            {
                canvas=MushroomUI.Canvas("BridgeQuest_UI"); uiRoot=canvas.gameObject;
                uiRoot.SetActive(false);
            }
            if(!uiRoot.activeSelf) { player.SetUiMode(this,true); uiRoot.SetActive(true); }
            if(returnToMap!=null) StopCoroutine(returnToMap);
            returnToMap=null;
            if(typing!=null) StopCoroutine(typing);
            typing=null; isTyping=false;
            if(page!=null) { page.SetActive(false); Destroy(page); }
            Image panel=MushroomUI.Image("Page",canvas,new Color(.035f,.075f,.065f,full ? 1f : dialogueOpacity));
            page=panel.gameObject;
            if(full) MushroomUI.Place(panel.rectTransform,0,0,1200,680);
            else
            {
                panel.rectTransform.anchorMin=panel.rectTransform.anchorMax=new Vector2(.5f,0);
                panel.rectTransform.pivot=new Vector2(.5f,0);
                panel.rectTransform.anchoredPosition=new Vector2(0,20);
                panel.rectTransform.sizeDelta=new Vector2(1200,300);
            }
        }
        TMP_Text Label(string text,float x,float y,float w,float h,float size=24)
        {
            TMP_Text label=MushroomUI.Text("Text",page.transform,text,size,Color.white);
            label.rectTransform.pivot=new Vector2(0,.5f);
            MushroomUI.Place(label.rectTransform,x,y,w,h);
            label.alignment=TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping=true; label.overflowMode=TextOverflowModes.Truncate;
            return label;
        }
        Button Button(string text,float x,float y,float w,float h,Action action)
        {
            TMP_Text label;
            Button button=MushroomUI.Button("Button",page.transform,text,new Color(.22f,.38f,.29f),out label);
            label.fontSize=21; label.color=Color.white;
            MushroomUI.Place((RectTransform)button.transform,x,y,w,h);
            Navigation nav=button.navigation; nav.mode=Navigation.Mode.None; button.navigation=nav;
            button.onClick.AddListener(()=> { if(!isTyping) action(); });
            return button;
        }
        void Speak(string name,string[] text,Action finish)
        {
            OpenPage(false); mode=ScreenMode.Dialogue;
            lines=text; lineIndex=0; afterLines=finish;
            Label(name,-550,116,1050,32,22);
            speech=Label("",-550,24,1100,116,25);
            Button("Close",-390,-104,300,46,Close);
            next=Button("Next / Show all",270,-104,520,46,Advance);
            next.onClick.RemoveAllListeners(); next.onClick.AddListener(Advance);
            TypeLine();
        }
        void TypeLine()
        {
            speech.text=lines[lineIndex]; speech.maxVisibleCharacters=0;
            speech.ForceMeshUpdate(); typing=StartCoroutine(Type(speech.textInfo.characterCount));
        }
        IEnumerator Type(int count)
        {
            isTyping=true;
            for(int i=0;i<=count;i++)
            {
                speech.maxVisibleCharacters=i;
                if(typingSpeed>0) yield return new WaitForSecondsRealtime(typingSpeed);
            }
            isTyping=false; typing=null;
        }
        void ShowAll()
        {
            if(returnToMap!=null) StopCoroutine(returnToMap);
            returnToMap=null;
            if(typing!=null) StopCoroutine(typing);
            typing=null; isTyping=false; if(speech!=null) speech.maxVisibleCharacters=int.MaxValue;
        }
        void Advance()
        {
            if(isTyping) { ShowAll(); return; }
            if(++lineIndex<lines.Length) TypeLine(); else afterLines?.Invoke();
        }
        void TalkFisher()
        {
            if(progress.Stage==BridgeStage.MeetFisher)
            {
                Speak(fisherName,new[]{"Oh, this bridge... Good morning! What brings you to the river?"},()=>{});
                next.gameObject.SetActive(false);
                // Chiudi il saluto con Esc; i tre pulsanti sostituiscono i controlli di avanzamento.
                foreach(Button button in page.GetComponentsInChildren<Button>()) button.gameObject.SetActive(false);
                Button("You knew my grandfather?",-390,-104,380,46,()=>FisherBranch(0));
                Button("What happened to the bridge?",0,-104,380,46,()=>FisherBranch(1));
                Button("Can I help you?",390,-104,380,46,()=>FisherBranch(2));
            }
            else if(progress.Stage==BridgeStage.RepairBridge)
                Speak(fisherName,new[]{"You have the wood! Use it to fill the three gaps. Press E at the bridge repair point."},Close);
            else if(progress.Stage==BridgeStage.ReportRepair)
                Speak(fisherName,new[]{"The bridge is safe again! Thank you.","Your grandfather taught me to fish here. Now you have helped keep this place alive."},()=>{progress.Finish();Close();});
            else if(progress.Stage==BridgeStage.Complete)
                Speak(fisherName,new[]{"Thank you, my friend. Take care as you cross!"},Close);
            else Speak(fisherName,new[]{"Ask "+woodcutterName+" for suitable planks. Follow the marker into the forest."},Close);
        }
        void FisherBranch(int branch)
        {
            string[][] branches={
                new[]{"Sixty years ago, your grandfather taught me to fish from this bridge. I was only a child.","You: What happened to it?","The last flood damaged it. We cannot cross safely now."},
                new[]{"The last flood damaged the bridge. The current tore some boards away.","You: Did you know this place before?","Yes. Your grandfather gave me fishing lessons here when I was a child, sixty years ago."},
                new[]{"We need suitable wood to repair the bridge after the flood.","You: Why is this bridge so special to you?","Your grandfather taught me to fish here, sixty years ago. I would love to see it safe again."}
            };
            List<string> text=new List<string>(branches[branch]);
            text.Add("You: Who could help us repair it?");
            text.Add("The woodcutter, "+woodcutterName+", works in the forest. Will you ask him for three planks?");
            Speak(fisherName,text.ToArray(),FisherOffer);
        }
        void FisherOffer()
        {
            OpenPage(false); mode=ScreenMode.Dialogue;
            Label(fisherName,-550,116,1000,32,22);
            Label("Will you help us repair the bridge?",-550,30,1100,90);
            Button("I will find the woodcutter.",-280,-104,520,46,()=>{progress.AcceptFisherTask();Close();});
            Button("Later",280,-104,520,46,Close);
        }
        void TalkWoodcutter()
        {
            if(progress.Stage==BridgeStage.MeetFisher)
            { Speak(woodcutterName,new[]{"Hello! If you need help with the bridge, speak to "+fisherName+" by the river first."},Close); return; }
            if(progress.Stage==BridgeStage.FindWoodcutter)
            {
                Speak(woodcutterName,new[]{"You need planks for the bridge? I have some suitable wood.","You: Can you help us?","Yes. First, help me cut these fallen logs. We will use storm-felled wood and leave healthy trees standing."},ChopInstructions);
                return;
            }
            if(progress.Stage==BridgeStage.PrepareWood)
            {
                if(progress.Attempt==ChopResult.Won) OfferPlanks();
                else ChopInstructions();
                return;
            }
            Speak(woodcutterName,new[]{progress.Stage==BridgeStage.RepairBridge ? "You have the planks. Take them back to the bridge!" : "Good work! We made useful timber from the fallen wood."},Close);
        }
        void ChopInstructions()
        {
            OpenPage(true); mode=ScreenMode.ChopInstructions;
            Label("HELP THE WOODCUTTER",-540,280,1080,54,32);
            Label("Find "+minimumLogs+" to "+maximumLogs+" fallen logs on the forest map.\nSelect a log, then hit it "+hitsPerLog+" times to cut it.\n\nYou have "+timeLimitSeconds.ToString("0")+" seconds for the whole map.\nThe timer starts only when you press Start.",-500,55,1000,300,26);
            Button("Start",-270,-270,480,56,StartChopping);
            Button("Later",270,-270,480,56,Close);
        }
        void StartChopping()
        {
            int logs=UnityEngine.Random.Range(minimumLogs,maximumLogs+1);
            if(!progress.StartAttempt(logs,hitsPerLog,Now,timeLimitSeconds)) return;
            OpenLogMap();
        }
        Image Box(string name,float x,float y,float width,float height,Color color)
        {
            Image image=MushroomUI.Image(name,page.transform,color);
            MushroomUI.Place(image.rectTransform,x,y,width,height);
            image.raycastTarget=false; return image;
        }
        TimberPicture Art(Transform parent,TimberPicture.Picture kind,float x,float y,float width,float height)
        {
            TimberPicture picture=MushroomUI.Rect("Illustration",parent).gameObject.AddComponent<TimberPicture>();
            picture.picture=kind; picture.raycastTarget=false;
            MushroomUI.Place(picture.rectTransform,x,y,width,height); picture.SetVerticesDirty();
            return picture;
        }
        void ChopHeader(string title)
        {
            Label(title,-540,290,1080,44,30);
            timerText=Label("",-540,235,500,38,25);
            chopCount=Label("",50,235,490,38,23);
        }
        void OpenLogMap()
        {
            if (progress.Attempt!=ChopResult.Running) { ShowChopResult(); return; }
            OpenPage(true); mode=ScreenMode.LogMap;
            activeLogPicture=null; hitCaption=null;
            ChopHeader("FOREST MAP - CHOOSE A FALLEN LOG");
            Box("Forest clearing",0,-10,1100,390,new Color(.18f,.28f,.16f));
            Box("Footpath",0,-10,140,390,new Color(.40f,.34f,.23f));
            for(int i=0;i<12;i++)
            {
                float x=i%2==0 ? -480 : 480; float y=150-(i/2)*64;
                Art(page.transform,TimberPicture.Picture.Tree,x,y,100,130);
            }
            Vector2[] positions={new Vector2(-265,90),new Vector2(245,75),new Vector2(-220,-110),new Vector2(275,-110)};
            for(int i=0;i<progress.RequiredLogs;i++)
            {
                int index=i;
                logButtons[i]=Button("",positions[i].x,positions[i].y,300,120,()=>OpenLog(index));
                logButtons[i].GetComponent<Image>().color=new Color(.10f,.17f,.09f,.32f);
                TimberPicture picture=Art(logButtons[i].transform,TimberPicture.Picture.Log,0,10,260,100);
                picture.damage=progress.HitsForLog(i)/(float)progress.HitsPerLog; picture.SetVerticesDirty();
                TMP_Text label=logButtons[i].GetComponentInChildren<TMP_Text>();
                MushroomUI.Place(label.rectTransform,0,-40,290,28); label.fontSize=19;
                label.text=progress.IsLogComplete(i) ? "CUT - Log "+(i+1) : "Log "+(i+1)+" - select";
                logCaptions[i]=label;
                logButtons[i].interactable=!progress.IsLogComplete(i);
            }
            feedback=Label("Select any uncut log. The timer keeps running between views.",-540,-244,1080,48,22);
            Button("Leave challenge",0,-300,440,48,Close);
            RefreshChopDisplay();
        }
        void OpenLog(int index)
        {
            progress.Tick(Now);
            if (progress.Attempt!=ChopResult.Running) { ShowChopResult(); return; }
            if (!progress.SelectLog(index)) return;
            OpenPage(true); mode=ScreenMode.Chopping;
            ChopHeader("CUT LOG "+(index+1));
            Box("Ground",0,0,1100,370,new Color(.18f,.25f,.14f));
            chopButton=Button("",0,10,900,300,TapLog);
            chopButton.GetComponent<Image>().color=new Color(1,1,1,0);
            activeLogPicture=Art(chopButton.transform,TimberPicture.Picture.Log,0,0,850,280);
            activeLogPicture.showAxe=true;
            activeLogPicture.damage=progress.HitsForLog(index)/(float)progress.HitsPerLog;
            activeLogPicture.SetVerticesDirty();
            hitCaption=Label("",-500,-175,1000,38,26); hitCaption.alignment=TextAlignmentOptions.Center;
            feedback=Label("Tap the log to swing the axe.",-540,-235,1080,48,22);
            feedback.alignment=TextAlignmentOptions.Center;
            Button("Back to the map",-270,-300,490,48,OpenLogMap);
            Button("Leave challenge",270,-300,490,48,Close);
            RefreshChopDisplay();
        }
        void TapLog()
        {
            int index=progress.SelectedLog;
            if(!progress.HitLog(index,Now))
            {
                if(progress.Attempt==ChopResult.Failed) ShowChopResult();
                return;
            }
            if(activeLogPicture!=null) activeLogPicture.PlayHit(progress.HitsForLog(index)/(float)progress.HitsPerLog);
            RefreshChopDisplay();
            if(progress.IsLogComplete(index))
            {
                chopButton.interactable=false;
                feedback.text="Log cut!";
                returnToMap=StartCoroutine(ReturnAfterCut());
            }
        }
        IEnumerator ReturnAfterCut()
        {
            yield return new WaitForSecondsRealtime(.30f);
            returnToMap=null;
            if(!IsOpen) yield break;
            if(progress.Attempt==ChopResult.Won) ShowChopResult(); else OpenLogMap();
        }
        void RefreshChopDisplay()
        {
            if(timerText!=null)
            {
                timerText.text="Time left: "+Math.Ceiling(progress.Remaining(Now))+" s";
                timerText.color=progress.Remaining(Now)<=5 ? new Color(1,.6f,.4f) : Color.white;
            }
            if(chopCount!=null) chopCount.text="Logs cut: "+progress.CompletedLogs+" / "+progress.RequiredLogs;
            if(mode==ScreenMode.Chopping && hitCaption!=null)
                hitCaption.text="Hits: "+progress.HitsForLog(progress.SelectedLog)+" / "+progress.HitsPerLog;
        }
        void ShowChopResult()
        {
            OpenPage(true); mode=ScreenMode.ChopInstructions;
            bool won=progress.Attempt==ChopResult.Won;
            Label(won ? "WOOD READY!" : "TIME IS UP",-540,275,1080,60,34);
            TimberPicture picture=Art(page.transform,TimberPicture.Picture.Log,0,50,700,260);
            picture.damage=won ? 1f : .3f; picture.SetVerticesDirty();
            Label(won ? "You cut all the logs. Talk to "+woodcutterName+" to collect the planks."
                : "You cut "+progress.CompletedLogs+" of "+progress.RequiredLogs+" logs. Try again!",-500,-150,1000,85,25);
            Button(won ? "Talk to "+woodcutterName : "Try again",-270,-280,490,54,
                ()=> { if(won) OfferPlanks(); else StartChopping(); });
            Button("Back",270,-280,490,54,Close);
        }
        void OfferPlanks()
        {
            Speak(woodcutterName,new[]{"Good work! Here are three planks of different lengths.","Take them to the bridge. Drag each plank into the gap with the same length."},()=>
            {
                if(progress.TakePlanks()) Close();
            });
        }
        void OpenPuzzle()
        {
            if(bridge==null || !bridge.IsConfigured)
            { Debug.LogError("Bridge quest: configure intact and broken bridge first.",this); return; }
            OpenPage(true); mode=ScreenMode.Puzzle;
            Label("REBUILD THE BRIDGE",-540,290,1080,46,32);
            Label("Drag each plank from the bottom onto a gap of the same length.",-540,238,1080,42,23);
            DrawRiverAndBanks();
            int[] lengths={170,250,330}; int[] order={2,0,1};
            for(int i=0;i<3;i++)
            {
                float y=120-i*68;
                float remaining=420-lengths[i]*.5f;
                // Assi residue sulle due rive: il buco e allineato al centro del fiume.
                Art(page.transform,TimberPicture.Picture.Plank,-420+remaining*.5f,y,remaining,57);
                Art(page.transform,TimberPicture.Picture.Plank,420-remaining*.5f,y,remaining,57);
                Image slot=Box("Gap "+i,0,y,lengths[i],57,new Color(.035f,.10f,.12f,.86f));
                gapRects[i]=slot.rectTransform;
                slot.gameObject.AddComponent<Outline>().effectColor=new Color(.84f,.78f,.49f);
            }
            Box("Plank tray",0,-223,1120,105,new Color(.09f,.14f,.12f));
            for(int i=0;i<3;i++)
            {
                int id=order[i];
                Image image=MushroomUI.Image("Draggable plank "+id,page.transform,new Color(1,1,1,0));
                MushroomUI.Place(image.rectTransform,-375+i*375,-223,lengths[id],57);
                image.raycastTarget=true;
                Art(image.transform,TimberPicture.Picture.Plank,0,0,lengths[id],57);
                draggablePlanks[id]=image.gameObject.AddComponent<BridgePlankDrag>();
                draggablePlanks[id].Initialize(this,id);
            }
            puzzleFeedback=Label("Drag a plank to start repairing.",-540,-137,1080,36,22);
            Button("Back to the river",0,-302,500,46,Close);
            RefreshPuzzle();
        }
        void DrawRiverAndBanks()
        {
            Box("River",0,26,1100,350,new Color(.12f,.39f,.47f));
            Box("Left bank",-440,26,220,350,new Color(.30f,.40f,.20f));
            Box("Right bank",440,26,220,350,new Color(.30f,.40f,.20f));
            Box("Left shore",-321,26,22,350,new Color(.53f,.48f,.32f));
            Box("Right shore",321,26,22,350,new Color(.53f,.48f,.32f));
            for(int i=0;i<8;i++)
            {
                float x=-260+i*75;
                Box("Current",x,-80+(i%3)*22,38,3,new Color(.42f,.70f,.74f,.65f));
            }
            for(int i=0;i<6;i++)
            {
                float x=i%2==0 ? -475 : 475; float y=160-(i/2)*108;
                Art(page.transform,TimberPicture.Picture.Tree,x,y,75,110);
            }
            Box("Left abutment",-400,52,32,235,new Color(.35f,.25f,.14f));
            Box("Right abutment",400,52,32,235,new Color(.35f,.25f,.14f));
        }
        public bool DropPlank(int id,Vector2 screenPosition,Camera eventCamera)
        {
            if(mode!=ScreenMode.Puzzle || !IsOpen || progress.Stage!=BridgeStage.RepairBridge) return false;
            int slot=-1;
            for(int i=0;i<3;i++)
                if(!progress.IsPlaced(i) && RectTransformUtility.RectangleContainsScreenPoint(gapRects[i],screenPosition,eventCamera))
                { slot=i; break; }
            if(slot<0) { puzzleFeedback.text="Drop the plank inside a bridge gap."; return false; }
            if(bridge==null || !bridge.IsConfigured) { puzzleFeedback.text="Bridge setup is incomplete."; return false; }
            if(!progress.PlacePlank(id,slot))
            { puzzleFeedback.text="That plank does not fit. Match the length."; return false; }
            RefreshPuzzle();
            if(progress.Stage==BridgeStage.ReportRepair)
            {
                bridge.SetRepaired(true);
                Speak("BRIDGE REPAIRED",new[]{"All three planks fit. The bridge is safe to cross!","Return to "+fisherName+" and tell him the good news."},Close);
            }
            else puzzleFeedback.text="Good fit! Drag the next plank into place.";
            return true;
        }
        void RefreshPuzzle()
        {
            for(int i=0;i<3;i++)
                if(progress.IsPlaced(i) && draggablePlanks[i]!=null) draggablePlanks[i].LockTo(gapRects[i]);
        }
        public void Close()
        {
            if(returnToMap!=null) StopCoroutine(returnToMap);
            returnToMap=null;
            if(typing!=null) StopCoroutine(typing);
            typing=null; isTyping=false;
            if(mode==ScreenMode.Chopping || mode==ScreenMode.LogMap) progress.CancelAttempt();
            mode=ScreenMode.None;
            if(uiRoot!=null) uiRoot.SetActive(false);
            if(player!=null) player.SetUiMode(this,false);
            closeFrame=Time.frameCount;
        }
        void OnGUI()
        {
            if(!IsUnlocked || IsOpen || player==null || player.IsGameplayBlocked || closeFrame==Time.frameCount) return;
            string text="";
            if(progress.Stage==BridgeStage.RepairBridge && Near(bridgeInteraction)) text="E - Repair the bridge";
            else if(Near(fisher) && Distance(fisher)<=Distance(woodcutter))
                text=progress.Stage==BridgeStage.MeetFisher ? fisherName+": 'That bridge... what a mess.'\nE - Talk" : "E - Talk to "+fisherName;
            else if(Near(woodcutter)) text="E - Talk to "+woodcutterName;
            if(text!="") GUI.Box(new Rect(Screen.width/2-220,Screen.height-105,440,65),text);
            if(progress.Stage==BridgeStage.RepairBridge) GUI.Box(new Rect(20,20,280,40),"Planks remaining: "+progress.Planks+" / 3");
        }
        void OnDisable() { Close(); if(marker!=null) marker.Hide(); }
        void OnDestroy() { if(uiRoot!=null) Destroy(uiRoot); }
    }
}
