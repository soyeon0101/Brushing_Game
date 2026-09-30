using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>치료 완료: 치료 전/후 비교와 고맙다는 대사</summary>
  public class CompleteScreen : ScreenBase
  {
    [SerializeField] private Image _before;
    [SerializeField] private Image _after;
    [SerializeField] private Text _thanks;
    [SerializeField] private Button _nextButton;

    public event Action Confirmed;

    public void Setup(PatientInfo patient)
    {
      ArtSlot.Apply(_before, patient.before);
      ArtSlot.Apply(_after, patient.after);
      _thanks.text = $"\"{patient.thanksLine}\"";
    }

    private void Awake()
    {
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke());
    }
  }
}
