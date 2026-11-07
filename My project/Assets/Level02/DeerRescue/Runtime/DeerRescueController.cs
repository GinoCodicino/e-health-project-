using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Level02.BridgeMission;

namespace Level02.DeerRescue
{
    // Esegue le nuove interazioni prima dei vecchi NPC; SetUiMode impedisce aperture doppie.
    [DefaultExecutionOrder(-150)]
    public sealed class DeerRescueController : MonoBehaviour
    {
        [Header("Missioni precedenti")]
        public playercontroller player;
        public ForestQuest mushroomQuest;
        public BridgeQuestController bridgeQuest;
        public BridgeCondition bridgeCondition;
        [Header("Punti del terzo gioco")]
        public Transform encounterPoint;
        public Transform ranger;
        public Transform fishingPoint;
        public Transform deerPoint;
        public DeerRescueView deerView;
        public RiverFishingGame fishingGame;
        public string rangerName="Mia";
        public float interactionDistance=3;
        public float encounterRadius=4;
        public float rangerSpeed=3;
        [Range(0,.1f)] public float typingSpeed=.025f;
        public RescueStage CurrentStage { get { return progress.Stage; } }
        public bool IsOpen { get { return uiRoot!=null && uiRoot.activeSelf; } }
        readonly RescueProgress progress=new RescueProgress();
        BridgeQuestMarker marker;
        Camera view;
        bool encounterStarted,encounterArrived;
        bool ownsBridgeNpc,ownsPeterNpc;
        GameObject uiRoot,page;
        RectTransform canvas,dressingTarget;
        TMP_Text speech,feedback,branchCounter;
        RescueArtwork deerPicture;
        readonly GameObject[] branches=new GameObject[3];
        readonly RescueArtwork[] branchArt=new RescueArtwork[3];
        Button hitTarget;
        int selectedBranch=-1,lineIndex,closedFrame=-1;
        string[] lines;
        Action afterLines;
        Coroutine typing;
        bool isTyping;

