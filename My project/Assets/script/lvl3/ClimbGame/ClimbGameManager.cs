using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Flow of the "Lanes" mini-game: fade-in, climb, weather sections,
/// fall → back to the start of the section (checkpoint), summit → victory,
/// Escape → back to the mountain.
/// The camera (ClimbCamera), the UI (ClimbHud) and the scenery (ClimbScenery) are in their own scripts.
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

    [Header("Game")]
    [SerializeField] private float summitHeight = 120f;
    [SerializeField] private float fadeDuration = 0.5f;
    [Tooltip("How long the climber is seen falling before the fade.")]
    [SerializeField] private float fallDuration = 0.8f;
    [SerializeField] private string summitMessage = "Summit reached!";
    [Tooltip("Called when the summit is reached (end of the game: ending scene, credits...).")]
    public UnityEvent onSummitReached;

    public float SummitHeight => summitHeight;

    private float checkpoint;
    private bool busy, finished;

    // ---------- Lifecycle ----------

    private void Awake()
    {
        climbCamera.MaxY = summitHeight + 1f;
        hud.SummitHeight = summitHeight;
        spawner.TopLimit = summitHeight;
        scenery.Build(summitHeight);
    }

    private void OnEnable()
    {
        climber.Stamina.OnExhausted += HandleExhausted;
        weather.OnSectionChanged += HandleSectionChanged;
    }

    private void OnDisable()
    {
        climber.Stamina.OnExhausted -= HandleExhausted;
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

    // ---------- Flow ----------

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

    // The checkpoint is the start of the last section reached.
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
        hud.ShowBanner("Exhausted!", fallDuration + fadeDuration);

        // Show the climber falling.
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
        Debug.Log("Summit reached: victory!");
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
