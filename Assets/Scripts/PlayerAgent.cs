using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

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

    //called when the agent is first created 
    public override void Initialize()
    {
        //retreives the renderer component attached to the agent 
        _renderer = GetComponent<Renderer>();
        _rb = GetComponent<Rigidbody2D>();
        CurrentEpisode = 0;
        CumulativeReward = 0f;

        downwardHit = Physics2D.Raycast(transform.position, Vector2.down, _downRayDist, _isGround);
        forwardHit = Physics2D.Raycast(transform.position, Vector2.right, _forRayDist, _isHazard);
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

        _isGrounded = Physics2D.Raycast(transform.position, Vector2.down, _downRayDist, _isGround);
    }

    //reset environment on each restart
    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode: " + CurrentEpisode);
        Debug.Log("cumulative reward: " + CumulativeReward);

        //reset checkpoints
        Checkpoint.activated = false;
        RespawnManager.Instance.SetCheckpoint(_spawnPosition.position);

        //reset
        transform.position = _spawnPosition.position;
        CurrentEpisode++;
        CumulativeReward = 0f;
        _renderer.material.color = Color.blue;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //the goals postition - keep between -1, 1, ensure the inputs are within a similar range 
        float goalPosx = _goal.position.x / 5f;

        //the agents position
        float agentPosx = transform.position.x / 5f;

        //sensor is the container for the observations we want the agent to know 
        //Vector Observation - space size, in the behaviour parameters, is how ever many of these floats we pass in
        sensor.AddObservation(goalPosx/5f);//1
        sensor.AddObservation(agentPosx/5f);//2

        //forward ray detect hazard
        bool hazardAhead = forwardHit.collider != null;

        //downward ray detect hazard
        bool groundBelow = downwardHit.collider != null;

        sensor.AddObservation(hazardAhead ? 1f : 0f);//3
        sensor.AddObservation(groundBelow ? 1f : 0f);//4

        //velocity of agent
        sensor.AddObservation(_velocityX / 10f);//5

        //goal direction sign: -1, 0, or 1
        sensor.AddObservation(Mathf.Sign(_goal.position.x - transform.position.x));//6
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

        float dir = Mathf.Sign(_goal.position.x - transform.position.x); // +1 = right, -1 = left
        float velocity = _rb.linearVelocity.x;// normalize velocity

        // Reward movement toward the goal
        AddReward(0.003f * dir * velocity);

        //AddReward(-0.001f);//light step penalty

        //forward ray detect hazard
        bool hazardAhead = forwardHit.collider != null;

        //downward ray detect hazard
        bool groundBelow = downwardHit.collider != null;

        // reward for jumping when hazard ahead
        if (hazardAhead && actions.DiscreteActions[1] == 1)
        {
            AddReward(0.2f);
        }

        //larger reward deduction for jumping with no hazard
        if (!hazardAhead && actions.DiscreteActions[1] == 1)
        {
            AddReward(-0.05f);
        }

        //tiny minus reward for moving left 
        if (actions.DiscreteActions[0] == 1)
        {
            AddReward(-0.02f);
        }

        // reward for jumping over gap (no ground)
        //if (!groundBelow && actions.DiscreteActions[1] == 1)
        //{
        //    AddReward(0.002f);
        //}

        //negative reward for falling off level
        if (transform.position.y < -10f)
        {
            AddReward(-1f);
            EndEpisode();
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
    }

    private void GoalReached()
    {
        AddReward(1f);//large reward for reaching goal
        CumulativeReward = GetCumulativeReward();

        EndEpisode();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("wall");
            //apply small negative reward
            AddReward(-0.05f);

            //change colour
            if (_renderer != null)
            {
                _renderer.material.color = Color.red;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("wall stay");
            //continually penalise the agent while its in contatct with the wall
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
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
        EndEpisode();
        //RespawnManager.Instance.Respawn(this);
    }
}
