using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScorePanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI coreText;

    private Sequence _diamondsAnimation;
    private Tween _textBounceTween;

    public void SetScore(double score)
    {
        if(score > 1000)
            coreText.text = AbbreviateNumber(score);
        else
            coreText.text = $"{score.ToString("N0")}";
    }

    public void SetDiamondsScore(double score)
    {
        coreText.text = $"{score.ToString("N0")}";
    }

    public Tween AnimateDiamondsScore(double from, double to, float duration)
    {
        if (coreText == null)
            return null;

        _diamondsAnimation?.Kill(false);
        _textBounceTween?.Kill(false);
        DestroyRuntimeIconGlows();

        duration = Mathf.Max(0.01f, duration);

        Transform panelTransform = transform;
        Transform textTransform = coreText.transform;
        Image iconImage = GetPrimaryIconImage();
        Transform iconTransform = iconImage != null ? iconImage.transform : null;

        Vector3 panelScale = panelTransform.localScale;
        Vector3 textScale = textTransform.localScale;
        Vector3 iconScale = iconTransform != null ? iconTransform.localScale : Vector3.one;
        Color textColor = coreText.color;
        Color iconColor = iconImage != null ? iconImage.color : Color.white;
        Color pulseTextColor = new Color(0.78f, 0.94f, 1f, textColor.a);
        Color pulseIconColor = new Color(0.78f, 0.94f, 1f, iconColor.a);

        int lastDisplayedValue = int.MinValue;
        double displayedValue = from;

        _diamondsAnimation = DOTween.Sequence();
        _diamondsAnimation
            .AppendCallback(() =>
            {
                coreText.text = $"{from.ToString("N0")}";
                panelTransform.localScale = panelScale;
                textTransform.localScale = textScale;
                coreText.color = textColor;

                if (iconTransform != null)
                    iconTransform.localScale = iconScale;

                if (iconImage != null)
                    iconImage.color = iconColor;
            })
            .Append(DOTween.To(
                () => displayedValue,
                value =>
                {
                    displayedValue = value;
                    int roundedValue = Mathf.RoundToInt((float)value);

                    if (roundedValue == lastDisplayedValue)
                        return;

                    lastDisplayedValue = roundedValue;
                    coreText.text = $"{roundedValue:N0}";
                    PlayTextBounce(textTransform, textScale);
                },
                to,
                duration).SetEase(Ease.OutCubic))
            .Join(panelTransform.DOScale(panelScale * 1.12f, duration * 0.3f)
                .SetEase(Ease.OutSine)
                .SetLoops(2, LoopType.Yoyo))
            .Join(DOTween.To(
                () => coreText.color,
                color => coreText.color = color,
                pulseTextColor,
                duration * 0.25f).SetEase(Ease.OutSine).SetLoops(4, LoopType.Yoyo));

        if (iconImage != null)
        {
            _diamondsAnimation.Join(DOTween.To(
                () => iconImage.color,
                color => iconImage.color = color,
                pulseIconColor,
                duration * 0.25f).SetEase(Ease.OutSine).SetLoops(4, LoopType.Yoyo));
        }

        _diamondsAnimation
            .Append(DOTween.To(
                () => coreText.color,
                color => coreText.color = color,
                textColor,
                0.2f).SetEase(Ease.OutSine))
            .OnComplete(() =>
            {
                RestoreDiamondsAnimationState(
                    to,
                    panelTransform,
                    textTransform,
                    iconTransform,
                    iconImage,
                    panelScale,
                    textScale,
                    iconScale,
                    textColor,
                    iconColor);
            })
            .OnKill(() =>
            {
                if (_diamondsAnimation != null && !_diamondsAnimation.IsComplete())
                {
                    RestoreDiamondsAnimationState(
                        to,
                        panelTransform,
                        textTransform,
                        iconTransform,
                        iconImage,
                        panelScale,
                        textScale,
                        iconScale,
                        textColor,
                        iconColor);
                }

                _diamondsAnimation = null;
            });

        return _diamondsAnimation;
    }


    string AbbreviateNumber(double number)
    {
        string[] suffixes = { "", "K", "M", "B", "T" };
        int suffixIndex = 0;

        while (number >= 1000 && suffixIndex < suffixes.Length - 1)
        {
            number /= 1000;
            suffixIndex++;
        }

        return string.Format("{0:0.##} {1}", number, suffixes[suffixIndex]).Replace(',', '.');
    }

    private void PlayTextBounce(Transform textTransform, Vector3 baseScale)
    {
        _textBounceTween?.Kill(false);
        textTransform.localScale = baseScale;
        _textBounceTween = textTransform
            .DOPunchScale(Vector3.one * 0.08f, 0.12f, 1, 0.35f)
            .OnComplete(() => textTransform.localScale = baseScale);
    }

    private Image GetPrimaryIconImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].sprite != null)
                return images[i];
        }

        return null;
    }

    private void DestroyRuntimeIconGlows()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "RuntimeDiamondIconGlow")
                Destroy(children[i].gameObject);
        }
    }

    private void RestoreDiamondsAnimationState(
        double finalValue,
        Transform panelTransform,
        Transform textTransform,
        Transform iconTransform,
        Image iconImage,
        Vector3 panelScale,
        Vector3 textScale,
        Vector3 iconScale,
        Color textColor,
        Color iconColor)
    {
        _textBounceTween?.Kill(false);
        _textBounceTween = null;

        coreText.text = $"{finalValue.ToString("N0")}";
        coreText.color = textColor;
        panelTransform.localScale = panelScale;
        textTransform.localScale = textScale;

        if (iconTransform != null)
            iconTransform.localScale = iconScale;

        if (iconImage != null)
            iconImage.color = iconColor;

        DestroyRuntimeIconGlows();
    }

}
