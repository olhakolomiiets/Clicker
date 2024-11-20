using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundBtn : MonoBehaviour
{
    [SerializeField]
    private AudioMixer _audioMixer;

    [SerializeField]
    private string _musicGroupName = "Music";

    [SerializeField]
    private AudioMixerSnapshot _defaultAudioMixedSnapshot, _mutedAudioMixerSnapshot;

    [SerializeField]
    private Sprite _soundOffIcon, _soundOnIcon;

    [SerializeField]
    private Image _buttonIconImage;


    private bool _soundToggle = true;

    private bool _isMusicOn;

    private void Awake()
    {
        UpdateMusic();
    }

    public void ToggleMusic()
    {
        _soundToggle = !_soundToggle;
        _buttonIconImage.sprite = _soundToggle ? _soundOffIcon : _soundOnIcon;
        if (_soundToggle)
            _defaultAudioMixedSnapshot.TransitionTo(0.1f);
        else
            _mutedAudioMixerSnapshot.TransitionTo(0.1f);
    }

    public void SetMusicVolume()
    {
        _isMusicOn = !_isMusicOn;
        _buttonIconImage.sprite = _isMusicOn ? _soundOffIcon : _soundOnIcon;
        if (_audioMixer != null)
        {
            float musicVolume;
            if (_isMusicOn)
            {
                musicVolume = -13f;
                PlayerPrefs.SetInt("music", 0);
            }
            else
            {
                musicVolume = -80f;
                PlayerPrefs.SetInt("music", 1);
            }
            _audioMixer.SetFloat(_musicGroupName, musicVolume);
        }

    }

    private void UpdateMusic()
    {
        if (PlayerPrefs.HasKey("music"))
        {
            _isMusicOn = PlayerPrefs.GetInt("music") == 1 ? true : false; ;
        }
        _buttonIconImage.sprite = _isMusicOn ? _soundOffIcon : _soundOnIcon;
        SetMusicVolume();
    }
}