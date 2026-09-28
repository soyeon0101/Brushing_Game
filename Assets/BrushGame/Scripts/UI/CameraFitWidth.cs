using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   세로 화면에서 기기 비율이 달라도 정사영 카메라의 가로 폭을 고정해 좌우가 잘리지 않게 한다.
  ///   화면이 maxAspect(기본 9:16)보다 넓으면 (태블릿, 가로로 넓은 Game 창 등)
  ///   게임 영역을 가운데 9:16으로 고정하고 양옆은 여백으로 채운다. Screen Space - Camera 캔버스도 같이 따라간다.
  /// </summary>
  [ExecuteAlways, RequireComponent(typeof(Camera))]
  public class CameraFitWidth : MonoBehaviour
  {
    [SerializeField] private float _targetWorldWidth = 10f;
    [Tooltip("이보다 넓은 화면은 양옆에 여백을 둔다 (가로/세로)")]
    [SerializeField] private float _maxAspect = 9f / 16f;
    [SerializeField] private Color _barColor = Color.black;

    private Camera _cam;
    private Camera _barCamera;
    private int _lastWidth = -1;
    private int _lastHeight = -1;

    private void OnEnable()
    {
      _cam = GetComponent<Camera>();
      _lastWidth = -1;
      EnsureBarCamera();
    }

    private void OnDisable()
    {
      if (_barCamera != null)
      {
        DestroyBarCamera();
      }
      _cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void LateUpdate()
    {
      // 카메라 rect를 바꾸면 cam.aspect도 바뀌므로, 카메라가 그리는 대상(Game 창/기기 화면) 전체 크기로 변화를 본다
      var rect = _cam.rect;
      var w = Mathf.RoundToInt(_cam.pixelWidth / Mathf.Max(rect.width, 0.0001f));
      var h = Mathf.RoundToInt(_cam.pixelHeight / Mathf.Max(rect.height, 0.0001f));
      if (w == _lastWidth && h == _lastHeight || w <= 0 || h <= 0)
      {
        return;
      }
      _lastWidth = w;
      _lastHeight = h;

      var screenAspect = (float)w / h;
      if (screenAspect > _maxAspect)
      {
        var width = _maxAspect / screenAspect;
        _cam.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
      }
      else
      {
        _cam.rect = new Rect(0f, 0f, 1f, 1f);
      }
      _cam.orthographicSize = _targetWorldWidth / (2f * _cam.aspect);
    }

    /// <summary>게임 영역 밖(여백)을 칠하는 카메라. 씬에 저장되지 않는다</summary>
    private void EnsureBarCamera()
    {
      if (_barCamera != null)
      {
        return;
      }
      var go = new GameObject("LetterboxBars") { hideFlags = HideFlags.HideAndDontSave };
      _barCamera = go.AddComponent<Camera>();
      _barCamera.depth = _cam.depth - 1f;
      _barCamera.clearFlags = CameraClearFlags.SolidColor;
      _barCamera.backgroundColor = _barColor;
      _barCamera.cullingMask = 0;
      _barCamera.orthographic = true;
    }

    private void DestroyBarCamera()
    {
      if (Application.isPlaying)
      {
        Destroy(_barCamera.gameObject);
      }
      else
      {
        DestroyImmediate(_barCamera.gameObject);
      }
      _barCamera = null;
    }
  }
}
