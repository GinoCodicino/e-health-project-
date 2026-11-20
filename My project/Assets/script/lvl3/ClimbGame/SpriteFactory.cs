using UnityEngine;

/// <summary>
/// Sprites generated in code (square, circle) for flat, transparent effects
/// (wet patch, lightning zone, raindrops). Size: 1 unit.
/// 3D objects go through ModelFactory.
/// </summary>
public static class SpriteFactory
{
    private static Sprite square, circle;
    private static Material defaultMaterial;

    /// <summary>Default sprite material (transparent), reused by the LineRenderers.</summary>
    public static Material DefaultMaterial
    {
        get
        {
            if (defaultMaterial == null)
            {
                var probe = new GameObject("SpriteMaterialProbe");
                defaultMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            return defaultMaterial;
        }
    }

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return circle;
        }
    }

    /// <summary>Creates an object with a SpriteRenderer (asset if provided, otherwise the default shape).</summary>
    public static SpriteRenderer Create(string name, Transform parent, Sprite sprite, Sprite fallback,
                                        Color color, Vector2 size, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : fallback;
        // A real asset keeps its colors; the default shape is tinted.
        sr.color = sprite != null ? Color.white : color;
        sr.sortingOrder = sortingOrder;
        Vector2 spriteSize = sr.sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        return sr;
    }
}
