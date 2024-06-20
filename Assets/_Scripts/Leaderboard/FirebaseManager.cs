using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.Events;

public class FirebaseManager : MonoBehaviour
{
    [Header("Firebase")]
    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser User;
    public DatabaseReference DBreference;

    [Header("UserData")]
    [SerializeField] private LeaderboardEntry _leaderboardEntry;
    [SerializeField] private Transform _leaderboardContent;
    [SerializeField] private List<LeaderboardEntry> _userList = new();
    [SerializeField] private UIController _uiController;

    GameData _currentGameData;
    GeneralGameData _currentGeneralData;
    private int _rank = 0;
    private LeaderboardEntry _targetUser;
    [HideInInspector] public UnityEvent OnLoadScoreboardData;

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

    public void PrepareData(GeneralGameData generalGameData, GameData gameData)
    {
        _currentGeneralData = generalGameData;
        _currentGameData = gameData;
    }

    //Function for the save button
    public void SaveDataButton()
    {
        OnLoadScoreboardData?.Invoke();

        StartCoroutine(UpdateUsernameDatabase(_currentGeneralData.UserName, _currentGeneralData.UserId));
        StartCoroutine(UpdateMoney(_currentGameData.Money, _currentGeneralData.UserId));
    }

    //Function for the scoreboard button
    public void ScoreboardButton()
    {
        StartCoroutine(LoadScoreboardData());
    }

    private IEnumerator UpdateUsernameDatabase(string _username, string userId)
    {
        //Set the currently logged in user username in the database
        Task DBTask = DBreference.Child("users").Child(userId).Child("username").SetValueAsync(_username);

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

    private IEnumerator UpdateMoney(double _money, string userId)
    {
        Task DBTask = DBreference.Child("users").Child(userId).Child("money").SetValueAsync(_money);

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
        _userList.Clear();
        //Get all the users data ordered by money amount
        Task<DataSnapshot> DBTask = DBreference.Child("users").OrderByChild("money").GetValueAsync();

        yield return new WaitUntil(predicate: () => DBTask.IsCompleted);

        if (DBTask.Exception != null)
        {
            Debug.LogWarning(message: $"Failed to register task with {DBTask.Exception}");
        }
        else
        {
            _rank = 0;
            //Data has been retrieved
            DataSnapshot snapshot = DBTask.Result;

            //Destroy any existing scoreboard elements
            foreach (Transform child in _leaderboardContent.transform)
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

    public void UpdateLeaderboard()
    {
        foreach (var user in _userList)
        {
            if (user.UserName == _currentGeneralData.UserName)
            {
                _targetUser = user;
                break;
            }
        }

        if (_targetUser != null)
        {
            if (_targetUser.UserMoney < _currentGameData.Money)
            {
                _targetUser.UpdateLeaderboardElement(_currentGameData.Money);
            }
        }
        else
        {
            Debug.LogError("Game object not found!");
        }
    }

    public void MoveUserUp()
    {
        if (_targetUser == null)
        {
            Debug.LogError("Target user not found!");
            return;
        }

        int index = _userList.IndexOf(_targetUser);
        if (index == -1)
        {
            Debug.LogError("Target user not found in the user list!");
            return;
        }

        if (index > 0)
        {
            // Swap the target user with the user above it
            LeaderboardEntry temp = _userList[index];
            _userList[index] = _userList[index - 1];
            _userList[index - 1] = temp;

            // Update ranks based on the new order
            UpdateRanks();

            // Update UI positions
            UpdateUIPositions();
        }
    }

    private void UpdateRanks()
    {
        for (int i = 0; i < _userList.Count; i++)
        {
            _userList[i].Rank = i + 1;
        }
    }

    private void UpdateUIPositions()
    {
        foreach (LeaderboardEntry userEntry in _userList)
        {
            // Update UI position of each user entry based on its rank
            // You need to implement this part based on your UI setup
            // For example, you might adjust the position of userEntry.gameObject in a vertical layout group
            // or update the text displaying the rank
        }
    }

}