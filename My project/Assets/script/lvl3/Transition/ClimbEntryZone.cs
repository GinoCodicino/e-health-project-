using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zone at the foot of the route: when the player is inside, shows "Press C to climb"
/// and, on C, fades out then loads the climbing mini-game scene.
/// The Collider only defines the zone (we test its bounds).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClimbEntryZone : MonoBehaviour
{
    [SerializeField] private string prompt = "Press C to climb";
    [SerializeField] private int fontSize = 28;
    [SerializeField] private float fadeDuration = 0.5f;

    private Collider zone;
    private playercontroller player;
    private ScreenFader fader;
    private bool showPrompt;
    private bool loading;
    private GUIStyle style;

    private void Awake()
    {
        zone = GetComponent<Collider>();
        zone.isTrigger = true;
        player = FindFirstObjectByType<playercontroller>();
        fader = FindFirstObjectByType<ScreenFader>();
    }

    private void Update()
    {
        showPrompt = !loading && player != null && zone.bounds.Contains(player.transform.position);

        if (showPrompt && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            StartCoroutine(EnterClimbGame());
    }

    private IEnumerator EnterClimbGame()
    {
        loading = true;
        if (fader != null) yield return fader.FadeTo(1f, fadeDuration);
        ClimbGameSession.LoadClimbGame();
    }

    private void OnGUI()
    {
        if (!showPrompt) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height * 0.85f), prompt, style);
    }
}
