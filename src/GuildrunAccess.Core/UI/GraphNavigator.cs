using System;
using System.Collections.Generic;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Input;
using GuildrunAccess.Core.Text;

namespace GuildrunAccess.Core.UI
{
    /// <summary>
    /// The graph-based navigator: runs every screen on the key-graph core (<see cref="KeyGraph"/>) with
    /// pull-based diffing: the graph is rebuilt per operation and per frame, focus is reconciled by
    /// identity, and a focus change is announced exactly once no matter what caused it (input, a screen
    /// moving focus, a content rebuild, or the game yanking a view). Screens declare their graph fresh
    /// from live game state on every render (immediate mode).
    /// </summary>
    public sealed class GraphNavigator : Navigator
    {
        // One GraphState per LIVE screen (focus cursor, per-stop memory, tree expansion): a screen
        // covered by another keeps its state and restores exactly where you were when focus returns; a
        // POPPED screen's state is dropped (ScreenClosed), so reopening starts fresh.
        private readonly Dictionary<Screens.Screen, GraphState> _states =
            new Dictionary<Screens.Screen, GraphState>();
        private GraphState _state = new GraphState();
        private KeyGraph _graph;

        // The differ's memory: the node identity (and its render node, for context diffing) last spoken.
        private ControlId _lastSpokenKey;
        private GraphNode _lastSpokenNode;

        // A focus request whose target is not in the render yet (lazy content): applied by EnsureFocus.
        private ControlId _pendingFocus;
        private bool _pendingAnnounce;

        public GraphNavigator()
        {
            _search.OnNoMatch = text => Speak(Strings.Strings.SearchNoMatch(text), interrupt: true);
        }

        /// <summary>Focus = a focused NODE.</summary>
        public override bool HasFocus => _graph?.CurrentNode != null;

        public override void Attach(Screens.Screen screen)
        {
            bool same = ReferenceEquals(screen, Screen);
            Screen = screen;
            ClearSearch(announce: false);
            if (!same)
            {
                // Swap to this screen's own state (creating it on first attach). The differ memory
                // resets so the (possibly restored) landing announces itself on return.
                if (screen != null)
                {
                    if (!_states.TryGetValue(screen, out _state))
                    {
                        _state = new GraphState();
                        _states[screen] = _state;
                    }
                }
                else
                {
                    _state = new GraphState();
                }
                _lastSpokenKey = null;
                _lastSpokenNode = null;
                _pendingFocus = null;
                _pendingStop = null;
                _liveKey = null;
            }
            _graph = screen != null ? new KeyGraph(() => BuildRender(screen), _state) : null;
        }

        public override void ScreenClosed(Screens.Screen screen)
        {
            if (screen != null) _states.Remove(screen);
        }

        public override void FocusNode(ControlId id, bool announce = true)
        {
            if (id == null) return;
            _pendingFocus = id;
            _pendingAnnounce = announce;
        }

        // Idle-rebuild throttle (see EnsureFocus): full immediate-mode rebuilds only every Nth frame.
        private int _lastIdleRender = int.MinValue / 2; // far past; halved so the subtraction never overflows
        private const int IdleRenderEvery = 6; // ~100ms at 60fps

        // A pending land-on-stop request (applied by EnsureFocus once the stop has nodes): resolves to
        // the stop's FIRST node at apply time, so it works when the caller cannot know the node keys.
        private object _pendingStop;

        public override void FocusStop(object stopKey)
        {
            _pendingStop = stopKey;
        }

        public override object FocusedStopKey => _graph?.CurrentNode?.StopKey;

        /// <summary>The live render + focused node id (dev inspection).</summary>
        public GraphRender CurrentRender => _graph?.Current;
        public ControlId FocusedNodeId => _graph?.CurrentNode?.Id;
        public GraphNode FocusedNode => _graph?.CurrentNode;

        // Screens declare fresh from live game state on every render (immediate mode).
        private GraphRender BuildRender(Screens.Screen screen)
        {
            var b = new GraphBuilder(_state.Expanded); // groups consult the persistent expansion set
            screen.Build(b);
            return b.Build();
        }

        public override void Blur()
        {
            _state.CurKey = null;
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _pendingFocus = null;
            _liveKey = null;
        }

