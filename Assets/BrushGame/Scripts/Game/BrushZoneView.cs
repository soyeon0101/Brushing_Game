using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   양치 구역 하나(예: 왼쪽 위 치아들)의 모습. 투명도만 바꾸므로 그림을 넣어도 색이 변하지 않는다.
  ///   닦는 동안 거품이 차오르고, 진행도에 따라 얼룩이 하나씩 사라지고, 완료되면 반짝인다.
  /// </summary>
  public class BrushZoneView : MonoBehaviour
  {
    [Tooltip("지금 닦을 구역일 때만 켜지는 강조 테두리")]
    [SerializeField] private GameObject _highlight;
    [Tooltip("진행도에 따라 순서대로 사라지는 얼룩")]
    [SerializeField] private Graphic[] _dirt;
    [Tooltip("닦는 동안 보이는 거품")]
    [SerializeField] private Graphic[] _foam;
    [Tooltip("구역 완료 시 반짝임")]
    [SerializeField] private Graphic[] _sparkles;

    [SerializeField] private float _foamRiseSpeed = 3f;
    [SerializeField] private float _foamFallSpeed = 1.2f;
    [SerializeField] private float _sparkleSec = 1.2f;

    private float _progress;
    private bool _brushing;
    private float _foamLevel;
    private float _sparkleT = -1f;

    public void ResetDirty()
    {
      _progress = 0f;
      _brushing = false;
      _foamLevel = 0f;
      _sparkleT = -1f;
      SetHighlight(false);
      Apply();
    }

    public void SetHighlight(bool on)
    {
      _highlight.SetActive(on);
    }

    public void SetState(float progress, bool brushing)
    {
      _progress = progress;
      _brushing = brushing;
    }

    public void PlayComplete()
    {
      _progress = 1f;
      _brushing = false;
      _sparkleT = 0f;
      SetHighlight(false);
    }

    private void Update()
    {
      var target = _brushing ? 1f : 0f;
      _foamLevel = Mathf.MoveTowards(_foamLevel, target, (target > _foamLevel ? _foamRiseSpeed : _foamFallSpeed) * Time.deltaTime);
      if (_sparkleT >= 0f)
      {
        _sparkleT += Time.deltaTime;
        if (_sparkleT >= _sparkleSec)
        {
          _sparkleT = -1f;
        }
      }
      Apply();
    }

    private void Apply()
    {
      // 얼룩 i는 진행도 구간 [i/n, (i+1)/n] 근처에서 서서히 사라진다
      var n = _dirt.Length;
      for (var i = 0; i < n; i++)
      {
        var start = 0.85f * i / n;
        var end = 0.85f * (i + 1) / n + 0.15f;
        SetAlpha(_dirt[i], 1f - Mathf.InverseLerp(start, end, _progress));
      }

      // 거품은 하나씩 차례로 차오른다
      for (var i = 0; i < _foam.Length; i++)
      {
        SetAlpha(_foam[i], 0.9f * Mathf.Clamp01(_foamLevel * _foam.Length - i));
      }

      var k = _sparkleT >= 0f ? _sparkleT / _sparkleSec : 1f;
      var sparkle = _sparkleT >= 0f ? Mathf.Sin(k * Mathf.PI) : 0f;
      foreach (var s in _sparkles)
      {
        SetAlpha(s, sparkle);
        s.rectTransform.localScale = Vector3.one * (0.6f + 0.6f * sparkle);
      }
    }

    private static void SetAlpha(Graphic g, float a)
    {
      var c = g.color;
      c.a = a;
      g.color = c;
    }
  }
}
