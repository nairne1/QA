using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
//explorer agent for QA phase 1 - basic movement and coverage
public class QAExplorerAgentPhase1 : Agent
{
    [Header("Scene refs")]
    [Tooltip("Records which cels have been visited")]
    public GridCoverageTracker2D coverage;
    [Tooltip("Position of feet for ground check")]
    public Transform groundCheck;
    [Tooltip("Layer for ground check")]
    public LayerMask groundLayer;
    [Tooltip("Spawn points to randomise starting location")]
    public List<Transform> spawnPoints;

    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpImpulse = 10f;
    public float groundCheckRadius = 0.12f;

    [Header("Rays (observations)")]
    public float rayDist = 3f;

    [Header("Rewards")]
    [Tooltip("Reward for entering a new WALKABLE grid cell for the first time this episode")]
    public float newCellReward = 5.0f;

    [Tooltip("One-time penalty for re-entering a visited cell (should be less than guidance reward)")]
    public float revisitCellPenalty = -10.5f;

    [Tooltip("Small step penalty")]
    public float stepPenalty = -0.005f;

    [Tooltip("Extra penalty applied if the agent is 'stuck' (low movement) for too long)")]
    public float stuckPenalty = -1.0f;

    [Tooltip("Seconds of low movement before we consider the agent stuck")]
    public float stuckTimeThreshold = 2.0f;

    [Tooltip("Movement speed threshold (consider the agent not moving)")]
    public float stuckMoveThreshold = 1f;

    [Header("Exploration Guidance")]
    [Tooltip("Enable gentle reward/penalty for moving toward/away from unexplored cells")]
    public bool guideTowardUnexplored = true;

    [Tooltip("Reward per unit of distance moved closer to nearest unexplored cell")]
    public float approachRewardPerUnit = 20.0f;

    [Tooltip("Penalty per unit of distance moved away from nearest unexplored cell")]
    public float retreatPenaltyPerUnit = -0.05f;

    [Header("Phase 2: Death & Checkpoints")]
    [Tooltip("Penalty for dying to hazards")]
    public float deathPenalty = -1.0f;

    [Tooltip("Small reward for reaching checkpoints")]
    public float checkpointReward = 1f;

    [Tooltip("Max deaths before forcing episode end")]
    public int maxDeathsPerEpisode = 25;

    [Header("Bug Rewards")]
    public float bugFoundReward = 1.5f;

    private Rigidbody2D _rb;
    private bool _isGrounded;
    [Tooltip("Position last step, for detecting whether stuck or not")]
    private Vector2 _lastPos;
    [Tooltip("Timer tracks while agent is stuck/ barely moving")]
    private float _stuckTimer;
    [Tooltip("Prevents duplicate bug reward")]
    private HashSet<string> _foundBugs = new HashSet<string>();

    [Tooltip("Total seconds agent was considered stuck")]
    [HideInInspector] public float EpisodeStuckSeconds = 0f;

    //checkpoint tracking
    private Vector2 _currentCheckpoint;
    private int _deathCount = 0;

    //progress tracking for difficult sections
    private float _maxXReached = 0f;

    //tracking for exploration guidance
    private int _lastCellIndex = -1; //track which cell we were in last frame (ANY cell, not just walkable)
    private float _lastDistanceToUnexplored = 0f;
    private Vector2 _nearestUnexploredCellPos = Vector2.zero; //for visualization

    //event triggered before episode begins (for metrics tracking)
    public event System.Action OnEpisodeBeginEvent;

    public override void Initialize()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (!coverage) Debug.LogError("QAExplorerAgentPhase1: coverage reference not set.");

