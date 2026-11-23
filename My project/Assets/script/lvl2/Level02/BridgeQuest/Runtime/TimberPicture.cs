using UnityEngine;
using UnityEngine.UI;

namespace Level02.BridgeMission
{
    // Illustrazioni UI: legno, vegetazione, tagli, schegge e ascia animata.
    [RequireComponent(typeof(CanvasRenderer))]
    
    public sealed class TimberPicture : MaskableGraphic
    {
        public enum Picture { Log, Plank, Tree }
        public Picture picture;
        [Range(0,1)] public float damage;
        public bool showAxe;
        float impactTime;
        Vector2 restPosition;
        Quaternion restRotation;
        bool animating;
        public void PlayHit(float amount)
        {
            damage=Mathf.Clamp01(amount);
            if(!animating) { restPosition=rectTransform.anchoredPosition; restRotation=rectTransform.localRotation; }
            animating=true; impactTime=.22f; SetVerticesDirty();
        }
        void Update()
        {
            if(!animating) return;
            impactTime=Mathf.Max(0,impactTime-Time.unscaledDeltaTime);
            float power=impactTime/.22f;
            rectTransform.anchoredPosition=restPosition+new Vector2(Mathf.Sin(power*38)*8*power,Mathf.Cos(power*31)*2*power);
            rectTransform.localRotation=restRotation*Quaternion.Euler(0,0,Mathf.Sin(power*29)*1.4f*power);
            if(impactTime<=0)
            {
                rectTransform.anchoredPosition=restPosition;
                rectTransform.localRotation=restRotation; animating=false;
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(picture==Picture.Tree) { Tree(vh); return; }
            if(picture==Picture.Plank) { Plank(vh); return; }
            Log(vh);
        }
        void Log(VertexHelper vh)
        {
            Color bark=new Color(.29f,.14f,.055f);
            Ellipse(vh,.48f,.17f,.45f,.08f,new Color(0,0,0,.18f));
            Polygon(vh,new Color(.37f,.20f,.085f),new Vector2(.09f,.25f),new Vector2(.80f,.25f),new Vector2(.87f,.74f),new Vector2(.14f,.76f));
            Polygon(vh,new Color(.47f,.28f,.12f),new Vector2(.12f,.61f),new Vector2(.84f,.60f),new Vector2(.87f,.74f),new Vector2(.14f,.76f));
            for(int i=0;i<7;i++)
            {
                float y=.30f+i*.055f;
                Line(vh,new Vector2(.16f,y),new Vector2(.76f,y+.025f*Mathf.Sin(i*2)),.009f,bark);
                Line(vh,new Vector2(.25f+i*.08f,y),new Vector2(.28f+i*.08f,y+.055f),.006f,bark);
            }
            Polygon(vh,bark,new Vector2(.31f,.70f),new Vector2(.37f,.93f),new Vector2(.44f,.93f),new Vector2(.41f,.70f));
            Ellipse(vh,.405f,.925f,.038f,.027f,new Color(.74f,.52f,.29f));
            Ellipse(vh,.84f,.495f,.13f,.255f,bark);
            for(int i=0;i<6;i++)
            {
                float f=1-i*.15f;
                Ellipse(vh,.845f,.497f,.115f*f,.23f*f,i%2==0 ? new Color(.77f,.55f,.31f) : new Color(.55f,.33f,.15f));
            }
            if(damage>0)
            {
                float depth=.12f+damage*.43f;
                Polygon(vh,new Color(.095f,.055f,.025f),new Vector2(.48f,.77f),new Vector2(.53f,.77f),new Vector2(.515f,.77f-depth),new Vector2(.495f,.80f-depth));
                Line(vh,new Vector2(.50f,.65f),new Vector2(.455f,.65f-damage*.22f),.008f,new Color(.80f,.59f,.32f));
                if(damage>.45f) Line(vh,new Vector2(.514f,.49f),new Vector2(.56f,.37f),.010f,bark);
                if(damage>=1) Line(vh,new Vector2(.50f,.76f),new Vector2(.50f,.24f),.022f,new Color(.09f,.12f,.07f));
            }
            if(showAxe)
            {
                float swing=animating ? Mathf.Sin((1-impactTime/.22f)*Mathf.PI) : 0;
                float shift=swing*.11f;
                Polygon(vh,new Color(.62f,.39f,.18f),new Vector2(.64f-shift,.61f),new Vector2(.67f-shift,.64f),new Vector2(.78f,.98f),new Vector2(.74f,.99f));
                Polygon(vh,new Color(.71f,.76f,.75f),new Vector2(.60f-shift,.63f),new Vector2(.73f-shift,.66f),new Vector2(.76f-shift,.81f),new Vector2(.66f-shift,.86f));
                Line(vh,new Vector2(.60f-shift,.63f),new Vector2(.66f-shift,.86f),.013f,new Color(.91f,.94f,.88f));
                if(animating)
                {
                    float burst=1-impactTime/.22f;
                    for(int i=0;i<5;i++)
                    {
                        float x=.50f+(i-2)*.045f*burst, y=.73f+Mathf.Sin(burst*Mathf.PI)*(.09f+i*.015f);
                        Quad(vh,x,y,x+.018f,y+.024f,new Color(.88f,.66f,.35f,1-burst));
                    }
                }
            }
        }
        void Plank(VertexHelper vh)
        {
            Polygon(vh,new Color(.48f,.28f,.12f),new Vector2(.015f,.08f),new Vector2(.985f,.08f),new Vector2(.985f,.92f),new Vector2(.015f,.92f));
            Quad(vh,.015f,.79f,.985f,.92f,new Color(.69f,.44f,.22f));
            Quad(vh,.015f,.08f,.985f,.18f,new Color(.29f,.16f,.07f));
            for(int i=0;i<4;i++)
                Line(vh,new Vector2(.08f,.28f+i*.13f),new Vector2(.92f,.29f+i*.13f),.015f,new Color(.36f,.19f,.075f));
            Ellipse(vh,.35f,.49f,.055f,.11f,new Color(.34f,.18f,.07f));
            Ellipse(vh,.35f,.49f,.027f,.06f,new Color(.56f,.33f,.14f));
            foreach(float x in new[]{.07f,.93f})
            foreach(float y in new[]{.28f,.70f}) Ellipse(vh,x,y,.012f,.04f,new Color(.20f,.21f,.18f));
        }
        void Tree(VertexHelper vh)
        {
            Ellipse(vh,.5f,.10f,.34f,.07f,new Color(0,0,0,.14f));
            Quad(vh,.45f,.12f,.55f,.62f,new Color(.31f,.19f,.08f));
            for(int i=0;i<3;i++)
            {
                float bottom=.26f+i*.19f, half=.40f-i*.08f;
                Polygon(vh,i%2==0 ? new Color(.14f,.34f,.20f) : new Color(.21f,.43f,.24f),new Vector2(.5f-half,bottom),new Vector2(.5f+half,bottom),new Vector2(.5f,bottom+.36f));
            }
        }
        void Vertex(VertexHelper vh,float x,float y,Color tint)
        {
            Rect r=rectTransform.rect;
            UIVertex v=UIVertex.simpleVert; v.position=new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            v.color=tint; vh.AddVert(v);
        }
        void Polygon(VertexHelper vh,Color tint,params Vector2[] points)
        {
            int start=vh.currentVertCount;
            foreach(Vector2 p in points) Vertex(vh,p.x,p.y,tint);
            for(int i=1;i<points.Length-1;i++) vh.AddTriangle(start,start+i,start+i+1);
        }
        void Quad(VertexHelper vh,float x0,float y0,float x1,float y1,Color tint)
        { Polygon(vh,tint,new Vector2(x0,y0),new Vector2(x0,y1),new Vector2(x1,y1),new Vector2(x1,y0)); }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Polygon(vh,tint,a+normal,b+normal,b-normal,a-normal);
        }
        void Ellipse(VertexHelper vh,float x,float y,float rx,float ry,Color tint)
        {
            int start=vh.currentVertCount; Vertex(vh,x,y,tint);
            for(int i=0;i<=32;i++)
            {
                float a=i*Mathf.PI*2/32;
                Vertex(vh,x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry,tint);
                if(i>0) vh.AddTriangle(start,start+i,start+i+1);
            }
        }
    }
}
