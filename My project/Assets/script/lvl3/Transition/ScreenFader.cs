using System.Collections;
using UnityEngine;

/// <summary>
/// Full-screen fade to black + centered message, drawn with OnGUI.
/// One per scene: transitions between the mountain and the climbing mini-game.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private int fontSize = 40;

    /// <summary>Text shown in the center of the screen (null = nothing).</summary>
    public string Message { get; set; }

    private float alpha;
    private GUIStyle style;

    /// <summary>Immediate opacity (e.g. start a scene in black before a fade-in).</summary>
    public void SetAlpha(float value) => alpha = Mathf.Clamp01(value);

    /// <summary>Animates the black overlay's opacity to target (0 = transparent, 1 = black).</summary>
    public IEnumerator FadeTo(float target, float duration)
    {
        float start = alpha;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        alpha = target;
    }

    private void OnGUI()
    {
        if (alpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (!string.IsNullOrEmpty(Message))
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), Message, style);
        }
    }
}
