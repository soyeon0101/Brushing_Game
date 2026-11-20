using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   보호자 설정 (로비 위에 뜨는 창). 탭(사운드, 게임 설정 …)으로 나뉜다.
  ///   소리는 바꾸는 즉시, 양치 시간은 확인 팝업을 거쳐 저장한다.
  ///   탭을 늘릴 때는 _tabs에 버튼과 페이지를 한 쌍 더 넣으면 된다.
  /// </summary>
  public class GuardianSettingsPanel : MonoBehaviour
  {
    [Serializable]
    public class Tab
    {
      public Button button;
      public GameObject page;
    }

    /// <summary>볼륨 슬라이더 + 음소거 버튼 한 줄</summary>
    [Serializable]
    public class VolumeRow
    {
      public Slider slider;
      public Button muteButton;
      [Tooltip("글자 버튼일 때 (켜짐/음소거)")]
      public Text muteLabel;
      [Tooltip("아이콘 버튼일 때. 있으면 켬/끔 아이콘을 바꿔 보여준다")]
      public Image muteIcon;
    }

    [Header("탭")]
    [SerializeField] private Tab[] _tabs;
    [SerializeField] private Color _tabOnColor = new Color(1f, 0.55f, 0.78f);
    [SerializeField] private Color _tabOffColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color _tabOnText = Color.white;
    [SerializeField] private Color _tabOffText = new Color(0.29f, 0.23f, 0.55f);

    [Header("사운드 탭")]
    [SerializeField] private VolumeRow _master;
    [SerializeField] private VolumeRow _music;
    [SerializeField] private VolumeRow _effect;
    [Tooltip("안내 음성 (TTS) 크기 + 켜고 끄기")]
    [SerializeField] private VolumeRow _voice;
    [SerializeField] private Sprite _soundOnIcon;
    [SerializeField] private Sprite _soundOffIcon;
    [Tooltip("예전 켜짐/꺼짐 버튼 (볼륨 줄이 없을 때만 쓴다)")]
    [SerializeField] private Button _voiceButton;
    [SerializeField] private Text _voiceLabel;
    [Tooltip("안내 음성 크기를 바꿀 때 들려줄 예시 대사 (Sound/Voice 파일 이름)")]
    [SerializeField] private string _voiceSampleId = "cheer_3";
    [Tooltip("양치 음악 고르기 상자 (목록 순서 = SoundManager 양치 음악 후보 순서). 고르면 미리 들려준다")]
    [SerializeField] private Dropdown _musicDropdown;
    [Tooltip("예전 방식: 곡마다 버튼 (상자가 없을 때만 쓴다)")]
    [SerializeField] private Button[] _musicButtons;

    [Header("게임 설정 탭")]
    [SerializeField] private InputField _zoneSecondsInput;
    [SerializeField] private Button _zoneSecondsApply;
    [Tooltip("현재 값, 범위 안내, 잘못 입력했을 때 알림")]
    [SerializeField] private Text _zoneSecondsNote;
    [SerializeField] private Button _resetButton;
    [Tooltip("양치 기준 다시 재기 (동화의 측정 부분만). 없으면 숨김")]
    [SerializeField] private Button _remeasureButton;
    [Tooltip("지금 저장된 양치 측정값")]
    [SerializeField] private Text _calibrationNote;

    [SerializeField] private Button _closeButton;

    [Header("확인 팝업 (양치 시간 변경, 초기화 함께 사용)")]
    [SerializeField] private GameObject _confirmPanel;
    [SerializeField] private Text _confirmMessage;
    [SerializeField] private Button _confirmOkButton;
    [SerializeField] private Text _confirmOkLabel;
    [SerializeField] private Button _confirmCancelButton;

    public event Action ResetConfirmed;
    public event Action RemeasureRequested;

    private static GuardianSettings Settings => SaveStore.Data.settings;

    private bool HasVoiceRow => _voice != null && _voice.slider != null && _voice.muteButton != null;

    private int _tab;
    private Action _onConfirm;
    private bool _refreshing;

    public void Open()
    {
      gameObject.SetActive(true);
      _confirmPanel.SetActive(false);
      SelectTab(0);
      Refresh();
    }

    public void Close()
    {
      gameObject.SetActive(false);
      // 양치 음악을 미리 들었으면 원래 배경음으로 (설정 창은 로비에서 연다)
      if (_previewing)
      {
        _previewing = false;
        SoundManager.PlayMenuMusic();
      }
    }

    private bool _previewing;

    private void Awake()
    {
      for (var i = 0; i < _tabs.Length; i++)
      {
        var index = i;
        _tabs[i].button.onClick.AddListener(() => SelectTab(index));
      }

      BindVolume(_master, v => Settings.masterVolume = v, () => Settings.sound = !Settings.sound);
      BindVolume(_music, v => Settings.musicVolume = v, () => Settings.music = !Settings.music);
      BindVolume(_effect, v => Settings.effectVolume = v, () => Settings.effects = !Settings.effects);
      if (HasVoiceRow)
      {
        // 크기를 바꾸면 예시 대사로 들려준다 (끄면 하던 말도 바로 멈춘다: ApplyAudio → SoundManager)
        BindVolume(_voice, v =>
        {
          Settings.voiceVolume = v;
          SoundManager.Voice(_voiceSampleId, SoundManager.Say.IfFree);
        }, () => Settings.voiceGuide = !Settings.voiceGuide);
      }
      else if (_voiceButton != null)
      {
        _voiceButton.onClick.AddListener(() =>
        {
          Settings.voiceGuide = !Settings.voiceGuide;
          SoundManager.ApplySettings();
          SaveAndRefresh();
        });
      }

      if (_musicDropdown != null)
      {
        _musicDropdown.onValueChanged.AddListener(PickMusic);
      }
      else
      {
        for (var i = 0; _musicButtons != null && i < _musicButtons.Length; i++)
        {
          var index = i;
          _musicButtons[i].onClick.AddListener(() => PickMusic(index));
        }
      }

      _zoneSecondsInput.contentType = InputField.ContentType.IntegerNumber;
      _zoneSecondsInput.characterLimit = 2;
      _zoneSecondsApply.onClick.AddListener(RequestZoneSeconds);
      _resetButton.onClick.AddListener(() => Confirm("처음부터 다시 시작할까요?\n기록이 모두 지워져요", "초기화", () =>
      {
        Close();
        ResetConfirmed?.Invoke();
      }));

      if (_remeasureButton != null)
      {
        _remeasureButton.onClick.AddListener(() => RemeasureRequested?.Invoke());
      }

      _confirmCancelButton.onClick.AddListener(() => _confirmPanel.SetActive(false));
      _confirmOkButton.onClick.AddListener(() =>
      {
        _confirmPanel.SetActive(false);
        var action = _onConfirm;
        _onConfirm = null;
        action?.Invoke();
      });
      _closeButton.onClick.AddListener(Close);
    }

    private void PickMusic(int index)
    {
      if (_refreshing)
      {
        return;
      }
      Settings.brushingMusic = index;
      _previewing = true;
      SoundManager.PlayTouch();
      SoundManager.PreviewGameplayMusic(index);
      SaveAndRefresh();
    }

    private void BindVolume(VolumeRow row, Action<float> setVolume, Action toggleMute)
    {
      row.slider.minValue = 0f;
      row.slider.maxValue = 1f;
      row.slider.onValueChanged.AddListener(v =>
      {
        if (_refreshing)
        {
          return;
        }
        setVolume(v);
        Settings.ApplyAudio();
        SaveStore.Save();
      });
      row.muteButton.onClick.AddListener(() =>
      {
        toggleMute();
        Settings.ApplyAudio();
        SaveAndRefresh();
      });
    }

    private void SelectTab(int index)
    {
      _tab = Mathf.Clamp(index, 0, _tabs.Length - 1);
      for (var i = 0; i < _tabs.Length; i++)
      {
        var on = i == _tab;
        _tabs[i].page.SetActive(on);
        if (_tabs[i].button.targetGraphic != null)
        {
          _tabs[i].button.targetGraphic.color = on ? _tabOnColor : _tabOffColor;
        }
        var label = _tabs[i].button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
          label.color = on ? _tabOnText : _tabOffText;
        }
      }
    }

    // ---------- 양치 시간 ----------

    private void RequestZoneSeconds()
    {
      if (!int.TryParse(_zoneSecondsInput.text, out var seconds) ||
          seconds < GuardianSettings.MinZoneSeconds || seconds > GuardianSettings.MaxZoneSeconds)
      {
        ShowZoneNote(true);
        return;
      }
      if (seconds == Settings.zoneSeconds)
      {
        ShowZoneNote(false);
        return;
      }
      Confirm($"구역별 양치 시간을\n{Settings.zoneSeconds}초 → {seconds}초로 바꿀까요?", "변경", () =>
      {
        Settings.zoneSeconds = seconds;
        SaveAndRefresh();
      });
    }

    private void ShowZoneNote(bool invalid)
    {
      var range = $"{GuardianSettings.MinZoneSeconds}~{GuardianSettings.MaxZoneSeconds}초 사이 숫자를 넣어주세요";
      _zoneSecondsNote.text = invalid ? range : $"지금: {Settings.zoneSeconds}초  ·  {range}";
      _zoneSecondsNote.color = invalid ? new Color(0.9f, 0.3f, 0.35f) : new Color(0.45f, 0.44f, 0.5f);
    }

    // ---------- 공용 ----------

    private void Confirm(string message, string okLabel, Action onConfirm)
    {
      _confirmMessage.text = message;
      _confirmOkLabel.text = okLabel;
      _onConfirm = onConfirm;
      _confirmPanel.SetActive(true);
    }

    private void SaveAndRefresh()
    {
      SaveStore.Save();
      Refresh();
    }

    private void Refresh()
    {
      _refreshing = true;
      SetRow(_master, Settings.masterVolume, Settings.sound);
      SetRow(_music, Settings.musicVolume, Settings.music);
      SetRow(_effect, Settings.effectVolume, Settings.effects);
      if (HasVoiceRow)
      {
        SetRow(_voice, Settings.voiceVolume, Settings.voiceGuide);
      }
      _refreshing = false;
      if (_voiceLabel != null)
      {
        _voiceLabel.text = Settings.voiceGuide ? "켜짐" : "꺼짐";
      }
      if (_musicDropdown != null && _musicDropdown.options.Count > 0)
      {
        _musicDropdown.SetValueWithoutNotify(Mathf.Clamp(Settings.brushingMusic, 0, _musicDropdown.options.Count - 1));
      }
      // 예전 방식: 고른 양치 음악 버튼만 분홍 (탭과 같은 색)
      for (var i = 0; _musicDropdown == null && _musicButtons != null && i < _musicButtons.Length; i++)
      {
        var on = i == Mathf.Clamp(Settings.brushingMusic, 0, _musicButtons.Length - 1);
        if (_musicButtons[i].targetGraphic != null)
        {
          _musicButtons[i].targetGraphic.color = on ? _tabOnColor : _tabOffColor;
        }
        var label = _musicButtons[i].GetComponentInChildren<Text>(true);
        if (label != null)
        {
          label.color = on ? _tabOnText : _tabOffText;
        }
      }
      _zoneSecondsInput.text = Settings.zoneSeconds.ToString();
      ShowZoneNote(false);
      if (_calibrationNote != null)
      {
        var c = SaveStore.Data.brushCalibration;
        _calibrationNote.text = c != null && c.measured
          ? $"왕복 폭: 손 크기의 {c.swing * 100f:F0}%  ·  속도: 초당 {c.rate:F1}번"
          : "아직 측정하지 않았어요 (기본 기준 사용)";
      }
    }

    private void SetRow(VolumeRow row, float volume, bool on)
    {
      row.slider.value = volume;
      row.slider.interactable = on;
      // 음소거한 줄은 슬라이더를 흐리게
      var group = row.slider.GetComponent<CanvasGroup>();
      if (group == null)
      {
        group = row.slider.gameObject.AddComponent<CanvasGroup>();
      }
      group.alpha = on ? 1f : 0.35f;
      if (row.muteLabel != null)
      {
        row.muteLabel.text = on ? "켜짐" : "음소거";
      }
      if (row.muteIcon != null && _soundOnIcon != null && _soundOffIcon != null)
      {
        row.muteIcon.sprite = on ? _soundOnIcon : _soundOffIcon;
      }
    }
  }
}
