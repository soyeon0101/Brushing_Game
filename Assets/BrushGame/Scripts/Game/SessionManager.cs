using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   환자 입장 → 치료 → 완료 → 퇴장 → 다음 환자 흐름과 치료 진행도(CleanProgress)의 유일한 기준.
  ///   입력은 IBrushInput만 본다 (키보드 / 카메라 교체 가능).
  ///   입력 해석 우선순위: 손 안 보임 > 과속 > 양치 > 대기
  /// </summary>
  public class SessionManager : MonoBehaviour
  {
    [Tooltip("IBrushInput을 구현한 컴포넌트 (KeyboardBrushInput / CameraBrushInput)")]
    [SerializeField] private MonoBehaviour _brushInputSource;
    [SerializeField] private Patient _patient;
    [SerializeField] private PatientReactionController _reactions;
    [Tooltip("간단한 진행 표시 (없어도 됨)")]
    [SerializeField] private Image _progressFill;

    [Header("치료")]
    [Tooltip("정상 속도로 이만큼 닦으면 치료 완료 (초)")]
    [SerializeField, Min(1f)] private float _brushSecondsToClean = 11.7f;
    [Tooltip("과속 중 진행 속도 배율. 오검출이어도 화면이 완전히 멈추지 않게 (0.3~0.5)")]
    [SerializeField, Range(0f, 1f)] private float _tooFastSpeedMultiplier = 0.4f;
    [Tooltip("손이 이만큼 안 보이면 안내한다 (초)")]
    [SerializeField, Min(0f)] private float _handLostDelay = 1.5f;
    [Tooltip("이 진행도 구간에서 환자 표정이 점점 편안해진다")]
    [SerializeField] private Vector2 _comfortRange = new Vector2(0.7f, 0.9f);
    [Tooltip("완료 연출을 보여주는 시간 (초)")]
    [SerializeField, Min(0f)] private float _completeHoldSec = 2.5f;

    private IBrushInput _input;
    private float _progress;
    private float _handLostTimer;
    private int _patientIndex;

    public float Progress => _progress;

    private void Awake()
    {
      _input = _brushInputSource as IBrushInput;
      if (_input == null)
      {
        Debug.LogError($"[SessionManager] {_brushInputSource} 는 IBrushInput이 아닙니다.", this);
        enabled = false;
      }
    }

    private void OnValidate()
    {
      if (_brushInputSource != null && !(_brushInputSource is IBrushInput))
      {
        Debug.LogWarning($"[SessionManager] {_brushInputSource.GetType().Name} 는 IBrushInput을 구현하지 않습니다.", this);
        _brushInputSource = null;
      }
    }

    private void Start()
    {
      StartCoroutine(RunPatients());
    }

    private IEnumerator RunPatients()
    {
      while (true)
      {
        _progress = 0f;
        _handLostTimer = 0f;
        UpdateProgressFill();
        _patient.Prepare(_patientIndex);
        _reactions.OnPatientStart(_patient.SkinColor);
        yield return _patient.Enter();

        _reactions.OnMouthOpen();
        yield return Treat();
        UpdateProgressFill();

        _patient.Mouth.SetState(1f, false, false);
        _patient.Mouth.PlayComplete();
        _patient.PlayHappy();
        _reactions.OnTreatmentComplete();
        yield return new WaitForSeconds(_completeHoldSec);

        yield return _patient.Leave();
        _patientIndex++;
      }
    }

    private IEnumerator Treat()
    {
      while (_progress < 1f)
      {
        var dt = Time.deltaTime;

        // 손이 안 보이면 양치/과속 판정은 적용하지 않는다
        var visible = _input.HandVisible;
        _handLostTimer = visible ? 0f : _handLostTimer + dt;
        var handLost = _handLostTimer >= _handLostDelay;
        var brushing = visible && _input.IsBrushing;
        var tooFast = brushing && _input.IsTooFast;

        // 진행은 닦고 있는 시간 기준. 과속이면 느려질 뿐 멈추거나 줄지 않는다
        if (brushing)
        {
          var multiplier = tooFast ? _tooFastSpeedMultiplier : 1f;
          _progress = Mathf.Min(1f, _progress + dt * multiplier / _brushSecondsToClean);
        }

        _patient.SetExpression(tooFast ? PatientExpression.Pain : PatientExpression.Normal);
        _patient.SetComfort(Mathf.InverseLerp(_comfortRange.x, _comfortRange.y, _progress));
        _patient.Mouth.SetState(_progress, brushing, tooFast);
        _reactions.Tick(handLost, tooFast, brushing, _progress);
        UpdateProgressFill();

        yield return null;
      }
    }

    private void UpdateProgressFill()
    {
      if (_progressFill != null)
      {
        _progressFill.fillAmount = _progress;
      }
    }
  }
}
