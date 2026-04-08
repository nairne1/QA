using UnityEngine;

[RequireComponent(typeof(Collider2D))]
//checkpoint that can be activated by the player.
//It can be a normal checkpoint or a bug checkpoint.
//If it's a bug checkpoint, it will trigger the FoundBug method in the agent script.
public class Checkpoint : MonoBehaviour
{
    [Tooltip("If true, this checkpoint can only be activated once per episode")]
    public bool oneTime = true;
    [Tooltip("If true, this checkpoint is bugged. It won't save the respawn")]
    public bool isBug = false;

    [Header("Unique ID for this bug checkpoint")]
    public string bugId = "BUG_CP_01";
    
    public bool _activated = false;

    public void Reset()
    {
        gameObject.tag = "Checkpoint";
        GetComponent<Collider2D>().isTrigger = true;

        _activated = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        //check if already activated
        if (_activated && oneTime) return;

        //try to get AI agent first
        var agent = other.GetComponent<QAExplorerAgentPhase1>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //try to get human player
        var human = other.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
            return;
        }
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent)
    {
        if (!isBug)
        {
            //normal
            agent.SetCheckpoint(transform.position);
            _activated = true;
        }
        
        _activated = true;
    }

    private void HandleHuman(HumanPlayerController human)
    {
        if (isBug)
        {
            //bug checkpoint - doesn't save
            human.FoundBug($"checkpoint:{bugId}");
        }
        else
        {
            //normal checkpoint - save position
            human.SetCheckpoint(transform.position);
        }
        
        _activated = true;
    }
}
