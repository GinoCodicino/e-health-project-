using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FallenTreeForestSetup
{
    const string TargetName = "Treestumb_3 (1)";
    const string ForestName = "Foresta alberi caduti";
    const string PrefabPath = "Assets/used assets/LowPoly Nature Pack/Prefabs/Others/Treestumb_3.prefab";

    static readonly Vector3[] Offsets =
    {
        new Vector3(-3.5f, 0f, -2.0f),
        new Vector3(-1.6f, 0f, -3.4f),
        new Vector3( 0.8f, 0f, -2.7f),
        new Vector3( 3.0f, 0f, -1.8f),
        new Vector3( 4.2f, 0f,  0.4f),
        new Vector3( 2.4f, 0f,  1.8f),
        new Vector3( 0.2f, 0f,  2.5f),
        new Vector3(-2.2f, 0f,  2.2f),
        new Vector3(-4.0f, 0f,  1.0f),
        new Vector3(-5.3f, 0f, -0.8f),
        new Vector3(-0.2f, 0f, -5.0f),
        new Vector3( 2.7f, 0f, -4.1f),
        new Vector3( 5.0f, 0f,  2.4f),
        new Vector3(-3.8f, 0f,  4.0f),
        new Vector3( 1.0f, 0f,  4.4f)
    };

    [MenuItem("Tools/Foresta alberi caduti/Crea foresta accanto al tronco")]
    public static void CreateForestIfNeeded()
    {
        if (!Application.isEditor)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("Apri e salva una scena prima di creare la foresta di alberi caduti.");
            return;
        }

        Transform target = FindTarget();
        if (target == null)
        {
            Debug.LogWarning("Non trovo un oggetto chiamato '" + TargetName + "' nella scena aperta.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Prefab Treestumb_3 non trovato: " + PrefabPath);
            return;
        }

        string forestName = GetUniqueForestName(scene);
        GameObject forest = new GameObject(forestName);
        Undo.RegisterCreatedObjectUndo(forest, "Crea foresta di alberi caduti");
        forest.transform.SetParent(target.parent, true);
        forest.transform.SetPositionAndRotation(target.position, target.rotation);

        for (int i = 0; i < Offsets.Length; i++)
        {
            GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            tree.name = "Albero caduto " + (i + 1);
            tree.transform.SetParent(forest.transform, true);
            tree.transform.position = target.TransformPoint(Offsets[i]);
            tree.transform.rotation = target.rotation * Quaternion.Euler(0f, 25f + i * 31f, 0f);
            tree.transform.localScale = target.lossyScale;
            Undo.RegisterCreatedObjectUndo(tree, "Aggiungi albero caduto");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Creata " + forestName + " con " + Offsets.Length + " alberi caduti accanto a " + TargetName + ".");
    }

    static Transform FindTarget()
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == TargetName && transform.gameObject.scene.IsValid())
                return transform;
        }

        return null;
    }

    static string GetUniqueForestName(Scene scene)
    {
        string name = ForestName;
        int numero = 1;

        while (FindObjectInScene(scene, name) != null)
        {
            numero++;
            name = ForestName + " " + numero;
        }

        return name;
    }

    static GameObject FindObjectInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindChild(root.transform, objectName);
            if (result != null)
                return result.gameObject;
        }

        return null;
    }

    static Transform FindChild(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChild(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
