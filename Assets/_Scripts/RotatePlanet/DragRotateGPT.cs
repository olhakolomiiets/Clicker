using UnityEngine;
using System.Collections;

public class DragRotateGPT : MonoBehaviour
{
    private Vector2 dragStartPos;
    private float lastTouchDistance = 0f; // Для расчёта пинч-зума

    [Header("Настройки вращения объекта")]
    [Tooltip("Множитель для преобразования смещения в скорость вращения (°/пиксель)")]
    public float rotationSpeed = 0.5f;
    [Tooltip("Максимальный угол наклона по оси X (наклон объекта вперёд/назад)")]
    public float maxRotationX = 80f;
    [Tooltip("Базовая скорость авто-вращения (°/сек) по оси Y (вращение объекта вокруг вертикали)")]
    public float autoRotateSpeed = 2f;
    [Tooltip("Коэффициент затухания скорости (чем выше, тем быстрее скорость придёт к базовой)")]
    public float deceleration = 2f;

    [Header("Настройки камеры и зума (через параметр)")]
    [Tooltip("Камера, которую зумим. Если не задана, используется Camera.main")]
    public Camera targetCamera;
    [Tooltip("Позиция камеры при zoomParam = 0 (например, дальняя)")]
    public Vector3 startCamPos = new Vector3(-2f, 97f, -97f);  // Скрин №1
    [Tooltip("Позиция камеры при zoomParam = 1 (например, ближняя)")]
    public Vector3 endCamPos = new Vector3(-2f, 42f, -45f);   // Скрин №2
    [Tooltip("Начальное значение зума (0 = startCamPos, 1 = endCamPos)")]
    public Vector3 shopCamPos = new Vector3(-2f, 42f, -45f);   // Скрин №3
    [Tooltip("Начальное значение зума (0 = startCamPos, 1 = endCamPos)")]
    public float zoomParam = 0f;
    [Tooltip("Скорость изменения zoomParam при колёсике мыши")]
    public float mouseZoomSpeed = 0.5f;
    [Tooltip("Скорость изменения zoomParam при пинч-жесте (масштаб в пикселях)")]
    public float pinchZoomSpeed = 0.001f;

    // Внутренние переменные (скорости вращения в °/сек)
    private float currentAutoRotationSpeed = 0f; // авто-вращение по оси Y
    private float currentDragRotationSpeed = 0f;   // скорость, полученная от перетаскивания (ось Y)
    private bool isDragging = false;

    // Флаг, запрещающий ввод во время анимации зума
    private bool isAutoZooming = false;

