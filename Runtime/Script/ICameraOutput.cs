using UnityEngine;

namespace Dave6.ThirdPersonCamera
{
    // mover 혹은 combat에서 필요한 데이터
    public interface ICameraOutput
    {
        float referenceYaw {get;}
        Vector3 cameraForward {get;}
    }
}