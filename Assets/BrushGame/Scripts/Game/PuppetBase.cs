using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   부위별로 나눈 3D 캐릭터 하나. 전용 카메라가 이 캐릭터만 RenderTexture에 그리고, UI의 RawImage가 그걸 보여준다.
  ///   그래서 UI 쪽의 위치/크기 연출(앞으로 뛰어들기, 흔들림, 찌그러짐)은 그대로 쓰고, 3D 쪽은 팔·날개·귀 같은 부위만 움직인다.
  ///   부위의 처음 회전을 기억해 두고, 매 프레임 그 위에 동작 각도를 더한다.
  /// </summary>
  public abstract class PuppetBase : MonoBehaviour
  {
    [SerializeField] private Camera _camera;
    [Tooltip("이 캐릭터를 보여줄 UI 자리")]
    [SerializeField] private RawImage _target;
    [SerializeField, Min(64)] private int _textureSize = 512;
    [Tooltip("관객(카메라) 쪽에서 봤을 때 상대 쪽으로 몸을 트는 각도")]
    [SerializeField] private float _facingYaw = 25f;

    private RenderTexture _texture;
    private Transform[] _parts;
    private Quaternion[] _baseRotations;
    private Vector3 _baseScale;
    private Vector3 _basePosition;

    /// <summary>움직일 부위들 (처음 회전을 기억한다)</summary>
    protected abstract Transform[] Parts { get; }

    public RawImage Target => _target;

    protected virtual void Awake()
    {
      _parts = Parts;
      _baseRotations = new Quaternion[_parts.Length];
      for (var i = 0; i < _parts.Length; i++)
      {
        _baseRotations[i] = _parts[i] != null ? _parts[i].localRotation : Quaternion.identity;
      }
      _baseScale = transform.localScale;
      _basePosition = transform.localPosition;
    }

    private void OnEnable()
    {
      if (_texture == null)
      {
        _texture = new RenderTexture(_textureSize, _textureSize, 24, RenderTextureFormat.ARGB32) { name = $"{name} View" };
        _texture.Create();
      }
      _camera.targetTexture = _texture;
      _target.texture = _texture;
    }

    private void OnDestroy()
    {
      if (_texture != null)
      {
        _texture.Release();
        Destroy(_texture);
      }
    }

    /// <summary>UI 자리, 3D 캐릭터, 전용 카메라를 함께 켜고 끈다 (안 보일 때는 그리지 않는다)</summary>
    public void SetVisible(bool visible)
    {
      gameObject.SetActive(visible);
      _camera.gameObject.SetActive(visible);
      _target.enabled = visible;
    }

    /// <summary>부위 i를 처음 회전에서 euler만큼 더 돌린다</summary>
    protected void Rotate(int part, Vector3 euler)
    {
      if (_parts != null && _parts[part] != null)
      {
        _parts[part].localRotation = _baseRotations[part] * Quaternion.Euler(euler);
      }
    }

    /// <summary>몸 전체: 제자리 기준 이동, 방향(몸 틀기 포함), 찌그러짐</summary>
    protected void Root(Vector3 offset, Vector3 euler, Vector3 squash)
    {
      // 아직 한 번도 켜지지 않았으면(Awake 전) 처음 자세를 모르므로 건드리지 않는다
      if (_parts == null)
      {
        return;
      }
      transform.localPosition = _basePosition + offset;
      transform.localRotation = Quaternion.Euler(euler.x, _facingYaw + euler.y, euler.z);
      transform.localScale = Vector3.Scale(_baseScale, squash);
    }

    /// <summary>1 → 0으로 튕기며 줄어드는 값 (맞은 뒤 흔들림 등)</summary>
    protected static float Wobble(float k, float cycles) => Mathf.Sin(k * Mathf.PI * 2f * cycles) * (1f - k);

    protected static float Smooth(float k) => k * k * (3f - 2f * k);
  }
}
