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
    public Transform cardParent;      // container with GridLayoutGroup (or Vertical/Horizontal)
    public GameObject cardPrefab;     // prefab with Image + (optional TMP_Text)

    [Header("Settings")]
    public string packType = "bronze_pack";

    [Header("References")]
    public DropHistoryController dropHistoryController;

    // pool/cache so we don’t GC churn every open
    private readonly List<GameObject> _cards = new List<GameObject>();

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(() =>
            {
                packPanel.SetActive(false);
                dropHistoryPanel.SetActive(true);
                if (dropHistoryController != null) dropHistoryController.RefreshDropHistory();
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
            Debug.LogError("[PackOpening] cardParent/cardPrefab not assigned.");
            return;
        }

        var mgr = DropConfigManager.Instance;
        if (mgr == null || mgr.config == null)
        {
            Debug.LogError("[PackOpening] DropConfigManager/config missing.");
            return;
        }

        var rarities = mgr.PullCardRarities(packType, out bool pityTriggered, out string pityType);

        Debug.Log($"[PackOpening] {packType} → {rarities.Count} cards | pity={pityTriggered}:{pityType ?? "-"}");

        BuildOrReuseCards(rarities.Count);

        // Send to emotional system & hooks
        EmotionalStateManager.Instance?.HandleOutcomeEvent(rarities, pityTriggered, pityType);
        HookOrchestrator.Instance?.TryTriggerOutcomeHooks(rarities);

        // Log to telemetry
        TelemetryLogger.Instance?.LogPull(
            packType,
            packType,
            DropConfigManager.Instance.config.pack_types[packType].cost,
            rarities,
            0, 0, 0,
            pityTriggered, pityType
        );



        // apply visuals
        for (int i = 0; i < _cards.Count; i++)
        {
            var go = _cards[i];
            if (i < rarities.Count)
            {
                if (!go.activeSelf) go.SetActive(true);
                SetCard(go, rarities[i]);
            }
            else
            {
                go.SetActive(false);
            }
        }

        // if you want the panel to pop open here:
        if (packPanel != null) packPanel.SetActive(true);
        if (dropHistoryPanel != null) dropHistoryPanel.SetActive(false);

        // Rebuild layout so the grid looks crisp after re-activation
        var rt = cardParent as RectTransform;
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    void BuildOrReuseCards(int needed)
    {
        // create as many as needed
        while (_cards.Count < needed)
        {
            var go = Instantiate(cardPrefab, cardParent);
            go.SetActive(false);
            _cards.Add(go);
        }
        // leave extras in the pool (we toggle them off)
    }

    void SetCard(GameObject go, string rarityRaw)
    {
        string rarity = (rarityRaw ?? "common").ToLowerInvariant();

        // Color / sprite on Image
        var img = go.GetComponentInChildren<Image>(true);
        if (img != null)
        {
            img.color = GetColorForRarity(rarity);
        }

        // Optional label
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = rarity.ToUpper();
        }

        // If you drop CardView on the prefab, also let it know:
        var view = go.GetComponent<CardView>();
        if (view != null) view.Apply(rarity);
    }

    Color GetColorForRarity(string rarity)
    {
        switch (rarity)
        {
            case "common":    return new Color32(150,150,150,255);
            case "uncommon":  return new Color32(46,204,113,255);
            case "rare":      return new Color32(0,112,221,255);
            case "epic":      return new Color32(163,53,238,255);
            case "legendary": return new Color32(255,204,0,255);
            default:          return Color.white;
        }
    }
}
