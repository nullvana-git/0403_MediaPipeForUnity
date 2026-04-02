using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 주먹/손 펼침 제스처를 감지합니다.
/// HandTrackingManager가 같은 씬에 있어야 합니다.
/// </summary>
public class FistGesture : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandTrackingManager _tracker;

    [Header("Settings")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float _fistThreshold = 0.5f; // 이 값 이하면 주먹으로 판단 (손 크기 대비 비율)

    [Header("Events")]
    public UnityEvent OnFist;    // 주먹 쥘 때
    public UnityEvent OnOpen;    // 손 펼 때

    [Header("Debug")]
    [SerializeField] private bool _showGUI = true;

    public bool IsFist { get; private set; }
    public float CurrentRatio { get; private set; } // 디버그용

    private bool _prevFist = false;

    private void Awake()
    {
        if (_tracker == null)
            _tracker = FindFirstObjectByType<HandTrackingManager>();
    }

    private void Update()
    {
        var result = _tracker.CurrentResult;
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            return;

        // 첫 번째 손만 체크
        IsFist = CheckFist(0);

        if (IsFist && !_prevFist)
            OnFist?.Invoke();
        else if (!IsFist && _prevFist)
            OnOpen?.Invoke();

        _prevFist = IsFist;
    }

    /// <summary>
    /// 손가락 4개(검지~새끼)의 MCP-TIP 거리 평균으로 주먹 판단
    /// </summary>
    private bool CheckFist(int handIndex)
    {
        var result = _tracker.CurrentResult;
        if (result.handLandmarks == null || handIndex >= result.handLandmarks.Count)
            return false;

        var landmarks = result.handLandmarks[handIndex].landmarks;
        if (landmarks == null || landmarks.Count < 21) return false;

        // 손목 기준 손 크기로 threshold를 정규화
        var wrist = landmarks[HandTrackingManager.LandmarkIndex.WRIST];
        var middleMcp = landmarks[HandTrackingManager.LandmarkIndex.MIDDLE_MCP];
        float handSize = Dist(wrist, middleMcp);
        if (handSize < 0.001f) return false;

        // 각 손가락 MCP → TIP 거리 (손 크기 대비 비율)
        float[] fingerRatios = new float[]
        {
            Dist(landmarks[HandTrackingManager.LandmarkIndex.INDEX_MCP],  landmarks[HandTrackingManager.LandmarkIndex.INDEX_TIP])  / handSize,
            Dist(landmarks[HandTrackingManager.LandmarkIndex.MIDDLE_MCP], landmarks[HandTrackingManager.LandmarkIndex.MIDDLE_TIP]) / handSize,
            Dist(landmarks[HandTrackingManager.LandmarkIndex.RING_MCP],   landmarks[HandTrackingManager.LandmarkIndex.RING_TIP])   / handSize,
            Dist(landmarks[HandTrackingManager.LandmarkIndex.PINKY_MCP],  landmarks[HandTrackingManager.LandmarkIndex.PINKY_TIP])  / handSize,
        };

        float avg = 0f;
        foreach (var r in fingerRatios) avg += r;
        avg /= fingerRatios.Length;

        CurrentRatio = avg;
        // avg가 작을수록 손가락이 접혀있음 (주먹)
        return avg < _fistThreshold;
    }

    private float Dist(
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark a,
        Mediapipe.Tasks.Components.Containers.NormalizedLandmark b)
    {
        float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void OnGUI()
    {
        if (!_showGUI) return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = UnityEngine.Color.white;

        if (IsFist)
        {
            GUI.color = UnityEngine.Color.red;
            GUI.Box(new UnityEngine.Rect(UnityEngine.Screen.width / 2f - 100, 20, 200, 60), "FIST", style);
        }
        else
        {
            GUI.color = UnityEngine.Color.green;
            GUI.Box(new UnityEngine.Rect(UnityEngine.Screen.width / 2f - 100, 20, 200, 60), "OPEN", style);
        }
        GUI.color = UnityEngine.Color.white;

        // 현재 비율 표시 (threshold 튜닝용)
        var debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        debugStyle.normal.textColor = UnityEngine.Color.yellow;
        GUI.Label(new UnityEngine.Rect(10, UnityEngine.Screen.height - 60, 400, 30),
            $"Ratio: {CurrentRatio:F3}  |  Threshold: {_fistThreshold:F3}", debugStyle);
        GUI.Label(new UnityEngine.Rect(10, UnityEngine.Screen.height - 30, 400, 30),
            "주먹쥐기: ratio 낮음 / 손펼기: ratio 높음", debugStyle);
    }
}
