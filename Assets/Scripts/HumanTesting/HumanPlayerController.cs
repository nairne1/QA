using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
//human player controller for manual testing - mirrors agent movement
public class HumanPlayerController : MonoBehaviour
{
    [Header("Scene refs")]
    [Tooltip("Records which cells have been visited")]
    public GridCoverageTracker2D coverage;
    [Tooltip("Position of feet for ground check")]
    public Transform groundCheck;
    [Tooltip("Layer for ground check")]
    public LayerMask groundLayer;

    [Header("Movement - matches agent settings")]
    public float moveSpeed = 6f;
    public float jumpImpulse = 10f;
    public float groundCheckRadius = 0.12f;

    [Header("Respawn")]
    public Transform initialSpawnPoint;

    private Rigidbody2D _rb;
    private bool _isGrounded;
    private Vector2 _currentCheckpoint;
    private Vector2 _initialPosition;

    [Header("Debug Info (for developer only, not shown to tester)")]
    [SerializeField] private int _deathCount = 0;
    [SerializeField] private float _playTime = 0f;
    [SerializeField] private int _score = 0;

    //event triggered when player respawns (for duplication collectible bug)
    public static event System.Action OnHumanPlayerRespawn;

    //initial setup
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _initialPosition = initialSpawnPoint != null ? initialSpawnPoint.position : (Vector2)transform.position;
        _currentCheckpoint = _initialPosition;

        //ensure player tag is set
        if (!gameObject.CompareTag("Player"))
        {
            Debug.LogWarning("HumanPlayerController: GameObject should have 'Player' tag!");
            gameObject.tag = "Player";
        }
    }

    private void Start()
    {
        //initialize coverage if assigned
        if (coverage != null)
        {
            coverage.ResetCoverage();
        }
    }

    private void Update()
    {
        //only update play time when not paused
        if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            _playTime += Time.deltaTime;
        }

        HandleInput();
    }

    private void FixedUpdate()
    {
        //track coverage
        if (coverage != null)
        {
            coverage.TryVisitWalkable((Vector2)transform.position, out _);
        }
    }

    private void HandleInput()
    {
        if (GameManager.Instance.IsBugReportOpen) return;//cant move while bug report panel is open

        //pause toggle (ESC or P)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TogglePause();
            }
            return;
        }

        //don't allow movement or actions while paused
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            return;
        }

        //ground check
        if(groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        //horizontal movement
        float h = Input.GetAxisRaw("Horizontal");
        _rb.linearVelocity = new Vector2(h * moveSpeed, _rb.linearVelocity.y);

        //jump
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        }

        //debug: manual reset to checkpoint
        if (Input.GetKeyDown(KeyCode.R))
        {
            Respawn();
        }
    }

    //called by working collectibles to add score
    public void AddScore(int points)
    {
        _score += points;
    }

    //called by hazards, pits, etc to kill player
    public void Kill()
    {
        _deathCount++;
        Respawn();
    }

    //respawn player at current checkpoint
    public void Respawn()
    {
        transform.position = _currentCheckpoint;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        //trigger respawn event for collectibles
        OnHumanPlayerRespawn?.Invoke();
    }

    //reset player to initial state for new session
    public void ResetPlayer()
    {
        //reset stats
        _deathCount = 0;
        _playTime = 0f;
        _score = 0;

        //reset position to initial spawn
        _currentCheckpoint = _initialPosition;
        transform.position = _initialPosition;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        //reset coverage
        if (coverage != null)
        {
            coverage.ResetCoverage();
        }
    }

    //called by checkpoint triggers
    public void SetCheckpoint(Vector2 checkpointPosition)
    {
        _currentCheckpoint = checkpointPosition;
    }


    //optional: visualize in scene view (only visible in editor)
    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        //draw checkpoint
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_currentCheckpoint, 0.5f);
    }

    //public getters for UI/metrics (for HumanPlayerUI)
    public int DeathCount => _deathCount;
    public float PlayTime => _playTime;
    public int Score => _score;
    public GridCoverageTracker2D Coverage => coverage;
}