using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityUtils;

namespace Dave6.ThirdPersonCamera
{
    public class ThirdPersonCameraController : MonoBehaviour, ICameraOutput
    {
        public ThirdPersonCameraContext CameraCtx;
        Transform _MainCamera;
        public float ReferenceYaw => CameraCtx.AimYaw;
        public Vector3 CameraForward => _MainCamera.forward;

        CinemachineCamera _CinemachineCamera;
        CinemachineThirdPersonFollow _ThirdPersonFollow;
        Transform _CameraTarget;

        [Header("설정")]
        [SerializeField] ThirdPersonCameraConfig _Config;


        [Header("트렌지션 효과")]
        [SerializeField] float _TransitionDuration = 0.35f;
        [SerializeField] AnimationCurve _TransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        Coroutine _TransitionCoroutine;

        List<CameraKick> _Kicks = new();
        CameraSway _Sway = new(0,0);

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
            if (_CameraTarget == null) return;

            CameraCtx.FinalYaw = CameraCtx.AimYaw + CameraCtx.SwayYaw;
            CameraCtx.FinalPitch = CameraCtx.AimPitch + CameraCtx.SwayPitch;

            _CameraTarget.rotation = Quaternion.Euler(CameraCtx.FinalPitch, CameraCtx.FinalYaw, 0.0f);
        }

        void Initialize()
        {
            CameraCtx = new();
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("MainCamera not found.");
                enabled = false;
            }
            _MainCamera = cam.transform;
            Camera.main.gameObject.GetOrAddComponent<CinemachineBrain>();

            _CinemachineCamera = gameObject.GetOrAddComponent<CinemachineCamera>();
            _ThirdPersonFollow = gameObject.GetOrAddComponent<CinemachineThirdPersonFollow>();
        }

        void UpdateShake(float deltaTime)
        {
            CameraCtx.KickYaw = 0f;
            CameraCtx.KickPitch = 0f;

            for (int i = _Kicks.Count - 1; i >= 0; i--)
            {
                var kick = _Kicks[i];
                kick.UpdateShake(deltaTime, out float yawOffset, out float pitchOffset);
                CameraCtx.KickYaw += yawOffset;
                CameraCtx.KickPitch += pitchOffset;

                if (kick.isFinished)
                {
                    _Kicks.RemoveAt(i);
                }
            }

            _Sway.UpdateShake(deltaTime, out CameraCtx.SwayYaw, out CameraCtx.SwayPitch, CameraCtx.MoveSpeed01);
        }

        void UpdateAim()
        {
            CameraCtx.AimYaw = CameraCtx.InputYaw + CameraCtx.KickYaw;
            CameraCtx.AimPitch = CameraCtx.InputPitch + CameraCtx.KickPitch;
        }

        #region Camera API
        public void SetFollowTarget(Transform target)
        {
            _CameraTarget = target;
            _CinemachineCamera.Follow = _CameraTarget;

            _ThirdPersonFollow.Damping = Vector3.zero;
            _ThirdPersonFollow.ShoulderOffset = new Vector3(1,0,0);
            _ThirdPersonFollow.VerticalArmLength = 0;
            _ThirdPersonFollow.CameraDistance = 1;
            _ThirdPersonFollow.AvoidObstacles.Enabled = true;
            _ThirdPersonFollow.AvoidObstacles.DampingFromCollision = 0.2f;
            _ThirdPersonFollow.AvoidObstacles.DampingIntoCollision = 0.2f;
        }
        public void LookInput(Vector2 lookDelta)
        {
            if (lookDelta.sqrMagnitude >= 0.0001f)
            {
                CameraCtx.InputYaw += lookDelta.x * _Config.LookSensitive;
                CameraCtx.InputPitch += lookDelta.y * _Config.LookSensitive;
            }

            CameraCtx.InputYaw = ClampAngle(CameraCtx.InputYaw, float.MinValue, float.MaxValue);
            CameraCtx.InputPitch = ClampAngle(CameraCtx.InputPitch, _Config.BottomClamp, _Config.TopClamp);

            CameraCtx.FinalYaw = CameraCtx.InputYaw;
            CameraCtx.FinalPitch = CameraCtx.InputPitch;
        }
        public void StartTransition(ThirdPersonPreset preset, float? duration = null)
        {
            CameraCtx.TargetPreset = preset;

            if (_TransitionCoroutine != null)
                StopCoroutine(_TransitionCoroutine);

            _TransitionCoroutine = StartCoroutine(TransitionRoutine(duration ?? _TransitionDuration));
        }

        public void AddKick(Vector2 direction, float intensity, float duration)
        {
            _Kicks.Add(new CameraKick(direction, intensity, duration));
        }
        public void SetMoveSpeed01(float value)
        {
            CameraCtx.MoveSpeed01 = Mathf.Clamp01(value);
        }
        public void SetSway(float intensity, float speed)
        {
            _Sway.intensity = intensity;
            _Sway.speed = speed;
        }
        public void ClearSway()
        {
            _Sway.intensity = 0;
            _Sway.speed = 0;
        }
        #endregion


        IEnumerator TransitionRoutine(float duration)
        {
            float elapsed = 0f;
            float invDuration = 1f / duration;

            // 시작값 저장
            float startFOV = _CinemachineCamera.Lens.FieldOfView;
            float startSide = _ThirdPersonFollow.CameraSide;
            float startDistance = _ThirdPersonFollow.CameraDistance;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed * invDuration);
                float curveT = _TransitionCurve.Evaluate(t);

                _CinemachineCamera.Lens.FieldOfView = Mathf.LerpUnclamped(startFOV, CameraCtx.TargetPreset.fov, curveT);
                _ThirdPersonFollow.CameraSide = Mathf.LerpUnclamped(startSide, CameraCtx.TargetPreset.sideLength, curveT);
                _ThirdPersonFollow.CameraDistance = Mathf.LerpUnclamped(startDistance, CameraCtx.TargetPreset.distance, curveT);

                yield return null;
            }

            // 정확히 목표값으로 마무리 (부동소수점 오차 방지)
            _CinemachineCamera.Lens.FieldOfView = CameraCtx.TargetPreset.fov;
            _ThirdPersonFollow.CameraSide = CameraCtx.TargetPreset.sideLength;
            _ThirdPersonFollow.CameraDistance = CameraCtx.TargetPreset.distance;

            _TransitionCoroutine = null;
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