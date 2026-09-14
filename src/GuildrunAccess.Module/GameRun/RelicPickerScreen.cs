using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.RelicPicker;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The relic reward picker (<see cref="RelicPickerController"/>: "Choose your reward" after a
    /// challenge fight, and any pending relic choice the run controller shows): the panel's own title
    /// as the context, the relics on offer as buttons carrying name, rarity and effect (the full tooltip
    /// in the control buffer), then Skip Reward and Back when the panel shows them. Choosing goes through
    /// the choice view's own click handler. Escape presses Back when it is up; Skip Reward is never
    /// pressed for the player.
    /// </summary>
    public sealed class RelicPickerScreen : Screen
    {
        public override string Key => "gamerun.relic_picker";
        public override int Layer => 20;
        public override bool Exclusive => true;

        private static RelicPickerController Picker => GameScopes.Controller<RelicPickerController>();

        // The picker's panel while it is on show, else null.
        private static GameObject Panel()
        {
            var picker = Picker;
            var panel = picker != null ? picker._panelParent : null;
            return panel != null && panel.activeInHierarchy ? panel : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var picker = Picker;
            if (picker == null || Panel() == null) return;

            b.PushContext(Title(picker), null, positions: false);

            // The relics on offer: the choice views the controller spawned into its holder.
            b.BeginStop("choices");
            b.PushContext(Strings.PickerChoices, Strings.RoleList);
            int n = 0;
            foreach (var choice in Choices(picker))
            {
                n++;
                b.AddItem(ControlId.Structural("relicpicker:" + choice.GetInstanceID()), Choice(choice));
            }
            if (n == 0) b.AddItem(ControlId.Structural("relicpicker:none"), GameNodes.Text(() => Strings.PickerNoChoices));
            b.PopContext();

            var skip = picker._skipChallengeFightRewardButton;
            var back = picker._closeButton;
            if (GameNodes.IsShown(skip) || GameNodes.IsShown(back))
            {
                b.BeginStop("actions");
                if (GameNodes.IsShown(skip)) b.AddItem(ControlId.Structural("relicpicker:skip"), GameNodes.Button(skip));
                if (GameNodes.IsShown(back)) b.AddItem(ControlId.Structural("relicpicker:back"), GameNodes.Button(back));
            }

            b.PopContext();
        }

        private static List<RelicChoiceView> Choices(RelicPickerController picker)
        {
            var list = new List<RelicChoiceView>();
            var holder = picker._relicHolderTransform;
            if (holder == null) return list;
            for (int i = 0; i < holder.childCount; i++)
            {
                var choice = holder.GetChild(i).GetComponent<RelicChoiceView>();
                if (choice != null && choice.gameObject.activeInHierarchy) list.Add(choice);
            }
            return list;
        }

        // The panel's own title ("Choose your reward"): the holder's text that belongs to no choice.
        private static string Title(RelicPickerController picker)
        {
            var holder = picker._relicHolderTransform;
            if (holder != null)
            {
                for (int i = 0; i < holder.childCount; i++)
                {
                    var child = holder.GetChild(i);
                    if (child.GetComponent<RelicChoiceView>() != null) continue;
                    string text = Shown(child.GetComponent<TMP_Text>());
                    if (text != null) return text;
                }
            }
            return Strings.ScreenRelicPicker;
        }

        // A relic on offer: "Shard Constructor, Rare Relic, After Stall (30), gain 9 Shards.", Enter picks it.
        private static NodeVtable Choice(RelicChoiceView choice)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => Name(choice)),
                    new NodeAnnouncement(() => Shown(choice._subtitleText), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => Shown(choice._descriptionText)),
                },
                SearchText = () => Name(choice),
                OnActivate = () => choice.OnPointerClick(new PointerEventData(EventSystem.current)),
                Details = () => Details(choice),
            };
        }

        // The relic's name: the choice's title, else its relic view's caption or tooltip title.
        private static string Name(RelicChoiceView choice)
            => Shown(choice._titleText) ?? ItemNodes.RelicName(choice._relicView);

        private static string Shown(TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy) return null;
            string s = text.text;
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // The tooltip as lines (heading, effect, keyword definitions), from the choice's own target or
        // its relic view's, else the shown description as the one line.
        private static IEnumerable<string> Details(RelicChoiceView choice)
        {
            var lines = TooltipReader.Lines(choice._tooltipRaycastTarget);
            if (lines.Count == 0 && choice._relicView != null) lines = TooltipReader.Lines(choice._relicView._tooltipRaycastTarget);
            return lines.Count > 0 ? lines : GameNodes.Lines(Shown(choice._descriptionText));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape presses Back when the panel offers it.
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var picker = Picker;
                var back = picker != null ? picker._closeButton : null;
                if (GameNodes.IsShown(back) && back.interactable) back.onClick.Invoke();
            });
        }
    }
}
