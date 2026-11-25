using UnityEngine;
using UnityEngine.UI;

namespace Level02.DeerRescue
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RescueArtwork : MaskableGraphic
    {
        public enum Picture { River, Fish, Stone, Branch, Boot, Bottle, Net, Deer, Dressing }
        public Picture picture;
        [Range(0,1)] public float damage;
        public bool bandaged;
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();
            switch(picture)
            {
                case Picture.River: River(m); break;
                case Picture.Fish: Fish(m); break;
                case Picture.Stone: Rock(m,.5f,.4f,.42f,.42f); break;
                case Picture.Branch: Branch(m); break;
                case Picture.Boot: Boot(m); break;
                case Picture.Bottle: Bottle(m); break;
                case Picture.Net: Net(m); break;
                case Picture.Deer: Deer(m); break;
                case Picture.Dressing: Dressing(m); break;
            }
        }
        void River(VertexHelper m)
        {
            Quad(m,0,0,1,1,new Color(.10f,.31f,.37f));
            Quad(m,0,.55f,1,1,new Color(.15f,.43f,.47f));
            Polygon(m,new Color(.25f,.40f,.31f),new Vector2(0,1),new Vector2(.06f,.72f),new Vector2(.08f,.25f),new Vector2(.16f,0),new Vector2(0,0));
            Polygon(m,new Color(.25f,.40f,.31f),new Vector2(1,1),new Vector2(.94f,.72f),new Vector2(.92f,.25f),new Vector2(.84f,0),new Vector2(1,0));
            Polygon(m,new Color(.39f,.40f,.29f),new Vector2(0,0),new Vector2(0,.06f),new Vector2(.20f,.12f),new Vector2(.43f,.07f),new Vector2(.68f,.12f),new Vector2(1,.06f),new Vector2(1,0));
            for(int i=0;i<18;i++)
            {
                float x=.13f+(i%6)*.14f,y=.23f+(i/6)*.25f;
                Quad(m,x,y,x+.05f,y+.005f,new Color(.60f,.83f,.77f,.30f));
            }
            for(int i=0;i<8;i++) Rock(m,.07f+i*.12f,.035f,.035f,.045f);
            for(int i=0;i<7;i++)
            {
                float x=.035f+i*.155f;
                Line(m,new Vector2(x,.04f),new Vector2(x-.015f,.17f),.008f,new Color(.29f,.56f,.29f));
                Line(m,new Vector2(x+.01f,.04f),new Vector2(x+.03f,.21f),.007f,new Color(.39f,.62f,.32f));
            }
        }
        void Fish(VertexHelper m)
        {
            Polygon(m,new Color(.90f,.59f,.25f),new Vector2(.28f,.50f),new Vector2(.02f,.86f),new Vector2(.06f,.19f));
            Ellipse(m,.58f,.50f,.36f,.29f,new Color(.89f,.69f,.36f));
            Polygon(m,new Color(.56f,.64f,.44f),new Vector2(.30f,.67f),new Vector2(.57f,.91f),new Vector2(.76f,.73f));
            Polygon(m,new Color(.96f,.83f,.56f),new Vector2(.34f,.43f),new Vector2(.72f,.32f),new Vector2(.91f,.47f),new Vector2(.71f,.24f),new Vector2(.42f,.27f));
            Ellipse(m,.79f,.59f,.045f,.06f,new Color(.10f,.18f,.17f));
            Ellipse(m,.80f,.61f,.012f,.016f,Color.white);
            for(int i=0;i<4;i++) Line(m,new Vector2(.38f+i*.075f,.48f),new Vector2(.40f+i*.075f,.61f),.016f,new Color(.65f,.48f,.25f));
        }
        void Branch(VertexHelper m)
        {
            Quad(m,.04f,.22f,.96f,.73f,new Color(.34f,.19f,.09f));
            Quad(m,.04f,.60f,.96f,.73f,new Color(.49f,.29f,.13f));
            Ellipse(m,.94f,.475f,.055f,.255f,new Color(.76f,.55f,.30f));
            Ellipse(m,.94f,.475f,.03f,.16f,new Color(.49f,.29f,.13f));
            for(int i=0;i<4;i++) Line(m,new Vector2(.1f,.30f+i*.085f),new Vector2(.86f,.32f+i*.085f),.012f,new Color(.22f,.12f,.06f));
            Polygon(m,new Color(.34f,.19f,.09f),new Vector2(.65f,.66f),new Vector2(.70f,1),new Vector2(.76f,.99f),new Vector2(.73f,.66f));
            if(damage>0) Polygon(m,new Color(.12f,.10f,.06f),new Vector2(.18f,.74f),new Vector2(.22f,.74f),new Vector2(.205f,.74f-.5f*damage));
        }
        void Boot(VertexHelper m)
        {
            Polygon(m,new Color(.34f,.20f,.12f),new Vector2(.30f,.92f),new Vector2(.63f,.92f),new Vector2(.64f,.43f),new Vector2(.89f,.29f),new Vector2(.94f,.15f),new Vector2(.12f,.15f),new Vector2(.12f,.35f),new Vector2(.29f,.46f));
            Quad(m,.12f,.10f,.94f,.20f,new Color(.16f,.14f,.10f));
            for(int i=0;i<4;i++) Quad(m,.36f,.48f+i*.09f,.57f,.51f+i*.09f,new Color(.78f,.66f,.43f));
        }
        void Bottle(VertexHelper m)
        {
            Polygon(m,new Color(.40f,.67f,.57f),new Vector2(.35f,.09f),new Vector2(.68f,.09f),new Vector2(.68f,.65f),new Vector2(.58f,.77f),new Vector2(.58f,.94f),new Vector2(.44f,.94f),new Vector2(.44f,.77f),new Vector2(.35f,.65f));
            Quad(m,.43f,.92f,.60f,.99f,new Color(.35f,.31f,.23f));
            Quad(m,.37f,.28f,.66f,.53f,new Color(.85f,.80f,.61f));
        }
        void Net(VertexHelper m)
        {
            Color rim=new Color(.85f,.76f,.50f),mesh=new Color(.68f,.85f,.78f,.8f);
            for(int i=0;i<8;i++)
            {
                float x=.10f+i*.11f;
                Line(m,new Vector2(x,.78f),new Vector2(Mathf.Lerp(.5f,x,.55f),.15f),.010f,mesh);
            }
            for(int i=0;i<4;i++)
            {
                float y=.23f+i*.13f,half=.18f+i*.055f;
                Line(m,new Vector2(.5f-half,y),new Vector2(.5f+half,y),.01f,mesh);
            }
            Line(m,new Vector2(.07f,.80f),new Vector2(.93f,.80f),.045f,rim);
            Line(m,new Vector2(.07f,.80f),new Vector2(.30f,.13f),.025f,rim);
            Line(m,new Vector2(.93f,.80f),new Vector2(.70f,.13f),.025f,rim);
            Line(m,new Vector2(.30f,.13f),new Vector2(.70f,.13f),.025f,rim);
        }
        void Deer(VertexHelper m)
        {
            Color fur=new Color(.62f,.38f,.18f),light=new Color(.85f,.67f,.39f),dark=new Color(.21f,.15f,.09f);
            Ellipse(m,.46f,.25f,.36f,.06f,new Color(0,0,0,.15f));
            Ellipse(m,.45f,.47f,.29f,.22f,fur);
            Ellipse(m,.48f,.39f,.22f,.10f,light);
            Polygon(m,fur,new Vector2(.60f,.46f),new Vector2(.64f,.77f),new Vector2(.73f,.76f),new Vector2(.75f,.44f));
            Ellipse(m,.73f,.77f,.13f,.12f,fur);
            Polygon(m,fur,new Vector2(.69f,.84f),new Vector2(.63f,1),new Vector2(.73f,.92f));
            Polygon(m,fur,new Vector2(.77f,.85f),new Vector2(.84f,.98f),new Vector2(.82f,.80f));
            Ellipse(m,.82f,.74f,.07f,.055f,light); Ellipse(m,.87f,.75f,.025f,.03f,dark);
            Ellipse(m,.76f,.81f,.018f,.025f,dark);
            Polygon(m,fur,new Vector2(.26f,.36f),new Vector2(.15f,.20f),new Vector2(.35f,.19f),new Vector2(.35f,.24f),new Vector2(.25f,.25f),new Vector2(.37f,.37f));
            Polygon(m,fur,new Vector2(.59f,.36f),new Vector2(.68f,.20f),new Vector2(.81f,.20f),new Vector2(.81f,.26f),new Vector2(.72f,.27f),new Vector2(.68f,.40f));
            Line(m,new Vector2(.15f,.21f),new Vector2(.21f,.21f),.035f,dark);
            Line(m,new Vector2(.77f,.23f),new Vector2(.82f,.23f),.04f,dark);
            if(bandaged) Quad(m,.70f,.19f,.77f,.29f,new Color(.94f,.92f,.79f));
        }
        void Dressing(VertexHelper m)
        {
            Quad(m,.10f,.25f,.90f,.75f,new Color(.91f,.90f,.77f));
            Quad(m,.32f,.29f,.68f,.71f,new Color(.99f,.97f,.86f));
            for(int i=0;i<4;i++) Quad(m,.15f+i*.18f,.37f,.18f+i*.18f,.42f,new Color(.73f,.75f,.66f));
        }
        void Rock(VertexHelper m,float x,float y,float w,float h)
        {
            Polygon(m,new Color(.45f,.52f,.47f),new Vector2(x-w,y),new Vector2(x-w*.6f,y+h*.8f),new Vector2(x+w*.3f,y+h),new Vector2(x+w,y+h*.2f),new Vector2(x+w*.7f,y));
            Polygon(m,new Color(.64f,.68f,.58f),new Vector2(x-w*.6f,y+h*.8f),new Vector2(x+w*.3f,y+h),new Vector2(x+w*.45f,y+h*.4f));
        }
        void Vertex(VertexHelper m,Vector2 p,Color tint)
        { Rect r=rectTransform.rect; UIVertex v=UIVertex.simpleVert; v.position=new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height); v.color=tint*color; m.AddVert(v); }
        void Polygon(VertexHelper m,Color tint,params Vector2[] points)
        { int start=m.currentVertCount; foreach(var p in points) Vertex(m,p,tint); for(int i=1;i<points.Length-1;i++) m.AddTriangle(start,start+i,start+i+1); }
        void Quad(VertexHelper m,float x0,float y0,float x1,float y1,Color tint)
        { Polygon(m,tint,new Vector2(x0,y0),new Vector2(x0,y1),new Vector2(x1,y1),new Vector2(x1,y0)); }
        void Line(VertexHelper m,Vector2 a,Vector2 b,float w,Color tint)
        { Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*w*.5f; Polygon(m,tint,a+n,b+n,b-n,a-n); }
        void Ellipse(VertexHelper m,float x,float y,float rx,float ry,Color tint)
        {
            int start=m.currentVertCount; Vertex(m,new Vector2(x,y),tint);
            for(int i=0;i<=32;i++) { float a=i*Mathf.PI*2/32; Vertex(m,new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),tint); if(i>0)m.AddTriangle(start,start+i,start+i+1); }
        }
    }
}
