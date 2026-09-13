using System;
using System.Collections.Generic;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.GameRun.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using TMPro;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "info" stop: gold, shards, difficulty and the battle timer, each a live value with
    /// the game's tooltip as its buffer line.</summary>
    internal sealed class InfoSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            b.BeginStop("info");
            b.PushContext(Strings.RunInfo, Strings.RoleList);
            var info = GameScopes.Controller<BasicInfoUIPanelController>();
            if (info != null && info.gameObject.activeInHierarchy)
            {
                AddValue(b, "gold", () => TooltipReader.Title(info._currentGoldTooltip) ?? Strings.RunGold,
                    () => info._currentGoldText != null ? info._currentGoldText.text : null, info._currentGoldTooltip);
                AddValue(b, "shards", () => TooltipReader.Title(info._stabilizerTooltip) ?? Strings.RunShards,
                    () => info._accumulatedShardsText != null ? info._accumulatedShardsText.text : null, info._stabilizerTooltip);
                var difficulty = info._difficultyPanel;
                if (difficulty != null && difficulty.activeInHierarchy)
                    AddValue(b, "difficulty", () => TooltipReader.Title(info._difficultyTooltip) ?? Strings.RunDifficulty,
                        () => { var tmp = difficulty.GetComponentInChildren<TMP_Text>(false); return tmp != null ? tmp.text : null; },
                        info._difficultyTooltip);
            }
            var timer = GameScopes.Controller<BattleTimerController>();
            if (timer != null && timer._timerText != null && timer._timerText.gameObject.activeInHierarchy)
                AddValue(b, "timer", () => Strings.RunTimer, () => timer._timerText.text, null);
            b.PopContext();
        }

        private static void AddValue(GraphBuilder b, string key, Func<string> label, Func<string> value, TooltipRaycastTarget tooltip)
        {
            b.AddItem(ControlId.Structural("run:info:" + key), new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                Details = tooltip == null ? (Func<IEnumerable<string>>)null
                    : () => TooltipReader.Lines(tooltip),
            });
        }
    }
}
