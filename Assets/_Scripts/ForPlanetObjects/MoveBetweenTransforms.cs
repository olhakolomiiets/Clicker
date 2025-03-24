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
    public float repeatDelay = 60f; // Delay before repeating the entire movement cycle
    public bool invertMovement = false; // Set to true to invert movement direction

    private void OnEnable()
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
            yield return StartCoroutine(MoveToWaypoint(waypoints[nextWaypointIndex], nextWaypointIndex));

            yield return null; // Smooth transition
        }
    }

    IEnumerator MoveToWaypoint(Transform waypoint, int index)
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

        yield return new WaitForSeconds(repeatDelay);
    }
}
