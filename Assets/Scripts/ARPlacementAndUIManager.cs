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
    [SerializeField] private Camera _arCamera;

    [Header("Interaction")]
    [SerializeField] private ObjectInteractionManager _interactionManager;

    [Header("UI")]
    [SerializeField] private UIDocument _uiDocument;

    [Header("Prefabs")]
    [SerializeField] private List<GameObject> _placeablePrefabs;
    [SerializeField] private float _targetSizeInMeters = 0.2f;

    [Header("Debug Settings")]
    [SerializeField] private bool _debugRaycast = true;
    [SerializeField] private float _raycastMaxDistance = 20f;

    // Private variables
    private GameObject _selectedPrefab;
    private GameObject _selectedObject; // ✅ NEW: Currently selected object for interaction
    private readonly List<GameObject> _instantiatedObjects = new List<GameObject>();
    private readonly HashSet<string> _placedPrefabNames = new HashSet<string>();
    private VisualElement _root;
    private readonly List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();
    private readonly Dictionary<string, GameObject> _prefabDictionary = new Dictionary<string, GameObject>();

    // ✅ NEW: For scaling
    private float _initialPinchDistance;
    private Vector3 _initialScale;
    private bool _isDragging = false;

    private void Awake()
    {
        if (_planeManager == null) _planeManager = GetComponent<ARPlaneManager>();
        if (_raycastManager == null) _raycastManager = GetComponent<ARRaycastManager>();

        // Auto-assign AR Camera if not set
        if (_arCamera == null)
        {
            _arCamera = Camera.main;
            if (_arCamera == null)
            {
                var xrOrigin = GetComponent<XROrigin>();
                if (xrOrigin != null) _arCamera = xrOrigin.Camera;
            }
        }

        foreach (var prefab in _placeablePrefabs)
        {
            if (prefab != null) _prefabDictionary[prefab.name] = prefab;
        }
    }

    // ✅ NEW: Add Update method for touch handling
    private void Update()
    {
        // Handle touch input for selected object interaction
        if (_selectedObject != null)
        {
            if (Input.touchCount == 1)
            {
                HandleObjectMovement();
            }
            else if (Input.touchCount == 2)
            {
                HandleObjectScaling();
            }
            else if (_isDragging)
            {
                _isDragging = false;
                Debug.Log("Stopped dragging");
            }
        }
    }

    private void OnEnable()
    {
        if (_uiDocument == null) { Debug.LogError("UIDocument not assigned!"); return; }
        _root = _uiDocument.rootVisualElement;

        var placementArea = _root.Q<VisualElement>("PlacementArea");
        if (placementArea != null)
        {
            placementArea.RegisterCallback<PointerDownEvent>(OnPointerDownForPlacement);
        }
        RegisterButtonCallbacks();
    }

    private void OnPointerDownForPlacement(PointerDownEvent evt)
    {
        if (_arCamera == null)
        {
            Debug.LogError("AR Camera is not assigned! Cannot perform raycast.");
            return;
        }

        // ✅ IMPROVED: Convert coordinates properly and add debug info
        Vector2 uiPosition = evt.position;
        Vector2 screenPosition = new Vector2(uiPosition.x, Screen.height - uiPosition.y);

        if (_debugRaycast)
        {
            Debug.Log($"🎯 Tap detected - UI: {uiPosition}, Screen: {screenPosition}, Screen Size: {Screen.width}x{Screen.height}");
        }

        // ✅ IMPROVED: Create ray for object detection with better debugging
        Ray ray = _arCamera.ScreenPointToRay(screenPosition);

        if (_debugRaycast)
        {
            Debug.Log($"🔍 Raycast from: {ray.origin}, Direction: {ray.direction}");
            // Visual debug ray
            Debug.DrawRay(ray.origin, ray.direction * _raycastMaxDistance, Color.red, 2f);
        }

        // ✅ IMPROVED: Use RaycastAll to see all hits, then filter for interactables
        RaycastHit[] allHits = Physics.RaycastAll(ray, _raycastMaxDistance);

        if (_debugRaycast)
        {
            Debug.Log($"🎯 Physics.RaycastAll found {allHits.Length} hits");
            for (int i = 0; i < allHits.Length; i++)
            {
                var hit = allHits[i];
                Debug.Log($"  Hit {i}: {hit.collider.name} (Tag: {hit.collider.tag}) at distance {hit.distance:F2}");
            }
        }

        // Look for the closest interactable object
        RaycastHit? closestInteractableHit = null;
        float closestDistance = float.MaxValue;

        foreach (var hit in allHits)
        {
            if (hit.collider.CompareTag("Interactable") && hit.distance < closestDistance)
            {
                closestInteractableHit = hit;
                closestDistance = hit.distance;
            }
        }

        // If we found an interactable object, select it
        if (closestInteractableHit.HasValue)
        {
            var hit = closestInteractableHit.Value;
            Debug.Log($"✅ Hit interactable object: {hit.collider.name} at distance {hit.distance:F2}");
            _interactionManager.SelectObject(hit.collider.gameObject);
            return;
        }

        // If we didn't hit an interactable object, deselect any previous object
        if (_debugRaycast)
        {
            Debug.Log("❌ No interactable objects hit, deselecting current object");
        }
        _interactionManager.DeselectObject();

        // Now, proceed with the logic to place a NEW object
        if (_selectedPrefab == null)
        {
            if (_debugRaycast) Debug.Log("⚠️ No prefab selected for placement");
            return;
        }

        if (_placedPrefabNames.Contains(_selectedPrefab.name))
        {
            Debug.Log($"An instance of '{_selectedPrefab.name}' has already been placed.");
            return;
        }

        // Proceed with placement
        PlaceObject(screenPosition);
    }

    private void OnDisable()
    {
        if (_root == null) return;
        var placementArea = _root.Q<VisualElement>("PlacementArea");
        if (placementArea != null)
        {
            placementArea.UnregisterCallback<PointerDownEvent>(OnPointerDownForPlacement);
        }
    }

    private void PlaceObject(Vector2 screenPosition)
    {
        TrackableType trackableTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.PlaneEstimated;

        if (_raycastManager.Raycast(screenPosition, _raycastHits, trackableTypes))
        {
            var hitPose = _raycastHits[0].pose;

            // ✅ FIX ROTATION: Use identity rotation instead of plane rotation to keep objects upright
            var newObject = Instantiate(_selectedPrefab, hitPose.position, Quaternion.identity);

            // ✅ CRITICAL: Ensure the new object is properly set up for interaction
            SetupInteractableObject(newObject);

            _instantiatedObjects.Add(newObject);
            _placedPrefabNames.Add(_selectedPrefab.name);
            RescaleToTargetSize(newObject);

            var rotator = newObject.AddComponent<RotateObject>();
            rotator.RotationVector = new Vector3(0, 15, 0);

            Debug.Log($"✅ Successfully placed '{_selectedPrefab.name}' at {hitPose.position}");
            SetPlanesActive(false);
        }
        else
        {
            Debug.LogWarning($"❌ Placement failed: Raycast did not hit any detected planes.");
        }
    }

    /// <summary>
    /// Ensures the instantiated object has proper components for interaction
    /// </summary>
    private void SetupInteractableObject(GameObject obj)
    {
        // Ensure the object has the Interactable tag
        if (!obj.CompareTag("Interactable"))
        {
            obj.tag = "Interactable";
            Debug.Log($"🏷️ Added 'Interactable' tag to {obj.name}");
        }

        // ✅ NEW: Find all child objects with meshes and add colliders to them
        SetupCollidersRecursively(obj);
    }

    /// <summary>
    /// Recursively sets up colliders on all objects with meshes in the hierarchy
    /// </summary>
    private void SetupCollidersRecursively(GameObject obj)
    {
        // Check current object
        SetupColliderOnObject(obj);

        // Check all children
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            GameObject child = obj.transform.GetChild(i).gameObject;
            SetupCollidersRecursively(child);
        }
    }

    /// <summary>
    /// Sets up collider on a specific object if it has a mesh
    /// </summary>
    private void SetupColliderOnObject(GameObject obj)
    {
        // Skip if already has a collider
        if (obj.GetComponent<Collider>() != null)
        {
            Debug.Log($"✅ {obj.name} already has collider");
            return;
        }

        // Check if this object has a mesh
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();

        if (meshRenderer != null && meshFilter != null && meshFilter.mesh != null)
        {
            // Add mesh collider to the object that actually has the mesh
            MeshCollider meshCollider = obj.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.mesh;
            meshCollider.convex = true;

            // ✅ IMPORTANT: Also add the Interactable tag to this child object
            if (!obj.CompareTag("Interactable"))
            {
                obj.tag = "Interactable";
            }

            Debug.Log($"🔧 Added MeshCollider to {obj.name} (mesh: {meshFilter.mesh.name})");
        }
    }

    private void RegisterButtonCallbacks()
    {
        var backButton = _root.Q<Button>("BackButton");
        if (backButton != null)
            backButton.RegisterCallback<ClickEvent>(evt => SceneManager.LoadScene(0));

        var clearButton = _root.Q<Button>("ClearCanvas");
        if (clearButton != null)
            clearButton.RegisterCallback<ClickEvent>(evt => ClearARScene());

        foreach (var prefabName in _prefabDictionary.Keys)
        {
            var prefabButton = _root.Q<Button>(prefabName);
            if (prefabButton != null)
                prefabButton.RegisterCallback<ClickEvent>(evt => SelectPrefab(prefabName));
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

    private void RescaleToTargetSize(GameObject targetObject)
    {
        Vector3 originalScale = targetObject.transform.localScale;
        targetObject.transform.localScale = Vector3.one;

        var renderers = targetObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { targetObject.transform.localScale = originalScale; return; }

        Bounds totalBounds = new Bounds();
        bool hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (renderer.enabled)
            {
                if (!hasBounds) { totalBounds = renderer.bounds; hasBounds = true; }
                else { totalBounds.Encapsulate(renderer.bounds); }
            }
        }

        if (!hasBounds) { targetObject.transform.localScale = originalScale; return; }

        float maxDimension = Mathf.Max(totalBounds.size.x, totalBounds.size.y, totalBounds.size.z);
        if (maxDimension < 0.0001f) { targetObject.transform.localScale = originalScale; return; }

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
        _selectedObject = null; // ✅ NEW: Also deselect interaction object
        SetPlanesActive(true);
        Debug.Log("AR scene has been cleared.");
    }

    // ✅ NEW: Object interaction methods
    private void SelectObjectForInteraction(GameObject obj)
    {
        _selectedObject = obj;
        _isDragging = false;
        Debug.Log($"Selected {_selectedObject.name} for interaction");
    }

    private void DeselectObject()
    {
        if (_selectedObject != null)
        {
            Debug.Log($"Deselected {_selectedObject.name}");
        }
        _selectedObject = null;
        _isDragging = false;
    }

    private void HandleObjectMovement()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            _isDragging = true;
            Debug.Log($"Started dragging {_selectedObject.name}");
        }
        else if (touch.phase == TouchPhase.Moved && _isDragging)
        {
            TrackableType trackableTypes = TrackableType.PlaneWithinPolygon |
                                         TrackableType.PlaneWithinBounds |
                                         TrackableType.PlaneEstimated;

            if (_raycastManager.Raycast(touch.position, _raycastHits, trackableTypes))
            {
                Vector3 newPosition = _raycastHits[0].pose.position;
                _selectedObject.transform.position = newPosition;
                Debug.Log($"Moved {_selectedObject.name} to {newPosition}");
            }
            else
            {
                Debug.LogWarning("No AR plane hit during drag");
            }
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            _isDragging = false;
            Debug.Log("Touch ended, stopped dragging");
        }
    }

    private void HandleObjectScaling()
    {
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            _initialPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
            _initialScale = _selectedObject.transform.localScale;
            Debug.Log($"Started pinch scaling, initial distance: {_initialPinchDistance}");
        }
        else if (touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved)
        {
            float currentPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
            if (_initialPinchDistance == 0) return;

            float scaleFactor = currentPinchDistance / _initialPinchDistance;
            Vector3 newScale = _initialScale * scaleFactor;

            // Clamp scale to reasonable limits
            newScale = Vector3.Max(newScale, Vector3.one * 0.1f); // Min 10% of original
            newScale = Vector3.Min(newScale, Vector3.one * 5f);   // Max 500% of original

            _selectedObject.transform.localScale = newScale;
            Debug.Log($"Scaled {_selectedObject.name} to {newScale} (factor: {scaleFactor:F2})");
        }
    }

    private void SetPlanesActive(bool isActive)
    {
        foreach (var plane in _planeManager.trackables)
        {
            plane.gameObject.SetActive(isActive);
        }
    }

    // ✅ DEBUG METHOD: Call this to check your scene setup
    [ContextMenu("Debug Scene Setup")]
    private void DebugSceneSetup()
    {
        Debug.Log("=== SCENE SETUP DEBUG ===");
        Debug.Log($"AR Camera: {(_arCamera != null ? _arCamera.name : "NULL")}");
        Debug.Log($"Interaction Manager: {(_interactionManager != null ? _interactionManager.name : "NULL")}");
        Debug.Log($"Instantiated Objects: {_instantiatedObjects.Count}");

        foreach (var obj in _instantiatedObjects)
        {
            if (obj != null)
            {
                var collider = obj.GetComponent<Collider>();
                Debug.Log($"  - {obj.name}: Tag={obj.tag}, Collider={collider?.GetType().Name ?? "NONE"}");
            }
        }
    }
}