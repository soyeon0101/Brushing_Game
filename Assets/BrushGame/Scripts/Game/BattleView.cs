using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   확대된 구역 앞에서 친구(이빨 요정)와 충치균이 겨루는 연출.
  ///   요정의 칫솔 마법 동작은 시간이 아니라 아이의 칫솔질 진행에 맞춰 넘어간다 (SetSwingProgress).
  ///   동작 한 바퀴의 burstAt 지점에서 반짝이가 퐁 터지고 충치균이 움찔한다. 충치균 체력은 구역 진행도를 그대로 따른다.
  ///   위치/크기/투명도 움직임 위에, 동작별 프레임 그림이 있으면 넘겨 가며 보여준다 (없는 동작은 그림을 바꾸지 않는다).
  /// </summary>
  public class BattleView : MonoBehaviour
  {
    [System.Serializable]
    public class Frames
    {
      public Sprite[] idle;     // 대기 (반복)
      public Sprite[] action;   // 친구: 칫솔 마법 한 바퀴 (칫솔질 진행에 맞춰) / 충치균: 움찔 (반짝이마다 한 번)
      public Sprite[] stopped;  // 양치를 멈췄을 때 친구: 아파함 / 충치균: 깔깔 (반복)
      public Sprite[] finish;   // 친구: 승리 / 충치균: 쓰러짐 (한 번)
      [Tooltip("반복 동작(대기, 멈춤)의 초당 프레임 수. 영상에서 뽑은 프레임은 8 정도")]
      [Min(0.5f)] public float loopFps = 4f;
    }

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _hero;
    [SerializeField] private Image _heroImage;
    [SerializeField] private Frames _heroFrames = new Frames();
    [SerializeField] private RectTransform _villain;
    [SerializeField] private Image _villainImage;
    [SerializeField] private Frames _villainFrames = new Frames();
    [SerializeField] private CanvasGroup _villainGroup;
    [SerializeField] private RectTransform _hitEffect;
    [SerializeField] private CanvasGroup _hitGroup;
    [SerializeField] private RectTransform _hpBar;
    [Tooltip("체력만큼 가로로 줄어드는 채움 (anchorMax.x)")]
    [SerializeField] private RectTransform _hpFill;

    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _enterSec = 0.45f;
    [SerializeField, Min(0.01f)] private float _hitSec = 1f;
    [SerializeField, Min(0.01f)] private float _defeatSec = 1f;
    [SerializeField] private float _lungeDistance = 20f;
    [SerializeField] private float _shakeDistance = 24f;
    [SerializeField] private float _hpBarGap = 220f;

    [Header("칫솔 마법 (칫솔질 진행에 따라)")]
    [Tooltip("동작 한 바퀴 중 반짝이가 터지는 지점 (0~1). 칫솔을 앞으로 뻗은 프레임에 맞춘다")]
    [SerializeField, Range(0f, 1f)] private float _burstAt = 0.55f;
    [Tooltip("칫솔을 들 때 뒤로 젖히고, 뻗을 때 앞으로 숙이는 각도 (도). 프레임 사이를 부드럽게 잇는다")]
    [SerializeField] private float _swingTilt = 0f;
    [Tooltip("동작이 넘어가는 최대 속도 (바퀴/초). 아주 빨리 닦아도 동작이 휙휙 넘어가지 않게")]
    [SerializeField, Min(0.1f)] private float _swingSpeed = 1.2f;
    [Tooltip("동작이 이만큼 멈춰 있으면 대기 모습으로 돌아간다 (초)")]
    [SerializeField, Min(0f)] private float _swingRestSec = 0.8f;

    private Vector2 _heroHome, _villainHome, _hitHome;
    private float _time;
    private float _enterT = -1f, _hitT = -1f, _defeatT = -1f;
    private float _phase, _phaseTarget;
    private float _lastSwingTime = float.NegativeInfinity;
    private bool _idle;
    private float _hp = 1f, _shownHp = 1f;

    private void Awake()
    {
      _heroHome = _hero.anchoredPosition;
      _villainHome = _villain.anchoredPosition;
      _hitHome = _hitEffect.anchoredPosition;
    }

    /// <summary>
    ///   center: 부모 좌표에서 무대 가운데. hpBelow: 체력바를 캐릭터 아래(true) 또는 위에 둔다 (잇몸을 피해서)
    /// </summary>
    public void Enter(Vector2 center, bool hpBelow)
    {
      // 처음 켜질 때 Awake에서 제자리를 기억하므로 먼저 켠다
      gameObject.SetActive(true);
      ((RectTransform)transform).anchoredPosition = center;
      _hpBar.anchoredPosition = new Vector2(_villainHome.x, (hpBelow ? -1f : 1f) * _hpBarGap);
      _group.alpha = 1f;
      _villainGroup.alpha = 1f;
      _hitGroup.alpha = 0f;
      _hp = _shownHp = 1f;
      _idle = false;
      _enterT = 0f;
      _hitT = _defeatT = -1f;
      _phase = _phaseTarget = 0f;
      _lastSwingTime = float.NegativeInfinity;
      Apply();
    }

    public void Hide()
    {
      gameObject.SetActive(false);
    }

    /// <summary>
    ///   cycles: 이 구역에서 마법 동작을 몇 바퀴 할 만큼 닦았는지 (칫솔질 횟수 / 한 바퀴에 필요한 칫솔질).
    ///   cycles가 1, 2, 3...이 되는 순간이 반짝이가 터지는 지점이 되도록 맞춘다.
    /// </summary>
    public void SetSwingProgress(float cycles)
    {
      var target = Mathf.Max(0f, cycles - (1f - _burstAt));
      if (target <= _phaseTarget)
      {
        return;
      }
      _phaseTarget = target;
      // 한참 밀리면 따라잡는다 (한 바퀴 이상 늦지 않게)
      _phase = Mathf.Max(_phase, _phaseTarget - 1f);
    }

    private void Burst()
    {
      if (_defeatT >= 0f)
      {
        return;
      }
      _hitT = 0f;
      // 반짝이 자리가 조금씩 달라야 여러 번 닦는 느낌이 난다
      _hitEffect.anchoredPosition = _hitHome + Random.insideUnitCircle * 40f;
      _hitEffect.localEulerAngles = new Vector3(0f, 0f, Random.Range(-25f, 25f));
    }

    public void SetHp(float hp)
    {
      _hp = Mathf.Clamp01(hp);
    }

    /// <summary>양치를 멈춘 동안: 충치균은 신나고 친구는 아파한다</summary>
    public void SetIdle(bool idle)
    {
      _idle = idle;
    }

    public void PlayDefeat()
    {
      _hp = 0f;
      _idle = false;
      _hitT = -1f;
      _defeatT = 0f;
    }

    private void Update()
    {
      var dt = Time.deltaTime;
      _time += dt;
      if (_enterT >= 0f)
      {
        _enterT += dt;
        if (_enterT >= _enterSec)
        {
          _enterT = -1f;
        }
      }
      if (_hitT >= 0f)
      {
        _hitT += dt;
        if (_hitT >= _hitSec)
        {
          _hitT = -1f;
        }
      }
      if (_defeatT >= 0f)
      {
        _defeatT = Mathf.Min(_defeatT + dt, _defeatSec);
      }

      var prev = _phase;
      _phase = Mathf.MoveTowards(_phase, _phaseTarget, _swingSpeed * dt);
      if (_phase > prev)
      {
        _lastSwingTime = _time;
        if (Mathf.Floor(prev - _burstAt) < Mathf.Floor(_phase - _burstAt))
        {
          Burst();
        }
      }
      _shownHp = Mathf.MoveTowards(_shownHp, _hp, dt * 2f);
      Apply();
    }

    private void Apply()
    {
      var enter = _enterT >= 0f ? EaseOutBack(_enterT / _enterSec) : 1f;
      var hit = _hitT >= 0f ? _hitT / _hitSec : 1f;
      var hitting = _hitT >= 0f;

      // 친구: 등장 시 옆에서 들어오고, 칫솔을 뻗는 순간 살짝 앞으로, 멈추면 아파서 떤다
      var swinging = _time - _lastSwingTime <= _swingRestSec;
      var swing = _phase - Mathf.Floor(_phase);
      var heroPos = _heroHome + new Vector2(-300f * (1f - enter), 0f);
      if (swinging)
      {
        heroPos.x += _lungeDistance * Mathf.Max(0f, 1f - Mathf.Abs(swing - _burstAt) / 0.2f);
      }
      if (_idle)
      {
        heroPos += new Vector2(Mathf.Sin(_time * 40f) * 5f, -12f);
      }
      if (_defeatT >= 0f)
      {
        heroPos.y += 70f * Mathf.Abs(Mathf.Sin(_defeatT / _defeatSec * Mathf.PI * 2f));
      }
      _hero.anchoredPosition = heroPos;
      // 반짝이 지점(burstAt)에서 가장 앞으로, 그 반대편에서 가장 뒤로 기운다
      var tilt = _idle ? 8f : swinging ? _swingTilt * Mathf.Cos((swing - _burstAt) * Mathf.PI * 2f + Mathf.PI) : 0f;
      _hero.localEulerAngles = new Vector3(0f, 0f, tilt);

      // 충치균: 등장 시 튀어나오고, 맞으면 찌그러지며 흔들리고, 멈추면 신나서 들썩인다
      var villainPos = _villainHome;
      var villainScale = new Vector3(enter, enter, 1f);
      if (hitting)
      {
        var k = 1f - hit;
        villainPos.x += Mathf.Sin(hit * Mathf.PI * 6f) * _shakeDistance * k;
        villainScale = new Vector3(1f + 0.18f * k, 1f - 0.18f * k, 1f);
      }
      else if (_idle)
      {
        var bob = Mathf.Sin(_time * 7f);
        villainPos.y += 14f * bob;
        villainScale = Vector3.one * (1.06f + 0.06f * bob);
      }
      if (_defeatT >= 0f)
      {
        var k = _defeatT / _defeatSec;
        villainScale = Vector3.one * (1f - k);
        _villain.localEulerAngles = new Vector3(0f, 0f, 540f * k);
        _villainGroup.alpha = 1f - k;
      }
      else
      {
        _villain.localEulerAngles = Vector3.zero;
      }
      _villain.anchoredPosition = villainPos;
      _villain.localScale = villainScale;

      _hitGroup.alpha = hitting ? 1f - hit : 0f;
      _hitEffect.localScale = Vector3.one * (0.6f + 0.7f * hit);

      _hpBar.gameObject.SetActive(_defeatT < 0f);
      var max = _hpFill.anchorMax;
      max.x = _shownHp;
      _hpFill.anchorMax = max;

      // 친구의 마법 동작은 칫솔질 진행(swing)으로, 충치균의 움찔은 반짝이 이후 시간(hit)으로
      ApplyFrames(_heroImage, _heroFrames, swinging && !_idle, swing, loopAction: true);
      ApplyFrames(_villainImage, _villainFrames, hitting, hit, loopAction: false);
    }

    private void ApplyFrames(Image image, Frames frames, bool acting, float k, bool loopAction)
    {
      if (image == null || frames == null)
      {
        return;
      }
      Sprite sprite;
      if (_defeatT >= 0f)
      {
        sprite = PlayOnce(frames.finish, _defeatT / _defeatSec) ?? Loop(frames.idle, frames.loopFps);
      }
      else if (acting)
      {
        // 바퀴 진행도 k(0~1)에 해당하는 프레임. 한 바퀴 동작은 끝 프레임에서 멈추지 않고 다음 바퀴로 이어진다
        sprite = (loopAction ? AtProgress(frames.action, k) : PlayOnce(frames.action, k)) ?? Loop(frames.idle, frames.loopFps);
      }
      else if (_idle)
      {
        sprite = Loop(frames.stopped, frames.loopFps) ?? Loop(frames.idle, frames.loopFps);
      }
      else
      {
        sprite = Loop(frames.idle, frames.loopFps);
      }
      if (sprite != null && image.sprite != sprite)
      {
        image.sprite = sprite;
      }
    }

    private Sprite Loop(Sprite[] frames, float fps)
    {
      if (frames == null || frames.Length == 0)
      {
        return null;
      }
      return frames[(int)(_time * fps) % frames.Length];
    }

    private static Sprite AtProgress(Sprite[] frames, float k)
    {
      if (frames == null || frames.Length == 0)
      {
        return null;
      }
      return frames[Mathf.Clamp((int)(k * frames.Length), 0, frames.Length - 1)];
    }

    /// <summary>k: 0~1 진행도. 마지막 프레임에서 멈춘다</summary>
    private static Sprite PlayOnce(Sprite[] frames, float k)
    {
      if (frames == null || frames.Length == 0)
      {
        return null;
      }
      return frames[Mathf.Clamp((int)(k * frames.Length), 0, frames.Length - 1)];
    }

    private static float EaseOutBack(float t)
    {
      const float c1 = 1.70158f;
      const float c3 = c1 + 1f;
      t = Mathf.Clamp01(t) - 1f;
      return 1f + c3 * t * t * t + c1 * t * t;
    }
  }
}
