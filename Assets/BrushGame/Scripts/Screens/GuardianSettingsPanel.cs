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
    [SerializeField] private Sprite _soundOnIcon;
    [SerializeField] private Sprite _soundOffIcon;
    [SerializeField] private Button _voiceButton;
    [SerializeField] private Text _voiceLabel;

    [Header("게임 설정 탭")]
    [SerializeField] private InputField _zoneSecondsInput;
    [SerializeField] private Button _zoneSecondsApply;
    [Tooltip("현재 값, 범위 안내, 잘못 입력했을 때 알림")]
    [SerializeField] private Text _zoneSecondsNote;
    [SerializeField] private Button _resetButton;

    [SerializeField] private Button _closeButton;

    [Header("확인 팝업 (양치 시간 변경, 초기화 함께 사용)")]
    [SerializeField] private GameObject _confirmPanel;
    [SerializeField] private Text _confirmMessage;
    [SerializeField] private Button _confirmOkButton;
    [SerializeField] private Text _confirmOkLabel;
    [SerializeField] private Button _confirmCancelButton;

    public event Action ResetConfirmed;

    private static GuardianSettings Settings => SaveStore.Data.settings;

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
    }

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
      _voiceButton.onClick.AddListener(() =>
      {
        Settings.voiceGuide = !Settings.voiceGuide;
        SaveAndRefresh();
      });

      _zoneSecondsInput.contentType = InputField.ContentType.IntegerNumber;
      _zoneSecondsInput.characterLimit = 2;
      _zoneSecondsApply.onClick.AddListener(RequestZoneSeconds);
      _resetButton.onClick.AddListener(() => Confirm("처음부터 다시 시작할까요?\n기록이 모두 지워져요", "초기화", () =>
      {
        Close();
        ResetConfirmed?.Invoke();
      }));

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
      _refreshing = false;
      _voiceLabel.text = Settings.voiceGuide ? "켜짐" : "꺼짐";
      _zoneSecondsInput.text = Settings.zoneSeconds.ToString();
      ShowZoneNote(false);
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
