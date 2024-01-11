using UnityEngine;
using UnityEngine.EventSystems;

public class Rotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{

	[SerializeField] float force = 10f;

	Rigidbody rb;
	bool isDragging = false;
	Vector3 direction;

	void Awake()
	{
		rb = GetComponent<Rigidbody>();
	}

	public void OnBeginDrag(PointerEventData pointerEventData)
	{
		isDragging = true;
	}

	public void OnEndDrag(PointerEventData pointerEventData)
	{
		isDragging = false;
	}

	public void OnDrag(PointerEventData pointerEventData)
	{
		direction = new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0f);
	}

	void FixedUpdate()
	{
		if (isDragging)
		{
			rb.AddTorque(direction * force * Time.fixedDeltaTime);
		}
	}
}

