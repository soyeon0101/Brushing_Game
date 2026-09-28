namespace BrushGame
{
  /// <summary>
  ///   게임 로직이 보는 유일한 양치 입력. 카메라 / 키보드 구현을 교체할 수 있다.
  /// </summary>
  public interface IBrushInput
  {
    bool IsBrushing { get; }   // 지금 닦는 중인가
    bool IsTooFast { get; }    // 과속 상태인가
    float StrokeRate { get; }  // 초당 방향 반전 횟수 (디버그용)
    bool HandVisible { get; }  // 손이 하나라도 보이는가
  }
}
