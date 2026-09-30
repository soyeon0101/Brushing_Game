using System;
using System.IO;
using UnityEngine;

namespace BrushGame
{
  /// <summary>저장 파일 읽기/쓰기. 게임 어디서든 SaveStore.Data로 접근한다.</summary>
  public static class SaveStore
  {
    private const string FileName = "save.json";

    private static SaveData _data;

    public static SaveData Data => _data ??= Load();

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Save()
    {
      try
      {
        File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
      }
      catch (Exception e)
      {
        Debug.LogError($"[SaveStore] 저장 실패: {e.Message}");
      }
    }

    /// <summary>프로필과 기록을 모두 지운다 (보호자 설정의 초기화)</summary>
    public static void ResetAll()
    {
      _data = new SaveData();
      Save();
    }

    private static SaveData Load()
    {
      try
      {
        if (File.Exists(FilePath))
        {
          var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
          if (data != null)
          {
            return data;
          }
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[SaveStore] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
      }
      return new SaveData();
    }
  }
}
