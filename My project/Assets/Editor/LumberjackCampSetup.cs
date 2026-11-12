using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mettilo in una cartella chiamata "Editor" (es. Assets/Editor/LumberjackCampSetup.cs).
// Uso: seleziona Rowan nella Hierarchy > Tools > Level 02 > Create lumberjack camp around selected NPC.
// Costruisce con le primitive di Unity un accampamento low poly orientato come Rowan e appoggiato al terreno.
public static class LumberjackCampSetup
{
    const string Root = "Assets/Level02/LumberjackCamp";
    static readonly Color Bark = new Color(.36f,.22f,.11f);
    static readonly Color BarkDark = new Color(.26f,.16f,.08f);
    static readonly Color CutWood = new Color(.78f,.60f,.36f);
    static readonly Color CutWoodDark = new Color(.68f,.50f,.28f);
    static readonly Color Plank = new Color(.55f,.38f,.20f);
    static readonly Color PlankDark = new Color(.38f,.25f,.13f);
    static readonly Color Metal = new Color(.55f,.57f,.60f);
    static readonly Color Stone = new Color(.50f,.52f,.50f);
    static readonly Color Coal = new Color(.10f,.08f,.07f);

    static GameObject camp;
    static Transform npcT;
    static System.Random rng;

    [MenuItem("Tools/Level 02/Create lumberjack camp around selected NPC")]
    static void Create()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        GameObject npc = Selection.activeGameObject;
        if (npc == null || !npc.scene.IsValid()) { Debug.LogError("Select Rowan (the NPC) in the Hierarchy first."); return; }
        string name = "Lumberjack_Camp_"+npc.name;
        if (GameObject.Find(name) != null)
        { Debug.LogWarning("The camp already exists. Delete it first (Tools > Level 02 > Delete lumberjack camp around selected NPC) or move the existing one."); return; }
        Physics.SyncTransforms();
        rng = new System.Random(2024);
        npcT = npc.transform;
        camp = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(camp,"Create lumberjack camp");
        camp.transform.position = npcT.position;
        camp.transform.rotation = Quaternion.Euler(0,npcT.eulerAngles.y,0);

        ChoppingBlock(); Sawhorse(); LogPile(); Firewood(); Campfire();
        Woodshed(); FelledTrunks(); BigStump(); Crates(); Sign(); StrayLogs();

