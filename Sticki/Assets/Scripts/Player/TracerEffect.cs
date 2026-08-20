using UnityEngine;

namespace Sticki.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class TracerEffect : MonoBehaviour
    {
        [SerializeField] private float speed = 250f;
        [SerializeField] private float tailLength = 2f;
        private LineRenderer lineRenderer;
        
        private Vector3 startPoint;
        private Vector3 endPoint;
        private float distance;
        private float travelTime;
        private float startTime;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        public void Setup(Vector3 start, Vector3 end)
        {
            startPoint = start;
            endPoint = end;
            distance = Vector3.Distance(start, end);
            travelTime = distance / speed;
            startTime = Time.time;
            
            // Initialize both positions at start
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, start);
        }

        private void Update()
        {
            float elapsed = Time.time - startTime;
            
            // Calculate head position progress
            float headT = travelTime > 0 ? Mathf.Clamp01(elapsed / travelTime) : 1f;
            Vector3 headPos = Vector3.Lerp(startPoint, endPoint, headT);
            
            // Calculate tail position progress (delayed by tailLength)
            float tailTimeOffset = tailLength / speed;
            float tailT = travelTime > 0 ? Mathf.Clamp01((elapsed - tailTimeOffset) / travelTime) : 1f;
            Vector3 tailPos = Vector3.Lerp(startPoint, endPoint, tailT);
            
            lineRenderer.SetPosition(0, tailPos);
            lineRenderer.SetPosition(1, headPos);
        }

        public float CalculateDuration(Vector3 start, Vector3 end)
        {
            float dist = Vector3.Distance(start, end);
            return (dist / speed) + (tailLength / speed) + 0.1f; // +0.1 for safety
        }
    }
}

