using UnityEngine;

/// <summary>
/// A hazard announced before it arrives: a blinking warning for WarnTime seconds, then the strike.
/// The warning object is optional (the gust is announced by the HUD).
/// </summary>
public abstract class TelegraphedHazard : Hazard
{
    protected GameObject warning;
    private bool struck;

    /// <summary>True until the strike.</summary>
    public bool IsWarning => !struck;

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
            if (warning != null) ModelFactory.SetVisible(warning, Mathf.Repeat(timer * BlinkRate, 1f) > BlinkOff);
            if (timer < WarnTime) return;
            struck = true;
            timer = 0f;
            if (warning != null) Destroy(warning);
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
