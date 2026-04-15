using UnityEngine;

//toggles visibility of collision hitboxes for debugging purposes
public class CollisionToggle : MonoBehaviour
{
    [Tooltip("Key to toggle collision hitbox visibility")]
    [SerializeField] private KeyCode toggleKey = KeyCode.J;

    private GameObject[] debugHitboxes;//array to hold references to hitbox objects
    private bool isVisible = false;//current visibility state

    //start is called before the first frame update
    private void Start()
    {
        debugHitboxes = GameObject.FindGameObjectsWithTag("Collision");
        SetHitboxesVisible(false);
    }

    //update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            SetHitboxesVisible(isVisible);
        }
    }

    //helper method to set all hitboxes active/inactive
    private void SetHitboxesVisible(bool visible)
    {
        foreach (GameObject obj in debugHitboxes)
        {
            if (obj != null)
                obj.SetActive(visible);
        }
    }
}