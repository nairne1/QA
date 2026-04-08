using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using System;

public class PlayerAgentExplore : Agent
{
    [Header("Scene References")]
    [SerializeField] private Transform _spawnPosition;

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
    [SerializeField] private float _downRayDist = 0.5f;

    [SerializeField] private LayerMask _isGround;

    [Header("Exploration Rewards")]
    [SerializeField] private float _stepPenalty = -0.001f;
    [SerializeField] private float _cellSize = 0.5f;
    [SerializeField] private float _newCellReward = 0.03f;

    [Header("Bug Rewards")]
    [SerializeField] private float _bugFoundReward = 3f;

    [Header("Death / Fall")]
    [SerializeField] private float _deathPenalty = -1f;
    [SerializeField] private float _fallBelowSpawnOffset = 6f;
    [SerializeField] private bool _endEpisodeOnDeath = false; // Toggle: should death end the episode?

    [Header("Anti-stall")]
    [SerializeField] private int _stallStepThreshold = 10;
    [SerializeField] private float _stallPenalty = -1.0f;

    [Header("Jump Inconsistency Bug")]
    [SerializeField] private bool _hasJumpBug = false;
    [SerializeField] private string _jumpBugId = "BUG_JUMP_INCONSISTENT_01";
    [SerializeField] private float _frameRateSpikeIntervalMin = 3f; // Min seconds between spikes
    [SerializeField] private float _frameRateSpikeIntervalMax = 10f; // Max seconds between spikes
    [SerializeField] private float _frameRateSpikeDuration = 0.2f; // Spike lasts X seconds (increased for visibility)
    [SerializeField] private int _targetFPSDuringSpike = 15; // FPS during spike (lower = more noticeable)
    [SerializeField] private int _normalTargetFPS = 60; // Normal FPS
    [SerializeField] private bool _showJumpDebugUI = true; // Show jump height on screen

    private float _nextSpikeTime = 0f;
    private float _spikeTimer = 0f;
    private bool _isSpikingFrameRate = false;
    private float _lastJumpHeight = 0f;
    private float _lastJumpTime = 0f;
    private List<float> _recentJumpHeights = new List<float>();
    private const int _maxJumpHistorySize = 5;
    private bool _isTrackingJump = false;

    private int _stepsSinceNewCell = 0;

    private HashSet<int> _visitedCellsX = new HashSet<int>();
    private HashSet<string> _foundBugs = new HashSet<string>();

    private HashSet<string> _collectedThisLife = new HashSet<string>();
    private HashSet<string> _collectedAcrossRespawns = new HashSet<string>();

    private bool _episodeAlreadyEnded = false;

    // Event to notify other scripts when episode begins
    public static event Action OnEpisodeBeginEvent;

    //called when the agent is first created 
    public override void Initialize()
    {
        //retreives the renderer component attached to the agent 
        _renderer = GetComponent<Renderer>();
        _rb = GetComponent<Rigidbody2D>();
        _lastPos = transform.position;
    }

    private void OnDrawGizmos()
    {
        //down ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _downRayDist);
    }

