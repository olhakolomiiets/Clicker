using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public class FirebaseManager : MonoBehaviour
{
    //Firebase variables
    [Header("Firebase")]
    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;    
    public FirebaseUser User;
    public DatabaseReference DBreference;

    //User Data variables
    [Header("UserData")]
    [SerializeField] private UserData _userData;
    [SerializeField] private LeaderboardEntry _leaderboardEntry;
    [SerializeField] private Transform _leaderboardContent;

    [SerializeField] private List<LeaderboardEntry> _userList = new();
    [SerializeField] private UIController _uiController;

    GameData _currentGameData;
    private int _rank = 0;

    void Awake()
    {
        //Check that all of the necessary dependencies for Firebase are present on the system
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                //If they are avalible Initialize Firebase
                InitializeFirebase();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    private void InitializeFirebase()
    {
        Debug.Log("Setting up Firebase Auth");
        //Set the authentication instance object
        auth = FirebaseAuth.DefaultInstance;
        DBreference = FirebaseDatabase.DefaultInstance.RootReference;
    }

    public void PrepareGameData(GameData gameData)
    {
        _currentGameData = gameData;
    }

    //Function for the save button
    public void SaveDataButton()
    {
        _userList.Clear();

        StartCoroutine(UpdateUsernameDatabase(_userData.UserName));
        StartCoroutine(UpdateMoney(_currentGameData.Money));
    }

    //Function for the scoreboard button
    public void ScoreboardButton()
    {        
        StartCoroutine(LoadScoreboardData());
    }

    private IEnumerator UpdateUsernameDatabase(string _username)
    {
        //Set the currently logged in user username in the database
        Task DBTask = DBreference.Child("users").Child(_userData.UserId).Child("username").SetValueAsync(_username);

        yield return new WaitUntil(predicate: () => DBTask.IsCompleted);

        if (DBTask.Exception != null)
        {
            Debug.LogWarning(message: $"Failed to register task with {DBTask.Exception}");
        }
        else
        {
            //Database username is now updated
        }
    }

    private IEnumerator UpdateMoney(double _money)
    {
        Task DBTask = DBreference.Child("users").Child(_userData.UserId).Child("money").SetValueAsync(_money);

        yield return new WaitUntil(predicate: () => DBTask.IsCompleted);

        if (DBTask.Exception != null)
        {
            Debug.LogWarning(message: $"Failed to register task with {DBTask.Exception}");
        }
        else
        {
            //Money are now updated
        }

        StartCoroutine(LoadScoreboardData());
    }

    private IEnumerator LoadScoreboardData()
    {
        //Get all the users data ordered by money amount
        Task<DataSnapshot>  DBTask = DBreference.Child("users").OrderByChild("money").GetValueAsync();

        yield return new WaitUntil(predicate: () => DBTask.IsCompleted);

        if (DBTask.Exception != null)
        {
            Debug.LogWarning(message: $"Failed to register task with {DBTask.Exception}");
        }
        else
        {
            //Data has been retrieved
            DataSnapshot snapshot = DBTask.Result;

            //Destroy any existing scoreboard elements
            foreach (Transform child in _leaderboardEntry.transform)
            {
                Destroy(child.gameObject);
            }

            //Loop through every users UID
            foreach (DataSnapshot childSnapshot in snapshot.Children.Reverse<DataSnapshot>())
            {
                string username = childSnapshot.Child("username").Value.ToString();
                double money = double.Parse(childSnapshot.Child("money").Value.ToString());
                _rank++;

                //Instantiate new scoreboard elements
                LeaderboardEntry scoreboardElement = Instantiate(_leaderboardEntry, _leaderboardContent).GetComponent<LeaderboardEntry>();
                scoreboardElement.NewElement(_rank, username, money);

                _userList.Add(scoreboardElement);
            }

            //Go to scoareboard screen
            _uiController.LeaderboardScreen();
        }
    }

    public void UpdateUserRank()
    {
        LeaderboardEntry targetUser = null;
        foreach (var user in _userList)
        {
            if (user.UserName == _userData.UserName)
            {
                targetUser = user;
                break;
            }
        }

        if (targetUser != null)
        {
            foreach (var user in _userList)
            {
                if (user.UserMoney < _currentGameData.Money)
                {
                    user.Rank++;
                    user.UpdateLeaderboardRank(_currentGameData.Money);
                }
            }
            _userList.Sort((x, y) => y.Rank.CompareTo(x.Rank));
        }
        else
        {
            Debug.LogError("Game object not found!");
        }
    }
}