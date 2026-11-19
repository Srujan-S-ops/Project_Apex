using System;
using Apex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Animated hover/selection treatment shared by all PROJECT APEX buttons.</summary>
    public sealed class ApexButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        public Image Background;
        public Image Accent;
        public Text Label;
        public Text Sub;
        public RectTransform Content;
        public Color IdleText = UITheme.Metal;
        public Color ActiveText = UITheme.White;
        public Color IdleBg = new Color(1f, 1f, 1f, 0f);
        public Color ActiveBg = new Color(1f, 1f, 1f, 0.06f);
        public float SlideX = 14f;
        public bool Selected { get; private set; }

        private float _k;
        private float _flash;

        public void OnSelect(BaseEventData e) { Selected = true; UISound.Tick(); }
        public void OnDeselect(BaseEventData e) => Selected = false;

        public void OnPointerEnter(PointerEventData e)
        {
            var b = GetComponent<Selectable>();
            if (b != null && b.interactable && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSubmit(BaseEventData e) { _flash = 1f; UISound.Confirm(); }
        public void OnPointerClick(PointerEventData e) { _flash = 1f; UISound.Confirm(); }

        private void OnDisable() { Selected = false; _k = 0f; Apply(); }

        private void Update()
        {
            _k = Mathf.MoveTowards(_k, Selected ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 3f);
            Apply();
        }

        private void Apply()
        {
            float e = UITween.EaseOutCubic(_k);
            if (Background != null) Background.color = Color.Lerp(IdleBg, ActiveBg, e) + new Color(_flash * 0.3f, 0f, 0f, _flash * 0.2f);
            if (Accent != null)
            {
                Accent.rectTransform.localScale = new Vector3(Mathf.Lerp(0.0f, 1f, e), 1f, 1f);
                Accent.color = UITheme.Red;
            }
            if (Label != null) Label.color = Color.Lerp(IdleText, ActiveText, e);
            if (Sub != null) Sub.color = new Color(UITheme.Metal.r, UITheme.Metal.g, UITheme.Metal.b, e * 0.85f);
            if (Content != null) Content.anchoredPosition = new Vector2(SlideX * e, Content.anchoredPosition.y);
        }
    }

    /// <summary>Left/right option selector ("‹ VALUE ›") that integrates with keyboard/gamepad navigation.</summary>
    public sealed class ApexStepper : Selectable
    {
        public string[] Options = Array.Empty<string>();
        public int Index;
        public Text ValueText;
        public Action<int> Changed;
        public Image[] Pips;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { Step(-1); return; }
            if (e.moveDir == MoveDirection.Right) { Step(1); return; }
            base.OnMove(e);
        }

        public override void OnSelect(BaseEventData e) { base.OnSelect(e); UISound.Tick(); }

        public void Step(int dir)
        {
            if (Options.Length == 0 || !interactable) return;
            Index = (Index + dir + Options.Length) % Options.Length;
            Refresh();
            UISound.Tick();
            Changed?.Invoke(Index);
        }

        public void SetIndex(int i, bool notify)
        {
            Index = Mathf.Clamp(i, 0, Mathf.Max(0, Options.Length - 1));
            Refresh();
            if (notify) Changed?.Invoke(Index);
        }

        public void Refresh()
        {
            if (ValueText != null && Options.Length > 0) ValueText.text = Options[Index];
            if (Pips != null)
                for (int i = 0; i < Pips.Length; i++)
                    Pips[i].color = i == Index ? UITheme.Red : new Color(1f, 1f, 1f, 0.18f);
        }
    }

    /// <summary>Base class for full-screen menus with animated show/hide and back handling.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public CanvasGroup Group { get; private set; }
        public RectTransform Root { get; private set; }
        public bool Visible { get; private set; }
        protected GameObject FirstSelected;
        public Action BackRequested;

        protected void InitScreen()
        {
            Root = GetComponent<RectTransform>();
            Group = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            Visible = true;
            StopAllCoroutines();
            Group.interactable = true;
            Group.blocksRaycasts = true;
            StartCoroutine(UITween.Fade(Group, 1f, 0.28f));
            OnShown();
            if (FirstSelected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(FirstSelected);
            }
        }

        public virtual void Hide()
        {
            if (!gameObject.activeSelf) return;
            Visible = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            StopAllCoroutines();
            StartCoroutine(HideRoutine());
        }

        private System.Collections.IEnumerator HideRoutine()
        {
            yield return UITween.Fade(Group, 0f, 0.18f);
            gameObject.SetActive(false);
        }

        protected virtual void OnShown() { }

        protected virtual void Update()
        {
            if (!Visible || !Group.interactable) return;
            if (UIInput.BackPressed()) { UISound.Back(); BackRequested?.Invoke(); }
            // keep a selection for keyboard / gamepad users
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && FirstSelected != null && UIInput.AnyNavigation())
                EventSystem.current.SetSelectedGameObject(FirstSelected);
        }
    }

    /// <summary>UI-level input queries.</summary>
    public static class UIInput
    {
        public static bool BackPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) ||
                   (gp != null && gp.buttonEast.wasPressedThisFrame);
        }

        public static bool AnyNavigation()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) ||
                   (gp != null && (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame || gp.leftStick.ReadValue().sqrMagnitude > 0.4f));
        }
    }

    /// <summary>UI sound effects routed through a single 2D source.</summary>
    public static class UISound
    {
        private static AudioSource _src;

        private static AudioSource Src
        {
            get
            {
                if (_src == null && GameServices.Instance != null)
                {
                    _src = GameServices.Instance.gameObject.AddComponent<AudioSource>();
                    _src.spatialBlend = 0f;
                    _src.playOnAwake = false;
                    _src.ignoreListenerPause = true;
                }
                return _src;
            }
        }

        private static float Vol => GameServices.Instance != null ? GameServices.Instance.Settings.effectsVolume : 0.8f;
        private static float _lastTick;

        public static void Tick()
        {
            if (Time.unscaledTime - _lastTick < 0.04f || Src == null) return;
            _lastTick = Time.unscaledTime;
            Src.PlayOneShot(GameServices.Instance.AudioLib.UiTick(), 0.35f * Vol);
        }

        public static void Confirm() { if (Src != null) Src.PlayOneShot(GameServices.Instance.AudioLib.UiConfirm(), 0.6f * Vol); }
        public static void Back() { if (Src != null) Src.PlayOneShot(GameServices.Instance.AudioLib.UiBack(), 0.6f * Vol); }
    }
}
