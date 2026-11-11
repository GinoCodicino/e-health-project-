using UnityEngine;

/// <summary>
/// Point at the foot of the route, in the mountain scene.
/// When returning from the climbing mini-game (quit), the player is placed back here and the screen fades in.
/// </summary>
public class MountainReturnPoint : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.5f;

    private void Start()
    {
        if (!ClimbGameSession.ConsumeReturn()) return;

        var player = FindFirstObjectByType<playercontroller>();
        if (player != null) PlayerPlacement.PlaceOnGround(player.transform, transform.position, transform.eulerAngles.y);

        var fader = FindFirstObjectByType<ScreenFader>();
        if (fader != null)
        {
            fader.SetAlpha(1f);
            StartCoroutine(fader.FadeTo(0f, fadeDuration));
        }
    }
}
