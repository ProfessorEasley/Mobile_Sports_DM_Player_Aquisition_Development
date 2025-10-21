using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/// <summary>
/// Displays recent pack pull results and emotional summaries (Phase-1 telemetry view).
/// </summary>
public class DropHistoryController : MonoBehaviour
{
    [Header("Panels & Navigation")]
    public GameObject hubPanel;
    public GameObject dropHistoryPanel;
    public Button backToHubButton;

    [Header("Pull Results UI")]
    public Transform contentParent;      // Content container under ScrollView
    public GameObject resultTemplate;    // Disabled prefab with TMP child
    public ScrollRect scrollRect;

    [Header("Display")]
    [Range(1, 20)] public int recentPullsToShow = 3;

    // Internals
    private bool _pendingRefresh;
    private bool _isPopulating;
    private System.Action<TelemetryLogger.PackPullLog> _onPullLoggedHandler;

    void Awake()
    {
        // Ensure template is disabled to prevent showing placeholder
        if (resultTemplate != null && resultTemplate.activeSelf)
            resultTemplate.SetActive(false);
    }

    void Start()
    {
        // Navigation back button
        if (backToHubButton != null)
        {
            backToHubButton.onClick.AddListener(() =>
            {
                dropHistoryPanel.SetActive(false);
                hubPanel.SetActive(true);
            });
        }

        _onPullLoggedHandler = OnPullLogged;

        if (TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.OnPullLogged += _onPullLoggedHandler;
    }

    void OnEnable()
    {
        // Handle deferred refresh
        if (_pendingRefresh)
        {
            _pendingRefresh = false;
            RefreshDropHistory();
        }
    }

    void OnDestroy()
    {
        if (TelemetryLogger.Instance != null && _onPullLoggedHandler != null)
            TelemetryLogger.Instance.OnPullLogged -= _onPullLoggedHandler;
    }

    public void RefreshDropHistory()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }
        if (_isPopulating) return;

        StartCoroutine(PopulateAndScroll());
    }

    void OnPullLogged(TelemetryLogger.PackPullLog _)
    {
        RefreshDropHistory();
    }

    IEnumerator PopulateAndScroll()
    {
        _isPopulating = true;

        // --- Clear old entries ---
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            var child = contentParent.GetChild(i).gameObject;
            if (child == resultTemplate) continue;
            Destroy(child);
        }

        // --- Retrieve logs ---
        var logger = TelemetryLogger.Instance;
        var logs = logger != null ? logger.GetRecent(recentPullsToShow) : new List<TelemetryLogger.PackPullLog>();

        if (logs == null || logs.Count == 0)
        {
            var entry = Instantiate(resultTemplate, contentParent);
            entry.SetActive(true);
            entry.GetComponentInChildren<TextMeshProUGUI>().text = "No pulls yet.";
            _isPopulating = false;
            yield break;
        }

        int cardCount = 0;
        for (int i = logs.Count - 1; i >= 0; i--) // Newest first
        {
            var log = logs[i];
            var raritiesLine = new StringBuilder();

            // --- Individual card results ---
            foreach (var res in log.pull_results)
            {
                var entry = Instantiate(resultTemplate, contentParent);
                entry.SetActive(true);

                var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
                string rarity = (res.rarity ?? "common").ToLowerInvariant();

                tmp.text = $"{rarity.ToUpper()} CARD";
                tmp.color = GetColorForRarity(rarity);
                cardCount++;

                if (raritiesLine.Length > 0) raritiesLine.Append(", ");
                raritiesLine.Append(rarity);
            }

            // --- Emotional summary block ---
            var emoEntry = Instantiate(resultTemplate, contentParent);
            emoEntry.SetActive(true);

            var emoText = emoEntry.GetComponentInChildren<TextMeshProUGUI>();

            float fr = log.emotional_state.frustration;
            float sa = log.emotional_state.satisfaction;
            float cum = log.emotional_state.cumulative_score;
            int neg = log.emotional_state.negative_streaks;

            emoText.text = $"Satisfaction: {sa:F2}  |  Frustration: {fr:F2}  |  Score: {cum:F2}  |  Neg. Streaks: {neg}";
            emoText.color = Color.Lerp(Color.red, Color.green, Mathf.InverseLerp(0f, 10f, sa));

            Debug.Log($"[History] Rendered pull {log.event_id} ({log.pack_info.pack_type}) → [{raritiesLine}] | S={sa:F1} F={fr:F1}");
        }

        // Wait a frame before layout rebuild to ensure all entries exist
        yield return null;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
        yield return null;

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f; // Scroll to top (most recent)

        Debug.Log($"[History] Listed {cardCount} card(s) across {logs.Count} pull(s).");
        _isPopulating = false;
    }

    Color GetColorForRarity(string rarity)
    {
        return rarity switch
        {
            "common" => new Color32(150, 150, 150, 255),
            "uncommon" => new Color32(46, 204, 113, 255),
            "rare" => new Color32(0, 112, 221, 255),
            "epic" => new Color32(163, 53, 238, 255),
            "legendary" => new Color32(255, 204, 0, 255),
            _ => Color.white
        };
    }
}
