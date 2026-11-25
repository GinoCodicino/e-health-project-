using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class LowPolyWoodAssets
{
    const string Folder = "Assets/LowPolyWoodGenerated";
    static Material steel, wood, bark, end, bolt;
    [MenuItem("Tools/Low Poly Wood/Generate Both Prefabs")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "LowPolyWoodGenerated");
        steel=Mat("Steel",new Color(.64f,.67f,.69f));
        wood=Mat("Handle",new Color(.65f,.29f,.065f));
        bark=Mat("Bark",new Color(.28f,.14f,.07f));
        end=Mat("CutWood",new Color(.76f,.54f,.27f));
        bolt=Mat("Brass",new Color(.68f,.48f,.23f));
        Saw(); Pile(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Assets generated", "Two prefabs are ready in " + Folder, "OK");
    }
    static Material Mat(string name, Color color)
    {
        string path=Folder+"/"+name+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {
            Shader s=Shader.Find("Universal Render Pipeline/Lit");
            if(s==null) s=Shader.Find("Standard");
            m=new Material(s); AssetDatabase.CreateAsset(m,path);
        }
        m.color=color; return m;
    }
    static Mesh MeshAsset(string name,List<Vector3> vertices,List<int> triangles)
    {
        string path=Folder+"/"+name+".asset";
        Mesh m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(m==null) { m=new Mesh(); AssetDatabase.CreateAsset(m,path); }
        m.Clear(); m.name=name; m.SetVertices(vertices); m.SetTriangles(triangles,0);
        m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    static void Tri(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
    { int n=v.Count; v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2); }
    static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    { Tri(v,t,a,b,c); Tri(v,t,a,c,d); }
    static GameObject Part(GameObject root,string name,Mesh mesh,Material mat)
    {
        GameObject g=new GameObject(name);g.transform.SetParent(root.transform,false);
        g.AddComponent<MeshFilter>().sharedMesh=mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial=mat; return g;
    }
    static Mesh Extrude(string name,Vector2[] p,float depth)
    {
        var v=new List<Vector3>();var t=new List<int>();
        Vector2 center=Vector2.zero; foreach(var q in p) center+=q; center/=p.Length;
        for(int i=0;i<p.Length;i++) {
            Vector2 a=p[i],b=p[(i+1)%p.Length];
            Vector3 af=new Vector3(a.x,a.y,depth/2), bf=new Vector3(b.x,b.y,depth/2);
            Vector3 ab=new Vector3(a.x,a.y,-depth/2),bb=new Vector3(b.x,b.y,-depth/2);
            Tri(v,t,new Vector3(center.x,center.y,depth/2),af,bf);
            Tri(v,t,new Vector3(center.x,center.y,-depth/2),bb,ab);
            Quad(v,t,af,ab,bb,bf);
        }
        return MeshAsset(name,v,t);
    }
    static void BoxHit(GameObject parent,string name,Vector3 center,Vector3 size)
    {
        GameObject g=new GameObject(name);g.transform.SetParent(parent.transform,false);
        BoxCollider c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;
    }
    static void Save(GameObject root,string name)
    { PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root); }
    static void Saw()
    {
        GameObject root=new GameObject("HandSaw");
        // All polygons run counterclockwise in the XY plane.
        var blade=new List<Vector2>();
        blade.Add(new Vector2(.11f,.035f));
        for(int i=0;i<32;i++) {
            float x=.13f+i*.014f;
            blade.Add(new Vector2(x,.035f+i*.0009f));
            blade.Add(new Vector2(x+.007f,.017f+i*.0009f));
        }
        blade.Add(new Vector2(.585f,.070f));blade.Add(new Vector2(.57f,.13f));blade.Add(new Vector2(.11f,.18f));
        // Concave tooth outline: triangulate as a strip, avoiding a polygon fan.
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<blade.Count-3;i++) {
            Vector2 a=blade[i],b=blade[i+1];
            float ya=Mathf.Lerp(.18f,.13f,(a.x-.11f)/.46f),yb=Mathf.Lerp(.18f,.13f,(b.x-.11f)/.46f);
            Vector3 A=new Vector3(a.x,a.y,.0015f),B=new Vector3(b.x,b.y,.0015f);
            Vector3 C=new Vector3(b.x,yb,.0015f),D=new Vector3(a.x,ya,.0015f);
            Quad(v,t,A,B,C,D);Quad(v,t,new Vector3(A.x,A.y,-.0015f),new Vector3(D.x,D.y,-.0015f),new Vector3(C.x,C.y,-.0015f),new Vector3(B.x,B.y,-.0015f));
        }
        for(int i=0;i<blade.Count;i++) {
            Vector2 a=blade[i],b=blade[(i+1)%blade.Count];
            Quad(v,t,new Vector3(a.x,a.y,.0015f),new Vector3(a.x,a.y,-.0015f),new Vector3(b.x,b.y,-.0015f),new Vector3(b.x,b.y,.0015f));
        }
        Part(root,"Blade",MeshAsset("SawBlade",v,t),steel);
        Vector2[] outer={new Vector2(-.07f,.005f),new Vector2(.075f,.005f),new Vector2(.13f,.065f),new Vector2(.12f,.18f),new Vector2(-.08f,.18f),new Vector2(-.095f,.13f)};
        Vector2[] inner={new Vector2(-.035f,.05f),new Vector2(.035f,.05f),new Vector2(.055f,.075f),new Vector2(.04f,.13f),new Vector2(-.025f,.13f),new Vector2(-.045f,.105f)};
        v=new List<Vector3>();t=new List<int>();
        for(int i=0;i<outer.Length;i++) {
            int j=(i+1)%outer.Length;
            Vector3 a=new Vector3(outer[i].x,outer[i].y,.014f),b=new Vector3(outer[j].x,outer[j].y,.014f),c=new Vector3(inner[j].x,inner[j].y,.014f),d=new Vector3(inner[i].x,inner[i].y,.014f);
            Quad(v,t,a,b,c,d);Quad(v,t,new Vector3(d.x,d.y,-.014f),new Vector3(c.x,c.y,-.014f),new Vector3(b.x,b.y,-.014f),new Vector3(a.x,a.y,-.014f));
            Quad(v,t,a,new Vector3(a.x,a.y,-.014f),new Vector3(b.x,b.y,-.014f),b);
            Quad(v,t,d,c,new Vector3(c.x,c.y,-.014f),new Vector3(d.x,d.y,-.014f));
        }
        Part(root,"OpenHandle",MeshAsset("SawHandle",v,t),wood);
        for(int i=0;i<3;i++) {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Rivet";g.transform.SetParent(root.transform,false);
            g.transform.localPosition=new Vector3(.092f,.045f+i*.05f,.016f);g.transform.localScale=new Vector3(.012f,.012f,.008f);
            g.GetComponent<MeshRenderer>().sharedMaterial=bolt;Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        BoxHit(root,"BladeHitbox",new Vector3(.35f,.099f,0),new Vector3(.48f,.16f,.012f));
        BoxHit(root,"HandleTopHitbox",new Vector3(.01f,.155f,0),new Vector3(.21f,.05f,.028f));
        BoxHit(root,"HandleBottomHitbox",new Vector3(.01f,.027f,0),new Vector3(.17f,.045f,.028f));
        BoxHit(root,"HandleBackHitbox",new Vector3(-.065f,.085f,0),new Vector3(.05f,.10f,.028f));
        BoxHit(root,"HandleFrontHitbox",new Vector3(.085f,.09f,0),new Vector3(.07f,.12f,.028f));
        Save(root,"HandSaw");
    }
    static void Pile()
    {
        GameObject root=new GameObject("WoodPile");System.Random rng=new System.Random(42);
        for(int n=0;n<22;n++) {
            int layer=n/7;float radius=.035f+(float)rng.NextDouble()*.035f;
            float length=.35f+(float)rng.NextDouble()*.38f;
            var v=new List<Vector3>();var t=new List<int>();
            var cv=new List<Vector3>();var ct=new List<int>();
            int sides=6;float[] radii=new float[sides];for(int k=0;k<sides;k++)radii[k]=radius*(.86f+(float)rng.NextDouble()*.25f);
            for(int k=0;k<sides;k++) {
                float a=k*Mathf.PI*2/sides,b=(k+1)*Mathf.PI*2/sides;
                Vector3 A=new Vector3(Mathf.Cos(a)*radii[k],-length/2,Mathf.Sin(a)*radii[k]);
                Vector3 B=new Vector3(Mathf.Cos(b)*radii[(k+1)%sides],-length/2,Mathf.Sin(b)*radii[(k+1)%sides]);
                Vector3 C=B+Vector3.up*length,D=A+Vector3.up*length;
                Quad(v,t,A,D,C,B);Tri(cv,ct,Vector3.down*length/2,A,B);Tri(cv,ct,Vector3.up*length/2,C,D);
            }
            GameObject log=new GameObject("Log_"+n);log.transform.SetParent(root.transform,false);
            log.transform.localPosition=new Vector3(((n%7)-3)*.09f, .09f+layer*.10f,((float)rng.NextDouble()-.5f)*.22f);
            log.transform.localRotation=Quaternion.Euler(75+(float)rng.NextDouble()*25,(float)rng.NextDouble()*110-55,(float)rng.NextDouble()*20-10);
            Part(log,"Bark",MeshAsset("LogBark_"+n,v,t),bark);
            Part(log,"CutEnds",MeshAsset("LogEnds_"+n,cv,ct),end);
            // A six-sided convex collision mesh shares the visual log geometry.
            v.AddRange(cv);int offset=v.Count-cv.Count;foreach(int idx in ct)t.Add(idx+offset);
            MeshCollider hit=log.AddComponent<MeshCollider>();hit.sharedMesh=MeshAsset("LogCollision_"+n,v,t);hit.convex=true;
        }
        Save(root,"WoodPile");
    }
}
