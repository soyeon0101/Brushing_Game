using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   치료 완료 다음, 칭찬 도장 전: 양치 중 찍은 사진 4장을 액자에 담아 보여준다.
  ///   액자마다 사진 배치와 모양(하트, 동그라미, 폴라로이드 …)이 다르다. 아래 동그라미 버튼으로 액자를 바꾸고, 고른 액자는 다음에도 기억한다.
  ///   못 찍은 칸(카메라 없이 키보드로 했을 때 등)은 캐릭터 그림으로 채운다.
  /// </summary>
  public class PhotoFrameScreen : ScreenBase
  {
    public const int PhotosPerFrame = 4;

    [Tooltip("액자마다 하나 (장식과 사진 칸 포함). 고른 것만 켜진다")]
    [SerializeField] private GameObject[] _frames;
    [Tooltip("_frames와 같은 순서")]
    [SerializeField] private Button[] _frameButtons;
    [Tooltip("고른 액자 버튼에만 켜지는 표시 (_frameButtons와 같은 순서)")]
    [SerializeField] private GameObject[] _frameButtonMarks;
    [Tooltip("액자마다 사진 칸 4개씩 (액자 순서 × 구역 순서). 튀어나오는 연출에 쓴다")]
    [SerializeField] private RectTransform[] _slots;
    [Tooltip("_slots와 같은 순서")]
    [SerializeField] private RawImage[] _photos;
    [Tooltip("사진이 없을 때 보이는 캐릭터 그림 (_slots와 같은 순서)")]
    [SerializeField] private Image[] _emptyPhotos;
    [SerializeField] private Text _caption;
    [SerializeField] private Text _date;
    [SerializeField] private Button _nextButton;

    [Header("등장 연출")]
    [SerializeField, Min(0.05f)] private float _popSec = 0.35f;
    [Tooltip("사진이 한 장씩 나타나는 간격 (초)")]
    [SerializeField, Min(0f)] private float _popGap = 0.18f;

    private SaveData _data;
    private int _selected;
    private float _t = -1f;

    public event Action Confirmed;

    public void Setup(SaveData data, Texture2D[] photos, PlayerCharacter character)
    {
      _data = data;
      for (var i = 0; i < _photos.Length; i++)
      {
        var index = i % PhotosPerFrame;
        var photo = photos != null && index < photos.Length ? photos[index] : null;
        _photos[i].texture = photo;
        _photos[i].gameObject.SetActive(photo != null);
        _emptyPhotos[i].gameObject.SetActive(photo == null);
        ArtSlot.Apply(_emptyPhotos[i], character?.Portrait);
      }
      var name = data.profile != null ? data.profile.nickname : "";
      _caption.text = string.IsNullOrEmpty(name) ? "반짝반짝 양치 사진" : $"{name}의 반짝반짝 양치 사진";
      var now = DateTime.Now;
      _date.text = $"{now.Year}. {now.Month}. {now.Day}. {SaveData.SlotName(SaveData.SlotAt(now))}";
      SelectFrame(Mathf.Clamp(data.photoFrame, 0, _frames.Length - 1), false);
    }

    public override void OnShow()
    {
      _t = 0f;
      Pop(0f);
    }

    private void Awake()
    {
      for (var i = 0; i < _frameButtons.Length; i++)
      {
        var index = i;
        _frameButtons[i].onClick.AddListener(() => SelectFrame(index, true));
      }
      _nextButton.onClick.AddListener(() => Confirmed?.Invoke());
    }

    private void Update()
    {
      if (_t < 0f)
      {
        return;
      }
      _t += Time.deltaTime;
      Pop(_t);
      if (_t > _popGap * PhotosPerFrame + _popSec)
      {
        _t = -1f;
      }
    }

    /// <summary>고른 액자의 사진이 차례로 통통 튀어나온다</summary>
    private void Pop(float t)
    {
      for (var i = 0; i < _slots.Length; i++)
      {
        var order = i - _selected * PhotosPerFrame;
        var s = 1f;
        if (order >= 0 && order < PhotosPerFrame)
        {
          var k = Mathf.Clamp01((t - _popGap * order) / _popSec);
          s = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)) : 1f;
        }
        _slots[i].localScale = new Vector3(s, s, 1f);
      }
    }

    private void SelectFrame(int index, bool save)
    {
      _selected = index;
      for (var i = 0; i < _frames.Length; i++)
      {
        _frames[i].SetActive(i == index);
      }
      for (var i = 0; i < _frameButtonMarks.Length; i++)
      {
        _frameButtonMarks[i].SetActive(i == index);
      }
      if (save)
      {
        // 액자를 바꾸면 새 액자의 사진이 다시 튀어나온다
        _t = 0f;
        Pop(0f);
        if (_data != null && _data.photoFrame != index)
        {
          _data.photoFrame = index;
          SaveStore.Save();
        }
      }
    }
  }
}
