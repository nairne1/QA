using UnityEngine;
using System.IO;
using ExcelLibrary.Office.Excel;

//logger that records at what time did the agen t run into a bug, the position of the bug, and the bug's ID
public class SimpleRunLogger : MonoBehaviour
{
    [Header("Logger Settings")]
    [Tooltip("Folder that the run logs will be saved in")]
    public const string folderName = "../Run Logs";
    public static SimpleRunLogger Instance;//instance for global access
    [Tooltip("Name of the file")]
    public const string baseFileName = "ai_bug_logger";

    [Tooltip("Transform of the player to track position")]
    public Transform player;

    //excel objects
    private Workbook workbook;
    private Worksheet sheet;
    private int currentRow = 1; //row 0 = header

    //full file path
    private string path;
    private string folderPath;

    //sets up the instance and creates the excel file with headers
    private void Awake()
    {
        Instance = this;
    }

    //inisialises the logging system
    void Start()
    {
        //create the folder
        folderPath = Path.Combine(Application.dataPath, folderName);
        Directory.CreateDirectory(folderPath);

        //baseFileName_MMdd_HHmmss.xls
        string timestamp = System.DateTime.Now.ToString("MMdd_HHmmss");
        path = Path.Combine(folderPath, $"{baseFileName}_{timestamp}.xls");

        //create new excel file
        workbook = new Workbook();
        sheet = new Worksheet("Run Log");

        //setup header row
        sheet.Cells[0, 0] = new Cell("time");
        sheet.Cells[0, 1] = new Cell("px");
        sheet.Cells[0, 2] = new Cell("py");
        sheet.Cells[0, 3] = new Cell("event");
    }

    //logs an event with the current time and player position, then saves the file
    public void Log(string evt)
    {
        //skip logging if player reference or sheet isnt set 
        if (player == null || sheet == null) return;

        //get current time and player position
        float t = Time.time;
        Vector2 p = player.position;

        //write data to current row
        sheet.Cells[currentRow, 0] = new Cell(t.ToString("F3"));
        sheet.Cells[currentRow, 1] = new Cell(p.x.ToString("F3"));
        sheet.Cells[currentRow, 2] = new Cell(p.y.ToString("F3"));
        sheet.Cells[currentRow, 3] = new Cell(evt);

        currentRow++;

        //save the file each time so it always updates
        workbook.Worksheets.Clear();
        workbook.Worksheets.Add(sheet);
        workbook.Save(path);
    }
}
