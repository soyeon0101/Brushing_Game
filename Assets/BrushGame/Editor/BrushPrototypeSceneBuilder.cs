using System;
using System.IO;
using System.Reflection;
using BrushGame.HandTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace BrushGame.EditorTools
{
  /// <summary>
  ///   지침서 v2.1 기준 세로 화면 프로토타입 씬을 임시 도형으로 만든다.
  ///   씬이 이미 있으면 덮어쓰지 않는다 (손으로 고친 내용 보호). 다시 만들려면 씬 파일을 지우고 실행.
  /// </summary>
  public static class BrushPrototypeSceneBuilder
  {
    private const string ScenePath = "Assets/BrushGame/Scenes/BrushPrototype_v2.unity";
    private const string ArtDir = "Assets/BrushGame/Art/Placeholder";
    private const string BootstrapPrefabPath = "Assets/MediaPipeUnity/Samples/Resources/Bootstrap.prefab";

    private static readonly Color Dark = new Color(0.22f, 0.16f, 0.32f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 0f);

    private static Sprite _circle;
    private static Sprite _rounded;
    private static Sprite _square;
    private static Font _font;

    [MenuItem("BrushGame/Build Prototype Scene (v2 Portrait)")]
    public static void Build()
    {
      if (File.Exists(ScenePath))
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 가 이미 있어서 만들지 않았습니다. 다시 만들려면 파일을 지우고 실행하세요.");
        return;
      }

      ApplyPortraitSettings();

      _circle = LoadOrCreateSprite("Circle.png", 256, 128f, Vector4.zero);
      _rounded = LoadOrCreateSprite("Rounded.png", 128, 40f, new Vector4(44, 44, 44, 44));
      _square = LoadOrCreateSprite("Square.png", 8, 0f, Vector4.zero);
      _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

      var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
      var cam = camGo.AddComponent<Camera>();
      cam.orthographic = true;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(0.93f, 0.91f, 1f);
      camGo.transform.position = new Vector3(0f, 0f, -10f);
      camGo.AddComponent<CameraFitWidth>();

      var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
      var canvas = canvasGo.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = cam;
      canvas.planeDistance = 5f;
      var scaler = canvasGo.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1080f, 1920f);
      scaler.matchWidthOrHeight = 0f;
      var root = canvasGo.transform;

      BuildBackground(root);
      var patient = BuildPatient(root);

      // 상단 safe area 안의 반응 알림
      var safe = Rect("SafeArea", root, Vector2.zero, Vector2.zero);
      Stretch(safe);
      safe.gameObject.AddComponent<SafeAreaFitter>();
      var toast = BuildToast(safe);

      // 개발용 (실제 플레이에서는 숨김, F1로 켜기)
      var preview = BuildCameraPreview(root, out var screen);
      var hint = Txt("DevHint", root, new Vector2(0f, 60f), new Vector2(1040f, 60f),
        "[개발] Space 닦기 · Space+Shift 과속 · H 손 숨김 · F1 개발 표시", 32, new Color(0.3f, 0.3f, 0.4f, 0.8f), TextAnchor.MiddleCenter);
      hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);

      // 카메라 손 인식
      var ht = new GameObject("HandTracking");
      var runner = ht.AddComponent<BrushHandLandmarkerRunner>();
      SetRef(runner, "_bootstrapPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath));
      SetRef(runner, "screen", screen);
      SetBool(runner, "_drawLandmarks", false);
      var cameraInput = ht.AddComponent<CameraBrushInput>();
      SetRef(cameraInput, "_runner", runner);
      var overlay = ht.AddComponent<BrushDebugOverlay>();
      SetRef(overlay, "_input", cameraInput);

      // 게임 로직
      var game = new GameObject("Game");
      game.AddComponent<KeyboardBrushInput>();
      var reactions = game.AddComponent<PatientReactionController>();
      var session = game.AddComponent<SessionManager>();
      SetRef(reactions, "_toast", toast);
      SetRef(session, "_brushInputSource", cameraInput);
      SetRef(session, "_patient", patient);
      SetRef(session, "_reactions", reactions);
      var dev = game.AddComponent<DevOnlyObjects>();
      SetArray(dev, "_objects", new Object[] { preview.gameObject, hint.gameObject });
      SetArray(dev, "_behaviours", new Object[] { overlay });

      Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
      EditorSceneManager.SaveScene(scene, ScenePath);
      Debug.Log($"[BrushGame] 세로 프로토타입 씬 생성: {ScenePath}");
    }

    /// <summary>
    ///   현재 플랫폼(안드로이드 포함)의 Game 창을 1080x1920으로 맞추고,
    ///   열린 씬의 카메라/캔버스 설정을 세로 기준값으로 되돌린다.
    /// </summary>
    [MenuItem("BrushGame/Fix Portrait View (Camera + Canvas + Game View)")]
    public static void FixPortraitView()
    {
      ApplyPortraitSettings();

      var cam = Camera.main;
      if (cam == null)
      {
        Debug.LogWarning("[BrushGame] MainCamera 태그가 붙은 카메라가 없습니다.");
        return;
      }
      Undo.RecordObject(cam, "Fix Portrait View");
      Undo.RecordObject(cam.transform, "Fix Portrait View");
      cam.orthographic = true;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.rect = new UnityEngine.Rect(0f, 0f, 1f, 1f);
      cam.nearClipPlane = 0.3f;
      cam.farClipPlane = 1000f;
      cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
      cam.transform.localScale = Vector3.one;
      var fit = cam.GetComponent<CameraFitWidth>();
      if (fit == null)
      {
        fit = Undo.AddComponent<CameraFitWidth>(cam.gameObject);
      }
      var fso = new SerializedObject(fit);
      fso.FindProperty("_targetWorldWidth").floatValue = 10f;
      fso.ApplyModifiedProperties();
      fit.enabled = true;

      foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
      {
        if (!canvas.isRootCanvas)
        {
          continue;
        }
        Undo.RecordObject(canvas, "Fix Portrait View");
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
          Undo.RecordObject(scaler, "Fix Portrait View");
          scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
          scaler.referenceResolution = new Vector2(1080f, 1920f);
          scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
          scaler.matchWidthOrHeight = 0f;
        }
      }

      EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
      Debug.Log("[BrushGame] 세로 화면 설정 복구 완료 (Game 창 1080x1920, 카메라, 캔버스). 씬을 저장하세요.");
    }

    // ---------- 설정 ----------

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

    // ---------- 배경 ----------

    private static void BuildBackground(Transform root)
    {
      var wall = Img("Wall", root, Vector2.zero, Vector2.zero, _square, new Color(0.93f, 0.91f, 1f));
      Stretch(wall.rectTransform);
      var floor = Img("Floor", root, Vector2.zero, Vector2.zero, _square, new Color(0.84f, 0.8f, 0.96f));
      floor.rectTransform.anchorMin = new Vector2(0f, 0f);
      floor.rectTransform.anchorMax = new Vector2(1f, 0f);
      floor.rectTransform.pivot = new Vector2(0.5f, 0f);
      floor.rectTransform.sizeDelta = new Vector2(0f, 300f);

      // 밝은 파스텔 우주가 보이는 둥근 창
      var metal = new Color(0.8f, 0.82f, 0.92f);
      var metalDark = new Color(0.66f, 0.7f, 0.85f);
      var space = new Color(0.45f, 0.48f, 0.82f);
      for (var w = 0; w < 2; w++)
      {
        var x = w == 0 ? -390f : 390f;
        var porthole = Rect(w == 0 ? "PortholeL" : "PortholeR", root, new Vector2(x, 600f), new Vector2(260f, 260f));
        Img("Frame", porthole, Vector2.zero, new Vector2(260f, 260f), _circle, metal);
        Img("FrameInner", porthole, Vector2.zero, new Vector2(232f, 232f), _circle, metalDark);
        var glass = Img("Glass", porthole, Vector2.zero, new Vector2(210f, 210f), _circle, space);
        glass.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var rng = new Random(11 + w);
        for (var i = 0; i < 12; i++)
        {
          var s = 5f + (float)rng.NextDouble() * 8f;
          var p = new Vector2((float)rng.NextDouble() * 190f - 95f, (float)rng.NextDouble() * 190f - 95f);
          Img("Star", glass.transform, p, new Vector2(s, s), _circle, new Color(1f, 1f, 0.9f, 0.8f));
        }
        if (w == 0)
        {
          Img("PlanetRing", glass.transform, new Vector2(30f, -30f), new Vector2(160f, 36f), _circle, new Color(1f, 0.9f, 0.65f, 0.9f), rotZ: 20f);
          Img("Planet", glass.transform, new Vector2(30f, -30f), new Vector2(96f, 96f), _circle, new Color(1f, 0.72f, 0.55f));
        }
        else
        {
          Img("Planet", glass.transform, new Vector2(-40f, 40f), new Vector2(76f, 76f), _circle, new Color(0.6f, 0.92f, 0.82f));
          Img("Moon", glass.transform, new Vector2(55f, -45f), new Vector2(40f, 40f), _circle, new Color(1f, 0.95f, 0.8f));
        }
        Img("Glare", porthole, new Vector2(-48f, 55f), new Vector2(56f, 24f), _circle, new Color(1f, 1f, 1f, 0.45f), rotZ: 40f);
      }

      Img("Chair", root, new Vector2(0f, -560f), new Vector2(840f, 440f), _rounded, new Color(0.6f, 0.86f, 0.9f), ppu: 0.5f);
    }

    // ---------- 환자 ----------

    private static Patient BuildPatient(Transform parent)
    {
      var root = Rect("Patient", parent, new Vector2(0f, 40f), new Vector2(1000f, 1300f));
      var skin = new System.Collections.Generic.List<Object>();
      var white = Color.white;

      // 몸통과 팔 (머리 뒤)
      var armL = Rect("ArmL", root, new Vector2(-320f, -470f), new Vector2(90f, 240f));
      armL.pivot = new Vector2(0.5f, 0.9f);
      armL.localRotation = Quaternion.Euler(0f, 0f, -28f);
      skin.Add(Img("Arm", armL, Vector2.zero, new Vector2(90f, 240f), _rounded, white, ppu: 1f));
      skin.Add(Img("Hand", armL, new Vector2(0f, -110f), new Vector2(110f, 110f), _circle, white));

      var armR = Rect("ArmR", root, new Vector2(320f, -470f), new Vector2(90f, 240f));
      armR.pivot = new Vector2(0.5f, 0.9f);
      armR.localRotation = Quaternion.Euler(0f, 0f, 28f);
      skin.Add(Img("Arm", armR, Vector2.zero, new Vector2(90f, 240f), _rounded, white, ppu: 1f));
      skin.Add(Img("Hand", armR, new Vector2(0f, -110f), new Vector2(110f, 110f), _circle, white));

      skin.Add(Img("Torso", root, new Vector2(0f, -540f), new Vector2(560f, 380f), _rounded, white, ppu: 0.6f));
      Img("Belly", root, new Vector2(0f, -570f), new Vector2(320f, 210f), _circle, new Color(1f, 1f, 1f, 0.35f));

      // 귀와 더듬이 (부드러운 외계인 특징)
      foreach (var sx in new[] { -1f, 1f })
      {
        skin.Add(Img("Ear", root, new Vector2(440f * sx, 130f), new Vector2(170f, 170f), _circle, white));
        Img("EarInner", root, new Vector2(440f * sx, 130f), new Vector2(90f, 90f), _circle, new Color(1f, 0.78f, 0.84f));
        skin.Add(Img("Antenna", root, new Vector2(130f * sx, 560f), new Vector2(26f, 190f), _rounded, white, rotZ: -18f * sx, ppu: 3f));
        Img("AntennaBall", root, new Vector2(162f * sx, 650f), new Vector2(84f, 84f), _circle, sx < 0 ? new Color(1f, 0.82f, 0.5f) : new Color(1f, 0.72f, 0.86f));
      }

      skin.Add(Img("Head", root, new Vector2(0f, 20f), new Vector2(920f, 980f), _circle, white));
      Img("HeadShine", root, new Vector2(-230f, 380f), new Vector2(230f, 100f), _circle, new Color(1f, 1f, 1f, 0.3f), rotZ: 25f);

      // 볼 홍조 (모든 표정 공통)
      var cheeks = new Object[2];
      for (var i = 0; i < 2; i++)
      {
        var sx = i == 0 ? -1f : 1f;
        cheeks[i] = Img("Cheek", root, new Vector2(345f * sx, 120f), new Vector2(160f, 104f), _circle, new Color(1f, 0.64f, 0.74f, 0.75f));
      }

      var face = Rect("Face", root, new Vector2(0f, 270f), new Vector2(700f, 400f));
      var normal = BuildNormalFace(face);
      var pain = BuildPainFace(face);
      var happy = BuildHappyFace(face);

      var mouth = Rect("Mouth", root, new Vector2(0f, -150f), new Vector2(640f, 440f));
      var mouthView = BuildMouth(mouth);

      var patient = root.gameObject.AddComponent<Patient>();
      SetRef(patient, "_root", root);
      SetArray(patient, "_skin", skin.ToArray());
      SetRef(patient, "_normalFace", normal);
      SetRef(patient, "_painFace", pain);
      SetRef(patient, "_happyFace", happy);
      SetArray(patient, "_cheeks", cheeks);
      SetRef(patient, "_waveArm", armR);
      SetRef(patient, "_mouth", mouth);
      SetRef(patient, "_mouthView", mouthView);
      pain.SetActive(false);
      happy.SetActive(false);

      // 에디터에서도 파스텔 피부로 보이게 (실행 시 환자마다 바뀜)
      foreach (Graphic g in skin)
      {
        g.color = new Color(0.72f, 0.92f, 0.62f);
      }
      return patient;
    }

    private static GameObject BuildNormalFace(Transform face)
    {
      // 기본: 큰 눈 + 하이라이트 + 편안한 눈썹
      var normal = Rect("NormalFace", face, Vector2.zero, new Vector2(700f, 400f));
      foreach (var sx in new[] { -1f, 1f })
      {
        Img("EyeWhite", normal, new Vector2(190f * sx, 0f), new Vector2(210f, 230f), _circle, Color.white);
        Img("Pupil", normal, new Vector2(180f * sx, -14f), new Vector2(132f, 148f), _circle, Dark);
        Img("HighlightBig", normal, new Vector2(180f * sx - 28f, 26f), new Vector2(48f, 48f), _circle, Color.white);
        Img("HighlightSmall", normal, new Vector2(180f * sx + 26f, -48f), new Vector2(22f, 22f), _circle, Color.white);
        Img("Brow", normal, new Vector2(190f * sx, 165f), new Vector2(104f, 24f), _rounded, Dark, rotZ: -4f * sx, ppu: 4f);
      }
      return normal.gameObject;
    }

    private static GameObject BuildPainFace(Transform face)
    {
      // 아픔: >_< + 걱정 눈썹 + 눈물 한 방울
      var pain = Rect("PainFace", face, Vector2.zero, new Vector2(700f, 400f));
      foreach (var sx in new[] { -1f, 1f })
      {
        // 왼쪽 눈 ">" , 오른쪽 눈 "<"
        Img("EyeA", pain, new Vector2(190f * sx, 26f), new Vector2(130f, 30f), _rounded, Dark, rotZ: -24f * sx, ppu: 3f);
        Img("EyeB", pain, new Vector2(190f * sx, -26f), new Vector2(130f, 30f), _rounded, Dark, rotZ: 24f * sx, ppu: 3f);
        Img("Brow", pain, new Vector2(190f * sx, 150f), new Vector2(104f, 24f), _rounded, Dark, rotZ: 18f * sx, ppu: 4f);
      }
      Img("Tear", pain, new Vector2(-290f, -80f), new Vector2(40f, 54f), _circle, new Color(0.6f, 0.85f, 1f));
      return pain.gameObject;
    }

    private static GameObject BuildHappyFace(Transform face)
    {
      // 기쁨: ^ ^ + 올라간 눈썹
      var happy = Rect("HappyFace", face, Vector2.zero, new Vector2(700f, 400f));
      foreach (var sx in new[] { -1f, 1f })
      {
        Img("EyeA", happy, new Vector2(190f * sx - 40f, 0f), new Vector2(120f, 30f), _rounded, Dark, rotZ: 38f, ppu: 3f);
        Img("EyeB", happy, new Vector2(190f * sx + 40f, 0f), new Vector2(120f, 30f), _rounded, Dark, rotZ: -38f, ppu: 3f);
        Img("Brow", happy, new Vector2(190f * sx, 180f), new Vector2(104f, 24f), _rounded, Dark, rotZ: -8f * sx, ppu: 4f);
      }
      return happy.gameObject;
    }

    // ---------- 입 ----------

    private static MouthView BuildMouth(RectTransform mouth)
    {
      var rim = Img("MouthRim", mouth, Vector2.zero, Vector2.zero, _rounded, new Color(0.96f, 0.5f, 0.6f), ppu: 0.35f);
      Stretch(rim.rectTransform);
      var inside = Img("MouthInside", mouth, Vector2.zero, Vector2.zero, _rounded, new Color(1f, 0.72f, 0.76f), ppu: 0.38f);
      Stretch(inside.rectTransform, 16f);

      var content = Rect("Content", mouth, Vector2.zero, new Vector2(640f, 440f));
      Img("Tongue", content, new Vector2(0f, -168f), new Vector2(220f, 96f), _circle, new Color(1f, 0.5f, 0.62f));

      // 이: 윗줄 5개, 아랫줄 4개. 두 개는 처음부터 비교적 깨끗하게
      var teeth = new System.Collections.Generic.List<(Image tooth, Image gloss, CanvasGroup plaque, CanvasGroup foam)>();
      float[] topX = { -232f, -116f, 0f, 116f, 232f };
      float[] bottomX = { -180f, -60f, 60f, 180f };
      var rng = new Random(5);
      for (var i = 0; i < topX.Length; i++)
      {
        teeth.Add(Tooth($"ToothTop{i}", content, new Vector2(topX[i], 112f), new Vector2(108f, 132f), top: true, dirty: i != 2, rng));
      }
      for (var i = 0; i < bottomX.Length; i++)
      {
        teeth.Add(Tooth($"ToothBottom{i}", content, new Vector2(bottomX[i], -92f), new Vector2(118f, 118f), top: false, dirty: i != 1, rng));
      }

      // 음식 찌꺼기: 이 가장자리 / 이 사이에 붙어 있게
      var foods = new[]
      {
        Candy(content, new Vector2(-174f, 50f)),
        Pea(content, new Vector2(60f, -34f)),
        Cookie(content, new Vector2(116f, 48f)),
        Jelly(content, new Vector2(-120f, -30f)),
      };

      // 세균: 둥글고 말랑, 가시 없음, 장난꾸러기 표정
      var germs = new[]
      {
        Germ("GermWink", content, new Vector2(232f, 96f), new Color(0.78f, 0.64f, 1f), GermFace.Wink),
        Germ("GermTongue", content, new Vector2(180f, -104f), new Color(0.5f, 0.86f, 0.82f), GermFace.Tongue),
        Germ("GermGrin", content, new Vector2(-116f, 100f), new Color(1f, 0.72f, 0.56f), GermFace.Grin),
      };

      // 양치 중 입 전체 거품
      Vector3[] foamSpots =
      {
        new Vector3(-200f, 40f, 72f), new Vector3(-60f, 60f, 58f), new Vector3(90f, 30f, 82f), new Vector3(230f, 50f, 62f),
        new Vector3(-150f, -140f, 64f), new Vector3(20f, -150f, 52f), new Vector3(170f, -130f, 72f), new Vector3(-10f, 10f, 46f),
      };
      var foams = new Image[foamSpots.Length];
      for (var i = 0; i < foamSpots.Length; i++)
      {
        var f = foamSpots[i];
        foams[i] = Img($"Foam{i}", content, new Vector2(f.x, f.y), new Vector2(f.z, f.z), _circle, new Color(1f, 1f, 1f, 0f));
      }

      Vector2[] sparkleSpots = { new Vector2(-200f, 170f), new Vector2(90f, 175f), new Vector2(200f, -60f), new Vector2(-60f, -60f), new Vector2(10f, 150f) };
      var sparkles = new Image[sparkleSpots.Length];
      for (var i = 0; i < sparkleSpots.Length; i++)
      {
        sparkles[i] = Img($"Sparkle{i}", content, sparkleSpots[i], new Vector2(44f, 44f), _square, Gold, rotZ: 45f);
      }
      var pop = Img("PopSparkleTemplate", content, Vector2.zero, new Vector2(40f, 40f), _square, Gold);

      var view = mouth.gameObject.AddComponent<MouthView>();
      var so = new SerializedObject(view);
      so.FindProperty("_content").objectReferenceValue = content;
      var teethProp = so.FindProperty("_teeth");
      teethProp.arraySize = teeth.Count;
      for (var i = 0; i < teeth.Count; i++)
      {
        var e = teethProp.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("tooth").objectReferenceValue = teeth[i].tooth;
        e.FindPropertyRelative("gloss").objectReferenceValue = teeth[i].gloss;
        e.FindPropertyRelative("plaque").objectReferenceValue = teeth[i].plaque;
        e.FindPropertyRelative("foamCover").objectReferenceValue = teeth[i].foam;
      }
      var foodProp = so.FindProperty("_foods");
      foodProp.arraySize = foods.Length;
      for (var i = 0; i < foods.Length; i++)
      {
        foodProp.GetArrayElementAtIndex(i).objectReferenceValue = foods[i];
      }
      var germProp = so.FindProperty("_germs");
      germProp.arraySize = germs.Length;
      for (var i = 0; i < germs.Length; i++)
      {
        var e = germProp.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("group").objectReferenceValue = germs[i].group;
        e.FindPropertyRelative("bubble").objectReferenceValue = germs[i].bubble;
      }
      SetArrayProp(so.FindProperty("_foams"), foams);
      SetArrayProp(so.FindProperty("_sparkles"), sparkles);
      so.FindProperty("_popTemplate").objectReferenceValue = pop;
      so.ApplyModifiedPropertiesWithoutUndo();
      return view;
    }

    private static (Image tooth, Image gloss, CanvasGroup plaque, CanvasGroup foam) Tooth(string name, Transform parent, Vector2 pos, Vector2 size, bool top, bool dirty, Random rng)
    {
      var tooth = Img(name, parent, pos, size, _rounded, Color.white, ppu: 1f);
      CanvasGroup plaque = null;
      CanvasGroup foam = null;
      if (dirty)
      {
        // 치태: 이마다 모양과 양이 다른 불규칙한 파스텔 얼룩 (잇몸 쪽)
        var p = Rect("Plaque", tooth.transform, Vector2.zero, size);
        plaque = p.gameObject.AddComponent<CanvasGroup>();
        var color = rng.Next(2) == 0 ? new Color(1f, 0.88f, 0.45f, 0.95f) : new Color(0.8f, 0.93f, 0.45f, 0.95f);
        var blobs = 1 + rng.Next(3);
        var gumSide = top ? 1f : -1f;
        for (var b = 0; b < blobs; b++)
        {
          var bw = 30f + (float)rng.NextDouble() * 34f;
          var bh = 22f + (float)rng.NextDouble() * 24f;
          var bx = ((float)rng.NextDouble() * 0.6f - 0.3f) * size.x;
          var by = gumSide * (0.08f + (float)rng.NextDouble() * 0.28f) * size.y;
          Img("Blob", p, new Vector2(bx, by), new Vector2(bw, bh), _circle, color);
        }

        // 치태를 덮었다가 걷히는 거품
        var f = Rect("FoamCover", tooth.transform, Vector2.zero, size);
        foam = f.gameObject.AddComponent<CanvasGroup>();
        foam.alpha = 0f;
        Img("Bubble", f, new Vector2(-20f, 20f), new Vector2(70f, 70f), _circle, Color.white);
        Img("Bubble", f, new Vector2(24f, 30f), new Vector2(56f, 56f), _circle, Color.white);
        Img("Bubble", f, new Vector2(0f, -18f), new Vector2(80f, 80f), _circle, Color.white);
        Img("Bubble", f, new Vector2(30f, -34f), new Vector2(44f, 44f), _circle, Color.white);
        Img("Bubble", f, new Vector2(-30f, -40f), new Vector2(40f, 40f), _circle, Color.white);
      }
      var gloss = Img("Gloss", tooth.transform, new Vector2(-size.x * 0.24f, size.y * 0.18f), new Vector2(22f, 46f), _rounded, new Color(1f, 1f, 1f, 0f), ppu: 4f);
      return (tooth, gloss, plaque, foam);
    }

    private static CanvasGroup Candy(Transform parent, Vector2 pos)
    {
      var rt = Rect("FoodCandy", parent, pos, new Vector2(60f, 60f));
      Img("Body", rt, Vector2.zero, new Vector2(52f, 52f), _circle, new Color(1f, 0.56f, 0.76f));
      Img("Stripe", rt, Vector2.zero, new Vector2(40f, 10f), _rounded, new Color(1f, 1f, 1f, 0.85f), rotZ: 35f, ppu: 8f);
      Img("Shine", rt, new Vector2(-11f, 12f), new Vector2(12f, 12f), _circle, Color.white);
      return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static CanvasGroup Pea(Transform parent, Vector2 pos)
    {
      var rt = Rect("FoodPea", parent, pos, new Vector2(50f, 50f));
      Img("Body", rt, Vector2.zero, new Vector2(44f, 44f), _circle, new Color(0.55f, 0.85f, 0.36f));
      Img("Shine", rt, new Vector2(-9f, 9f), new Vector2(12f, 12f), _circle, new Color(1f, 1f, 1f, 0.8f));
      return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static CanvasGroup Cookie(Transform parent, Vector2 pos)
    {
      var rt = Rect("FoodCookie", parent, pos, new Vector2(60f, 50f));
      Img("Body", rt, Vector2.zero, new Vector2(54f, 44f), _rounded, new Color(0.9f, 0.72f, 0.46f), ppu: 3f);
      var chip = new Color(0.55f, 0.35f, 0.22f);
      Img("Chip", rt, new Vector2(-12f, 6f), new Vector2(10f, 10f), _circle, chip);
      Img("Chip", rt, new Vector2(10f, 10f), new Vector2(8f, 8f), _circle, chip);
      Img("Chip", rt, new Vector2(4f, -10f), new Vector2(10f, 10f), _circle, chip);
      return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static CanvasGroup Jelly(Transform parent, Vector2 pos)
    {
      var rt = Rect("FoodJelly", parent, pos, new Vector2(56f, 50f));
      Img("Body", rt, Vector2.zero, new Vector2(52f, 46f), _rounded, new Color(1f, 0.62f, 0.32f, 0.9f), ppu: 2.5f);
      Img("Shine", rt, new Vector2(-12f, 10f), new Vector2(14f, 9f), _circle, new Color(1f, 1f, 1f, 0.8f));
      return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private enum GermFace { Wink, Tongue, Grin }

    private static (CanvasGroup group, CanvasGroup bubble) Germ(string name, Transform parent, Vector2 pos, Color color, GermFace face)
    {
      var rt = Rect(name, parent, pos, new Vector2(100f, 100f));
      var group = rt.gameObject.AddComponent<CanvasGroup>();
      Img("Nub", rt, new Vector2(0f, 46f), new Vector2(24f, 24f), _circle, color);
      Img("Body", rt, Vector2.zero, new Vector2(90f, 86f), _circle, color);
      Img("Shine", rt, new Vector2(-22f, 22f), new Vector2(20f, 12f), _circle, new Color(1f, 1f, 1f, 0.5f), rotZ: 30f);

      switch (face)
      {
        case GermFace.Wink:
          Img("EyeClosed", rt, new Vector2(-16f, 8f), new Vector2(20f, 6f), _rounded, Dark, ppu: 10f);
          Img("Eye", rt, new Vector2(16f, 8f), new Vector2(14f, 16f), _circle, Dark);
          Img("Mouth", rt, new Vector2(2f, -16f), new Vector2(26f, 8f), _rounded, Dark, rotZ: -8f, ppu: 10f);
          break;
        case GermFace.Tongue:
          Img("Eye", rt, new Vector2(-15f, 8f), new Vector2(14f, 16f), _circle, Dark);
          Img("Eye", rt, new Vector2(15f, 8f), new Vector2(14f, 16f), _circle, Dark);
          Img("Mouth", rt, new Vector2(0f, -14f), new Vector2(28f, 8f), _rounded, Dark, ppu: 10f);
          Img("Tongue", rt, new Vector2(5f, -23f), new Vector2(16f, 20f), _circle, new Color(1f, 0.5f, 0.65f));
          break;
        default:
          Img("Eye", rt, new Vector2(-15f, 8f), new Vector2(14f, 16f), _circle, Dark);
          Img("Eye", rt, new Vector2(15f, 8f), new Vector2(14f, 16f), _circle, Dark);
          Img("EyeShine", rt, new Vector2(-13f, 12f), new Vector2(5f, 5f), _circle, Color.white);
          Img("EyeShine", rt, new Vector2(17f, 12f), new Vector2(5f, 5f), _circle, Color.white);
          Img("Grin", rt, new Vector2(0f, -16f), new Vector2(32f, 12f), _rounded, Dark, ppu: 8f);
          break;
      }

      // 세균을 가두는 거품 방울 (처음엔 안 보임)
      var b = Rect("Bubble", rt, Vector2.zero, new Vector2(150f, 150f));
      var bubble = b.gameObject.AddComponent<CanvasGroup>();
      bubble.alpha = 0f;
      Img("Film", b, Vector2.zero, new Vector2(150f, 150f), _circle, new Color(0.78f, 0.95f, 1f, 0.45f));
      Img("Shine", b, new Vector2(-38f, 42f), new Vector2(36f, 18f), _circle, new Color(1f, 1f, 1f, 0.9f), rotZ: 35f);
      return (group, bubble);
    }

    // ---------- UI ----------

    private static ReactionToastUI BuildToast(Transform parent)
    {
      var panel = Rect("ReactionToast", parent, new Vector2(0f, -125f), new Vector2(960f, 190f));
      panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
      var group = panel.gameObject.AddComponent<CanvasGroup>();
      group.alpha = 0f;
      group.blocksRaycasts = false;
      group.interactable = false;

      var bg = Img("Bg", panel, Vector2.zero, Vector2.zero, _rounded, new Color(1f, 1f, 1f, 0.97f), ppu: 0.8f);
      Stretch(bg.rectTransform);
      var shadow = bg.gameObject.AddComponent<Shadow>();
      shadow.effectColor = new Color(0.3f, 0.2f, 0.5f, 0.15f);
      shadow.effectDistance = new Vector2(0f, -8f);

      var ring = Img("IconRing", panel, new Vector2(-370f, 0f), new Vector2(160f, 160f), _circle, new Color(0.93f, 0.4f, 0.45f));
      var icon = Img("Icon", panel, new Vector2(-370f, 0f), new Vector2(136f, 136f), _circle, new Color(0.72f, 0.92f, 0.62f));
      Img("IconEyeL", panel, new Vector2(-398f, 12f), new Vector2(30f, 34f), _circle, Dark);
      Img("IconEyeR", panel, new Vector2(-342f, 12f), new Vector2(30f, 34f), _circle, Dark);
      Img("IconCheekL", panel, new Vector2(-420f, -22f), new Vector2(30f, 18f), _circle, new Color(1f, 0.58f, 0.68f, 0.7f));
      Img("IconCheekR", panel, new Vector2(-320f, -22f), new Vector2(30f, 18f), _circle, new Color(1f, 0.58f, 0.68f, 0.7f));
      var text = Txt("Message", panel, new Vector2(90f, 0f), new Vector2(700f, 170f), "", 66, Dark, TextAnchor.MiddleLeft);
      text.fontStyle = FontStyle.Bold;

      var toast = panel.gameObject.AddComponent<ReactionToastUI>();
      SetRef(toast, "_panel", panel);
      SetRef(toast, "_group", group);
      SetRef(toast, "_text", text);
      SetRef(toast, "_icon", icon);
      SetRef(toast, "_iconRing", ring);
      return toast;
    }

    private static RectTransform BuildCameraPreview(Transform parent, out Mediapipe.Unity.Screen screen)
    {
      var preview = Rect("CameraPreview", parent, new Vector2(-30f, 320f), new Vector2(240f, 320f));
      preview.anchorMin = preview.anchorMax = new Vector2(1f, 0f);
      preview.pivot = new Vector2(1f, 0f);
      var bg = preview.gameObject.AddComponent<Image>();
      bg.color = new Color(0f, 0f, 0f, 0.5f);
      bg.raycastTarget = false;
      preview.gameObject.AddComponent<RectMask2D>();

      var raw = Rect("Screen", preview, Vector2.zero, new Vector2(1280f, 720f));
      raw.localScale = new Vector3(0.25f, 0.25f, 1f);
      var rawImage = raw.gameObject.AddComponent<RawImage>();
      rawImage.raycastTarget = false;
      screen = raw.gameObject.AddComponent<Mediapipe.Unity.Screen>();
      SetRef(screen, "_screen", rawImage);
      return preview;
    }

    // ---------- helpers ----------

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      var rt = (RectTransform)go.transform;
      rt.SetParent(parent, false);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return rt;
    }

    private static Image Img(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color, float rotZ = 0f, float ppu = 1f)
    {
      var rt = Rect(name, parent, pos, size);
      rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
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

    private static Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, string value, int fontSize, Color color, TextAnchor align)
    {
      var rt = Rect(name, parent, pos, size);
      var t = rt.gameObject.AddComponent<Text>();
      t.font = _font;
      t.text = value;
      t.fontSize = fontSize;
      t.color = color;
      t.alignment = align;
      t.raycastTarget = false;
      t.horizontalOverflow = HorizontalWrapMode.Wrap;
      t.verticalOverflow = VerticalWrapMode.Overflow;
      return t;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = new Vector2(inset, inset);
      rt.offsetMax = new Vector2(-inset, -inset);
    }

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
      SetArrayProp(so.FindProperty(property), values);
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArrayProp(SerializedProperty prop, Object[] values)
    {
      prop.arraySize = values.Length;
      for (var i = 0; i < values.Length; i++)
      {
        prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
      }
    }

    private static Sprite LoadOrCreateSprite(string file, int size, float radius, Vector4 border)
    {
      var path = $"{ArtDir}/{file}";
      if (!File.Exists(path))
      {
        Directory.CreateDirectory(ArtDir);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
          for (var x = 0; x < size; x++)
          {
            byte a = 255;
            if (radius > 0f)
            {
              // 가장 가까운 안쪽 사각형 점까지의 거리로 모서리를 둥글게 (가장자리 안티앨리어싱)
              var px0 = x + 0.5f;
              var py0 = y + 0.5f;
              var cx = Mathf.Clamp(px0, radius, size - radius);
              var cy = Mathf.Clamp(py0, radius, size - radius);
              var d = Vector2.Distance(new Vector2(px0, py0), new Vector2(cx, cy));
              a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
            }
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
