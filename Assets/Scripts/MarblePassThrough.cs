using UnityEngine;

/// <summary>
/// Small one-way collider helper for interactive props that must visibly
/// react to the marble without bouncing it back up the run. After a short
/// contact window the prop stops colliding with the marble only.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MarblePassThrough : MonoBehaviour
{
    public float contactWindow = 0.10f;

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
        if (firstContactTime < 0f && collision.gameObject.CompareTag("Player"))
        {
            firstContactTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        if (cleared || ownCollider == null || marbleCollider == null || firstContactTime < 0f)
        {
            return;
        }

        if (Time.time - firstContactTime >= contactWindow)
        {
            Physics.IgnoreCollision(ownCollider, marbleCollider, true);
            cleared = true;
        }
    }
}
