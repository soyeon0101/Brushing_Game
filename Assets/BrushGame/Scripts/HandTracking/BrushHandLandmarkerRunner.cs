// 샘플 HandLandmarkerRunner(Assets/MediaPipeUnity/Samples/Scenes/Hand Landmark Detection)를 복제해 게임용으로 수정한 것.
// 변경점: NumHands를 인스펙터에서 지정(기본 2), 결과에서 손목/점9만 복사해 HandFrameBuffer에 넣음,
// 랜드마크 표시는 개발용 옵션으로만 사용.

using System.Collections;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrushGame.HandTracking
{
  public class BrushHandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
  {
    private const int WristIndex = 0;
    private const int MiddleMcpIndex = 9;
    private static readonly int[] PalmIndices = { 0, 5, 9, 13, 17 };

    [Tooltip("개발용 랜드마크 표시. 게임 씬에서는 비워 둔다")]
    [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;
    [SerializeField, Range(1, 4)] private int _numHands = 2;
    [Tooltip("낮출수록 빠르게 움직여 흐려진 손도 덜 놓친다 (대신 오검출 가능성 증가)")]
    [SerializeField, Range(0f, 1f)] private float _minHandDetectionConfidence = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _minHandPresenceConfidence = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _minTrackingConfidence = 0.3f;
    [Tooltip("개발용. 게임 화면에서는 끈다.")]
    [SerializeField] private bool _drawLandmarks = true;
    [Tooltip("폰을 세워 두고 화면을 보며 양치하므로 전면 카메라를 쓴다. 없으면 샘플 기본(첫 번째 카메라)")]
    [SerializeField] private bool _preferFrontCamera = true;
#pragma warning disable CS0414 // 안드로이드 빌드에서만 사용
    [SerializeField] private float _permissionTimeoutSec = 30f;
#pragma warning restore CS0414

    private Mediapipe.Unity.Experimental.TextureFramePool _textureFramePool;

    // 콜백 스레드에서만 쓰는 작업용 배열 (할당 방지)
    private HandPoints[] _scratch;
    private float _aspect = 1f;

    public readonly Mediapipe.Unity.Sample.HandLandmarkDetection.HandLandmarkDetectionConfig config =
      new Mediapipe.Unity.Sample.HandLandmarkDetection.HandLandmarkDetectionConfig();

    public HandFrameBuffer Buffer { get; private set; }

    private void Awake()
    {
      config.NumHands = _numHands;
      config.MinHandDetectionConfidence = _minHandDetectionConfidence;
      config.MinHandPresenceConfidence = _minHandPresenceConfidence;
      config.MinTrackingConfidence = _minTrackingConfidence;
      Buffer = new HandFrameBuffer(_numHands);
      _scratch = new HandPoints[_numHands];
    }

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
    }

    protected override IEnumerator Run()
    {
      Debug.Log($"[BrushHand] RunningMode = {config.RunningMode}, NumHands = {config.NumHands}, Delegate = {config.Delegate}, " +
        $"Confidence(det/presence/track) = {config.MinHandDetectionConfidence}/{config.MinHandPresenceConfidence}/{config.MinTrackingConfidence}");

      yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

      var options = config.GetHandLandmarkerOptions(config.RunningMode == Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null);
      taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
      var imageSource = ImageSourceProvider.ImageSource;

      if (_preferFrontCamera && imageSource is WebCamSource)
      {
        yield return RequestCameraPermission();
        SelectFrontCamera(imageSource);
      }

      yield return imageSource.Play();

      if (!imageSource.isPrepared)
      {
        Debug.LogError("Failed to start ImageSource, exiting...");
        yield break;
      }

      _textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

      screen.Initialize(imageSource);

      // 게임 씬에는 랜드마크 표시가 없을 수 있다
      if (_handLandmarkerResultAnnotationController != null)
      {
        SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);
      }

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
      var rotation = (int)transformationOptions.rotationAngle;
      var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: rotation);

      // 정규화 좌표는 x/y가 각각 가로/세로로 나뉜 값이라, x에 가로세로비를 곱해 단위를 맞춘다.
      var rotated = rotation == 90 || rotation == 270;
      _aspect = rotated
        ? (float)imageSource.textureHeight / imageSource.textureWidth
        : (float)imageSource.textureWidth / imageSource.textureHeight;

      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();
      var result = HandLandmarkerResult.Alloc(options.numHands);

      var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
      using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

      while (true)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return new WaitForEndOfFrame();
          continue;
        }

        Image image;
        switch (config.ImageReadMode)
        {
          case ImageReadMode.GPU:
            if (!canUseGpuImage)
            {
              throw new System.Exception("ImageReadMode.GPU is not supported");
            }
            textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            image = textureFrame.BuildGPUImage(glContext);
            yield return waitForEndOfFrame;
            break;
          case ImageReadMode.CPU:
            yield return waitForEndOfFrame;
            textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            image = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;
          case ImageReadMode.CPUAsync:
          default:
            req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            yield return waitUntilReqDone;

            if (req.hasError)
            {
              Debug.LogWarning($"Failed to read texture from the image source");
              continue;
            }
            image = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;
        }

        switch (taskApi.runningMode)
        {
          case Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE:
          case Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO:
            var timestamp = GetCurrentTimestampMillisec();
            var detected = taskApi.runningMode == Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE
              ? taskApi.TryDetect(image, imageProcessingOptions, ref result)
              : taskApi.TryDetectForVideo(image, timestamp, imageProcessingOptions, ref result);
            HandleResult(detected ? result : default, timestamp, drawNow: true);
            break;
          case Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM:
            taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
            break;
        }
      }
    }

    /// <summary>
    ///   안드로이드는 권한을 받기 전에 카메라 목록이 비어 있을 수 있어서, 전면 카메라를 고르기 전에 먼저 권한을 받는다.
    ///   (샘플 WebCamSource도 Play 시 권한을 요청하지만 그 시점엔 이미 첫 번째 카메라로 정해진다)
    /// </summary>
    private IEnumerator RequestCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
      {
        yield break;
      }
      UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
      var deadline = Time.realtimeSinceStartup + _permissionTimeoutSec;
      while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) &&
             Time.realtimeSinceStartup < deadline)
      {
        yield return null;
      }
