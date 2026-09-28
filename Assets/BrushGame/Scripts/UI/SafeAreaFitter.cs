using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   RectTransform을 Screen.safeArea에 맞춘다. 상단 알림처럼 노치에 가리면 안 되는 UI의 부모로 쓴다.
  /// </summary>
  [ExecuteAlways, RequireComponent(typeof(RectTransform))]
  public class SafeAreaFitter : MonoBehaviour
  {
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    private void OnEnable()
    {
      _rect = (RectTransform)transform;
      _lastScreen = Vector2Int.zero;
    }

    private void Update()
    {
      var safe = Screen.safeArea;
      var screen = new Vector2Int(Screen.width, Screen.height);
      if (safe == _lastSafeArea && screen == _lastScreen || screen.x <= 0 || screen.y <= 0)
      {
        return;
      }
      _lastSafeArea = safe;
      _lastScreen = screen;

      _rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
      _rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
      _rect.offsetMin = Vector2.zero;
      _rect.offsetMax = Vector2.zero;
    }
  }
}
