using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
//this agent is designed for a simplified coverage task in a 2D platformer environment
//this agent learns to navigate the environment and maximize coverage while avoiding hazards and finding bugs
//uses reinforcement learning to learn an effective exploration strategy, with rewards for discovering new areas and penalties for dying or being idle
public class SimplifiedCoverage : Agent
{
    [HideInInspector] public int CurrentEpisode = 0;

    [Header("Scene refs")]
    [Tooltip("Reference to the grid coverage tracker for tracking explored areas.")]
    public GridCoverageTracker2D coverage;
    public Transform groundCheck;
    public Transform platformCheck;
    public LayerMask groundLayer;
    public LayerMask platformLayer;
    public List<Transform> spawnPoints;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpImpulse = 10f;
    public float groundCheckRadius = 0.12f;

    [Header("Ray settings")]
    public float rayDist = 2f;

    [Header("Rewards")]
    public float newCellReward = 1.0f;
    public float approachRewardPerUnit = 0.05f;
    public float retreatPenaltyPerUnit = -0.05f;

    [Header("Bug Rewards")]
    public float bugFoundReward = 1.5f;
    [Tooltip("Small reward for reaching checkpoints")]
    public float checkpointReward = 0.5f;

    [Header("Penalties")]
    public float stepPenalty = -0.001f;
    public float deathPenalty = -1.0f;
    public float stillPenalty = -0.05f;
    public float sameCellJumpLoopPenalty = -0.02f;

    [Header("Hazard Detection")]
    [SerializeField] private LayerMask hazardLayer;
    [SerializeField] private float hazardCheckForward = 1.0f;

    private HashSet<string> _testedHazards = new HashSet<string>();

    [Header("Stuck detection")]
    public float stillTimeThreshold = 2.0f;
    public float stillMoveThreshold = 0.05f;

    [Tooltip("Prevents duplicate bug reward")]
    private HashSet<string> _foundBugs = new HashSet<string>();

    [Tooltip("Chance for the agent to attempt a jump up to a platform.")]
    [SerializeField] private float jumpChance = 0.5f;

    //for physics based movement
    private Rigidbody2D _rb;

    //grounded checks
    private bool _isGrounded;
    private bool _isGroundedPlatform;

    //death tracking
    private int _deathCount;
    [SerializeField] private int maxDeathsPerEpisode = 25;

    //stuck detection
    private Vector2 _lastPos;
    private float _stillTimer;
    private float _lastDistanceToUnexplored = -1f;

    //checkpoint tracking
    private Vector2 _currentCheckpoint;

    //walkable cell tracking for jump loop detection
    private int _lastWalkableCellIndex = -1;
    private bool _wasInNonWalkable = false;

    //checkpoint bug validation
    private Vector2 _expectedCheckpoint;
    private bool _hasExpectedCheckpoint = false;
    private bool _expectedCheckpointWasBugged = false;
    private string _expectedCheckpointBugId = "";
    private float checkpointValidationTolerance = 0.25f;

    //event for when agent respawns at checkpoint (for duplication bugs)
    public static event System.Action OnAgentRespawn;

    //lock to prevent multiple jump up attempts in the same frame
    private bool _jumpUpAttemptLocked = false;

    //public getters for debugging and logging
    public float IntendedMoveDirection { get; private set; }
    public int DeathCount => _deathCount;
    public int BugCount => _foundBugs.Count;
    public float CoveragePercent => coverage != null ? coverage.GetCoverage01() * 100f : 0f;

    //initial setup
    public override void Initialize()
    {
        //configure Rigidbody2D for better physics interactions
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        //validate coverage reference
        if (coverage == null)
        {
            Debug.LogError("[SimplifiedCoverage]: coverage reference not set.");
        }
    }

    //reset agent state at the beginning of each episode
    public override void OnEpisodeBegin()
    {
        //increment episode count
        CurrentEpisode++;

        //reset coverage tracker for new episode
        if (coverage != null)
        {
            coverage.ResetCoverage();
        }

        //reset all checkpoints in the scene
        ResetAllCheckpoints();

        //reset all collectibles at episode start
        ResetAllCollectibles();

        //spawn at random spawn point or default position
        Vector2 spawnPos = transform.position;
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            spawnPos = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        }

