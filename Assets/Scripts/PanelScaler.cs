
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PanelScaler : MonoBehaviour
{
    public Vector2 referenceResolution = new Vector2(1080, 1920);
    [Range(0, 1)]
    public float matchWidthOrHeight = 0.5f;

    private UIDocument uiDocument;

    void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        UpdateScale();
    }

    void Update()
    {
        UpdateScale();
    }

    private void UpdateScale()
    {
        if (uiDocument == null)
        {
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float referenceWidth = referenceResolution.x;
        float referenceHeight = referenceResolution.y;

        float scaleFactor = 0;
        float widthRatio = screenWidth / referenceWidth;
        float heightRatio = screenHeight / referenceHeight;

        scaleFactor = Mathf.Lerp(widthRatio, heightRatio, matchWidthOrHeight);

        root.transform.scale = new Vector3(scaleFactor, scaleFactor, 1);
    }
}
