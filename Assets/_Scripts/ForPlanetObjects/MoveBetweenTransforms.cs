using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class MoveBetweenTransforms : MonoBehaviour
{
    public GameObject targetObject; // The object to move
    public Transform[] waypoints; // Array of waypoints to move between
    public float moveDuration = 2f; // Duration of movement from one waypoint to another
    public float minRandomDelay = 1f; // Minimum random delay between movements
    public float maxRandomDelay = 5f; // Maximum random delay between movements
    public float repeatDelay = 3f; // Delay before repeating the entire movement cycle
    public bool invertMovement = false; // Set to true to invert movement direction

    public UnityEvent onCheckpointReached; // Event triggered when a checkpoint is reached
    public UnityEvent onCycleCompleted; // Event triggered when all checkpoints in a cycle are completed

    private void OnEnable()
    {
        StartCoroutine(RepeatMovement());
    }

    IEnumerator RepeatMovement()
    {
        while (true)
        {
            yield return StartCoroutine(MoveBetweenWaypoints());
            onCycleCompleted?.Invoke();
            float delayBeforeRepeat = Random.Range(minRandomDelay, maxRandomDelay);
            yield return new WaitForSeconds(delayBeforeRepeat);
        }
    }

    IEnumerator MoveBetweenWaypoints()
    {
        int numWaypoints = waypoints.Length;

        for (int i = 0; i < numWaypoints; i++)
        {
            int nextWaypointIndex = (i + 1) % numWaypoints;
            yield return StartCoroutine(MoveToWaypoint(waypoints[nextWaypointIndex]));
            onCheckpointReached?.Invoke();
            yield return null; // Smooth transition
        }
    }

    IEnumerator MoveToWaypoint(Transform waypoint)
    {
        Vector3 startPosition = targetObject.transform.position;
        Vector3 endPosition = waypoint.position;
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
        yield return new WaitForSeconds(4f); // Optional delay before moving to the next waypoint
    }
}
