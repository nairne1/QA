using UnityEngine;

//simulates an invisible wall
public class InvisibleWall : MonoBehaviour
{
    [Tooltip("If true, wall is a bug")]
    [SerializeField] private bool isBug = true;
    [Tooltip("Unique bug ID")]
    public string bugId = "BUG_COL_01";

    private bool _reported = false;//prevents multiple reports for the same bug

    public void Reset()
    {
        var col = GetComponent<BoxCollider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        //make visually invisible
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        //if not a bug or is reported or not hit player, do nothing
        if (!isBug || _reported || !collision.collider.CompareTag("Player")) return;

        _reported = true;

        //try to get AI agent first
        var agent = collision.collider.GetComponent<QAExplorerAgentPhase1>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //human player hits invisible wall naturally - no notification
        //They notice: "Something invisible is blocking me here"
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent)
    {
        agent.FoundBug($"collision_error:{bugId}");
        if (SimpleRunLogger.Instance) 
            SimpleRunLogger.Instance.Log($"bug_found:collision_error:{bugId}");
    }
}
