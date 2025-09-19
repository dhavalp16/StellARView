using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SimpleARPlacementManager : MonoBehaviour
{
    [SerializeField] private XROrigin _xrOrigin;
    [SerializeField] private ARPlaneManager _planeManager;
    [SerializeField] private ARRaycastManager _raycastManager;

    // Dictionary to store prefabs by name
    [System.Serializable]
    public class PrefabMapping
    {
        public string name;
        public GameObject prefab;
    }

    [SerializeField] private List<PrefabMapping> prefabMappings = new List<PrefabMapping>();
    private Dictionary<string, GameObject> prefabDictionary;

    private GameObject _currentSelectedPrefab;
    private GameObject _instantiatedObject = null;
    private List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();

    private void Start()
    {
        // Initialize prefab dictionary
        prefabDictionary = new Dictionary<string, GameObject>();
        foreach (var mapping in prefabMappings)
        {
            if (mapping.prefab != null && !string.IsNullOrEmpty(mapping.name))
            {
                prefabDictionary[mapping.name] = mapping.prefab;
            }
        }

        // Set default prefab if available
        if (prefabDictionary.Count > 0)
        {
            var firstPrefab = prefabDictionary.Values.GetEnumerator();
            firstPrefab.MoveNext();
            _currentSelectedPrefab = firstPrefab.Current;
        }
    }

    public void SetSelectedPrefab(string prefabName)
    {
        if (prefabDictionary.ContainsKey(prefabName))
        {
            _currentSelectedPrefab = prefabDictionary[prefabName];
            Debug.Log($"Selected prefab: {prefabName}");
        }
        else
        {
            Debug.LogWarning($"Prefab '{prefabName}' not found in dictionary!");
        }
    }

    public void ClearPlacedObject()
    {
        if (_instantiatedObject != null)
        {
            Destroy(_instantiatedObject);
            _instantiatedObject = null;

            // Re-enable plane detection
            _planeManager.enabled = true;
            foreach (ARPlane plane in _planeManager.trackables)
            {
                plane.gameObject.SetActive(true);
            }
        }
    }

    private void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            bool collision = _raycastManager.Raycast(Input.GetTouch(0).position, _raycastHits, TrackableType.PlaneWithinPolygon);

            if (collision && _currentSelectedPrefab != null)
            {
                // If there's already an object, destroy it first
                if (_instantiatedObject != null)
                {
                    Destroy(_instantiatedObject);
                }

                // Instantiate the selected prefab
                _instantiatedObject = Instantiate(_currentSelectedPrefab, _raycastHits[0].pose.position, _raycastHits[0].pose.rotation);

                // Hide planes after placement
                foreach (ARPlane plane in _planeManager.trackables)
                {
                    plane.gameObject.SetActive(false);
                }
                _planeManager.enabled = false;
            }
        }
    }
}