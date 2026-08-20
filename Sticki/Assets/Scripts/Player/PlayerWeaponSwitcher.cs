using Sticki.Combat;
using Sticki.Combat.Config;
using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Player
{
    public class PlayerWeaponSwitcher : MonoBehaviour
    {
        private enum WeaponSlot
        {
            Ar,
            Pistol,
            Lmg,
            Shotgun
        }

        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private GameObject arViewModelRoot;
        [SerializeField] private GameObject pistolViewModelRoot;
        [SerializeField] private GameObject lmgViewModelRoot;
        [SerializeField] private GameObject shotgunViewModelRoot;
        [SerializeField] private WeaponConfig arWeaponConfig;
        [SerializeField] private WeaponConfig pistolWeaponConfig;
        [SerializeField] private WeaponConfig lmgWeaponConfig;
        [SerializeField] private WeaponConfig shotgunWeaponConfig;
        [SerializeField] private float drawLockSeconds = 0.9f;

        private IInputSource inputSource;
        private WeaponSlot currentSlot;
        private WeaponSlot selectedPrimarySlot = WeaponSlot.Ar;
        private bool hasPrimaryWeaponSelected;
        private bool hasEquipped;

        private void Awake()
        {
            inputSource = inputSourceComponent as IInputSource;

            if (inputSource == null)
            {
                Debug.LogError("PlayerWeaponSwitcher requires an IInputSource component reference.", this);
            }
            if (combat == null)
            {
                Debug.LogError("PlayerWeaponSwitcher requires PlayerCombat reference.", this);
            }
            if (arViewModelRoot == null || pistolViewModelRoot == null || lmgViewModelRoot == null || shotgunViewModelRoot == null)
            {
                Debug.LogError("PlayerWeaponSwitcher requires all four view model root references.", this);
            }
            if (arWeaponConfig == null || pistolWeaponConfig == null || lmgWeaponConfig == null || shotgunWeaponConfig == null)
            {
                Debug.LogError("PlayerWeaponSwitcher requires all four weapon config references.", this);
            }
        }

        private void Start()
        {
            Equip(WeaponSlot.Pistol, true);
        }

        private void Update()
        {
            if (inputSource == null)
            {
                return;
            }

            if (inputSource.SelectArPressed)
            {
                Equip(WeaponSlot.Pistol, false);
                return;
            }

            if (inputSource.SelectPistolPressed && hasPrimaryWeaponSelected)
            {
                Equip(selectedPrimarySlot, false);
            }
        }

        public void ResetLoadout()
        {
            hasPrimaryWeaponSelected = false;
            selectedPrimarySlot = WeaponSlot.Ar;
            Equip(WeaponSlot.Pistol, true);
        }

        public bool SetPrimaryWeaponById(string weaponId, bool equipImmediately)
        {
            if (!TryResolvePrimarySlot(weaponId, out WeaponSlot resolvedSlot))
            {
                return false;
            }

            hasPrimaryWeaponSelected = true;
            selectedPrimarySlot = resolvedSlot;

            if (equipImmediately)
            {
                Equip(selectedPrimarySlot, true);
            }

            return true;
        }

        private void Equip(WeaponSlot slot, bool refillAmmo)
        {
            if (combat == null)
            {
                return;
            }

            if (hasEquipped && currentSlot == slot && !refillAmmo)
            {
                return;
            }

            hasEquipped = true;
            currentSlot = slot;

            if (arViewModelRoot != null)
            {
                arViewModelRoot.SetActive(slot == WeaponSlot.Ar);
            }
            if (pistolViewModelRoot != null)
            {
                pistolViewModelRoot.SetActive(slot == WeaponSlot.Pistol);
            }
            if (lmgViewModelRoot != null)
            {
                lmgViewModelRoot.SetActive(slot == WeaponSlot.Lmg);
            }
            if (shotgunViewModelRoot != null)
            {
                shotgunViewModelRoot.SetActive(slot == WeaponSlot.Shotgun);
            }

            combat.SetWeapon(GetConfigForSlot(slot), refillAmmo);
            combat.SetActionLock(drawLockSeconds);
        }

        private static bool TryResolvePrimarySlot(string weaponId, out WeaponSlot slot)
        {
            switch ((weaponId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "weapon_ar":
                    slot = WeaponSlot.Ar;
                    return true;
                case "weapon_lmg":
                    slot = WeaponSlot.Lmg;
                    return true;
                case "weapon_shotgun":
                    slot = WeaponSlot.Shotgun;
                    return true;
                default:
                    slot = default;
                    return false;
            }
        }

        private WeaponConfig GetConfigForSlot(WeaponSlot slot)
        {
            return slot switch
            {
                WeaponSlot.Ar => arWeaponConfig,
                WeaponSlot.Pistol => pistolWeaponConfig,
                WeaponSlot.Lmg => lmgWeaponConfig,
                WeaponSlot.Shotgun => shotgunWeaponConfig,
                _ => arWeaponConfig
            };
        }
    }
}
