using UnityEngine;

public class PerspectiveCameraController : MonoBehaviour
{
    [Header("Настройки зума (Field of View)")]
    public float zoomSpeed = 2f;      // Скорость изменения FOV
    public float minFOV = 15f;        // Минимальное значение FOV
    public float maxFOV = 90f;        // Максимальное значение FOV

    [Header("Настройки панорамирования (Pan)")]
    public float panSpeed = 1f;       // Множитель для смещения камеры при панорамировании

    [Header("Ограничения видимой области (крайние координаты)")]
    [Tooltip("Минимальное значение по оси X")]
    public float boundaryMinX = -10f;
    [Tooltip("Максимальное значение по оси X")]
    public float boundaryMaxX = 10f;
    [Tooltip("Минимальное значение по оси Z")]
    public float boundaryMinZ = -10f;
    [Tooltip("Максимальное значение по оси Z")]
    public float boundaryMaxZ = 10f;

    private Camera cam;
    private Vector3 lastPanPosition;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        HandleMouse();
        HandleTouch();
        ClampCamera();
    }

    // Обработка ввода мышью
    void HandleMouse()
    {
        // Зум с помощью колеса мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Zoom(-scroll * zoomSpeed * 100f * Time.deltaTime);
        }

        // Панорамирование: запоминаем позицию при нажатии и двигаем при удержании кнопки
        if (Input.GetMouseButtonDown(0))
        {
            lastPanPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            PanCamera(Input.mousePosition);
        }
    }

    // Обработка тач-ввода
    void HandleTouch()
    {
        if (Input.touchSupported && Input.touchCount > 0)
        {
            // Зум: жест пинч двумя пальцами
            if (Input.touchCount == 2)
            {
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float currentMagnitude = (touchZero.position - touchOne.position).magnitude;
                float deltaMagnitude = prevMagnitude - currentMagnitude;

                Zoom(deltaMagnitude * zoomSpeed * Time.deltaTime);
            }
            // Панорамирование: если один палец
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    lastPanPosition = touch.position;
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    PanCamera(touch.position);
                }
            }
        }
    }

    // Перемещение камеры с использованием проекции экранных координат на плоскость Y = 0
    void PanCamera(Vector3 newPanPosition)
    {
        // Определяем плоскость, на которой находится игровой мир (Y = 0)
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Ray rayOld = cam.ScreenPointToRay(lastPanPosition);
        Ray rayNew = cam.ScreenPointToRay(newPanPosition);

        if (groundPlane.Raycast(rayOld, out float enterOld) && groundPlane.Raycast(rayNew, out float enterNew))
        {
            Vector3 worldOld = rayOld.GetPoint(enterOld);
            Vector3 worldNew = rayNew.GetPoint(enterNew);
            Vector3 offset = worldOld - worldNew;
            transform.position += offset * panSpeed;
        }

        lastPanPosition = newPanPosition;
    }

    // Зумирование камеры изменением поля зрения (Field of View)
    void Zoom(float increment)
    {
        float fov = cam.fieldOfView + increment;
        cam.fieldOfView = Mathf.Clamp(fov, minFOV, maxFOV);
    }

    // Ограничение позиции камеры по крайним координатам (X и Z)
    void ClampCamera()
    {
        // Определяем плоскость Y = 0
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        // Получаем расстояние от камеры до плоскости
        Ray camRay = new Ray(cam.transform.position, cam.transform.forward);
        if (!groundPlane.Raycast(camRay, out float distance))
            return;

        // Вычисляем мировые координаты углов экрана на плоскости
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, distance));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, distance));

        // Текущие крайние координаты видимой области
        float currentMinX = bottomLeft.x;
        float currentMinZ = bottomLeft.z;
        float currentMaxX = topRight.x;
        float currentMaxZ = topRight.z;

        Vector3 pos = transform.position;

        // Если левая сторона выходит за минимальное значение по X, сдвигаем камеру вправо
        if (currentMinX < boundaryMinX)
        {
            pos.x += boundaryMinX - currentMinX;
        }
        // Если правая сторона выходит за максимальное значение по X, сдвигаем камеру влево
        if (currentMaxX > boundaryMaxX)
        {
            pos.x -= currentMaxX - boundaryMaxX;
        }
        // Если нижняя сторона выходит за минимальное значение по Z, сдвигаем камеру вверх (по оси Z)
        if (currentMinZ < boundaryMinZ)
        {
            pos.z += boundaryMinZ - currentMinZ;
        }
        // Если верхняя сторона выходит за максимальное значение по Z, сдвигаем камеру вниз (по оси Z)
        if (currentMaxZ > boundaryMaxZ)
        {
            pos.z -= currentMaxZ - boundaryMaxZ;
        }

        transform.position = pos;
    }
}
