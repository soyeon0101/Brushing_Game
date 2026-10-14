using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   파트너 로봇의 화면 눈. 그림 시트 대신 둥근 네모 두 개를 매 프레임 계산해서 그린다 (튀지 않고 부드럽게 바뀐다).
  ///   PartnerRobot이 아래 값만 바꾸면 모양은 여기서 정한다.
  ///   Open: 1 뜸 → 0 감음 (가운데로 납작), Lid: 윗눈꺼풀이 내려온 정도 (졸림), Happy: 1이면 ^^ 반달, Look: 시선 (-1~1, 위가 +)
  /// </summary>
  public class PartnerEyes : MonoBehaviour
  {
    [SerializeField] private RectTransform _screen;
    [SerializeField] private Image _leftEye;
    [SerializeField] private Image _rightEye;
    [SerializeField] private Image _leftGlow;
    [SerializeField] private Image _rightGlow;
    [SerializeField] private Image _leftHappy;
    [SerializeField] private Image _rightHappy;

    [Header("모양 (화면 크기 대비)")]
    [SerializeField] private Color _color = new Color(0.39f, 0.92f, 0.96f, 1f);
    [SerializeField, Range(0.02f, 0.4f)] private float _eyeWidth = 0.15f;
    [SerializeField, Range(0.05f, 0.9f)] private float _eyeHeight = 0.46f;
    [Tooltip("두 눈 사이 간격")]
    [SerializeField, Range(0f, 0.6f)] private float _gap = 0.17f;
    [Tooltip("시선이 끝까지 갔을 때 움직이는 거리")]
    [SerializeField] private Vector2 _lookRange = new Vector2(0.10f, 0.12f);
    [SerializeField, Range(0f, 1f)] private float _glowAlpha = 0.55f;

    [HideInInspector] public float Open = 1f;
    [HideInInspector] public float Lid;
    [HideInInspector] public float Happy;
    [HideInInspector] public Vector2 Look;

    private void LateUpdate()
    {
      Apply();
    }

    private void Apply()
    {
      var r = _screen.rect;
      var ew = r.width * _eyeWidth;
      var eh = r.height * _eyeHeight;
      var gap = r.width * _gap;
      var cy = -r.height * 0.02f + Look.y * r.height * _lookRange.y;
      // 표정을 겹쳐서 섞지 않는다: 앞 절반은 동그란 눈이 납작해져 사라지고, 뒤 절반에 ^^가 펴진다 (이전 표정 잔상 없음)
      var h = Mathf.Clamp01(Happy);
      var normal = 1f - Smooth(Mathf.Clamp01(h * 2f));
      var happy = Smooth(Mathf.Clamp01(h * 2f - 1f));
      Place(_leftEye, _leftGlow, _leftHappy, -1f, ew, eh, gap, cy, r, normal, happy);
      Place(_rightEye, _rightGlow, _rightHappy, 1f, ew, eh, gap, cy, r, normal, happy);
    }

    private void Place(Image eye, Image glow, Image happyEye, float side, float ew, float eh, float gap, float cy, Rect r, float normal, float happy)
    {
      var cx = side * (gap / 2f + ew / 2f) + Look.x * r.width * _lookRange.x;

      // 위아래 끝을 정한다: 감을수록 가운데로 납작, 졸릴수록 윗눈꺼풀이 내려온다
      var h = Mathf.Max(2f, eh * Mathf.Clamp01(Open) * normal);
      var top = cy + h / 2f;
      var bottom = cy - h / 2f;
      top -= (top - bottom) * Mathf.Clamp01(Lid);
      if (top - bottom < 4f && normal > 0.5f)
      {
        // 다 감으면 아래쪽에 가는 줄 (표정이 바뀌는 중에는 줄도 남기지 않는다)
        bottom = cy - eh * 0.25f - 2f;
        top = bottom + 4f;
      }
      var size = new Vector2(ew, Mathf.Max(0f, top - bottom));
      var pos = new Vector2(cx, (top + bottom) / 2f);
      var shown = normal > 0.02f;
      eye.enabled = shown;
      eye.rectTransform.anchoredPosition = pos;
      eye.rectTransform.sizeDelta = size;
      // 모서리가 늘 반원이 되게 9-slice 테두리 배율을 맞춘다
      var border = eye.sprite != null ? eye.sprite.border.x : 0f;
      if (border > 0f)
      {
        eye.pixelsPerUnitMultiplier = border / Mathf.Max(1f, Mathf.Min(size.x, size.y) / 2f);
      }
      eye.color = _color;

      // 번짐은 지금 보이는 눈 모양을 그대로 따라간다 (감으면 같이 납작, 사라지면 같이 사라짐)
      var openness = Mathf.Clamp01(size.y / Mathf.Max(1f, eh));
      glow.enabled = shown;
      glow.rectTransform.anchoredPosition = pos;
      glow.rectTransform.sizeDelta = new Vector2(size.x * 1.6f, size.y * 1.6f) + Vector2.one * ew * 0.25f * openness;
      glow.color = WithAlpha(_color, _glowAlpha * Mathf.Lerp(0.3f, 1f, openness));

      // ^^: 위아래로 펴지며 나타난다 (투명하게 겹쳐 섞지 않음)
      happyEye.enabled = happy > 0.02f;
      happyEye.rectTransform.anchoredPosition = new Vector2(cx, cy + eh * 0.05f);
      happyEye.rectTransform.sizeDelta = new Vector2(ew * 1.6f, ew * 0.75f);
      happyEye.rectTransform.localScale = new Vector3(1f, happy, 1f);
      happyEye.color = _color;
    }

    private static float Smooth(float k)
    {
      return k * k * (3f - 2f * k);
    }

    private static Color WithAlpha(Color c, float a)
    {
      c.a = a;
      return c;
    }
  }
}
