using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HerbalistAppearanceTools
{
    private const string Generated = "Assets/FindMushrooms/HerbalistLook/Generated";

    [MenuItem("Tools/Find Mushrooms/Aspetto erborista/Applica verde e accessori")]
    private static void Apply()
    {
        int undoGroup = -1;
        List<string> assets = new List<string>();
        List<Sampler> samplers = new List<Sampler>();
        try
        {
            GameObject npc = GetNpc();
            if (npc.GetComponent<HerbalistAppearance>() != null)
                throw new InvalidOperationException("Aspetto gia applicato. Usa Ripristina aspetto precedente prima di applicarlo di nuovo.");
            TextAsset asset = Resources.Load<TextAsset>("HerbalistGeometry");
            if (asset == null) throw new InvalidOperationException("Manca HerbalistGeometry.json: importa tutti i file del pacchetto.");
            Geometry data = JsonUtility.FromJson<Geometry>(asset.text);
            ValidateGeometry(data);

            SkinnedMeshRenderer renderer = null;
            int headIndex = -1, torsoIndex = -1, leftIndex = -1, rightIndex = -1;
            foreach (SkinnedMeshRenderer candidate in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int h = BoneIndex(candidate, "Head"), b = BoneIndex(candidate, "Spine00");
                if (candidate.sharedMesh != null && h >= 0 && b >= 0)
                {
                    renderer = candidate; headIndex = h; torsoIndex = b;
                    leftIndex = BoneIndex(candidate, "Arm_L"); rightIndex = BoneIndex(candidate, "Arm_R");
                    break;
                }
            }
            if (renderer == null || leftIndex < 0 || rightIndex < 0)
                throw new InvalidOperationException("Non trovo il modello con Head, Spine00, Arm_L e Arm_R dentro NPC_funghi.");
            Mesh original = renderer.sharedMesh;
            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials.Length == 0 || originalMaterials[0] == null)
                throw new InvalidOperationException("Il personaggio non ha un materiale valido.");
            // Menu commands run outside Play: Unity permits editor mesh access without
            // changing the model importer's Read/Write setting.
            BoneWeight[] weights = original.boneWeights;
            Vector2[] uv = original.uv;
            if (weights.Length != original.vertexCount || uv.Length != original.vertexCount)
                throw new InvalidOperationException("Il modello non contiene pesi o coordinate UV compatibili.");
            Vector3[] sourceVertices = original.vertices;
            Matrix4x4[] bindposes = original.bindposes;
            if (bindposes.Length != renderer.bones.Length)
                throw new InvalidOperationException("Mesh e scheletro hanno un numero di articolazioni diverso.");
            Matrix4x4[] skin = new Matrix4x4[bindposes.Length];
            for (int i = 0; i < skin.Length; i++)
                skin[i] = renderer.bones[i].localToWorldMatrix * bindposes[i];
            Vector3[] world = new Vector3[sourceVertices.Length];
            for (int i = 0; i < world.Length; i++)
            {
                BoneWeight weight = weights[i]; Vector3 vertex = sourceVertices[i];
                if (weight.weight0 > 0f) world[i] += skin[weight.boneIndex0].MultiplyPoint3x4(vertex) * weight.weight0;
                if (weight.weight1 > 0f) world[i] += skin[weight.boneIndex1].MultiplyPoint3x4(vertex) * weight.weight1;
                if (weight.weight2 > 0f) world[i] += skin[weight.boneIndex2].MultiplyPoint3x4(vertex) * weight.weight2;
                if (weight.weight3 > 0f) world[i] += skin[weight.boneIndex3].MultiplyPoint3x4(vertex) * weight.weight3;
            }

            List<int>[] rest = new List<int>[original.subMeshCount];
            List<int> shirt = new List<int>();
            List<Vector3> shirtPoints = new List<Vector3>();
            Vector3 headCenter = RegionBounds(world, weights, headIndex, new Frame(Vector3.zero, Quaternion.identity)).center;
            Vector3 eyeDirection = Vector3.zero;
            Vector3 up = npc.transform.up;
            for (int sm = 0; sm < original.subMeshCount; sm++)
            {
                Material material = originalMaterials[Mathf.Min(sm, originalMaterials.Length - 1)];
                if (material == null) throw new InvalidOperationException("Materiale mancante nel personaggio.");
                Sampler sample = new Sampler(material);
                samplers.Add(sample);
                rest[sm] = new List<int>();
                int[] triangles = original.GetTriangles(sm);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Color color = sample.At((uv[a] + uv[b] + uv[c]) / 3f);
                    bool isShirt = Belongs(weights[a], torsoIndex) && Belongs(weights[b], torsoIndex)
                        && Belongs(weights[c], torsoIndex) && IsCloth(color);
                    if (isShirt)
                    {
                        shirt.Add(a); shirt.Add(b); shirt.Add(c);
                        shirtPoints.Add(world[a]); shirtPoints.Add(world[b]); shirtPoints.Add(world[c]);
                    }
                    else { rest[sm].Add(a); rest[sm].Add(b); rest[sm].Add(c); }
                    if (Belongs(weights[a], headIndex) && Belongs(weights[b], headIndex)
                        && Belongs(weights[c], headIndex) && color.r < 0.13f && color.g < 0.13f && color.b < 0.13f)
                    {
                        Vector3 center = (world[a] + world[b] + world[c]) / 3f;
                        float area = Vector3.Cross(world[b] - world[a], world[c] - world[a]).magnitude;
                        eyeDirection += Vector3.ProjectOnPlane(center - headCenter, up) * area;
                    }
                }
            }
            if (shirt.Count < 12) throw new InvalidOperationException("Non riconosco la casacca rossa. Nessun cambiamento al personaggio.");
            Vector3 forward = eyeDirection.sqrMagnitude > 0.0000000001f
                ? eyeDirection.normalized : npc.transform.forward;
            Frame frame = new Frame(npc.transform.position, Quaternion.LookRotation(forward, up));
            Bounds head = RegionBounds(world, weights, headIndex, frame);
            Bounds torso = PointsBounds(shirtPoints, frame);
            Bounds left = RegionBounds(world, weights, leftIndex, frame);
            Bounds right = RegionBounds(world, weights, rightIndex, frame);
            float headSize = head.size.x;
            float bodySize = torso.size.x;
            if (headSize <= 0.0001f || bodySize <= 0.0001f)
                throw new InvalidOperationException("Dimensioni del modello non valide.");

            EnsureFolder(Generated);
            Color green;
            if (!ColorUtility.TryParseHtmlString(data.shirtColor, out green)) throw new InvalidOperationException("Colore casacca non valido.");
            Material shirtMaterial = MakeMaterial(originalMaterials[0], "Herbalist_Olive", null, green);
            Save(shirtMaterial, "Herbalist_Olive.mat", assets);
            Texture2D palette = new Texture2D(data.colors.Length, 1, TextureFormat.RGBA32, false, false);
            palette.name = "Herbalist_Palette";
            palette.filterMode = FilterMode.Point;
            palette.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < data.colors.Length; i++)
            {
                Color color;
                if (!ColorUtility.TryParseHtmlString(data.colors[i], out color)) throw new InvalidOperationException("Palette non valida.");
                palette.SetPixel(i, 0, color);
            }
            palette.Apply();
            Save(palette, "Herbalist_Palette.asset", assets);
            Material accessoryMaterial = MakeMaterial(originalMaterials[0], "Herbalist_Accessories", palette, Color.white);
            Save(accessoryMaterial, "Herbalist_Accessories.mat", assets);
            Mesh recolored = Object.Instantiate(original);
            recolored.name = "Herbalist_Clothes";
            recolored.subMeshCount = original.subMeshCount + 1;
            for (int i = 0; i < rest.Length; i++) recolored.SetTriangles(rest[i], i, false);
            recolored.SetTriangles(shirt, rest.Length, false);
            recolored.bounds = original.bounds;
            Save(recolored, "Herbalist_Clothes.asset", assets);
            Mesh hatMesh = MakeMesh(data.hat);
            Mesh bodyMesh = MakeMesh(data.body);
            Save(hatMesh, "Herbalist_Hat.asset", assets);
            Save(bodyMesh, "Herbalist_ApronPouch.asset", assets);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Vesti erborista");
            Undo.RecordObject(renderer, "Casacca verde dell'erborista");
            HerbalistAppearance state = Undo.AddComponent<HerbalistAppearance>(npc);
            Undo.RecordObject(state, "Memorizza aspetto precedente");
            state.originalRenderer = renderer;
            state.originalMesh = original;
            state.originalMaterials = originalMaterials;
            state.animationDriver = npc.GetComponentInChildren<NpcStorytellerAnimation>(true);
            state.leftHand = renderer.bones[leftIndex];
            state.rightHand = renderer.bones[rightIndex];
            state.leftFistOffset = state.leftHand.InverseTransformPoint(frame.World(left.center));
            state.rightFistOffset = state.rightHand.InverseTransformPoint(frame.World(right.center));
            state.headSize = headSize;

            GameObject hatRoot = NewRoot("Herbalist_Hat", renderer.bones[headIndex],
                frame.World(new Vector3(head.center.x, head.max.y - headSize * 0.025f, head.center.z)), frame.rotation, headSize);
            AddMesh(hatRoot, hatMesh, accessoryMaterial);
            float beltHeight = torso.min.y + torso.size.y * 0.28f;
            GameObject bodyRoot = NewRoot("Herbalist_ApronAndHerbs", renderer.bones[torsoIndex],
                frame.World(new Vector3(torso.center.x, beltHeight, torso.center.z)), frame.rotation, bodySize);
            AddMesh(bodyRoot, bodyMesh, accessoryMaterial);
            state.createdRoots = new[] { hatRoot, bodyRoot };

            float leftSign = Mathf.Sign(left.center.x - head.center.x);
            float rightSign = Mathf.Sign(right.center.x - head.center.x);
            if (leftSign == 0f) leftSign = -1f;
            if (rightSign == 0f) rightSign = 1f;
            float leftRadius = Mathf.Max(left.extents.x, left.extents.y, left.extents.z);
            float rightRadius = Mathf.Max(right.extents.x, right.extents.y, right.extents.z);
            state.headTarget = Marker("Thinking_Hat_Target", hatRoot.transform,
                new Vector3(leftSign * 0.22f, 0.46f + leftRadius / headSize * 0.90f, 0.06f));
            state.liftTarget = Marker("Thinking_Lift_Target", hatRoot.transform,
                new Vector3(leftSign * (0.85f + leftRadius / headSize), 0.46f + leftRadius / headSize + 0.20f, 0.10f));
            Vector3 facePoint = head.center + new Vector3(rightSign * headSize * 0.35f,
                -head.size.y * 0.29f, head.extents.z + rightRadius * 0.95f);
            state.faceTarget = MarkerWorld("Thinking_Face_Target", hatRoot.transform, frame.World(facePoint), frame.rotation);

            Material[] materials = new Material[original.subMeshCount + 1];
            for (int i = 0; i < original.subMeshCount; i++) materials[i] = originalMaterials[Mathf.Min(i, originalMaterials.Length - 1)];
            materials[materials.Length - 1] = shirtMaterial;
            renderer.sharedMesh = recolored;
            renderer.sharedMaterials = materials;
            Record(renderer); Record(state);
            EditorSceneManager.MarkSceneDirty(npc.scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = npc;
            EditorUtility.DisplayDialog("Erborista", "Aspetto applicato a NPC_funghi. Salva con Cmd+S e prova Play. Il cappello segue la testa; grembiule e borsa seguono il busto. La posa pensierosa si adatta al cappello.", "OK");
        }
        catch (Exception error)
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            foreach (string path in assets) AssetDatabase.DeleteAsset(path);
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Erborista", "Impossibile applicare l'aspetto: " + error.Message, "OK");
        }
        finally
        {
            foreach (Sampler sample in samplers) sample.Dispose();
        }
    }

    [MenuItem("Tools/Find Mushrooms/Aspetto erborista/Ripristina aspetto precedente")]
    private static void Restore()
    {
        try
        {
            GameObject npc = GetNpc();
            HerbalistAppearance state = npc.GetComponent<HerbalistAppearance>();
            if (state == null) throw new InvalidOperationException("Non risulta applicato l'aspetto dell'erborista.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Ripristina erborista");
            if (state.originalRenderer != null)
            {
                Undo.RecordObject(state.originalRenderer, "Ripristina modello e materiali");
                state.originalRenderer.sharedMesh = state.originalMesh;
                state.originalRenderer.sharedMaterials = state.originalMaterials;
                Record(state.originalRenderer);
            }
            if (state.createdRoots != null)
                foreach (GameObject root in state.createdRoots) if (root != null) Undo.DestroyObjectImmediate(root);
            Undo.DestroyObjectImmediate(state);
            EditorSceneManager.MarkSceneDirty(npc.scene);
            Undo.CollapseUndoOperations(group);
            EditorUtility.DisplayDialog("Erborista", "Aspetto precedente ripristinato. Salva con Cmd+S.", "OK");
        }
        catch (Exception error) { EditorUtility.DisplayDialog("Erborista", error.Message, "OK"); }
    }

    private static GameObject GetNpc()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Ferma Play prima di cambiare l'aspetto.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Esci dalla modifica del prefab e apri la scena con NPC_funghi.");
        GameObject npc = GameObject.Find("NPC_funghi");
        if (npc == null) throw new InvalidOperationException("Apri la scena che contiene NPC_funghi.");
        if (npc.GetComponent<NpcDialogue>() == null)
            throw new InvalidOperationException("NPC_funghi non ha il dialogo: apri la scena del minigioco gia configurata.");
        return npc;
    }

    private static bool IsCloth(Color color) { return color.r > 0.015f && color.g < color.r * 0.5f && color.b < color.r * 0.5f; }
    private static int BoneIndex(SkinnedMeshRenderer renderer, string name)
    {
        for (int i = 0; i < renderer.bones.Length; i++) if (renderer.bones[i] != null && renderer.bones[i].name == name) return i;
        return -1;
    }
    private static bool Belongs(BoneWeight weight, int bone)
    {
        float sum = (weight.boneIndex0 == bone ? weight.weight0 : 0f)
            + (weight.boneIndex1 == bone ? weight.weight1 : 0f)
            + (weight.boneIndex2 == bone ? weight.weight2 : 0f)
            + (weight.boneIndex3 == bone ? weight.weight3 : 0f);
        return sum > 0.5f;
    }
    private static Bounds RegionBounds(Vector3[] world, BoneWeight[] weights, int bone, Frame frame)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < world.Length; i++) if (Belongs(weights[i], bone)) points.Add(world[i]);
        if (points.Count == 0) throw new InvalidOperationException("Un'articolazione non influenza alcun vertice.");
        return PointsBounds(points, frame);
    }
    private static Bounds PointsBounds(List<Vector3> points, Frame frame)
    {
        Bounds bounds = new Bounds(frame.Local(points[0]), Vector3.zero);
        foreach (Vector3 point in points) bounds.Encapsulate(frame.Local(point));
        return bounds;
    }
    private static GameObject NewRoot(string name, Transform bone, Vector3 position, Quaternion rotation, float scale)
    {
        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Accessori erborista");
        Undo.RecordObject(root.transform, "Posiziona accessori");
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one * scale;
        Undo.SetTransformParent(root.transform, bone, "Collega accessori allo scheletro");
        return root;
    }
    private static void AddMesh(GameObject root, Mesh mesh, Material material)
    {
        MeshFilter filter = Undo.AddComponent<MeshFilter>(root);
        filter.sharedMesh = mesh;
        MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(root);
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
    }
    private static Transform Marker(string name, Transform parent, Vector3 local)
    {
        GameObject marker = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(marker, "Posa erborista");
        Undo.RecordObject(marker.transform, "Posiziona punto posa");
        Undo.SetTransformParent(marker.transform, parent, "Collega punto posa");
        marker.transform.localPosition = local;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one;
        return marker.transform;
    }
    private static Transform MarkerWorld(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        Transform marker = Marker(name, parent, Vector3.zero);
        marker.SetPositionAndRotation(position, rotation);
        return marker;
    }
    private static Material MakeMaterial(Material template, string name, Texture texture, Color color)
    {
        Material material = new Material(template);
        material.name = name;
        if (material.HasProperty("_BaseMap")) { material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero); }
        if (material.HasProperty("_MainTex")) { material.SetTexture("_MainTex", texture); material.SetTextureScale("_MainTex", Vector2.one); material.SetTextureOffset("_MainTex", Vector2.zero); }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        return material;
    }
    private static Mesh MakeMesh(Part part)
    {
        Mesh mesh = new Mesh(); mesh.name = part.name;
        mesh.vertices = part.vertices; mesh.uv = part.uv; mesh.triangles = part.triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
    private static void Save(Object asset, string name, List<string> paths)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(Generated + "/" + name);
        paths.Add(path);
        AssetDatabase.CreateAsset(asset, path);
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
    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static void ValidateGeometry(Geometry data)
    {
        if (data == null || data.colors == null || data.colors.Length == 0 || data.hat == null || data.body == null)
            throw new InvalidOperationException("Dati degli accessori non validi.");
        foreach (Part part in new[] { data.hat, data.body })
        {
            if (part.vertices == null || part.triangles == null || part.uv == null
                || part.uv.Length != part.vertices.Length || part.triangles.Length % 3 != 0)
                throw new InvalidOperationException("Geometria accessori incompleta.");
            foreach (int index in part.triangles) if (index < 0 || index >= part.vertices.Length)
                throw new InvalidOperationException("Indice della geometria non valido.");
        }
    }
    private struct Frame
    {
        public Vector3 origin; public Quaternion rotation;
        public Frame(Vector3 origin, Quaternion rotation) { this.origin = origin; this.rotation = rotation; }
        public Vector3 Local(Vector3 world) { return Quaternion.Inverse(rotation) * (world - origin); }
        public Vector3 World(Vector3 local) { return origin + rotation * local; }
    }
    private class Sampler : IDisposable
    {
        private Texture2D copy;
        private Color tint;
        private Vector2 scale = Vector2.one, offset = Vector2.zero;
        public Sampler(Material material)
        {
            string property = material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            Texture source = material.HasProperty(property) ? material.GetTexture(property) : null;
            if (source == null) return;
            scale = material.GetTextureScale(property); offset = material.GetTextureOffset(property);
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                copy.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }
        public Color At(Vector2 uv)
        {
            uv = Vector2.Scale(uv, scale) + offset;
            return (copy == null ? Color.white : copy.GetPixelBilinear(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f))) * tint;
        }
        public void Dispose() { if (copy != null) Object.DestroyImmediate(copy); }
    }
    [Serializable] private class Geometry { public string[] colors; public string shirtColor; public Part hat; public Part body; }
    [Serializable] private class Part { public string name; public Vector3[] vertices; public int[] triangles; public Vector2[] uv; }
}
