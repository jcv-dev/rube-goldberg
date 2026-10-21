using UnityEngine;

/// <summary>
/// Keeps a domino cascade from turning into a wall on the marble run.
/// Each piece still receives the marble impact and topples physically, but
/// after a short contact window it ignores only the marble collider. Pieces
/// continue to collide with the track and with each other.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DominoClearance : MonoBehaviour
{
    public float contactWindow = 0.07f;
    public float fallAngle = 45f;

    private Collider ownCollider;
    private Collider marbleCollider;
    private float firstContactTime = -1f;
    private bool cleared;

    private void Start()
    {
        ownCollider = GetComponent<Collider>();
        GameObject marble = GameObject.FindGameObjectWithTag("Player");
        if (marble != null)
        {
            marbleCollider = marble.GetComponent<Collider>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (firstContactTime >= 0f || !collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        firstContactTime = Time.time;
    }

    private void FixedUpdate()
    {
        if (cleared || ownCollider == null || marbleCollider == null)
        {
            return;
        }

        bool hasToppled = Vector3.Angle(transform.up, Vector3.up) > fallAngle;
        bool contactWindowElapsed = firstContactTime >= 0f && Time.time - firstContactTime >= contactWindow;
        if (hasToppled || contactWindowElapsed)
        {
            Physics.IgnoreCollision(ownCollider, marbleCollider, true);
            cleared = true;
        }
    }
}
