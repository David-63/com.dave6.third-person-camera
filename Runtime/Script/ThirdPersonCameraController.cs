using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityUtils;

namespace Dave6.ThirdPersonCamera
{
    public class ThirdPersonCameraController : MonoBehaviour, ICameraOutput
    {
        public ThirdPersonCameraContext cameraCtx;
        Transform m_MainCamera;
        public float referenceYaw => cameraCtx.aimYaw;
        public Vector3 cameraForward => m_MainCamera.forward;

        CinemachineCamera m_CinemachineCamera;
        CinemachineThirdPersonFollow m_ThirdPersonFollow;
        Transform m_CameraTarget;

        [Header("설정")]
        [SerializeField] ThirdPersonCameraConfig m_Config;


        [Header("트렌지션 효과")]
        [SerializeField] float m_TransitionDuration = 0.35f;
        [SerializeField] AnimationCurve m_TransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        Coroutine m_TransitionCoroutine;

        List<CameraKick> m_Kicks = new();
        CameraSway m_Sway = new(0,0);

        void Awake()
        {
            Initialize();
        }

        public void OnUpdate(Vector2 lookDelta)
        {
            LookInput(lookDelta);
            UpdateShake(Time.deltaTime);
            UpdateAim();
        }

        void LateUpdate()
        {
            if (m_CameraTarget == null) return;

            cameraCtx.finalYaw = cameraCtx.aimYaw + cameraCtx.swayYaw;
            cameraCtx.finalPitch = cameraCtx.aimPitch + cameraCtx.swayPitch;

            m_CameraTarget.rotation = Quaternion.Euler(cameraCtx.finalPitch, cameraCtx.finalYaw, 0.0f);
        }

        void Initialize()
        {
            cameraCtx = new();
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("MainCamera not found.");
                enabled = false;
            }
            m_MainCamera = cam.transform;
            Camera.main.gameObject.GetOrAddComponent<CinemachineBrain>();

            m_CinemachineCamera = gameObject.GetOrAddComponent<CinemachineCamera>();
            m_ThirdPersonFollow = gameObject.GetOrAddComponent<CinemachineThirdPersonFollow>();
        }

        void UpdateShake(float deltaTime)
        {
            cameraCtx.kickYaw = 0f;
            cameraCtx.kickPitch = 0f;

            for (int i = m_Kicks.Count - 1; i >= 0; i--)
            {
                var kick = m_Kicks[i];
                kick.UpdateShake(deltaTime, out float yawOffset, out float pitchOffset);
                cameraCtx.kickYaw += yawOffset;
                cameraCtx.kickPitch += pitchOffset;

                if (kick.isFinished)
                {
                    m_Kicks.RemoveAt(i);
                }
            }

            m_Sway.UpdateShake(deltaTime, out cameraCtx.swayYaw, out cameraCtx.swayPitch, cameraCtx.moveSpeed01);
        }

        void UpdateAim()
        {
            cameraCtx.aimYaw = cameraCtx.inputYaw + cameraCtx.kickYaw;
            cameraCtx.aimPitch = cameraCtx.inputPitch + cameraCtx.kickPitch;
        }

        #region Camera API
        public void SetFollowTarget(Transform target)
        {
            m_CameraTarget = target;
            m_CinemachineCamera.Follow = m_CameraTarget;

            m_ThirdPersonFollow.Damping = Vector3.zero;
            m_ThirdPersonFollow.ShoulderOffset = new Vector3(1,0,0);
            m_ThirdPersonFollow.VerticalArmLength = 0;
            m_ThirdPersonFollow.CameraDistance = 1;
            m_ThirdPersonFollow.AvoidObstacles.Enabled = true;
            m_ThirdPersonFollow.AvoidObstacles.DampingFromCollision = 0.2f;
            m_ThirdPersonFollow.AvoidObstacles.DampingIntoCollision = 0.2f;
        }
        public void LookInput(Vector2 lookDelta)
        {
            if (lookDelta.sqrMagnitude >= 0.0001f)
            {
                cameraCtx.inputYaw += lookDelta.x * m_Config.LookSensitive;
                cameraCtx.inputPitch += lookDelta.y * m_Config.LookSensitive;
            }

            cameraCtx.inputYaw = ClampAngle(cameraCtx.inputYaw, float.MinValue, float.MaxValue);
            cameraCtx.inputPitch = ClampAngle(cameraCtx.inputPitch, m_Config.BottomClamp, m_Config.TopClamp);

            cameraCtx.finalYaw = cameraCtx.inputYaw;
            cameraCtx.finalPitch = cameraCtx.inputPitch;
        }
        public void StartTransition(ThirdPersonPreset preset, float? duration = null)
        {
            cameraCtx.targetPreset = preset;

            if (m_TransitionCoroutine != null)
                StopCoroutine(m_TransitionCoroutine);

            m_TransitionCoroutine = StartCoroutine(TransitionRoutine(duration ?? m_TransitionDuration));
        }

