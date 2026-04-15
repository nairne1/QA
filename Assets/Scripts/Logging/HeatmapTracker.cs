using UnityEngine;
using System.IO;

public class HeatmapTracker : MonoBehaviour
{
    [Header("Tracking")]
    public Transform targetToTrack;
    public BoxCollider2D levelBounds;

    [Header("Sampling")]
    [Tooltip("Record every N frames. 1 = every frame.")]
    public int sampleEveryNFrames = 3;

    [Header("Heatmap")]
    [Tooltip("Pixels per world unit.")]
    public int pixelsPerUnit = 16;

    [Range(0.001f, 0.25f)]
    public float alphaPerVisit = 0.02f;

    [Range(0.05f, 1f)]
    public float maxAlpha = 0.85f;

    public Color heatColor = Color.red;

    [Header("Brush")]
    [Tooltip("Size of the painted square in pixels.")]
    public int brushSize = 3;

    [Header("Overlay")]
    public string overlaySortingLayer = "Default";
    public int overlayOrderInLayer = 50;
    public float overlayZOffset = 0f;

    [Header("Visibility")]
    [Tooltip("Show heatmap overlay at start")]
    public bool showOnStart = false;
    [Tooltip("Key to toggle heatmap visibility")]
    public KeyCode toggleVisibilityKey = KeyCode.H;

    [Header("Save/Load")]
    [Tooltip("Folder path relative to Assets folder, e.g., 'Heatmaps'")]
    public string saveFolderPath = "Heatmaps";
    
    [Tooltip("Press to save the current heatmap")]
    public KeyCode saveKey = KeyCode.F5;

    [Header("Tracking Control")]
    [Tooltip("Track during menu/pause")]
    public bool trackWhenPaused = false;
    public bool trackWhenMenu = false;

    [Tooltip("Persist heatmap across episode resets (for ML-Agents training)")]
    public bool persistAcrossEpisodes = false;

    [Tooltip("Only check pause state if GameManager exists")]
    public bool requireGameManager = true;

    //state
    private Texture2D _texture;
    private SpriteRenderer _spriteRenderer;
    private Sprite _sprite;
    private Color[] _pixels;

    private Bounds _bounds;
    private int _texWidth;
    private int _texHeight;

    private int _frameCounter = 0;
    private bool _initialized = false;
    private bool _isVisible = true;

    private SimplifiedCoverage _aiAgent;
    private HumanPlayerController _humanPlayer;

    private void Start()
    {
        //basic validation
        if (targetToTrack == null)
        {
            Debug.LogError("HeatmapOverlayOnly: targetToTrack not assigned.");
            enabled = false;
            return;
        }

        if (levelBounds == null)
        {
            Debug.LogError("HeatmapOverlayOnly: levelBounds not assigned.");
            enabled = false;
            return;
        }
        //detect if tracking an AI agent or human player
        _aiAgent = targetToTrack.GetComponent<SimplifiedCoverage>();
        _humanPlayer = targetToTrack.GetComponent<HumanPlayerController>();

        if (_aiAgent == null && _humanPlayer == null)
        {
            Debug.LogWarning("HeatmapTracker: Target has neither SimplifiedCoverage nor HumanPlayerController component.");
        }


        InitialiseHeatmap();

        PaintAtWorldPosition(targetToTrack.position);
        PaintAtWorldPosition((Vector2)targetToTrack.position + Vector2.right * 0.5f);
        PaintAtWorldPosition((Vector2)targetToTrack.position + Vector2.left * 0.5f);

        //set initial visibility
        _isVisible = showOnStart;
        UpdateVisibility();

        //subscribe to episode reset events if not persisting across episodes
        if (!persistAcrossEpisodes)
        {
            // Subscribe to AI agent episode reset
            if (_aiAgent != null)
            {
                SimplifiedCoverage.OnAgentRespawn += OnAgentEpisodeBegin;
            }

            // Subscribe to human player session reset
            if (_humanPlayer != null)
            {
                HumanPlayerController.OnHumanPlayerRespawn += OnHumanPlayerRespawn;
            }
        }
    }

