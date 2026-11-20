using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>이번 끼니의 환자 확인: 치료 전 모습과 부탁 대사</summary>
  public class PatientIntroScreen : ScreenBase
  {
    [Tooltip("\"아침에 온 친구\"처럼 끼니에 따라 바뀌는 제목")]
    [SerializeField] private Text _title;
    [SerializeField] private Image _portrait;
    [SerializeField] private Text _name;
    [SerializeField] private Text _line;
    [SerializeField] private Button _goButton;
    [SerializeField] private Button _backButton;

    public event Action Confirmed;
    public event Action BackPressed;

    private string _patientId;

    public void Setup(PatientInfo patient, MealSlot slot)
    {
      _patientId = patient.id;
      _title.text = SaveData.VisitorTitle(slot);
      ArtSlot.Apply(_portrait, patient.Portrait);
      _name.text = patient.displayName;
      _line.text = $"\"{patient.requestLine}\"";
    }

    /// <summary>환자가 부탁 대사를 직접 말한다</summary>
    public override void OnShow()
    {
      SoundManager.Voice($"{_patientId}_request", SoundManager.Say.Interrupt);
    }

    private void Awake()
    {
      _goButton.onClick.AddListener(() => Confirmed?.Invoke());
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }
  }
}
