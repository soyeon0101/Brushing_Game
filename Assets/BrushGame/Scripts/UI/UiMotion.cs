using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   UI 하나에 반복 움직임을 준다 (로비 꾸밈용). 인스펙터에서 종류·세기·속도를 바꾼다.
  ///   Breathe: 숨쉬듯 위아래로 눌렸다 펴짐 (피벗 기준) · Pulse: 통째로 커졌다 작아짐 · Bob: 위아래 둥실
  ///   Twinkle: 깜빡이며 커졌다 작아짐 · Rise: 위로 떠오르며 사라지고 다시 처음부터 · Sweep: 왼쪽에서 오른쪽으로 지나가고 쉼
  ///   꺼지면 처음 자리·크기로 돌아간다.
  /// </summary>
  [RequireComponent(typeof(RectTransform))]
  public class UiMotion : MonoBehaviour
  {
    public enum Mode { Breathe, Pulse, Bob, Twinkle, Rise, Sweep }

    [SerializeField] private Mode _mode = Mode.Pulse;
    [Tooltip("Breathe/Pulse/Twinkle: 크기 변화 비율 (0.03 = 3%) · Bob: 움직이는 거리 · Rise/Sweep: 움직이는 거리")]
    [SerializeField] private float _amount = 0.03f;
    [Tooltip("한 번 왕복(또는 한 번 지나가기)에 걸리는 초")]
    [SerializeField] private float _period = 2f;
    [Tooltip("0~1. 같은 움직임 여러 개의 박자를 어긋나게 한다")]
    [Range(0f, 1f)] [SerializeField] private float _phase;
    [Tooltip("Sweep: 한 번 지나간 뒤 쉬는 비율 (0.5 = 절반은 쉼)")]
    [Range(0f, 0.9f)] [SerializeField] private float _rest = 0.45f;

    private RectTransform _rt;
    private CanvasGroup _group;
    private Vector2 _home;
    private Vector3 _scale;
    private bool _ready;

    public void Configure(Mode mode, float amount, float period, float phase = 0f, float rest = 0.45f)
    {
      _mode = mode;
      _amount = amount;
      _period = period;
      _phase = phase;
      _rest = rest;
    }

    private void OnEnable()
    {
      _rt = (RectTransform)transform;
      if (!_ready)
      {
        _home = _rt.anchoredPosition;
        _scale = _rt.localScale;
        _ready = true;
      }
      if (_mode == Mode.Twinkle || _mode == Mode.Rise)
      {
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
        {
          _group = gameObject.AddComponent<CanvasGroup>();
          _group.blocksRaycasts = false;
          _group.interactable = false;
        }
      }
    }

    private void OnDisable()
    {
      if (!_ready)
      {
        return;
      }
      _rt.anchoredPosition = _home;
      _rt.localScale = _scale;
      if (_group != null)
      {
        _group.alpha = 1f;
      }
    }

    private void Update()
    {
      var period = Mathf.Max(0.05f, _period);
      var t = Mathf.Repeat(Time.time / period + _phase, 1f);
      var wave = Mathf.Sin(t * Mathf.PI * 2f);
      switch (_mode)
      {
        case Mode.Breathe:
          _rt.localScale = new Vector3(_scale.x * (1f - wave * _amount * 0.5f), _scale.y * (1f + wave * _amount), _scale.z);
          break;
        case Mode.Pulse:
          _rt.localScale = _scale * (1f + (wave * 0.5f + 0.5f) * _amount);
          break;
        case Mode.Bob:
          _rt.anchoredPosition = _home + new Vector2(0f, wave * _amount);
          break;
        case Mode.Twinkle:
          var k = wave * 0.5f + 0.5f;
          _rt.localScale = _scale * Mathf.Lerp(1f - _amount, 1f, k);
          _group.alpha = Mathf.Lerp(0.2f, 1f, k);
          break;
        case Mode.Rise:
          _rt.anchoredPosition = _home + new Vector2(Mathf.Sin(t * Mathf.PI * 3f) * 12f, t * _amount);
          _group.alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - t) / 0.4f);
          break;
        case Mode.Sweep:
          var move = Mathf.Clamp01(t / Mathf.Max(0.05f, 1f - _rest));
          _rt.anchoredPosition = _home + new Vector2(Mathf.SmoothStep(0f, 1f, move) * _amount, 0f);
          break;
      }
    }
  }
}
