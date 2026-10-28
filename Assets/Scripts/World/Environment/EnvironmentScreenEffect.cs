using UnityEngine;

/// <summary>컨트롤러가 준비, 강도, 종료만 제어하는 화면 효과의 최소 규칙입니다.</summary>
public abstract class EnvironmentScreenEffect : MonoBehaviour
{
    protected Camera OutputCamera { get; private set; }

    public bool Initialize(Camera outputCamera)
    {
        OutputCamera = outputCamera;
        return outputCamera != null && Prepare();
    }

    protected abstract bool Prepare();
    // 현재 표시 강도와 별도로 생성/종료 의도를 알립니다. 이미지 효과는 생략 가능합니다.
    public virtual void SetTargetIntensity(float intensity) { }
    public abstract void ApplyIntensity(float intensity);
    public abstract void ResetEffect();

    // 컨트롤러에서 실제 출력 카메라의 갱신이 끝난 뒤 호출합니다.
    public virtual void RefreshCamera() { }
}
