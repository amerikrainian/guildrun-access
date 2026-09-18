using System;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.GameRun.RunSession;
using Ember.Scopes.GameRun.RunSession.Services;
using GuildrunAccess.Core;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.Dev
{
    /// <summary>
    /// Dev-only shortcuts through a run, for reaching a screen without playing to it (<c>POST /input</c>
    /// with <c>dev.floor:N</c> or <c>dev.shop</c>): the run session's floor-node index is writable and
    /// its service proceeds from wherever it is. <c>dev.floor:4</c> makes floor 4's crossroads the next
    /// thing shown (on the first act's map: 0 the starter kit, 1 and 3 random events, 2 the challenge
    /// combat, 4 the campfire), <c>dev.shop</c> opens the shop as a battle result would. The game
    /// keeps whatever state that skips (a fight not fought stays unfought), so it is for inspection only.
    /// </summary>
    internal static class RunJump
    {
        public static string Floor(string arg)
        {
            int floor;
            if (!int.TryParse(arg, out floor)) return "dev.floor needs a floor index (dev.floor:4)";
            try
            {
                var flow = GameScopes.Controller<BattleFlowUIStateController>();
                var reader = flow != null ? flow._runSessionReader : null;
                var service = Service();
                if (reader == null || service == null) return "not in a run";
                var data = reader.Data;
                var chunk = reader.CurrentChunk;
                int count = chunk != null && chunk.Floors != null ? chunk.Floors.Length : 0;
                if (floor < 0 || floor >= count) return "floor " + floor + " is outside this chunk (0-" + (count - 1) + ")";
                data.CurrentFloorNodeIndex = floor;
                service.ProceedToCrossroads();
                return "floor node " + floor + ": proceeding to its crossroads";
            }
            catch (Exception e)
            {
                CoreLog.Warning("dev.floor failed: " + e.Message);
                return "dev.floor failed: " + e.Message;
            }
        }

        public static string Shop()
        {
            try
            {
                var service = Service();
                if (service == null) return "not in a run";
                service.SwitchToShop();
                return "switching to the shop";
            }
            catch (Exception e)
            {
                CoreLog.Warning("dev.shop failed: " + e.Message);
                return "dev.shop failed: " + e.Message;
            }
        }

        /// <summary><c>dev.event:1029</c>, while an event is on show: make the event of that sequential
        /// id the active one and have the event controller show it, so any of the game's events can be
        /// read without playing to it. The crossroads is bypassed, so the template events that take
        /// their numbers from it (the stat bonuses, the hero rewards) show the game's own format
        /// errors; the authored ones (1001 to 1035) and the item and relic templates come up whole.
        /// OnStart subscribes Proceed a second time: leave through <c>dev.floor</c>, not Proceed.</summary>
        public static string Event(string arg)
        {
            int id;
            if (!int.TryParse(arg, out id)) return "dev.event needs an event id (dev.event:1029)";
            try
            {
                var ui = GameScopes.Controller<Ember.Scopes.Event.UI.EventUIController>();
                if (ui == null || !ui.gameObject.activeInHierarchy || ui._eventService == null) return "no event on show: enter one first (dev.floor:1, then a path)";
                var compendium = GameScopes.Controller<Ember.Scopes.Application.Compendium.CompendiumUIController>();
                var balancing = compendium != null && compendium._heroInfoAdapter != null ? compendium._heroInfoAdapter.Balancing : null;
                if (balancing == null) return "no balancing to read the events from";
                var all = balancing.GetAll<Ember.Balancing.Sheets.Events.IEventEntry>();
                int count = all.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<Ember.Balancing.Sheets.Events.IEventEntry>>().Count;
                var indexed = all.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<Ember.Balancing.Sheets.Events.IEventEntry>>();
                Ember.Balancing.Sheets.Events.IEventEntry entry = null;
                for (int i = 0; i < count && entry == null; i++)
                {
                    var e = indexed[i].TryCast<gg.leyline.balancing.Data.IBalancingEntry>();
                    if (e != null && e.Id.SequentialId == id) entry = indexed[i];
                }
                if (entry == null) return "no event " + id;

                var service = ui._eventService;
                service.ClearEvents();
                service.AddActiveEvent(entry, new Il2CppSystem.Nullable<int>());
                service.SetActiveEvent(entry);
                ui.StopAllCoroutines();
                ui.ClearChoiceButtons();
                // The outcome summaries of the event before it are clones in the choices holder.
                var holder = ui._eventChoicesHolder;
                for (int i = holder.childCount - 1; i >= 0; i--)
                {
                    var summary = holder.GetChild(i).GetComponent<Ember.Scopes.Event.UI.ChoiceButtons.ChoiceOutcomeSummaryView>();
                    if (summary != null && summary.Pointer != ui._outcomeSummaryView.Pointer) UnityEngine.Object.Destroy(summary.gameObject);
                }
                if (ui._eventOutcomeTypewriterEffect != null) ui._eventOutcomeTypewriterEffect.gameObject.SetActive(false);
                ui._readyToChoose = false;
                ui._isReadyToProceed = false;
                ui._isProceeding = false;
                ui.OnStart();
                var named = entry.TryCast<Ember.Balancing.Sheets.Events.EventEntry>();
                return "showing event " + id + (named != null ? " " + named.Title : "");
            }
            catch (Exception e)
            {
                CoreLog.Warning("dev.event failed: " + e.Message);
                return "dev.event failed: " + e.Message;
            }
        }

        // The run session service, registered by its interface.
        private static RunSessionService Service()
        {
            var service = Services.Resolve<IRunSessionService>();
            return service != null ? service.TryCast<RunSessionService>() : null;
        }
    }
}
