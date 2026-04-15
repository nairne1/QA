using UnityEngine;
using UnityEngine.UI;

//UI for submitting bug reports from pause menu
public class BugReportUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main bug report panel")]
    public GameObject bugReportPanel;

    [Tooltip("Button to open bug report form")]
    public Button openReportButton;

    [Tooltip("Button to close bug report form")]
    public Button closeReportButton;

    [Tooltip("Button to submit bug report")]
    public Button submitButton;

    [Tooltip("Button to open bug report folder")]
    public Button openFolderButton;

    [Header("Input Fields")]
    public InputField bugTitleInput;
    public InputField expectedResultInput;
    public InputField actualResultInput;
    public InputField stepsToReproduceInput;
    public Slider severitySlider;
    public Text severityValueText;

    [Header("Context Display")]
    public Text contextInfoText;
    public Text confirmationText;

    [Header("References")]
    public HumanPlayerController player;
    public HumanPlayerUI playerUI;

    public bool isActive;

    private void Start()
    {

        //setup buttons
        if (openReportButton != null)
            openReportButton.onClick.AddListener(OpenBugReportPanel);

        if (closeReportButton != null)
            closeReportButton.onClick.AddListener(CloseBugReportPanel);

        if (submitButton != null)
            submitButton.onClick.AddListener(SubmitBugReport);

        if (openFolderButton != null)
            openFolderButton.onClick.AddListener(OpenBugFolder);

        //setup severity slider
        if (severitySlider != null)
        {
            severitySlider.minValue = 1;
            severitySlider.maxValue = 5;
            severitySlider.wholeNumbers = true;
            severitySlider.value = 3;
            severitySlider.onValueChanged.AddListener(UpdateSeverityText);
            UpdateSeverityText(3);
        }

        //hide panels initially
        if (bugReportPanel != null)
            bugReportPanel.SetActive(false);

        if (confirmationText != null)
            confirmationText.gameObject.SetActive(false);
    }

    private void OpenBugReportPanel()
    {
        if (bugReportPanel != null)
            bugReportPanel.SetActive(true);

        // Notify GameManager that bug report is open (keeps game paused)
        if (GameManager.Instance != null)
            GameManager.Instance.OnBugReportOpened();

        //hide game UI
        if (playerUI != null)
            playerUI.gameObject.SetActive(false);

        //populate context info
        UpdateContextInfo();

        //clear form
        ClearForm();

        //hide confirmation
        if (confirmationText != null)
            confirmationText.gameObject.SetActive(false);
    }

    public void CloseBugReportPanel()
    {
        if (bugReportPanel != null)
            bugReportPanel.SetActive(false);

        //notify GameManager that bug report is closed (returns to pause menu)
        if (GameManager.Instance != null)
            GameManager.Instance.OnBugReportClosed();

        isActive = false;
    }

    //gather relevant player context information to assist QA in documenting the bug
    private void UpdateContextInfo()
    {
        if (contextInfoText == null || player == null) return;

        Vector2 playerPos = player.transform.position;
        string context = $"<b>Current Game State:</b>\n" +
                        $"Position: ({playerPos.x:F2}, {playerPos.y:F2})\n" +
                        $"Play Time: {FormatTime(player.PlayTime)}\n" +
                        $"Deaths: {player.DeathCount}\n" +
                        $"Score: {player.Score}";

        contextInfoText.text = context;
    }

    //update severity label based on slider value
    private void UpdateSeverityText(float value)
    {
        if (severityValueText == null) return;

        int severity = (int)value;
        string severityLabel = "";

        switch (severity)
        {
            case 1: severityLabel = "1 - Minor"; break;
            case 2: severityLabel = "2 - Low"; break;
            case 3: severityLabel = "3 - Medium"; break;
            case 4: severityLabel = "4 - High"; break;
            case 5: severityLabel = "5 - Critical"; break;
        }

        severityValueText.text = severityLabel;
    }

    private void SubmitBugReport()
    {
        //validate required fields
        if (string.IsNullOrWhiteSpace(bugTitleInput.text))
        {
            ShowConfirmation("Please enter a bug title!", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(actualResultInput.text))
        {
            ShowConfirmation("Please describe what actually happened!", false);
            return;
        }

        //create bug report
        BugReport report = new BugReport();
        report.bugTitle = bugTitleInput.text.Trim();
        report.expectedResult = expectedResultInput.text.Trim();
        report.actualResult = actualResultInput.text.Trim();
        report.stepsToReproduce = stepsToReproduceInput.text.Trim();
        report.severity = (int)severitySlider.value;

        //add player context
        if (player != null)
        {
            report.position = player.transform.position;
            report.playTime = player.PlayTime;
            report.deathCount = player.DeathCount;
            report.score = player.Score;
        }

        //log the bug
        if (BugLogger.Instance != null)
        {
            BugLogger.Instance.LogBug(report);
            ShowConfirmation($"Bug '{report.bugTitle}' logged successfully!", true);
            
            //clear form after successful submission
            ClearForm();
        }
        else
        {
            ShowConfirmation("Error: BugLogger not found!", false);
        }
    }

    //clear form fields after submission or when opening the panel
    private void ClearForm()
    {
        if (bugTitleInput != null) bugTitleInput.text = "";
        if (expectedResultInput != null) expectedResultInput.text = "";
        if (actualResultInput != null) actualResultInput.text = "";
        if (stepsToReproduceInput != null) stepsToReproduceInput.text = "";
        if (severitySlider != null)
        {
            severitySlider.value = 3;
            UpdateSeverityText(3);
        }
    }

    //show confirmation message after submission
    private void ShowConfirmation(string message, bool success)
    {
        if (confirmationText == null) return;

        confirmationText.text = message;
        confirmationText.color = success ? Color.green : Color.red;
        confirmationText.gameObject.SetActive(true);

        //hide after 3 seconds
        CancelInvoke(nameof(HideConfirmation));
        Invoke(nameof(HideConfirmation), 3f);
    }

    //hide confirmation message after delay
    private void HideConfirmation()
    {
        if (confirmationText != null)
            confirmationText.gameObject.SetActive(false);
    }

    //open the folder where bug reports are saved
    private void OpenBugFolder()
    {
        if (BugLogger.Instance != null)
        {
            BugLogger.Instance.OpenBugReportFolder();
        }
    }

    //utility to format play time in minutes:seconds
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes:00}:{remainingSeconds:00.0}";
    }
}