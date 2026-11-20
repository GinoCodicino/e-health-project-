using UnityEngine.SceneManagement;

/// <summary>
/// Transition between the mountain (Level3_grandpa) and the climbing mini-game (Level3_ClimbGame).
/// Both scenes must be in the Build Settings.
/// </summary>
public static class ClimbGameSession
{
    public const string MountainScene = "Level3_grandpa";
    public const string ClimbScene = "Level3_ClimbGame";

    private static bool returningFromClimb;

    public static void LoadClimbGame()
    {
        returningFromClimb = false;
        SceneManager.LoadScene(ClimbScene);
    }

    public static void ReturnToMountain()
    {
        returningFromClimb = true;
        SceneManager.LoadScene(MountainScene);
    }

    /// <summary>True only once after returning from the mini-game (read by MountainReturnPoint).</summary>
    public static bool ConsumeReturn()
    {
        bool value = returningFromClimb;
        returningFromClimb = false;
        return value;
    }
}
