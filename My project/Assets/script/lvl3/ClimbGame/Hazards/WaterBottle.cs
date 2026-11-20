using UnityEngine;

/// <summary>Bonus: a water bottle on the wall that restores stamina.</summary>
public class WaterBottle : Hazard
{
    const float Amount = 30f;
    const float Spin = 90f;

    protected override void Build()
    {
        visual = ModelFactory.Create("Water", transform, visuals.water, PrimitiveType.Cylinder,
                                     new Color(0.2f, 0.6f, 1f), new Vector3(0.6f, 0.7f, 0.6f));
    }

    // Spins to catch the eye.
    protected override void Tick() =>
        visual.transform.Rotate(Vector3.up, Spin * Time.deltaTime, Space.World);

    protected override void OnTouch()
    {
        climber.Refill(Amount);
        Destroy(gameObject);
    }
}
