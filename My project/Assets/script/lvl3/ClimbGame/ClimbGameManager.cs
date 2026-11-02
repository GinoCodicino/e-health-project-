using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Déroulé du mini-jeu « Couloirs » : fondu d'entrée, montée, tronçons météo,
/// chute → retour au début du tronçon (point de contrôle), sommet → victoire,
/// Échap → retour à la montagne. Construit aussi le décor de la paroi.
/// La caméra (ClimbCamera) et l'interface (ClimbHud) sont dans leurs propres scripts.
/// </summary>
public class ClimbGameManager : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [SerializeField] private ClimbWeather weather;
    [SerializeField] private HazardSpawner spawner;
    [SerializeField] private ClimbCamera climbCamera;
    [SerializeField] private ClimbHud hud;
    [SerializeField] private ScreenFader fader;

    [Header("Partie")]
    [SerializeField] private float summitHeight = 120f;
    [SerializeField] private float fadeDuration = 0.5f;
    [Tooltip("Temps pendant lequel on voit le grimpeur tomber avant le fondu.")]
    [SerializeField] private float fallDuration = 0.8f;
    [SerializeField] private string summitMessage = "Sommet atteint !";
    [Tooltip("Appelé quand le sommet est atteint (fin du jeu : scène de fin, crédits…).")]
    public UnityEvent onSummitReached;

    [Header("Visuel (assets 2D, vides = formes par défaut)")]
    public Sprite wallSprite;
    public Sprite summitSprite;
    public Sprite groundSprite;

    private float checkpoint;
    private bool busy, finished;

    // ---------- Cycle de vie ----------

    private void Awake()
    {
        climbCamera.MaxY = summitHeight + 1f;
        hud.SummitHeight = summitHeight;
        spawner.TopLimit = summitHeight;
        BuildBackground();
    }

    private void OnEnable()
    {
        climber.OnExhausted += HandleExhausted;
        weather.OnSectionChanged += HandleSectionChanged;
    }

    private void OnDisable()
    {
        climber.OnExhausted -= HandleExhausted;
        weather.OnSectionChanged -= HandleSectionChanged;
    }

    private IEnumerator Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        busy = true;
        if (fader != null) fader.SetAlpha(1f);
        PlaceAt(0f);
        yield return Fade(0f);
        SetPlaying(true);
        busy = false;
    }

    private void Update()
    {
        if (finished) return;

        weather.UpdateForHeight(climber.Height);

        if (busy) return;
        if (climber.Height >= summitHeight) { Win(); return; }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            StartCoroutine(AbandonRoutine());
    }

    // ---------- Déroulé ----------

    private void PlaceAt(float height)
    {
        weather.ResetTo(height);
        climber.ResetAt(height);
        spawner.ResetSpawns();
        climbCamera.Snap();
    }

    private void SetPlaying(bool playing)
    {
        climber.Active = playing;
        spawner.Running = playing;
    }

    // Le point de contrôle est le début du dernier tronçon atteint.
    private void HandleSectionChanged(WeatherSection section)
    {
        checkpoint = section.startHeight;
        hud.ShowBanner(section.name, 2.5f);
    }

    private void HandleExhausted()
    {
        if (!busy && !finished) StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        busy = true;
        SetPlaying(false);
        hud.ShowBanner("Épuisé !", fallDuration + fadeDuration);

        // On voit le grimpeur tomber.
        float speed = 0f;
        for (float t = 0f; t < fallDuration; t += Time.deltaTime)
        {
            speed += 25f * Time.deltaTime;
            climber.transform.position += Vector3.down * speed * Time.deltaTime;
            yield return null;
        }

        yield return Fade(1f);
        PlaceAt(checkpoint);
        yield return Fade(0f);
        SetPlaying(true);
        busy = false;
    }

    private void Win()
    {
        finished = true;
        SetPlaying(false);
        hud.ShowBanners = false;
        if (fader != null) fader.Message = summitMessage;
        Debug.Log("Sommet atteint : victoire !");
        onSummitReached?.Invoke();
    }

    private IEnumerator AbandonRoutine()
    {
        busy = true;
        SetPlaying(false);
        yield return Fade(1f);
        ClimbGameSession.ReturnToMountain();
    }

    private IEnumerator Fade(float target)
    {
        if (fader != null) yield return fader.FadeTo(target, fadeDuration);
    }

    // ---------- Décor ----------

    private void BuildBackground()
    {
        // Couloirs : bandes verticales de deux teintes, enfants de la caméra (elles la suivent).
        Camera cam = climbCamera.Cam;
        float height = cam.orthographicSize * 2f + 2f;
        for (int i = 0; i < climber.laneCount; i++)
        {
            Color c = i % 2 == 0 ? new Color(0.5f, 0.47f, 0.44f) : new Color(0.56f, 0.53f, 0.5f);
            var lane = SpriteFactory.Create($"Lane{i}", cam.transform, wallSprite, SpriteFactory.Square, c,
                                            new Vector2(climber.laneWidth, height), -10);
            lane.transform.localPosition = new Vector3(climber.LaneX(i), 0f, 10f);
        }

        float width = climber.laneCount * climber.laneWidth + 1f;
        var summit = SpriteFactory.Create("Summit", transform, summitSprite, SpriteFactory.Square,
                                          new Color(1f, 0.85f, 0.2f), new Vector2(width, 0.5f), -5);
        summit.transform.position = new Vector3(0f, summitHeight + 0.6f, 0f);

        var ground = SpriteFactory.Create("Ground", transform, groundSprite, SpriteFactory.Square,
                                          new Color(0.35f, 0.6f, 0.3f), new Vector2(40f, 4f), -6);
        ground.transform.position = new Vector3(0f, -2.6f, 0f);
    }
}
