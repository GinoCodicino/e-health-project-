using UnityEngine;

/// <summary>
/// A hazard announced before it arrives: a blinking warning for WarnTime seconds, then the strike.
/// </summary>
public abstract class TelegraphedHazard : Hazard
{
    protected GameObject warning;
    private bool struck;

    protected abstract float WarnTime { get; }
    /// <summary>Warning blinks per second.</summary>
    protected virtual float BlinkRate => 6f;
    /// <summary>Part of each blink where the warning is hidden (0-1).</summary>
    protected virtual float BlinkOff => 0.3f;

    protected sealed override void Tick()
    {
        if (!struck)
        {
            TickWarning();
            ModelFactory.SetVisible(warning, Mathf.Repeat(timer * BlinkRate, 1f) > BlinkOff);
            if (timer < WarnTime) return;
            struck = true;
            timer = 0f;
            Destroy(warning);
            Strike();
        }
        TickStrike();
    }

    /// <summary>Every frame during the warning.</summary>
    protected virtual void TickWarning() { }

    /// <summary>Once, at the end of the warning.</summary>
    protected abstract void Strike();

    /// <summary>Every frame after the strike (starting with the strike frame).</summary>
    protected virtual void TickStrike() { }
}
