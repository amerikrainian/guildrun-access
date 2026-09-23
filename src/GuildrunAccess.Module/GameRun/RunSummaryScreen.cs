using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.EndScreen;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The end screen's Summary overlay (its Summary button: <see cref="EndScreenController.ShowSummaryOverlay"/>
    /// turns on <see cref="EndlessShareView"/>, a hero panel in its summary form): the final team's cards
    /// as the shared hero grid, the reserve behind its toggle when the panel shows one, the endless
    /// floor line when filled, the endless leaderboard, then Close. Sits above the end screen, which
    /// stays behind it. Escape presses Close.
    /// </summary>
    public sealed class RunSummaryScreen : Screen
    {
        public RunSummaryScreen()
        {
            Wrap = true;
        }

        public override string Key => "gamerun.summary";
        public override int Layer => 25;
        public override bool Exclusive => true;

        private static EndlessShareView View()
        {
            var end = GameScopes.Controller<EndScreenController>();
            var view = end != null && end.gameObject.activeInHierarchy ? end._endlessShareView : null;
            return view != null && view.gameObject.activeInHierarchy ? view : null;
        }

        public override bool IsActive() => View() != null;

        public override void Build(GraphBuilder b)
        {
            var view = View();
            if (view == null) return;
            b.PushContext(Title(view), null, positions: false);

            // The endless floor, when the panel fills it in (the demo's runs are not endless).
            var floor = view._floorText;
            if (floor != null && floor.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(floor.text))
            {
                b.BeginStop("floor");
                b.AddItem(ControlId.Structural("summary:floor"), GameNodes.Text(() => floor.text));
            }

            // The team: the cards on the board, and the reserve's when the panel shows those instead.
            var cards = Cards(view._heroesOnBoardViews);
            if (cards.Count == 0) cards = Cards(view._heroesInReserveViews);
            if (cards.Count > 0)
            {
                b.BeginStop("heroes");
                b.PushContext(Strings.EndHeroes, null, positions: true);
                HeroCardNodes.AddGrid(b, "summary:hero", cards, i => null, i => null);
                b.PopContext();
            }

            var leaderboard = view.GetComponentInChildren<LeaderboardController>(false);
            if (leaderboard != null && leaderboard.gameObject.activeInHierarchy)
                LeaderboardNodes.Add(b, leaderboard, "summary:lb");

            b.BeginStop("actions");
            if (GameNodes.IsShown(view._toggleReserveButton))
                b.AddItem(ControlId.Structural("summary:reserve"), GameNodes.Button(view._toggleReserveButton));
            var close = CloseButton(view);
            if (close != null)
                b.AddItem(ControlId.Structural("summary:close"), GameNodes.Button(close));
            b.PopContext();
        }

        private static List<HeroCardView> Cards(HeroCardView[] views)
        {
            var list = new List<HeroCardView>();
            if (views == null) return list;
            foreach (var card in views)
                if (card != null && card.gameObject.activeInHierarchy) list.Add(card);
            return list;
        }

        // The banner's title text ("Summary"), else the mod's word.
        private static string Title(EndlessShareView view)
        {
            foreach (var tmp in view.GetComponentsInChildren<TMP_Text>(false))
            {
                var parent = tmp.transform.parent;
                if (tmp.gameObject.name == "Title" && parent != null && parent.name == "GenericTopBanner" && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text.Trim();
            }
            return Strings.EndSummary;
        }

        // The banner's Close button (the game's ExitButton, a TargetActivator that turns the panel off).
        private static Button CloseButton(EndlessShareView view)
        {
            foreach (var button in view.GetComponentsInChildren<Button>(false))
                if (button.gameObject.name == "ExitButton" && GameNodes.IsShown(button)) return button;
            return null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var view = View();
                var close = view != null ? CloseButton(view) : null;
                if (close != null && close.interactable) close.onClick.Invoke();
            });
        }
    }
}
