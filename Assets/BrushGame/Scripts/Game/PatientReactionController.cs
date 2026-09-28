using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   상황에 맞는 환자 대사를 고르고 쿨타임을 관리한다. 표시는 ReactionToastUI가 한다.
  ///   우선순위: 손 인식 실패 > 과속 > 정상 복귀/진행/완료.
  /// </summary>
  public class PatientReactionController : MonoBehaviour
  {
    private const int PriorityHandLost = 0;
    private const int PriorityTooFast = 1;
    private const int PriorityPositive = 2;

    [SerializeField] private ReactionToastUI _toast;

    [Header("대사")]
    [SerializeField] private string _mouthOpenLine = "아~";
    [SerializeField] private string[] _tooFastLines = { "아야야~ 살살!", "조금만 천천히~" };
    [SerializeField] private string[] _handLostLines = { "칫솔이 안 보여!" };
    [SerializeField] private string[] _recoveredLines = { "응! 지금 좋아!", "그렇게 닦아줘!" };
    [SerializeField] private string _halfwayLine = "깨끗해지고 있어!";
    [SerializeField] private string _almostLine = "조금만 더!";
    [SerializeField] private string[] _completeLines = { "와! 깨끗해졌어!", "고마워!" };

    [Header("쿨타임 (초)")]
    [Tooltip("과속이 계속되면 이 간격으로 다시 알린다")]
    [SerializeField] private float _tooFastCooldown = 4f;
    [SerializeField] private float _handLostCooldown = 5f;
    [Tooltip("과속 후 이 시간 안에 정상으로 돌아오면 칭찬한다")]
    [SerializeField] private float _recoveredWindow = 5f;

    [Header("진행 대사 시점 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _halfwayAt = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _almostAt = 0.8f;

    [Header("아이콘 테두리 색")]
    [SerializeField] private Color _painColor = new Color(0.93f, 0.33f, 0.33f);
    [SerializeField] private Color _lostColor = new Color(0.6f, 0.6f, 0.65f);
    [SerializeField] private Color _happyColor = new Color(0.35f, 0.8f, 0.45f);

    private Color _patientColor = Color.white;
    private bool _wasHandLost;
    private bool _wasTooFast;
    private bool _halfwaySaid;
    private bool _almostSaid;
    private float _lastTooFastShown = float.NegativeInfinity;
    private float _lastHandLostShown = float.NegativeInfinity;
    private float _lastTooFastTime = float.NegativeInfinity;

    public void OnPatientStart(Color patientColor)
    {
      _patientColor = patientColor;
      _wasHandLost = false;
      _wasTooFast = false;
      _halfwaySaid = false;
      _almostSaid = false;
      _lastTooFastTime = float.NegativeInfinity;
    }

    /// <summary>환자가 자리에 앉아 입을 벌렸을 때</summary>
    public void OnMouthOpen()
    {
      Show(_mouthOpenLine, _happyColor, PriorityPositive);
    }

    /// <summary>치료 중 매 프레임 호출</summary>
    public void Tick(bool handLost, bool tooFast, bool brushing, float progress)
    {
      var now = Time.time;

      if (handLost)
      {
        if (!_wasHandLost || now - _lastHandLostShown >= _handLostCooldown)
        {
          Show(Pick(_handLostLines), _lostColor, PriorityHandLost);
          _lastHandLostShown = now;
        }
      }
      else if (_wasHandLost && _toast.CurrentPriority == PriorityHandLost)
      {
        // 손이 다시 보이면 안내를 바로 치운다
        _toast.Dismiss();
      }

      if (tooFast)
      {
        _lastTooFastTime = now;
        if (!_wasTooFast || now - _lastTooFastShown >= _tooFastCooldown)
        {
          Show(Pick(_tooFastLines), _painColor, PriorityTooFast);
          _lastTooFastShown = now;
        }
      }
      else if (_wasTooFast && brushing && now - _lastTooFastTime <= _recoveredWindow)
      {
        Show(Pick(_recoveredLines), _happyColor, PriorityPositive, force: true);
      }

      if (!_halfwaySaid && progress >= _halfwayAt && brushing && !tooFast)
      {
        _halfwaySaid = Show(_halfwayLine, _happyColor, PriorityPositive);
      }
      else if (!_almostSaid && progress >= _almostAt && brushing && !tooFast)
      {
        _almostSaid = Show(_almostLine, _happyColor, PriorityPositive);
      }

      _wasHandLost = handLost;
      _wasTooFast = tooFast;
    }

    public void OnTreatmentComplete()
    {
      Show(Pick(_completeLines), _happyColor, PriorityPositive, force: true);
    }

    /// <summary>
    ///   더 중요한 알림이 떠 있으면 덜 중요한 건 띄우지 않는다. force면 무조건 교체.
    /// </summary>
    private bool Show(string line, Color moodColor, int priority, bool force = false)
    {
      if (!force && _toast.IsShowing && _toast.CurrentPriority < priority)
      {
        return false;
      }
      _toast.Show(line, moodColor, _patientColor, priority);
      return true;
    }

    private static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];
  }
}
