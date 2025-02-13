using UnityEngine;

public class FaceTarget : MonoBehaviour
{
    [Header("Целевой объект (ГО), на который смотрим")]
    [SerializeField] private Transform target;

    [Header("Настройки поворота")]
    [SerializeField] private bool smoothRotation = false;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Смещение по оси Y (если требуется корректировка)")]
    [SerializeField] private float yRotationOffset = 0f;

    void LateUpdate()
    {
        if (target == null)
            return;

        // Вычисляем направление на цель, игнорируя вертикальную разницу
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        // Вычисляем угол в градусах по оси Y с помощью Atan2.
        // Atan2 возвращает угол в радианах, умножаем на Rad2Deg для перевода в градусы.
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Применяем дополнительное смещение, если нужно
        targetAngle += yRotationOffset;

        // Если необходимо, можно привести угол к диапазону -180...180.
        if (targetAngle > 180f)
            targetAngle -= 360f;
        else if (targetAngle < -180f)
            targetAngle += 360f;

        // Создаем кватернион, поворачивая только по оси Y
        Quaternion desiredRotation = Quaternion.Euler(0f, targetAngle, 0f);

        // Применяем поворот: мгновенно или плавно
        if (smoothRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = desiredRotation;
        }

        // Для отладки: выводим вычисленный угол в консоль.
        Debug.Log("Computed Y Angle: " + targetAngle);
    }
}