#else
      yield break;
#endif
    }

    private static void SelectFrontCamera(ImageSource imageSource)
    {
      var candidates = imageSource.sourceCandidateNames;
      if (candidates == null)
      {
        return;
      }
      foreach (var device in WebCamTexture.devices)
      {
        if (!device.isFrontFacing)
        {
          continue;
        }
        var index = System.Array.IndexOf(candidates, device.name);
        if (index >= 0)
        {
          imageSource.SelectSource(index);
          Debug.Log($"[BrushHand] 전면 카메라 선택: {device.name}");
        }
        return;
      }
      Debug.Log("[BrushHand] 전면 카메라가 없어 기본 카메라를 사용합니다.");
    }

    // LIVE_STREAM: 메인 스레드가 아닐 수 있음. Unity 오브젝트는 건드리지 않는다.
    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
      HandleResult(result, timestamp, drawNow: false);
    }

    private void HandleResult(HandLandmarkerResult result, long timestamp, bool drawNow)
    {
      var count = 0;
      var hands = result.handLandmarks;
      if (hands != null)
      {
        for (var i = 0; i < hands.Count && count < _scratch.Length; i++)
        {
          var landmarks = hands[i].landmarks;
          if (landmarks == null || landmarks.Count <= MiddleMcpIndex)
          {
            continue;
          }
          var wrist = landmarks[WristIndex];
          var mcp = landmarks[MiddleMcpIndex];
          _scratch[count].wrist = new Vector2(wrist.x * _aspect, wrist.y);
          _scratch[count].middleMcp = new Vector2(mcp.x * _aspect, mcp.y);

          float cx = 0f, cy = 0f;
          foreach (var index in PalmIndices)
          {
            cx += landmarks[index].x;
            cy += landmarks[index].y;
          }
          _scratch[count].palmCenter = new Vector2(cx / PalmIndices.Length * _aspect, cy / PalmIndices.Length);
          count++;
        }
      }
      Buffer.Publish(_scratch, count, timestamp);

      if (!_drawLandmarks || _handLandmarkerResultAnnotationController == null)
      {
        return;
      }
      // DrawLater는 스레드 안전하게 다음 LateUpdate에서 그리도록 샘플에서 쓰는 방식
      if (drawNow)
      {
        _handLandmarkerResultAnnotationController.DrawNow(result);
      }
      else
      {
        _handLandmarkerResultAnnotationController.DrawLater(result);
      }
    }
  }
}
