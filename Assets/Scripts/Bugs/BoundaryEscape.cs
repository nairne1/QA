using UnityEngine;

[RequireComponent(typeof(Collider2D))]
//detects when player escapes the boundary and ends the episode with a bug found
public class BoundaryEscape : MonoBehaviour
{
    [Tooltip("If true, escaping this boundary is a bug")]
    [SerializeField] private bool isBug = true;
    [Tooltip("Unique bug ID")]
    public string bugId = "BUG_BOUND_01";

    //reset the trigger to be a trigger collider with the correct name
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.name = "BoundaryEscapeTrigger";
    }

    //called if the player exits the boundary
    private void OnTriggerExit2D(Collider2D other)
    {
        //only act if this boundary exit is a bug, and if the exiting object is the player
        if (!isBug) return;
        if (!other.CompareTag("Player")) return;

        //try to get AI agent first
        var agent = other.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //human player escapes boundary naturally
    }

    private void HandleAgent(SimplifiedCoverage agent)
    {
        //report the bug, log it, and resapwn the agent
        agent.FoundBug($"boundary_bug:{bugId}");
        agent.Die();
    }
}
