using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   로비 꾸밈에 쓰는 작은 그림들을 코드로 그려 Art/UI/Lobby 에 PNG로 저장한다 (없을 때만 만든다).
  ///   더 예쁜 그림으로 바꾸려면 같은 이름의 PNG를 덮어쓰면 된다. 지우면 다음에 로비를 만들 때 다시 그린다.
  /// </summary>
  public static class LobbyArt
  {
    public const string Dir = "Assets/BrushGame/Art/UI/Lobby";

    /// <summary>둥근 판 (9-slice). UiSkin이 건드리지 않도록 Placeholder/Rounded 와 다른 그림</summary>
    public static Sprite Soft => Get("Lobby_Soft", 128, 128, (x, y) => Fill(Color.white, RoundRect(x, y, 128, 128, 48f)), new Vector4(48, 48, 48, 48));

    public static Sprite Disc => Get("Lobby_Disc", 256, 256, (x, y) => Fill(Color.white, Circle(x, y, 128, 128, 126f)));

    public static Sprite Ring => Get("Lobby_Ring", 256, 256, (x, y) => Fill(Color.white, Mathf.Abs(Circle(x, y, 128, 128, 116f)) - 10f));

    /// <summary>빈 도장 칸: 점선 동그라미</summary>
    public static Sprite DashRing => Get("Lobby_DashRing", 256, 256, (x, y) =>
    {
      var ring = Mathf.Abs(Circle(x, y, 128, 128, 118f)) - 5f;
      var angle = Mathf.Atan2(y - 128f, x - 128f);
      var dash = Mathf.Repeat(angle / (Mathf.PI * 2f) * 18f, 1f);
      var gap = dash < 0.62f ? 0f : 1f;
      return gap > 0f ? Color.clear : Fill(Color.white, ring);
    });

    /// <summary>환자 뒤 은은한 빛</summary>
    public static Sprite Glow => Get("Lobby_Glow", 256, 256, (x, y) =>
    {
      var d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(128f, 128f)) / 128f);
      var a = Mathf.Pow(1f - d, 1.6f);
      return new Color(1f, 1f, 1f, a);
    });

    /// <summary>말풍선 꼬리 (아래로 뾰족)</summary>
    public static Sprite Tail => Get("Lobby_Tail", 64, 40, (x, y) =>
    {
      // 위쪽 변 전체 → 아래 가운데 한 점
      var halfWidth = 32f * (y / 40f);
      return Fill(Color.white, Mathf.Abs(x - 32f) - halfWidth);
    });

    /// <summary>시작 버튼 위를 지나가는 반짝 띠</summary>
    public static Sprite Shine => Get("Lobby_Shine", 64, 8, (x, y) =>
    {
      var u = x / 64f;
      var a = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f);
      return new Color(1f, 1f, 1f, a);
    });

    /// <summary>떠오르는 비눗방울</summary>
    public static Sprite Bubble => Get("Lobby_Bubble", 128, 128, (x, y) =>
    {
      var edge = Mathf.Abs(Circle(x, y, 64, 64, 58f)) - 3f;
      var rim = Fill(new Color(1f, 1f, 1f, 0.85f), edge);
      var d = Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f)) / 58f;
      var inside = d < 1f ? new Color(1f, 1f, 1f, 0.12f) : Color.clear;
      var spot = Fill(new Color(1f, 1f, 1f, 0.9f), Circle(x, y, 42, 86, 11f));
      return Over(Over(inside, rim), spot);
    });

    /// <summary>네 갈래 반짝이 (환자 주변에서 깜빡임)</summary>
    public static Sprite Sparkle => Get("Lobby_Sparkle", 128, 128, (x, y) =>
    {
      var dx = Mathf.Abs(x - 64f) / 60f;
      var dy = Mathf.Abs(y - 64f) / 60f;
      // 별 모양 (|x|^0.5 + |y|^0.5 <= 1), 대략 픽셀 거리로 바꿔 부드럽게
      var f = (Mathf.Sqrt(dx) + Mathf.Sqrt(dy) - 1f) * 40f;
      var glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f)) / 30f) * 0.5f;
      return Over(new Color(1f, 0.96f, 0.7f, glow), Fill(new Color(1f, 0.97f, 0.8f), f));
    });

    public static Sprite Sunrise => Get("Lobby_Sunrise", 128, 128, (x, y) =>
    {
      var sun = Mathf.Max(Circle(x, y, 64, 44, 34f), 40f - y);
      var ground = Mathf.Abs(y - 36f) - 3.5f;
      ground = Mathf.Max(ground, Mathf.Abs(x - 64f) - 54f);
      var rays = Rays(x, y, 64f, 44f, 44f, 58f, 5, Mathf.PI * 0.25f, Mathf.PI * 0.75f);
      var c = new Color(1f, 0.63f, 0.35f);
      return Over(Over(Fill(c, rays), Fill(c, sun)), Fill(new Color(1f, 0.55f, 0.3f), ground));
    });

    public static Sprite Sun => Get("Lobby_Sun", 128, 128, (x, y) =>
    {
      var c = new Color(1f, 0.78f, 0.25f);
      var rays = Rays(x, y, 64f, 64f, 40f, 58f, 8, 0f, Mathf.PI * 2f);
      return Over(Fill(c, rays), Fill(c, Circle(x, y, 64, 64, 30f)));
    });

    public static Sprite Moon => Get("Lobby_Moon", 128, 128, (x, y) =>
    {
      var crescent = Mathf.Max(Circle(x, y, 60, 64, 44f), -Circle(x, y, 82, 78, 38f));
      var star = Circle(x, y, 100, 36, 6f);
      var c = new Color(0.6f, 0.53f, 0.93f);
      return Over(Fill(c, crescent), Fill(c, star));
    });

    /// <summary>시작 버튼 칫솔</summary>
    public static Sprite Brush => Get("Lobby_Brush", 256, 96, (x, y) =>
    {
      var handle = Capsule(x, y, 18f, 34f, 190f, 34f, 13f);
      var head = RoundRect(x - 156f, y - 36f, 70f, 40f, 8f);
      var bristles = Mathf.Max(RoundRect(x - 160f, y - 50f, 62f, 38f, 4f), Mathf.Repeat(x, 10f) < 5f ? 1f : -1f);
      return Over(Over(Fill(Color.white, handle), Fill(Color.white, head)), Fill(new Color(0.55f, 0.88f, 1f), bristles));
    });

    // ---------- 그리기 ----------

    private static Sprite Get(string name, int w, int h, Func<float, float, Color> paint, Vector4 border = default)
    {
      var path = $"{Dir}/{name}.png";
      if (!File.Exists(path))
      {
        Directory.CreateDirectory(Dir);
        const int ss = 3;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (var y = 0; y < h; y++)
        {
          for (var x = 0; x < w; x++)
          {
            var sum = Color.clear;
            for (var sy = 0; sy < ss; sy++)
            {
              for (var sx = 0; sx < ss; sx++)
              {
                var c = paint(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                // 미리 곱한 알파로 더해야 가장자리가 하얗게/검게 뜨지 않는다
                sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
              }
            }
            sum /= ss * ss;
            px[y * w + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
          }
        }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
      }
      return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>거리(안쪽 음수)로 칠한다. 경계는 한 픽셀에 걸쳐 부드럽게</summary>
    private static Color Fill(Color c, float sdf)
    {
      var a = Mathf.Clamp01(0.5f - sdf) * c.a;
      return new Color(c.r, c.g, c.b, a);
    }

    private static Color Over(Color under, Color over)
    {
      var a = over.a + under.a * (1f - over.a);
      if (a <= 0f)
      {
        return Color.clear;
      }
      var rgb = ((Vector4)over * over.a + (Vector4)under * under.a * (1f - over.a)) / a;
      return new Color(rgb.x, rgb.y, rgb.z, a);
    }

    private static float Circle(float x, float y, float cx, float cy, float r) => Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;

    private static float RoundRect(float x, float y, float w, float h, float r)
    {
      var qx = Mathf.Abs(x - w / 2f) - (w / 2f - r);
      var qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
      return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
    {
      var p = new Vector2(x - ax, y - ay);
      var ab = new Vector2(bx - ax, by - ay);
      var t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
      return (p - ab * t).magnitude - r;
    }

    /// <summary>해 둘레 빛줄기 (from~to 각도 사이에 count개)</summary>
    private static float Rays(float x, float y, float cx, float cy, float r0, float r1, int count, float from, float to)
    {
      var best = float.MaxValue;
      for (var i = 0; i < count; i++)
      {
        var a = count == 1 ? from : Mathf.Lerp(from, to, to - from >= Mathf.PI * 2f - 0.01f ? i / (float)count : i / (float)(count - 1));
        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        best = Mathf.Min(best, Capsule(x, y, cx + dir.x * r0, cy + dir.y * r0, cx + dir.x * r1, cy + dir.y * r1, 4.5f));
      }
      return best;
    }
  }
}
