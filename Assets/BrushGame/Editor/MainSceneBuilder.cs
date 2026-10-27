using System;
using System.Collections.Generic;
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
    private const string HudArtDir = "Assets/BrushGame/Art/Hud";
    private const string IconArtDir = "Assets/BrushGame/Art/Icons";
    private const string BadgeArtDir = "Assets/BrushGame/Art/Badges";
    private const string LoadingArtDir = "Assets/BrushGame/Art/Loading";
    private const string FontDir = "Assets/BrushGame/Fonts";
    private const string SoundDir = "Assets/BrushGame/Sound";
    private const string MapArtDir = "Assets/BrushGame/Art/Map";
    private const string PlanetDataDir = "Assets/BrushGame/Data/Planets";
    private const string PartnerArtDir = "Assets/BrushGame/Art/Partner";
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
    private static Sprite _star;
    private static Sprite _heart;
    private static Sprite _check;
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
      LinkPlanets(catalog);

      var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
      var cam = camGo.AddComponent<Camera>();
      cam.orthographic = true;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = Color.black;
      camGo.transform.position = new Vector3(0f, 0f, -10f);
      camGo.AddComponent<CameraFitWidth>();
      camGo.AddComponent<AudioListener>();

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
      var consent = BuildConsent(screens);
      var signup = BuildSignup(screens);
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
      var photoFrame = BuildPhotoFrame(screens);
      BuildPhotoBooth(brushing, feed);
      var stickers = BuildStickers(screens);
      var spaceMap = BuildSpaceMap(screens);
      var partnerRoom = BuildPartnerRoom(screens);
      var badges = BuildBadges(screens);
      var settings = BuildSettings(root);
      BuildQuitConfirm(root);

      new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

      var flow = new GameObject("Game").AddComponent<GameFlow>();
      SetRef(flow, "_catalog", catalog);
      SetRef(flow, "_cameraFeed", feed);
      SetRef(flow, "_title", title);
      SetRef(flow, "_consent", consent);
      SetRef(flow, "_signup", signup);
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
      SetRef(flow, "_photoFrame", photoFrame);
      SetRef(flow, "_stickers", stickers);
      SetRef(flow, "_spaceMap", spaceMap);
      SetRef(flow, "_partnerRoom", partnerRoom);
      SetRef(flow, "_badges", badges);
      SetRef(flow, "_settings", settings);
      BuildSound();

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
      FontRoles.ApplyAll(screen.transform);
      UiSkin.ApplyAll(screen.transform);

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
      FontRoles.ApplyAll(screen.transform);
      UiSkin.ApplyAll(screen.transform);

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
      FontRoles.ApplyAll(screen.transform);
      UiSkin.ApplyAll(screen.transform);

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log($"[BrushGame] {typeof(T).Name} 화면을 다시 만들고 씬을 저장했습니다.");
    }

    /// <summary>이미 있는 Main 씬에 소리(SoundManager, AudioListener)를 붙인다. 이미 있으면 소리 파일 연결만 채운다</summary>
    [MenuItem("BrushGame/Add Sound To Scene")]
    public static void AddSoundMenu()
    {
      var cam = Camera.main;
      if (cam != null && cam.GetComponent<AudioListener>() == null)
      {
        Undo.AddComponent<AudioListener>(cam.gameObject);
      }
      BuildSound();
      EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    ///   열려 있는 Main 씬에 우주 항해 지도를 만들어 GameFlow에 연결한다 (이미 있으면 다시 만든다).
    ///   지도는 로비의 반짝 배지 버튼 자리라, 예전 배지함은 지우지 않고 씬에 남긴다 (GameFlow가 열지 않는다).
    ///   행성 데이터가 없으면 Art/Map/planet_*.png로 만든다.
    /// </summary>
    [MenuItem("BrushGame/Add Space Map To Scene")]
    public static void AddSpaceMapMenu()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var stickers = Object.FindFirstObjectByType<StickerScreen>(FindObjectsInactive.Include);
      if (flow == null || stickers == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      LoadCommonAssets();
      var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
      LinkPlanets(catalog);

      var old = Object.FindFirstObjectByType<SpaceMapScreen>(FindObjectsInactive.Include);
      if (old != null)
      {
        Undo.DestroyObjectImmediate(old.gameObject);
      }
      var screen = BuildSpaceMap(stickers.transform.parent);
      Undo.RegisterCreatedObjectUndo(screen.gameObject, "Add Space Map");
      screen.transform.SetSiblingIndex(stickers.transform.GetSiblingIndex() + 1);
      screen.gameObject.SetActive(false);
      SetRef(flow, "_spaceMap", screen);
      FontRoles.ApplyAll(screen.transform);
      UiSkin.ApplyAll(screen.transform);

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 우주 항해 지도를 만들고 씬을 저장했습니다.");
    }

    /// <summary>
    ///   열려 있는 Main 씬에 파트너 방을 만들어 GameFlow에 연결하고 (이미 있으면 다시 만든다),
    ///   로비 스티커판 버튼 자리를 파트너 방으로 바꾼다. 되돌리기: BrushGame/Restore/Sticker Button - Sticker Board
    /// </summary>
    [MenuItem("BrushGame/Add Partner Room To Scene")]
    public static void AddPartnerRoomMenu()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var stickers = Object.FindFirstObjectByType<StickerScreen>(FindObjectsInactive.Include);
      if (flow == null || stickers == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      LoadCommonAssets();
      SetupPartnerArt();
      var old = Object.FindFirstObjectByType<PartnerRoomScreen>(FindObjectsInactive.Include);
      if (old != null)
      {
        Undo.DestroyObjectImmediate(old.gameObject);
      }
      var screen = BuildPartnerRoom(stickers.transform.parent);
      Undo.RegisterCreatedObjectUndo(screen.gameObject, "Add Partner Room");
      screen.transform.SetSiblingIndex(stickers.transform.GetSiblingIndex() + 1);
      screen.gameObject.SetActive(false);
      SetRef(flow, "_partnerRoom", screen);
      FontRoles.ApplyAll(screen.transform);
      UiSkin.ApplyAll(screen.transform);
      SetStickerSlot(GameFlow.StickerSlot.PartnerRoom);
      Debug.Log("[BrushGame] 파트너 방을 만들고 로비 스티커판 버튼 자리를 파트너 방으로 바꿨습니다.");
    }

    /// <summary>
    ///   열려 있는 Main 씬에 보호자 동의·회원가입 화면(타이틀 다음), 양치 사진 액자(완료 다음),
    ///   양치 중 사진 찍기(카메라 작은 창의 번쩍임)를 만들어 연결하고 저장한다. 이미 있으면 다시 만든다.
    ///   양치 화면은 다시 만들지 않으므로 손으로 고친 내용은 그대로다.
    /// </summary>
    [MenuItem("BrushGame/Add Signup And Photo Frame To Scene")]
    public static void AddSignupAndPhotoMenu()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var title = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
      var complete = Object.FindFirstObjectByType<CompleteScreen>(FindObjectsInactive.Include);
      var brushing = Object.FindFirstObjectByType<BrushingScreen>(FindObjectsInactive.Include);
      var feed = flow != null ? new SerializedObject(flow).FindProperty("_cameraFeed").objectReferenceValue as GameObject : null;
      if (flow == null || title == null || complete == null || brushing == null || feed == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      LoadCommonAssets();

      void Replace<T>(string flowField, Transform after, Func<Transform, T> build) where T : ScreenBase
      {
        var old = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (old != null)
        {
          Undo.DestroyObjectImmediate(old.gameObject);
        }
        var screen = build(after.parent);
        Undo.RegisterCreatedObjectUndo(screen.gameObject, $"Add {typeof(T).Name}");
        screen.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);
        screen.gameObject.SetActive(false);
        SetRef(flow, flowField, screen);
        FontRoles.ApplyAll(screen.transform);
        UiSkin.ApplyAll(screen.transform);
      }

      Replace("_signup", title.transform, BuildSignup);
      Replace("_consent", title.transform, BuildConsent);
      Replace("_photoFrame", complete.transform, BuildPhotoFrame);
      BuildPhotoBooth(brushing, feed);

      // 완료 화면 버튼은 이제 사진 액자로 간다
      var next = (Button)new SerializedObject(complete).FindProperty("_nextButton").objectReferenceValue;
      var label = next != null ? next.GetComponentInChildren<Text>(true) : null;
      if (label != null)
      {
        Undo.RecordObject(label, "Complete Label");
        label.text = CompleteNextLabel;
      }

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 보호자 동의·회원가입, 양치 사진 액자를 만들고 씬을 저장했습니다.");
    }

    // ---------- 로비 버튼 자리 바꾸기 / 되돌리기 (팀원 PC에서도 MCP 메뉴 실행 한 번으로) ----------

    [MenuItem("BrushGame/Lobby/Sticker Button - Partner Room")]
    public static void StickerSlotPartner() => SetStickerSlot(GameFlow.StickerSlot.PartnerRoom);

    [MenuItem("BrushGame/Restore/Sticker Button - Sticker Board")]
    public static void StickerSlotBoard() => SetStickerSlot(GameFlow.StickerSlot.StickerBoard);

    [MenuItem("BrushGame/Lobby/Badge Button - Space Map")]
    public static void BadgeSlotMap() => SetBadgeSlot(GameFlow.BadgeSlot.SpaceMap);

    [MenuItem("BrushGame/Restore/Badge Button - Badge Box")]
    public static void BadgeSlotBox() => SetBadgeSlot(GameFlow.BadgeSlot.BadgeBox);

    /// <summary>스티커판 버튼 자리: GameFlow 스위치 + 로비 버튼 그림·글자를 같이 바꾸고 씬을 저장한다</summary>
    private static void SetStickerSlot(GameFlow.StickerSlot slot)
    {
      LoadCommonAssets();
      var partner = slot == GameFlow.StickerSlot.PartnerRoom;
      SetLobbySlot("_stickerSlot", (int)slot, "_stickerButton",
        partner ? "내 친구" : "스티커판",
        partner ? LoadArt(PartnerArtDir, "partner_icon") : LoadArt(IconArtDir, "Icon_Stickers"));
    }

    private static void SetBadgeSlot(GameFlow.BadgeSlot slot)
    {
      LoadCommonAssets();
      var map = slot == GameFlow.BadgeSlot.SpaceMap;
      SetLobbySlot("_badgeSlot", (int)slot, "_badgeButton",
        map ? "지도" : "반짝 배지",
        map ? LoadArt(MapArtDir, "map_icon") : LoadArt(IconArtDir, "Icon_Badge"));
    }

    private static void SetLobbySlot(string flowField, int value, string buttonField, string label, Sprite icon)
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var lobby = Object.FindFirstObjectByType<LobbyScreen>(FindObjectsInactive.Include);
      if (flow == null || lobby == null)
      {
        Debug.LogWarning($"[BrushGame] {ScenePath} 를 열고 실행하세요.");
        return;
      }
      var so = new SerializedObject(flow);
      so.FindProperty(flowField).enumValueIndex = value;
      so.ApplyModifiedProperties();

      var button = (Button)new SerializedObject(lobby).FindProperty(buttonField).objectReferenceValue;
      var text = button.GetComponentInChildren<Text>(true);
      Undo.RecordObject(text, "Lobby Slot");
      text.text = label;
      var iconImage = button.transform.Find("Icon")?.GetComponent<Image>();
      if (iconImage != null && icon != null)
      {
        Undo.RecordObject(iconImage, "Lobby Slot");
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
      }
      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log($"[BrushGame] 로비 {buttonField} 자리: {label}");
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
      _star = LoadOrCreateShape("Star.png", InStar);
      _heart = LoadOrCreateShape("Heart.png", InHeart);
      _check = LoadOrCreateShape("Check.png", InCheck);
      _font = LoadFont();
    }

    // 장식 모양 (u, v는 0~1, v는 아래가 0)
    private static bool InStar(float u, float v)
    {
      var inside = false;
      const int points = 10;
      for (int i = 0, j = points - 1; i < points; j = i++)
      {
        var a = StarVertex(i);
        var b = StarVertex(j);
        if ((a.y > v) != (b.y > v) && u < (b.x - a.x) * (v - a.y) / (b.y - a.y) + a.x)
        {
          inside = !inside;
        }
      }
      return inside;
    }

    private static Vector2 StarVertex(int i)
    {
      var r = i % 2 == 0 ? 0.49f : 0.22f;
      var angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
      return new Vector2(0.5f + r * Mathf.Cos(angle), 0.47f + r * Mathf.Sin(angle));
    }

    private static bool InHeart(float u, float v)
    {
      var x = (u - 0.5f) * 2.5f;
      var y = (v - 0.44f) * 2.5f;
      var k = x * x + y * y - 1f;
      return k * k * k - x * x * y * y * y <= 0f;
    }

    private static bool InCheck(float u, float v)
    {
      var p = new Vector2(u, v);
      return Mathf.Min(SegmentDistance(p, new Vector2(0.18f, 0.52f), new Vector2(0.42f, 0.28f)),
        SegmentDistance(p, new Vector2(0.42f, 0.28f), new Vector2(0.82f, 0.74f))) < 0.085f;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
      var ab = b - a;
      var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
      return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>모양 함수로 흰색 투명 배경 그림을 만든다 (4x4 샘플로 가장자리를 부드럽게)</summary>
    private static Sprite LoadOrCreateShape(string file, Func<float, float, bool> inside, int size = 256)
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
            var hits = 0;
            for (var sy = 0; sy < 4; sy++)
            {
              for (var sx = 0; sx < 4; sx++)
              {
                if (inside((x + (sx + 0.5f) / 4f) / size, (y + (sy + 0.5f) / 4f) / size))
                {
                  hits++;
                }
              }
            }
            px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
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
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
      }
      return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
        if (BattleHeroes.TryGetValue(c.id, out var hero))
        {
          linked += LinkBattle(c, hero);
        }
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
      foreach (var b in catalog.badges)
      {
        linked += Link(ref b.icon, BadgeArtDir, $"Badge_{Capitalize(b.id)}");
      }
      EditorUtility.SetDirty(catalog);
      AssetDatabase.SaveAssets();
      Debug.Log($"[BrushGame] 카탈로그에 그림 {linked}개 연결");
    }

    /// <summary>
    ///   캐릭터 id → 양치 화면 친구 그림 이름. 여기 없는 캐릭터는 씬에 들어 있는 기본 친구(토끼, Hero_*)를 쓴다.
    ///   그림: Art/Battle/{이름}/Frames/{이름}_Idle1.png, _Attack01~, _Hurt1~, _Win1~ (1024 캔버스, 발이 아래 가운데)
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> BattleHeroes = new()
    {
      ["prince"] = "Hero2",
    };

    private static int LinkBattle(PlayerCharacter character, string hero)
    {
      if (character.battle != null && !character.battle.IsEmpty)
      {
        return 0;
      }
      var dir = $"{BattleArtDir}/{hero}/Frames";
      if (!AssetDatabase.IsValidFolder(dir))
      {
        return 0;
      }
      Sprite[] Load(string action) => AssetDatabase.FindAssets($"{hero}_{action} t:Sprite", new[] { dir })
        .Select(AssetDatabase.GUIDToAssetPath)
        .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith($"{hero}_{action}"))
        .OrderBy(p => p)
        .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
        .ToArray();
      character.battle = new BattleView.Frames
      {
        idle = Load("Idle"),
        action = Load("Attack"),
        stopped = Load("Hurt"),
        finish = Load("Win"),
      };
      return character.battle.IsEmpty ? 0 : 1;
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

    /// <summary>
    ///   소리 담당 오브젝트. 소리 파일은 Sound 폴더의 정해진 이름으로 연결하고, 이미 넣은 칸은 건드리지 않는다.
    ///   background_Sound(기본 배경음), Game_play_Sound(양치 배경음), touch, Clear, Gargle
    /// </summary>
    private static SoundManager BuildSound()
    {
      var sound = Object.FindFirstObjectByType<SoundManager>();
      if (sound == null)
      {
        var go = new GameObject("Sound");
        Undo.RegisterCreatedObjectUndo(go, "Add Sound");
        sound = go.AddComponent<SoundManager>();
      }
      var so = new SerializedObject(sound);
      foreach (var (property, file) in new[]
               {
                 ("_menuMusic", "background_Sound"), ("_gameplayMusic", "Game_play_Sound"),
                 ("_touch", "touch"), ("_clear", "Clear"), ("_gargle", "Gargle"),
               })
      {
        var slot = so.FindProperty(property);
        if (slot.objectReferenceValue == null)
        {
          slot.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/{file}.wav");
        }
      }
      so.ApplyModifiedProperties();
      return sound;
    }

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
      var header = Txt("Header", card.transform, new Vector2(0f, 250f), new Vector2(800f, 70f), "아침에 온 친구", 44, Primary, bold: true);
      var portrait = Img("PatientArt", card.transform, new Vector2(0f, 40f), new Vector2(340f, 340f), Peach, _circle);
      var patientName = Txt("PatientName", card.transform, new Vector2(0f, -170f), new Vector2(800f, 80f), "", 54, Dark, bold: true);
      var patientLine = Txt("PatientLine", card.transform, new Vector2(0f, -250f), new Vector2(820f, 80f), "", 34, Grey);

      var treat = Btn("TreatButton", c, new Vector2(0f, -380f), new Vector2(800f, 170f), "치료 시작", Green, Color.white, 64);
      var bookButton = NavButton("BookButton", c, -378f, "환자 도감", "Icon_Book");
      var stickerButton = NavButton("StickerButton", c, -126f, "스티커판", "Icon_Stickers");
      // 반짝 배지 자리는 우주 항해 지도가 맡는다 (필드 이름은 그대로 _badgeButton)
      var badgeButton = NavButton("BadgeButton", c, 126f, "지도", "map_icon", MapArtDir);
      var settingsButton = NavButton("SettingsButton", c, 378f, "보호자 설정", "Icon_Settings");

      var screen = root.gameObject.AddComponent<LobbyScreen>();
      SetRef(screen, "_avatar", avatar);
      SetRef(screen, "_nickname", nickname);
      SetArray(screen, "_todayStickers", todayStickers);
      SetRef(screen, "_patientHeader", header);
      SetRef(screen, "_patientPortrait", portrait);
      SetRef(screen, "_patientName", patientName);
      SetRef(screen, "_patientLine", patientLine);
      SetRef(screen, "_treatButton", treat);
      SetRef(screen, "_bookButton", bookButton);
      SetRef(screen, "_stickerButton", stickerButton);
      SetRef(screen, "_settingsButton", settingsButton);
      SetRef(screen, "_badgeButton", badgeButton);
      BuildPracticeConfirm(screen);
      return screen;
    }

    /// <summary>뒤로가기 종료 확인 창. 모든 화면과 보호자 설정보다 위에 둔다</summary>
    internal static QuitConfirmPanel BuildQuitConfirm(Transform canvasRoot)
    {
      LoadCommonAssets();
      var root = Stretch("QuitConfirm", canvasRoot);
      var panel = Stretch("Panel", root);
      panel.gameObject.AddComponent<Image>().color = Dim;
      var box = Img("Box", panel, Vector2.zero, new Vector2(820f, 520f), Color.white, _rounded);
      Txt("Message", box.transform, new Vector2(0f, 100f), new Vector2(760f, 200f), "게임을 끝낼까요?", 52, Dark, bold: true);
      var quit = Btn("QuitButton", box.transform, new Vector2(-190f, -140f), new Vector2(330f, 130f), "끝내기", Lavender, Dark, 48);
      var stay = Btn("StayButton", box.transform, new Vector2(190f, -140f), new Vector2(330f, 130f), "계속하기", Green, Color.white, 48);
      panel.gameObject.SetActive(false);

      var comp = root.gameObject.AddComponent<QuitConfirmPanel>();
      SetRef(comp, "_panel", panel.gameObject);
      SetRef(comp, "_quitButton", quit);
      SetRef(comp, "_stayButton", stay);
      return comp;
    }

    /// <summary>로비 아래 메뉴 버튼: 위에 그림, 아래에 이름 (그림이 없으면 이름만 가운데)</summary>
    internal static Button NavButton(string name, Transform parent, float x, string label, string iconName, string iconDir = IconArtDir)
    {
      LoadCommonAssets();
      var size = new Vector2(235f, 230f);
      var button = Btn(name, parent, new Vector2(x, -700f), size, label, Lavender, Dark, 34);
      var icon = LoadArt(iconDir, iconName);
      if (icon != null)
      {
        ArtSlot.Apply(Img("Icon", button.transform, new Vector2(0f, 28f), new Vector2(140f, 140f), Color.white, null), icon);
        var text = button.GetComponentInChildren<Text>(true).rectTransform;
        text.anchoredPosition = new Vector2(0f, -78f);
        text.sizeDelta = new Vector2(235f, 60f);
      }
      return button;
    }

    /// <summary>이미 도장을 받은 끼니에 치료 시작을 누르면 뜨는 확인 창. 문구는 LobbyScreen이 채운다</summary>
    internal static void BuildPracticeConfirm(LobbyScreen screen)
    {
      var old = screen.transform.Find("PracticeConfirm");
      if (old != null)
      {
        Undo.DestroyObjectImmediate(old.gameObject);
      }
      LoadCommonAssets();
      var confirm = Stretch("PracticeConfirm", screen.transform);
      confirm.gameObject.AddComponent<Image>().color = Dim;
      var box = Img("Box", confirm, Vector2.zero, new Vector2(820f, 520f), Color.white, _rounded);
      var message = Txt("Message", box.transform, new Vector2(0f, 100f), new Vector2(760f, 200f), "", 46, Dark, bold: true);
      var cancel = Btn("CancelButton", box.transform, new Vector2(-190f, -140f), new Vector2(330f, 130f), "다음에 할래", Lavender, Dark, 44);
      var practice = Btn("PracticeButton", box.transform, new Vector2(190f, -140f), new Vector2(330f, 130f), "연습할래", Green, Color.white, 48);
      confirm.gameObject.SetActive(false);

      SetRef(screen, "_practicePanel", confirm.gameObject);
      SetRef(screen, "_practiceMessage", message);
      SetRef(screen, "_practiceButton", practice);
      SetRef(screen, "_practiceCancelButton", cancel);
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
      var title = Txt("Title", c, new Vector2(0f, 760f), new Vector2(1000f, 100f), "아침에 온 친구", 60, Dark, bold: true);
      var portrait = Img("PatientArt", c, new Vector2(0f, 220f), new Vector2(620f, 620f), Peach, _circle);
      var patientName = Txt("PatientName", c, new Vector2(0f, -160f), new Vector2(900f, 90f), "", 64, Primary, bold: true);
      var bubble = Img("Bubble", c, new Vector2(0f, -330f), new Vector2(900f, 200f), Peach, _rounded);
      var line = Txt("Line", bubble.transform, Vector2.zero, new Vector2(840f, 180f), "", 44, Dark, bold: true);
      var go = Btn("GoButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "치료하러 가기", Green, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<PatientIntroScreen>();
      SetRef(screen, "_title", title);
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
      var (root, c) = NewScreen("Loading", parent, new Color(0.16f, 0.12f, 0.35f));
      // 배경 → 흐르는 별 → 치과 → 우주선 → 글자 순서로 그린다
      var bg = Img("SpaceBg", root, Vector2.zero, Vector2.zero, Color.white, null);
      Fit(bg.rectTransform);
      bg.transform.SetSiblingIndex(0);
      var bgArt = LoadArt(LoadingArtDir, "Loading_BG");
      if (bgArt != null)
      {
        bg.sprite = bgArt;
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = bgArt.rect.width / bgArt.rect.height;
      }
      else
      {
        bg.color = new Color(0.16f, 0.12f, 0.35f);
      }
      var stars = Stretch("StarField", root);
      stars.SetSiblingIndex(1);
      var clinic = Img("Clinic", c, new Vector2(0f, 430f), new Vector2(560f, 560f), Mint, _circle);
      ArtSlot.Apply(clinic, LoadArt(LoadingArtDir, "Loading_Clinic"));
      var rocket = Img("Rocket", c, new Vector2(0f, -400f), new Vector2(320f, 320f), Peach, _circle);
      ArtSlot.Apply(rocket, LoadArt(LoadingArtDir, "Loading_Rocket"));
      var message = Txt("Message", c, new Vector2(0f, -640f), new Vector2(1000f, 100f), "우주 치과로 날아가는 중...", 56, Color.white, bold: true);
      Outlined(message, new Color(0.29f, 0.23f, 0.55f), 4f);
      var bar = Img("Bar", c, new Vector2(0f, -740f), new Vector2(700f, 44f), new Color(1f, 1f, 1f, 0.35f), _rounded, ppu: 3f);
      var fill = Img("Fill", bar.transform, Vector2.zero, Vector2.zero, Gold, _rounded, ppu: 3f);
      var fillRt = fill.rectTransform;
      fillRt.anchorMin = Vector2.zero;
      fillRt.anchorMax = new Vector2(0f, 1f);
      fillRt.offsetMin = Vector2.zero;
      fillRt.offsetMax = Vector2.zero;

      var screen = root.gameObject.AddComponent<LoadingScreen>();
      SetRef(screen, "_barFill", fillRt);
      SetRef(screen, "_message", message);
      SetRef(screen, "_rocket", rocket.rectTransform);
      SetRef(screen, "_clinic", clinic.rectTransform);
      SetRef(screen, "_starField", stars);
      SetRef(screen, "_starSprite", _circle);
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
      IconButton(pause, LoadArt(HudArtDir, "Hud_Pause"));
      var sound = Btn("SoundButton", c, new Vector2(-100f, -100f), new Vector2(130f, 130f), "소리\n켬", Color.white, Dark, 30, TopRight, _circle);
      var soundOn = LoadArt(HudArtDir, "Hud_SoundOn");
      IconButton(sound, soundOn);
      var timerPill = Img("TimerPill", c, new Vector2(0f, -90f), new Vector2(300f, 110f), Primary, _rounded, Top);
      var timerIcon = LoadArt(HudArtDir, "Hud_Timer");
      if (timerIcon != null)
      {
        ArtSlot.Apply(Img("TimerIcon", timerPill.transform, new Vector2(-112f, 4f), new Vector2(96f, 96f), Color.white, null), timerIcon);
      }
      var timer = Txt("Timer", timerPill.transform, new Vector2(timerIcon != null ? 36f : 0f, 0f), new Vector2(220f, 110f), "0:25", 60, Color.white, bold: true);
      var dotsRoot = Node("ZoneDots", c, new Vector2(0f, -185f), new Vector2(300f, 40f), Top);
      var dots = new Object[4];
      var dotFills = new Object[4];
      var starArt = LoadArt(HudArtDir, "Hud_Star");
      var dotSize = starArt != null ? 54f : 36f;
      for (var i = 0; i < 4; i++)
      {
        var dot = Img($"Dot{i}", dotsRoot, new Vector2(-90f + 60f * i, 0f), new Vector2(dotSize, dotSize), new Color(1f, 1f, 1f, 0.7f), _circle);
        var fill = Img("Fill", dot.transform, Vector2.zero, new Vector2(dotSize, dotSize), Gold, _circle);
        if (starArt != null)
        {
          // 아직 안 닦은 구역은 흐린 별, 다 닦은 구역은 금색 별
          ArtSlot.Apply(dot, starArt);
          dot.color = new Color(0.2f, 0.15f, 0.35f, 0.45f);
          ArtSlot.Apply(fill, starArt);
        }
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
      SetRef(screen, "_soundLabel", sound.GetComponentInChildren<Text>(true));
      if (soundOn != null)
      {
        SetRef(screen, "_soundIcon", sound.targetGraphic);
        SetRef(screen, "_soundOnSprite", soundOn);
        SetRef(screen, "_soundOffSprite", LoadArt(HudArtDir, "Hud_SoundOff"));
      }
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
      var toothArt = LoadArt(HudArtDir, "Hud_Tooth");
      foreach (var x in xs)
      {
        var tooth = Img("Tooth", root, new Vector2(x, 0f), new Vector2(140f, 210f), Color.white, _rounded);
        if (toothArt != null)
        {
          ArtSlot.Apply(tooth, toothArt);
          tooth.rectTransform.sizeDelta = new Vector2(160f, 220f);
          // 그림은 뿌리가 아래라서, 윗니는 뒤집어 뿌리가 잇몸(위)으로 가게 한다
          tooth.rectTransform.localScale = new Vector3(1f, upper ? -1f : 1f, 1f);
        }
      }
      Color[] dirtColors = { new Color(1f, 0.85f, 0.35f), new Color(0.7f, 0.88f, 0.4f), new Color(0.8f, 0.62f, 1f) };
      string[] dirtArt = { "Hud_Plaque", "Hud_Food", "Hud_Germ" };
      var gumSide = upper ? 1f : -1f;
      var dirt = new Object[xs.Length];
      var foam = new Object[xs.Length];
      for (var i = 0; i < xs.Length; i++)
      {
        var d = Img("Dirt", root, new Vector2(xs[i] + (i - 1) * 8f, gumSide * 50f), new Vector2(96f, 64f), dirtColors[i], _circle);
        var art = LoadArt(HudArtDir, dirtArt[i]);
        if (art != null)
        {
          ArtSlot.Apply(d, art);
          d.rectTransform.sizeDelta = new Vector2(104f, 104f);
        }
        dirt[i] = d;
      }
      var foamArt = LoadArt(HudArtDir, "Hud_Foam");
      for (var i = 0; i < xs.Length; i++)
      {
        var f = Img("Foam", root, new Vector2(xs[i], -gumSide * 10f), new Vector2(140f, 140f), new Color(1f, 1f, 1f, 0f), _circle);
        ArtSlot.Apply(f, foamArt);
        foam[i] = f;
      }
      var sparkleArt = LoadArt(UiArtDir, "FX_Sparkle");
      var sparkles = new Object[]
      {
        Img("Sparkle", root, new Vector2(-90f, 60f), new Vector2(64f, 64f), new Color(1f, 0.95f, 0.5f, 0f), _circle),
        Img("Sparkle", root, new Vector2(110f, -50f), new Vector2(64f, 64f), new Color(1f, 0.95f, 0.5f, 0f), _circle),
      };
      foreach (Image s in sparkles)
      {
        if (sparkleArt != null)
        {
          ArtSlot.Apply(s, sparkleArt);
          s.rectTransform.sizeDelta = new Vector2(96f, 96f);
        }
      }

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
      var next = Btn("NextButton", c, new Vector2(0f, -700f), new Vector2(760f, 160f), CompleteNextLabel, Green, Color.white, 56);

      var screen = root.gameObject.AddComponent<CompleteScreen>();
      SetRef(screen, "_before", before);
      SetRef(screen, "_after", after);
      SetRef(screen, "_thanks", thanks);
      SetRef(screen, "_nextButton", next);
      return screen;
    }

    // ---------- 보호자 동의 · 회원가입 ----------

    private const string PrivacyTerms =
      "- 수집 항목: 보호자 이름과 이메일, 아이 나이와 닉네임, 양치 기록\n" +
      "- 이용 목적: 회원 확인, 양치 기록과 칭찬 도장 관리\n" +
      "- 보관 장소: 이 기기 안에만 저장 (서버로 보내지 않아요)\n" +
      "- 보관 기간: 보호자 설정의 '초기화' 또는 앱 삭제 시까지\n" +
      "- 동의하지 않으면 게임을 시작할 수 없어요";

    private const string CameraTerms =
      "- 이용 목적: 칫솔 든 손의 움직임 인식, 양치하는 모습 사진 4장 촬영\n" +
      "- 손 인식은 기기 안에서만 처리되고, 영상은 저장하지 않아요\n" +
      "- <b><color=#E0508A>촬영한 사진은 이 기기에만 저장되며, 서버로 보내거나 서버에 저장하지 않아요</color></b>\n" +
      "- 사진은 보호자 설정의 '초기화'로 언제든 지울 수 있어요\n" +
      "- 기기가 카메라 권한을 물어보면 '허용'을 눌러 주세요";

    private const string CompleteNextLabel = "양치 사진 보기";

    private static ConsentScreen BuildConsent(Transform parent)
    {
      var (root, c) = NewScreen("Consent", parent, Cream);
      Txt("Title", c, new Vector2(0f, 800f), new Vector2(1000f, 100f), "보호자 동의", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 720f), new Vector2(1000f, 70f), "게임을 시작하기 전에 보호자님이 읽고 체크해 주세요", 36, Grey);

      var all = CheckRow("AgreeAll", c, new Vector2(0f, 610f), new Vector2(960f, 130f), "전체 동의하기", 50, Color.white, out var allMark);

      var privacyPanel = Img("PrivacyPanel", c, new Vector2(0f, 290f), new Vector2(960f, 440f), Color.white, _rounded);
      var privacy = CheckRow("AgreePrivacy", privacyPanel.transform, new Vector2(0f, 155f), new Vector2(920f, 110f), "[필수] 개인정보 수집·이용 동의", 40, Lavender, out var privacyMark);
      Txt("Terms", privacyPanel.transform, new Vector2(0f, -65f), new Vector2(880f, 290f), PrivacyTerms, 36, Dark, TextAnchor.UpperLeft).lineSpacing = 1.15f;

      var cameraPanel = Img("CameraPanel", c, new Vector2(0f, -230f), new Vector2(960f, 560f), Color.white, _rounded);
      var camera = CheckRow("AgreeCamera", cameraPanel.transform, new Vector2(0f, 215f), new Vector2(920f, 110f), "[필수] 카메라 권한 이용 동의", 40, Lavender, out var cameraMark);
      Txt("Terms", cameraPanel.transform, new Vector2(0f, -65f), new Vector2(880f, 410f), CameraTerms, 36, Dark, TextAnchor.UpperLeft).lineSpacing = 1.15f;

      var next = Btn("NextButton", c, new Vector2(0f, -810f), new Vector2(760f, 150f), "동의하고 다음", Primary, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<ConsentScreen>();
      SetRef(screen, "_all.button", all);
      SetRef(screen, "_all.mark", allMark);
      SetRef(screen, "_privacy.button", privacy);
      SetRef(screen, "_privacy.mark", privacyMark);
      SetRef(screen, "_camera.button", camera);
      SetRef(screen, "_camera.mark", cameraMark);
      SetRef(screen, "_nextButton", next);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    /// <summary>왼쪽에 동그란 체크 칸이 있는 한 줄 버튼. mark는 체크됐을 때만 켠다</summary>
    private static Button CheckRow(string name, Transform parent, Vector2 pos, Vector2 size, string label, int fontSize, Color background, out GameObject mark)
    {
      var button = Btn(name, parent, pos, size, "", background, Dark, fontSize);
      var labelText = button.GetComponentInChildren<Text>();
      labelText.text = label;
      labelText.alignment = TextAnchor.MiddleLeft;
      labelText.rectTransform.sizeDelta = new Vector2(size.x - 150f, size.y);
      labelText.rectTransform.anchoredPosition = new Vector2(60f, 0f);
      var box = Img("CheckBox", button.transform, new Vector2(-size.x * 0.5f + 70f, 0f), new Vector2(78f, 78f), new Color(0.85f, 0.82f, 0.95f), _circle);
      var on = Img("Checked", box.transform, Vector2.zero, new Vector2(78f, 78f), new Color(1f, 0.55f, 0.78f), _circle);
      Img("Check", on.transform, Vector2.zero, new Vector2(60f, 60f), Color.white, _check);
      on.gameObject.SetActive(false);
      mark = on.gameObject;
      return button;
    }

    private static SignupScreen BuildSignup(Transform parent)
    {
      var (root, c) = NewScreen("Signup", parent, Cream);
      Txt("Title", c, new Vector2(0f, 660f), new Vector2(1000f, 100f), "회원가입", 64, Dark, bold: true);
      Txt("Subtitle", c, new Vector2(0f, 580f), new Vector2(1000f, 70f), "보호자님의 정보를 입력해 주세요", 40, Grey);
      Txt("NameLabel", c, new Vector2(0f, 400f), new Vector2(800f, 70f), "보호자 이름", 40, Dark, TextAnchor.MiddleLeft, bold: true);
      var nameInput = InputBox("NameInput", c, new Vector2(0f, 300f), new Vector2(800f, 140f), "이름", SignupScreen.NameMaxLength);
      Txt("EmailLabel", c, new Vector2(0f, 140f), new Vector2(800f, 70f), "이메일", 40, Dark, TextAnchor.MiddleLeft, bold: true);
      var emailInput = InputBox("EmailInput", c, new Vector2(0f, 40f), new Vector2(800f, 140f), "example@email.com", SignupScreen.EmailMaxLength);
      var note = Txt("Note", c, new Vector2(0f, -90f), new Vector2(900f, 70f), "", 34, Grey);
      var done = Btn("DoneButton", c, new Vector2(0f, -720f), new Vector2(760f, 160f), "가입하기", Primary, Color.white, 56);
      var back = BackButton(c);

      var screen = root.gameObject.AddComponent<SignupScreen>();
      SetRef(screen, "_nameInput", nameInput);
      SetRef(screen, "_emailInput", emailInput);
      SetRef(screen, "_note", note);
      SetRef(screen, "_doneButton", done);
      SetRef(screen, "_backButton", back);
      return screen;
    }

    // ---------- 양치 사진 액자 ----------

    private enum Deco { Heart, Star, Bubble, Cloud }

    /// <summary>사진 칸 모양. Polaroid는 아래 여백이 넓은 흰 종이 + 위에 붙인 테이프</summary>
    private enum PhotoShape { Rounded, Circle, Heart, Polaroid }

    /// <summary>사진 칸 하나: 가운데 위치, 사진 크기, 기울기, 모양</summary>
    private readonly struct PhotoSpot
    {
      public readonly Vector2 Pos;
      public readonly float Size;
      public readonly float Tilt;
      public readonly PhotoShape Shape;

      public PhotoSpot(float x, float y, float size, float tilt, PhotoShape shape)
      {
        Pos = new Vector2(x, y);
        Size = size;
        Tilt = tilt;
        Shape = shape;
      }
    }

    /// <summary>액자 하나. 액자를 늘리려면 여기에 하나 추가하면 된다 (사진 칸은 구역 순서대로 4개)</summary>
    private sealed class FrameTheme
    {
      public string Name;
      public Color Outer, Inner, Accent, Accent2;
      /// <summary>사진 칸 테두리 색</summary>
      public Color Border = Color.white;
      public Deco Deco;
      public bool Rainbow;
      public PhotoSpot[] Spots;
    }

    // 사진 칸은 액자 가운데 기준. 제목 띠(위 475~565)와 날짜 띠(아래 -470~-540) 사이에 둔다
    private static readonly FrameTheme[] FrameThemes =
    {
      // 딸기우유: 하트 사진 넷, 서로 마주 보게 기울인다
      new FrameTheme
      {
        Name = "Strawberry", Outer = Hex("FFB8D4"), Inner = Hex("FFEAF3"), Accent = Hex("FF6FA8"), Accent2 = Color.white,
        Deco = Deco.Heart,
        Spots = new[]
        {
          new PhotoSpot(-205f, 240f, 420f, 8f, PhotoShape.Heart), new PhotoSpot(205f, 240f, 420f, -8f, PhotoShape.Heart),
          new PhotoSpot(-205f, -200f, 420f, -8f, PhotoShape.Heart), new PhotoSpot(205f, -200f, 420f, 8f, PhotoShape.Heart),
        },
      },
      // 반짝우주: 크기가 다른 동그란 행성 창문이 지그재그로
      new FrameTheme
      {
        Name = "Space", Outer = Hex("4B3C9E"), Inner = Hex("6D5CCB"), Accent = Hex("FFE07A"), Accent2 = Hex("FFB3E0"),
        Border = Hex("FFE07A"), Deco = Deco.Star,
        Spots = new[]
        {
          new PhotoSpot(-170f, 255f, 400f, 0f, PhotoShape.Circle), new PhotoSpot(225f, 150f, 290f, 0f, PhotoShape.Circle),
          new PhotoSpot(-225f, -160f, 290f, 0f, PhotoShape.Circle), new PhotoSpot(170f, -255f, 400f, 0f, PhotoShape.Circle),
        },
      },
      // 민트거품: 첫 사진을 크게, 나머지는 아래에 방울 셋
      new FrameTheme
      {
        Name = "Bubble", Outer = Hex("8EDDC6"), Inner = Hex("E3FAF2"), Accent = Color.white, Accent2 = Hex("6CC7E8"),
        Deco = Deco.Bubble,
        Spots = new[]
        {
          new PhotoSpot(0f, 175f, 560f, 0f, PhotoShape.Rounded), new PhotoSpot(-285f, -320f, 240f, 0f, PhotoShape.Circle),
          new PhotoSpot(0f, -340f, 240f, 0f, PhotoShape.Circle), new PhotoSpot(285f, -320f, 240f, 0f, PhotoShape.Circle),
        },
      },
      // 무지개: 테이프로 붙인 폴라로이드를 흩어 놓은 콜라주
      new FrameTheme
      {
        Name = "Rainbow", Outer = Hex("FFF4CC"), Inner = Hex("FFFBEA"), Accent = Hex("FFC845"), Accent2 = Color.white,
        Deco = Deco.Cloud, Rainbow = true,
        Spots = new[]
        {
          new PhotoSpot(-175f, 225f, 330f, -8f, PhotoShape.Polaroid), new PhotoSpot(185f, 190f, 310f, 7f, PhotoShape.Polaroid),
          new PhotoSpot(-170f, -215f, 310f, 6f, PhotoShape.Polaroid), new PhotoSpot(185f, -185f, 330f, -6f, PhotoShape.Polaroid),
        },
      },
    };

    private static readonly Color[] RainbowColors =
    {
      Hex("FF9AA2"), Hex("FFB870"), Hex("FDE57A"), Hex("8FDC8F"), Hex("89CFF0"), Hex("B9A3E3"),
    };

    private static readonly Color[] TapeColors = { Hex("FFB3C7"), Hex("A8E0FF"), Hex("FFE68A"), Hex("B8F0C0") };

    // 장식 자리 (액자 가운데 기준): 위치, 크기, 기울기
    private static readonly (Vector2 pos, float size, float tilt)[] DecoSpots =
    {
      (new Vector2(-400f, 560f), 96f, -12f), (new Vector2(400f, 560f), 84f, 14f),
      (new Vector2(-405f, -560f), 80f, 10f), (new Vector2(405f, -560f), 96f, -8f),
      (new Vector2(-447f, 0f), 64f, 8f), (new Vector2(447f, 0f), 64f, -10f),
      (new Vector2(-447f, 300f), 50f, 0f), (new Vector2(447f, -300f), 50f, 0f),
      (new Vector2(-447f, -300f), 44f, 20f), (new Vector2(447f, 300f), 44f, -20f),
      (new Vector2(-270f, -578f), 46f, -15f), (new Vector2(270f, -578f), 46f, 15f),
    };

    private static Color Hex(string hex)
    {
      ColorUtility.TryParseHtmlString("#" + hex, out var color);
      return color;
    }

    private static PhotoFrameScreen BuildPhotoFrame(Transform parent)
    {
      var (root, c) = NewScreen("PhotoFrame", parent, Cream);
      Txt("Title", c, new Vector2(0f, 805f), new Vector2(1000f, 100f), "찰칵! 오늘의 양치 사진", 62, Dark, bold: true);

      var card = Node("FrameCard", c, new Vector2(0f, 80f), new Vector2(940f, 1240f));
      var frames = new Object[FrameThemes.Length];
      var slots = new List<Object>();
      var photos = new List<Object>();
      var empties = new List<Object>();
      for (var i = 0; i < FrameThemes.Length; i++)
      {
        var theme = FrameThemes[i];
        var frame = BuildFrameTheme(card, theme);
        frames[i] = frame.gameObject;
        for (var p = 0; p < theme.Spots.Length; p++)
        {
          slots.Add(PhotoSlot(frame, $"Photo{p}", theme.Spots[p], theme.Border, TapeColors[(i + p) % TapeColors.Length], out var raw, out var empty));
          photos.Add(raw);
          empties.Add(empty);
        }
      }

      var caption = Img("CaptionPill", card, new Vector2(0f, 520f), new Vector2(660f, 92f), new Color(1f, 1f, 1f, 0.92f), _rounded, ppu: 0.5f);
      var captionText = Txt("Caption", caption.transform, Vector2.zero, new Vector2(640f, 92f), "", 42, Dark, bold: true);

      var date = Img("DatePill", card, new Vector2(0f, -505f), new Vector2(440f, 72f), new Color(1f, 1f, 1f, 0.92f), _rounded, ppu: 0.6f);
      var dateText = Txt("Date", date.transform, Vector2.zero, new Vector2(420f, 72f), "", 34, Grey, bold: true);

      Txt("LocalNote", c, new Vector2(0f, -583f), new Vector2(1000f, 50f), "사진은 이 기기에만 저장되고 서버에는 저장되지 않아요", 30, Grey);

      var buttons = new Object[FrameThemes.Length];
      var marks = new Object[FrameThemes.Length];
      for (var i = 0; i < FrameThemes.Length; i++)
      {
        var theme = FrameThemes[i];
        var pos = new Vector2(-240f + 160f * i, -680f);
        var mark = Img($"FrameMark{i}", c, pos, new Vector2(146f, 146f), new Color(1f, 0.55f, 0.78f), _circle);
        var button = Btn($"FrameButton{i}", c, pos, new Vector2(118f, 118f), "", theme.Outer, Dark, 20, sprite: _circle);
        Object.DestroyImmediate(button.GetComponentInChildren<Text>().gameObject);
        // 버튼 바탕은 UI 스킨이 흰 원으로 바꾸므로, 테두리 색은 안쪽 동그라미로 보여준다
        Img("Swatch", button.transform, Vector2.zero, new Vector2(96f, 96f), theme.Outer, _circle);
        DecoImage("Icon", button.transform, Vector2.zero, 56f, 0f, theme, 0);
        buttons[i] = button;
        marks[i] = mark.gameObject;
      }

      var next = Btn("NextButton", c, new Vector2(0f, -830f), new Vector2(760f, 150f), "칭찬 도장 받기", Green, Color.white, 56);

      var screen = root.gameObject.AddComponent<PhotoFrameScreen>();
      SetArray(screen, "_frames", frames);
      SetArray(screen, "_frameButtons", buttons);
      SetArray(screen, "_frameButtonMarks", marks);
      SetArray(screen, "_slots", slots.ToArray());
      SetArray(screen, "_photos", photos.ToArray());
      SetArray(screen, "_emptyPhotos", empties.ToArray());
      SetRef(screen, "_caption", captionText);
      SetRef(screen, "_date", dateText);
      SetRef(screen, "_nextButton", next);
      return screen;
    }

    /// <summary>
    ///   사진 칸 하나: 테두리 + 모양대로 잘린 사진(Mask) + 사진이 없을 때의 캐릭터 그림.
    ///   돌려받는 RectTransform은 튀어나오는 연출에 쓰는 칸 전체
    /// </summary>
    private static RectTransform PhotoSlot(Transform frame, string name, PhotoSpot spot, Color border, Color tape, out RawImage raw, out Image empty)
    {
      var size = spot.Size;
      var slot = Node(name, frame, spot.Pos, new Vector2(size, size));
      slot.localEulerAngles = new Vector3(0f, 0f, spot.Tilt);

      var shape = _rounded;
      var maskPos = Vector2.zero;
      var maskPpu = 1.4f;
      switch (spot.Shape)
      {
        case PhotoShape.Heart:
          shape = _heart;
          // 하트는 그림 안에서 살짝 위에 있어서 테두리를 조금 크게, 아래로
          Shadowed(Img("Border", slot, new Vector2(0f, -6f), new Vector2(size * 1.13f, size * 1.13f), border, _heart));
          break;
        case PhotoShape.Circle:
          shape = _circle;
          Shadowed(Img("Border", slot, Vector2.zero, new Vector2(size + 32f, size + 32f), border, _circle));
          break;
        case PhotoShape.Polaroid:
          maskPos = new Vector2(0f, 28f);
          maskPpu = 4f;
          Shadowed(Img("Border", slot, Vector2.zero, new Vector2(size + 36f, size + 100f), border, _rounded, ppu: 4f));
          break;
        default:
          Shadowed(Img("Border", slot, Vector2.zero, new Vector2(size + 36f, size + 36f), border, _rounded, ppu: 0.9f));
          break;
      }

      var maskSize = new Vector2(size, size);
      var mask = Img("Mask", slot, maskPos, maskSize, Color.white, shape, ppu: maskPpu);
      mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      var bg = Img("EmptyBg", mask.transform, Vector2.zero, Vector2.zero, Lavender, null);
      Fit(bg.rectTransform);
      raw = Node("Picture", mask.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
      Fit(raw.rectTransform);
      raw.raycastTarget = false;
      // 하트·동그라미는 가장자리가 잘리므로 캐릭터를 조금 작게
      var emptyScale = spot.Shape == PhotoShape.Heart ? 0.68f : spot.Shape == PhotoShape.Circle ? 0.8f : 0.9f;
      empty = Img("NoPicture", mask.transform, new Vector2(0f, spot.Shape == PhotoShape.Heart ? -size * 0.04f : 0f), maskSize * emptyScale, Mint, _circle);

      if (spot.Shape == PhotoShape.Polaroid)
      {
        var strip = Img("Tape", slot, new Vector2(0f, (size + 100f) * 0.5f - 6f), new Vector2(150f, 46f), new Color(tape.r, tape.g, tape.b, 0.85f), null);
        strip.rectTransform.localEulerAngles = new Vector3(0f, 0f, -spot.Tilt * 1.5f + 4f);
      }
      return slot;
    }

    private static void Shadowed(Image image)
    {
      image.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -8f);
    }

    private static RectTransform BuildFrameTheme(Transform card, FrameTheme theme)
    {
      var frame = Node($"Frame_{theme.Name}", card, Vector2.zero, new Vector2(940f, 1240f));
      Img("Outer", frame, Vector2.zero, new Vector2(940f, 1240f), theme.Outer, _rounded, ppu: 0.45f).gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -10f);
      if (theme.Rainbow)
      {
        // 바깥에서 안쪽으로 무지개 띠
        for (var r = 0; r < RainbowColors.Length; r++)
        {
          var inset = 12f + 13f * r;
          Img($"Stripe{r}", frame, Vector2.zero, new Vector2(940f - inset * 2f, 1240f - inset * 2f), RainbowColors[r], _rounded, ppu: 0.45f + 0.03f * r);
        }
      }
      var innerInset = theme.Rainbow ? 104f : 40f;
      Img("Inner", frame, Vector2.zero, new Vector2(940f - innerInset * 2f, 1240f - innerInset * 2f), theme.Inner, _rounded, ppu: 0.6f);
      for (var i = 0; i < DecoSpots.Length; i++)
      {
        var (pos, size, tilt) = DecoSpots[i];
        DecoImage($"Deco{i}", frame, pos, size, tilt, theme, i);
      }
      return frame;
    }

    /// <summary>테두리 장식 하나. 짝수 번째는 주 색, 홀수 번째는 보조 색/모양</summary>
    private static void DecoImage(string name, Transform parent, Vector2 pos, float size, float tilt, FrameTheme theme, int index)
    {
      var alt = index % 2 == 1;
      var color = alt ? theme.Accent2 : theme.Accent;
      Image img;
      switch (theme.Deco)
      {
        case Deco.Heart:
          img = Img(name, parent, pos, new Vector2(size, size), color, _heart);
          break;
        case Deco.Star when alt:
          // 우주: 별 사이사이 고리 없는 작은 행성
          img = Img(name, parent, pos, new Vector2(size * 0.8f, size * 0.8f), color, _circle);
          Img("Shine", img.transform, new Vector2(-size * 0.15f, size * 0.15f), new Vector2(size * 0.25f, size * 0.25f), new Color(1f, 1f, 1f, 0.7f), _circle);
          break;
        case Deco.Star:
          img = Img(name, parent, pos, new Vector2(size, size), color, _star);
          break;
        case Deco.Bubble:
          img = Img(name, parent, pos, new Vector2(size, size), new Color(color.r, color.g, color.b, 0.85f), _circle);
          Img("Shine", img.transform, new Vector2(-size * 0.18f, size * 0.18f), new Vector2(size * 0.28f, size * 0.28f), Color.white, _circle);
          break;
        default:
          if (alt)
          {
            // 구름: 동그라미 세 개
            img = Img(name, parent, pos, new Vector2(size * 0.7f, size * 0.7f), color, _circle);
            Img("Puff1", img.transform, new Vector2(-size * 0.38f, -size * 0.1f), new Vector2(size * 0.5f, size * 0.5f), color, _circle);
            Img("Puff2", img.transform, new Vector2(size * 0.38f, -size * 0.12f), new Vector2(size * 0.45f, size * 0.45f), color, _circle);
          }
          else
          {
            img = Img(name, parent, pos, new Vector2(size, size), color, _star);
          }
          break;
      }
      img.rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt);
    }

    /// <summary>양치 화면 카메라 작은 창에 사진 찍기(번쩍임 + 찰칵 글자)를 붙이고 양치 화면에 연결한다. 이미 있으면 다시 만든다</summary>
    private static void BuildPhotoBooth(BrushingScreen brushing, GameObject feed)
    {
      var window = new SerializedObject(brushing).FindProperty("_cameraWindow").objectReferenceValue as RectTransform;
      var host = window != null ? window.gameObject : brushing.gameObject;
      var old = host.GetComponent<PhotoBooth>();
      if (old != null)
      {
        Undo.DestroyObjectImmediate(old);
      }
      var oldFlash = window != null ? window.Find("PhotoFlash") : null;
      if (oldFlash != null)
      {
        Undo.DestroyObjectImmediate(oldFlash.gameObject);
      }

      var booth = Undo.AddComponent<PhotoBooth>(host);
      SetRef(booth, "_feed", feed.GetComponentInChildren<RawImage>(true));
      if (window != null)
      {
        var flash = Img("PhotoFlash", window, Vector2.zero, Vector2.zero, Color.white, null);
        Fit(flash.rectTransform);
        Txt("Label", flash.transform, Vector2.zero, new Vector2(300f, 100f), "찰칵!", 52, Primary, bold: true);
        flash.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(flash.gameObject, "Photo Flash");
        SetRef(booth, "_flash", flash);
      }
      SetRef(brushing, "_photoBooth", booth);
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
      BuildWeekArrows(screen);
      return screen;
    }

    /// <summary>스티커판의 지난주/다음주 화살표 (이번 주 도장 수 글자 양옆)</summary>
    internal static void BuildWeekArrows(StickerScreen screen)
    {
      LoadCommonAssets();
      var c = screen.transform.Find("Content");
      var prev = Btn("PrevWeekButton", c, new Vector2(-430f, 700f), new Vector2(110f, 110f), "<", Color.white, Dark, 56, sprite: _circle);
      var next = Btn("NextWeekButton", c, new Vector2(430f, 700f), new Vector2(110f, 110f), ">", Color.white, Dark, 56, sprite: _circle);
      SetRef(screen, "_prevWeekButton", prev);
      SetRef(screen, "_nextWeekButton", next);
    }

    /// <summary>반짝 배지함: 위 줄은 매일 양치(연속 일수), 아래 줄은 꽉 찬 도장판. 칸은 GameCatalog.badges 순서</summary>
    internal static BadgeScreen BuildBadges(Transform parent)
    {
      LoadCommonAssets();
      var (root, c) = NewScreen("Badges", parent, Cream);
      var title = Txt("Title", c, new Vector2(0f, 760f), new Vector2(1000f, 100f), "반짝 배지", 64, Dark, bold: true);
      var summary = Txt("Subtitle", c, new Vector2(0f, 680f), new Vector2(1000f, 70f), "", 38, Grey);
      Txt("StreakLabel", c, new Vector2(0f, 560f), new Vector2(1000f, 70f), "매일매일 양치하기", 44, Dark, bold: true);
      Txt("BoardLabel", c, new Vector2(0f, 20f), new Vector2(1000f, 70f), "도장판 꽉 채우기", 44, Dark, bold: true);

      var screen = root.gameObject.AddComponent<BadgeScreen>();
      var so = new SerializedObject(screen);
      var cards = so.FindProperty("_cards");
      cards.arraySize = 6;
      for (var i = 0; i < 6; i++)
      {
        var pos = new Vector2(-330f + 330f * (i % 3), i < 3 ? 300f : -240f);
        var card = Img($"Badge{i}", c, pos, new Vector2(310f, 440f), Color.white, _rounded);
        var icon = Img("Icon", card.transform, new Vector2(0f, 90f), new Vector2(200f, 200f), Gold, _circle);
        var iconLabel = Txt("IconLabel", icon.transform, Vector2.zero, new Vector2(200f, 120f), "", 60, Dark, bold: true);
        var badgeName = Txt("Name", card.transform, new Vector2(0f, -60f), new Vector2(300f, 60f), "", 32, Dark, bold: true);
        var info = Txt("Info", card.transform, new Vector2(0f, -150f), new Vector2(300f, 90f), "", 28, Grey);
        var newTag = Img("NewTag", card.transform, new Vector2(95f, 200f), new Vector2(150f, 60f), new Color(1f, 0.45f, 0.65f), _rounded);
        Txt("Label", newTag.transform, Vector2.zero, new Vector2(150f, 60f), "새 배지!", 28, Color.white, bold: true);
        newTag.gameObject.SetActive(false);

        var e = cards.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("root").objectReferenceValue = card.gameObject;
        e.FindPropertyRelative("icon").objectReferenceValue = icon;
        e.FindPropertyRelative("iconLabel").objectReferenceValue = iconLabel;
        e.FindPropertyRelative("name").objectReferenceValue = badgeName;
        e.FindPropertyRelative("info").objectReferenceValue = info;
        e.FindPropertyRelative("newTag").objectReferenceValue = newTag.gameObject;
      }
      so.FindProperty("_title").objectReferenceValue = title;
      so.FindProperty("_summary").objectReferenceValue = summary;
      so.FindProperty("_backButton").objectReferenceValue = BackButton(c);
      so.ApplyModifiedPropertiesWithoutUndo();
      return screen;
    }

    /// <summary>
    ///   우주 항해 지도: 세로 스크롤 하나에 배경·점선·발판·행성·우주선을 층으로 둔다.
    ///   행성·발판·점선은 화면을 열 때 SpaceMapScreen이 GameCatalog.planets 수에 맞춰 만든다.
    ///   그림은 Art/Map (map_bg, pad_off, pad_on, path_dot, ship_icon, planet_lock_sheet). 없으면 임시 도형.
    /// </summary>
    private static SpaceMapScreen BuildSpaceMap(Transform parent)
    {
      var (root, c) = NewScreen("SpaceMap", parent, new Color(0.16f, 0.13f, 0.3f));

      // 스크롤은 safe area 밖까지 화면 전체
      var scrollRt = Stretch("Scroll", root);
      scrollRt.SetSiblingIndex(0);
      var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
      var viewport = Stretch("Viewport", scrollRt);
      viewport.gameObject.AddComponent<RectMask2D>();
      viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
      var content = Node("Content", viewport, Vector2.zero, new Vector2(0f, 4000f));
      content.anchorMin = new Vector2(0f, 0f);
      content.anchorMax = new Vector2(1f, 0f);
      content.pivot = new Vector2(0.5f, 0f);
      content.sizeDelta = new Vector2(0f, 4000f);
      scroll.content = content;
      scroll.viewport = viewport;
      scroll.horizontal = false;
      scroll.vertical = true;
      scroll.movementType = ScrollRect.MovementType.Clamped;
      scroll.scrollSensitivity = 40f;

      var bg = Stretch("Background", content).gameObject.AddComponent<RawImage>();
      bg.raycastTarget = false;
      var bgSprite = LoadArt(MapArtDir, "map_bg");
      bg.texture = bgSprite != null ? bgSprite.texture : null;
      bg.color = bgSprite != null ? Color.white : new Color(0.75f, 0.68f, 0.95f);
      Layer("Dots", content);
      Layer("Pads", content);
      Layer("Planets", content);
      var ship = Img("Ship", content, Vector2.zero, new Vector2(130f, 130f), Color.white, LoadArt(MapArtDir, "ship_icon") ?? _circle, new Vector2(0.5f, 0f));
      ship.preserveAspect = true;

      // 위: 제목과 남은 칸
      var header = Img("Header", c, new Vector2(0f, 760f), new Vector2(940f, 220f), new Color(1f, 1f, 1f, 0.85f), _rounded);
      var title = Txt("Title", header.transform, new Vector2(0f, 40f), new Vector2(900f, 100f), "우주 항해 지도", 60, Dark, bold: true);
      var remaining = Txt("Remaining", header.transform, new Vector2(0f, -50f), new Vector2(900f, 70f), "", 42, Primary, bold: true);
      var back = Btn("BackButton", c, new Vector2(0f, -790f), new Vector2(800f, 160f), "로비로 돌아가기", Primary, Color.white, 56);

      // 개발용 버튼 (에디터·Development Build에서만 켜진다)
      var dev = Node("DevPanel", c, new Vector2(0f, 590f), new Vector2(1000f, 90f));
      string[] devLabels = { "+1", "+5", "+15", "초기화", "다시 보기", "해금만" };
      var devButtons = new Button[devLabels.Length];
      for (var i = 0; i < devLabels.Length; i++)
      {
        devButtons[i] = Btn($"Dev{i}", dev, new Vector2(-415f + 166f * i, 0f), new Vector2(156f, 80f), devLabels[i], new Color(0.2f, 0.2f, 0.25f, 0.85f), Color.white, 30);
      }

      var screen = root.gameObject.AddComponent<SpaceMapScreen>();
      var so = new SerializedObject(screen);
      so.FindProperty("_scroll").objectReferenceValue = scroll;
      so.FindProperty("_content").objectReferenceValue = content;
      so.FindProperty("_background").objectReferenceValue = bg;
      so.FindProperty("_dotLayer").objectReferenceValue = content.Find("Dots");
      so.FindProperty("_padLayer").objectReferenceValue = content.Find("Pads");
      so.FindProperty("_planetLayer").objectReferenceValue = content.Find("Planets");
      so.FindProperty("_ship").objectReferenceValue = ship.rectTransform;
      so.FindProperty("_title").objectReferenceValue = title;
      so.FindProperty("_remaining").objectReferenceValue = remaining;
      so.FindProperty("_backButton").objectReferenceValue = back;
      so.FindProperty("_padOff").objectReferenceValue = LoadArt(MapArtDir, "pad_off") ?? _rounded;
      so.FindProperty("_padOn").objectReferenceValue = LoadArt(MapArtDir, "pad_on") ?? _circle;
      so.FindProperty("_dot").objectReferenceValue = LoadArt(MapArtDir, "path_dot") ?? _circle;
      var frames = LoadLockFrames();
      var framesProp = so.FindProperty("_unlockFrames");
      framesProp.arraySize = frames.Length;
      for (var i = 0; i < frames.Length; i++)
      {
        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
      }
      so.FindProperty("_devPanel").objectReferenceValue = dev.gameObject;
      string[] devFields = { "_devAdd1", "_devAdd5", "_devAdd15", "_devReset", "_devReplay", "_devUnlock" };
      for (var i = 0; i < devFields.Length; i++)
      {
        so.FindProperty(devFields[i]).objectReferenceValue = devButtons[i];
      }
      so.ApplyModifiedPropertiesWithoutUndo();
      return screen;
    }

    /// <summary>지도 내용 안의 층 하나 (아래 가운데 기준 좌표)</summary>
    private static RectTransform Layer(string name, Transform parent)
    {
      var rt = Stretch(name, parent);
      return rt;
    }

    /// <summary>잠금 해제 프레임 planet_lock_00~10 (시트를 잘라 둔 것). 없으면 빈 배열</summary>
    private static Sprite[] LoadLockFrames()
    {
      return AssetDatabase.LoadAllAssetsAtPath($"{MapArtDir}/planet_lock_sheet.png")
        .OfType<Sprite>()
        .Where(s => s.name.StartsWith("planet_lock_"))
        .OrderBy(s => s.name)
        .ToArray();
    }

    /// <summary>
    ///   행성 데이터가 비어 있으면 Art/Map/planet_*.png로 PlanetData를 만들어 순서대로 넣는다.
    ///   순서는 아래(처음)부터. 바꾸려면 GameCatalog.planets 순서를 바꾸면 된다.
    /// </summary>
    private static void LinkPlanets(GameCatalog catalog)
    {
      if (catalog == null || (catalog.planets != null && catalog.planets.Length > 0))
      {
        return;
      }
      (string file, string name)[] order =
      {
        ("forest", "숲 행성"), ("ocean", "바다 행성"), ("desert", "사막 행성"),
        ("ice", "얼음 행성"), ("candy", "사탕 행성"), ("ring", "고리 행성"),
      };
      Directory.CreateDirectory(PlanetDataDir);
      var list = new List<PlanetData>();
      foreach (var (file, name) in order)
      {
        var path = $"{PlanetDataDir}/Planet_{Capitalize(file)}.asset";
        var data = AssetDatabase.LoadAssetAtPath<PlanetData>(path);
        if (data == null)
        {
          data = ScriptableObject.CreateInstance<PlanetData>();
          AssetDatabase.CreateAsset(data, path);
        }
        data.planetName = name;
        data.sprite = LoadArt(MapArtDir, $"planet_{file}");
        EditorUtility.SetDirty(data);
        list.Add(data);
      }
      catalog.planets = list.ToArray();
      EditorUtility.SetDirty(catalog);
      AssetDatabase.SaveAssets();
    }

    /// <summary>파트너 그림 불러오기 설정: 눈 모양은 9-slice(반원 모서리), 나머지는 기본 스프라이트</summary>
    private static void SetupPartnerArt()
    {
      var path = $"{PartnerArtDir}/eye_shape.png";
      var imp = AssetImporter.GetAtPath(path) as TextureImporter;
      if (imp != null && imp.spriteBorder != new Vector4(63, 63, 63, 63))
      {
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteBorder = new Vector4(63, 63, 63, 63);
        imp.SaveAndReimport();
      }
    }

    /// <summary>
    ///   파트너 방: 방 배경(Art/Partner/partner_room_bg) 위 바닥에 로봇.
    ///   로봇 = 몸 그림 한 장(partner_body) + 화면 위 코드 눈(PartnerEyes) + 소품 자리. 움직임은 PartnerRobot이 코드로 만든다.
    ///   화면 위치는 partner_body 그림 기준 (가운데 0.4985, 0.5959 / 크기 0.6172 x 0.2441, 아래가 0).
    /// </summary>
    private static PartnerRoomScreen BuildPartnerRoom(Transform parent)
    {
      var (root, c) = NewScreen("PartnerRoom", parent, Lavender);
      // 방과 로봇은 1080x1920 판 하나에 두고 판째로 화면을 덮게 키운다 (화면 비율이 달라도 바닥과 로봇이 어긋나지 않게)
      var stage = Node("Stage", root, Vector2.zero, new Vector2(1080f, 1920f));
      stage.gameObject.AddComponent<CoverFit>();
      stage.SetAsFirstSibling();
      var room = Img("Room", stage, Vector2.zero, Vector2.zero, Color.white, LoadArt(PartnerArtDir, "partner_room_bg"));
      Fit(room.rectTransform);

      const float floorY = 430f;   // 오른쪽 아래 밥그릇 버튼과 겹치지 않는 높이
      var robotSize = new Vector2(340f, 490f);
      var floor = Stretch("Floor", stage);
      var bottom = new Vector2(0.5f, 0f);
      var shadow = Img("Shadow", floor, new Vector2(0f, floorY - 6f), new Vector2(230f, 44f), new Color(0.35f, 0.24f, 0.55f, 0.28f), _circle, bottom);

      var mover = Node("Robot", floor, new Vector2(0f, floorY), robotSize, bottom);
      mover.pivot = bottom;
      var visual = Node("Visual", mover, Vector2.zero, robotSize, bottom);
      visual.pivot = bottom;
      var body = Img("Body", visual, Vector2.zero, Vector2.zero, Color.white, LoadArt(PartnerArtDir, "partner_body") ?? _circle);
      Fit(body.rectTransform);
      body.preserveAspect = true;
      body.raycastTarget = true;
      var touch = body.gameObject.AddComponent<Button>();
      touch.transition = Selectable.Transition.None;

      var screenAnchor = new Vector2(0.4985f, 0.5959f);
      var screen = Node("Screen", visual, Vector2.zero, new Vector2(robotSize.x * 0.6172f, robotSize.y * 0.2441f), screenAnchor);
      screen.gameObject.AddComponent<RectMask2D>();
      var eyeShape = LoadArt(PartnerArtDir, "eye_shape");
      var eyeGlow = LoadArt(PartnerArtDir, "eye_glow");
      var eyeHappy = LoadArt(PartnerArtDir, "eye_happy");
      Image EyePart(string name, Sprite sprite, bool sliced)
      {
        var img = Img(name, screen, Vector2.zero, new Vector2(30f, 50f), Color.white, sprite);
        if (sliced)
        {
          img.type = Image.Type.Sliced;
        }
        else
        {
          img.preserveAspect = !sliced && sprite == eyeHappy;
        }
        return img;
      }
      var leftGlow = EyePart("LeftGlow", eyeGlow, false);
      var rightGlow = EyePart("RightGlow", eyeGlow, false);
      var leftEye = EyePart("LeftEye", eyeShape, true);
      var rightEye = EyePart("RightEye", eyeShape, true);
      var leftHappy = EyePart("LeftHappy", eyeHappy, false);
      var rightHappy = EyePart("RightHappy", eyeHappy, false);
      var screenProps = Stretch("ScreenProps", screen);
      var headProps = Node("HeadProps", visual, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.82f));

      // 머리 위 배터리 (충전 기다림·충전 중에만). 채움은 배터리 안쪽에서 가로로 늘어난다
      var battery = Node("Battery", visual, new Vector2(0f, 20f), new Vector2(180f, 140f), new Vector2(0.5f, 1f));
      battery.pivot = new Vector2(0.5f, 0f);
      var batteryArt = Img("Art", battery, Vector2.zero, Vector2.zero, Color.white, LoadArt(PartnerArtDir, "prop_battery") ?? _rounded);
      Fit(batteryArt.rectTransform);
      batteryArt.preserveAspect = true;
      var batteryFill = Img("Fill", battery, new Vector2(28f, 0f), new Vector2(114f, 66f), new Color(1f, 0.45f, 0.45f), null, new Vector2(0f, 0.5f));
      batteryFill.rectTransform.pivot = new Vector2(0f, 0.5f);
      battery.gameObject.SetActive(false);

      // 창고 어둠: 로봇 자리만 밝은 원형 그라데이션 + 위에서 내려오는 빛줄기. 데려오면 서서히 사라진다
      var darkness = Node("Darkness", stage, Vector2.zero, Vector2.zero);
      Fit(darkness);
      var darkGroup = darkness.gameObject.AddComponent<CanvasGroup>();
      darkGroup.blocksRaycasts = false;
      darkGroup.interactable = false;
      Img("Spot", darkness, new Vector2(0f, floorY + 230f), new Vector2(2600f, 2600f), Color.white, LoadArt(PartnerArtDir, "fx_spotlight"), bottom);
      var beam = Img("Beam", darkness, new Vector2(0f, floorY - 20f), new Vector2(520f, 1000f), Color.white, LoadArt(PartnerArtDir, "fx_beam"), bottom);
      beam.rectTransform.pivot = bottom;
      darkness.gameObject.SetActive(false);

      var eyes = screen.gameObject.AddComponent<PartnerEyes>();
      var eso = new SerializedObject(eyes);
      eso.FindProperty("_screen").objectReferenceValue = screen;
      eso.FindProperty("_leftEye").objectReferenceValue = leftEye;
      eso.FindProperty("_rightEye").objectReferenceValue = rightEye;
      eso.FindProperty("_leftGlow").objectReferenceValue = leftGlow;
      eso.FindProperty("_rightGlow").objectReferenceValue = rightGlow;
      eso.FindProperty("_leftHappy").objectReferenceValue = leftHappy;
      eso.FindProperty("_rightHappy").objectReferenceValue = rightHappy;
      eso.ApplyModifiedPropertiesWithoutUndo();

      var robot = floor.gameObject.AddComponent<PartnerRobot>();
      var rso = new SerializedObject(robot);
      rso.FindProperty("_mover").objectReferenceValue = mover;
      rso.FindProperty("_visual").objectReferenceValue = visual;
      rso.FindProperty("_shadow").objectReferenceValue = shadow;
      rso.FindProperty("_eyes").objectReferenceValue = eyes;
      rso.FindProperty("_screenProps").objectReferenceValue = screenProps;
      rso.FindProperty("_headProps").objectReferenceValue = headProps;
      rso.FindProperty("_touch").objectReferenceValue = touch;
      rso.FindProperty("_note").objectReferenceValue = LoadArt(PartnerArtDir, "prop_note");
      rso.FindProperty("_ball").objectReferenceValue = LoadArt(PartnerArtDir, "prop_ball");
      rso.FindProperty("_font").objectReferenceValue = _font;
      rso.FindProperty("_grain").objectReferenceValue = LoadArt(PartnerArtDir, "eye_shape");
      var bowls = new[] { "full", "half", "empty" }.Select(n => LoadArt(PartnerArtDir, $"prop_bowl_{n}")).Where(s => s != null).ToArray();
      var bowlProp = rso.FindProperty("_bowls");
      bowlProp.arraySize = bowls.Length;
      for (var i = 0; i < bowls.Length; i++)
      {
        bowlProp.GetArrayElementAtIndex(i).objectReferenceValue = bowls[i];
      }
      var fireworks = Enumerable.Range(0, 12).Select(i => LoadArt(PartnerArtDir, $"prop_firework_{i:00}")).Where(s => s != null).ToArray();
      var fw = rso.FindProperty("_fireworks");
      fw.arraySize = fireworks.Length;
      for (var i = 0; i < fireworks.Length; i++)
      {
        fw.GetArrayElementAtIndex(i).objectReferenceValue = fireworks[i];
      }
      rso.ApplyModifiedPropertiesWithoutUndo();

      // 뒤로가기: 화살표 그림 버튼 (다른 화면의 뒤로가기와 같은 왼쪽 위)
      var back = ArtButton("BackButton", c, new Vector2(110f, -110f), new Vector2(150f, 150f), LoadArt(PartnerArtDir, "ui_back"), TopLeft);
      // 밥 주기: 밥그릇 그림 버튼 (오른쪽 아래). 양치 도장 하나가 밥 한 그릇, 남은 수는 분홍 동그라미
      var bottomRight = new Vector2(1f, 0f);
      var feed = ArtButton("FeedButton", c, new Vector2(-190f, 160f), new Vector2(250f, 187f), LoadArt(PartnerArtDir, "ui_rice_button"), bottomRight);
      var badge = Img("Count", feed.transform, new Vector2(98f, 68f), new Vector2(84f, 84f), new Color(1f, 0.45f, 0.65f), _circle);
      var count = Txt("Number", badge.transform, Vector2.zero, new Vector2(84f, 84f), "0", 46, Color.white, bold: true);
      var hint = Txt("NoMealHint", c, new Vector2(-190f, 40f), new Vector2(380f, 56f), "양치하면 밥이 생겨요", 30, Dark, bold: true, anchor: bottomRight);

      // 첫 만남: 데려가기 버튼, 이름 짓기 창, 위쪽 안내 말풍선 (말풍선이 가장 위)
      var bring = Btn("BringButton", c, new Vector2(0f, -560f), new Vector2(600f, 150f), "데려가기", Green, Color.white, 56);
      bring.gameObject.SetActive(false);
      var namePanel = Stretch("NamePanel", c);
      namePanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
      var nameBox = Img("Box", namePanel, new Vector2(0f, -40f), new Vector2(860f, 520f), Color.white, _rounded);
      Txt("Title", nameBox.transform, new Vector2(0f, 170f), new Vector2(800f, 90f), "이름을 지어 줘!", 54, Dark, bold: true);
      var nameInput = InputBox("NameInput", nameBox.transform, new Vector2(0f, 20f), new Vector2(700f, 140f), $"이름 ({PartnerRoomScreen.NameMaxLength}글자까지)");
      var nameOk = Btn("NameOk", nameBox.transform, new Vector2(0f, -170f), new Vector2(420f, 130f), "좋아!", Primary, Color.white, 52);
      namePanel.gameObject.SetActive(false);
      var bubble = Img("Bubble", c, new Vector2(0f, 600f), new Vector2(900f, 210f), new Color(1f, 1f, 1f, 0.94f), _rounded);
      var bubbleText = Txt("Text", bubble.transform, Vector2.zero, new Vector2(860f, 190f), "", 46, Dark, bold: true);
      bubble.gameObject.SetActive(false);

      // 개발용 버튼 (에디터·Development Build에서만 켜진다): 첫 만남 다시 보기. 뒤로 버튼 옆 맨 윗줄 (말풍선을 가리지 않게)
      var dev = Node("DevPanel", c, new Vector2(90f, 850f), new Vector2(860f, 90f));
      string[] devLabels = { "창고부터", "깨우기", "이름 다시", "밥 +3" };
      var devButtons = new Button[devLabels.Length];
      for (var i = 0; i < devLabels.Length; i++)
      {
        devButtons[i] = Btn($"Dev{i}", dev, new Vector2(215f * (i - 1.5f), 0f), new Vector2(200f, 80f), devLabels[i], new Color(0.2f, 0.2f, 0.25f, 0.85f), Color.white, 30);
      }

      var screenComp = root.gameObject.AddComponent<PartnerRoomScreen>();
      SetRef(screenComp, "_devPanel", dev.gameObject);
      SetRef(screenComp, "_devStorage", devButtons[0]);
      SetRef(screenComp, "_devWake", devButtons[1]);
      SetRef(screenComp, "_devRename", devButtons[2]);
      SetRef(screenComp, "_devMeals", devButtons[3]);
      SetRef(screenComp, "_darkness", darkGroup);
      SetRef(screenComp, "_bubble", bubble.gameObject);
      SetRef(screenComp, "_bubbleText", bubbleText);
      SetRef(screenComp, "_bringButton", bring);
      SetRef(screenComp, "_battery", battery.gameObject);
      SetRef(screenComp, "_batteryFill", batteryFill.rectTransform);
      SetRef(screenComp, "_batteryFillImage", batteryFill);
      SetRef(screenComp, "_namePanel", namePanel.gameObject);
      SetRef(screenComp, "_nameInput", nameInput);
      SetRef(screenComp, "_nameOkButton", nameOk);
      SetRef(screenComp, "_robot", robot);
      SetRef(screenComp, "_backButton", back);
      SetRef(screenComp, "_feedButton", feed);
      SetRef(screenComp, "_countBadge", badge.gameObject);
      SetRef(screenComp, "_countText", count);
      SetRef(screenComp, "_noMealHint", hint.gameObject);
      return screenComp;
    }

    /// <summary>그림 한 장짜리 버튼 (누르면 살짝 어두워지고, 못 누를 땐 흐리게)</summary>
    private static Button ArtButton(string name, Transform parent, Vector2 pos, Vector2 size, Sprite art, Vector2 anchor)
    {
      var img = Img(name, parent, pos, size, Color.white, art ?? _circle, anchor);
      img.preserveAspect = true;
      img.raycastTarget = true;
      var button = img.gameObject.AddComponent<Button>();
      button.targetGraphic = img;
      var colors = button.colors;
      colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
      colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
      button.colors = colors;
      return button;
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

    /// <summary>
    ///   보호자 설정 창: 위쪽 탭(사운드, 게임 설정)과 탭마다 페이지 하나. 탭을 늘리려면 tabNames에 이름을 더하고 페이지를 만든다.
    ///   확인 팝업 하나를 양치 시간 변경과 초기화가 같이 쓴다.
    ///   그림(Art/UI/UI_SettingsPanel, Art/Icons/Icon_*, Art/Hud/Hud_Sound*)이 없으면 임시 도형과 글자로 만든다.
    /// </summary>
    private static GuardianSettingsPanel BuildSettings(Transform parent)
    {
      var frameArt = LoadArt(UiArtDir, "UI_SettingsPanel");
      var soundOn = LoadArt(HudArtDir, "Hud_SoundOn");
      var soundOff = LoadArt(HudArtDir, "Hud_SoundOff");

      var root = Stretch("GuardianSettings", parent);
      var dim = root.gameObject.AddComponent<Image>();
      dim.color = Dim;
      var panel = Frame("Panel", root, new Vector2(1000f, 1340f), frameArt);
      var p = panel.transform;
      Txt("Title", p, new Vector2(0f, 490f), new Vector2(760f, 90f), "보호자 설정", 58, Dark, bold: true);

      // 탭: 왼쪽부터 차례로 (나중에 계정 연동 같은 탭이 붙을 수 있다)
      var tabNames = new[] { "사운드", "게임 설정" };
      var tabIcons = new[] { soundOn, LoadArt(IconArtDir, "Icon_TabGame") };
      var tabButtons = new Button[tabNames.Length];
      for (var i = 0; i < tabNames.Length; i++)
      {
        var tab = Btn($"Tab{i}", p, new Vector2(-260f + 270f * i, 380f), new Vector2(250f, 96f), tabNames[i], Lavender, Dark, 38);
        if (tabIcons[i] != null)
        {
          ArtSlot.Apply(Img("Icon", tab.transform, new Vector2(-82f, 2f), new Vector2(64f, 64f), Color.white, null), tabIcons[i]);
          tab.GetComponentInChildren<Text>().rectTransform.anchoredPosition = new Vector2(30f, 0f);
        }
        tabButtons[i] = tab;
      }
      Img("TabLine", p, new Vector2(0f, 315f), new Vector2(780f, 6f), new Color(0.85f, 0.8f, 0.97f), null);

      // ---- 사운드 탭 ----
      var soundPage = Node("SoundPage", p, Vector2.zero, new Vector2(1000f, 1340f));
      var master = VolumeRow("Master", soundPage, 200f, "전체", soundOn, soundOn);
      var music = VolumeRow("Music", soundPage, 65f, "배경음", LoadArt(IconArtDir, "Icon_Music"), soundOn);
      var effect = VolumeRow("Effect", soundPage, -70f, "효과음", LoadArt(IconArtDir, "Icon_Bell"), soundOn);
      RowIcon("VoiceIcon", soundPage, new Vector2(-335f, -215f), LoadArt(IconArtDir, "Icon_Voice"));
      Txt("VoiceLabel", soundPage, new Vector2(-120f, -215f), new Vector2(300f, 110f), "안내 음성\n(준비 중)", 36, Dark, TextAnchor.MiddleLeft, true);
      var voice = Btn("VoiceButton", soundPage, new Vector2(290f, -215f), new Vector2(170f, 84f), "켜짐", Primary, Color.white, 34);

      // ---- 게임 설정 탭 ----
      var gamePage = Node("GamePage", p, Vector2.zero, new Vector2(1000f, 1340f));
      RowIcon("ZoneIcon", gamePage, new Vector2(-230f, 200f), LoadArt(HudArtDir, "Hud_Timer"));
      Txt("ZoneLabel", gamePage, new Vector2(40f, 200f), new Vector2(460f, 80f), "구역별 양치 시간", 44, Dark, TextAnchor.MiddleLeft, true);
      var input = InputBox("ZoneSecondsInput", gamePage, new Vector2(-130f, 60f), new Vector2(280f, 120f), GuardianSettings.DefaultZoneSeconds.ToString());
      input.characterLimit = 2;
      input.contentType = InputField.ContentType.IntegerNumber;
      input.textComponent.alignment = TextAnchor.MiddleCenter;
      ((Text)input.placeholder).alignment = TextAnchor.MiddleCenter;
      ((Image)input.targetGraphic).color = new Color(0.96f, 0.94f, 1f);
      Txt("Unit", gamePage, new Vector2(55f, 60f), new Vector2(80f, 100f), "초", 48, Dark, bold: true);
      var apply = Btn("ZoneSecondsApply", gamePage, new Vector2(250f, 60f), new Vector2(200f, 110f), "변경", Primary, Color.white, 44);
      var note = Txt("ZoneSecondsNote", gamePage, new Vector2(0f, -50f), new Vector2(760f, 60f),
        $"{GuardianSettings.MinZoneSeconds}~{GuardianSettings.MaxZoneSeconds}초 사이 숫자를 넣어주세요", 28, Grey);
      // 실수로 누르지 않게 오른쪽 아래에 작게
      var reset = Btn("ResetButton", gamePage, new Vector2(290f, -405f), new Vector2(170f, 64f), "초기화", Red, Color.white, 28);
      gamePage.gameObject.SetActive(false);

      var close = Btn("CloseButton", p, new Vector2(0f, -515f), new Vector2(560f, 120f), "닫기", Primary, Color.white, 50);

      var confirm = Stretch("Confirm", root);
      confirm.gameObject.AddComponent<Image>().color = Dim;
      var box = Frame("Box", confirm, new Vector2(880f, 600f), frameArt);
      var message = Txt("Message", box.transform, new Vector2(0f, 80f), new Vector2(680f, 220f), "", 42, Dark, bold: true);
      var cancel = Btn("CancelButton", box.transform, new Vector2(-170f, -140f), new Vector2(290f, 120f), "취소", Lavender, Dark, 46);
      var ok = Btn("OkButton", box.transform, new Vector2(170f, -140f), new Vector2(290f, 120f), "확인", Primary, Color.white, 46);
      confirm.gameObject.SetActive(false);

      var panelComp = root.gameObject.AddComponent<GuardianSettingsPanel>();
      var so = new SerializedObject(panelComp);
      var tabs = so.FindProperty("_tabs");
      tabs.arraySize = tabNames.Length;
      var pages = new[] { soundPage.gameObject, gamePage.gameObject };
      for (var i = 0; i < tabNames.Length; i++)
      {
        tabs.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue = tabButtons[i];
        tabs.GetArrayElementAtIndex(i).FindPropertyRelative("page").objectReferenceValue = pages[i];
      }
      foreach (var (field, row) in new[] { ("_master", master), ("_music", music), ("_effect", effect) })
      {
        so.FindProperty($"{field}.slider").objectReferenceValue = row.slider;
        so.FindProperty($"{field}.muteButton").objectReferenceValue = row.mute;
        var label = row.mute.GetComponentInChildren<Text>(true);
        so.FindProperty($"{field}.muteLabel").objectReferenceValue = label != null && label.gameObject.activeSelf ? label : null;
        so.FindProperty($"{field}.muteIcon").objectReferenceValue = soundOn != null ? row.mute.targetGraphic : null;
      }
      so.FindProperty("_soundOnIcon").objectReferenceValue = soundOn;
      so.FindProperty("_soundOffIcon").objectReferenceValue = soundOff;
      so.ApplyModifiedPropertiesWithoutUndo();
      SetRef(panelComp, "_voiceButton", voice);
      SetRef(panelComp, "_voiceLabel", voice.GetComponentInChildren<Text>());
      SetRef(panelComp, "_zoneSecondsInput", input);
      SetRef(panelComp, "_zoneSecondsApply", apply);
      SetRef(panelComp, "_zoneSecondsNote", note);
      SetRef(panelComp, "_resetButton", reset);
      SetRef(panelComp, "_closeButton", close);
      SetRef(panelComp, "_confirmPanel", confirm.gameObject);
      SetRef(panelComp, "_confirmMessage", message);
      SetRef(panelComp, "_confirmOkButton", ok);
      SetRef(panelComp, "_confirmOkLabel", ok.GetComponentInChildren<Text>());
      SetRef(panelComp, "_confirmCancelButton", cancel);
      return panelComp;
    }

    /// <summary>창 판. 테두리 그림(9-slice)이 있으면 그것을, 없으면 흰 둥근 판</summary>
    private static Image Frame(string name, Transform parent, Vector2 size, Sprite art)
    {
      if (art == null)
      {
        return Img(name, parent, Vector2.zero, size, Color.white, _rounded);
      }
      var frame = Img(name, parent, Vector2.zero, size, Color.white, art);
      frame.type = Image.Type.Sliced;
      // 그림 테두리(약 170px)가 창에서 130px 정도로 보이게
      frame.pixelsPerUnitMultiplier = 1.3f;
      frame.raycastTarget = true;
      return frame;
    }

    private static void RowIcon(string name, Transform parent, Vector2 pos, Sprite art)
    {
      if (art != null)
      {
        ArtSlot.Apply(Img(name, parent, pos, new Vector2(84f, 84f), Color.white, null), art);
      }
    }

    /// <summary>아이콘 + 이름 + 볼륨 슬라이더 + 음소거 버튼 한 줄. 켬/끔 아이콘이 있으면 음소거 버튼을 아이콘으로</summary>
    private static (Slider slider, Button mute) VolumeRow(string name, Transform parent, float y, string label, Sprite icon, Sprite muteArt)
    {
      RowIcon($"{name}Icon", parent, new Vector2(-335f, y), icon);
      Txt($"{name}Label", parent, new Vector2(-195f, y), new Vector2(170f, 90f), label, 40, Dark, TextAnchor.MiddleLeft, true);
      var slider = VolumeSlider($"{name}Slider", parent, new Vector2(65f, y), new Vector2(300f, 72f));
      var mute = Btn($"{name}Mute", parent, new Vector2(300f, y), new Vector2(muteArt != null ? 100f : 170f, muteArt != null ? 100f : 84f), "켜짐", Primary, Color.white, 34);
      IconButton(mute, muteArt);
      return (slider, mute);
    }

    private static Slider VolumeSlider(string name, Transform parent, Vector2 pos, Vector2 size)
    {
      // 손잡이: 행성처럼 동그란 UFO (없으면 동그라미). 막대보다 조금 크게
      var ufo = LoadArt(IconArtDir, "Icon_Ufo");
      var handleHeight = ufo != null ? size.y + 14f : size.y;
      var handleWidth = ufo != null ? handleHeight * ufo.rect.width / ufo.rect.height : size.y;
      var root = Node(name, parent, pos, size);
      // 손잡이 가운데가 움직이는 범위와 채움 영역을 같은 폭으로 잡아야 채움 끝이 손잡이 가운데와 맞는다.
      // UFO는 막대보다 커서 양끝에서는 막대 밖으로 조금 나간다
      var inset = size.y / 2f + 4f;
      const float barHeight = 28f;
      var track = Img("Track", root, Vector2.zero, new Vector2(0f, barHeight), new Color(0.86f, 0.82f, 0.97f), _rounded, ppu: 4f);
      track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
      track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
      track.rectTransform.sizeDelta = new Vector2(-inset, barHeight);
      var fillArea = Node("FillArea", root, Vector2.zero, new Vector2(-inset * 2f, barHeight));
      fillArea.anchorMin = new Vector2(0f, 0.5f);
      fillArea.anchorMax = new Vector2(1f, 0.5f);
      var fill = Img("Fill", fillArea, Vector2.zero, Vector2.zero, new Color(1f, 0.5f, 0.75f), _rounded, ppu: 4f);
      var handleArea = Stretch("HandleArea", root);
      handleArea.offsetMin = new Vector2(inset, 0f);
      handleArea.offsetMax = new Vector2(-inset, 0f);
      var handle = Img("Handle", handleArea, Vector2.zero, new Vector2(handleWidth, handleHeight - size.y), Color.white, _circle);
      handle.raycastTarget = true;
      if (ufo != null)
      {
        handle.sprite = ufo;
        handle.preserveAspect = true;
      }
      else
      {
        handle.gameObject.AddComponent<Shadow>().effectColor = new Color(0.2f, 0.1f, 0.4f, 0.35f);
      }

      var slider = root.gameObject.AddComponent<Slider>();
      slider.fillRect = fill.rectTransform;
      slider.handleRect = handle.rectTransform;
      slider.targetGraphic = handle;
      slider.direction = Slider.Direction.LeftToRight;
      slider.minValue = 0f;
      slider.maxValue = 1f;
      slider.value = 1f;
      // 슬라이더가 정한 앵커는 그대로 두고, 채움은 막대 왼쪽 끝부터 시작하게
      fill.rectTransform.offsetMin = new Vector2(-inset / 2f, 0f);
      fill.rectTransform.offsetMax = Vector2.zero;
      if (ufo != null)
      {
        // UFO 그림은 위에 돔이 있어 고리가 가운데보다 아래(약 60% 높이)에 있다. 고리가 막대에 오도록 올린다
        handle.rectTransform.anchoredPosition = new Vector2(0f, handleHeight * 0.095f);
      }
      return slider;
    }

    /// <summary>열려 있는 Main 씬의 보호자 설정 창만 새로 만들어 GameFlow에 다시 연결하고 저장한다</summary>
    [MenuItem("BrushGame/Rebuild Settings Panel")]
    public static void RebuildSettings()
    {
      var flow = Object.FindFirstObjectByType<GameFlow>(FindObjectsInactive.Include);
      var old = Object.FindFirstObjectByType<GuardianSettingsPanel>(FindObjectsInactive.Include);
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

      var panel = BuildSettings(parent);
      Undo.RegisterCreatedObjectUndo(panel.gameObject, "Rebuild Settings Panel");
      panel.transform.SetSiblingIndex(index);
      panel.gameObject.SetActive(active);
      SetRef(flow, "_settings", panel);
      FontRoles.ApplyAll(panel.transform);
      UiSkin.ApplyAll(panel.transform);

      var scene = flow.gameObject.scene;
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      Debug.Log("[BrushGame] 보호자 설정 창을 다시 만들고 씬을 저장했습니다.");
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

    /// <summary>버튼 전체를 그림으로 바꾸고 글자는 숨긴다. 그림이 없으면 그대로 둔다</summary>
    private static void IconButton(Button button, Sprite art)
    {
      if (art == null)
      {
        return;
      }
      ArtSlot.Apply((Image)button.targetGraphic, art);
      button.GetComponentInChildren<Text>(true).gameObject.SetActive(false);
    }

    /// <summary>그림이 아직 없으면 null (자리는 임시 도형으로 남는다)</summary>
    private static Sprite LoadArt(string dir, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

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

    private static InputField InputBox(string name, Transform parent, Vector2 pos, Vector2 size, string placeholder, int limit = NicknameScreen.MaxLength)
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
      field.characterLimit = limit;
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
