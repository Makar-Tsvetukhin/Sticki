using Sticki.Core.Interfaces;
using Sticki.Player;
using Sticki.Spawning;
using Sticki.Upgrades;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Sticki.Core
{
    public class ArenaExitDoor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ArenaSpawner spawner;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private SceneTransitionHelper sceneTransitionHelper;
        [SerializeField] private UpgradeRuntimeController upgradeRuntimeController;

        [Header("Transition")]
        [SerializeField] private string combatExitSceneName = "UpgradeRoom";

        [Header("Interaction")]
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactRayMask = ~0;
        [SerializeField] [Range(0.5f, 0.99f)] private float aimDotThreshold = 0.9f;

        [Header("Highlight")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private Renderer[] highlightRenderers;
        [SerializeField] private Color pulseColor = Color.white;
        [SerializeField] private Color targetedColor = new(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private float pulseSpeed = 1.6f;
        [SerializeField] private float pulseMinIntensity = 0.12f;
        [SerializeField] private float pulseMaxIntensity = 2f;
        [SerializeField] private float targetedIntensity = 0.22f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;
        [SerializeField] private bool unlockedByDefault;

        public UnityEvent OnExitRequested;

        private Transform playerTransform;
        private IInputSource inputSource;
        private HudController hud;
        private bool unlocked;
        private bool exitTriggered;
        private bool spawnerSubscribed;
        private float nextPlayerResolveTime;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (spawner == null)
            {
                spawner = FindFirstObjectByType<ArenaSpawner>();
            }

            if (sceneTransitionHelper == null)
            {
                sceneTransitionHelper = FindFirstObjectByType<SceneTransitionHelper>();
            }

            if (upgradeRuntimeController == null)
            {
                upgradeRuntimeController = FindFirstObjectByType<UpgradeRuntimeController>();
            }

            unlocked = unlockedByDefault || IsUpgradeRoomDoor();

            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (highlightObject != null)
            {
                highlightObject.SetActive(unlocked);
            }

            TryResolvePlayerReference();
            hud = FindFirstObjectByType<HudController>();

            propertyBlock = new MaterialPropertyBlock();
            SetEmission(Color.black);

            if (unlocked)
            {
                UpdateEmission(false);
            }
        }

        private void OnEnable()
        {
            TryBindSpawner();
        }

        private void OnDisable()
        {
            if (spawner != null && spawnerSubscribed)
            {
                spawner.OnArenaCleared -= HandleArenaCleared;
                spawnerSubscribed = false;
            }
        }

        private void Update()
        {
            if (spawner == null || !spawnerSubscribed)
            {
                TryBindSpawner();
            }

            if (!unlocked || exitTriggered)
            {
                return;
            }

            if (playerTransform == null || inputSource == null)
            {
                TryResolvePlayerReference();
            }

            bool inRange = IsPlayerInRange();
            bool targeted = inRange && IsDoorTargeted();

            if (targeted && inputSource != null && inputSource.InteractPressed)
            {
                TriggerExit();
                return;
            }

            UpdateEmission(targeted);
            UpdatePrompt(targeted);
        }

        private void TryBindSpawner()
        {
            if (IsUpgradeRoomDoor())
            {
                return;
            }

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<ArenaSpawner>();
            }

            if (spawner == null || spawnerSubscribed)
            {
                return;
            }

            spawner.OnArenaCleared += HandleArenaCleared;
            spawnerSubscribed = true;

            if (debugLogs)
            {
                Debug.Log($"ArenaExitDoor: subscribed to spawner '{spawner.name}'.", this);
            }
        }

        private void HandleArenaCleared()
        {
            if (debugLogs)
            {
                Debug.Log("ArenaExitDoor: arena cleared -> unlock.", this);
            }

            UnlockDoor();
        }

        private void UnlockDoor()
        {
            if (unlocked)
            {
                return;
            }

            unlocked = true;
            if (highlightObject != null)
            {
                highlightObject.SetActive(true);
            }

            UpdateEmission(false);
            UpdatePrompt(false);
        }

        private void TriggerExit()
        {
            exitTriggered = true;
            UpdatePrompt(false);

            OnExitRequested?.Invoke();

            if (IsUpgradeRoomDoor())
            {
                upgradeRuntimeController?.ResetRoomOptionCache();
                sceneTransitionHelper?.LoadNextArena();
                return;
            }

            sceneTransitionHelper?.LoadScene(combatExitSceneName);
        }

        private void TryResolvePlayerReference()
        {
            if (Time.time < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.time + 0.5f;

            PlayerHealth playerHealth = PlayerHealth.Instance;
            if (playerHealth == null)
            {
                return;
            }

            playerTransform = playerHealth.transform;
            inputSource = playerHealth.GetComponentInParent<IInputSource>();
            if (inputSource == null)
            {
                inputSource = playerHealth.GetComponentInChildren<IInputSource>();
            }
        }

        private bool IsPlayerInRange()
        {
            if (playerTransform == null)
            {
                return false;
            }

            Vector3 toPlayer = playerTransform.position - transform.position;
            float maxDistance = Mathf.Max(0.1f, interactDistance);
            return toPlayer.sqrMagnitude <= maxDistance * maxDistance;
        }

        private bool IsDoorTargeted()
        {
            if (raycastCamera == null)
            {
                return false;
            }

            Vector3 camPos = raycastCamera.transform.position;
            Vector3 camForward = raycastCamera.transform.forward;

            Vector3 toDoor = GetAimPoint() - camPos;
            float distance = toDoor.magnitude;
            if (distance <= 0.0001f)
            {
                return false;
            }

            Vector3 toDoorDir = toDoor / distance;
            float dot = Vector3.Dot(camForward.normalized, toDoorDir);
            if (dot < aimDotThreshold)
            {
                return false;
            }

            if (Physics.Raycast(camPos, camForward, out RaycastHit hit, interactDistance, interactRayMask, QueryTriggerInteraction.Collide))
            {
                return hit.collider != null && hit.collider.transform.IsChildOf(transform);
            }

            return true;
        }

        private Vector3 GetAimPoint()
        {
            Renderer renderer = highlightRenderers != null && highlightRenderers.Length > 0 ? highlightRenderers[0] : null;
            if (renderer != null)
            {
                return renderer.bounds.center;
            }

            return transform.position;
        }

        private void UpdateEmission(bool targeted)
        {
            if (!unlocked || highlightRenderers == null || highlightRenderers.Length == 0)
            {
                return;
            }

            Color emission;
            if (targeted)
            {
                emission = targetedColor * Mathf.Max(0.01f, targetedIntensity);
            }
            else if (IsUpgradeRoomDoor())
            {
                emission = Color.black;
            }
            else
            {
                emission = pulseColor * Mathf.Max(0.01f, GetPulseIntensity());
            }

            SetEmission(emission);
        }

        private void UpdatePrompt(bool targeted)
        {
            if (hud == null)
            {
                return;
            }

            if (!unlocked || exitTriggered || !targeted)
            {
                hud.SetInteractionPrompt(string.Empty, string.Empty, false);
                return;
            }

            hud.SetInteractionPrompt("E", GetPromptAction(), true);
        }

        private string GetPromptAction()
        {
            return IsUpgradeRoomDoor() ? "СЛЕДУЮЩАЯ АРЕНА" : "ОТКРЫТЬ ДВЕРЬ";
        }

        private bool IsUpgradeRoomDoor()
        {
            return SceneManager.GetActiveScene().name == "UpgradeRoom";
        }

        private float GetPulseIntensity()
        {
            float t = (Mathf.Sin(Time.time * Mathf.Max(0.1f, pulseSpeed)) + 1f) * 0.5f;
            return Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, t);
        }

        private void SetEmission(Color emission)
        {
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                Renderer renderer = highlightRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
