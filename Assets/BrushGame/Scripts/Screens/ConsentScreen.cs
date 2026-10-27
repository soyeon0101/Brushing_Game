using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   첫 실행 1단계 (보호자용): [필수] 개인정보 수집·이용 동의, [필수] 카메라 권한 이용 동의.
  ///   둘 다 체크해야 다음(회원가입)으로 간다. 카메라 사진은 기기에만 저장되고 서버에 저장되지 않음을 여기서 알린다.
  /// </summary>
  public class ConsentScreen : ScreenBase
  {
    [Serializable]
    public class CheckRow
    {
      public Button button;
      [Tooltip("체크됐을 때만 켜지는 표시")]
      public GameObject mark;
    }

    [SerializeField] private CheckRow _all;
    [SerializeField] private CheckRow _privacy;
    [SerializeField] private CheckRow _camera;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _backButton;

    private bool _privacyOn;
    private bool _cameraOn;

    public event Action Confirmed;
    public event Action BackPressed;

    public void Setup(GuardianAccount account)
    {
      _privacyOn = account != null && account.agreedPrivacy;
      _cameraOn = account != null && account.agreedCamera;
      Refresh();
    }

    private void Awake()
    {
      _all.button.onClick.AddListener(() =>
      {
        var on = !(_privacyOn && _cameraOn);
        _privacyOn = on;
        _cameraOn = on;
        Refresh();
      });
      _privacy.button.onClick.AddListener(() =>
      {
        _privacyOn = !_privacyOn;
        Refresh();
      });
      _camera.button.onClick.AddListener(() =>
      {
        _cameraOn = !_cameraOn;
        Refresh();
      });
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke());
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Refresh()
    {
      _all.mark.SetActive(_privacyOn && _cameraOn);
      _privacy.mark.SetActive(_privacyOn);
      _camera.mark.SetActive(_cameraOn);
      _nextButton.interactable = _privacyOn && _cameraOn;
    }
  }
}
