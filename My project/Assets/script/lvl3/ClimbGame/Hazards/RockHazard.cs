using UnityEngine;

/// <summary>Falling rock: a warning at the top of the screen, then the rock falls and spins.</summary>
public class RockHazard : TelegraphedHazard
{
    const float Speed = 9f;
    const float Spin = 220f;

    private Vector3 spinAxis;

    protected override float WarnTime => 1f;
    protected override float DespawnMargin => 1f;

    protected override void Build()
    {
        warning = ModelFactory.Create("Warning", transform, visuals.warning, PrimitiveType.Sphere,
                                      new Color(1f, 0.25f, 0.2f), Vector3.one * 0.45f);
    }

    protected override void TickWarning()
    {
        transform.position = new Vector3(transform.position.x, view.Top - 0.6f, 0f);
    }

    protected override void Strike()
    {
        visual = ModelFactory.Create("Rock", transform, PickRock(), PrimitiveType.Sphere,
                                     new Color(0.45f, 0.4f, 0.35f), Vector3.one * 0.7f);
        spinAxis = Random.onUnitSphere;
        transform.position += Vector3.up * 1.2f;
    }

    protected override void TickStrike()
    {
        transform.position += Vector3.down * Speed * Time.deltaTime;
        visual.transform.Rotate(spinAxis, Spin * Time.deltaTime, Space.World);
    }

    protected override void OnTouch()
    {
        climber.Hit();
        Destroy(gameObject);
    }

    private GameObject PickRock() =>
        visuals.rocks != null && visuals.rocks.Length > 0 ? visuals.rocks[Random.Range(0, visuals.rocks.Length)] : null;
}
