using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BrushGame.HandTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   게임 전체 화면이 들어간 Main 씬을 임시 도형으로 만든다.
  ///   씬이 이미 있으면 덮어쓰지 않는다 (손으로 고친 내용 보호). 다시 만들려면 씬 파일을 지우고 실행.
  ///   그림이 들어갈 자리는 Image 하나씩이며, 그림은 GameCatalog 또는 Image의 Source Image로 넣는다.
  /// </summary>
  public static class MainSceneBuilder
  {
    private const string ScenePath = "Assets/BrushGame/Scenes/Main.unity";
    private const string CatalogPath = "Assets/BrushGame/Data/GameCatalog.asset";
    private const string PlaceholderDir = "Assets/BrushGame/Art/Placeholder";
    private const string CharacterArtDir = "Assets/BrushGame/Art/Characters";
    private const string PatientArtDir = "Assets/BrushGame/Art/Patients";
    private const string BattleArtDir = "Assets/BrushGame/Art/Battle";
    private const string UiArtDir = "Assets/BrushGame/Art/UI";
    private const string FontDir = "Assets/BrushGame/Fonts";
    private const string BootstrapPrefabPath = "Assets/MediaPipeUnity/Samples/Resources/Bootstrap.prefab";

    // 임시 색. 그림을 넣을 때 Image 색을 흰색으로 바꿔야 그림 색이 그대로 나온다 (ArtSlot은 자동으로 함)
    private static readonly Color Primary = new Color(0.36f, 0.31f, 0.8f);
    private static readonly Color Green = new Color(0.1f, 0.45f, 0.36f);
    private static readonly Color Brown = new Color(0.55f, 0.36f, 0.12f);
    private static readonly Color Cream = new Color(0.99f, 0.98f, 0.95f);
    private static readonly Color Lavender = new Color(0.93f, 0.92f, 1f);
    private static readonly Color Dark = new Color(0.2f, 0.18f, 0.28f);
    private static readonly Color Grey = new Color(0.45f, 0.44f, 0.5f);
    private static readonly Color Mint = new Color(0.78f, 0.94f, 0.88f);
    private static readonly Color Peach = new Color(1f, 0.88f, 0.84f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
    private static readonly Color Gum = new Color(0.96f, 0.6f, 0.66f);
    private static readonly Color Red = new Color(0.9f, 0.42f, 0.42f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);

    private static readonly Vector2 Top = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    private static readonly Vector2 BottomLeft = Vector2.zero;

    private static Sprite _circle;
    private static Sprite _rounded;
    private static Font _font;

    [MenuItem("BrushGame/Build Main Scene")]
    public static void Build()
    {
      if (File.Exists(ScenePath))
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 가 이미 있어서 만들지 않았습니다. 다시 만들려면 파일을 지우고 실행하세요.");
        return;
      }

      ApplyPortraitSettings();
      Directory.CreateDirectory(CharacterArtDir);
      Directory.CreateDirectory(PatientArtDir);
      Directory.CreateDirectory(FontDir);
      // 새 씬을 만들 때 쓰지 않는 에셋이 내려가므로, 에셋은 씬을 만든 뒤에 불러온다
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      LoadCommonAssets();
      var catalog = LoadOrCreateCatalog();

      var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
      var cam = camGo.AddComponent<Camera>();
      cam.orthographic = true;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = Color.black;
      camGo.transform.position = new Vector3(0f, 0f, -10f);
      camGo.AddComponent<CameraFitWidth>();

      var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      var canvas = canvasGo.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = cam;
      canvas.planeDistance = 5f;
      var scaler = canvasGo.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1080f, 1920f);
      scaler.matchWidthOrHeight = 0f;
      var root = canvasGo.transform;

      // 카메라 영상은 모든 화면 뒤에 한 장만 두고, 카메라 준비/양치 화면에서만 켠다
      var feed = BuildCameraFeed(root, out var mpScreen);

      var ht = new GameObject("HandTracking");
      var runner = ht.AddComponent<BrushHandLandmarkerRunner>();
      SetRef(runner, "_bootstrapPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath));
      SetRef(runner, "screen", mpScreen);
      SetBool(runner, "_drawLandmarks", false);
      var cameraInput = ht.AddComponent<CameraBrushInput>();
      SetRef(cameraInput, "_runner", runner);
      var keyboard = ht.AddComponent<KeyboardBrushInput>();
      var hub = ht.AddComponent<BrushInputHub>();
      SetRef(hub, "_camera", cameraInput);
      SetRef(hub, "_keyboard", keyboard);
      var overlay = ht.AddComponent<BrushDebugOverlay>();
      SetRef(overlay, "_input", cameraInput);
      overlay.enabled = false;

      var screens = Stretch("Screens", root);
      var title = BuildTitle(screens, catalog);
      var age = BuildAgeSelect(screens);
      var character = BuildCharacterSelect(screens);
      var nickname = BuildNickname(screens);
      var lobby = BuildLobby(screens);
      var book = BuildPatientBook(screens);
      var intro = BuildPatientIntro(screens);
      var ready = BuildCameraReady(screens, hub);
      var loading = BuildLoading(screens);
      var brushing = BuildBrushing(screens, hub, (RectTransform)feed.transform);
      var complete = BuildComplete(screens);
      var stickers = BuildStickers(screens);
      var settings = BuildSettings(root);

      new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

      var flow = new GameObject("Game").AddComponent<GameFlow>();
      SetRef(flow, "_catalog", catalog);
      SetRef(flow, "_cameraFeed", feed);
      SetRef(flow, "_title", title);
      SetRef(flow, "_ageSelect", age);
      SetRef(flow, "_characterSelect", character);
      SetRef(flow, "_nickname", nickname);
      SetRef(flow, "_lobby", lobby);
      SetRef(flow, "_patientBook", book);
      SetRef(flow, "_patientIntro", intro);
      SetRef(flow, "_cameraReady", ready);
      SetRef(flow, "_loading", loading);
      SetRef(flow, "_brushing", brushing);
      SetRef(flow, "_complete", complete);
      SetRef(flow, "_stickers", stickers);
      SetRef(flow, "_settings", settings);

      FontRoles.ApplyAll();
      UiSkin.ApplyAll();
      Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
      EditorSceneManager.SaveScene(scene, ScenePath);
      SetFirstBuildScene(ScenePath);
      Debug.Log($"[BrushGame] Main 씬 생성: {ScenePath}");
    }

    [MenuItem("BrushGame/Link Art To Catalog")]
    public static void LinkArtMenu()
    {
      var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
      if (catalog == null)
      {
        Debug.LogWarning($"[BrushGame] {CatalogPath} 가 없습니다. Build Main Scene을 먼저 실행하세요.");
        return;
      }
      LinkArt(catalog);
    }

    /// <summary>
    ///   열려 있는 Main 씬에서 양치 화면만 새로 만든다. 다른 화면에서 손으로 고친 내용은 그대로 둔다.
    ///   양치 화면 안에서 손으로 고친 내용(넣은 그림 등)은 사라지므로 다시 넣어야 한다.
    /// </summary>
    [MenuItem("BrushGame/Rebuild Brushing Screen")]
    public static void RebuildBrushing()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var old = Object.FindFirstObjectByType<BrushingScreen>(FindObjectsInactive.Include);
      var hub = Object.FindFirstObjectByType<BrushInputHub>(FindObjectsInactive.Include);
      var feed = flow != null ? new SerializedObject(flow).FindProperty("_cameraFeed").objectReferenceValue as GameObject : null;
      if (flow == null || old == null || hub == null || feed == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }

      LoadCommonAssets();
      var parent = old.transform.parent;
      var index = old.transform.GetSiblingIndex();
      var active = old.gameObject.activeSelf;
      Undo.DestroyObjectImmediate(old.gameObject);

      var screen = BuildBrushing(parent, hub, (RectTransform)feed.transform);
      Undo.RegisterCreatedObjectUndo(screen.gameObject, "Rebuild Brushing Screen");
      screen.transform.SetSiblingIndex(index);
      screen.gameObject.SetActive(active);
      SetRef(flow, "_brushing", screen);
      FontRoles.ApplyAll();
      UiSkin.ApplyAll();

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 양치 화면을 다시 만들고 씬을 저장했습니다.");
    }

    /// <summary>열려 있는 Main 씬에서 타이틀 화면만 새로 만든다 (배경/로고/친구들 그림을 다시 읽는다)</summary>
    [MenuItem("BrushGame/Rebuild Title Screen")]
    public static void RebuildTitle()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var old = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
      if (flow == null || old == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      LoadCommonAssets();
      var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
      var parent = old.transform.parent;
      var index = old.transform.GetSiblingIndex();
      var active = old.gameObject.activeSelf;
      Undo.DestroyObjectImmediate(old.gameObject);

      var screen = BuildTitle(parent, catalog);
      Undo.RegisterCreatedObjectUndo(screen.gameObject, "Rebuild Title Screen");
      screen.transform.SetSiblingIndex(index);
      screen.gameObject.SetActive(active);
      SetRef(flow, "_title", screen);
      FontRoles.ApplyAll();
      UiSkin.ApplyAll();

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 타이틀 화면을 다시 만들고 씬을 저장했습니다.");
    }

    [MenuItem("BrushGame/Rebuild Lobby Screen")]
    public static void RebuildLobby() => RebuildScreen("_lobby", BuildLobby);

    [MenuItem("BrushGame/Rebuild Sticker Screen")]
    public static void RebuildStickers() => RebuildScreen("_stickers", BuildStickers);

    /// <summary>열려 있는 Main 씬에서 화면 하나만 새로 만들어 GameFlow에 다시 연결하고 저장한다</summary>
    private static void RebuildScreen<T>(string flowField, Func<Transform, T> build) where T : ScreenBase
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var old = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
      if (flow == null || old == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      LoadCommonAssets();
      var parent = old.transform.parent;
      var index = old.transform.GetSiblingIndex();
      var active = old.gameObject.activeSelf;
      Undo.DestroyObjectImmediate(old.gameObject);

      var screen = build(parent);
      Undo.RegisterCreatedObjectUndo(screen.gameObject, $"Rebuild {typeof(T).Name}");
      screen.transform.SetSiblingIndex(index);
      screen.gameObject.SetActive(active);
      SetRef(flow, flowField, screen);
      FontRoles.ApplyAll();
      UiSkin.ApplyAll();

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log($"[BrushGame] {typeof(T).Name} 화면을 다시 만들고 씬을 저장했습니다.");
    }

    [MenuItem("BrushGame/Delete Save Data")]
    public static void DeleteSaveData()
    {
      SaveStore.ResetAll();
      Debug.Log($"[BrushGame] 저장 데이터를 초기화했습니다: {SaveStore.FilePath}");
    }

    // ---------- 데이터 ----------

    private static void LoadCommonAssets()
    {
      _circle = LoadOrCreateSprite("Circle.png", 256, 128f, Vector4.zero);
      _rounded = LoadOrCreateSprite("Rounded.png", 128, 40f, new Vector4(44, 44, 44, 44));
      _font = LoadFont();
    }

    private static GameCatalog LoadOrCreateCatalog()
    {
      var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
      if (catalog == null)
      {
        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
        catalog = ScriptableObject.CreateInstance<GameCatalog>();
        catalog.characters = new[]
        {
          new PlayerCharacter { id = "princess", displayName = "공주" },
          new PlayerCharacter { id = "prince", displayName = "왕자" },
        };
        catalog.patients = new[]
        {
          new PatientInfo
          {
            id = "zig", displayName = "지그",
            requestLine = "이 사이에 젤리가 끼었어! 도와줘!",
            thanksLine = "고마워! 이제 이가 반짝반짝해!",
          },
          new PatientInfo
          {
            id = "blu", displayName = "블루",
            requestLine = "거품으로 깨끗하게 닦아줄래?",
            thanksLine = "와, 거품 덕분에 반짝반짝!",
          },
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
      }
      LinkArt(catalog);
      return catalog;
    }

    /// <summary>
    ///   정해진 파일 이름의 그림을 카탈로그에 연결한다. 이미 손으로 넣은 칸은 건드리지 않는다.
    ///   캐릭터: Art/Characters/{Id}.png   (Princess.png, Prince.png)
    ///   환자:   Art/Patients/{Id}_Before.png, {Id}_After.png   (Zig_Before.png ...)
    ///   양치 화면: Art/Patients/{Id}_Face.png (입 벌린 얼굴 배경), {Id}_MouthTop.png, {Id}_MouthBottom.png
    /// </summary>
    private static void LinkArt(GameCatalog catalog)
    {
      var linked = 0;
      foreach (var c in catalog.characters)
      {
        linked += Link(ref c.image, CharacterArtDir, Capitalize(c.id));
        linked += Link(ref c.portrait, CharacterArtDir, $"{Capitalize(c.id)}_Bust");
      }
      foreach (var p in catalog.patients)
      {
        linked += Link(ref p.before, PatientArtDir, $"{Capitalize(p.id)}_Before");
        linked += Link(ref p.after, PatientArtDir, $"{Capitalize(p.id)}_After");
        linked += Link(ref p.portrait, PatientArtDir, $"{Capitalize(p.id)}_Bust");
        linked += Link(ref p.face, PatientArtDir, $"{Capitalize(p.id)}_Face");
        linked += Link(ref p.mouthTop, PatientArtDir, $"{Capitalize(p.id)}_MouthTop");
        linked += Link(ref p.mouthBottom, PatientArtDir, $"{Capitalize(p.id)}_MouthBottom");
      }
      EditorUtility.SetDirty(catalog);
      AssetDatabase.SaveAssets();
      Debug.Log($"[BrushGame] 카탈로그에 그림 {linked}개 연결");
    }

    private static int Link(ref Sprite slot, string dir, string baseName)
    {
      if (slot != null)
      {
        return 0;
      }
      foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".psd" })
      {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{baseName}{ext}");
        if (sprite != null)
        {
          slot = sprite;
          return 1;
        }
      }
      return 0;
    }

    private static string Capitalize(string id) => string.IsNullOrEmpty(id) ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);

    private static Font LoadFont()
    {
      if (Directory.Exists(FontDir))
      {
        foreach (var path in Directory.GetFiles(FontDir).OrderBy(p => p))
        {
          var ext = Path.GetExtension(path).ToLowerInvariant();
          if (ext != ".ttf" && ext != ".otf")
          {
            continue;
          }
          var font = AssetDatabase.LoadAssetAtPath<Font>(path.Replace('\\', '/'));
          if (font != null)
          {
            return font;
          }
        }
      }
      return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // ---------- 공통 ----------

    private static GameObject BuildCameraFeed(Transform parent, out Mediapipe.Unity.Screen screen)
    {
      var feed = Img("CameraFeed", parent, Vector2.zero, Vector2.zero, new Color(0.12f, 0.12f, 0.18f), null);
      Fit(feed.rectTransform);
      feed.gameObject.AddComponent<RectMask2D>();
      var raw = Node("Feed", feed.transform, Vector2.zero, new Vector2(1280f, 720f));
      var rawImage = raw.gameObject.AddComponent<RawImage>();
      rawImage.raycastTarget = false;
      screen = raw.gameObject.AddComponent<Mediapipe.Unity.Screen>();
      SetRef(screen, "_screen", rawImage);
      var fitter = feed.gameObject.AddComponent<CameraFeedFitter>();
      SetRef(fitter, "_feed", rawImage);
      return feed.gameObject;
    }

    /// <summary>화면 하나: 배경은 화면 끝까지, 내용은 safe area 안</summary>
    private static (RectTransform root, RectTransform content) NewScreen(string name, Transform parent, Color? background)
    {
      var root = Stretch(name, parent);
      if (background.HasValue)
      {
        root.gameObject.AddComponent<Image>().color = background.Value;
      }
      var content = Stretch("Content", root);
      content.gameObject.AddComponent<SafeAreaFitter>();
      return (root, content);
    }

    private static Button BackButton(Transform content)
    {
      return Btn("BackButton", content, new Vector2(100f, -100f), new Vector2(130f, 130f), "<", Color.white, Dark, 64, TopLeft, _circle);
    }

    // ---------- 첫 실행 ----------

    /// <summary>
    ///   타이틀: 친구들이 모여 있는 일러스트 한 장(Art/UI/TitleBG.png, 위 30%는 로고 자리, 아래 15%는 버튼 자리)이 배경.
    ///   로고는 Art/UI/Logo_Text.png 가 있으면 그림으로, 없으면 폰트 글자로. 시작 버튼은 Art/UI/UI_Button.png 가 있으면 그 그림.
    /// </summary>
    private static TitleScreen BuildTitle(Transform parent, GameCatalog catalog)
    {
      var (root, c) = NewScreen("Title", parent, Lavender);
      var stage = Node("Stage", root, Vector2.zero, new Vector2(1080f, 1920f));
      stage.gameObject.AddComponent<CoverFit>();
      stage.SetAsFirstSibling();
      var bg = Img("Background", stage, Vector2.zero, Vector2.zero, Lavender, null);
      Fit(bg.rectTransform);
      var bgArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/TitleBG.png");
      if (bgArt != null)
      {
        bg.sprite = bgArt;
        bg.color = Color.white;
      }

      // 로고: AI가 한글을 정확히 못 그리므로, 그림은 빈 로고 판(Art/UI/Logo_Plate.png)만 쓰고 글자는 폰트로 얹는다
      var logoRoot = Node("LogoGroup", c, new Vector2(0f, -290f), new Vector2(980f, 540f), Top);
      logoRoot.gameObject.AddComponent<FloatBob>().Configure(8f, 1.4f, 0f);
      var plateArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/Logo_Plate.png");
      if (plateArt != null)
      {
        var plate = Img("Plate", logoRoot, Vector2.zero, new Vector2(980f, 540f), Color.white, null);
        ArtSlot.Apply(plate, plateArt);
      }
      var logo = Txt("Logo", logoRoot, new Vector2(0f, 10f), new Vector2(900f, 360f), "닦아줘!\n<color=#FFB6DA>프렌즈</color>", 138, Color.white, bold: true);
      logo.supportRichText = true;
      logo.lineSpacing = 0.9f;
      Outlined(logo, new Color(0.42f, 0.24f, 0.72f), 7f);
      var start = Btn("StartButton", c, new Vector2(0f, 170f), new Vector2(760f, 190f), "치료 시작하기", Primary, Color.white, 64, Bottom);
      SkinButton(start);

      var screen = root.gameObject.AddComponent<TitleScreen>();
      SetRef(screen, "_startButton", start);
      return screen;
    }

    /// <summary>Art/UI/UI_Button.png(9-slice 테두리가 잡힌 젤리 버튼)가 있으면 버튼에 입힌다. 글자에는 진한 테두리</summary>
    private static void SkinButton(Button button)
    {
      var art = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/UI_Button.png");
      if (art == null)
      {
        return;
      }
      var image = (Image)button.targetGraphic;
      image.sprite = art;
      image.color = Color.white;
      image.type = art.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
      // 그림 높이 전체가 버튼 높이에 맞도록 (둥근 끝이 찌그러지지 않게 가로만 늘어난다)
      image.pixelsPerUnitMultiplier = art.rect.height / image.rectTransform.sizeDelta.y;
      var label = button.GetComponentInChildren<Text>();
      label.rectTransform.anchoredPosition = new Vector2(0f, 6f);
      Outlined(label, new Color(0.75f, 0.2f, 0.45f), 3f);
    }

    private static void Outlined(Text text, Color color, float thickness)
    {
      var outline = text.gameObject.AddComponent<Outline>();
      outline.effectColor = color;
      outline.effectDistance = new Vector2(thickness, -thickness);
      var shadow = text.gameObject.AddComponent<Shadow>();
      shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
      shadow.effectDistance = new Vector2(0f, -thickness * 1.6f);
    }

    private static AgeSelectScreen BuildAgeSelect(Transform parent)
    {
      var (root, c) = NewScreen("AgeSelect", parent, Cream);
      Txt("Title", c, new Vector2(0f, 660f), new Vector2(1000f, 100f), "우리 아이는 몇 살인가요?", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 580f), new Vector2(1000f, 70f), "나이를 골라 주세요", 40, Grey);
      Vector2[] positions = { new Vector2(-210f, 200f), new Vector2(210f, 200f), new Vector2(-210f, -240f), new Vector2(210f, -240f) };
      var cards = new Object[positions.Length];
      for (var i = 0; i < positions.Length; i++)
      {
        cards[i] = Card($"Age{i}", c, positions[i], new Vector2(380f, 400f), $"{4 + i}세", 96, false, Vector2.zero, Vector2.zero, Vector2.zero);
      }
      var next = Btn("NextButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "다음", Primary, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<AgeSelectScreen>();
      SetArray(screen, "_cards", cards);
      SetRef(screen, "_nextButton", next);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    private static CharacterSelectScreen BuildCharacterSelect(Transform parent)
    {
      var (root, c) = NewScreen("CharacterSelect", parent, Cream);
      Txt("Title", c, new Vector2(0f, 660f), new Vector2(1000f, 100f), "나만의 캐릭터를 골라줘!", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 580f), new Vector2(1000f, 70f), "게임 속에서 나를 대신할 친구예요", 40, Grey);
      var cards = new Object[]
      {
        Card("Character0", c, new Vector2(-235f, 40f), new Vector2(430f, 660f), "공주", 56, true, new Vector2(0f, 70f), new Vector2(360f, 460f), new Vector2(0f, -250f)),
        Card("Character1", c, new Vector2(235f, 40f), new Vector2(430f, 660f), "왕자", 56, true, new Vector2(0f, 70f), new Vector2(360f, 460f), new Vector2(0f, -250f)),
      };
      var next = Btn("NextButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "다음", Primary, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<CharacterSelectScreen>();
      SetArray(screen, "_cards", cards);
      SetRef(screen, "_nextButton", next);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    private static NicknameScreen BuildNickname(Transform parent)
    {
      var (root, c) = NewScreen("Nickname", parent, Cream);
      Txt("Title", c, new Vector2(0f, 660f), new Vector2(1000f, 100f), "이름을 알려줘!", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 580f), new Vector2(1000f, 70f), "환자들이 이 이름으로 불러줄 거예요", 40, Grey);
      var art = Img("CharacterArt", c, new Vector2(0f, 230f), new Vector2(420f, 480f), Mint, _circle);
      var input = InputBox("NicknameInput", c, new Vector2(0f, -170f), new Vector2(800f, 150f), $"이름 (최대 {NicknameScreen.MaxLength}자)");
      var done = Btn("DoneButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "완료", Primary, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<NicknameScreen>();
      SetRef(screen, "_input", input);
      SetRef(screen, "_characterImage", art);
      SetRef(screen, "_doneButton", done);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    // ---------- 로비 ----------

    private static LobbyScreen BuildLobby(Transform parent)
    {
      var (root, c) = NewScreen("Lobby", parent, Cream);

      var profile = Img("ProfileCard", c, new Vector2(0f, -190f), new Vector2(980f, 230f), Color.white, _rounded, Top);
      var avatar = Img("Avatar", profile.transform, new Vector2(-370f, 0f), new Vector2(180f, 180f), Mint, _circle);
      var nickname = Txt("Nickname", profile.transform, new Vector2(80f, 25f), new Vector2(620f, 80f), "", 52, Dark, TextAnchor.MiddleLeft, true);
      Txt("Greeting", profile.transform, new Vector2(80f, -45f), new Vector2(620f, 60f), "오늘도 반짝반짝 치료해볼까요?", 32, Grey, TextAnchor.MiddleLeft);

      Txt("TodayLabel", c, new Vector2(0f, 600f), new Vector2(900f, 60f), "오늘의 칭찬 도장", 40, Dark, bold: true);
      string[] slotNames = { "아침", "점심", "저녁" };
      var todayStickers = new Object[3];
      var stampArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/UI_Stamp.png");
      for (var i = 0; i < 3; i++)
      {
        var slot = Img($"Today{i}", c, new Vector2(-250f + 250f * i, 510f), new Vector2(220f, 110f), Lavender, _rounded);
        Txt("Label", slot.transform, new Vector2(-40f, 0f), new Vector2(120f, 80f), slotNames[i], 36, Dark, bold: true);
        var sticker = Stamp(slot.transform, 84f, stampArt);
        sticker.rectTransform.anchoredPosition = new Vector2(60f, 0f);
        sticker.gameObject.SetActive(false);
        todayStickers[i] = sticker.gameObject;
      }

      var card = Img("PatientCard", c, new Vector2(0f, 90f), new Vector2(900f, 640f), Color.white, _rounded);
      Txt("Header", card.transform, new Vector2(0f, 250f), new Vector2(800f, 70f), "오늘의 환자", 44, Primary, bold: true);
      var portrait = Img("PatientArt", card.transform, new Vector2(0f, 40f), new Vector2(340f, 340f), Peach, _circle);
      var patientName = Txt("PatientName", card.transform, new Vector2(0f, -170f), new Vector2(800f, 80f), "", 54, Dark, bold: true);
      var patientLine = Txt("PatientLine", card.transform, new Vector2(0f, -250f), new Vector2(820f, 80f), "", 34, Grey);

      var treat = Btn("TreatButton", c, new Vector2(0f, -380f), new Vector2(800f, 170f), "치료 시작", Green, Color.white, 64);
      var bookButton = Btn("BookButton", c, new Vector2(-330f, -680f), new Vector2(300f, 160f), "환자 도감", Lavender, Dark, 40);
      var stickerButton = Btn("StickerButton", c, new Vector2(0f, -680f), new Vector2(300f, 160f), "스티커판", Lavender, Dark, 40);
      var settingsButton = Btn("SettingsButton", c, new Vector2(330f, -680f), new Vector2(300f, 160f), "보호자 설정", Lavender, Dark, 40);

      var screen = root.gameObject.AddComponent<LobbyScreen>();
      SetRef(screen, "_avatar", avatar);
      SetRef(screen, "_nickname", nickname);
      SetArray(screen, "_todayStickers", todayStickers);
      SetRef(screen, "_patientPortrait", portrait);
      SetRef(screen, "_patientName", patientName);
      SetRef(screen, "_patientLine", patientLine);
      SetRef(screen, "_treatButton", treat);
      SetRef(screen, "_bookButton", bookButton);
      SetRef(screen, "_stickerButton", stickerButton);
      SetRef(screen, "_settingsButton", settingsButton);
      return screen;
    }

    private static PatientBookScreen BuildPatientBook(Transform parent)
    {
      var (root, c) = NewScreen("PatientBook", parent, Cream);
      Txt("Title", c, new Vector2(0f, 760f), new Vector2(1000f, 100f), "환자 도감", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 680f), new Vector2(1000f, 70f), "치료한 친구들이 여기에 모여요", 38, Grey);

      var screen = root.gameObject.AddComponent<PatientBookScreen>();
      var so = new SerializedObject(screen);
      var cards = so.FindProperty("_cards");
      cards.arraySize = 6;
      for (var i = 0; i < 6; i++)
      {
        var pos = new Vector2(i % 2 == 0 ? -240f : 240f, 330f - 450f * (i / 2));
        var card = Img($"Card{i}", c, pos, new Vector2(440f, 420f), Color.white, _rounded);
        var portrait = Img("PatientArt", card.transform, new Vector2(0f, 40f), new Vector2(280f, 280f), Peach, _circle);
        var patientName = Txt("Name", card.transform, new Vector2(0f, -160f), new Vector2(400f, 70f), "", 44, Dark, bold: true);
        var locked = Img("Locked", card.transform, Vector2.zero, new Vector2(440f, 420f), new Color(0.86f, 0.86f, 0.9f, 0.95f), _rounded);
        Txt("Mark", locked.transform, new Vector2(0f, 30f), new Vector2(300f, 200f), "?", 140, Grey, bold: true);

        var e = cards.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("root").objectReferenceValue = card.gameObject;
        e.FindPropertyRelative("portrait").objectReferenceValue = portrait;
        e.FindPropertyRelative("name").objectReferenceValue = patientName;
        e.FindPropertyRelative("locked").objectReferenceValue = locked.gameObject;
      }
      so.FindProperty("_backButton").objectReferenceValue = BackButton(c);
      so.ApplyModifiedPropertiesWithoutUndo();
      return screen;
    }

    // ---------- 치료 ----------

    private static PatientIntroScreen BuildPatientIntro(Transform parent)
    {
      var (root, c) = NewScreen("PatientIntro", parent, Cream);
      Txt("Title", c, new Vector2(0f, 760f), new Vector2(1000f, 100f), "오늘의 환자", 60, Dark, bold: true);
      var portrait = Img("PatientArt", c, new Vector2(0f, 220f), new Vector2(620f, 620f), Peach, _circle);
      var patientName = Txt("PatientName", c, new Vector2(0f, -160f), new Vector2(900f, 90f), "", 64, Primary, bold: true);
      var bubble = Img("Bubble", c, new Vector2(0f, -330f), new Vector2(900f, 200f), Peach, _rounded);
      var line = Txt("Line", bubble.transform, Vector2.zero, new Vector2(840f, 180f), "", 44, Dark, bold: true);
      var go = Btn("GoButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "치료하러 가기", Green, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<PatientIntroScreen>();
      SetRef(screen, "_portrait", portrait);
      SetRef(screen, "_name", patientName);
      SetRef(screen, "_line", line);
      SetRef(screen, "_goButton", go);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    private static CameraReadyScreen BuildCameraReady(Transform parent, BrushInputHub input)
    {
      // 배경 없음: 뒤의 카메라 영상이 보인다
      var (root, c) = NewScreen("CameraReady", parent, null);
      var pill = Img("TitlePill", c, new Vector2(0f, -150f), new Vector2(860f, 110f), Dim, _rounded, Top);
      Txt("Title", pill.transform, Vector2.zero, new Vector2(860f, 110f), "카메라를 맞춰볼까?", 48, Color.white, bold: true);
      Img("GuideFrame", c, new Vector2(0f, 60f), new Vector2(760f, 1000f), new Color(1f, 1f, 1f, 0.2f), _circle);

      var guidePill = Img("GuidePill", c, new Vector2(0f, -540f), new Vector2(940f, 170f), Dim, _rounded);
      var guide = Txt("Guide", guidePill.transform, new Vector2(0f, 25f), new Vector2(900f, 80f), "", 48, Color.white, bold: true);
      Txt("Hint", guidePill.transform, new Vector2(0f, -45f), new Vector2(900f, 60f), "얼굴과 손이 다 보이게 조금 뒤로 가볼까?", 30, Color.white);

      var countdownRoot = Img("Countdown", c, new Vector2(0f, -780f), new Vector2(220f, 220f), Green, _circle);
      var countdown = Txt("Number", countdownRoot.transform, Vector2.zero, new Vector2(220f, 220f), "3", 120, Color.white, bold: true);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<CameraReadyScreen>();
      SetRef(screen, "_input", input);
      SetRef(screen, "_guide", guide);
      SetRef(screen, "_countdownRoot", countdownRoot.gameObject);
      SetRef(screen, "_countdown", countdown);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    private static LoadingScreen BuildLoading(Transform parent)
    {
      var (root, c) = NewScreen("Loading", parent, new Color(0.05f, 0.2f, 0.16f));
      Txt("Message", c, new Vector2(0f, 80f), new Vector2(1000f, 100f), "우주 치과 입장 중...", 56, Color.white, bold: true);
      var bar = Img("Bar", c, new Vector2(0f, -40f), new Vector2(700f, 44f), new Color(1f, 1f, 1f, 0.25f), _rounded, ppu: 3f);
      var fill = Img("Fill", bar.transform, Vector2.zero, Vector2.zero, Gold, _rounded, ppu: 3f);
      var fillRt = fill.rectTransform;
      fillRt.anchorMin = Vector2.zero;
      fillRt.anchorMax = new Vector2(0f, 1f);
      fillRt.offsetMin = Vector2.zero;
      fillRt.offsetMax = Vector2.zero;

      var screen = root.gameObject.AddComponent<LoadingScreen>();
      SetRef(screen, "_barFill", fillRt);
      return screen;
    }

    private static BrushingScreen BuildBrushing(Transform parent, BrushInputHub input, RectTransform cameraFeed)
    {
      var (root, c) = NewScreen("Brushing", parent, null);

      // 배경은 환자 얼굴(입 벌린 모습), 그 위에 환자 입(위아래 잇몸 + 이빨)이 HUD처럼 얹힌다.
      // 얼굴과 입을 rig 하나에 담아 구역마다 함께 확대/이동한다 (MouthZoom)
      var rig = Stretch("MouthRig", root);
      // 얼굴 무대: 얼굴 그림(1080x1920) 좌표 그대로 잇몸과 이빨을 입 위치에 놓고, 판 전체를 화면에 꽉 차게 키운다
      var stage = Node("FaceStage", rig, Vector2.zero, new Vector2(1080f, 1920f));
      stage.gameObject.AddComponent<CoverFit>();
      var face = Img("PatientFace", stage, Vector2.zero, Vector2.zero, new Color(0.36f, 0.13f, 0.22f), null);
      Fit(face.rectTransform);
      var facePlaceholder = Txt("Placeholder", face.transform, new Vector2(0f, 500f), new Vector2(900f, 200f), "환자 얼굴 배경 자리\n(입을 벌린 얼굴 그림)", 44, new Color(1f, 1f, 1f, 0.35f), bold: true);
      // 잇몸 그림은 투명 여백 포함 통째로 들어간다. 위: 잇몸 띠가 그림 위에서 175~330px, 아래: 220~340px
      var upper = MouthPart("MouthTop", stage, new Vector2(0f, 127f), new Vector2(1080f, 640f), new Vector2(0f, 67f), new Vector2(1040f, 160f), out var upperPlaceholder);
      var lower = MouthPart("MouthBottom", stage, new Vector2(0f, -739f), new Vector2(1080f, 560f), Vector2.zero, new Vector2(1040f, 130f), out var lowerPlaceholder);
      // HUD(Content)보다 뒤에 그려지게
      rig.SetAsFirstSibling();
      var zoom = rig.gameObject.AddComponent<MouthZoom>();
      SetRef(zoom, "_rig", rig);

      // 이빨은 위 잇몸 아래로 매달리고, 아래 잇몸 위로 솟는다
      var center = new Vector2(0.5f, 0.5f);
      var zones = new Object[]
      {
        Zone("ZoneTopLeft", upper.transform, new Vector2(-265f, -115f), center, true),
        Zone("ZoneTopRight", upper.transform, new Vector2(265f, -115f), center, true),
        Zone("ZoneBottomLeft", lower.transform, new Vector2(-265f, 155f), center, false),
        Zone("ZoneBottomRight", lower.transform, new Vector2(265f, 155f), center, false),
      };

      var battle = BuildBattle(root);
      // 입 앞, HUD 뒤
      battle.transform.SetSiblingIndex(1);

      // HUD
      var pause = Btn("PauseButton", c, new Vector2(100f, -100f), new Vector2(130f, 130f), "II", Color.white, Dark, 56, TopLeft, _circle);
      var sound = Btn("SoundButton", c, new Vector2(-100f, -100f), new Vector2(130f, 130f), "소리\n켬", Color.white, Dark, 30, TopRight, _circle);
      var timerPill = Img("TimerPill", c, new Vector2(0f, -90f), new Vector2(300f, 110f), Primary, _rounded, Top);
      var timer = Txt("Timer", timerPill.transform, Vector2.zero, new Vector2(300f, 110f), "0:25", 60, Color.white, bold: true);
      var dotsRoot = Node("ZoneDots", c, new Vector2(0f, -185f), new Vector2(300f, 40f), Top);
      var dots = new Object[4];
      var dotFills = new Object[4];
      for (var i = 0; i < 4; i++)
      {
        var dot = Img($"Dot{i}", dotsRoot, new Vector2(-90f + 60f * i, 0f), new Vector2(36f, 36f), new Color(1f, 1f, 1f, 0.7f), _circle);
        var fill = Img("Fill", dot.transform, Vector2.zero, new Vector2(36f, 36f), Gold, _circle);
        fill.gameObject.SetActive(false);
        dots[i] = dot.rectTransform;
        dotFills[i] = fill.gameObject;
      }
      var countPill = Img("ZoneCountPill", c, new Vector2(130f, 120f), new Vector2(200f, 100f), Primary, _rounded, BottomLeft);
      var zoneCount = Txt("ZoneCount", countPill.transform, Vector2.zero, new Vector2(200f, 100f), "1/4", 52, Color.white, bold: true);

      // 카메라 작은 창 (오른쪽 위, 소리 버튼 아래): 아이가 자기 손이 잘 찍히는지 보는 용도. 확대되지 않는다
      var camFrame = Img("CameraWindowFrame", c, new Vector2(-150f, -390f), new Vector2(232f, 302f), Color.white, _rounded, TopRight, ppu: 2f);
      var camWindow = Node("CameraWindow", camFrame.transform, Vector2.zero, new Vector2(220f, 290f));
      var camLabel = Img("LabelPill", camFrame.transform, new Vector2(0f, 178f), new Vector2(170f, 50f), Dark, _rounded, ppu: 2f);
      Txt("Label", camLabel.transform, Vector2.zero, new Vector2(170f, 50f), "내 칫솔", 30, Color.white, bold: true);

      // 안내 말풍선 (HUD 바로 아래. 확대한 구역과 전투 무대를 가리지 않게)
      // 오른쪽은 카메라 작은 창 자리라 왼쪽으로 붙인다
      var bubble = Img("GuideBubble", c, new Vector2(-120f, -310f), new Vector2(800f, 150f), new Color(1f, 1f, 1f, 0.95f), _rounded, Top);
      var guidePortrait = Img("GuidePortrait", bubble.transform, new Vector2(-320f, 0f), new Vector2(120f, 120f), Peach, _circle);
      var guideText = Txt("GuideText", bubble.transform, new Vector2(70f, 0f), new Vector2(600f, 130f), "", 40, Dark, TextAnchor.MiddleLeft, true);

      // 손 인식 끊김: 화면을 막지 않는 안내만
      var lost = Img("HandLostOverlay", root, Vector2.zero, Vector2.zero, Dim, null);
      Fit(lost.rectTransform);
      var lostBox = Img("Box", lost.transform, new Vector2(0f, 60f), new Vector2(860f, 260f), Color.white, _rounded);
      Txt("Title", lostBox.transform, new Vector2(0f, 40f), new Vector2(800f, 90f), "칫솔이 안 보여!", 64, Red, bold: true);
      Txt("Sub", lostBox.transform, new Vector2(0f, -50f), new Vector2(800f, 70f), "손이 화면에 보이게 해줘", 40, Dark);
      lost.gameObject.SetActive(false);

      var pauseOverlay = Img("PauseOverlay", root, Vector2.zero, Vector2.zero, Dim, null);
      pauseOverlay.raycastTarget = true;
      Fit(pauseOverlay.rectTransform);
      var panel = Img("Panel", pauseOverlay.transform, Vector2.zero, new Vector2(820f, 640f), Color.white, _rounded);
      Txt("Title", panel.transform, new Vector2(0f, 200f), new Vector2(760f, 100f), "잠깐 쉬는 중", 60, Dark, bold: true);
      var resume = Btn("ResumeButton", panel.transform, new Vector2(0f, 20f), new Vector2(620f, 150f), "계속하기", Green, Color.white, 56);
      var quit = Btn("QuitButton", panel.transform, new Vector2(0f, -160f), new Vector2(620f, 130f), "그만하기", Lavender, Dark, 48);
      Txt("Note", panel.transform, new Vector2(0f, -265f), new Vector2(760f, 60f), "그만하면 이번 치료는 기록되지 않아요", 30, Grey);
      pauseOverlay.gameObject.SetActive(false);

      var screen = root.gameObject.AddComponent<BrushingScreen>();
      SetRef(screen, "_input", input);
      SetArray(screen, "_zones", zones);
      SetRef(screen, "_zoom", zoom);
      SetRef(screen, "_battle", battle);
      SetRef(screen, "_face", face);
      SetRef(screen, "_facePlaceholder", facePlaceholder.gameObject);
      SetRef(screen, "_mouthTop", upper);
      SetRef(screen, "_mouthBottom", lower);
      SetRef(screen, "_mouthTopPlaceholder", upperPlaceholder);
      SetRef(screen, "_mouthBottomPlaceholder", lowerPlaceholder);
      SetRef(screen, "_cameraFeed", cameraFeed);
      SetRef(screen, "_cameraWindow", camWindow);
      SetRef(screen, "_timer", timer);
      SetRef(screen, "_zoneCount", zoneCount);
      SetArray(screen, "_zoneDots", dots);
      SetArray(screen, "_zoneDotFills", dotFills);
      SetRef(screen, "_pauseButton", pause);
      SetRef(screen, "_soundButton", sound);
      SetRef(screen, "_soundLabel", sound.GetComponentInChildren<Text>());
      SetRef(screen, "_guidePortrait", guidePortrait);
      SetRef(screen, "_guideText", guideText);
      SetRef(screen, "_handLostOverlay", lost.gameObject);
      SetRef(screen, "_pauseOverlay", pauseOverlay.gameObject);
      SetRef(screen, "_resumeButton", resume);
      SetRef(screen, "_quitButton", quit);
      return screen;
    }

    /// <summary>
    ///   동작별 프레임({name}_{동작}1.png, 2.png ...)을 이름 순서대로 읽는다. 순서: 대기, 행동, 멈춤, 끝.
    ///   프레임이 있으면 캔버스 크기에 맞춰 자리를 키우고 첫 대기 프레임을 넣는다. 없으면 {name}.png 한 장 또는 임시 도형.
    /// </summary>
    private static Sprite[][] BattleArt(Image image, string name, string[] actions, string label, Color labelColor)
    {
      var frames = actions.Select(a => AssetDatabase.FindAssets($"{name}_{a} t:Sprite", new[] { BattleArtDir })
          .Select(AssetDatabase.GUIDToAssetPath)
          .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith($"{name}_{a}"))
          .OrderBy(p => p)
          .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
          .ToArray())
        .ToArray();

      var first = frames[0].FirstOrDefault() ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{BattleArtDir}/{name}.png");
      if (first == null)
      {
        Txt("Label", image.transform, Vector2.zero, new Vector2(260f, 80f), label, 44, labelColor, bold: true);
        return frames;
      }
      ArtSlot.Apply(image, first);
      if (frames[0].Length > 0)
      {
        // 프레임 캔버스는 여백이 넓어서 자리를 키운다 (발은 자리 아래쪽)
        image.rectTransform.sizeDelta = new Vector2(580f, 580f);
        image.rectTransform.anchoredPosition += new Vector2(0f, 100f);
        // 기울이거나 찌그러뜨릴 때 발이 미끄러지지 않게 회전 중심을 발(아래)로. 화면 위치는 그대로
        const float pivotY = 0.03f;
        image.rectTransform.pivot = new Vector2(0.5f, pivotY);
        image.rectTransform.anchoredPosition -= new Vector2(0f, 580f * (0.5f - pivotY));
      }
      return frames;
    }

    private static void SetFrames(Object target, string property, Sprite[][] frames)
    {
      var so = new SerializedObject(target);
      string[] fields = { "idle", "action", "stopped", "finish" };
      for (var i = 0; i < fields.Length; i++)
      {
        var prop = so.FindProperty($"{property}.{fields[i]}");
        prop.arraySize = frames[i].Length;
        for (var k = 0; k < frames[i].Length; k++)
        {
          prop.GetArrayElementAtIndex(k).objectReferenceValue = frames[i][k];
        }
      }
      // 영상에서 뽑은 대기 동작(프레임이 많음)은 영상 속도(16fps의 절반)로, 그림 몇 장이면 천천히
      so.FindProperty($"{property}.loopFps").floatValue = frames[0].Length > 4 ? 8f : 4f;
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    ///   잇몸 그림 자리 하나. 그림이 없으면 투명하고 대신 임시 잇몸 띠가 보인다.
    ///   입 크기에 맞게 이빨과 함께 조금 줄여 둔다.
    /// </summary>
    private static Image MouthPart(string name, Transform parent, Vector2 pos, Vector2 size, Vector2 bandPos, Vector2 bandSize, out GameObject placeholder)
    {
      var part = Img(name, parent, pos, size, new Color(1f, 1f, 1f, 0f), null);
      part.rectTransform.localScale = Vector3.one * 0.85f;
      placeholder = Img("GumPlaceholder", part.transform, bandPos, bandSize, Gum, _rounded, ppu: 0.8f).gameObject;
      return part;
    }

    /// <summary>
    ///   전투 무대: 친구(왼쪽) vs 충치균(오른쪽) + 때린 효과 + 충치균 체력바. 모두 따로 교체할 수 있는 Image.
    ///   무대 위치는 BrushingScreen이 구역마다 정한다.
    /// </summary>
    private static BattleView BuildBattle(Transform parent)
    {
      var stage = Node("Battle", parent, Vector2.zero, new Vector2(900f, 400f));
      var group = stage.gameObject.AddComponent<CanvasGroup>();
      group.blocksRaycasts = false;
      group.interactable = false;

      // Art/Battle/Hero_Idle1.png ... 프레임(1024 캔버스, 발이 아래 가운데)이 있으면 그 그림을, 없으면 임시 도형 + 이름표
      var hero = Img("Hero", stage, new Vector2(-230f, 0f), new Vector2(300f, 300f), Mint, _circle);
      var heroFrames = BattleArt(hero, "Hero", new[] { "Idle", "Attack", "Hurt", "Win" }, "친구", Dark);

      var villain = Node("Villain", stage, new Vector2(230f, 0f), new Vector2(300f, 300f));
      var villainGroup = villain.gameObject.AddComponent<CanvasGroup>();
      var villainArt = Img("VillainArt", villain, Vector2.zero, new Vector2(300f, 300f), new Color(0.45f, 0.3f, 0.65f), _circle);
      var villainFrames = BattleArt(villainArt, "Villain", new[] { "Idle", "Hit", "Laugh", "Defeat" }, "충치균", Color.white);

      // 칫솔 끝에서 비눗방울과 반짝이가 퐁 터지는 효과 (때리는 표현 없이)
      var hit = Node("HitEffect", stage, new Vector2(150f, 30f), new Vector2(260f, 260f));
      var hitGroup = hit.gameObject.AddComponent<CanvasGroup>();
      hitGroup.alpha = 0f;
      var burst = Img("Burst", hit, Vector2.zero, new Vector2(260f, 260f), new Color(1f, 1f, 1f, 0.7f), _circle);
      var burstArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/FX_Sparkle.png");
      if (burstArt != null)
      {
        ArtSlot.Apply(burst, burstArt);
      }

      var hpBar = Img("HpBar", stage, new Vector2(230f, -175f), new Vector2(280f, 44f), new Color(0f, 0f, 0f, 0.45f), _rounded, ppu: 2.5f);
      var hpFill = Img("Fill", hpBar.transform, Vector2.zero, Vector2.zero, Red, _rounded, ppu: 3f);
      hpFill.rectTransform.anchorMin = Vector2.zero;
      hpFill.rectTransform.anchorMax = Vector2.one;
      hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
      hpFill.rectTransform.offsetMin = new Vector2(6f, 6f);
      hpFill.rectTransform.offsetMax = new Vector2(-6f, -6f);

      var view = stage.gameObject.AddComponent<BattleView>();
      SetRef(view, "_group", group);
      SetRef(view, "_hero", hero.rectTransform);
      SetRef(view, "_heroImage", hero);
      SetFrames(view, "_heroFrames", heroFrames);
      SetRef(view, "_villain", villain);
      SetRef(view, "_villainImage", villainArt);
      SetFrames(view, "_villainFrames", villainFrames);
      SetRef(view, "_villainGroup", villainGroup);
      SetRef(view, "_hitEffect", hit);
      SetRef(view, "_hitGroup", hitGroup);
      SetRef(view, "_hpBar", hpBar.rectTransform);
      SetRef(view, "_hpFill", hpFill.rectTransform);
      return view;
    }

    /// <summary>구역 하나: 치아 3개 + 얼룩 3개 + 거품 3개 + 반짝임 2개. 모두 따로 교체할 수 있는 Image</summary>
    private static BrushZoneView Zone(string name, Transform parent, Vector2 pos, Vector2 anchor, bool upper)
    {
      var root = Node(name, parent, pos, new Vector2(480f, 240f), anchor);
      var highlight = Img("Highlight", root, Vector2.zero, new Vector2(510f, 270f), new Color(1f, 0.92f, 0.35f, 0.85f), _rounded);
      float[] xs = { -160f, 0f, 160f };
      foreach (var x in xs)
      {
        Img("Tooth", root, new Vector2(x, 0f), new Vector2(140f, 210f), Color.white, _rounded);
      }
      Color[] dirtColors = { new Color(1f, 0.85f, 0.35f), new Color(0.7f, 0.88f, 0.4f), new Color(0.8f, 0.62f, 1f) };
      var gumSide = upper ? 1f : -1f;
      var dirt = new Object[xs.Length];
      var foam = new Object[xs.Length];
      for (var i = 0; i < xs.Length; i++)
      {
        dirt[i] = Img("Dirt", root, new Vector2(xs[i] + (i - 1) * 8f, gumSide * 50f), new Vector2(96f, 64f), dirtColors[i], _circle);
      }
      for (var i = 0; i < xs.Length; i++)
      {
        foam[i] = Img("Foam", root, new Vector2(xs[i], -gumSide * 10f), new Vector2(140f, 140f), new Color(1f, 1f, 1f, 0f), _circle);
      }
      var sparkles = new Object[]
      {
        Img("Sparkle", root, new Vector2(-90f, 60f), new Vector2(64f, 64f), new Color(1f, 0.95f, 0.5f, 0f), _circle),
        Img("Sparkle", root, new Vector2(110f, -50f), new Vector2(64f, 64f), new Color(1f, 0.95f, 0.5f, 0f), _circle),
      };

      var view = root.gameObject.AddComponent<BrushZoneView>();
      SetRef(view, "_highlight", highlight.gameObject);
      SetArray(view, "_dirt", dirt);
      SetArray(view, "_foam", foam);
      SetArray(view, "_sparkles", sparkles);
      highlight.gameObject.SetActive(false);
      return view;
    }

    private static CompleteScreen BuildComplete(Transform parent)
    {
      var (root, c) = NewScreen("Complete", parent, Cream);
      Txt("Title", c, new Vector2(0f, 720f), new Vector2(1000f, 130f), "치료 완료!", 90, Primary, bold: true);
      var before = Img("BeforeArt", c, new Vector2(-250f, 260f), new Vector2(380f, 380f), Peach, _circle);
      var after = Img("AfterArt", c, new Vector2(250f, 260f), new Vector2(380f, 380f), Mint, _circle);
      Txt("Arrow", c, new Vector2(0f, 260f), new Vector2(100f, 100f), "→", 80, Grey, bold: true);
      Txt("BeforeLabel", c, new Vector2(-250f, 30f), new Vector2(400f, 60f), "치료 전", 40, Grey);
      Txt("AfterLabel", c, new Vector2(250f, 30f), new Vector2(400f, 60f), "치료 후", 40, Grey);
      var bubble = Img("Bubble", c, new Vector2(0f, -230f), new Vector2(900f, 200f), Lavender, _rounded);
      var thanks = Txt("Thanks", bubble.transform, Vector2.zero, new Vector2(840f, 180f), "", 46, Dark, bold: true);
      var next = Btn("NextButton", c, new Vector2(0f, -700f), new Vector2(760f, 160f), "스티커 받기", Green, Color.white, 56);

      var screen = root.gameObject.AddComponent<CompleteScreen>();
      SetRef(screen, "_before", before);
      SetRef(screen, "_after", after);
      SetRef(screen, "_thanks", thanks);
      SetRef(screen, "_nextButton", next);
      return screen;
    }

    private static StickerScreen BuildStickers(Transform parent)
    {
      var (root, c) = NewScreen("Stickers", parent, Cream);
      var title = Txt("Title", c, new Vector2(0f, 780f), new Vector2(1000f, 100f), "칭찬 스티커판", 64, Dark, bold: true);
      var summary = Txt("Summary", c, new Vector2(0f, 700f), new Vector2(1000f, 70f), "", 40, Grey);
      var board = Img("Board", c, Vector2.zero, new Vector2(980f, 1180f), Color.white, _rounded);
      var boardArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/UI_StickerBoard.png");
      if (boardArt != null)
      {
        // 장식 테두리가 늘어나지 않게 9-slice
        board.sprite = boardArt;
        board.type = boardArt.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        board.pixelsPerUnitMultiplier = 1.4f;
        board.name = "StickerBoard";
      }
      var stampArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/UI_Stamp.png");

      string[] slotNames = { "아침", "점심", "저녁" };
      float[] columns = { -105f, 70f, 245f };
      for (var s = 0; s < 3; s++)
      {
        Txt($"Slot{s}", board.transform, new Vector2(columns[s], 395f), new Vector2(170f, 70f), slotNames[s], 40, Dark, bold: true);
      }
      string[] dayNames = { "월", "화", "수", "목", "금", "토", "일" };
      var stickers = new Object[21];
      for (var d = 0; d < 7; d++)
      {
        var y = 290f - 108f * d;
        Txt($"Day{d}", board.transform, new Vector2(-265f, y), new Vector2(90f, 90f), dayNames[d], 44, Dark, bold: true);
        for (var s = 0; s < 3; s++)
        {
          var cell = Img($"Cell{d}_{s}", board.transform, new Vector2(columns[s], y), new Vector2(160f, 92f), new Color(0.93f, 0.9f, 1f, 0.75f), _rounded);
          var sticker = Stamp(cell.transform, 100f, stampArt);
          sticker.gameObject.SetActive(false);
          stickers[d * 3 + s] = sticker.gameObject;
        }
      }
      // 도장이 찍힐 때 퍼지는 반짝임 (칸들보다 앞에)
      var fx = Img("StampFx", board.transform, Vector2.zero, new Vector2(320f, 320f), new Color(1f, 0.95f, 0.6f, 0.8f), _circle);
      var fxArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtDir}/FX_Sparkle.png");
      if (fxArt != null)
      {
        ArtSlot.Apply(fx, fxArt);
      }
      fx.gameObject.SetActive(false);
      var back = Btn("BackButton", c, new Vector2(0f, -790f), new Vector2(800f, 160f), "로비로 돌아가기", Primary, Color.white, 56);

      var screen = root.gameObject.AddComponent<StickerScreen>();
      SetRef(screen, "_title", title);
      SetRef(screen, "_summary", summary);
      SetArray(screen, "_stickers", stickers);
      SetRef(screen, "_backButton", back);
      SetRef(screen, "_stampFx", fx.rectTransform);
      return screen;
    }

    /// <summary>칭찬 도장 한 개. Art/UI/UI_Stamp.png 가 없으면 노란 원</summary>
    private static Image Stamp(Transform parent, float size, Sprite art)
    {
      var stamp = Img("Sticker", parent, Vector2.zero, new Vector2(size, size), Gold, _circle);
      if (art != null)
      {
        ArtSlot.Apply(stamp, art);
      }
      return stamp;
    }

    private static GuardianSettingsPanel BuildSettings(Transform parent)
    {
      var root = Stretch("GuardianSettings", parent);
      var dim = root.gameObject.AddComponent<Image>();
      dim.color = Dim;
      var panel = Img("Panel", root, Vector2.zero, new Vector2(920f, 1260f), Color.white, _rounded);
      var p = panel.transform;
      Txt("Title", p, new Vector2(0f, 540f), new Vector2(860f, 100f), "보호자 설정", 60, Dark, bold: true);

      Txt("SoundLabel", p, new Vector2(-180f, 380f), new Vector2(460f, 100f), "사운드", 46, Dark, TextAnchor.MiddleLeft, true);
      var sound = Btn("SoundButton", p, new Vector2(250f, 380f), new Vector2(280f, 110f), "켜짐", Primary, Color.white, 44);
      Txt("VoiceLabel", p, new Vector2(-180f, 230f), new Vector2(460f, 110f), "안내 음성\n(준비 중)", 40, Dark, TextAnchor.MiddleLeft, true);
      var voice = Btn("VoiceButton", p, new Vector2(250f, 230f), new Vector2(280f, 110f), "켜짐", Primary, Color.white, 44);

      Txt("ZoneLabel", p, new Vector2(0f, 90f), new Vector2(860f, 80f), "구역별 양치 시간", 46, Dark, bold: true);
      var zoneCards = new Object[3];
      for (var i = 0; i < 3; i++)
      {
        zoneCards[i] = Card($"Zone{i}", p, new Vector2(-270f + 270f * i, -40f), new Vector2(240f, 130f), $"{20 + 5 * i}초", 48, false, Vector2.zero, Vector2.zero, Vector2.zero, Lavender);
      }

      var reset = Btn("ResetButton", p, new Vector2(0f, -260f), new Vector2(640f, 130f), "프로필 초기화", Red, Color.white, 44);
      Txt("ResetNote", p, new Vector2(0f, -360f), new Vector2(860f, 60f), "처음 실행 상태로 돌아가요 (기록 삭제)", 30, Grey);
      var close = Btn("CloseButton", p, new Vector2(0f, -500f), new Vector2(640f, 140f), "닫기", Primary, Color.white, 52);

      var confirm = Stretch("ConfirmReset", root);
      confirm.gameObject.AddComponent<Image>().color = Dim;
      var box = Img("Box", confirm, Vector2.zero, new Vector2(820f, 520f), Color.white, _rounded);
      Txt("Message", box.transform, new Vector2(0f, 100f), new Vector2(760f, 200f), "처음부터 다시 시작할까요?\n기록이 모두 지워져요", 46, Dark, bold: true);
      var cancel = Btn("CancelButton", box.transform, new Vector2(-190f, -140f), new Vector2(330f, 130f), "취소", Lavender, Dark, 48);
      var confirmReset = Btn("ConfirmButton", box.transform, new Vector2(190f, -140f), new Vector2(330f, 130f), "초기화", Red, Color.white, 48);
      confirm.gameObject.SetActive(false);

      var panelComp = root.gameObject.AddComponent<GuardianSettingsPanel>();
      SetRef(panelComp, "_soundButton", sound);
      SetRef(panelComp, "_soundLabel", sound.GetComponentInChildren<Text>());
      SetRef(panelComp, "_voiceButton", voice);
      SetRef(panelComp, "_voiceLabel", voice.GetComponentInChildren<Text>());
      SetArray(panelComp, "_zoneSecondCards", zoneCards);
      SetRef(panelComp, "_resetButton", reset);
      SetRef(panelComp, "_closeButton", close);
      SetRef(panelComp, "_confirmPanel", confirm.gameObject);
      SetRef(panelComp, "_confirmResetButton", confirmReset);
      SetRef(panelComp, "_cancelResetButton", cancel);
      return panelComp;
    }

    // ---------- UI helpers ----------

    private static RectTransform Node(string name, Transform parent, Vector2 pos, Vector2 size, Vector2? anchor = null)
    {
      var go = new GameObject(name, typeof(RectTransform));
      var rt = (RectTransform)go.transform;
      rt.SetParent(parent, false);
      rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return rt;
    }

    private static RectTransform Stretch(string name, Transform parent)
    {
      var rt = Node(name, parent, Vector2.zero, Vector2.zero);
      Fit(rt);
      return rt;
    }

    private static void Fit(RectTransform rt)
    {
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
    }

    /// <summary>화면 위(또는 아래) 가장자리에 붙여 가로로 꽉 채운다. sizeDelta.y가 높이</summary>
    private static void AnchorEdge(RectTransform rt, bool top)
    {
      var y = top ? 1f : 0f;
      var height = rt.sizeDelta.y;
      rt.anchorMin = new Vector2(0f, y);
      rt.anchorMax = new Vector2(1f, y);
      rt.pivot = new Vector2(0.5f, y);
      rt.anchoredPosition = Vector2.zero;
      rt.sizeDelta = new Vector2(0f, height);
    }

    private static Image Img(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Sprite sprite, Vector2? anchor = null, float ppu = 1f)
    {
      var rt = Node(name, parent, pos, size, anchor);
      var img = rt.gameObject.AddComponent<Image>();
      img.sprite = sprite;
      img.color = color;
      img.raycastTarget = false;
      if (sprite == _rounded)
      {
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = ppu;
      }
      return img;
    }

    private static Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, string value, int fontSize, Color color,
      TextAnchor align = TextAnchor.MiddleCenter, bool bold = false, Vector2? anchor = null)
    {
      var rt = Node(name, parent, pos, size, anchor);
      var t = rt.gameObject.AddComponent<Text>();
      t.font = _font;
      t.text = value;
      t.fontSize = fontSize;
      t.color = color;
      t.alignment = align;
      t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
      t.raycastTarget = false;
      t.horizontalOverflow = HorizontalWrapMode.Wrap;
      t.verticalOverflow = VerticalWrapMode.Overflow;
      return t;
    }

    private static Button Btn(string name, Transform parent, Vector2 pos, Vector2 size, string label, Color background, Color textColor, int fontSize,
      Vector2? anchor = null, Sprite sprite = null)
    {
      var img = Img(name, parent, pos, size, background, sprite != null ? sprite : _rounded, anchor);
      img.raycastTarget = true;
      var button = img.gameObject.AddComponent<Button>();
      button.targetGraphic = img;
      Txt("Label", img.transform, Vector2.zero, size, label, fontSize, textColor, bold: true);
      return button;
    }

    /// <summary>고르는 카드. 선택 테두리(Selected)는 배경보다 살짝 크게 뒤에 깔린다</summary>
    private static ChoiceCard Card(string name, Transform parent, Vector2 pos, Vector2 size, string label, int fontSize,
      bool withArt, Vector2 artPos, Vector2 artSize, Vector2 labelPos, Color? background = null)
    {
      var root = Node(name, parent, pos, size);
      var selected = Img("Selected", root, Vector2.zero, size + new Vector2(28f, 28f), Primary, _rounded);
      var bg = Img("Bg", root, Vector2.zero, size, background ?? Color.white, _rounded);
      bg.raycastTarget = true;
      var art = withArt ? Img("Art", root, artPos, artSize, Mint, _circle) : null;
      var text = Txt("Label", root, labelPos, new Vector2(size.x, 120f), label, fontSize, Dark, bold: true);
      var button = root.gameObject.AddComponent<Button>();
      button.targetGraphic = bg;

      var card = root.gameObject.AddComponent<ChoiceCard>();
      SetRef(card, "_button", button);
      SetRef(card, "_selectedMark", selected.gameObject);
      SetRef(card, "_image", art);
      SetRef(card, "_label", text);
      selected.gameObject.SetActive(false);
      return card;
    }

    private static InputField InputBox(string name, Transform parent, Vector2 pos, Vector2 size, string placeholder)
    {
      var bg = Img(name, parent, pos, size, Color.white, _rounded);
      bg.raycastTarget = true;
      var area = Stretch("TextArea", bg.transform);
      area.offsetMin = new Vector2(40f, 10f);
      area.offsetMax = new Vector2(-40f, -10f);
      var hint = Txt("Placeholder", area, Vector2.zero, Vector2.zero, placeholder, 48, Grey);
      Fit(hint.rectTransform);
      var text = Txt("Text", area, Vector2.zero, Vector2.zero, "", 56, Dark, bold: true);
      Fit(text.rectTransform);
      text.supportRichText = false;

      var field = bg.gameObject.AddComponent<InputField>();
      field.targetGraphic = bg;
      field.textComponent = text;
      field.placeholder = hint;
      field.lineType = InputField.LineType.SingleLine;
      field.characterLimit = NicknameScreen.MaxLength;
      return field;
    }

    // ---------- 설정 ----------

    private static void SetFirstBuildScene(string path)
    {
      var others = EditorBuildSettings.scenes.Where(s => s.path != path);
      EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) }.Concat(others).ToArray();
    }

    private static void ApplyPortraitSettings()
    {
      PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
      PlayerSettings.allowedAutorotateToPortrait = true;
      PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
      PlayerSettings.allowedAutorotateToLandscapeLeft = false;
      PlayerSettings.allowedAutorotateToLandscapeRight = false;

      try
      {
        SetGameViewPortrait();
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[BrushGame] Game 창 해상도를 자동으로 바꾸지 못했습니다. 직접 1080x1920으로 설정하세요. ({e.Message})");
      }
    }

    /// <summary>Game 창에 1080x1920 해상도를 추가하고 선택한다 (에디터 내부 API라 실패할 수 있음)</summary>
    private static void SetGameViewPortrait()
    {
      const int w = 1080;
      const int h = 1920;
      var asm = typeof(Editor).Assembly;
      var sizesType = asm.GetType("UnityEditor.GameViewSizes");
      var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
      var group = sizesType.GetProperty("currentGroup").GetValue(singleton);
      var groupType = group.GetType();

      var count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
      var index = -1;
      for (var i = 0; i < count; i++)
      {
        var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
        var sw = (int)size.GetType().GetProperty("width").GetValue(size);
        var sh = (int)size.GetType().GetProperty("height").GetValue(size);
        if (sw == w && sh == h)
        {
          index = i;
          break;
        }
      }
      if (index < 0)
      {
        var sizeType = asm.GetType("UnityEditor.GameViewSize");
        var kindType = asm.GetType("UnityEditor.GameViewSizeType");
        var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
        var newSize = ctor.Invoke(new[] { Enum.Parse(kindType, "FixedResolution"), w, h, (object)"Portrait 1080x1920" });
        groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
        index = count;
      }

      var gameViewType = asm.GetType("UnityEditor.GameView");
      var gameView = EditorWindow.GetWindow(gameViewType);
      gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Invoke(gameView, new object[] { index, null });
    }

    // ---------- serialized fields ----------

    private static void SetRef(Object target, string property, Object value)
    {
      var so = new SerializedObject(target);
      so.FindProperty(property).objectReferenceValue = value;
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string property, bool value)
    {
      var so = new SerializedObject(target);
      so.FindProperty(property).boolValue = value;
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string property, Object[] values)
    {
      var so = new SerializedObject(target);
      var prop = so.FindProperty(property);
      prop.arraySize = values.Length;
      for (var i = 0; i < values.Length; i++)
      {
        prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
      }
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- placeholder sprites ----------

    private static Sprite LoadOrCreateSprite(string file, int size, float radius, Vector4 border)
    {
      var path = $"{PlaceholderDir}/{file}";
      if (!File.Exists(path))
      {
        Directory.CreateDirectory(PlaceholderDir);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
          for (var x = 0; x < size; x++)
          {
            // 가장 가까운 안쪽 사각형 점까지의 거리로 모서리를 둥글게 (가장자리 안티앨리어싱)
            var px0 = x + 0.5f;
            var py0 = y + 0.5f;
            var cx = Mathf.Clamp(px0, radius, size - radius);
            var cy = Mathf.Clamp(py0, radius, size - radius);
            var d = Vector2.Distance(new Vector2(px0, py0), new Vector2(cx, cy));
            var a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
          }
        }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
      }
      return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
  }
}
