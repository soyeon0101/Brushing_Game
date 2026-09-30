using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   치료 시작 전 우주 치과 입장 화면.
  ///   별이 아래로 흐르는 우주를 우주선이 날아가고, 위에서 우주 치과가 다가오면 우주선이 쏙 들어가며 끝난다.
  ///   그림은 Image 자리에 넣기만 하면 되고, 움직임은 전부 코드로 한다 (영상 파일 없음).
  /// </summary>
  public class LoadingScreen : ScreenBase
  {
    [Tooltip("왼쪽 기준으로 늘어나는 진행 막대")]
    [SerializeField] private RectTransform _barFill;
    [SerializeField] private Text _message;
    [SerializeField] private RectTransform _rocket;
    [SerializeField] private RectTransform _clinic;
    [Tooltip("흐르는 별이 만들어질 영역 (화면 전체)")]
    [SerializeField] private RectTransform _starField;
    [SerializeField] private Sprite _starSprite;

    [SerializeField, Min(0.5f)] private float _duration = 3.2f;
    [SerializeField] private string _flyingLine = "우주 치과로 날아가는 중...";
    [SerializeField] private string _arrivedLine = "도착!";

    [Header("움직임 (1080x1920 기준 좌표)")]
    [SerializeField] private Vector2 _rocketStart = new Vector2(0f, -400f);
    [SerializeField] private Vector2 _rocketCruise = new Vector2(0f, -110f);
    [SerializeField] private Vector2 _clinicFrom = new Vector2(0f, 1250f);
    [SerializeField] private Vector2 _clinicAt = new Vector2(0f, 430f);
    [Tooltip("이 진행도부터 우주선이 치과로 들어간다 (0~1)")]
    [SerializeField, Range(0.3f, 0.95f)] private float _dockAt = 0.72f;
    [SerializeField, Min(0)] private int _starCount = 36;

    private RectTransform[] _stars;
    private float[] _starSpeeds;
    private float _t;
    private bool _finished;

    public event Action Finished;

    public override void OnShow()
    {
      _t = 0f;
      _finished = false;
      EnsureStars();
      Apply(0f, 0f);
    }

    private void Update()
    {
      if (_finished)
      {
        return;
      }
      _t += Time.deltaTime;
      var p = Mathf.Clamp01(_t / _duration);
      Apply(p, Time.deltaTime);
      if (p >= 1f)
      {
        _finished = true;
        Finished?.Invoke();
      }
    }

    private void Apply(float p, float dt)
    {
      _barFill.anchorMax = new Vector2(p, 1f);
      _message.text = p < 0.9f ? _flyingLine : _arrivedLine;

      // 치과는 위에서 내려오며 커진다 (우주선이 다가가는 느낌)
      var approach = Smooth(Mathf.Clamp01(p / _dockAt));
      _clinic.anchoredPosition = Vector2.Lerp(_clinicFrom, _clinicAt, approach);
      _clinic.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, approach);

      // 우주선: 올라오며 둥실거리다가, 마지막에 치과 입구로 작아지며 들어간다
      var bob = new Vector2(Mathf.Sin(_t * 5f) * 18f, Mathf.Sin(_t * 7f) * 12f);
      if (p < _dockAt)
      {
        var k = Smooth(p / _dockAt);
        _rocket.anchoredPosition = Vector2.Lerp(_rocketStart, _rocketCruise, k) + bob;
        _rocket.localScale = Vector3.one;
        _rocket.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(_t * 4f) * 6f);
      }
      else
      {
        var k = Smooth((p - _dockAt) / (1f - _dockAt));
        _rocket.anchoredPosition = Vector2.Lerp(_rocketCruise + bob * (1f - k), _clinicAt, k);
        _rocket.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, k);
        _rocket.localEulerAngles = Vector3.zero;
      }

      // 별: 아래로 흐르고, 도착 직전에 빨라진다
      if (_stars == null)
      {
        return;
      }
      var h = _starField.rect.height;
      var boost = p < _dockAt ? 1f : Mathf.Lerp(1f, 2.5f, (p - _dockAt) / (1f - _dockAt));
      for (var i = 0; i < _stars.Length; i++)
      {
        var pos = _stars[i].anchoredPosition;
        pos.y -= _starSpeeds[i] * boost * dt;
        if (pos.y < -h / 2f - 20f)
        {
          pos.y += h + 40f;
        }
        _stars[i].anchoredPosition = pos;
      }
    }

    private void EnsureStars()
    {
      if (_stars != null || _starField == null)
      {
        return;
      }
      var size = _starField.rect.size;
      _stars = new RectTransform[_starCount];
      _starSpeeds = new float[_starCount];
      for (var i = 0; i < _starCount; i++)
      {
        var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_starField, false);
        var near = UnityEngine.Random.value;
        var d = Mathf.Lerp(6f, 16f, near);
        rt.sizeDelta = new Vector2(d, d * Mathf.Lerp(1f, 4f, near));
        rt.anchoredPosition = new Vector2(
          UnityEngine.Random.Range(-size.x / 2f, size.x / 2f),
          UnityEngine.Random.Range(-size.y / 2f, size.y / 2f));
        var img = go.GetComponent<Image>();
        img.sprite = _starSprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0.9f, near));
        _stars[i] = rt;
        // 가까운 별일수록 빠르고 길다 (원근감)
        _starSpeeds[i] = Mathf.Lerp(250f, 900f, near);
      }
    }

    private static float Smooth(float k) => k * k * (3f - 2f * k);
  }
}
