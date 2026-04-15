using System.Collections.Generic;
using UnityEngine;

//simulates an invisible wall bug
//cant walk through because thers a collider but not a visible wall sprite
public class InvisibleWall : MonoBehaviour
{
    [Header("Bug Settings")]
    [SerializeField] private bool isBug = true;
    [SerializeField] private string bugId = "BUG_INVIS_WALL_01";

    [Header("Detection")]
    [Tooltip("Layers that count as real, visible walls.")]
    [SerializeField] private LayerMask visibleWallLayer;

    [Tooltip("Distance to check for a real wall in front of the agent.")]
    [SerializeField] private float wallCheckDistance = 0.25f;

    [Tooltip("How small the x velocity must be to count as blocked.")]
    [SerializeField] private float blockedVelocityThreshold = 0.05f;

    [Tooltip("How long the agent must keep pushing into the invisible wall before reporting it.")]
    [SerializeField] private float blockTimeRequired = 0.25f;

    [Tooltip("Minimum intended movement input to count as trying to move.")]
    [SerializeField] private float minIntentThreshold = 0.5f;

    [Tooltip("Height offset for the forward wall raycast.")]
    [SerializeField] private float rayHeightOffset = 0.1f;

    //tracks how long each agent has been blocked against this wall
    private readonly Dictionary<SimplifiedCoverage, float> blockedTimers = new();
    //to avoid spamming multiple bug found reports for the same agent
    private readonly HashSet<SimplifiedCoverage> reportedAgents = new();

    //reset function to ensure collider is set up correctly and sprite is hidden
    public void Reset()
    {
        var col = GetComponent<BoxCollider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = false;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        //only run the detection if this is actually supposed to be a bug
        if (!isBug)
            return;

        //only care about player collisions
        if (!collision.collider.CompareTag("Player"))
            return;

        //get the agent component from the collider
        var agent = collision.collider.GetComponent<SimplifiedCoverage>();
        if (agent == null)
            return;

        //if we've already reported this agent for this wall, don't report again until they exit and re-enter
        if (reportedAgents.Contains(agent))
            return;

        //get the rigidbody to check velocity
        Rigidbody2D rb = collision.rigidbody;
        if (rb == null)
            return;

        //get the agent's movement direction (positive for right, negative for left)
        float intent = agent.IntendedMoveDirection;

        //agent is not actually trying to move left/right
        if (Mathf.Abs(intent) < minIntentThreshold)
        {
            //reset timer
            blockedTimers[agent] = 0f;
            return;
        }

        //determine which direction the agent is trying to move
        Vector2 moveDir = intent > 0f ? Vector2.right : Vector2.left;

        //if a visible wall exists in front, this is normal blocking, not a bug
        bool visibleWallAhead = HasVisibleWall(collision.collider, moveDir);
        if (visibleWallAhead)
        {
            blockedTimers[agent] = 0f;
            return;
        }

        //agent is trying to move, but barely moving horizontally
        bool isBlocked = Mathf.Abs(rb.linearVelocity.x) <= blockedVelocityThreshold;

        if (!isBlocked)
        {
            //reset timer if not actually blocked
            blockedTimers[agent] = 0f;
            return;
        }

        //agent is trying to move into this wall and not making progress, start counting how long they've been blocked
        if (!blockedTimers.ContainsKey(agent))
            blockedTimers[agent] = 0f;

        //increment timer
        blockedTimers[agent] += Time.fixedDeltaTime;

        //if they've been blocked long enough, report the invisible wall bug
        if (blockedTimers[agent] >= blockTimeRequired)
        { 
            ReportInvisibleWall(agent, intent);
            reportedAgents.Add(agent);
            blockedTimers[agent] = 0f;
        }
    }

    //reset tracking when agent exits collision
    private void OnCollisionExit2D(Collision2D collision)
    {
        var agent = collision.collider.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
            blockedTimers.Remove(agent);
        }
    }

    //helper function to check if there's a visible wall in the given direction from the agent's position
    private bool HasVisibleWall(Collider2D actorCollider, Vector2 direction)
    {
        Bounds bounds = actorCollider.bounds;

        //cast a ray from the agent in the movement direction to check for a visible wall
        Vector2 origin = new Vector2(
            bounds.center.x,
            bounds.center.y + rayHeightOffset
        );

        //cast a ray from the agent in the movement direction to check for a visible wall
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance, visibleWallLayer);

        //no wall hit, likely an invisible wall if the agent is blocked
        if (!hit.collider)
            return false;

        //ignore this invisible wall object itself
        if (hit.collider.gameObject == gameObject)
            return false;

        //visible wall detected
        return true;
    }

    //reports the invisible wall bug for the given agent and movement intent direction
    private void ReportInvisibleWall(SimplifiedCoverage agent, float intent)
    {
        //conmvert intent to a readable direction
        string directionText = intent > 0f ? "right" : "left";
        string foundBugId = $"invisible_wall:{bugId}";

        //report the bug
        agent.FoundBug(foundBugId);
    }
}