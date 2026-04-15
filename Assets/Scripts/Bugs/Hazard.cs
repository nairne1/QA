using UnityEngine;

[RequireComponent(typeof(Collider2D))]
//hazard trigger can be configured to be a bug that doesn't kill the player, but instead logs a bug found for the gaent
public class Hazard : MonoBehaviour
{
    [Tooltip("If true, hazard is bugged")]
    [SerializeField] bool isBug = false;

    [Tooltip("Unique bug report ID")]
    public string bugId = "BUG_HZ_01";

    //public getter for whether this hazard is a bug
    public bool IsBug => isBug;

    //reset sets the layer to Hazard and makes sure the collider is a trigger
    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer("Hazard");
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    //detects when a player enters the hazard
    void OnTriggerEnter2D(Collider2D other)
    {
        //only act if the entering object is the player
        if (!other.CompareTag("Player")) return;

        //try to get AI agent first
        var agent = other.GetComponent<SimplifiedCoverage>();
        if (agent != null) {

            HandleAgent(agent);
            return;
        }

        //check for human player controller
        var human = other.GetComponent<HumanPlayerController>();
        if (human != null)
        {
            HandleHuman(human);
            return;
        }
    }

    private void HandleAgent(SimplifiedCoverage agent)
    {
        //if agent isnt null, register the hazard test and either kill the agent or log a bug found
        if (agent != null)
        {
            agent.RegisterHazardTest(bugId);

            if (!isBug)
            {
                // normal hazard
                agent.Die();
            }
            else
            {
                // bug hazard - doesn't kill
                agent.FoundBug($"hazard:{bugId}");
            }
        }
    }

    private void HandleHuman(HumanPlayerController human) {

        if (!isBug)
        {
            //normal hazard
            human.Kill();
        }
        //do nothing if bug as playe should report on their own
    }
}
