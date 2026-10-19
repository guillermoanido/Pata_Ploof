using System;
using FallingWizard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ChoiceScreen : MonoBehaviour
    {
        const int DefaultSortingOrder = 200;

        const float BothSides = 2f;

        const float PanelWidth = 760f;
        const float PanelPadding = 40f;
        const float PanelSpacing = 16f;
        const float InnerWidth = PanelWidth - PanelPadding * BothSides;

        const float ButtonWidth = 640f;
        const float ButtonHeight = 68f;
        const float ButtonFontSize = 28f;

        const float TitleFontSize = 56f;
        const float TitleHeight = 70f;

        const float BlurbFontSize = 26f;
        const float BlurbHeight = 72f;

        const float StatusFontSize = 28f;
        const float StatusHeight = 40f;

        RectTransform column;
        TextMeshProUGUI status;

        public static ChoiceScreen Open(string title, string blurb,
            int sortingOrder = DefaultSortingOrder)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas(title, sortingOrder);
            var screen = canvas.gameObject.AddComponent<ChoiceScreen>();
            screen.Build(title, blurb);

            return screen;
        }

        public ChoiceScreen Status(string text)
        {
            if (status != null)
                status.text = text;

            return this;
        }

        public ChoiceScreen Choice(string text, Action onChosen, bool isEnabled = true)
        {
            Button button = Ui.CreateButton(text, column, ButtonWidth, ButtonHeight,
                ButtonFontSize);
            button.interactable = isEnabled;

            if (isEnabled && onChosen != null)
                button.onClick.AddListener(() => onChosen());

            if (isEnabled && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);

            return this;
        }

        public void Close()
        {
            Screens.Release();
            Game.SetPaused(false);
            Destroy(gameObject);
        }

        public void CloseThen(Action next)
        {
            Screens.Release();
            Destroy(gameObject);
            next?.Invoke();
        }

        void Build(string title, string blurb)
        {
            Ui.CreateShroud(transform);

            column = Ui.CreateSheet("Panel", transform, Ui.Panel, PanelWidth,
                PanelPadding, PanelSpacing);

            Ui.CreateLabel(title, column, TitleFontSize, InnerWidth, TitleHeight);

            if (!string.IsNullOrEmpty(blurb))
                Ui.CreateLabel(blurb, column, BlurbFontSize, InnerWidth, BlurbHeight).color =
                    Ui.FadedInk;

            status = Ui.CreateLabel(string.Empty, column, StatusFontSize, InnerWidth, StatusHeight);
            status.color = Ui.Accent;
        }
    }
}
