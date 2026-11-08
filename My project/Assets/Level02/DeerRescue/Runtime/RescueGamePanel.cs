using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Level02.DeerRescue
{
    // Base comune: crea la UI senza prefab e blocca il movimento soltanto mentre e aperta.
    public abstract class RescueGamePanel : MonoBehaviour
    {
        static Sprite deerSprite,roundedSprite;
        public static Sprite RoundedPanelSprite()
        {
            if(roundedSprite!=null)return roundedSprite;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.name="Rounded panel";texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float dx=Mathf.Max(16-x,Mathf.Max(x-47,0)),dy=Mathf.Max(16-y,Mathf.Max(y-47,0));
                pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(16.5f-Mathf.Sqrt(dx*dx+dy*dy)));
            }
            texture.SetPixels(pixels);texture.Apply();
            roundedSprite=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(18,18,18,18));return roundedSprite;
        }
        protected GameObject root,page;
        protected RectTransform canvas;
        protected playercontroller player;
        Action<bool> completed;
        public bool IsOpen { get { return root!=null && root.activeSelf; } }
        protected void Begin(playercontroller who,Action<bool> callback,string title)
        {
            player=who;completed=callback;player.SetUiMode(this,true);
            if(root==null){canvas=MushroomUI.Canvas(title);root=canvas.gameObject;}
            root.SetActive(true);NewPage();
        }
        protected void NewPage()
        {
            if(page!=null){page.SetActive(false);Destroy(page);}
            var bg=MushroomUI.Image("Panel",canvas,new Color(.035f,.095f,.09f));
            page=bg.gameObject;MushroomUI.Place(bg.rectTransform,0,0,1200,680);
        }
        protected TMP_Text Label(string text,float x,float y,float w=1080,float h=42,int size=23)
        {
            var t=MushroomUI.Text("Label",page.transform,text,size,Color.white);
            t.rectTransform.pivot=new Vector2(0,.5f);MushroomUI.Place(t.rectTransform,x,y,w,h);
            t.alignment=TextAlignmentOptions.MidlineLeft;t.enableWordWrapping=true;t.raycastTarget=false;return t;
        }
        protected Button Button(string text,Transform parent,float x,float y,float w,float h,Action action)
        {
            TMP_Text t;var b=MushroomUI.Button(text,parent,text,new Color(.22f,.4f,.31f),out t);
            t.fontSize=20;t.color=Color.white;MushroomUI.Place((RectTransform)b.transform,x,y,w,h);
            Navigation n=b.navigation;n.mode=Navigation.Mode.None;b.navigation=n;b.onClick.AddListener(()=>action());return b;
        }
        protected RescueArtwork Art(RescueArtwork.Picture type,Transform parent,float x,float y,float w,float h)
        {
            var a=MushroomUI.Rect(type.ToString(),parent).gameObject.AddComponent<RescueArtwork>();
            a.picture=type;a.raycastTarget=false;MushroomUI.Place(a.rectTransform,x,y,w,h);
            if(type==RescueArtwork.Picture.Deer)
            {
                if(deerSprite==null)
                {
                    var texture=Resources.Load<Texture2D>("DeerRescueArt/RoeDeer");
                    if(texture!=null)deerSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                }
                if(deerSprite!=null)
                {
                    a.color=Color.clear;
                    var portrait=MushroomUI.Image("Roe deer illustration",a.transform,Color.white);
                    portrait.sprite=deerSprite;portrait.preserveAspect=true;portrait.raycastTarget=false;MushroomUI.Fill(portrait.rectTransform,0);
                }
            }
            return a;
        }
        protected RescueArcadeArt Icon(RescueArcadeArt.Shape type,Transform parent,float x,float y,float w,float h,Color tint)
        {
            var a=MushroomUI.Rect(type.ToString(),parent).gameObject.AddComponent<RescueArcadeArt>();
            a.shape=type;a.color=tint;a.raycastTarget=false;MushroomUI.Place(a.rectTransform,x,y,w,h);return a;
        }
        public void Close(bool won=false)
        {
            if(!IsOpen)return;
            StopAllCoroutines();root.SetActive(false);if(player!=null)player.SetUiMode(this,false);
            var callback=completed;completed=null;callback?.Invoke(won);
        }
        protected virtual void OnDisable(){Close();}
        protected virtual void OnDestroy(){if(root!=null)Destroy(root);}
    }
}
