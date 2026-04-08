using UnityEngine;

//Simple on-screen display for human QA testing
//Shows timer and player position relative to level bounds
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
    public int fontSize = 16;
    [Tooltip("UI position offset from top-left")]
    public Vector2 uiOffset = new Vector2(10, 10);

    private GUIStyle _textStyle;
    private GUIStyle _warningStyle;
    private GUIStyle _pausedStyle;
    private GUIStyle _hintStyle;

    private void Start()
    {
        //create GUI style
        _textStyle = new GUIStyle();
        _textStyle.fontSize = fontSize;
        _textStyle.normal.textColor = Color.white;
        _textStyle.fontStyle = FontStyle.Bold;
        
        //add black outline for readability
        _textStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.5f));
        _textStyle.padding = new RectOffset(5, 5, 5, 5);

        //create warning style (red text)
        _warningStyle = new GUIStyle(_textStyle);
        _warningStyle.normal.textColor = Color.red;

        //create paused style (yellow text)
        _pausedStyle = new GUIStyle(_textStyle);
        _pausedStyle.normal.textColor = Color.yellow;
        _pausedStyle.fontSize = fontSize + 4;

        //create hint style (gray text, smaller)
        _hintStyle = new GUIStyle(_textStyle);
        _hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        _hintStyle.fontSize = fontSize - 2;
    }

    private void OnGUI()
    {
        if (player == null) return;

        float yPos = uiOffset.y;
        float lineHeight = fontSize + 10;

        //show PAUSED indicator
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GUI.Label(new Rect(uiOffset.x, yPos, 300, lineHeight), " PAUSED", _pausedStyle);
            yPos += lineHeight + 5;
        }

        //play timer
        if (showPlayTimer)
        {
            string timeText = $"Time: {FormatTime(player.PlayTime)}";
            GUI.Label(new Rect(uiOffset.x, yPos, 300, lineHeight), timeText, _textStyle);
            yPos += lineHeight;
        }

        //session timer
        if (showSessionTimer && GameManager.Instance != null)
        {
            float remaining = GameManager.Instance.SessionTimeRemaining;
            bool isWarning = remaining <= GameManager.Instance.warningTime;
            
            string sessionText = $"Session Time: {FormatTime(remaining)}";
            GUI.Label(new Rect(uiOffset.x, yPos, 300, lineHeight), sessionText, isWarning ? _warningStyle : _textStyle);
            yPos += lineHeight;
        }

        //score
        if (showScore)
        {
            string scoreText = $"Score: {player.Score}";
            GUI.Label(new Rect(uiOffset.x, yPos, 300, lineHeight), scoreText, _textStyle);
            yPos += lineHeight;
        }

        //coordinates
        if (showCoordinates)
        {
            //get actual player position (either center or feet)
            Vector2 playerPos;
            if (useFeetPosition && player.groundCheck != null)
            {
                playerPos = player.groundCheck.position;
            }
            else
            {
                playerPos = player.transform.position;
            }

            string coordText;

            if (levelBounds != null)
            {
                coordText = $"Position: ({playerPos.x:F1}, {playerPos.y:F1})";
            }
            else
            {
                coordText = $"Position: ({playerPos.x:F1}, {playerPos.y:F1})";
            }

            GUI.Label(new Rect(uiOffset.x, yPos, 400, lineHeight), coordText, _textStyle);
            yPos += lineHeight;
        }

        //deaths
        if (showDeaths)
        {
            string deathText = $"Deaths: {player.DeathCount}";
            GUI.Label(new Rect(uiOffset.x, yPos, 300, lineHeight), deathText, _textStyle);
            yPos += lineHeight;
        }

        //pause hint (bottom of UI)
        if (showPauseHint && GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            yPos += 5;
            GUI.Label(new Rect(uiOffset.x, yPos, 400, lineHeight), "Press ESC or P to pause for bug documentation", _hintStyle);
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