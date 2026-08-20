using Sticki.Combat;
using UnityEngine;

namespace Sticki.Player
{
    public class PlayerControlCoordinator : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private PlayerCombat combat;

        public bool IsControlEnabled { get; private set; } = true;

        private void Awake()
        {
            if (motor == null || look == null || combat == null)
            {
                Debug.LogError("PlayerControlCoordinator requires PlayerMotor, PlayerLook and PlayerCombat references.", this);
            }
        }

        public void SetControlEnabled(bool enabled)
        {
            IsControlEnabled = enabled;

            if (motor != null)
            {
                motor.CanControl = enabled;
            }
            if (look != null)
            {
                look.CanControl = enabled;
            }
            if (combat != null)
            {
                combat.CanControl = enabled;
            }
        }
    }
}
