using System.Collections.Generic;
using UnityEngine;

public class GridCoverageTracker2D : MonoBehaviour
{
    [Header("Bounds")]
    public BoxCollider2D levelBounds;

    [Header("Grid")]
    public float cellSize = 1f;

    [Header("Ground check")]
    public LayerMask groundLayer;
    public LayerMask platformLayer;
    public float groundCheckDistance = 0.2f;

    [Header("Debug")]
    public bool showGizmos = true;
    public Color walkableColor = Color.yellow;
    public Color visitedColor = Color.green;

    //walkable cells that have been visited at least once by the player
    public int TotalWalkableCells => _walkableCells.Count;

    //visited walkable cells that have been visited at least once by the player
    public int VisitedWalkableCells => _visited.Count;

    //all walkable cells that have been visited at least once by the player
    public HashSet<int> _visited = new HashSet<int>();
    public HashSet<int> _walkableCells = new HashSet<int>();

    private Vector2 _min;
    private Vector2 _max;
    private int _cols;
    private int _rows;
    private int _totalCells;

    private void Awake()
    {
        ResetCoverage();
    }

    public void ResetCoverage()
    {
        CacheBounds();
        _visited.Clear();
        ComputeWalkableCells();
    }

    //cache the bounds and compute grid dimensions based on the levelBounds collider
    private void CacheBounds()
    {
        if (levelBounds == null)
        {
            Debug.LogError("GridCoverageTracker2D: levelBounds not set.");
            return;
        }

        Bounds b = levelBounds.bounds;
        _min = b.min;
        _max = b.max;

        _cols = Mathf.Max(1, Mathf.CeilToInt((_max.x - _min.x) / cellSize));
        _rows = Mathf.Max(1, Mathf.CeilToInt((_max.y - _min.y) / cellSize));
        _totalCells = _cols * _rows;
    }

    //compute walkable cells by raycasting down from the center of each cell to check for ground or platform
    private void ComputeWalkableCells()
    {
        _walkableCells.Clear();

        for (int i = 0; i < _totalCells; i++)
        {
            Vector2 cellCenter = CellIndexToWorldCenter(i);

            float startOffset = cellSize * 0.5f + 0.05f;
            Vector2 rayStart = cellCenter + Vector2.up * startOffset;
            float rayDistance = startOffset + groundCheckDistance;

            RaycastHit2D hitGround = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, groundLayer);
            RaycastHit2D hitPlat = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, platformLayer);

            if (hitGround.collider != null || hitPlat.collider != null)
            {
                _walkableCells.Add(i);
            }
        }
    }

    //get the cell index for a given world position, returns -1 if out of bounds
    public int GetCellIndex(Vector2 worldPos)
    {
        if (levelBounds == null) return -1;

        if (worldPos.x < _min.x || worldPos.x > _max.x || worldPos.y < _min.y || worldPos.y > _max.y)
            return -1;

        int cx = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - _min.x) / cellSize), 0, _cols - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt((worldPos.y - _min.y) / cellSize), 0, _rows - 1);

        return cy * _cols + cx;
    }

    //get the world position of the center of a cell given its index
    public Vector2 CellIndexToWorldCenter(int index)
    {
        int cx = index % _cols;
        int cy = index / _cols;

        float x = _min.x + (cx + 0.5f) * cellSize;
        float y = _min.y + (cy + 0.5f) * cellSize;

        return new Vector2(x, y);
    }

    //mark a cell as visited if it's walkable and return true, otherwise return false
    public bool TryVisitWalkable(Vector2 worldPos, out int cellIndex)
    {
        cellIndex = GetCellIndex(worldPos);

        if (cellIndex < 0) return false;
        if (!_walkableCells.Contains(cellIndex)) return false;

        return _visited.Add(cellIndex);
    }

    //get the coverage percentage of visited walkable cells over total walkable cells, returns 0 if no walkable cells
    public float GetCoverage01()
    {
        if (_walkableCells.Count == 0) return 0f;
        return (float)_visited.Count / _walkableCells.Count;
    }

    //get a normalised direction vector from the current position to the nearest unexplored walkable cell, returns Vector2.zero if all walkable cells are visited or no walkable cells
    public Vector2 GetDirectionToUnexploredWalkable(Vector2 currentPosition)
    {
        float minDistance = float.MaxValue;
        Vector2 nearestDirection = Vector2.zero;

        foreach (int cellIndex in _walkableCells)
        {
            if (_visited.Contains(cellIndex)) continue;

            Vector2 cellCenter = CellIndexToWorldCenter(cellIndex);
            float distance = Vector2.Distance(currentPosition, cellCenter);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestDirection = cellCenter - currentPosition;
            }
        }

        return nearestDirection == Vector2.zero ? Vector2.zero : nearestDirection.normalized;
    }

    //draw gizmos for walkable cells, using different colors for visited and unvisited cells
    private void OnDrawGizmos()
    {
        if (!showGizmos || levelBounds == null) return;

        CacheBounds();

        if (_walkableCells.Count == 0)
        {
            ComputeWalkableCells();
        }

        Vector3 size = new Vector3(cellSize, cellSize, 0.05f);

        foreach (int cellIndex in _walkableCells)
        {
            Vector2 center2 = CellIndexToWorldCenter(cellIndex);
            Vector3 center = new Vector3(center2.x, center2.y, 0f);

            Gizmos.color = _visited.Contains(cellIndex) ? visitedColor : walkableColor;
            Gizmos.DrawWireCube(center, size);
        }
    }
}