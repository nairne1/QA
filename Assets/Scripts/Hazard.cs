using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    [SerializeField] bool isBug = false;
    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer("Hazard");
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isBug)
        {
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerAgent>();
                if (player) player.Kill();

                if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("hazard");
            }
        }
        else {
            if (SimpleRunLogger.Instance) SimpleRunLogger.Instance.Log("bugged hazard");
        }
    }
}
