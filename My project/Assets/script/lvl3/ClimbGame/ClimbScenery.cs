using UnityEngine;

/// <summary>
/// 3D scenery of the mini-game, built at startup from its layers (see SceneryLayer):
/// the lanes' wall, the backdrop (ground, pines, mountains), the clouds and the summit with its flag.
/// What layers build in Build() can be saved in the scene; what they build in BuildRuntime()
/// (high peaks, clouds) is always rebuilt at startup.
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
    [Tooltip("Fog distance that blends the mountains into the sky (m).")]
    [SerializeField] private Vector2 fogRange = new Vector2(60f, 350f);

    [SerializeField] private WallLayer wall = new WallLayer();
    [SerializeField] private BackdropLayer backdrop = new BackdropLayer();
    [SerializeField] private CloudLayer clouds = new CloudLayer();
    [SerializeField] private SummitLayer summit = new SummitLayer();

    /// <summary>Depth of the wall face: the climber is at z = 0, just in front.</summary>
    public const float WallZ = 0.5f;

    public LaneClimber Climber => climber;
    public ClimbCamera View => view;
    public ClimbWeather Weather => weather;
    public Vector2 FogRange => fogRange;

    private const string RootName = "Scenery";
    private const string BackdropName = "Backdrop";

    private SceneryLayer[] layers;

    private SceneryLayer[] Layers
    {
        get
        {
            if (layers == null)
            {
                layers = new SceneryLayer[] { wall, backdrop, clouds, summit };
                foreach (var layer in layers) layer.Bind(this);
            }
            return layers;
        }
    }

    /// <summary>
    /// Called at startup. If the scenery was already generated in the scene (component ⋮ menu),
    /// it is kept as is, with any manual tweaks; otherwise it is built.
    /// </summary>
    public void Build(float summitHeight)
    {
        ApplyFog();
        if (transform.Find(RootName) == null) Generate(summitHeight);
        BuildRuntime(summitHeight);
    }

    private void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = fogRange.x;
        RenderSettings.fogEndDistance = fogRange.y;
    }

    private Transform Generate(float summitHeight)
    {
        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);
        foreach (var layer in Layers) layer.Build(root, summitHeight);
        return root;
    }

    private void BuildRuntime(float summitHeight)
    {
        var old = transform.Find(BackdropName);
        if (old != null) ModelFactory.DestroySafe(old.gameObject);

        var root = new GameObject(BackdropName).transform;
        root.SetParent(transform, false);
        if (view == null) view = FindFirstObjectByType<ClimbCamera>();
        if (weather == null) weather = FindFirstObjectByType<ClimbWeather>();

        foreach (var layer in Layers) layer.BuildRuntime(root, summitHeight);
    }

    private void Update()
    {
        if (layers == null) return;
        float dt = Time.deltaTime;
        foreach (var layer in layers) layer.Tick(dt);
    }

#if UNITY_EDITOR
    // Right-click the component (or ⋮ menu) in the Inspector.
    [ContextMenu("Generate scenery in scene")]
    private void GenerateInEditor()
    {
        var manager = GetComponent<ClimbGameManager>();
        if (manager == null) manager = FindFirstObjectByType<ClimbGameManager>();
        float summitHeight = manager != null ? manager.SummitHeight : 120f;

        layers = null;   // re-bind to the layers as currently edited in the Inspector
        ClearInEditor();
        ApplyFog();
        var root = Generate(summitHeight);
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
}
