using System;
using UnityEngine;

namespace BrushGame
{
  [Serializable]
  public class PlayerCharacter
  {
    public string id;
    public string displayName;
    public Sprite image;
    [Tooltip("얼굴~쇄골까지 자른 그림. 선택 카드, 프로필에 쓴다. 비우면 image")]
    public Sprite portrait;
    [Tooltip("양치 화면에서 충치균과 겨루는 친구의 프레임 그림. 비우면 씬에 들어 있는 기본 친구(토끼)")]
    public BattleView.Frames battle = new BattleView.Frames();

    public Sprite Portrait => portrait != null ? portrait : image;
  }

  [Serializable]
  public class PatientInfo
  {
    public string id;
    public string displayName;
    [Tooltip("로비 환자 카드와 치료 전 모습")]
    public Sprite before;
    [Tooltip("치료 완료 후 모습")]
    public Sprite after;
    [Tooltip("치료 전 모습을 얼굴~쇄골까지 자른 그림. 로비, 환자 소개, 양치 안내에 쓴다. 비우면 before")]
    public Sprite portrait;
    [Tooltip("양치 화면 배경: 입을 크게 벌린 얼굴 (세로 화면을 꽉 채운다)")]
    public Sprite face;
    [Tooltip("양치 화면의 윗잇몸/입 모양. 비우면 기본 잇몸")]
    public Sprite mouthTop;
    [Tooltip("양치 화면의 아랫잇몸/입 모양. 비우면 기본 잇몸")]
    public Sprite mouthBottom;
    [Tooltip("얼굴 그림(1080x1920) 가운데 기준 잇몸 크기. 0이면 씬에 놓인 위치/크기 그대로 (지그 얼굴 기준)")]
    [Min(0f)] public float mouthScale;
    [Tooltip("mouthScale이 0이 아닐 때 윗잇몸 그림의 가운데 위치")]
    public Vector2 mouthTopPos;
    [Tooltip("mouthScale이 0이 아닐 때 아랫잇몸 그림의 가운데 위치")]
    public Vector2 mouthBottomPos;
    [Tooltip("윗니를 잇몸 그림 안에서 위아래로 옮기는 값 (위가 +). 잇몸 띠 위치가 지그 그림과 다를 때")]
    public float teethTopShift;
    [Tooltip("아랫니를 잇몸 그림 안에서 위아래로 옮기는 값 (위가 +)")]
    public float teethBottomShift;
    [TextArea] public string requestLine;

    public Sprite Portrait => portrait != null ? portrait : before;
    [TextArea] public string thanksLine;
  }

  public enum BadgeKind
  {
    /// <summary>하루도 빠짐없이 양치한 날 수 (하루에 도장 1개 이상)</summary>
    StreakDays,
    /// <summary>아침·점심·저녁 21칸을 모두 채운 주 수 (누적)</summary>
    FullWeeks,
  }

  /// <summary>반짝 배지 하나. 조건을 처음 채우면 받고, 한 번 받으면 사라지지 않는다</summary>
  [Serializable]
  public class BadgeInfo
  {
    public string id;
    public string displayName;
    [TextArea] public string description;
    public BadgeKind kind;
    [Min(1)] public int target = 1;
    [Tooltip("배지 그림. 비우면 임시 원에 목표(7일, 1주 등)를 적어 보여준다")]
    public Sprite icon;

    public string TargetLabel => kind == BadgeKind.StreakDays ? $"{target}일" : $"{target}주";
  }

