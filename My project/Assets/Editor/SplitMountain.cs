using UnityEditor;
using UnityEngine;

public static class SplitMountain
{
    const string Dir = "Assets/Scenes/Level3_Data/";

    [MenuItem("Tools/Split Mountain")]
    static void Split()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "Mountain_LowPoly.asset");
        if (src == null) { Debug.LogError("Mountain_LowPoly.asset not found"); return; }

        // Submesh 0 = LP_Grass (ground), 1 = LP_Rock, 2 = LP_RockDark (mountain)
        Save(Extract(src, new[] { 0 }, "Mountain_Ground"), "Mountain_Ground.asset");
        Save(Extract(src, new[] { 1, 2 }, "Mountain_Only"), "Mountain_Only.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Created Mountain_Ground and Mountain_Only in " + Dir);
    }

    static Mesh Extract(Mesh src, int[] subs, string name)
    {
        var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = src.vertices;
        m.normals = src.normals;
        m.uv = src.uv;
        m.tangents = src.tangents;
        m.colors = src.colors;
        m.subMeshCount = subs.Length;
        for (int i = 0; i < subs.Length; i++)
            m.SetTriangles(src.GetTriangles(subs[i]), i);
        m.Optimize();
        m.RecalculateBounds();
        return m;
    }

    static void Save(Mesh m, string file)
    {
        var path = Dir + file;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }
}
