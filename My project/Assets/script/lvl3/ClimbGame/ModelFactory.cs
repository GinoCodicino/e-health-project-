using UnityEngine;

/// <summary>
/// Crée les visuels 3D du mini-jeu : le prefab s'il est assigné, sinon une forme Unity colorée
/// (cube, sphère…) pour pouvoir jouer sans asset. Le modèle est centré sur son parent
/// et mis à l'échelle pour tenir dans la taille demandée.
/// </summary>
public static class ModelFactory
{
    /// <summary>
    /// Crée un objet « name » sous parent, qui contient le modèle.
    /// Avec un prefab, l'échelle est uniforme (on garde ses proportions) ; sans prefab, la forme prend exactement size.
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
            // On garde la rotation propre du prefab : les .fbx venus de Blender ont -90° en X pour être debout.
            model.transform.localRotation = prefab.transform.localRotation;
            // En mode édition, on désactive au lieu de détruire (pas de « composant supprimé » sur l'instance de prefab).
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
                // En mode édition, renderer.material est interdit : on crée une copie, enregistrée dans la scène.
                renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };
        }
        model.name = "Model";
        return root;
    }

    // En mode édition, on garde le lien avec le prefab (modifiable ensuite dans la scène).
    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Object.Instantiate(prefab, parent);
    }

    /// <summary>Destroy en jeu, DestroyImmediate en mode édition.</summary>
    public static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    /// <summary>Boîte englobante de tous les renderers, dans l'espace local de root.</summary>
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

    /// <summary>Boîte englobante de tous les renderers, dans le monde.</summary>
    public static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Les collisions du jeu se font sur le plan (X, Y) : la profondeur des modèles 3D est ignorée.</summary>
    public static bool OverlapsXY(Bounds a, Bounds b) =>
        a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;

    public static void SetVisible(GameObject go, bool visible)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = visible;
    }

    /// <summary>Remplace le matériau de tous les renderers (ex. la texture du personnage principal).</summary>
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
