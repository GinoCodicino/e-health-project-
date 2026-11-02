using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zone au pied de la voie : quand le joueur est dedans, affiche « Appuie sur C pour grimper »
/// et, sur C, fait un fondu puis charge la scène du mini-jeu d'escalade.
/// Le Collider sert juste à définir la zone (on teste ses bounds).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClimbEntryZone : MonoBehaviour
{
    [SerializeField] private string prompt = "Appuie sur C pour grimper";
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
