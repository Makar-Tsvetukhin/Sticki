using Sticki.Combat;
using UnityEngine;

namespace Sticki.Player
{
    public class WeaponTracer : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private GameObject tracerPrefab;
        [SerializeField] private Transform muzzlePoint;

        private void OnEnable()
        {
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombat>();
            }

            if (muzzlePoint == null)
            {
                muzzlePoint = transform;
            }

            if (combat != null)
            {
                combat.OnTracerGenerated += SpawnTracer;
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.OnTracerGenerated -= SpawnTracer;
            }
        }

        private void SpawnTracer(Vector3 start, Vector3 end)
        {
            if (tracerPrefab == null) return;

            // Use the target point (end) and current muzzle position, 
            // but ensure the tracer object is oriented toward the hit point.
            var tracerComponent = tracerPrefab.GetComponent<TracerEffect>();
            float duration = tracerComponent != null ? tracerComponent.CalculateDuration(muzzlePoint.position, end) : 0.1f;

            GameObject tracerObj = VfxPoolService.Spawn(tracerPrefab, muzzlePoint.position, Quaternion.identity, duration);
            if (tracerObj == null) return;
            
            var tracer = tracerObj.GetComponent<TracerEffect>();
            if (tracer != null)
            {
                // We keep the visual start at the muzzle, but the path is already set by PlayerCombat's hitscan logic.
                // This will still look like it's coming from the gun, but won't "bend" if the barrel isn't perfectly aligned.
                tracer.Setup(muzzlePoint.position, end);
            }
        }
    }
}

