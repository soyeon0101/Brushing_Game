using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   충치균 3D 동작. 맞으면 뒤로 젖혀지며 팔·날개가 번쩍 들리고, 양치를 멈추면 포크를 흔들며 깔깔대고, 쓰러지면 축 늘어진다.
  ///   각도는 전부 "처음 자세에서 얼마나 더 돌리는지"이다. 팔은 어깨, 날개는 등, 꼬리는 엉덩이가 중심이다.
  /// </summary>
  public class VillainPuppet : PuppetBase
  {
    [SerializeField] private Transform _wingL;
    [SerializeField] private Transform _wingR;
    [Tooltip("포크를 든 팔")]
    [SerializeField] private Transform _forkArm;
    [SerializeField] private Transform _otherArm;
    [SerializeField] private Transform _tail;

    [Header("맞기")]
    [Tooltip("맞았을 때 뒤로 젖혀지는 각도 (도)")]
    [SerializeField] private float _recoil = 18f;

    private const int WingL = 0, WingR = 1, ForkArm = 2, OtherArm = 3, Tail = 4;

    protected override Transform[] Parts => new[] { _wingL, _wingR, _forkArm, _otherArm, _tail };

    /// <param name="hit">맞은 뒤 진행도 (0~1), 아니면 음수</param>
    /// <param name="taunt">양치를 멈춰서 신난 중</param>
    /// <param name="defeat">쓰러지는 진행도 (0~1), 아니면 음수</param>
    public void Pose(float time, float hit, bool taunt, float defeat)
    {
      var breathe = Mathf.Sin(time * 2f);
      var flap = Mathf.Sin(time * 7f) * 14f;
      var armA = new Vector3(0f, 0f, 5f * breathe);
      var armB = new Vector3(0f, 0f, -5f * breathe);
      var tail = new Vector3(0f, Mathf.Sin(time * 3f) * 15f, 0f);
      var body = Vector3.zero;
      var offset = Vector3.zero;
      var squash = new Vector3(1f + 0.015f * breathe, 1f - 0.015f * breathe, 1f + 0.015f * breathe);

      if (defeat >= 0f)
      {
        // 축 늘어짐: 날개·팔·꼬리가 처지고 뒤로 넘어간다 (회전과 사라짐은 UI 쪽에서)
        var k = Smooth(Mathf.Clamp01(defeat * 2f));
        flap = -35f * k;
        armA = new Vector3(0f, 0f, 30f * k);
        armB = new Vector3(0f, 0f, -30f * k);
        tail = new Vector3(40f * k, 0f, 0f);
        body = new Vector3(-25f * k, 0f, 0f);
        squash = new Vector3(1f + 0.1f * k, 1f - 0.2f * k, 1f + 0.1f * k);
      }
      else if (hit >= 0f)
      {
        // 맞음: 뒤로 젖혀지며 팔·날개가 번쩍, 몸이 출렁
        var w = Wobble(hit, 2.5f);
        var jolt = 1f - Smooth(Mathf.Clamp01(hit * 1.5f));
        body = new Vector3(-_recoil * jolt, 0f, -_recoil * 0.6f * w);
        armA = new Vector3(0f, 0f, -70f * jolt);
        armB = new Vector3(0f, 0f, 70f * jolt);
        flap = 35f * jolt + 10f * w;
        tail = new Vector3(-30f * jolt, 25f * w, 0f);
        squash = new Vector3(1f + 0.12f * w, 1f - 0.12f * w, 1f + 0.12f * w);
      }
      else if (taunt)
      {
        // 깔깔: 들썩들썩, 포크를 흔들고, 꼬리를 살랑
        var bounce = Mathf.Abs(Mathf.Sin(time * 8f));
        offset = new Vector3(0f, 0.05f * bounce, 0f);
        armA = new Vector3(0f, 0f, -60f - 25f * Mathf.Sin(time * 12f));
        armB = new Vector3(0f, 0f, 20f * Mathf.Sin(time * 8f));
        flap = Mathf.Sin(time * 14f) * 25f;
        tail = new Vector3(0f, Mathf.Sin(time * 10f) * 30f, 0f);
        body = new Vector3(0f, 0f, Mathf.Sin(time * 8f) * 6f);
        squash = new Vector3(1f - 0.05f * bounce, 1f + 0.05f * bounce, 1f - 0.05f * bounce);
      }

      Root(offset, body, squash);
      Rotate(WingL, new Vector3(0f, flap, 0f));
      Rotate(WingR, new Vector3(0f, -flap, 0f));
      Rotate(ForkArm, armA);
      Rotate(OtherArm, armB);
      Rotate(Tail, tail);
    }
  }
}
