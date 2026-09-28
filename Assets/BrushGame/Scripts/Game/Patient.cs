using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  public enum PatientExpression { Normal, Pain, Happy }

  /// <summary>
  ///   외계인 환자 한 명의 모습. 등장/퇴장, 입 벌리기, 표정 전환, 손 흔들기를 맡는다.
  ///   입속 변화는 MouthView가 한다.
  /// </summary>
  public class Patient : MonoBehaviour
  {
    [SerializeField] private RectTransform _root;
    [Tooltip("환자마다 피부색을 바꿀 부분")]
    [SerializeField] private Graphic[] _skin;
    [SerializeField] private GameObject _normalFace;
    [SerializeField] private GameObject _painFace;
    [SerializeField] private GameObject _happyFace;
    [Tooltip("볼 홍조. 치료가 진행될수록 진해져 편안해 보인다")]
    [SerializeField] private Graphic[] _cheeks;
    [SerializeField] private RectTransform _waveArm;
    [SerializeField] private RectTransform _mouth;
    [SerializeField] private MouthView _mouthView;

    [Header("환자별 파스텔 피부색 (순서대로 돌아감)")]
    [SerializeField] private Color[] _skinColors =
    {
      new Color(0.72f, 0.92f, 0.62f),
      new Color(0.66f, 0.84f, 1f),
      new Color(0.9f, 0.76f, 1f),
      new Color(1f, 0.82f, 0.62f),
    };

    [Header("볼 홍조")]
    [SerializeField, Range(0f, 1f)] private float _cheekAlphaMin = 0.75f;
    [SerializeField, Range(0f, 1f)] private float _cheekAlphaMax = 1f;

    [Header("움직임")]
    [SerializeField] private float _offscreenX = 1400f;
    [SerializeField] private float _enterSec = 1.1f;
    [SerializeField] private float _leaveSec = 1.3f;
    [SerializeField] private float _hopHeight = 50f;
    [SerializeField] private int _hopCount = 4;
    [SerializeField] private float _mouthOpenSec = 0.4f;
    [SerializeField] private float _mouthClosedScale = 0.08f;
    [SerializeField] private float _painShake = 8f;
    [SerializeField] private float _happyBounceSec = 0.6f;
    [SerializeField] private float _waveAngle = 25f;
    [SerializeField] private float _waveSpeed = 12f;

    private Vector2 _basePos;
    private float _armBaseAngle;
    private PatientExpression _expression;
    private bool _expressionApplied;
    private bool _moving;
    private bool _waving;
    private float _happyT = -1f;
    private float _comfort;

    public MouthView Mouth => _mouthView;
    public Color SkinColor { get; private set; }

    private void Awake()
    {
      _basePos = _root.anchoredPosition;
      _armBaseAngle = _waveArm.localEulerAngles.z;
      SetExpression(PatientExpression.Normal);
    }

    /// <summary>index번째 환자 모습으로 준비하고 화면 밖 오른쪽에 둔다</summary>
    public void Prepare(int index)
    {
      SkinColor = _skinColors[index % _skinColors.Length];
      foreach (var g in _skin)
      {
        g.color = SkinColor;
      }
      _mouthView.ResetDirty();
      _mouth.localScale = new Vector3(1f, _mouthClosedScale, 1f);
      _root.anchoredPosition = _basePos + new Vector2(_offscreenX, 0f);
      _happyT = -1f;
      _waving = false;
      SetComfort(0f);
      SetExpression(PatientExpression.Normal);
    }

    /// <summary>통통 튀며 들어와 앉고 "아~" 입을 벌린다</summary>
    public IEnumerator Enter()
    {
      yield return Move(_basePos + new Vector2(_offscreenX, 0f), _basePos, _enterSec, easeOut: true);
      yield return ScaleMouth(_mouthClosedScale, 1f, _mouthOpenSec);
    }

    /// <summary>입을 닫고 손을 흔들며 왼쪽으로 나간다</summary>
    public IEnumerator Leave()
    {
      _waving = true;
      yield return ScaleMouth(1f, _mouthClosedScale, _mouthOpenSec * 0.7f);
      yield return Move(_basePos, _basePos - new Vector2(_offscreenX, 0f), _leaveSec, easeOut: false);
      _waving = false;
    }

    public void SetExpression(PatientExpression expression)
    {
      if (_expressionApplied && expression == _expression)
      {
        return;
      }
      _expressionApplied = true;
      _expression = expression;
      _normalFace.SetActive(expression == PatientExpression.Normal);
      _painFace.SetActive(expression == PatientExpression.Pain);
      _happyFace.SetActive(expression == PatientExpression.Happy);
      ApplyCheeks();
    }

    /// <summary>0~1. 치료가 진행될수록 표정이 편안해진다</summary>
    public void SetComfort(float comfort)
    {
      if (Mathf.Approximately(comfort, _comfort))
      {
        return;
      }
      _comfort = comfort;
      ApplyCheeks();
    }

    public void PlayHappy()
    {
      SetExpression(PatientExpression.Happy);
      _happyT = 0f;
      _waving = true;
    }

    private void ApplyCheeks()
    {
      var a = _expression == PatientExpression.Happy ? _cheekAlphaMax : Mathf.Lerp(_cheekAlphaMin, _cheekAlphaMax, _comfort);
      foreach (var cheek in _cheeks)
      {
        var c = cheek.color;
        c.a = a;
        cheek.color = c;
      }
    }

    private void Update()
    {
      var armAngle = _waving ? Mathf.Sin(Time.time * _waveSpeed) * _waveAngle : 0f;
      _waveArm.localRotation = Quaternion.Euler(0f, 0f, _armBaseAngle + armAngle);

      if (_moving)
      {
        return;
      }

      var offset = Vector2.zero;
      var scale = 1f;
      if (_expression == PatientExpression.Pain)
      {
        // 아파서 움찔움찔
        offset.x = Mathf.Sin(Time.time * 55f) * _painShake;
      }
      if (_happyT >= 0f)
      {
        _happyT += Time.deltaTime;
        var k = Mathf.Clamp01(_happyT / _happyBounceSec);
        offset.y = Mathf.Sin(k * Mathf.PI * 2f) * 30f * (1f - k);
        scale = 1f + Mathf.Sin(k * Mathf.PI) * 0.05f;
        if (k >= 1f)
        {
          _happyT = -1f;
        }
      }
      _root.anchoredPosition = _basePos + offset;
      _root.localScale = new Vector3(scale, scale, 1f);
    }

    private IEnumerator Move(Vector2 from, Vector2 to, float duration, bool easeOut)
    {
      _moving = true;
      _root.localScale = Vector3.one;
      for (var t = 0f; t < duration; t += Time.deltaTime)
      {
        var k = t / duration;
        var e = easeOut ? 1f - (1f - k) * (1f - k) : k * k;
        // 통통 튀며 걷기
        var hop = Mathf.Abs(Mathf.Sin(k * Mathf.PI * _hopCount)) * _hopHeight;
        _root.anchoredPosition = Vector2.LerpUnclamped(from, to, e) + new Vector2(0f, hop);
        yield return null;
      }
      _root.anchoredPosition = to;
      _moving = false;
    }

    private IEnumerator ScaleMouth(float from, float to, float duration)
    {
      for (var t = 0f; t < duration; t += Time.deltaTime)
      {
        var k = t / duration;
        _mouth.localScale = new Vector3(1f, Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k)), 1f);
        yield return null;
      }
      _mouth.localScale = new Vector3(1f, to, 1f);
    }
  }
}
