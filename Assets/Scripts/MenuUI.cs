using UnityEngine;
using UnityEngine.UI;

//Simple menu UI for QA testing sessions
public class MenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Button to start testing session")]
    public Button startButton;
    [Tooltip("Button to quit application")]
    public Button quitButton;
    [Tooltip("Optional: Text to display session time limit")]
    public Text sessionInfoText;

    private void Start()
    {
        //hook up button events
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        UpdateSessionInfo();
    }

    private void UpdateSessionInfo()
    {
        if (sessionInfoText != null && GameManager.Instance != null)
        {
            float minutes = GameManager.Instance.sessionTimeLimit / 60f;
            sessionInfoText.text = $"Testing Session Duration: {minutes:F0} minutes";
        }
    }

    private void OnStartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartSession();
    }

    private void OnQuitClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.QuitGame();
    }
}