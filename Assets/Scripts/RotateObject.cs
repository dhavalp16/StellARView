using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [Tooltip("Rotation speed in X, Y, Z (degrees per second)")]
    public Vector3 RotationVector = new Vector3(0, 50, 0);

    void Update()
    {
        transform.Rotate(RotationVector * Time.deltaTime);
    }
}
