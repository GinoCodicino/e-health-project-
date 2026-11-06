using UnityEngine;

/// <summary>
/// 3D scenery of the mini-game, built at startup: the lanes' wall, cliffs on each side,
/// the background mountains and pine trees, the starting ground and the summit with its flag.
/// Any empty field is replaced by a simple colored shape.
/// The clouds and the farthest row of mountains are always rebuilt at startup
/// (they are not saved in the scene): see BuildBackdrop.
/// To edit it by hand: component ⋮ menu → "Generate scenery in scene", then save the scene.
/// The game then reuses this scenery instead of building a new one.
/// </summary>
public class ClimbScenery : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [Tooltip("Empty = found in the scene. Used to tint the clouds from the sky.")]
    [SerializeField] private ClimbCamera view;
    [Tooltip("Empty = found in the scene. Used to place the cloud ceiling where the rain starts.")]
    [SerializeField] private ClimbWeather weather;

    [Header("Wall")]
    [Tooltip("Lane materials, alternating (e.g. MountainRidge_Rock / MountainRidge_RockLight).")]
    public Material[] laneMaterials;
    [Tooltip("Cliffs placed on each side of the lanes (e.g. Cliff_1 to Cliff_8).")]
    public GameObject[] sideCliffs;
    [Tooltip("Material forced on the cliffs (empty = the prefab's).")]
    public Material cliffMaterial;
    [SerializeField] private float cliffSpacing = 4.5f;
    [SerializeField] private float cliffSize = 6f;
    [Tooltip("Small rocks embedded in the wall to give it relief (e.g. Rock_Small_1 to 5).")]
    public GameObject[] wallDetails;
    public Material wallDetailMaterial;
    [SerializeField] private float detailsPerMeter = 1.2f;

    [Header("Background")]
    public GameObject[] farMountains;
    public GameObject[] pines;
    public Material groundMaterial;
    [Tooltip("Background mountains start at this depth (m).")]
    [SerializeField] private float mountainsMinZ = 70f;
    [Tooltip("Fog distance that blends the mountains into the sky (m).")]
    [SerializeField] private Vector2 fogRange = new Vector2(60f, 350f);
    [Tooltip("Material forced on the distant mountains (empty = the prefab's).")]
    public Material mountainMaterial;

    [Header("Clouds")]
    [Tooltip("Cloud sprites (empty = soft cloud generated in code).")]
    public Sprite[] cloudSprites;
    [SerializeField] private float cloudDriftSpeed = 0.6f;
    [Tooltip("Opacity of clouds passing in front of the wall at the cloud ceiling (keep low for readability).")]
    [Range(0f, 1f)] [SerializeField] private float frontCloudAlpha = 0.2f;

    [Header("Summit")]
    public Material flagPoleMaterial;
    public Material flagClothMaterial;

    /// <summary>Depth of the wall face: the climber is at z = 0, just in front.</summary>
    public const float WallZ = 0.5f;

    private const string RootName = "Scenery";
    private const string BackdropName = "Backdrop";
    private Transform root;

    // Clouds built at startup: each drifts along x and takes the sky tint based on its depth.
    private struct Cloud
    {
        public SpriteRenderer renderer;
        public float speed, haze, alpha, range;
    }
    private readonly System.Collections.Generic.List<Cloud> clouds = new System.Collections.Generic.List<Cloud>();
    private static Sprite softCloud;

    /// <summary>
    /// Called at startup. If the scenery was already generated in the scene (component ⋮ menu),
    /// it is kept as is, with any manual tweaks; otherwise it is built.
    /// </summary>
    public void Build(float summitHeight)
    {
        ApplyFog();
        if (transform.Find(RootName) == null) Generate(summitHeight);
        BuildBackdrop(summitHeight);
    }

    private void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = fogRange.x;
        RenderSettings.fogEndDistance = fogRange.y;
    }

    private void Generate(float summitHeight)
    {
        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildWall(summitHeight);
        BuildBackground();
        BuildSummit(summitHeight);
    }

#if UNITY_EDITOR
    // Right-click the component (or ⋮ menu) in the Inspector.
    [ContextMenu("Generate scenery in scene")]
    private void GenerateInEditor()
    {
        var manager = GetComponent<ClimbGameManager>();
        if (manager == null) manager = FindFirstObjectByType<ClimbGameManager>();
        float summitHeight = manager != null ? manager.SummitHeight : 120f;

        ClearInEditor();
        ApplyFog();
        Generate(summitHeight);
        UnityEditor.Undo.RegisterCreatedObjectUndo(root.gameObject, "Generate scenery");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [ContextMenu("Delete scenery from scene")]
    private void ClearInEditor()
    {
        var existing = transform.Find(RootName);
        if (existing != null) UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);
    }