        //ensure rigidbody2D is configured correctly
        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.gravityScale = 3f;
        }
    }

    public override void OnEpisodeBegin()
    {
        //trigger event BEFORE we reset anything (allows metrics to capture final coverage)
        OnEpisodeBeginEvent?.Invoke();

        CurrentEpisode++;
        Debug.Log("Episode: " + CurrentEpisode);
        Debug.Log("cumulative reward: " + GetCumulativeReward());

        //reset coverage
        if (coverage != null)
        {
            coverage.ResetCoverage();
        }

        //reset all checkpoints in the scene
        ResetAllCheckpoints();

        //random spawn
        Vector2 spawnPos = transform.position;
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            spawnPos = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        }

        //set initial checkpoint to spawn point
        _currentCheckpoint = spawnPos;
        _maxXReached = spawnPos.x;

        //teleport to spawn at start of episodes, and zero out physics so we don't carry over momentum
        transform.position = spawnPos;
        
        //ensure rigidbody is awake and reset physics
        if (_rb != null)
        {
            _rb.WakeUp();
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        //reset tracking per-episode
        _lastPos = transform.position;
        _stuckTimer = 0f;
        EpisodeStuckSeconds = 0f;
        _foundBugs.Clear();
        _deathCount = 0;
        _lastCellIndex = -1;
        _lastDistanceToUnexplored = 0f;
        _nearestUnexploredCellPos = Vector2.zero;

        //mark starting cell (ANY cell, not just walkable)
        if (coverage != null)
        {
            var visitPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
            _lastCellIndex = coverage.GetCellIndex(visitPos);
            
            //also try to visit it if it's walkable
            coverage.TryVisitWalkable(visitPos, out _);
        }
    }

    //reset all checkpoints in the scene at episode start
    private void ResetAllCheckpoints()
    {
        var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.Reset();
        }
    }

    // Gathers observations for the neural network
    public override void CollectObservations(VectorSensor sensor)
    {
        //refresh ground state
        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        //normalised velocity 
        Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
        sensor.AddObservation(Mathf.Clamp(v.x / 10f, -1f, 1f));//obs 1
        sensor.AddObservation(Mathf.Clamp(v.y / 10f, -1f, 1f));//obs 2

        //ground check
        sensor.AddObservation(_isGrounded ? 1f : 0f);//obs 3

        //8 directional rays (obs 4-11)
        sensor.AddObservation(Ray01(Vector2.right));
        sensor.AddObservation(Ray01(Vector2.down));
        sensor.AddObservation(Ray01(Vector2.left));
        sensor.AddObservation(Ray01(Vector2.up));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.down).normalized));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.up).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.down).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.up).normalized));

        //normalised position in the level bounds (obs 12-13)
        if (coverage != null && coverage.levelBounds != null)
        {
            var bounds = coverage.levelBounds.bounds;
            float normX = 0f;
            float normY = 0f;
            if (bounds.max.x - bounds.min.x > 0.0001f)
                normX = (transform.position.x - bounds.min.x) / (bounds.max.x - bounds.min.x);
            if (bounds.max.y - bounds.min.y > 0.0001f)
                normY = (transform.position.y - bounds.min.y) / (bounds.max.y - bounds.min.y);

            sensor.AddObservation(Mathf.Clamp01(normX));
            sensor.AddObservation(Mathf.Clamp01(normY));
        }
        else
        {
            // two dummy observations if no bounds
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        //direction to unexplored WALKABLE areas
        if (coverage != null)
        {
            Vector2 directionToUnexplored = coverage.GetDirectionToUnexploredWalkable(transform.position);
            sensor.AddObservation(directionToUnexplored.normalized); //obs 14-15
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        //add observation for whether current cell is visited (obs 16)
        if (coverage != null)
        {
            var visitPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
            int cellIndex = coverage.GetCellIndex(visitPos);
            bool isVisited = cellIndex >= 0 && coverage._visited.Contains(cellIndex);
            sensor.AddObservation(isVisited ? 1f : 0f);
        }
        else
        {
            sensor.AddObservation(0f);
        }
    }

    //executes the action chosen by the neural network
    //also contains reward logic and anti-stuck logic
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_rb == null) return;

        AddReward(stepPenalty);

        int move = actions.DiscreteActions[0]; // 0 left, 1 idle, 2 right
        int jump = actions.DiscreteActions[1]; // 0 no, 1 yes

        float moveAxis = move == 0 ? -1f : (move == 2 ? 1f : 0f);
        _rb.linearVelocity = new Vector2(moveAxis * moveSpeed, _rb.linearVelocity.y);

        //jump
        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }
        
        if (jump == 1 && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        }

        //ALWAYS track which cell we're in (not just walkable cells)
        if (coverage != null)
        {
            var visitPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
            int currentCellIndex = coverage.GetCellIndex(visitPos);
            
            //check if we've entered a different cell than last frame
            if (currentCellIndex >= 0 && currentCellIndex != _lastCellIndex)
            {
                //we've entered a different cell
                //check if it's a new WALKABLE cell
                bool isNewWalkableCell = coverage.TryVisitWalkable(visitPos, out _);
                
                if (isNewWalkableCell)
                {
                    //BIG reward for discovering new WALKABLE cell
                    AddReward(newCellReward);
                    Debug.Log($"[Reward] New cell discovered! +{newCellReward}");
                }
                else if (coverage._walkableCells.Contains(currentCellIndex) && coverage._visited.Contains(currentCellIndex))
                {
                    //entering a different WALKABLE cell that we've already visited
                    AddReward(revisitCellPenalty);
                    Debug.Log($"[Penalty] Revisited walkable cell! {revisitCellPenalty}");
                }
                //if it's a non-walkable cell, we still update _lastCellIndex but don't penalize
                //this means jumping through air cells will trigger the cell change, preventing spam
            }

            //update last cell index for next frame (ALL cells, not just walkable)
            _lastCellIndex = currentCellIndex;

            //gentle guidance toward unexplored cells
            if (guideTowardUnexplored)
            {
                ApplyExplorationGuidance();
            }
        }

        //anti-stall (horizontal only so jumping in place doesn't hide stagnation)
        float dist = Mathf.Abs(transform.position.x - _lastPos.x);
        if (dist < stuckMoveThreshold * Time.fixedDeltaTime)
        {
            _stuckTimer += Time.fixedDeltaTime;
            EpisodeStuckSeconds += Time.fixedDeltaTime;

            if (_stuckTimer >= stuckTimeThreshold)
            {
                AddReward(stuckPenalty);
                Debug.Log($"[Penalty] Agent stuck for {stuckTimeThreshold} seconds! {stuckPenalty}");
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        _lastPos = transform.position;
    }

    //applies gentle reward/penalty for moving toward/away from unexplored cells
    private void ApplyExplorationGuidance()
    {
        if (coverage == null) return;

        Vector2 currentPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 directionToUnexplored = coverage.GetDirectionToUnexploredWalkable(currentPos);

        //if no unexplored cells, skip
        if (directionToUnexplored == Vector2.zero)
        {
            _nearestUnexploredCellPos = Vector2.zero;
            return;
        }

        //find the actual nearest unexplored cell position for accurate distance calculation
        _nearestUnexploredCellPos = GetNearestUnexploredCellPosition(currentPos);
        
        if (_nearestUnexploredCellPos == Vector2.zero) return;

        //calculate distance to nearest unexplored
        float currentDistance = Vector2.Distance(currentPos, _nearestUnexploredCellPos);

        //only apply guidance after first step (when we have previous distance)
        if (_lastDistanceToUnexplored > 0f)
        {
            float distanceChange = _lastDistanceToUnexplored - currentDistance;

            if (distanceChange > 0.01f) //moved closer (with small threshold to avoid noise)
            {
                float reward = approachRewardPerUnit * distanceChange;
                AddReward(reward);
                Debug.Log($"[Guidance Reward] Moved closer to unexplored cell by {distanceChange:F2} units! +{reward:F3}");
            }
            else if (distanceChange < -0.01f) //moved away
            {
                float penalty = retreatPenaltyPerUnit * Mathf.Abs(distanceChange);
                AddReward(penalty);
                Debug.Log($"[Guidance Penalty] Moved away from unexplored cell by {Mathf.Abs(distanceChange):F2} units! {penalty:F3}");
            
            }
        }

        _lastDistanceToUnexplored = currentDistance;
    }

    //finds the actual position of the nearest unexplored walkable cell
    private Vector2 GetNearestUnexploredCellPosition(Vector2 currentPos)
    {
        if (coverage == null) return Vector2.zero;

        float minDistance = float.MaxValue;
        Vector2 nearestPos = Vector2.zero;

        //iterate through all walkable cells to find nearest unexplored
        foreach (int cellIndex in coverage._walkableCells)
        {
            if (coverage._visited.Contains(cellIndex)) continue; //skip visited

            Vector2 cellCenter = coverage.CellIndexToWorldCenter(cellIndex);
            float distance = Vector2.Distance(currentPos, cellCenter);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPos = cellCenter;
            }
        }

        return nearestPos;
    }

    //called by hazards/spikes when agent takes damage
    public void Die()
    {
        _deathCount++;
        AddReward(deathPenalty);
        Debug.Log($"[Death] Agent died! {deathPenalty} penalty applied.");

        Debug.Log($"Agent died! Deaths: {_deathCount}/{maxDeathsPerEpisode}");
        
        //if too many deaths, end episode
        if (_deathCount >= maxDeathsPerEpisode)
        {
            Debug.Log($"Max deaths reached ({_deathCount}), ending episode");
            EndEpisode();
            return;
        }
        
        //otherwise, respawn at last checkpoint
        RespawnAtCheckpoint();
    }

    //respawn agent at last checkpoint
    private void RespawnAtCheckpoint()
    {
        transform.position = _currentCheckpoint;
        
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
        
        Debug.Log($"Respawned at checkpoint: {_currentCheckpoint}");
    }

    //called by checkpoint triggers when agent reaches a checkpoint
    public void SetCheckpoint(Vector2 checkpointPosition)
    {
        _currentCheckpoint = checkpointPosition;
        AddReward(checkpointReward);
        Debug.Log($"Checkpoint saved at {checkpointPosition}");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        float h = Input.GetAxisRaw("Horizontal");
        d[0] = h < 0 ? 0 : (h > 0 ? 2 : 1);
        d[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    public void FoundBug(string bugId)
    {
        if (string.IsNullOrEmpty(bugId)) bugId = "unknown_bug_id";

        //hashset ensures we only reward once per unique bug ID
        if (_foundBugs.Add(bugId))
        {
            int uniqueCount = _foundBugs.Count;
            AddReward(bugFoundReward);

            Debug.Log($"BUG FOUND: {bugId} (unique={uniqueCount}, reward={bugFoundReward})");
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log($"bug_found:{bugId}");
        }
    }

    //cast a ray and return normalised distance to hit (1 if no hit, 0 at point blank)
    private float Ray01(Vector2 dir)
    {
        var hit = Physics2D.Raycast(transform.position, dir, rayDist, groundLayer);
        if (!hit.collider) return 1f;
        return Mathf.Clamp01(hit.distance / rayDist);
    }

    //draws gizmos for ground check and rays in the editor
    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)Vector2.right * rayDist);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)Vector2.left * rayDist);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)Vector2.down * rayDist);
        
        //draw current checkpoint
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_currentCheckpoint, 0.5f);
        
        //draw nearest unexplored cell and line to it
        if (_nearestUnexploredCellPos != Vector2.zero && Application.isPlaying)
        {
            Vector2 currentPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
            
            //draw large green sphere at nearest unexplored cell
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_nearestUnexploredCellPos, 1.0f);
            Gizmos.DrawSphere(_nearestUnexploredCellPos, 0.3f);
            
            //draw line from agent to nearest unrevealed
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f); //semi-transparent green
            Gizmos.DrawLine(currentPos, _nearestUnexploredCellPos);
            
            //draw distance text in scene view
            float distance = Vector2.Distance(currentPos, _nearestUnexploredCellPos);
            Vector3 midPoint = (currentPos + _nearestUnexploredCellPos) * 0.5f;
            
#if UNITY_EDITOR
            UnityEditor.Handles.Label(midPoint, $"Distance: {distance:F1}");
#endif
        }
    }
}
