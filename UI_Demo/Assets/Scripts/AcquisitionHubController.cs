using UnityEngine;
using TMPro;
using UnityEngine.UI; 

public class AcquisitionHubController : MonoBehaviour
{
  public TMP_Text coinsText;
  public TMP_Text emotionText;
  public Button   goToMarketButton;
  public Button   openPackButton;
  public GameObject hubPanel, marketPanel, packPanel;

  void Start()
  {
    // Populate from config
    var cfg = CCASConfig.Instance;
    coinsText.text = $"Coins: {cfg.packs[0].cost}";
    emotionText.text = "Emotion: Neutral";

    // Wire buttons
    goToMarketButton.onClick.AddListener(() => {
      hubPanel.SetActive(false);
      marketPanel.SetActive(true);
    });
    openPackButton.onClick.AddListener(() => {
      hubPanel.SetActive(false);
      packPanel.SetActive(true);
    });
  }
}
