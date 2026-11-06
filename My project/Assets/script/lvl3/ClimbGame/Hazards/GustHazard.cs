using UnityEngine;

/// <summary>
/// Gust of wind: announced by the HUD (arrows), then pushes the climber one lane over.
/// No visual and no lane: it follows the camera so it is never despawned while pending.
/// </summary>
public class GustHazard : TelegraphedHazard
{
    /// <summary>-1 = pushes left, +1 = pushes right.</summary>
    public int Direction { get; private set; }

    protected override float WarnTime => 1f;

    protected override void Build()
    {
        Direction = Random.value < 0.5f ? -1 : 1;
    }

    protected override void TickWarning() =>
        transform.position = new Vector3(transform.position.x, view.FocusY, 0f);

    protected override void Strike()
    {
        if (climber.Active) climber.Push(Direction);
        Destroy(gameObject);
    }
}
