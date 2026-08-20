using UnityEngine;

namespace Sticki.Player
{
    [ExecuteInEditMode]
    public class MuzzleAlignmentHelper : MonoBehaviour
    {
        [SerializeField] private bool showLine = true;
        [SerializeField] private float length = 50f;
        [SerializeField] private Color lineColor = Color.red;
        
        private LineRenderer lineRenderer;

        private void OnEnable()
        {
            UpdateLineRenderer();
        }

        private void Update()
        {
            if (!showLine)
            {
                if (lineRenderer != null) lineRenderer.enabled = false;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                // In some cases Camera.main might be null in Edit Mode or if not tagged
                cam = GameObject.FindObjectOfType<Camera>();
            }

            if (cam == null) return;

            UpdateLineRenderer();
            
            Vector3 start = transform.position;
            // Precise center of the screen at a distance
            Vector3 end = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, length));
            
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private void UpdateLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            
            if (lineRenderer.sharedMaterial == null)
            {
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
        }

        private void OnDisable()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
        }
    }
}
