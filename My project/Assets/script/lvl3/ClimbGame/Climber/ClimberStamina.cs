using System;
using UnityEngine;

/// <summary>
/// The climber's stamina: drains over time and on hits, refilled by water bottles.
/// Plain class, edited in the LaneClimber inspector.
/// </summary>
[Serializable]
public class ClimberStamina
{
    [SerializeField] private float max = 100f;
    [SerializeField] private float drainPerSecond = 2.5f;

    public float Current { get; private set; }
    public float Normalized => Current / max;

    /// <summary>Raised once when stamina reaches 0.</summary>
    public event Action OnExhausted;

    public void Reset() => Current = max;

    /// <summary>Loss over time; multiplier comes from the weather.</summary>
    public void Drain(float multiplier, float dt) => Spend(drainPerSecond * multiplier * dt);

    public void Refill(float amount) => Current = Mathf.Min(max, Current + amount);

    public void Spend(float amount)
    {
        if (Current <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        if (Current <= 0f) OnExhausted?.Invoke();
    }
}
