using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   양치 중 카메라 영상을 정사각형 사진으로 찍는다 (구역마다 한 장, 모두 4장).
  ///   화면에 보이는 그대로(좌우 반전·회전 포함) 찍고, 찍은 사진은 PhotoStore로 기기에만 저장한다.
  ///   카메라 영상이 없으면 (키보드 입력 등) 찍지 않고 그 칸은 비워 둔다.
  /// </summary>
  public class PhotoBooth : MonoBehaviour
  {
    public const int Count = 4;

    [Tooltip("모든 화면이 같이 쓰는 카메라 영상")]
    [SerializeField] private RawImage _feed;
    [Tooltip("찍을 때 카메라 창 위에 번쩍이는 흰 막 (없어도 됨)")]
    [SerializeField] private Image _flash;
    [SerializeField, Min(64)] private int _size = 512;
    [SerializeField, Min(0.05f)] private float _flashSec = 0.4f;

    private readonly Texture2D[] _photos = new Texture2D[Count];
    private float _flashT = -1f;

    public Texture2D[] Photos => _photos;

    /// <summary>양치를 새로 시작할 때 지난 사진을 비운다</summary>
    public void Begin()
    {
      for (var i = 0; i < Count; i++)
      {
        if (_photos[i] != null)
        {
          Destroy(_photos[i]);
          _photos[i] = null;
        }
      }
      SetFlash(0f);
    }

    /// <summary>index번째 사진을 찍어 저장한다. 찍었으면 true</summary>
    public bool Capture(int index)
    {
      var photo = Grab();
      if (photo == null)
      {
        return false;
      }
      if (_photos[index] != null)
      {
        Destroy(_photos[index]);
      }
      _photos[index] = photo;
      PhotoStore.Save(photo, DateTime.Now, index);
      if (_flash != null)
      {
        // 양치 화면이 카메라 영상을 같은 창에 나중에 붙이므로, 번쩍임이 영상보다 앞에 오게
        _flash.transform.SetAsLastSibling();
      }
      _flashT = 0f;
      SoundManager.PlayTouch();
      return true;
    }

    private void Update()
    {
      if (_flashT < 0f)
      {
        return;
      }
      _flashT += Time.deltaTime;
      var k = _flashT / _flashSec;
      SetFlash(k < 1f ? 0.85f * (1f - k) : 0f);
      if (k >= 1f)
      {
        _flashT = -1f;
      }
    }

    private void SetFlash(float alpha)
    {
      if (_flash == null)
      {
        return;
      }
      _flash.gameObject.SetActive(alpha > 0f);
      var c = _flash.color;
      c.a = alpha;
      _flash.color = c;
    }

    private Texture2D Grab()
    {
      var src = _feed != null ? _feed.texture : null;
      if (src == null || src.width < 16 || src.height < 16)
      {
        return null;
      }

      // RawImage가 보여주는 uvRect(좌우/상하 반전)를 그대로 적용해 읽는다
      var uv = _feed.uvRect;
      var full = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
      Graphics.Blit(src, full, uv.size, uv.position);
      var prev = RenderTexture.active;
      RenderTexture.active = full;
      var read = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
      read.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
      read.Apply();
      RenderTexture.active = prev;
      RenderTexture.ReleaseTemporary(full);

      // 모바일 카메라는 RawImage를 돌려서 보여주므로 사진도 같은 방향으로 돌린다
      var angle = Mathf.RoundToInt(Mathf.Repeat(_feed.rectTransform.localEulerAngles.z, 360f) / 90f) % 4 * 90;
      var rotated = Rotate(read, angle);
      if (rotated != read)
      {
        Destroy(read);
      }

      // 가운데 정사각형만 잘라 _size로 줄인다
      var side = Mathf.Min(rotated.width, rotated.height);
      var scale = new Vector2((float)side / rotated.width, (float)side / rotated.height);
      var offset = new Vector2((1f - scale.x) * 0.5f, (1f - scale.y) * 0.5f);
      var square = RenderTexture.GetTemporary(_size, _size, 0, RenderTextureFormat.ARGB32);
      Graphics.Blit(rotated, square, scale, offset);
      RenderTexture.active = square;
      var photo = new Texture2D(_size, _size, TextureFormat.RGB24, false);
      photo.ReadPixels(new Rect(0, 0, _size, _size), 0, 0);
      photo.Apply();
      RenderTexture.active = prev;
      RenderTexture.ReleaseTemporary(square);
      Destroy(rotated);
      return photo;
    }

    /// <summary>화면에서 반시계 방향으로 angle도 돌린 모습 (UI 회전 방향과 같다)</summary>
    private static Texture2D Rotate(Texture2D tex, int angle)
    {
      if (angle == 0)
      {
        return tex;
      }
      int w = tex.width, h = tex.height;
      var src = tex.GetPixels32();
      var swap = angle != 180;
      var nw = swap ? h : w;
      var nh = swap ? w : h;
      var dst = new Color32[src.Length];
      for (var y = 0; y < h; y++)
      {
        for (var x = 0; x < w; x++)
        {
          int nx, ny;
          switch (angle)
          {
            case 90: nx = h - 1 - y; ny = x; break;
            case 180: nx = w - 1 - x; ny = h - 1 - y; break;
            default: nx = y; ny = w - 1 - x; break;
          }
          dst[ny * nw + nx] = src[y * w + x];
        }
      }
      var result = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
      result.SetPixels32(dst);
      result.Apply();
      return result;
    }
  }
}
