using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PackOpeningController : MonoBehaviour
{
    [Header("Navigation")]
    public Button continueButton;
    public GameObject packPanel;
    public GameObject dropHistoryPanel;

    [Header("Card UI")]
    public Transform cardParent;     // Container with GridLayoutGroup
    public GameObject cardPrefab;    // Prefab for individual card display

    [Header("Settings")]
    public string packType = "bronze_pack";

    [Header("References")]
    public DropHistoryController dropHistoryController;

    // Cached card objects (reuse between openings)
    private readonly List<GameObject> _cards = new();

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(() =>
            {
                if (packPanel) packPanel.SetActive(false);
                if (dropHistoryPanel) dropHistoryPanel.SetActive(true);
                dropHistoryController?.RefreshDropHistory();
            });
        }
    }

    public void OpenPackOfType(string key)
    {
        packType = key;
        OpenPack();
    }

    [ContextMenu("Open Pack (Current Setting)")]
    public void OpenPack()
    {
        if (cardParent == null || cardPrefab == null)
        {
            Debug.LogError("[PackOpening] Missing references: cardParent or cardPrefab.");
            return;
        }

        var mgr = DropConfigManager.Instance;
        if (mgr == null || mgr.config == null)
        {
            Debug.LogError("[PackOpening] DropConfigManager/config missing.");
            return;
        }

        // --- Pull cards from config ---
        var rarities = mgr.PullCardRarities(packType, out bool pityTriggered, out string pityType);

#if UNITY_EDITOR
        Debug.Log($"[PackOpening] {packType} → {rarities.Count} cards | pity={pityTriggered}:{pityType ?? "-"}");
#endif

        BuildOrReuseCards(rarities.Count);

        // --- Emotional state (Phase 1: Satisfaction & Frustration) ---
        EmotionalStateManager.Instance?.HandleOutcomeEvent(rarities, pityTriggered, pityType);

        // --- Hook orchestration ---
        HookOrchestrator.Instance?.TryTriggerOutcomeHooks(rarities);

        // --- Telemetry logging ---
        var packData = DropConfigManager.Instance.config.pack_types[packType];
        TelemetryLogger.Instance?.LogPull(
            packType,
            packData.pack_id,       // use actual pack_id from config
            packData.cost,
            rarities,
            0, 0, 0,                // placeholder pity counters for now
            pityTriggered,
            pityType
        );

        // --- Visual display ---
        for (int i = 0; i < _cards.Count; i++)
        {
            var go = _cards[i];
            if (i < rarities.Count)
            {
                go.SetActive(true);
                SetCard(go, rarities[i]);
            }
            else go.SetActive(false);
        }

        // --- UI state ---
        if (packPanel) packPanel.SetActive(true);
        if (dropHistoryPanel) dropHistoryPanel.SetActive(false);

        // Force immediate layout rebuild
        if (cardParent is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------
    void BuildOrReuseCards(int needed)
    {
        while (_cards.Count < needed)
        {
            var go = Instantiate(cardPrefab, cardParent);
            go.SetActive(false);
            _cards.Add(go);
        }
    }

    void SetCard(GameObject go, string rarityRaw)
    {
        string rarity = (rarityRaw ?? "common").ToLowerInvariant();

        var img = go.GetComponentInChildren<Image>(true);
        if (img != null) img.color = GetColorForRarity(rarity);

        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = rarity.ToUpperInvariant();

        // Optional: use CardView for unified appearance
        var view = go.GetComponent<CardView>();
        if (view != null) view.Apply(rarity);
    }

    Color GetColorForRarity(string rarity) => rarity switch
    {
        "common"    => new Color32(150, 150, 150, 255),
        "uncommon"  => new Color32(46, 204, 113, 255),
        "rare"      => new Color32(0, 112, 221, 255),
        "epic"      => new Color32(163, 53, 238, 255),
        "legendary" => new Color32(255, 204, 0, 255),
        _           => Color.white
    };
}
