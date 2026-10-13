using System;
using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "party" stop: the active slots, then the reserve slots, each reading its hero's
    /// name, abilities and items; Enter opens the hero's menu. While placing, Shift+Q E A D Z C on
    /// the slot of a hero who stands on the board moves that hero one cell, as the same keys do on
    /// its cell, focus staying on the slot: the glance keys read a hero from here, and so the
    /// moves work from here. Bare letters stay the type-ahead search's; it stands down only while
    /// Shift is held on such a slot (<see cref="OwnsShiftLetters"/>).</summary>
    internal sealed class PartySection : ScreenSection
    {
        // The control buffer: the full stats line (the card the landing showed, else the bar vitals),
        // the abilities and items tooltips, then the card's tag and stat tooltips.
        private static IEnumerable<string> SlotDetails(BottomHeroView view)
        {
            var lines = new List<string>();
            var card = HeroActions.ShownHeroCard(RunData.HeroName(view));
            string stats = card != null ? HeroCardNodes.StatsLine(card) : BoardSection.VitalsOf(view);
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
            lines.AddRange(RunLabels.SlotTooltips(view));
            if (card != null) { lines.AddRange(HeroCardNodes.TagsTooltips(card)); lines.AddRange(HeroCardNodes.StatsTooltips(card)); }
            return lines;
        }

        // The hero buffer: the card rows when shown, else name, vitals, abilities.
        private static IEnumerable<string> SlotRows(BottomHeroView view)
        {
            var card = HeroActions.ShownHeroCard(RunData.HeroName(view));
            return card != null ? HeroLines.ForCard(card) : HeroLines.ForSlot(view, BoardSection.VitalsOf(view));
        }

        private readonly HeroActions _actions;

        public PartySection(HeroActions actions) { _actions = actions; }

        private const string PartyPrefix = "run:party:";

        // The focused party slot's hero's cell, while placing; false off the party slots, for an
        // empty slot, and outside placement (the reserve's heroes stand on no cell).
        private static bool TryFocusedBoardHero(out UnityEngine.Vector2Int cell)
        {
            cell = default;
            var node = Navigation.FocusedNode;
            var key = node != null ? node.Id.StructuralKey as string : null;
            if (key == null || !key.StartsWith(PartyPrefix, StringComparison.Ordinal) || !RunData.Placing()) return false;
            var view = node.Vtable != null && node.Vtable.Subject != null ? node.Vtable.Subject() as BottomHeroView : null;
            return RunData.TryHeroId(view, out var hero) && RunData.TryCellOf(hero, out cell);
        }

        /// <summary>Whether Shift+a letter is the party strip's key right now, not a typed letter: Shift
        /// is down on the slot of a hero who can be moved.</summary>
        internal static bool OwnsShiftLetters() => NavInput.Current.ShiftHeld && TryFocusedBoardHero(out _);

        public override IEnumerable<ElementAction> GetActions()
        {
            if (!TryFocusedBoardHero(out var cell)) yield break;
            yield return new ElementAction(BoardSection.MoveUpLeft, Strings.Get("bind.run.move.upleft"), _ => BoardSection.Nudge(cell, HexDir.UpLeft, focusFollows: false));
            yield return new ElementAction(BoardSection.MoveUpRight, Strings.Get("bind.run.move.upright"), _ => BoardSection.Nudge(cell, HexDir.UpRight, focusFollows: false));
            yield return new ElementAction(BoardSection.MoveLeft, Strings.Get("bind.run.move.left"), _ => BoardSection.Nudge(cell, HexDir.Left, focusFollows: false));
            yield return new ElementAction(BoardSection.MoveRight, Strings.Get("bind.run.move.right"), _ => BoardSection.Nudge(cell, HexDir.Right, focusFollows: false));
            yield return new ElementAction(BoardSection.MoveDownLeft, Strings.Get("bind.run.move.downleft"), _ => BoardSection.Nudge(cell, HexDir.DownLeft, focusFollows: false));
            yield return new ElementAction(BoardSection.MoveDownRight, Strings.Get("bind.run.move.downright"), _ => BoardSection.Nudge(cell, HexDir.DownRight, focusFollows: false));
        }

        public override void Build(GraphBuilder b)
        {
            var party = RunData.Party;
            if (party == null) return;
            b.BeginStop("party");
            AddHeroPanel(b, party._activeHeroPanel, Strings.RunParty, "party", false);
            AddHeroPanel(b, party._reserveHeroPanel, Strings.RunReserve, "reserve", true);
        }

        private void AddHeroPanel(GraphBuilder b, BottomHeroPanelView panel, string label, string key, bool reserve)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var views = panel.HeroViews;
            if (views == null) return;
            // No positions: every slot names its own number, and a hero's row has no count of its own.
            b.PushContext(label, Strings.RoleList, positions: false);
            int n = 0;
            foreach (var v in views)
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                n++;
                int index = n;
                var view = v;
                Func<string> slotLabel = () => reserve ? Strings.RunReserveSlot(index) : Strings.RunPartySlot(index);
                string id = "run:" + key + ":" + view.GetInstanceID();
                if (view.IsEmpty)
                {
                    b.StartRow(key);
                    b.AddItem(ControlId.Structural(id), new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            new NodeAnnouncement(slotLabel, kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(() => Strings.RunSlotEmpty, kind: AnnouncementKinds.Value),
                        },
                    });
                    b.EndRow();
                    continue;
                }
                // A hero is a row: the hero, then its item slots ("slot 1, empty"), Up/Down keeping the
                // column between heroes. The row's context is "party slot 1, Nyx", spoken on entering
                // the row on one of its slots; on the hero itself the hero's own label says it.
                Func<string> heroLabel = () => slotLabel() + ", " + (RunData.HeroLabel(view) ?? Strings.RunParty);
                b.PushContext(heroLabel(), positions: false);
                b.StartRow(key);
                b.AddItem(ControlId.Structural(id), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(heroLabel, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => RunLabels.SlotSummary(view, name: false), live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => RunLabels.SlotSummary(view),
                    OnActivate = () => _actions.OpenHeroMenu(view, reserve),
                    // Backspace opens the hero's compendium page, as on its card.
                    OnSecondary = () => { if (RunData.TryHeroId(view, out var hero)) HeroActions.OpenCompendium(hero); },
                    // Landing shows the hero's card in the sidebar, as hovering the slot does; the line
                    // and the buffers read it.
                    OnFocus = () => Peek(view),
                    Details = () => SlotDetails(view),
                    Subject = () => view,
                    SideLines = HeroLines.SideOfSlots(() => SlotRows(view), () => view._itemSlotViews, () => view._abilitiesView),
                });
                if (view._itemSlotViews != null)
                {
                    int number = 0;
                    foreach (var s in view._itemSlotViews)
                    {
                        if (s == null || !s.gameObject.activeInHierarchy) continue;
                        number++;
                        int slotNumber = number;
                        var slot = s;
                        b.AddItem(ControlId.Structural(id + ":slot:" + slotNumber), ItemSlot(view, slot, slotNumber));
                    }
                }
                b.EndRow();
                b.PopContext();
            }
            b.PopContext();
        }

        // "slot 2, Hammer": one of a hero's item slots, by position (leftmost first, as the game counts
        // them); its item's tooltip and quests are its buffers, the hero's the others, the glance
        // keys read the hero. Enter on an item is that item's part of the hero's menu.
        private NodeVtable ItemSlot(BottomHeroView view, Ember.Scopes.GameRun.UI.Slots.PlaceholderSlotView slot, int number)
        {
            Func<IEnumerable<Ember.Scopes.GameRun.UI.Slots.PlaceholderSlotView>> slots
                = () => ItemNodes.HasItem(slot) ? new[] { slot } : (IEnumerable<Ember.Scopes.GameRun.UI.Slots.PlaceholderSlotView>)view._itemSlotViews;
            // The quests are the item's own, an empty slot's the hero's.
            var side = HeroLines.SideOfSlots(() => SlotRows(view), slots, () => ItemNodes.HasItem(slot) ? null : view._abilitiesView);
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => Strings.RunItemSlot(number), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => ItemNodes.ItemName(slot) ?? Strings.RunSlotEmpty, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = () => ItemNodes.ItemName(slot),
                OnActivate = ItemNodes.HasItem(slot) ? () => _actions.OpenItemMenu(view, slot) : (Action)null,
                OnFocus = () => Peek(view),
                Details = () => ItemNodes.HasItem(slot) ? TooltipReader.Lines(slot._tooltipRaycastTarget) : new List<string>(),
                Subject = () => view,
                // The item buffer is the hero's items wherever in the row focus is.
                SideLines = buffer => buffer == GuildrunAccess.Core.Buffers.BufferKeys.Item
                    ? ItemNodes.ItemTooltips(view._itemSlotViews) : side(buffer),
            };
        }

        private static void Peek(BottomHeroView view)
        {
            if (RunData.TryHeroId(view, out var id)) HeroActions.PeekHero(id);
        }
    }
}
