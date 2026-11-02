
using UnityEngine;

[DisallowMultipleComponent]
public class PassiveIncomeChestClick : MonoBehaviour
{
    private PassiveIncomeChestController _controller;

    private bool _pressAllowed;
    private bool _isDragging;
    private Vector3 _pressPosition;

    private const float DragThreshold = 15f;

    public void Initialize(PassiveIncomeChestController controller)
    {
        _controller = controller;
    }

    private void OnMouseDown()
    {
        _pressAllowed = !PlanetInputBlocker.IsBlocked();
        _isDragging = false;
        _pressPosition = Input.mousePosition;
    }

    private void OnMouseDrag()
    {
        if (!_pressAllowed)
            return;

        if ((Input.mousePosition - _pressPosition).sqrMagnitude >
            DragThreshold * DragThreshold)
        {
            _isDragging = true;
        }
    }

    private void OnMouseUpAsButton()
    {
        if (!_pressAllowed || _isDragging ||
            PlanetInputBlocker.IsBlocked())
        {
            _pressAllowed = false;
            return;
        }

        if ((Input.mousePosition - _pressPosition).sqrMagnitude <=
            DragThreshold * DragThreshold)
        {
            _controller?.HandleChestClick();
        }

        _pressAllowed = false;
    }

    private void OnDisable()
    {
        _pressAllowed = false;
        _isDragging = false;
    }
}
