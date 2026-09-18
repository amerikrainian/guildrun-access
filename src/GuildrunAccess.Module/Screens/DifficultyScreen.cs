using System;
using System.Collections.Generic;
using Ember.Balancing.Difficulty;
using Ember.Scopes.Application.Difficulty;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.GameRun;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The run-start screen (<see cref="DifficultyUIController"/>, "New Run"): the difficulty tiers as
    /// tabs (locked ones read as disabled), the selected tier's modifier description and score bonus,
    /// the Red Rift stats when that mode shows, the endless leaderboard, then Start Game and Back.
    /// Escape presses Back.
    /// </summary>
    public sealed class DifficultyScreen : Screen
    {
        public DifficultyScreen()
        {
            // Tab past the actions comes round to the tiers, as on the settings and the run HUD.
            Wrap = true;
        }

        public override string Key => "app.difficulty";
        public override int Layer => 5;
        public override bool Exclusive => true;

        private static DifficultyUIController Difficulty => GameScopes.Controller<DifficultyUIController>();

        private DifficultyUIController Panel()
        {
            var panel = Difficulty;
            if (panel == null || !panel.gameObject.activeInHierarchy) return null;
            var root = panel._difficultyUIRoot;
            return root == null || root.activeInHierarchy ? panel : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            if (panel == null) return;

            b.PushContext(Title(panel), null, positions: false);

            // The tiers.
            b.BeginStop("difficulties");
            b.PushContext(Strings.DifficultyLevels, null, positions: true);
            var options = panel._difficulties;
            if (options != null)
                for (int i = 0; i < options.Length; i++)
                {
                    var option = options[i];
                    if (option == null || !option.gameObject.activeInHierarchy) continue;
                    var toggle = option._toggle != null ? option._toggle : option.GetComponent<Toggle>();
                    if (toggle == null) continue;
                    var o = option;
                    int index = i;
                    b.AddItem(ControlId.Structural("difficulty:tier:" + i), Tier(toggle, o, index));
                }
            b.PopContext();

            // What the selected tier means: the modifier description(s), the score bonus, Red Rift stats.
            b.BeginStop("modifiers");
            b.PushContext(Strings.DifficultyModifiers, null, positions: false);
            int n = 0;
            var modifiers = panel._difficultyModifiersRoot;
            if (modifiers != null)
                foreach (var tmp in modifiers.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    var t = tmp;
                    b.AddItem(ControlId.Structural("difficulty:modifier:" + n++), GameNodes.Text(() => t.text));
                }
            var bonusHolder = panel._difficultyBonusTextElementContainer;
            var bonus = panel._difficultyBonusText != null ? panel._difficultyBonusText.GetComponent<TMP_Text>() : null;
            if (bonusHolder != null && bonusHolder.activeInHierarchy && bonus != null && !string.IsNullOrWhiteSpace(bonus.text))
                b.AddItem(ControlId.Structural("difficulty:bonus"), GameNodes.Text(() => bonus.text));
            var rift = panel._riftRunParent;
            if (rift != null && rift.activeInHierarchy)
            {
                AddText(b, "difficulty:rift:streak", panel._redRiftRunStreak, Strings.DifficultyStreak);
                AddText(b, "difficulty:rift:highest", panel._highestRedRiftStreak, null);
                AddText(b, "difficulty:rift:wins", panel._totalRedRiftWins, null);
            }
            b.PopContext();

            // The leaderboard, shared with the result panel.
            var lb = panel.GetComponentInChildren<LeaderboardController>(false);
            if (lb != null && lb.gameObject.activeInHierarchy)
            {
                    LeaderboardNodes.Add(b, lb, "difficulty:lb");
            }

            b.BeginStop("actions");
            if (GameNodes.IsShown(panel._playUsualButton))
                b.AddItem(ControlId.Structural("difficulty:play"), GameNodes.Button(panel._playUsualButton));
            if (GameNodes.IsShown(panel._playRiftButton))
                b.AddItem(ControlId.Structural("difficulty:playrift"), GameNodes.Button(panel._playRiftButton));
            var back = BackButton(panel);
            if (back != null)
                b.AddItem(ControlId.Structural("difficulty:back"), GameNodes.Button(back));

            b.PopContext();
        }

        // A tier: its caption, radio-button role, selected state, and "disabled" while locked. Landing
        // on an unlocked tier selects it, as Enter does. The control buffer reads what an UNLOCKED tier means from the
        // game's difficulty data (the screen itself describes only the selected one); a locked tier's
        // description is something the game keeps hidden, so it stays hidden here too.
        private static NodeVtable Tier(Toggle toggle, DifficultyOptionItemView option, int index)
        {
            Action select = () => { if (toggle.interactable && !option._isLocked && !toggle.isOn) toggle.isOn = true; };
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => Caption(option)),
                    GameNodes.SelectedPart(() => toggle.isOn),
                    GameNodes.DisabledPart(() => toggle.interactable && !option._isLocked),
                },
                SearchText = () => Caption(option),
                OnActivate = select,
                OnFocus = select,
                Details = () => option._isLocked ? null : GameNodes.Lines(TierInfo(index)),
            };
        }

        // The tier's own name and description from the balancing ("Difficulty: C. Heroes take 10% Max
        // HP damage at the start of each combat."): the configs run parallel to the controller's tier
        // items (Base, then C to SSS); The Red Rift has no config and stays silent.
        private static string TierInfo(int index)
        {
            var panel = Difficulty;
            var singleton = panel != null && panel._difficultiesSingleton != null
                ? panel._difficultiesSingleton.TryCast<DifficultiesSingleton>() : null;
            var configs = singleton != null ? singleton._difficulties : null;
            if (configs == null || index < 0 || index >= configs.Length || configs[index] == null) return null;
            var config = configs[index];
            var sb = new System.Text.StringBuilder();
            foreach (var text in new[] { Localized(config.DisplayName), Localized(config.DisplayDescription) })
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text.Trim());
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static string Localized(UnityEngine.Localization.LocalizedString text)
        {
            if (text == null) return null;
            try { return text.GetLocalizedString(); }
            catch (Exception e)
            {
                CoreLog.Warning("Difficulty: localized string unreadable: " + e.Message);
                return null;
            }
        }

        // The tier's name label ("Base", "LETHAL", "THE RED RIFT"), with the rank its icon shows when it
        // has one: the six lethal tiers all carry the label "LETHAL" and tell apart, to the eye, only by
        // their rank icon (Difficulty_C ... Difficulty_SSS), so the rank is the distinguishing word.
        private static string Caption(DifficultyOptionItemView option)
        {
            var tmp = option.GetComponentInChildren<TMP_Text>(true);
            string name = tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : option.gameObject.name;
            string rank = Rank(option);
            return rank != null ? Strings.DifficultyTierRank(name, rank) : name;
        }

        private static readonly string[] Ranks = { "C", "B", "A", "S", "SS", "SSS" };
        private const string RankSpritePrefix = "Difficulty_";

        private static string Rank(DifficultyOptionItemView option)
        {
            foreach (var image in option.GetComponentsInChildren<Image>(true))
            {
                var sprite = image != null ? image.sprite : null;
                string spriteName = sprite != null ? sprite.name : null;
                if (spriteName == null || !spriteName.StartsWith(RankSpritePrefix, StringComparison.Ordinal)) continue;
                string suffix = spriteName.Substring(RankSpritePrefix.Length);
                if (Array.IndexOf(Ranks, suffix) >= 0) return suffix;
            }
            return null;
        }

        private static string Title(DifficultyUIController panel)
        {
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "Title" && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text;
            return Strings.ScreenDifficulty;
        }

        private static Button BackButton(DifficultyUIController panel)
        {
            foreach (var button in panel.GetComponentsInChildren<Button>(false))
                if (button != null && button.gameObject.name == "Back" && GameNodes.IsShown(button)) return button;
            return null;
        }

        private static void AddText(GraphBuilder b, string key, TMP_Text text, string label)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => label == null ? text.text : label + " " + text.text));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var panel = Panel();
                var back = panel != null ? BackButton(panel) : null;
                if (back != null && back.interactable) back.onClick.Invoke();
            });
        }
    }
}
