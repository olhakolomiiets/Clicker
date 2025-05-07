using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Firebase.Analytics;

public class FeedbackCollector : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtData;
    [SerializeField] private Button btnSubmit;
    [SerializeField] private CollectionOption option;

    private enum CollectionOption { openGFormLink, sendGFormData };

    private const string kReceiverEmailAddress = "noc.game.dev@gmail.com";

    private const string kGFormBaseURL = "https://docs.google.com/forms/d/e/1FAIpQLSfj9vPK33ovHiBJlv7ZYaoJIHSymvzO_ompAqdqjVVUFN43ow/";
    private const string kGFormEntryID = "entry.141714192";

    void Start()
    {
        UnityEngine.Assertions.Assert.IsNotNull(txtData);
        UnityEngine.Assertions.Assert.IsNotNull(btnSubmit);
        btnSubmit.onClick.AddListener(delegate {
            switch (option)
            {
                case CollectionOption.openGFormLink:
                    OpenGFormLink();
                    break;
                case CollectionOption.sendGFormData:
                    StartCoroutine(SendGFormData(txtData.text));
                    FirebaseAnalytics.LogEvent(name: "feedback_in_googleForm");
                    break;
            }
        });
    }

    private static void OpenGFormLink()
    {
        string urlGFormView = kGFormBaseURL + "viewform";
        OpenLink(urlGFormView);
    }

    private static IEnumerator SendGFormData<T>(T dataContainer)
    {
        bool isString = dataContainer is string;
        string jsonData = isString ? dataContainer.ToString() : JsonUtility.ToJson(dataContainer);

        WWWForm form = new WWWForm();
        form.AddField(kGFormEntryID, jsonData);
        string urlGFormResponse = kGFormBaseURL + "formResponse";
        using (UnityWebRequest www = UnityWebRequest.Post(urlGFormResponse, form))
        {
            yield return www.SendWebRequest();
        }
    }

    // We cannot have spaces in links for iOS
    public static void OpenLink(string link)
    {
        bool googleSearch = link.Contains("google.com/search");
        string linkNoSpaces = link.Replace(" ", googleSearch ? "+" : "%20");
        Application.OpenURL(linkNoSpaces);
    }
}


