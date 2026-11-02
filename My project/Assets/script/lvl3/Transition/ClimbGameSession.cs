using UnityEngine.SceneManagement;

/// <summary>
/// Passage entre la montagne (Level3_grandpa) et le mini-jeu d'escalade (Level3_ClimbGame).
/// Les deux scènes doivent être dans les Build Settings.
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

    /// <summary>Vrai une seule fois après un retour du mini-jeu (lu par MountainReturnPoint).</summary>
    public static bool ConsumeReturn()
    {
        bool value = returningFromClimb;
        returningFromClimb = false;
        return value;
    }
}