        //respawn at initial position
        transform.position = spawnPos;
        //set initial checkpoint to spawn point
        _currentCheckpoint = spawnPos;

        //rset velocity
        if (_rb != null)
        {
            _rb.WakeUp();
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        //reset tracking variables
        _deathCount = 0;
        _foundBugs.Clear();
        _lastPos = transform.position;
        _stillTimer = 0f;
        _lastWalkableCellIndex = -1;
        _wasInNonWalkable = false;
        _lastDistanceToUnexplored = -1f;
        _testedHazards.Clear();
        _expectedCheckpoint = spawnPos;
        _hasExpectedCheckpoint = false;
        _expectedCheckpointWasBugged = false;
        _expectedCheckpointBugId = "";

        //reset intended move direction for logging
        IntendedMoveDirection = 0f;

        //mark the current cell as visited to prevent immediate jump loop penalty
        MarkCurrentWalkableCell();
    }

    //reset all checkpoints in the scene at episode start
    public void ResetAllCheckpoints()
    {
        //find all checkpoints in the scene and call their Reset method to ensure they are active and ready for the new episode
        var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.Reset();
        }
    }

    //reset all collectibles in the scene at episode start
    private void ResetAllCollectibles()
    {
        var collectibles = FindObjectsByType<CollectibleBug>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var collectible in collectibles)
        {
            collectible.ResetCollectible();
        }
    }

    //collect observations for the agent's state, including velocity, grounded status, raycasts, and direction to unexplored areas
    public override void CollectObservations(VectorSensor sensor)
    {
        UpdateGrounded();

        //add velocity observations 
        Vector2 velocity = _rb != null ? _rb.linearVelocity : Vector2.zero;
        sensor.AddObservation(Mathf.Clamp(velocity.x / 10f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(velocity.y / 10f, -1f, 1f));
        //add grounded status
        sensor.AddObservation(_isGrounded ? 1f : 0f);

        //add raycast observations in 8 directions
        sensor.AddObservation(Ray01(Vector2.right));
        sensor.AddObservation(Ray01(Vector2.left));
        sensor.AddObservation(Ray01(Vector2.up));
        sensor.AddObservation(Ray01(Vector2.down));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.up).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.up).normalized));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.down).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.down).normalized));

        //add direction to nearest unexplored walkable cell
        if (coverage != null)
        {
            Vector2 dir = coverage.GetDirectionToUnexploredWalkable(GetVisitPosition());
            sensor.AddObservation(dir.x);
            sensor.AddObservation(dir.y);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    //receive actions from the policy and apply movement, jumping, and rewards based on coverage and hazards
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_rb == null || coverage == null)
            return;

        //apply small step penalty to encourage efficiency
        AddReward(stepPenalty);

        //update grounded status at the start of the action to inform movement decisions
        UpdateGrounded();

        //interpret discrete action for movement: 0 = left, 1 = idle, 2 = right
        int move = actions.DiscreteActions[0];
        float moveAxis = move == 0 ? -1f : (move == 2 ? 1f : 0f);

        //store intended move direction for logging and debugging purposes
        IntendedMoveDirection = moveAxis;

        //apply horizontal movement
        _rb.linearVelocity = new Vector2(moveAxis * moveSpeed, _rb.linearVelocity.y);

        //determine if we should attempt a jump in the move direction (for gaps) or up direction (for platforms)
        bool shouldJump = ShouldJumpInMoveDirection(moveAxis);

        Hazard hazardAhead = GetHazardAhead(moveAxis);
        if (hazardAhead != null)
        {
            if (ShouldTestHazard(hazardAhead))
            {
                //first time: run into it to test it
                shouldJump = false;
            }
            else
            {
                //already tested: jump over it
                shouldJump = true;
            }
        }

        //only attempt jump if we are grounded and trying to move towards a gap, or if we are trying to jump up onto a platform and are grounded on either ground or platform
        if (shouldJump && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        }

        bool shouldJumpUp = ShouldJumpInUpDirection(moveAxis);

        //lock jump up attempts to one per frame to prevent multiple jumps in the same frame
        if (!shouldJumpUp)
        {
            _jumpUpAttemptLocked = false;
        }

        if (shouldJumpUp && (_isGroundedPlatform || _isGrounded) && !_jumpUpAttemptLocked)
        {
            _jumpUpAttemptLocked = true;

            if (Random.value < jumpChance)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
                _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
            }
        }

        //handle rewards and penalties related to coverage, exploration, and being stuck after applying movement
        HandleCoverageAndRewards();
        HandleStillPenalty();
    }

    //handles coverage tracking and rewards
    //rewards new cells, penalises jump spam, applies exploration guidance 
    private void HandleCoverageAndRewards()
    { 
        Vector2 visitPos = GetVisitPosition();
        int rawCellIndex = coverage.GetCellIndex(visitPos);
        bool isWalkable = rawCellIndex >= 0 && coverage._walkableCells.Contains(rawCellIndex);

        //skip if cell isnt walkable 
        if (!isWalkable)
        {
            _wasInNonWalkable = true;
            return;
        }

        //try to visit current walkable cell
        bool isNewCell = coverage.TryVisitWalkable(visitPos, out int walkableCellIndex);

        if (isNewCell)
        {
            //rewar for discovering new cell
            AddReward(newCellReward);
        }
        else
        {
            //penalise jumping in the same cell repeatedly after being in non-walkable cell
            if (_wasInNonWalkable && walkableCellIndex == _lastWalkableCellIndex)
            {
                AddReward(sameCellJumpLoopPenalty);
            }
        }

        //apply rewards/penalties for moving towaqrd/away from unexplores areas
        ApplyExplorationGuidance(visitPos);

        _lastWalkableCellIndex = walkableCellIndex;
        _wasInNonWalkable = false;
    }

    private void ApplyExplorationGuidance(Vector2 currentPos)
    {
        Vector2 nearestUnexplored = GetNearestUnexploredCellPosition(currentPos);

        //no unexplored cells remaining
        if (nearestUnexplored == Vector2.zero)
        {
            _lastDistanceToUnexplored = -1f;
            return;
        }

        float currentDistance = Vector2.Distance(currentPos, nearestUnexplored);

        //only apply guidance if we have previous distance to compare
        if (_lastDistanceToUnexplored >= 0f)
        {
            float delta = _lastDistanceToUnexplored - currentDistance;

            if (delta > 0.001f)
            {
                //moving closer = reward
                AddReward(delta * approachRewardPerUnit);
            }
            else if (delta < -0.001f)
            {
                //mocing away = penalty
                AddReward(Mathf.Abs(delta) * retreatPenaltyPerUnit);
            }
        }

        _lastDistanceToUnexplored = currentDistance;
    }

    //finds the nearest unexplored walkable cell position
    private Vector2 GetNearestUnexploredCellPosition(Vector2 currentPos)
    {
        float minDistance = float.MaxValue;
        Vector2 nearest = Vector2.zero;

        foreach (int cellIndex in coverage._walkableCells)
        {
            //skip visited cells
            if (coverage._visited.Contains(cellIndex))
                continue;

            Vector2 cellCenter = coverage.CellIndexToWorldCenter(cellIndex);
            float distance = Vector2.Distance(currentPos, cellCenter);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = cellCenter;
            }
        }

        return nearest;
    }

    //handles stillnedd detectionpenalty
    private void HandleStillPenalty()
    {
        float dist = Vector2.Distance(transform.position, _lastPos);

        if (dist < stillMoveThreshold)
        {
            //agent is still - increment timer
            _stillTimer += Time.fixedDeltaTime;

            //apply penalty if still for too long
            if (_stillTimer >= stillTimeThreshold)
            {
                AddReward(stillPenalty);
                _stillTimer = 0f;
            }
        }
        else
        {
            //agent is mocing - reset timer
            _stillTimer = 0f;
        }

        _lastPos = transform.position;
    }

    //determines if the agent should jump forward to clear a gap
    private bool ShouldJumpInMoveDirection(float moveAxis)
    {
        //not moving, dont jump
        if (Mathf.Approximately(moveAxis, 0f))
            return false;

        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 forward = moveAxis > 0f ? Vector2.right : Vector2.left;

        //check for ground ahead and below
        Vector2 frontCheckOrigin = origin + forward * 0.75f;
        bool groundAhead = Physics2D.Raycast(frontCheckOrigin, Vector2.down, 1.5f, groundLayer);

        //no ground ahead - should jump to clear the gap
        return !groundAhead;
    }

    //determines if the agent should jump upward to platforms
    private bool ShouldJumpInUpDirection(float moveAxis)
    {
        //not moving
        if (Mathf.Approximately(moveAxis, 0f))
            return false;

        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 forward = moveAxis > 0f ? Vector2.right : Vector2.left;

        //check for platform ahead and above
        Vector2 checkOrigin = origin + forward * 3.0f + Vector2.up * 2.0f;
        RaycastHit2D hitAhead = Physics2D.Raycast(checkOrigin, Vector2.down, 2.5f, platformLayer);

        //no platform detected ahead
        if (!hitAhead.collider)
            return false;

        //platform must be meaningfully above player
        float minHeightDifference = 0.75f;
        bool isActuallyAboveAhead = hitAhead.point.y > origin.y + minHeightDifference;

        if (!isActuallyAboveAhead)
            return false;

        //check if platform is directly above us
        Vector2 directAboveOrigin = origin + Vector2.up * 0.1f;
        RaycastHit2D hitDirectAbove = Physics2D.Raycast(directAboveOrigin, Vector2.up, 2.0f, platformLayer);

        bool platformDirectlyAbove = hitDirectAbove.collider != null &&
                                     hitDirectAbove.point.y > origin.y + minHeightDifference;

        //dont jump is it's directly overhead, as cant jump through it to land on it (also prevent jump spamming)
        if (platformDirectlyAbove)
            return false;

        return true;
    }

    //marks the current walkable cell as visited
    private void MarkCurrentWalkableCell()
    {
        if (coverage == null)
            return;

        Vector2 visitPos = GetVisitPosition();
        coverage.TryVisitWalkable(visitPos, out int startCellIndex);
        _lastWalkableCellIndex = startCellIndex;
    }

    //gets the position used for cell visiting (ground check if avalilable, otherwise center)
    private Vector2 GetVisitPosition()
    {
        return groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
    }

    //updates the grounded state for ground and platform
    private void UpdateGrounded()
    {
        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }
        else
        {
            _isGrounded = false;
        }

        if (platformCheck != null)
        {
            _isGroundedPlatform = Physics2D.OverlapCircle(platformCheck.position, groundCheckRadius, platformLayer);
        }
        else
        {
            _isGroundedPlatform = false;
        }
    }

    //called when the player dies
    public void Die()
    {
        //increment
        _deathCount++;
        //penalise
        AddReward(deathPenalty);
        
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

    //respawns the agent at the current checkpoint
    public void RespawnAtCheckpoint()
    {
        transform.position = _currentCheckpoint;

        if (_rb != null)
        {
            _rb.WakeUp();
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        //check whether the last checkpoint crossed was bugged and failed to save properly
        if (_hasExpectedCheckpoint && _expectedCheckpointWasBugged)
        {
            float distance = Vector2.Distance((Vector2)transform.position, _expectedCheckpoint);

            //if respawn position doesnt match expected, log checkpoint as a bug
            if (distance > checkpointValidationTolerance)
            {
                string foundBugId = $"checkpoint:{_expectedCheckpointBugId}";

                FoundBug(foundBugId);
            }
        }

        //reset state tracking
        _lastPos = transform.position;
        _stillTimer = 0f;
        _lastDistanceToUnexplored = -1f;

        MarkCurrentWalkableCell();

        //trigger event for duplication bugs
        OnAgentRespawn?.Invoke();
    }

    //performs raycasts in the specified direction
    private float Ray01(Vector2 dir)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, rayDist, groundLayer);
        if (!hit.collider) return 1f;
        return Mathf.Clamp01(hit.distance / rayDist);
    }

    //manual controls for testing
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        float h = Input.GetAxisRaw("Horizontal");

        //map horizontal input to discrete actions (0 = left, 1 = idle, 2 = right)
        d[0] = h < 0 ? 0 : (h > 0 ? 2 : 1);
    }

    //called when a bug is detected
    public void FoundBug(string bugId)
    {
        if (string.IsNullOrEmpty(bugId)) bugId = "unknown_bug_id";

        //hashset ensures we only reward once per unique bug ID
        if (_foundBugs.Add(bugId))
        {
            int uniqueCount = _foundBugs.Count;
            AddReward(bugFoundReward);

            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log($"bug_found:{bugId}");
        }
    }

    //called by checkpoint triggers when agent reaches a checkpoint
    public void SetCheckpoint(Vector2 checkpointPosition)
    {
        _currentCheckpoint = checkpointPosition;
        AddReward(checkpointReward);
    }

    //called by checkpoint triggers to register the expected checkpoint position
    public void RegisterExpectedCheckpoint(Vector2 checkpointPosition, bool isBugged, string bugId)
    {
        _expectedCheckpoint = checkpointPosition;
        _hasExpectedCheckpoint = true;
        _expectedCheckpointWasBugged = isBugged;
        _expectedCheckpointBugId = bugId;

    }

    //marks hazards as tested so the agent will then jump overf them
    public void RegisterHazardTest(string hazardId)
    {
        if (!string.IsNullOrEmpty(hazardId))
        {
            _testedHazards.Add(hazardId);
        }
    }

    //checks for hazards ahead
    private Hazard GetHazardAhead(float moveAxis)
    {
        //if still, no hazard ahead
        if (Mathf.Approximately(moveAxis, 0f))
        {
            return null;
        }

        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 forward = moveAxis > 0f ? Vector2.right : Vector2.left;

        //forward ray
        RaycastHit2D forwardHit = Physics2D.Raycast(origin, forward, hazardCheckForward, hazardLayer);

        //if hazard hit
        if (forwardHit.collider != null)
        {
            Hazard forwardHazard = forwardHit.collider.GetComponent<Hazard>();
            if (forwardHazard != null)
                return forwardHazard;
        }
        return null;
    }

    //determines if we've already tested a hazard
    private bool ShouldTestHazard(Hazard hazard)
    {
        if (hazard == null)
            return false;

        return !_testedHazards.Contains(hazard.bugId);
    }

    //visualisation of rays 
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Vector2 origin = groundCheck.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

            // Gap check
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + Vector2.right * 0.75f, origin + Vector2.right * 0.75f + Vector2.down * 1.5f);
            Gizmos.DrawLine(origin + Vector2.left * 0.75f, origin + Vector2.left * 0.75f + Vector2.down * 1.5f);

            // Wall check
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin + Vector2.up * 0.5f, origin + Vector2.up * 0.5f + Vector2.right * 0.8f);
            Gizmos.DrawLine(origin + Vector2.up * 0.5f, origin + Vector2.up * 0.5f + Vector2.left * 0.8f);

            // Landing-above check
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(origin + Vector2.right * 1.2f + Vector2.up * 2.0f,
                            origin + Vector2.right * 1.2f + Vector2.up * 2.0f + Vector2.down * 2.5f);
            Gizmos.DrawLine(origin + Vector2.left * 1.2f + Vector2.up * 2.0f,
                            origin + Vector2.left * 1.2f + Vector2.up * 2.0f + Vector2.down * 2.5f);

            //draw current checkpoint
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_currentCheckpoint, 0.5f);
        }
    }
}