        void Awake()
        {
            if(player==null) player=FindFirstObjectByType<playercontroller>();
            if(bridgeQuest==null) bridgeQuest=FindFirstObjectByType<BridgeQuestController>();
            if(bridgeCondition==null && bridgeQuest!=null) bridgeCondition=bridgeQuest.bridge;
            if(mushroomQuest==null && bridgeQuest!=null) mushroomQuest=bridgeQuest.mushroomQuest;
            if(mushroomQuest==null) mushroomQuest=FindFirstObjectByType<ForestQuest>();
            if(fishingGame==null) fishingGame=GetComponent<RiverFishingGame>();
            if(fishingGame==null) fishingGame=gameObject.AddComponent<RiverFishingGame>();
            marker=gameObject.AddComponent<BridgeQuestMarker>();
            if(player!=null) view=player.GetComponentInChildren<Camera>(true);
            if(view==null) view=Camera.main;
        }
        bool PreviousComplete { get { return bridgeCondition!=null && bridgeCondition.IsRepaired && bridgeQuest!=null && bridgeQuest.CurrentStage==BridgeStage.Complete; } }
        float Distance(Transform t) { return t==null || player==null ? float.PositiveInfinity : Vector3.Distance(t.position,player.transform.position); }
        bool Near(Transform t) { return Distance(t)<=interactionDistance; }
        void Update()
        {
            if(player==null) return;
            SyncNpcOwnership();
            if(IsOpen)
            {
                marker.Hide();
                if(playerinputcontroller.ConsumeEscape()) { Close(); return; }
                if(isTyping && Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame) ShowAll();
                return;
            }
            if(player.IsGameplayBlocked || fishingGame.IsOpen || closedFrame==Time.frameCount) { marker.Hide(); return; }
            if(progress.Stage==RescueStage.WaitingForCrossing)
            {
                marker.Hide();
                if(!PreviousComplete) return;
                if(!encounterStarted && Distance(encounterPoint)<=encounterRadius) encounterStarted=true;
                if(encounterStarted && !encounterArrived)
                {
                    if(ranger==null) { encounterArrived=true; Intro(); return; }
                    Vector3 target=player.transform.position; target.y=ranger.position.y;
                    Vector3 delta=target-ranger.position;
                    if(delta.sqrMagnitude>1.9f*1.9f)
                    {
                        ranger.position=Vector3.MoveTowards(ranger.position,target,Mathf.Max(.5f,rangerSpeed)*Time.deltaTime);
                        if(delta.sqrMagnitude>.01f) ranger.rotation=Quaternion.Slerp(ranger.rotation,Quaternion.LookRotation(delta),Time.deltaTime*8);
                    }
                    else { encounterArrived=true; Intro(); return; }
                }
                if(encounterArrived && Near(ranger) && PressE()) Intro();
                return;
            }
            UpdateMarker();
            if(!PressE()) return;
            switch(progress.Stage)
            {
                case RescueStage.FindWoodcutter:
                case RescueStage.DeliverFish:
                    if(bridgeQuest!=null && Near(bridgeQuest.woodcutter)) TalkWoodcutter(); break;
                case RescueStage.CatchFish:
                    if(Near(fishingPoint)) fishingGame.Open(player,FishingFinished); break;
                case RescueStage.FreeDeer:
                    if(Near(deerPoint)) OpenRelease(); break;
                case RescueStage.GetBandages:
                    if(mushroomQuest!=null && mushroomQuest.CurrentStage==ForestQuest.Stage.Complete && Near(mushroomQuest.transform)) TalkPeter(); break;
                case RescueStage.TreatDeer:
                    if(Near(deerPoint)) OpenTreatment(); break;
                case RescueStage.Complete:
                    if(Near(deerPoint)) Speak(rangerName,new[]{"The deer can rest here now. Thank you for helping!"},Close); break;
            }
        }
        // Prende temporaneamente in carico le interazioni, mantenendo intatti gli script precedenti.
        void SyncNpcOwnership()
        {
            bool active=progress.Stage!=RescueStage.WaitingForCrossing && progress.Stage!=RescueStage.Complete;
            if(active && bridgeQuest!=null && bridgeQuest.enabled)
            { ownsBridgeNpc=true; bridgeQuest.enabled=false; }
            else if(!active && ownsBridgeNpc && bridgeQuest!=null)
            { ownsBridgeNpc=false; bridgeQuest.enabled=true; }
            bool needsPeter=progress.Stage==RescueStage.GetBandages && mushroomQuest!=null && mushroomQuest.CurrentStage==ForestQuest.Stage.Complete;
            if(needsPeter && mushroomQuest.enabled) { ownsPeterNpc=true; mushroomQuest.enabled=false; }
            else if(!needsPeter && ownsPeterNpc && mushroomQuest!=null) { ownsPeterNpc=false; mushroomQuest.enabled=true; }
        }
        void RestoreNpcOwnership()
        {
            if(ownsBridgeNpc && bridgeQuest!=null) bridgeQuest.enabled=true;
            if(ownsPeterNpc && mushroomQuest!=null) mushroomQuest.enabled=true;
            ownsBridgeNpc=false;ownsPeterNpc=false;
        }
        bool PressE() { return Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame; }
        void UpdateMarker()
        {
            Transform target=null; string text="";
            switch(progress.Stage)
            {
                case RescueStage.FindWoodcutter: target=bridgeQuest==null ? null : bridgeQuest.woodcutter; text="Ask Rowan for an axe"; break;
                case RescueStage.CatchFish: target=fishingPoint; text="Catch fish for Rowan"; break;
                case RescueStage.DeliverFish: target=bridgeQuest==null ? null : bridgeQuest.woodcutter; text="Exchange fish for the axe"; break;
                case RescueStage.FreeDeer: target=deerPoint; text="Free the trapped deer"; break;
                case RescueStage.GetBandages: target=mushroomQuest==null ? null : mushroomQuest.transform; text="Ask Peter for bandages"; break;
                case RescueStage.TreatDeer: target=deerPoint; text="Help the injured deer"; break;
            }
            marker.Show(target,text,view);
        }
        void Page(bool full)
        {
            if(uiRoot==null) { canvas=MushroomUI.Canvas("DeerRescue_UI"); uiRoot=canvas.gameObject; uiRoot.SetActive(false); }
            if(!uiRoot.activeSelf) { player.SetUiMode(this,true); uiRoot.SetActive(true); }
            if(typing!=null) StopCoroutine(typing); typing=null; isTyping=false;
            if(page!=null) { page.SetActive(false); Destroy(page); }
            Image image=MushroomUI.Image("Page",canvas,new Color(.045f,.10f,.07f,full ? 1 : .82f)); page=image.gameObject;
            if(full) MushroomUI.Place(image.rectTransform,0,0,1200,680);
            else
            {
                image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,0);
                image.rectTransform.pivot=new Vector2(.5f,0); image.rectTransform.anchoredPosition=new Vector2(0,20);
                image.rectTransform.sizeDelta=new Vector2(1200,300);
            }
        }
        TMP_Text Label(string text,float x,float y,float w,float h,int size=24)
        {
            var t=MushroomUI.Text("Text",page.transform,text,size,Color.white);
            t.rectTransform.pivot=new Vector2(0,.5f); MushroomUI.Place(t.rectTransform,x,y,w,h);
            t.alignment=TextAlignmentOptions.MidlineLeft; t.enableWordWrapping=true; t.overflowMode=TextOverflowModes.Truncate; return t;
        }
        Button Button(string caption,float x,float y,float w,float h,Action action)
        {
            TMP_Text t; var b=MushroomUI.Button("Button",page.transform,caption,new Color(.23f,.40f,.28f),out t);
            t.color=Color.white; t.fontSize=21; MushroomUI.Place((RectTransform)b.transform,x,y,w,h);
            Navigation n=b.navigation;n.mode=Navigation.Mode.None;b.navigation=n;
            b.onClick.AddListener(()=>{if(!isTyping)action();});return b;
        }
        RescueArtwork Art(Transform parent,RescueArtwork.Picture type,float x,float y,float w,float h)
        {
            var a=MushroomUI.Rect(type.ToString(),parent).gameObject.AddComponent<RescueArtwork>();
            a.picture=type;a.raycastTarget=false;MushroomUI.Place(a.rectTransform,x,y,w,h);a.SetVerticesDirty();return a;
        }
        void Speak(string name,string[] text,Action finish)
        {
            Page(false); lines=text;lineIndex=0;afterLines=finish;
            Label(name,-550,116,1000,32,22);speech=Label("",-550,25,1100,116,25);
            Button("Close",-390,-104,300,46,Close);
            Button next=Button("Next / Show all",270,-104,520,46,Next);
            next.onClick.RemoveAllListeners();next.onClick.AddListener(Next);TypeLine();
        }
        void TypeLine()
        { speech.text=lines[lineIndex];speech.maxVisibleCharacters=0;speech.ForceMeshUpdate();typing=StartCoroutine(Type(speech.textInfo.characterCount)); }
        IEnumerator Type(int length)
        {
            isTyping=true;
            for(int i=0;i<=length;i++) {speech.maxVisibleCharacters=i;if(typingSpeed>0)yield return new WaitForSecondsRealtime(typingSpeed);}
            isTyping=false;typing=null;
        }
        void ShowAll() {if(typing!=null)StopCoroutine(typing);typing=null;isTyping=false;if(speech!=null)speech.maxVisibleCharacters=int.MaxValue;}
        void Next() {if(isTyping){ShowAll();return;}if(++lineIndex<lines.Length)TypeLine();else afterLines?.Invoke();}
        void Intro()
        {
            Speak(rangerName,new[]{"Please, can you help? A deer is trapped under fallen branches further along the trail!",
                "The heavy rain brought down the old pine. The deer cannot get out.",
                "You: The woodcutter may be able to lend us an axe.","Could you ask him? I will stay with the deer."},()=>
            {
                Page(false);Label(rangerName,-550,116,1000,32,22);Label("Will you help the trapped deer?",-550,30,1100,90);
                Button("I will ask the woodcutter.",-280,-104,520,46,()=>{progress.AcceptRescue();Close();});
                Button("I will come back.",280,-104,520,46,Close);
            });
        }
        void TalkWoodcutter()
        {
            string name=bridgeQuest==null ? "Rowan" : bridgeQuest.woodcutterName;
            if(progress.Stage==RescueStage.FindWoodcutter)
                Speak(name,new[]{"A deer is trapped? I can lend you my axe.","Could you bring me "+fishingGame.fishGoal+" fish from the river in exchange? There is a fishing spot by the bench.",
                    "You: I will catch them and come straight back."},()=>{progress.AcceptFishing();Close();});
            else
                Speak(name,new[]{"Thank you for the fish. Here is the axe.","Remove the loose branches carefully. Keep the blade away from the animal."},()=>{progress.ExchangeFish();Close();});
        }
        void FishingFinished(bool won,int fish)
        {
            if(!isActiveAndEnabled) return;
            closedFrame=Time.frameCount;
            if(won) progress.FinishFishing(fish);
        }
        void OpenRelease()
        {
            Page(true); selectedBranch=-1;
            Label("FREE THE DEER",-540,290,1080,46,32);
            Label("Select the top branch, then tap its marked end three times.",-540,235,1080,42,23);
            Image ground=MushroomUI.Image("Ground",page.transform,new Color(.23f,.31f,.17f));
            MushroomUI.Place(ground.rectTransform,0,20,1100,360);ground.raycastTarget=false;
            Art(page.transform,RescueArtwork.Picture.Deer,-30,-15,700,330);
            for(int i=2;i>=0;i--)
            {
                if(i<progress.ClearedBranches) {branches[i]=null;continue;}
                int index=i;float y=100-i*65;
                Button b=Button("",0,y,900,78,()=>SelectBranch(index));b.GetComponent<Image>().color=new Color(1,1,1,0);
                branchArt[i]=Art(b.transform,RescueArtwork.Picture.Branch,0,0,900,78);
                branchArt[i].damage=i==progress.ClearedBranches ? progress.HitsOnBranch/3f : 0;
                branchArt[i].SetVerticesDirty();branches[i]=b.gameObject;
            }
            branchCounter=Label("Branches removed: "+progress.ClearedBranches+" / 3",-540,-194,1080,35,23);
            feedback=Label("Start with the branch on top.",-540,-242,1080,40,22);
            hitTarget=Button("CUT",-430,100,110,62,HitBranch);hitTarget.gameObject.SetActive(false);
            Button("Back to the trail",0,-300,480,48,Close);
        }
        void SelectBranch(int index)
        {
            if(index!=progress.ClearedBranches) {feedback.text="Remove the branch above first.";return;}
            selectedBranch=index;hitTarget.gameObject.SetActive(true);
            ((RectTransform)hitTarget.transform).anchoredPosition=new Vector2(-430,100-index*65);
            feedback.text="Tap CUT at the safe end: "+progress.HitsOnBranch+" / 3";
        }
        void HitBranch()
        {
            if(!progress.HitBranch(selectedBranch)) return;
            if(progress.Stage==RescueStage.GetBandages)
            {
                if(deerView!=null)deerView.Free();
                Speak(rangerName,new[]{"The deer is free! Thank you.","Its leg is injured. Peter at the red house may have some bandages.","Please ask him. I will stay here and keep the deer calm."},Close);return;
            }
            if(selectedBranch<progress.ClearedBranches) {if(deerView!=null)deerView.SetClearedBranches(progress.ClearedBranches);OpenRelease();return;}
            branchArt[selectedBranch].damage=progress.HitsOnBranch/3f;branchArt[selectedBranch].SetVerticesDirty();
            feedback.text="Tap CUT at the safe end: "+progress.HitsOnBranch+" / 3";
        }
        void TalkPeter()
        {
            string name=mushroomQuest==null ? "Peter" : mushroomQuest.speakerName;
            bool ready=mushroomQuest!=null && mushroomQuest.CurrentStage==ForestQuest.Stage.Complete;
            if(!ready) {Speak(name,new[]{"Please finish helping with the mushrooms and the garden first."},Close);return;}
            Speak(name,new[]{"A deer needs help? Of course. Here are some clean bandages.","You helped me and my garden. I am glad I can help you now.","Take them back to the ranger."},()=>{progress.ReceiveBandages(true);Close();});
        }
        void OpenTreatment()
        {
            Page(true);
            Label("HELP THE INJURED DEER",-540,290,1080,46,32);
            Label(progress.DressingPlaced ? "Wrap the bandage by selecting the numbered points in order." : "Drag the dressing onto the highlighted part of the leg.",-540,235,1080,42,23);
            Image ground=MushroomUI.Image("Ground",page.transform,new Color(.23f,.31f,.17f));
            MushroomUI.Place(ground.rectTransform,0,10,1100,350);ground.raycastTarget=false;
            deerPicture=Art(page.transform,RescueArtwork.Picture.Deer,-30,0,700,330);
            deerPicture.bandaged=progress.DressingPlaced;deerPicture.SetVerticesDirty();
            Image target=MushroomUI.Image("Dressing target",page.transform,new Color(.83f,.89f,.52f,.18f));
            dressingTarget=target.rectTransform;MushroomUI.Place(dressingTarget,135,-82,100,72);target.raycastTarget=false;
            target.gameObject.AddComponent<Outline>().effectColor=new Color(.87f,.91f,.59f);
            if(!progress.DressingPlaced)
            {
                Image item=MushroomUI.Image("Dressing to drag",page.transform,new Color(1,1,1,0));
                MushroomUI.Place(item.rectTransform,425,-30,140,90);
                Art(item.transform,RescueArtwork.Picture.Dressing,0,0,140,90);
                item.gameObject.AddComponent<RescueDressingDrag>().owner=this;
            }
            else
            {
                Vector2[] points={new Vector2(90,-54),new Vector2(170,-81),new Vector2(110,-110)};
                for(int i=0;i<3;i++)
                {
                    int step=i;
                    Button b=Button(i<progress.WrapSteps ? "OK" : (i+1).ToString(),points[i].x,points[i].y,52,44,()=>Wrap(step));
                    b.interactable=i>=progress.WrapSteps;
                }
            }
            feedback=Label("Keep the deer calm and complete the dressing.",-540,-235,1080,45,22);
            Button("Back to the trail",0,-300,480,48,Close);
        }
        public bool DropDressing(Vector2 position,Camera eventCamera)
        {
            if(!IsOpen || progress.Stage!=RescueStage.TreatDeer || !RectTransformUtility.RectangleContainsScreenPoint(dressingTarget,position,eventCamera))
            {if(feedback!=null)feedback.text="Place the dressing inside the highlighted area.";return false;}
            if(!progress.PlaceDressing())return false;
            OpenTreatment();return true;
        }
        void Wrap(int step)
        {
            if(!progress.Wrap(step)) {feedback.text="Follow the numbered points in order.";return;}
            if(progress.Stage==RescueStage.Complete)
            {
                if(deerView!=null)deerView.Heal();
                Speak(rangerName,new[]{"The deer is free and its leg is supported. It can rest here with me.","You brought everyone together to help. Your grandfather would have loved that."},Close);
            }
            else OpenTreatment();
        }
        public void Close()
        {
            if(typing!=null)StopCoroutine(typing);typing=null;isTyping=false;
            if(uiRoot!=null)uiRoot.SetActive(false);
            if(player!=null)player.SetUiMode(this,false);
            closedFrame=Time.frameCount;
        }
        void OnGUI()
        {
            if(IsOpen || player==null || player.IsGameplayBlocked || !encounterStarted) return;
            string text="";
            if(progress.Stage==RescueStage.WaitingForCrossing && encounterArrived && Near(ranger))text="E - Talk to "+rangerName;
            else if((progress.Stage==RescueStage.FindWoodcutter || progress.Stage==RescueStage.DeliverFish) && bridgeQuest!=null && Near(bridgeQuest.woodcutter))text="E - Ask about the rescue";
            else if(progress.Stage==RescueStage.CatchFish && Near(fishingPoint))text="E - Start fishing";
            else if(progress.Stage==RescueStage.FreeDeer && Near(deerPoint))text="E - Free the deer";
            else if(progress.Stage==RescueStage.GetBandages && mushroomQuest!=null && Near(mushroomQuest.transform))text="E - Ask for bandages";
            else if(progress.Stage==RescueStage.TreatDeer && Near(deerPoint))text="E - Help the injured deer";
            if(text!="")GUI.Box(new Rect(Screen.width/2-220,Screen.height-95,440,50),text);
            string inventory=progress.HasAxe ? "Axe borrowed" : "";
            if(progress.Fish>0)inventory="Fish: "+progress.Fish;
            if(progress.HasBandages)inventory+=" | Bandages";
            if(inventory!="")GUI.Box(new Rect(20,20,330,38),inventory);
        }
        void OnDisable()
        {
            Close();if(marker!=null)marker.Hide();
            if(fishingGame!=null && fishingGame.IsOpen)fishingGame.Close(false);
            RestoreNpcOwnership();
        }
        void OnDestroy() {if(uiRoot!=null)Destroy(uiRoot);}
    }
}