        /// <summary>The per-frame pull: rebuild + reconcile, establish initial focus when content appears,
        /// apply pending focus requests, and announce any focus-identity change exactly once.</summary>
        public override void EnsureFocus()
        {
            if (Screen == null || _graph == null) return;

            // Throttle the IDLE rebuild: every input path rerenders on its own before acting, so this
            // per-frame pull only needs to catch content appearing, external focus drift, and live-state
            // flips, none of which need 60Hz. On throttled frames still watch the focused node's live
            // parts against the cached render (closures read live game state, so value flips are caught
            // at full rate; only structure lags ~100ms).
            bool mustRender = _state.CurKey == null || _pendingFocus != null || _pendingStop != null;
            if (!mustRender && NavInput.Current.FrameCount - _lastIdleRender < IdleRenderEvery)
            {
                var cached = _graph.CurrentNode;
                if (cached != null) WatchLive(cached);
                return;
            }
            _lastIdleRender = NavInput.Current.FrameCount;

            if (_state.CurKey == null && _pendingFocus == null)
            {
                // Unfocused screens stay unfocused until Tab seats a cursor.
                if (Screen.StartUnfocused) return;
                if (!_graph.Rerender()) return; // no content yet: Reconcile will seat the start node once there is
                // Declared initial landing: seat the stop's landing node (remembered, selected member,
                // first), BEFORE the differ announces below.
                var stop = Screen.InitialFocusStop;
                if (stop != null)
                {
                    var land = KeyGraph.StopLanding(_graph.Current, _graph.State, stop);
                    if (land != null) _graph.Focus(land.Id);
                }
            }
            else
            {
                if (!_graph.Rerender()) return; // nothing focusable this frame: retry
                if (_pendingFocus != null)
                {
                    // One retry frame for a target focused mid-build; a target that still is not in the
                    // render was removed, so drop the request rather than re-seating every frame.
                    if (_graph.Current.Nodes.ContainsKey(_pendingFocus))
                    {
                        _graph.Focus(_pendingFocus);
                        if (!_pendingAnnounce) { _lastSpokenKey = _pendingFocus; _lastSpokenNode = _graph.CurrentNode; }
                    }
                    _pendingFocus = null;
                }
                if (_pendingStop != null)
                {
                    var land = KeyGraph.StopLanding(_graph.Current, _graph.State, _pendingStop);
                    if (land != null) _graph.Focus(land.Id);
                    _pendingStop = null; // announce rides the normal differ below
                }
            }

            var node = _graph.CurrentNode;
            if (node == null) return;

            if (_lastSpokenKey == null || !_lastSpokenKey.Equals(node.Id))
            {
                // Queued (not interrupting): landings follow the screen name / preceding feedback.
                if (Navigation.FocusActive()) Speak(ComposeMove(_lastSpokenNode, node, entry: _lastSpokenNode == null));
                _lastSpokenKey = node.Id;
                _lastSpokenNode = node;
            }

            WatchLive(node);
        }

        // ---- live announcements: watch the FOCUSED node's Live parts and speak a part when its value
        // changes (an async toggle settling, the game flipping a state). Baselines silently whenever
        // focus lands on a new identity (the focus announcement already spoke the initial state).
        private ControlId _liveKey;
        private readonly List<string> _liveValues = new List<string>();

        private void WatchLive(GraphNode node)
        {
            var anns = GraphAnnouncer.EffectiveAnnouncements(node); // type-merged + settings-filtered
            if (anns.Count == 0) return;
            bool baseline = _liveKey == null || !_liveKey.Equals(node.Id) || _liveValues.Count != anns.Count;
            if (baseline) { _liveKey = node.Id; _liveValues.Clear(); }

            for (int i = 0; i < anns.Count; i++)
            {
                if (anns[i] == null || !anns[i].Live)
                {
                    if (baseline) _liveValues.Add(null);
                    continue;
                }
                string v = null;
                try { v = anns[i].Text?.Invoke(); } catch { }
                if (baseline) { _liveValues.Add(v); continue; }
                if (!string.Equals(_liveValues[i], v))
                {
                    _liveValues[i] = v;
                    if (!string.IsNullOrEmpty(v) && Navigation.FocusActive()) Speak(v, interrupt: false);
                }
            }
        }

