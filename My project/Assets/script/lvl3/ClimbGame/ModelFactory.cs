using UnityEngine;

/// <summary>
/// Creates the mini-game's 3D visuals: the prefab if assigned, otherwise a colored Unity shape
/// (cube, sphere...) so the game is playable without assets. The model is centered on its parent
/// and scaled to fit the requested size.
/// </summary>
public static class ModelFactory
{
    /// <summary>
    /// Creates an object "name" under parent, containing the model.
    /// With a prefab, scaling is uniform (proportions are kept); without a prefab, the shape takes exactly size.
    /// </summary>
    public static GameObject Create(string name, Transform parent, GameObject prefab, PrimitiveType fallback,
                                    Color color, Vector3 size)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        GameObject model;
        if (prefab != null)
        {
            model = InstantiatePrefab(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            // Keep the prefab's own rotation: .fbx files from Blender have -90° on X to stand upright.
            model.transform.localRotation = prefab.transform.localRotation;
            // In edit mode, disable instead of destroying (no "removed component" on the prefab instance).
            foreach (var c in model.GetComponentsInChildren<Collider>())
                if (Application.isPlaying) Object.Destroy(c); else c.enabled = false;
            foreach (var a in model.GetComponentsInChildren<Animator>()) a.enabled = false;

            Bounds b = LocalBounds(root.transform);
            float s = Mathf.Min(size.x / Mathf.Max(b.size.x, 0.001f), size.y / Mathf.Max(b.size.y, 0.001f));
            model.transform.localScale *= s;
            model.transform.localPosition = -b.center * s;
        }
        else
        {
            model = GameObject.CreatePrimitive(fallback);
            DestroySafe(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = size;
            var renderer = model.GetComponent<Renderer>();
            if (Application.isPlaying)
                renderer.material.color = color;
            else
                // In edit mode, renderer.material is not allowed: create a copy, saved in the scene.
                renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };
        }
        model.name = "Model";
        return root;
    }

    // In edit mode, keep the prefab link (editable later in the scene).
    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Object.Instantiate(prefab, parent);
    }

    /// <summary>Destroy in play mode, DestroyImmediate in edit mode.</summary>
    public static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    /// <summary>Bounding box of all renderers, in root's local space.</summary>
    public static Bounds LocalBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds world = renderers[0].bounds;
        foreach (var r in renderers) world.Encapsulate(r.bounds);
        Vector3 center = root.InverseTransformPoint(world.center);
        Vector3 scale = root.lossyScale;
        return new Bounds(center, new Vector3(world.size.x / scale.x, world.size.y / scale.y, world.size.z / scale.z));
    }

    /// <summary>Bounding box of all renderers, in world space.</summary>
    public static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Game collisions happen on the (X, Y) plane: the depth of 3D models is ignored.</summary>
    public static bool OverlapsXY(Bounds a, Bounds b) =>
        a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;

    public static void SetVisible(GameObject go, bool visible)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = visible;
    }

    /// <summary>Replaces the material of all renderers (e.g. the main character's texture).</summary>
    public static void SetMaterial(GameObject go, Material material)
    {
        if (material == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
        }
    }
}
