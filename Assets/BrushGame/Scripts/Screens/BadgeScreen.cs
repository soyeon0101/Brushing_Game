using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  [Serializable]
  public class BadgeCard
  {
    public GameObject root;
    public Image icon;
    [Tooltip("배지 그림이 없을 때 임시 원 위에 쓰는 목표 (7일, 1주 등)")]
    public Text iconLabel;
    public Text name;
    [Tooltip("받았으면 설명, 못 받았으면 진행도")]
    public Text info;
    [Tooltip("방금 받은 배지에만 켜지는 표시")]
    public GameObject newTag;
  }

  /// <summary>
  ///   반짝 배지함. 받은 배지는 밝게, 못 받은 배지는 흐리게 진행도와 함께 보여준다.
  ///   치료 후 새 배지를 받았으면 그 배지가 하나씩 퐁 튀어나온다.
  /// </summary>
  public class BadgeScreen : ScreenBase
  {
    [SerializeField] private Text _title;
    [SerializeField] private Text _summary;
    [Tooltip("GameCatalog.badges 순서")]
    [SerializeField] private BadgeCard[] _cards;
    [SerializeField] private Button _backButton;

    [Header("색 (배지 그림이 없을 때)")]
    [SerializeField] private Color _earnedColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color _lockedColor = new Color(0.85f, 0.84f, 0.9f);
    [Tooltip("배지 그림이 있을 때 못 받은 배지를 그림자처럼 보이게 하는 색")]
    [SerializeField] private Color _lockedArtTint = new Color(0.3f, 0.28f, 0.4f, 0.45f);

    [Header("새 배지 연출")]
    [SerializeField, Min(0f)] private float _popDelay = 0.5f;
    [SerializeField, Min(0f)] private float _popInterval = 0.6f;
    [SerializeField, Min(0.05f)] private float _popSec = 0.45f;

    private readonly List<RectTransform> _popping = new List<RectTransform>();
    private float _t = -1f;

    public event Action BackPressed;

    /// <param name="newBadges">방금 받은 배지. 배지함만 보러 온 경우 null</param>
    public void Setup(GameCatalog catalog, SaveData data, IList<BadgeInfo> newBadges)
    {
      var today = DateTime.Today;
      var earnedCount = 0;
      _popping.Clear();

      for (var i = 0; i < _cards.Length; i++)
      {
        var card = _cards[i];
        var used = i < catalog.badges.Length;
        card.root.SetActive(used);
        if (!used)
        {
          continue;
        }
        var badge = catalog.badges[i];
        var earned = data.badgeIds.Contains(badge.id);
        var isNew = newBadges != null && newBadges.Contains(badge);
        if (earned)
        {
          earnedCount++;
        }

        var hasArt = badge.icon != null;
        ArtSlot.Apply(card.icon, badge.icon);
        card.icon.color = hasArt ? (earned ? Color.white : _lockedArtTint) : (earned ? _earnedColor : _lockedColor);
        card.iconLabel.gameObject.SetActive(!hasArt);
        card.iconLabel.text = badge.TargetLabel;
        card.name.text = badge.displayName;
        card.info.text = earned
          ? badge.description
          : $"{Mathf.Min(BadgeRules.Progress(badge, data, today), badge.target)} / {badge.TargetLabel}";
        card.newTag.SetActive(isNew);

        var rt = (RectTransform)card.root.transform;
        rt.localScale = isNew ? Vector3.zero : Vector3.one;
        if (isNew)
        {
          _popping.Add(rt);
        }
      }

      var hasNew = _popping.Count > 0;
      _t = hasNew ? 0f : -1f;
      _title.text = hasNew ? "새 배지를 받았어요!" : "반짝 배지";
      _summary.text = $"모은 배지 {earnedCount} / {catalog.badges.Length}";
    }

    private void Awake()
    {
      _backButton.onClick.AddListener(() => BackPressed?.Invoke());
    }

    private void Update()
    {
      if (_t < 0f)
      {
        return;
      }
      _t += Time.deltaTime;
      var done = true;
      for (var i = 0; i < _popping.Count; i++)
      {
        var k = Mathf.Clamp01((_t - _popDelay - _popInterval * i) / _popSec);
        _popping[i].localScale = Vector3.one * EaseOutBack(k);
        done &= k >= 1f;
      }
      if (done)
      {
        _t = -1f;
      }
    }

    private static float EaseOutBack(float k)
    {
      const float c1 = 1.70158f;
      const float c3 = c1 + 1f;
      var x = k - 1f;
      return 1f + c3 * x * x * x + c1 * x * x;
    }
  }
}
