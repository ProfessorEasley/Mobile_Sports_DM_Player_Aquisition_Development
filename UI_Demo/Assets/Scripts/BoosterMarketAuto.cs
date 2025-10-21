using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CCAS.Config;

/// <summary>
/// Dynamically builds the Booster Market UI from the drop configuration.
/// Handles pack purchasing, wallet deduction, and navigation to Pack Opening.
/// Integrated with Phase 1 telemetry & emotion tracking.
/// </summary>
public class BoosterMarketAuto : MonoBehaviour
{
    [Header("Panels")]
    public GameObject marketPanel;
    public GameObject packPanel;

    [Header("References")]
    public PackOpeningController opener;
    public Transform listParent;         // Parent object for pack buttons
    public GameObject buttonPrefab;      // Prefab containing Button + TMP + CanvasGroup

    // Keep references for UI refreshes (e.g., after wallet updates)
    private readonly List<(Button btn, CanvasGroup cg, string key, PackType pack)> _items = new();

    void Start()
    {
        // --- Load drop configuration ---
        var cfg = DropConfigManager.Instance?.config;
        if (cfg?.pack_types == null)
        {
            Debug.LogError("[Market] No pack_types found in drop config. Check ccas_drop_config.json");
            return;
        }

        // --- Build dynamic buttons ---
        foreach (var kv in cfg.pack_types)
        {
            string key = kv.Key;
            var packData = kv.Value;

            // Instantiate UI elements
            var go = Instantiate(buttonPrefab, listParent);
            var btn = go.GetComponent<Button>();
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            var txt = go.GetComponentInChildren<TextMeshProUGUI>(true);

            if (btn == null || txt == null)
            {
                Debug.LogWarning($"[Market] ButtonPrefab missing Button or TMP_Text for {key}");
                continue;
            }

            // Compose pack label
            string price = (packData.cost != null && packData.cost.coins > 0)
                ? $"{packData.cost.coins} coins"
                : (packData.cost != null && packData.cost.gems > 0)
                    ? $"{packData.cost.gems} gems"
                    : "Free";

            txt.text = $"{packData.name} • {price}";
            btn.onClick.AddListener(() => TryBuyAndOpen(packData, key));

            _items.Add((btn, cg, key, packData));
        }

        // --- Initial affordability check ---
        RefreshAffordability();

        // Subscribe for wallet updates
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += RefreshAffordability;
    }

    void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= RefreshAffordability;
    }

    /// <summary>
    /// Greys out packs that the player cannot afford.
    /// </summary>
    void RefreshAffordability()
    {
        var wallet = PlayerWallet.Instance;
        foreach (var item in _items)
        {
            bool canAfford = wallet == null || wallet.CanAfford(item.pack);
            item.btn.interactable = canAfford;
            item.cg.alpha = canAfford ? 1f : 0.45f; // Dim if not affordable
            item.cg.blocksRaycasts = canAfford;
        }
    }

    /// <summary>
    /// Handles pack purchases and transitions to the pack-opening panel.
    /// </summary>
    void TryBuyAndOpen(PackType packData, string packKey)
    {
        var wallet = PlayerWallet.Instance;
        if (wallet != null && !wallet.SpendForPack(packData))
        {
            Debug.Log($"[Market] Not enough currency for {packData.name}");
            return;
        }

        // Close market, open pack screen
        marketPanel?.SetActive(false);
        packPanel?.SetActive(true);

        // Reset emotional state before new session pull
        EmotionalStateManager.Instance?.ResetSession();

        // Ensure PackOpeningController exists and trigger open
        if (opener == null)
        {
            opener = packPanel?.GetComponentInChildren<PackOpeningController>(true);
        }

        if (opener != null)
        {
            opener.OpenPackOfType(packKey);
        }
        else
        {
            Debug.LogError($"[Market] No PackOpeningController found in {packPanel?.name ?? "packPanel"}");
        }
    }
}
