using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuManager : MonoBehaviour
{
    [Tooltip("The UI Document for the main menu.")]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("The build index of the AR scene to load.")]
    [SerializeField] private int arSceneID = 1;

    void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("MainMenuManager: UIDocument is not assigned in the Inspector!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Query buttons from the MainView.uxml
        var playButton = root.Q<Button>("PlayButton");
        var exitButton = root.Q<Button>("ExitButton");

        // Register callbacks
        if (playButton != null)
        {
            playButton.RegisterCallback<ClickEvent>(evt => LoadARScene());
        }

        if (exitButton != null)
        {
            exitButton.RegisterCallback<ClickEvent>(evt => QuitApplication());
        }
    }

    private void LoadARScene()
    {
        SceneManager.LoadScene(arSceneID);
    }

    private void QuitApplication()
    {
        Application.Quit();
        // If running in the editor, this will stop play mode
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
