using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
//collectible bug: 3 variants - working, broken, duplication.
//each collectible in the level can be set to one of these variants
//to test if the agent can detect the bug based on its behaviour and logging
public class CollectibleBug : MonoBehaviour
{
    [Header("Bug Variant Selection")]
    [SerializeField] private BugVariant variant = BugVariant.Working;

    [Tooltip("Unique instance id")]
    [SerializeField] private string _itemId = "Item_ID_1";
    //public getter for item ID so it can be included in logs
    public string InstanceId => _itemId;

    [Tooltip("Bug report id used when logging")]
    public string brokenBugId = "BUG_COLLECT_BROKEN_01";
    public string duplicationBugId = "BUG_COLLECT_DUP_01";

    [Header("Normal Behaviour")]
    [SerializeField] private float collectReward = 0.5f;
    [SerializeField] private int scoreValue = 10;

    private bool _pickedUp = false; //whether the agent has picked up this collectible in the current episode
    private bool _wasActiveAtStart = true;//used to reset to initial state at episode begin
    
    //track collections across episodes for duplication detection
    private static Dictionary<string, int> _episodeCollectionCount = new Dictionary<string, int>();
    private static int _currentEpisode = 0;

    //track if agent has collected this before
    private bool _agentCollectedBefore = false;
    private bool _humanCollectedBefore = false;
    private bool _isSubscribed = false;

    //3 bug variants
    public enum BugVariant
    {
        Working, //working: gives normal score, disappears, stays gone on respawn
        Broken, //broken: doesn't disappear, doesn't give score, agent logs immediately
        Duplication //duplication: disappears, gives score, reappears on respawn, agent logs on 2nd pickup
    }

