using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   화면 전체를 하나의 톤으로 맞춘다. 여러 번 실행해도 된다.
  ///   원칙: 배경은 분위기만 (옅은 흰 막으로 눌러 준다) · 내용은 반투명 흰 판 위에 · 카드는 테두리 없이 연보라 단색
  ///   · 광택 젤리 버튼은 화면의 주 버튼에만 · 나머지 버튼은 흰 알약/흰 원에 보라 글자 · 제목은 진한 보라(판 위) 또는 흰 글자+보라 테두리(배경 위)
  /// </summary>
  public static class UiSkin
  {
    private const string Dir = "Assets/BrushGame/Art/UI";
    private const string PlaceholderDir = "Assets/BrushGame/Art/Placeholder";

    private static readonly Dictionary<string, string> Backgrounds = new Dictionary<string, string>
    {
      { "AgeSelect", "BG_Soft" }, { "CharacterSelect", "BG_Soft" }, { "Nickname", "BG_Soft" },
      { "Lobby", "BG_Lobby" }, { "PatientBook", "BG_Lobby" }, { "PatientIntro", "BG_Lobby" },
      { "Complete", "BG_Celebrate" }, { "Stickers", "BG_Celebrate" },
    };

    // 제목부터 주 버튼 위까지 흰 판 하나에 담는 화면
    private static readonly HashSet<string> SheetScreens = new HashSet<string> { "AgeSelect", "CharacterSelect", "Nickname" };

    public static readonly Color Ink = new Color(0.29f, 0.23f, 0.55f);
    private static readonly Color InkSoft = new Color(0.48f, 0.42f, 0.66f);
    private static readonly Color SoftCard = new Color(0.95f, 0.93f, 1f);
    private static readonly Color SelectedPink = new Color(1f, 0.55f, 0.78f);
    private static readonly Color PinkOutline = new Color(0.75f, 0.2f, 0.45f);
    private static readonly Color ShadowColor = new Color(0.35f, 0.25f, 0.6f, 0.18f);
    private static readonly Color Red = new Color(0.9f, 0.42f, 0.42f);
    private static readonly Color Lavender = new Color(0.93f, 0.92f, 1f);

    private static Sprite _rounded, _circle, _pink;

    [MenuItem("BrushGame/Apply UI Skin")]
    public static void ApplyAllMenu()
    {
      var count = ApplyAll();
      EditorSceneManager.MarkAllScenesDirty();
      Debug.Log($"[BrushGame] UI 스킨 {count}곳 적용. 씬을 저장하세요.");
    }

    public static int ApplyAll()
    {
      _rounded = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/Rounded.png");
      _circle = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/Circle.png");
      _pink = Load("UI_Button");
      var count = 0;

      foreach (var screen in Object.FindObjectsByType<ScreenBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        var name = screen.gameObject.name;
        if (Backgrounds.TryGetValue(name, out var bgName) && Load(bgName) is { } bg)
        {
          SetBackground(screen.transform, bg);
          count++;
        }
        var content = screen.transform.Find("Content");
        if (content == null)
        {
          continue;
        }
        var onSheet = SheetScreens.Contains(name);
        if (onSheet)
        {
          EnsureSheet(content);
        }
        foreach (var text in content.GetComponentsInChildren<Text>(true))
        {
          if (text.transform.parent != content)
          {
            continue;
          }
          if (text.name == "Title")
          {
            StyleHeading(text, onSheet);
            count++;
          }
          else if (text.name == "Subtitle")
          {
            StyleSubheading(text, onSheet);
          }
        }
      }

      foreach (var card in Object.FindObjectsByType<ChoiceCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        var bg = card.transform.Find("Bg")?.GetComponent<Image>();
        if (bg != null)
        {
          Surface(bg, SoftCard, 50f);
        }
        var selected = card.transform.Find("Selected")?.GetComponent<Image>();
        if (selected != null)
        {
          Surface(selected, SelectedPink, 58f, shadow: false);
        }
        var label = card.transform.Find("Label")?.GetComponent<Text>();
        if (label != null)
        {
          Ink_(label);
        }
        // 그림 카드: 상반신이 카드 폭을 꽉 채우도록 키운다
        var art = card.transform.Find("Art") as RectTransform;
        if (art != null)
        {
          // 카드 높이를 그림 + 이름 만큼으로 줄여 아래 빈 공간을 없앤다
          var cardRt = (RectTransform)card.transform;
          var side = cardRt.sizeDelta.x - 40f;
          Undo.RecordObject(cardRt, "Apply UI Skin");
          cardRt.sizeDelta = new Vector2(cardRt.sizeDelta.x, side + 170f);
          // 흰 판(제목 아래 ~ 주 버튼 위)의 가운데쯤으로
          cardRt.anchoredPosition = new Vector2(cardRt.anchoredPosition.x, -20f);
          foreach (var n in new[] { "Bg", "Selected" })
          {
            if (card.transform.Find(n) is RectTransform part)
            {
              Undo.RecordObject(part, "Apply UI Skin");
              part.sizeDelta = cardRt.sizeDelta + (n == "Selected" ? new Vector2(28f, 28f) : Vector2.zero);
            }
          }
          var cardSize = cardRt.sizeDelta;
          Undo.RecordObject(art, "Apply UI Skin");
          art.sizeDelta = new Vector2(side, side);
          art.anchoredPosition = new Vector2(0f, cardSize.y / 2f - side / 2f - 20f);
          var labelRt = label != null ? label.rectTransform : null;
          if (labelRt != null)
          {
            Undo.RecordObject(labelRt, "Apply UI Skin");
            labelRt.anchoredPosition = new Vector2(0f, art.anchoredPosition.y - side / 2f - 65f);
          }
        }
        count++;
      }

      foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (!(button.targetGraphic is Image image) || button.GetComponent<ChoiceCard>() != null)
        {
          continue;
        }
        var label = button.GetComponentInChildren<Text>(true);
        var h = image.rectTransform.rect.height;
        if (image.sprite == _circle)
        {
          Surface(image, new Color(1f, 1f, 1f, 0.92f), 0f, sprite: _circle, type: Image.Type.Simple);
          image.preserveAspect = true;
          Ink_(label);
          count++;
        }
        else if (image.sprite == _pink || image.sprite == _rounded && !Near(image.color, Red, 0.03f) && !IsLight(image.color))
        {
          // 주 버튼: 광택 젤리
          if (_pink == null)
          {
            continue;
          }
          Record(image);
          image.sprite = _pink;
          image.color = Color.white;
          image.type = Image.Type.Sliced;
          image.pixelsPerUnitMultiplier = _pink.rect.height / h;
          RemoveEffects(image.gameObject);
          if (label != null)
          {
            Record(label);
            label.color = Color.white;
            SetOutline(label, PinkOutline, 3f);
          }
          count++;
        }
        else if (image.sprite == _rounded && IsLight(image.color))
        {
          // 보조 버튼: 흰 알약
          Surface(image, new Color(1f, 1f, 1f, 0.95f), h / 2f);
          Ink_(label);
          count++;
        }
      }

      // 흰 패널/카드 (선택 카드 제외, 치아처럼 작은 것 제외)
      foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (image.name == "Sheet" || image.GetComponent<Button>() != null || image.GetComponentInParent<ChoiceCard>(true) != null)
        {
          continue;
        }
        var size = image.rectTransform.rect.size;
        var candidate = image.sprite == _rounded && (Near(image.color, Color.white, 0.03f) || Near(image.color, Lavender, 0.03f));
        if (!candidate || size.x < 220f || size.y < 120f)
        {
          continue;
        }
        Surface(image, new Color(1f, 1f, 1f, Mathf.Min(0.92f, image.color.a)), Mathf.Min(50f, Mathf.Min(size.x, size.y) / 2.2f));
        count++;
      }
      return count;
    }

    // ---------- 요소별 ----------

    private static void SetBackground(Transform screen, Sprite sprite)
    {
      var art = Child(screen, "BgArt", 0);
      art.sprite = sprite;
      art.color = Color.white;
      var fitter = art.GetComponent<AspectRatioFitter>();
      if (fitter == null)
      {
        fitter = art.gameObject.AddComponent<AspectRatioFitter>();
      }
      fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
      fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

      // 배경이 앞으로 튀어나오지 않게 옅은 흰 막
      var tint = Child(screen, "BgTint", 1);
      tint.sprite = null;
      tint.color = new Color(1f, 1f, 1f, 0.35f);
      Stretch(tint.rectTransform);
    }

    private static void EnsureSheet(Transform content)
    {
      var sheet = Child(content, "Sheet", 0);
      var rt = sheet.rectTransform;
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = new Vector2(0f, 90f);
      rt.sizeDelta = new Vector2(1000f, 1360f);
      Surface(sheet, new Color(1f, 1f, 1f, 0.86f), 70f);
    }

    private static void StyleHeading(Text text, bool onSheet)
    {
      Record(text);
      RemoveEffects(text.gameObject);
      if (onSheet)
      {
        text.color = Ink;
        return;
      }
      text.color = Color.white;
      SetOutline(text, Ink, 5f);
      var shadow = text.gameObject.AddComponent<Shadow>();
      shadow.effectColor = new Color(0.2f, 0.1f, 0.4f, 0.35f);
      shadow.effectDistance = new Vector2(0f, -7f);
    }

    private static void StyleSubheading(Text text, bool onSheet)
    {
      Record(text);
      RemoveEffects(text.gameObject);
      text.color = onSheet ? InkSoft : Color.white;
      if (!onSheet)
      {
        SetOutline(text, Ink, 3f);
      }
    }

    /// <summary>둥근 판: placeholder Rounded 스프라이트를 모서리 반경 radius로, 부드러운 그림자</summary>
    private static void Surface(Image image, Color color, float radius, bool shadow = true, Sprite sprite = null, Image.Type type = Image.Type.Sliced)
    {
      Record(image);
      image.sprite = sprite ?? _rounded;
      image.color = color;
      image.type = type;
      if (type == Image.Type.Sliced && radius > 0f)
      {
        image.pixelsPerUnitMultiplier = _rounded.border.x / radius;
      }
      RemoveEffects(image.gameObject);
      if (shadow)
      {
        var s = image.gameObject.AddComponent<Shadow>();
        s.effectColor = ShadowColor;
        s.effectDistance = new Vector2(0f, -8f);
      }
    }

    private static void Ink_(Text label)
    {
      if (label == null)
      {
        return;
      }
      Record(label);
      label.color = Ink;
      RemoveEffects(label.gameObject);
    }

    // ---------- 도우미 ----------

    private static Image Child(Transform parent, string name, int siblingIndex)
    {
      var t = parent.Find(name);
      Image image;
      if (t == null)
      {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Apply UI Skin");
        go.transform.SetParent(parent, false);
        image = go.GetComponent<Image>();
        image.raycastTarget = false;
      }
      else
      {
        image = t.GetComponent<Image>();
        Record(image);
      }
      image.transform.SetSiblingIndex(siblingIndex);
      return image;
    }

    private static void Stretch(RectTransform rt)
    {
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
    }

    private static void SetOutline(Text text, Color color, float thickness)
    {
      var outline = text.GetComponent<Outline>();
      if (outline == null)
      {
        outline = text.gameObject.AddComponent<Outline>();
      }
      outline.effectColor = color;
      outline.effectDistance = new Vector2(thickness, -thickness);
    }

    /// <summary>Shadow/Outline(Outline은 Shadow를 상속) 전부 제거. 다시 실행할 때 겹치지 않게</summary>
    private static void RemoveEffects(GameObject go)
    {
      foreach (var effect in go.GetComponents<Shadow>())
      {
        Undo.DestroyObjectImmediate(effect);
      }
    }

    private static void Record(Object target)
    {
      Undo.RecordObject(target, "Apply UI Skin");
    }

    private static bool IsLight(Color c) => c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;

    private static bool Near(Color a, Color b, float tolerance)
    {
      return Mathf.Abs(a.r - b.r) < tolerance && Mathf.Abs(a.g - b.g) < tolerance && Mathf.Abs(a.b - b.b) < tolerance;
    }

    private static Sprite Load(string name)
    {
      return AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
    }
  }
}
