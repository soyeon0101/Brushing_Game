using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   AI로 만든 초록 배경(크로마키) 그림을 게임용 투명 PNG로 바꾼다.
  ///   ArtDrafts/Raw/*.png → Assets/BrushGame/Art/Battle/*.png
  ///   - 가장자리에서 초록으로 이어진 부분만 배경으로 지운다 (캐릭터 안쪽의 초록빛 그림자는 남긴다)
  ///   - 애니메이션 프레임이 떨리지 않도록, 캐릭터 발(맨 아래) 가운데를 캔버스 아래 가운데에 맞춘다. 크기는 바꾸지 않는다
  /// </summary>
  public static class ChromaKeyTool
  {
    private const string RawDir = "ArtDrafts/Raw";
    private const string OutDir = "Assets/BrushGame/Art/Battle";
    private const int Canvas = 1024;
    private const int BottomMargin = 16;

    [MenuItem("BrushGame/Process Green Screen Art (ArtDrafts/Raw)")]
    public static void ProcessAll()
    {
      if (!Directory.Exists(RawDir))
      {
        Debug.LogWarning($"[BrushGame] {RawDir} 폴더가 없습니다.");
        return;
      }
      Directory.CreateDirectory(OutDir);
      var count = 0;
      foreach (var src in Directory.GetFiles(RawDir, "*.png"))
      {
        var dst = $"{OutDir}/{Path.GetFileName(src)}";
        Process(src, dst);
        count++;
      }
      AssetDatabase.Refresh();
      Debug.Log($"[BrushGame] 초록 배경 그림 {count}장 처리 → {OutDir}");
    }

    /// <summary>alignFeet: 캐릭터 프레임용 (1024 캔버스에 발 맞춤). false면 그림 크기에 맞게 잘라낸다 (로고, 아이콘)</summary>
    public static void Process(string src, string dst, bool alignFeet = true)
    {
      var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
      tex.LoadImage(File.ReadAllBytes(src));
      var w = tex.width;
      var h = tex.height;
      var px = tex.GetPixels32();
      Object.DestroyImmediate(tex);

      var green = new float[px.Length];
      for (var i = 0; i < px.Length; i++)
      {
        green[i] = (px[i].g - Mathf.Max(px[i].r, px[i].b)) / 255f;
      }
      var background = FloodFromEdges(green, w, h, 0.04f);

      // 배경을 지우고, 남은 초록 번짐을 줄인다
      var keyed = new Color32[px.Length];
      for (var i = 0; i < px.Length; i++)
      {
        var c = px[i];
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        var mx = Mathf.Max(r, b);
        var a = background[i] ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, green[i])) : 1f;
        if (g > mx)
        {
          g = Mathf.Lerp(mx, g, background[i] ? 0.1f : 0.35f);
        }
        if (a <= 0.001f)
        {
          keyed[i] = new Color32(0, 0, 0, 0);
          continue;
        }
        keyed[i] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255));
      }

      // 위치 맞춤은 가장 큰 덩어리(캐릭터 본체) 기준. 떨어진 부스러기나 거품은 그림에는 남기되 기준에서 뺀다
      if (!LargestBlobBounds(keyed, w, h, out var minX, out var maxX, out var minY))
      {
        Debug.LogWarning($"[BrushGame] {src}: 캐릭터를 찾지 못했습니다.");
        return;
      }

      if (!alignFeet)
      {
        WriteCropped(keyed, w, h, dst);
        return;
      }

      // 발(아래, Unity 텍스처는 y=0이 아래) 가운데를 캔버스 아래 가운데로 옮긴다.
      // 칫솔/날개가 옆으로 뻗으면 전체 폭의 가운데가 흔들리므로, 맨 아래 띠(발)만의 가운데를 쓴다
      var feetCenter = FeetCenterX(keyed, w, minX, maxX, minY);
      var dx = Canvas / 2 - feetCenter;
      var dy = BottomMargin - minY;
      var outPx = new Color32[Canvas * Canvas];
      for (var y = 0; y < h; y++)
      {
        var ty = y + dy;
        if (ty < 0 || ty >= Canvas)
        {
          continue;
        }
        for (var x = 0; x < w; x++)
        {
          var tx = x + dx;
          if (tx >= 0 && tx < Canvas)
          {
            outPx[ty * Canvas + tx] = keyed[y * w + x];
          }
        }
      }
      var outTex = new Texture2D(Canvas, Canvas, TextureFormat.RGBA32, false);
      outTex.SetPixels32(outPx);
      outTex.Apply();
      File.WriteAllBytes(dst, outTex.EncodeToPNG());
      Object.DestroyImmediate(outTex);
    }

    /// <summary>본체 맨 아래에서 위로 FeetBand 픽셀 안의 불투명 픽셀 가로 가운데</summary>
    private static int FeetCenterX(Color32[] px, int w, int minX, int maxX, int minY)
    {
      const int FeetBand = 60;
      long sum = 0;
      var n = 0;
      for (var y = minY; y < minY + FeetBand && y * w < px.Length; y++)
      {
        for (var x = minX; x <= maxX; x++)
        {
          if (px[y * w + x].a >= 128)
          {
            sum += x;
            n++;
          }
        }
      }
      return n > 0 ? (int)(sum / n) : (minX + maxX) / 2;
    }

    /// <summary>보이는 부분 전체(떨어진 반짝이 포함)에 맞게 여백 조금 두고 잘라 저장</summary>
    private static void WriteCropped(Color32[] px, int w, int h, string dst)
    {
      int minX = w, maxX = -1, minY = h, maxY = -1;
      for (var i = 0; i < px.Length; i++)
      {
        if (px[i].a < 64)
        {
          continue;
        }
        int x = i % w, y = i / w;
        minX = Mathf.Min(minX, x);
        maxX = Mathf.Max(maxX, x);
        minY = Mathf.Min(minY, y);
        maxY = Mathf.Max(maxY, y);
      }
      const int pad = 8;
      minX = Mathf.Max(0, minX - pad);
      minY = Mathf.Max(0, minY - pad);
      maxX = Mathf.Min(w - 1, maxX + pad);
      maxY = Mathf.Min(h - 1, maxY + pad);
      int cw = maxX - minX + 1, ch = maxY - minY + 1;
      var outPx = new Color32[cw * ch];
      for (var y = 0; y < ch; y++)
      {
        for (var x = 0; x < cw; x++)
        {
          outPx[y * cw + x] = px[(y + minY) * w + x + minX];
        }
      }
      var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
      outTex.SetPixels32(outPx);
      outTex.Apply();
      File.WriteAllBytes(dst, outTex.EncodeToPNG());
      Object.DestroyImmediate(outTex);
    }

    private static bool LargestBlobBounds(Color32[] px, int w, int h, out int minX, out int maxX, out int minY)
    {
      var label = new int[px.Length];
      var queue = new Queue<int>();
      int best = 0, bestSize = 0, next = 0;
      for (var s = 0; s < px.Length; s++)
      {
        if (label[s] != 0 || px[s].a < 64)
        {
          continue;
        }
        next++;
        var size = 0;
        label[s] = next;
        queue.Enqueue(s);
        while (queue.Count > 0)
        {
          var i = queue.Dequeue();
          size++;
          int x = i % w, y = i / w;
          foreach (var n in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
          {
            if (n >= 0 && label[n] == 0 && px[n].a >= 64)
            {
              label[n] = next;
              queue.Enqueue(n);
            }
          }
        }
        if (size > bestSize)
        {
          bestSize = size;
          best = next;
        }
      }

      minX = w;
      maxX = -1;
      minY = h;
      for (var i = 0; i < px.Length; i++)
      {
        if (label[i] != best || best == 0)
        {
          continue;
        }
        int x = i % w, y = i / w;
        minX = Mathf.Min(minX, x);
        maxX = Mathf.Max(maxX, x);
        minY = Mathf.Min(minY, y);
      }
      return maxX >= 0;
    }

    private static bool[] FloodFromEdges(float[] green, int w, int h, float threshold)
    {
      var mask = new bool[green.Length];
      var queue = new Queue<int>();
      void Push(int i)
      {
        if (!mask[i] && green[i] > threshold)
        {
          mask[i] = true;
          queue.Enqueue(i);
        }
      }
      for (var x = 0; x < w; x++)
      {
        Push(x);
        Push((h - 1) * w + x);
      }
      for (var y = 0; y < h; y++)
      {
        Push(y * w);
        Push(y * w + w - 1);
      }
      while (queue.Count > 0)
      {
        var i = queue.Dequeue();
        int x = i % w, y = i / w;
        if (x > 0) Push(i - 1);
        if (x < w - 1) Push(i + 1);
        if (y > 0) Push(i - w);
        if (y < h - 1) Push(i + w);
      }
      return mask;
    }
  }
}
