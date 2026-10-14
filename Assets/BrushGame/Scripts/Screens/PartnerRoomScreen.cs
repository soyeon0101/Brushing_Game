using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   파트너 방: 우주선 안 방에서 파트너 로봇이 돌아다니고 논다. 로봇을 누르면 반응한다.
  ///   양치로 받은 도장 하나가 밥 한 그릇. "밥 주기"를 누르면 로봇이 먹고 기뻐한다 (안 줘도 벌은 없다).
  ///
  ///   첫 만남 (저장: partnerBrought, partnerAwake, partnerName):
  ///   1. 창고 — 살짝 어두운 곳, 빛 아래 꺼진 로봇. [데려가기]를 누르면 방이 밝아지며 내 방으로
  ///   2. 충전 기다림 — 꺼진 채 배터리 표시, "양치를 하면 충전돼요!"
  ///   3. 첫 양치 후 — 배터리가 차오르고 깨어남 (치료가 끝나면 GameFlow가 자동으로 이 화면을 연다)
  ///   4. 이름 짓기 — 깨어난 직후 "안녕! 내 이름을 지어 줄래?"
  /// </summary>
  public class PartnerRoomScreen : ScreenBase
  {
    public const int NameMaxLength = 6;

    [SerializeField] private PartnerRobot _robot;
    [SerializeField] private Button _backButton;

    [Header("밥 주기 (밥그릇 버튼)")]
    [SerializeField] private Button _feedButton;
    [Tooltip("남은 밥 수를 보여주는 동그라미 (0이면 숨김)")]
    [SerializeField] private GameObject _countBadge;
    [SerializeField] private Text _countText;
    [Tooltip("줄 밥이 없을 때만 보이는 안내 (\"양치하면 밥이 생겨요\")")]
    [SerializeField] private GameObject _noMealHint;

    [Header("첫 만남")]
    [Tooltip("창고 어둠 (가운데만 밝은 빛 그림). 데려오면 서서히 사라진다")]
    [SerializeField] private CanvasGroup _darkness;
    [Tooltip("위에서 말하는 안내 말풍선")]
    [SerializeField] private GameObject _bubble;
    [SerializeField] private Text _bubbleText;
    [SerializeField] private Button _bringButton;
    [Tooltip("로봇 머리 위 배터리 (충전 기다림·충전 중에만)")]
    [SerializeField] private GameObject _battery;
    [Tooltip("배터리 안 채움 (가로로 늘어난다)")]
    [SerializeField] private RectTransform _batteryFill;
    [SerializeField] private Image _batteryFillImage;

    [Header("이름 짓기")]
    [SerializeField] private GameObject _namePanel;
    [SerializeField] private InputField _nameInput;
    [SerializeField] private Button _nameOkButton;

    [Header("개발용 (에디터·Development Build에서만 보인다)")]
    [SerializeField] private GameObject _devPanel;
    [SerializeField] private Button _devStorage;
    [SerializeField] private Button _devWake;
    [SerializeField] private Button _devRename;
    [SerializeField] private Button _devMeals;

    [Header("문구")]
    [SerializeField] private string _storageLine = "창고에 꺼진 로봇이 있어!\n데려가서 충전해 줄까?";
    [SerializeField] private string _chargeLine = "양치를 하면 충전돼요!";
    [SerializeField] private string _helloLine = "안녕! 내 이름을 지어 줄래?";
    [SerializeField] private string _nameDoneLine = "{0}! 좋은 이름이야!";

    private SaveData _data;
    private Coroutine _story;
    private float _batteryWidth;

    public event Action BackPressed;

    public void Setup(SaveData data)
    {
      _data = data;
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
      _feedButton.onClick.AddListener(Feed);
      _bringButton.onClick.AddListener(Bring);
      _nameInput.characterLimit = NameMaxLength;
      _nameInput.onValueChanged.AddListener(_ => _nameOkButton.interactable = _nameInput.text.Trim().Length > 0);
      _nameOkButton.onClick.AddListener(ConfirmName);
      _batteryWidth = _batteryFill.sizeDelta.x;
      _devStorage.onClick.AddListener(DevStorage);
      _devWake.onClick.AddListener(DevWake);
      _devRename.onClick.AddListener(DevRename);
      _devMeals.onClick.AddListener(DevMeals);
    }

    public override void OnShow()
    {
      _devPanel.SetActive(Application.isEditor || Debug.isDebugBuild);
      _robot.ResetState();
      _namePanel.SetActive(false);
      _bringButton.gameObject.SetActive(false);
      _battery.SetActive(false);
      _darkness.gameObject.SetActive(false);
      ShowBubble(null);

      if (_data == null || !_data.partnerBrought)
      {
        ShowStorage();
      }
      else if (!_data.partnerAwake)
      {
        _robot.SetPoweredOff();
        ShowBattery(0.15f, false);
        if (_data.PartnerReadyToWake)
        {
          _story = StartCoroutine(Wake());
        }
        else
        {
          ShowBubble(_chargeLine);
        }
      }
      else if (string.IsNullOrEmpty(_data.partnerName))
      {
        AskName();
      }
      Refresh();
    }

    public override void OnHide()
    {
      if (_story != null)
      {
        StopCoroutine(_story);
        _story = null;
      }
    }

    private void Update()
    {
      // 먹는 동안이나 첫 만남 중에는 못 누르게
      _feedButton.interactable = _data != null && _data.partnerAwake && _data.PartnerMealsLeft > 0 && !_robot.IsEating && _story == null;
    }

    // ---------- 첫 만남 ----------

    private void ShowStorage()
    {
      _robot.SetPoweredOff();
      _darkness.gameObject.SetActive(true);
      _darkness.alpha = 1f;
      ShowBubble(_storageLine);
      _bringButton.gameObject.SetActive(true);
    }

    private void Bring()
    {
      _data.partnerBrought = true;
      SaveStore.Save();
      _bringButton.gameObject.SetActive(false);
      _story = StartCoroutine(BringHome());
    }

    /// <summary>창고가 밝아지며 내 방이 된다 → 충전 시작 (이미 양치했으면 바로 깨어난다)</summary>
    private IEnumerator BringHome()
    {
      ShowBubble(null);
      for (var t = 0f; t < 1.2f; t += Time.deltaTime)
      {
        _darkness.alpha = 1f - t / 1.2f;
        yield return null;
      }
      _darkness.gameObject.SetActive(false);
      ShowBattery(0.15f, false);
      ShowBubble(_chargeLine);
      _story = null;
      if (_data.PartnerReadyToWake)
      {
        yield return new WaitForSeconds(1.2f);
        _story = StartCoroutine(Wake());
      }
      Refresh();
    }

    /// <summary>첫 양치 뒤: 배터리가 차오르고 → 깨어나서 → 이름 짓기</summary>
    private IEnumerator Wake()
    {
      ShowBubble(null);
      ShowBattery(0.15f, true);
      yield return new WaitForSeconds(0.5f);
      for (var t = 0f; t < 1.6f; t += Time.deltaTime)
      {
        ShowBattery(Mathf.Lerp(0.15f, 1f, t / 1.6f), true);
        yield return null;
      }
      ShowBattery(1f, true);
      yield return new WaitForSeconds(0.4f);
      _battery.SetActive(false);
      _data.partnerAwake = true;
      SaveStore.Save();
      yield return _robot.PowerOn();
      _story = null;
      AskName();
      Refresh();
    }

    private void ShowBattery(float fill, bool charging)
    {
      _battery.SetActive(true);
      _batteryFill.sizeDelta = new Vector2(_batteryWidth * Mathf.Clamp01(fill), _batteryFill.sizeDelta.y);
      // 거의 비었을 땐 빨강, 차오를수록 초록
      _batteryFillImage.color = Color.Lerp(new Color(1f, 0.45f, 0.45f), new Color(0.35f, 0.85f, 0.55f), Mathf.InverseLerp(0.2f, 0.7f, fill));
      if (!charging)
      {
        _batteryFillImage.color = new Color(1f, 0.45f, 0.45f);
      }
    }

    // ---------- 이름 짓기 ----------

    private void AskName()
    {
      ShowBubble(_helloLine);
      _nameInput.text = "";
      _nameOkButton.interactable = false;
      _namePanel.SetActive(true);
    }

    private void ConfirmName()
    {
      var name = _nameInput.text.Trim();
      if (name.Length == 0)
      {
        return;
      }
      _data.partnerName = name;
      SaveStore.Save();
      _namePanel.SetActive(false);
      _story = StartCoroutine(NameDone(name));
    }

    private IEnumerator NameDone(string name)
    {
      ShowBubble(string.Format(_nameDoneLine, name));
      _robot.Celebrate();     // 이름을 받은 기념으로 ^^ 통통 + 폭죽
      yield return new WaitForSeconds(2.5f);
      ShowBubble(null);
      _story = null;
    }

    // ---------- 밥 주기 ----------

    private void Feed()
    {
      if (_data == null || !_data.partnerAwake || _data.PartnerMealsLeft <= 0 || _robot.IsEating)
      {
        return;
      }
      _data.partnerFedCount++;
      SaveStore.Save();
      _robot.Feed();
      Refresh();
    }

    private void Refresh()
    {
      var awake = _data != null && _data.partnerAwake;
      var left = _data != null ? _data.PartnerMealsLeft : 0;
      _feedButton.gameObject.SetActive(awake);
      _countBadge.SetActive(awake && left > 0);
      _countText.text = left.ToString();
      _noMealHint.SetActive(awake && left <= 0);
    }

    private void ShowBubble(string line)
    {
      _bubble.SetActive(!string.IsNullOrEmpty(line));
      _bubbleText.text = line ?? "";
    }

    // ---------- 개발용: 첫 만남을 다시 보기 ----------

    /// <summary>안 데려온 상태(창고)부터. 도장은 그대로라 데려가면 바로 깨어난다</summary>
    private void DevStorage()
    {
      _data.partnerBrought = false;
      _data.partnerAwake = false;
      _data.partnerName = "";
      SaveStore.Save();
      Restart();
    }

    /// <summary>충전 → 깨어남 → 이름 짓기 장면 (도장이 없으면 개발용 도장 1개)</summary>
    private void DevWake()
    {
      if (_data.stickers.Count == 0)
      {
        AddDevStickers(1);
      }
      _data.partnerBrought = true;
      _data.partnerAwake = false;
      _data.partnerName = "";
      SaveStore.Save();
      Restart();
    }

    private void DevRename()
    {
      _data.partnerName = "";
      SaveStore.Save();
      Restart();
    }

    private void DevMeals()
    {
      AddDevStickers(3);
      SaveStore.Save();
      Refresh();
    }

    /// <summary>아주 옛날 날짜의 도장 (오늘 도장·로비 표시에는 영향 없음)</summary>
    private void AddDevStickers(int count)
    {
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
    }

    private void Restart()
    {
      OnHide();
      OnShow();
    }
  }
}
