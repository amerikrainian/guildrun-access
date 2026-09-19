using System;
using Ember.Scopes.GameRun.RunSession;
using Ember.Scopes.GameRun.RunSession.Data;
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
                var service = Service();
                var data = service != null ? service.Data : null;
                if (data == null) return "not in a run";
                if (!BattleSceneUp()) return NoBattleScene;
                var chunk = CurrentChunk(data);
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
        /// Leave through Proceed, as a player would.</summary>
        public static string Event(string arg)
        {
            int id;
            if (!int.TryParse(arg, out id)) return "dev.event needs an event id (dev.event:1029)";
            try
            {
                var ui = GameScopes.Controller<Ember.Scopes.Event.UI.EventUIController>();
                if (ui == null || !ui.gameObject.activeInHierarchy || ui._eventService == null) return "no event on show: enter one first (dev.floor:1, then a path)";
                var entry = Entry<Ember.Balancing.Sheets.Events.IEventEntry>(id);
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
                // OnStart adds its Proceed listener again: with two, one press advances the floor twice and
                // loads the battle scene twice, and the one left over keeps a crossroads controller whose
                // gates are gone, throwing every frame at the next crossroads (the game's error dialog
                // reopening as fast as it is closed).
                if (ui._proceedButton != null) ui._proceedButton.onClick.RemoveAllListeners();
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

        /// <summary><c>dev.crossroads:113</c>: the crossroads of that sequential id on the current floor
        /// node, entered as the game enters one (its own path set-up, its own way into the event), for a
        /// crossroads a run would only reach by luck.</summary>
        public static string Crossroads(string arg)
        {
            int id;
            if (!int.TryParse(arg, out id)) return "dev.crossroads needs a crossroads id (dev.crossroads:113)";
            try
            {
                var service = Service();
                var data = service != null ? service.Data : null;
                var chunk = data != null ? CurrentChunk(data) : null;
                if (chunk == null || chunk.Floors == null) return "not in a run";
                if (!BattleSceneUp()) return NoBattleScene;
                int node = data.CurrentFloorNodeIndex;
                if (node < 0 || node >= chunk.Floors.Length) return "floor node " + node + " is outside this chunk";
                var entry = Entry<Ember.Balancing.Sheets.Crossroads.ICrossroadsEntry>(id);
                if (entry == null) return "no crossroads " + id;
                chunk.Floors[node].Crossroads = entry;
                service.ProceedToCrossroads();
                return "crossroads " + id + " on floor node " + node + ": proceeding to it";
            }
            catch (Exception e)
            {
                CoreLog.Warning("dev.crossroads failed: " + e.Message);
                return "dev.crossroads failed: " + e.Message;
            }
        }

        // The crossroads is a panel of the battle scene, which an event unloads: under an event the flow
        // state would change with nothing to show it.
        private const string NoBattleScene = "an event is on show: proceed out of it first (the crossroads lives in the battle scene)";

        private static bool BattleSceneUp() => GameScopes.Controller<Ember.Scopes.Battle.UI.BattleFlow.BattleFlowUIStateController>() != null;

        private static ActChunkData CurrentChunk(RunSessionData data)
        {
            var chunks = data.ActChunks;
            int index = data.CurrentChunkIndex;
            return chunks != null && index >= 0 && index < chunks.Length ? chunks[index] : null;
        }

        // The balancing entry of a sheet by its sequential id, or null.
        private static T Entry<T>(int id) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            var compendium = GameScopes.Controller<Ember.Scopes.Application.Compendium.CompendiumUIController>();
            var balancing = compendium != null && compendium._heroInfoAdapter != null ? compendium._heroInfoAdapter.Balancing : null;
            if (balancing == null) { CoreLog.Warning("RunJump: no balancing to read entries from"); return null; }
            var all = balancing.GetAll<T>();
            int count = all.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
            var indexed = all.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<T>>();
            for (int i = 0; i < count; i++)
            {
                var e = indexed[i].TryCast<gg.leyline.balancing.Data.IBalancingEntry>();
                if (e != null && e.Id.SequentialId == id) return indexed[i];
            }
            return null;
        }

        // The run session service, registered by its interface.
        private static RunSessionService Service()
        {
            var service = Services.Resolve<IRunSessionService>();
            return service != null ? service.TryCast<RunSessionService>() : null;
        }
    }
}
