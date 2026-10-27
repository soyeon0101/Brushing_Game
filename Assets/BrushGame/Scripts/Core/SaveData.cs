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

  /// <summary>보호자 회원가입 정보와 동의 기록. 서버 없이 이 기기 저장 파일에만 남는다</summary>
  [Serializable]
  public class GuardianAccount
  {
    public string guardianName;
    public string email;
    [Tooltip("[필수] 개인정보 수집·이용 동의")]
    public bool agreedPrivacy;
    [Tooltip("[필수] 카메라 권한 이용 동의 (손 인식 + 양치 사진 촬영, 사진은 기기에만 저장)")]
    public bool agreedCamera;
    [Tooltip("동의한 시각 (yyyy-MM-dd HH:mm)")]
    public string agreedAt;
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
    public GuardianAccount account = new GuardianAccount();
    public PlayerProfile profile = new PlayerProfile();
    public GuardianSettings settings = new GuardianSettings();
    [Tooltip("양치 사진 액자에서 마지막으로 고른 테두리")]
    public int photoFrame;
    public List<StickerRecord> stickers = new List<StickerRecord>();
    public List<string> curedPatientIds = new List<string>();
    public List<string> badgeIds = new List<string>();
    public int treatmentCount;
    [Tooltip("우주 항해 지도에서 마지막으로 켜지는 연출을 보여준 발판 수")]
    public int lastSeenLitPads;

    /// <summary>우주 항해 지도의 켜진 발판 수 = 받은 도장 수 (같은 날 같은 끼니는 한 번만이라 규칙이 같다)</summary>
    public int TotalLitPads => stickers.Count;

    [Tooltip("파트너에게 지금까지 준 밥 수")]
    public int partnerFedCount;
    [Tooltip("창고에서 꺼진 로봇을 데려왔는지")]
    public bool partnerBrought;
    [Tooltip("첫 양치로 충전돼 깨어났는지")]
    public bool partnerAwake;
    [Tooltip("아이가 지어 준 파트너 이름")]
    public string partnerName = "";

    /// <summary>데려왔고, 양치 도장이 하나라도 있는데 아직 안 깨어났으면 깨우는 장면을 보여줄 차례</summary>
    public bool PartnerReadyToWake => partnerBrought && !partnerAwake && stickers.Count > 0;

    /// <summary>파트너에게 줄 수 있는 밥 = 받은 도장 수 - 준 밥 수 (안 줘도 줄어들거나 사라지지 않는다)</summary>
    public int PartnerMealsLeft => Mathf.Max(0, stickers.Count - partnerFedCount);

    /// <summary>닉네임까지 정해야 프로필이 만들어진 것으로 본다</summary>
    public bool HasProfile => profile != null && !string.IsNullOrEmpty(profile.nickname);

    /// <summary>필수 동의 두 가지를 하고 보호자 회원가입까지 마쳤는지 (예전 저장 파일은 false라 동의부터 다시 받는다)</summary>
    public bool HasAccount => account != null && account.agreedPrivacy && account.agreedCamera && !string.IsNullOrEmpty(account.guardianName);

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
