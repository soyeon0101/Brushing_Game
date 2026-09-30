using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   이빨 요정 3D 동작. 칫솔 든 팔로 칫솔질 진행에 맞춰 휘두르고, 귀와 날개가 따라 흔들린다.
  ///   각도는 전부 "처음 자세에서 얼마나 더 돌리는지"이다. 팔은 어깨, 머리는 목, 귀는 귀뿌리, 날개는 등이 중심이다.
  /// </summary>
  public class HeroPuppet : PuppetBase
  {
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _earL;
    [SerializeField] private Transform _earR;
    [SerializeField] private Transform _wingL;
    [SerializeField] private Transform _wingR;
    [Tooltip("칫솔을 든 팔 (충치균 쪽)")]
    [SerializeField] private Transform _brushArm;
    [SerializeField] private Transform _otherArm;

    [Header("휘두르기 (팔 각도, 도)")]
    [Tooltip("칫솔을 치켜든 각도 (앞뒤축)")]
    [SerializeField] private float _raise = -150f;
    [Tooltip("칫솔을 내리친 각도 (앞뒤축)")]
    [SerializeField] private float _strike = -30f;
    [Tooltip("휘두를 때 팔을 옆으로 벌리는 각도")]
    [SerializeField] private float _swingOut = 25f;
    [Tooltip("내리칠 때 몸이 상대 쪽으로 기우는 각도")]
    [SerializeField] private float _lean = 10f;

    private const int Head = 0, EarL = 1, EarR = 2, WingL = 3, WingR = 4, BrushArm = 5, OtherArm = 6;

    protected override Transform[] Parts => new[] { _head, _earL, _earR, _wingL, _wingR, _brushArm, _otherArm };

    /// <param name="swing">휘두르기 한 바퀴 진행도 (0~1). swinging일 때만 쓴다</param>
    /// <param name="burstAt">한 바퀴 중 칫솔이 닿는 지점</param>
    /// <param name="hurt">양치를 멈춰서 아파하는 중</param>
    /// <param name="win">충치균을 물리친 뒤 기뻐하는 진행도 (0~1), 아니면 음수</param>
    public void Pose(float time, bool swinging, float swing, float burstAt, bool hurt, float win)
    {
      var breathe = Mathf.Sin(time * 2.4f);
      var flapSpeed = swinging ? 16f : hurt ? 5f : 9f;
      var flap = Mathf.Sin(time * flapSpeed) * (swinging ? 28f : 16f);

      var arm = Vector3.zero;
      var other = new Vector3(0f, 0f, -6f * breathe);
      var head = new Vector3(3f * breathe, 0f, 0f);
      var earTilt = 4f * breathe;
      var earFwd = 0f;
      var body = Vector3.zero;
      var offset = new Vector3(0f, 0.008f * breathe, 0f);
      var squash = Vector3.one;

      if (win >= 0f)
      {
        // 만세: 두 팔을 번쩍, 귀가 쫑긋쫑긋, 제자리에서 빙그르
        var up = Smooth(Mathf.Clamp01(win * 4f));
        arm = new Vector3(_raise * up, 0f, -_swingOut * up);
        other = new Vector3(_raise * up, 0f, _swingOut * up);
        earTilt = Mathf.Sin(time * 18f) * 12f;
        body = new Vector3(0f, 360f * Smooth(win), 0f);
        flap = Mathf.Sin(time * 20f) * 30f;
      }
      else if (hurt)
      {
        // 아파함: 귀가 축 처지고, 고개를 떨구고, 몸이 오들오들
        earTilt = 35f;
        earFwd = 25f;
        head = new Vector3(14f, 0f, 6f);
        arm = new Vector3(-20f, 0f, 0f);
        other = new Vector3(-20f, 0f, 0f);
        offset += new Vector3(Mathf.Sin(time * 45f) * 0.006f, -0.01f, 0f);
        squash = new Vector3(1.02f, 0.97f, 1.02f);
      }
      else if (swinging)
      {
        // 치켜들기 → 내리치기(burstAt) → 따라가기 → 제자리
        var windup = burstAt - 0.3f;
        float a;
        if (swing < windup)
        {
          a = Mathf.Lerp(0f, _raise, Smooth(swing / windup));
        }
        else if (swing < burstAt)
        {
          var k = (swing - windup) / (burstAt - windup);
          a = Mathf.Lerp(_raise, _strike, k * k);
        }
        else
        {
          a = Mathf.Lerp(_strike, 0f, Smooth((swing - burstAt) / (1f - burstAt)));
        }
        // 칫솔 팔은 몸의 -X쪽이라 바깥으로 벌리는 방향이 -Z 회전이다
        arm = new Vector3(a, 0f, -_swingOut * Mathf.Sin(swing * Mathf.PI));
        // 내리치는 순간 몸이 앞으로 쏠리고 귀가 뒤로 휙
        var hitPulse = Mathf.Max(0f, 1f - Mathf.Abs(swing - burstAt) / 0.18f);
        body = new Vector3(0f, 0f, _lean * hitPulse);
        earFwd = -18f * hitPulse + 8f * Mathf.Sin(swing * Mathf.PI * 2f);
        head = new Vector3(-6f * hitPulse, 0f, 0f);
        squash = new Vector3(1f + 0.04f * hitPulse, 1f - 0.04f * hitPulse, 1f);
      }

      Root(offset, body, squash);
      Rotate(Head, head);
      Rotate(EarL, new Vector3(earFwd, 0f, earTilt));
      Rotate(EarR, new Vector3(earFwd, 0f, -earTilt));
      Rotate(WingL, new Vector3(0f, flap, 0f));
      Rotate(WingR, new Vector3(0f, -flap, 0f));
      Rotate(BrushArm, arm);
      Rotate(OtherArm, other);
    }
  }
}
