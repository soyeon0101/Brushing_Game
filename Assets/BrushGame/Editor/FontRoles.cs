using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   글자 역할에 따라 Fonts 폴더의 폰트를 나눠 준다. 굵은 폰트 파일을 쓰므로 가짜 굵기(Bold 스타일)는 끈다.
  ///   로고/큰 숫자: CookieRun Black · 제목/버튼/HUD: CookieRun Bold · 캐릭터 대사: Maplestory Bold
  ///   일반 글: 배민 주아체 · 작은 안내문(32 이하): Pretendard
  ///   파일이 없는 역할은 일반 글 폰트, 그것도 없으면 지금 폰트를 그대로 둔다.
  /// </summary>
  public static class FontRoles
  {
    private const string FontDir = "Assets/BrushGame/Fonts";

    // 캐릭터가 말하는 글자 (오브젝트 이름)
    private static readonly HashSet<string> DialogueNames = new HashSet<string>
    {
      "GuideText", "Line", "Thanks", "PatientLine", "Greeting",
    };

    [MenuItem("BrushGame/Apply Fonts By Role")]
    public static void ApplyAllMenu()
    {
      var count = ApplyAll();
      EditorSceneManager.MarkAllScenesDirty();
      Debug.Log($"[BrushGame] 글자 {count}개에 역할별 폰트를 적용했습니다. 씬을 저장하세요.");
    }

    /// <param name="scope">이 오브젝트 아래만 적용한다 (새로 만든 화면만 꾸밀 때). null이면 열린 씬 전체</param>
    public static int ApplyAll(Transform scope = null)
    {
      var display = Load("CookieRun Black");
      var heading = Load("CookieRun Bold");
      var dialogue = Load("Maplestory Bold");
      var body = Load("BMJUA_ttf");
      var small = Load("PretendardVariable");

      var count = 0;
      foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (scope != null && !text.transform.IsChildOf(scope))
        {
          continue;
        }
        Font font;
        if (text.fontSize >= 90)
        {
          font = display ?? heading;
        }
        else if (DialogueNames.Contains(text.gameObject.name))
        {
          font = dialogue;
        }
        else if (text.fontStyle == FontStyle.Bold || text.GetComponentInParent<Button>(true) != null)
        {
          font = heading;
        }
        else if (text.fontSize <= 32)
        {
          font = small;
        }
        else
        {
          font = body;
        }
        font ??= body;
        if (font == null)
        {
          continue;
        }
        Undo.RecordObject(text, "Apply Fonts By Role");
        text.font = font;
        text.fontStyle = FontStyle.Normal;
        count++;
      }
      return count;
    }

    private static Font Load(string name)
    {
      foreach (var ext in new[] { ".ttf", ".otf" })
      {
        var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}{ext}");
        if (font != null)
        {
          return font;
        }
      }
      return null;
    }
  }
}
