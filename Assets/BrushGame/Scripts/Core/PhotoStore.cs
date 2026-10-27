using System;
using System.IO;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   양치 사진을 기기 안(Application.persistentDataPath/Photos)에만 저장한다. 서버로 보내는 코드는 없다.
  ///   같은 날 같은 끼니에 다시 양치하면 그 끼니 사진을 덮어쓴다.
  /// </summary>
  public static class PhotoStore
  {
    private const string DirName = "Photos";

    public static string RootPath => Path.Combine(Application.persistentDataPath, DirName);

    public static void Save(Texture2D photo, DateTime time, int index)
    {
      if (photo == null)
      {
        return;
      }
      try
      {
        var dir = Path.Combine(RootPath, $"{SaveData.DateKey(time)}_{SaveData.SlotAt(time)}");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"brush_{index + 1}.png"), photo.EncodeToPNG());
      }
      catch (Exception e)
      {
        Debug.LogError($"[PhotoStore] 사진 저장 실패: {e.Message}");
      }
    }

    /// <summary>보호자 설정의 초기화: 기록과 함께 사진도 모두 지운다</summary>
    public static void DeleteAll()
    {
      try
      {
        if (Directory.Exists(RootPath))
        {
          Directory.Delete(RootPath, true);
        }
      }
      catch (Exception e)
      {
        Debug.LogError($"[PhotoStore] 사진 삭제 실패: {e.Message}");
      }
    }
  }
}
