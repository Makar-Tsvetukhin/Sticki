using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private bool canControl = true;

        private CharacterController characterController;
        private IInputSource inputSource;
        private float verticalVelocity;

        public bool CanControl
        {
            get => canControl;
            set => canControl = value;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputSource = inputSourceComponent as IInputSource;

            if (inputSource == null)
            {
                Debug.LogError("PlayerMotor requires a component that implements IInputSource.", this);
            }
            if (stats == null)
            {
                Debug.LogError("PlayerMotor requires PlayerStats reference.", this);
            }
        }

        private void Update()
        {
            if (!canControl || inputSource == null || stats == null || !stats.IsReady)
            {
                return;
            }

            Vector2 moveInput = inputSource.Move.normalized;
            Vector3 worldMove = transform.right * moveInput.x + transform.forward * moveInput.y;
            float speedMultiplier = inputSource.SprintHeld ? Mathf.Max(0f, stats.SprintMultiplier) : 1f;
            Vector3 horizontalVelocity = worldMove * stats.MoveSpeed * speedMultiplier;

            bool isGrounded = characterController.isGrounded;
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (isGrounded && inputSource.JumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(stats.JumpHeight * -2f * stats.Gravity);
            }

            verticalVelocity += stats.Gravity * Time.deltaTime;

            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
