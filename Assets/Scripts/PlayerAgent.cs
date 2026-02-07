using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using System;
using Unity.VisualScripting.ReorderableList.Element_Adder_Menu;
using System.Runtime.CompilerServices;

public class PlayerAgent : Agent
{
    [Header("Scene References")]
    [SerializeField] private Transform _goal;
    [SerializeField] private Transform _hazard;
    [SerializeField] private Transform _spawnPosition;// = new Vector3(-8f, -3.35f);

    [Header("Player Attributes")]
    private Rigidbody2D _rb;
    private bool _isGrounded = true;
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _jumpPower = 9f;

    //stores agent's renderer component - change colour when collides with wall, etc
    private Renderer _renderer;

    //player velocity
    private Vector2 _lastPos;
    private float _velocityX;

    //for training
    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    //for raycast
    [Header("Raycast Attributes")]
    [SerializeField] private float _forRayDist = 2f;
    [SerializeField] private float _downRayDist = 0.5f;

    private RaycastHit2D forwardHit;
    private RaycastHit2D downwardHit;
    [SerializeField] private LayerMask _isGround;
    [SerializeField] private LayerMask _isHazard;

    [Header("Exploration Rewards")]
    [SerializeField] private float _stepPenalty = -0.001f;

    [Header("Progress Rewards")]
    [SerializeField] private float _newMaxXRewardScale = 0.02f;
    private HashSet<Vector2Int> _visitedCells;
    private float _maxX;

    [Header("Landing Trigger Rewards")]
    [SerializeField] private float _landingReward = 0.2f;

    private HashSet<Collider2D> _visitedLandingTriggers;
    private bool _triggeredLand;

    [Header("Phase Progress Tracking")]
    [SerializeField] private int goalRateWindow = 100;

    //static so it persists across episodes
    private static int totalEpisodesEnded = 0;
    private static int totalGoalsReached = 0;

    private static Queue<int> recentGoalHits = new Queue<int>();
    private static int recentGoalHitSum = 0;

    //prevent double-counting
    private bool episodeAlreadyEnded = false;

    private bool _isJumping = false;

    //Trigger & Death Tracking
    private static int totalTriggersReached = 0;
    private static int totalDeaths = 0;

    private static Queue<int> recentTriggerHits = new Queue<int>();
    private static Queue<int> recentDeaths = new Queue<int>();

    private static int recentTriggerHitSum = 0;
    private static int recentDeathSum = 0;

    //per-ep flags
    private bool episodeTriggeredLand = false;
    private bool episodeDied = false;


    //called when the agent is first created 
    public override void Initialize()
    {
        //retreives the renderer component attached to the agent 
        _renderer = GetComponent<Renderer>();
        _rb = GetComponent<Rigidbody2D>();
        CurrentEpisode = 0;
        CumulativeReward = 0f;
        _triggeredLand = false;
    }

