using Sticki.Combat;
using UnityEngine;

namespace Sticki.Player
{
    public class WeaponMuzzleFlash : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private Vector2 randomZRotation = new(-18f, 18f);

        private void Awake()
        {
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombat>();
            }

            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        private void OnEnable()
        {
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombat>();
            }

            if (combat != null)
            {
                combat.OnShot += Play;
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.OnShot -= Play;
            }
        }

        private void Play()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(randomZRotation.x, randomZRotation.y));

            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }
}
