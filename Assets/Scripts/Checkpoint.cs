using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    public bool oneTime = true;
    public bool isBug = false;
    [HideInInspector] public static bool activated;

    void Reset()
    {
        gameObject.tag = "Checkpoint";
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated && oneTime) return;
        if (!isBug)
        {
            if (other.CompareTag("Player"))
            {
                RespawnManager.Instance.SetCheckpoint(transform.position);
                if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("checkpoint");
                activated = true;
            }
        }
        else {
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("bugged checkpoint");
        }
    }
}
