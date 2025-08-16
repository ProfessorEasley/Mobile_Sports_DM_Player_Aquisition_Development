using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PackOpeningController : MonoBehaviour
{
    [Header("Navigation")]
    public Button continueButton;
    public GameObject packPanel;
    public GameObject dropHistoryPanel;

    [Header("Card Slots")]
    public Image[] cardImages; // Assign Card1–CardN here in Inspector

    [Header("Settings")]
    public string packType = "bronze_pack"; // updated default

    [Header("References")]
    public DropHistoryController dropHistoryController;

    void Start()
    {
        continueButton.onClick.AddListener(() =>
        {
            packPanel.SetActive(false);
            dropHistoryPanel.SetActive(true);
            dropHistoryController.RefreshDropHistory();
        });
    }
    public void OpenPackOfType(string key)
    {
        packType = key;   // e.g., "silver_pack"
        OpenPack();
    }
    public void OpenPack()
    {
        var mgr = DropConfigManager.Instance;
        var rarities = mgr.PullCardRarities(packType, out bool pityTriggered, out string pityType);

        Debug.Log($"Pulled {rarities.Count} cards. PityTriggered={pityTriggered}({pityType})");

        for (int i = 0; i < cardImages.Length; i++)
        {
            if (i < rarities.Count)
            {
                string rarity = rarities[i].ToLowerInvariant();
                cardImages[i].color = GetColorForRarity(rarity);
            }
            else
            {
                cardImages[i].color = Color.white;
            }
        }
    }

    private Color GetColorForRarity(string rarity)
    {
        switch (rarity)
        {
            case "common":    return new Color32(150,150,150,255);
            case "uncommon":  return new Color32(46,204,113,255); // green
            case "rare":      return new Color32(0,112,221,255);
            case "epic":      return new Color32(163,53,238,255);
            case "legendary": return new Color32(255,204,0,255);
            default:          return Color.white;
        }
    }
}
