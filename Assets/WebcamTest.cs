using UnityEngine;
using UnityEngine.UI;

public class WebcamTest : MonoBehaviour
{
    [SerializeField] private RawImage _display;

    private WebCamTexture _webCam;

    void Start()
    {
        _webCam = new WebCamTexture("HD Pro Webcam C920", 1280, 720);
        _webCam.Play();
        _display.texture = _webCam;
        Debug.Log($"[WebcamTest] Started: {_webCam.deviceName}, playing={_webCam.isPlaying}");
    }

    void OnDestroy()
    {
        if (_webCam != null)
        {
            _webCam.Stop();
            Destroy(_webCam);
        }
    }
}
