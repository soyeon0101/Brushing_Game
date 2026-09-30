using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   메인 로비: 내 프로필, 오늘의 스티커, 이번 끼니의 환자, 치료 시작.
  ///   지금 끼니의 도장을 이미 받았으면 치료 시작 전에 연습할지 물어본다 (치료는 되지만 도장은 또 주지 않는다).
  /// </summary>
  public class LobbyScreen : ScreenBase
  {
    [Header("프로필")]
    [SerializeField] private Image _avatar;
    [SerializeField] private Text _nickname;

    [Header("오늘의 스티커 (아침/점심/저녁 순)")]
    [SerializeField] private GameObject[] _todayStickers;

    [Header("이번 끼니의 환자")]
    [Tooltip("\"아침에 온 친구\"처럼 끼니에 따라 바뀌는 제목")]
    [SerializeField] private Text _patientHeader;
    [SerializeField] private Image _patientPortrait;
    [SerializeField] private Text _patientName;
    [SerializeField] private Text _patientLine;

    [Header("버튼")]
    [SerializeField] private Button _treatButton;
    [SerializeField] private Button _bookButton;
    [SerializeField] private Button _stickerButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _badgeButton;

    [Header("이미 도장을 받은 끼니에 치료 시작")]
    [SerializeField] private GameObject _practicePanel;
    [SerializeField] private Text _practiceMessage;
    [SerializeField] private Button _practiceButton;
    [SerializeField] private Button _practiceCancelButton;

    private SaveData _data;

    public event Action TreatPressed;
    public event Action BookPressed;
    public event Action StickersPressed;
    public event Action SettingsPressed;
    public event Action BadgesPressed;

    public void Setup(SaveData data, PlayerCharacter character, PatientInfo patient, MealSlot slot)
    {
      _data = data;
      _practicePanel.SetActive(false);
      _nickname.text = $"{data.profile.nickname} 선생님";
      ArtSlot.Apply(_avatar, character?.Portrait);

      var today = SaveData.DateKey(DateTime.Now);
      for (var i = 0; i < _todayStickers.Length; i++)
      {
        _todayStickers[i].SetActive(data.HasSticker(today, (MealSlot)i));
      }

      _patientHeader.text = SaveData.VisitorTitle(slot);
      _patientName.text = patient.displayName;
      _patientLine.text = $"\"{patient.requestLine}\"";
      ArtSlot.Apply(_patientPortrait, patient.Portrait);
    }

    private void Awake()
    {
      _treatButton.onClick.AddListener(OnTreat);
      _bookButton.onClick.AddListener(() => BookPressed?.Invoke());
      _stickerButton.onClick.AddListener(() => StickersPressed?.Invoke());
      _settingsButton.onClick.AddListener(() => SettingsPressed?.Invoke());
      _badgeButton.onClick.AddListener(() => BadgesPressed?.Invoke());
      _practiceButton.onClick.AddListener(() =>
      {
        _practicePanel.SetActive(false);
        TreatPressed?.Invoke();
      });
      _practiceCancelButton.onClick.AddListener(() => _practicePanel.SetActive(false));
    }

    /// <summary>로비에 오래 머물다 끼니가 바뀔 수 있으므로 누른 시각으로 판단한다</summary>
    private void OnTreat()
    {
      var now = DateTime.Now;
      var slot = SaveData.SlotAt(now);
      if (_data == null || !_data.HasSticker(SaveData.DateKey(now), slot))
      {
        TreatPressed?.Invoke();
        return;
      }
      _practiceMessage.text = $"{SaveData.SlotName(slot)} 도장은 벌써 받았어!\n그래도 연습할래?";
      _practicePanel.SetActive(true);
    }
  }
}
