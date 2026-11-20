using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   메인 로비: 내 프로필(연속 양치 일수), 오늘의 칭찬 도장, 이번 끼니의 환자(말풍선), 치료 시작, 아래 메뉴 3개, 오른쪽 위 보호자 설정.
  ///   지금 끼니의 도장을 이미 받았으면 치료 시작 전에 연습할지 물어본다 (치료는 되지만 도장은 또 주지 않는다).
  ///   움직임: 지금 끼니 도장 칸 통통 · 새로 찍힌 도장 쾅 · 환자를 누르면 흔들며 대사를 다시 말함 (나머지 반복 움직임은 UiMotion).
  /// </summary>
  public class LobbyScreen : ScreenBase
  {
    [Header("프로필")]
    [SerializeField] private Image _avatar;
    [SerializeField] private Text _nickname;
    [Tooltip("연속 양치 일수 칸. 0일이면 숨긴다")]
    [SerializeField] private GameObject _streakRoot;
    [SerializeField] private Text _streakText;

    [Header("오늘의 스티커 (아침/점심/저녁 순)")]
    [SerializeField] private GameObject[] _todayStickers;
    [Tooltip("지금 끼니 칸에만 켜지는 분홍 테두리")]
    [SerializeField] private GameObject[] _slotNowRings;
    [Tooltip("지금 끼니이고 아직 도장이 없을 때 켜지는 \"지금!\" 표시")]
    [SerializeField] private GameObject[] _slotNowTags;
    [Tooltip("지금 끼니 칸만 통통 튀게 할 움직임")]
    [SerializeField] private UiMotion[] _slotBobs;

    [Header("이번 끼니의 환자")]
    [Tooltip("\"아침에 온 친구\"처럼 끼니에 따라 바뀌는 제목")]
    [SerializeField] private Text _patientHeader;
    [SerializeField] private Image _patientPortrait;
    [SerializeField] private Text _patientName;
    [SerializeField] private Text _patientLine;
    [Tooltip("환자를 누르면 흔들며 대사를 다시 말한다")]
    [SerializeField] private Button _patientButton;
    [SerializeField] private RectTransform _bubble;

    [Header("버튼")]
    [SerializeField] private Button _treatButton;
    [SerializeField] private Button _bookButton;
    [SerializeField] private Button _stickerButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _badgeButton;

    [Header("NEW 표시 (GameFlow가 켜고 끈다)")]
    [SerializeField] private GameObject _bookNew;
    [SerializeField] private GameObject _stickerNew;
    [SerializeField] private GameObject _badgeNew;

    [Header("이미 도장을 받은 끼니에 치료 시작")]
    [SerializeField] private GameObject _practicePanel;
    [SerializeField] private Text _practiceMessage;
    [SerializeField] private Button _practiceButton;
    [SerializeField] private Button _practiceCancelButton;

    // 로비를 다시 열었을 때 새로 생긴 도장만 쾅 찍는다 (앱을 켤 때마다 처음 본 도장은 그냥 보여 준다)
    private static string s_shownDate;
    private static int s_shownMask = -1;

    private SaveData _data;
    private int _slamMask;
    private Coroutine _wobble;
    private string _patientId;

    public event Action TreatPressed;
    public event Action BookPressed;
    public event Action StickersPressed;
    public event Action SettingsPressed;
    public event Action BadgesPressed;

    public void Setup(SaveData data, PlayerCharacter character, PatientInfo patient, MealSlot slot)
    {
      _data = data;
      _practicePanel.SetActive(false);
      _nickname.text = $"{data.profile.nickname} 선생님";
      ArtSlot.Apply(_avatar, character?.Portrait);

      var now = DateTime.Now;
      if (_streakRoot != null)
      {
        var streak = BadgeRules.CurrentStreak(data, now);
        _streakRoot.SetActive(streak > 0);
        if (_streakText != null)
        {
          _streakText.text = $"{streak}일째";
        }
      }

      var today = SaveData.DateKey(now);
      var mask = 0;
      for (var i = 0; i < _todayStickers.Length; i++)
      {
        var has = data.HasSticker(today, (MealSlot)i);
        _todayStickers[i].SetActive(has);
        if (has)
        {
          mask |= 1 << i;
        }
        var isNow = i == (int)slot;
        SetActive(_slotNowRings, i, isNow);
        SetActive(_slotNowTags, i, isNow && !has);
        if (_slotBobs != null && i < _slotBobs.Length && _slotBobs[i] != null)
        {
          _slotBobs[i].enabled = isNow && !has;
        }
      }
      _slamMask = s_shownDate == today && s_shownMask >= 0 ? mask & ~s_shownMask : 0;
      s_shownDate = today;
      s_shownMask = mask;

      _patientId = patient.id;
      _patientHeader.text = SaveData.VisitorTitle(slot);
      _patientName.text = patient.displayName;
      _patientLine.text = patient.requestLine;
      ArtSlot.Apply(_patientPortrait, patient.Portrait);
    }

    /// <summary>스티커판 자리 버튼의 글자 (파트너 방일 때 파트너 이름)</summary>
    public void SetStickerButtonLabel(string label)
    {
      var text = _stickerButton.GetComponentInChildren<Text>(true);
      if (text != null)
      {
        text.text = label;
      }
    }

    /// <summary>아래 메뉴 버튼의 NEW 표시 (도감 / 스티커판·친구 자리 / 배지·지도 자리)</summary>
    public void SetNewMarks(bool book, bool sticker, bool badge)
    {
      if (_bookNew != null) _bookNew.SetActive(book);
      if (_stickerNew != null) _stickerNew.SetActive(sticker);
      if (_badgeNew != null) _badgeNew.SetActive(badge);
    }

    public override void OnShow()
    {
      for (var i = 0; i < _todayStickers.Length; i++)
      {
        if ((_slamMask & (1 << i)) != 0)
        {
          StartCoroutine(Slam((RectTransform)_todayStickers[i].transform, 0.25f + 0.2f * i));
        }
      }
      _slamMask = 0;
      if (_bubble != null)
      {
        StartCoroutine(Pop(_bubble, 0.15f));
        SoundManager.Sfx("bubble_pop", 0.7f);
      }
      if (IsOn(_bookNew) || IsOn(_stickerNew) || IsOn(_badgeNew))
      {
        SoundManager.Sfx("new_pop", 0.8f);
      }
      // 환자가 부탁 대사를 직접 말한다
      SoundManager.Voice($"{_patientId}_request", SoundManager.Say.Queue);
    }

    private static bool IsOn(GameObject go) => go != null && go.activeSelf;

    public override void OnHide()
    {
      StopAllCoroutines();
      _wobble = null;
      foreach (var sticker in _todayStickers)
      {
        sticker.transform.localScale = Vector3.one;
      }
      if (_bubble != null)
      {
        _bubble.localScale = Vector3.one;
      }
      if (_patientButton != null)
      {
        _patientButton.transform.localRotation = Quaternion.identity;
      }
    }

    private void Awake()
    {
      _treatButton.onClick.AddListener(OnTreat);
      _bookButton.onClick.AddListener(() => BookPressed?.Invoke());
      _stickerButton.onClick.AddListener(() => StickersPressed?.Invoke());
      _settingsButton.onClick.AddListener(() => SettingsPressed?.Invoke());
      _badgeButton.onClick.AddListener(() => BadgesPressed?.Invoke());
      if (_patientButton != null)
      {
        _patientButton.onClick.AddListener(OnPatientTapped);
      }
      _practiceButton.onClick.AddListener(() =>
      {
        _practicePanel.SetActive(false);
        TreatPressed?.Invoke();
      });
      _practiceCancelButton.onClick.AddListener(() => _practicePanel.SetActive(false));
    }

    /// <summary>로비에 오래 머물다 끼니가 바뀔 수 있으므로 누른 시각으로 판단한다</summary>
    private void OnTreat()
    {
      var now = DateTime.Now;
      var slot = SaveData.SlotAt(now);
      if (_data == null || !_data.HasSticker(SaveData.DateKey(now), slot))
      {
        TreatPressed?.Invoke();
        return;
      }
      _practiceMessage.text = $"{SaveData.SlotName(slot)} 도장은 벌써 받았어!\n그래도 연습할래?";
      _practicePanel.SetActive(true);
      var slotId = slot == MealSlot.Morning ? "morning" : slot == MealSlot.Lunch ? "lunch" : "evening";
      SoundManager.Voice($"lobby_practice_{slotId}", SoundManager.Say.Interrupt);
    }

    private void OnPatientTapped()
    {
      // 누르는 소리(patient_tap)는 버튼의 ButtonSound가 낸다
      SoundManager.Voice($"{_patientId}_request", SoundManager.Say.Interrupt);
      if (_wobble != null)
      {
        StopCoroutine(_wobble);
      }
      _wobble = StartCoroutine(Wobble((RectTransform)_patientButton.transform));
      if (_bubble != null)
      {
        StartCoroutine(Pop(_bubble, 0f));
      }
    }

    private static void SetActive(GameObject[] items, int index, bool on)
    {
      if (items != null && index < items.Length && items[index] != null)
      {
        items[index].SetActive(on);
      }
    }

    /// <summary>도장 쾅: 크게 떠 있다가 내려찍히며 살짝 튕긴다</summary>
    private static IEnumerator Slam(RectTransform target, float delay)
    {
      target.localScale = Vector3.zero;
      yield return new WaitForSeconds(delay);
      const float duration = 0.4f;
      var stamped = false;
      for (var t = 0f; t < duration; t += Time.deltaTime)
      {
        var k = t / duration;
        // 2.2배 → 0.85배(찍힘) → 1배
        var s = k < 0.55f ? Mathf.Lerp(2.2f, 0.85f, EaseIn(k / 0.55f)) : Mathf.Lerp(0.85f, 1f, EaseOut((k - 0.55f) / 0.45f));
        if (!stamped && k >= 0.55f)
        {
          // 내려찍히는 순간 쿵!
          stamped = true;
          SoundManager.Sfx("stamp");
        }
        target.localScale = new Vector3(s, s, 1f);
        yield return null;
      }
      target.localScale = Vector3.one;
    }

    /// <summary>말풍선이 톡 튀어나온다</summary>
    private static IEnumerator Pop(RectTransform target, float delay)
    {
      target.localScale = Vector3.zero;
      yield return new WaitForSeconds(delay);
      const float duration = 0.35f;
      for (var t = 0f; t < duration; t += Time.deltaTime)
      {
        var k = t / duration;
        var s = 1f + 1.7f * Mathf.Pow(k - 1f, 3f) + 0.7f * Mathf.Pow(k - 1f, 2f); // 살짝 넘쳤다 돌아오기
        target.localScale = new Vector3(s, s, 1f);
        yield return null;
      }
      target.localScale = Vector3.one;
    }

    /// <summary>환자가 "아야" 하듯 좌우로 흔들린다</summary>
    private IEnumerator Wobble(RectTransform target)
    {
      const float duration = 0.6f;
      for (var t = 0f; t < duration; t += Time.deltaTime)
      {
        var k = t / duration;
        var angle = Mathf.Sin(k * Mathf.PI * 5f) * 9f * (1f - k);
        target.localRotation = Quaternion.Euler(0f, 0f, angle);
        yield return null;
      }
      target.localRotation = Quaternion.identity;
      _wobble = null;
    }

    private static float EaseIn(float k) => k * k;

    private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
  }
}
