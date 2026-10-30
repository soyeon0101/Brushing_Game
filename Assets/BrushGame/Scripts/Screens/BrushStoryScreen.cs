using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   양치로 넘기는 동화 (처음 치료 시작을 눌렀을 때 환자 확인 전에 한 번, 보호자 설정의 "다시 재기").
  ///   카메라 영상은 배경 그림으로 가린다 (손 인식은 계속 돈다). 페이지 그림은 고른 캐릭터(왕자/공주)에 맞는 것을 쓴다.
  ///   페이지마다 칫솔질을 몇 초 하면 다음 장으로 넘어간다. 터치가 아니라 양치가 페이지 넘기기.
  ///   "측정" 페이지들을 넘기는 동안 아이의 왕복 폭·속도를 재서 SaveData.brushCalibration에 저장한다 (판정 기준은 BrushCalibrationRules).
  ///   이 화면에서는 판정을 느슨하게 해서, 잘게·느리게 닦는 아이도 페이지가 막히지 않는다.
  ///   오래 안 넘어가면 "다음 장" 버튼이 나온다 (측정이 모자라면 버리고 기본 기준을 쓴다).
  /// </summary>
  public class BrushStoryScreen : ScreenBase
  {
    [Serializable]
    public class CharacterArt
    {
      [Tooltip("GameCatalog의 캐릭터 id (prince, princess)")]
      public string characterId;
      public Sprite art;
    }

    [Serializable]
    public class Page
    {
      [Tooltip("{0} = 닉네임")]
      [TextArea] public string text;
      [Tooltip("캐릭터별 페이지 그림. 고른 캐릭터가 없으면 첫 그림, 하나도 없으면 임시 그림 자리")]
      public CharacterArt[] arts = Array.Empty<CharacterArt>();
      [Tooltip("다음 장으로 넘어가려면 닦아야 하는 시간 (초). 0이면 닦지 않고 버튼으로 넘긴다")]
      [Min(0f)] public float brushSec = 3f;
      [Tooltip("이 페이지 동안 양치 동작을 잰다 (칫솔을 입에 넣고 실제로 닦는 장면)")]
      public bool measure;
      [Tooltip("아래 안내 문구")]
      public string guide;
      [Tooltip("brushSec이 0일 때 버튼 글자")]
      public string buttonLabel;

      public Sprite ArtFor(string characterId)
      {
        if (arts == null || arts.Length == 0)
        {
          return null;
        }
        var match = Array.Find(arts, a => a != null && a.characterId == characterId);
        return (match ?? arts[0])?.art;
      }
    }

    [SerializeField] private BrushInputHub _input;

    [Header("화면")]
    [SerializeField] private CanvasGroup _card;
    [SerializeField] private Image _art;
    [Tooltip("그림이 없을 때만 보이는 임시 표시")]
    [SerializeField] private GameObject _artPlaceholder;
    [SerializeField] private Text _text;
    [SerializeField] private Text _pageCount;
    [SerializeField] private GameObject _gauge;
    [Tooltip("닦은 만큼 가로로 늘어난다")]
    [SerializeField] private RectTransform _gaugeFill;
    [SerializeField] private GameObject _guideRoot;
    [SerializeField] private Text _guide;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Text _nextLabel;
    [Tooltip("오래 안 넘어갈 때만 보이는 버튼")]
    [SerializeField] private Button _skipButton;

    [Header("이야기 (첫 실행)")]
    [SerializeField] private Page[] _storyPages =
    {
      new Page { text = "아주 먼 우주에\n반짝반짝 이빨 행성이 있었어요.\n친구들은 매일 웃으며 지냈지요.", brushSec = 1.5f, guide = "칫솔 든 손을 흔들면 다음 장으로 넘어가!" },
      new Page { text = "그런데 충치균들이 몰려와\n친구들의 이를 아프게\n하기 시작했어요!", brushSec = 3.5f, measure = true, guide = "칫솔을 입에 넣고 슥삭슥삭 쫓아내 봐!" },
      new Page { text = "우주 치과에\n도와 달라는 연락이 왔어요!\n{0}, 충치균을 물리치러 가자!", brushSec = 3.5f, measure = true, guide = "평소처럼 슥삭슥삭!" },
      new Page { text = "이제 {0}도\n우주 치과의 치과의사예요!\n아픈 친구들을 도와줄래요?", brushSec = 0f, buttonLabel = "좋아!" },
    };

    [Header("다시 재기 (보호자 설정)")]
    [SerializeField] private Page[] _remeasurePages =
    {
      new Page { text = "{0}, 칫솔을 입에 넣고\n평소처럼 닦아 볼까?", brushSec = 2.5f, measure = true, guide = "평소처럼 슥삭슥삭!" },
      new Page { text = "좋아! 충치균들이 도망가고 있어!", brushSec = 2.5f, measure = true, guide = "평소처럼 슥삭슥삭!" },
      new Page { text = "거의 다 됐어!", brushSec = 2.5f, measure = true, guide = "조금만 더!" },
    };
    [SerializeField, TextArea] private string _measuredLine = "고마워!\n이제 {0}에게 딱 맞게 볼게!";
    [SerializeField, TextArea] private string _notMeasuredLine = "칫솔이 잘 안 보였어.\n다음 양치 때 다시 맞춰 볼게!";
    [SerializeField] private string _doneButtonLabel = "확인";

    [Header("안내")]
    [SerializeField] private string _handLostLine = "칫솔 든 손을 화면에 보여줘!";
    [SerializeField] private string _idleLine = "칫솔을 움직여 봐!";
    [Tooltip("이만큼 안 닦으면 멈춤 안내 (초)")]
    [SerializeField, Min(0f)] private float _idleHintSec = 1.5f;
    [Tooltip("이만큼 안 닦으면 다음 장 버튼이 나온다 (초)")]
    [SerializeField, Min(1f)] private float _skipAfterSec = 10f;
    [SerializeField, Min(0f)] private float _turnSec = 0.35f;

    private Page[] _pages;
    private Page _resultPage;
    private bool _remeasure;
    private string _nickname = "";
    private string _characterId = "";
    private int _index;
    private float _pageT;
    private float _idleT;
    private float _lostT;
    private bool _measuring;
    private bool _turning;
    private float _gaugeWidth = -1f;

    /// <summary>끝까지 넘겼을 때. 인자 = 이번에 측정값을 새로 저장했는지</summary>
    public event Action<bool> Finished;

    public bool Measured { get; private set; }

    /// <param name="characterId">고른 캐릭터. 페이지 그림을 고른다</param>
    /// <param name="remeasure">true = 보호자 설정의 다시 재기 (측정 페이지만 + 결과 한 장)</param>
    public void Setup(string nickname, string characterId, bool remeasure)
    {
      _nickname = nickname ?? "";
      _characterId = characterId ?? "";
      _remeasure = remeasure;
    }

    private void Awake()
    {
      _nextButton.onClick.AddListener(Advance);
      _skipButton.onClick.AddListener(Advance);
    }

    public override void OnShow()
    {
      if (_gaugeWidth < 0f)
      {
        _gaugeWidth = _gaugeFill.sizeDelta.x;
      }
      _pages = _remeasure ? _remeasurePages : _storyPages;
      _resultPage = null;
      _measuring = false;
      _turning = false;
      Measured = false;
      _input.ApplyCalibration(SaveStore.Data.brushCalibration);
      _input.SetLenient(true);
      _card.alpha = 1f;
      ShowPage(0);
    }

    public override void OnHide()
    {
      StopAllCoroutines();
      if (_measuring)
      {
        _input.CancelMeasure();
        _measuring = false;
      }
      _input.SetLenient(false);
    }

    private Page CurrentPage => _resultPage ?? _pages[_index];

    private void Update()
    {
      if (_turning)
      {
        return;
      }
      var page = CurrentPage;
      var visible = _input.HandVisible;
      var brushing = visible && _input.IsBrushing;
      _lostT = visible ? 0f : _lostT + Time.deltaTime;
      if (page.brushSec <= 0f)
      {
        return;
      }

      if (brushing)
      {
        _pageT += Time.deltaTime;
        _idleT = 0f;
      }
      else
      {
        _idleT += Time.deltaTime;
      }
      SetGauge(_pageT / page.brushSec);
      _guide.text = _lostT >= 1f ? _handLostLine : _idleT >= _idleHintSec ? _idleLine : page.guide;
      _skipButton.gameObject.SetActive(_idleT >= _skipAfterSec);

      if (_pageT >= page.brushSec)
      {
        Advance();
      }
    }

    private void Advance()
    {
      if (_turning)
      {
        return;
      }
      if (_resultPage != null)
      {
        Finish();
        return;
      }

      var page = _pages[_index];
      var next = _index + 1;
      // 측정 페이지가 끝나는 곳에서 결과를 저장한다
      if (_measuring && page.measure && (next >= _pages.Length || !_pages[next].measure))
      {
        EndMeasure();
      }
      if (next < _pages.Length)
      {
        StartCoroutine(Turn(() => ShowPage(next)));
      }
      else if (_remeasure)
      {
        StartCoroutine(Turn(ShowResult));
      }
      else
      {
        Finish();
      }
    }

    private void EndMeasure()
    {
      _measuring = false;
      var calibration = SaveStore.Data.brushCalibration;
      Measured = _input.EndMeasure(calibration, fresh: true);
      if (Measured)
      {
        _input.ApplyCalibration(calibration);
        SaveStore.Save();
      }
      // 동화 나머지 페이지는 계속 느슨하게
      _input.SetLenient(true);
    }

    private void Finish()
    {
      if (_measuring)
      {
        EndMeasure();
      }
      Finished?.Invoke(Measured);
    }

    private IEnumerator Turn(Action show)
    {
      _turning = true;
      _skipButton.gameObject.SetActive(false);
      for (var t = 0f; t < _turnSec; t += Time.deltaTime)
      {
        _card.alpha = 1f - t / _turnSec;
        yield return null;
      }
      show();
      for (var t = 0f; t < _turnSec; t += Time.deltaTime)
      {
        _card.alpha = t / _turnSec;
        yield return null;
      }
      _card.alpha = 1f;
      _turning = false;
    }

    private void ShowPage(int index)
    {
      _index = index;
      var page = _pages[index];
      if (page.measure && !_measuring)
      {
        _measuring = true;
        _input.BeginMeasure();
      }
      ShowContent(page, $"{index + 1} / {_pages.Length}");
    }

    private void ShowResult()
    {
      _resultPage = new Page
      {
        text = Measured ? _measuredLine : _notMeasuredLine,
        brushSec = 0f,
        buttonLabel = _doneButtonLabel,
        arts = _pages[_pages.Length - 1].arts,
      };
      ShowContent(_resultPage, "");
    }

    private void ShowContent(Page page, string count)
    {
      _pageT = 0f;
      _idleT = 0f;
      _lostT = 0f;
      _text.text = string.Format(page.text ?? "", _nickname);
      _pageCount.text = count;
      var art = page.ArtFor(_characterId);
      _artPlaceholder.SetActive(art == null);
      _art.enabled = art != null;
      ArtSlot.Apply(_art, art);
      // 그림 비율대로 틀을 꽉 채운다 (AspectRatioFitter가 있을 때)
      var fitter = _art.GetComponent<AspectRatioFitter>();
      if (fitter != null && art != null && art.rect.height > 0f)
      {
        fitter.aspectRatio = art.rect.width / art.rect.height;
      }

      var byBrushing = page.brushSec > 0f;
      _gauge.SetActive(byBrushing);
      _guideRoot.SetActive(byBrushing);
      _guide.text = page.guide ?? "";
      _nextButton.gameObject.SetActive(!byBrushing);
      _nextLabel.text = string.IsNullOrEmpty(page.buttonLabel) ? _doneButtonLabel : page.buttonLabel;
      _skipButton.gameObject.SetActive(false);
      SetGauge(0f);
    }

    private void SetGauge(float fill)
    {
      _gaugeFill.sizeDelta = new Vector2(_gaugeWidth * Mathf.Clamp01(fill), _gaugeFill.sizeDelta.y);
    }
  }
}
