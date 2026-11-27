using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Genera l'ingresso di una baita d'alta quota in stile low-poly:
/// sentiero di pietre, aiuole con fiori alpini (stella alpina, genziana, rododendro,
/// arnica, nontiscordardimé), pini mughi, ginepri, massi, catasta di legna,
/// panca, fontana di legno, lanterne e oggetti da cercatore di funghi
/// (cesti, essiccatoio, cassette, ceppo per pulirli, insegna, funghi selvatici).
///
/// USO:
/// 1. Crea un GameObject vuoto, mettilo a terra DAVANTI ALLA PORTA.
/// 2. Ruotalo in modo che la freccia BLU (Z) punti VERSO L'ESTERNO della casa.
/// 3. Aggiungi questo script e fai click destro sul titolo del componente -> "Genera ingresso baita".
/// Tutto è annullabile con Ctrl+Z, oppure con "Rimuovi ingresso".
/// </summary>
public class IngressoBaitaGenerator : MonoBehaviour
{
    [Header("Generale")]
    [Tooltip("Ingrandisce/rimpicciolisce tutto (1 = misure in metri)")]
    public float scale = 1f;
    [Tooltip("Cambia questo numero per ottenere una disposizione diversa")]
    public int seed = 12345;

    [Header("Dimensioni (metri, prima della scala)")]
    public float pathLength = 6f;
    public float pathWidth = 1.2f;
    public float bedLength = 3f;          // lunghezza aiuola lungo la facciata
    public float bedDepth = 1.3f;         // profondità aiuola
    public float bedDistanceFromWall = 0.3f;

    [Header("Elementi")]
    public bool stonePath = true;
    public bool flowerBeds = true;
    public bool pathFlowers = true;
    public bool bushes = true;
    public bool boulders = true;
    public bool woodpile = true;
    public bool bench = true;
    public bool waterTrough = true;
    public bool lanterns = true;
    public bool lanternLights = true;
    public bool grassTufts = true;
    [Header("Casa del cercatore di funghi")]
    public bool mushroomBaskets = true;     // cesti pieni di porcini e finferli
    public bool dryingRack = true;          // essiccatoio con funghi a fette
    public bool mushroomCrates = true;      // cassette di legno impilate
    public bool cleaningStump = true;       // ceppo con coltello per pulire i funghi
    public bool mushroomSign = true;        // insegna con un grande fungo
    public bool walkingStick = true;        // bastone appoggiato vicino alla porta
    public bool wildMushrooms = true;       // funghi selvatici sparsi nel prato
    [Tooltip("Quanti funghi ci sono. Bassa = stagione scarsa: cesti e cassette quasi vuoti (come dice il dialogo)")]
    [Range(0f, 1f)] public float mushroomAbundance = 0.12f;
    [Tooltip("Collider su massi, catasta, panca, fontana e lanterne (il giocatore non ci passa attraverso)")]
    public bool addColliders = true;

    [Header("Fiori")]
    [Range(2f, 20f)] public float flowerDensity = 8f;   // fiori per metro quadro nelle aiuole

    [SerializeField, HideInInspector] GameObject generatedRoot;

    // ---------------- Colori ----------------
    static readonly Color Soil = new Color(0.30f, 0.20f, 0.13f);
    static readonly Color[] StoneCols = { new Color(0.55f, 0.55f, 0.52f), new Color(0.62f, 0.60f, 0.56f), new Color(0.48f, 0.47f, 0.45f) };
    static readonly Color[] PathStoneCols = { new Color(0.80f, 0.78f, 0.72f), new Color(0.74f, 0.72f, 0.67f), new Color(0.85f, 0.83f, 0.78f) };
    static readonly Color[] MugoCols = { new Color(0.15f, 0.32f, 0.17f), new Color(0.19f, 0.38f, 0.20f), new Color(0.13f, 0.28f, 0.16f) };
    static readonly Color Juniper = new Color(0.24f, 0.40f, 0.33f);
    static readonly Color LeafGreen = new Color(0.30f, 0.52f, 0.24f);
    static readonly Color LeafGrey = new Color(0.56f, 0.64f, 0.52f);   // foglie feltrose della stella alpina
    static readonly Color RoseLeaf = new Color(0.18f, 0.35f, 0.19f);
    static readonly Color White = new Color(0.94f, 0.94f, 0.89f);
    static readonly Color Yellow = new Color(0.95f, 0.80f, 0.22f);
    static readonly Color Gentian = new Color(0.13f, 0.25f, 0.85f);
    static readonly Color[] RoseCols = { new Color(0.90f, 0.25f, 0.45f), new Color(0.80f, 0.18f, 0.38f) };
    static readonly Color Arnica = new Color(1.00f, 0.66f, 0.12f);
    static readonly Color ArnicaMid = new Color(0.85f, 0.45f, 0.08f);
    static readonly Color Myosotis = new Color(0.45f, 0.65f, 1.00f);
    static readonly Color[] GrassCols = { new Color(0.34f, 0.58f, 0.24f), new Color(0.42f, 0.64f, 0.28f) };
    static readonly Color Wood = new Color(0.47f, 0.31f, 0.18f);
    static readonly Color WoodDark = new Color(0.33f, 0.21f, 0.12f);
    static readonly Color WoodEnd = new Color(0.78f, 0.62f, 0.42f);
    static readonly Color Water = new Color(0.32f, 0.62f, 0.86f);
    static readonly Color Metal = new Color(0.16f, 0.16f, 0.17f);
    static readonly Color Glow = new Color(1.00f, 0.78f, 0.40f);

    static readonly Color MushStem = new Color(0.90f, 0.86f, 0.74f);
    static readonly Color[] PorcinoCaps = { new Color(0.48f, 0.28f, 0.14f), new Color(0.58f, 0.36f, 0.18f) };
    static readonly Color AmanitaRed = new Color(0.86f, 0.12f, 0.10f);
    static readonly Color Finferlo = new Color(0.98f, 0.66f, 0.16f);
    static readonly Color Wicker = new Color(0.64f, 0.45f, 0.24f);
    static readonly Color WickerDark = new Color(0.50f, 0.34f, 0.18f);
    static readonly Color LightWood = new Color(0.72f, 0.56f, 0.36f);
    static readonly Color DriedSlice = new Color(0.70f, 0.52f, 0.32f);
    static readonly Color Rope = new Color(0.80f, 0.70f, 0.50f);