        public override void AnnounceCurrent()
        {
            if (_graph == null) return;
            // An unfocused screen with nothing focused has nothing to announce, and Rerender would
            // auto-seat a phantom focus at the start node.
            if (_state.CurKey == null && Screen != null && Screen.StartUnfocused) return;
            if (!_graph.Rerender()) return;
            var node = _graph.CurrentNode;
            if (node == null) return;
            Speak(ComposeMove(null, node, entry: true));
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
        }

        // ---- input ----

        public override bool OnInputJustPressed(InputAction action)
        {
            if (_search.IsSearchActive)
            {
                if (_searchFocusId != null && !_searchFocusId.Equals(_graph?.CurrentNode?.Id))
                    ClearSearch(announce: false); // focus moved under us: results are stale
                else if (action.Key == UiActions.Home && _search.ResultCount > 0) { _search.JumpToFirstResult(); return true; }
                else if (action.Key == UiActions.End && _search.ResultCount > 0) { _search.JumpToLastResult(); return true; }
                else if (FiredFromSearchKey(action)) return true; // reserved key: TickTypeahead owns it
                else ClearSearch(announce: false);
            }

            switch (action.Key)
            {
                case UiActions.Up: return Arrow(NavDirection.Up);
                case UiActions.Down: return Arrow(NavDirection.Down);
                case UiActions.Left: return Arrow(NavDirection.Left);
                case UiActions.Right: return Arrow(NavDirection.Right);
                case UiActions.Next: return Tab(1);
                case UiActions.Prev: return Tab(-1);
                case UiActions.Home: return JumpEdge(first: true);
                case UiActions.End: return JumpEdge(first: false);
                // Region jumps consume only when the focused node is IN a region; elsewhere Ctrl+arrows bubble.
                case UiActions.RegionPrev: return _graph?.CurrentNode?.RegionKey != null && RegionJump(-1);
                case UiActions.RegionNext: return _graph?.CurrentNode?.RegionKey != null && RegionJump(1);
                case UiActions.Activate:
                {
                    if (_graph?.CurrentNode == null) return false;
                    VtableActivate();
                    return true;
                }
                case UiActions.Secondary:
                {
                    var node = _graph?.CurrentNode;
                    if (node == null) return false;
                    if (node.Vtable.OnSecondary != null) _graph.Secondary();
                    return true;
                }
                case UiActions.Back:
                    return Screen != null && Screen.InvokeAction(ActionIds.Back);
                case UiActions.Tooltip:
                {
                    var node = _graph?.CurrentNode;
                    if (node == null) return false;
                    if (node.Vtable.OnTooltip != null) { _graph.Tooltip(); return true; }
                    Speak(Strings.Strings.NoTooltip);
                    return true;
                }
                case UiActions.Drag:
                {
                    var node = _graph?.CurrentNode;
                    if (node == null) return false;
                    if (node.Vtable.OnDrag != null) { _graph.Drag(); return true; }
                    Speak(Strings.Strings.NoDragTarget);
                    return true;
                }
                case UiActions.Delete:
                {
                    var node = _graph?.CurrentNode;
                    if (node == null) return false;
                    if (node.Vtable.OnDelete != null) { _graph.Delete(); return true; }
                    Speak(Strings.Strings.NoDeleteTarget);
                    return true;
                }
                case UiActions.ReadFocus:
                {
                    if (_graph?.CurrentNode == null) return false;
                    AnnounceCurrent();
                    return true;
                }
                default:
                    return false;
            }
        }

        private static GraphDir ToDir(NavDirection dir)
        {
            switch (dir)
            {
                case NavDirection.Up: return GraphDir.Up;
                case NavDirection.Down: return GraphDir.Down;
                case NavDirection.Left: return GraphDir.Left;
                default: return GraphDir.Right;
            }
        }