        Selection.activeGameObject = camp;
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Debug.Log("Lumberjack camp created around "+npc.name+". Move or rotate the root '"+name+"' if needed, check that nothing blocks the way to the NPC, then save the scene. Ctrl+Z undoes it.",camp);
    }

    [MenuItem("Tools/Level 02/Delete lumberjack camp around selected NPC")]
    static void Delete()
    {
        if (Application.isPlaying) return;
        GameObject npc = Selection.activeGameObject;
        if (npc == null) { Debug.LogError("Select Rowan first."); return; }
        GameObject old = GameObject.Find("Lumberjack_Camp_"+npc.name);
        if (old == null) { Debug.LogWarning("No camp found for this NPC."); return; }
        Undo.DestroyObjectImmediate(old);
        EditorSceneManager.MarkSceneDirty(npc.scene);
    }

    // ---------- Gruppi di oggetti (posizioni locali: x = destra di Rowan, z = davanti a Rowan) ----------
    static void ChoppingBlock()
    {
        Transform g = Group("ChoppingBlock_Axe",1.8f,1.1f,30f);
        Part("Block",PrimitiveType.Cylinder,g,new Vector3(0,.25f,0),Vector3.zero,new Vector3(.7f,.25f,.7f),Bark,true,false);
        Part("BlockTop",PrimitiveType.Cylinder,g,new Vector3(0,.503f,0),Vector3.zero,new Vector3(.62f,.004f,.62f),CutWood,false,false);
        // Ascia conficcata nel ceppo.
        Part("AxeHandle",PrimitiveType.Cylinder,g,new Vector3(.12f,.83f,0),new Vector3(0,0,-20),new Vector3(.06f,.35f,.06f),Plank,false,false);
        Part("AxeHead",PrimitiveType.Cube,g,new Vector3(.02f,.56f,0),new Vector3(0,0,-20),new Vector3(.05f,.16f,.26f),Metal,false,false);
        for (int i=0;i<28;i++) Chip(g,R(.35f,1.4f));
    }
    static void Sawhorse()
    {
        Transform g = Group("Sawhorse_Log",-2.4f,1.3f,25f);
        foreach (float x in new [] { -.6f, .6f })
        {
            Part("Leg",PrimitiveType.Cube,g,new Vector3(x,.42f,.17f),new Vector3(-12,0,0),new Vector3(.08f,.9f,.08f),PlankDark,false,false);
            Part("Leg",PrimitiveType.Cube,g,new Vector3(x,.42f,-.17f),new Vector3(12,0,0),new Vector3(.08f,.9f,.08f),PlankDark,false,false);
            Part("Rest",PrimitiveType.Cube,g,new Vector3(x,.86f,0),Vector3.zero,new Vector3(.12f,.08f,.5f),PlankDark,false,false);
        }
        Part("LogOnHorse",PrimitiveType.Cylinder,g,new Vector3(0,1.05f,0),new Vector3(0,0,90),new Vector3(.34f,.9f,.34f),Bark,false,false);
        Part("LogEnd",PrimitiveType.Cylinder,g,new Vector3(.905f,1.05f,0),new Vector3(0,0,90),new Vector3(.30f,.004f,.30f),CutWood,false,false);
        Part("CutPiece",PrimitiveType.Cylinder,g,new Vector3(.5f,.12f,.75f),new Vector3(0,20,90),new Vector3(.24f,.12f,.24f),Bark,false,false);
        Part("CutPiece",PrimitiveType.Cylinder,g,new Vector3(-.2f,.12f,.9f),new Vector3(0,-30,90),new Vector3(.22f,.10f,.22f),BarkDark,false,false);
        for (int i=0;i<10;i++) Chip(g,R(.5f,1.2f));
        AddBox(g,new Vector3(0,.6f,0),new Vector3(1.9f,1.2f,.7f));
    }
    static void LogPile()
    {
        Transform g = Group("LogPile",-3.4f,-1.2f,90f);
        int[] rows = { 4, 3, 2, 1 };
        for (int r=0;r<rows.Length;r++)
            for (int i=0;i<rows[r];i++)
            {
                float x = (i-(rows[r]-1)/2f)*.35f, y = .17f+r*.30f;
                Part("Log",PrimitiveType.Cylinder,g,new Vector3(x,y,0),new Vector3(90,0,0),new Vector3(.34f,1.1f,.34f),(i+r)%2 == 0 ? Bark : BarkDark,false,false);
                Part("LogEnd",PrimitiveType.Cylinder,g,new Vector3(x,y,1.105f),new Vector3(90,0,0),new Vector3(.30f,.004f,.30f),CutWood,false,false);
            }
        AddBox(g,new Vector3(0,.6f,0),new Vector3(1.5f,1.2f,2.2f));
    }
    static void Firewood()
    {
        Transform g = Group("FirewoodStack",3.3f,-1.6f,-15f);
        int[] counts = { 7, 7, 6, 6 };
        for (int r=0;r<counts.Length;r++)
            for (int i=0;i<counts[r];i++)
            {
                float x = (i-(counts[r]-1)/2f)*.19f, y = .09f+r*.165f;
                Part("Firewood",PrimitiveType.Cylinder,g,new Vector3(x,y,0),new Vector3(90,0,0),new Vector3(.18f,.3f,.18f),(i+r)%2 == 0 ? Plank : Bark,false,false);
                Part("FirewoodEnd",PrimitiveType.Cylinder,g,new Vector3(x,y,.305f),new Vector3(90,0,0),new Vector3(.16f,.003f,.16f),CutWood,false,false);
            }
        Part("Stake",PrimitiveType.Cube,g,new Vector3(-.82f,.35f,0),Vector3.zero,new Vector3(.08f,.7f,.08f),PlankDark,false,false);
        Part("Stake",PrimitiveType.Cube,g,new Vector3(.82f,.35f,0),Vector3.zero,new Vector3(.08f,.7f,.08f),PlankDark,false,false);
        AddBox(g,new Vector3(0,.35f,0),new Vector3(1.5f,.7f,.6f));
    }
    static void Campfire()
    {
        Transform g = Group("Campfire",1.3f,-2.6f,0f);
        Part("Coals",PrimitiveType.Cylinder,g,new Vector3(0,.015f,0),Vector3.zero,new Vector3(.7f,.015f,.7f),Coal,false,false);
        for (int i=0;i<8;i++)
        {
            float a = i*Mathf.PI/4f;
            Part("Stone",PrimitiveType.Cube,g,new Vector3(Mathf.Cos(a)*.5f,.07f,Mathf.Sin(a)*.5f),new Vector3(0,R(0,360),0),new Vector3(.22f,.14f,.18f),Stone,false,false);
        }
        for (int i=0;i<3;i++)
            Part("FireLog",PrimitiveType.Cylinder,g,new Vector3(0,.13f,0),new Vector3(0,i*60f+10f,90),new Vector3(.14f,.42f,.14f),Bark,false,false);
        // Fiamma low poly (materiale Unlit, sempre luminoso).
        Part("Flame",PrimitiveType.Cube,g,new Vector3(0,.35f,0),new Vector3(0,45,10),new Vector3(.22f,.5f,.22f),new Color(1f,.5f,.1f),false,true);
        Part("FlameInner",PrimitiveType.Cube,g,new Vector3(0,.42f,0),new Vector3(0,10,-8),new Vector3(.13f,.38f,.13f),new Color(1f,.85f,.25f),false,true);
        GameObject lightObject = new GameObject("Campfire_Light");
        lightObject.transform.SetParent(g,false);
        lightObject.transform.localPosition = new Vector3(0,.8f,0);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f,.6f,.25f);
        light.intensity = 2f; light.range = 8f; light.shadows = LightShadows.None;
        // Tronchi per sedersi.
        Stump(g,1.1f,.6f,.22f,.38f);
        Stump(g,-1.0f,.8f,.20f,.34f);
        Stump(g,.1f,-1.1f,.20f,.36f);
    }
    static void Woodshed()
    {
        Transform g = Group("Woodshed",0f,-6.0f,0f);
        foreach (float x in new [] { -1.6f, 1.6f })
        {
            Part("PostFront",PrimitiveType.Cylinder,g,new Vector3(x,1.15f,1.1f),Vector3.zero,new Vector3(.28f,1.15f,.28f),Bark,true,false);
            Part("PostRear",PrimitiveType.Cylinder,g,new Vector3(x,1.4f,-1.1f),Vector3.zero,new Vector3(.28f,1.4f,.28f),Bark,true,false);
        }
        Part("Roof",PrimitiveType.Cube,g,new Vector3(0,2.62f,0),new Vector3(12.8f,0,0),new Vector3(3.8f,.12f,2.9f),Plank,false,false);
        Part("BackWall",PrimitiveType.Cube,g,new Vector3(0,1.2f,-1.12f),Vector3.zero,new Vector3(3.4f,2.2f,.12f),PlankDark,true,false);
        for (int i=0;i<7;i++)
            Part("WallPlank",PrimitiveType.Cube,g,new Vector3(-1.5f+i*.5f,1.2f,-1.05f),Vector3.zero,new Vector3(.46f,2.1f,.04f),i%2 == 0 ? Plank : PlankDark,false,false);
        // Legna accatastata sotto la tettoia.
        int[] rows = { 6, 5, 4, 3 };
        for (int r=0;r<rows.Length;r++)
            for (int i=0;i<rows[r];i++)
            {
                float x = -.7f+(i-(rows[r]-1)/2f)*.28f, y = .13f+r*.24f;
                Part("ShedLog",PrimitiveType.Cylinder,g,new Vector3(x,y,-.3f),new Vector3(90,0,0),new Vector3(.26f,.7f,.26f),(i+r)%2 == 0 ? Bark : BarkDark,false,false);
                Part("ShedLogEnd",PrimitiveType.Cylinder,g,new Vector3(x,y,.405f),new Vector3(90,0,0),new Vector3(.23f,.003f,.23f),CutWood,false,false);
            }
        AddBox(g,new Vector3(-.7f,.5f,-.3f),new Vector3(1.8f,1f,1.4f));
    }
    static void FelledTrunks()
    {
        Transform g = Group("FelledTrunks",-4.4f,2.6f,70f);
        Part("Trunk",PrimitiveType.Cylinder,g,new Vector3(0,.32f,0),new Vector3(0,0,90),new Vector3(.64f,2.1f,.64f),Bark,true,false);
        Part("TrunkCut",PrimitiveType.Cylinder,g,new Vector3(2.105f,.32f,0),new Vector3(0,0,90),new Vector3(.58f,.004f,.58f),CutWood,false,false);
        for (int i=0;i<3;i++)
            Part("BranchStub",PrimitiveType.Cylinder,g,new Vector3(-1.2f+i*1.0f,.52f,i%2 == 0 ? .22f : -.22f),new Vector3(i%2 == 0 ? 35f : -35f,0,0),new Vector3(.1f,.25f,.1f),Bark,false,false);
        Part("Trunk2",PrimitiveType.Cylinder,g,new Vector3(.2f,.26f,.75f),new Vector3(0,-8,90),new Vector3(.52f,1.7f,.52f),BarkDark,true,false);
        Part("Trunk2Cut",PrimitiveType.Cylinder,g,new Vector3(1.883f,.26f,.986f),new Vector3(0,-8,90),new Vector3(.47f,.004f,.47f),CutWood,false,false);
    }
    static void BigStump()
    {
        Transform g = Group("BigStump",4.1f,2.4f,0f);
        Part("Stump",PrimitiveType.Cylinder,g,new Vector3(0,.275f,0),Vector3.zero,new Vector3(1.2f,.275f,1.2f),Bark,true,false);
        Part("Rings",PrimitiveType.Cylinder,g,new Vector3(0,.551f,0),Vector3.zero,new Vector3(1.1f,.003f,1.1f),CutWood,false,false);
        Part("Rings",PrimitiveType.Cylinder,g,new Vector3(0,.553f,0),Vector3.zero,new Vector3(.86f,.003f,.86f),CutWoodDark,false,false);
        Part("Rings",PrimitiveType.Cylinder,g,new Vector3(0,.555f,0),Vector3.zero,new Vector3(.58f,.003f,.58f),CutWood,false,false);
        for (int i=0;i<4;i++)
        {
            float a = i*Mathf.PI/2f+.4f;
            Part("Root",PrimitiveType.Cube,g,new Vector3(Mathf.Cos(a)*.62f,.07f,Mathf.Sin(a)*.62f),new Vector3(0,-a*Mathf.Rad2Deg,15),new Vector3(.5f,.12f,.16f),Bark,false,false);
        }
        for (int i=0;i<15;i++) Chip(g,R(.7f,1.6f));
    }
    static void Crates()
    {
        Transform g = Group("Crates_Barrel",-1.9f,-3.4f,15f);
        Part("Crate",PrimitiveType.Cube,g,new Vector3(0,.275f,0),Vector3.zero,new Vector3(.7f,.55f,.7f),Plank,true,false);
        Part("CrateTop",PrimitiveType.Cube,g,new Vector3(0,.575f,0),Vector3.zero,new Vector3(.74f,.05f,.74f),PlankDark,false,false);
        Part("CrateSmall",PrimitiveType.Cube,g,new Vector3(.05f,.82f,0),new Vector3(0,30,0),new Vector3(.5f,.4f,.5f),Plank,false,false);
        Part("Barrel",PrimitiveType.Cylinder,g,new Vector3(.95f,.42f,.1f),Vector3.zero,new Vector3(.55f,.42f,.55f),PlankDark,true,false);
        Part("BarrelRing",PrimitiveType.Cylinder,g,new Vector3(.95f,.2f,.1f),Vector3.zero,new Vector3(.57f,.03f,.57f),Metal,false,false);
        Part("BarrelRing",PrimitiveType.Cylinder,g,new Vector3(.95f,.64f,.1f),Vector3.zero,new Vector3(.57f,.03f,.57f),Metal,false,false);
        Part("BarrelLid",PrimitiveType.Cylinder,g,new Vector3(.95f,.841f,.1f),Vector3.zero,new Vector3(.5f,.004f,.5f),CutWood,false,false);
    }
    static void Sign()
    {
        Transform g = Group("Sign",-1.6f,4.3f,0f);
        Part("Post",PrimitiveType.Cube,g,new Vector3(0,.8f,0),Vector3.zero,new Vector3(.13f,1.6f,.13f),PlankDark,false,false);
        Part("Board",PrimitiveType.Cube,g,new Vector3(0,1.65f,0),Vector3.zero,new Vector3(2f,.65f,.12f),Plank,false,false);
        GameObject label = new GameObject("Label");
        label.transform.SetParent(g,false);
        label.transform.localPosition = new Vector3(0,1.65f,.07f);
        TMPro.TextMeshPro text = label.AddComponent<TMPro.TextMeshPro>();
        text.text = "LUMBERJACK CAMP"; text.fontSize = 3; text.color = Color.black;
        text.alignment = TMPro.TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(1.9f,.6f);
        // Secondo lato per leggere il cartello da entrambe le direzioni.
        GameObject reverse = Object.Instantiate(label,g);
        reverse.name = "LabelReverse";
        reverse.transform.localPosition = new Vector3(0,1.65f,-.07f);
        reverse.transform.localRotation = Quaternion.Euler(0,180,0);
    }
    static void StrayLogs()
    {
        float[,] spots = { {-5.4f,-3.4f}, {5.6f,-.6f}, {4.8f,-3.9f}, {-2.6f,6.0f}, {2.9f,5.2f}, {-5.8f,.6f} };
        for (int i=0;i<spots.GetLength(0);i++)
        {
            Transform g = Group("StrayLog",spots[i,0],spots[i,1],R(0,360));
            Part("Log",PrimitiveType.Cylinder,g,new Vector3(0,.13f,0),new Vector3(0,0,90),new Vector3(.26f,R(.35f,.6f),.26f),i%2 == 0 ? Bark : BarkDark,false,false);
            for (int c=0;c<5;c++) Chip(g,R(.3f,.9f));
        }
    }

    // ---------- Funzioni di supporto ----------
    static float R(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }

    // Gruppo posizionato rispetto a Rowan e appoggiato al terreno (raggio verso il basso).
    static Transform Group(string name,float x,float z,float yaw)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(camp.transform,false);
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
            if (h.transform.IsChildOf(camp.transform) || h.transform.IsChildOf(npcT)) continue;
            if (h.distance < bestDistance) { bestDistance = h.distance; best = h.point.y; }
        }
        return best;
    }
    static void Chip(Transform g,float distance)
    {
        float a = R(0,Mathf.PI*2f);
        Part("Chip",PrimitiveType.Cube,g,new Vector3(Mathf.Cos(a)*distance,.02f,Mathf.Sin(a)*distance),new Vector3(0,R(0,360),R(-8,8)),
            new Vector3(R(.12f,.24f),.03f,R(.06f,.11f)),rng.Next(0,2) == 0 ? CutWood : Bark,false,false);
    }
    static void Stump(Transform g,float x,float z,float radius,float height)
    {
        Part("Stump",PrimitiveType.Cylinder,g,new Vector3(x,height/2f,z),Vector3.zero,new Vector3(radius*2f,height/2f,radius*2f),Bark,false,false);
        Part("StumpTop",PrimitiveType.Cylinder,g,new Vector3(x,height+.002f,z),Vector3.zero,new Vector3(radius*1.8f,.003f,radius*1.8f),CutWood,false,false);
    }
    static void AddBox(Transform g,Vector3 center,Vector3 size)
    {
        BoxCollider box = g.gameObject.AddComponent<BoxCollider>();
        box.center = center; box.size = size;
    }
    static GameObject Part(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 euler,Vector3 scale,Color color,bool collider,bool unlit)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition = pos; go.transform.localEulerAngles = euler; go.transform.localScale = scale;
        if (!collider) { Collider c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
        go.GetComponent<Renderer>().sharedMaterial = Mat(color,unlit);
        return go;
    }
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Level02")) AssetDatabase.CreateFolder("Assets","Level02");
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Level02","LumberjackCamp");
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
            if (!unlit && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",0.1f);   // legno opaco, non lucido
            AssetDatabase.CreateAsset(mat,path);
        }
        return mat;
    }
}
