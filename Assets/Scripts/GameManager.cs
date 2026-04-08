using UnityEngine;
using UnityEngine.SceneManagement;

//Manages game state, menu, and time limits for QA testing sessions
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Testing Session Settings")]
    [Tooltip("Maximum testing session time in seconds (default: 10 minutes)")]
    public float sessionTimeLimit = 600f;
    [Tooltip("Show warning before session ends")]
    public bool showTimeWarning = true;
    [Tooltip("Seconds before end to show warning")]
    public float warningTime = 60f;

    [Header("References")]
    [Tooltip("The menu panel")]
    public GameObject menuPanel;
    [Tooltip("The pause panel")]
    public GameObject pausePanel;
    [Tooltip("The human player controller")]
    public HumanPlayerController player;
    [Tooltip("The UI overlay")]
    public HumanPlayerUI playerUI;

    [Header("State")]
    public bool isSessionActive = false;
    private bool _isPaused = false;
    private float _sessionTimeRemaining;
    private bool _warningShown = false;

    public bool IsPaused => _isPaused;

    private void Awake()
    {
        //singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        _isPaused = true;
        ShowMenu();
        
        //ensure pause panel is hidden at start
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        //don't update timers if paused or session not active
        if (!isSessionActive || _isPaused) return;

        //countdown session timer
        _sessionTimeRemaining -= Time.deltaTime;

        //show warning
        if (showTimeWarning && !_warningShown && _sessionTimeRemaining <= warningTime)
        {
            _warningShown = true;
            Debug.Log($"[GameManager] Warning: {warningTime} seconds remaining in testing session!");
        }

        //end session when time runs out
        if (_sessionTimeRemaining <= 0f)
        {
            EndSession();
        }
    }

    public void StartSession()
    {
        Debug.Log("[GameManager] Starting QA testing session");
        
        //reset the entire level
        ResetLevel();

        //hide menu
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            _isPaused = false;
        }


        //enable player
        if (player != null)
        {
            player.enabled = true;
            player.gameObject.SetActive(true);
            player.ResetPlayer(); //reset player stats
        }

        //enable UI
        if (playerUI != null)
            playerUI.enabled = true;

        //reset session timer
        _sessionTimeRemaining = sessionTimeLimit;
        _warningShown = false;
        isSessionActive = true;
        _isPaused = false;

        //unpause game
        Time.timeScale = 1f;
    }

    public void EndSession()
    {
        Debug.Log("[GameManager] Ending QA testing session");
        
        isSessionActive = false;
        _isPaused = false;

        //show final stats
        if (player != null)
        {
            Debug.Log($"[GameManager] Session Summary - Time: {player.PlayTime:F1}s, Deaths: {player.DeathCount}, Score: {player.Score}");
            if (player.Coverage != null)
            {
                float coverage = player.Coverage._visited.Count / (float)player.Coverage.TotalWalkableCells * 100f;
                Debug.Log($"[GameManager] Coverage: {coverage:F1}% ({player.Coverage._visited.Count}/{player.Coverage.TotalWalkableCells} cells)");
            }
        }

        ShowMenu();
    }

    public void TogglePause()
    {
        if (!isSessionActive) return;

        _isPaused = !_isPaused;

        if (_isPaused)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    private void Pause()
    {
        Debug.Log("[GameManager] Game paused for bug documentation");
        
        //show pause panel
        if (pausePanel != null)
            pausePanel.SetActive(true);

        //freeze physics
        Time.timeScale = 0f;
    }

    private void Resume()
    {
        Debug.Log("[GameManager] Game resumed");
        
        //hide pause panel
        if (pausePanel != null)
            pausePanel.SetActive(false);

        //unfreeze physics
        Time.timeScale = 1f;
    }

    private void ResetLevel()
    {
        Debug.Log("[GameManager] Resetting level");

        //reset all collectibles in the scene
        var collectibles = FindObjectsByType<CollectibleBug>(FindObjectsSortMode.None);
        foreach (var collectible in collectibles)
        {
            collectible.ResetForNewSession();
        }

        //reset coverage if available
        if (player != null && player.Coverage != null)
        {
            player.Coverage.ResetCoverage();
        }

        Debug.Log($"[GameManager] Reset {collectibles.Length} collectibles");
    }

    public void ShowMenu()
    {
        //show menu
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
            _isPaused = true;
        }

        //hide pause panel
        if (pausePanel != null)
            pausePanel.SetActive(false);

        //disable player
        if (player != null)
        {
            player.enabled = false;
        }

        //disable UI
        if (playerUI != null)
            playerUI.enabled = false;

        //pause game
        Time.timeScale = 0f;
    }

    public void QuitGame()
    {
        Debug.Log("[GameManager] Quitting application");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    //public getter for UI
    public float SessionTimeRemaining => _sessionTimeRemaining;
}