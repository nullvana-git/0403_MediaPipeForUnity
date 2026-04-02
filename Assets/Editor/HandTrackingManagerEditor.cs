using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HandTrackingManager))]
public class HandTrackingManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 필드 (_cameraDeviceIndex는 HideInInspector라 안 보임)
        DrawDefaultInspector();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);

        var devices = WebCamTexture.devices;
        var indexProp = serializedObject.FindProperty("_cameraDeviceIndex");

        if (devices.Length == 0)
        {
            EditorGUILayout.HelpBox("연결된 웹캠이 없습니다.", MessageType.Warning);
        }
        else
        {
            var names = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++)
                names[i] = $"[{i}]  {devices[i].name}";

            int current = Mathf.Clamp(indexProp.intValue, 0, devices.Length - 1);
            int selected = EditorGUILayout.Popup("카메라 선택", current, names);

            if (selected != current)
                indexProp.intValue = selected;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
