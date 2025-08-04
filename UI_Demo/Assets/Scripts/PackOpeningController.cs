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
    public Image[] cardImages; // Assign Card1–Card5 here in Inspector

    [Header("Settings")]
    public string packType = "basic_pack";
    
    [Header("References")]
    public DropHistoryController dropHistoryController;
  void Start()
  {
    continueButton.onClick.AddListener(() => {
        packPanel.SetActive(false);
        dropHistoryPanel.SetActive(true);

        dropHistoryController.RefreshDropHistory(); // Safe direct call
    });
  }

  public void OpenPack()
  {
      List<string> rarities = DropConfigManager.Instance.PullCardRarities(packType);
      Debug.Log("Pulled " + rarities.Count + " cards");

      for (int i = 0; i < cardImages.Length; i++)
      {
          if (i < rarities.Count)
          {
              string rarity = rarities[i].ToLower(); // normalize
              Debug.Log($"Card {i + 1}: {rarity}");
              cardImages[i].color = GetColorForRarity(rarity);
          }
          else
          {
              cardImages[i].color = Color.white; // fallback if not pulled
          }
      }
  }

    private Color GetColorForRarity(string rarity)
    {
    switch (rarity.ToLower())
    {
      // case "common": return new Color(0.7f, 0.7f, 0.7f);   // gray
      // case "rare": return new Color(0.3f, 0.6f, 1.0f);      // blue
      // case "epic": return new Color(0.7f, 0.3f, 1.0f);      // purple
      // case "legendary": return new Color(1.0f, 0.85f, 0.1f); // gold
      // default: return Color.white;
        case "common":    return new Color32(150, 150, 150, 255); // dark gray
        case "rare":      return new Color32(0, 112, 221, 255);   // strong blue
        case "epic":      return new Color32(163, 53, 238, 255);  // strong purple
        case "legendary": return new Color32(255, 204, 0, 255);   // gold/yellow
        default:          return Color.white;
      }
    }
}
