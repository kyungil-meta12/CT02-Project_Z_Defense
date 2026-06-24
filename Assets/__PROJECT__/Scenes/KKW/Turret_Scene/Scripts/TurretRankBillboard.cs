using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 터렛 랭크 배지나 레벨 텍스트가 현재 카메라를 바라보도록 회전시키는 월드 오브젝트용 빌보드 컴포넌트.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TurretRankBillboard : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useMainCameraWhenMissing = true;
    [SerializeField] private bool useSceneViewCameraInEditMode = true;

    [Header("회전")]
    [SerializeField] private bool yawOnly = true;
    [SerializeField] private bool reverseForward = false;

    private Transform cachedTransform;
    private Transform cachedCameraTransform;

    // 컴포넌트 추가 시 기본 카메라 참조를 수집한다
    private void Reset()
    {
        CacheTransform();
        ResolveCamera();
    }

    // 시작 전 변환과 카메라 참조를 캐싱한다
    private void Awake()
    {
        CacheTransform();
        ResolveCamera();
    }

    // 활성화될 때 누락된 카메라 참조를 다시 확인한다
    private void OnEnable()
    {
        ResolveCamera();
        FaceCamera();
    }

    // 카메라 이동이 끝난 뒤 빌보드 방향을 갱신한다
    private void LateUpdate()
    {
        if (cachedCameraTransform == null)
        {
            ResolveCamera();
        }

        FaceCamera();
    }

    // 외부에서 바라볼 카메라를 명시적으로 지정한다
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        cachedCameraTransform = targetCamera == null ? null : targetCamera.transform;
        FaceCamera();
    }

    // 현재 오브젝트 Transform을 캐싱한다
    private void CacheTransform()
    {
        cachedTransform = transform;
    }

    // 누락된 카메라 참조를 Camera.main에서 가져온다
    private void ResolveCamera()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && useSceneViewCameraInEditMode)
        {
            Camera sceneViewCamera = GetSceneViewCamera();
            if (sceneViewCamera != null)
            {
                cachedCameraTransform = sceneViewCamera.transform;
                return;
            }
        }
#endif

        if (targetCamera == null && useMainCameraWhenMissing)
        {
            targetCamera = Camera.main;
        }

        cachedCameraTransform = targetCamera == null ? null : targetCamera.transform;
    }

#if UNITY_EDITOR
    // 에디터 프리팹 모드와 씬 뷰에서 사용할 SceneView 카메라를 반환한다
    private static Camera GetSceneViewCamera()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        return sceneView == null ? null : sceneView.camera;
    }
#endif

    // 현재 카메라를 바라보도록 회전값을 적용한다
    private void FaceCamera()
    {
        if (cachedTransform == null || cachedCameraTransform == null)
        {
            return;
        }

        Vector3 direction = reverseForward
            ? cachedCameraTransform.position - cachedTransform.position
            : cachedTransform.position - cachedCameraTransform.position;

        if (yawOnly)
        {
            direction.y = 0.0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 up = yawOnly ? Vector3.up : cachedCameraTransform.up;
        cachedTransform.rotation = Quaternion.LookRotation(direction, up);
    }
}
