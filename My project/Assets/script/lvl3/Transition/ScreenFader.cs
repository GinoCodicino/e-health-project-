using System.Collections;
using UnityEngine;

/// <summary>
/// Fondu au noir plein écran + message centré, dessinés avec OnGUI.
/// Un seul par scène : transitions entre la montagne et le mini-jeu d'escalade.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private int fontSize = 40;

    /// <summary>Texte affiché au centre de l'écran (null = rien).</summary>
    public string Message { get; set; }

    private float alpha;
    private GUIStyle style;

    /// <summary>Opacité immédiate (ex. démarrer une scène en noir avant un fondu d'entrée).</summary>
    public void SetAlpha(float value) => alpha = Mathf.Clamp01(value);

    /// <summary>Anime l'opacité du noir jusqu'à target (0 = transparent, 1 = noir).</summary>
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
