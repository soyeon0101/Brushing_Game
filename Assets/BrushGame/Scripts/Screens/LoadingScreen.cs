using System;
using UnityEngine;

namespace BrushGame
{
  /// <summary>치료 시작 전 짧은 전환 화면 ("우주 치과 입장 중...")</summary>
  public class LoadingScreen : ScreenBase
  {
    [Tooltip("왼쪽 기준으로 늘어나는 진행 막대")]
    [SerializeField] private RectTransform _barFill;
    [SerializeField, Min(0.1f)] private float _duration = 1.5f;

    private float _t;
    private bool _finished;

    public event Action Finished;

    public override void OnShow()
    {
      _t = 0f;
      _finished = false;
      SetBar(0f);
    }

    private void Update()
    {
      if (_finished)
      {
        return;
      }
      _t += Time.deltaTime;
      var p = Mathf.Clamp01(_t / _duration);
      SetBar(p);
      if (p >= 1f)
      {
        _finished = true;
        Finished?.Invoke();
      }
    }

    private void SetBar(float p)
    {
      _barFill.anchorMax = new Vector2(p, 1f);
    }
  }
}
