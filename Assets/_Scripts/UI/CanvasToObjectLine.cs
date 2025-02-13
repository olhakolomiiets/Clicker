using UnityEngine;

public class CanvasToObjectLine : MonoBehaviour
{
    [Header("Начальный объект (World Space)")]
    public Transform startObject;

    [Header("Конечный объект (World Space)")]
    public Transform endObject;

    [Header("Настройки линии")]
    public float lineThickness = 0.1f; // толщина линии

    private LineRenderer lineRenderer;

    void Awake()
    {
        // Получаем компонент LineRenderer или добавляем его, если его нет
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Настройка LineRenderer
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineThickness;
        lineRenderer.endWidth = lineThickness;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        // Устанавливаем сортировочный порядок, чтобы линия была позади Canvas
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = -100;
    }

    void Update()
    {
        if (startObject != null && endObject != null)
        {
            // Обновляем толщину, если lineThickness изменяется в процессе игры
            lineRenderer.startWidth = lineThickness;
            lineRenderer.endWidth = lineThickness;

            // Устанавливаем позиции начала и конца линии
            lineRenderer.SetPosition(0, startObject.position);
            lineRenderer.SetPosition(1, endObject.position);
        }
    }
}