    private void OnDestroy()
    {
        //unsubscribe from AI agent events
        if (_aiAgent != null && !persistAcrossEpisodes)
        {
            SimplifiedCoverage.OnAgentRespawn -= OnAgentEpisodeBegin;
        }

        //unsubscribe from human player events
        if (_humanPlayer != null && !persistAcrossEpisodes)
        {
            HumanPlayerController.OnHumanPlayerRespawn -= OnHumanPlayerRespawn;
        }
    }

    //called when AI agent resets episode
    private void OnAgentEpisodeBegin()
    {
        //clear heatmap when agent resets episode
        if (!persistAcrossEpisodes)
        {
            //ClearHeatmap();
            Debug.Log("Heatmap not cleared for new episode");
        }
    }

    //called when human player respawns
    private void OnHumanPlayerRespawn()
    {
        if (!persistAcrossEpisodes)
        {
            Debug.Log("[HeatmapTracker] Human player respawned");
        }
    }

    //initialises the heatmap texture and overlay
    private void InitialiseHeatmap()
    {
        _bounds = levelBounds.bounds;

        //show bounds info for debugging
        Debug.Log($"LevelBounds GameObject: {levelBounds.gameObject.name}");
        Debug.Log($"LevelBounds Position: {levelBounds.transform.position}");
        Debug.Log($"LevelBounds Size: {levelBounds.size}");
        Debug.Log($"LevelBounds Offset: {levelBounds.offset}");
        Debug.Log($"Calculated World Bounds: min={_bounds.min}, max={_bounds.max}, center={_bounds.center}, size={_bounds.size}");

        //calculate texture size based on bounds and pixels per unit
        _texWidth = Mathf.Max(1, Mathf.CeilToInt(_bounds.size.x * pixelsPerUnit));
        _texHeight = Mathf.Max(1, Mathf.CeilToInt(_bounds.size.y * pixelsPerUnit));

        //create texture
        _texture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false);
        _texture.filterMode = FilterMode.Point;
        _texture.wrapMode = TextureWrapMode.Clamp;

        //initialise pixel array to transparent
        _pixels = new Color[_texWidth * _texHeight];
        for (int i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        _texture.SetPixels(_pixels);
        _texture.Apply();

        //create overlay sprite
        GameObject overlayObject = new GameObject("HeatmapOverlay");
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.position = new Vector3(
            _bounds.center.x,
            _bounds.center.y,
            overlayZOffset
        );

        //add SpriteRenderer
        _spriteRenderer = overlayObject.AddComponent<SpriteRenderer>();
        _spriteRenderer.sortingLayerName = overlaySortingLayer;
        _spriteRenderer.sortingOrder = overlayOrderInLayer;

        //create sprite from texture
        _sprite = Sprite.Create(
            _texture,
            new Rect(0, 0, _texWidth, _texHeight),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        //assign sprite to renderer
        _spriteRenderer.sprite = _sprite;
        _spriteRenderer.color = Color.white;

        //log heatmap creation
        Debug.Log($"Heatmap created: tex={_texWidth}x{_texHeight}, bounds={_bounds.size}, center={_bounds.center}");

        _initialized = true;
    }

    //main update loop - checks for input and tracks position
    private void Update()
    {
        if (Input.GetKeyDown(saveKey))
        {
            SaveHeatmap();
        }

        if (Input.GetKeyDown(toggleVisibilityKey))
        {
            ToggleVisibility();
        }
    }

    //LateUpdate is used to ensure we track the target's position after it has moved for the frame
    private void LateUpdate()
    {
        if (!_initialized || targetToTrack == null)
            return;

        //skip tracking if paused (only if GameManager exists and we require it)
        if (!trackWhenPaused && requireGameManager)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                return;
        }

        _frameCounter++;
        if (_frameCounter < sampleEveryNFrames)
            return;

        _frameCounter = 0;

        Vector2 worldPos = targetToTrack.position;
        PaintAtWorldPosition(worldPos);
    }

