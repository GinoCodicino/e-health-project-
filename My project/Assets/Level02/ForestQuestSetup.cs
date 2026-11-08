using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForestQuestSetup
{
    const string Root = "Assets/Level02/MushroomQuest";
    [MenuItem("Tools/Level 02/Configure selected mushroom NPC")]
    static void Configure()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        GameObject npc = Selection.activeGameObject;
        if (npc == null || !npc.scene.IsValid()) { Debug.LogError("Select NPC_funghi in the Hierarchy first."); return; }
        NpcDialogue quest = npc.GetComponent<NpcDialogue>();
        if (quest == null) quest = Undo.AddComponent<NpcDialogue>(npc);
        Undo.RecordObject(quest,"Configure forest quest");
        quest.enabled = true;
        quest.player = Object.FindFirstObjectByType<playercontroller>();
        foreach (MonoBehaviour old in npc.GetComponents<MonoBehaviour>())
            if (old != null && (old.GetType().Name == "FindMushroomsGame" || old.GetType().Name == "MushroomLearningQuest"))
            { Undo.RecordObject(old,"Disable old minigame"); old.enabled = false; }
        if (quest.dialoguePanel != null)
        { Undo.RecordObject(quest.dialoguePanel,"Hide old dialogue"); quest.dialoguePanel.SetActive(false); }
        // Il tetto si ripara davanti alla casa; l'area di ricerca si crea con il menu "Create roof search area and repair point".
        if (quest.roofSearchArea == null) Debug.LogWarning("Run Tools > Level 02 > Create roof search area and repair point with the NPC selected.");
        EditorUtility.SetDirty(quest);
        PrefabUtility.RecordPrefabInstancePropertyModifications(quest);
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Selection.activeGameObject = npc;
        Debug.Log("Quest configured. Now create the roof search area, then save the scene.",npc);
    }

    [MenuItem("Tools/Level 02/Create red house behind selected NPC")]
    static void House()
    {
        if(Application.isPlaying) return;
        GameObject npc=Selection.activeGameObject;
        if(npc == null || !npc.scene.IsValid()) { Debug.LogError("Select the NPC in the Hierarchy."); return; }
        string name="Quest_RedHouse_"+npc.name;
        if(GameObject.Find(name)!=null) { Debug.LogWarning("House already exists. Move the existing house instead."); return; }
        GameObject root=new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root,"Create red house");
        root.transform.position=npc.transform.position-npc.transform.forward*7;
        root.transform.rotation=npc.transform.rotation;
        // Costruzione low poly di base: orientamento e altezza vanno controllati nella scena.
        Part("Red walls",root.transform,new Vector3(0,1.5f,0),new Vector3(4,3,3.4f),new Color(.65f,.10f,.07f));
        GameObject roof=Part("Roof left",root.transform,new Vector3(-1.05f,3.35f,0),new Vector3(2.6f,.22f,4),new Color(.19f,.13f,.10f));
        roof.transform.localRotation=Quaternion.Euler(0,0,30);
        roof=Part("Roof right",root.transform,new Vector3(1.05f,3.35f,0),new Vector3(2.6f,.22f,4),new Color(.19f,.13f,.10f));
        roof.transform.localRotation=Quaternion.Euler(0,0,-30);
        Part("Door",root.transform,new Vector3(0,1,1.72f),new Vector3(.9f,2,.08f),new Color(.19f,.10f,.04f));
        Part("Window",root.transform,new Vector3(1.2f,1.9f,1.73f),new Vector3(.7f,.7f,.08f),new Color(.45f,.73f,.80f));
        GameObject sign=new GameObject("Sign_MOVE_TO_PATH");
        sign.transform.SetParent(root.transform,false); sign.transform.localPosition=new Vector3(3,0,5);
        Part("Post",sign.transform,new Vector3(0,.8f,0),new Vector3(.13f,1.6f,.13f),new Color(.25f,.14f,.06f));
        Part("Board",sign.transform,new Vector3(0,1.65f,0),new Vector3(2,.65f,.12f),new Color(.75f,.58f,.34f));
        GameObject label=new GameObject("red house"); label.transform.SetParent(sign.transform,false);
        label.transform.localPosition=new Vector3(0,1.65f,.08f);
        TMPro.TextMeshPro text=label.AddComponent<TMPro.TextMeshPro>();
        text.text="red house"; text.fontSize=4; text.color=Color.black;
        text.alignment=TMPro.TextAlignmentOptions.Center; text.rectTransform.sizeDelta=new Vector2(1.9f,.6f);
        // Secondo lato per leggere il cartello da entrambe le direzioni.
        GameObject reverse=Object.Instantiate(label,sign.transform); reverse.name="red house reverse";
        reverse.transform.localPosition=new Vector3(0,1.65f,-.08f); reverse.transform.localRotation=Quaternion.Euler(0,180,0);
        Selection.activeGameObject=root;
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Debug.Log("House created. Check ground contact, NPC clearance and sign position before saving.");
    }

    [MenuItem("Tools/Level 02/Create roof search area and repair point")]
    static void RoofSetup()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        GameObject npc = Selection.activeGameObject;
        if (npc == null || !npc.scene.IsValid()) { Debug.LogError("Select NPC_funghi in the Hierarchy first."); return; }
        NpcDialogue quest = npc.GetComponent<NpcDialogue>();
        if (quest == null) { Debug.LogError("The selected object has no NpcDialogue component."); return; }
        Undo.RecordObject(quest,"Configure roof quest");
        GameObject house = GameObject.Find("Quest_RedHouse_"+npc.name);
        if (house == null) Debug.LogWarning("Red house not found: create it first or assign House Point manually.");
        else if (quest.housePoint == null)
        {
            Transform point = house.transform.Find("Quest_RoofRepair_POINT");
            if (point == null)
            {
                GameObject p = new GameObject("Quest_RoofRepair_POINT");
                Undo.RegisterCreatedObjectUndo(p,"Create roof repair point");
                p.transform.SetParent(house.transform,false);
                p.transform.localPosition = new Vector3(0,0,4.2f);   // davanti alla porta
                point = p.transform;
            }
            quest.housePoint = point;
        }
        if (quest.roofSearchArea == null)
        {
            GameObject area = new GameObject("Quest_RoofSearchArea_MOVE_ME");
            Undo.RegisterCreatedObjectUndo(area,"Create roof search area");
            Transform basis = house != null ? house.transform : npc.transform;
            // Posizione di partenza davanti alla casa, di lato: va trascinata dal lato opposto del path.
            Vector3 pos = basis.position+basis.forward*6+npc.transform.right*12;
            RaycastHit hit;
            if (Physics.Raycast(pos+Vector3.up*60,Vector3.down,out hit,200f)) pos = hit.point;
            area.transform.position = pos;
            quest.roofSearchArea = area.transform;
            Selection.activeGameObject = area;
        }
        EditorUtility.SetDirty(quest);
        PrefabUtility.RecordPrefabInstancePropertyModifications(quest);
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Debug.Log("Roof area created. Move Quest_RoofSearchArea_MOVE_ME in front of the house, on the opposite side of the path (the orange circle shows it when the NPC is selected), then save the scene.");
    }
    static GameObject Part(string name,Transform parent,Vector3 pos,Vector3 size,Color color)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
        go.transform.SetParent(parent,false); go.transform.localPosition=pos; go.transform.localScale=size;
        string folder=Root+"/Materials";
        if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root,"Materials");
        string path=folder+"/Color_"+ColorUtility.ToHtmlStringRGB(color)+".mat";
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null) shader=Shader.Find("Standard");
            mat=new Material(shader); mat.color=color; AssetDatabase.CreateAsset(mat,path);
        }
        go.GetComponent<Renderer>().sharedMaterial=mat;
        return go;
    }
}
