using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GrandmotherMemorySetup
{
    private const string Folder = "Assets/FindMushrooms/ForestMemory";
    private const string RootName = "GrandmotherMemory_Point";

    [MenuItem("Tools/Find Mushrooms/Ricordo della nonna/Crea apertura vicino al giocatore")]
    private static void CreateOpening()
    {
        int group = -1;
        try
        {
            CheckEditMode();
            if (Object.FindFirstObjectByType<GrandmotherForestMemory>(FindObjectsInactive.Include) != null)
                throw new InvalidOperationException("L'apertura esiste gia. Seleziona GrandmotherMemory_Point nella Hierarchy per spostarla o cambiarne la lingua.");
            GameObject main = GameObject.Find("main_character");
            playercontroller player = main == null ? null : main.GetComponentInChildren<playercontroller>();
            if (player == null) player = Object.FindFirstObjectByType<playercontroller>();
            if (player == null) throw new InvalidOperationException("Apri la scena con il giocatore gia configurato.");
            GameObject npc = GameObject.Find("NPC_funghi");
            NpcDialogue herbalist = npc == null ? null : npc.GetComponent<NpcDialogue>();
            TextAsset story = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Resources/ForestMemory/ForestMemoryStory.json");
            ConfigureTexture(Folder + "/Resources/ForestMemory/MemoryPast.png");
            ConfigureTexture(Folder + "/Resources/ForestMemory/MemoryPresent.png");
            Texture2D past = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Resources/ForestMemory/MemoryPast.png");
            Texture2D present = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Resources/ForestMemory/MemoryPresent.png");
            if (story == null || past == null || present == null)
                throw new InvalidOperationException("Mancano testo o immagini: importa tutti i file di Forest_Memory.unitypackage.");

            EnsureFolder(Folder + "/Generated");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Generated/MemoryStone.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Non trovo uno shader per la pietra.");
                material = new Material(shader); material.name = "MemoryStone";
                Color color = new Color(0.39f, 0.41f, 0.36f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(material, Folder + "/Generated/MemoryStone.mat");
            }
            Mesh rock = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Generated/MemoryStone.asset");
            if (rock == null)
            {
                rock = CreateRock();
                AssetDatabase.CreateAsset(rock, Folder + "/Generated/MemoryStone.asset");
            }

            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Crea ricordo della nonna");
            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Crea punto del ricordo");
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 point = player.transform.position + forward * 3.5f;
            root.transform.position = GroundPoint(point, player, herbalist);
            GrandmotherForestMemory memory = Undo.AddComponent<GrandmotherForestMemory>(root);
            Undo.RecordObject(memory, "Collega apertura");
            memory.player = player;
            memory.herbalist = herbalist;
            memory.storyAsset = story;
            memory.pastIllustration = past;
            memory.presentIllustration = present;
            memory.approachRadius = 2f;
            memory.startAfterTravel = 8f;

            GameObject visual = new GameObject("MemoryStone_Visual");
            Undo.RegisterCreatedObjectUndo(visual, "Crea pietra del ricordo");
            Undo.SetTransformParent(visual.transform, root.transform, "Collega pietra");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            MeshFilter filter = Undo.AddComponent<MeshFilter>(visual);
            filter.sharedMesh = rock;
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(visual);
            renderer.sharedMaterial = material;
            BoxCollider solid = Undo.AddComponent<BoxCollider>(visual);
            solid.center = rock.bounds.center;
            solid.size = rock.bounds.size;
            EditorUtility.SetDirty(memory);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            EditorUtility.DisplayDialog("Ricordo della nonna", "Apertura creata vicino al punto iniziale del giocatore. Salva con Cmd+S e premi Play. Fai pochi passi verso la pietra. Italian Dialogue nell'Inspector attiva il testo italiano. Il ricordo precede il dialogo dell'erborista.", "OK");
        }
        catch (Exception error)
        {
            if (group >= 0) Undo.RevertAllDownToGroup(group);
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Ricordo della nonna", error.Message, "OK");
        }
    }

    [MenuItem("Tools/Find Mushrooms/Ricordo della nonna/Sposta punto sulla pietra selezionata")]
    private static void MoveToSelected()
    {
        try
        {
            CheckEditMode();
            GrandmotherForestMemory memory = Object.FindFirstObjectByType<GrandmotherForestMemory>(FindObjectsInactive.Include);
            Transform selected = Selection.activeTransform;
            if (memory == null) throw new InvalidOperationException("Crea prima l'apertura.");
            if (selected == null || selected == memory.transform || selected.IsChildOf(memory.transform))
                throw new InvalidOperationException("Seleziona una pietra gia presente nella Hierarchy, per esempio Rock_3.");
            if (selected.GetComponentInParent<playercontroller>() != null || selected.GetComponentInParent<NpcDialogue>() != null)
                throw new InvalidOperationException("Seleziona una pietra della scena, non il giocatore o un NPC.");
            Renderer[] renderers = selected.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("L'oggetto selezionato non contiene una pietra visibile.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.RecordObject(memory.transform, "Sposta ricordo");
            memory.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Transform visual = memory.transform.Find("MemoryStone_Visual");
            if (visual != null) { Undo.RecordObject(visual.gameObject, "Usa pietra esistente"); visual.gameObject.SetActive(false); }
            EditorSceneManager.MarkSceneDirty(memory.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = memory.gameObject;
            EditorUtility.DisplayDialog("Ricordo della nonna", "Punto spostato sulla pietra selezionata. La pietra aggiunta dal comando e stata nascosta. Salva con Cmd+S.", "OK");
        }
        catch (Exception error) { EditorUtility.DisplayDialog("Ricordo della nonna", error.Message, "OK"); }
    }

    [MenuItem("Tools/Find Mushrooms/Ricordo della nonna/Rimuovi apertura dalla scena")]
    private static void RemoveOpening()
    {
        try
        {
            CheckEditMode();
            GrandmotherForestMemory memory = Object.FindFirstObjectByType<GrandmotherForestMemory>(FindObjectsInactive.Include);
            if (memory == null) return;
            if (memory.gameObject.name != RootName)
                throw new InvalidOperationException("Il punto e stato rinominato. Rimuovi manualmente il componente GrandmotherForestMemory dall'oggetto che hai scelto.");
            UnityEngine.SceneManagement.Scene scene = memory.gameObject.scene;
            Undo.DestroyObjectImmediate(memory.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Ricordo della nonna", "Apertura rimossa dalla scena. I personaggi e i minigiochi esistenti sono conservati. Salva con Cmd+S.", "OK");
        }
        catch (Exception error) { EditorUtility.DisplayDialog("Ricordo della nonna", error.Message, "OK"); }
    }

    private static void CheckEditMode()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Ferma Play prima di configurare l'apertura.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Esci dalla modifica del prefab e apri la scena del livello.");
    }

    private static Vector3 GroundPoint(Vector3 point, playercontroller player, NpcDialogue npc)
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 100f, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false; float score = float.MaxValue; Vector3 result = point;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<playercontroller>() != null
                || hit.collider.GetComponentInParent<NpcDialogue>() != null || hit.normal.y < 0.35f) continue;
            if (hit.point.y > player.transform.position.y + 2.5f) continue;
            float candidate = Mathf.Abs(hit.point.y - player.transform.position.y);
            if (hit.collider is TerrainCollider || hit.collider.name.ToLowerInvariant().Contains("ground")) candidate -= 2f;
            if (candidate >= score) continue;
            score = candidate; result = hit.point; found = true;
        }
        return found ? result : point;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/'); string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled
            || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 2048;
        if (!changed) return;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static Mesh CreateRock()
    {
        Vector3[] lower = new Vector3[7], upper = new Vector3[7];
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.PI * 2f / 7f;
            lower[i] = new Vector3(Mathf.Cos(angle) * 0.72f, 0.02f, Mathf.Sin(angle) * 0.53f);
            upper[i] = new Vector3(Mathf.Cos(angle) * 0.60f + 0.06f, 0.42f + (i % 2) * 0.025f, Mathf.Sin(angle) * 0.42f);
        }
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
        Vector3 top = new Vector3(0.04f, 0.46f, 0f);
        for (int i = 0; i < 7; i++)
        {
            int j = (i + 1) % 7;
            AddTriangle(vertices, triangles, lower[i], upper[i], upper[j]);
            AddTriangle(vertices, triangles, lower[i], upper[j], lower[j]);
            AddTriangle(vertices, triangles, top, upper[j], upper[i]);
            AddTriangle(vertices, triangles, Vector3.zero, lower[i], lower[j]);
        }
        Mesh mesh = new Mesh(); mesh.name = "MemoryStone";
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
    }
}
