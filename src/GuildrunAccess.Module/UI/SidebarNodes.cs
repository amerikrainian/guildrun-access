using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Battle.DamageTracker.Controller;
using Ember.Scopes.Battle.UI.Sidebar;
using Ember.Scopes.GameRun.UI.EnemyCard;
using Ember.Scopes.GameRun.UI.HeroCard;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Run;
using TMPro;
using UnityEngine.UI;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// The run's information sidebar (<see cref="InformationSidebarController"/>): whichever panel it
    /// shows: an inspected hero's card (the shared hero grid), an inspected enemy's card (name, health,
    /// mana, abilities, stats), the damage tracker (its mode tabs and every hero's live number), or the
    /// challenge panel's text. Declared inside the caller's current Tab-stop.
    /// </summary>
    internal static class SidebarNodes
    {
        public static void Add(GraphBuilder b, InformationSidebarController sidebar, string keyPrefix)
        {
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            b.PushContext(Strings.RunSidebar, null, positions: false);

            // The panel switches.
            AddToggle(b, keyPrefix + ":tracker", sidebar._damageTrackerToggle, Strings.RunDamageTracker);
            AddToggle(b, keyPrefix + ":challenge", sidebar._challengeModeToggle, Strings.RunChallenge);

            var hero = sidebar._heroCardView;
            if (hero != null && hero.gameObject.activeInHierarchy)
            {
                b.PushContext(Strings.RunInspect, null, positions: false);
                HeroCardNodes.AddGrid(b, keyPrefix + ":hero", new List<HeroCardView> { hero }, i => null, null,
                    HeroCardNodes.StatsRow, HeroCardNodes.AbilitiesRow, HeroCardNodes.ItemsRow);
                b.PopContext();
            }

            var enemy = sidebar._enemyCardView;
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                b.PushContext(Strings.RunInspect, null, positions: false);
                b.AddItem(ControlId.Structural(keyPrefix + ":enemy:name"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => EnemyLine(enemy)) },
                    SearchText = () => enemy._nameText != null ? enemy._nameText.text : null,
                    OnTooltip = () => GameNodes.SayTooltip(HeroCardNodes.AbilitiesTooltips(enemy)),
                });
                b.AddItem(ControlId.Structural(keyPrefix + ":enemy:abilities"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => Strings.HeroAbilities),
                        GameNodes.LabelPart(() => HeroCardNodes.AbilitiesLine(enemy)),
                    },
                    OnTooltip = () => GameNodes.SayTooltip(HeroCardNodes.AbilitiesTooltips(enemy)),
                });
                b.AddItem(ControlId.Structural(keyPrefix + ":enemy:stats"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => Strings.HeroStats),
                        GameNodes.LabelPart(() => Stats(enemy)),
                    },
                    OnTooltip = () => GameNodes.SayTooltip(StatTooltips(enemy)),
                });
                b.PopContext();
            }

            var tracker = sidebar._damageTrackerPanel;
            var trackerController = tracker != null ? tracker.GetComponent<DamageTrackerUIController>() : null;
            if (tracker != null && tracker.activeInHierarchy && trackerController != null)
                AddTracker(b, trackerController, keyPrefix + ":dmg");

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
            b.AddItem(ControlId.Structural(key), GameNodes.Tab(toggle, () => label));
        }

        // "Turtle, 200 health, mana 0 of 60".
        private static string EnemyLine(EnemyCardView enemy)
        {
            var sb = new StringBuilder();
            if (enemy._nameText != null) sb.Append(enemy._nameText.text);
            if (enemy._healthText != null && !string.IsNullOrWhiteSpace(enemy._healthText.text))
                sb.Append(", ").Append(Strings.HeroStat(Strings.HeroHealth, enemy._healthText.text));
            if (enemy._manaText != null && enemy._manaText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(enemy._manaText.text))
                sb.Append(", ").Append(Strings.HeroStat(Strings.HeroMana, enemy._manaText.text));
            return sb.ToString();
        }

        private static string Stats(EnemyCardView enemy)
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

        private static string StatTooltips(EnemyCardView enemy)
        {
            var sb = new StringBuilder();
            foreach (var stat in enemy.GetComponentsInChildren<StatView>(false))
            {
                var text = stat != null ? TooltipReader.Describe(stat._tooltipRaycastTarget) : null;
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // The damage tracker: the mode tabs, then one live line per hero. No context of its own: the
        // sidebar's "Damage tracker" switch already names it, and the panel's title is the selected
        // mode's name, which the mode tab already says.
        private static void AddTracker(GraphBuilder b, DamageTrackerUIController tracker, string keyPrefix)
        {
            var modes = tracker._modeToggles;
            if (modes != null)
                for (int i = 0; i < modes.Length; i++)
                {
                    int mode = i;
                    if (!GameNodes.IsShown(modes[i])) continue;
                    b.AddItem(ControlId.Structural(keyPrefix + ":mode:" + i), GameNodes.Tab(modes[i], () => Strings.ResultTrackerMode(mode)));
                }
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
                            // Live numbers change every tick of a fight: read on demand (Ctrl+Space).
                            new NodeAnnouncement(() => TrackerValue(v), kind: AnnouncementKinds.Value),
                        },
                        SearchText = () => TrackerName(v),
                    });
                }
                b.PopContext();
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
