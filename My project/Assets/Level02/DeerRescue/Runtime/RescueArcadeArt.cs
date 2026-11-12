using UnityEngine;
using UnityEngine.UI;
namespace Level02.DeerRescue
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RescueArcadeArt : MaskableGraphic
    {
        public enum Shape { Bolt,Plank,Bacterium,Splinter,Tweezers,Spray,CutMark,Axe,Chip }
        public Shape shape;
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            switch(shape)
            {
                case Shape.Bolt:
                    Circle(v,.5f,.5f,.47f,.47f,new Color(.05f,.09f,.10f));
                    Circle(v,.5f,.55f,.39f,.37f,color);Circle(v,.43f,.65f,.13f,.08f,new Color(1,1,1,.35f));
                    Line(v,new Vector2(.27f,.55f),new Vector2(.73f,.55f),.07f,new Color(.08f,.13f,.14f));break;
                case Shape.Plank:
                    Quad(v,0,.10f,.96f,.94f,new Color(.68f,.40f,.16f));
                    Quad(v,.015f,.20f,.945f,.88f,new Color(.79f,.51f,.25f));
                    Quad(v,.02f,.84f,.94f,.91f,new Color(.90f,.65f,.34f));
                    Polygon(v,new Color(.99f,.76f,.44f),new Vector2(0,.94f),new Vector2(.04f,1),new Vector2(1,1),new Vector2(.96f,.94f));
                    Polygon(v,new Color(.44f,.24f,.10f),new Vector2(.96f,.10f),new Vector2(1,.18f),new Vector2(1,1),new Vector2(.96f,.94f));
                    for(int i=0;i<3;i++)Line(v,new Vector2(.10f,.30f+i*.17f),new Vector2(.87f,.34f+i*.16f),.009f,new Color(.43f,.24f,.10f,.5f));
                    Circle(v,.45f,.53f,.035f,.12f,new Color(.62f,.36f,.14f));Circle(v,.45f,.53f,.023f,.07f,new Color(.76f,.47f,.21f));break;
                case Shape.CutMark:
                    Circle(v,.5f,.46f,.46f,.43f,new Color(.19f,.15f,.10f,.38f));
                    Circle(v,.5f,.54f,.42f,.42f,new Color(1,.99f,.92f));
                    Circle(v,.5f,.54f,.35f,.35f,color);
                    Line(v,new Vector2(.33f,.36f),new Vector2(.67f,.72f),.09f,Color.white);
                    Line(v,new Vector2(.33f,.72f),new Vector2(.67f,.36f),.09f,Color.white);break;
                case Shape.Chip:
                    Polygon(v,new Color(.28f,.17f,.07f),new Vector2(.10f,.23f),new Vector2(.84f,.17f),new Vector2(.95f,.62f),new Vector2(.30f,.89f));
                    Polygon(v,color,new Vector2(.10f,.31f),new Vector2(.81f,.25f),new Vector2(.93f,.69f),new Vector2(.27f,.96f));
                    Line(v,new Vector2(.28f,.47f),new Vector2(.75f,.55f),.055f,new Color(1,1,1,.45f));break;
                case Shape.Axe:
                    Line(v,new Vector2(.37f,.10f),new Vector2(.66f,.88f),.13f,new Color(.29f,.16f,.07f));
                    Line(v,new Vector2(.39f,.12f),new Vector2(.66f,.86f),.07f,new Color(.72f,.44f,.19f));
                    Polygon(v,new Color(.29f,.39f,.43f),new Vector2(.43f,.92f),new Vector2(.84f,.91f),new Vector2(.94f,.69f),new Vector2(.82f,.48f),new Vector2(.54f,.63f));
                    Polygon(v,new Color(.76f,.85f,.87f),new Vector2(.72f,.89f),new Vector2(.84f,.91f),new Vector2(.94f,.69f),new Vector2(.82f,.48f),new Vector2(.74f,.55f),new Vector2(.84f,.71f));break;
                case Shape.Bacterium:
                    for(int i=0;i<10;i++){float a=i*Mathf.PI/5;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(v,new Vector2(.5f,.5f)+d*.25f,new Vector2(.5f,.5f)+d*.48f,.045f,color);}
                    Circle(v,.5f,.5f,.32f,.33f,color);Circle(v,.4f,.57f,.055f,.065f,new Color(.07f,.12f,.14f));Circle(v,.62f,.57f,.055f,.065f,new Color(.07f,.12f,.14f));
                    Line(v,new Vector2(.4f,.34f),new Vector2(.63f,.38f),.035f,new Color(.08f,.14f,.15f));break;
                case Shape.Splinter:
                    Triangle(v,new Vector2(.15f,.15f),new Vector2(.55f,.95f),new Vector2(.84f,.25f),new Color(.66f,.35f,.18f));
                    Circle(v,.54f,.46f,.12f,.11f,new Color(.72f,.9f,.20f));break;
                case Shape.Tweezers:
                    Line(v,new Vector2(.22f,.10f),new Vector2(.55f,.9f),.10f,color);Line(v,new Vector2(.85f,.10f),new Vector2(.55f,.9f),.10f,color);
                    Line(v,new Vector2(.22f,.1f),new Vector2(.33f,.08f),.05f,Color.white);Line(v,new Vector2(.85f,.1f),new Vector2(.74f,.08f),.05f,Color.white);break;
                case Shape.Spray:
                    Quad(v,.25f,.04f,.8f,.69f,new Color(.64f,.85f,.84f));Quad(v,.37f,.7f,.67f,.85f,color);Quad(v,.55f,.79f,.9f,.92f,color);
                    Quad(v,.34f,.25f,.71f,.52f,new Color(.95f,.94f,.84f));Quad(v,.49f,.27f,.56f,.49f,color);Quad(v,.4f,.35f,.65f,.42f,color);break;
            }
        }
        Vector2 P(Vector2 p){Rect r=rectTransform.rect;return new Vector2(r.x+p.x*r.width,r.y+p.y*r.height);}
        void Triangle(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Color col)
        {int i=v.currentVertCount;v.AddVert(P(a),col,Vector2.zero);v.AddVert(P(b),col,Vector2.zero);v.AddVert(P(c),col,Vector2.zero);v.AddTriangle(i,i+1,i+2);}
        void Polygon(VertexHelper v,Color c,params Vector2[] points)
        {for(int i=1;i<points.Length-1;i++)Triangle(v,points[0],points[i],points[i+1],c);}
        void Quad(VertexHelper v,float x,float y,float x2,float y2,Color c)
        {Triangle(v,new Vector2(x,y),new Vector2(x,y2),new Vector2(x2,y2),c);Triangle(v,new Vector2(x,y),new Vector2(x2,y2),new Vector2(x2,y),c);}
        void Circle(VertexHelper v,float x,float y,float rx,float ry,Color c)
        {for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Triangle(v,new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),new Vector2(x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry),c);}}
        void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Triangle(v,a+n,b+n,b-n,c);Triangle(v,a+n,b-n,a-n,c);}
    }
}
