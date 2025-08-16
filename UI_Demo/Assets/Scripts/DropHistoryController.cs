using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropHistoryController : MonoBehaviour
{
    [Header("Panels & Navigation")]
    public GameObject hubPanel;
    public GameObject dropHistoryPanel;
    public Button backToHubButton;

    [Header("Pull Results UI")]
    public Transform contentParent;     // Content under Viewport
    public GameObject resultTemplate;   // Disabled template with TMP child
    public ScrollRect scrollRect;       // The Scroll View component

    [Header("Display")]
    [Range(1, 20)] public int recentPullsToShow = 1;

    // --- internals ---
    bool _pendingRefresh;
    bool _isPopulating;
    System.Action<TelemetryLogger.PackPullLog> _onPullLoggedHandler;

    void Awake()
    {
        // Ensure the template is disabled in the scene
        if (resultTemplate != null && resultTemplate.activeSelf)
            resultTemplate.SetActive(false);
    }

    void Start()
    {
        if (backToHubButton != null)
        {
            backToHubButton.onClick.AddListener(() => {
                dropHistoryPanel.SetActive(false);
                hubPanel.SetActive(true);
            });
        }

        // Store the handler so we can unsubscribe correctly
        _onPullLoggedHandler = OnPullLogged;
        if (TelemetryLogger.Instance != null)
            TelemetryLogger.Instance.OnPullLogged += _onPullLoggedHandler;
    }

    void OnEnable()
    {
        // If a refresh was requested while inactive, do it now
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

    // Called by telemetry event and by your UI when you open the history screen
    public void RefreshDropHistory()
    {
        // If this controller is disabled or its GameObject is inactive, defer
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }
        if (_isPopulating) return; // throttle double calls
        StartCoroutine(PopulateAndScroll());
    }

    void OnPullLogged(TelemetryLogger.PackPullLog _)
    {
        // Auto-refresh when a new pull is logged
        RefreshDropHistory();
    }

    IEnumerator PopulateAndScroll()
    {
        _isPopulating = true;

        // Clear existing entries (keep the inactive template)
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            var child = contentParent.GetChild(i).gameObject;
            if (child == resultTemplate) continue;
            Destroy(child);
        }

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

        var tiers = DropConfigManager.Instance?.config?.rarity_tiers;

        int cardCount = 0;
        for (int i = logs.Count - 1; i >= 0; i--) // newest first
        {
            var log = logs[i];
            var raritiesLine = new System.Text.StringBuilder();

            foreach (var res in log.pull_results)
            {
                var entry = Instantiate(resultTemplate, contentParent);
                entry.SetActive(true);

                var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
                string key = (res.rarity ?? "common").ToLowerInvariant();
                int xp = (tiers != null && tiers.TryGetValue(key, out var tier)) ? tier.dupe_xp : 0;

                tmp.text = $"{res.rarity?.ToUpper() ?? "COMMON"} → Dupe XP: {xp}";
                cardCount++;

                if (raritiesLine.Length > 0) raritiesLine.Append(", ");
                raritiesLine.Append(res.rarity ?? "common");
            }

            Debug.Log($"[History] Rendered pull {log.event_id} ({log.pack_info.pack_type}) → [{raritiesLine}]");
        }

        yield return null;

        var rt = contentParent as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

        Debug.Log($"[History] Listed {cardCount} card(s) across {logs.Count} pull(s).");

        _isPopulating = false;
    }
}
