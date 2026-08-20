using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Sticki.Combat;
using TMPro;

namespace Sticki.Core
{
    public class PerformanceOverlay : MonoBehaviour
    {
        [SerializeField] private bool createCanvasAtRuntime = true;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private int targetFpsForColor = 60;

        private float elapsed;
        private float smoothedFps = 60f;
        private float minFps = 999f;
        private float maxFps = 0f;
        private int frames;
        private readonly StringBuilder sb = new(256);

        private void Awake()
        {
            if (createCanvasAtRuntime && statsText == null)
            {
                BuildRuntimeUi();
            }
        }

        private void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float fps = 1f / dt;
            smoothedFps = Mathf.Lerp(smoothedFps, fps, 0.08f);
            minFps = Mathf.Min(minFps, fps);
            maxFps = Mathf.Max(maxFps, fps);
            frames++;
            elapsed += dt;

            if (elapsed < Mathf.Max(0.05f, refreshInterval))
            {
                return;
            }

            RefreshText();
            elapsed = 0f;
        }

        private void RefreshText()
        {
            if (statsText == null)
            {
                return;
            }

            VfxPoolService.TryGetStats(out int pools, out int totalVfx, out int availableVfx, out int activeVfx);
            long memoryMb = System.GC.GetTotalMemory(false) / (1024 * 1024);

            sb.Clear();
            sb.AppendLine("STRESS HUD");
            sb.Append("FPS: ").Append(smoothedFps.ToString("0.0"))
              .Append("  (min ").Append(minFps.ToString("0"))
              .Append(" / max ").Append(maxFps.ToString("0")).AppendLine(")");
            sb.Append("Frame: ").Append((1000f / Mathf.Max(1f, smoothedFps)).ToString("0.00")).AppendLine(" ms");
            sb.Append("Enemies: ").Append(EnemyHealth.AliveCount).Append(" alive / ").Append(EnemyHealth.RegisteredCount).AppendLine(" registered");
            sb.Append("VFX Pools: ").Append(pools).Append(" | Active: ").Append(activeVfx)
              .Append(" | Available: ").Append(availableVfx).Append(" | Total: ").Append(totalVfx).AppendLine();
            sb.Append("GC Memory: ").Append(memoryMb).AppendLine(" MB");

            statsText.text = sb.ToString();
            statsText.color = smoothedFps >= targetFpsForColor ? new Color(0.8f, 1f, 0.8f, 1f) : new Color(1f, 0.82f, 0.82f, 1f);
        }

        private void BuildRuntimeUi()
        {
            GameObject canvasGo = new GameObject("PerfOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            GameObject panelGo = new GameObject("PerfOverlayPanel", typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            Image panel = panelGo.GetComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.45f);

            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(14f, -14f);
            panelRt.sizeDelta = new Vector2(360f, 230f);

            GameObject textGo = new GameObject("PerfOverlayText", typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            statsText = textGo.GetComponent<TMP_Text>();
            statsText.fontSize = 19;
            statsText.alignment = TextAlignmentOptions.TopLeft;
            statsText.textWrappingMode = TextWrappingModes.Normal;
            statsText.richText = false;

            RectTransform textRt = statsText.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(10f, 8f);
            textRt.offsetMax = new Vector2(-10f, -8f);
        }
    }
}
