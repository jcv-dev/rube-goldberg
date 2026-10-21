using UnityEngine;

/// <summary>
/// Small reliability aid for a long physical marble run. Unity can put a
/// slow rigidbody to sleep on a gentle slope; if the marble is truly stalled
/// for a moment, this gives it a gentle downhill wake-up impulse.
/// It never teleports or completes a stage by itself.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MarbleFlowAssist : MonoBehaviour
{
    public float stalledSpeed = 0.06f;
    public float stalledTime = 0.65f;
    public float assistVelocity = 1.15f;
    public float cooldown = 1.6f;

    private Rigidbody body;
    private float stillSince;
    private float lastAssistTime = -10f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        Vector3 position = transform.position;
        if (position.y < 3.15f && position.x > 4.8f)
        {
            // The finish zone is allowed to come to rest.
            return;
        }

        if (body.linearVelocity.magnitude > stalledSpeed)
        {
            stillSince = 0f;
            return;
        }

        if (stillSince <= 0f)
        {
            stillSince = Time.time;
        }

        if (Time.time - stillSince < stalledTime || Time.time - lastAssistTime < cooldown)
        {
            return;
        }

        float direction = position.y > 6.35f ? 1f : position.y > 3.9f ? -1f : 1f;
        body.WakeUp();
        body.AddForce(new Vector3(direction * assistVelocity, 0f, 0f), ForceMode.VelocityChange);
        lastAssistTime = Time.time;
        stillSince = 0f;
    }
}
