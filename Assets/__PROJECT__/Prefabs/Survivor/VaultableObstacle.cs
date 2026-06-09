using UnityEngine;

public class VaultableObstacle : MonoBehaviour
{
    private const float MIN_MIRROR_DISTANCE = 0.1f;
    private const float CENTER_EPSILON = 0.001f;

    // 장애물의 X 위치를 기준으로 생존자의 반대편 착지 위치를 계산한다
    public Vector3 GetLandingPosition(Vector3 survivorPosition, Vector3 moveDirection, float fallbackForwardOffset, float fallbackVerticalOffset)
    {
        float obstacleX = transform.position.x;
        float mirroredXOffset = obstacleX - survivorPosition.x;

        if (Mathf.Abs(mirroredXOffset) <= CENTER_EPSILON)
        {
            float directionX = Mathf.Abs(moveDirection.x) > CENTER_EPSILON ? moveDirection.x : transform.right.x;
            mirroredXOffset = Mathf.Sign(directionX == 0f ? 1f : directionX) * Mathf.Max(MIN_MIRROR_DISTANCE, fallbackForwardOffset);
        }

        Vector3 landingPosition = survivorPosition;
        landingPosition.x = obstacleX + mirroredXOffset;
        landingPosition.y += fallbackVerticalOffset;

        return landingPosition;
    }
}
