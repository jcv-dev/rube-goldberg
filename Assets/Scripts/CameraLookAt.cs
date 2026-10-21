using UnityEngine;

/// <summary>
/// Keeps the camera pointed at a moving target.
/// This is the small, self-contained script required by the workshop brief.
/// </summary>
public class CameraLookAt : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target != null)
        {
            transform.LookAt(target);
        }
    }
}
