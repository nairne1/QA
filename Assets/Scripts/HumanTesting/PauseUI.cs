using UnityEngine;
using UnityEngine.UI;

//pause menu UI for bug documentation
public class PauseUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Button to resume game")]
    public Button resumeButton;
    [Tooltip("Button to end session early")]
    public Button endSessionButton;
    [Tooltip("Optional: Information text")]
    public Text infoText;

    [Header("Bug Reporting")]
    [Tooltip("Bug report UI component")]
    public BugReportUI bugReportUI;

    private void Start()
    {
        //hook up button events
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (endSessionButton != null)
            endSessionButton.onClick.AddListener(OnEndSessionClicked);

        //update info text
        if (infoText != null)
        {
            infoText.text = "Game Paused\n\nClick 'Report Bug' to document issues.\n\nPress ESC or P to resume.";
        }
    }

    private void Update()
    {
        //allow ESC/P to resume from pause menu
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            //check if bug report panel is open
            if (bugReportUI != null && bugReportUI.bugReportPanel != null && bugReportUI.bugReportPanel.activeSelf)
            {
                //close bug report panel instead
                bugReportUI.CloseBugReportPanel();
            }
            else
            {
                OnResumeClicked();
            }
        }
    }

    //button event handlers
    private void OnResumeClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause();
    }

    private void OnEndSessionClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.EndSession();
    }
}