using System.Collections;
using UnityEngine;

public class MoveBetweenTransforms : MonoBehaviour
{
    public GameObject targetObject;
    public Transform[] waypoints;
    public float moveDuration = 2f;
    public float minRandomDelay = 1f;
    public float maxRandomDelay = 5f;
    public float repeatDelay = 3f;
    public bool invertMovement = false; // Set to true to invert movement

    private void Start()
    {
        StartCoroutine(RepeatMovement());
    }

    IEnumerator RepeatMovement()
    {
        while (true)
        {
            yield return StartCoroutine(MoveBetweenWaypoints());
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