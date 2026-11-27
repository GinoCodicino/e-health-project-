using UnityEngine.SceneManagement;

/// <summary>
/// Transition between the mountain (Level3_grandpa) and the climbing mini-game (Level3_ClimbGame).
/// Both scenes must be in the Build Settings.
/// </summary>
public static class ClimbGameSession
{
    public const string MountainScene = "Level3_grandpa";
    public const string ClimbScene = "Level3_ClimbGame";

    /// <summary>Where the player appears when coming back to the mountain.</summary>
    public enum Arrival { None, Foot, Summit }

    private static Arrival pending = Arrival.None;

    public static void LoadClimbGame()
    {
        pending = Arrival.None;
        SceneManager.LoadScene(ClimbScene);
    }

    /// <summary>Mini-game abandoned: back to the foot of the route.</summary>
    public static void ReturnToMountain()
    {
        pending = Arrival.Foot;
        SceneManager.LoadScene(MountainScene);
    }

    /// <summary>Mini-game won: the player arrives on the summit.</summary>
    public static void GoToSummit()
    {
        pending = Arrival.Summit;
        SceneManager.LoadScene(MountainScene);
    }

    /// <summary>
    /// True only once, if the player is arriving at this place
    /// (read by MountainReturnPoint and SummitArrivalPoint).
    /// </summary>
    public static bool ConsumeArrival(Arrival place)
    {
        if (pending != place) return false;
        pending = Arrival.None;
        return true;
    }
}
