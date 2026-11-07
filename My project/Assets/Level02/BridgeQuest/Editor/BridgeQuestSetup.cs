using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Level02.BridgeMission;

public static class BridgeQuestSetup
{
    const string Folder="Assets/Level02/BridgeQuest/Materials";
    const string RootName="Level02_BridgeQuest";
    [MenuItem("Tools/Level 02/Bridge quest/Configure selected bridge")]
    static void Configure()
    {
        if(Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        GameObject intact=Selection.activeGameObject;
        if(intact==null || !intact.scene.IsValid() || !intact.activeInHierarchy)
        { Debug.LogError("Select the intact Footbridge_1 INSTANCE in the scene Hierarchy."); return; }
        if(GameObject.Find(RootName)!=null)
        { Debug.LogWarning("Bridge quest already exists. Edit its Inspector references instead of creating it again."); return; }
        MeshFilter mesh=intact.GetComponent<MeshFilter>();
        if(mesh==null || mesh.sharedMesh==null || intact.GetComponent<Renderer>()==null)
        { Debug.LogError("Select Footbridge_1 itself, with Mesh Filter and Renderer."); return; }
        Bounds bounds=mesh.sharedMesh.bounds;
        if(bounds.size.x<=0 || bounds.size.z<=0) { Debug.LogError("Invalid bridge mesh bounds."); return; }
        playercontroller player=Object.FindFirstObjectByType<playercontroller>();
        if(player==null) { Debug.LogError("No playercontroller found in the open scene."); return; }
        // Prepara le cartelle prima di creare oggetti: evita una configurazione parziale.
        EnsureFolder(Folder);
        Material wood=MaterialFor("BrokenWood",new Color(.36f,.19f,.08f));
        Material signMat=MaterialFor("WarningWood",new Color(.65f,.40f,.15f));
        Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configure bridge quest");
        try
        {
            GameObject root=NewObject(RootName,null);
            BridgeQuestController quest=Undo.AddComponent<BridgeQuestController>(root);
            BridgeCondition condition=Undo.AddComponent<BridgeCondition>(root);
            quest.player=player; quest.bridge=condition;
            quest.FindMushroomQuest();
            condition.intactBridge=intact;
            bool alongZ=bounds.size.z>=bounds.size.x;
            GameObject broken=CreateBroken(intact,bounds,alongZ,wood,root.transform);
            condition.brokenBridge=broken;

            Vector3 axis=alongZ ? Vector3.forward : Vector3.right;
            Vector3 side=alongZ ? Vector3.right : Vector3.forward;
            float length=alongZ ? bounds.size.z : bounds.size.x;
            Vector3 deck=bounds.center; deck.y=bounds.max.y;
            Vector3 endA=intact.transform.TransformPoint(deck+axis*(length*.5f));
            Vector3 endB=intact.transform.TransformPoint(deck-axis*(length*.5f));
            float direction=(player.transform.position-endA).sqrMagnitude<=(player.transform.position-endB).sqrMagnitude ? 1f : -1f;
            Vector3 bank=direction>0 ? endA : endB;
            Vector3 outward=intact.transform.TransformDirection(axis*direction).normalized;
            Vector3 lateral=intact.transform.TransformDirection(side).normalized;
            Vector3 repairPosition=FindGround(bank+outward*1.5f,intact,broken,player);
            GameObject repair=NewObject("BridgeRepair_POINT_MOVE_TO_BANK",root.transform);
            repair.transform.position=repairPosition;
            quest.bridgeInteraction=repair.transform;

            GameObject template=GameObject.Find("NPC_funghi");
            quest.fisher=CreateNpc("NPC_Fisher_MOVE_TO_RIVERBANK",template,FindGround(repairPosition+lateral*4f,intact,broken,player),root.transform);
            quest.woodcutter=CreateNpc("NPC_Woodcutter_MOVE_INTO_FOREST",template,FindGround(repairPosition+lateral*18f+outward*8f,intact,broken,player),root.transform);
            quest.fisher.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            quest.woodcutter.rotation=Quaternion.LookRotation(-lateral,Vector3.up);

            GameObject board=NewObject("BridgeWarningSign",root.transform);
            board.transform.position=repairPosition+lateral*1.6f;
            board.transform.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            Part("Post",board.transform,new Vector3(0,.7f,0),new Vector3(.10f,1.4f,.1f),signMat,true);
            Part("Board",board.transform,new Vector3(0,1.45f,0),new Vector3(2,.65f,.12f),signMat,true);
            AddText(board.transform,"BRIDGE CLOSED\nRepair needed",new Vector3(0,1.45f,-.08f),Quaternion.identity);
            condition.closedSign=board;
            Undo.RecordObject(intact,"Set initial broken state");
            condition.SetRepaired(false);
            EditorUtility.SetDirty(quest); EditorUtility.SetDirty(condition);
            PrefabUtility.RecordPrefabInstancePropertyModifications(intact);
            EditorSceneManager.MarkSceneDirty(intact.scene);
            Selection.activeGameObject=root;
            Undo.CollapseUndoOperations(group);
            Debug.Log("Bridge quest created. Move both NPCs and the repair point onto reachable ground. Check bridge alignment, then save the scene.",root);
        }
        catch(System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
        }
    }
    static GameObject CreateBroken(GameObject intact,Bounds b,bool alongZ,Material wood,Transform root)
    {
        // Stesso sistema locale del ponte originale, anche con scala non uniforme.
        GameObject broken=NewObject("Bridge_BROKEN",intact.transform.parent);
        broken.transform.localPosition=intact.transform.localPosition;
        broken.transform.localRotation=intact.transform.localRotation;
        broken.transform.localScale=intact.transform.localScale;
        float length=alongZ ? b.size.z : b.size.x;
        float width=alongZ ? b.size.x : b.size.z;
        float thickness=Mathf.Max(.045f,b.size.y*.06f);
        float deckY=b.max.y-thickness*.5f;
        int count=20;
        for(int i=0;i<count;i++)
        {
            float fraction=(i+.5f)/count;
            // Il 60% centrale e realmente mancante: nessun collider di pavimento.
            if(fraction>.20f && fraction<.80f) continue;
            Vector3 pos=b.center; pos.y=deckY;
            if(alongZ) pos.z=b.min.z+length*fraction; else pos.x=b.min.x+length*fraction;
            Vector3 size=alongZ ? new Vector3(width,thickness,length/count*.88f) : new Vector3(length/count*.88f,thickness,width);
            GameObject plank=Part("Remaining board "+i,broken.transform,pos,size,wood,true);
            if(i==3 || i==16) plank.transform.localRotation=Quaternion.Euler(alongZ ? 12 : 0,0,alongZ ? 0 : 12);
        }
        // Pali di sostegno senza trave continua su cui aggirare il buco.
        foreach(float f in new[]{.08f,.18f,.82f,.92f})
        foreach(float side in new[]{-.42f,.42f})
        {
            Vector3 p=b.center; p.y=b.min.y+b.size.y*.5f;
            if(alongZ) { p.z=b.min.z+length*f; p.x=b.center.x+width*side; }
            else { p.x=b.min.x+length*f; p.z=b.center.z+width*side; }
            Part("Damaged support",broken.transform,p,new Vector3(width*.07f,Mathf.Max(.1f,b.size.y-thickness),width*.07f),wood,true);
        }
        // Blocco fisico temporaneo circoscritto al centro del ponte; sparisce riparandolo.
        GameObject block=NewObject("ClosedBridge_COLLIDER",broken.transform);
        BoxCollider collider=Undo.AddComponent<BoxCollider>(block);
        Vector3 worldScale=broken.transform.lossyScale;
        float localHeight=4f/Mathf.Max(.001f,Mathf.Abs(worldScale.y));
        collider.center=new Vector3(b.center.x,b.max.y+localHeight*.5f,b.center.z);
        collider.size=alongZ ? new Vector3(width*1.25f,localHeight,length*.10f) : new Vector3(length*.10f,localHeight,width*1.25f);
        return broken;
    }
    static Transform CreateNpc(string name,GameObject template,Vector3 position,Transform parent)
    {
        GameObject npc=null;
        if(template!=null)
        {
            GameObject source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(template);
            if(source!=null) npc=PrefabUtility.InstantiatePrefab(source) as GameObject;
        }
        if(npc==null)
        {
            npc=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.transform.localScale=new Vector3(.7f,1,.7f);
            // Placeholder chiaramente riconoscibile, sostituibile con un prefab umano.
        }
        Undo.RegisterCreatedObjectUndo(npc,"Create quest NPC");
        npc.name=name; npc.transform.SetParent(parent,true); npc.transform.position=position;
        // Non portare copie della prima missione sui nuovi personaggi.
        foreach(MonoBehaviour component in npc.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(component==null) continue;
            string type=component.GetType().Name;
            if(component is ForestQuest || type=="FindMushroomsGame" || type=="MushroomLearningQuest")
                Undo.DestroyObjectImmediate(component);
        }
        return npc.transform;
    }
    static Vector3 FindGround(Vector3 point,GameObject intact,GameObject broken,playercontroller player)
    {
        RaycastHit[] hits=Physics.RaycastAll(point+Vector3.up*40,Vector3.down,100,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            Transform t=hit.collider.transform;
            if(t.IsChildOf(intact.transform) || t.IsChildOf(broken.transform) || t.IsChildOf(player.transform)) continue;
            string name=t.name.ToLowerInvariant();
            if(name.Contains("water") || name.Contains("river")) continue;
            if(hit.collider is TerrainCollider || name.Contains("ground") || name.Contains("terrain"))
                return new Vector3(point.x,hit.point.y+.03f,point.z);
        }
        return point; // Altezza iniziale del ponte: controllare manualmente se non trova suolo.
    }
    static GameObject NewObject(string name,Transform parent)
    {
        GameObject go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Create bridge quest object");
        if(parent!=null) go.transform.SetParent(parent,false);
        return go;
    }
    static GameObject Part(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool solid)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go,"Create bridge part"); go.name=name;
        go.transform.SetParent(parent,false); go.transform.localPosition=pos; go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!solid) Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void AddText(Transform parent,string caption,Vector3 position,Quaternion rotation)
    {
        GameObject go=NewObject("Sign text",parent); go.transform.localPosition=position;
        // TextMeshPro guarda verso -Z: questo lato e rivolto al giocatore sulla riva.
        go.transform.localRotation=rotation;
        var label=Undo.AddComponent<TMPro.TextMeshPro>(go);
        label.text=caption; label.fontSize=2.4f; label.color=Color.black;
        label.alignment=TMPro.TextAlignmentOptions.Center; label.rectTransform.sizeDelta=new Vector2(1.9f,.6f);
    }
    static Material MaterialFor(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null) return mat;
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null) shader=Shader.Find("Standard");
        if(shader==null) throw new System.InvalidOperationException("No compatible material shader found.");
        mat=new Material(shader); mat.color=color; AssetDatabase.CreateAsset(mat,path); return mat;
    }
    static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/'); string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
            if(!AssetDatabase.IsValidFolder(next)) throw new System.IO.IOException("Cannot create folder "+next);
            current=next;
        }
    }

    [MenuItem("Tools/Level 02/Bridge quest/Apply update to existing quest")]
    static void UpdateExisting()
    {
        if(Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        BridgeQuestController quest=Selection.activeGameObject!=null ? Selection.activeGameObject.GetComponent<BridgeQuestController>() : null;
        if(quest==null) quest=Object.FindFirstObjectByType<BridgeQuestController>();
        if(quest==null) { Debug.LogError("No existing bridge quest found. Configure the bridge first."); return; }
        Undo.RecordObject(quest,"Update existing bridge quest");
        quest.requireMushroomQuest=true;
        quest.FindMushroomQuest();
        quest.minimumLogs=Mathf.Clamp(quest.minimumLogs,3,4);
        quest.maximumLogs=Mathf.Clamp(quest.maximumLogs,quest.minimumLogs,4);
        EditorUtility.SetDirty(quest);
        PrefabUtility.RecordPrefabInstancePropertyModifications(quest);
        EditorSceneManager.MarkSceneDirty(quest.gameObject.scene);
        Selection.activeGameObject=quest.gameObject;
        if(quest.mushroomQuest==null)
            Debug.LogWarning("Assign Mushroom Quest to the NpcDialogue component on NPC_funghi. The bridge quest stays locked until that quest is complete.",quest);
        else Debug.Log("Bridge quest updated. Existing NPC positions and bridge references were preserved. Save the scene.",quest);
    }

    [MenuItem("Tools/Level 02/Bridge quest/Preview repaired bridge")]
    static void PreviewIntact() { Preview(true); }
    [MenuItem("Tools/Level 02/Bridge quest/Preview broken bridge")]
    static void PreviewBroken() { Preview(false); }
    static void Preview(bool repaired)
    {
        if(Application.isPlaying) { Debug.LogWarning("Use preview commands outside Play only."); return; }
        BridgeCondition bridge=Object.FindFirstObjectByType<BridgeCondition>();
        if(bridge==null || !bridge.IsConfigured) return;
        Undo.RecordObject(bridge.intactBridge,"Preview bridge state");
        Undo.RecordObject(bridge.brokenBridge,"Preview bridge state");
        if(bridge.closedSign!=null) Undo.RecordObject(bridge.closedSign,"Preview bridge state");
        bridge.SetRepaired(repaired);
        EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
    }
}
