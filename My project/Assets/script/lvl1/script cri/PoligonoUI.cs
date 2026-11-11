using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class PoligonoUI : MaskableGraphic
{
    Vector2[] punti = new Vector2[0];

    protected PoligonoUI()
    {
        useLegacyMeshGeneration = false;
    }

    public void Imposta(Vector2[] nuovi)
    {
        punti = nuovi;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (punti == null || punti.Length < 3) return;

        int n = punti.Length;
        Color32 c32 = color;
        for (int i = 0; i < n; i++)
            vh.AddVert(new Vector3(punti[i].x, punti[i].y, 0f), c32, new Vector4(0f, 0f, 0f, 0f));

        List<int> tri = Triangola();
        for (int i = 0; i + 2 < tri.Count; i += 3)
            vh.AddTriangle(tri[i], tri[i + 1], tri[i + 2]);
    }

    // Ear clipping; se fallisce usa un ventaglio di triangoli
    List<int> Triangola()
    {
        int n = punti.Length;
        List<int> tri = new List<int>();

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = punti[i], b = punti[(i + 1) % n];
            area += a.x * b.y - b.x * a.y;
        }
        bool antiorario = area > 0f;

        List<int> idx = new List<int>();
        for (int i = 0; i < n; i++) idx.Add(i);

        int guardia = 0;
        while (idx.Count > 3 && guardia++ < 1000)
        {
            bool tagliato = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int i0 = idx[(i + idx.Count - 1) % idx.Count];
                int i1 = idx[i];
                int i2 = idx[(i + 1) % idx.Count];
                Vector2 a = punti[i0], b = punti[i1], c = punti[i2];

                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (antiorario ? cross <= 0f : cross >= 0f) continue;

                bool dentro = false;
                for (int j = 0; j < idx.Count; j++)
                {
                    int k = idx[j];
                    if (k == i0 || k == i1 || k == i2) continue;
                    if (Dentro(punti[k], a, b, c)) { dentro = true; break; }
                }
                if (dentro) continue;

                tri.Add(i0); tri.Add(i1); tri.Add(i2);
                idx.RemoveAt(i);
                tagliato = true;
                break;
            }
            if (!tagliato) break;
        }

        if (idx.Count == 3)
        {
            tri.Add(idx[0]); tri.Add(idx[1]); tri.Add(idx[2]);
            return tri;
        }

        tri.Clear();
        for (int i = 1; i < n - 1; i++)
        {
            tri.Add(0); tri.Add(i); tri.Add(i + 1);
        }
        return tri;
    }

    static bool Dentro(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Segno(p, a, b), d2 = Segno(p, b, c), d3 = Segno(p, c, a);
        bool tuttiPos = d1 > 0f && d2 > 0f && d3 > 0f;
        bool tuttiNeg = d1 < 0f && d2 < 0f && d3 < 0f;
        return tuttiPos || tuttiNeg;
    }

    static float Segno(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}