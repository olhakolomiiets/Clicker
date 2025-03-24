using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AstronautMoveBetweenTransforms : MonoBehaviour
{
    [SerializeField] private RewardTimers _rewardTimer;
    public GameObject targetObject; // The object to move
    public Transform[] waypoints; // Array of waypoints to move between
    public float moveDuration = 2f; // Duration of movement from one waypoint to another
    public bool invertMovement = false; // Set to true to invert movement direction

    public UnityEvent onCheckpointReached; // Event triggered when a checkpoint is reached
    public UnityEvent onCycleCompleted; // Event triggered when all checkpoints in a cycle are completed
    public UnityEvent onStart;

    private Vector3 targetObjectPos; // My precious

    private int[] indexPattern = new int[] { 1, 2, 1, 1, 2, 3 };
    private int currentPatternIndex = 0;

    private void OnEnable()
    {       
        StartCoroutine(RepeatMovement());
        onCheckpointReached?.Invoke();
        onStart?.Invoke();
    }

    IEnumerator RepeatMovement()
    {
        while (true)
        {            
            yield return StartCoroutine(MoveBetweenWaypoints());
            onCycleCompleted?.Invoke();

            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! MoveBetweenTransforms /// RepeatMovement /// Invoke UnityEvent: onCycleCompleted");
        }
    }

    IEnumerator MoveBetweenWaypoints()
    {
        int numWaypoints = waypoints.Length;

        for (int i = 0; i < numWaypoints; i++)
        {     
            int nextWaypointIndex = (i + 1) % numWaypoints;
            yield return StartCoroutine(MoveToWaypoint(waypoints[nextWaypointIndex], nextWaypointIndex));
            onCheckpointReached?.Invoke();

            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! MoveBetweenTransforms /// RepeatMovement /// Invoke MoveBetweenWaypoints: onCheckpointReached");
            yield return null; // Smooth transition
        }
    }

    IEnumerator MoveToWaypoint(Transform waypoint, int index)
    {
        Vector3 startPosition = targetObject.transform.position;
        Vector3 endPosition = waypoint.position;

        if (startPosition == endPosition)
        {
            endPosition = waypoints[index == 0 ? 1 : 0].position;
        }

        int rewardIndex = indexPattern[currentPatternIndex];
        Debug.Log($"Reward Ad activated with index: {rewardIndex}");
        _rewardTimer.ActivateRewardAd(rewardIndex);
        currentPatternIndex = (currentPatternIndex + 1) % indexPattern.Length;

        targetObjectPos = endPosition; // My precious

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            targetObject.transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / moveDuration);

            // Look at the waypoint while moving
            if (!invertMovement)
                targetObject.transform.LookAt(waypoint);
            else
                targetObject.transform.LookAt(startPosition);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        targetObject.transform.position = endPosition;

        float interval = _rewardTimer.GetRandomDelay();
        Debug.Log($"MoveBetweenTransforms /// MoveToWaypoint /// Random Delay: {interval}");
        yield return new WaitForSeconds(interval); // Optional delay before moving to the next waypoint

    }

    public void SetObjectPosition() // My precious
    {
        targetObject.transform.position = targetObjectPos;
    }
}
