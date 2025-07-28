using UnityEngine;
using UnityEngine.UI;

public class BoosterMarketController : MonoBehaviour
{
  public Button buyBasicPackButton;
  public GameObject marketPanel;
  public GameObject packPanel;

  void Start()
  {
    buyBasicPackButton.onClick.AddListener(() => {
      marketPanel.SetActive(false);
      packPanel.SetActive(true);
    });
  }
}
