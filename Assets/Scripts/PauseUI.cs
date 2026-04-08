using UnityEngine;
using UnityEngine.UI;

//Pause menu UI for bug documentation
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
        //Hook up button events
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (endSessionButton != null)
            endSessionButton.onClick.AddListener(OnEndSessionClicked);

        //Update info text
        if (infoText != null)
        {
            infoText.text = "Game Paused\n\nClick 'Report Bug' to document issues.\n\nPress ESC or P to resume.";
        }
    }

    private void Update()
    {
        //Allow ESC/P to resume from pause menu (only if bug report panel is closed)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            //Check if bug report panel is open
            if (bugReportUI != null && bugReportUI.bugReportPanel != null && bugReportUI.bugReportPanel.activeSelf)
            {
                //Close bug report panel instead
                bugReportUI.CloseBugReportPanel();
            }
            else
            {
                OnResumeClicked();
            }
        }
    }

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