using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mettilo in una cartella chiamata "Editor" (es. Assets/Editor/MushroomNpcMakeover.cs).
// Serve anche MushroomNpcCute.cs, fuori dalle cartelle Editor.
// Uso: seleziona NPC_funghi nella Hierarchy > Tools > Level 02 > Make mushroom NPC cuter.
public static class MushroomNpcMakeover
{
    const string Root = "Assets/Level02/MushroomNpc";
    static readonly Color Red = new Color(.80f,.18f,.14f);
    static readonly Color Orange = new Color(.90f,.52f,.15f);
    static readonly Color Purple = new Color(.62f,.36f,.66f);
    static readonly Color Tan = new Color(.78f,.62f,.42f);
    static readonly Color Cream = new Color(.96f,.90f,.74f);
    static readonly Color White = new Color(.98f,.96f,.90f);
    static readonly Color Wicker = new Color(.72f,.52f,.26f);
    static readonly Color WickerDark = new Color(.48f,.32f,.15f);
    static readonly Color Plank = new Color(.62f,.44f,.24f);
    static readonly Color PlankDark = new Color(.38f,.25f,.13f);
    static readonly Color Glow = new Color(1f,.93f,.55f);

    static Transform npcT;
    static GameObject extras;
    static System.Random rng;
    static Mesh blob;

    [MenuItem("Tools/Level 02/Make mushroom NPC cuter")]
    static void Make()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        GameObject npc = Selection.activeGameObject;
        if (npc == null || !npc.scene.IsValid()) { Debug.LogError("Select NPC_funghi in the Hierarchy first."); return; }
        string extrasName = "Cute_Extras_"+npc.name;
        if (GameObject.Find(extrasName) != null || npc.GetComponent<MushroomNpcCute>() != null)
        { Debug.LogWarning("This NPC already has the makeover. Remove it first (Tools > Level 02 > Remove mushroom NPC makeover)."); return; }

        Physics.SyncTransforms();
        npcT = npc.transform;
        rng = new System.Random(77);
        blob = BlobMesh();

        float top, bottom, headWidth; Vector3 headCenter;
        Measure(npc,out top,out bottom,out headWidth,out headCenter);
        float height = Mathf.Max(.5f,top-bottom);
        float r = Mathf.Max(headWidth*.65f,height*.12f);         // raggio del cappello

        extras = new GameObject(extrasName);
        Undo.RegisterCreatedObjectUndo(extras,"Mushroom NPC makeover");
        extras.transform.position = npcT.position;
        extras.transform.rotation = Quaternion.Euler(0,npcT.eulerAngles.y,0);

        Transform hat = BuildHat(npc,headCenter,top,r);
        NpcDialogue quest = npc.GetComponent<NpcDialogue>();
        string label = quest != null && !string.IsNullOrEmpty(quest.speakerName) ? quest.speakerName : npc.name;
        Transform plate = BuildNameplate(label,top+r*.75f+height*.12f,height*.45f);
        Basket();
        FairyRing();
        Motes(height);

