using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   안드로이드 뒤로가기(에디터에서는 Esc)를 누르면 게임을 끝낼지 묻는다. 창이 떠 있을 때 다시 누르면 닫힌다.
  ///   창이 떠 있는 동안은 게임 시간을 멈춘다 (양치 타이머, 입장 화면 등).
  ///   이 오브젝트는 항상 켜 두고, 창(_panel)만 켜고 끈다.
  /// </summary>
  public class QuitConfirmPanel : MonoBehaviour
  {
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _stayButton;

    private float _timeScale = 1f;

    public bool IsOpen => _panel.activeSelf;

    private void Awake()
    {
      _panel.SetActive(false);
      _quitButton.onClick.AddListener(Quit);
      _stayButton.onClick.AddListener(() => SetOpen(false));
    }

    private void Update()
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        SetOpen(!IsOpen);
      }
    }

    private void SetOpen(bool open)
    {
      if (open == IsOpen)
      {
        return;
      }
      if (open)
      {
        _timeScale = Time.timeScale;
        Time.timeScale = 0f;
      }
      else
      {
        Time.timeScale = _timeScale;
      }
      _panel.SetActive(open);
    }

    private void Quit()
    {
      Time.timeScale = _timeScale;
      SaveStore.Save();
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }
  }
}