#endif

    private void BuildWall(float summitHeight)
    {
        float bottom = -4f, top = summitHeight + 6f;
        float height = top - bottom;

        // One strip of rock per lane.
        for (int i = 0; i < climber.laneCount; i++)
        {
            var lane = ModelFactory.Create($"Lane{i}", root, null, PrimitiveType.Cube,
                                           i % 2 == 0 ? new Color(0.5f, 0.47f, 0.44f) : new Color(0.56f, 0.53f, 0.5f),
                                           new Vector3(climber.laneWidth, height, 1f));
            lane.transform.position = new Vector3(climber.LaneX(i), bottom + height / 2f, WallZ + 0.5f);
            if (laneMaterials != null && laneMaterials.Length > 0)
                ModelFactory.SetMaterial(lane, laneMaterials[i % laneMaterials.Length]);
        }

        // Staggered cliffs on each side, to frame the lanes.
        float halfWidth = climber.laneCount * climber.laneWidth / 2f;
        var random = new System.Random(3);
        for (float y = bottom; y < top; y += cliffSpacing)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject prefab = Pick(sideCliffs, random);
                float size = cliffSize * (0.85f + 0.3f * (float)random.NextDouble());
                var cliff = ModelFactory.Create("Cliff", root, prefab, PrimitiveType.Cube,
                                                new Color(0.45f, 0.42f, 0.4f), new Vector3(size, size, size));
                cliff.transform.position = new Vector3(side * (halfWidth + size * 0.5f),
                                                       y + (side > 0 ? cliffSpacing / 2f : 0f), WallZ + size * 0.4f);
                // Flipped: their flat face (meant to sit on the ground) points up,
                // where the low-angle camera can't see it.
                cliff.transform.rotation = Quaternion.Euler(180f, random.Next(4) * 90f, 0f);
                ModelFactory.SetMaterial(cliff, cliffMaterial);
            }
        }

        // Small rocks half sunk into the lanes' face.
        int details = Mathf.RoundToInt(height * detailsPerMeter);
        for (int i = 0; i < details; i++)
        {
            float size = 0.15f + 0.25f * (float)random.NextDouble();
            var rock = ModelFactory.Create("WallDetail", root, Pick(wallDetails, random), PrimitiveType.Cube,
                                           new Color(0.4f, 0.37f, 0.35f), Vector3.one * size);
            float x = ((float)random.NextDouble() * 2f - 1f) * (halfWidth - 0.15f);
            rock.transform.position = new Vector3(x, bottom + height * (float)random.NextDouble(), WallZ);
            rock.transform.rotation = Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360));
            ModelFactory.SetMaterial(rock, wallDetailMaterial);
        }
    }

    private void BuildBackground()
    {
        var random = new System.Random(7);

        var ground = ModelFactory.Create("Ground", root, null, PrimitiveType.Cube,
                                         new Color(0.35f, 0.6f, 0.3f), new Vector3(120f, 1f, 60f));
        ground.transform.position = new Vector3(0f, -1.1f, -10f);
        ModelFactory.SetMaterial(ground, groundMaterial);

        // Pine trees at the foot of the wall, on both sides.
        for (int i = 0; i < 14; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * (6f + 18f * (float)random.NextDouble());
            float z = -6f + 14f * (float)random.NextDouble();
            float h = 4f + 3f * (float)random.NextDouble();
            var pine = ModelFactory.Create("Pine", root, Pick(pines, random), PrimitiveType.Capsule,
                                           new Color(0.2f, 0.45f, 0.25f), new Vector3(h * 0.5f, h, h * 0.5f));
            pine.transform.position = new Vector3(x, -0.6f + h / 2f, z);
        }

        // Distant mountains behind the wall: visible on the sides.
        // These models are very deep: each is pushed back until its front face is at mountainsMinZ.
        for (int i = 0; i < 7; i++)
        {
            float x = -180f + 60f * i + 20f * (float)random.NextDouble();
            float h = 60f + 50f * (float)random.NextDouble();
            var mountain = ModelFactory.Create("Mountain", root, Pick(farMountains, random), PrimitiveType.Cube,
                                               new Color(0.6f, 0.65f, 0.72f), new Vector3(h * 1.6f, h, h));
            mountain.transform.rotation = Quaternion.Euler(0f, random.Next(360), 0f);
            mountain.transform.position = new Vector3(x, h / 2f - 12f, 0f);
            float front = ModelFactory.WorldBounds(mountain).min.z;
            mountain.transform.position += Vector3.forward * (mountainsMinZ - front + 40f * (float)random.NextDouble());
            ModelFactory.SetMaterial(mountain, mountainMaterial);
        }
    }

    // ---------- Backdrop rebuilt at every startup ----------

    private void BuildBackdrop(float summitHeight)
    {
        var old = transform.Find(BackdropName);
        if (old != null) ModelFactory.DestroySafe(old.gameObject);
        clouds.Clear();

        var backdrop = new GameObject(BackdropName).transform;
        backdrop.SetParent(transform, false);
        if (view == null) view = FindFirstObjectByType<ClimbCamera>();
        if (weather == null) weather = FindFirstObjectByType<ClimbWeather>();

        BuildHighPeaks(backdrop, summitHeight);
        BuildClouds(backdrop, summitHeight);
    }

    // Large distant peaks, tall enough to stay visible on the sides up to the top of the route.
    // Placed near the end of the fog: they show up as pale, horizon-colored silhouettes.
    private const float PeakSink = 70f;

    private void BuildHighPeaks(Transform parent, float summitHeight)
    {
        var random = new System.Random(11);
        float depth = Mathf.Max(fogRange.y * 0.7f, 40f);
        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * (25f + 30f * (i / 2) + 10f * (float)random.NextDouble());
            float h = summitHeight * (1.2f + 0.6f * (float)random.NextDouble());
            var peak = ModelFactory.Create("Peak", parent, Pick(farMountains, random), PrimitiveType.Cube,
                                           new Color(0.6f, 0.65f, 0.72f), new Vector3(h * 4f, h, h));  // generous width: the height sets the scale
            peak.transform.rotation = Quaternion.Euler(0f, random.Next(360), 0f);
            // Base sunk deep: the low-angle camera must never see the models' flat bottom.
            peak.transform.position = new Vector3(x, 0f, 0f);
            Bounds bounds = ModelFactory.WorldBounds(peak);
            peak.transform.position += new Vector3(0f, -PeakSink - bounds.min.y,
                                                   depth - bounds.min.z + 20f * (float)random.NextDouble());
            ModelFactory.SetMaterial(peak, mountainMaterial);
        }
    }

    private void BuildClouds(Transform parent, float summitHeight)
    {
        var random = new System.Random(5);
        float top = summitHeight + 25f;

        // Three layers behind the wall: perspective makes them scroll at different speeds.
        //        depth, count, min-max width, x spread, haze (share of horizon color), opacity
        AddCloudLayer(parent, random, 60f, 22, 20f, 34f, 110f, -10f, top, 0.65f, 0.9f);
        AddCloudLayer(parent, random, 30f, 16, 10f, 18f, 60f, -5f, top, 0.35f, 0.85f);
        AddCloudLayer(parent, random, 12f, 10, 6f, 10f, 35f, 0f, top, 0.15f, 0.8f);

        // Cloud ceiling where the rain begins: a dense layer behind, and a light veil in front of the wall.
        float ceiling = CeilingHeight(summitHeight);
        AddCloudLayer(parent, random, 20f, 18, 14f, 24f, 50f, ceiling - 6f, ceiling + 6f, 0.25f, 0.95f);
        AddCloudLayer(parent, random, -3f, 7, 8f, 14f, 22f, ceiling - 3f, ceiling + 3f, 0.1f, frontCloudAlpha);
    }

    private void AddCloudLayer(Transform parent, System.Random random, float z, int count,
                               float minWidth, float maxWidth, float spreadX, float minY, float maxY,
                               float haze, float alpha)
    {
        for (int i = 0; i < count; i++)
        {
            float width = Mathf.Lerp(minWidth, maxWidth, (float)random.NextDouble());
            Sprite sprite = cloudSprites != null && cloudSprites.Length > 0
                ? cloudSprites[random.Next(cloudSprites.Length)] : null;
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
                speed = cloudDriftSpeed * (0.5f + (float)random.NextDouble()) * (z < 0f ? 1.5f : 1f) / (1f + Mathf.Max(z, 0f) / 30f),
                haze = haze,
                alpha = alpha,
                range = spreadX + maxWidth,
            });
        }
    }

    // The cloud ceiling sits at the start of the first rainy section (otherwise halfway up).
    private float CeilingHeight(float summitHeight)
    {
        if (weather != null)
            foreach (var section in weather.sections)
                if (section.rain) return section.startHeight;
        return summitHeight * 0.5f;
    }

    private void Update()
    {
        if (clouds.Count == 0) return;
        Color horizon = view != null ? view.HorizonColor : RenderSettings.fogColor;
        // In bad weather the horizon darkens: the clouds turn gray with it.
        Color tint = Color.Lerp(Color.white, horizon * 1.3f, 0.3f);
        float dt = Time.deltaTime;

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

    private void BuildSummit(float summitHeight)
    {
        float width = climber.laneCount * climber.laneWidth + 2f;
        var ledge = ModelFactory.Create("Summit", root, null, PrimitiveType.Cube,
                                        new Color(0.92f, 0.94f, 0.97f), new Vector3(width, 0.8f, 3f));
        ledge.transform.position = new Vector3(0f, summitHeight + 0.4f, WallZ);

        var pole = ModelFactory.Create("FlagPole", root, null, PrimitiveType.Cylinder,
                                       new Color(0.4f, 0.3f, 0.2f), new Vector3(0.08f, 1.4f, 0.08f));
        pole.transform.position = new Vector3(width / 2f - 1f, summitHeight + 2.2f, WallZ);
        ModelFactory.SetMaterial(pole, flagPoleMaterial);

        var cloth = ModelFactory.Create("FlagCloth", root, null, PrimitiveType.Cube,
                                        new Color(0.9f, 0.2f, 0.2f), new Vector3(0.9f, 0.55f, 0.04f));
        cloth.transform.position = new Vector3(width / 2f - 1f - 0.48f, summitHeight + 3.2f, WallZ);
        ModelFactory.SetMaterial(cloth, flagClothMaterial);
    }

    private static GameObject Pick(GameObject[] prefabs, System.Random random) =>
        prefabs != null && prefabs.Length > 0 ? prefabs[random.Next(prefabs.Length)] : null;
}
