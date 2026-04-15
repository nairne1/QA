using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
//renders an outline around the parent GameObject based on its 2D collider - created after human testing session feedback
public class OutlineRenderer : MonoBehaviour
{
    //LineRenderer component for drawing the outline
    private LineRenderer lr;

    //support BoxCollider2D, CircleCollider2D, and PolygonCollider2D
    private BoxCollider2D box;
    private CircleCollider2D circle;
    private PolygonCollider2D poly;

    //outline settings
    [SerializeField] private float width = 0.05f;
    [SerializeField] private Color lineColor = Color.red;
    [SerializeField] private int circleSegments = 32;

    private void Awake()
    {
        //get LineRenderer component
        lr = GetComponent<LineRenderer>();

        //check for colliders in parent hierarchy
        box = GetComponentInParent<BoxCollider2D>();
        circle = GetComponentInParent<CircleCollider2D>();
        poly = GetComponentInParent<PolygonCollider2D>();

        //configure LineRenderer
        lr.loop = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = false;

        //use a simple unlit material for the outline
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingOrder = 999;

        lr.startColor = lineColor;
        lr.endColor = lineColor;

        UpdateOutline();
    }

    //redraw outline if collider properties change in editor
    private void UpdateOutline()
    {
        if (box != null)
        {
            DrawBox();
        }
        else if (circle != null)
        {
            DrawCircle();
        }
        else if (poly != null)
        {
            DrawPolygon();
        }
    }

    //draw outline for BoxCollider2D
    private void DrawBox()
    {
        //BoxCollider2D is defined by its size and offset from the GameObject's position
        lr.positionCount = 4;

        Vector2 size = box.size;
        Vector2 offset = box.offset;

        Vector3[] corners = new Vector3[4];

        //calculate the 4 corners of the box based on size and offset
        corners[0] = new Vector3(offset.x - size.x / 2, offset.y - size.y / 2);
        corners[1] = new Vector3(offset.x - size.x / 2, offset.y + size.y / 2);
        corners[2] = new Vector3(offset.x + size.x / 2, offset.y + size.y / 2);
        corners[3] = new Vector3(offset.x + size.x / 2, offset.y - size.y / 2);

        lr.SetPositions(corners);
    }

    private void DrawCircle()
    {
        //CircleCollider2D is defined by its radius and offset from the GameObject's position
        lr.positionCount = circleSegments;

        //account for lossy scale of parent objects to ensure outline matches collider size
        Vector3 scale = transform.parent.lossyScale;

        //use the larger of the x and y scale to maintain a circular outline even if the parent is stretched
        float radius = circle.radius * Mathf.Max(scale.x, scale.y);
        Vector2 offset = Vector2.Scale(circle.offset, new Vector2(scale.x, scale.y));

        //calculate points around the circumference of the circle
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (i / (float)circleSegments) * Mathf.PI * 2f;

            float x = Mathf.Cos(angle) * radius + offset.x;
            float y = Mathf.Sin(angle) * radius + offset.y;

            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void DrawPolygon()
    {
        //PolygonCollider2D can have multiple paths, but we'll just use the first one for the outline
        Vector2[] points = poly.GetPath(0);

        //account for lossy scale of parent objects to ensure outline matches collider size
        lr.positionCount = points.Length;

        //scale points by lossy scale of parent objects
        for (int i = 0; i < points.Length; i++)
        {
            lr.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
        }
    }
}