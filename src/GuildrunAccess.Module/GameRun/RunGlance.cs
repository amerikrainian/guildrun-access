using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.GameRun.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using TMPro;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The run's glance key: Ctrl+S speaks the shards, by the game's own tooltip title, from
    /// the same live text the info stop reads (the panel's second counter is the Emergency Rewind,
    /// not a currency, and stays the info stop's). Silent outside a run, where no info panel is up.</summary>
    internal static class RunGlance
    {
        public static void Shards()
        {
            try
            {
                var info = GameScopes.Controller<BasicInfoUIPanelController>();
                if (info == null || !info.gameObject.activeInHierarchy) return;
                var parts = new List<string>();
                Add(parts, TooltipReader.Title(info._currentGoldTooltip) ?? Strings.RunGold, info._currentGoldText);
                if (parts.Count > 0) Speech.Say(string.Join(", ", parts), interrupt: true);
            }
            catch (Exception e) { CoreLog.Warning("RunGlance: shards failed: " + e.Message); }
        }

        private static void Add(List<string> parts, string title, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            parts.Add(Strings.GlanceValue(title, text.text));
        }
    }
}
