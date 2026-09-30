using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   이번 주 칭찬 스티커판 (월~일 × 아침/점심/저녁).
  ///   치료 직후에는 방금 받은 칭찬 도장이 크게 나타났다가 칸에 쾅 찍히고 반짝인다.
  ///   찍힌 도장은 손으로 찍은 것처럼 칸마다 조금씩 기울어져 있다.
  /// </summary>
  public class StickerScreen : ScreenBase
  {
    private const int Days = 7;
    private const int Slots = 3;

    [SerializeField] private Text _title;
    [SerializeField] private Text _summary;
    [Tooltip("요일 × 끼니 순서 (월아침, 월점심, 월저녁, 화아침...)")]
    [SerializeField] private GameObject[] _stickers;
    [SerializeField] private Button _backButton;
    [Tooltip("도장이 찍힐 때 퍼지는 반짝임 (없어도 됨)")]
    [SerializeField] private RectTransform _stampFx;

    [Header("도장 연출")]
    [Tooltip("화면이 뜨고 도장을 찍기까지 기다리는 시간 (초)")]
    [SerializeField, Min(0f)] private float _stampDelay = 0.5f;
    [SerializeField, Min(0.05f)] private float _stampSec = 0.3f;
    [SerializeField, Min(1f)] private float _stampStartScale = 3f;
    [SerializeField, Min(0.05f)] private float _settleSec = 0.35f;
    [SerializeField, Min(0.05f)] private float _fxSec = 0.6f;
    [Tooltip("찍힌 도장의 최대 기울기 (도)")]
    [SerializeField] private float _maxTilt = 12f;

    private RectTransform _stamp;
    private Image _stampImage;
    private float _stampTilt;
    private float _t = -1f;

    public event Action BackPressed;

    /// <param name="newSticker">방금 받은 도장. 스티커판만 보러 온 경우 null</param>
    public void Setup(SaveData data, StickerRecord newSticker)
    {
      var today = DateTime.Today;
      var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
      var count = 0;
      _stamp = null;
      _t = -1f;

      for (var d = 0; d < Days; d++)
      {
        var date = SaveData.DateKey(monday.AddDays(d));
        for (var s = 0; s < Slots; s++)
        {
          var index = d * Slots + s;
          var sticker = _stickers[index];
          var has = data.HasSticker(date, (MealSlot)s);
          sticker.SetActive(has);
          var rt = (RectTransform)sticker.transform;
          rt.localScale = Vector3.one;
          rt.localEulerAngles = new Vector3(0f, 0f, Tilt(index));
          SetAlpha(sticker.GetComponent<Image>(), 1f);
          if (has)
          {
            count++;
          }
          if (newSticker != null && newSticker.date == date && (int)newSticker.slot == s)
          {
            _stamp = rt;
            _stampImage = sticker.GetComponent<Image>();
            _stampTilt = Tilt(index);
          }
        }
      }

      if (_stampFx != null)
      {
        _stampFx.gameObject.SetActive(false);
      }
      if (_stamp != null)
      {
        // 찍기 전에는 안 보이게
        SetAlpha(_stampImage, 0f);
        _t = 0f;
      }
      _title.text = newSticker != null ? "칭찬 도장을 받았어요!" : "칭찬 스티커판";
      _summary.text = $"이번 주 칭찬 도장 {count}개";
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Update()
    {
      if (_stamp == null || _t < 0f)
      {
        return;
      }
      _t += Time.deltaTime;
      var t = _t - _stampDelay;
      if (t < 0f)
      {
        return;
      }

      if (t < _stampSec)
      {
        // 위에서 크게 내려와 찍힌다 (가속)
        var k = t / _stampSec;
        var scale = Mathf.Lerp(_stampStartScale, 1f, k * k);
        _stamp.localScale = new Vector3(scale, scale, 1f);
        _stamp.localEulerAngles = new Vector3(0f, 0f, _stampTilt - 25f * (1f - k));
        SetAlpha(_stampImage, Mathf.Clamp01(k * 2.5f));
        return;
      }

      var after = t - _stampSec;
      if (_stampFx != null && after < _fxSec)
      {
        if (!_stampFx.gameObject.activeSelf)
        {
          _stampFx.gameObject.SetActive(true);
          _stampFx.position = _stamp.position;
        }
        var f = after / _fxSec;
        _stampFx.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, f);
        SetAlpha(_stampFx.GetComponent<Image>(), 1f - f);
      }
      else if (_stampFx != null)
      {
        _stampFx.gameObject.SetActive(false);
      }

      // 찍힌 뒤 살짝 눌렸다 돌아온다
      var s = after < _settleSec ? 1f - 0.15f * Mathf.Sin(after / _settleSec * Mathf.PI) : 1f;
      _stamp.localScale = new Vector3(1f + (1f - s) * 0.5f, s, 1f);
      _stamp.localEulerAngles = new Vector3(0f, 0f, _stampTilt);
      SetAlpha(_stampImage, 1f);
      if (after >= Mathf.Max(_settleSec, _fxSec))
      {
        _t = -1f;
      }
    }

    /// <summary>칸마다 정해진 기울기 (다시 열어도 같은 모양)</summary>
    private float Tilt(int index)
    {
      return ((index * 37 % 23) / 22f * 2f - 1f) * _maxTilt;
    }

    private static void SetAlpha(Graphic g, float a)
    {
      if (g == null)
      {
        return;
      }
      var c = g.color;
      c.a = a;
      g.color = c;
    }
  }
}
