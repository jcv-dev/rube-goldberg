using UnityEngine;

/// <summary>
/// Invisible checkpoint placed along the marble run. Only the tagged player
/// ball can advance the HUD, and each checkpoint fires once.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MachineStageTrigger : MonoBehaviour
{
    public MachineHud hud;
    public int stageIndex;
    public string stageLabel;

    private bool fired;

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fired || hud == null)
        {
            return;
        }

        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.CompareTag("Player"))
        {
            return;
        }

        fired = true;
        hud.ReachStage(stageIndex, stageLabel);
    }
}
