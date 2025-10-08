using UnityEngine;
using TMPro;
using UnityEngine.UI; 

public class AcquisitionHubController : MonoBehaviour
{
  public TMP_Text coinsText;

  public Button   goToMarketButton;
  public Button   openPackButton;
  public GameObject hubPanel, marketPanel, packPanel;

  [Header("Defaults")]
  public string defaultPackKey = "bronze_pack";

  void Start()
  {
    // Show placeholder currency 
    coinsText.text = "Coins: 1200";

    goToMarketButton.onClick.AddListener(() => {
      hubPanel.SetActive(false);
      marketPanel.SetActive(true);
    });

    openPackButton.onClick.AddListener(() => {
      hubPanel.SetActive(false);
      packPanel.SetActive(true);
      // PackOpeningController will use its serialized packType; set it if you want default
      var opener = packPanel.GetComponentInChildren<PackOpeningController>(true);
      if (opener != null) opener.packType = defaultPackKey;
    });
  }
}
