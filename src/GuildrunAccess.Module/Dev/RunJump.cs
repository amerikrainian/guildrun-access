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

        // The run session service, registered by its interface.
        private static RunSessionService Service()
        {
            var service = Services.Resolve<IRunSessionService>();
            return service != null ? service.TryCast<RunSessionService>() : null;
        }
    }
}