        public void AddKick(Vector2 direction, float intensity, float duration)
        {
            m_Kicks.Add(new CameraKick(direction, intensity, duration));
        }
        public void SetMoveSpeed01(float value)
        {
            cameraCtx.moveSpeed01 = Mathf.Clamp01(value);
        }
        public void SetSway(float intensity, float speed)
        {
            m_Sway.intensity = intensity;
            m_Sway.speed = speed;
        }
        public void ClearSway()
        {
            m_Sway.intensity = 0;
            m_Sway.speed = 0;
        }
        #endregion


        IEnumerator TransitionRoutine(float duration)
        {
            float elapsed = 0f;
            float invDuration = 1f / duration;

            // 시작값 저장
            float startFOV = m_CinemachineCamera.Lens.FieldOfView;
            float startSide = m_ThirdPersonFollow.CameraSide;
            float startDistance = m_ThirdPersonFollow.CameraDistance;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed * invDuration);
                float curveT = m_TransitionCurve.Evaluate(t);

                m_CinemachineCamera.Lens.FieldOfView = Mathf.LerpUnclamped(startFOV, cameraCtx.targetPreset.fov, curveT);
                m_ThirdPersonFollow.CameraSide = Mathf.LerpUnclamped(startSide, cameraCtx.targetPreset.sideLength, curveT);
                m_ThirdPersonFollow.CameraDistance = Mathf.LerpUnclamped(startDistance, cameraCtx.targetPreset.distance, curveT);

                yield return null;
            }

            // 정확히 목표값으로 마무리 (부동소수점 오차 방지)
            m_CinemachineCamera.Lens.FieldOfView = cameraCtx.targetPreset.fov;
            m_ThirdPersonFollow.CameraSide = cameraCtx.targetPreset.sideLength;
            m_ThirdPersonFollow.CameraDistance = cameraCtx.targetPreset.distance;

            m_TransitionCoroutine = null;
        }
        float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }


    
    public abstract class CameraShake
    {
        public abstract void UpdateShake(float deltaTime, out float offsetYaw, out float offsetPitch);
    }

    public class CameraKick : CameraShake
    {
        Vector3 m_Direction;
        float m_Intensity;
        float m_Duration;
        float m_ElapsedTime;
        public bool isFinished => m_ElapsedTime >= m_Duration;
        public CameraKick(Vector3 dir, float intensity, float duration)
        {
            m_Direction = dir.normalized;
            m_Intensity = intensity;
            m_Duration = duration;
            m_ElapsedTime = 0f;
        }
        public override void UpdateShake(float deltaTime, out float offsetYaw, out float offsetPitch)
        {
            if (isFinished)
            {
                offsetYaw = 0;
                offsetPitch = 0;
                return;
            }

            float progress = m_ElapsedTime / m_Duration;
            float damper = 1f - progress; // 점점 줄어드는 감쇠

            offsetYaw = m_Direction.y * m_Intensity * damper;
            offsetPitch = m_Direction.x * m_Intensity * damper;


            m_ElapsedTime += deltaTime;
        }
    }

    public class CameraSway : CameraShake
    {
        public float intensity;
        public float speed;
        float m_Time;

        public CameraSway(float intensity, float speed)
        {
            this.intensity = intensity;
            this.speed = speed;
            m_Time = 0f;
        }

        public override void UpdateShake(float deltaTime, out float offsetYaw, out float offsetPitch)
        {
            m_Time += deltaTime;
            offsetYaw = Mathf.Sin(m_Time * speed) * intensity;
            offsetPitch = Mathf.Cos(m_Time * speed * 0.5f) * intensity * 0.5f;
        }

        public void UpdateShake(float deltaTime, out float offsetYaw, out float offsetPitch, float weight)
        {
            if (weight <= 0.0001f)
            {
                offsetYaw = 0f;
                offsetPitch = 0f;
                return;
            }
            m_Time += deltaTime * speed;
            offsetYaw = Mathf.Sin(m_Time) * intensity;
            offsetPitch = Mathf.Cos(m_Time * 0.5f) * intensity * 0.5f;
            offsetYaw *= weight;
            offsetPitch *= weight;
        }
    }
}