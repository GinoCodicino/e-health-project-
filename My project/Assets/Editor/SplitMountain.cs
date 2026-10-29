using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SplitMountain
{
    const string Dir = "Assets/Scenes/Level3_Data/";
    const float BaseTolerance = 0.05f;

    [MenuItem("Tools/Split Mountain")]
    static void Split()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "Mountain_LowPoly.asset");
        if (src == null) { Debug.LogError("Mountain_LowPoly.asset not found"); return; }

        // The flat floor sits at the lowest y of the mesh; a triangle belongs to
        // the mountain as soon as one of its vertices leaves the floor.
        var verts = src.vertices;
        float floor = verts.Min(v => v.y);
        var ground = new List<int>[src.subMeshCount];
        var mountain = new List<int>[src.subMeshCount];
        for (int s = 0; s < src.subMeshCount; s++)
        {
            ground[s] = new List<int>();
            mountain[s] = new List<int>();
            var t = src.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                bool flat = verts[t[i]].y - floor <= BaseTolerance
                         && verts[t[i + 1]].y - floor <= BaseTolerance
                         && verts[t[i + 2]].y - floor <= BaseTolerance;
                (flat ? ground[s] : mountain[s]).AddRange(new[] { t[i], t[i + 1], t[i + 2] });
            }
        }

        Save(Build(src, ground, "Mountain_Ground"), "Mountain_Ground.asset");
        Save(Build(src, mountain, "Mountain_Only"), "Mountain_Only.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Split at floor y=" + floor + " into Mountain_Ground and Mountain_Only in " + Dir);
    }

    // Keeps the same submesh layout as the source so the 3 materials map 1:1,
    // but only copies the vertices the kept triangles use (correct bounds, smaller asset).
    static Mesh Build(Mesh src, List<int>[] tris, string name)
    {
        var sv = src.vertices; var sn = src.normals; var suv = src.uv; var st = src.tangents; var sc = src.colors;
        var map = new Dictionary<int, int>();
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
        var t = new List<Vector4>(); var c = new List<Color>();
        int Remap(int i)
        {
            if (map.TryGetValue(i, out int r)) return r;
            r = v.Count; map[i] = r;
            v.Add(sv[i]);
            if (sn.Length > 0) n.Add(sn[i]);
            if (suv.Length > 0) uv.Add(suv[i]);
            if (st.Length > 0) t.Add(st[i]);
            if (sc.Length > 0) c.Add(sc[i]);
            return r;
        }
        var newTris = tris.Select(l => l.Select(Remap).ToList()).ToArray();

        var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v);
        if (n.Count > 0) m.SetNormals(n);
        if (uv.Count > 0) m.SetUVs(0, uv);
        if (t.Count > 0) m.SetTangents(t);
        if (c.Count > 0) m.SetColors(c);
        m.subMeshCount = newTris.Length;
        for (int s = 0; s < newTris.Length; s++)
            m.SetTriangles(newTris[s], s);
        m.RecalculateBounds();
        return m;
    }

    static void Save(Mesh m, string file)
    {
        var path = Dir + file;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing); // keep the GUID so scene references survive
            return;
        }
        AssetDatabase.CreateAsset(m, path);
    }
}
