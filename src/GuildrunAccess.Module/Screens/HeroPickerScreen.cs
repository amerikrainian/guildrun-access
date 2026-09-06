using System;
using System.Collections.Generic;
using System.Text;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.HeroCard.Elements;
using Ember.Scopes.GameRun.UI.HeroPicker;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The starting-hero choice at the top of a run (<see cref="HeroPickerController"/>): the offered
    /// heroes as a GRID, heroes across (left/right) and their details down (up/down, column preserved):
    /// name and class, stats, abilities, relic. Enter on any cell of a column recruits that hero (the
    /// card's own selection button), Space reads the cell's full tooltip text through the game's tooltip
    /// pipeline, and the reroll button (when offered) is the last stop.
    /// </summary>
    public sealed class HeroPickerScreen : Screen
    {
        public override string Key => "gamerun.heropicker";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private HeroPickerController _controller;
        private const int SearchEvery = 30;
        private int _lastSearchFrame = -SearchEvery;

        private HeroPickerController Controller()
        {
            if (_controller != null) return _controller;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<HeroPickerController>());
            _controller = found != null ? found.TryCast<HeroPickerController>() : null;
            return _controller;
        }

        // The offered cards, in on-screen order: the active choice views under the holder.
        private static List<InitialHeroChoiceView> Choices(HeroPickerController c)
        {
            var list = new List<InitialHeroChoiceView>();
            var holder = c._heroChoiceHolder;
            if (holder == null) return list;
            foreach (var v in holder.GetComponentsInChildren<InitialHeroChoiceView>(false))
                if (v != null && v.gameObject.activeInHierarchy) list.Add(v);
            return list;
        }

        public override bool IsActive()
        {
            var c = Controller();
            if (c == null) return false;
            var panel = c._panelParent;
            return panel != null && panel.activeInHierarchy && Choices(c).Count > 0;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;
            var choices = Choices(c);
            if (choices.Count == 0) return;

            b.PushContext(PickerTitle(c) ?? Strings.ScreenChooseHero, Strings.RoleList, positions: false);
            b.BeginStop("heroes");

            // Row 1: name + class; the column identity for the reader.
            b.StartRow("heroes");
            for (int i = 0; i < choices.Count; i++)
            {
                var v = choices[i];
                b.AddItem(ControlId.Structural("hero" + i + ":name"), Cell(v,
                    () => NameAndClass(v), ControlTypes.Button, () => HeroTooltip(v)));
            }
            b.EndRow();

            // Row 2: health, mana and the visible stats.
            b.StartRow("heroes");
            for (int i = 0; i < choices.Count; i++)
            {
                var v = choices[i];
                b.AddItem(ControlId.Structural("hero" + i + ":stats"), Cell(v,
                    () => StatsLine(v), ControlTypes.Text, () => StatsTooltips(v), Strings.HeroStats));
            }
            b.EndRow();

            // Row 3: abilities (their tooltip titles); Space reads the descriptions.
            b.StartRow("heroes");
            for (int i = 0; i < choices.Count; i++)
            {
                var v = choices[i];
                b.AddItem(ControlId.Structural("hero" + i + ":abilities"), Cell(v,
                    () => AbilitiesLine(v), ControlTypes.Text, () => AbilitiesTooltips(v), Strings.HeroAbilities));
            }
            b.EndRow();

            // Row 4: the bundled relic, when the offer includes one.
            bool anyRelic = false;
            foreach (var v in choices) if (HasRelic(v)) { anyRelic = true; break; }
            if (anyRelic)
            {
                b.StartRow("heroes");
                for (int i = 0; i < choices.Count; i++)
                {
                    var v = choices[i];
                    b.AddItem(ControlId.Structural("hero" + i + ":relic"), Cell(v,
                        () => RelicLine(v), ControlTypes.Text, () => RelicTooltip(v), Strings.HeroRelic));
                }
                b.EndRow();
            }

            // The reroll offer, when the game shows one.
            var reroll = c._reRollPanelView;
            if (reroll != null && GameNodes.IsShown(reroll._reRollButton))
            {
                b.BeginStop("actions");
                b.AddItem(ControlId.Structural("heropicker:reroll"),
                    GameNodes.Button(reroll._reRollButton, () => Strings.HeroReroll));
            }

            b.PopContext();
        }

        // A grid cell: "prefix, text"; Enter recruits the column's hero; Space speaks the tooltip text.
        private static NodeVtable Cell(InitialHeroChoiceView v, Func<string> text, ControlType type,
            Func<string> tooltip, string rowCaption = null)
        {
            // A captioned cell is untyped so its parts speak in declaration order: caption, then value
            // ("stats, health 875, ..."); the name cell keeps its button role.
            var parts = new List<NodeAnnouncement>();
            if (rowCaption != null) parts.Add(new NodeAnnouncement(() => rowCaption));
            parts.Add(GameNodes.LabelPart(text));
            return new NodeVtable
            {
                ControlType = rowCaption != null ? null : type,
                Announcements = parts,
                SearchText = () => NameAndClass(v),
                OnActivate = () => Select(v),
                OnTooltip = () =>
                {
                    string t = tooltip != null ? tooltip() : null;
                    Core.Speech.Say(string.IsNullOrWhiteSpace(t) ? Strings.NoTooltip : t, interrupt: true);
                },
            };
        }

        private static void Select(InitialHeroChoiceView v)
        {
            var buttons = v._selectionButtons;
            if (buttons == null) return;
            foreach (var button in buttons)
                if (GameNodes.IsShown(button)) { button.onClick.Invoke(); return; }
        }

        private static string PickerTitle(HeroPickerController c)
        {
            var panel = c._panelParent;
            if (panel == null) return null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "TitleText" && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text;
            return null;
        }

        // ---- card readouts, read live ----

        private static HeroCardView Card(InitialHeroChoiceView v) => v != null ? v._heroCardView : null;

        private static string NameAndClass(InitialHeroChoiceView v)
        {
            var card = Card(v);
            if (card == null) return null;
            var sb = new StringBuilder();
            if (card._nameText != null) sb.Append(card._nameText.text);
            foreach (var tag in card.GetComponentsInChildren<HeroTagView>(false))
            {
                if (tag == null || tag._name == null || string.IsNullOrWhiteSpace(tag._name.text)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(tag._name.text);
            }
            return sb.ToString();
        }

        private static string StatsLine(InitialHeroChoiceView v)
        {
            var card = Card(v);
            if (card == null) return null;
            var sb = new StringBuilder();
            if (card._healthText != null && !string.IsNullOrWhiteSpace(card._healthText.text))
                sb.Append(Strings.HeroStat(Strings.HeroHealth, card._healthText.text));
            if (card._manaText != null && !string.IsNullOrWhiteSpace(card._manaText.text))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroStat(Strings.HeroMana, card._manaText.text));
            }
            // Active panels only: the card keeps inactive template panels with placeholder values.
            foreach (var stat in card.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null || stat._statText == null || string.IsNullOrWhiteSpace(stat._statText.text)) continue;
                string name = TooltipReader.Title(stat._tooltipRaycastTarget) ?? StatNameFromObject(stat.gameObject.name);
                if (string.IsNullOrEmpty(name)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroStat(name, stat._statText.text));
            }
            return sb.ToString();
        }

        // "SingleStatPanel_AtkSpd" -> "AtkSpd" (a last-resort caption when the stat has no tooltip title).
        private static string StatNameFromObject(string objectName)
        {
            int us = objectName.LastIndexOf('_');
            return us >= 0 && us < objectName.Length - 1 ? objectName.Substring(us + 1) : null;
        }

        private static List<HeroCardAbilityView> Abilities(InitialHeroChoiceView v)
        {
            var list = new List<HeroCardAbilityView>();
            var card = Card(v);
            if (card == null) return list;
            foreach (var a in card.GetComponentsInChildren<HeroCardAbilityView>(false))
                if (a != null && a._tooltipRaycastTarget != null && a._tooltipRaycastTarget.TooltipSource != null && a._tooltipRaycastTarget.IsActive)
                    list.Add(a);
            return list;
        }

        private static string AbilitiesLine(InitialHeroChoiceView v)
        {
            var sb = new StringBuilder();
            foreach (var a in Abilities(v))
            {
                var head = TooltipReader.Heading(a._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(head)) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(head);
            }
            return sb.Length > 0 ? sb.ToString() : Strings.HeroNoAbilities;
        }

        private static string AbilitiesTooltips(InitialHeroChoiceView v)
        {
            var sb = new StringBuilder();
            foreach (var a in Abilities(v))
            {
                var text = TooltipReader.Describe(a._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.ToString();
        }

        // The relic panel is hidden when the offer bundles no relic (the tutorial run); its views then
        // still carry the prefab's placeholder text, so the panel's visibility gates the row.
        private static bool HasRelic(InitialHeroChoiceView v)
            => v._relicPanel != null && v._relicPanel.gameObject.activeInHierarchy;

        private static string RelicLine(InitialHeroChoiceView v)
        {
            if (!HasRelic(v)) return Strings.HeroNoRelic;
            var relic = v._relicView;
            string name = relic != null && relic._nameText != null ? relic._nameText.text : null;
            if (string.IsNullOrWhiteSpace(name) && relic != null)
                name = TooltipReader.Title(relic._tooltipRaycastTarget);
            string desc = v._relicDescriptionText != null ? v._relicDescriptionText.text : null;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc)) return Strings.HeroNoRelic;
            return string.IsNullOrWhiteSpace(desc) ? name : name + ", " + desc;
        }

        private static string RelicTooltip(InitialHeroChoiceView v)
        {
            if (!HasRelic(v)) return null;
            var relic = v._relicView;
            return relic != null ? TooltipReader.Describe(relic._tooltipRaycastTarget) : null;
        }

        private static string StatsTooltips(InitialHeroChoiceView v)
        {
            var card = Card(v);
            if (card == null) return null;
            var sb = new StringBuilder();
            foreach (var stat in card.GetComponentsInChildren<StatView>(true))
            {
                if (stat == null) continue;
                var text = TooltipReader.Describe(stat._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.ToString();
        }

        private static string HeroTooltip(InitialHeroChoiceView v)
        {
            // The card as a whole has no single tooltip; abilities are the defining detail.
            return AbilitiesTooltips(v);
        }
    }
}
