using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Déroulé du mini-jeu « Couloirs » : fondu d'entrée, montée, tronçons météo,
/// chute → retour au début du tronçon (point de contrôle), sommet → victoire,
/// Échap → retour à la montagne.
/// La caméra (ClimbCamera), l'interface (ClimbHud) et le décor (ClimbScenery) sont dans leurs propres scripts.
/// </summary>
public class ClimbGameManager : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [SerializeField] private ClimbWeather weather;
    [SerializeField] private HazardSpawner spawner;
    [SerializeField] private ClimbCamera climbCamera;
    [SerializeField] private ClimbHud hud;
    [SerializeField] private ClimbScenery scenery;
    [SerializeField] private ScreenFader fader;

    [Header("Partie")]
    [SerializeField] private float summitHeight = 120f;
    [SerializeField] private float fadeDuration = 0.5f;
    [Tooltip("Temps pendant lequel on voit le grimpeur tomber avant le fondu.")]
    [SerializeField] private float fallDuration = 0.8f;
    [SerializeField] private string summitMessage = "Sommet atteint !";
    [Tooltip("Appelé quand le sommet est atteint (fin du jeu : scène de fin, crédits…).")]
    public UnityEvent onSummitReached;

    public float SummitHeight => summitHeight;

    private float checkpoint;
    private bool busy, finished;

    // ---------- Cycle de vie ----------

    private void Awake()
    {
        climbCamera.MaxY = summitHeight + 1f;
        hud.SummitHeight = summitHeight;
        spawner.TopLimit = summitHeight;
        scenery.Build(summitHeight);
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
}
