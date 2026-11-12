using UnityEngine;
using UnityEngine.UI;

// Piccole illustrazioni vettoriali: le aree cliccabili sono i pulsanti, non i disegni.
[RequireComponent(typeof(CanvasRenderer))]
public class MushroomIcon : MaskableGraphic
{
    public enum Picture { Mushroom, Leaf, Rock, Flower }
    public Picture picture;
    public Color capColor = new Color(0.77f, 0.28f, 0.18f);
    public bool spots = true;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Color stem = new Color(0.85f, 0.75f, 0.54f);
        Color green = new Color(0.24f, 0.46f, 0.25f);
        Ellipse(mesh, 0.50f, 0.13f, 0.33f, 0.055f, new Color(0.23f, 0.28f, 0.20f, 0.12f));
        if (picture == Picture.Mushroom)
        {
            Polygon(mesh, stem, new Vector2(0.41f, 0.17f), new Vector2(0.59f, 0.17f),
                new Vector2(0.56f, 0.60f), new Vector2(0.44f, 0.60f));
            Ellipse(mesh, 0.50f, 0.19f, 0.09f, 0.05f, stem);
            Ellipse(mesh, 0.50f, 0.53f, 0.37f, 0.10f, new Color(0.49f, 0.34f, 0.24f));
            Fan(mesh, 0.50f, 0.55f, 0.39f, 0.31f, 0f, Mathf.PI, capColor);
            if (spots)
            {
                Color spot = new Color(1f, 0.96f, 0.81f);
                Ellipse(mesh, 0.30f, 0.63f, 0.045f, 0.037f, spot);
                Ellipse(mesh, 0.48f, 0.76f, 0.055f, 0.042f, spot);
                Ellipse(mesh, 0.68f, 0.64f, 0.05f, 0.035f, spot);
            }
        }
        else if (picture == Picture.Leaf)
        {
            Polygon(mesh, green, new Vector2(0.22f, 0.20f), new Vector2(0.23f, 0.57f),
                new Vector2(0.48f, 0.79f), new Vector2(0.83f, 0.85f),
                new Vector2(0.78f, 0.50f), new Vector2(0.55f, 0.26f));
            Polygon(mesh, new Color(0.48f, 0.63f, 0.28f), new Vector2(0.22f, 0.17f),
                new Vector2(0.26f, 0.17f), new Vector2(0.78f, 0.79f), new Vector2(0.73f, 0.77f));
        }
        else if (picture == Picture.Rock)
        {
            Color grey = new Color(0.47f, 0.51f, 0.48f);
            Polygon(mesh, grey, new Vector2(0.13f, 0.25f), new Vector2(0.30f, 0.66f),
                new Vector2(0.61f, 0.79f), new Vector2(0.85f, 0.57f),
                new Vector2(0.90f, 0.23f), new Vector2(0.54f, 0.15f));
            Polygon(mesh, new Color(0.66f, 0.68f, 0.62f), new Vector2(0.30f, 0.66f),
                new Vector2(0.61f, 0.79f), new Vector2(0.68f, 0.43f), new Vector2(0.38f, 0.39f));
        }
        else
        {
            Polygon(mesh, green, new Vector2(0.47f, 0.17f), new Vector2(0.53f, 0.17f),
                new Vector2(0.53f, 0.65f), new Vector2(0.47f, 0.65f));
            Ellipse(mesh, 0.36f, 0.34f, 0.15f, 0.07f, green);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Ellipse(mesh, 0.5f + Mathf.Cos(a) * 0.17f, 0.66f + Mathf.Sin(a) * 0.17f,
                    0.12f, 0.12f, new Color(0.86f, 0.64f, 0.38f));
            }
            Ellipse(mesh, 0.50f, 0.66f, 0.11f, 0.11f, new Color(0.51f, 0.30f, 0.17f));
        }
    }

    private Vector2 Point(float x, float y)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
    }

    private void Vertex(VertexHelper mesh, Vector2 position, Color tint)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = Point(position.x, position.y);
        v.color = tint;
        mesh.AddVert(v);
    }

    private void Polygon(VertexHelper mesh, Color tint, params Vector2[] points)
    {
        int start = mesh.currentVertCount;
        for (int i = 0; i < points.Length; i++) Vertex(mesh, points[i], tint);
        for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
    }

    private void Ellipse(VertexHelper mesh, float x, float y, float rx, float ry, Color tint)
    {
        Fan(mesh, x, y, rx, ry, 0f, Mathf.PI * 2f, tint);
    }

    private void Fan(VertexHelper mesh, float x, float y, float rx, float ry, float from, float to, Color tint)
    {
        int start = mesh.currentVertCount;
        Vertex(mesh, new Vector2(x, y), tint);
        const int segments = 32;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)segments);
            Vertex(mesh, new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry), tint);
            if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
