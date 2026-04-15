using System.Runtime.CompilerServices;
using UnityEngine;

//simulates a platform with a missing collider bug
//if 'isBug' = true, the solid collider is turned off
//a trigger detection zone detects if the agent passes through the platform
public class MissingCollider : MonoBehaviour
{
    [Header("Bug Settings")]
    [Tooltip("If true, the platform collider is disabled to simulate the bug.")]
    [SerializeField] private bool isBug = false;

    [Tooltip("Unique ID for this bug instance.")]
    [SerializeField] private string bugId = "BUG_MISSING_COLLIDER_01";

    [Header("Platform References")]
    [Tooltip("The solid platform collider that should normally block the player.")]
    [SerializeField] private Collider2D solidCollider;

    [Tooltip("A trigger collider covering the platform area, used to detect fall-through.")]
    [SerializeField] private Collider2D detectTrigger;

    private float platformTopY; //coord of the top oif the platform, used to check if player is falling through
    private bool hasReported; //to prevent multiple reports

    private SimplifiedCoverage _agent; //reference to the agent

    //initialises references and applies bug state
    private void Awake()
    {
        //auto assign the component if not set in inspector
        if (solidCollider == null)
            solidCollider = GetComponent<Collider2D>();

        //calculate the top Y coordinate of the platform for fall-through detection
        if (solidCollider != null)
            platformTopY = solidCollider.bounds.max.y;
        else
            platformTopY = transform.position.y;

        //ensure the detection trigger is set as a trigger
        if (detectTrigger != null)
            detectTrigger.isTrigger = true;

        //apply the initial bug state
        ApplyBugState();
    }

    //called when the inspector values are changed
    private void OnValidate()
    {
        if (detectTrigger != null)
            detectTrigger.isTrigger = true;
    }

    //applies the bug state by enabling/disabling the solid collider
    private void ApplyBugState()
    {
        if (solidCollider != null)
            solidCollider.enabled = !isBug;
    }

    //detects if the player falls through the platform when the bug is active
    private void OnTriggerEnter2D(Collider2D other)
    {
        //only check for fall-through if the bug is active and we haven't already reported it
        if (!isBug || hasReported)
            return;

        //only detect player collisions
        if (!other.CompareTag("Player")) return;

        //get the agent component
        var agent = other.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
            //report bug to the agent
            agent.FoundBug($"collision_error:{bugId}");
            return;
        }
    }

    //allows toggling the bug state at runtime
    public void SetBugState(bool enabled)
    {
        isBug = enabled;
        hasReported = false;
        ApplyBugState();
    }
}