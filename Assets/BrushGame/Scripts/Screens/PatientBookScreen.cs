using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  [Serializable]
  public class PatientBookCard
  {
    public GameObject root;
    public Image portrait;
    public Text name;
    [Tooltip("아직 치료하지 않은 환자를 가리는 덮개")]
    public GameObject locked;
  }

  /// <summary>환자 도감: 한 번이라도 치료한 환자가 열린다</summary>
  public class PatientBookScreen : ScreenBase
  {
    [SerializeField] private PatientBookCard[] _cards;
    [SerializeField] private Button _backButton;

    public event Action BackPressed;

    public void Setup(GameCatalog catalog, SaveData data)
    {
      for (var i = 0; i < _cards.Length; i++)
      {
        var card = _cards[i];
        var used = i < catalog.patients.Length;
        card.root.SetActive(used);
        if (!used)
        {
          continue;
        }
        var patient = catalog.patients[i];
        var cured = data.curedPatientIds.Contains(patient.id);
        card.locked.SetActive(!cured);
        card.name.text = cured ? patient.displayName : "???";
        ArtSlot.Apply(card.portrait, patient.after != null ? patient.after : patient.before);
      }
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }
  }
}
