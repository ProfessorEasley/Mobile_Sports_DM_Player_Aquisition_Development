using UnityEngine;
using UnityEngine.UI;

public class PackOpeningController : MonoBehaviour
{
  public Button continueButton;
  public GameObject packPanel;
  public GameObject dropHistoryPanel;

  void Start()
  {
    continueButton.onClick.AddListener(() => {
      packPanel.SetActive(false);
      dropHistoryPanel.SetActive(true);
    });
  }
}
