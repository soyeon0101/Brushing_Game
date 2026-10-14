using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   우주 항해 지도 (로비의 반짝 배지 버튼 자리, 치료 후에는 스티커판 다음). 도장 1개 = 발판 1개, 발판 padsPerPlanet개를 채우면 다음 행성이 열린다.
  ///   양치는 다른 화면에서 하므로, 새로 켜질 발판과 행성 해금 연출은 지도를 열 때 몰아서 보여준다.
  ///   연출 중 화면을 누르면 남은 연출을 건너뛰고 최종 상태로 정리한다.
  ///   행성·발판·점선은 GameCatalog.planets 수에 맞춰 화면을 열 때 만든다 (행성을 추가하면 지도가 자동으로 길어진다).
  /// </summary>
  public class SpaceMapScreen : ScreenBase
  {
    [Header("화면")]
    [SerializeField] private ScrollRect _scroll;
    [Tooltip("지도 내용 (아래 가운데 기준으로 행성·발판을 놓는다)")]
    [SerializeField] private RectTransform _content;
    [Tooltip("세로로 반복되는 우주 배경")]
    [SerializeField] private RawImage _background;
    [SerializeField] private RectTransform _dotLayer;
    [SerializeField] private RectTransform _padLayer;
    [SerializeField] private RectTransform _planetLayer;
    [SerializeField] private RectTransform _ship;
    [SerializeField] private Text _title;
    [SerializeField] private Text _remaining;
    [SerializeField] private Button _backButton;

    [Header("그림")]
    [SerializeField] private Sprite _padOff;
    [SerializeField] private Sprite _padOn;
    [SerializeField] private Sprite _dot;
    [Tooltip("잠금 해제 프레임 (planet_lock_00~10). 0번이 잠긴 모습")]
    [SerializeField] private Sprite[] _unlockFrames;

    [Header("배치")]
    [SerializeField, Min(1)] private int _padsPerPlanet = 15;
    [SerializeField, Min(100f)] private float _segmentHeight = 1800f;
    [Tooltip("행성이 가운데에서 좌우로 떨어진 거리")]
    [SerializeField] private float _horizontalOffset = 230f;
    [Tooltip("S자로 휘는 정도 (0이면 거의 직선)")]
    [SerializeField, Range(0f, 3f)] private float _curveStrength = 2f;
    [Tooltip("점선 점 간격")]
    [SerializeField, Min(4f)] private float _dotSpacing = 34f;
    [SerializeField] private float _bottomMargin = 700f;
    [SerializeField] private float _topMargin = 900f;
    [SerializeField, Min(10f)] private float _planetSize = 380f;
    [SerializeField, Min(10f)] private float _padSize = 95f;
    [SerializeField, Min(1f)] private float _dotSize = 16f;
    [Tooltip("점선 색 (밝은 배경에서도 보이게)")]
    [SerializeField] private Color _dotColor = new Color(0.55f, 0.42f, 0.9f, 0.85f);
    [Tooltip("행성 중심에서 이만큼은 발판·점선을 놓지 않는다")]
    [SerializeField] private float _planetClearance = 280f;

    [Header("행성 상태")]
    [Tooltip("잠긴 행성 색. 검은 실루엣으로 가려 \"다음 행성은 어떤 곳일까?\"를 만든다 (열릴 때 원래 색으로 밝아진다)")]
    [SerializeField] private Color _lockedTint = Color.black;
    [Tooltip("잠금 표시 크기 (행성 지름 배수). 사슬 X자가 행성을 덮을 정도")]
    [SerializeField, Min(0.1f)] private float _lockScale = 1.2f;
    [Tooltip("다음 해금까지 이만큼 이하로 남으면 잠금 표시가 흔들린다")]
    [SerializeField, Min(0)] private int _hintPads = 3;
    [Tooltip("흔들림 프레임(0 ↔ 1) 바뀌는 간격 (초)")]
    [SerializeField, Min(0.05f)] private float _hintFrameSec = 0.5f;

    [Header("연출")]
    [Tooltip("잠금 해제 재생 속도 (초당 프레임)")]
    [SerializeField, Min(1f)] private float _unlockFps = 12f;
    [Tooltip("이 프레임부터 행성 어두움이 풀린다")]
    [SerializeField, Min(0)] private int _brightenFromFrame = 5;
    [Tooltip("발판 하나 켜는 시간 (초)")]
    [SerializeField, Min(0.05f)] private float _padSec = 0.3f;
    [SerializeField, Min(0.05f)] private float _scrollSec = 0.45f;

    [Header("개발용 (에디터·Development Build에서만 보인다)")]
    [SerializeField] private GameObject _devPanel;
    [SerializeField] private Button _devAdd1;
    [SerializeField] private Button _devAdd5;
    [SerializeField] private Button _devAdd15;
    [SerializeField] private Button _devReset;
    [SerializeField] private Button _devReplay;
    [SerializeField] private Button _devUnlock;

    private class PlanetView
    {
      public RectTransform root;
      public Image image;
      public Image lockMark;
      public bool unlocked;
    }

    private readonly List<PlanetView> _planets = new List<PlanetView>();
    private readonly List<Image> _pads = new List<Image>();
    private MapPath _path;
    private GameCatalog _catalog;
    private SaveData _data;
    private int _shownPads;
    private Coroutine _sequence;
    private bool _skip;

    public event Action BackPressed;

    private int MaxPads => Mathf.Max(0, (_planets.Count - 1) * _padsPerPlanet);
    private static bool DevMode => Application.isEditor || Debug.isDebugBuild;

    public void Setup(GameCatalog catalog, SaveData data)
    {
      _catalog = catalog;
      _data = data;
    }

    public override void OnShow()
    {
      Build();
      _devPanel.SetActive(DevMode);
      Play();
    }

    public override void OnHide()
    {
      Finish();
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
      _devAdd1.onClick.AddListener(() => DevAddPads(1));
      _devAdd5.onClick.AddListener(() => DevAddPads(5));
      _devAdd15.onClick.AddListener(() => DevAddPads(_padsPerPlanet));
      _devReset.onClick.AddListener(DevReset);
      _devReplay.onClick.AddListener(DevReplay);
      _devUnlock.onClick.AddListener(DevUnlockOnly);
    }

    private void Update()
    {
      // 연출 중 화면을 누르면 남은 연출을 건너뛴다
      if (_sequence != null && Input.GetMouseButtonDown(0))
      {
        _skip = true;
      }
      UpdateHint();
    }

    // ---------- 지도 만들기 ----------

    private void Build()
    {
      foreach (Transform layer in new Transform[] { _dotLayer, _padLayer, _planetLayer })
      {
        for (var i = layer.childCount - 1; i >= 0; i--)
        {
          Destroy(layer.GetChild(i).gameObject);
        }
      }
      _planets.Clear();
      _pads.Clear();

      var planets = _catalog != null && _catalog.planets != null ? _catalog.planets : Array.Empty<PlanetData>();
      _path = new MapPath(new MapPath.Settings
      {
        planetCount = Mathf.Max(1, planets.Length),
        padsPerPlanet = _padsPerPlanet,
        segmentHeight = _segmentHeight,
        horizontalOffset = _horizontalOffset,
        curveStrength = _curveStrength,
        dotSpacing = _dotSpacing,
        bottomMargin = _bottomMargin,
        topMargin = _topMargin,
        planetClearance = _planetClearance,
      });

      _content.sizeDelta = new Vector2(_content.sizeDelta.x, _path.Height);
      // 배경 그림의 가로를 화면 너비에 맞추고, 세로는 그 비율대로 반복한다
      if (_background.texture != null)
      {
        var tileHeight = _content.rect.width * _background.texture.height / _background.texture.width;
        _background.uvRect = new Rect(0f, 0f, 1f, _path.Height / Mathf.Max(1f, tileHeight));
      }

      foreach (var dot in _path.dots)
      {
        var img = NewImage("Dot", _dotLayer, dot.position, _dotSize, _dot);
        img.color = _dotColor;
      }
      foreach (var pad in _path.pads)
      {
        var img = NewImage("Pad", _padLayer, pad.position, _padSize, _padOff);
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, pad.angle);
        _pads.Add(img);
      }
      for (var i = 0; i < _path.planets.Count; i++)
      {
        var data = i < planets.Length ? planets[i] : null;
        var root = NewNode(data != null ? data.planetName : "Planet", _planetLayer, _path.planets[i], _planetSize);
        var image = NewImage("Art", root, Vector2.zero, _planetSize, data != null ? data.sprite : null);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var mark = NewImage("Lock", root, Vector2.zero, _planetSize * _lockScale, Frame(0));
        mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        mark.rectTransform.anchoredPosition = LockOffset(_planetSize * _lockScale);
        _planets.Add(new PlanetView { root = root, image = image, lockMark = mark });
      }
      _ship.SetAsLastSibling();
    }

    private static RectTransform NewNode(string name, Transform parent, Vector2 pos, float size)
    {
      var go = new GameObject(name, typeof(RectTransform));
      var rt = (RectTransform)go.transform;
      rt.SetParent(parent, false);
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = new Vector2(size, size);
      return rt;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, float size, Sprite sprite)
    {
      var img = NewNode(name, parent, pos, size).gameObject.AddComponent<Image>();
      img.sprite = sprite;
      img.preserveAspect = true;
      img.raycastTarget = false;
      return img;
    }

    /// <summary>
    ///   UI Image는 스프라이트 피벗을 무시하므로, 피벗(자물쇠 몸통 중심)이 행성 가운데 오도록 그림 자리를 옮긴다.
    ///   모든 프레임이 같은 피벗이라 0번 프레임 기준으로 한 번만 계산한다.
    /// </summary>
    private Vector2 LockOffset(float size)
    {
      var frame = Frame(0);
      if (frame == null)
      {
        return Vector2.zero;
      }
      var pivot = new Vector2(frame.pivot.x / frame.rect.width, frame.pivot.y / frame.rect.height);
      return (new Vector2(0.5f, 0.5f) - pivot) * size;
    }

    private Sprite Frame(int index)
    {
      if (_unlockFrames == null || _unlockFrames.Length == 0)
      {
        return null;
      }
      return _unlockFrames[Mathf.Clamp(index, 0, _unlockFrames.Length - 1)];
    }

    // ---------- 상태 표시 ----------

    /// <summary>발판 lit개가 켜진 상태로 그린다 (연출 없이)</summary>
    private void ShowState(int lit)
    {
      _shownPads = Mathf.Clamp(lit, 0, MaxPads);
      for (var i = 0; i < _pads.Count; i++)
      {
        _pads[i].sprite = i < _shownPads ? _padOn : _padOff;
        _pads[i].rectTransform.localScale = Vector3.one;
      }
      for (var i = 0; i < _planets.Count; i++)
      {
        SetPlanetUnlocked(i, i == 0 || _shownPads >= i * _padsPerPlanet);
      }
      PlaceShip(_shownPads);
      RefreshRemaining();
    }

    /// <summary>열린 행성은 원래 색, 잠긴 행성은 모두 검은 실루엣 + 자물쇠</summary>
    private void SetPlanetUnlocked(int index, bool unlocked)
    {
      var p = _planets[index];
      p.unlocked = unlocked;
      p.image.color = unlocked ? Color.white : _lockedTint;
      p.root.localScale = Vector3.one;
      p.lockMark.gameObject.SetActive(!unlocked && Frame(0) != null);
      p.lockMark.sprite = Frame(0);
    }

    /// <summary>우주선은 마지막으로 켜진 발판 위. 아직 없으면 첫 행성 위</summary>
    private void PlaceShip(int lit)
    {
      if (lit <= 0 || _pads.Count == 0)
      {
        _ship.anchoredPosition = _path.planets[0] + new Vector2(0f, _planetSize * 0.55f);
        _ship.localEulerAngles = Vector3.zero;
        return;
      }
      var pad = _path.pads[Mathf.Min(lit, _pads.Count) - 1];
      _ship.anchoredPosition = pad.position;
      _ship.localEulerAngles = new Vector3(0f, 0f, pad.angle);
    }

    private void RefreshRemaining()
    {
      if (_shownPads >= MaxPads)
      {
        _remaining.text = MaxPads > 0 ? "모든 행성에 도착했어!" : "";
        return;
      }
      var left = _padsPerPlanet - _shownPads % _padsPerPlanet;
      _remaining.text = $"다음 행성까지 {left}칸";
    }

    /// <summary>다음 해금까지 _hintPads칸 이하면 그 행성의 잠금 표시가 0 ↔ 1번 프레임을 오간다</summary>
    private void UpdateHint()
    {
      // 연출 중에는 잠금 표시 프레임을 연출이 정한다
      if (_planets.Count == 0 || _sequence != null)
      {
        return;
      }
      var next = _shownPads / _padsPerPlanet + 1;
      var left = next * _padsPerPlanet - _shownPads;
      for (var i = 1; i < _planets.Count; i++)
      {
        var p = _planets[i];
        if (p.unlocked || !p.lockMark.gameObject.activeSelf || p.lockMark.sprite == null)
        {
          continue;
        }
        var shaking = i == next && left <= _hintPads;
        var frame = shaking && (int)(Time.unscaledTime / _hintFrameSec) % 2 == 1 ? 1 : 0;
        p.lockMark.sprite = Frame(frame);
      }
    }

    // ---------- 연출 ----------

    private void Play()
    {
      var target = Mathf.Min(_data.TotalLitPads, MaxPads);
      var seen = Mathf.Clamp(_data.lastSeenLitPads, 0, target);
      ShowState(seen);
      Canvas.ForceUpdateCanvases();
      ScrollTo(ShipY(), instant: true);
      if (seen >= target)
      {
        MarkSeen();
        return;
      }
      _skip = false;
      _scroll.vertical = false;
      _sequence = StartCoroutine(Sequence(seen, target));
    }

    private IEnumerator Sequence(int from, int to)
    {
      for (var lit = from + 1; lit <= to && !_skip; lit++)
      {
        yield return LightPad(lit - 1);
        if (_skip)
        {
          break;
        }
        if (lit % _padsPerPlanet == 0 && lit / _padsPerPlanet < _planets.Count)
        {
          yield return Unlock(lit / _padsPerPlanet);
        }
      }
      _sequence = null;
      Finish();
    }

    /// <summary>발판 하나를 켠다: 우주선이 따라가고, 발판이 살짝 커졌다 돌아온다</summary>
    private IEnumerator LightPad(int index)
    {
      _shownPads = index + 1;
      var pad = _pads[index];
      pad.sprite = _padOn;
      var from = _ship.anchoredPosition;
      var placement = _path.pads[index];
      var startScroll = _scroll.verticalNormalizedPosition;
      var endScroll = NormalizedFor(placement.position.y);
      RefreshRemaining();
      for (var t = 0f; t < _padSec && !_skip; t += Time.deltaTime)
      {
        var k = t / _padSec;
        pad.rectTransform.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));
        _ship.anchoredPosition = Vector2.Lerp(from, placement.position, Smooth(k));
        _ship.localEulerAngles = new Vector3(0f, 0f, placement.angle);
        _scroll.verticalNormalizedPosition = Mathf.Lerp(startScroll, endScroll, Smooth(k));
        yield return null;
      }
      pad.rectTransform.localScale = Vector3.one;
      PlaceShip(_shownPads);
    }

    /// <summary>행성 해금: 그 행성으로 스크롤 → 잠금 표시 1~10번 재생 (5번부터 실루엣이 원래 색으로 밝아짐) → 잠금 표시 끄고 "통"</summary>
    private IEnumerator Unlock(int index)
    {
      var p = _planets[index];
      yield return ScrollRoutine(p.root.anchoredPosition.y);
      yield return PlayUnlockFrames(p);
    }

    private IEnumerator PlayUnlockFrames(PlanetView p)
    {
      var frames = _unlockFrames != null ? _unlockFrames.Length : 0;
      if (frames > 1)
      {
        p.lockMark.gameObject.SetActive(true);
        var frameSec = 1f / _unlockFps;
        for (var f = 1; f < frames && !_skip; f++)
        {
          p.lockMark.sprite = _unlockFrames[f];
          // _brightenFromFrame번부터 마지막 프레임까지 어두움이 서서히 풀린다
          var k = Mathf.InverseLerp(_brightenFromFrame - 1, frames - 1, f);
          p.image.color = Color.Lerp(_lockedTint, Color.white, k);
          yield return new WaitForSeconds(frameSec);
        }
      }
      SetPlanetUnlocked(_planets.IndexOf(p), true);
      if (_skip)
      {
        yield break;
      }
      // 통: 살짝 커졌다 돌아온다
      for (var t = 0f; t < 0.3f && !_skip; t += Time.deltaTime)
      {
        p.root.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / 0.3f * Mathf.PI));
        yield return null;
      }
      p.root.localScale = Vector3.one;
    }

    /// <summary>남은 연출을 정리해 최종 상태로 두고, 본 것으로 저장한다</summary>
    private void Finish()
    {
      if (_sequence != null)
      {
        StopCoroutine(_sequence);
        _sequence = null;
      }
      if (_data == null || _path == null)
      {
        return;
      }
      ShowState(Mathf.Min(_data.TotalLitPads, MaxPads));
      ScrollTo(ShipY(), instant: true);
      _scroll.vertical = true;
      MarkSeen();
    }

    private void MarkSeen()
    {
      if (_data.lastSeenLitPads != _data.TotalLitPads)
      {
        _data.lastSeenLitPads = _data.TotalLitPads;
        SaveStore.Save();
      }
    }

    // ---------- 스크롤 ----------

    private float ShipY() => _ship.anchoredPosition.y;

    private float NormalizedFor(float y)
    {
      var view = _scroll.viewport != null ? _scroll.viewport.rect.height : ((RectTransform)_scroll.transform).rect.height;
      var range = _content.rect.height - view;
      return range <= 0f ? 0f : Mathf.Clamp01((y - view * 0.5f) / range);
    }

    private void ScrollTo(float y, bool instant)
    {
      _scroll.StopMovement();
      _scroll.verticalNormalizedPosition = NormalizedFor(y);
    }

    private IEnumerator ScrollRoutine(float y)
    {
      var from = _scroll.verticalNormalizedPosition;
      var to = NormalizedFor(y);
      for (var t = 0f; t < _scrollSec && !_skip; t += Time.deltaTime)
      {
        _scroll.verticalNormalizedPosition = Mathf.Lerp(from, to, Smooth(t / _scrollSec));
        yield return null;
      }
      _scroll.verticalNormalizedPosition = to;
    }

    private static float Smooth(float k) => k * k * (3f - 2f * k);

    // ---------- 개발용 ----------

    /// <summary>가짜 도장을 넣어 발판을 켠다 (아주 옛날 날짜라 오늘 도장·로비 표시에는 영향이 없다)</summary>
    private void DevAddPads(int count)
    {
      // 진행 중인 연출은 먼저 끝낸다. 데이터를 바꾼 뒤에 끝내면 새 발판까지 "본 것"으로 저장돼 연출이 안 나온다
      Finish();
      var day = new DateTime(2000, 1, 1);
      var added = 0;
      for (var i = 0; added < count && i < 100000; i++)
      {
        var date = SaveData.DateKey(day.AddDays(i / 3));
        var slot = (MealSlot)(i % 3);
        if (_data.HasSticker(date, slot))
        {
          continue;
        }
        _data.stickers.Add(new StickerRecord { date = date, slot = slot, patientId = "dev" });
        added++;
      }
      SaveStore.Save();
      Rebuild();
    }

    private void DevReset()
    {
      Finish();
      _data.stickers.Clear();
      _data.lastSeenLitPads = 0;
      SaveStore.Save();
      Rebuild();
    }

    /// <summary>연출 다시 보기: 본 위치를 한 행성 분량만큼 되돌린다</summary>
    private void DevReplay()
    {
      Finish();
      _data.lastSeenLitPads = Mathf.Max(0, Mathf.Min(_data.TotalLitPads, MaxPads) - _padsPerPlanet);
      SaveStore.Save();
      Rebuild();
    }

    /// <summary>잠금 해제 애니메이션만 다시: 다음 잠긴 행성(없으면 마지막 행성)에서 재생하고 원래대로 돌린다</summary>
    private void DevUnlockOnly()
    {
      Finish();
      var index = _planets.FindIndex(p => !p.unlocked);
      if (index < 0)
      {
        index = _planets.Count - 1;
      }
      if (index <= 0)
      {
        return;
      }
      _skip = false;
      _sequence = StartCoroutine(DevUnlockRoutine(index));
    }

    private IEnumerator DevUnlockRoutine(int index)
    {
      var p = _planets[index];
      SetPlanetUnlocked(index, false);
      p.lockMark.gameObject.SetActive(true);
      yield return Unlock(index);
      yield return new WaitForSeconds(0.6f);
      _sequence = null;
      ShowState(_shownPads);
    }

    /// <summary>바뀐 데이터로 지도를 다시 만들고, 아직 안 본 발판이 있으면 연출부터 보여준다</summary>
    private void Rebuild()
    {
      Build();
      Play();
    }
  }
}
