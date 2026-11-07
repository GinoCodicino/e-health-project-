using UnityEngine;
using UnityEngine.UI;

// Illustrazioni UI del secchio: nessuna immagine o materiale da assegnare.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForestBucketArt : MaskableGraphic
{
    public enum Picture { Riverbank, Bucket, Droplet, Panel }
    public Picture picture;
    [Range(0,1)] public float waterLevel;
    public bool animateWater=true;
    public bool success;
    float wavePhase;
    public void SetWater(float level,bool moving,bool complete)
    {
        waterLevel=Mathf.Clamp01(level); animateWater=moving; success=complete;
        SetVerticesDirty();
    }
    void Update()
    {
        if(picture!=Picture.Bucket || !animateWater) return;
        wavePhase+=Time.unscaledDeltaTime*2.4f; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch(picture)
        {
            case Picture.Riverbank: Riverbank(mesh); break;
            case Picture.Bucket: Bucket(mesh); break;
            case Picture.Droplet:
                Polygon(mesh,Color.white,new Vector2(.5f,1),new Vector2(.88f,.36f),new Vector2(.84f,.12f),new Vector2(.5f,0),new Vector2(.16f,.12f),new Vector2(.12f,.36f)); break;
            case Picture.Panel:
                Polygon(mesh,Color.white,new Vector2(.022f,0),new Vector2(.978f,0),new Vector2(1,.17f),new Vector2(1,.83f),new Vector2(.978f,1),new Vector2(.022f,1),new Vector2(0,.83f),new Vector2(0,.17f)); break;
        }
    }
    void Riverbank(VertexHelper m)
    {
        Quad(m,0,0,1,1,new Color(.28f,.44f,.43f));
        Polygon(m,new Color(.39f,.54f,.50f),new Vector2(0,.46f),new Vector2(.13f,.89f),new Vector2(.30f,.51f),new Vector2(.43f,.82f),new Vector2(.65f,.43f),new Vector2(1,.66f),new Vector2(1,0),new Vector2(0,0));
        Polygon(m,new Color(.24f,.40f,.36f),new Vector2(0,.34f),new Vector2(.14f,.58f),new Vector2(.30f,.37f),new Vector2(.54f,.65f),new Vector2(.79f,.30f),new Vector2(1,.45f),new Vector2(1,0),new Vector2(0,0));
        Polygon(m,new Color(.16f,.33f,.28f),new Vector2(0,.52f),new Vector2(.30f,.35f),new Vector2(.60f,.37f),new Vector2(1,.57f),new Vector2(1,0),new Vector2(0,0));
        Polygon(m,new Color(.18f,.47f,.51f),new Vector2(.29f,.38f),new Vector2(.47f,.39f),new Vector2(.68f,0),new Vector2(.03f,0),new Vector2(.21f,.22f));
        Polygon(m,new Color(.25f,.60f,.63f),new Vector2(.33f,.37f),new Vector2(.40f,.38f),new Vector2(.49f,.15f),new Vector2(.27f,.04f),new Vector2(.18f,.07f),new Vector2(.35f,.22f));
        for(int i=0;i<13;i++)
        {
            float y=.04f+(i%5)*.058f, x=.11f+(i%4)*.09f;
            Quad(m,x,y,x+.055f,y+.005f,new Color(.63f,.85f,.80f,.32f));
        }
        for(int i=0;i<6;i++) Pine(m,.035f+i*.055f,.26f+(i%2)*.07f,.065f,.34f,new Color(.10f,.27f,.21f));
        for(int i=0;i<4;i++) Rock(m,.08f+i*.11f,.035f+(i%2)*.025f,.04f,.055f);
        Rock(m,.56f,.07f,.07f,.08f); Rock(m,.61f,.05f,.035f,.045f);
        // Vegetazione sul bordo, senza coprire il secchio e il pannello delle informazioni.
        for(int i=0;i<9;i++)
        {
            float x=.57f+i*.038f;
            Polygon(m,new Color(.38f,.49f,.25f),new Vector2(x,.03f),new Vector2(x-.012f,.15f),new Vector2(x+.008f,.09f),new Vector2(x+.016f,.19f),new Vector2(x+.025f,.03f));
        }
    }
    void Pine(VertexHelper m,float x,float y,float w,float h,Color tint)
    {
        Quad(m,x-w*.07f,y,x+w*.07f,y+h*.5f,new Color(.22f,.23f,.14f));
        for(int i=0;i<3;i++)
        {
            float baseY=y+h*(.18f+i*.18f),half=w*(.5f-i*.09f);
            Polygon(m,tint,new Vector2(x-half,baseY),new Vector2(x+half,baseY),new Vector2(x,baseY+h*.42f));
        }
    }
    void Rock(VertexHelper m,float x,float y,float w,float h)
    {
        Polygon(m,new Color(.44f,.50f,.43f),new Vector2(x-w,y),new Vector2(x-w*.6f,y+h*.8f),new Vector2(x+w*.3f,y+h),new Vector2(x+w,y+h*.3f),new Vector2(x+w*.7f,y));
        Polygon(m,new Color(.61f,.65f,.55f),new Vector2(x-w*.6f,y+h*.8f),new Vector2(x+w*.3f,y+h),new Vector2(x+w*.45f,y+h*.45f),new Vector2(x-w*.2f,y+h*.5f));
    }
    void Bucket(VertexHelper m)
    {
        Color dark=new Color(.17f,.12f,.065f),metal=new Color(.28f,.34f,.32f);
        Ellipse(m,.5f,.075f,.32f,.045f,new Color(.025f,.065f,.055f,.30f));
        Arc(m,.5f,.64f,.29f,.31f,.024f,0,Mathf.PI,metal);
        Arc(m,.5f,.64f,.275f,.295f,.007f,.1f,Mathf.PI-.1f,new Color(.68f,.73f,.65f));
        Polygon(m,dark,new Vector2(.17f,.69f),new Vector2(.83f,.69f),new Vector2(.73f,.115f),new Vector2(.27f,.115f));
        for(int i=0;i<8;i++)
        {
            float f0=i/8f,f1=(i+1)/8f;
            Color wood=i%2==0 ? new Color(.61f,.38f,.18f) : new Color(.70f,.46f,.24f);
            Polygon(m,wood,new Vector2(Mathf.Lerp(.19f,.81f,f0),.67f),new Vector2(Mathf.Lerp(.19f,.81f,f1)-.004f,.67f),new Vector2(Mathf.Lerp(.285f,.715f,f1)-.004f,.14f),new Vector2(Mathf.Lerp(.285f,.715f,f0),.14f));
        }
        // Vista in sezione del recipiente: acqua confinata nei bordi inclinati.
        Polygon(m,new Color(.12f,.23f,.22f),new Vector2(.235f,.65f),new Vector2(.765f,.65f),new Vector2(.685f,.18f),new Vector2(.315f,.18f));
        if(waterLevel>0)
        {
            float top=.18f+waterLevel*.47f;
            float left=Mathf.Lerp(.315f,.235f,waterLevel),right=1-left;
            Vector2[] points=new Vector2[39]; points[0]=new Vector2(.315f,.18f); points[1]=new Vector2(.685f,.18f);
            for(int i=0;i<=36;i++)
            {
                float t=i/36f, x=Mathf.Lerp(right,left,t);
                float wave=Mathf.Sin(t*Mathf.PI*3+wavePhase)*.006f*Mathf.Sin(t*Mathf.PI);
                points[i+2]=new Vector2(x,Mathf.Clamp(top+wave,.18f,.65f));
            }
            Polygon(m,new Color(.22f,.66f,.72f),points);
            Line(m,new Vector2(left+.015f,top),new Vector2(right-.015f,top),.008f,new Color(.70f,.92f,.88f));
            if(waterLevel>.15f) Ellipse(m,.44f,top-.035f,.065f,.008f,new Color(.58f,.86f,.83f,.65f));
        }
        foreach(float y in new[]{.23f,.56f})
        {
            float f=(y-.115f)/(.69f-.115f),left=Mathf.Lerp(.27f,.17f,f),right=1-left;
            Quad(m,left-.01f,y-.018f,right+.01f,y+.018f,metal);
            Quad(m,left-.01f,y+.007f,right+.01f,y+.018f,new Color(.49f,.56f,.50f));
            Ellipse(m,left+.035f,y,.012f,.012f,new Color(.72f,.74f,.62f));
            Ellipse(m,right-.035f,y,.012f,.012f,new Color(.72f,.74f,.62f));
        }
        // Orlo e attacchi del manico.
        Quad(m,.17f,.66f,.83f,.70f,metal);
        Quad(m,.18f,.688f,.82f,.70f,new Color(.65f,.68f,.55f));
        Ellipse(m,.205f,.65f,.027f,.035f,metal); Ellipse(m,.795f,.65f,.027f,.035f,metal);
        Ellipse(m,.205f,.65f,.010f,.013f,new Color(.76f,.76f,.63f));
        Ellipse(m,.795f,.65f,.010f,.013f,new Color(.76f,.76f,.63f));
        if(success)
        {
            Line(m,new Vector2(.70f,.86f),new Vector2(.76f,.80f),.022f,new Color(.76f,.94f,.60f));
            Line(m,new Vector2(.76f,.80f),new Vector2(.88f,.94f),.022f,new Color(.76f,.94f,.60f));
        }
    }
    void Arc(VertexHelper m,float x,float y,float rx,float ry,float width,float from,float to,Color tint)
    {
        for(int i=0;i<40;i++)
        {
            float a=Mathf.Lerp(from,to,i/40f),b=Mathf.Lerp(from,to,(i+1)/40f);
            Line(m,new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),new Vector2(x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry),width,tint);
        }
    }
    void Vertex(VertexHelper m,Vector2 point,Color tint)
    {
        Rect r=rectTransform.rect; UIVertex v=UIVertex.simpleVert;
        v.position=new Vector2(r.xMin+point.x*r.width,r.yMin+point.y*r.height);
        v.color=tint*color; m.AddVert(v);
    }
    void Polygon(VertexHelper m,Color tint,params Vector2[] points)
    {
        int start=m.currentVertCount;
        foreach(Vector2 point in points) Vertex(m,point,tint);
        for(int i=1;i<points.Length-1;i++) m.AddTriangle(start,start+i,start+i+1);
    }
    void Quad(VertexHelper m,float x0,float y0,float x1,float y1,Color tint)
    { Polygon(m,tint,new Vector2(x0,y0),new Vector2(x0,y1),new Vector2(x1,y1),new Vector2(x1,y0)); }
    void Line(VertexHelper m,Vector2 a,Vector2 b,float width,Color tint)
    {
        Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
        Polygon(m,tint,a+n,b+n,b-n,a-n);
    }
    void Ellipse(VertexHelper m,float x,float y,float rx,float ry,Color tint)
    {
        int start=m.currentVertCount; Vertex(m,new Vector2(x,y),tint);
        for(int i=0;i<=36;i++)
        {
            float angle=i*Mathf.PI*2/36;
            Vertex(m,new Vector2(x+Mathf.Cos(angle)*rx,y+Mathf.Sin(angle)*ry),tint);
            if(i>0) m.AddTriangle(start,start+i,start+i+1);
        }
    }
}