        private bool Arrow(NavDirection dir)
        {
            var focusNode = _graph?.CurrentNode;
            if (focusNode == null) return false;

            // A focused slider/dropdown adjusts on Left/Right (priority over any navigation).
            if (dir == NavDirection.Left || dir == NavDirection.Right)
            {
                if (VtableAdjust(dir == NavDirection.Right ? 1 : -1)) return true;
            }

            // Edge-wired movement first (rows/grids/flattened tree rows all ride edges).
            var move = _graph.Move(ToDir(dir));
            if (move.Moved) { AnnounceMove(move); return true; }

            // At an edge. Left/Right get tree semantics: expand/collapse a group, descend into an
            // expanded one, ascend from a child.
            if (dir == NavDirection.Left || dir == NavDirection.Right)
            {
                var tr = dir == NavDirection.Right ? _graph.TreeRight() : _graph.TreeLeft();
                switch (tr.Kind)
                {
                    case KeyGraph.TreeMove.Expanded:
                    case KeyGraph.TreeMove.Collapsed:
                        SpeakFocusedState();
                        return true;
                    case KeyGraph.TreeMove.EmptyGroup:
                        Speak(Strings.Strings.NoDetails, interrupt: true);
                        return true;
                    case KeyGraph.TreeMove.Descended:
                    case KeyGraph.TreeMove.Ascended:
                        AnnounceMove(tr.Move);
                        return true;
                    case KeyGraph.TreeMove.Leaf:
                        return true; // inside a tree; nothing that way: consume
                }
            }

            // Nothing moved: consume edges inside trees; bubble from plain lists so an unfocused
            // screen's arrows fall through to the game-layer handlers.
            return KeyGraph.InTree(focusNode);
        }

