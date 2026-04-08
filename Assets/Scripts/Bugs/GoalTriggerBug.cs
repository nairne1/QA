using UnityEngine;
using UnityEngine.Events;

// A goal trigger that can sometimes fail to complete the level
public class GoalTriggerBug : MonoBehaviour
{
    [Tooltip("If true, the goal trigger has a chance to not trigger")]
    [SerializeField] private bool isBug = false;
    [Tooltip("Unique bug ID")]
    public string bugId = "BUG_TR_01";

    [Range(0f, 1f)]
    [Tooltip("Probability that the goal trigger fails")]
    [SerializeField] private float failChance = 0.6f;

    [Header("Completion callback")]
    public UnityEvent onLevelComplete;

    private bool _isFired = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        gameObject.tag = "Goal";
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isFired || !collision.CompareTag("Player")) return;

        //try to get AI agent first
        var agent = collision.GetComponent<QAExplorerAgentPhase1>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //try to get human player
        var human = collision.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
            return;
        }
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent)
    {
        _isFired = true;

        if (!isBug)
        {
            //normal behaviour - end episode successfully
            agent.EndEpisode();
            onLevelComplete?.Invoke();
            return;
        }

        //sometimes doesnt complete
        bool failed = Random.value < failChance;
        if (failed)
        {
            agent.FoundBug($"trigger_failure:{bugId}");
            if (SimpleRunLogger.Instance) 
                SimpleRunLogger.Instance.Log($"bug_found:trigger_failure:{bugId}");
            _isFired = false; // Allow retry
        }
        else
        {
            agent.EndEpisode();
            onLevelComplete?.Invoke();
        }
    }

    private void HandleHuman(HumanPlayerController human)
    {
        _isFired = true;

        if (!isBug)
        {
            //normal behaviour - goal reached
            human.GoalReached();
            onLevelComplete?.Invoke();
            return;
        }

        //sometimes doesn't complete
        bool failed = Random.value < failChance;
        if (failed)
        {
            human.FoundBug($"trigger_failure:{bugId}");
            if (SimpleRunLogger.Instance)
                SimpleRunLogger.Instance.Log($"bug_found:trigger_failure:{bugId}");
            _isFired = false; // Allow retry
        }
        else
        {
            human.GoalReached();
            onLevelComplete?.Invoke();
            //no retry as player has reached the goal and knows it, so we don't reset _isFired
        }
    }
}
