using Sticki.Combat;
using UnityEngine;

namespace Sticki.Player
{
    public class ShotgunShellVisual : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private GameObject shellVisual;
        [SerializeField] private bool hideOnEnable = true;

        private void OnEnable()
        {
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombat>();
            }

            if (hideOnEnable)
            {
                SetShellVisible(false);
            }

            if (combat == null)
            {
                return;
            }

            combat.OnReloadEnded += HandleReloadEnded;
            combat.OnWeaponChanged += HandleWeaponChanged;
        }

        private void OnDisable()
        {
            if (combat == null)
            {
                return;
            }

            combat.OnReloadEnded -= HandleReloadEnded;
            combat.OnWeaponChanged -= HandleWeaponChanged;
        }

        public void ShowShellEvent()
        {
            SetShellVisible(true);
        }

        public void HideShellEvent()
        {
            SetShellVisible(false);
        }

        private void HandleReloadEnded()
        {
            SetShellVisible(false);
        }

        private void HandleWeaponChanged(Combat.Config.WeaponConfig _)
        {
            SetShellVisible(false);
        }

        private void SetShellVisible(bool visible)
        {
            if (shellVisual != null)
            {
                shellVisual.SetActive(visible);
            }
        }
    }
}
