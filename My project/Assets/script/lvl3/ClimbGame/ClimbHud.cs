using UnityEngine;

/// <summary>
/// Mini-game UI, drawn with OnGUI: stamina, altitude, weather,
/// section announcement, gust warning and controls reminder.
/// </summary>
public class ClimbHud : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [SerializeField] private ClimbWeather weather;
    [SerializeField] private HazardSpawner spawner;

    /// <summary>Summit altitude, shown in "Altitude: x / summit".</summary>
    public float SummitHeight { get; set; } = 120f;
    /// <summary>False = no more announcements (after victory).</summary>
    public bool ShowBanners { get; set; } = true;

    private string banner;
    private float bannerTimer;
    private GUIStyle hudStyle, bannerStyle, arrowStyle;

    /// <summary>Shows a large message at the upper middle of the screen for duration seconds.</summary>
    public void ShowBanner(string text, float duration)
    {
        banner = text;
        bannerTimer = duration;
    }

    private void Update()
    {
        bannerTimer -= Time.deltaTime;
    }

    private void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bannerStyle = new GUIStyle(hudStyle) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            arrowStyle = new GUIStyle(hudStyle) { fontSize = 40, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.9f, 0.3f) } };
        }

        // Stamina
        const float x = 20f, y = 20f, w = 300f, h = 26f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        float k = climber.Stamina.Normalized;
        GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.35f), k);
        GUI.DrawTexture(new Rect(x + 3f, y + 3f, (w - 6f) * k, h - 6f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + h + 4f, 400f, 30f), "Stamina", hudStyle);

        // Altitude and weather
        float altitude = Mathf.Clamp(climber.Height, 0f, SummitHeight);
        GUI.Label(new Rect(x, y + h + 34f, 400f, 30f), $"Altitude: {altitude:0} / {SummitHeight:0} m", hudStyle);
        if (weather.Current != null)
            GUI.Label(new Rect(x, y + h + 64f, 400f, 30f), $"Weather: {weather.Current.name}", hudStyle);

        // Announcement (new section, fall)
        if (ShowBanners && bannerTimer > 0f && !string.IsNullOrEmpty(banner))
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60f), banner, bannerStyle);

        // Incoming gust
        if (spawner.GustWarning > 0f && spawner.PendingGustDirection != 0)
        {
            string arrows = spawner.PendingGustDirection > 0 ? "GUST  →→→" : "←←←  GUST";
            GUI.Label(new Rect(0, Screen.height * 0.32f, Screen.width, 50f), arrows, arrowStyle);
        }

        GUI.Label(new Rect(x, Screen.height - 40f, 700f, 30f), "← → : change lane   ·   Esc : quit", hudStyle);
    }
}
