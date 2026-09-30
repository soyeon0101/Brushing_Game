using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>오늘의 환자 확인: 치료 전 모습과 부탁 대사</summary>
  public class PatientIntroScreen : ScreenBase
  {
    [SerializeField] private Image _portrait;
    [SerializeField] private Text _name;
    [SerializeField] private Text _line;
    [SerializeField] private Button _goButton;
    [SerializeField] private Button _backButton;

    public event Action Confirmed;
    public event Action BackPressed;

    public void Setup(PatientInfo patient)
    {
      ArtSlot.Apply(_portrait, patient.Portrait);
      _name.text = patient.displayName;
      _line.text = $"\"{patient.requestLine}\"";
    }

    private void Awake()
    {
      _goButton.onClick.AddListener(() => Confirmed?.Invoke());
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }
  }
}
