using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
//collectible bug: 3 variants - working, broken, duplication.
//each collectible in the level can be set to one of these variants to test if the agent can detect the bug based on its behaviour and logging
public class CollectibleBug : MonoBehaviour
{
    [Header("Bug Variant Selection")]
    [SerializeField] private BugVariant variant = BugVariant.Working;

    [Tooltip("Unique instance id")]
    [SerializeField] private string _itemId = "Item_ID_1";
    public string InstanceId => _itemId;

    [Tooltip("Bug report id used when logging")]
    public string brokenBugId = "BUG_COLLECT_BROKEN_01";
    public string duplicationBugId = "BUG_COLLECT_DUP_01";

    [Header("Normal Behaviour")]
    [SerializeField] private float collectReward = 0.5f;
    [SerializeField] private int scoreValue = 10;

    private bool _pickedUp = false; //whether the agent has picked up this collectible in the current episode
    private bool _wasActiveAtStart = true;//used to reset to initial state at episode begin
    
    //track collections across episodes for duplication detection (AI only)
    private static Dictionary<string, int> _episodeCollectionCount = new Dictionary<string, int>();
    private static int _currentEpisode = 0;

    //track if human player has collected this before
    private bool _humanCollectedBefore = false;
    private bool _isSubscribed = false;

    //3 possible bug variants
    public enum BugVariant
    {
        Working,      // Normal: gives score, disappears, stays gone
        Broken,       // Bug: doesn't disappear, doesn't give score, agent logs immediately
        Duplication   // Bug: disappears, gives score, reappears on respawn, agent logs on 2nd pickup
    }

    // On reset ensure the collider is set up correctly and tagged as "Collectible"
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";
    }

    private void Awake()
    {
        //generate a unique item ID to differentiate multiple collectibles in the same level
        _itemId = gameObject.GetInstanceID().ToString();
        _wasActiveAtStart = gameObject.activeSelf;

        //subscribe in Awake so it persists even when GameObject is inactive
        SubscribeToRespawnEvent();
    }

    private void OnDestroy()
    {
        //only unsubscribe when actually destroyed, not when disabled
        UnsubscribeFromRespawnEvent();
    }

    private void SubscribeToRespawnEvent()
    {
        if (_isSubscribed) return;
        
        HumanPlayerController.OnHumanPlayerRespawn += OnHumanPlayerRespawned;
        _isSubscribed = true;
    }

    private void UnsubscribeFromRespawnEvent()
    {
        if (!_isSubscribed) return;
        
        HumanPlayerController.OnHumanPlayerRespawn -= OnHumanPlayerRespawned;
        _isSubscribed = false;
    }

    //trigger handling for when the player collides with the collectible, with different behaviour based on the selected variant
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

    private void HandleAgent(QAExplorerAgentPhase1 agent)
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

    //=== AI AGENT HANDLERS ===

    private void HandleWorkingVariantAgent(QAExplorerAgentPhase1 agent)
    {
        // Working variant: give score, disappear, stay gone
        if (_pickedUp) return;
        _pickedUp = true;

        agent.AddReward(collectReward);

        if (SimpleRunLogger.Instance)
            SimpleRunLogger.Instance.Log($"collectible_working:{_itemId}:collected");

        gameObject.SetActive(false);
    }

    private void HandleBrokenVariantAgent(QAExplorerAgentPhase1 agent)
    {
        //log bug
        agent.FoundBug($"collectible_broken:{brokenBugId}");

        if (SimpleRunLogger.Instance)
            SimpleRunLogger.Instance.Log($"bug_found:collectible_broken:{brokenBugId}");
    }

    private void HandleDuplicationVariantAgent(QAExplorerAgentPhase1 agent)
    {
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

            if (SimpleRunLogger.Instance)
                SimpleRunLogger.Instance.Log($"bug_found:collectible_duplication:{duplicationBugId}");
        }
        else
        {
            if (SimpleRunLogger.Instance)
                SimpleRunLogger.Instance.Log($"collectible_duplication:{_itemId}:first_pickup");
        }

        //disappear (normal behavior)
        gameObject.SetActive(false);
    }

    //=== HUMAN PLAYER HANDLERS ===

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
        //BUG: Item doesn't disappear, doesn't give reward/score
        //Human notices: "I touched it but nothing happened, it's still there"
        //No notification - they discover the bug naturally
    }

    private void HandleDuplicationVariantHuman(HumanPlayerController human)
    {
        //BUG: Item disappears and gives score, but will reappear if player dies/respawns
        //Human notices: "Wait, I already collected this earlier" (can farm score)
        //No notification - they discover the bug naturally
        
        if (_pickedUp) return;
        _pickedUp = true;

        //give score (this is the bug - they can collect it multiple times!)
        human.AddScore(scoreValue);

        //mark that human has collected this before
        _humanCollectedBefore = true;
        
        gameObject.SetActive(false);
    }

    //called when human player respawns (for duplication bug)
    private void OnHumanPlayerRespawned()
    {
        //only duplication variant reappears after respawn
        if (variant != BugVariant.Duplication) return;

        //if human had collected this before, reactivate it (BUG!)
        if (_humanCollectedBefore)
        {
            _pickedUp = false;
            gameObject.SetActive(true);
        }
    }

    //Called to reset for new human testing session
    public void ResetForNewSession()
    {
        Debug.Log($"[CollectibleBug] Resetting {_itemId} for new session. Variant: {variant}");

        //reset all state
        _pickedUp = false;
        _humanCollectedBefore = false;
        gameObject.SetActive(_wasActiveAtStart);
    }

    //Called manually to reset collectibles at episode begin (for AI)
    public void ResetCollectible()
    {
        //reset all collectibles to their initial state at episode begin
        _pickedUp = false;
        gameObject.SetActive(_wasActiveAtStart);

        if (SimpleRunLogger.Instance && _wasActiveAtStart)
            SimpleRunLogger.Instance.Log($"collectible_reset:{variant}:{_itemId}");
    }
}