        MushroomNpcCute cute = Undo.AddComponent<MushroomNpcCute>(npc);
        cute.hat = hat; cute.nameplate = plate;
        EditorUtility.SetDirty(cute);
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Selection.activeGameObject = npc;
        Debug.Log("Makeover done. If the hat does not sit well on the head, select 'MushroomHat' (inside the head bone) and adjust its position or scale. Settings (wobble, look at player) are on the Mushroom Npc Cute component. Ctrl+Z undoes everything.",npc);
    }

    [MenuItem("Tools/Level 02/Remove mushroom NPC makeover")]
    static void Remove()
    {
        if (Application.isPlaying) return;
        GameObject npc = Selection.activeGameObject;
        if (npc == null) { Debug.LogError("Select the NPC first."); return; }
        GameObject old = GameObject.Find("Cute_Extras_"+npc.name);
        if (old != null) Undo.DestroyObjectImmediate(old);
        foreach (string n in new [] { "MushroomHat", "Nameplate" })
            foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == n) { Undo.DestroyObjectImmediate(t.gameObject); break; }
        MushroomNpcCute cute = npc.GetComponent<MushroomNpcCute>();
        if (cute != null) Undo.DestroyObjectImmediate(cute);
        EditorSceneManager.MarkSceneDirty(npc.scene);
    }

    // ---------- Misure del personaggio (nella posa attuale) ----------
    static void Measure(GameObject npc,out float top,out float bottom,out float headWidth,out Vector3 headCenter)
    {
        List<Vector3> pts = new List<Vector3>();
        foreach (SkinnedMeshRenderer smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh baked = new Mesh();
            smr.BakeMesh(baked);
            foreach (Vector3 v in baked.vertices) pts.Add(smr.transform.TransformPoint(v));
            Object.DestroyImmediate(baked);
        }
        foreach (MeshRenderer mr in npc.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds b = mr.bounds;
            for (int i=0;i<8;i++)
                pts.Add(new Vector3(i%2 == 0 ? b.min.x : b.max.x,(i/2)%2 == 0 ? b.min.y : b.max.y,i/4 == 0 ? b.min.z : b.max.z));
        }
        if (pts.Count == 0)
        {
            top = npcT.position.y+1.8f; bottom = npcT.position.y; headWidth = .4f;
            headCenter = new Vector3(npcT.position.x,top,npcT.position.z);
            Debug.LogWarning("No mesh found on the NPC: using default measures.");
            return;
        }
        top = float.MinValue; bottom = float.MaxValue;
        foreach (Vector3 p in pts) { top = Mathf.Max(top,p.y); bottom = Mathf.Min(bottom,p.y); }
        float limit = top-(top-bottom)*.12f;
        Vector3 sum = Vector3.zero; int n = 0;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 p in pts)
        {
            if (p.y < limit) continue;
            sum += p; n++;
            minX = Mathf.Min(minX,p.x); maxX = Mathf.Max(maxX,p.x);
            minZ = Mathf.Min(minZ,p.z); maxZ = Mathf.Max(maxZ,p.z);
        }
        headCenter = n > 0 ? sum/n : npcT.position;
        headWidth = Mathf.Max(maxX-minX,maxZ-minZ);
    }
    static Transform FindHead(Transform root)
    {
        Transform best = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (!n.Contains("head") || n.Contains("end") || n.Contains("top") || n.Contains("nub")) continue;
            if (best == null || t.name.Length < best.name.Length) best = t;
        }
        return best;
    }

    // ---------- Cappello a fungo (si aggancia all'osso della testa) ----------
    static Transform BuildHat(GameObject npc,Vector3 headCenter,float topY,float r)
    {
        Transform head = FindHead(npcT);
        GameObject hat = new GameObject("MushroomHat");
        hat.transform.position = new Vector3(headCenter.x,topY+r*.05f,headCenter.z);
        hat.transform.rotation = Quaternion.Euler(0,npcT.eulerAngles.y,0);
        Blob("Cap",hat.transform,Vector3.zero,Quaternion.identity,new Vector3(2f*r,1.4f*r,2f*r),Red);
        Prim("Gills",PrimitiveType.Cylinder,hat.transform,new Vector3(0,-.3f*r,0),Quaternion.identity,new Vector3(1.92f*r,.03f*r,1.92f*r),Cream);
        for (int i=0;i<6;i++)
        {
            float t = .3f+(i%3)*.28f, p = i*2.4f;
            Vector3 pos = new Vector3(r*Mathf.Sin(t)*Mathf.Cos(p),.7f*r*Mathf.Cos(t),r*Mathf.Sin(t)*Mathf.Sin(p))*.98f;
            Vector3 normal = new Vector3(pos.x/(r*r),pos.y/(.49f*r*r),pos.z/(r*r)).normalized;
            Blob("Spot",hat.transform,pos,Quaternion.FromToRotation(Vector3.up,normal),new Vector3(.26f*r,.13f*r,.26f*r),White);
        }
        Undo.RegisterCreatedObjectUndo(hat,"Mushroom hat");
        hat.transform.SetParent(head != null ? head : npcT,true);
        if (head == null) Debug.LogWarning("Head bone not found: the hat is attached to the NPC root and will not follow head animations.");
        return hat.transform;
    }

    // ---------- Cartellino col nome (sempre rivolto alla camera, vedi MushroomNpcCute) ----------
    static Transform BuildNameplate(string label,float y,float scale)
    {
        GameObject plate = new GameObject("Nameplate");
        plate.transform.position = new Vector3(npcT.position.x,y,npcT.position.z);
        plate.transform.localScale = Vector3.one*scale;
        Prim("Border",PrimitiveType.Cube,plate.transform,new Vector3(0,0,.01f),Quaternion.identity,new Vector3(1.4f,.6f,.04f),PlankDark);
        Prim("Board",PrimitiveType.Cube,plate.transform,Vector3.zero,Quaternion.identity,new Vector3(1.3f,.5f,.05f),Plank);
        GameObject textObject = new GameObject("Name");
        textObject.transform.SetParent(plate.transform,false);
        textObject.transform.localPosition = new Vector3(0,0,-.035f);
        TMPro.TextMeshPro text = textObject.AddComponent<TMPro.TextMeshPro>();
        text.text = label; text.fontSize = 5; text.fontStyle = TMPro.FontStyles.Bold;
        text.color = new Color(.18f,.10f,.04f); text.alignment = TMPro.TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(1.2f,.45f);
        plate.transform.SetParent(npcT,true);
        Undo.RegisterCreatedObjectUndo(plate,"Nameplate");
        return plate.transform;
    }

    // ---------- Cestino di funghi accanto all'NPC ----------
    static void Basket()
    {
        Transform g = Group("Basket",.95f,.75f,20f);
        Prim("Basket",PrimitiveType.Cylinder,g,new Vector3(0,.15f,0),Quaternion.identity,new Vector3(.5f,.15f,.5f),Wicker);
        Prim("Rim",PrimitiveType.Cylinder,g,new Vector3(0,.3f,0),Quaternion.identity,new Vector3(.54f,.02f,.54f),WickerDark);
        Prim("Weave",PrimitiveType.Cylinder,g,new Vector3(0,.15f,0),Quaternion.identity,new Vector3(.52f,.015f,.52f),WickerDark);
        for (int i=0;i<=8;i++)
        {
            float a = i*Mathf.PI/8f;
            Prim("Handle",PrimitiveType.Cube,g,new Vector3(Mathf.Cos(a)*.22f,.3f+Mathf.Sin(a)*.22f,0),Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90f),new Vector3(.04f,.07f,.04f),WickerDark);
        }
        Mushroom(g,new Vector3(-.1f,.28f,.05f),.22f,Red);
        Mushroom(g,new Vector3(.1f,.28f,-.05f),.2f,Orange);
        Mushroom(g,new Vector3(.02f,.28f,.12f),.16f,Tan);
        Mushroom(g,new Vector3(-.02f,.28f,-.12f),.18f,Purple);
    }

    // ---------- Cerchio di funghi intorno all'NPC ----------
    static void FairyRing()
    {
        Color[] colors = { Red, Orange, Purple, Tan };
        int count = 10;
        for (int i=0;i<count;i++)
        {
            float a = (i+R(-.25f,.25f))*Mathf.PI*2f/count, radius = R(1.5f,1.9f);
            Transform g = Group("RingMushroom",Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,R(0,360));
            Mushroom(g,Vector3.zero,R(.22f,.4f),colors[rng.Next(0,colors.Length)]);
            if (rng.NextDouble() < .45)
                Mushroom(g,new Vector3(R(.15f,.28f),0,R(-.2f,.2f)),R(.12f,.2f),colors[rng.Next(0,colors.Length)]);
        }
    }

    // ---------- Spore luminose che fluttuano ----------
    static void Motes(float height)
    {
        GameObject go = new GameObject("Spore_Motes");
        go.transform.SetParent(extras.transform,false);
        go.transform.localPosition = new Vector3(0,height*.45f,0);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true; main.startLifetime = 4f; main.startSpeed = .08f;
        main.startSize = new ParticleSystem.MinMaxCurve(.05f,.1f); main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -.03f;
        ParticleSystem.EmissionModule emission = ps.emission; emission.rateOverTime = 6f;
        ParticleSystem.ShapeModule shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.6f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,1),new Keyframe(1,0)));
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh; psr.mesh = blob;
        psr.sharedMaterial = Mat(Glow,true);
    }

    // ---------- Funzioni di supporto ----------
    static float R(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }

    // Un fungo piccolo: gambo, cappello a facce piatte e due macchie. "size" = diametro del cappello.
    static void Mushroom(Transform parent,Vector3 pos,float size,Color cap)
    {
        Prim("Stem",PrimitiveType.Cylinder,parent,pos+new Vector3(0,.35f*size,0),Quaternion.identity,new Vector3(.28f*size,.35f*size,.28f*size),Cream);
        Blob("Cap",parent,pos+new Vector3(0,.72f*size,0),Quaternion.identity,new Vector3(size,.6f*size,size),cap);
        Blob("Spot",parent,pos+new Vector3(.14f*size,.98f*size,.05f*size),Quaternion.Euler(0,0,-15),new Vector3(.2f*size,.08f*size,.2f*size),White);
        Blob("Spot",parent,pos+new Vector3(-.12f*size,.95f*size,-.1f*size),Quaternion.Euler(10,0,10),new Vector3(.16f*size,.07f*size,.16f*size),White);
    }
    static Transform Group(string name,float x,float z,float yaw)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(extras.transform,false);
        g.transform.localPosition = new Vector3(x,0,z);
        g.transform.localRotation = Quaternion.Euler(0,yaw,0);
        Vector3 w = g.transform.position;
        g.transform.position = new Vector3(w.x,GroundY(w),w.z);
        return g.transform;
    }
    static float GroundY(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x,npcT.position.y+3f,p.z),Vector3.down,12f,~0,QueryTriggerInteraction.Ignore);
        float best = npcT.position.y, bestDistance = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(extras.transform) || h.transform.IsChildOf(npcT)) continue;
            if (h.distance < bestDistance) { bestDistance = h.distance; best = h.point.y; }
        }
        return best;
    }
    static GameObject Prim(string name,PrimitiveType type,Transform parent,Vector3 localPos,Quaternion localRot,Vector3 scale,Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition = localPos; go.transform.localRotation = localRot; go.transform.localScale = scale;
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);          // niente collider: non deve bloccare il giocatore
        go.GetComponent<Renderer>().sharedMaterial = Mat(color,false);
        return go;
    }
    static GameObject Blob(string name,Transform parent,Vector3 localPos,Quaternion localRot,Vector3 scale,Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent,false);
        go.transform.localPosition = localPos; go.transform.localRotation = localRot; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = blob;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color,false);
        return go;
    }
    // Icosaedro a facce piatte (stile low poly), salvato come asset.
    static Mesh BlobMesh()
    {
        EnsureFolders();
        string path = Root+"/LowPolyBlob.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        float t = (1f+Mathf.Sqrt(5f))/2f;
        Vector3[] v = { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
            new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
            new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        Vector3[] verts = new Vector3[60]; int[] tris = new int[60];
        for (int i=0;i<60;i+=3)
        {
            Vector3 a = v[f[i]].normalized*.5f, b = v[f[i+1]].normalized*.5f, c = v[f[i+2]].normalized*.5f;
            if (Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
            verts[i] = a; verts[i+1] = b; verts[i+2] = c;
            tris[i] = i; tris[i+1] = i+1; tris[i+2] = i+2;
        }
        Mesh m = new Mesh(); m.name = "LowPolyBlob";
        m.vertices = verts; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m,path);
        return m;
    }
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Level02")) AssetDatabase.CreateFolder("Assets","Level02");
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Level02","MushroomNpc");
        if (!AssetDatabase.IsValidFolder(Root+"/Materials")) AssetDatabase.CreateFolder(Root,"Materials");
    }
    static Material Mat(Color color,bool unlit)
    {
        EnsureFolders();
        string path = Root+"/Materials/"+(unlit ? "Unlit_" : "Color_")+ColorUtility.ToHtmlStringRGB(color)+".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader); mat.color = color;
            if (!unlit && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",0.1f);
            AssetDatabase.CreateAsset(mat,path);
        }
        return mat;
    }
}
