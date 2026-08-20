using System;
using System.Collections.Generic;
using Sticki.Core.Interfaces;
using Sticki.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sticki.UI
{
    public class UpgradeSelectionController : MonoBehaviour
    {
        public enum SelectionStation
        {
            Character,
            WeaponUpgrade,
            PrimaryWeapon
        }

        [Serializable]
        public class OptionData
        {
            public string id;
            public string title;
            [TextArea(2, 4)] public string description;
            public Sprite icon;
            public string iconText;
            public bool stackable = true;
            [Min(0)] public int maxStacks;
        }

        [Header("World References")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private Transform shieldLeft;
        [SerializeField] private Transform shieldRight;
        [SerializeField] private Transform shieldCenter;
        [SerializeField] private PlayerControlCoordinator playerControlCoordinator;
        [SerializeField] private HudController hudController;
        [SerializeField] private UpgradeSelectionUI upgradeSelectionUI;

        [Header("Interaction")]
        [SerializeField] private float interactDistance = 4f;
        [SerializeField] private LayerMask interactRayMask = ~0;
        [SerializeField] private bool oneSelectionPerShieldPerRoom = true;

        [Header("Shield Highlight")]
        [SerializeField] private Renderer[] shieldLeftRenderers;
        [SerializeField] private Renderer[] shieldRightRenderers;
        [SerializeField] private Renderer[] shieldCenterRenderers;
        [SerializeField] private Color targetedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private float targetedIntensity = 1.2f;

        [Header("Option Pools")]
        [SerializeField] private List<OptionData> primaryWeaponChoices = new();

        [Header("Generation")]
        [SerializeField] [Min(1)] private int upgradesPerRoll = 3;
        [SerializeField] private bool randomizeSeedOnAwake = true;
        [SerializeField] private int sessionSeed;

        private readonly Dictionary<string, int> characterUpgradeStacks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> weaponUpgradeStacks = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<SelectionStation> consumedStations = new();

        private Transform playerTransform;
        private IInputSource inputSource;
        private bool isPanelOpen;
        private bool wasCursorVisible;
        private CursorLockMode previousCursorLockMode;
        private SelectionStation activeStation;
        private OptionData selectedPrimaryWeapon;
        private float nextPlayerResolveTime;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        public Func<SelectionStation, int, List<OptionData>> OptionsProvider { get; set; }
        public Func<SelectionStation, OptionData, bool> SelectionValidator { get; set; }

        public event Action<OptionData> OnCharacterUpgradeSelected;
        public event Action<OptionData> OnWeaponUpgradeSelected;
        public event Action<OptionData> OnPrimaryWeaponSelected;

        public IReadOnlyDictionary<string, int> CharacterUpgradeStacks => characterUpgradeStacks;
        public IReadOnlyDictionary<string, int> WeaponUpgradeStacks => weaponUpgradeStacks;
        public OptionData SelectedPrimaryWeapon => selectedPrimaryWeapon;

        private void Reset() { EnsureDefaultPrimaryWeaponChoices(); }

        private void Awake()
        {
            EnsureDefaultPrimaryWeaponChoices();
            if (raycastCamera == null) raycastCamera = Camera.main;
            if (hudController == null) hudController = FindFirstObjectByType<HudController>();
            if (playerControlCoordinator == null) playerControlCoordinator = FindFirstObjectByType<PlayerControlCoordinator>();
            if (upgradeSelectionUI == null) upgradeSelectionUI = FindFirstObjectByType<UpgradeSelectionUI>();

            ResolvePlayerReference();
            CacheShieldRenderers();
            PrepareHighlightMaterials();
            propertyBlock = new MaterialPropertyBlock();

            int seed = sessionSeed;
            if (randomizeSeedOnAwake || seed == 0) seed = Environment.TickCount;
            sessionSeed = seed;
        }

        private void OnEnable()
        {
            if (upgradeSelectionUI != null)
            {
                upgradeSelectionUI.OnCloseRequested += ClosePanel;
                upgradeSelectionUI.OnOptionSelected += ApplySelection;
            }
        }

        private void OnDisable()
        {
            if (upgradeSelectionUI != null)
            {
                upgradeSelectionUI.OnCloseRequested -= ClosePanel;
                upgradeSelectionUI.OnOptionSelected -= ApplySelection;
            }

            if (isPanelOpen) { RestoreControlStateAfterClose(); isPanelOpen = false; }
            HidePrompt();
        }

        private void Update()
        {
            if (playerTransform == null || inputSource == null) ResolvePlayerReference();

            if (isPanelOpen)
            {
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) ClosePanel();
                UpdateShieldHighlight(false, default);
                return;
            }

            if (raycastCamera == null || playerTransform == null || inputSource == null)
            {
                UpdateShieldHighlight(false, default);
                HidePrompt();
                return;
            }

            if (!TryGetTargetedStation(out SelectionStation station))
            {
                UpdateShieldHighlight(false, default);
                HidePrompt();
                return;
            }

            bool stationConsumed = oneSelectionPerShieldPerRoom && IsSingleUseStation(station) && consumedStations.Contains(station);
            if (stationConsumed)
            {
                UpdateShieldHighlight(false, default);
                hudController?.SetInteractionPrompt("E", "УЖЕ ВЫБРАНО", true);
                return;
            }

            UpdateShieldHighlight(true, station);
            hudController?.SetInteractionPrompt("E", GetStationPrompt(station), true);
            if (inputSource.InteractPressed) OpenPanel(station);
        }

        public void SetPrimaryWeaponChoices(List<OptionData> options) { primaryWeaponChoices = CloneOptions(options); }
        public void ResetRoomSelections() { consumedStations.Clear(); }

        private void OpenPanel(SelectionStation station)
        {
            if (upgradeSelectionUI == null) return;
            activeStation = station;
            isPanelOpen = true;

            ApplyControlStateForOpen();
            string title = "", subtitle = "";
            List<OptionData> choices = null;

            switch (station)
            {
                case SelectionStation.Character:
                    title = "УЛУЧШЕНИЕ ПЕРСОНАЖА";
                    subtitle = "Выберите улучшение";
                    choices = OptionsProvider?.Invoke(station, upgradesPerRoll);
                    if (choices == null)
                    {
                        Debug.LogWarning("UpgradeSelection: Character OptionsProvider is not assigned or returned null.", this);
                        choices = new List<OptionData>();
                    }
                    break;
                case SelectionStation.WeaponUpgrade:
                    title = "УЛУЧШЕНИЕ ОРУЖИЯ";
                    subtitle = "Выберите улучшение";
                    choices = OptionsProvider?.Invoke(station, upgradesPerRoll);
                    if (choices == null)
                    {
                        Debug.LogWarning("UpgradeSelection: Weapon OptionsProvider is not assigned or returned null.", this);
                        choices = new List<OptionData>();
                    }
                    break;
                default:
                    title = "ВЫБОР ОСНОВНОГО ОРУЖИЯ";
                    subtitle = "Выберите оружие";
                    choices = CloneOptions(primaryWeaponChoices);
                    break;
            }

            upgradeSelectionUI.Setup(title, subtitle, choices);
            upgradeSelectionUI.Show();
            HidePrompt();
        }

        private void ClosePanel()
        {
            if (!isPanelOpen) return;
            isPanelOpen = false;
            upgradeSelectionUI?.Hide();
            RestoreControlStateAfterClose();
        }

        private void ApplySelection(OptionData option)
        {
            if (SelectionValidator != null && !SelectionValidator.Invoke(activeStation, option))
            {
                Debug.LogWarning($"UpgradeSelection: selection '{option?.id}' was rejected for station {activeStation}.", this);
                return;
            }

            switch (activeStation)
            {
                case SelectionStation.Character: AddStack(characterUpgradeStacks, option.id); OnCharacterUpgradeSelected?.Invoke(option); break;
                case SelectionStation.WeaponUpgrade: AddStack(weaponUpgradeStacks, option.id); OnWeaponUpgradeSelected?.Invoke(option); break;
                default: selectedPrimaryWeapon = option; OnPrimaryWeaponSelected?.Invoke(option); break;
            }

            if (oneSelectionPerShieldPerRoom && IsSingleUseStation(activeStation)) consumedStations.Add(activeStation);
            ClosePanel();
        }

        private static void AddStack(Dictionary<string, int> stacks, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            stacks.TryGetValue(id, out int value);
            stacks[id] = value + 1;
        }

        private bool TryGetTargetedStation(out SelectionStation station)
        {
            station = default;
            Transform target = GetTargetedTransform();
            if (target == null) return false;

            if (shieldLeft != null && (target == shieldLeft || target.IsChildOf(shieldLeft))) { station = SelectionStation.Character; return true; }
            if (shieldRight != null && (target == shieldRight || target.IsChildOf(shieldRight))) { station = SelectionStation.WeaponUpgrade; return true; }
            if (shieldCenter != null && (target == shieldCenter || target.IsChildOf(shieldCenter))) { station = SelectionStation.PrimaryWeapon; return true; }
            return false;
        }

        private Transform GetTargetedTransform()
        {
            if (raycastCamera == null) return null;

            Vector3 camPos = raycastCamera.transform.position;
            Vector3 camForward = raycastCamera.transform.forward;
            Vector3 rayStart = camPos - camForward * 0.5f;
            float adjustedDistance = interactDistance + 0.5f;

            if (!Physics.SphereCast(rayStart, 0.1f, camForward, out RaycastHit hit, adjustedDistance, interactRayMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider != null ? hit.collider.transform : null;
        }

        private static string GetStationPrompt(SelectionStation station)
        {
            switch (station)
            {
                case SelectionStation.Character: return "ОТКРЫТЬ УЛУЧШЕНИЯ ПЕРСОНАЖА";
                case SelectionStation.WeaponUpgrade: return "ОТКРЫТЬ УЛУЧШЕНИЯ ОРУЖИЯ";
                default: return "ВЫБРАТЬ ОСНОВНОЕ ОРУЖИЕ";
            }
        }

        private static bool IsSingleUseStation(SelectionStation station) { return station != SelectionStation.PrimaryWeapon; }

        private void ResolvePlayerReference()
        {
            if (Time.time < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.time + 0.5f;

            PlayerHealth playerHealth = PlayerHealth.Instance;
            if (playerHealth == null) return;

            playerTransform = playerHealth.transform;
            inputSource = playerHealth.GetComponentInParent<IInputSource>();
            if (inputSource == null) inputSource = playerHealth.GetComponentInChildren<IInputSource>();
        }

        private void ApplyControlStateForOpen()
        {
            wasCursorVisible = UnityEngine.Cursor.visible;
            previousCursorLockMode = UnityEngine.Cursor.lockState;
            if (playerControlCoordinator != null) playerControlCoordinator.SetControlEnabled(false);
            if (hudController != null)
            {
                hudController.SetCrosshairVisible(false);
                hudController.SetInteractionPrompt(string.Empty, string.Empty, false);
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void RestoreControlStateAfterClose()
        {
            if (playerControlCoordinator != null) playerControlCoordinator.SetControlEnabled(true);
            if (hudController != null) hudController.SetCrosshairVisible(true);
            UnityEngine.Cursor.lockState = previousCursorLockMode;
            UnityEngine.Cursor.visible = wasCursorVisible;
        }

        private void HidePrompt()
        {
            if (isPanelOpen || hudController == null)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name == "UpgradeRoom")
            {
                return;
            }

            hudController.SetInteractionPrompt(string.Empty, string.Empty, false);
        }

        private void CacheShieldRenderers()
        {
            if (shieldLeftRenderers == null || shieldLeftRenderers.Length == 0) shieldLeftRenderers = shieldLeft != null ? shieldLeft.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            if (shieldRightRenderers == null || shieldRightRenderers.Length == 0) shieldRightRenderers = shieldRight != null ? shieldRight.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            if (shieldCenterRenderers == null || shieldCenterRenderers.Length == 0) shieldCenterRenderers = shieldCenter != null ? shieldCenter.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        }

        private void UpdateShieldHighlight(bool hasTarget, SelectionStation targetStation)
        {
            if (propertyBlock == null) return;
            Color active = targetedColor * Mathf.Max(0.01f, targetedIntensity);
            ApplyShieldHighlightState(SelectionStation.Character, hasTarget && targetStation == SelectionStation.Character, active);
            ApplyShieldHighlightState(SelectionStation.WeaponUpgrade, hasTarget && targetStation == SelectionStation.WeaponUpgrade, active);
            ApplyShieldHighlightState(SelectionStation.PrimaryWeapon, hasTarget && targetStation == SelectionStation.PrimaryWeapon, active);
        }

        private void ApplyShieldHighlightState(SelectionStation station, bool highlighted, Color emission)
        {
            Renderer[] renderers = GetShieldRenderers(station);
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                if (!highlighted) { renderer.SetPropertyBlock(null); continue; }
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void PrepareHighlightMaterials()
        {
            var visited = new HashSet<int>();
            PrepareRendererMaterials(shieldLeftRenderers, visited);
            PrepareRendererMaterials(shieldRightRenderers, visited);
            PrepareRendererMaterials(shieldCenterRenderers, visited);
        }

        private static void PrepareRendererMaterials(Renderer[] renderers, HashSet<int> visited)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    int id = mat.GetInstanceID();
                    if (!visited.Add(id)) continue;
                    if (!mat.HasProperty(EmissionColorId)) continue;
                    mat.EnableKeyword("_EMISSION");
                    if (mat.GetColor(EmissionColorId).maxColorComponent > 0.0001f) continue;
                    mat.SetColor(EmissionColorId, Color.black);
                }
            }
        }

        private Renderer[] GetShieldRenderers(SelectionStation station)
        {
            switch (station)
            {
                case SelectionStation.Character: return shieldLeftRenderers;
                case SelectionStation.WeaponUpgrade: return shieldRightRenderers;
                default: return shieldCenterRenderers;
            }
        }

        private void EnsureDefaultPrimaryWeaponChoices()
        {
            if (primaryWeaponChoices.Count == 0)
            {
                primaryWeaponChoices = new List<OptionData>
                {
                    new() { id = "weapon_ar", title = "Автомат", description = "Сбалансированная скорострельность и контроль.", iconText = "AR" },
                    new() { id = "weapon_lmg", title = "Пулемет", description = "Высокая плотность огня, большой магазин.", iconText = "LMG" },
                    new() { id = "weapon_shotgun", title = "Дробовик", description = "Максимальный урон в ближнем бою.", iconText = "SHG" }
                };
            }
        }

        private static List<OptionData> CloneOptions(List<OptionData> source)
        {
            List<OptionData> clone = new();
            if (source == null) return clone;
            for (int i = 0; i < source.Count; i++)
            {
                OptionData item = source[i];
                if (item == null) continue;
                clone.Add(new OptionData { id = item.id, title = item.title, description = item.description, icon = item.icon, iconText = item.iconText, stackable = item.stackable, maxStacks = item.maxStacks });
            }
            return clone;
        }
    }
}
