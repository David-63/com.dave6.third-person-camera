using UnityEngine;

namespace Dave6.ThirdPersonCamera
{
    [CreateAssetMenu(fileName = "ThirdPersonCameraConfig", menuName = "DaveAssets/Config/Camera/Third Person Camera Config")]
    public class ThirdPersonCameraConfig : ScriptableObject
    {
        [Header("Camera Settings")]
        [Tooltip("아래로 처다볼 수 있는 디그리 각도 (높을수록 아래를 향함)")]
        public float TopClamp = 70.0f;
        [Tooltip("위로 처다볼 수 있는 디그리 각도 (낮을수록 위를 향함) ")]
        public float BottomClamp = -80.0f;

        public float MaxLookRange = 500.0f;
        [Range(0, 10)]public float LookSensitive = 1f;
    }
}