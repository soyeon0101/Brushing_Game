using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>보호자 설정 (로비 위에 뜨는 창). 바꾸는 즉시 저장한다.</summary>
  public class GuardianSettingsPanel : MonoBehaviour
  {
    private static readonly int[] ZoneSecondOptions = { 20, 25, 30 };

    [SerializeField] private Button _soundButton;
    [SerializeField] private Text _soundLabel;
    [SerializeField] private Button _musicButton;
    [SerializeField] private Text _musicLabel;
    [SerializeField] private Button _effectButton;
    [SerializeField] private Text _effectLabel;
    [SerializeField] private Button _voiceButton;
    [SerializeField] private Text _voiceLabel;
    [Tooltip("20초, 25초, 30초 순서")]
    [SerializeField] private ChoiceCard[] _zoneSecondCards;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _closeButton;

    [Header("초기화 확인")]
    [SerializeField] private GameObject _confirmPanel;
    [SerializeField] private Button _confirmResetButton;
    [SerializeField] private Button _cancelResetButton;

    public event Action ResetConfirmed;

    private static GuardianSettings Settings => SaveStore.Data.settings;

    public void Open()
    {
      gameObject.SetActive(true);
      _confirmPanel.SetActive(false);
      Refresh();
    }

    public void Close()
    {
      gameObject.SetActive(false);
    }

    private void Awake()
    {
      _soundButton.onClick.AddListener(() =>
      {
        Settings.sound = !Settings.sound;
        Settings.ApplyAudio();
        SaveAndRefresh();
      });
      _musicButton.onClick.AddListener(() =>
      {
        Settings.music = !Settings.music;
        Settings.ApplyAudio();
        SaveAndRefresh();
      });
      _effectButton.onClick.AddListener(() =>
      {
        Settings.effects = !Settings.effects;
        Settings.ApplyAudio();
        SaveAndRefresh();
      });
      _voiceButton.onClick.AddListener(() =>
      {
        Settings.voiceGuide = !Settings.voiceGuide;
        SaveAndRefresh();
      });
      for (var i = 0; i < _zoneSecondCards.Length; i++)
      {
        var seconds = ZoneSecondOptions[i];
        _zoneSecondCards[i].Button.onClick.AddListener(() =>
        {
          Settings.zoneSeconds = seconds;
          SaveAndRefresh();
        });
      }
      _resetButton.onClick.AddListener(() => _confirmPanel.SetActive(true));
      _cancelResetButton.onClick.AddListener(() => _confirmPanel.SetActive(false));
      _confirmResetButton.onClick.AddListener(() =>
      {
        Close();
        ResetConfirmed?.Invoke();
      });
      _closeButton.onClick.AddListener(Close);
    }

    private void SaveAndRefresh()
    {
      SaveStore.Save();
      Refresh();
    }

    private void Refresh()
    {
      _soundLabel.text = Settings.sound ? "켜짐" : "꺼짐";
      _musicLabel.text = Settings.music ? "켜짐" : "꺼짐";
      _effectLabel.text = Settings.effects ? "켜짐" : "꺼짐";
      _voiceLabel.text = Settings.voiceGuide ? "켜짐" : "꺼짐";
      for (var i = 0; i < _zoneSecondCards.Length; i++)
      {
        _zoneSecondCards[i].SetSelected(ZoneSecondOptions[i] == Settings.zoneSeconds);
      }
    }
  }
}