    // Для сохранения позиции камеры при анимации
    private Vector3 savedCamPos;
    private bool hasSavedCamPos;

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        currentAutoRotationSpeed = autoRotateSpeed;
        zoomParam = 0f;
        UpdateCameraPosition();
    }

    void Update()
    {
        // Если не происходит автоматическая анимация зума, обрабатываем пользовательский ввод
        if (!isAutoZooming)
        {
            if (Input.touchCount == 2)
            {
                HandlePinchZoom();
            }
            else if (Input.touchCount == 1)
            {
                HandleSingleTouchRotation();
            }
            else
            {
                HandleMouseInput();
            }

            // Зум колесиком мыши (меняем zoomParam)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f && targetCamera != null)
            {
                // scroll > 0 => приближаемся к endCamPos (увеличиваем zoomParam)
                zoomParam += scroll * mouseZoomSpeed;
                zoomParam = Mathf.Clamp01(zoomParam);
                UpdateCameraPosition();
            }
        }

        // Если пользователь не взаимодействует (независимо от анимации), объект продолжает авто-вращение
        if (!isDragging)
        {
            float targetSpeed = (currentAutoRotationSpeed >= 0f) ? autoRotateSpeed : -autoRotateSpeed;
            currentAutoRotationSpeed = Mathf.Lerp(currentAutoRotationSpeed, targetSpeed, deceleration * Time.deltaTime);
            transform.Rotate(Vector3.up, currentAutoRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// Линейно интерполирует позицию камеры от startCamPos к endCamPos по zoomParam.
    /// </summary>
    void UpdateCameraPosition()
    {
        if (targetCamera == null) return;
        Vector3 newPos = Vector3.Lerp(startCamPos, endCamPos, zoomParam);
        targetCamera.transform.position = newPos;
    }

    /// <summary>
    /// Обработка пинч-зума (изменение zoomParam) при двух пальцах.
    /// </summary>
    void HandlePinchZoom()
    {
        if (Input.touchCount < 2)
            return;

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
        {
            lastTouchDistance = Vector2.Distance(touch0.position, touch1.position);
            isDragging = false; // не вращаем объект при двух пальцах
            return;
        }

        float currentTouchDistance = Vector2.Distance(touch0.position, touch1.position);
        float deltaDistance = currentTouchDistance - lastTouchDistance;

        zoomParam += deltaDistance * pinchZoomSpeed;
        zoomParam = Mathf.Clamp01(zoomParam);
        UpdateCameraPosition();

        lastTouchDistance = currentTouchDistance;
    }

    /// <summary>
    /// Вращение объекта одним касанием.
    /// </summary>
    void HandleSingleTouchRotation()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            dragStartPos = touch.position;
            isDragging = true;
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            Vector2 dragDelta = touch.position - dragStartPos;
            float rotationYSpeed = -dragDelta.x * rotationSpeed;
            float rotationXSpeed = dragDelta.y * rotationSpeed;

            float currentRotationX = transform.eulerAngles.x;
            currentRotationX = (currentRotationX > 180f) ? currentRotationX - 360f : currentRotationX;
            float newRotationX = Mathf.Clamp(currentRotationX + rotationXSpeed * Time.deltaTime, -maxRotationX, maxRotationX);
            float appliedRotationX = newRotationX - currentRotationX;

            transform.Rotate(Vector3.up, rotationYSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, appliedRotationX, Space.World);

            currentDragRotationSpeed = rotationYSpeed;
            dragStartPos = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isDragging = false;
            currentAutoRotationSpeed = currentDragRotationSpeed;
        }
    }

    /// <summary>
    /// Вращение объекта мышью (левая кнопка).
    /// </summary>
    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = Input.mousePosition;
            isDragging = true;
        }
        else if (Input.GetMouseButton(0))
        {
            Vector2 currentPos = Input.mousePosition;
            Vector2 dragDelta = currentPos - dragStartPos;
            float rotationYSpeed = -dragDelta.x * rotationSpeed;
            float rotationXSpeed = dragDelta.y * rotationSpeed;

            float currentRotationX = transform.eulerAngles.x;
            currentRotationX = (currentRotationX > 180f) ? currentRotationX - 360f : currentRotationX;
            float newRotationX = Mathf.Clamp(currentRotationX + rotationXSpeed * Time.deltaTime, -maxRotationX, maxRotationX);
            float appliedRotationX = newRotationX - currentRotationX;

            transform.Rotate(Vector3.up, rotationYSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, appliedRotationX, Space.World);

            currentDragRotationSpeed = rotationYSpeed;
            dragStartPos = currentPos;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            currentAutoRotationSpeed = currentDragRotationSpeed;
        }
    }

    //=====================================================
    //      Публичные методы для анимационного зума
    //=====================================================

    /// <summary>
    /// Сохраняет текущую позицию камеры и плавно зумит до максимума (endCamPos) за заданное время.
    /// </summary>
    public void SaveAndZoomToMax(float duration)
    {
        if (targetCamera == null) return;
        savedCamPos = targetCamera.transform.position;
        hasSavedCamPos = true;

        StopAllCoroutines();
        StartCoroutine(ZoomCameraCoroutine(targetCamera.transform.position, shopCamPos, duration));
    }

    /// <summary>
    /// Плавно возвращает камеру с максимума к сохранённой позиции за заданное время, если сохранённая позиция имеется.
    /// </summary>
    public void ZoomBack(float duration)
    {
        if (targetCamera == null || !hasSavedCamPos) return;

        StopAllCoroutines();
        StartCoroutine(ZoomCameraCoroutine(targetCamera.transform.position, savedCamPos, duration));
    }

    /// <summary>
    /// Корутина для плавного перемещения камеры между двумя позициями за duration секунд.
    /// Во время анимации отключается пользовательский ввод.
    /// </summary>
    private IEnumerator ZoomCameraCoroutine(Vector3 fromPos, Vector3 toPos, float duration)
    {
        isAutoZooming = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            targetCamera.transform.position = Vector3.Lerp(fromPos, toPos, t);
            yield return null;
        }
        targetCamera.transform.position = toPos;
        isAutoZooming = false;
    }
}
