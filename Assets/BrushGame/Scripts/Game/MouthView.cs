using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  [Serializable]
  public class ToothView
  {
    public Image tooth;
    [Tooltip("깨끗해질수록 보이는 광택")]
    public Image gloss;
    [Tooltip("없으면 처음부터 비교적 깨끗한 치아")]
    public CanvasGroup plaque;
    [Tooltip("치태를 덮었다가 걷히는 거품")]
    public CanvasGroup foamCover;
  }

  [Serializable]
  public class GermView
  {
    public CanvasGroup group;
    [Tooltip("세균을 가두는 거품 방울")]
    public CanvasGroup bubble;
  }

  /// <summary>
  ///   치료 진행도(0~1)를 입속 모습으로 보여준다. 실제 닦는 위치와는 무관한 진행 연출이다.
  ///   언제 무엇이 사라질지는 TreatmentPlan(환자마다 섞인 일정)이 정하고, 여기서는 연출만 한다.
  ///   치태: 거품에 덮임 → 거품 걷힘 → 깨끗한 치아 + 반짝
  ///   음식물: 흔들림 → 톡 튕겨 떨어짐
  ///   세균: 거품 방울에 갇힘 → 떠올라 화면 밖으로 날아감
  /// </summary>
  public class MouthView : MonoBehaviour
  {
    [Header("요소")]
    [SerializeField] private RectTransform _content;   // 양치 중 흔들리는 입속 전체
    [SerializeField] private ToothView[] _teeth;
    [SerializeField] private CanvasGroup[] _foods;
    [SerializeField] private GermView[] _germs;
    [Tooltip("양치하는 동안 입 전체에 생기는 거품")]
    [SerializeField] private Image[] _foams;
    [Tooltip("마지막 광택 단계의 반짝임")]
    [SerializeField] private Image[] _sparkles;
    [Tooltip("오염이 사라질 때 터지는 작은 반짝임 원본. 실행 시 복제해서 쓴다")]
    [SerializeField] private Image _popTemplate;

    [Header("치료 일정 (환자마다 순서와 간격을 섞음)")]
    [SerializeField] private TreatmentCategorySettings _plaqueSchedule = new TreatmentCategorySettings
    {
      removeRange = new Vector2(0.08f, 0.88f), windowLength = new Vector2(0.08f, 0.14f), jitter = 0.35f,
    };
    [SerializeField] private TreatmentCategorySettings _foodSchedule = new TreatmentCategorySettings
    {
      removeRange = new Vector2(0.14f, 0.68f), windowLength = new Vector2(0.04f, 0.08f), jitter = 0.35f,
    };
    [SerializeField] private TreatmentCategorySettings _germSchedule = new TreatmentCategorySettings
    {
      removeRange = new Vector2(0.35f, 0.9f), windowLength = new Vector2(0.08f, 0.14f), jitter = 0.35f,
    };
    [Tooltip("이 진행도부터 남은 거품이 걷히고 광택 단계")]
    [SerializeField, Range(0f, 1f)] private float _finishFrom = 0.92f;

    [Header("색")]
    [SerializeField] private Color _dirtyToothColor = new Color(1f, 0.93f, 0.7f);
    [SerializeField] private Color _slightlyDirtyToothColor = new Color(0.99f, 0.97f, 0.9f);
    [SerializeField] private Color _cleanToothColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float _glossMax = 0.7f;

    [Header("반응")]
    [SerializeField] private float _brushShake = 5f;
    [SerializeField] private float _painShake = 12f;
    [SerializeField] private float _foamRiseSpeed = 3f;
    [SerializeField] private float _foamFallSpeed = 0.8f;

    [Header("음식물 톡 떨어짐")]
    [SerializeField] private float _foodFallSec = 0.8f;
    [SerializeField] private float _foodSideSpeed = 140f;
    [SerializeField] private float _foodHopSpeed = 260f;
    [SerializeField] private float _foodGravity = 1600f;

    [Header("세균 날아감")]
    [SerializeField] private float _germFlySec = 1.6f;
    [SerializeField] private float _germFlyHeight = 1500f;
    [SerializeField] private float _germSway = 40f;

    [Header("작은 반짝임")]
    [SerializeField] private int _popPoolSize = 16;
    [SerializeField] private float _popSec = 0.45f;
    [SerializeField] private float _popScale = 1.4f;
    [Tooltip("치료 완료 시 치아마다 반짝임이 터지는 간격 (초)")]
    [SerializeField] private float _completeStagger = 0.07f;
    [SerializeField] private float _completeFlashSec = 1.5f;

    private TreatmentPlan _plan;
    private System.Random _rng;
    private Vector2 _contentBasePos;

    private int[] _toothPlaque;      // 치아 → 일정의 치태 번호 (-1이면 치태 없음)
    private bool[] _toothCleared;

    private RectTransform[] _foodRects;
    private Vector2[] _foodBasePos;
    private float[] _foodFallT;
    private float[] _foodDir;

    private RectTransform[] _germRects;
    private RectTransform[] _bubbleRects;
    private Vector2[] _germBasePos;
    private float[] _germFlyT;

    private Image[] _pops;
    private float[] _popT;
    private int _nextPop;

    private float _progress;
    private float _foam;
    private bool _brushing;
    private bool _pain;
    private float _completeT = -1f;
    private int _completePopped;

    private void Awake()
    {
      _rng = new System.Random();
      _contentBasePos = _content.anchoredPosition;

      _toothPlaque = new int[_teeth.Length];
      _toothCleared = new bool[_teeth.Length];
      var plaqueCount = 0;
      for (var i = 0; i < _teeth.Length; i++)
      {
        _toothPlaque[i] = _teeth[i].plaque != null ? plaqueCount++ : -1;
      }

      _foodRects = new RectTransform[_foods.Length];
      _foodBasePos = new Vector2[_foods.Length];
      _foodFallT = new float[_foods.Length];
      _foodDir = new float[_foods.Length];
      for (var i = 0; i < _foods.Length; i++)
      {
        _foodRects[i] = (RectTransform)_foods[i].transform;
        _foodBasePos[i] = _foodRects[i].anchoredPosition;
      }

      _germRects = new RectTransform[_germs.Length];
      _bubbleRects = new RectTransform[_germs.Length];
      _germBasePos = new Vector2[_germs.Length];
      _germFlyT = new float[_germs.Length];
      for (var i = 0; i < _germs.Length; i++)
      {
        _germRects[i] = (RectTransform)_germs[i].group.transform;
        _bubbleRects[i] = (RectTransform)_germs[i].bubble.transform;
        _germBasePos[i] = _germRects[i].anchoredPosition;
      }

      _pops = new Image[_popPoolSize];
      _popT = new float[_popPoolSize];
      for (var i = 0; i < _popPoolSize; i++)
      {
        _pops[i] = Instantiate(_popTemplate, _popTemplate.transform.parent);
        _pops[i].name = "Pop";
        _pops[i].transform.SetAsLastSibling();
        _popT[i] = -1f;
        SetAlpha(_pops[i], 0f);
      }
      _popTemplate.gameObject.SetActive(false);

      _plan = new TreatmentPlan(plaqueCount, _foods.Length, _germs.Length);
      ResetDirty();
    }

    /// <summary>새 환자: 새 치료 일정을 만들고 더러운 상태로 되돌린다</summary>
    public void ResetDirty()
    {
      _plan.Randomize(_rng, _plaqueSchedule, _foodSchedule, _germSchedule);
      _progress = 0f;
      _foam = 0f;
      _completeT = -1f;
      _completePopped = 0;

      for (var i = 0; i < _teeth.Length; i++)
      {
        _toothCleared[i] = false;
      }
      for (var i = 0; i < _foods.Length; i++)
      {
        _foodFallT[i] = -1f;
        _foodDir[i] = _rng.Next(2) == 0 ? -1f : 1f;
        _foodRects[i].anchoredPosition = _foodBasePos[i];
        _foodRects[i].localRotation = Quaternion.identity;
        _foods[i].alpha = 1f;
      }
      for (var i = 0; i < _germs.Length; i++)
      {
        _germFlyT[i] = -1f;
        _germRects[i].anchoredPosition = _germBasePos[i];
        _germRects[i].localRotation = Quaternion.identity;
        _germRects[i].localScale = Vector3.one;
        _germs[i].group.alpha = 1f;
        _germs[i].bubble.alpha = 0f;
      }
      for (var i = 0; i < _pops.Length; i++)
      {
        _popT[i] = -1f;
        SetAlpha(_pops[i], 0f);
      }
      Refresh(0f);
    }

    public void SetState(float progress, bool brushing, bool pain)
    {
      _progress = progress;
      _brushing = brushing;
      _pain = pain;
    }

    /// <summary>치료 완료: 치아 전체에 짧게 반짝임</summary>
    public void PlayComplete()
    {
      _completeT = 0f;
      _completePopped = 0;
    }

    private void Update()
    {
      var dt = Time.deltaTime;

      // 양치가 감지되는 즉시 거품. 마지막 단계에서는 거품이 걷힌다
      var foamTarget = _brushing && _progress < _finishFrom ? 1f : 0f;
      _foam = Mathf.MoveTowards(_foam, foamTarget, (foamTarget > _foam ? _foamRiseSpeed : _foamFallSpeed) * dt);

      var shake = _pain ? _painShake : _brushing ? _brushShake : 0f;
      var t = Time.time;
      _content.anchoredPosition = _contentBasePos + new Vector2(Mathf.Sin(t * 47f), Mathf.Sin(t * 31f + 1f)) * shake;

      Refresh(dt);
      UpdateComplete(dt);
      UpdatePops(dt);
    }

    private void Refresh(float dt)
    {
      var p = _progress;
      var time = Time.time;
      var flash = _completeT >= 0f ? Mathf.Clamp01(1f - _completeT / _completeFlashSec) : 0f;
      var finish = Mathf.InverseLerp(_finishFrom, 1f, p);

      // 치아: 치태가 거품에 덮였다가 걷히며 깨끗한 치아가 드러난다
      for (var i = 0; i < _teeth.Length; i++)
      {
        var view = _teeth[i];
        float clean;
        if (_toothPlaque[i] < 0)
        {
          clean = p;
          view.tooth.color = Color.Lerp(_slightlyDirtyToothColor, _cleanToothColor, p);
        }
        else
        {
          var k = _plan.Plaques[_toothPlaque[i]].Evaluate(p);
          view.plaque.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, k));
          var cover = Mathf.Sin(k * Mathf.PI);
          view.foamCover.alpha = cover;
          var bob = 1f + (_brushing ? Mathf.Sin(time * 12f + i) * 0.06f : 0f);
          view.foamCover.transform.localScale = new Vector3(bob * (0.6f + 0.4f * cover), bob * (0.6f + 0.4f * cover), 1f);
          clean = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, k));
          view.tooth.color = Color.Lerp(_dirtyToothColor, _cleanToothColor, clean);

          if (!_toothCleared[i] && k >= 1f)
          {
            _toothCleared[i] = true;
            SpawnPop(LocalPos(view.tooth.transform));
          }
          else if (_toothCleared[i] && k < 1f)
          {
            _toothCleared[i] = false;
          }
        }
        var twinkle = 0.85f + 0.15f * Mathf.Sin(time * 5f + i * 1.3f);
        SetAlpha(view.gloss, Mathf.Max(_glossMax * clean * Mathf.Lerp(0.5f, 1f, finish) * twinkle, flash));
      }

      for (var i = 0; i < _foods.Length; i++)
      {
        UpdateFood(i, p, dt, time);
      }
      for (var i = 0; i < _germs.Length; i++)
      {
        UpdateGerm(i, p, dt, time);
      }

      // 입 전체 거품: 닦는 동안 차오르고 보글거림
      for (var i = 0; i < _foams.Length; i++)
      {
        var reveal = Mathf.Clamp01(_foam * _foams.Length - i);
        SetAlpha(_foams[i], 0.9f * reveal);
        var bubble = reveal * (0.9f + 0.12f * Mathf.Sin(time * 9f + i * 1.3f));
        _foams[i].rectTransform.localScale = new Vector3(bubble, bubble, 1f);
      }

      // 마지막 광택 단계: 반짝임이 하나씩 켜짐
      for (var i = 0; i < _sparkles.Length; i++)
      {
        var k = Mathf.Clamp01(finish * _sparkles.Length - i);
        var twinkle = 0.6f + 0.4f * Mathf.Sin(time * 6f + i * 1.7f);
        SetAlpha(_sparkles[i], Mathf.Max(k * twinkle, flash));
        var scale = 0.6f + 0.4f * k + flash * 0.6f;
        _sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
      }
    }

    private void UpdateFood(int i, float p, float dt, float time)
    {
      var rt = _foodRects[i];
      if (_foodFallT[i] >= 0f)
      {
        // 톡 튕겨 올랐다가 떨어짐
        var ft = _foodFallT[i] += dt;
        var dir = _foodDir[i];
        rt.anchoredPosition = _foodBasePos[i] + new Vector2(dir * _foodSideSpeed * ft, _foodHopSpeed * ft - 0.5f * _foodGravity * ft * ft);
        rt.localRotation = Quaternion.Euler(0f, 0f, -dir * 360f * ft);
        _foods[i].alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, ft / _foodFallSec));
        return;
      }

      var w = _plan.Foods[i];
      if (p >= w.end)
      {
        _foodFallT[i] = 0f;
        SpawnPop(_foodBasePos[i]);
        return;
      }
      // 제거 직전 구간에서 닦는 중이면 점점 크게 흔들림
      var k = w.Evaluate(p);
      var wobble = _brushing && k > 0f ? Mathf.Sin(time * 30f + i) * (4f + 14f * k) : 0f;
      rt.localRotation = Quaternion.Euler(0f, 0f, wobble);
    }

    private void UpdateGerm(int i, float p, float dt, float time)
    {
      var rt = _germRects[i];
      if (_germFlyT[i] >= 0f)
      {
        // 거품 방울째 둥실 떠올라 화면 밖으로
        var ft = _germFlyT[i] += dt;
        var k = Mathf.Clamp01(ft / _germFlySec);
        rt.anchoredPosition = _germBasePos[i] + new Vector2(Mathf.Sin(ft * 5f + i) * _germSway, _germFlyHeight * k * k);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(ft * 4f) * 12f);
        _germs[i].group.alpha = 1f - Mathf.InverseLerp(0.7f, 1f, k);
        return;
      }

      var w = _plan.Germs[i];
      if (p >= w.end)
      {
        _germFlyT[i] = 0f;
        _germs[i].bubble.alpha = 0.85f;
        _bubbleRects[i].localScale = Vector3.one;
        SpawnPop(_germBasePos[i]);
        return;
      }

      // 거품 방울이 점점 둘러쌈. 평소엔 장난스럽게 통통, 닦는 중엔 버둥
      var bk = w.Evaluate(p);
      var grow = 1f - (1f - bk) * (1f - bk);
      _germs[i].bubble.alpha = bk > 0f ? 0.3f + 0.55f * bk : 0f;
      _bubbleRects[i].localScale = new Vector3(grow, grow, 1f);
      var bounce = Mathf.Abs(Mathf.Sin(time * 3f + i * 2f)) * 6f;
      var wiggle = _brushing ? Mathf.Sin(time * 22f + i) * 6f : 0f;
      rt.anchoredPosition = _germBasePos[i] + new Vector2(wiggle, bounce);
      rt.localRotation = Quaternion.Euler(0f, 0f, _brushing ? Mathf.Sin(time * 18f + i) * 8f : 0f);
    }

    private void UpdateComplete(float dt)
    {
      if (_completeT < 0f)
      {
        return;
      }
      _completeT += dt;
      // 치아마다 차례로 반짝
      while (_completePopped < _teeth.Length && _completeT >= _completePopped * _completeStagger)
      {
        SpawnPop(LocalPos(_teeth[_completePopped].tooth.transform));
        _completePopped++;
      }
    }

    private void UpdatePops(float dt)
    {
      for (var i = 0; i < _pops.Length; i++)
      {
        if (_popT[i] < 0f)
        {
          continue;
        }
        _popT[i] += dt;
        var k = _popT[i] / _popSec;
        if (k >= 1f)
        {
          _popT[i] = -1f;
          SetAlpha(_pops[i], 0f);
          continue;
        }
        var s = Mathf.Sin(k * Mathf.PI) * _popScale;
        var rt = _pops[i].rectTransform;
        rt.localScale = new Vector3(s, s, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * k);
        SetAlpha(_pops[i], 1f - k * k);
      }
    }

    private void SpawnPop(Vector2 localPos)
    {
      var i = _nextPop;
      _nextPop = (_nextPop + 1) % _pops.Length;
      _popT[i] = 0f;
      _pops[i].rectTransform.anchoredPosition = localPos;
    }

    private Vector2 LocalPos(Transform target)
    {
      return _content.InverseTransformPoint(target.position);
    }

    private static void SetAlpha(Graphic g, float a)
    {
      var c = g.color;
      c.a = a;
      g.color = c;
    }
  }
}
