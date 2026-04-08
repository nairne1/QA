    using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BoxCollider2D))]
public class SoftLockPit : MonoBehaviour
{
    [Header("Softlock Configuration")]
    [SerializeField] private bool hasSoftlockBug = false; //toggle
    [SerializeField] private string bugId = "BUG_SOFTLOCK_PIT_01";

    [Header("Detection Settings")]
    [Tooltip("Time before checking if player is stuck")]
    [SerializeField] private float detectionDelay = 2f;
    [Tooltip("How often to check if player can escape")]
    [SerializeField] private float escapeCheckInterval = 1f;
    [Tooltip("How high we check if player can jump out")]
    [SerializeField] private float jumpCheckHeight = 3f;
    [Tooltip("What counts as ground to escape to")]
    [SerializeField] private LayerMask groundLayer;

    private BoxCollider2D _pitCollider;
    private Transform _trappedPlayer = null;
    private bool _softlockDetected = false;
    private Vector3 _playerEntryPosition;
    private float _timeInPit = 0f;   
    private bool _isAIAgent = false;

    private void Awake()
    {
        _pitCollider = GetComponent<BoxCollider2D>();
        
        
        _pitCollider.isTrigger = true;

        gameObject.tag = hasSoftlockBug ? "SoftlockPit" : "Hazard";
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

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

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (other.transform == _trappedPlayer)
        {
            _trappedPlayer = null;
            StopAllCoroutines();
        }
    }

    private void HandleAgent(QAExplorerAgentPhase1 agent)
    {
        if (hasSoftlockBug)
        {
            //bugged pit: trap the player instead of killing them
            _trappedPlayer = agent.transform;
            _playerEntryPosition = agent.transform.position;
            _timeInPit = 0f;
            _softlockDetected = false;
            _isAIAgent = true;

            //check if player is stuck
            StartCoroutine(MonitorSoftlock());
        }
        else
        {
            //normal pit - kill agent
            agent.Die();
        }
    }

    private void HandleHuman(HumanPlayerController human)
    {
        if (hasSoftlockBug)
        {
            //BUG: Pit doesn't kill human, but they can't escape
            //Human notices: "I'm stuck in this pit and can't jump out"
            //They must press R to respawn manually
            
            _trappedPlayer = human.transform;
            _playerEntryPosition = human.transform.position;
            _timeInPit = 0f;
            _softlockDetected = false;
            _isAIAgent = false;

            //no monitoring needed for human - they experience the bug naturally
        }
        else
        {
            //normal pit - kill human
            human.Kill();
        }
    }

    //check if the player is still stuck in the pit and can't escape (AI only)
    private IEnumerator MonitorSoftlock()
    {
        //wait before starting detection (give player a chance to escape)
        yield return new WaitForSeconds(detectionDelay);

        while (_trappedPlayer != null && !_softlockDetected && _isAIAgent)
        {
            _timeInPit += escapeCheckInterval;

            //check if player can escape
            bool canEscape = CanPlayerEscape(_trappedPlayer.position);

            if (!canEscape)
            {
                //check if player has been stuck for a while and hasn't moved much
                float distanceMoved = Vector2.Distance(_trappedPlayer.position, _playerEntryPosition);
                
                if (distanceMoved < 2f && _timeInPit >= detectionDelay * 2)
                {
                    //player is stuck - softlock detected
                    _softlockDetected = true;

                    var agent = _trappedPlayer.GetComponent<QAExplorerAgentPhase1>();
                    if (agent != null)
                    {
                        agent.FoundBug($"softlock_pit:{bugId}");
                        if (SimpleRunLogger.Instance)
                            SimpleRunLogger.Instance.Log($"bug_found:softlock_pit:{bugId}");

                        //end episode for AI
                        agent.Die();
                    }

                    yield break;
                }
            }

            yield return new WaitForSeconds(escapeCheckInterval);
        }
    }

    private bool CanPlayerEscape(Vector3 playerPos)
    {
        //cast rays upward to check if there's reachable ground above
        RaycastHit2D leftRay = Physics2D.Raycast(
            playerPos + Vector3.left * 0.5f,
            Vector2.up,
            jumpCheckHeight,
            groundLayer
        );

        RaycastHit2D rightRay = Physics2D.Raycast(
            playerPos + Vector3.right * 0.5f,
            Vector2.up,
            jumpCheckHeight,
            groundLayer
        );

        RaycastHit2D centerRay = Physics2D.Raycast(
            playerPos,
            Vector2.up,
            jumpCheckHeight,
            groundLayer
        );

        //if any ray hits ground within jump height, player can potentially escape
        bool hasGroundAbove = leftRay.collider != null || rightRay.collider != null || centerRay.collider != null;

        //check horizontal escapes
        RaycastHit2D leftHorizontal = Physics2D.Raycast(
            playerPos,
            Vector2.left,
            2f,
            groundLayer
        );

        RaycastHit2D rightHorizontal = Physics2D.Raycast(
            playerPos,
            Vector2.right,
            2f,
            groundLayer
        );

        //if there's a wall nearby but no ground above, player is trapped
        bool hasWallsAround = leftHorizontal.collider != null && rightHorizontal.collider != null;

        if (hasWallsAround && !hasGroundAbove)
        {
            return false; //definitely trapped
        }

        //check if player has enough vertical space to jump
        if (!hasGroundAbove)
        {
            return false; //can't reach any ground by jumping
        }

        return true; //player can potentially escape
    }

    private void OnDrawGizmosSelected()
    {
        if (_pitCollider == null) _pitCollider = GetComponent<BoxCollider2D>();

        //draw the pit area
        Gizmos.color = hasSoftlockBug ? Color.red : Color.magenta;
        Gizmos.DrawWireCube(transform.position, _pitCollider.size);

        //draw jump height check
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Gizmos.DrawLine(center, center + Vector3.up * jumpCheckHeight);
        Gizmos.DrawLine(center + Vector3.left * 0.5f, center + Vector3.left * 0.5f + Vector3.up * jumpCheckHeight);
        Gizmos.DrawLine(center + Vector3.right * 0.5f, center + Vector3.right * 0.5f + Vector3.up * jumpCheckHeight);

        //draw horizontal check
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center, center + Vector3.left * 2f);
        Gizmos.DrawLine(center, center + Vector3.right * 2f);
    }
}
