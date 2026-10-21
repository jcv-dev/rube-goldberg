using UnityEngine;

/// <summary>
/// For compound rigidbodies such as paddle wheels and gates. The object reacts
/// physically to the marble, then its child colliders stop colliding with the
/// marble so the chain cannot rebound backwards.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CompoundPassThrough : MonoBehaviour
{
    public float contactWindow = 0.09f;

    private Collider[] ownColliders;
    private Collider marbleCollider;
    private float firstContactTime = -1f;
    private bool cleared;

    private void Start()
    {
        ownColliders = GetComponentsInChildren<Collider>(true);
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
        if (cleared || ownColliders == null || marbleCollider == null || firstContactTime < 0f)
        {
            return;
        }

        if (Time.time - firstContactTime >= contactWindow)
        {
            foreach (Collider own in ownColliders)
            {
                Physics.IgnoreCollision(own, marbleCollider, true);
            }

            cleared = true;
        }
    }
}
