using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  public class TitleScreen : ScreenBase
  {
    [SerializeField] private Button _startButton;

    public event Action StartPressed;

    private void Awake()
    {
      _startButton.onClick.AddListener(() => StartPressed?.Invoke());
    }
  }
}
