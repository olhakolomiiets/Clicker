using UnityEngine;

[CreateAssetMenu]

public class LeaderboardData : ScriptableObject
{
    [field: SerializeField]
    public string UserName { get; set; }
}
