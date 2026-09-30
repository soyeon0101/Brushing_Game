using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   카메라 영상(MediaPipe Screen이 텍스처 크기로 맞춘 RawImage)을 이 영역에 꽉 차게 키운다.
  ///   넘치는 부분은 이 오브젝트의 RectMask2D로 잘린다. 영상이 오기 전에는 숨긴다.
  /// </summary>
  [RequireComponent(typeof(RectTransform))]
  public class CameraFeedFitter : MonoBehaviour
  {
    [SerializeField] private RawImage _feed;

    private void LateUpdate()
    {
      var hasTexture = _feed.texture != null;
      _feed.enabled = hasTexture;
      if (!hasTexture)
      {
        return;
      }

      var rt = _feed.rectTransform;
      var size = rt.sizeDelta;
      if (size.x <= 0f || size.y <= 0f)
      {
        return;
      }
      // 모바일 카메라는 90도 돌아간 채로 올 수 있다
      var rotated = Mathf.Abs(Mathf.Repeat(rt.localEulerAngles.z, 180f) - 90f) < 1f;
      var w = rotated ? size.y : size.x;
      var h = rotated ? size.x : size.y;

      var box = ((RectTransform)transform).rect.size;
      var scale = Mathf.Max(box.x / w, box.y / h);
      rt.localScale = new Vector3(scale, scale, 1f);
      rt.anchoredPosition = Vector2.zero;
    }
  }
}
