using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Level02.DeerRescue
{
    // Il nome rimane uguale per preservare i collegamenti della scena. Ora si taglia con l'ascia.
    public sealed class ScrewRescueGame : RescueGamePanel
    {
        [Range(30,180)] public float timeLimitSeconds=60;
        float deadline;
        bool roundRunning;
        TMP_Text clock;
        static Sprite timberSprite;
        ScrewPuzzle puzzle;
        GameObject[] boards;
        Button[] marks;
        readonly RescueArcadeArt[] stored=new RescueArcadeArt[3];
        readonly Image[] collection=new Image[2];
        Image boxCard,nextSwatch,progressFill;
        TMP_Text status,boxTitle,nextTitle,progressLabel;
        RectTransform axe,travellingChip;
        bool animating,finished;
        static readonly Color Ink=new Color(.92f,.95f,.88f);
        static readonly Color[] Palette={new Color(.91f,.30f,.39f),new Color(.19f,.61f,.86f),new Color(.96f,.66f,.13f)};
        static readonly string[] Names={"CORAL","BLUE","GOLD"};
        public void Open(playercontroller who,Action<bool> callback)
        {if(IsOpen || who==null)return;Begin(who,callback,"ScrewRescue_UI");Instructions();}
        TMP_Text Text(string caption,float x,float y,float w,float h,int size=22)
        {var t=Label(caption,x,y,w,h,size);t.color=Ink;return t;}
        Image Card(string name,float x,float y,float w,float h,Color tint)
        {
            var card=MushroomUI.Image(name,page.transform,tint);MushroomUI.Place(card.rectTransform,x,y,w,h);card.raycastTarget=false;card.sprite=RoundedPanelSprite();card.type=Image.Type.Sliced;
            var shadow=card.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.20f,.19f,.13f);shadow.effectDistance=new Vector2(0,-5);return card;
        }
        void Build()
        {
            StopAllCoroutines();NewPage();animating=false;finished=false;roundRunning=true;deadline=Time.unscaledTime+Mathf.Clamp(timeLimitSeconds,30,180);
            page.GetComponent<Image>().color=new Color(.035f,.095f,.09f);
            Text("A CLEAR PATH FOR THE DEER",-550,299,1100,44,31);
            Text("Clear the logs. Three unsafe cuts or a fourth waiting chip ends the attempt.",-550,248,1100,46,21);
            clock=Text("",180,299,370,42,22);
            var ground=Card("Forest clearing",-170,5,760,398,new Color(.13f,.23f,.17f));
            // Ombra e piccole pietre incorniciano l'illustrazione, lasciando liberi i punti di taglio.
            Art(RescueArtwork.Picture.Stone,ground.transform,-315,-135,55,42);
            Art(RescueArtwork.Picture.Stone,ground.transform,310,-155,67,45);
            Art(RescueArtwork.Picture.Deer,ground.transform,15,-35,620,340);
            boards=new GameObject[puzzle.BoardCount];marks=new Button[puzzle.TotalCuts];
            var layout=new System.Random(puzzle.Seed);
            for(int i=0;i<puzzle.BoardCount;i++)
            {
                var board=MushroomUI.Rect("Timber layer "+i,page.transform);boards[i]=board.gameObject;
                float x=(puzzle.LayoutColumn(i)==0?-340:-3)+layout.Next(-9,10);
                float y=-35+(i/2)*45+layout.Next(-10,11);
                float width=layout.Next(316,347),angle=layout.Next(-22,23);
                // Misure limitate: tronchi e bersagli restano dentro la schermata e abbastanza grandi.
                MushroomUI.Place(board,x,y,width,70);board.localRotation=Quaternion.Euler(0,0,angle);
                if(timberSprite==null)
                {
                    var texture=Resources.Load<Texture2D>("DeerRescueArt/Timber");
                    if(texture!=null)timberSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                }
                if(timberSprite!=null)
                {
                    var wood=MushroomUI.Image("Textured timber",board,Color.white);wood.sprite=timberSprite;wood.raycastTarget=false;
                    MushroomUI.Place(wood.rectTransform,0,0,width,100);
                    wood.color=Color.Lerp(Color.white,new Color(.72f,.80f,.69f),layout.Next(0,13)/100f);
                }
                else Icon(RescueArcadeArt.Shape.Plank,board,0,0,width,70,Color.white);
                for(int j=0;j<2;j++)
                {
                    int id=i*2+j;var mark=Button("",board,j==0?-width*.37f:width*.37f,4,56,56,()=>Chop(id));mark.GetComponent<Image>().color=Color.clear;
                    Icon(RescueArcadeArt.Shape.CutMark,mark.transform,0,0,56,56,Palette[puzzle.Colour(id)]);marks[id]=mark;
                }
            }
            boxCard=Card("Active colour box",405,134,272,144,new Color(.10f,.20f,.18f));
            boxTitle=Text("",285,178,240,30,22);
            for(int i=0;i<2;i++)
            {
                collection[i]=MushroomUI.Image("Collection hole",page.transform,Color.white);
                MushroomUI.Place(collection[i].rectTransform,355+i*94,123,68,58);collection[i].raycastTarget=false;
            }
            Text("2 chips fill a box",310,79,230,27,18);
            Card("Next colour",405,23,272,44,new Color(.12f,.21f,.20f));
            nextTitle=Text("",286,23,240,34,18);
            nextSwatch=MushroomUI.Image("Next swatch",page.transform,Color.white);MushroomUI.Place(nextSwatch.rectTransform,509,23,24,24);nextSwatch.raycastTarget=false;
            Text("WAITING AREA",285,-37,240,28,20);
            for(int i=0;i<3;i++)
            {
                var slot=Card("Waiting slot "+(i+1),318+i*87,-88,66,66,new Color(.16f,.25f,.23f));
                stored[i]=Icon(RescueArcadeArt.Shape.Chip,slot.transform,0,0,49,49,Color.white);
                var number=MushroomUI.Text("Slot number",slot.transform,(i+1).ToString(),19,new Color(.64f,.73f,.65f));
                MushroomUI.Fill(number.rectTransform,0);number.alignment=TextAlignmentOptions.Center;number.raycastTarget=false;number.transform.SetAsFirstSibling();
            }
            Text("Other colours wait here.\nThey move into their box automatically when that colour is next.",285,-174,250,82,18);
            Card("Progress track",-170,-214,760,10,new Color(.16f,.25f,.23f));
            progressFill=MushroomUI.Image("Progress fill",page.transform,new Color(.25f,.56f,.39f));progressFill.raycastTarget=false;
            progressLabel=Text("",-550,-243,340,35,17);
            status=Text("",-190,-243,735,43,18);
            var restart=Button("Restart",page.transform,-285,-300,490,48,StartAttempt);
            restart.GetComponent<Image>().color=new Color(.20f,.35f,.28f);restart.GetComponentInChildren<TMP_Text>().color=Ink;
            Button("Back to the trail",page.transform,285,-300,490,48,()=>Close());
            axe=Icon(RescueArcadeArt.Shape.Axe,page.transform,0,0,130,155,Color.white).rectTransform;axe.gameObject.SetActive(false);
            travellingChip=Icon(RescueArcadeArt.Shape.Chip,page.transform,0,0,45,45,Color.white).rectTransform;travellingChip.gameObject.SetActive(false);
            Refresh();status.text="Choose between the upper logs. Plan your colour order.";
        }
        void Refresh()
        {
            Color active=Palette[puzzle.BoxColour];boxCard.color=Color.Lerp(new Color(.08f,.17f,.15f),active,.13f);
            boxTitle.text="COLLECT "+Names[puzzle.BoxColour];boxTitle.color=active;boxTitle.color=new Color(boxTitle.color.r,boxTitle.color.g,boxTitle.color.b,1);
            for(int i=0;i<2;i++)
            {
                collection[i].color=i<puzzle.InBox?active:Color.Lerp(new Color(.12f,.21f,.19f),active,.15f);
                // Il bordo visibile dei due spazi rende chiaro quanti pezzi mancano.
                var outline=collection[i].GetComponent<Outline>();if(outline==null)outline=collection[i].gameObject.AddComponent<Outline>();
                outline.effectColor=active;outline.effectDistance=new Vector2(2,-2);
            }
            nextTitle.text="NEXT: "+Names[puzzle.NextColour];nextSwatch.color=Palette[puzzle.NextColour];
            for(int i=0;i<puzzle.TotalCuts;i++)
            {
                marks[i].gameObject.SetActive(!puzzle.IsRemoved(i));marks[i].interactable=true;
                Color c=Palette[puzzle.Colour(i)];if(!puzzle.Accessible(i/2))c=Color.Lerp(c,new Color(.35f,.30f,.21f),.72f);
                marks[i].GetComponentInChildren<RescueArcadeArt>().color=c;
            }
            for(int i=0;i<3;i++){stored[i].gameObject.SetActive(i<puzzle.TrayCount);if(i<puzzle.TrayCount)stored[i].color=Palette[puzzle.TrayColour(i)];}
            progressLabel.text="CUTS: "+puzzle.Count+" / "+puzzle.TotalCuts+"     ERRORS: "+puzzle.Mistakes+" / 3";
            progressLabel.fontSize=17;
            float width=760*puzzle.Count/(float)puzzle.TotalCuts;MushroomUI.Place(progressFill.rectTransform,-550+width*.5f,-214,width,10);
        }
        Vector2 PanelPoint(RectTransform rect)
        {return ((RectTransform)page.transform).InverseTransformPoint(rect.position);}
        void Chop(int id)
        {
            if(animating || finished || !roundRunning)return;
            if(Time.unscaledTime>=deadline){puzzle.TimeUp();Failure("Time is up.");return;}
            int beforeBox=puzzle.BoxColour,beforeStored=puzzle.TrayCount,beforeCollected=puzzle.InBox;
            if(!puzzle.Remove(id))
            {
                Refresh();
                if(puzzle.Failed)Failure(puzzle.Mistakes>=3?"Three unsafe cuts.":"The waiting area overflowed.");
                else status.text="That log is still covered. Unsafe cuts: "+puzzle.Mistakes+" / 3.";
                return;
            }
            Vector2 destination=beforeBox==puzzle.Colour(id)?new Vector2(355+beforeCollected*94,123):new Vector2(318+beforeStored*87,-88);
            status.text=beforeBox==puzzle.Colour(id)?Names[beforeBox]+" chip collected.":Names[puzzle.Colour(id)]+" chip saved in the waiting area.";
            StartCoroutine(ChopAnimation(id,destination,beforeBox));
        }
        IEnumerator ChopAnimation(int id,Vector2 destination,int oldColour)
        {
            animating=true;var point=PanelPoint((RectTransform)marks[id].transform);axe.gameObject.SetActive(true);axe.anchoredPosition=new Vector2(point.x-25,Mathf.Min(point.y+50,120));axe.SetAsLastSibling();
            for(float t=0;t<.30f;t+=Time.unscaledDeltaTime)
            {float u=t/.30f;axe.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(55,-35,u*u));yield return null;}
            axe.gameObject.SetActive(false);marks[id].gameObject.SetActive(false);
            var board=(RectTransform)boards[id/2].transform;Vector2 origin=board.anchoredPosition;
            travellingChip.GetComponent<RescueArcadeArt>().color=Palette[puzzle.Colour(id)];travellingChip.gameObject.SetActive(true);travellingChip.SetAsLastSibling();
            for(float t=0;t<.40f;t+=Time.unscaledDeltaTime)
            {
                float u=t/.40f;travellingChip.anchoredPosition=Vector2.Lerp(point,destination,u)+Vector2.up*Mathf.Sin(u*Mathf.PI)*70;
                board.anchoredPosition=origin+Vector2.right*Mathf.Sin(u*30)*3*(1-u);yield return null;
            }
            travellingChip.gameObject.SetActive(false);board.anchoredPosition=origin;
            if(puzzle.BoardGone(id/2))
            {
                for(float t=0;t<.40f;t+=Time.unscaledDeltaTime)
                {float u=t/.40f;board.anchoredPosition=origin+new Vector2(-820*u*u,35*Mathf.Sin(u*Mathf.PI));board.Rotate(0,0,-25*Time.unscaledDeltaTime);yield return null;}
                board.gameObject.SetActive(false);
            }
            Refresh();animating=false;
            if(oldColour!=puzzle.BoxColour)status.text="Box complete! Collect "+Names[puzzle.BoxColour]+" now. Matching waiting chips moved in.";
            if(puzzle.Won)Success();
        }
        void Success()
        {
            finished=true;roundRunning=false;NewPage();page.GetComponent<Image>().color=new Color(.035f,.095f,.09f);
            Text("THE DEER IS FREE",-550,280,1100,55,33);
            Text("The timber has been cleared. Your aid kit is ready for the next step.",-550,221,1100,50,23);
            Card("Clearing",0,-5,1080,350,new Color(.13f,.23f,.17f));Art(RescueArtwork.Picture.Deer,page.transform,0,-2,690,350);
            Button("Continue to the aid kit",page.transform,0,-245,650,52,()=>Close(true));
            var back=Button("Back to the trail",page.transform,0,-305,650,42,()=>Close());back.GetComponent<Image>().color=new Color(.45f,.55f,.45f);
        }
        void Instructions()
        {
            roundRunning=false;finished=false;NewPage();
            Text("CLEAR THE FALLEN TIMBER",-550,280,1100,55,32);
            Text("Plan your cuts before you swing.",-550,211,1100,50,25);
            Text("• Each attempt has six logs, twelve cuts and a different layout.\n• Match the active colour to fill its two spaces.\n• Other colours wait in the three spare spaces. A fourth one ends the attempt.\n• Dark marks are under another log: three unsafe cuts end the attempt.\n• You have "+Mathf.Clamp(timeLimitSeconds,30,180).ToString("0")+" seconds, starting when you press Start.\n• You can retry without losing the axe or hurting the deer.",-520,-15,1040,320,24);
            Button("Start challenge",page.transform,-275,-285,480,50,StartAttempt);
            Button("Back to the trail",page.transform,275,-285,480,50,()=>Close());
        }
        int lastSeed;
        void StartAttempt()
        {
            int seed;do{seed=UnityEngine.Random.Range(1,int.MaxValue);}while(seed==lastSeed);
            lastSeed=seed;puzzle=new ScrewPuzzle(seed);Build();
        }
        void Failure(string reason)
        {
            StopAllCoroutines();animating=false;roundRunning=false;finished=true;NewPage();
            Text("TRY A DIFFERENT ORDER",-550,280,1100,55,32);
            Text(reason,-550,205,1100,70,27);
            Text("The deer is safe and the axe is still yours.\nFill the active box before storing too many other colours.\nClear upper logs before touching dark marks.",-510,15,1020,210,25);
            Button("Try again",page.transform,-275,-285,480,50,StartAttempt);
            Button("Back to the trail",page.transform,275,-285,480,50,()=>Close());
        }
        void Update()
        {
            if(!IsOpen)return;
            if(playerinputcontroller.ConsumeEscape()){roundRunning=false;Close();return;}
            if(!roundRunning)return;
            float remaining=Mathf.Max(0,deadline-Time.unscaledTime);
            if(clock!=null){clock.text="TIME: "+Mathf.CeilToInt(remaining)+" s";clock.color=remaining<=10?new Color(1,.51f,.40f):Ink;}
            if(remaining<=0 && !puzzle.Won){puzzle.TimeUp();Failure("Time is up.");}
        }
    }
}
