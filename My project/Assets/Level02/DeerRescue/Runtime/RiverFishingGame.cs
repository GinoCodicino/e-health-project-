using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Level02.DeerRescue
{
    public sealed class RiverFishingGame : MonoBehaviour
    {
        [Range(3,4)] public int fishGoal=3;
        [Min(15)] public float timeLimitSeconds=45;
        [Min(200)] public float netSpeed=650;
        [Range(60,220)] public float fallSpeed=110;
        public bool IsOpen { get { return root!=null && root.activeSelf; } }
        readonly FishingRound round=new FishingRound();
        readonly List<FallingItem> items=new List<FallingItem>();
        GameObject root,page;
        RectTransform canvas,field,net;
        TMP_Text clock,count,message;
        playercontroller player;
        Action<bool,int> completed;
        float spawnTimer,netX;
        int spawnNumber;
        bool ending;
        static double Now { get { return Time.realtimeSinceStartupAsDouble; } }
        sealed class FallingItem
        { public RectTransform rect; public bool fish; public string caption; public float y,x,sway; }

        public void Open(playercontroller who,Action<bool,int> callback)
        {
            if(IsOpen || who==null) return;
            player=who; completed=callback; player.SetUiMode(this,true);
            if(root==null) { canvas=MushroomUI.Canvas("RiverFishing_UI"); root=canvas.gameObject; }
            root.SetActive(true); Instructions();
        }
        void Page()
        {
            ClearItems();
            if(page!=null) { page.SetActive(false); Destroy(page); }
            Image panel=MushroomUI.Image("Page",canvas,new Color(.045f,.12f,.13f));
            MushroomUI.Place(panel.rectTransform,0,0,1200,680); page=panel.gameObject;
        }
        TMP_Text Label(string text,float x,float y,float w,float h,int size=24)
        {
            TMP_Text label=MushroomUI.Text("Text",page.transform,text,size,Color.white);
            label.rectTransform.pivot=new Vector2(0,.5f); MushroomUI.Place(label.rectTransform,x,y,w,h);
            label.alignment=TextAlignmentOptions.MidlineLeft; label.enableWordWrapping=true;
            return label;
        }
        void Button(string caption,float x,float y,float w,Action action)
        {
            TMP_Text label; var button=MushroomUI.Button("Button",page.transform,caption,new Color(.23f,.43f,.32f),out label);
            label.color=Color.white; MushroomUI.Place((RectTransform)button.transform,x,y,w,50);
            Navigation nav=button.navigation; nav.mode=Navigation.Mode.None; button.navigation=nav;
            button.onClick.AddListener(()=>action());
        }
        RescueArtwork Art(Transform parent,RescueArtwork.Picture type,float x,float y,float w,float h)
        {
            var art=MushroomUI.Rect(type.ToString(),parent).gameObject.AddComponent<RescueArtwork>();
            art.picture=type; art.raycastTarget=false; MushroomUI.Place(art.rectTransform,x,y,w,h); art.SetVerticesDirty(); return art;
        }
        void Instructions()
        {
            ending=true; Page();
            Label("RIVER FISHING",-540,280,1080,50,32);
            Art(page.transform,RescueArtwork.Picture.Fish,0,90,210,130);
            Label("Catch "+fishGoal+" fish in "+timeLimitSeconds.ToString("0")+" seconds.\nMove the net with A / D or the arrow keys.\nAvoid rocks, branches and litter: they briefly block your net.\nThe timer starts when you press Start.",-500,-80,1000,190,25);
            Button("Start",-270,-280,480,StartRound); Button("Back",270,-280,480,()=>Close(false));
        }
        void StartRound()
        {
            fishGoal=Mathf.Clamp(fishGoal,3,4); timeLimitSeconds=Mathf.Max(15,timeLimitSeconds);
            if(!round.Begin(fishGoal,Now,timeLimitSeconds)) return;
            Page(); ending=false; spawnTimer=.4f; spawnNumber=0; netX=0;
            Label("CATCH THE FISH",-540,295,1080,44,32);
            count=Label("",-540,242,540,36); clock=Label("",120,242,420,36);
            field=MushroomUI.Rect("River play area",page.transform); MushroomUI.Place(field,0,4,1100,410);
            field.gameObject.AddComponent<RectMask2D>();
            Art(field,RescueArtwork.Picture.River,0,0,1100,410);
            net=Art(field,RescueArtwork.Picture.Net,0,-166,170,72).rectTransform;
            message=Label("Move: A / D or arrows. Catch fish only.",-540,-238,1080,42,21);
            Button("Leave fishing",0,-300,460,()=>Close(false));
            Refresh();
        }
        void Update()
        {
            if(!IsOpen) return;
            if(playerinputcontroller.ConsumeEscape()) { Close(false); return; }
            if(ending) return;
            round.Tick(Now);
            if(round.Result!=FishingResult.Running) { EndRound(); return; }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f),direction=0;
            Keyboard keys=Keyboard.current;
            if(keys!=null)
            {
                if(keys.aKey.isPressed || keys.leftArrowKey.isPressed) direction--;
                if(keys.dKey.isPressed || keys.rightArrowKey.isPressed) direction++;
            }
            if(!round.IsBlocked(Now)) netX=Mathf.Clamp(netX+direction*netSpeed*dt,-450,450);
            net.anchoredPosition=new Vector2(netX,-166);
            net.GetComponent<RescueArtwork>().color=round.IsBlocked(Now) ? new Color(.64f,.67f,.62f) : Color.white;
            spawnTimer-=dt;
            if(spawnTimer<=0) { Spawn(); spawnTimer=1.05f; }
            for(int i=items.Count-1;i>=0;i--)
            {
                FallingItem item=items[i]; float previous=item.y;
                item.y-=Mathf.Clamp(fallSpeed,60,220)*dt;
                float x=item.x+Mathf.Sin(Time.unscaledTime*1.6f+item.sway)*10;
                item.rect.anchoredPosition=new Vector2(x,item.y);
                // Test di attraversamento: non perde oggetti tra due frame.
                if(previous>-145 && item.y<=-145 && Mathf.Abs(x-netX)<112)
                {
                    if(round.Catch(item.fish,Now))
                        message.text=item.fish ? "A fish! "+round.Caught+" / "+round.Goal : item.caption+" - the net is briefly blocked.";
                    Remove(i);
                    if(round.Result==FishingResult.Won) { EndRound(); return; }
                }
                else if(item.y<-240) Remove(i);
            }
            Refresh();
        }
        void Spawn()
        {
            // Un pesce ogni due oggetti: abbastanza occasioni per raggiungere l'obiettivo.
            bool fish=spawnNumber%2==0;
            var kinds=new[]{RescueArtwork.Picture.Stone,RescueArtwork.Picture.Branch,RescueArtwork.Picture.Boot,RescueArtwork.Picture.Bottle};
            RescueArtwork.Picture kind=fish ? RescueArtwork.Picture.Fish : kinds[(spawnNumber/2)%kinds.Length];
            float x=UnityEngine.Random.Range(-430f,430f);
            RescueArtwork art=Art(field,kind,x,230,fish ? 90 : 72,fish ? 62 : 74);
            art.transform.SetAsLastSibling(); net.SetAsLastSibling();
            items.Add(new FallingItem{rect=art.rectTransform,fish=fish,x=x,y=230,sway=spawnNumber,
                caption=kind==RescueArtwork.Picture.Boot || kind==RescueArtwork.Picture.Bottle ? "Litter caught" : "An obstacle caught"});
            spawnNumber++;
        }
        void Refresh()
        {
            count.text="Fish: "+round.Caught+" / "+round.Goal;
            clock.text="Time: "+Math.Ceiling(round.Remaining(Now))+" s";
            clock.color=round.Remaining(Now)<=10 ? new Color(1,.72f,.45f) : Color.white;
        }
        void EndRound()
        {
            ending=true; bool won=round.Result==FishingResult.Won; Page();
            Label(won ? "FISHING COMPLETE!" : "TIME IS UP",-540,275,1080,60,34);
            Art(page.transform,RescueArtwork.Picture.Fish,0,75,260,170);
            Label(won ? "You caught "+round.Caught+" fish. Take them to the woodcutter." : "You caught "+round.Caught+" fish. You can try again.",-500,-100,1000,100,26);
            Button(won ? "Keep the fish" : "Try again",-270,-280,480,()=>{if(won)Close(true);else StartRound();});
            Button("Back",270,-280,480,()=>Close(false));
        }
        void Remove(int i) { if(items[i].rect!=null) Destroy(items[i].rect.gameObject); items.RemoveAt(i); }
        void ClearItems() { for(int i=items.Count-1;i>=0;i--) Remove(i); }
        public void Close(bool success)
        {
            if(!IsOpen) return;
            bool won=success && round.Result==FishingResult.Won;
            round.Cancel(); root.SetActive(false); ending=true; ClearItems();
            if(player!=null) player.SetUiMode(this,false);
            Action<bool,int> callback=completed; completed=null; callback?.Invoke(won,won ? round.Caught : 0);
        }
        void OnDisable() { Close(false); }
        void OnDestroy() { if(root!=null) Destroy(root); }
    }
}
