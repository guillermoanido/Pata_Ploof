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
        const string PrefabName = "Choice Screen";

        [Header("Rig")]
        public TextMeshProUGUI title;

        [Tooltip("Hidden when a question has no blurb.")]
        public TextMeshProUGUI blurb;

        [Tooltip("An extra line under the blurb, for example how much Magical Scrap would be lost.")]
        public TextMeshProUGUI status;

        [Tooltip("One answer button, copied once per answer and placed where this one sits. It " +
                 "stays switched off itself, so restyle it and every answer follows.")]
        public Button choiceTemplate;

        void Awake()
        {
            if (choiceTemplate != null)
                choiceTemplate.gameObject.SetActive(false);
        }

        public static ChoiceScreen Open(string titleText, string blurbText)
        {
            var screen = Ui.OpenScreen<ChoiceScreen>(PrefabName);

            if (screen == null)
                return null;

            Game.SetPaused(true);
            Ui.ClaimScreen();
            screen.Show(titleText, blurbText);

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
            var button = Ui.CopyOf(choiceTemplate.gameObject).GetComponent<Button>();
            Ui.Retext(button, text);
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
            Ui.ReleaseScreen();
            Game.SetPaused(false);
            Destroy(gameObject);
        }

        public void CloseThen(Action next)
        {
            Ui.ReleaseScreen();
            Destroy(gameObject);
            next?.Invoke();
        }

        void Show(string titleText, string blurbText)
        {
            if (title != null)
                title.text = titleText;

            if (blurb != null)
            {
                blurb.text = blurbText;
                blurb.gameObject.SetActive(!string.IsNullOrEmpty(blurbText));
            }

            Status(string.Empty);
        }
    }
}
