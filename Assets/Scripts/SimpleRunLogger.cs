using UnityEngine;
using System.IO;

public class SimpleRunLogger : MonoBehaviour
{
    //folder will be created one level above 'Assets'
    public const string folderName = "../Run Logs";
    public const string baseFileName = "run_log";
    string path;

    public Transform player;

    public static SimpleRunLogger Instance;

    public string fileName = "run_log.csv";

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        //create the folder
        string folderPath = Path.Combine(Application.dataPath, folderName);
        Directory.CreateDirectory(folderPath);

        //timestamped filename - month, day _ hours, mins, seconds
        string timeStamp = System.DateTime.Now.ToString("MMdd_HHmmss");
        path = Path.Combine(folderPath, $"{baseFileName}_{timeStamp}.csv");

        File.WriteAllText(path, "time,px,py,event\n");
        Debug.Log($"[Logger] Writing to: {path}");
    }

    void Update()
    {
        Log("pos");
    }

    public void Log(string evt)
    {
        if (player == null) return;

        var t = Time.time;
        var p = player.localPosition;
        File.AppendAllText(path, $"{t:F3},{p.x:F3},{p.y:F3},{evt}\n");
    }
}
