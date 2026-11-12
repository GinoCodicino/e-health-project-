using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Level02.DeerRescue
{
    public sealed class BacteriaBattleGame : RescueGamePanel
    {
        [Min(1)] public float splinterInterval=2.8f;
        [Range(18,100)] public float splinterSpeed=42;
        [Range(.1f,1)] public float sprayCooldown=.35f;
        CareBattle battle;
        KitTool tool;
        readonly Button[] bugs=new Button[CareBattle.EnemyCount];
        readonly TMP_Text[] hearts=new TMP_Text[CareBattle.EnemyCount];
        readonly List<Fragment> fragments=new List<Fragment>();
        readonly bool[] shielded=new bool[CareBattle.EnemyCount];
        sealed class Fragment {public Button button;public float x,y,phase;public int owner=-1;}
        TMP_Text status,toolLabel;
        Button tweezers,spray;
        float timer,nextSpray,elapsed;
        bool running;
        public void Open(playercontroller who,Action<bool> callback)
        {
            if(IsOpen || who==null)return;running=false;Begin(who,callback,"FirstAidArcade_UI");
            Label("FIRST-AID KIT: MICROBE CHALLENGE",-550,285,1100,64,30);
            Art(RescueArtwork.Picture.Deer,page.transform,0,80,440,220);
            Label("An arcade challenge inside the kit:\nFUCHSIA TWEEZERS remove falling infected splinters.\nDISINFECTANT defeats microbes with two hits.\nA first spray releases a splinter: remove it before the second spray.\nSwitch tools and avoid five misses. The deer is safe during retries.",-510,-115,1020,210,25);
            Button("Start challenge",page.transform,-270,-290,480,48,StartRound);
            Button("Back to the trail",page.transform,270,-290,480,48,()=>Close());
        }
        void StartRound()
        {
            NewPage();fragments.Clear();System.Array.Clear(shielded,0,shielded.Length);battle=new CareBattle();tool=KitTool.Disinfectant;timer=2.8f;nextSpray=0;elapsed=0;running=true;
            Label("FIRST-AID KIT",-550,299,1100,44,30);
            Label("Spray once, tweeze the released splinter, then spray again. Avoid five misses.",-550,252,1100,46,21);
            var arena=MushroomUI.Image("Kit arena",page.transform,new Color(.10f,.23f,.22f));MushroomUI.Place(arena.rectTransform,0,25,1100,380);arena.raycastTarget=false;
            for(int i=0;i<CareBattle.EnemyCount;i++)
            {
                int index=i;float x=-420+(i%3)*420,y=115-(i/3)*110;
                var b=Button("",page.transform,x,y,110,100,()=>SprayBug(index));b.GetComponent<Image>().color=Color.clear;
                Icon(RescueArcadeArt.Shape.Bacterium,b.transform,0,0,100,90,new Color(.65f,.85f,.30f));bugs[i]=b;
                var h=MushroomUI.Text("Health",b.transform,"2",20,Color.white);MushroomUI.Place(h.rectTransform,0,-43,60,28);h.alignment=TextAlignmentOptions.Center;h.raycastTarget=false;hearts[i]=h;
            }
            tweezers=Button("1  Fuchsia tweezers",page.transform,-300,-225,450,50,()=>Select(KitTool.Tweezers));
            Icon(RescueArcadeArt.Shape.Tweezers,tweezers.transform,-175,0,35,37,new Color(1,.25f,.68f));
            spray=Button("2  Disinfectant",page.transform,300,-225,450,50,()=>Select(KitTool.Disinfectant));
            Icon(RescueArcadeArt.Shape.Spray,spray.transform,-170,0,34,37,new Color(.3f,.78f,.89f));
            toolLabel=Label("",-540,-168,1080,35,20);status=Label("",-540,207,1080,35,20);
            Button("Back to the trail",page.transform,0,-300,480,46,()=>Close());Select(tool);Refresh();
        }
        void Select(KitTool selected)
        {
            tool=selected;
            tweezers.GetComponent<Image>().color=selected==KitTool.Tweezers?new Color(.58f,.17f,.42f):new Color(.18f,.32f,.29f);
            spray.GetComponent<Image>().color=selected==KitTool.Disinfectant?new Color(.14f,.46f,.53f):new Color(.18f,.32f,.29f);
            toolLabel.text=selected==KitTool.Tweezers?"TWEEZERS selected - click infected splinters.":"DISINFECTANT selected - click microbes twice.";
        }
        void SprayBug(int id)
        {
            if(!running || Time.unscaledTime<nextSpray)return;
            if(shielded[id] && tool==KitTool.Disinfectant){toolLabel.text="Remove this microbe's splinter with TWEEZERS before spraying again.";return;}
            nextSpray=Time.unscaledTime+sprayCooldown;
            if(battle.Hit(id,tool))
            {
                hearts[id].text=battle.Health(id).ToString();
                if(battle.Health(id)==0)bugs[id].gameObject.SetActive(false);
                else {bugs[id].GetComponentInChildren<RescueArcadeArt>().color=new Color(1,.62f,.25f);shielded[id]=true;SpawnFrom(id,true);}
            }
            Refresh();CheckEnd();
        }
        void Spawn()
        {
            var alive=new List<int>();for(int i=0;i<bugs.Length;i++)if(battle.Health(i)>0)alive.Add(i);
            if(alive.Count==0)return;
            int id=alive[UnityEngine.Random.Range(0,alive.Count)];SpawnFrom(id,false);
        }
        void SpawnFrom(int id,bool unlocks)
        {
            Vector2 p=((RectTransform)bugs[id].transform).anchoredPosition;
            var f=new Fragment{x=p.x,y=p.y-30,phase=UnityEngine.Random.value*6,owner=unlocks?id:-1};
            f.button=Button("",page.transform,f.x,f.y,76,78,()=>Remove(f));f.button.GetComponent<Image>().color=Color.clear;
            Icon(RescueArcadeArt.Shape.Splinter,f.button.transform,0,0,65,72,Color.white);fragments.Add(f);
        }
        void Remove(Fragment f)
        {
            if(!running || !fragments.Contains(f))return;
            if(battle.RemoveSplinter(tool)){if(f.owner>=0)shielded[f.owner]=false;fragments.Remove(f);Destroy(f.button.gameObject);}
            Refresh();CheckEnd();
        }
        void Refresh(){status.text="Microbes: "+battle.Defeated+" / 6     Kit overload: "+battle.Danger+" / 5";}
        void CheckEnd()
        {
            if(!running)return;
            if(battle.Lost)End(false);else if(battle.Won(fragments.Count))End(true);
        }
        void End(bool won)
        {
            running=false;NewPage();fragments.Clear();
            Label(won?"KIT CHALLENGE COMPLETE!":"LET'S TRY AGAIN",-550,275,1100,65,31);
            var deer=Art(RescueArtwork.Picture.Deer,page.transform,0,55,620,310);deer.bandaged=won;deer.SetVerticesDirty();
            Label(won?"The kit is ready. Mia can dress the deer's injured leg.":"Switch tools to handle each target. Your kit is still available.",-520,-150,1040,90,25);
            Button(won?"Help the deer":"Try again",page.transform,-270,-285,480,48,()=>{if(won)Close(true);else StartRound();});
            Button("Back to the trail",page.transform,270,-285,480,48,()=>Close());
        }
        void Update()
        {
            if(!IsOpen)return;
            if(playerinputcontroller.ConsumeEscape()){running=false;Close();return;}
            if(!running)return;
            var k=UnityEngine.InputSystem.Keyboard.current;
            if(k!=null){if(k.digit1Key.wasPressedThisFrame)Select(KitTool.Tweezers);if(k.digit2Key.wasPressedThisFrame)Select(KitTool.Disinfectant);}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);elapsed+=dt;timer-=dt;
            for(int i=0;i<bugs.Length;i++)if(battle.Health(i)>0)
                ((RectTransform)bugs[i].transform).anchoredPosition=new Vector2(-420+(i%3)*420+Mathf.Sin(elapsed*1.1f+i)*24,115-(i/3)*110);
            if(timer<=0){Spawn();timer=Mathf.Max(1,splinterInterval);}
            for(int i=fragments.Count-1;i>=0;i--)
            {
                var f=fragments[i];f.y-=splinterSpeed*dt;
                ((RectTransform)f.button.transform).anchoredPosition=new Vector2(f.x+Mathf.Sin(elapsed+f.phase)*18,f.y);
                if(f.y<-142){if(f.owner>=0)shielded[f.owner]=false;Destroy(f.button.gameObject);fragments.RemoveAt(i);battle.Miss();Refresh();}
            }
            CheckEnd();
        }
    }
}
