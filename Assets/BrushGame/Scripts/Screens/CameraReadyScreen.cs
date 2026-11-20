using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   카메라 준비: 손이 화면에 잠깐 안정적으로 보이면 3초 카운트다운 후 시작.
  ///   카운트다운 중 손이 사라지면 처음부터 다시 센다.
  /// </summary>
  public class CameraReadyScreen : ScreenBase
  {
    [SerializeField] private BrushInputHub _input;
    [SerializeField] private Text _guide;
    [SerializeField] private GameObject _countdownRoot;
    [SerializeField] private Text _countdown;
    [SerializeField] private Button _backButton;

    [SerializeField] private string _waitingLine = "칫솔 든 손을 화면에 보여줘!";
    [SerializeField] private string _readyLine = "좋아! 그대로 준비~";
    [Tooltip("손이 이만큼 계속 보여야 카운트다운을 시작한다 (초)")]
    [SerializeField, Min(0f)] private float _steadySec = 0.5f;
    [SerializeField, Min(1f)] private float _countdownSec = 3f;

    private float _visibleSec;
    private bool _finished;
    private bool _wasVisible;
    private bool _saidReady;

    public event Action Finished;
    public event Action BackPressed;

    public override void OnShow()
    {
      _visibleSec = 0f;
      _finished = false;
      _wasVisible = false;
      _saidReady = false;
      Refresh();
      SoundManager.Voice("camera_title", SoundManager.Say.Interrupt);
      SoundManager.Voice("camera_hint", SoundManager.Say.Queue);
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Update()
    {
      if (_finished)
      {
        return;
      }
      var visible = _input.HandVisible;
      _visibleSec = visible ? _visibleSec + Time.deltaTime : 0f;
      Refresh();

      if (visible != _wasVisible)
      {
        _wasVisible = visible;
        SoundManager.Sfx(visible ? "hand_found" : "hand_lost", 0.8f);
        if (!visible)
        {
          _saidReady = false;
        }
      }
      if (!_saidReady && _visibleSec >= _steadySec)
      {
        // 카운트다운 시작: "좋아! 그대로 준비~"
        _saidReady = true;
        SoundManager.Voice("camera_ready", SoundManager.Say.Interrupt, 3f);
      }
      else if (!visible)
      {
        // 처음 안내가 끝난 뒤에도 손이 안 보이면 5초마다
        SoundManager.Voice("camera_wait", SoundManager.Say.IfFree, 5f);
      }

      if (_visibleSec >= _steadySec + _countdownSec)
      {
        _finished = true;
        Finished?.Invoke();
      }
    }

    private void Refresh()
    {
      var counting = _visibleSec >= _steadySec;
      _guide.text = _visibleSec > 0f ? _readyLine : _waitingLine;
      _countdownRoot.SetActive(counting);
      if (counting)
      {
        var remaining = _steadySec + _countdownSec - _visibleSec;
        _countdown.text = Mathf.Max(1, Mathf.CeilToInt(remaining)).ToString();
      }
    }
  }
}
