using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Level02.BridgeMission;
using Level02.DeerRescue;

public static class DeerRescueSetup
{
    const string RootName="Level02_DeerRescue";
    const string Folder="Assets/Level02/DeerRescue/Materials";
    [MenuItem("Tools/Level 02/Deer rescue/Configure from existing bridge quest")]
    static void Configure()
    {
        if(Application.isPlaying){Debug.LogWarning("Exit Play mode first.");return;}
        if(Object.FindFirstObjectByType<DeerRescueController>()!=null){Debug.LogWarning("Deer rescue already exists. Edit its references instead of creating it again.");return;}
        BridgeQuestController previous=Object.FindFirstObjectByType<BridgeQuestController>();
        if(previous==null || previous.bridge==null || !previous.bridge.IsConfigured || previous.player==null)
        {Debug.LogError("Configure the bridge quest first and assign its Player.");return;}
        GameObject intact=previous.bridge.intactBridge;
        MeshFilter mesh=intact.GetComponent<MeshFilter>();
        if(mesh==null || mesh.sharedMesh==null || previous.bridgeInteraction==null){Debug.LogError("Bridge mesh or repair point missing.");return;}
        EnsureFolder(Folder);
        Material wood=Mat("Timber",new Color(.35f,.19f,.08f));
        Material fur=Mat("DeerFur",new Color(.55f,.33f,.15f));
        Material dark=Mat("DeerDark",new Color(.16f,.12f,.08f));
        Material bandage=Mat("Bandage",new Color(.94f,.93f,.82f));
        Material sign=Mat("Sign",new Color(.69f,.51f,.28f));
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure deer rescue");
        try
        {
            GameObject root=New(RootName,null);
            var quest=Undo.AddComponent<DeerRescueController>(root);
            quest.player=previous.player;quest.bridgeQuest=previous;quest.bridgeCondition=previous.bridge;
            quest.mushroomQuest=previous.mushroomQuest;
            if(quest.mushroomQuest==null)quest.mushroomQuest=Object.FindFirstObjectByType<ForestQuest>();
            quest.fishingGame=Undo.AddComponent<RiverFishingGame>(root);
            Bounds b=mesh.sharedMesh.bounds;bool alongZ=b.size.z>=b.size.x;
            Vector3 axis=alongZ?Vector3.forward:Vector3.right;
            Vector3 lateral=intact.transform.TransformDirection(alongZ?Vector3.right:Vector3.forward).normalized;
            float length=alongZ?b.size.z:b.size.x;Vector3 deck=b.center;deck.y=b.max.y;
            Vector3 a=intact.transform.TransformPoint(deck+axis*length*.5f),c=intact.transform.TransformPoint(deck-axis*length*.5f);
            bool aIsFar=(a-previous.bridgeInteraction.position).sqrMagnitude>(c-previous.bridgeInteraction.position).sqrMagnitude;
            Vector3 far=aIsFar?a:c;
            Vector3 outward=intact.transform.TransformDirection(axis*(aIsFar?1:-1)).normalized;
            Vector3 encounter=Ground(far+outward*5,previous.player);
            quest.encounterPoint=Point("Encounter_POINT_AFTER_BRIDGE",encounter,root.transform);
            GameObject template=GameObject.Find("NPC_funghi");
            quest.ranger=Npc(template,Ground(far+outward*11+lateral*3,previous.player),quest.encounterPoint);
            quest.ranger.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            quest.fishingPoint=Point("Fishing_POINT_MOVE_TO_SAFE_BANK",Ground(previous.bridgeInteraction.position+lateral*8-outward*2,previous.player),root.transform);
            Transform bench=Point("FishingBench_MOVE_WITH_POINT",quest.fishingPoint.position+lateral*1.8f,quest.fishingPoint);
            bench.rotation=Quaternion.LookRotation(outward,Vector3.up);
            Part("Seat",bench,new Vector3(0,.55f,0),new Vector3(2,.14f,.6f),wood,true);
            Part("Back",bench,new Vector3(0,1,-.26f),new Vector3(2,.55f,.12f),wood,true);
            Part("Leg L",bench,new Vector3(-.75f,.25f,0),new Vector3(.12f,.5f,.55f),wood,true);
            Part("Leg R",bench,new Vector3(.75f,.25f,0),new Vector3(.12f,.5f,.55f),wood,true);
            Transform board=Point("FishingSign",quest.fishingPoint.position-lateral*1.4f,quest.fishingPoint);
            board.rotation=Quaternion.LookRotation(outward,Vector3.up);
            Part("Post",board,new Vector3(0,.65f,0),new Vector3(.12f,1.3f,.12f),wood,true);
            Part("Board",board,new Vector3(0,1.35f,0),new Vector3(1.8f,.6f,.12f),sign,true);
            Text(board,"Fishing spot",new Vector3(0,1.35f,-.08f));

            Vector3 deerPosition=Ground(far+outward*24+lateral*10,previous.player);
            Transform deerRoot=Point("DeerRescue_AREA_MOVE_TO_TRAIL_END",deerPosition,root.transform);
            quest.deerView=Undo.AddComponent<DeerRescueView>(deerRoot.gameObject);
            quest.deerPoint=Point("Deer_INTERACTION_POINT",deerPosition-outward*2,deerRoot);
            Transform pivot=Point("AnimalVisual",deerPosition+Vector3.up*.46f,deerRoot);
            // Una forma low poly di base, sostituibile con un vostro prefab di capriolo.
            Part("Body",pivot,new Vector3(0,.35f,0),new Vector3(1.35f,.65f,.50f),fur,false);
            Part("Neck",pivot,new Vector3(.56f,.75f,0),new Vector3(.25f,.7f,.30f),fur,false);
            Part("Head",pivot,new Vector3(.75f,1.05f,0),new Vector3(.55f,.30f,.30f),fur,false);
            Part("Muzzle",pivot,new Vector3(1.02f,1.0f,0),new Vector3(.18f,.17f,.25f),dark,false);
            Part("Ear L",pivot,new Vector3(.67f,1.30f,-.15f),new Vector3(.13f,.30f,.10f),fur,false);
            Part("Ear R",pivot,new Vector3(.67f,1.30f,.15f),new Vector3(.13f,.30f,.10f),fur,false);
            foreach(float x in new[]{-.46f,.46f})foreach(float z in new[]{-.17f,.17f})
            {
                Part("Leg",pivot,new Vector3(x,-.12f,z),new Vector3(.13f,.55f,.13f),fur,false);
                Part("Hoof",pivot,new Vector3(x,-.41f,z),new Vector3(.17f,.10f,.17f),dark,false);
            }
            Part("Eye L",pivot,new Vector3(.83f,1.10f,-.16f),new Vector3(.06f,.06f,.02f),dark,false);
            Part("Eye R",pivot,new Vector3(.83f,1.10f,.16f),new Vector3(.06f,.06f,.02f),dark,false);
            quest.deerView.animalVisual=pivot;
            GameObject bandageObject=Part("Bandage after rescue",pivot,new Vector3(.46f,-.13f,.17f),new Vector3(.17f,.20f,.17f),bandage,false);
            bandageObject.SetActive(false);quest.deerView.bandageVisual=bandageObject;
            Transform logs=Point("FallenBranches",deerPosition+Vector3.up*.55f,deerRoot);
            for(int i=0;i<3;i++)
            {
                GameObject log=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Undo.RegisterCreatedObjectUndo(log,"Create fallen branch");
                log.name="Branch "+(i+1);log.transform.SetParent(logs,false);
                log.transform.localPosition=new Vector3(0,.20f+i*.13f,(i-1)*.48f);
                log.transform.localRotation=Quaternion.Euler(0,i*13,90);
                log.transform.localScale=new Vector3(.23f,2.2f,.23f);
                log.GetComponent<Renderer>().sharedMaterial=wood;
                Undo.DestroyObjectImmediate(log.GetComponent<Collider>());
            }
            quest.deerView.fallenBranches=logs.gameObject;
            EditorUtility.SetDirty(quest);EditorUtility.SetDirty(quest.deerView);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject=root;Undo.CollapseUndoOperations(group);
            Debug.Log("Deer rescue created. Check encounter, ranger, fishing and deer positions on reachable ground, then save.",root);
        }
        catch(System.Exception e){Undo.RevertAllDownToGroup(group);Debug.LogException(e);}
    }
    [MenuItem("Tools/Level 02/Deer rescue/Test fishing in Play")]
    static void PreviewFishing()
    {
        if(!Application.isPlaying){Debug.LogWarning("Enter Play mode to test fishing.");return;}
        var quest=Object.FindFirstObjectByType<DeerRescueController>();
        if(quest==null || quest.fishingGame==null || quest.player==null){Debug.LogWarning("Configure deer rescue first.");return;}
        if(quest.CurrentStage!=RescueStage.WaitingForCrossing || quest.IsOpen || quest.player.IsGameplayBlocked)
        {Debug.LogWarning("This preview is available only before the rescue starts, with no other UI open.");return;}
        quest.fishingGame.Open(quest.player,null);
        Debug.Log("Fishing preview: this test does not award quest fish.");
    }

