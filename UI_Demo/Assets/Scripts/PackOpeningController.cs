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
    public Transform cardParent;
    public GameObject cardPrefab;

    [Header("Settings")]
    public string packType = "bronze_pack";

    [Header("References")]
    public DropHistoryController dropHistoryController;

    private readonly List<GameObject> _cards = new();

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(() =>
            {

                FindObjectOfType<AcquisitionHubController>()?.ShowHistory();
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
        if (string.IsNullOrEmpty(packType))
        {
            Debug.LogWarning("[PackOpening] packType not set.");
            return;
        }
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

        var rarities = mgr.PullCardRarities(packType);
        Debug.Log($"[PackOpening] {packType} → {rarities.Count} cards");

        BuildOrReuseCards(rarities.Count);

        // Emotion + hooks
        EmotionalStateManager.Instance?.ApplyPackOutcome(packType, rarities);
        HookOrchestrator.Instance?.TryTriggerOutcomeHooks(rarities);

        // Telemetry (simplified)
        var packData = mgr.config.pack_types[packType];
        TelemetryLogger.Instance?.LogPull(
            packType,
            packData.name,  // replaced pack_id
            packData.cost,
            rarities
        );

        // Visuals
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

        // UI
        if (packPanel) packPanel.SetActive(true);
        if (dropHistoryPanel) dropHistoryPanel.SetActive(false);

        if (cardParent is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

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
    }

    Color GetColorForRarity(string rarity) => rarity switch
    {
        "common" => new Color32(150, 150, 150, 255),
        "uncommon" => new Color32(46, 204, 113, 255),
        "rare" => new Color32(0, 112, 221, 255),
        "epic" => new Color32(163, 53, 238, 255),
        "legendary" => new Color32(255, 204, 0, 255),
        _ => Color.white
    };
}
