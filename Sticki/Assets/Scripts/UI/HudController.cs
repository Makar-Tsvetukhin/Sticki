using UnityEngine;
using UnityEngine.UIElements;
using Sticki.UI.Core;

public class HudController : UIScreenController
{
    private VisualElement _hpFill;
    private Label _hpValueLabel;
    private Label _ammoCurrentLabel;
    private Label _ammoReserveLabel;
    private VisualElement _crosshairRoot;
    private Label _arenaProgressLabel;
    private Label _arenaProgressValueLabel;
    private VisualElement _interactionPromptRoot;
    private Label _interactionKeyLabel;
    private Label _interactionActionLabel;
    private VisualElement _hitMarkerRoot;
    private IVisualElementScheduledItem _hitMarkerHideTask;

    private const string LowHealthClass = "hp-bar-fill--low";
    private const string PromptVisibleClass = "interaction-prompt--visible";
    private const string HitVisibleClass = "hit-marker--visible";
    private const string HitKillClass = "hit-marker--kill";
    private const float HitMarkerDuration = 0.12f;
    private const float HpSmoothTime = 0.18f;
    private const float HpValueSmoothTime = 0.15f;

    private float _currentHpNormalized;
    private float _targetHpNormalized;
    private float _hpVelocity;
    private float _currentHpValue;
    private float _targetHpValue;
    private float _hpValueVelocity;

    protected override void OnInitialize() 
    {
        _hpFill = root.Q<VisualElement>("hp-bar-fill");
        _hpValueLabel = root.Q<Label>("hp-value");
        _ammoCurrentLabel = root.Q<Label>("ammo-current");
        _ammoReserveLabel = root.Q<Label>("ammo-reserve");
        _crosshairRoot = root.Q<VisualElement>("crosshair-container");
        _arenaProgressLabel = root.Q<Label>("arena-progress-label");
        _arenaProgressValueLabel = root.Q<Label>("arena-progress-value");
        _interactionPromptRoot = root.Q<VisualElement>("interaction-prompt");
            
        _interactionKeyLabel = root.Q<Label>("interaction-key");
        _interactionActionLabel = root.Q<Label>("interaction-action");
        _hitMarkerRoot = root.Q<VisualElement>("hit-marker-container");

        _currentHpNormalized = 1f;
        _targetHpNormalized = 1f;
        _currentHpValue = 100f;
        _targetHpValue = 100f;

        UpdateHealthVisuals();
    }

    private void Update()
    {
        if (Mathf.Abs(_currentHpNormalized - _targetHpNormalized) < 0.0001f &&
            Mathf.Abs(_currentHpValue - _targetHpValue) < 0.01f &&
            Mathf.Abs(_hpVelocity) < 0.0001f &&
            Mathf.Abs(_hpValueVelocity) < 0.01f)
        {
            return;
        }

        UpdateHealthSmooth();
    }

    public void UpdateHealth(float normalized, int value)
    {
        if (_hpFill == null || _hpValueLabel == null) return;

        _targetHpNormalized = Mathf.Clamp01(normalized);
        _targetHpValue = Mathf.Max(0, value);
        UpdateHealthSmooth();
    }

    private void UpdateHealthSmooth()
    {
        if (_hpFill == null || _hpValueLabel == null) return;

        _currentHpNormalized = Mathf.SmoothDamp(_currentHpNormalized, _targetHpNormalized, ref _hpVelocity, HpSmoothTime);
        _currentHpValue = Mathf.SmoothDamp(_currentHpValue, _targetHpValue, ref _hpValueVelocity, HpValueSmoothTime);
        UpdateHealthVisuals();
    }

    private void UpdateHealthVisuals()
    {
        float normalized = Mathf.Clamp01(_currentHpNormalized);
        if (_hpFill != null) _hpFill.style.width = Length.Percent(normalized * 100f);
        if (_hpValueLabel != null) _hpValueLabel.text = Mathf.RoundToInt(_currentHpValue).ToString();

        if (normalized < 0.3f)
        {
            if (_hpFill != null && !_hpFill.ClassListContains(LowHealthClass))
                _hpFill.AddToClassList(LowHealthClass);
        }
        else
        {
            if (_hpFill != null && _hpFill.ClassListContains(LowHealthClass))
                _hpFill.RemoveFromClassList(LowHealthClass);
        }
    }

    public void UpdateAmmo(int current, int reserve, bool infinite = false)
    {
        if (_ammoCurrentLabel == null || _ammoReserveLabel == null) return;

        _ammoCurrentLabel.text = current.ToString();
        _ammoReserveLabel.text = infinite ? "∞" : reserve.ToString();
    }

    public void SetCrosshairVisible(bool visible)
    {
        if (_crosshairRoot != null)
            _crosshairRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void UpdateArenaProgress(int kills, int target)
    {
        if (_arenaProgressLabel == null || _arenaProgressValueLabel == null) return;

        int safeTarget = Mathf.Max(0, target);
        int safeKills = Mathf.Clamp(kills, 0, safeTarget);
        _arenaProgressValueLabel.text = $"{safeKills}/{safeTarget}";
    }

    public void SetInteractionPrompt(string key, string action, bool visible)
    {
        if (_interactionPromptRoot == null || _interactionKeyLabel == null || _interactionActionLabel == null)
        {
            return;
        }

        _interactionKeyLabel.text = key;
        _interactionActionLabel.text = action;

        if (visible)
        {
            if (!_interactionPromptRoot.ClassListContains(PromptVisibleClass))
            {
                _interactionPromptRoot.AddToClassList(PromptVisibleClass);
            }
        }
        else
        {
            if (_interactionPromptRoot.ClassListContains(PromptVisibleClass))
            {
                _interactionPromptRoot.RemoveFromClassList(PromptVisibleClass);
            }
        }
    }

    public void ShowHitMarker(bool isKill)
    {
        if (_hitMarkerRoot == null) return;

        _hitMarkerHideTask?.Pause();

        if (isKill)
        {
            if (!_hitMarkerRoot.ClassListContains(HitKillClass))
                _hitMarkerRoot.AddToClassList(HitKillClass);
        }
        else
        {
            if (_hitMarkerRoot.ClassListContains(HitKillClass))
                _hitMarkerRoot.RemoveFromClassList(HitKillClass);
        }

        if (!_hitMarkerRoot.ClassListContains(HitVisibleClass))
            _hitMarkerRoot.AddToClassList(HitVisibleClass);

        _hitMarkerHideTask = _hitMarkerRoot.schedule.Execute(() => {
            _hitMarkerRoot.RemoveFromClassList(HitVisibleClass);
        }).StartingIn(Mathf.RoundToInt(HitMarkerDuration * 1000));
    }
    }

