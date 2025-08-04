using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;     // for ScrollRect & LayoutRebuilder
using System.Collections.Generic; 

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

    }
    public void RefreshDropHistory()
    {
        StartCoroutine(PopulateAndScroll());
    }
    
    IEnumerator PopulateAndScroll()
    {
        foreach (Transform child in contentParent)
        {
            if (child.gameObject == resultTemplate) continue; // don't destroy the template
            Destroy(child.gameObject);
        }

        // 2) Instantiate entries
        var pulls = DropConfigManager.Instance.allPullResults;
        var rules = DropConfigManager.Instance.config.duplicate_conversion.xp_conversion_rates;

        foreach (var pull in pulls)
        {
            foreach (var rarity in pull)
            {
                var entry = Instantiate(resultTemplate, contentParent);
                entry.SetActive(true);

                var tmp = entry.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                int xp = GetXPForTier(rarity, rules);
                tmp.text = $"{rarity.ToUpper()} → XP: {xp}";
            }
        }

        yield return null;

        var rt = contentParent as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        scrollRect.verticalNormalizedPosition = 1f;
    }

    int GetXPForTier(string tier, Dictionary<string, int> xpMap)
    {
        if (xpMap.TryGetValue(tier.ToLower(), out int xp))
            return xp;
        return 0;
    }

}
