using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;     // for ScrollRect & LayoutRebuilder

public class DropHistoryController : MonoBehaviour
{
    [Header("Panels & Navigation")]
    public GameObject hubPanel;
    public GameObject dropHistoryPanel;
    public Button backToHubButton;

    [Header("Pull Results UI")]
    public Transform contentParent;     // Content under Viewport
    public GameObject resultTemplate;   // Disabled template GameObject with TMP child
    public ScrollRect scrollRect;       // The Scroll View component

    void Start()
    {
        // Only run this logic in Play mode
        if (!Application.isPlaying) 
            return;

        // Back-to-hub navigation
        backToHubButton.onClick.AddListener(() => {
            dropHistoryPanel.SetActive(false);
            hubPanel.SetActive(true);
        });

        // Launch the population + scroll coroutine
        StartCoroutine(PopulateAndScroll());
    }

    IEnumerator PopulateAndScroll()
    {
        // 1) Instantiate entries
        var config = CCASConfig.Instance;
        var pulls  = config.samplePull;
        var rules  = config.dupeRules;

        foreach (var tier in pulls)
        {
            var entry = Instantiate(resultTemplate, contentParent);
            entry.SetActive(true);

            var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
            tmp.text = $"{tier.ToUpper()} → XP: {GetXPForTier(tier, rules)}";
        }

        // 2) Wait one frame for the layout group & size fitter to do their work
        yield return null;

        // 3) Force an immediate layout rebuild (just in case)
        var rt = contentParent as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        // 4) Reset scroll to top
        scrollRect.verticalNormalizedPosition = 1f;
    }

    int GetXPForTier(string tier, DupeRules rules)
    {
        switch (tier.ToLower())
        {
            case "common":    return rules.common;
            case "rare":      return rules.rare;
            case "epic":      return rules.epic;
            case "legendary": return rules.legendary;
            default:          return 0;
        }
    }
}
