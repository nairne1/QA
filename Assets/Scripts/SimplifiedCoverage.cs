using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimplifiedCoverage : Agent
{
    [Header("Scene refs")]
    public GridCoverageTracker2D coverage;
    public Transform groundCheck;
    public LayerMask groundLayer;
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

    [Header("Penalties")]
    public float stepPenalty = -0.001f;
    public float deathPenalty = -1.0f;
    public float stillPenalty = -0.05f;
    public float sameCellJumpLoopPenalty = -0.02f;

    [Header("Stuck detection")]
    public float stillTimeThreshold = 2.0f;
    public float stillMoveThreshold = 0.05f;

    private Rigidbody2D _rb;
    private bool _isGrounded;

    private Vector2 _lastPos;
    private float _stillTimer;

    private int _lastWalkableCellIndex = -1;
    private bool _wasInNonWalkable = false;

    private float _lastDistanceToUnexplored = -1f;

    public override void Initialize()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (coverage == null)
        {
            Debug.LogError("QAExplorerAgentPhase1: coverage reference not set.");
        }
    }

    public override void OnEpisodeBegin()
    {
        if (coverage != null)
        {
            coverage.ResetCoverage();
        }

        Vector2 spawnPos = transform.position;
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            spawnPos = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        }

        transform.position = spawnPos;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        _lastPos = transform.position;
        _stillTimer = 0f;
        _lastWalkableCellIndex = -1;
        _wasInNonWalkable = false;
        _lastDistanceToUnexplored = -1f;

        MarkCurrentWalkableCell();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        UpdateGrounded();

        Vector2 velocity = _rb != null ? _rb.linearVelocity : Vector2.zero;
        sensor.AddObservation(Mathf.Clamp(velocity.x / 10f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(velocity.y / 10f, -1f, 1f));
        sensor.AddObservation(_isGrounded ? 1f : 0f);

        sensor.AddObservation(Ray01(Vector2.right));
        sensor.AddObservation(Ray01(Vector2.left));
        sensor.AddObservation(Ray01(Vector2.up));
        sensor.AddObservation(Ray01(Vector2.down));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.up).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.up).normalized));
        sensor.AddObservation(Ray01((Vector2.right + Vector2.down).normalized));
        sensor.AddObservation(Ray01((Vector2.left + Vector2.down).normalized));

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

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_rb == null || coverage == null)
            return;

        AddReward(stepPenalty);

        UpdateGrounded();

        int move = actions.DiscreteActions[0]; // 0 left, 1 idle, 2 right
        float moveAxis = move == 0 ? -1f : (move == 2 ? 1f : 0f);

        _rb.linearVelocity = new Vector2(moveAxis * moveSpeed, _rb.linearVelocity.y);

        bool shouldJump = ShouldJumpInMoveDirection(moveAxis);
        if (shouldJump && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        }

        HandleCoverageAndRewards();
        HandleStillPenalty();
    }

    private void HandleCoverageAndRewards()
    {
        Vector2 visitPos = GetVisitPosition();
        int rawCellIndex = coverage.GetCellIndex(visitPos);
        bool isWalkable = rawCellIndex >= 0 && coverage._walkableCells.Contains(rawCellIndex);

        if (!isWalkable)
        {
            _wasInNonWalkable = true;
            return;
        }

        bool isNewCell = coverage.TryVisitWalkable(visitPos, out int walkableCellIndex);

        if (isNewCell)
        {
            AddReward(newCellReward);
        }
        else
        {
            if (_wasInNonWalkable && walkableCellIndex == _lastWalkableCellIndex)
            {
                AddReward(sameCellJumpLoopPenalty);
            }
        }

        ApplyExplorationGuidance(visitPos);

        _lastWalkableCellIndex = walkableCellIndex;
        _wasInNonWalkable = false;
    }

    private void ApplyExplorationGuidance(Vector2 currentPos)
    {
        Vector2 nearestUnexplored = GetNearestUnexploredCellPosition(currentPos);
        if (nearestUnexplored == Vector2.zero)
        {
            _lastDistanceToUnexplored = -1f;
            return;
        }

        float currentDistance = Vector2.Distance(currentPos, nearestUnexplored);

        if (_lastDistanceToUnexplored >= 0f)
        {
            float delta = _lastDistanceToUnexplored - currentDistance;

            if (delta > 0.001f)
            {
                AddReward(delta * approachRewardPerUnit);
            }
            else if (delta < -0.001f)
            {
                AddReward(Mathf.Abs(delta) * retreatPenaltyPerUnit);
            }
        }

        _lastDistanceToUnexplored = currentDistance;
    }

    private Vector2 GetNearestUnexploredCellPosition(Vector2 currentPos)
    {
        float minDistance = float.MaxValue;
        Vector2 nearest = Vector2.zero;

        foreach (int cellIndex in coverage._walkableCells)
        {
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

    private void HandleStillPenalty()
    {
        float dist = Vector2.Distance(transform.position, _lastPos);

        if (dist < stillMoveThreshold)
        {
            _stillTimer += Time.fixedDeltaTime;

            if (_stillTimer >= stillTimeThreshold)
            {
                AddReward(stillPenalty);
                _stillTimer = 0f;
            }
        }
        else
        {
            _stillTimer = 0f;
        }

        _lastPos = transform.position;
    }

    private bool ShouldJumpInMoveDirection(float moveAxis)
    {
        if (Mathf.Approximately(moveAxis, 0f))
            return false;

        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 forward = moveAxis > 0f ? Vector2.right : Vector2.left;

        Vector2 frontCheckOrigin = origin + forward * 0.75f;
        bool groundAhead = Physics2D.Raycast(frontCheckOrigin, Vector2.down, 1.5f, groundLayer);

        return !groundAhead;
    }

    private void MarkCurrentWalkableCell()
    {
        if (coverage == null)
            return;

        Vector2 visitPos = GetVisitPosition();
        coverage.TryVisitWalkable(visitPos, out int startCellIndex);
        _lastWalkableCellIndex = startCellIndex;
    }

    private Vector2 GetVisitPosition()
    {
        return groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
    }

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
    }

    public void Die()
    {
        AddReward(deathPenalty);
        EndEpisode();
    }

    private float Ray01(Vector2 dir)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, rayDist, groundLayer);
        if (!hit.collider) return 1f;
        return Mathf.Clamp01(hit.distance / rayDist);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        float h = Input.GetAxisRaw("Horizontal");

        d[0] = h < 0 ? 0 : (h > 0 ? 2 : 1);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * rayDist);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.left * rayDist);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * rayDist);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDist);
    }
}