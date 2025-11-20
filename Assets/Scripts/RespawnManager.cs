using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance;

    public Transform initialSpawnPoint;

    Vector3 currentSpawn;

    void Awake()
    {
        Instance = this;
        currentSpawn = initialSpawnPoint.position;
    }

    public void SetCheckpoint(Vector3 position)
    {
        currentSpawn = position;
    }

    public void Respawn(PlayerAgent player)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        player.transform.SetParent(null);
        player.transform.position = currentSpawn;
    }
}
