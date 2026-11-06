using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RedHouseSetup
{
    private const string Folder = "Assets/FindMushrooms/RedHouse";
    private const string RootName = "RedHouse_Landmark";
    private const string Menu = "Tools/Find Mushrooms/Casa rossa/";

    [MenuItem(Menu + "Crea casa e obiettivo")]
    private static void CreateNearHerbalist() { Create(false); }

    [MenuItem(Menu + "Crea nel punto selezionato")]
    private static void CreateAtSelectedPoint() { Create(true); }

    private static void Create(bool atSelection)
    {
        int undoGroup = -1;
        try
        {
            Scene scene = CheckScene();
            RedHouseLandmark existing = InScene<RedHouseLandmark>(scene);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("Casa rossa", "La casa e gia presente. Puoi spostare RedHouse_Landmark nella scena senza spostare l'erborista.", "OK");
                return;
            }
            GrandmotherForestMemory memory = InScene<GrandmotherForestMemory>(scene);
            if (memory == null) throw new InvalidOperationException("Crea prima il ricordo della nonna nella scena aperta.");
            playercontroller player = memory.player != null ? memory.player : InScene<playercontroller>(scene);
            NpcDialogue npc = memory.herbalist != null ? memory.herbalist : InScene<NpcDialogue>(scene);
            if (player == null || npc == null) throw new InvalidOperationException("Nel ricordo mancano il giocatore o l'erborista.");
            TextAsset story = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Resources/ForestMemory/ForestMemoryStory_RedHouse.json");
            TextAsset geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Editor/RedHouseGeometry.json");
            if (story == null || geometry == null) throw new InvalidOperationException("Importa tutti i file di Casa_Rossa_Mac.unitypackage.");
            Geometry data = JsonUtility.FromJson<Geometry>(geometry.text);
            if (data == null || data.meshes == null || data.palette == null) throw new InvalidOperationException("Dati della casetta incompleti.");

            Vector3 site;
            if (atSelection)
            {
                Transform selected = Selection.activeTransform;
                if (selected == null || selected.gameObject.scene != scene
                    || selected.GetComponentInParent<playercontroller>() != null
                    || selected.GetComponentInParent<NpcDialogue>() != null)
                    throw new InvalidOperationException("Seleziona un oggetto nella radura dove vuoi la casa, per esempio una pietra. L'oggetto selezionato resta al suo posto.");
                if (!TryGround(selected.position, player, npc, out site))
                    throw new InvalidOperationException("Non trovo il terreno sotto il punto selezionato.");
                if (Planar(site, npc.transform.position) < 5.5f || Planar(site, player.transform.position) < 5.5f)
                    throw new InvalidOperationException("Scegli un punto distante almeno sei metri dal giocatore e dall'erborista, per lasciare spazio al passaggio.");
            }
            else if (!FindSite(scene, memory, player, npc, out site))
                throw new InvalidOperationException("Non trovo una radura abbastanza libera vicino all'erborista. Seleziona un oggetto nella radura scelta e usa Casa rossa > Crea nel punto selezionato.");

            string backup = BackupSavedScene(scene);
            EnsureFolder(Folder + "/Generated");
            Material[] materials = Materials(data.palette);
            Mesh[] meshes = new Mesh[data.meshes.Length];
            for (int i = 0; i < meshes.Length; i++) meshes[i] = LoadMesh(data.meshes[i]);
            Quaternion rotation = FaceNpc(site, npc);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Aggiungi casa rossa e obiettivo");
            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Aggiungi casa rossa");
            Undo.RegisterCompleteObjectUndo(root.transform, "Posiziona casa rossa");
            root.transform.SetPositionAndRotation(site, rotation);
            RedHouseLandmark landmark = Undo.AddComponent<RedHouseLandmark>(root);
            Undo.RecordObject(landmark, "Collega obiettivo");
            landmark.memory = memory;
            landmark.player = player;
            landmark.herbalist = npc;
            landmark.previousStory = memory.storyAsset;
            landmark.redHouseStory = story;
            for (int i = 0; i < meshes.Length; i++)
            {
                GameObject part = new GameObject(data.meshes[i].name);
                SceneManager.MoveGameObjectToScene(part, scene);
                Undo.RegisterCreatedObjectUndo(part, "Aggiungi modello casa");
                Undo.SetTransformParent(part.transform, root.transform, "Collega modello casa");
                Undo.RecordObject(part.transform, "Allinea modello casa");
                part.transform.localPosition = Vector3.zero;
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = Vector3.one;
                MeshFilter filter = Undo.AddComponent<MeshFilter>(part);
                filter.sharedMesh = meshes[i];
                MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(part);
                renderer.sharedMaterials = materials;
                if (data.meshes[i].name == "HouseModel")
                {
                    BoxCollider body = Undo.AddComponent<BoxCollider>(part);
                    body.center = new Vector3(0f, 1.53f, 0f);
                    body.size = new Vector3(5.2f, 4.14f, 4.4f);
                }
            }
            // Record this after object creation: RegisterCreatedObjectUndo flushes RecordObject.
            Undo.RecordObject(memory, "Collega ricordo alla casa rossa");
            memory.storyAsset = story;
            EditorUtility.SetDirty(memory);
            EditorUtility.SetDirty(landmark);
            PrefabUtility.RecordPrefabInstancePropertyModifications(memory);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            string detail = string.IsNullOrEmpty(backup) ? "" : "\n\nCopia della scena salvata: " + backup;
            Debug.Log("Casa rossa creata. Ricordo collegato; obiettivo attivo dopo il prologo." + detail, root);
            EditorUtility.DisplayDialog("Casa rossa", "Casa e obiettivo aggiunti. Salva con Cmd+S, premi Play e termina o salta il ricordo. Comparira Reach the red house.\n\nControlla la posizione nella tua scena: puoi spostare RedHouse_Landmark con lo strumento Move. L'erborista resta nella sua posizione." + detail, "OK");
        }
        catch (Exception error)
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Casa rossa", error.Message, "OK");
        }
    }

    [MenuItem(Menu + "Seleziona casa")]
    private static void SelectHouse()
    {
        RedHouseLandmark house = InScene<RedHouseLandmark>(SceneManager.GetActiveScene());
        if (house == null) { EditorUtility.DisplayDialog("Casa rossa", "Crea prima la casa nella scena aperta.", "OK"); return; }
        Selection.activeGameObject = house.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }

    [MenuItem(Menu + "Rimuovi casa e obiettivo")]
    private static void Remove()
    {
        try
        {
            Scene scene = CheckScene();
            RedHouseLandmark house = InScene<RedHouseLandmark>(scene);
            if (house == null) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rimuovi casa rossa e obiettivo");
            GrandmotherForestMemory memory = house.memory;
            if (memory != null && memory.storyAsset == house.redHouseStory)
            {
                Undo.RecordObject(memory, "Ripristina testo precedente del ricordo");
                memory.storyAsset = house.previousStory;
                PrefabUtility.RecordPrefabInstancePropertyModifications(memory);
                EditorUtility.SetDirty(memory);
            }
            Undo.DestroyObjectImmediate(house.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        catch (Exception error) { EditorUtility.DisplayDialog("Casa rossa", error.Message, "OK"); }
    }

    private static Scene CheckScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ferma completamente Play prima di aggiungere o rimuovere la casa.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Esci dalla modifica del prefab e apri la scena del bosco.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Apri prima la scena del livello.");
        return scene;
    }

    private static T InScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T item = root.GetComponentInChildren<T>(true);
            if (item != null) return item;
        }
        return null;
    }

    private static Quaternion FaceNpc(Vector3 site, NpcDialogue npc)
    {
        Vector3 forward = Vector3.ProjectOnPlane(npc.transform.position - site, Vector3.up);
        return Quaternion.LookRotation(forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized, Vector3.up);
    }

    private static bool FindSite(Scene scene, GrandmotherForestMemory memory, playercontroller player, NpcDialogue npc, out Vector3 result)
    {
        Vector3 away = Vector3.ProjectOnPlane(npc.transform.position - player.transform.position, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.01f) away = -npc.transform.forward;
        float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 115f, -115f, 150f, -150f, 180f };
        float[] radii = { 7.5f, 10f, 12.5f, 16f };
        List<Renderer> sceneRenderers = new List<Renderer>();
        foreach (GameObject root in scene.GetRootGameObjects()) sceneRenderers.AddRange(root.GetComponentsInChildren<Renderer>());
        bool found = false;
        float bestScore = float.PositiveInfinity;
        result = Vector3.zero;
        Physics.SyncTransforms();
        foreach (float radius in radii)
        foreach (float angle in angles)
        {
            Vector3 trial = npc.transform.position + Quaternion.Euler(0f, angle, 0f) * away * radius;
            Vector3 ground;
            if (!TryGround(trial, player, npc, out ground)) continue;
            if (Planar(ground, player.transform.position) < 6f || Planar(ground, memory.transform.position) < 4f) continue;
            Quaternion rotation = FaceNpc(ground, npc);
            float lowest = ground.y, highest = ground.y;
            bool supported = true;
            for (int x = -1; x <= 1 && supported; x++)
            for (int z = -1; z <= 1 && supported; z++)
            {
                Vector3 edge;
                if (!TryGround(ground + rotation * new Vector3(x * 2.55f, 0f, z * 2.15f), player, npc, out edge))
                    { supported = false; break; }
                lowest = Mathf.Min(lowest, edge.y);
                highest = Mathf.Max(highest, edge.y);
                if (highest - lowest > 0.50f) supported = false;
            }
            if (!supported) continue;
            ground.y = highest - 0.035f;
            if (!ClearSite(ground, rotation, sceneRenderers, player, npc, memory)) continue;
            float score = radius + Mathf.Abs(angle) * 0.035f;
            Vector3 eye = player.transform.position + Vector3.up * 1.7f;
            Vector3 target = ground + Vector3.up * 3f;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, (target - eye).normalized, Vector3.Distance(eye, target), ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<playercontroller>() == null && hit.collider.GetComponentInParent<NpcDialogue>() == null)
                    { score += 30f; break; }
            if (score >= bestScore) continue;
            bestScore = score;
            result = ground;
            found = true;
        }
        return found;
    }

    private static bool ClearSite(Vector3 site, Quaternion rotation, List<Renderer> renderers,
        playercontroller player, NpcDialogue npc, GrandmotherForestMemory memory)
    {
        Vector3 center = site + Vector3.up * 2.8f;
        Vector3 half = new Vector3(3.65f, 2.25f, 2.8f);
        foreach (Collider obstacle in Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (obstacle is TerrainCollider || obstacle.GetComponentInParent<playercontroller>() == player
                || obstacle.GetComponentInParent<NpcDialogue>() == npc) continue;
            if (obstacle.bounds.max.y <= site.y + 0.55f) continue;
            return false;
        }
        Vector3 r = rotation * Vector3.right, f = rotation * Vector3.forward;
        Vector3 worldHalf = new Vector3(Mathf.Abs(r.x) * half.x + Mathf.Abs(f.x) * half.z, half.y,
            Mathf.Abs(r.z) * half.x + Mathf.Abs(f.z) * half.z);
        Bounds room = new Bounds(center, worldHalf * 2f);
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer
                || renderer.GetComponentInParent<playercontroller>() == player
                || renderer.GetComponentInParent<NpcDialogue>() == npc
                || renderer.transform.IsChildOf(memory.transform)) continue;
            Bounds waterRoom = room;
            waterRoom.center = new Vector3(room.center.x, site.y, room.center.z);
            waterRoom.size = new Vector3(room.size.x, 2f, room.size.z);
            if (Water(renderer.transform) && waterRoom.Intersects(renderer.bounds)) return false;
            if (renderer.bounds.max.y <= site.y + 0.55f) continue;
            // A combined mountain/terrain mesh can have bounds enclosing the entire valley.
            // Its real colliders and sampled ground handle placement; its empty bounds cannot.
            if (Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) > 35f) continue;
            if (room.Intersects(renderer.bounds)) return false;
        }
        return true;
    }

    private static bool TryGround(Vector3 point, playercontroller player, NpcDialogue npc, out Vector3 result)
    {
        result = point;
        float referenceY = npc.transform.position.y;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(point.x, referenceY + 15f, point.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<playercontroller>() != null
                || hit.collider.GetComponentInParent<NpcDialogue>() != null || hit.normal.y < 0.82f
                || Mathf.Abs(hit.point.y - referenceY) > 3f || Water(hit.collider.transform)) continue;
            float score = Mathf.Abs(hit.point.y - referenceY);
            if (hit.collider is TerrainCollider || hit.collider.name.ToLowerInvariant().Contains("ground")) score -= 3f;
            if (score >= best) continue;
            result = hit.point;
            best = score;
            found = true;
        }
        // An explicit water collider above a ground hit prevents placing a cabin in the river.
        if (found)
            foreach (RaycastHit hit in hits)
                if (Water(hit.collider.transform) && hit.point.y >= result.y - 0.05f) return false;
        return found;
    }

    private static bool Water(Transform item)
    {
        for (Transform t = item; t != null; t = t.parent)
        {
            string name = t.name.ToLowerInvariant();
            if (name.Contains("water") || name.Contains("fiume") || name.Contains("river")) return true;
        }
        return false;
    }

    private static float Planar(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

    private static string BackupSavedScene(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return "";
        string project = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(project, scene.path);
        if (!File.Exists(source)) return "";
        string folder = Path.Combine(project, "RedHouse_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_ffffff"));
        Directory.CreateDirectory(folder);
        File.Copy(source, Path.Combine(folder, Path.GetFileName(source)));
        if (File.Exists(source + ".meta")) File.Copy(source + ".meta", Path.Combine(folder, Path.GetFileName(source) + ".meta"));
        return folder;
    }

    private static Material[] Materials(Palette[] palette)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Non trovo lo shader URP Lit o Standard per la casetta.");
        Material[] result = new Material[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            string path = Folder + "/Generated/RedHouse_" + palette[i].name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "RedHouse_" + palette[i].name };
                Color color;
                if (!ColorUtility.TryParseHtmlString(palette[i].color, out color)) color = Color.magenta;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
                AssetDatabase.CreateAsset(material, path);
            }
            result[i] = material;
        }
        return result;
    }

    private static Mesh LoadMesh(MeshData data)
    {
        string path = Folder + "/Generated/RedHouse_" + data.name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        if (data.vertices == null || data.vertices.Length == 0 || data.submeshes == null)
            throw new InvalidOperationException("Geometria incompleta: " + data.name);
        mesh = new Mesh { name = "RedHouse_" + data.name };
        mesh.vertices = data.vertices;
        mesh.subMeshCount = data.submeshes.Length;
        for (int i = 0; i < data.submeshes.Length; i++) mesh.SetTriangles(data.submeshes[i].triangles, i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    [Serializable] private class Geometry { public Palette[] palette; public MeshData[] meshes; }
    [Serializable] private class Palette { public string name; public string color; }
    [Serializable] private class MeshData { public string name; public Vector3[] vertices; public Submesh[] submeshes; }
    [Serializable] private class Submesh { public int[] triangles; }
}
