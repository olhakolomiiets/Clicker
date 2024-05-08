using UnityEngine;

[CreateAssetMenu]

public class UserData : ScriptableObject
{
    [field: SerializeField]
    public string UserName { get; set; }
    [field: SerializeField]
    public string UserId { get; set; }
    public double UserMoney { get; set; }
}
