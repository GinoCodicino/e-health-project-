using UnityEngine;

/// <summary>
/// Point on the summit, in the mountain scene.
/// When the climbing mini-game is won, the player is placed here and the screen fades in.
/// </summary>
public class SummitArrivalPoint : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1.5f;

    private void Start()
    {
        if (!ClimbGameSession.ConsumeArrival(ClimbGameSession.Arrival.Summit)) return;

        var player = FindAnyObjectByType<playercontroller>();
        if (player != null) PlayerPlacement.PlaceOnGround(player.transform, transform.position, transform.eulerAngles.y);

        var fader = FindAnyObjectByType<ScreenFader>();
        if (fader != null)
        {
            fader.SetAlpha(1f);
            StartCoroutine(fader.FadeTo(0f, fadeDuration));
        }
    }
}
