using UnityEngine;

/// <summary>
/// Décor 3D du mini-jeu, construit au lancement : la paroi des couloirs, des falaises de chaque côté,
/// les montagnes et les sapins du fond, le sol de départ et le sommet avec son drapeau.
/// Tout champ vide est remplacé par une forme simple colorée.
/// Pour l'éditer à la main : menu ⋮ du composant → « Générer le décor dans la scène », puis sauvegarder la scène.
/// Le jeu réutilise alors ce décor au lieu d'en reconstruire un.
/// </summary>
public class ClimbScenery : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;

    [Header("Paroi")]
    [Tooltip("Matériaux des couloirs, en alternance (ex. MountainRidge_Rock / MountainRidge_RockLight).")]
    public Material[] laneMaterials;
    [Tooltip("Falaises posées de chaque côté des couloirs (ex. Cliff_1 à Cliff_8).")]
    public GameObject[] sideCliffs;
    [Tooltip("Matériau imposé aux falaises (vide = celui du prefab).")]
    public Material cliffMaterial;
    [SerializeField] private float cliffSpacing = 4.5f;
    [SerializeField] private float cliffSize = 6f;
    [Tooltip("Petits rochers incrustés dans la paroi, pour lui donner du relief (ex. Rock_Small_1 à 5).")]
    public GameObject[] wallDetails;
    public Material wallDetailMaterial;
    [SerializeField] private float detailsPerMeter = 1.2f;

    [Header("Fond")]
    public GameObject[] farMountains;
    public GameObject[] pines;
    public Material groundMaterial;
    [Tooltip("Les montagnes du fond commencent à cette profondeur (m).")]
    [SerializeField] private float mountainsMinZ = 70f;
    [Tooltip("Distance du brouillard qui fond les montagnes dans le ciel (m).")]
    [SerializeField] private Vector2 fogRange = new Vector2(60f, 350f);

    [Header("Sommet")]
    public Material flagPoleMaterial;
    public Material flagClothMaterial;

    /// <summary>Profondeur de la face de la paroi : le grimpeur est en z = 0, juste devant.</summary>
    public const float WallZ = 0.5f;

    private const string RootName = "Scenery";
    private Transform root;

    /// <summary>
    /// Appelé au lancement. Si le décor a déjà été généré dans la scène (menu ⋮ du composant),
    /// on le garde tel quel, avec les retouches faites à la main ; sinon on le construit.
    /// </summary>
    public void Build(float summitHeight)
    {
        ApplyFog();
        if (transform.Find(RootName) != null) return;
        Generate(summitHeight);
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
    // Clic droit sur le composant (ou menu ⋮) dans l'Inspector.
    [ContextMenu("Générer le décor dans la scène")]
    private void GenerateInEditor()
    {
        var manager = GetComponent<ClimbGameManager>();
        if (manager == null) manager = FindFirstObjectByType<ClimbGameManager>();
        float summitHeight = manager != null ? manager.SummitHeight : 120f;

        ClearInEditor();
        ApplyFog();
        Generate(summitHeight);
        UnityEditor.Undo.RegisterCreatedObjectUndo(root.gameObject, "Générer le décor");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [ContextMenu("Supprimer le décor de la scène")]
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

        // Une bande de roche par couloir.
        for (int i = 0; i < climber.laneCount; i++)
        {
            var lane = ModelFactory.Create($"Lane{i}", root, null, PrimitiveType.Cube,
                                           i % 2 == 0 ? new Color(0.5f, 0.47f, 0.44f) : new Color(0.56f, 0.53f, 0.5f),
                                           new Vector3(climber.laneWidth, height, 1f));
            lane.transform.position = new Vector3(climber.LaneX(i), bottom + height / 2f, WallZ + 0.5f);
            if (laneMaterials != null && laneMaterials.Length > 0)
                ModelFactory.SetMaterial(lane, laneMaterials[i % laneMaterials.Length]);
        }

        // Falaises de chaque côté, en quinconce, pour encadrer les couloirs.
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
                // Retournées : leur face plate (faite pour poser au sol) part vers le haut,
                // là où la caméra en contre-plongée ne la voit pas.
                cliff.transform.rotation = Quaternion.Euler(180f, random.Next(4) * 90f, 0f);
                ModelFactory.SetMaterial(cliff, cliffMaterial);
            }
        }

        // Petits rochers à moitié enfoncés dans la face des couloirs.
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

        // Sapins au pied de la paroi, de part et d'autre.
        for (int i = 0; i < 14; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * (6f + 18f * (float)random.NextDouble());
            float z = -6f + 14f * (float)random.NextDouble();
            float h = 4f + 3f * (float)random.NextDouble();
            var pine = ModelFactory.Create("Pine", root, Pick(pines, random), PrimitiveType.Capsule,
                                           new Color(0.2f, 0.45f, 0.25f), new Vector3(h * 0.5f, h, h * 0.5f));
            pine.transform.position = new Vector3(x, -0.6f + h / 2f, z);
        }

        // Montagnes lointaines, derrière la paroi : on les voit sur les côtés.
        // Ces modèles sont très profonds : on recule chacune jusqu'à ce que sa face avant soit à mountainsMinZ.
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
