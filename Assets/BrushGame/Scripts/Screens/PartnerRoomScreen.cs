using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   파트너 방: 우주선 안 방에서 파트너 로봇이 돌아다니고 논다. 로봇을 누르면 반응한다.
  ///   양치로 받은 도장 하나가 밥 한 그릇. "밥 주기"를 누르면 로봇이 먹고 기뻐한다 (안 줘도 벌은 없다).
  ///   (첫 만남·이름 짓기는 다음 단계)
  /// </summary>
  public class PartnerRoomScreen : ScreenBase
  {
    [SerializeField] private PartnerRobot _robot;
    [SerializeField] private Button _backButton;

    [Header("밥 주기 (밥그릇 버튼)")]
    [SerializeField] private Button _feedButton;
    [Tooltip("남은 밥 수를 보여주는 동그라미 (0이면 숨김)")]
    [SerializeField] private GameObject _countBadge;
    [SerializeField] private Text _countText;
    [Tooltip("줄 밥이 없을 때만 보이는 안내 (\"양치하면 밥이 생겨요\")")]
    [SerializeField] private GameObject _noMealHint;

    private SaveData _data;

    public event Action BackPressed;

    public void Setup(SaveData data)
    {
      _data = data;
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
      _feedButton.onClick.AddListener(Feed);
    }

    public override void OnShow()
    {
      _robot.ResetState();
      Refresh();
    }

    private void Update()
    {
      // 먹는 동안에는 한 번 더 못 누르게
      _feedButton.interactable = _data != null && _data.PartnerMealsLeft > 0 && !_robot.IsEating;
    }

    private void Feed()
    {
      if (_data == null || _data.PartnerMealsLeft <= 0 || _robot.IsEating)
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
      var left = _data != null ? _data.PartnerMealsLeft : 0;
      _countBadge.SetActive(left > 0);
      _countText.text = left.ToString();
      _noMealHint.SetActive(left <= 0);
    }
  }
}
