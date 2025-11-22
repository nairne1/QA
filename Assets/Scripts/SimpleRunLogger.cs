using UnityEngine;
using System.IO;
using ExcelLibrary.Office.Excel;

public class SimpleRunLogger : MonoBehaviour
{
    public const string folderName = "../Run Logs";
    public static SimpleRunLogger Instance;
    public const string baseFileName = "run_log";

    public Transform player;

    private Workbook workbook;
    private Worksheet sheet;
    private int currentRow = 1; //row 0 = header

    private string path;
    private string folderPath;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        //create the folder
        folderPath = Path.Combine(Application.dataPath, folderName);
        Directory.CreateDirectory(folderPath);

        //run_log_MMdd_HHmmss.xls
        string timestamp = System.DateTime.Now.ToString("MMdd_HHmmss");
        path = Path.Combine(folderPath, $"{baseFileName}_{timestamp}.xls");

        workbook = new Workbook();
        sheet = new Worksheet("Run Log");

        sheet.Cells[0, 0] = new Cell("time");
        sheet.Cells[0, 1] = new Cell("px");
        sheet.Cells[0, 2] = new Cell("py");
        sheet.Cells[0, 3] = new Cell("event");
    }

    void Update()
    {
        Log("pos");
    }

    public void Log(string evt)
    {
        if (player == null || sheet == null) return;

        float t = Time.time;
        Vector2 p = player.position;

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
