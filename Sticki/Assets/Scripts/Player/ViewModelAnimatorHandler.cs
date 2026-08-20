using Sticki.Combat;
using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Player
{
    public class ViewModelAnimatorHandler : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float drawLockSeconds = 0.9f;
        [SerializeField] private float inspectLockSeconds = 0.8f;

        private IInputSource inputSource;
        private float actionLockUntil;

        private bool hasSpeedParam;
        private bool hasIsRunningParam;
        private bool hasFireParam;
        private bool hasReloadParam;
        private bool hasReloadStartParam;
        private bool hasReloadInsertParam;
        private bool hasReloadEndParam;
        private bool hasDrawParam;
        private bool hasInspectParam;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int ReloadStartHash = Animator.StringToHash("ReloadStart");
        private static readonly int ReloadInsertHash = Animator.StringToHash("ReloadInsert");
        private static readonly int ReloadEndHash = Animator.StringToHash("ReloadEnd");
        private static readonly int DrawHash = Animator.StringToHash("Draw");
        private static readonly int InspectHash = Animator.StringToHash("Inspect");

        private void OnEnable()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (combat == null) combat = GetComponentInParent<PlayerCombat>();
            if (characterController == null) characterController = GetComponentInParent<CharacterController>();

            if (motor != null)
            {
                inputSource = motor.GetComponent<IInputSource>();
            }

            if (combat != null)
            {
                combat.OnShot += HandleShot;
                combat.OnReloadStarted += HandleReloadStarted;
                combat.OnReloadShellInserted += HandleReloadInsert;
                combat.OnReloadEnded += HandleReloadEnd;
            }

            CacheAnimatorParameters();

            if (combat != null)
            {
                combat.SetActionLock(drawLockSeconds);
            }

            actionLockUntil = Time.time + drawLockSeconds;
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.OnShot -= HandleShot;
                combat.OnReloadStarted -= HandleReloadStarted;
                combat.OnReloadShellInserted -= HandleReloadInsert;
                combat.OnReloadEnded -= HandleReloadEnd;
            }
        }

        private void Update()
        {
            if (animator == null || characterController == null) return;

            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            float speed = horizontalVelocity.magnitude;

            if (hasSpeedParam)
            {
                animator.SetFloat(SpeedHash, speed);
            }

            bool isRunning = inputSource != null ? inputSource.SprintHeld && speed > 0.1f : speed > 6f;
            if (hasIsRunningParam)
            {
                animator.SetBool(IsRunningHash, isRunning);
            }

            if (inputSource != null && inputSource.InspectPressed)
            {
                TriggerInspect();
            }
        }

        private void HandleShot()
        {
            if (animator != null && hasFireParam)
            {
                animator.SetTrigger(FireHash);
            }
        }

        private void HandleReloadStarted()
        {
            if (animator == null)
            {
                return;
            }

            ResetConflictingActionTriggers();

            if (hasReloadStartParam)
            {
                animator.SetTrigger(ReloadStartHash);
                return;
            }

            if (hasReloadParam)
            {
                animator.SetTrigger(ReloadHash);
            }
        }

        private void HandleReloadInsert()
        {
            if (animator == null)
            {
                return;
            }

            ResetConflictingActionTriggers();
            if (hasReloadInsertParam)
            {
                animator.SetTrigger(ReloadInsertHash);
            }
        }

        private void HandleReloadEnd()
        {
            if (animator == null)
            {
                return;
            }

            ResetConflictingActionTriggers();
            if (hasReloadEndParam)
            {
                animator.SetTrigger(ReloadEndHash);
            }
        }

        public void TriggerInspect()
        {
            if (Time.time < actionLockUntil)
            {
                return;
            }

            if (animator != null && hasInspectParam)
            {
                animator.SetTrigger(InspectHash);
            }

            if (combat != null)
            {
                combat.SetActionLock(inspectLockSeconds);
            }

            actionLockUntil = Time.time + inspectLockSeconds;
        }

        private void CacheAnimatorParameters()
        {
            hasSpeedParam = HasParameter(SpeedHash, AnimatorControllerParameterType.Float);
            hasIsRunningParam = HasParameter(IsRunningHash, AnimatorControllerParameterType.Bool);
            hasFireParam = HasParameter(FireHash, AnimatorControllerParameterType.Trigger);
            hasReloadParam = HasParameter(ReloadHash, AnimatorControllerParameterType.Trigger);
            hasReloadStartParam = HasParameter(ReloadStartHash, AnimatorControllerParameterType.Trigger);
            hasReloadInsertParam = HasParameter(ReloadInsertHash, AnimatorControllerParameterType.Trigger);
            hasReloadEndParam = HasParameter(ReloadEndHash, AnimatorControllerParameterType.Trigger);
            hasDrawParam = HasParameter(DrawHash, AnimatorControllerParameterType.Trigger);
            hasInspectParam = HasParameter(InspectHash, AnimatorControllerParameterType.Trigger);
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetConflictingActionTriggers()
        {
            if (animator == null)
            {
                return;
            }

            if (hasFireParam)
            {
                animator.ResetTrigger(FireHash);
            }

            if (hasInspectParam)
            {
                animator.ResetTrigger(InspectHash);
            }

            if (hasDrawParam)
            {
                animator.ResetTrigger(DrawHash);
            }
        }
    }
}
