using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject instructionsPanel;

    // Starts the AR scene
    public void StartAR()
    {
        SceneManager.LoadScene("MainScene");
    }

    // Shows the instructions panel
    public void ShowInstructions()
    {
        if (instructionsPanel != null)
            instructionsPanel.SetActive(true);
    }

    // Returns to the Main Menu scene
    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // Exits the application
    public void ExitApp()
    {
        Application.Quit();
    }
}