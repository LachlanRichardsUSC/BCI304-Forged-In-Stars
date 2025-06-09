using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuReturn : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}