using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>메인 로비: 내 프로필, 오늘의 스티커, 오늘의 환자, 치료 시작</summary>
  public class LobbyScreen : ScreenBase
  {
    [Header("프로필")]
    [SerializeField] private Image _avatar;
    [SerializeField] private Text _nickname;

    [Header("오늘의 스티커 (아침/점심/저녁 순)")]
    [SerializeField] private GameObject[] _todayStickers;

    [Header("오늘의 환자")]
    [SerializeField] private Image _patientPortrait;
    [SerializeField] private Text _patientName;
    [SerializeField] private Text _patientLine;

    [Header("버튼")]
    [SerializeField] private Button _treatButton;
    [SerializeField] private Button _bookButton;
    [SerializeField] private Button _stickerButton;
    [SerializeField] private Button _settingsButton;

    public event Action TreatPressed;
    public event Action BookPressed;
    public event Action StickersPressed;
    public event Action SettingsPressed;

    public void Setup(SaveData data, PlayerCharacter character, PatientInfo patient)
    {
      _nickname.text = $"{data.profile.nickname} 선생님";
      ArtSlot.Apply(_avatar, character?.Portrait);

      var today = SaveData.DateKey(DateTime.Now);
      for (var i = 0; i < _todayStickers.Length; i++)
      {
        _todayStickers[i].SetActive(data.HasSticker(today, (MealSlot)i));
      }

      _patientName.text = patient.displayName;
      _patientLine.text = $"\"{patient.requestLine}\"";
      ArtSlot.Apply(_patientPortrait, patient.Portrait);
    }

    private void Awake()
    {
      _treatButton.onClick.AddListener(() => TreatPressed?.Invoke());
      _bookButton.onClick.AddListener(() => BookPressed?.Invoke());
      _stickerButton.onClick.AddListener(() => StickersPressed?.Invoke());
      _settingsButton.onClick.AddListener(() => SettingsPressed?.Invoke());
    }
  }
}