        // Speak the focused group's post-toggle state (its full readout includes expanded/collapsed) and
        // rebaseline the differ+live watch so the toggle is not re-announced.
        private void SpeakFocusedState()
        {
            var node = _graph.CurrentNode;
            if (node == null) return;
            Speak(GraphAnnouncer.LeafText(node), interrupt: true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            _liveKey = null;
        }

        private bool Tab(int step)
        {
            // Snapshot BEFORE rerendering: Reconcile auto-seats a null cursor at the start node, and an
            // unfocused screen's Tab must enter at the first stop, not step from that phantom seat.
            bool wasUnfocused = _state.CurKey == null;
            if (_graph == null || !_graph.Rerender())
            {
                // No focusable content: on an unfocused-capable screen Tab has nothing to enter.
                return false;
            }

            var stops = new List<object>();
            foreach (var n in _graph.Current.Order)
                if (n.StopKey != null && !stops.Contains(n.StopKey)) stops.Add(n.StopKey);
            if (stops.Count == 0) return false;

            var curNode = wasUnfocused ? null : _graph.CurrentNode;
            int idx = curNode != null ? stops.IndexOf(curNode.StopKey) : -1;

            if (idx < 0)
            {
                // Unfocused: Tab enters at the first/last stop.
                return LandOnStop(stops[step >= 0 ? 0 : stops.Count - 1]);
            }

            int ni = idx + step;
            if (ni < 0 || ni >= stops.Count)
            {
                if (Screen != null && Screen.StartUnfocused)
                {
                    Blur(); // truly unfocused: a later re-entry stays with the game
                    if (!string.IsNullOrEmpty(Screen.ScreenName)) Speak(Screen.ScreenName, interrupt: true);
                    return true;
                }
                if (Screen != null && Screen.Wrap)
                    ni = ((ni % stops.Count) + stops.Count) % stops.Count;
                else
                    return true; // at the end; consume, no wrap
            }
            return LandOnStop(stops[ni]);
        }

        // Land on a stop (Tab cycling).
        private bool LandOnStop(object stopKey)
        {
            // Remembered position, SELECTED member, first node (the shared StopLanding).
            var land = KeyGraph.StopLanding(_graph.Current, _graph.State, stopKey);
            if (land == null || !_graph.Focus(land.Id)) return true;

            var node = _graph.CurrentNode;
            PlayHover(node);
            FireFocus(node);
            Speak(ComposeMove(_lastSpokenNode, node, entry: false), interrupt: true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            return true;
        }

        // The landed node's own hover sound when it declares one, else the default.
        private static void PlayHover(GraphNode node)
        {
            var custom = node?.Vtable?.HoverSound;
            if (custom != null) { try { custom(); } catch { } }
            else Navigation.HoverSound?.Invoke();
        }

        private bool JumpEdge(bool first)
        {
            var focusNode = _graph?.CurrentNode;
            if (focusNode == null) return false;

            // In a tree: first/last sibling at the current depth.
            if (KeyGraph.InTree(focusNode))
            {
                var sib = _graph.MoveToSiblingEdge(first);
                if (sib.Moved) AnnounceMove(sib);
                return true;
            }

            // First/last along the vertical axis of the current structure.
            var move = _graph.MoveToEdge(first ? GraphDir.Up : GraphDir.Down);
            if (move.Moved) AnnounceMove(move);
            return true;
        }

        private bool RegionJump(int dir)
        {
            var result = _graph.MoveRegion(dir);
            if (!result.Moved) return true; // no region that way: consume
            AnnounceMove(result, regionEntry: true);
            return true;
        }

        private void AnnounceMove(MoveResult result, bool regionEntry = false)
        {
            var node = result.To;
            if (node == null) return;
            PlayHover(node);
            FireFocus(node); // before speaking, so a tab that selects itself is read as selected
            Speak(ComposeMove(result.From, node, entry: false, transitionLabel: result.TransitionLabel, regionEntry: regionEntry), interrupt: true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
        }

        // The landed node's focus hook (a tab selecting itself); a throwing hook must not break navigation.
        private static void FireFocus(GraphNode node)
        {
            var hook = node?.Vtable?.OnFocus;
            if (hook == null) return;
            try { hook(); }
            catch (Exception e) { CoreLog.Warning("Navigator: focus hook failed: " + e.Message); }
        }

        // Run the focused node's vtable activation; speak its StateText as immediate feedback when it
        // declares one, and rebaseline the live watch so the same change is not spoken twice.
        private bool VtableActivate()
        {
            var node = _graph.CurrentNode;
            if (node?.Vtable.OnActivate == null) return false;
            _graph.Activate();
            node = _graph.CurrentNode;
            var st = node?.Vtable.StateText;
            if (st != null)
            {
                Speak(st(), interrupt: true);
                _liveKey = null; // rebaseline: the change was just spoken synchronously
            }
            return true;
        }

        private bool VtableAdjust(int sign)
        {
            var node = _graph.CurrentNode;
            if (node?.Vtable.OnAdjust == null) return false;
            _graph.TryAdjust(sign, large: false);
            node = _graph.CurrentNode;
            var st = node?.Vtable.StateText;
            if (st != null)
            {
                Speak(st(), interrupt: true);
                _liveKey = null; // rebaseline: the change was just spoken synchronously
            }
            return true;
        }

        // ---- composition ----

        private string ComposeMove(GraphNode from, GraphNode to, bool entry, string transitionLabel = null, bool regionEntry = false)
        {
            return GraphAnnouncer.Compose(entry ? null : from, to, transitionLabel);
        }

        // ---- type-ahead search (landing goes via the graph) ----

        private readonly TypeAheadSearch _search = new TypeAheadSearch();
        private readonly List<GraphNode> _searchNodes = new List<GraphNode>(); // the focused stop's nodes
        private ControlId _searchFocusId; // where the last result landed (staleness check)
        private Screens.Screen _lastTypeaheadScreen;
        private int _searchHeldDir;
        private float _searchRepeatIn;

        public override void TickTypeahead()
        {
            var input = NavInput.Current;
            if (Screen == null || Screen.CapturesRawInput || !Screen.AllowsTypeahead
                || !Navigation.FocusActive() || _graph?.CurrentNode == null)
            {
                if (_search.IsSearchActive || _search.HasBuffer) ClearSearch(announce: false);
                _lastTypeaheadScreen = Screen;
                return;
            }

            if (!ReferenceEquals(Screen, _lastTypeaheadScreen))
            {
                _lastTypeaheadScreen = Screen;
                if (_search.IsSearchActive || _search.HasBuffer) ClearSearch(announce: false);
                return;
            }

            if (input.CtrlHeld || input.AltHeld)
                return;

            bool shift = input.ShiftHeld;
            if (_search.IsSearchActive && !shift)
            {
                if (input.EscapeDown) { ClearSearch(announce: true); return; }
                if (_search.ResultCount > 0 && TickResultArrows(input)) return;
            }
            else
            {
                _searchHeldDir = 0;
            }

            var typed = input.TypedText;
            if (string.IsNullOrEmpty(typed)) return;

            foreach (var ch in typed)
            {
                if (char.IsLetter(ch)) TypeChar(ch);
                else if (ch == ' ' && _search.HasBuffer) TypeChar(ch);
            }
        }

        private bool TickResultArrows(INavInput input)
        {
            int dir = input.UpHeld ? -1 : input.DownHeld ? 1 : 0;
            if (dir == 0) { _searchHeldDir = 0; return false; }

            if (dir != _searchHeldDir)
            {
                _searchHeldDir = dir;
                _searchRepeatIn = OsKeyboard.InitialDelay;
                _search.NavigateResults(dir);
                return true;
            }

            _searchRepeatIn -= input.UnscaledDeltaTime;
            if (_searchRepeatIn <= 0f)
            {
                _searchRepeatIn = OsKeyboard.RepeatInterval;
                _search.NavigateResults(dir);
            }
            return true;
        }

        // An action that fired from a key type-ahead owns while a search is live (a bare letter, space,
        // up/down, escape) is swallowed here: TickTypeahead handles that key itself.
        private static bool FiredFromSearchKey(InputAction action)
        {
            foreach (var b in action.Bindings)
                if (b.ConflictsWithTypeahead && b.Held()) return true;
            return false;
        }

        private void TypeChar(char c)
        {
            // A fresh search remembers the column you are on: every result lands there (row changes,
            // column does not). Captured before the first result moves focus to a primary.
            if (!_search.HasBuffer)
                _searchColumn = _graph?.CurrentNode?.Vtable?.Column ?? -1;
            RebuildSearchScope();
            if (_searchNodes.Count > 0)
            {
                _search.AddChar(c);
                _search.Search(_searchNodes.Count, i => SearchTextOf(_searchNodes[i]), SearchFocusNodeResult);
            }
        }

        // The node's type-ahead text, STRIPPED of rich-text markup: node labels are raw game strings.
        private static string SearchTextOf(GraphNode n)
        {
            string t;
            if (n.Vtable.SearchText != null) t = n.Vtable.SearchText();
            else
            {
                var first = n.Vtable.Announcements != null && n.Vtable.Announcements.Count > 0
                    ? n.Vtable.Announcements[0] : null;
                t = first?.Text?.Invoke();
            }
            return t == null ? null : TextUtil.StripRichText(t);
        }

        private void RebuildSearchScope()
        {
            _searchNodes.Clear();
            // The searchable scope is the focused node's Tab-stop. Tabular rows contribute ONE result,
            // the primary (metadata cells all match the row's name).
            var node = _graph?.CurrentNode;
            if (node == null || _graph.Current == null) return;
            foreach (var n in _graph.Current.Order)
                if (Equals(n.StopKey, node.StopKey) && !n.Vtable.ExcludeFromSearch && n.Vtable.Column <= 0)
                    _searchNodes.Add(n);
        }

        // The tabular column focus was on when the search began: results land ON THE MATCHED ROW at
        // this column, so searching never yanks you out of the column you were scanning.
        private int _searchColumn = -1;

        private void SearchFocusNodeResult(int index)
        {
            if (index < 0 || index >= _searchNodes.Count) return;
            if (!_graph.FocusAtColumn(_searchNodes[index].Id, _searchColumn)) return;
            var node = _graph.CurrentNode;
            PlayHover(node);
            Speak(ComposeMove(_lastSpokenNode, node, entry: false), interrupt: true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            _searchFocusId = node.Id; // the staleness check clears results if focus moves off this
        }

        private void ClearSearch(bool announce)
        {
            bool had = _search.IsSearchActive || _search.HasBuffer;
            _search.Clear();
            _searchNodes.Clear();
            _searchFocusId = null;
            _searchHeldDir = 0;
            _searchColumn = -1;
            if (announce && had) Speak(Strings.Strings.SearchCleared, interrupt: true);
        }
    }

    /// <summary>The UI-category action keys the navigator understands (the input registrations use them).</summary>
    public static class UiActions
    {
        public const string Up = "ui.up";
        public const string Down = "ui.down";
        public const string Left = "ui.left";
        public const string Right = "ui.right";
        public const string Next = "ui.next";
        public const string Prev = "ui.prev";
        public const string Home = "ui.home";
        public const string End = "ui.end";
        public const string RegionPrev = "ui.regionPrev";
        public const string RegionNext = "ui.regionNext";
        public const string Activate = "ui.activate";
        public const string Secondary = "ui.secondary";
        public const string Back = "ui.back";
        public const string Tooltip = "ui.tooltip";
        public const string Drag = "ui.drag";
        public const string Delete = "ui.delete";
        public const string ReadFocus = "ui.readFocus";
    }
}