    //paints the heatmap at the given world position
    private void PaintAtWorldPosition(Vector2 worldPos)
    {
        //2D bounds check
        if (worldPos.x < _bounds.min.x || worldPos.x > _bounds.max.x ||
            worldPos.y < _bounds.min.y || worldPos.y > _bounds.max.y)
        {
            return;
        }

        //convert world position to normalized 0-1 based on bounds
        float nx = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, worldPos.x);
        float ny = Mathf.InverseLerp(_bounds.min.y, _bounds.max.y, worldPos.y);

        //convert normalized position to pixel coordinates
        int px = Mathf.Clamp(Mathf.FloorToInt(nx * _texWidth), 0, _texWidth - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(ny * _texHeight), 0, _texHeight - 1);

        int halfBrush = Mathf.Max(0, brushSize / 2);

        //paint a square brush around the pixel coordinates
        for (int y = py - halfBrush; y <= py + halfBrush; y++)
        {
            for (int x = px - halfBrush; x <= px + halfBrush; x++)
            {
                if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight)
                    continue;

                int index = y * _texWidth + x;
                Color current = _pixels[index];
                float newAlpha = Mathf.Clamp(current.a + alphaPerVisit, 0f, maxAlpha);

                _pixels[index] = new Color(heatColor.r, heatColor.g, heatColor.b, newAlpha);
            }
        }

        //apply updated pixels to texture
        _texture.SetPixels(_pixels);
        _texture.Apply(false);
    }

    //toggles heatmap visibility on/off
    public void ToggleVisibility()
    {
        _isVisible = !_isVisible;
        UpdateVisibility();
        Debug.Log($"Heatmap visibility: {(_isVisible ? "ON" : "OFF")}");
    }

    //sets heatmap visibility explicitly
    public void SetVisibility(bool visible)
    {
        _isVisible = visible;
        UpdateVisibility();
    }

    //updates the SpriteRenderer's enabled state based on _isVisible
    private void UpdateVisibility()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = _isVisible;
        }
    }

    //saves the heatmap texture to a PNG file in the specified folder
    public void SaveHeatmap()
    {
        if (_texture == null)
        {
            Debug.LogError("No heatmap texture to save.");
            return;
        }

        //ensure save folder exists
        string folderPath = Path.Combine(Application.dataPath, saveFolderPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        //generate filename with timestamp
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"Heatmap_{timestamp}.png";
        string fullPath = Path.Combine(folderPath, filename);

        byte[] pngData = _texture.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngData);

        Debug.Log($"Heatmap saved to: {fullPath}");

        //refresh the AssetDatabase so the new file appears in the Unity Editor
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        
        // Configure the texture import settings
        string assetPath = $"Assets/{saveFolderPath}/{filename}";
        UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
        
        if (importer != null)
        {
            importer.textureType = UnityEditor.TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            
            Debug.Log($"Texture import settings configured: PPU={pixelsPerUnit}");
        }
#endif
    }

    //overload to save with custom name instead of timestamp
    public void SaveHeatmapWithName(string customName)
    {
        if (_texture == null)
        {
            Debug.LogError("No heatmap texture to save.");
            return;
        }

        string folderPath = Path.Combine(Application.dataPath, saveFolderPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filename = $"{customName}.png";
        string fullPath = Path.Combine(folderPath, filename);

        byte[] pngData = _texture.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngData);

        Debug.Log($"Heatmap saved to: {fullPath}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        
        // Configure the texture import settings
        string assetPath = $"Assets/{saveFolderPath}/{filename}";
        UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
        
        if (importer != null)
        {
            importer.textureType = UnityEditor.TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            
            Debug.Log($"Texture import settings configured: PPU={pixelsPerUnit}");
        }
#endif
    }
    //clears the heatmap by resetting all pixels to transparent
    public void ClearHeatmap()
    {
        if (_texture == null || _pixels == null) return;

        for (int i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        _texture.SetPixels(_pixels);
        _texture.Apply();
    }

    private void OnApplicationQuit()
    {
        // Auto-save on quit
        SaveHeatmap();
    }
}