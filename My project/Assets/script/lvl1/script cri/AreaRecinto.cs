using UnityEngine;

public class AreaRecinto : MonoBehaviour
{
    [Tooltip("Gli angoli del recinto, in ordine lungo il perimetro (almeno 3)")]
    public Transform[] angoli;

    [Tooltip("Distanza minima dalla recinzione per le mete scelte")]
    public float margine = 0.8f;

    public bool Valido
    {
        get
        {
            if (angoli == null || angoli.Length < 3) return false;
            foreach (Transform t in angoli) if (t == null) return false;
            return true;
        }
    }

    Vector2 Punto(int i)
    {
        return new Vector2(angoli[i].position.x, angoli[i].position.z);
    }

    // Il punto sta dentro il poligono?
    public bool Dentro(Vector3 p)
    {
        bool dentro = false;
        int n = angoli.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = Punto(i), b = Punto(j);
            if ((a.y > p.z) != (b.y > p.z) &&
                p.x < (b.x - a.x) * (p.z - a.y) / (b.y - a.y) + a.x)
                dentro = !dentro;
        }
        return dentro;
    }

    // Distanza dal lato più vicino
    public float DistanzaBordo(Vector3 p)
    {
        Vector2 q = new Vector2(p.x, p.z);
        float min = float.MaxValue;
        int n = angoli.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = Punto(i), b = Punto((i + 1) % n);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            float d = (q - (a + ab * t)).magnitude;
            if (d < min) min = d;
        }
        return min;
    }

    // fattore 1 = margine pieno, 0.5 = metà margine (per il movimento), 0 = basta essere dentro
    public bool Permesso(Vector3 p, float fattoreMargine)
    {
        if (!Valido) return true;
        return Dentro(p) && DistanzaBordo(p) >= margine * fattoreMargine;
    }

    public Vector3 Centro(float y)
    {
        Vector3 c = Vector3.zero;
        if (Valido)
        {
            foreach (Transform t in angoli) c += t.position;
            c /= angoli.Length;
        }
        else c = transform.position;
        c.y = y;
        return c;
    }

    public Vector3 PuntoCasuale(float y)
    {
        if (!Valido) return new Vector3(transform.position.x, y, transform.position.z);

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Transform t in angoli)
        {
            minX = Mathf.Min(minX, t.position.x); maxX = Mathf.Max(maxX, t.position.x);
            minZ = Mathf.Min(minZ, t.position.z); maxZ = Mathf.Max(maxZ, t.position.z);
        }

        for (int i = 0; i < 60; i++)
        {
            Vector3 p = new Vector3(Random.Range(minX, maxX), y, Random.Range(minZ, maxZ));
            if (Permesso(p, 1f)) return p;
        }
        return Centro(y);
    }

    [ContextMenu("Crea 4 angoli")]
    void CreaAngoli()
    {
        if (angoli != null && angoli.Length > 0) return;

        Vector2[] off = { new Vector2(-4, -4), new Vector2(-4, 4), new Vector2(4, 4), new Vector2(4, -4) };
        angoli = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject g = new GameObject("Angolo " + (i + 1));
            g.transform.SetParent(transform, false);
            g.transform.position = transform.position + new Vector3(off[i].x, 0f, off[i].y);
            angoli[i] = g.transform;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void OnDrawGizmos()
    {
        if (!Valido) return;

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
        int n = angoli.Length;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = angoli[i].position + Vector3.up * 0.15f;
            Vector3 b = angoli[(i + 1) % n].position + Vector3.up * 0.15f;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.2f);
        }
    }
}