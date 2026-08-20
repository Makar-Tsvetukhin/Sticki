using UnityEngine;

namespace Sticki.Player
{
    public class PlayerDeathController : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerControlCoordinator controlCoordinator;

        private void Awake()
        {
            if (health == null || controlCoordinator == null)
            {
                Debug.LogError("PlayerDeathController requires PlayerHealth and PlayerControlCoordinator references.", this);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= HandleDied;
            }
        }

        private void HandleDied()
        {
            if (controlCoordinator != null)
            {
                controlCoordinator.SetControlEnabled(false);
            }
        }
    }
}
