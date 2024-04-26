using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class EditTextPopup : MonoBehaviour
    {
        [SerializeField]
        private Text Title;
        [SerializeField]
        private InputField Input;

        private Action<string> SaveAction { get; set; }
        private Action CancelAction { get; set; }

        // setup popup information
        public void Setup(EditTextPopupRequest request)
        {
            Clear();
            Title.text = request.Title;
            Input.text = request.InitialInput;
            SaveAction = request.SaveAction;
            CancelAction = request.CancelAction;
        }

        // reset view
        private void Clear()
        {
            Title.text = string.Empty;
            Input.text = string.Empty;
            SaveAction = null;
            CancelAction = null;
        }

        // button event
        public void SaveHandler()
        {
            SaveAction?.Invoke(Input.text);
            gameObject.SetActive(false);
        }

        public void NoHandler()
        {
            CancelAction?.Invoke();
            gameObject.SetActive(false);
        }
    }

    public struct EditTextPopupRequest
    {
        public string Title;
        public string InitialInput;
        public Action<string> SaveAction;
        public Action CancelAction;
    }
}
