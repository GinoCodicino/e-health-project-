using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Navigation only: quest progress and player input stay with the existing scripts.
[DisallowMultipleComponent]
public class RedHouseLandmark : MonoBehaviour
{
    [Header("Collegamenti")]
    public GrandmotherForestMemory memory;
    public playercontroller player;
    public NpcDialogue herbalist;

    [Header("Indicazione dopo il ricordo")]
    public bool showObjective = true;
    [Min(2f)] public float arrivalRadius = 8f;
    [Tooltip("Usa la stessa lingua impostata nel ricordo della nonna.")]
    public bool followMemoryLanguage = true;
    public bool italian;

    [HideInInspector] public TextAsset previousStory;
    [HideInInspector] public TextAsset redHouseStory;
    public bool HasMetHerbalist { get; private set; }

    private Camera viewCamera;
    private GameObject uiRoot;
    private TMP_Text heading, objective, direction;
    private float refreshAt;
    private bool arrived;

    private void Start()
    {
        if (player == null) player = Object.FindFirstObjectByType<playercontroller>();
        if (memory == null) memory = Object.FindFirstObjectByType<GrandmotherForestMemory>();
        if (herbalist == null && memory != null) herbalist = memory.herbalist;
        if (player != null) viewCamera = player.GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.12f;
        if (HasMetHerbalist || !showObjective || player == null || herbalist == null
            || (memory != null && !memory.IsCompleted))
        {
            Hide();
            return;
        }

        // A conversation, including one opened immediately after Skip, completes navigation.
        // No calls to OpenDialogue, SetUiMode, quest reset or minigame start are needed here.
        if (herbalist.IsOpen
            || (herbalist.mushroomGame != null && herbalist.mushroomGame.IsOpen)
            || (herbalist.learningQuest != null && herbalist.learningQuest.IsOpen))
        {
            HasMetHerbalist = true;
            Hide();
            return;
        }
        if (player.IsGameplayBlocked) { Hide(); return; }

        Vector3 here = player.transform.position;
        Vector3 toHouse = transform.position - here;
        if (new Vector2(toHouse.x, toHouse.z).magnitude <= arrivalRadius
            && Mathf.Abs(toHouse.y) < 4f) arrived = true;
        if (uiRoot == null) BuildUI();
        uiRoot.SetActive(true);
        bool useItalian = followMemoryLanguage && memory != null ? memory.italianDialogue : italian;
        heading.text = useItalian ? "SULLE SUE TRACCE" : "IN HER FOOTSTEPS";
        objective.text = arrived
            ? (useItalian ? "Parla con l'erborista" : "Talk to the herbalist")
            : (useItalian ? "Raggiungi la casa rossa" : "Reach the red house");

        Vector3 destination = arrived ? herbalist.transform.position : transform.TransformPoint(0f, 0f, 3f);
        Vector3 offset = Vector3.ProjectOnPlane(destination - here, Vector3.up);
        Transform facing = viewCamera != null ? viewCamera.transform : player.transform;
        Vector3 forward = Vector3.ProjectOnPlane(facing.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = player.transform.forward;
        float angle = offset.sqrMagnitude < 0.01f ? 0f : Vector3.SignedAngle(forward, offset, Vector3.up);
        string bearing = Mathf.Abs(angle) > 135f ? (useItalian ? "Alle tue spalle" : "Behind you")
            : angle < -28f ? (useItalian ? "Alla tua sinistra" : "To your left")
            : angle > 28f ? (useItalian ? "Alla tua destra" : "To your right")
            : (useItalian ? "Davanti a te" : "Ahead of you");
        direction.text = bearing + "  |  " + Mathf.CeilToInt(offset.magnitude) + " m";
    }

    private void BuildUI()
    {
        uiRoot = new GameObject("RedHouse_Objective", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        CanvasGroup group = uiRoot.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        // Deliberately no GraphicRaycaster or EventSystem: this is a passive HUD.
        Image panel = MushroomUI.Image("ObjectivePanel", uiRoot.transform, new Color(0.045f, 0.10f, 0.065f, 0.85f));
        panel.raycastTarget = false;
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0f, 1f);
        panel.rectTransform.pivot = new Vector2(0f, 1f);
        panel.rectTransform.anchoredPosition = new Vector2(20f, -20f);
        panel.rectTransform.sizeDelta = new Vector2(382f, 112f);
        Image stripe = MushroomUI.Image("RedAccent", panel.transform, new Color(0.69f, 0.25f, 0.19f));
        stripe.raycastTarget = false;
        stripe.rectTransform.anchorMin = Vector2.zero;
        stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
        stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
        stripe.rectTransform.sizeDelta = new Vector2(5f, 0f);
        heading = Label("Heading", panel.transform, 16f, new Color(0.87f, 0.80f, 0.58f), 12f, 22f);
        objective = Label("Objective", panel.transform, 26f, MushroomUI.Paper, 36f, 35f);
        direction = Label("Direction", panel.transform, 17f, new Color(0.80f, 0.85f, 0.77f), 78f, 24f);
    }

    private static TMP_Text Label(string name, Transform parent, float size, Color color, float top, float height)
    {
        TMP_Text text = MushroomUI.Text(name, parent, "", size, color);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.offsetMin = new Vector2(20f, -top - height);
        text.rectTransform.offsetMax = new Vector2(-14f, -top);
        return text;
    }

    private void Hide() { if (uiRoot != null) uiRoot.SetActive(false); }
    private void OnDisable() { Hide(); }
    private void OnDestroy() { if (uiRoot != null) Destroy(uiRoot); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.26f, 0.18f, 0.75f);
        Gizmos.DrawWireCube(transform.TransformPoint(0f, 1.7f, 0f), new Vector3(5.2f, 3.4f, 4.4f));
        Gizmos.color = new Color(0.95f, 0.83f, 0.50f, 0.9f);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.TransformPoint(0f, 1f, 4f));
        if (herbalist != null) Gizmos.DrawLine(transform.position + Vector3.up, herbalist.transform.position + Vector3.up);
    }
}
