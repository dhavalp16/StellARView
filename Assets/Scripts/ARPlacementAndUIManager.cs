using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(XROrigin), typeof(ARPlaneManager), typeof(ARRaycastManager))]
public class ARPlacementAndUIManager : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARPlaneManager _planeManager;
    [SerializeField] private ARRaycastManager _raycastManager;

    [Header("UI")]
    [Tooltip("The UIDocument that holds the CameraView.uxml.")]
    [SerializeField] private UIDocument _uiDocument;

    [Header("Prefabs")]
    [Tooltip("A list of prefabs that can be placed. The name of each prefab MUST match the button name in the UXML (e.g., 'Sun', 'Mars').")]
    [SerializeField] private List<GameObject> _placeablePrefabs;

    [Tooltip("The desired size for the largest dimension of the placed object (in meters). The script will automatically scale the prefab to match this size.")]
    [SerializeField] private float _targetSizeInMeters = 0.2f;

    // Private variables
    private GameObject _selectedPrefab;
    private readonly List<GameObject> _instantiatedObjects = new List<GameObject>();
    private readonly HashSet<string> _placedPrefabNames = new HashSet<string>();
    private VisualElement _root;
    private readonly List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();
    private Dictionary<string, GameObject> _prefabDictionary = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (_planeManager == null) _planeManager = GetComponent<ARPlaneManager>();
        if (_raycastManager == null) _raycastManager = GetComponent<ARRaycastManager>();

        foreach (var prefab in _placeablePrefabs)
        {
            if (prefab != null)
            {
                _prefabDictionary[prefab.name] = prefab;
            }
        }
    }

    private void OnEnable()
    {
        if (_uiDocument == null)
        {
            Debug.LogError("ARPlacementAndUIManager: UIDocument is not assigned in the Inspector!");
            return;
        }
        _root = _uiDocument.rootVisualElement;

        RegisterButtonCallbacks();

        var placementArea = _root.Q<VisualElement>("PlacementArea");
        if (placementArea != null)
        {
            placementArea.RegisterCallback<PointerDownEvent>(OnPointerDownForPlacement);
        }
        else
        {
            Debug.LogError("Could not find 'PlacementArea' VisualElement in the UXML. Make sure it exists in CameraView.uxml.");
        }
    }

    private void OnPointerDownForPlacement(PointerDownEvent evt)
    {
        if (_selectedPrefab != null)
        {
            if (_placedPrefabNames.Contains(_selectedPrefab.name))
            {
                Debug.Log($"An instance of '{_selectedPrefab.name}' has already been placed. Select another object.");
                return;
            }

            Vector2 screenPosition = evt.position;
            PlaceObject(screenPosition);
        }
    }

    private void RegisterButtonCallbacks()
    {
        var backButton = _root.Q<Button>("BackButton");
        if (backButton != null)
        {
            backButton.RegisterCallback<ClickEvent>(evt => SceneManager.LoadScene(0));
        }

        var clearButton = _root.Q<Button>("ClearCanvas");
        if (clearButton != null)
        {
            clearButton.RegisterCallback<ClickEvent>(evt => ClearARScene());
        }

        foreach (var prefabName in _prefabDictionary.Keys)
        {
            var prefabButton = _root.Q<Button>(prefabName);
            if (prefabButton != null)
            {
                prefabButton.RegisterCallback<ClickEvent>(evt => SelectPrefab(prefabName));
            }
        }
    }

    private void SelectPrefab(string prefabName)
    {
        if (_prefabDictionary.TryGetValue(prefabName, out GameObject prefab))
        {
            _selectedPrefab = prefab;
            Debug.Log($"Prefab '{prefabName}' selected.");
        }
    }

    private void PlaceObject(Vector2 touchPosition)
    {
        if (_raycastManager.Raycast(touchPosition, _raycastHits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = _raycastHits[0].pose;
            var newObject = Instantiate(_selectedPrefab, hitPose.position, hitPose.rotation);
            _instantiatedObjects.Add(newObject);
            _placedPrefabNames.Add(_selectedPrefab.name);

            RescaleToTargetSize(newObject);

            // 🔄 Add rotation script automatically
            if (newObject.GetComponent<RotateObject>() == null)
            {
                var rotator = newObject.AddComponent<RotateObject>();
                rotator.RotationVector = new Vector3(0, 50, 0); // Default rotation (Y-axis)
            }

            Debug.Log($"Instantiated and automatically scaled '{_selectedPrefab.name}' at {hitPose.position}");
        }
        else
        {
            Debug.LogWarning("Raycast did not hit any detected planes. Are planes being detected?");
        }
    }

    private void RescaleToTargetSize(GameObject targetObject)
    {
        var renderers = targetObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds totalBounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            totalBounds.Encapsulate(renderer.bounds);
        }

        float maxDimension = Mathf.Max(totalBounds.size.x, totalBounds.size.y, totalBounds.size.z);
        if (maxDimension == 0) return;

        float scaleFactor = _targetSizeInMeters / maxDimension;
        targetObject.transform.localScale = Vector3.one * scaleFactor;
    }

    private void ClearARScene()
    {
        foreach (var obj in _instantiatedObjects)
        {
            Destroy(obj);
        }
        _instantiatedObjects.Clear();
        _placedPrefabNames.Clear();

        _selectedPrefab = null;
        _planeManager.enabled = true;
        SetPlanesActive(true);
        Debug.Log("AR scene has been cleared.");
    }

    private void SetPlanesActive(bool isActive)
    {
        foreach (var plane in _planeManager.trackables)
        {
            plane.gameObject.SetActive(isActive);
        }
    }
}
