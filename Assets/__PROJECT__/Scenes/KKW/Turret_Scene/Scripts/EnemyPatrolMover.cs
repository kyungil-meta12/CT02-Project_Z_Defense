using UnityEngine;

public class EnemyPatrolMover : MonoBehaviour
{
    private enum MoveAxis
    {
        X,
        Z
    }

    [SerializeField] private MoveAxis moveAxis = MoveAxis.X;
    [SerializeField] private float moveRange = 10.0f;
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private bool lookMoveDirection = true;

    private Vector3 startPosition;
    private int moveDirection = 1;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        Vector3 moveVector = GetMoveVector();
        transform.position += moveVector * moveDirection * moveSpeed * Time.deltaTime;

        float currentOffset = GetCurrentOffset();
        if (Mathf.Abs(currentOffset) >= moveRange)
        {
            ClampPosition();
            moveDirection *= -1;
        }

        if (lookMoveDirection)
        {
            transform.forward = moveVector * moveDirection;
        }
    }

    private Vector3 GetMoveVector()
    {
        if (moveAxis == MoveAxis.X)
        {
            return Vector3.right;
        }

        return Vector3.forward;
    }

    private float GetCurrentOffset()
    {
        if (moveAxis == MoveAxis.X)
        {
            return transform.position.x - startPosition.x;
        }

        return transform.position.z - startPosition.z;
    }

    private void ClampPosition()
    {
        Vector3 position = transform.position;
        float clampedOffset = Mathf.Clamp(GetCurrentOffset(), -moveRange, moveRange);

        if (moveAxis == MoveAxis.X)
        {
            position.x = startPosition.x + clampedOffset;
        }
        else
        {
            position.z = startPosition.z + clampedOffset;
        }

        transform.position = position;
    }
}
