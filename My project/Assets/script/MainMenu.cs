using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Scene_lvl1");
    }

    public void OpenSettings()
    {
        Debug.Log("Apri il pannello impostazioni");
        // Qui potrai attivare un pannello Settings
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
