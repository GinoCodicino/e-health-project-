using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drifting clouds, rebuilt at every startup: three layers behind the wall,
/// plus a cloud ceiling where the rain begins. They take the sky's tint.
/// </summary>
[Serializable]
public class CloudLayer : SceneryLayer
{
    [Tooltip("Cloud sprites (empty = soft cloud generated in code).")]
    public Sprite[] sprites;
    [SerializeField] private float driftSpeed = 0.6f;
    [Tooltip("Opacity of clouds passing in front of the wall at the cloud ceiling (keep low for readability).")]
    [Range(0f, 1f)] [SerializeField] private float frontAlpha = 0.2f;

    // Each cloud drifts along x and takes the sky tint based on its depth.
    private struct Cloud
    {
        public SpriteRenderer renderer;
        public float speed, haze, alpha, range;
    }
    private readonly List<Cloud> clouds = new List<Cloud>();
    private static Sprite softCloud;

    public override void BuildRuntime(Transform root, float summitHeight)
    {
        clouds.Clear();
        var random = new System.Random(5);
        float top = summitHeight + 25f;

        // Three layers behind the wall: perspective makes them scroll at different speeds.
        //        depth, count, min-max width, x spread, haze (share of horizon color), opacity
        AddLayer(root, random, 60f, 22, 20f, 34f, 110f, -10f, top, 0.65f, 0.9f);
        AddLayer(root, random, 30f, 16, 10f, 18f, 60f, -5f, top, 0.35f, 0.85f);
        AddLayer(root, random, 12f, 10, 6f, 10f, 35f, 0f, top, 0.15f, 0.8f);

        // Cloud ceiling where the rain begins: a dense layer behind, and a light veil in front of the wall.
        float ceiling = CeilingHeight(summitHeight);
        AddLayer(root, random, 20f, 18, 14f, 24f, 50f, ceiling - 6f, ceiling + 6f, 0.25f, 0.95f);
        AddLayer(root, random, -3f, 7, 8f, 14f, 22f, ceiling - 3f, ceiling + 3f, 0.1f, frontAlpha);
    }

    public override void Tick(float dt)
    {
        if (clouds.Count == 0) return;
        Color horizon = Scenery.View != null ? Scenery.View.HorizonColor : RenderSettings.fogColor;
        // In bad weather the horizon darkens: the clouds turn gray with it.
        Color tint = Color.Lerp(Color.white, horizon * 1.3f, 0.3f);

        foreach (var c in clouds)
        {
            if (c.renderer == null) continue;
            var t = c.renderer.transform;
            Vector3 p = t.position;
            p.x += c.speed * dt;
            if (p.x > c.range) p.x = -c.range;
            t.position = p;

            Color col = Color.Lerp(tint, horizon, c.haze);
            col.a = c.alpha;
            c.renderer.color = col;
        }
    }

    private void AddLayer(Transform parent, System.Random random, float z, int count,
                          float minWidth, float maxWidth, float spreadX, float minY, float maxY,
                          float haze, float alpha)
    {
        for (int i = 0; i < count; i++)
        {
            float width = Mathf.Lerp(minWidth, maxWidth, (float)random.NextDouble());
            Sprite sprite = sprites != null && sprites.Length > 0 ? sprites[random.Next(sprites.Length)] : null;
            var sr = SpriteFactory.Create("Cloud", parent, sprite, SoftCloud, Color.white,
                                          new Vector2(width, width * 0.45f), 0);
            float x = ((float)random.NextDouble() * 2f - 1f) * spreadX;
            float y = Mathf.Lerp(minY, maxY, (float)random.NextDouble());
            sr.transform.position = new Vector3(x, y, z);
            if (random.Next(2) == 0) sr.flipX = true;

            clouds.Add(new Cloud
            {
                renderer = sr,
                // Distant clouds look slower; slight random direction = prevailing wind to the right.
                speed = driftSpeed * (0.5f + (float)random.NextDouble()) * (z < 0f ? 1.5f : 1f) / (1f + Mathf.Max(z, 0f) / 30f),
                haze = haze,
                alpha = alpha,
                range = spreadX + maxWidth,
            });
        }
    }

    // The cloud ceiling sits at the start of the first rainy section (otherwise halfway up).
    private float CeilingHeight(float summitHeight)
    {
        if (Scenery.Weather != null)
            foreach (var section in Scenery.Weather.sections)
                if (section.rain) return section.startHeight;
        return summitHeight * 0.5f;
    }

    // Soft cloud generated in code: a few blurry blobs blended together, slightly flattened base.
    private static Sprite SoftCloud
    {
        get
        {
            if (softCloud != null) return softCloud;
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var puffs = new[]
            {
                new Vector3(0.30f, 0.38f, 0.20f), new Vector3(0.50f, 0.50f, 0.26f),
                new Vector3(0.70f, 0.40f, 0.20f), new Vector3(0.42f, 0.32f, 0.18f),
                new Vector3(0.60f, 0.30f, 0.18f), new Vector3(0.18f, 0.30f, 0.12f),
                new Vector3(0.82f, 0.30f, 0.12f),
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float density = 0f;
                foreach (var puff in puffs)
                {
                    float dx = (u - puff.x) / puff.z, dy = (v - puff.y) / (puff.z * 2f);
                    density += Mathf.Exp(-(dx * dx + dy * dy) * 2.5f);
                }
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((density - 0.25f) * 1.6f));
                a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v * 5f));  // softened base
                // A bit of shadow at the bottom of the cloud for depth.
                float shade = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(v * 1.6f));
                pixels[y * w + x] = new Color(shade, shade, shade, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            softCloud = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            return softCloud;
        }
    }
}
