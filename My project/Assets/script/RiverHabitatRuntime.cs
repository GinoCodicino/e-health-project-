// Keep this file outside Editor. The serialized recipes rebuild the geometry
// after reopening the scene and in a player build; generated meshes are temporary.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RiverHabitatRuntime : MonoBehaviour
{
    [Serializable]
    public struct Stone
    {
        public Vector3 position; // Local to this component.
        public Quaternion rotation;
        public Vector3 scale;
        public int shade;
    }

    [Serializable]
    public sealed class Fish
    {
        public GameObject prefab; // Optional: a simple low-poly fish is included.
        public Vector3[] route;   // Local to this component, closed loop.
        public Vector3 modelEuler;
        public float length = 0.8f;
        public float speed = 0.8f;
        public float phase;
    }

    [Serializable]
    public sealed class WaterVolume
    {
        public Terrain terrain;
        public MeshRenderer water;
        public Collider[] extraGrounds = new Collider[0];
        public Bounds[] obstacles = new Bounds[0]; // World-space snapshots at generation.
        public Bounds[] exclusions = new Bounds[0];
        public float waterLevel;
        public bool automaticLevel = true;
        public float bottomClearance = 0.45f;
        public float gravelHeight;
        [NonSerialized] private Dictionary<Vector2Int, List<int>> obstacleIndex;
        private const float Bucket = 8f;

        public float Level
        {
            get
            {
                MeshFilter filter = water ? water.GetComponent<MeshFilter>() : null;
                return automaticLevel && filter && filter.sharedMesh
                    ? water.transform.TransformPoint(filter.sharedMesh.bounds.center).y : waterLevel;
            }
        }

        public void BuildIndex()
        {
            obstacleIndex = new Dictionary<Vector2Int, List<int>>();
            if (obstacles == null) obstacles = new Bounds[0];
            for (int i = 0; i < obstacles.Length; i++)
            {
                Bounds b = obstacles[i];
                int minX = Mathf.FloorToInt(b.min.x / Bucket), maxX = Mathf.FloorToInt(b.max.x / Bucket);
                int minZ = Mathf.FloorToInt(b.min.z / Bucket), maxZ = Mathf.FloorToInt(b.max.z / Bucket);
                // Huge boxes belong in exclusions rather than occupying millions of buckets.
                if ((long)(maxX - minX + 1) * (maxZ - minZ + 1) > 10000) continue;
                for (int z = minZ; z <= maxZ; z++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2Int key = new Vector2Int(x, z);
                        if (!obstacleIndex.TryGetValue(key, out List<int> list))
                        { list = new List<int>(); obstacleIndex.Add(key, list); }
                        list.Add(i);
                    }
            }
        }

        public bool SampleGround(float x, float z, out float height)
        {
            height = float.NegativeInfinity;
            if (!terrain || !terrain.terrainData || !water) return false;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            float nx = (x - origin.x) / data.size.x, nz = (z - origin.z) / data.size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) return false;
            MeshFilter filter = water.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) return false;
            Bounds wb = filter.sharedMesh.bounds;
            Vector3 local = water.transform.InverseTransformPoint(new Vector3(x, Level, z));
            if (local.x < wb.min.x || local.x > wb.max.x || local.z < wb.min.z || local.z > wb.max.z) return false;
            int resolution = data.holesResolution;
            bool valid = resolution < 1 || !data.IsHole(
                Mathf.Clamp(Mathf.FloorToInt(nx * resolution), 0, resolution - 1),
                Mathf.Clamp(Mathf.FloorToInt(nz * resolution), 0, resolution - 1));
            if (valid) height = origin.y + data.GetInterpolatedHeight(nx, nz);
            if (extraGrounds != null)
                foreach (Collider ground in extraGrounds)
                {
                    if (!ground || !ground.enabled || !ground.gameObject.activeInHierarchy || ground.isTrigger) continue;
                    Bounds b = ground.bounds;
                    if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) continue;
                    float top = Mathf.Max(origin.y + data.size.y, b.max.y) + 20f;
                    if (ground.Raycast(new Ray(new Vector3(x, top, z), Vector3.down), out RaycastHit hit,
                        top - origin.y + 100f) && hit.normal.y > 0.05f && hit.point.y > height)
                    { height = hit.point.y; valid = true; }
                }
            return valid && !float.IsInfinity(height) && !float.IsNaN(height);
        }

        public bool FishFits(Vector3 center, float radius)
        {
            if (center.y + radius > Level - 0.12f) return false;
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float r = i == 8 ? 0f : radius;
                if (!SampleGround(center.x + Mathf.Cos(angle) * r, center.z + Mathf.Sin(angle) * r, out float ground) ||
                    center.y - radius < ground + bottomClearance + gravelHeight) return false;
            }
            if (exclusions != null)
                foreach (Bounds b in exclusions)
                {
                    float dx = Mathf.Max(b.min.x - center.x, 0f, center.x - b.max.x);
                    float dz = Mathf.Max(b.min.z - center.z, 0f, center.z - b.max.z);
                    if (dx * dx + dz * dz <= radius * radius) return false;
                }
            if (obstacleIndex == null) BuildIndex();
            int minX = Mathf.FloorToInt((center.x - radius) / Bucket), maxX = Mathf.FloorToInt((center.x + radius) / Bucket);
            int minZ = Mathf.FloorToInt((center.z - radius) / Bucket), maxZ = Mathf.FloorToInt((center.z + radius) / Bucket);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    if (obstacleIndex.TryGetValue(new Vector2Int(x, z), out List<int> list))
                        foreach (int index in list)
                            if (obstacles[index].SqrDistance(center) < radius * radius) return false;
            return true;
        }
    }

    [SerializeField, HideInInspector] private Stone[] stones = new Stone[0];
    [SerializeField, HideInInspector] private Fish[] fish = new Fish[0];
    [SerializeField, HideInInspector] private WaterVolume volume = new WaterVolume();
    [SerializeField, HideInInspector] private Material gravelMaterial, fishMaterial, finMaterial;
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly List<FishState> swimming = new List<FishState>();
    private GameObject previewRoot;
    private bool rebuild;
    private const int StonesPerChunk = 600; // 43,200 vertices: fits 16-bit indices.

    private sealed class FishState
    {
        public Transform root, visual;
        public Fish recipe;
        public int target = 1, direction = 1;
    }

    public void Configure(Stone[] newStones, Fish[] newFish, WaterVolume newVolume,
        Material gravel, Material body, Material fins)
    {
        stones = newStones ?? new Stone[0]; fish = newFish ?? new Fish[0];
        volume = newVolume ?? new WaterVolume();
        gravelMaterial = gravel; fishMaterial = body; finMaterial = fins;
        rebuild = true;
    }

    private void OnEnable() { rebuild = true; }
    private void OnValidate() { rebuild = true; }
    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }

    private void Update()
    {
        if (rebuild)
        {
            rebuild = false;
            try { RebuildNow(); }
            catch (Exception ex) { Release(); Debug.LogException(ex, this); }
        }
        if (!Application.IsPlaying(gameObject)) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        foreach (FishState state in swimming)
        {
            Vector3 target = transform.TransformPoint(state.recipe.route[state.target]);
            Vector3 current = state.root.position;
            Vector3 next = Vector3.MoveTowards(current, target, state.recipe.speed * dt);
            // A bounding sphere encloses the complete normalized fish model.
            float guard = state.recipe.length * 0.90f;
            if (!volume.FishFits(next, guard))
            {
                state.direction = -state.direction;
                state.target = Wrap(state.target + state.direction, state.recipe.route.Length);
                continue;
            }
            state.root.position = next;
            Vector3 direction = next - current;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.000001f)
                state.root.rotation = Quaternion.RotateTowards(state.root.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), 120f * dt);
            if ((next - target).sqrMagnitude < 0.0025f)
                state.target = Wrap(state.target + state.direction, state.recipe.route.Length);
            // Only the visual turns gently; the validated path does not bob out of water.
            state.visual.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 5f + state.recipe.phase) * 4f, 0);
        }
    }

    private static int Wrap(int index, int count) { return (index % count + count) % count; }

    public void RebuildNow()
    {
        rebuild = false;
        Release();
        if (!isActiveAndEnabled || (stones.Length == 0 && fish.Length == 0)) return;
        previewRoot = Child("Geometria_ricostruita", transform);
        Material[] shades = CreateShades(gravelMaterial);
        for (int start = 0; start < stones.Length; start += StonesPerChunk)
        {
            MeshBuilder builder = new MeshBuilder(3);
            int end = Mathf.Min(stones.Length, start + StonesPerChunk);
            for (int i = start; i < end; i++) AppendStone(builder, stones[i]);
            Mesh mesh = Own(builder.ToMesh("Ciottoli_" + start));
            GameObject chunk = Child(mesh.name, previewRoot.transform);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = shades;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        volume.BuildIndex();
        Mesh bodyMesh = null, finsMesh = null;
        foreach (Fish recipe in fish)
        {
            if (recipe.route == null || recipe.route.Length < 3) continue;
            Vector3 spawn = transform.TransformPoint(recipe.route[0]);
            if (!volume.FishFits(spawn, recipe.length * 0.90f)) continue;
            GameObject root = Child("Pesce", previewRoot.transform);
            root.transform.position = spawn;
            Vector3 heading = transform.TransformPoint(recipe.route[1]) - spawn; heading.y = 0;
            root.transform.rotation = heading.sqrMagnitude > 0.00001f ? Quaternion.LookRotation(heading) : Quaternion.identity;
            GameObject visual = Child("Nuoto", root.transform);
            GameObject modelContainer = Child("Modello_centrato", visual.transform);
            if (recipe.prefab)
            {
                GameObject model = Instantiate(recipe.prefab, modelContainer.transform, false);
                HideHierarchy(model);
                model.SetActive(true);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(recipe.modelEuler) * recipe.prefab.transform.localRotation;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (Rigidbody r in model.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            }
            else
            {
                if (!bodyMesh) { bodyMesh = Own(CreateFishBody()); finsMesh = Own(CreateFishFins()); }
                MeshChild("Corpo", modelContainer.transform, bodyMesh, fishMaterial);
                MeshChild("Pinne", modelContainer.transform, finsMesh, finMaterial);
            }
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            bool found = false; Bounds bounds = new Bounds();
            foreach (Renderer r in renderers)
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
                if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (!found) { Remove(root); continue; }
            float dimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (dimension < 0.00001f) { Remove(root); continue; }
            modelContainer.transform.localScale *= recipe.length / dimension;
            found = false;
            foreach (Renderer r in renderers)
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
                if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
            }
            modelContainer.transform.position += spawn - bounds.center;
            swimming.Add(new FishState { root = root.transform, visual = visual.transform, recipe = recipe });
        }
    }

    private Material[] CreateShades(Material source)
    {
        Material[] result = new Material[3];
        for (int i = 0; i < result.Length; i++)
        {
            if (!source) continue;
            Material material = Own(new Material(source)); material.name = "Ciottoli_tono_" + i;
            float factor = 0.82f + i * 0.14f;
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (material.HasProperty(property))
                {
                    Color color = source.GetColor(property);
                    material.SetColor(property, new Color(color.r * factor, color.g * factor, color.b * factor, color.a));
                }
            result[i] = material;
        }
        return result;
    }

    private T Own<T>(T resource) where T : UnityEngine.Object
    { resource.hideFlags = HideFlags.DontSave; owned.Add(resource); return resource; }

    private static GameObject Child(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void HideHierarchy(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
    }

    private static void MeshChild(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject child = Child(name, parent);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void Release()
    {
        GameObject previous = previewRoot;
        if (previewRoot) { previewRoot.SetActive(false); Remove(previewRoot); previewRoot = null; }
        // Undo or a domain reload can restore a transient child without restoring
        // the managed reference. Remove those remnants before reconstructing it.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child == previous || child.name != "Geometria_ricostruita" || (child.hideFlags & HideFlags.DontSave) == 0) continue;
            child.SetActive(false); Remove(child);
        }
        foreach (UnityEngine.Object resource in owned) if (resource) Remove(resource);
        owned.Clear(); swimming.Clear();
    }

    private static void Remove(UnityEngine.Object item)
    {
        if (!item) return;
        if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
    }

    // Fixed geometry shared by the editor's grounding checks and the mesh builder.
    public static Vector3 StoneLowerPoint(int side)
    {
        float angle = side * Mathf.PI / 3f;
        return new Vector3(Mathf.Cos(angle) * 0.78f, -0.15f, Mathf.Sin(angle) * 0.78f);
    }

    private static void AppendStone(MeshBuilder builder, Stone stone)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(stone.position, stone.rotation, stone.scale);
        int shade = Mathf.Clamp(stone.shade, 0, 2);
        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            Vector3 lowA = StoneLowerPoint(i), lowB = StoneLowerPoint(j);
            Vector3 midA = new Vector3(lowA.x / 0.78f, 0.16f, lowA.z / 0.78f);
            Vector3 midB = new Vector3(lowB.x / 0.78f, 0.16f, lowB.z / 0.78f);
            Vector3 top = new Vector3(0.05f, 1f, -0.06f), bottom = new Vector3(0, -0.17f, 0);
            builder.Triangle(matrix.MultiplyPoint3x4(lowA), matrix.MultiplyPoint3x4(midA), matrix.MultiplyPoint3x4(midB), shade);
            builder.Triangle(matrix.MultiplyPoint3x4(lowA), matrix.MultiplyPoint3x4(midB), matrix.MultiplyPoint3x4(lowB), shade);
            builder.Triangle(matrix.MultiplyPoint3x4(midA), matrix.MultiplyPoint3x4(top), matrix.MultiplyPoint3x4(midB), shade);
            builder.Triangle(matrix.MultiplyPoint3x4(lowA), matrix.MultiplyPoint3x4(lowB), matrix.MultiplyPoint3x4(bottom), shade);
        }
    }

    private static Mesh CreateFishBody()
    {
        MeshBuilder builder = new MeshBuilder(1);
        float[] z = { -0.48f, -0.28f, 0.15f, 0.43f };
        float[] r = { 0.025f, 0.15f, 0.13f, 0.025f };
        for (int ring = 0; ring < z.Length - 1; ring++)
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector3 a = FishRing(i, z[ring], r[ring]), b = FishRing(j, z[ring], r[ring]);
                Vector3 c = FishRing(i, z[ring + 1], r[ring + 1]), d = FishRing(j, z[ring + 1], r[ring + 1]);
                builder.OutwardTriangle(a, b, c); builder.OutwardTriangle(b, d, c);
            }
        for (int i = 0; i < 8; i++)
        {
            builder.OutwardTriangle(FishRing(i, z[0], r[0]), new Vector3(0, 0, -0.51f), FishRing((i + 1) % 8, z[0], r[0]));
            builder.OutwardTriangle(FishRing(i, z[3], r[3]), FishRing((i + 1) % 8, z[3], r[3]), new Vector3(0, 0, 0.47f));
        }
        return builder.ToMesh("Pesce_corpo_low_poly");
    }

    private static Vector3 FishRing(int index, float z, float radius)
    { float a = index * Mathf.PI / 4f; return new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 1.2f, z); }

    private static Mesh CreateFishFins()
    {
        MeshBuilder builder = new MeshBuilder(1);
        builder.DoubleTriangle(new Vector3(0, 0, -0.45f), new Vector3(0, 0.22f, -0.72f), new Vector3(0, -0.22f, -0.72f));
        builder.DoubleTriangle(new Vector3(0, 0.15f, -0.2f), new Vector3(0, 0.31f, -0.15f), new Vector3(0, 0.15f, 0.12f));
        builder.DoubleTriangle(new Vector3(-0.11f, -0.02f, 0.12f), new Vector3(-0.3f, -0.1f, -0.12f), new Vector3(-0.09f, -0.07f, -0.18f));
        builder.DoubleTriangle(new Vector3(0.11f, -0.02f, 0.12f), new Vector3(0.09f, -0.07f, -0.18f), new Vector3(0.3f, -0.1f, -0.12f));
        return builder.ToMesh("Pesce_pinne_low_poly");
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int>[] triangles;
        public MeshBuilder(int materials)
        { triangles = new List<int>[materials]; for (int i = 0; i < materials; i++) triangles[i] = new List<int>(); }
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material = 0)
        {
            AddVertex(a, material); AddVertex(b, material); AddVertex(c, material);
        }
        private void AddVertex(Vector3 p, int material)
        { triangles[material].Add(vertices.Count); vertices.Add(p); uv.Add(new Vector2(p.x, p.z)); }
        public void DoubleTriangle(Vector3 a, Vector3 b, Vector3 c)
        { Triangle(a, b, c); Triangle(c, b, a); }
        public void OutwardTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f) < 0) Triangle(a, c, b);
            else Triangle(a, b, c);
        }
        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
