using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BoxCollider2D))]
//simulates a pit that can cause a softlock bug for AI agents if they fall in and don't exit within a certain time,
//works normally for human players
public class SoftLockPit : MonoBehaviour
{
    [Header("Bug Settings")]
    [SerializeField] private bool hasSoftlockBug = false;
    [SerializeField] private string bugId = "BUG_SOFTLOCK_PIT_01";

    [Header("Detection")]
    [Tooltip("Time in seconds before the softlock bug is triggered for AI agents")]
    [SerializeField] private float softlockTime = 4f;

    private BoxCollider2D _pitCollider;//box collider that defines the pit area
    private Coroutine _monitorRoutine;//coroutine reference for monitoring AI agents in the pit

    //initialise the pit collider and set it as a trigger
    private void Awake()
    {
        _pitCollider = GetComponent<BoxCollider2D>();
        _pitCollider.isTrigger = true;
    }

    //called when an object enters the pit trigger area
    private void OnTriggerEnter2D(Collider2D other)
    {
        //only act if the entering object is the player
        if (!other.CompareTag("Player"))
            return;

        //try to get AI agent first
        var agent = other.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //check if it's a human player
        var human = other.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
        }
    }

    //called when an object exits the pit trigger area
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        //if an AI agent exits the pit, stop monitoring for softlock
        if (_monitorRoutine != null)
        {
            StopCoroutine(_monitorRoutine);
            _monitorRoutine = null;
        }
    }

    //handles logic for when an AI agent enters the pit
    private void HandleAgent(SimplifiedCoverage agent)
    {
        //if the softlock bug is not enabled, simply kill the agent as normal
        if (!hasSoftlockBug)
        {
            agent.Die();
            return;
        }

        //start monitoring the agent to see if they softlock by staying in the pit too long without exiting
        if (_monitorRoutine != null)
            StopCoroutine(_monitorRoutine);

        //start a new coroutine to monitor the agent for softlock
        _monitorRoutine = StartCoroutine(AgentSoftlockTimer(agent));
    }

    private void HandleHuman(HumanPlayerController human)
    {
        //kill player as normal if the softlock bug is not enabled
        if (!hasSoftlockBug)
        {
            human.Kill();
            return;
        }

        //do nothing if bug true
    }

    //coroutine that checks if an AI agent has been in the pit for too long and triggers the softlock bug if so
    private IEnumerator AgentSoftlockTimer(SimplifiedCoverage agent)
    {
        //wait for the specified softlock time
        yield return new WaitForSeconds(softlockTime);

        if (agent == null)
            yield break;

        //report the bug
        agent.FoundBug($"softlock_pit:{bugId}");

        //respawn the agent at the last checkpoint
        agent.RespawnAtCheckpoint();

        //reset the routine
        _monitorRoutine = null;
    }
}