    [MenuItem("Tools/Level 02/Deer rescue/Update existing rescue - Elena version")]
    static void UpdateExisting()
    {
        if(Application.isPlaying){Debug.LogWarning("Exit Play mode first.");return;}
        var q=Object.FindFirstObjectByType<DeerRescueController>();
        if(q==null){Debug.LogWarning("Use Configure from existing bridge quest once, then run this update.");return;}
        Undo.RecordObject(q,"Update rescue games");
        if(q.fishingGame==null)q.fishingGame=q.GetComponent<RiverFishingGame>();
        if(q.fishingGame==null)q.fishingGame=Undo.AddComponent<RiverFishingGame>(q.gameObject);
        Undo.RecordObject(q.fishingGame,"Update fishing difficulty");
        q.fishingGame.fishGoal=10;q.fishingGame.timeLimitSeconds=90;
        if(q.releaseGame==null)q.releaseGame=q.GetComponent<ScrewRescueGame>();
        if(q.releaseGame==null)q.releaseGame=Undo.AddComponent<ScrewRescueGame>(q.gameObject);
        if(q.careGame==null)q.careGame=q.GetComponent<BacteriaBattleGame>();
        if(q.careGame==null)q.careGame=Undo.AddComponent<BacteriaBattleGame>(q.gameObject);
        if(q.mushroomQuest==null)q.mushroomQuest=Object.FindFirstObjectByType<ForestQuest>();
        EditorUtility.SetDirty(q);EditorUtility.SetDirty(q.fishingGame);EditorSceneManager.MarkSceneDirty(q.gameObject.scene);
        Selection.activeGameObject=q.gameObject;
        Debug.Log("Rescue updated. Existing positions preserved. Assign Woodcutter Axe if the prop is a separate object.",q);
    }
    [MenuItem("Tools/Level 02/Deer rescue/Test screw puzzle in Play")]
    static void PreviewScrews(){Preview(true);}
    [MenuItem("Tools/Level 02/Deer rescue/Test kit challenge in Play")]
    static void PreviewKit(){Preview(false);}
    static void Preview(bool screws)
    {
        if(!Application.isPlaying){Debug.LogWarning("Enter Play mode first.");return;}
        var q=Object.FindFirstObjectByType<DeerRescueController>();
        if(q==null || q.player==null || q.player.IsGameplayBlocked){Debug.LogWarning("Configure rescue and close other UI first.");return;}
        if(screws)q.releaseGame.Open(q.player,null);else q.careGame.Open(q.player,null);
        Debug.Log("Preview only: no quest progress awarded.");
    }

