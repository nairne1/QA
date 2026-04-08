using UnityEngine;

[RequireComponent(typeof(Collider2D))]
//Hazard trigger zone. Can be configured to be a bug that doesn't kill the player, but instead logs a bug found.
public class Hazard : MonoBehaviour
{
    [Tooltip("If true, hazard is bugged")]
    [SerializeField] bool isBug = false;

    [Tooltip("Unique bug report ID")]
    public string bugId = "BUG_HZ_01";
    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer("Hazard");
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var agent = other.GetComponent<QAExplorerAgentPhase1>();
        if (agent != null) {

            HandleAgent(agent);
            return;
        }

        var human = other.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
            return;
        }
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent) { 
    
        if (agent != null) {
            if (!isBug)
            {
                //normal hazard - end episode with penalty
                agent.Die();
            }
            else
            {
                //bug hazard - doesn't kill
                agent.FoundBug($"hazard:{bugId}");
            }
         }
    }

    private void HandleHuman(HumanPlayerController human) {

        if (!isBug)
        {
            //normal hazard
            human.Kill();
        }
        else
        {
            //bug hazard
            human.FoundBug($"hazard:{bugId}");
        }
    }
}
