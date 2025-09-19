using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation; // ARSession

public class ChangeScene : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private VisualElement root;

    public int homeSceneID = 0;
    public int arSceneID = 1;

    void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not assigned in Inspector!");
            return;
        }

        root = uiDocument.rootVisualElement;

        // HomeScreen buttons
        var playBtn = root.Q<Button>("PlayButton");
        if (playBtn != null)
            playBtn.RegisterCallback<ClickEvent>(evt => LoadARScene());

        var exitBtn = root.Q<Button>("ExitButton");
        if (exitBtn != null)
            exitBtn.RegisterCallback<ClickEvent>(evt => Application.Quit());

        // ARScene buttons
        var backBtn = root.Q<Button>("BackButton");
        if (backBtn != null)
            backBtn.RegisterCallback<ClickEvent>(evt => LoadHomeScene());

        var clearBtn = root.Q<Button>("ClearCanvas");
        if (clearBtn != null)
            clearBtn.RegisterCallback<ClickEvent>(evt => Debug.Log("Canvas cleared"));

        // Planet buttons
        string[] planets = { "Sun", "Mercury", "Venus", "Earth", "Moon", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto" };
        foreach (var planet in planets)
        {
            var btn = root.Q<Button>(planet);
            if (btn != null)
                btn.RegisterCallback<ClickEvent>(evt => Debug.Log($"{planet} clicked"));
        }
    }

    private void LoadARScene()
    {
        // Persist ARSession manually
        var arSession = Object.FindFirstObjectByType<ARSession>();
        if (arSession != null)
            DontDestroyOnLoad(arSession.gameObject);

        // Persist AR Camera (assume your AR Camera is tagged "MainCamera")
        var arCamera = Camera.main;
        if (arCamera != null)
            DontDestroyOnLoad(arCamera.gameObject);

        SceneManager.LoadScene(arSceneID);
    }

    private void LoadHomeScene()
    {
        SceneManager.LoadScene(homeSceneID);
    }

    public void LoadScene(int sceneID)
    {
        SceneManager.LoadScene(sceneID);
    }
}
