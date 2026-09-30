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

    public event Action Finished;
    public event Action BackPressed;

    public override void OnShow()
    {
      _visibleSec = 0f;
      _finished = false;
      Refresh();
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
      _visibleSec = _input.HandVisible ? _visibleSec + Time.deltaTime : 0f;
      Refresh();

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
