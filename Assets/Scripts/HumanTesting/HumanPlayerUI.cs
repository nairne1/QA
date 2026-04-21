using UnityEngine;

//on-screen display for human QA testing
//shows timer and player position relative to level bounds
public class HumanPlayerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The human player controller")]
    public HumanPlayerController player;
    [Tooltip("The level bounds for coordinate reference")]
    public BoxCollider2D levelBounds;

    [Header("Coordinate Reference")]
    [Tooltip("Show position of player's feet (groundCheck) instead of center")]
    public bool useFeetPosition = true;

    [Header("UI Settings")]
    [Tooltip("Show play timer")]
    public bool showPlayTimer = true;
    [Tooltip("Show session time remaining")]
    public bool showSessionTimer = true;
    [Tooltip("Show score")]
    public bool showScore = true;
    [Tooltip("Show coordinates")]
    public bool showCoordinates = true;
    [Tooltip("Show deaths")]
    public bool showDeaths = true;
    [Tooltip("Show pause hint")]
    public bool showPauseHint = true;
    [Tooltip("UI text size")]
    public int fontSize = 36;
    [Tooltip("UI position offset from top-left")]
    public Vector2 uiOffset = new Vector2(10, 10);

    [Header("Bug Report Integration")]
    [Tooltip("Bug report UI reference (to check if open)")]
    public BugReportUI bugReportUI;

    [Tooltip("Minimum width of the main UI background")]
    public float minPanelWidth = 420f;

    [Tooltip("Minimum width of the hint background")]
    public float minHintWidth = 520f;

    [Tooltip("Extra padding added to calculated text width")]
    public float panelPadding = 20f;

    private GUIStyle _textStyle;
    private GUIStyle _warningStyle;
    private GUIStyle _pausedStyle;
    private GUIStyle _hintStyle;

    public float scale = 1f;
    private int scaledFontSize;

    //initialize GUI styles
    private void Start()
    {
        //scale based on 1080p reference
        scale = Screen.height / 1080f;
        scaledFontSize = Mathf.RoundToInt(fontSize * scale);

        //create GUI style
        _textStyle = new GUIStyle();
        _textStyle.fontSize = scaledFontSize;
        _textStyle.normal.textColor = Color.white;
        _textStyle.fontStyle = FontStyle.Bold;
        _textStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.5f));
        _textStyle.padding = new RectOffset(5, 5, 5, 5);

        //add black outline for readability
        _textStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.5f));
        _textStyle.padding = new RectOffset(5, 5, 5, 5);

        //create warning style (red text)
        _warningStyle = new GUIStyle(_textStyle);
        _warningStyle.normal.textColor = Color.red;

        //create paused style (yellow text)
        _pausedStyle = new GUIStyle(_textStyle);
        _pausedStyle.normal.textColor = Color.yellow;
        _pausedStyle.fontSize = scaledFontSize + Mathf.RoundToInt(4 * scale);

        //create hint style (gray text, smaller)
        _hintStyle = new GUIStyle(_textStyle);
        _hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        _hintStyle.fontSize = Mathf.Max(12, scaledFontSize - 2);

        //scale layout values too
        minPanelWidth *= scale;
        minHintWidth *= scale;
        panelPadding *= scale;
        uiOffset *= scale;
    }

    private void OnGUI()
    {
        if (player == null) return;

        if (!enabled || !gameObject.activeInHierarchy) return;

        if (bugReportUI != null && bugReportUI.bugReportPanel != null && bugReportUI.bugReportPanel.activeSelf)
            return;

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        float yPos = uiOffset.y;
        float lineHeight = scaledFontSize + (10f * scale);

        //build all main UI lines first so we can calculate one consistent width
        System.Collections.Generic.List<string> mainLines = new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<GUIStyle> mainStyles = new System.Collections.Generic.List<GUIStyle>();

        if (showPlayTimer)
        {
            mainLines.Add($"Time: {FormatTime(player.PlayTime)}");
            mainStyles.Add(_textStyle);
        }

        if (showSessionTimer && GameManager.Instance != null)
        {
            float remaining = GameManager.Instance.SessionTimeRemaining;
            bool isWarning = remaining <= GameManager.Instance.warningTime;

            mainLines.Add($"Session Time: {FormatTime(remaining)}");
            mainStyles.Add(isWarning ? _warningStyle : _textStyle);
        }

        if (showScore)
        {
            mainLines.Add($"Score: {player.Score}");
            mainStyles.Add(_textStyle);
        }

        if (showCoordinates)
        {
            Vector2 playerPos = (useFeetPosition && player.groundCheck != null)
                ? (Vector2)player.groundCheck.position
                : (Vector2)player.transform.position;

            mainLines.Add($"Position: ({playerPos.x:F1}, {playerPos.y:F1})");
            mainStyles.Add(_textStyle);
        }

        if (showDeaths)
        {
            mainLines.Add($"Deaths: {player.DeathCount}");
            mainStyles.Add(_textStyle);
        }

        //calculate one width for the main block
        float mainPanelWidth = minPanelWidth;
        for (int i = 0; i < mainLines.Count; i++)
        {
            Vector2 size = mainStyles[i].CalcSize(new GUIContent(mainLines[i]));
            mainPanelWidth = Mathf.Max(mainPanelWidth, size.x + panelPadding);
        }

        //draw main block
        for (int i = 0; i < mainLines.Count; i++)
        {
            GUI.Label(new Rect(uiOffset.x, yPos, mainPanelWidth, lineHeight), mainLines[i], mainStyles[i]);
            yPos += lineHeight;
        }

        //draw hints with their own consistent width
        if (showPauseHint && GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            yPos += 5;

            string hint1 = "Press ESC or P to pause for bug documentation";
            string hint2 = "Press H to show heatmap, J to show hitboxes, R to respawn";

            float hintWidth = minHintWidth;
            hintWidth = Mathf.Max(hintWidth, _hintStyle.CalcSize(new GUIContent(hint1)).x + panelPadding);
            hintWidth = Mathf.Max(hintWidth, _hintStyle.CalcSize(new GUIContent(hint2)).x + panelPadding);

            GUI.Label(new Rect(uiOffset.x, yPos, hintWidth, lineHeight), hint1, _hintStyle);
            GUI.Label(new Rect(uiOffset.x, yPos + (45f * scale), hintWidth, lineHeight), hint2, _hintStyle);
        }
    }

    //format seconds into MM:SS.ss
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes:00}:{remainingSeconds:00.00}";
    }

    //create a simple colored texture for background
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}