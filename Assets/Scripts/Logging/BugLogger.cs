using System.IO;
using UnityEngine;

//handles logging bug reports to CSV file
public class BugLogger : MonoBehaviour
{
    public static BugLogger Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Folder path relative to Assets folder")]
    public string bugReportFolder = "BugReports";

    [Tooltip("CSV filename")]
    public string csvFilename = "BugReports.csv";

    private string _fullPath;

    private void Awake()
    {
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
        InitializeCSV();
    }

    //ensure the CSV file exists and has a header
    private void InitializeCSV()
    {
        string folderPath = Path.Combine(Application.dataPath, bugReportFolder);

        //create folder if it doesn't exist
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        _fullPath = Path.Combine(folderPath, csvFilename);

        //create file with header if it doesn't exist
        if (!File.Exists(_fullPath))
        {
            File.WriteAllText(_fullPath, BugReport.CSVHeader() + "\n");
            Debug.Log($"Created bug report CSV at: {_fullPath}");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    }

    //log a bug report to the CSV file
    public void LogBug(BugReport report)
    {
        if (string.IsNullOrEmpty(_fullPath))
        {
            InitializeCSV();
        }

        try
        {
            //append to CSV
            File.AppendAllText(_fullPath, report.ToCSV() + "\n");
            Debug.Log($"Bug logged: {report.bugTitle}");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to log bug: {e.Message}");
        }
    }

    //open the folder containing the bug report CSV file
    public void OpenBugReportFolder()
    {
        string folderPath = Path.Combine(Application.dataPath, bugReportFolder);
        
        if (Directory.Exists(folderPath))
        {
            Application.OpenURL("file:///" + folderPath);
        }
    }
}