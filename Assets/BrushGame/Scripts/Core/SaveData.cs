using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BrushGame
{
  public enum MealSlot { Morning, Lunch, Evening }

  [Serializable]
  public class PlayerProfile
  {
    [Tooltip("4~7세")]
    public int age;
    [Tooltip("GameCatalog의 캐릭터 id")]
    public string characterId;
    public string nickname;
  }

  [Serializable]
  public class StickerRecord
  {
    [Tooltip("yyyy-MM-dd")]
    public string date;
    public MealSlot slot;
    public string patientId;
  }

  [Serializable]
  public class GuardianSettings
  {
    public const int MinZoneSeconds = 20;
    public const int MaxZoneSeconds = 30;

    public bool sound = true;
    [Tooltip("음성 안내는 아직 없음. 설정값만 저장해 둔다")]
    public bool voiceGuide = true;
    [Tooltip("한 구역을 닦아야 하는 시간 (초)")]
    public int zoneSeconds = 25;

    public void ApplyAudio()
    {
      AudioListener.volume = sound ? 1f : 0f;
    }
  }

  /// <summary>
  ///   기기에 저장되는 전체 기록.
  ///   필드를 새로 추가해도 예전 저장 파일은 그대로 읽히고, 새 필드만 기본값이 된다.
  /// </summary>
  [Serializable]
  public class SaveData
  {
    public int version = 1;
    public PlayerProfile profile = new PlayerProfile();
    public GuardianSettings settings = new GuardianSettings();
    public List<StickerRecord> stickers = new List<StickerRecord>();
    public List<string> curedPatientIds = new List<string>();
    public int treatmentCount;

    /// <summary>닉네임까지 정해야 프로필이 만들어진 것으로 본다</summary>
    public bool HasProfile => profile != null && !string.IsNullOrEmpty(profile.nickname);

    public static string DateKey(DateTime time) => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static MealSlot SlotAt(DateTime time) =>
      time.Hour < 11 ? MealSlot.Morning : time.Hour < 17 ? MealSlot.Lunch : MealSlot.Evening;

    public bool HasSticker(string date, MealSlot slot) => stickers.Exists(s => s.date == date && s.slot == slot);
  }
}
