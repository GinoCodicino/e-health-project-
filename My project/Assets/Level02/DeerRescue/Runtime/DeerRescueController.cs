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
        public ScrewRescueGame releaseGame;
        public BacteriaBattleGame careGame;
        [Tooltip("Trascina qui SOLO il modello dell accetta di Rowan, se separato.")]
        public GameObject woodcutterAxe;
        public string rangerName="Mia";
        public float interactionDistance=3;
        public float encounterRadius=4;
        public float rangerSpeed=3;
        [Range(0,.1f)] public float typingSpeed=.025f;
        public RescueStage CurrentStage { get { return progress.Stage; } }
        public bool IsOpen { get { return uiRoot!=null && uiRoot.activeSelf; } }
        readonly RescueProgress progress=new RescueProgress();
        QuestEquipmentHud equipment;
        BridgeQuestMarker marker;
        Camera view;
        bool encounterStarted,encounterArrived;
        bool ownsBridgeNpc,ownsPeterNpc;
        GameObject uiRoot,page;
        RectTransform canvas;
        TMP_Text speech;
        int lineIndex,closedFrame=-1;
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
            if(releaseGame==null)releaseGame=GetComponent<ScrewRescueGame>();
            if(releaseGame==null)releaseGame=gameObject.AddComponent<ScrewRescueGame>();
            if(careGame==null)careGame=GetComponent<BacteriaBattleGame>();
            if(careGame==null)careGame=gameObject.AddComponent<BacteriaBattleGame>();
            equipment=gameObject.AddComponent<QuestEquipmentHud>();
            if(bridgeQuest!=null)bridgeQuest.showLegacyInventory=false;
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
            progress.GrantPeterGifts(mushroomQuest!=null && mushroomQuest.HasFishingNet && mushroomQuest.HasMedicalKit);
            SyncNpcOwnership();
            if(bridgeQuest!=null)bridgeQuest.showLegacyInventory=false;
            equipment.Show(progress.HasAxe,progress.HasMedicalKit,progress.HasNet,progress.Fish,
                bridgeQuest==null?0:bridgeQuest.RemainingPlanks,!IsOpen && !player.IsGameplayBlocked);
            if(IsOpen)
            {
                marker.Hide();
                if(playerinputcontroller.ConsumeEscape()) { Close(); return; }
                if(isTyping && Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame) ShowAll();
                return;
            }
            if(player.IsGameplayBlocked || fishingGame.IsOpen || releaseGame.IsOpen || careGame.IsOpen || closedFrame==Time.frameCount) { marker.Hide(); return; }
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
                    if(Near(fishingPoint) && progress.HasNet) fishingGame.Open(player,FishingFinished); break;
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
                "The heavy rain brought down the old pine and a wooden fence. The deer cannot get out.",
                "You: The woodcutter may be able to lend us an axe.","You already have Peter's first-aid kit. Ask Rowan for tools; I will stay with the deer."},()=>
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
                Speak(name,new[]{"Thank you for the fish. Here is the axe.","Use it on the loose timber. Follow the coloured cut marks. Remove the loose timber layer by layer, away from the deer."},()=>{if(progress.ExchangeFish() && woodcutterAxe!=null)woodcutterAxe.SetActive(false);Close();});
        }
        void FishingFinished(bool won,int fish)
        {
            if(!isActiveAndEnabled) return;
            closedFrame=Time.frameCount;
            if(won) progress.FinishFishing(fish,fishingGame.fishGoal);
        }
        void OpenRelease()
        {
            releaseGame.Open(player,won=>
            {
                closedFrame=Time.frameCount;
                if(!won || !progress.CompleteRelease())return;
                if(deerView!=null)deerView.Free();
                Speak(rangerName,new[]{"The deer is free! Its leg is hurt, but you already have Peter's kit.",
                    "Let's open the kit here. There is no need to go back to the red house."},OpenTreatment);
            });
        }
        void TalkPeter() { } // Compatibilita con il vecchio stato, non usato dalla nuova sequenza.
        void OpenTreatment()
        {
            Close();
            careGame.Open(player,won=>
            {
                closedFrame=Time.frameCount;
                if(!won || !progress.CompleteCare())return;
                if(deerView!=null)deerView.Heal();
                Speak(rangerName,new[]{"The deer is free and its injured leg is dressed. I will stay here while it rests.",
                    "You brought everyone together to help. Your grandfather would have loved that."},Close);
            });
        }
        // Il vecchio componente di trascinamento puo restare nel progetto senza riferimenti mancanti.
        public bool DropDressing(Vector2 position,Camera eventCamera) { return false; }
        public void Close()
        {
            if(typing!=null)StopCoroutine(typing);typing=null;isTyping=false;
            if(uiRoot!=null)uiRoot.SetActive(false);
            if(player!=null)player.SetUiMode(this,false);
            closedFrame=Time.frameCount;
        }
        void OnGUI()
        {
            if(IsOpen || player==null || player.IsGameplayBlocked) return;
            string text="";
            if(progress.Stage==RescueStage.WaitingForCrossing && encounterArrived && Near(ranger))text="E - Talk to "+rangerName;
            else if((progress.Stage==RescueStage.FindWoodcutter || progress.Stage==RescueStage.DeliverFish) && bridgeQuest!=null && Near(bridgeQuest.woodcutter))text="E - Ask about the rescue";
            else if(progress.Stage==RescueStage.CatchFish && Near(fishingPoint))text="E - Start fishing";
            else if(progress.Stage==RescueStage.FreeDeer && Near(deerPoint))text="E - Free the deer";
            else if(progress.Stage==RescueStage.GetBandages && mushroomQuest!=null && Near(mushroomQuest.transform))text="E - Ask for bandages";
            else if(progress.Stage==RescueStage.TreatDeer && Near(deerPoint))text="E - Help the injured deer";
            if(text!="")GUI.Box(new Rect(Screen.width/2-220,Screen.height-95,440,50),text);

        }
        void OnDisable()
        {
            Close();if(marker!=null)marker.Hide();if(equipment!=null)equipment.Hide();
            if(bridgeQuest!=null)bridgeQuest.showLegacyInventory=true;
            if(fishingGame!=null && fishingGame.IsOpen)fishingGame.Close(false);
            if(releaseGame!=null && releaseGame.IsOpen)releaseGame.Close();
            if(careGame!=null && careGame.IsOpen)careGame.Close();
            RestoreNpcOwnership();
        }
        void OnDestroy() {if(uiRoot!=null)Destroy(uiRoot);}
    }
}
