using System;
using UnityEngine;

//Data structure for a bug report
[System.Serializable]
public class BugReport
{
    public string timestamp;
    public string bugTitle;
    public string expectedResult;
    public string actualResult;
    public string stepsToReproduce;
    public int severity; // 1-5 (1=minor, 5=critical)
    public Vector2 position;
    public float playTime;
    public int deathCount;
    public int score;

    public BugReport()
    {
        // Use session time from GameManager instead of system time
        if (GameManager.Instance != null)
        {
            float sessionTime = GameManager.Instance.sessionTimeLimit - GameManager.Instance.SessionTimeRemaining;
            int minutes = Mathf.FloorToInt(sessionTime / 60f);
            float seconds = sessionTime % 60f;
            timestamp = $"{minutes:00}:{seconds:00.00}";
        }
        else
        {
            timestamp = "00:00.00";
        }
        
        bugTitle = "";
        expectedResult = "";
        actualResult = "";
        stepsToReproduce = "";
        severity = 3; // Default to medium
        position = Vector2.zero;
        playTime = 0f;
        deathCount = 0;
        score = 0;
    }

    //Convert to CSV row
    public string ToCSV()
    {
        return $"\"{timestamp}\",\"{EscapeCSV(bugTitle)}\",\"{EscapeCSV(expectedResult)}\",\"{EscapeCSV(actualResult)}\"," +
               $"\"{EscapeCSV(stepsToReproduce)}\",{severity},\"({position.x:F2}, {position.y:F2})\"," +
               $"{playTime:F2},{deathCount},{score}";
    }

    //Escape quotes in CSV fields
    private string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }

    //CSV header
    public static string CSVHeader()
    {
        return "Session Time,Bug Title,Expected Result,Actual Result,Steps to Reproduce,Severity (1-5),Position,Play Time,Death Count,Score";
    }
}