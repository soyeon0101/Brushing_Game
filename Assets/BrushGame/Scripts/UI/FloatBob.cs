using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   제자리에서 위아래로 둥실거린다. 여러 개가 같이 있으면 phase로 박자를 어긋나게 한다.
  /// </summary>
  [RequireComponent(typeof(RectTransform))]
  public class FloatBob : MonoBehaviour
  {
    [SerializeField] private float _amplitude = 14f;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _phase;

    private RectTransform _rt;
    private Vector2 _home;

    public void Configure(float amplitude, float speed, float phase)
    {
      _amplitude = amplitude;
      _speed = speed;
      _phase = phase;
    }

    private void Awake()
    {
      _rt = (RectTransform)transform;
      _home = _rt.anchoredPosition;
    }

    private void OnDisable()
    {
      if (_rt != null)
      {
        _rt.anchoredPosition = _home;
      }
    }

    private void Update()
    {
      _rt.anchoredPosition = _home + new Vector2(0f, Mathf.Sin(Time.time * _speed + _phase) * _amplitude);
    }
  }
}