    static GameObject New(string name,Transform parent)
    {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create deer rescue object");if(parent!=null)go.transform.SetParent(parent,false);return go;}
    static Transform Point(string name,Vector3 world,Transform parent)
    {var t=New(name,parent).transform;t.position=world;return t;}
    static Transform Npc(GameObject template,Vector3 position,Transform parent)
    {
        GameObject go=null;
        if(template!=null)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(template);
            if(source!=null)go=PrefabUtility.InstantiatePrefab(source) as GameObject;
        }
        if(go==null)go=GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Undo.RegisterCreatedObjectUndo(go,"Create ranger");go.name="NPC_Ranger_MOVE_TO_FAR_BANK";
        go.transform.SetParent(parent,true);go.transform.position=position;
        foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true))
            if(component!=null && (component is ForestQuest || component.GetType().Name=="FindMushroomsGame" || component.GetType().Name=="MushroomLearningQuest"))Undo.DestroyObjectImmediate(component);
        return go.transform;
    }
    static Vector3 Ground(Vector3 point,playercontroller player)
    {
        var hits=Physics.RaycastAll(point+Vector3.up*50,Vector3.down,130,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var h in hits)
        {
            if(h.collider.transform.IsChildOf(player.transform))continue;
            string n=h.collider.name.ToLowerInvariant();
            if(h.collider is TerrainCollider || n.Contains("ground") || n.Contains("terrain"))return new Vector3(point.x,h.point.y+.03f,point.z);
        }
        return point;
    }
    static GameObject Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool solid)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(go,"Create rescue prop");go.name=name;
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)Undo.DestroyObjectImmediate(go.GetComponent<Collider>());return go;
    }
    static void Text(Transform parent,string text,Vector3 pos)
    {
        GameObject go=New("Label",parent);go.transform.localPosition=pos;
        var t=Undo.AddComponent<TMPro.TextMeshPro>(go);t.text=text;t.fontSize=3;t.color=Color.black;
        t.alignment=TMPro.TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(1.7f,.55f);
    }
    static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
    }
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
        if(shader==null)throw new System.InvalidOperationException("No compatible shader found for rescue props.");
        mat=new Material(shader);mat.color=color;AssetDatabase.CreateAsset(mat,path);return mat;
    }
}
