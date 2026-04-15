using UnityEngine;

[RequireComponent(typeof(Collider2D))]
//checkpoint can be activated by the player
//can be configured to be a bug that doesn't actually save the respawn point,
//but still registers as the expected checkpoint for the agent
public class Checkpoint : MonoBehaviour
{
    [Tooltip("If true, this checkpoint can only be activated once per episode")]
    public bool oneTime = true;

    [Tooltip("If true, this checkpoint is bugged and won't actually save the respawn")]
    public bool isBug = false;

    [Header("Unique ID for this bug checkpoint")]
    public string bugId = "BUG_CP_01";

    //tracks whether the checkpoint has been activated
    private bool _activated = false;

    //reset the checkpoint to its initial state at the start of each episode
    public void Reset()
    {
        gameObject.tag = "Checkpoint";
        GetComponent<Collider2D>().isTrigger = true;
        _activated = false;
    }

    //when the player enters the checkpoint trigger, attempt to activate it
    private void OnTriggerEnter2D(Collider2D other)
    {
        //only activate if the entering object is the player
        if (!other.CompareTag("Player"))
            return;

        //if this checkpoint is one-time use and has already been activated, do nothing
        if (_activated && oneTime)
            return;

        //try to get AI agent first
        var agent = other.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
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

    private void HandleAgent(SimplifiedCoverage agent)
    {
        //remember what the latest checkpoint 'should' be
        agent.RegisterExpectedCheckpoint(transform.position, isBug, bugId);

        //only save the real respawn point if this checkpoint is not bugged
        if (!isBug)
        {
            agent.SetCheckpoint(transform.position);
        }

        _activated = true;
    }

    private void HandleHuman(HumanPlayerController human)
    {
        //humans can just work normally, they'll notice if it's wrong
        if (!isBug)
        {
            human.SetCheckpoint(transform.position);
        }

        _activated = true;
    }
}