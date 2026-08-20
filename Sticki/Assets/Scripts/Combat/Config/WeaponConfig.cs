using UnityEngine;

namespace Sticki.Combat.Config
{
    public enum WeaponReloadType
    {
        Magazine = 0,
        ShellByShell = 1
    }

    [CreateAssetMenu(menuName = "Sticki/Combat/Weapon Config", fileName = "WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Ballistics")]
        [Min(0.1f)] public float damage = 12f;
        [Min(0.01f)] public float fireIntervalSeconds = 0.25f;
        [Min(1f)] public float range = 100f;
        [Min(1)] public int pelletsPerShot = 1;
        [Min(0f)] public float spreadAngle = 0f;

        [Header("Ammo")]
        [Min(1)] public int magazineSize = 12;
        public bool infiniteReserveAmmo = false;
        [Min(0)] public int initialReserveAmmo = 0;
        public WeaponReloadType reloadType = WeaponReloadType.Magazine;
        [Min(0f)] public float reloadSeconds = 1.2f;

        [Header("Shell Reload")]
        [Min(0f)] public float reloadStartSeconds = 0.25f;
        [Min(0f)] public float reloadInsertSeconds = 0.35f;
        [Min(0f)] public float reloadEndSeconds = 0.25f;
        [Min(0f)] public float reloadShellAppearStartDelay = 0f;
        [Min(0f)] public float reloadShellAppearInsertDelay = 0f;
    }
}
