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
    /// main menu, on the difficulty screen and on the run's final result panel, read the same way
    /// everywhere as ONE Tab-stop, top to bottom: the visibility toggle (the eye), the Global / Friend
    /// List tabs as a row (Left and Right between them, as they lie), the streak and loading lines,
    /// every entry as "rank, name, floor", and the reset countdown (its tooltip a buffer line). Tab
    /// skips the whole board; Down from the tabs is the first entry.
    /// </summary>
    internal static class LeaderboardNodes
    {
        public static void Add(GraphBuilder b, LeaderboardController lb, string keyPrefix)
        {
            if (lb == null || !lb.gameObject.activeInHierarchy) return;
            b.BeginStop(keyPrefix);
            b.PushContext(Title(lb), null, positions: false);
            // The eye: shows or hides the board (its own view; the toggle inside it is the widget).
            var eye = lb._visibilityToggle != null ? lb._visibilityToggle._visibilityToggle : null;
            if (GameNodes.IsShown(eye))
                b.AddItem(ControlId.Structural(keyPrefix + ":visible"), GameNodes.Toggle(eye, () => Strings.LeaderboardVisible));
            if (GameNodes.IsShown(lb._globalTab) || GameNodes.IsShown(lb._friendsTab))
            {
                b.StartRow();
                if (GameNodes.IsShown(lb._globalTab))
                    b.AddItem(ControlId.Structural(keyPrefix + ":global"), GameNodes.Tab(lb._globalTab));
                if (GameNodes.IsShown(lb._friendsTab))
                    b.AddItem(ControlId.Structural(keyPrefix + ":friends"), GameNodes.Tab(lb._friendsTab));
                b.EndRow();
            }
            AddLine(b, keyPrefix + ":streak", lb._currentStreakText);
            // "Loading...", while a tab's list is fetched (the friends' takes a moment). A player who
            // steps onto it and waits lands on what it loaded, the first entry, when it goes: the
            // nearest survivor would be the tab that started the load.
            var load = lb._loadText;
            if (load != null && load.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(load.text))
            {
                var line = GameNodes.Text(() => load.text);
                line.VanishTo = () => FirstEntryId(lb, keyPrefix);
                b.AddItem(ControlId.Structural(keyPrefix + ":load"), line);
            }

            var entries = lb._entries;
            if (entries != null)
            {
                b.PushContext(Strings.ResultEntries, Strings.RoleList);
                foreach (var entry in entries)
                {
                    if (entry == null || !entry.gameObject.activeInHierarchy) continue;
                    var e = entry;
                    b.AddItem(EntryId(keyPrefix, e), GameNodes.Text(() => Strings.ResultEntry(
                        e.RankText != null ? e.RankText.text : "", e.NameText != null ? e.NameText.text : "", e.FloorText != null ? e.FloorText.text : "")));
                }
                b.PopContext();
            }
            var reset = lb._resetCountdownText;
            if (reset != null && reset.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(reset.text))
                b.AddItem(ControlId.Structural(keyPrefix + ":reset"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Strings.ResultReset + " " + reset.text) },
                    Details = () => TooltipReader.Lines(lb._resetTooltipRaycastTarget),
                });
            b.PopContext();
        }

        private static ControlId EntryId(string keyPrefix, LeaderboardEntryView entry)
            => ControlId.Structural(keyPrefix + ":" + entry.GetInstanceID());

        // The first entry on show, read when asked (the entries a load brings are not there before it).
        private static ControlId FirstEntryId(LeaderboardController lb, string keyPrefix)
        {
            var entries = lb != null ? lb._entries : null;
            if (entries == null) return null;
            foreach (var entry in entries)
                if (entry != null && entry.gameObject.activeInHierarchy) return EntryId(keyPrefix, entry);
            return null;
        }

        // The board's shown title ("Endless Mode Leaderboard", or the challenge one), else our word.
        private static string Title(LeaderboardController lb)
        {
            foreach (var titles in new[] { lb._regularTitles, lb._challengeModeTitles })
            {
                if (titles == null) continue;
                foreach (var go in titles)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    var tmp = go.GetComponentInChildren<TMP_Text>(false);
                    if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text.Trim();
                }
            }
            return Strings.ResultLeaderboard;
        }

        private static void AddLine(GraphBuilder b, string key, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => text.text));
        }
    }
}
