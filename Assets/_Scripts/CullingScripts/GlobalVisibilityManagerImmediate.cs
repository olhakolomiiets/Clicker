using UnityEngine;
using System.Collections.Generic;

public class GlobalVisibilityManagerImmediate : MonoBehaviour
{
    [Tooltip("На сколько (в мировых единицах) расширять границы объекта для раннего включения")]
    public float enableMargin = 1.0f;

    [Tooltip("Время задержки (в секундах) после выхода из кадра для отключения объекта")]
    public float disableDelay = 1.0f;

    private Camera mainCamera;
    // Список всех управляемых рендереров
    private List<MeshRenderer> renderersToManage = new List<MeshRenderer>();
    // Словарь для хранения времени последней видимости каждого рендерера
    private Dictionary<MeshRenderer, float> lastVisibleTime = new Dictionary<MeshRenderer, float>();

    void Start()
    {
        mainCamera = Camera.main;
        // Ищем все объекты с MeshRenderer в сцене при запуске
        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        foreach (MeshRenderer mr in allRenderers)
        {
            RegisterRenderer(mr);
        }
    }

    void Update()
    {
        if (mainCamera == null)
            return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        float currentTime = Time.time;

        // Проходим по всем управляемым рендерерам
        foreach (MeshRenderer mr in renderersToManage)
        {
            if (mr == null)
                continue;

            // Берём реальные границы объекта и расширяем их для раннего включения
            Bounds bounds = mr.bounds;
            bounds.Expand(enableMargin);

            // Проверяем, пересекаются ли расширенные границы с фрустумом камеры
            bool isInFrustum = GeometryUtility.TestPlanesAABB(planes, bounds);

            if (isInFrustum)
            {
                // Если объект в расширенном фрустуме, немедленно включаем MeshRenderer
                if (!mr.enabled)
                    mr.enabled = true;
                // Обновляем время, когда объект последний раз был видим
                lastVisibleTime[mr] = currentTime;
            }
            else
            {
                // Если объект не виден и прошло достаточно времени с момента последней видимости, отключаем его
                if (mr.enabled && (currentTime - lastVisibleTime[mr] >= disableDelay))
                {
                    mr.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Регистрация нового объекта для управления видимостью.
    /// Вызывайте этот метод при создании новых объектов с MeshRenderer.
    /// </summary>
    public void RegisterRenderer(MeshRenderer mr)
    {
        if (mr == null)
            return;

        if (!renderersToManage.Contains(mr))
        {
            renderersToManage.Add(mr);
            lastVisibleTime[mr] = Time.time;
        }
    }

    /// <summary>
    /// Отмена регистрации объекта (если объект удаляется или больше не нужен).
    /// </summary>
    public void UnregisterRenderer(MeshRenderer mr)
    {
        if (mr == null)
            return;

        if (renderersToManage.Contains(mr))
        {
            renderersToManage.Remove(mr);
            lastVisibleTime.Remove(mr);
        }
    }
}
