using UnityEngine;

[System.Serializable]
public class RarityWeights {
  public float common, rare, epic, legendary;
}

[System.Serializable]
public class Pack {
  public string id;
  public int cost;
  public string currency;
  public RarityWeights rarityWeights;
}

[System.Serializable]
public class DupeRules {
  public int common, rare, epic, legendary;
}

[System.Serializable]
public class ConfigData {
  public Pack[] packs;
  public DupeRules dupeRules;
  public string[] samplePull;
}

public class CCASConfig : MonoBehaviour {
  public static ConfigData Instance { get; private set; }

  void Awake() {
    string path = Application.streamingAssetsPath + "/CCAS_Config.json";
    string json = System.IO.File.ReadAllText(path);
    Instance = JsonUtility.FromJson<ConfigData>(json);
    Debug.Log($"[CCASConfig] Loaded {Instance.packs.Length} packs");
  }
}

