using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class PlayerAgent : Agent
{
    [SerializeField] private Transform _goal;
    [SerializeField] private float _moveSpeed = 1.5f;
    [SerializeField] private float _jumpPower = 6f;

    //stores agent's renderer component - change colour when collides with wall, etc
    private Renderer _renderer;

    private Vector2 _spawnPosition = new Vector3(-8f, -3.35f);

    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    //called when the agent is first created 
    public override void Initialize()
    {
        //retreives the renderer component attached to the agent 
        _renderer = GetComponent<Renderer>();
        CurrentEpisode = 0;
        CumulativeReward = 0f;
    }

    //reset environment on each restart
    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode: " + CurrentEpisode);
        Debug.Log("cumulative reward: " + CumulativeReward);

        CurrentEpisode++;
        CumulativeReward = 0f;
        _renderer.material.color = Color.blue;

        //reposition agent and goal
        SpawnObjects();
    }

    private void SpawnObjects()
    {
        //reset angents position
        transform.localPosition = _spawnPosition;

        //randomise the distance within range
        float randomDistance = Random.Range(-8f, 8f);

        //apply the calcd position to the goal
        _goal.localPosition = new Vector2(randomDistance, -3.35f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //the goals postition - keep between -1, 1, ensure the inputs are within a similar range 
        float goalPosx = _goal.localPosition.x / 5f;

        //the agents position
        float agentPosx = transform.localPosition.x / 5f;

        //sensor is the container for the observations we want the agent to know 
        //Vector Observation - space size, in the behaviour parameters, is how ever many of these floats we pass in
        sensor.AddObservation(goalPosx);
        sensor.AddObservation(agentPosx);
    }

    //telling the agent exactly what to do
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        discreteActionsOut[0] = 0;//default action - do nothing

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[0] = 2;
        }
        else if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 3;
        }
    }

    //executing actions its given - called every step
    //actions holds the decision output from the ml agents backend
    public override void OnActionReceived(ActionBuffers actions)
    {
        //move the agent using the action
        MoveAgent(actions.DiscreteActions);

        //penalty given each step, so it reaches goal asap
        AddReward(-2f / MaxStep);

        //update the cumulative reward
        CumulativeReward = GetCumulativeReward();
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        var action = act[0];

        switch (action)
        {
            case 1://move left
                transform.position += Vector3.left * _moveSpeed * Time.deltaTime;
                break;
            case 2://move right
                transform.position += Vector3.right * _moveSpeed * Time.deltaTime;
                break;
            case 3://Jump
                transform.position += Vector3.up * _jumpPower * Time.deltaTime;
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Goal"))
        {
            GoalReached();
        }
    }

    private void GoalReached()
    {
        Debug.Log("goal reached");
        AddReward(1f);//large reward for reaching goal
        CumulativeReward = GetCumulativeReward();

        EndEpisode();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
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
}
