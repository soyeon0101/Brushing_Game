using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrushGame
{
  /// <summary>
  ///   도장 기록으로 반짝 배지 조건을 계산한다.
  ///   연속 일수: 도장이 1개 이상 있는 날이 끊기지 않고 이어진 날 수.
  ///   꽉 찬 도장판: 월~일 21칸(아침·점심·저녁)을 모두 채운 주의 누적 수.
  /// </summary>
  public static class BadgeRules
  {
    private const int SlotsPerWeek = 21;

    /// <summary>새로 조건을 채운 배지를 저장 데이터에 넣고 돌려준다 (받은 순서대로)</summary>
    public static List<BadgeInfo> Evaluate(GameCatalog catalog, SaveData data)
    {
      var earned = new List<BadgeInfo>();
      var longest = LongestStreak(data);
      var fullWeeks = FullWeeks(data);
      foreach (var badge in catalog.badges)
      {
        if (data.badgeIds.Contains(badge.id))
        {
          continue;
        }
        var value = badge.kind == BadgeKind.StreakDays ? longest : fullWeeks;
        if (value >= badge.target)
        {
          data.badgeIds.Add(badge.id);
          earned.Add(badge);
        }
      }
      return earned;
    }

    /// <summary>아직 못 받은 배지의 진행도 (배지함 표시용). 연속 일수는 지금 이어지고 있는 날 수</summary>
    public static int Progress(BadgeInfo badge, SaveData data, DateTime today)
    {
      return badge.kind == BadgeKind.StreakDays ? CurrentStreak(data, today) : FullWeeks(data);
    }

    public static DateTime MondayOf(DateTime day)
    {
      day = day.Date;
      return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    public static int LongestStreak(SaveData data)
    {
      var days = BrushedDays(data);
      var best = 0;
      var run = 0;
      var prev = DateTime.MinValue;
      foreach (var day in days)
      {
        run = prev != DateTime.MinValue && (day - prev).Days == 1 ? run + 1 : 1;
        best = Math.Max(best, run);
        prev = day;
      }
      return best;
    }

    /// <summary>오늘(또는 오늘 아직 안 닦았으면 어제)까지 이어진 연속 일수</summary>
    public static int CurrentStreak(SaveData data, DateTime today)
    {
      var days = new HashSet<DateTime>(BrushedDays(data));
      var day = today.Date;
      if (!days.Contains(day))
      {
        day = day.AddDays(-1);
      }
      var count = 0;
      while (days.Contains(day))
      {
        count++;
        day = day.AddDays(-1);
      }
      return count;
    }

    public static int FullWeeks(SaveData data)
    {
      var perWeek = new Dictionary<DateTime, int>();
      foreach (var sticker in data.stickers)
      {
        if (TryParse(sticker.date, out var day))
        {
          var monday = MondayOf(day);
          perWeek.TryGetValue(monday, out var n);
          perWeek[monday] = n + 1;
        }
      }
      var full = 0;
      foreach (var n in perWeek.Values)
      {
        // 같은 날 같은 끼니 도장은 한 장뿐이라 21개면 다 채운 것
        if (n >= SlotsPerWeek)
        {
          full++;
        }
      }
      return full;
    }

    /// <summary>도장이 있는 가장 이른 날 (없으면 null)</summary>
    public static DateTime? FirstDay(SaveData data)
    {
      var days = BrushedDays(data);
      return days.Count > 0 ? days[0] : (DateTime?)null;
    }

    private static List<DateTime> BrushedDays(SaveData data)
    {
      var set = new HashSet<DateTime>();
      foreach (var sticker in data.stickers)
      {
        if (TryParse(sticker.date, out var day))
        {
          set.Add(day);
        }
      }
      var days = new List<DateTime>(set);
      days.Sort();
      return days;
    }

    private static bool TryParse(string date, out DateTime day) =>
      DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
  }
}