    static readonly string[] FlowerNames = { "Stella_Alpina", "Genziana", "Rododendro", "Arnica", "Nontiscordardime" };

    // ---------------- Stato di generazione ----------------
    System.Random rng;
    Transform root;
    Mesh cubeMesh;
    List<Mesh> rockPool, blobPool, flatStonePool;
    readonly Dictionary<string, Mesh> prismCache = new Dictionary<string, Mesh>();
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    readonly List<Transform> colliderTargets = new List<Transform>();

    // =====================================================================
    //  COMANDI (click destro sul titolo del componente)
    // =====================================================================

    [ContextMenu("Genera ingresso baita")]
    public void Generate()
    {
        Clear();
        Physics.SyncTransforms();

        rng = new System.Random(seed);
        mats.Clear();
        prismCache.Clear();
        colliderTargets.Clear();
        BuildPools();

        generatedRoot = new GameObject("IngressoBaita_Generated");
        generatedRoot.transform.SetParent(transform.parent, false);
        generatedRoot.transform.position = Vector3.zero;
        generatedRoot.transform.rotation = Quaternion.identity;
        root = generatedRoot.transform;

        float hw = pathWidth * 0.5f;
        float L = bedLength, D = bedDepth, W = bedDistanceFromWall;
        float bedX = hw + 0.25f + L * 0.5f;
        float outer = hw + 0.25f + L;

        if (stonePath) BuildPath();

        if (flowerBeds)
        {
            Bed("Aiuola_Sinistra", -bedX, W + D * 0.5f);
            Bed("Aiuola_Destra", bedX, W + D * 0.5f);
        }

        if (pathFlowers) BuildPathFlowers();

        if (bushes)
        {
            var g = Container("Cespugli");
            Mugo(g, outer + 0.55f, W + 0.5f, 0.85f);
            Mugo(g, -(hw + 2.1f), pathLength * 0.55f, 0.7f);
            Mugo(g, outer + 1.4f, pathLength * 0.6f, 0.6f);
            Ginepro(g, -(hw + 1.05f), pathLength - 0.5f, 0.55f);
            Ginepro(g, hw + 1.05f, pathLength - 0.5f, 0.55f);
        }

        if (boulders)
        {
            var g = Container("Massi");
            Boulder(g, -(hw + 2.7f), pathLength * 0.8f, 0.9f);
            Boulder(g, hw + 2.4f, pathLength * 0.72f, 0.7f);
            Boulder(g, -(outer + 1.8f), D + 1.9f, 0.6f);
            Boulder(g, outer + 2.1f, pathLength * 0.95f, 1.0f);
            Boulder(g, hw + 1.7f, pathLength + 0.9f, 0.5f);
        }

        if (woodpile) Woodpile(-(outer + 0.9f), 0.6f);
        if (bench) Bench(bedX, W + D + 0.65f);
        if (waterTrough) Trough(outer + 0.75f, W + D + 1.7f);

        if (lanterns)
        {
            Lantern(-(hw + 0.35f), pathLength - 0.2f);
            Lantern(hw + 0.35f, pathLength - 0.2f);
        }

        // ---- Casa del cercatore di funghi ----
        if (mushroomBaskets)
        {
            Transform benchT = root.Find("Panca");
            if (benchT != null) Basket(benchT, new Vector3(-0.3f, 0.455f, 0f), 15f);   // un cesto sopra la panca
            var b2 = Group("Cesto_Terra", root, bedX + 0.95f, W + D + 0.75f, R(0, 360), 0f);
            Basket(b2, Vector3.zero, 0f);
        }
        if (dryingRack) DryingRack(-bedX, W + D + 0.85f);
        if (mushroomCrates) Crates(-(outer + 0.9f), W + D + 0.55f);
        if (cleaningStump) CleaningStump(outer + 0.45f, W + D + 0.35f);
        if (mushroomSign) Sign(-(hw + 1.9f), pathLength + 0.25f);
        if (walkingStick) WalkingStick(hw + 0.18f, 0.18f);
        if (wildMushrooms) ScatterMushrooms(Mathf.Max(1, Mathf.RoundToInt(10 * mushroomAbundance) + 1));

        if (grassTufts) ScatterGrass(18);

        if (addColliders) AddColliders();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<Light>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(generatedRoot, "Genera ingresso baita");
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        Debug.Log($"[IngressoBaita] Generati {root.GetComponentsInChildren<Transform>().Length - 1} oggetti.", this);
    }

    [ContextMenu("Rimuovi ingresso")]
    public void Clear()
    {
        if (generatedRoot == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { Undo.DestroyObjectImmediate(generatedRoot); generatedRoot = null; return; }
#endif
        Destroy(generatedRoot);
        generatedRoot = null;
    }

    // =====================================================================
    //  ELEMENTI
    // =====================================================================

    void BuildPath()
    {
        var g = Container("Sentiero_Pietre");
        float z = 0.5f; int i = 0;
        while (z < pathLength)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * pathWidth * 0.18f + R(-0.08f, 0.08f);
            float rad = R(0.26f, 0.36f);
            var s = Group("Pietra", g, x, z, R(0, 360), 0.02f);
            MeshPart("Mesh", s, Pick(flatStonePool), Pick(PathStoneCols), Vector3.zero, new Vector3(rad, 0.4f, rad), Vector3.zero);
            z += R(0.45f, 0.6f); i++;
        }
    }