    //on reset ensure the collider is set up correctly and tagged as "Collectible"
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";
    }

    //initialises the collectible generating a unique ID and subscribing to respawn events for the duplication bug variant
    private void Awake()
    {
        //generate a unique item ID to differentiate multiple collectibles in the same level
        _itemId = gameObject.GetInstanceID().ToString();
        //remember if the collectible was active at the start to reset properly later (for duplication bug)
        _wasActiveAtStart = gameObject.activeSelf;

        //subscribe in Awake so it persists even when GameObject is inactive
        SubscribeToRespawnEvents();
    }

    //unsubscribe from events when destroyed to avoid memory leaks
    private void OnDestroy()
    {
        //only unsubscribe when actually destroyed, not when disabled
        UnsubscribeFromRespawnEvents();
    }

    //subscribe to respawn events to handle the duplication bug variant
    private void SubscribeToRespawnEvents()
    {
        if (_isSubscribed) return;

        //subscribe to both human and agent respawn events since the same collectible can be collected by either
        HumanPlayerController.OnHumanPlayerRespawn += OnHumanPlayerRespawned;
        SimplifiedCoverage.OnAgentRespawn += OnAgentRespawned;
        _isSubscribed = true;
    }

    //unsubscribe from events to avoid memory leaks when the object is destroyed
    private void UnsubscribeFromRespawnEvents()
    {
        if (!_isSubscribed) return;

        //unsubscribe from both human and agent respawn events
        HumanPlayerController.OnHumanPlayerRespawn -= OnHumanPlayerRespawned;
        SimplifiedCoverage.OnAgentRespawn -= OnAgentRespawned;
        _isSubscribed = false;
    }

    //trigger handling for when the player collides with the collectible, with different behaviour based on the selected variant
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        //try to get AI agent first
        var agent = other.GetComponent<SimplifiedCoverage>();
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

    //handle AI agent collision based on the selected bug variant
    private void HandleAgent(SimplifiedCoverage agent)
    {
        //track episode changes to reset collection counts
        if (_currentEpisode != agent.CurrentEpisode)
        {
            _currentEpisode = agent.CurrentEpisode;
            _episodeCollectionCount.Clear();
        }

        switch (variant)
        {
            case BugVariant.Working:
                HandleWorkingVariantAgent(agent);
                break;

            case BugVariant.Broken:
                HandleBrokenVariantAgent(agent);
                break;

            case BugVariant.Duplication:
                HandleDuplicationVariantAgent(agent);
                break;
        }
    }

    //handle human player collision based on the selected bug variant
    private void HandleHuman(HumanPlayerController human)
    {
        //human experiences bugs naturally without notifications
        switch (variant)
        {
            case BugVariant.Working:
                HandleWorkingVariantHuman(human);
                break;

            case BugVariant.Broken:
                HandleBrokenVariantHuman(human);
                break;

            case BugVariant.Duplication:
                HandleDuplicationVariantHuman(human);
                break;
        }
    }

    //AI Variant Handlers

    private void HandleWorkingVariantAgent(SimplifiedCoverage agent)
    {
        // Working variant: give score, disappear, stay gone
        if (_pickedUp) return;
        _pickedUp = true;

        gameObject.SetActive(false);
    }

    private void HandleBrokenVariantAgent(SimplifiedCoverage agent)
    {
        //log bug
        agent.FoundBug($"collectible_broken:{brokenBugId}");
    }

    private void HandleDuplicationVariantAgent(SimplifiedCoverage agent)
    {
        //prevent multiple pickups in the same episode
        if (_pickedUp) return;
        _pickedUp = true;

        //check if the agent has collected this item before in this episode
        if (!_episodeCollectionCount.ContainsKey(_itemId))
        {
            _episodeCollectionCount[_itemId] = 0;
        }

        _episodeCollectionCount[_itemId]++;
        int pickupCount = _episodeCollectionCount[_itemId];

        //give reward
        agent.AddReward(collectReward);

        //if this is the second or later time the agent picked it up in the same episode, log the bug
        if (pickupCount > 1)
        {
            agent.FoundBug($"collectible_duplication:{duplicationBugId}");
        }

        //mark that agent has collected this before
        _agentCollectedBefore = true;

        //disappear
        gameObject.SetActive(false);
    }

    //Humnan Player Handlers

    private void HandleWorkingVariantHuman(HumanPlayerController human)
    {
        // Working variant: give score, disappear, stay gone (normal behavior)
        if (_pickedUp) return;
        _pickedUp = true;

        //add score to human player
        human.AddScore(scoreValue);

        gameObject.SetActive(false);
    }

    private void HandleBrokenVariantHuman(HumanPlayerController human)
    {
        //since broken, do nothing, as shouldn't log for human player 
    }

    private void HandleDuplicationVariantHuman(HumanPlayerController human)
    {
        //item disappears and gives score, but will reappear if player dies/respawns
        
        if (_pickedUp) return;
        _pickedUp = true;

        //give score
        human.AddScore(scoreValue);

        //mark that human has collected this before
        _humanCollectedBefore = true;
        
        gameObject.SetActive(false);
    }

    //called when AI agent respawns at checkpoint
    private void OnAgentRespawned()
    {
        //only duplication variant reappears after respawn
        if (variant != BugVariant.Duplication) return;

        //if agent had collected this before, reactivate it
        if (_agentCollectedBefore)
        {
            _pickedUp = false;
            gameObject.SetActive(true);
        }
    }

    //called when human player respawns
    private void OnHumanPlayerRespawned()
    {
        //only duplication variant reappears after respawn
        if (variant != BugVariant.Duplication) return;

        //if human had collected this before, reactivate it
        if (_humanCollectedBefore)
        {
            _pickedUp = false;
            gameObject.SetActive(true);
        }
    }

    //called to reset for new human testing session
    public void ResetForNewSession()
    {
        //reset all state
        _pickedUp = false;
        _agentCollectedBefore = false;
        _humanCollectedBefore = false;
        gameObject.SetActive(_wasActiveAtStart);
    }

    //called manually to reset collectibles at episode begin
    public void ResetCollectible()
    {
        //reset all collectibles to their initial state at episode begin
        _pickedUp = false;
        _agentCollectedBefore = false;
        gameObject.SetActive(_wasActiveAtStart);
    }
}