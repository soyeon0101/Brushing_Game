using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   상단 환자 반응 알림. 한 번에 하나만 표시하고, 표시 중 새 메시지가 오면 내용을 교체한다.
  ///   등장("툭") → 유지 → 퇴장. 코루틴 대신 상태 하나로 관리해 애니메이션이 겹치지 않는다.
  /// </summary>
  public class ReactionToastUI : MonoBehaviour
  {
    private enum Phase { Hidden, Entering, Holding, Exiting }

    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Text _text;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _iconRing;

    [Header("Timing (초)")]
    [SerializeField, Range(0.45f, 0.6f)] private float _enterSec = 0.5f;
    [SerializeField, Range(2.5f, 3f)] private float _holdSec = 3f;
    [SerializeField, Range(0.5f, 0.7f)] private float _exitSec = 0.6f;

    [Header("Motion")]
    [Tooltip("등장 시 위에서 내려오는 거리")]
    [SerializeField] private float _enterOffsetY = 70f;
    [SerializeField, Range(0.5f, 1f)] private float _enterStartScale = 0.85f;
    [Tooltip("퇴장 시 위로 올라가는 거리")]
    [SerializeField] private float _exitOffsetY = 30f;
    [Tooltip("표시 중 내용이 바뀔 때 살짝 튕기는 크기")]
    [SerializeField] private float _punchScale = 1.06f;
    [SerializeField] private float _punchSec = 0.18f;

    private Phase _phase = Phase.Hidden;
    private float _t;
    private float _punchT = -1f;
    private Vector2 _basePos;

    public bool IsShowing => _phase == Phase.Entering || _phase == Phase.Holding;
    public int CurrentPriority { get; private set; } = int.MaxValue;

    private void Awake()
    {
      _basePos = _panel.anchoredPosition;
      Apply(0f, _enterOffsetY, _enterStartScale);
    }

    /// <summary>priority는 작을수록 중요. 우선순위 판단은 호출하는 쪽에서 한다.</summary>
    public void Show(string message, Color moodColor, Color iconColor, int priority)
    {
      _text.text = message;
      _iconRing.color = moodColor;
      _icon.color = iconColor;
      CurrentPriority = priority;

      switch (_phase)
      {
        case Phase.Hidden:
          SetPhase(Phase.Entering, 0f);
          break;
        case Phase.Exiting:
          // 사라지던 중이면 현재 모습에서 이어서 다시 등장
          SetPhase(Phase.Entering, (1f - Mathf.Clamp01(_t / _exitSec)) * _enterSec);
          break;
        case Phase.Entering:
          break;
        case Phase.Holding:
          SetPhase(Phase.Holding, 0f);
          _punchT = 0f;
          break;
      }
    }

    public void Dismiss()
    {
      if (IsShowing)
      {
        SetPhase(Phase.Exiting, 0f);
      }
    }

    private void SetPhase(Phase phase, float t)
    {
      _phase = phase;
      _t = t;
    }

    private void Update()
    {
      if (_phase == Phase.Hidden)
      {
        return;
      }
      _t += Time.deltaTime;

      switch (_phase)
      {
        case Phase.Entering:
        {
          var p = Mathf.Clamp01(_t / _enterSec);
          var e = EaseOutBack(p);
          Apply(Mathf.Clamp01(p * 2.5f), _enterOffsetY * (1f - e), Mathf.LerpUnclamped(_enterStartScale, 1f, e));
          if (p >= 1f)
          {
            SetPhase(Phase.Holding, 0f);
          }
          break;
        }
        case Phase.Holding:
        {
          var scale = 1f;
          if (_punchT >= 0f)
          {
            _punchT += Time.deltaTime;
            var k = Mathf.Clamp01(_punchT / _punchSec);
            scale = Mathf.Lerp(_punchScale, 1f, k);
            if (k >= 1f)
            {
              _punchT = -1f;
            }
          }
          Apply(1f, 0f, scale);
          if (_t >= _holdSec)
          {
            SetPhase(Phase.Exiting, 0f);
          }
          break;
        }
        case Phase.Exiting:
        {
          var p = Mathf.Clamp01(_t / _exitSec);
          var e = p * p;
          Apply(1f - p, _exitOffsetY * e, 1f);
          if (p >= 1f)
          {
            SetPhase(Phase.Hidden, 0f);
            CurrentPriority = int.MaxValue;
          }
          break;
        }
      }
    }

    private void Apply(float alpha, float offsetY, float scale)
    {
      _group.alpha = alpha;
      _panel.anchoredPosition = _basePos + new Vector2(0f, offsetY);
      _panel.localScale = new Vector3(scale, scale, 1f);
    }

    // 약하게 넘쳤다 돌아오는 "툭" 느낌
    private static float EaseOutBack(float x)
    {
      const float c1 = 1.2f;
      const float c3 = c1 + 1f;
      var m = x - 1f;
      return 1f + c3 * m * m * m + c1 * m * m;
    }
  }
}