    void Bed(string name, float cx, float cz)
    {
        float L = bedLength, D = bedDepth;
        var b = Group(name, root, cx, cz, 0f, 0f);

        Box("Terra", b, Soil, new Vector3(0, 0.05f, 0), new Vector3(L, 0.12f, D), Vector3.zero);

        // bordo di sassi
        float per = 2f * (L + D);
        int n = Mathf.Max(8, Mathf.RoundToInt(per / 0.26f));
        for (int k = 0; k < n; k++)
        {
            Vector3 p = PerimeterPoint(k * per / n, L, D);
            float s = R(0.1f, 0.15f);
            MeshPart("Sasso", b, Pick(rockPool), Pick(StoneCols), p + Vector3.up * 0.05f,
                     new Vector3(s, s * 0.75f, s), new Vector3(0, R(0, 360), 0));
        }

        // fiori a "macchie" naturali
        int count = Mathf.RoundToInt(flowerDensity * L * D);
        float off = R(0, 100);
        for (int k = 0; k < count; k++)
        {
            float x = R(-L * 0.5f + 0.15f, L * 0.5f - 0.15f);
            float z = R(-D * 0.5f + 0.15f, D * 0.5f - 0.15f);
            float noise = Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(x * 1.3f + off, z * 1.3f + off));
            int type = rng.NextDouble() < 0.3 ? rng.Next(5) : Mathf.Clamp((int)(noise * 5f), 0, 4);
            if (z < -D * 0.2f && rng.NextDouble() < 0.35) type = 2;   // rododendri verso il muro
            Flower(b, new Vector3(x, 0.11f, z), type);
        }
    }

    void BuildPathFlowers()
    {
        var g = Container("Fiori_Sentiero");
        float hw = pathWidth * 0.5f;
        for (float z = bedDistanceFromWall + bedDepth + 0.7f; z < pathLength - 0.4f; z += R(0.8f, 1.2f))
        {
            foreach (int side in new[] { -1, 1 })
            {
                var c = Group("Ciuffo_Fiori", g, side * (hw + R(0.2f, 0.4f)), z + R(-0.15f, 0.15f), 0f, 0f);
                FlowerCluster(c, rng.Next(3, 7), 0.22f);
                if (grassTufts && rng.NextDouble() < 0.5) Grass(c, new Vector3(side * 0.18f, 0, R(-0.1f, 0.1f)));
            }
        }
    }

    void Flower(Transform parent, Vector3 lp, int type)
    {
        var f = new GameObject(FlowerNames[type]).transform;
        f.SetParent(parent, false);
        f.localPosition = lp;
        f.localRotation = Quaternion.Euler(0, R(0, 360), 0);
        f.localScale = Vector3.one * R(0.8f, 1.25f);

        switch (type)
        {
            case 0: // Stella alpina: stella bianca con centro giallo
                {
                    float h = R(0.1f, 0.16f);
                    MeshPart("Foglie", f, Pick(blobPool), LeafGrey, new Vector3(0, 0.015f, 0), new Vector3(0.09f, 0.035f, 0.09f), Vector3.zero);
                    Box("Stelo", f, LeafGrey, new Vector3(0, h * 0.5f, 0), new Vector3(0.018f, h, 0.018f), Vector3.zero);
                    for (int p = 0; p < 6; p++)
                    {
                        float a = p * 60f;
                        Vector3 pos = Quaternion.Euler(0, a, 0) * new Vector3(0.028f, 0, 0) + new Vector3(0, h, 0);
                        Box("Petalo", f, White, pos, new Vector3(0.055f, 0.012f, 0.022f), new Vector3(0, a, 0));
                    }
                    MeshPart("Centro", f, Pick(blobPool), Yellow, new Vector3(0, h + 0.008f, 0), new Vector3(0.022f, 0.014f, 0.022f), Vector3.zero);
                    break;
                }
            case 1: // Genziana: campanella blu rivolta in alto
                {
                    MeshPart("Foglie", f, Pick(blobPool), LeafGreen, new Vector3(0, 0.012f, 0), new Vector3(0.08f, 0.03f, 0.08f), Vector3.zero);
                    int bells = rng.NextDouble() < 0.4 ? 2 : 1;
                    for (int k = 0; k < bells; k++)
                    {
                        float h = R(0.03f, 0.06f);
                        Vector3 o = k == 0 ? Vector3.zero : new Vector3(0.04f, 0, 0.02f);
                        Box("Stelo", f, LeafGreen, o + new Vector3(0, h * 0.5f, 0), new Vector3(0.015f, h, 0.015f), Vector3.zero);
                        MeshPart("Fiore", f, Prism(5, 0.018f, 0.048f, 0.09f), Gentian, o + new Vector3(0, h, 0), Vector3.one, Vector3.zero);
                    }
                    break;
                }
            case 2: // Rododendro (rosa delle Alpi): cespuglietto con fiori rosa
                {
                    float bs = R(0.18f, 0.26f);
                    MeshPart("Cespuglio", f, Pick(blobPool), RoseLeaf, new Vector3(0, bs * 0.55f, 0), new Vector3(bs, bs * 0.75f, bs), Vector3.zero);
                    int n = rng.Next(5, 9);
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 dir = new Vector3(R(-1, 1), R(0.2f, 1f), R(-1, 1)).normalized;
                        Vector3 pos = new Vector3(dir.x * bs * 0.95f, bs * 0.55f + dir.y * bs * 0.72f, dir.z * bs * 0.95f);
                        MeshPart("Fiore", f, Pick(blobPool), Pick(RoseCols), pos, Vector3.one * 0.05f, Vector3.zero);
                    }
                    break;
                }
            case 3: // Arnica: margherita gialla-arancio
                {
                    float h = R(0.16f, 0.24f);
                    MeshPart("Foglie", f, Pick(blobPool), LeafGreen, new Vector3(0, 0.015f, 0), new Vector3(0.08f, 0.03f, 0.08f), Vector3.zero);
                    Box("Stelo", f, LeafGreen, new Vector3(0, h * 0.5f, 0), new Vector3(0.016f, h, 0.016f), Vector3.zero);
                    MeshPart("Petali", f, Prism(8, 0.045f, 0.05f, 0.012f), Arnica, new Vector3(0, h, 0), Vector3.one, Vector3.zero);
                    MeshPart("Centro", f, Pick(blobPool), ArnicaMid, new Vector3(0, h + 0.014f, 0), new Vector3(0.02f, 0.012f, 0.02f), Vector3.zero);
                    break;
                }
            default: // Nontiscordardimé: tanti puntini azzurri
                {
                    MeshPart("Foglie", f, Pick(blobPool), LeafGreen, new Vector3(0, 0.015f, 0), new Vector3(0.09f, 0.035f, 0.09f), Vector3.zero);
                    int n = rng.Next(5, 8);
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 pos = new Vector3(R(-0.045f, 0.045f), R(0.05f, 0.085f), R(-0.045f, 0.045f));
                        MeshPart("Fiore", f, Pick(blobPool), Myosotis, pos, Vector3.one * 0.022f, Vector3.zero);
                    }
                    break;
                }
        }
    }

    void FlowerCluster(Transform parent, int count, float radius)
    {
        for (int k = 0; k < count; k++)
        {
            int type = rng.Next(5);
            if (type == 2 && rng.NextDouble() < 0.6) type = 0;   // pochi rododendri sparsi
            Vector3 p = new Vector3(R(-radius, radius), 0, R(-radius, radius));
            Flower(parent, p, type);
        }
    }

    void Grass(Transform parent, Vector3 lp)
    {
        var g = new GameObject("Erba").transform;
        g.SetParent(parent, false);
        g.localPosition = lp;
        int n = rng.Next(4, 8);
        for (int k = 0; k < n; k++)
        {
            float h = R(0.12f, 0.26f);
            MeshPart("Filo", g, Prism(3, 0.025f, 0f, 1f), Pick(GrassCols),
                     new Vector3(R(-0.05f, 0.05f), 0, R(-0.05f, 0.05f)),
                     new Vector3(1, h, 1), new Vector3(R(-20, 20), R(0, 360), R(-20, 20)));
        }
    }

    void ScatterGrass(int count)
    {
        var g = Container("Erba_Sparsa");
        float hw = pathWidth * 0.5f;
        float maxX = hw + bedLength + 2.5f;
        for (int k = 0; k < count; k++)
        {
            float x = R(-maxX, maxX);
            if (Mathf.Abs(x) < hw + 0.2f) x += Mathf.Sign(x == 0 ? 1 : x) * (hw + 0.3f);
            float z = R(bedDistanceFromWall + bedDepth + 0.5f, pathLength + 1.2f);
            var c = Group("Ciuffo_Erba", g, x, z, 0f, 0f);
            Grass(c, Vector3.zero);
            if (rng.NextDouble() < 0.3) FlowerCluster(c, rng.Next(1, 3), 0.12f);
        }
    }

    void Mugo(Transform parent, float x, float z, float size)
    {
        var b = Group("Pino_Mugo", parent, x, z, R(0, 360), 0.05f);
        int n = rng.Next(3, 5);
        for (int k = 0; k < n; k++)
        {
            float s = size * R(0.6f, 1f);
            Vector3 p = new Vector3(R(-0.4f, 0.4f) * size, s * 0.45f, R(-0.4f, 0.4f) * size);
            MeshPart("Chioma", b, Pick(blobPool), Pick(MugoCols), p, new Vector3(s, s * 0.7f, s), new Vector3(0, R(0, 360), 0));
        }
    }

    void Ginepro(Transform parent, float x, float z, float size)
    {
        var b = Group("Ginepro", parent, x, z, R(0, 360), 0.03f);
        MeshPart("Chioma", b, Pick(blobPool), Juniper, new Vector3(0, size * 0.9f, 0), new Vector3(size * 0.65f, size * 1.1f, size * 0.65f), Vector3.zero);
        MeshPart("Base", b, Pick(blobPool), Pick(MugoCols), new Vector3(0, size * 0.3f, 0), new Vector3(size * 0.8f, size * 0.5f, size * 0.8f), Vector3.zero);
    }

    void Boulder(Transform parent, float x, float z, float size)
    {
        var b = Group("Masso", parent, x, z, R(0, 360), size * 0.22f);
        MeshPart("Roccia", b, Pick(rockPool), Pick(StoneCols), new Vector3(0, size * 0.5f, 0),
                 new Vector3(size, size * R(0.55f, 0.8f), size * R(0.8f, 1.1f)), new Vector3(R(-10, 10), 0, R(-10, 10)));
        if (rng.NextDouble() < 0.6)
        {
            float s2 = size * R(0.35f, 0.5f);
            MeshPart("Roccia_Piccola", b, Pick(rockPool), Pick(StoneCols), new Vector3(size * 0.75f, s2 * 0.4f, R(-0.3f, 0.3f) * size),
                     new Vector3(s2, s2 * 0.7f, s2), new Vector3(0, R(0, 360), 0));
        }
        colliderTargets.Add(b);

        // fiori ed erba ai piedi del masso
        var deco = Group("Fiori_Masso", parent, x + R(-0.6f, 0.6f) * size, z - size * 0.8f, 0f, 0f);
        FlowerCluster(deco, rng.Next(2, 5), 0.2f);
        if (grassTufts) Grass(deco, new Vector3(0.15f, 0, 0));
        if (wildMushrooms && Rng01() < mushroomAbundance) MushroomPatch(deco, new Vector3(-0.2f, 0, 0.1f), 1, true);
    }

    void Woodpile(float x, float z)
    {
        var g = Group("Catasta_Legna", root, x, z, 0f, 0f);
        int rows = 4, perRow = 6;
        float logR = 0.09f, logLen = 0.7f;
        for (int row = 0; row < rows; row++)
        {
            int n = perRow - (row == rows - 1 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                float lx = (i - (n - 1) * 0.5f) * (logR * 2.1f) + (row % 2) * logR;
                float ly = logR + row * logR * 1.8f;
                float jz = R(-0.04f, 0.04f);
                // il tronco è costruito lungo Y: ruotato di 90° su X diventa lungo Z
                MeshPart("Tronco", g, Prism(6, logR, logR, logLen), Wood,
                         new Vector3(lx, ly, -logLen * 0.5f + jz), Vector3.one, new Vector3(90, R(0, 60), 0));
                MeshPart("Taglio", g, Prism(6, logR * 0.85f, logR * 0.85f, 0.01f), WoodEnd,
                         new Vector3(lx, ly, logLen * 0.5f + jz), Vector3.one, new Vector3(90, R(0, 60), 0));
            }
        }
        // pali e tettoia
        float w = perRow * logR * 2.1f * 0.5f + 0.08f;
        float top = rows * logR * 1.8f + 0.25f;
        foreach (int s in new[] { -1, 1 })
            Box("Palo", g, WoodDark, new Vector3(s * w, top * 0.5f, 0), new Vector3(0.08f, top, 0.08f), Vector3.zero);
        Box("Tettoia", g, WoodDark, new Vector3(0, top + 0.05f, 0.05f), new Vector3(w * 2f + 0.3f, 0.05f, logLen + 0.35f), new Vector3(-12, 0, 0));
        colliderTargets.Add(g);
    }

    void Bench(float x, float z)
    {
        var g = Group("Panca", root, x, z, 0f, 0f);
        foreach (int s in new[] { -1, 1 })
            MeshPart("Ceppo", g, Prism(7, 0.13f, 0.12f, 0.39f), WoodDark, new Vector3(s * 0.45f, 0, 0), Vector3.one, new Vector3(0, R(0, 50), 0));
        Box("Seduta", g, Wood, new Vector3(0, 0.42f, 0), new Vector3(1.3f, 0.07f, 0.34f), Vector3.zero);
        foreach (int s in new[] { -1, 1 })
            Box("Montante", g, WoodDark, new Vector3(s * 0.55f, 0.6f, -0.15f), new Vector3(0.06f, 0.36f, 0.05f), Vector3.zero);
        Box("Schienale", g, Wood, new Vector3(0, 0.72f, -0.16f), new Vector3(1.3f, 0.18f, 0.05f), new Vector3(-8, 0, 0));
        colliderTargets.Add(g);
    }

    void Trough(float x, float z)
    {
        var g = Group("Fontana_Legno", root, x, z, 90f, 0f);
        foreach (int s in new[] { -1, 1 })
            Box("Gamba", g, WoodDark, new Vector3(s * 0.5f, 0.15f, 0), new Vector3(0.15f, 0.3f, 0.5f), Vector3.zero);
        Box("Fondo", g, Wood, new Vector3(0, 0.32f, 0), new Vector3(1.4f, 0.08f, 0.5f), Vector3.zero);
        foreach (int s in new[] { -1, 1 })
        {
            Box("Sponda", g, Wood, new Vector3(0, 0.5f, s * 0.215f), new Vector3(1.4f, 0.35f, 0.07f), Vector3.zero);
            Box("Testata", g, Wood, new Vector3(s * 0.665f, 0.5f, 0), new Vector3(0.07f, 0.35f, 0.5f), Vector3.zero);
        }
        Box("Acqua", g, Water, new Vector3(0, 0.6f, 0), new Vector3(1.26f, 0.02f, 0.36f), Vector3.zero);
        Box("Palo", g, WoodDark, new Vector3(-0.82f, 0.6f, 0), new Vector3(0.15f, 1.2f, 0.15f), Vector3.zero);
        Box("Cannella", g, Metal, new Vector3(-0.64f, 1.0f, 0), new Vector3(0.24f, 0.04f, 0.04f), Vector3.zero);
        Box("Getto", g, Water, new Vector3(-0.53f, 0.8f, 0), new Vector3(0.025f, 0.38f, 0.025f), Vector3.zero);
        colliderTargets.Add(g);
    }

    void Lantern(float x, float z)
    {
        var g = Group("Lanterna", root, x, z, 0f, 0f);
        Box("Palo", g, WoodDark, new Vector3(0, 0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f), Vector3.zero);
        Box("Base", g, Metal, new Vector3(0, 1.12f, 0), new Vector3(0.2f, 0.04f, 0.2f), Vector3.zero);
        Box("Luce", g, Glow, new Vector3(0, 1.23f, 0), new Vector3(0.14f, 0.18f, 0.14f), Vector3.zero, 2.5f);
        MeshPart("Tetto", g, Prism(4, 0.16f, 0f, 0.1f), Metal, new Vector3(0, 1.32f, 0), Vector3.one, new Vector3(0, 45, 0));

        if (lanternLights)
        {
            var lgo = new GameObject("PointLight");
            lgo.transform.SetParent(g, false);
            lgo.transform.localPosition = new Vector3(0, 1.23f, 0);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Glow;
            l.range = 4f * scale;
            l.intensity = 1.5f;
            l.shadows = LightShadows.None;
        }
        colliderTargets.Add(g);
    }


    // =====================================================================
    //  CERCATORE DI FUNGHI
    // =====================================================================

    /// 0 = porcino, 1 = amanita (rosso a pois bianchi), 2 = finferlo
    void Mushroom(Transform parent, Vector3 lp, int type, float size)
    {
        var m = new GameObject(type == 0 ? "Porcino" : type == 1 ? "Amanita" : "Finferlo").transform;
        m.SetParent(parent, false);
        m.localPosition = lp;
        m.localRotation = Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8));
        m.localScale = Vector3.one * size;

        switch (type)
        {
            case 0: // Porcino: gambo tozzo, cappello marrone
                MeshPart("Gambo", m, Prism(7, 0.05f, 0.038f, 0.09f), MushStem, Vector3.zero, Vector3.one, Vector3.zero);
                MeshPart("Cappello", m, Pick(blobPool), Pick(PorcinoCaps), new Vector3(0, 0.095f, 0), new Vector3(0.13f, 0.065f, 0.13f), Vector3.zero);
                break;
            case 1: // Amanita muscaria: decorativa, NON va nei cesti
                {
                    MeshPart("Gambo", m, Prism(6, 0.026f, 0.02f, 0.13f), White, Vector3.zero, Vector3.one, Vector3.zero);
                    MeshPart("Anello", m, Prism(6, 0.03f, 0.03f, 0.01f), White, new Vector3(0, 0.1f, 0), Vector3.one, Vector3.zero);
                    MeshPart("Cappello", m, Pick(blobPool), AmanitaRed, new Vector3(0, 0.135f, 0), new Vector3(0.12f, 0.05f, 0.12f), Vector3.zero);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = R(0, Mathf.PI * 2f), d = R(0.015f, 0.08f);
                        float y = 0.135f + 0.05f * Mathf.Sqrt(Mathf.Max(0f, 1f - (d / 0.12f) * (d / 0.12f)));
                        MeshPart("Pois", m, Pick(blobPool), White, new Vector3(Mathf.Cos(a) * d, y, Mathf.Sin(a) * d), Vector3.one * 0.012f, Vector3.zero);
                    }
                    break;
                }
            default: // Finferlo: imbuto arancione
                MeshPart("Gambo", m, Prism(6, 0.016f, 0.02f, 0.03f), Finferlo, Vector3.zero, Vector3.one, Vector3.zero);
                MeshPart("Cappello", m, Prism(6, 0.02f, 0.05f, 0.045f), Finferlo, new Vector3(0, 0.03f, 0), Vector3.one, Vector3.zero);
                break;
        }
    }

    void MushroomPatch(Transform parent, Vector3 lp, int count, bool allowAmanita)
    {
        var g = new GameObject("Funghi").transform;
        g.SetParent(parent, false);
        g.localPosition = lp;
        int baseType = allowAmanita ? rng.Next(3) : (rng.NextDouble() < 0.5 ? 0 : 2);
        for (int k = 0; k < count; k++)
        {
            int t = rng.NextDouble() < 0.75 ? baseType : (allowAmanita ? rng.Next(3) : (rng.NextDouble() < 0.5 ? 0 : 2));
            Mushroom(g, new Vector3(R(-0.15f, 0.15f), 0, R(-0.15f, 0.15f)), t, R(0.7f, 1.4f));
        }
    }

    /// Cesto di vimini pieno di porcini e finferli (niente funghi velenosi!)
    void Basket(Transform parent, Vector3 lp, float yaw)
    {
        var b = new GameObject("Cesto_Funghi").transform;
        b.SetParent(parent, false);
        b.localPosition = lp;
        b.localRotation = Quaternion.Euler(0, yaw, 0);

        MeshPart("Cesto", b, Prism(8, 0.15f, 0.19f, 0.16f), Wicker, Vector3.zero, Vector3.one, new Vector3(0, 22.5f, 0));
        MeshPart("Bordo", b, Prism(8, 0.2f, 0.2f, 0.025f), WickerDark, new Vector3(0, 0.15f, 0), Vector3.one, new Vector3(0, 22.5f, 0));
        // manico ad arco
        int seg = 7;
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.18f, 0.16f + Mathf.Sin(a0) * 0.2f, 0);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.18f, 0.16f + Mathf.Sin(a1) * 0.2f, 0);
            Vector3 mid = (p0 + p1) * 0.5f;
            float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            Box("Manico", b, WickerDark, mid, new Vector3((p1 - p0).magnitude + 0.01f, 0.025f, 0.03f), new Vector3(0, 0, ang));
        }
        // funghi dentro
        int n = Few(rng.Next(6, 10));
        for (int k = 0; k < n; k++)
        {
            Vector3 p = new Vector3(R(-0.11f, 0.11f), R(0.02f, 0.06f) + n * 0.01f, R(-0.11f, 0.11f));
            Mushroom(b, p, rng.NextDouble() < 0.6 ? 0 : 2, R(0.9f, 1.2f));
        }
    }

    /// Essiccatoio: telaio di legno con ripiani di funghi a fette e una tettoia
    void DryingRack(float x, float z)
    {
        var g = Group("Essiccatoio_Funghi", root, x, z, 0f, 0f);
        float w = 1.1f, d = 0.45f, h = 1.25f;
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Box("Palo", g, WoodDark, new Vector3(sx * w * 0.5f, h * 0.5f, sz * d * 0.5f), new Vector3(0.06f, h, 0.06f), Vector3.zero);

        float[] levels = { 0.35f, 0.65f, 0.95f };
        foreach (float y in levels)
        {
            // ripiano a listelli
            for (int i = 0; i < 5; i++)
                Box("Listello", g, LightWood, new Vector3(0, y, -d * 0.4f + i * d * 0.2f), new Vector3(w, 0.02f, 0.05f), Vector3.zero);
            // fette di fungo
            int n = Few(rng.Next(8, 13));
            for (int k = 0; k < n; k++)
            {
                float s = R(0.035f, 0.055f);
                MeshPart("Fetta", g, Prism(7, s, s * 0.95f, 0.012f), Rng01() < 0.7 ? DriedSlice : Finferlo,
                         new Vector3(R(-w * 0.42f, w * 0.42f), y + 0.012f, R(-d * 0.38f, d * 0.38f)), Vector3.one, new Vector3(0, R(0, 360), 0));
            }
        }
        // collana di funghi appesa sul davanti
        Box("Spago", g, Rope, new Vector3(0, h - 0.08f, d * 0.5f + 0.02f), new Vector3(w, 0.01f, 0.01f), Vector3.zero);
        for (int i = 0; i < 7; i++)
        {
            if (Rng01() > mushroomAbundance + 0.15f) continue;   // spago quasi vuoto
            float px = -w * 0.42f + i * w * 0.14f;
            float sag = Mathf.Sin((i / 6f) * Mathf.PI) * 0.06f;
            MeshPart("Fetta_Appesa", g, Prism(7, 0.04f, 0.04f, 0.012f), DriedSlice,
                     new Vector3(px, h - 0.12f - sag, d * 0.5f + 0.03f), Vector3.one, new Vector3(90, 0, R(0, 360)));
        }
        Box("Tettoia", g, WoodDark, new Vector3(0, h + 0.06f, -0.03f), new Vector3(w + 0.3f, 0.05f, d + 0.35f), new Vector3(-10, 0, 0));
        colliderTargets.Add(g);
    }

    /// Cassette di legno impilate piene di funghi
    void Crates(float x, float z)
    {
        var g = Group("Cassette_Funghi", root, x, z, R(-15, 15), 0f);
        Crate(g, new Vector3(0, 0, 0), 0f, true);
        Crate(g, new Vector3(0.58f, 0, 0.05f), R(-10, 10), true);
        Crate(g, new Vector3(0.25f, 0.26f, 0f), R(-12, 12), true);
        colliderTargets.Add(g);
    }

    void Crate(Transform parent, Vector3 lp, float yaw, bool full)
    {
        var c = new GameObject("Cassetta").transform;
        c.SetParent(parent, false);
        c.localPosition = lp;
        c.localRotation = Quaternion.Euler(0, yaw, 0);
        float w = 0.55f, d = 0.38f, h = 0.25f;
        Box("Fondo", c, LightWood, new Vector3(0, 0.015f, 0), new Vector3(w, 0.03f, d), Vector3.zero);
        foreach (int s in new[] { -1, 1 })
        {
            for (int k = 0; k < 2; k++)
            {
                float y = 0.06f + k * 0.11f;
                Box("Listello", c, LightWood, new Vector3(0, y, s * (d * 0.5f - 0.01f)), new Vector3(w, 0.07f, 0.02f), Vector3.zero);
            }
            Box("Testata", c, Wood, new Vector3(s * (w * 0.5f - 0.015f), h * 0.5f, 0), new Vector3(0.03f, h, d), Vector3.zero);
        }
        if (!full) return;
        int n = Few(rng.Next(7, 11));
        for (int k = 0; k < n; k++)
            Mushroom(c, new Vector3(R(-w * 0.38f, w * 0.38f), 0.03f, R(-d * 0.35f, d * 0.35f)),
                     rng.NextDouble() < 0.55 ? 0 : 2, R(0.9f, 1.25f));
    }

    /// Ceppo dove si puliscono i funghi, con coltellino e qualche fungo
    void CleaningStump(float x, float z)
    {
        var g = Group("Ceppo_Pulizia_Funghi", root, x, z, R(0, 360), 0f);
        MeshPart("Ceppo", g, Prism(8, 0.26f, 0.23f, 0.45f), WoodDark, Vector3.zero, Vector3.one, Vector3.zero);
        MeshPart("Taglio", g, Prism(8, 0.22f, 0.22f, 0.012f), WoodEnd, new Vector3(0, 0.45f, 0), Vector3.one, Vector3.zero);
        // coltello
        Box("Lama", g, new Color(0.75f, 0.76f, 0.78f), new Vector3(0.05f, 0.47f, 0.06f), new Vector3(0.12f, 0.008f, 0.025f), new Vector3(0, 30, 0));
        Box("Manico_Coltello", g, Wood, new Vector3(-0.04f, 0.475f, 0.11f), new Vector3(0.09f, 0.022f, 0.028f), new Vector3(0, 30, 0));
        // funghi sopra
        Mushroom(g, new Vector3(-0.08f, 0.46f, -0.1f), 0, 1.2f);   // l'unico porcino trovato
        Box("Ritagli", g, MushStem, new Vector3(0.1f, 0.46f, -0.05f), new Vector3(0.03f, 0.01f, 0.02f), new Vector3(0, 40, 0));
        // un cesto vuoto per terra accanto
        var b = new GameObject("Cesto_Vuoto").transform;
        b.SetParent(g, false);
        b.localPosition = new Vector3(0.45f, 0, 0.1f);
        MeshPart("Cesto", b, Prism(8, 0.13f, 0.17f, 0.14f), Wicker, Vector3.zero, Vector3.one, new Vector3(0, 22.5f, 0));
        MeshPart("Bordo", b, Prism(8, 0.18f, 0.18f, 0.02f), WickerDark, new Vector3(0, 0.13f, 0), Vector3.one, new Vector3(0, 22.5f, 0));
        colliderTargets.Add(g);
    }

    /// Insegna di legno con un grande fungo sopra (da cercatore di funghi)
    void Sign(float x, float z)
    {
        var g = Group("Insegna_Funghi", root, x, z, R(-10, 10), 0f);
        Box("Palo", g, WoodDark, new Vector3(0, 0.6f, 0), new Vector3(0.1f, 1.2f, 0.1f), Vector3.zero);
        Box("Tavola", g, LightWood, new Vector3(0, 0.95f, 0.07f), new Vector3(0.75f, 0.32f, 0.04f), new Vector3(0, 0, R(-3, 3)));
        Box("Cornice", g, Wood, new Vector3(0, 0.95f, 0.05f), new Vector3(0.82f, 0.38f, 0.02f), Vector3.zero);
        // disegno di un fungo sulla tavola
        Box("Disegno_Gambo", g, MushStem, new Vector3(0, 0.9f, 0.095f), new Vector3(0.06f, 0.1f, 0.01f), Vector3.zero);
        Box("Disegno_Cappello", g, AmanitaRed, new Vector3(0, 0.98f, 0.095f), new Vector3(0.2f, 0.07f, 0.01f), Vector3.zero);
        // grande fungo decorativo in cima
        Mushroom(g, new Vector3(0, 1.2f, 0), 1, 2.6f);
        colliderTargets.Add(g);
    }

    void WalkingStick(float x, float z)
    {
        var g = Group("Bastone_Cercatore", root, x, z, 0f, 0f);
        MeshPart("Bastone", g, Prism(6, 0.022f, 0.018f, 1.25f), Wood, Vector3.zero, Vector3.one, new Vector3(-10, 0, 8));
        MeshPart("Pomolo", g, Pick(blobPool), WoodDark, new Vector3(-0.174f, 1.22f, -0.215f), Vector3.one * 0.035f, Vector3.zero);
    }

    void ScatterMushrooms(int patches)
    {
        var g = Container("Funghi_Selvatici");
        float hw = pathWidth * 0.5f;
        float maxX = hw + bedLength + 2.5f;
        for (int k = 0; k < patches; k++)
        {
            float x = R(-maxX, maxX);
            if (Mathf.Abs(x) < hw + 0.3f) x += Mathf.Sign(x == 0 ? 1 : x) * (hw + 0.4f);
            float z = R(bedDistanceFromWall + bedDepth + 1.2f, pathLength + 1.5f);
            var c = Group("Gruppo_Funghi", g, x, z, 0f, 0f);
            MushroomPatch(c, Vector3.zero, Mathf.Max(1, Few(rng.Next(2, 6))), true);
            if (grassTufts && rng.NextDouble() < 0.5) Grass(c, new Vector3(0.18f, 0, 0));
        }
    }

    float Rng01() => (float)rng.NextDouble();

    /// Riduce un numero di funghi in base a mushroomAbundance (può dare 0).
    int Few(int full) => Mathf.FloorToInt(full * mushroomAbundance + Rng01());

    // =====================================================================
    //  AIUTI
    // =====================================================================

    Transform Container(string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        return g;
    }

    /// Crea un gruppo a (x,z) nelle coordinate dell'ingresso, appoggiato al terreno.
    Transform Group(string name, Transform parent, float x, float z, float yaw, float sink)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        Vector3 w = transform.position + transform.rotation * (new Vector3(x, 0, z) * scale);
        w.y = GroundY(w) - sink * scale;
        g.position = w;
        g.rotation = Quaternion.Euler(0, transform.eulerAngles.y + yaw, 0);
        g.localScale = Vector3.one * scale;
        return g;
    }

    float GroundY(Vector3 w)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 tp = t.transform.position, size = t.terrainData.size;
            if (w.x >= tp.x && w.x <= tp.x + size.x && w.z >= tp.z && w.z <= tp.z + size.z)
                return t.SampleHeight(w) + tp.y;
        }

        var hits = Physics.RaycastAll(new Vector3(w.x, transform.position.y + 30f, w.z), Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool found = false;
        foreach (var h in hits)
        {
            if (generatedRoot != null && h.transform.IsChildOf(generatedRoot.transform)) continue;
            if (h.normal.y < 0.5f) continue;
            if (h.point.y < best) { best = h.point.y; found = true; }   // il punto più basso = il terreno
        }
        return found ? best : transform.position.y;
    }

    Transform MeshPart(string name, Transform parent, Mesh mesh, Color col, Vector3 lp, Vector3 ls, Vector3 euler, float emission = 0f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = ls;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = Mat(col, emission);
        return g.transform;
    }

    Transform Box(string name, Transform parent, Color col, Vector3 lp, Vector3 size, Vector3 euler, float emission = 0f)
        => MeshPart(name, parent, cubeMesh, col, lp, size, euler, emission);

    Material Mat(Color c, float emission = 0f)
    {
        string key = ColorUtility.ToHtmlStringRGB(c) + "_" + emission;
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { name = "Baita_" + key };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        mats[key] = m;
        return m;
    }

    void AddColliders()
    {
        foreach (var t in colliderTargets)
        {
            if (t == null) continue;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.transform.localScale.y < 0.02f) continue;   // salta pezzi sottilissimi
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }
    }

    // ---------------- Mesh low-poly procedurali ----------------

    void BuildPools()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);

        rockPool = new List<Mesh>();
        blobPool = new List<Mesh>();
        flatStonePool = new List<Mesh>();
        for (int i = 0; i < 6; i++)
        {
            rockPool.Add(Blob(0.28f, "Baita_Roccia_" + i));
            blobPool.Add(Blob(0.12f, "Baita_Blob_" + i));
            flatStonePool.Add(PrismJitter(7, 1f, 0.9f, 0.18f, 0.2f, "Baita_PietraPiatta_" + i));
        }
    }

    Mesh Blob(float jitter, string name)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * (1f + R(-jitter, jitter));
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        return BuildFlat(v, new List<int>(f), name);
    }

    Mesh Prism(int sides, float rb, float rt, float h)
    {
        string key = $"{sides}_{rb}_{rt}_{h}";
        if (prismCache.TryGetValue(key, out var m) && m != null) return m;
        m = PrismJitter(sides, rb, rt, h, 0f, "Baita_Prisma_" + key);
        prismCache[key] = m;
        return m;
    }

    Mesh PrismJitter(int sides, float rb, float rt, float h, float jitter, string name)
    {
        var v = new List<Vector3>();
        var tr = new List<int>();
        float a0 = jitter > 0 ? R(0, Mathf.PI * 2f) : 0f;
        for (int i = 0; i < sides; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / sides;
            float j = 1f + (jitter > 0 ? R(-jitter, jitter) : 0f);
            v.Add(new Vector3(Mathf.Cos(a) * rb * j, 0, Mathf.Sin(a) * rb * j));
        }
        for (int i = 0; i < sides; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / sides;
            float j = 1f + (jitter > 0 ? R(-jitter, jitter) : 0f);
            v.Add(new Vector3(Mathf.Cos(a) * rt * j, h, Mathf.Sin(a) * rt * j));
        }
        int cb = v.Count; v.Add(Vector3.zero);
        int ct = v.Count; v.Add(new Vector3(0, h, 0));
        for (int i = 0; i < sides; i++)
        {
            int n = (i + 1) % sides;
            int b0 = i, b1 = n, t0 = sides + i, t1 = sides + n;
            tr.AddRange(new[] { b0, b1, t0, b1, t1, t0, cb, b0, b1, ct, t0, t1 });
        }
        return BuildFlat(v, tr, name);
    }

    /// Mesh a facce piatte (look low-poly). Orienta ogni triangolo verso l'esterno.
    static Mesh BuildFlat(List<Vector3> v, List<int> tris, string name)
    {
        Vector3 center = Vector3.zero;
        foreach (var p in v) center += p;
        center /= v.Count;

        var verts = new List<Vector3>();
        var idx = new List<int>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) continue;               // triangolo degenere
            if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0) { var tmp = b; b = c; c = tmp; }
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            idx.Add(k); idx.Add(k + 1); idx.Add(k + 2);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- Varie ----------------

    float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

    static Vector3 PerimeterPoint(float d, float L, float D)
    {
        if (d < L) return new Vector3(-L * 0.5f + d, 0, -D * 0.5f); d -= L;
        if (d < D) return new Vector3(L * 0.5f, 0, -D * 0.5f + d); d -= D;
        if (d < L) return new Vector3(L * 0.5f - d, 0, D * 0.5f); d -= L;
        return new Vector3(-L * 0.5f, 0, D * 0.5f - d);
    }

    // Anteprima della zona nella Scene view
    void OnDrawGizmosSelected()
    {
        float hw = pathWidth * 0.5f;
        float maxX = hw + bedLength + 2.5f;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0, transform.eulerAngles.y, 0), Vector3.one * scale);

        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0, 0, (pathLength + 1.5f) * 0.5f), new Vector3(maxX * 2f, 0.05f, pathLength + 1.5f));

        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.9f);
        float bedX = hw + 0.25f + bedLength * 0.5f, bedZ = bedDistanceFromWall + bedDepth * 0.5f;
        Gizmos.DrawWireCube(new Vector3(-bedX, 0, bedZ), new Vector3(bedLength, 0.1f, bedDepth));
        Gizmos.DrawWireCube(new Vector3(bedX, 0, bedZ), new Vector3(bedLength, 0.1f, bedDepth));

        Gizmos.color = Color.cyan;   // direzione "verso l'esterno"
        Gizmos.DrawLine(Vector3.zero, new Vector3(0, 0, pathLength));
        Gizmos.DrawLine(new Vector3(0, 0, pathLength), new Vector3(-0.3f, 0, pathLength - 0.4f));
        Gizmos.DrawLine(new Vector3(0, 0, pathLength), new Vector3(0.3f, 0, pathLength - 0.4f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
