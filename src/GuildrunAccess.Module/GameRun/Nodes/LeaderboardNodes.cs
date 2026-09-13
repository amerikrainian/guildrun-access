using System.Collections.Generic;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using TMPro;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The endless-mode leaderboard (<see cref="LeaderboardController"/>), which the game shows on the
    /// difficulty screen and on the run's final result panel: its Global / Friend List tabs, the
    /// streak and loading lines, every entry as "rank, name, floor", and the reset countdown (its tooltip a
    /// buffer line). Two Tab-stops of its own: the tabs and lines, then the entries, so a
    /// player can skip the list without arrowing through it.
    /// </summary>
    internal static class LeaderboardNodes
    {
        public static void Add(GraphBuilder b, LeaderboardController lb, string keyPrefix)
        {
            if (lb == null || !lb.gameObject.activeInHierarchy) return;
            b.BeginStop(keyPrefix + ":tabs");
            b.PushContext(Strings.ResultLeaderboard, null, positions: false);
            if (GameNodes.IsShown(lb._globalTab))
                b.AddItem(ControlId.Structural(keyPrefix + ":global"), GameNodes.Tab(lb._globalTab));
            if (GameNodes.IsShown(lb._friendsTab))
                b.AddItem(ControlId.Structural(keyPrefix + ":friends"), GameNodes.Tab(lb._friendsTab));
            AddLine(b, keyPrefix + ":streak", lb._currentStreakText);
            AddLine(b, keyPrefix + ":load", lb._loadText);

            b.BeginStop(keyPrefix + ":entries");
            var entries = lb._entries;
            if (entries != null)
            {
                b.PushContext(Strings.ResultEntries, Strings.RoleList);
                foreach (var entry in entries)
                {
                    if (entry == null || !entry.gameObject.activeInHierarchy) continue;
                    var e = entry;
                    b.AddItem(ControlId.Structural(keyPrefix + ":" + e.GetInstanceID()), GameNodes.Text(() => Strings.ResultEntry(
                        e.RankText != null ? e.RankText.text : "", e.NameText != null ? e.NameText.text : "", e.FloorText != null ? e.FloorText.text : "")));
                }
                b.PopContext();
            }
            var reset = lb._resetCountdownText;
            if (reset != null && reset.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(reset.text))
                b.AddItem(ControlId.Structural(keyPrefix + ":reset"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Strings.ResultReset + " " + reset.text) },
                    Details = () => GameNodes.Lines(TooltipReader.Describe(lb._resetTooltipRaycastTarget)),
                });
            b.PopContext();
        }

        private static void AddLine(GraphBuilder b, string key, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => text.text));
        }
    }
}
