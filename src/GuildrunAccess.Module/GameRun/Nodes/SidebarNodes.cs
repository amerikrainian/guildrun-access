using System;
using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Battle.DamageTracker.Controller;
using Ember.Scopes.Battle.UI.Sidebar;
using Ember.Scopes.Battle.UI.Tracking.Data;
using Ember.Scopes.Battle.UI.Tracking.Views;
using GuildrunAccess.Core;
using Ember.Scopes.GameRun.UI.EnemyCard;
using Ember.Scopes.GameRun.UI.HeroCard;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using TMPro;
using UnityEngine.UI;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run's information sidebar (<see cref="InformationSidebarController"/>) as a strip and a
    /// page. The strip, in the caller's current Tab-stop, holds the panel switches and, while the
    /// damage tracker is the shown panel, its mode tabs (damage dealt, damage taken, healing done). A
    /// panel switch (Damage tracker, Challenge) is a free-standing game Toggle, no group: on shows its
    /// panel, off hides it, and the game turns it off itself when an inspected card takes the sidebar;
    /// so it reads as a toggle, "Damage tracker, toggle, on", flipped by Enter and never by landing on
    /// it (that would discard a card the player was reading). A mode tab selects on landing like any
    /// tab: it only changes which number the tracker shows. The page is the NEXT stop: an inspected
    /// hero's card (the shared hero grid), an inspected enemy's card (name, health, mana, abilities,
    /// stats), the damage tracker's per-hero live numbers, or the challenge panel's text. Two stops so
    /// that arrows inside the page never reach the strip.
    /// </summary>
    internal static class SidebarNodes
    {
        /// <summary>The first node of an inspected hero's card (its name row), where focus lands after
        /// an inspect; <paramref name="keyPrefix"/> as passed to <see cref="Add"/>.</summary>
        public static ControlId HeroCardId(string keyPrefix) => ControlId.Structural(keyPrefix + ":hero:0:name");
        public static ControlId EnemyCardId(string keyPrefix) => ControlId.Structural(keyPrefix + ":enemy:name");

        public static void Add(GraphBuilder b, InformationSidebarController sidebar, string keyPrefix)
        {
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            b.PushContext(Strings.RunSidebar, null, positions: false);

            // The strip: the panel switches, then the tracker's mode tabs while it is the shown panel.
            AddToggle(b, keyPrefix + ":tracker", sidebar._damageTrackerToggle, Strings.RunDamageTracker);
            AddToggle(b, keyPrefix + ":challenge", sidebar._challengeModeToggle, Strings.RunChallenge);
            var tracker = sidebar._damageTrackerPanel;
            var trackerController = tracker != null && tracker.activeInHierarchy ? tracker.GetComponent<DamageTrackerUIController>() : null;
            if (trackerController != null) AddTrackerModes(b, trackerController, keyPrefix + ":dmg");

            // The page: its own stop (empty, hence absent, when no panel is shown).
            b.BeginStop(keyPrefix + ":panel");

            var hero = sidebar._heroCardView;
            if (hero != null && hero.gameObject.activeInHierarchy)
            {
                b.PushContext(Strings.RunInspect, null, positions: false);
                HeroCardNodes.AddGrid(b, keyPrefix + ":hero", new List<HeroCardView> { hero }, i => null, null);
                b.PopContext();
            }

            var enemy = sidebar._enemyCardView;
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                b.PushContext(Strings.RunInspect, null, positions: false);
                // One control, as a hero card is: its line; the buffers hold the rest (the hero buffer
                // name, stats, abilities; the control buffer every ability and stat tooltip).
                b.AddItem(ControlId.Structural(keyPrefix + ":enemy:name"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => EnemyLine(enemy)),
                        new NodeAnnouncement(() => StatsBrief(enemy), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => EnemyName(enemy),
                    Details = () => EnemyDetails(enemy),
                    SideLines = HeroLines.Side(() => EnemyRows(enemy), null),
                });
                b.PopContext();
            }

            if (trackerController != null) AddTrackerHeroes(b, trackerController, keyPrefix + ":dmg");

            var challenge = sidebar._challengeModePanel;
            if (challenge != null && challenge.activeInHierarchy)
            {
                b.PushContext(Strings.RunChallenge, null, positions: false);
                int n = 0;
                foreach (var tmp in challenge.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    var t = tmp;
                    b.AddItem(ControlId.Structural(keyPrefix + ":challenge:" + n++), GameNodes.Text(() => t.text));
                }
                b.PopContext();
            }

            b.PopContext();
        }

        private static void AddToggle(GraphBuilder b, string key, Toggle toggle, string label)
        {
            if (!GameNodes.IsShown(toggle)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Toggle(toggle, () => label));
        }

        // "Turtle, 200 health, mana 0 of 60".
        /// <summary>The enemy card as the hero buffer's rows: name with health and mana, the stats, the
        /// abilities line.</summary>
        internal static IEnumerable<string> EnemyRows(EnemyCardView enemy)
            => GameNodes.Lines(EnemyLine(enemy), Stats(enemy), HeroCardNodes.AbilitiesLine(enemy));

        /// <summary>The enemy card as control-buffer lines: the full stats line, every ability, then every
        /// stat tooltip.</summary>
        internal static List<string> EnemyDetails(EnemyCardView enemy)
        {
            var lines = new List<string>();
            string stats = Stats(enemy);
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
            lines.AddRange(HeroCardNodes.AbilitiesTooltips(enemy));
            lines.AddRange(StatTooltips(enemy));
            return lines;
        }

        /// <summary>The enemy card's non-zero stats, for a focus line.</summary>
        internal static string StatsBrief(EnemyCardView enemy) => HeroCardNodes.StatsBrief(enemy, null, null);

        /// <summary>The card's name text; an enemy the game draws nameless is named through the registry
        /// (<see cref="RunData.EnemyName"/>).</summary>
        internal static string EnemyName(EnemyCardView enemy)
        {
            string name = enemy._nameText != null ? enemy._nameText.text : null;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return Interop.Nullables.TryGet(() => enemy.EnemyId, out Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId id)
                ? RunData.EnemyName(id) : null;
        }

        internal static string EnemyLine(EnemyCardView enemy)
        {
            var sb = new StringBuilder();
            sb.Append(EnemyName(enemy));
            if (enemy._healthText != null && !string.IsNullOrWhiteSpace(enemy._healthText.text))
                sb.Append(", ").Append(Strings.HeroStat(Strings.HeroHealth, enemy._healthText.text));
            if (enemy._manaText != null && enemy._manaText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(enemy._manaText.text))
                sb.Append(", ").Append(Strings.HeroStat(Strings.HeroMana, enemy._manaText.text));
            return sb.ToString();
        }

        internal static string Stats(EnemyCardView enemy)
        {
            var sb = new StringBuilder();
            foreach (var stat in enemy.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null || stat._statText == null || string.IsNullOrWhiteSpace(stat._statText.text)) continue;
                string name = TooltipReader.Title(stat._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(name)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroStat(name, stat._statText.text));
            }
            return sb.Length > 0 ? sb.ToString() : Strings.EndNoHighlights;
        }

        internal static List<string> StatTooltips(EnemyCardView enemy)
        {
            var lines = new List<string>();
            lines.AddRange(TooltipReader.Lines(enemy._healthTooltip));
            lines.AddRange(TooltipReader.Lines(enemy._manaTooltip));
            foreach (var stat in enemy.GetComponentsInChildren<StatView>(false))
            {
                if (stat != null) lines.AddRange(TooltipReader.Lines(stat._tooltipRaycastTarget));
            }
            return lines;
        }

        // The damage tracker's mode tabs (in the strip). The panel's title is the selected mode's name,
        // which the tab already says.
        private static void AddTrackerModes(GraphBuilder b, DamageTrackerUIController tracker, string keyPrefix)
        {
            var modes = tracker._modeToggles;
            if (modes == null) return;
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                if (!GameNodes.IsShown(modes[i])) continue;
                b.AddItem(ControlId.Structural(keyPrefix + ":mode:" + i), GameNodes.Tab(modes[i], () => Strings.ResultTrackerMode(mode)));
            }
        }

        // The damage tracker's page: one live line per hero.
        private static void AddTrackerHeroes(GraphBuilder b, DamageTrackerUIController tracker, string keyPrefix)
        {
            var heroes = tracker._heroTrackerViews;
            if (heroes != null)
            {
                b.PushContext(Strings.ResultTracker, Strings.RoleList);
                for (int i = 0; i < heroes.Length; i++)
                {
                    var view = heroes[i];
                    if (view == null) continue;
                    var content = view.Content;
                    if (content == null || !content.activeInHierarchy) continue;
                    // Unused template rows keep placeholder numbers and no hero behind the portrait.
                    var portrait = view.Portrait;
                    if (portrait == null || portrait._characterEntry == null) continue;
                    var v = view;
                    b.AddItem(ControlId.Structural(keyPrefix + ":hero:" + i), new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GameNodes.LabelPart(() => TrackerName(v)),
                            // Live numbers change every tick of a fight: not re-read on their own.
                            new NodeAnnouncement(() => TrackerValue(v), kind: AnnouncementKinds.Value),
                        },
                        SearchText = () => TrackerName(v),
                        // The row's hover tooltip: the shown mode's total broken down by source.
                        Details = () => GameNodes.Lines(TrackerTooltip(tracker._tooltipView, tracker._currentMode, v.Tracker)),
                    });
                }
                b.PopContext();
            }
        }

        /// <summary>
        /// What a tracker row shows on hover: the game's own breakdown of the row's total for
        /// <paramref name="mode"/> by source (an ability, a relic's contribution indented under it),
        /// "Irini: Basic attack 120, Limitless 80". Composed by the tracker's tooltip view from the
        /// row's data, as the hover does, read, and cleared again without showing it. Null until a
        /// fight has produced data for the row (the game's hover shows nothing then either).
        /// </summary>
        public static string TrackerTooltip(TrackerTooltipView tip, TrackerMode mode, BaseTrackerView row)
        {
            if (tip == null || row == null) return null;
            try
            {
                var data = row.TrackerData;
                if (data == null) return null;
                tip.UpdateTooltip(mode, data);

                // The used entries in the tooltip's own (hierarchy) order; the pool keeps the free ones
                // parented too, so membership in the used lists is one filter, and the entry being
                // active the other: the used lists also hold the prefab's template rows ("TotalTotal...
                // 9999999", "Defensive Energy Crystal") and rows left over from another mode, all of
                // them inactive, which the result screen's tooltip never tidies.
                var used = new HashSet<IntPtr>();
                var plain = tip._usedEntryViews;
                if (plain != null) for (int i = 0; i < plain.Count; i++) if (plain[i] != null) used.Add(plain[i].Pointer);
                var indented = tip._usedIndentedEntryViews;
                if (indented != null) for (int i = 0; i < indented.Count; i++) if (indented[i] != null) used.Add(indented[i].Pointer);

                var sb = new StringBuilder();
                foreach (var entry in tip.GetComponentsInChildren<TrackerTooltipEntryView>(true))
                {
                    if (entry == null || !used.Contains(entry.Pointer) || !entry.gameObject.activeSelf) continue;
                    string title = entry.TitleText != null ? entry.TitleText.text : null;
                    string value = entry.ValueText != null ? entry.ValueText.text : null;
                    bool hasTitle = !string.IsNullOrWhiteSpace(title), hasValue = !string.IsNullOrWhiteSpace(value);
                    if (!hasTitle && !hasValue) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    if (hasTitle) sb.Append(title.Trim());
                    if (hasTitle && hasValue) sb.Append(' ');
                    if (hasValue) sb.Append(value.Trim());
                }
                string heading = tip._heroNameText != null ? tip._heroNameText.text : null;
                tip.HideTooltip();
                if (sb.Length == 0) return null;
                return string.IsNullOrWhiteSpace(heading) ? sb.ToString() : heading.Trim() + ": " + sb;
            }
            catch (Exception e)
            {
                CoreLog.Warning("TrackerTooltip: " + e.Message);
                return null;
            }
        }

        private static string TrackerName(DamageTrackerUIController.HeroView view)
        {
            var portrait = view.Portrait;
            string name = portrait != null ? RunData.EntryName(portrait._characterEntry) : null;
            return string.IsNullOrEmpty(name) ? Strings.RunParty : name;
        }

        private static string TrackerValue(DamageTrackerUIController.HeroView view)
        {
            var tracker = view.Tracker;
            var text = tracker != null ? tracker._damageDealtText : null;
            return text != null ? text.text : null;
        }
    }
}
