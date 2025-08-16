using UnityEngine;
using UnityEngine.UI;

[System.Obsolete("Legacy single-button market. Use BoosterMarketAuto instead.")]
public class BoosterMarketController : MonoBehaviour
{
  public Button buyBronzePackButton;
  public GameObject marketPanel;
  public GameObject packPanel;

  void Start()
  {
    buyBronzePackButton.onClick.AddListener(() =>
    {
      marketPanel.SetActive(false);
      packPanel.SetActive(true);

      var opener = packPanel.GetComponentInChildren<PackOpeningController>(true);
      if (opener != null) { opener.packType = "bronze_pack"; opener.OpenPack(); }
    });
  }
}
