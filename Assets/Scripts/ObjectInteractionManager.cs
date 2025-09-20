using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ObjectInteractionManager : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private bool debugMovement = true;

    private ARRaycastManager arRaycastManager;
    private GameObject selectedObject;

    // For scaling
    private float initialPinchDistance;
    private Vector3 initialScale;

    // For movement
    private bool isDragging = false;

    void Awake()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
        // Debug touch input detection
        if (Input.touchCount > 0 && debugMovement)
        {
            Debug.Log($"Touch detected: {Input.touchCount} touches, Selected object: {(selectedObject != null ? selectedObject.name : "NONE")}");
        }

        // This script now ONLY handles moving and scaling an object that has already been selected.
        if (selectedObject == null)
        {
            // Still log touches even when no object selected for debugging
            if (Input.touchCount > 0 && debugMovement)
            {
                Debug.Log("Touch detected but no object selected - cannot move/scale");
            }
            return;
        }

        if (Input.touchCount == 1)
        {
            HandleSingleTouchDrag();
        }
        else if (Input.touchCount == 2)
        {
            HandleTwoFingerPinch();
        }
        else
        {
            // No touches, stop dragging
            if (isDragging)
            {
                isDragging = false;
                if (debugMovement) Debug.Log("Stopped dragging");
            }
        }
    }

    // This is the public method the other script will call.
    public void SelectObject(GameObject obj)
    {
        selectedObject = obj;
        isDragging = false; // Reset dragging state
        Debug.Log($"Selected {selectedObject.name} via manager.");
    }

    // This method is called when no object is hit, deselecting the current one.
    public void DeselectObject()
    {
        selectedObject = null;
        isDragging = false;
        if (debugMovement) Debug.Log("Deselected object");
    }

    private void HandleSingleTouchDrag()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            isDragging = true;
            if (debugMovement) Debug.Log($"Started dragging {selectedObject.name}");
        }
        else if (touch.phase == TouchPhase.Moved && isDragging)
        {
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            TrackableType trackableTypes = TrackableType.PlaneWithinPolygon |
                                         TrackableType.PlaneWithinBounds |
                                         TrackableType.PlaneEstimated;

            if (arRaycastManager.Raycast(touch.position, hits, trackableTypes))
            {
                Vector3 newPosition = hits[0].pose.position;
                selectedObject.transform.position = newPosition;

                if (debugMovement)
                {
                    Debug.Log($"Moved {selectedObject.name} to {newPosition}");
                }
            }
            else
            {
                if (debugMovement) Debug.LogWarning("No AR plane hit during drag");
            }
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isDragging = false;
            if (debugMovement) Debug.Log("Touch ended, stopped dragging");
        }
    }

    private void HandleTwoFingerPinch()
    {
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            initialPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
            initialScale = selectedObject.transform.localScale;
            if (debugMovement) Debug.Log($"Started pinch, initial distance: {initialPinchDistance}");
        }
        else if (touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved)
        {
            float currentPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
            if (initialPinchDistance == 0) return;

            float scaleFactor = currentPinchDistance / initialPinchDistance;
            Vector3 newScale = initialScale * scaleFactor;

            // Clamp scale to reasonable limits
            newScale = Vector3.Max(newScale, Vector3.one * 0.1f); // Min 10% of original
            newScale = Vector3.Min(newScale, Vector3.one * 5f);   // Max 500% of original

            selectedObject.transform.localScale = newScale;

            if (debugMovement)
            {
                Debug.Log($"Scaled {selectedObject.name} to {newScale} (factor: {scaleFactor:F2})");
            }
        }
    }
}