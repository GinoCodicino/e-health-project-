using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Scene_lvl1");
    }

    [SerializeField] private GameObject settingsPopup;

    public void OpenSettings()
    {
        settingsPopup.transform.SetAsLastSibling();
        settingsPopup.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPopup.SetActive(false);
    }


    public void QuitGame()
    {
        Application.Quit();
    }
}
