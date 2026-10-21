using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal in-game readout used to prove the chain is progressing.
/// It is intentionally dependency-free (legacy uGUI + built-in font).
/// </summary>
public class MachineHud : MonoBehaviour
{
    public Text stageCounter;
    public Text stageName;
    public Text status;
    public Image progressFill;
    public int totalStages = 16;

    private int currentStage;

    private void Start()
    {
        Refresh("Awaiting release");
    }

    public void ReachStage(int index, string displayName)
    {
        if (index <= currentStage)
        {
            return;
        }

        currentStage = Mathf.Clamp(index, 0, totalStages);
        string suffix = currentStage >= totalStages ? "  /  Chain complete" : "  /  Live physics";
        Refresh(displayName, suffix);
        Debug.Log($"[Rube Goldberg] Stage {currentStage:00}/{totalStages}: {displayName}");
    }

    private void Refresh(string displayName, string suffix = "  /  Live physics")
    {
        if (stageCounter != null)
        {
            stageCounter.text = $"{currentStage:00}  /  {totalStages:00}";
        }

        if (stageName != null)
        {
            stageName.text = displayName.ToUpperInvariant();
        }

        if (status != null)
        {
            status.text = "RUBE GOLDBERG MACHINE" + suffix.ToUpperInvariant();
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = totalStages > 0 ? (float)currentStage / totalStages : 0f;
        }
    }
}
