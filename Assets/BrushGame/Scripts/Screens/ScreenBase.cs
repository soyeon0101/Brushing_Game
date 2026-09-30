using UnityEngine;

namespace BrushGame
{
  /// <summary>GameFlow가 켜고 끄는 화면 하나</summary>
  public abstract class ScreenBase : MonoBehaviour
  {
    /// <summary>화면이 켜진 직후 (Awake 이후)</summary>
    public virtual void OnShow() { }

    /// <summary>화면이 꺼지기 직전</summary>
    public virtual void OnHide() { }
  }
}