  /// <summary>
  ///   캐릭터·환자처럼 늘어날 수 있는 콘텐츠 목록. 코드 수정 없이 여기에 항목을 추가하면 된다.
  /// </summary>
  [CreateAssetMenu(menuName = "BrushGame/Game Catalog", fileName = "GameCatalog")]
  public class GameCatalog : ScriptableObject
  {
    public int[] ages = { 4, 5, 6, 7 };
    public PlayerCharacter[] characters;
    public PatientInfo[] patients;
    [Tooltip("우주 항해 지도의 행성. 첫 행성이 맨 아래(처음부터 열림). 하나 추가하면 지도가 한 구간 길어진다")]
    public PlanetData[] planets;
    [Tooltip("배지함에 보이는 순서. 매일 양치 3개가 윗줄, 도장판 3개가 아랫줄")]
    public BadgeInfo[] badges =
    {
      new BadgeInfo { id = "streak7", displayName = "매일매일 별", description = "7일 동안\n매일 양치했어!", kind = BadgeKind.StreakDays, target = 7 },
      new BadgeInfo { id = "streak15", displayName = "매일매일 달", description = "15일 동안\n매일 양치했어!", kind = BadgeKind.StreakDays, target = 15 },
      new BadgeInfo { id = "streak30", displayName = "매일매일 해", description = "30일 동안\n매일 양치했어!", kind = BadgeKind.StreakDays, target = 30 },
      new BadgeInfo { id = "board1", displayName = "꽉 찬 도장판", description = "도장판 한 주를\n꽉 채웠어!", kind = BadgeKind.FullWeeks, target = 1 },
      new BadgeInfo { id = "board2", displayName = "꽉 찬 도장판 2주", description = "도장판을 2주\n꽉 채웠어!", kind = BadgeKind.FullWeeks, target = 2 },
      new BadgeInfo { id = "board4", displayName = "꽉 찬 도장판 4주", description = "도장판을 4주\n꽉 채웠어!", kind = BadgeKind.FullWeeks, target = 4 },
    };

    public PlayerCharacter FindCharacter(string id) => Array.Find(characters, c => c.id == id);

    public PatientInfo FindPatient(string id) => Array.Find(patients, p => p.id == id);

    /// <summary>
    ///   끼니(아침/점심/저녁)마다 환자가 한 명 온다.
    ///   이번 끼니 도장을 이미 받았으면 그때 치료한 환자가 다시 오고 (연습).
    ///   맨 처음(도장 0개)은 목록 첫 환자. 그 뒤로는 한 바퀴씩 섞어서, 모두 한 번씩 만난 뒤에 다시 섞는다.
    ///   첫 바퀴 = 첫 환자를 뺀 나머지, 다음 바퀴부터 = 전원. 바퀴가 바뀔 때 같은 환자가 연달아 오지 않게 한다.
    ///   순서는 "닉네임 + 몇 번째 바퀴"로 정해서, 도장을 하나 더 받기 전까지는 로비·환자 소개·다시 켜도 같은 환자다.
    /// </summary>
    public PatientInfo PatientFor(SaveData data, DateTime time)
    {
      var sticker = data.FindSticker(SaveData.DateKey(time), SaveData.SlotAt(time));
      var done = sticker != null ? FindPatient(sticker.patientId) : null;
      if (done != null)
      {
        return done;
      }
      var count = data.stickers.Count;
      if (count == 0 || patients.Length == 1)
      {
        return patients[0];
      }
      var seed = StableHash(data.profile?.nickname ?? "");
      var n = patients.Length;
      var step = count - 1;
      if (step < n - 1)
      {
        return Round(seed, 0)[step];
      }
      step -= n - 1;
      return Round(seed, 1 + step / n)[step % n];
    }

    /// <summary>한 바퀴의 순서. 0번째 바퀴는 첫 환자를 뺀 나머지</summary>
    private PatientInfo[] Round(int seed, int round)
    {
      var skip = round == 0 ? 1 : 0;
      var list = new PatientInfo[patients.Length - skip];
      Array.Copy(patients, skip, list, 0, list.Length);
      var random = new System.Random(seed ^ (round * 7919 + 17));
      for (var i = list.Length - 1; i > 0; i--)
      {
        var j = random.Next(i + 1);
        (list[i], list[j]) = (list[j], list[i]);
      }
      if (round > 0 && list.Length > 1)
      {
        var previous = Round(seed, round - 1);
        if (list[0] == previous[previous.Length - 1])
        {
          (list[0], list[1]) = (list[1], list[0]);
        }
      }
      return list;
    }

    /// <summary>실행할 때마다 같은 값이 나오는 문자열 해시 (string.GetHashCode는 실행마다 다를 수 있다)</summary>
    private static int StableHash(string text)
    {
      unchecked
      {
        var hash = (int)2166136261;
        foreach (var ch in text)
        {
          hash = (hash ^ ch) * 16777619;
        }
        return hash;
      }
    }
  }
}
