using UnityEngine;

namespace Sticki.Player.Config
{
    [CreateAssetMenu(menuName = "Sticki/Player/Base Config", fileName = "PlayerBaseConfig")]
    public class PlayerBaseConfig : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 5.5f;
        [Min(0f)] public float sprintMultiplier = 1.4f;
        [Min(0f)] public float jumpHeight = 1.5f;
        [Min(0f)] public float gravity = -20f;

        [Header("Look")]
        [Min(0.01f)] public float lookSensitivity = 1.2f;
        [Range(1f, 89f)] public float maxPitchAngle = 80f;

        [Header("Vitality")]
        [Min(1f)] public float maxHealth = 100f;
    }
}
