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
    public const int MinZoneSeconds = 10;
    public const int MaxZoneSeconds = 60;
    public const int DefaultZoneSeconds = 20;

    [Tooltip("전체 소리. 끄면(음소거) 배경음/효과음 설정과 상관없이 모두 꺼진다")]
    public bool sound = true;
    [Tooltip("배경음 (끄면 음소거)")]
    public bool music = true;
    [Tooltip("버튼, 완료 같은 효과음 (끄면 음소거)")]
    public bool effects = true;
    [Tooltip("전체 볼륨 0~1")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float effectVolume = 1f;
    [Tooltip("음성 안내는 아직 없음. 설정값만 저장해 둔다")]
    public bool voiceGuide = true;
    [Tooltip("한 구역을 닦아야 하는 시간 (초)")]
    public int zoneSeconds = DefaultZoneSeconds;

    public void ApplyAudio()
    {
      AudioListener.volume = sound ? Mathf.Clamp01(masterVolume) : 0f;
      SoundManager.ApplySettings();
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
    public List<string> badgeIds = new List<string>();
    public int treatmentCount;

    /// <summary>닉네임까지 정해야 프로필이 만들어진 것으로 본다</summary>
    public bool HasProfile => profile != null && !string.IsNullOrEmpty(profile.nickname);

    public static string DateKey(DateTime time) => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static MealSlot SlotAt(DateTime time) =>
      time.Hour < 11 ? MealSlot.Morning : time.Hour < 17 ? MealSlot.Lunch : MealSlot.Evening;

    public static string SlotName(MealSlot slot) => slot switch
    {
      MealSlot.Morning => "아침",
      MealSlot.Lunch => "점심",
      _ => "저녁",
    };

    /// <summary>로비 카드, 환자 소개 제목 ("아침에 온 친구")</summary>
    public static string VisitorTitle(MealSlot slot) => $"{SlotName(slot)}에 온 친구";

    public bool HasSticker(string date, MealSlot slot) => FindSticker(date, slot) != null;

    public StickerRecord FindSticker(string date, MealSlot slot) => stickers.Find(s => s.date == date && s.slot == slot);
  }
}
