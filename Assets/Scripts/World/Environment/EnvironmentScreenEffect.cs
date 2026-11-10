using UnityEngine;

/// <summary>영역 또는 기존 화면 컨트롤러가 효과를 준비하고 설정·종료하는 공통 규칙입니다.</summary>
public abstract class EnvironmentScreenEffect : MonoBehaviour
{
    protected Camera OutputCamera { get; private set; }
    protected EnvironmentScreenEffect SourcePrefab { get; private set; }

    public void BindSourcePrefab(EnvironmentScreenEffect prefab)
    {
        SourcePrefab = prefab;
    }

    public bool Initialize(Camera outputCamera)
    {
        OutputCamera = outputCamera;
        return outputCamera != null && Prepare();
    }

    protected abstract bool Prepare();
    // 영역 배치를 지원하는 효과만 연결합니다. 화면 전용 효과는 기본적으로 거절합니다.
    public virtual bool BindArea(EnvironmentEffectArea area) { return false; }
    // 입자가 없는 이미지 효과는 생성량 설정을 사용하지 않습니다.
    public virtual void SetEmissionMultiplier(float multiplier) { }
    // 월드 위치에 반응하는 효과만 사용합니다. 기존 먼지/안개는 연결할 필요가 없습니다.
    public virtual void BindWorldContext(Transform target, Transform mapRoot) { }
    // 현재 표시 강도와 별도로 생성/종료 의도를 알립니다. 이미지 효과는 생략 가능합니다.
    public virtual void SetTargetIntensity(float intensity) { }
    public abstract void ApplyIntensity(float intensity);
    public abstract void ResetEffect();

    // 컨트롤러에서 실제 출력 카메라의 갱신이 끝난 뒤 호출합니다.
    public virtual void RefreshCamera() { }
}
