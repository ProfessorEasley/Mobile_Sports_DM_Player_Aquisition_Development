using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CCAS.Config;

public class BoosterMarketAuto : MonoBehaviour
{
    [Header("Panels")]
    public GameObject marketPanel;
    public GameObject packPanel;

    [Header("References")]
    public PackOpeningController opener;
    public Transform listParent;        // ← your PackList object
    public GameObject buttonPrefab;     // Button + TMP + CanvasGroup

    // keep for refresh when wallet changes
    private readonly List<(Button btn, CanvasGroup cg, string key, PackType pack)> _items = new();

    void Start()
    {
        var cfg = DropConfigManager.Instance.config;
        if (cfg?.pack_types == null) { Debug.LogError("No pack_types in config"); return; }

        foreach (var kv in cfg.pack_types)
        {
            string key = kv.Key;
            var p = kv.Value;

            var go  = Instantiate(buttonPrefab, listParent);
            var btn = go.GetComponent<Button>();
            var cg  = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();

            // Show name + price (prefers coins; uses gems if coins==0)
            string price = (p.cost != null && p.cost.coins > 0) ? $"{p.cost.coins} coins"
                         : (p.cost != null && p.cost.gems  > 0) ? $"{p.cost.gems} gems"
                         : "Free";
            txt.text = $"{p.name} • {price}";

            btn.onClick.AddListener(() => TryBuyAndOpen(p, key));

            _items.Add((btn, cg, key, p));
        }

        // initial afford check + subscribe for future updates
        RefreshAffordability();
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += RefreshAffordability;
    }

    void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= RefreshAffordability;
    }

    void RefreshAffordability()
    {
        var wallet = PlayerWallet.Instance;
        foreach (var item in _items)
        {
            bool can = wallet == null || wallet.CanAfford(item.pack);
            item.btn.interactable = can;
            item.cg.alpha = can ? 1f : 0.45f;     // grey out
            item.cg.blocksRaycasts = can;         // avoid clicks when disabled
        }
    }

    void TryBuyAndOpen(PackType p, string key)
    {
        var wallet = PlayerWallet.Instance;
        if (wallet != null && !wallet.SpendForPack(p))
        {
            Debug.Log("Not enough currency for " + p.name);
            return;
        }

        marketPanel.SetActive(false);
        packPanel.SetActive(true);
        opener.OpenPackOfType(key);
    }
}
