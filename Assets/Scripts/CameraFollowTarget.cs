using UnityEngine;

/// <summary>
/// Gives the camera a subtle horizontal dolly movement while it follows the ball.
/// CameraLookAt performs the actual look-at behaviour.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraFollowTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 basePosition = new Vector3(0f, 6.2f, -17f);
    [Range(0f, 1f)] public float horizontalFollow = 0.16f;
    [Range(0f, 1f)] public float verticalFollow = 0.03f;
    public float smoothing = 1.8f;

    private void OnEnable()
    {
        if (target != null)
        {
            transform.position = GetDesiredPosition();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, GetDesiredPosition(), t);
    }

    private Vector3 GetDesiredPosition()
    {
        return basePosition + new Vector3(
            target.position.x * horizontalFollow,
            target.position.y * verticalFollow,
            0f);
    }
}