    private void OnGUI()
    {
        if (_hasJumpBug && _showJumpDebugUI)
        {
            // Display jump debug info on screen
            GUI.color = Color.white;
            GUIStyle style = new GUIStyle();
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            int yOffset = 10;
            
            // Show actual FPS
            float currentFPS = 1f / Time.unscaledDeltaTime;
            GUI.Label(new Rect(10, yOffset, 400, 30), $"FPS: {(int)currentFPS} (Target: {Application.targetFrameRate})", style);
            yOffset += 25;

            if (_isSpikingFrameRate)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, yOffset, 400, 30), "*** FPS SPIKE ACTIVE ***", style);
                yOffset += 25;
            }

            GUI.color = _isTrackingJump ? Color.magenta : Color.yellow;
            GUI.Label(new Rect(10, yOffset, 400, 30), $"Last Jump Height: {_lastJumpHeight:F2} {(_isTrackingJump ? "(tracking...)" : "")}", style);
            yOffset += 25;

            if (_recentJumpHeights.Count > 0)
            {
                GUI.color = Color.cyan;
                string jumpsStr = string.Join(", ", _recentJumpHeights.ConvertAll(h => h.ToString("F2")));
                GUI.Label(new Rect(10, yOffset, 600, 30), $"Recent Jumps: {jumpsStr}", style);
                yOffset += 25;

                if (_recentJumpHeights.Count >= 2)
                {
                    float minHeight = float.MaxValue;
                    float maxHeight = float.MinValue;
                    foreach (float h in _recentJumpHeights)
                    {
                        if (h < minHeight) minHeight = h;
                        if (h > maxHeight) maxHeight = h;
                    }
                    float variance = maxHeight - minHeight;
                    GUI.color = variance > 0.5f ? Color.red : Color.green;
                    GUI.Label(new Rect(10, yOffset, 400, 30), $"Jump Variance: {variance:F2} (Min: {minHeight:F2}, Max: {maxHeight:F2})", style);
                }
            }
            else
            {
                GUI.color = Color.gray;
                GUI.Label(new Rect(10, yOffset, 400, 30), "No jumps recorded yet", style);
            }
        }
    }

    private void FixedUpdate()
    {
        _velocityX = (transform.position.x - _lastPos.x) / Time.fixedDeltaTime;
        _lastPos = transform.position;

        //update raycast every frame
        _isGrounded = Physics2D.Raycast(transform.position, Vector2.down, _downRayDist, _isGround);

        // Handle frame rate spikes for jump bug
        if (_hasJumpBug)
        {
            HandleFrameRateSpikes();
        }
    }

    //reset environment on each restart
    public override void OnEpisodeBegin()
    {
        CurrentEpisode++;
        Debug.Log("Episode: " + CurrentEpisode);
        Debug.Log("cumulative reward: " + CumulativeReward);

        _collectedThisLife.Clear();
        _collectedAcrossRespawns.Clear(); // Clear collected items across respawns for new episode

        //reset checkpoints
        //Checkpoint.activated = false;
        RespawnManager.Instance.SetCheckpoint(_spawnPosition.position);
        RespawnManager.Instance.Respawn(this);

        _episodeAlreadyEnded = false;
        _stepsSinceNewCell = 0;
        _visitedCellsX.Clear();
        _foundBugs.Clear();
        _lastPos = transform.position;

        // Reset jump bug tracking
        _nextSpikeTime = Time.time + UnityEngine.Random.Range(_frameRateSpikeIntervalMin, _frameRateSpikeIntervalMax);
        _spikeTimer = 0f;
        _isSpikingFrameRate = false;
        _recentJumpHeights.Clear();
        _lastJumpHeight = 0f;
        _isTrackingJump = false;
        Application.targetFrameRate = _normalTargetFPS;

        // Fire event to notify collectibles and other objects to reset
        OnEpisodeBeginEvent?.Invoke();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position.x / 10f);//1
        sensor.AddObservation(transform.position.y / 10f);//2
        sensor.AddObservation(_isGrounded ? 1f : 0f);//3
        sensor.AddObservation(_velocityX / 10f);//4
        sensor.AddObservation(_rb.linearVelocity.y / 10f);//5
        sensor.AddObservation(_foundBugs.Count / 10f);//6
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

        //coverage reward
        if (TryVisitNewCell(transform.position))
        {
            AddReward(_newCellReward);
            _stepsSinceNewCell = 0;// reset when progress is made
        }
        else {

            _stepsSinceNewCell++;

            if (_stepsSinceNewCell >= _stallStepThreshold)
            {
                AddReward(_stallPenalty);
                _stepsSinceNewCell = 0; // reset so penalty isn't spammed
            }
        }

        //if max bugs found, end ep and reward 
        if (_foundBugs.Count == 2) {

            AddReward(2f);
            EndWithReason("all bugs found");
        }

        //fall off penalty
        if (transform.position.y < _spawnPosition.position.y - _fallBelowSpawnOffset)
        {
            AddReward(_deathPenalty);
            CumulativeReward = GetCumulativeReward();
            
            if (_endEpisodeOnDeath)
            {
                EndWithReason("fallen");
            }
            else
            {
                // Respawn at checkpoint within the same episode
                HandleDeath("fallen");
            }
        }

        if (MaxStep > 0 && StepCount >= MaxStep - 1)
        {
            CumulativeReward = GetCumulativeReward();
            Debug.Log($"About to hit MaxStep. StepCount={StepCount}, Reward={GetCumulativeReward()}");
            EndWithReason("max step reached");
        }
    }

    public void RegisterCollection(string id)
    {
        _collectedThisLife.Add(id);
        _collectedAcrossRespawns.Add(id);
    }

    public bool HasCollectedBefore(string id)
    {
        return _collectedAcrossRespawns.Contains(id);
    }

    public void GoalReached()
    {
        AddReward(3f);//large reward for reaching goal
        CumulativeReward = GetCumulativeReward();

        //reset checkpoint back to spawn
        //Checkpoint.activated = false;
        RespawnManager.Instance.SetCheckpoint(_spawnPosition.position);

        EndWithReason("goal");
    }

    private bool TryVisitNewCell(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x / _cellSize);
        //int cy = Mathf.FloorToInt(worldPos.y / _cellSize);

        return _visitedCellsX.Add(cx);
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
            PerformJump();
        }
    }

    private void PerformJump()
    {
        // Store the Y position at the start of the jump
        float jumpStartY = transform.position.y;
        _lastJumpTime = Time.time;

        if (_hasJumpBug)
        {
            // BUG: Jump is frame-rate dependent (uses Time.deltaTime scaled)
            // During low FPS, Time.deltaTime increases, making jump weaker
            float frameMultiplier = Time.deltaTime / (1f / _normalTargetFPS);
            float frameRateDependentJump = _jumpPower * frameMultiplier;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, frameRateDependentJump);

            Debug.Log($"[JUMP] Frame multiplier: {frameMultiplier:F2}, Jump velocity: {frameRateDependentJump:F2}, FPS: {(int)(1f / Time.deltaTime)}");
        }
        else
        {
            // CORRECT: Jump uses fixed velocity (frame-rate independent)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpPower);
        }

        // Always track jump height (regardless of bug status) for debugging
        if (!_isTrackingJump)
        {
            StartCoroutine(TrackJumpHeight(jumpStartY));
        }
    }

    private System.Collections.IEnumerator TrackJumpHeight(float startY)
    {
        _isTrackingJump = true;
        Debug.Log($"[JUMP] Started tracking from Y: {startY:F2}");

        // Wait until agent reaches peak of jump or lands
        float maxY = startY;
        float checkTime = 0f;
        float maxCheckTime = 2f; // Check for 2 seconds max

        while (checkTime < maxCheckTime)
        {
            checkTime += Time.deltaTime;

            if (transform.position.y > maxY)
            {
                maxY = transform.position.y;
            }

            // If agent has landed or is falling significantly
            if ((_isGrounded && _rb.linearVelocity.y <= 0.1f) || _rb.linearVelocity.y < -3f)
            {
                Debug.Log($"[JUMP] Stopped tracking - Grounded: {_isGrounded}, VelY: {_rb.linearVelocity.y:F2}");
                break;
            }

            yield return null;
        }

        float jumpHeight = maxY - startY;
        _lastJumpHeight = jumpHeight;
        _recentJumpHeights.Add(jumpHeight);

        if (_recentJumpHeights.Count > _maxJumpHistorySize)
        {
            _recentJumpHeights.RemoveAt(0);
        }

        Debug.Log($"[JUMP HEIGHT] {jumpHeight:F2} (Peak Y: {maxY:F2}, Start Y: {startY:F2})");
        Debug.Log($"[JUMP HISTORY] Recent: {string.Join(", ", _recentJumpHeights.ConvertAll(h => h.ToString("F2")))}");

        // Detect if there's significant variance in jump heights (indicating the bug)
        if (_hasJumpBug && _recentJumpHeights.Count >= 3)
        {
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            foreach (float height in _recentJumpHeights)
            {
                if (height < minHeight) minHeight = height;
                if (height > maxHeight) maxHeight = height;
            }

            float variance = maxHeight - minHeight;

            // If variance is > 0.5 units (more aggressive threshold), the bug is detected
            if (variance > 0.5f && !_foundBugs.Contains($"jump:{_jumpBugId}"))
            {
                FoundBug($"jump:{_jumpBugId}");
                Debug.Log($"[JUMP BUG DETECTED] Variance: {variance:F2}, Min: {minHeight:F2}, Max: {maxHeight:F2}");
            }
        }

        _isTrackingJump = false;
    }

    private void HandleFrameRateSpikes()
    {
        if (_isSpikingFrameRate)
        {
            // Currently in a spike
            _spikeTimer += Time.fixedDeltaTime;

            if (_spikeTimer >= _frameRateSpikeDuration)
            {
                // End spike, return to normal FPS
                _isSpikingFrameRate = false;
                _spikeTimer = 0f;
                Application.targetFrameRate = _normalTargetFPS;
                
                // Schedule next spike at random interval
                _nextSpikeTime = Time.time + UnityEngine.Random.Range(_frameRateSpikeIntervalMin, _frameRateSpikeIntervalMax);
                
                Debug.Log($"[Frame Rate] Spike ended. Returning to {_normalTargetFPS} FPS. Next spike in {(_nextSpikeTime - Time.time):F1}s");
            }
        }
        else
        {
            // Check if it's time for a new spike
            if (Time.time >= _nextSpikeTime)
            {
                // Start a new spike
                _isSpikingFrameRate = true;
                _spikeTimer = 0f;

                Application.targetFrameRate = _targetFPSDuringSpike;
                Debug.Log($"[Frame Rate] *** SPIKE STARTED! *** Target FPS: {_targetFPSDuringSpike} for {_frameRateSpikeDuration}s");
            }
        }
    }

    public void FoundBug(string bugId)
    {
        if (string.IsNullOrEmpty(bugId)) bugId = "unknown_bug_id";

        // One-time reward per unique bug object (HashSet prevents farming)
        if (_foundBugs.Add(bugId))
        {
            // Make the 2nd (and 3rd...) bug more valuable than the 1st.
            // This helps break the "find one bug then camp" local optimum.
            int uniqueCount = _foundBugs.Count;           // 1, then 2, then 3...
            
            AddReward(_bugFoundReward);

            Debug.Log($"BUG FOUND: {bugId} (unique={uniqueCount}, reward={_bugFoundReward})");
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log($"bug_found:{bugId}");

            // Log the bug finding event with more details
            if (SimpleRunLogger.Instance)
            {
                SimpleRunLogger.Instance.Log($"bug_detail:{bugId}:{_lastJumpHeight:F2}:{_recentJumpHeights.Count}:{Time.time:F2}");
            }
        }
    }

    public void Kill()
    {
        AddReward(_deathPenalty);
        if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("hazard");
        CumulativeReward = GetCumulativeReward();
        
        if (_endEpisodeOnDeath)
        {
            EndWithReason("hazard");
        }
        else
        {
            // Respawn at checkpoint within the same episode
            HandleDeath("hazard");
        }
    }

    private void HandleDeath(string reason)
    {
        Debug.Log($"[Death] Reason: {reason}. Respawning at checkpoint within episode.");
        
        // Clear items collected only in this life (not across respawns)
        _collectedThisLife.Clear();
        
        // Respawn at the current checkpoint (not the initial spawn)
        RespawnManager.Instance.Respawn(this);

        if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log($"death_respawn:{reason}");
    }

    public void EndWithReason(string reason) {

        if (_episodeAlreadyEnded) return;

        _episodeAlreadyEnded = true;

        var stats = Academy.Instance.StatsRecorder;

        //per ep metric for tensorboard
        stats.Add("explore/cells_visited_episode", _visitedCellsX.Count, StatAggregationMethod.MostRecent);
        stats.Add("explore/bugs_found_episode", _foundBugs.Count, StatAggregationMethod.MostRecent);

        Debug.Log(
            $"[Episode End] Bugs found this episode: {_foundBugs.Count} | " +
            $"Cells visited: {_visitedCellsX.Count}"
        );
        
        EndEpisode();
    }
}
