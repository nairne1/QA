using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Unity.MLAgents;

//represents a goal trigger, used to test if agents can detect when they have successfully completed a level by reaching the goal area
public class GoalTriggerBug : MonoBehaviour
{
    [Header("Bug Settings")]
    [Tooltip("If true, the goal has a chance to fail instead of completing normally.")]
    [SerializeField] private bool isBug = false;

    [Tooltip("Unique ID for this trigger bug.")]
    [SerializeField] private string bugId = "BUG_TR_01";

    [Range(0f, 1f)]
    [Tooltip("Chance that the goal trigger fails when bug mode is enabled (0 = never fails, 1 = always fails).")]
    [SerializeField] private float failChance = 0.6f;

    [Header("Respawn")]
    [Tooltip("Possible spawn points. If empty, current position is used as fallback.")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    //prevents multiple simultaneous trigger activations while processing
    private bool isProcessing = false;

    //called when the component is reset in the Unity Editor
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();

        col.isTrigger = true;
        gameObject.tag = "Goal";
    }

    //detects when a player enters the goal trigger zone
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //prevent multiple simultaneous processing
        if (isProcessing)
            return;

        //only respond to player objects
        if (!collision.CompareTag("Player"))
            return;

        //try to get AI agent component first
        var agent = collision.GetComponent<SimplifiedCoverage>();
        if (agent != null)
        {
            HandleAgent(agent);
            return;
        }

        //try to get human player component
        var human = collision.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
            return;
        }
    }

    //handles AI agent interaction with the goal.
    private void HandleAgent(SimplifiedCoverage agent)
    {
        isProcessing = true;

        //check if the trigger should fail (only in bug mode)
        if (ShouldFail())
        {
            //report the trigger failure bug
            agent.FoundBug($"trigger_failure:{bugId}");

            isProcessing = false;
            return;
        }

        //reset all checkpoints in the scene so they can be activated again
        ResetAllCheckpoints();

        //reset agent's checkpoint to first spawn point
        Vector2 agentSpawn = agent.spawnPoints != null && agent.spawnPoints.Count > 0
            ? agent.spawnPoints[0].position
            : agent.transform.position;
        
        agent.SetCheckpoint(agentSpawn);

        //respawn to starting position 
        RespawnToSpawn(agent.transform, agent.GetComponent<Rigidbody2D>());

        isProcessing = false;
    }

    private void HandleHuman(HumanPlayerController human)
    {
        isProcessing = true;

        //check if the trigger should fail (only in bug mode)
        if (ShouldFail())
        {
            isProcessing = false;
            return;
        }

        //reset all checkpoints in the scene so they can be activated again
        ResetAllCheckpoints();

        //reset human's checkpoint to initial spawn point
        Vector2 humanSpawn = human.initialSpawnPoint != null
            ? human.initialSpawnPoint.position
            : human.transform.position;
        
        human.SetCheckpoint(humanSpawn);

        //respawn to starting position
        RespawnToSpawn(human.transform, human.GetComponent<Rigidbody2D>());

        isProcessing = false;
    }

    //determines whether the goal trigger should fail based on bug settings and fail chance
    private bool ShouldFail()
    {
        //normal mode - never fails
        if (!isBug)
            return false;

        //bug mode - random chance to fail based on failChance setting
        return Random.value < failChance;
    }

    //respawns to the initial spawn point
    private void RespawnToSpawn(Transform target, Rigidbody2D rb)
    {
        //default to current position
        Vector2 spawnPos = target.position;

        //use random spawn point if available
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Count);
            spawnPos = spawnPoints[randomIndex].position;
        }

        //reposition the target
        target.position = spawnPos;

        //reset physics velocity to prevent carryover
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    //resets all checkpoints in the scene to their initial state
    private void ResetAllCheckpoints()
    {
        var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.Reset();
        }
    }
}