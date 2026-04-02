using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Components.Containers;
using MpImage = Mediapipe.Image;

public class HandTrackingManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage _webcamDisplay;

    [Header("Settings")]
    [SerializeField] private int _numHands = 2;
    [SerializeField] private float _minDetectionConfidence = 0.5f;

    [HideInInspector]
    [SerializeField] private int _cameraDeviceIndex = 0;

    public event Action<HandLandmarkerResult> OnHandLandmarkDetected;
    public HandLandmarkerResult CurrentResult { get; private set; }
    public bool IsTracking { get; private set; }

    private HandLandmarker _handLandmarker;
    private Mediapipe.Unity.Experimental.TextureFramePool _textureFramePool;
    private HandLandmarkerResult _callbackBuffer = HandLandmarkerResult.Alloc(2); // 콜백 스레드 전용
    private HandLandmarkerResult _displayBuffer = HandLandmarkerResult.Alloc(2);  // 메인 스레드 전용
    private bool _hasNewResult;
    private readonly object _resultLock = new object();
    private WebCamTexture _webCamTexture;
    private RenderTexture _renderTexture;

    public static class LandmarkIndex
    {
        public const int WRIST = 0;
        public const int THUMB_TIP = 4;
        public const int INDEX_MCP = 5;
        public const int INDEX_TIP = 8;
        public const int MIDDLE_MCP = 9;
        public const int MIDDLE_TIP = 12;
        public const int RING_MCP = 13;
        public const int RING_TIP = 16;
        public const int PINKY_MCP = 17;
        public const int PINKY_TIP = 20;
    }

    private IEnumerator Start()
    {
        var bootstrap = InitBootstrap();
        yield return new WaitUntil(() => bootstrap.isFinished);

        var modelPath = "hand_landmarker.bytes";
        yield return AssetLoader.PrepareAssetAsync(modelPath);

        var options = new HandLandmarkerOptions(
            new Mediapipe.Tasks.Core.BaseOptions(
                Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                modelAssetPath: modelPath),
            runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
            numHands: _numHands,
            minHandDetectionConfidence: _minDetectionConfidence,
            minHandPresenceConfidence: _minDetectionConfidence,
            minTrackingConfidence: _minDetectionConfidence,
            resultCallback: OnResult
        );
        _handLandmarker = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

        var imageSource = ImageSourceProvider.ImageSource;

        // 인스펙터에서 선택한 카메라 인덱스로 소스 지정
        var candidates = imageSource.sourceCandidateNames;
        if (candidates != null && candidates.Length > 0)
        {
            int idx = Mathf.Clamp(_cameraDeviceIndex, 0, candidates.Length - 1);
            imageSource.SelectSource(idx);
            Debug.Log($"Selected camera [{idx}]: {candidates[idx]}");
        }

        yield return imageSource.Play();

        if (!imageSource.isPrepared)
        {
            Debug.LogError("Failed to start webcam");
            yield break;
        }

        Debug.Log($"Webcam started: {imageSource.textureWidth}x{imageSource.textureHeight}");

        // 웹캠 화면 표시: WebCamTexture → RenderTexture → RawImage
        _webCamTexture = imageSource.GetCurrentTexture() as WebCamTexture;
        if (_webCamTexture != null && _webcamDisplay != null)
        {
            _renderTexture = new RenderTexture(_webCamTexture.width, _webCamTexture.height, 0);
            _webcamDisplay.texture = _renderTexture;
            _webcamDisplay.rectTransform.localScale = Vector3.one;
            Debug.Log($"RenderTexture created: {_renderTexture.width}x{_renderTexture.height}");
        }

        _textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(
            imageSource.textureWidth, imageSource.textureHeight,
            TextureFormat.RGBA32, 10);

        IsTracking = true;

        var transformationOptions = imageSource.GetTransformationOptions();
        var flipH = transformationOptions.flipHorizontally;
        var flipV = transformationOptions.flipVertically;
        var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(
            rotationDegrees: (int)transformationOptions.rotationAngle);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        AsyncGPUReadbackRequest req = default;
        var waitUntilReqDone = new WaitUntil(() => req.done);

        while (IsTracking)
        {
            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                yield return new WaitForEndOfFrame();
                continue;
            }

            req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipH, flipV);
            yield return waitUntilReqDone;

            if (req.hasError)
            {
                Debug.LogWarning("Texture read failed");
                continue;
            }

            var image = textureFrame.BuildCPUImage();
            textureFrame.Release();

            long timestamp = stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond;
            _handLandmarker.DetectAsync(image, timestamp, imageProcessingOptions);
        }
    }

    private void OnResult(HandLandmarkerResult result, MpImage image, long timestamp)
    {
        lock (_resultLock)
        {
            result.CloneTo(ref _callbackBuffer);
            _hasNewResult = true;
        }
    }

    private void Update()
    {
        // WebCamTexture → RenderTexture 매 프레임 복사
        if (_webCamTexture != null && _webCamTexture.didUpdateThisFrame && _renderTexture != null)
        {
            Graphics.Blit(_webCamTexture, _renderTexture);
        }

        lock (_resultLock)
        {
            if (_hasNewResult)
            {
                _callbackBuffer.CloneTo(ref _displayBuffer);
                CurrentResult = _displayBuffer;
                _hasNewResult = false;
            }
        }

        if (CurrentResult.handLandmarks != null && CurrentResult.handLandmarks.Count > 0)
        {
            OnHandLandmarkDetected?.Invoke(CurrentResult);
        }
    }

    private void OnGUI()
    {
        // 상태 표시
        var style = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        style.normal.textColor = UnityEngine.Color.green;

        // 로컬 복사본 사용 (Update 스레드와의 race condition 방지)
        var result = CurrentResult;
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            GUI.Label(new UnityEngine.Rect(10, 10, 600, 40), IsTracking ? "Tracking... Show your hand!" : "Initializing...", style);
            return;
        }

        // 디버그 정보
        string info = $"Hands: {result.handLandmarks.Count}";
        for (int h = 0; h < result.handLandmarks.Count; h++)
        {
            var hand = result.handLandmarks[h].landmarks;
            if (hand == null || hand.Count < 21) continue;
            string handedness = "?";
            if (CurrentResult.handedness != null && h < CurrentResult.handedness.Count)
            {
                var cats = CurrentResult.handedness[h].categories;
                if (cats != null && cats.Count > 0) handedness = cats[0].categoryName;
            }
            var tip = hand[LandmarkIndex.INDEX_TIP];
            info += $"\n  [{handedness}] Index: ({tip.x:F2}, {tip.y:F2})";
        }
        GUI.Label(new UnityEngine.Rect(10, 10, 600, 150), info, style);

        // 랜드마크 점 그리기 (x 반전 — 웹캠 미러링에 맞춤)
        var dotTex = Texture2D.whiteTexture;
        for (int h = 0; h < result.handLandmarks.Count; h++)
        {
            var hand = result.handLandmarks[h].landmarks;
            if (hand == null || hand.Count < 21) continue;

            GUI.color = h == 0 ? UnityEngine.Color.green : UnityEngine.Color.cyan;
            for (int i = 0; i < hand.Count; i++)
            {
                float sx = (1f - hand[i].x) * UnityEngine.Screen.width;
                float sy = hand[i].y * UnityEngine.Screen.height;
                GUI.DrawTexture(new UnityEngine.Rect(sx - 6, sy - 6, 12, 12), dotTex);
            }

            // 손목 위치에 라벨
            var wrist = hand[LandmarkIndex.WRIST];
            GUI.color = UnityEngine.Color.white;
            GUI.Label(new UnityEngine.Rect((1f - wrist.x) * UnityEngine.Screen.width, wrist.y * UnityEngine.Screen.height - 30, 200, 30),
                $"Hand {h}", style);
        }
        GUI.color = UnityEngine.Color.white;
    }

    public Vector2 GetScreenPosition(int handIndex, int landmarkIndex)
    {
        if (CurrentResult.handLandmarks == null || handIndex >= CurrentResult.handLandmarks.Count)
            return Vector2.zero;
        var lm = CurrentResult.handLandmarks[handIndex].landmarks[landmarkIndex];
        return new Vector2(lm.x * UnityEngine.Screen.width, (1f - lm.y) * UnityEngine.Screen.height);
    }

    public float GetDistance(int handIndex, int lmA, int lmB)
    {
        if (CurrentResult.handLandmarks == null || handIndex >= CurrentResult.handLandmarks.Count)
            return -1f;
        var landmarks = CurrentResult.handLandmarks[handIndex].landmarks;
        var a = landmarks[lmA];
        var b = landmarks[lmB];
        return Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z));
    }

    private Bootstrap InitBootstrap()
    {
        var bootstrapObj = GameObject.Find("Bootstrap");
        if (bootstrapObj == null)
        {
            var prefab = Resources.Load<GameObject>("Bootstrap");
            bootstrapObj = Instantiate(prefab);
            bootstrapObj.name = "Bootstrap";
            DontDestroyOnLoad(bootstrapObj);
        }
        return bootstrapObj.GetComponent<Bootstrap>();
    }

    private void OnDestroy()
    {
        IsTracking = false;
        _textureFramePool?.Dispose();
        _handLandmarker?.Close();
        ImageSourceProvider.ImageSource?.Stop();
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }
}
