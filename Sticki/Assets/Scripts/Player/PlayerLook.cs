using Sticki.Core;
using Sticki.Core.Interfaces;
using UnityEngine;

namespace Sticki.Player
{
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private bool canControl = true;

        private IInputSource inputSource;
        private float pitch;

        public bool CanControl
        {
            get => canControl;
            set => canControl = value;
        }

        private void Awake()
        {
            inputSource = inputSourceComponent as IInputSource;
            if (inputSource == null)
            {
                Debug.LogError("PlayerLook requires a component that implements IInputSource.", this);
            }
            if (stats == null)
            {
                Debug.LogError("PlayerLook requires PlayerStats reference.", this);
            }
            if (cameraPivot == null)
            {
                Debug.LogError("PlayerLook requires cameraPivot reference.", this);
            }
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            if (!canControl || inputSource == null || stats == null || !stats.IsReady || cameraPivot == null)
            {
                return;
            }

            Vector2 lookInput = inputSource.Look * stats.LookSensitivity * GameSettingsService.GetMouseSensitivityMultiplier();

            transform.Rotate(Vector3.up * lookInput.x);

            pitch -= lookInput.y;
            pitch = Mathf.Clamp(pitch, -stats.MaxPitchAngle, stats.MaxPitchAngle);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
