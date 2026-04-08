using UnityEngine;

//simulates a platform with a missing collider
public class MissingCollider : MonoBehaviour
{
    [Tooltip("If true, the platform's collider is disabled and the player can fall through")]
    [SerializeField] private bool isBug = false;
    [Tooltip("Unique bug ID")]
    public string bugId = "BUG_PLAT_01";

    [Tooltip("The collider you stand on")]
    [SerializeField] private Collider2D solidCollider;
    [Tooltip("Trigger volume covering the platform area")]
    [SerializeField] private Collider2D detectTrigger;

    [Header("Fall-through detection")]
    [Tooltip("How far the player must fall below platform before we consider it a fall-through")]
    [SerializeField] private float belowPlatformThreshold = 0.15f;

    private float _platformY;
    private bool _reported;

    void Awake()
    {
        //if solid collider not assigned, try to find one on the same object
        if (!solidCollider) solidCollider = GetComponent<Collider2D>();
        //platform y position is used to determine if player fell through
        _platformY = transform.position.y;

        //if this is a bug, disable the solid collider so player can fall through
        if (isBug && solidCollider)
        {
            solidCollider.enabled = false;
        }

        //ensure detect trigger is a trigger
        if (detectTrigger)
            detectTrigger.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        //only report if this is a bug and we haven't already reported it, and only if the player is in the trigger
        if (!isBug || _reported) return;
        if (!other.CompareTag("Player")) return;

        //if player is inside trigger but is below the platform surface, they fell through it
        if (other.transform.position.y < _platformY - belowPlatformThreshold)
        {
            _reported = true;

            //try to get AI agent first
            var agent = other.GetComponent<QAExplorerAgentPhase1>();
            if (agent != null)
            {
                HandleAgent(agent);
                return;
            }

            //human player falls through naturally - no notification
            //They notice: "Wait, I just fell through that platform!"
        }
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent)
    {
        agent.FoundBug($"missing_collider:{bugId}");
        if (SimpleRunLogger.Instance) 
            SimpleRunLogger.Instance.Log($"bug_found:missing_collider:{bugId}");
    }
}
