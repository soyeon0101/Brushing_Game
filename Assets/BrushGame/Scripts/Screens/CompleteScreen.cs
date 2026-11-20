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

    private string _patientId;

    public void Setup(PatientInfo patient)
    {
      _patientId = patient.id;
      ArtSlot.Apply(_before, patient.before);
      ArtSlot.Apply(_after, patient.after);
      _thanks.text = $"\"{patient.thanksLine}\"";
    }

    /// <summary>로봇 친구 "치료 완료!" → 이어서 환자가 고맙다고 말한다</summary>
    public override void OnShow()
    {
      SoundManager.Voice("complete_title", SoundManager.Say.Interrupt);
      SoundManager.Voice($"{_patientId}_thanks", SoundManager.Say.Queue);
    }

    private void Awake()
    {
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke());
    }
  }
}
