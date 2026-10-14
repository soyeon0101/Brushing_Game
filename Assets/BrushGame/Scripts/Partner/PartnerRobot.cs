using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   파트너 방의 로봇. 그림은 기본 자세 한 장이고, 움직임은 전부 코드로 만든다.
  ///   평소: 둥실둥실 떠 있다가 무작위로 돌아다니기 / 두리번 / 노래(음표) / 화면 속 공놀이.
  ///   한동안 가만히 있으면 꾸벅꾸벅 졸다 잠든다. 누르면 깨거나(자는 중) 기뻐하며 폭죽.
  ///   눈은 PartnerEyes, 소품(음표·공·폭죽)은 그림 한 장씩을 코드로 움직인다.
  /// </summary>
  public class PartnerRobot : MonoBehaviour
  {
    [Header("연결")]
    [Tooltip("바닥을 좌우로 움직이는 자리 (아래 가운데 기준)")]
    [SerializeField] private RectTransform _mover;
    [Tooltip("떠오름·기울기·눌림을 주는 자리 (_mover 안, 아래 가운데 기준)")]
    [SerializeField] private RectTransform _visual;
    [SerializeField] private Image _shadow;
    [SerializeField] private PartnerEyes _eyes;
    [Tooltip("화면 안 소품 (공). 화면 밖으로 안 나가게 RectMask2D 안")]
    [SerializeField] private RectTransform _screenProps;
    [Tooltip("머리 위 소품 (음표, 폭죽, z)")]
    [SerializeField] private RectTransform _headProps;
    [SerializeField] private Button _touch;

    [Header("소품 그림")]
    [SerializeField] private Sprite _note;
    [SerializeField] private Sprite _ball;
    [SerializeField] private Sprite[] _fireworks;
    [SerializeField] private Font _font;
    [Tooltip("밥그릇: 가득, 반, 빈 순서")]
    [SerializeField] private Sprite[] _bowls;
    [Tooltip("튀는 밥알 (작은 흰 동그라미)")]
    [SerializeField] private Sprite _grain;

    [Header("움직임")]
    [Tooltip("돌아다니는 범위 (맨 앞에서 가운데로부터 좌우 거리)")]
    [SerializeField] private float _roamX = 300f;
    [Header("앞뒤 (원근)")]
    [Tooltip("맨 뒤로 갔을 때 바닥에서 올라가는 높이")]
    [SerializeField] private float _backRise = 360f;
    [Tooltip("맨 뒤로 갔을 때 크기")]
    [SerializeField, Range(0.2f, 1f)] private float _backScale = 0.6f;
    [Tooltip("맨 뒤에서는 바닥이 좁아져 좌우 범위도 이만큼 줄어든다")]
    [SerializeField, Range(0.2f, 1f)] private float _backRoam = 0.55f;
    [SerializeField, Min(10f)] private float _moveSpeed = 220f;
    [Tooltip("이동할 때 진행 방향으로 기우는 각도")]
    [SerializeField] private float _moveTilt = 8f;
    [SerializeField] private float _hoverHeight = 14f;
    [SerializeField] private float _bobAmount = 9f;
    [Tooltip("둥실둥실 속도 (초당 왕복)")]
    [SerializeField] private float _bobSpeed = 0.7f;

    [Header("행동 간격")]
    [Tooltip("행동 사이에 가만히 있는 시간 (초, 최소~최대)")]
    [SerializeField] private Vector2 _idleSec = new Vector2(0.5f, 1.5f);
    [SerializeField] private Vector2 _blinkSec = new Vector2(2.5f, 6f);
    [Tooltip("누르지 않고 이만큼 지나면 졸기 시작한다 (초)")]
    [SerializeField, Min(5f)] private float _dozeAfterSec = 30f;

    private float _x;          // 좌우 (맨 앞 기준 거리)
    private float _depth;      // 0 맨 앞 ~ 1 맨 뒤
    private float _frontY;     // 맨 앞일 때 바닥 높이 (씬에 놓인 자리)
    private float _shadowY;
    private bool _placed;
    private float _lift;
    private float _tilt;
    private float _squash;
    private float _drowsy;     // 0 평소 ~ 1 잠듦 (떠 있는 높이가 낮아지고 둥실 속도가 느려진다)
    private bool _asleep;
    private bool _eyesBusy;    // 표정 연출 중에는 자동 깜빡임을 쉰다
    private float _awakeSec;
    private float _time;
    private Coroutine _brain;
    private Coroutine _blinker;
    private Coroutine _action;

    /// <summary>밥 먹는 중 (밥 주기 버튼을 잠시 막는다)</summary>
    public bool IsEating { get; private set; }

    private void Awake()
    {
      if (_touch != null)
      {
        _touch.onClick.AddListener(OnTapped);
      }
    }

    private void OnEnable()
    {
      ResetState();
      _brain = StartCoroutine(Brain());
      _blinker = StartCoroutine(Blinker());
    }

    private void OnDisable()
    {
      StopAllCoroutines();
      ClearProps();
    }

    public void ResetState()
    {
      IsEating = false;
      _asleep = false;
      _eyesBusy = false;
      _awakeSec = 0f;
      _drowsy = 0f;
      _lift = _tilt = _squash = 0f;
      _eyes.Open = 1f;
      _eyes.Lid = 0f;
      _eyes.Happy = 0f;
      _eyes.Look = Vector2.zero;
      if (!_placed)
      {
        _frontY = _mover.anchoredPosition.y;
        _shadowY = _shadow != null ? _shadow.rectTransform.anchoredPosition.y : _frontY;
        _placed = true;
      }
      _x = 0f;
      _depth = 0.35f;
    }

    private void Update()
    {
      _time += Time.deltaTime;
      if (!_asleep)
      {
        _awakeSec += Time.deltaTime;
      }
      // 둥실둥실: 졸릴수록 낮고 느리게
      var speed = Mathf.Lerp(_bobSpeed, _bobSpeed * 0.4f, _drowsy);
      var bob = Mathf.Sin(_time * Mathf.PI * 2f * speed) * Mathf.Lerp(_bobAmount, _bobAmount * 0.3f, _drowsy);
      var lift = Mathf.Lerp(_hoverHeight, _hoverHeight * 0.25f, _drowsy) + bob + _lift;
      // 원근: 뒤로 갈수록 바닥 위쪽으로, 작게, 좌우 폭도 좁게
      var persp = Mathf.Lerp(1f, _backScale, _depth);
      var x = _x * Mathf.Lerp(1f, _backRoam, _depth);
      var rise = _backRise * _depth;
      _mover.anchoredPosition = new Vector2(x, _frontY + rise);
      _mover.localScale = Vector3.one * persp;
      _visual.anchoredPosition = new Vector2(0f, lift);
      _visual.localEulerAngles = new Vector3(0f, 0f, _tilt);
      _visual.localScale = new Vector3(1f + _squash * 0.5f, 1f - _squash, 1f);
      if (_shadow != null)
      {
        var k = Mathf.Clamp01(lift / 60f);
        _shadow.rectTransform.anchoredPosition = new Vector2(x, _shadowY + rise);
        _shadow.rectTransform.localScale = Vector3.one * (persp * (1f - k * 0.35f));
        var c = _shadow.color;
        c.a = 0.28f * (1f - k * 0.6f);
        _shadow.color = c;
      }
    }

    // ---------- 행동 고르기 ----------

    private IEnumerator Brain()
    {
      yield return Wait(Random.Range(0.6f, 1.2f));
      while (true)
      {
        if (_asleep)
        {
          yield return null;
          continue;
        }
        if (_awakeSec >= _dozeAfterSec)
        {
          yield return Act(Doze());
          continue;
        }
        // 돌아다니기가 가장 자주 나온다
        var roll = Random.value;
        if (roll < 0.6f)
        {
          yield return Act(Wander());
        }
        else if (roll < 0.72f)
        {
          yield return Act(LookAround());
        }
        else if (roll < 0.84f)
        {
          yield return Act(Sing());
        }
        else if (roll < 0.94f)
        {
          yield return Act(PlayBall());
        }
        yield return Wait(Random.Range(_idleSec.x, _idleSec.y));
      }
    }

    /// <summary>행동 하나를 돌리고, 누르면 끊을 수 있게 핸들을 잡아 둔다</summary>
    private IEnumerator Act(IEnumerator routine)
    {
      _action = StartCoroutine(routine);
      yield return _action;
      _action = null;
    }

    private static IEnumerator Wait(float sec)
    {
      yield return new WaitForSeconds(sec);
    }

    private IEnumerator Blinker()
    {
      while (true)
      {
        yield return new WaitForSeconds(Random.Range(_blinkSec.x, _blinkSec.y));
        if (_asleep || _eyesBusy || _eyes.Lid > 0.2f || _eyes.Happy > 0.1f)
        {
          continue;
        }
        yield return Blink();
        if (Random.value < 0.2f)
        {
          yield return new WaitForSeconds(0.12f);
          yield return Blink();
        }
      }
    }

    private IEnumerator Blink()
    {
      yield return Tween(0.07f, k => _eyes.Open = 1f - k);
      yield return Tween(0.09f, k => _eyes.Open = k);
    }

    // ---------- 행동 ----------

    /// <summary>바닥 위 아무 곳으로 이동: 좌우뿐 아니라 앞뒤로도 (뒤로 가면 원근으로 작아진다)</summary>
    private IEnumerator Wander()
    {
      var from = new Vector2(_x, _depth);
      Vector2 to;
      var tries = 0;
      do
      {
        to = new Vector2(Random.Range(-_roamX, _roamX), Random.Range(0f, 1f));
        tries++;
      } while (tries < 8 && Mathf.Abs(to.x - from.x) / _roamX + Mathf.Abs(to.y - from.y) < 0.5f);
      // 걸리는 시간은 실제 화면에서 움직이는 거리로 (뒤쪽은 작게 보이니 같은 거리도 느리게 간다)
      var dx = (to.x - from.x) * Mathf.Lerp(1f, _backRoam, (from.y + to.y) / 2f);
      var dy = (to.y - from.y) * _backRise;
      var sec = Mathf.Max(0.6f, new Vector2(dx, dy).magnitude / _moveSpeed);
      var dir = Mathf.Abs(dx) > 1f ? Mathf.Sign(dx) : 0f;
      var up = Mathf.Clamp(dy / Mathf.Max(1f, _backRise), -1f, 1f);
      // 천천히 출발해서 천천히 멈추고, 가는 동안 그쪽으로 기울고 그쪽을 본다 (멀어질 땐 위를, 다가올 땐 아래를)
      yield return Tween(sec, k =>
      {
        var e = Smooth(k);
        _x = Mathf.Lerp(from.x, to.x, e);
        _depth = Mathf.Lerp(from.y, to.y, e);
        var lean = Mathf.Sin(k * Mathf.PI);
        _tilt = -dir * _moveTilt * lean * Mathf.Clamp01(Mathf.Abs(dx) / 150f);
        var look = Mathf.Min(1f, k * 3f) * (1f - Mathf.Max(0f, k - 0.85f) / 0.15f);
        _eyes.Look = new Vector2(dir * 0.8f * Mathf.Clamp01(Mathf.Abs(dx) / 150f), up * 0.6f) * look;
      });
      _tilt = 0f;
      // 멈추면서 살짝 눌림
      yield return Tween(0.3f, k => _squash = 0.08f * Mathf.Sin(k * Mathf.PI));
      _squash = 0f;
      yield return Tween(0.25f, k => _eyes.Look = Vector2.Lerp(_eyes.Look, Vector2.zero, k));
    }

    private IEnumerator LookAround()
    {
      var first = Random.value < 0.5f ? -1f : 1f;
      yield return Tween(0.35f, k => _eyes.Look = new Vector2(first * 0.9f * Smooth(k), 0.1f * Smooth(k)));
      yield return Wait(0.5f);
      yield return Tween(0.45f, k => _eyes.Look = new Vector2(first * 0.9f * (1f - 2f * Smooth(k)), 0.1f));
      yield return Wait(0.5f);
      yield return Tween(0.3f, k => _eyes.Look = Vector2.Lerp(new Vector2(-first * 0.9f, 0.1f), Vector2.zero, Smooth(k)));
    }

    /// <summary>노래: 눈이 ^^, 몸을 좌우로 흔들고 머리 위로 음표가 둥실 떠오른다</summary>
    private IEnumerator Sing()
    {
      _eyesBusy = true;
      yield return Tween(0.25f, k => _eyes.Happy = k);
      var sec = 3.2f;
      var next = 0f;
      for (var t = 0f; t < sec; t += Time.deltaTime)
      {
        _tilt = Mathf.Sin(t * Mathf.PI * 2f * 0.9f) * 5f;
        if (t >= next)
        {
          SpawnNote();
          next += 0.8f;
        }
        yield return null;
      }
      yield return Tween(0.3f, k =>
      {
        _tilt = Mathf.Lerp(_tilt, 0f, k);
        _eyes.Happy = 1f - k;
      });
      _tilt = 0f;
      _eyesBusy = false;
    }

    private void SpawnNote()
    {
      if (_note == null || _headProps == null)
      {
        return;
      }
      var img = NewProp("Note", _headProps, _note, 70f);
      StartCoroutine(FloatUp(img, Random.Range(-1f, 1f)));
    }

    private IEnumerator FloatUp(Image img, float side)
    {
      var rt = img.rectTransform;
      var start = new Vector2(side * 60f, -20f);
      var sec = 1.6f;
      for (var t = 0f; t < sec && img != null; t += Time.deltaTime)
      {
        var k = t / sec;
        rt.anchoredPosition = start + new Vector2(Mathf.Sin(k * Mathf.PI * 3f) * 18f + side * 40f * k, 170f * k);
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 12f);
        img.color = new Color(1f, 1f, 1f, k < 0.15f ? k / 0.15f : 1f - Mathf.Max(0f, k - 0.6f) / 0.4f);
        yield return null;
      }
      if (img != null)
      {
        Destroy(img.gameObject);
      }
    }

    /// <summary>공놀이: 화면 안에서 공이 통통 튀고, 눈이 공을 따라간다</summary>
    private IEnumerator PlayBall()
    {
      if (_ball == null || _screenProps == null)
      {
        yield break;
      }
      _eyesBusy = true;
      var area = _screenProps.rect;
      var size = area.height * 0.32f;
      var img = NewProp("Ball", _screenProps, _ball, size);
      var rt = img.rectTransform;
      var halfW = area.width / 2f - size / 2f;
      var halfH = area.height / 2f - size / 2f;
      var pos = new Vector2(Random.Range(-halfW, halfW), halfH);
      var vel = new Vector2(Random.value < 0.5f ? -1f : 1f, 0f) * area.width * 0.55f;
      var gravity = -area.height * 4f;
      var sec = 3.5f;
      for (var t = 0f; t < sec; t += Time.deltaTime)
      {
        var dt = Time.deltaTime;
        vel.y += gravity * dt;
        pos += vel * dt;
        if (pos.y < -halfH)
        {
          pos.y = -halfH;
          vel.y = Mathf.Abs(vel.y) * 0.85f;
          if (vel.y < area.height * 1.2f)
          {
            vel.y = area.height * 2.2f;   // 계속 통통 튀게
          }
          _squash = 0.05f;
        }
        if (Mathf.Abs(pos.x) > halfW)
        {
          pos.x = Mathf.Sign(pos.x) * halfW;
          vel.x = -vel.x;
        }
        _squash = Mathf.MoveTowards(_squash, 0f, dt * 0.5f);
        rt.anchoredPosition = pos;
        rt.localEulerAngles += new Vector3(0f, 0f, -vel.x * dt * 2f);
        img.color = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f, (sec - t) * 4f));
        // 눈은 공 쪽을 본다
        _eyes.Look = Vector2.Lerp(_eyes.Look, new Vector2(pos.x / Mathf.Max(1f, halfW), pos.y / Mathf.Max(1f, halfH) * 0.8f), dt * 8f);
        yield return null;
      }
      Destroy(img.gameObject);
      _squash = 0f;
      yield return Tween(0.3f, k => _eyes.Look = Vector2.Lerp(_eyes.Look, Vector2.zero, k));
      _eyesBusy = false;
    }

    /// <summary>졸기: 꾸벅(감겼다 퍼뜩) 두 번 → 천천히 잠듦</summary>
    private IEnumerator Doze()
    {
      _eyesBusy = true;
      yield return Tween(1.0f, k => SetDrowsy(0.55f * Smooth(k), 0.4f * k));
      yield return Tween(0.18f, k => SetDrowsy(0.55f * (1f - k), 0.4f));
      yield return Wait(0.7f);
      yield return Tween(1.3f, k => SetDrowsy(0.75f * Smooth(k), 0.4f + 0.3f * k));
      yield return Tween(0.35f, k => SetDrowsy(0.75f - 0.5f * k, 0.7f));
      yield return Wait(0.4f);
      yield return Tween(2.2f, k => SetDrowsy(0.25f + 0.75f * Smooth(k), 0.7f + 0.3f * k));
      _asleep = true;
      _eyesBusy = false;
      StartCoroutine(Snore());
    }

    /// <summary>lid: 윗눈꺼풀, drowsy: 몸이 내려앉고 고개가 숙여지는 정도</summary>
    private void SetDrowsy(float lid, float drowsy)
    {
      _eyes.Lid = lid;
      _eyes.Look = new Vector2(0f, -0.3f * lid);
      _drowsy = drowsy;
      _tilt = 6f * lid;
    }

    private IEnumerator Snore()
    {
      while (_asleep)
      {
        if (_headProps != null && _font != null)
        {
          var z = new GameObject("Z", typeof(RectTransform)).AddComponent<Text>();
          z.rectTransform.SetParent(_headProps, false);
          z.font = _font;
          z.text = "z";
          z.fontSize = 56;
          z.fontStyle = FontStyle.Bold;
          z.alignment = TextAnchor.MiddleCenter;
          z.raycastTarget = false;
          z.rectTransform.sizeDelta = new Vector2(80f, 80f);
          StartCoroutine(Drift(z));
        }
        yield return new WaitForSeconds(1.1f);
      }
    }

    private IEnumerator Drift(Text z)
    {
      var sec = 2.2f;
      for (var t = 0f; t < sec && z != null; t += Time.deltaTime)
      {
        var k = t / sec;
        z.rectTransform.anchoredPosition = new Vector2(60f + 50f * k, -10f + 120f * k);
        z.rectTransform.localScale = Vector3.one * (0.6f + 0.6f * k);
        z.color = new Color(0.6f, 0.47f, 0.9f, Mathf.Sin(k * Mathf.PI));
        yield return null;
      }
      if (z != null)
      {
        Destroy(z.gameObject);
      }
    }

    // ---------- 누르기 ----------

    private void OnTapped()
    {
      if (IsEating)
      {
        return;
      }
      var wasAsleep = _asleep;
      Interrupt();
      _brain = StartCoroutine(TapThenResume(wasAsleep ? WakeUp() : Cheer()));
    }

    /// <summary>밥 주기: 하던 걸 멈추고 (자고 있으면 깨서) 밥을 먹는다</summary>
    public void Feed()
    {
      if (IsEating)
      {
        return;
      }
      var wasAsleep = _asleep;
      Interrupt();
      IsEating = true;
      _brain = StartCoroutine(TapThenResume(EatAfter(wasAsleep)));
    }

    private IEnumerator EatAfter(bool wake)
    {
      if (wake)
      {
        yield return WakeUp();
      }
      yield return Eat();
    }

    /// <summary>하던 행동을 멈추고 자세를 편다 (자고 있으면 자는 자세는 그대로, 깨우는 쪽에서 편다)</summary>
    private void Interrupt()
    {
      _awakeSec = 0f;
      if (_action != null)
      {
        StopCoroutine(_action);
        _action = null;
      }
      if (_brain != null)
      {
        StopCoroutine(_brain);
      }
      ClearScreenProps();
      if (!_asleep)
      {
        _drowsy = 0f;
        _tilt = 0f;
        _eyes.Lid = 0f;
        _eyes.Look = Vector2.zero;
      }
    }

    /// <summary>
    ///   밥 먹기: 화면 아래에 밥그릇 → 눈은 위로 올라가 ^^ → 한 입(숙였다 올라옴, 밥알 퐁) + 오물오물 x4
    ///   → 그릇이 가득 → 반 → 빈 그릇 → 그릇 사라지고 기뻐하며 폭죽
    /// </summary>
    private IEnumerator Eat()
    {
      IsEating = true;
      _eyesBusy = true;
      Image bowl = null;
      if (_bowls != null && _bowls.Length > 0 && _screenProps != null)
      {
        var area = _screenProps.rect;
        bowl = NewProp("Bowl", _screenProps, _bowls[0], area.height * 0.55f);
        bowl.rectTransform.anchoredPosition = new Vector2(0f, -area.height * 0.24f);
      }
      var lookUp = new Vector2(0f, 0.9f);
      yield return Tween(0.35f, k =>
      {
        if (bowl != null)
        {
          bowl.rectTransform.localScale = Vector3.one * EaseOutBack(k);
        }
        _eyes.Look = lookUp * Smooth(k);
        _eyes.Happy = k;
      });
      for (var bite = 0; bite < 4; bite++)
      {
        // 한 입: 살짝 숙였다 올라오며 밥알이 퐁
        yield return Tween(0.2f, k => _squash = 0.08f * Mathf.Sin(k * Mathf.PI));
        if (bowl != null)
        {
          SpawnGrains(bowl.rectTransform.anchoredPosition + new Vector2(0f, bowl.rectTransform.rect.height * 0.25f));
        }
        // 오물오물
        for (var chew = 0; chew < 2; chew++)
        {
          yield return Tween(0.14f, k => _squash = 0.035f * Mathf.Sin(k * Mathf.PI));
        }
        if (bowl != null && _bowls.Length >= 3)
        {
          bowl.sprite = bite >= 3 ? _bowls[2] : bite >= 1 ? _bowls[1] : _bowls[0];
        }
        yield return Wait(0.12f);
      }
      _squash = 0f;
      yield return Wait(0.25f);
      if (bowl != null)
      {
        var b = bowl;
        yield return Tween(0.25f, k =>
        {
          b.color = new Color(1f, 1f, 1f, 1f - k);
          b.rectTransform.localScale = Vector3.one * (1f - 0.3f * k);
        });
        Destroy(b.gameObject);
      }
      yield return Tween(0.2f, k => _eyes.Look = Vector2.Lerp(lookUp, Vector2.zero, k));
      yield return Cheer();
      IsEating = false;
    }

    private void SpawnGrains(Vector2 from)
    {
      if (_grain == null)
      {
        return;
      }
      for (var i = 0; i < 3; i++)
      {
        var g = NewProp("Grain", _screenProps, _grain, 9f);
        StartCoroutine(Pop(g, from, new Vector2(Random.Range(-60f, 60f), Random.Range(70f, 110f))));
      }
    }

    private IEnumerator Pop(Image g, Vector2 from, Vector2 vel)
    {
      var gravity = -420f;
      var pos = from;
      for (var t = 0f; t < 0.5f && g != null; t += Time.deltaTime)
      {
        vel.y += gravity * Time.deltaTime;
        pos += vel * Time.deltaTime;
        g.rectTransform.anchoredPosition = pos;
        g.color = new Color(1f, 1f, 1f, 1f - t / 0.5f);
        yield return null;
      }
      if (g != null)
      {
        Destroy(g.gameObject);
      }
    }

    private static float EaseOutBack(float t)
    {
      const float c1 = 1.70158f;
      const float c3 = c1 + 1f;
      t = Mathf.Clamp01(t) - 1f;
      return 1f + c3 * t * t * t + c1 * t * t;
    }

    private IEnumerator TapThenResume(IEnumerator reaction)
    {
      yield return Act(reaction);
      _brain = StartCoroutine(Brain());
    }

    /// <summary>자다가 깸: 퍼뜩 눈을 뜨고 통 튀어 오른 뒤 반가워한다</summary>
    private IEnumerator WakeUp()
    {
      _asleep = false;
      _eyesBusy = true;
      var lid0 = _eyes.Lid;
      var drowsy0 = _drowsy;
      yield return Tween(0.25f, k =>
      {
        _eyes.Lid = Mathf.Lerp(lid0, 0f, Smooth(k));
        _eyes.Look = Vector2.Lerp(_eyes.Look, Vector2.zero, k);
        _drowsy = Mathf.Lerp(drowsy0, 0f, k);
        _tilt = Mathf.Lerp(_tilt, 0f, k);
      });
      yield return Hop(1, 26f);
      yield return Cheer();
    }

    /// <summary>기쁨: 눈 ^^ + 통통 두 번 + 머리 위 폭죽</summary>
    private IEnumerator Cheer()
    {
      _eyesBusy = true;
      _eyes.Lid = 0f;
      yield return Tween(0.15f, k => _eyes.Happy = k);
      StartCoroutine(Firework());
      yield return Hop(2, 30f);
      yield return Wait(0.3f);
      yield return Tween(0.25f, k => _eyes.Happy = 1f - k);
      _eyesBusy = false;
    }

    private IEnumerator Hop(int times, float height)
    {
      for (var i = 0; i < times; i++)
      {
        yield return Tween(0.12f, k => _squash = 0.1f * Mathf.Sin(k * Mathf.PI));
        yield return Tween(0.42f, k =>
        {
          _lift = height * Mathf.Sin(k * Mathf.PI);
          _squash = -0.06f * Mathf.Sin(k * Mathf.PI);
        });
      }
      _lift = 0f;
      _squash = 0f;
    }

    private IEnumerator Firework()
    {
      if (_fireworks == null || _fireworks.Length == 0 || _headProps == null)
      {
        yield break;
      }
      var img = NewProp("Firework", _headProps, _fireworks[0], 260f);
      img.rectTransform.anchoredPosition = new Vector2(0f, 40f);
      foreach (var f in _fireworks)
      {
        if (img == null)
        {
          yield break;
        }
        img.sprite = f;
        yield return new WaitForSeconds(1f / 14f);
      }
      if (img != null)
      {
        Destroy(img.gameObject);
      }
    }

    // ---------- 공용 ----------

    private static Image NewProp(string name, RectTransform parent, Sprite sprite, float size)
    {
      var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
      img.rectTransform.SetParent(parent, false);
      img.sprite = sprite;
      img.preserveAspect = true;
      img.raycastTarget = false;
      img.rectTransform.sizeDelta = new Vector2(size, size);
      return img;
    }

    private void ClearScreenProps()
    {
      if (_screenProps == null)
      {
        return;
      }
      for (var i = _screenProps.childCount - 1; i >= 0; i--)
      {
        Destroy(_screenProps.GetChild(i).gameObject);
      }
      _eyesBusy = false;
      _eyes.Happy = 0f;
      _squash = 0f;
      _lift = 0f;
    }

    private void ClearProps()
    {
      ClearScreenProps();
      if (_headProps == null)
      {
        return;
      }
      for (var i = _headProps.childCount - 1; i >= 0; i--)
      {
        Destroy(_headProps.GetChild(i).gameObject);
      }
    }

    private static IEnumerator Tween(float sec, System.Action<float> step)
    {
      for (var t = 0f; t < sec; t += Time.deltaTime)
      {
        step(t / sec);
        yield return null;
      }
      step(1f);
    }

    private static float Smooth(float k)
    {
      k = Mathf.Clamp01(k);
      return k * k * (3f - 2f * k);
    }
  }
}