    private void OnDrawGizmos()
    {
        //down ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _downRayDist);

        //forward ray
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * _forRayDist);
    }

    private void FixedUpdate()
    {
        _velocityX = (transform.position.x - _lastPos.x) / Time.fixedDeltaTime;
        _lastPos = transform.position;

        //update raycast every frame
        downwardHit = Physics2D.Raycast(transform.position, Vector2.down, _downRayDist, _isGround);
        forwardHit = Physics2D.Raycast(transform.position, Vector2.right, _forRayDist, _isHazard);

        _isGrounded = Physics2D.Raycast(transform.position, Vector2.down, _downRayDist, _isGround);
    }

    //reset environment on each restart
    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode: " + CurrentEpisode);
        Debug.Log("cumulative reward: " + CumulativeReward);

        // If we have NOT activated a checkpoint yet, default checkpoint is spawn.
        if (!Checkpoint.activated)
        {
            RespawnManager.Instance.SetCheckpoint(_spawnPosition.position);
        }

        // Always respawn at the CURRENT checkpoint (spawn initially, checkpoint later)
        RespawnManager.Instance.Respawn(this);

        CurrentEpisode++;
        _renderer.material.color = Color.blue;

        //reset progress trackers
        _visitedCells = new HashSet<Vector2Int>();
        _maxX = transform.position.x;

        _visitedLandingTriggers = new HashSet<Collider2D>();
        _triggeredLand = false;

        episodeAlreadyEnded = false;

        episodeTriggeredLand = false;
        episodeDied = false;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        //the goals postition - keep between -1, 1, ensure the inputs are within a similar range 
        float goalPosx = _goal.position.x / 5f;

        //the agents position
        float agentPosx = transform.position.x / 5f;
        float agentPosy = transform.position.y / 5f;

        //sensor is the container for the observations we want the agent to know 
        //Vector Observation - space size, in the behaviour parameters, is how ever many of these floats we pass in
        sensor.AddObservation(goalPosx);//1
        sensor.AddObservation(agentPosx);//2
        sensor.AddObservation(agentPosy);//3

        //forward ray detect hazard
        bool hazardAhead = forwardHit.collider != null;
        //downward ray detect hazard
        bool groundBelow = downwardHit.collider != null;

        sensor.AddObservation(hazardAhead ? 1f : 0f);//4
        sensor.AddObservation(groundBelow ? 1f : 0f);//5

        //velocity of agent
        sensor.AddObservation(_velocityX / 10f);//6

        //goal direction sign: -1, 0, or 1
        sensor.AddObservation(Mathf.Sign(_goal.position.x - transform.position.x));//7
    }

    //telling the agent exactly what to do
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        // Branch 0
        if (Input.GetKey(KeyCode.LeftArrow))
            discreteActionsOut[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow))
            discreteActionsOut[0] = 2;
        else
            discreteActionsOut[0] = 0; // no movement

        // Branch 1
        discreteActionsOut[1] = Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
    }

    //executing actions its given - called every step
    //actions holds the decision output from the ml agents backend
    public override void OnActionReceived(ActionBuffers actions)
    {
        //move the agent using the action
        MoveAgent(actions.DiscreteActions);

        //small step penalty
        AddReward(_stepPenalty);

        //progress reward
        float x = transform.position.x;

        if (x > _maxX)
        {

            float delta = x - _maxX;
            AddReward(_newMaxXRewardScale * delta);
            _maxX = x;
        }

        if (!_isGrounded && actions.DiscreteActions[1] == 1)
        {

            AddReward(-0.003f);//small minus reward for pressing jump while already in air, reduce spamming
        }

        //discourage jumping when already jumping
        if (_isJumping && !_isGrounded)
        {
            AddReward(-0.001f);
        }

        //negative reward for falling off level
        if (transform.position.y < _spawnPosition.position.y - 6)
        {
            AddReward(-1.5f);
            episodeDied = true;
            EndWithReason("fell off");
        }

        if (MaxStep > 0 && StepCount >= MaxStep - 1)
        {
            Debug.Log($"About to hit MaxStep. StepCount={StepCount}, Reward={GetCumulativeReward()}");
        }
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        int moveAction = act[0];  // left/right
        int jumpAction = act[1];  // jump

        float moveDir = 0f;
        switch (moveAction)
        {
            case 1: // left
                moveDir = -1f;
                break;
            case 2: // right
                moveDir = 1f;
                break;
        }
        _rb.linearVelocity = new Vector2(moveDir * _moveSpeed, _rb.linearVelocity.y);

        if (jumpAction == 1 && _isGrounded)
        {
            _isJumping = true;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpPower);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Goal"))
        {
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("goal");
            GoalReached();
        }

        //landing trigger positive reward 
        if (other.CompareTag("LandTrigger"))
        {
            if (!_triggeredLand)
            {
                if (_visitedLandingTriggers.Add(other))
                {
                    AddReward(_landingReward);
                    _triggeredLand = true;
                    episodeTriggeredLand = true;
                    Debug.Log("Triggered Land");
                }
            }
        }
    }

    private void GoalReached()
    {
        AddReward(3f);//large reward for reaching goal
        CumulativeReward = GetCumulativeReward();

        //reset checkpoint back to spawn
        Checkpoint.activated = false;
        RespawnManager.Instance.SetCheckpoint(_spawnPosition.position);

        EndWithReason("goal");
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            //reset colour
            if (_renderer != null)
            {
                _renderer.material.color = Color.blue;
            }
        }
    }
    public void Kill()
    {
        AddReward(-1f); 
        episodeDied = true;
        CumulativeReward = GetCumulativeReward();
        EndWithReason("hazard");
    }

    private void EndWithReason(string reason)
    {
        if (episodeAlreadyEnded) return;
        episodeAlreadyEnded = true;

        bool hitGoal = reason == "goal";
        bool died = episodeDied;
        bool hitTrigger = episodeTriggeredLand;

        totalEpisodesEnded++;
        if (hitGoal) totalGoalsReached++;
        if (hitTrigger) totalTriggersReached++;
        if (died) totalDeaths++;

        //goal
        recentGoalHits.Enqueue(hitGoal ? 1 : 0);
        recentGoalHitSum += hitGoal ? 1 : 0;

        if (recentGoalHits.Count > goalRateWindow)
            recentGoalHitSum -= recentGoalHits.Dequeue();

        //trigger
        recentTriggerHits.Enqueue(hitTrigger ? 1 : 0);
        recentTriggerHitSum += hitTrigger ? 1 : 0;
        if (recentTriggerHits.Count > goalRateWindow)
            recentTriggerHitSum -= recentTriggerHits.Dequeue();

        //death
        recentDeaths.Enqueue(died ? 1 : 0);
        recentDeathSum += died ? 1 : 0;
        if (recentDeaths.Count > goalRateWindow)
            recentDeathSum -= recentDeaths.Dequeue();

        //custom tensorboard graphs to refer to 
        float goalOverall = (float)totalGoalsReached / totalEpisodesEnded;
        float goalWindow = (float)recentGoalHitSum / recentGoalHits.Count;

        float triggerOverall = (float)totalTriggersReached / totalEpisodesEnded;
        float triggerWindow = (float)recentTriggerHitSum / recentTriggerHits.Count;

        float deathOverall = (float)totalDeaths / totalEpisodesEnded;
        float deathWindow = (float)recentDeathSum / recentDeaths.Count;

        var stats = Academy.Instance.StatsRecorder;

        stats.Add("custom/goal_rate_overall", goalOverall, StatAggregationMethod.MostRecent);
        stats.Add($"custom/goal_rate_last_{goalRateWindow}", goalWindow, StatAggregationMethod.MostRecent);

        stats.Add("custom/trigger_rate_overall", triggerOverall, StatAggregationMethod.MostRecent);
        stats.Add($"custom/trigger_rate_last_{goalRateWindow}", triggerWindow, StatAggregationMethod.MostRecent);

        stats.Add("custom/death_rate_overall", deathOverall, StatAggregationMethod.MostRecent);
        stats.Add($"custom/death_rate_last_{goalRateWindow}", deathWindow, StatAggregationMethod.MostRecent);

        EndEpisode();
    }
}
