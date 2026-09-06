using System;
using System.Collections.Generic;
using System.Text;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.Battle.UI.Hud;
using Ember.Scopes.GameRun.UI;
using Ember.Scopes.GameRun.UI.BattleSpeed;
using Ember.Scopes.GameRun.UI.ChunkUI;
using Ember.Scopes.GameRun.UI.HeroCard.Elements;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.GameRun.UI.Relics;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.Equipment;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Module.Run;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The run HUD: the base context while a run is loaded, between and during fights. Tab-stops, in
    /// order: actions (Fight), the battlefield (every unit with a health bar: heroes then enemies, with
    /// health and mana), the party (active and reserve slots with their abilities and item slots), the
    /// item reserve, relics, info (gold, shards, difficulty, timer, the act map), battle speed, and the
    /// menu (heroes, settings, feedback). Everything is read live from the game's own views; Space reads
    /// a control's tooltip through the game's tooltip pipeline.
    /// </summary>
    public sealed class GameRunScreen : Screen
    {
        public override string Key => "gamerun";
        public override int Layer => 0;
        public override bool AllowsTypeahead => true;

        // The controllers, each re-found by scene scan (throttled) when absent.
        private readonly Finder<BottomHeroPanelUIController> _party = new Finder<BottomHeroPanelUIController>();
        private readonly Finder<BattleFlowUIStateController> _flow = new Finder<BattleFlowUIStateController>();
        private readonly Finder<BattleUIController> _battle = new Finder<BattleUIController>();
        private readonly Finder<ItemReserveUIController> _reserve = new Finder<ItemReserveUIController>();
        private readonly Finder<RelicUIController> _relics = new Finder<RelicUIController>();
        private readonly Finder<BasicInfoUIPanelController> _info = new Finder<BasicInfoUIPanelController>();
        private readonly Finder<ChunkUIController> _chunk = new Finder<ChunkUIController>();
        private readonly Finder<BattleTimerController> _timer = new Finder<BattleTimerController>();
        private readonly Finder<BattleSpeedController> _speed = new Finder<BattleSpeedController>();
        private readonly Finder<NavigationUIController> _nav = new Finder<NavigationUIController>();

        public override bool IsActive()
        {
            var party = _party.Get();
            return party != null && party.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            b.PushContext(Strings.ScreenRun, null, positions: false);
            BuildActions(b);
            BuildBoard(b);
            BuildParty(b);
            BuildItems(b);
            BuildRelics(b);
            BuildInfo(b);
            BuildSpeed(b);
            BuildMenu(b);
            b.PopContext();
        }

        // ---- actions: the Fight button while placing ----

        private void BuildActions(GraphBuilder b)
        {
            var flow = _flow.Get();
            var placement = flow != null ? flow._placementParent : null;
            if (placement == null || !placement.activeInHierarchy) return;
            b.BeginStop("actions");
            foreach (var button in placement.GetComponentsInChildren<Button>(false))
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                var btn = button;
                b.AddItem(ControlId.Structural("run:action:" + button.gameObject.name + button.GetInstanceID()),
                    GameNodes.Button(btn));
            }
        }

        // ---- the battlefield: units with health bars, heroes first ----

        private struct Unit { public string Name; public HealthBarView Bar; public bool IsHero; }

        private List<Unit> Units()
        {
            var units = new List<Unit>();
            var battle = _battle.Get();
            var bars = battle != null ? battle._healthBars : null;
            if (bars == null) return units;
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<CharacterViewController>()))
            {
                var c = o != null ? o.TryCast<CharacterViewController>() : null;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                HealthBarView bar;
                if (!bars.TryGetValue(c.EntityId, out bar) || bar == null || !bar.gameObject.activeInHierarchy) continue;
                string name = bar._characterNameText != null ? bar._characterNameText.text : null;
                if (string.IsNullOrWhiteSpace(name)) name = c.gameObject.name.Replace("(Clone)", "");
                units.Add(new Unit { Name = name, Bar = bar, IsHero = IsHero(c) });
            }
            units.Sort((x, y) => x.IsHero == y.IsHero ? string.CompareOrdinal(x.Name, y.Name) : (x.IsHero ? -1 : 1));
            return units;
        }

        // A hero character carries a hero id; the interop nullable throws when there is none (an enemy).
        private static bool IsHero(CharacterViewController c)
        {
            try { return c.HeroId.HasValue; }
            catch (NullReferenceException) { return false; }
        }

        private void BuildBoard(GraphBuilder b)
        {
            var units = Units();
            b.BeginStop("board");
            b.PushContext(Strings.RunBoard, Strings.RoleList);
            if (units.Count == 0)
                b.AddItem(ControlId.Structural("run:board:none"), GameNodes.Text(() => Strings.RunNoUnits));
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                b.AddItem(ControlId.Structural("run:unit:" + u.Bar.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => Strings.RunUnit(u.IsHero, u.Name, Health(u.Bar)), kind: AnnouncementKinds.Label),
                        // Not live: mana and health change every tick of a fight; re-read on demand (Ctrl+Space).
                        new NodeAnnouncement(() => Mana(u.Bar), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => u.Name,
                    OnTooltip = () => Core.Speech.Say(ItemsOn(u.Bar) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();
        }

        private static string Health(HealthBarView bar)
            => bar._healthText != null ? bar._healthText.text : "";

        private static string Mana(HealthBarView bar)
        {
            var slider = bar._manaSlider;
            if (slider == null || !slider.gameObject.activeInHierarchy || slider.maxValue <= 0) return null;
            return Strings.RunMana(((int)slider.value).ToString(), ((int)slider.maxValue).ToString());
        }

        // The unit's equipped items, by name.
        private static string ItemsOn(HealthBarView bar) => ItemNodes.ItemNames(bar._itemSlotViews);

        // ---- the party: active and reserve slots ----

        private void BuildParty(GraphBuilder b)
        {
            var party = _party.Get();
            if (party == null) return;
            b.BeginStop("party");
            AddHeroPanel(b, party._activeHeroPanel, Strings.RunParty, "party", false);
            AddHeroPanel(b, party._reserveHeroPanel, Strings.RunReserve, "reserve", true);
        }

        private static void AddHeroPanel(GraphBuilder b, BottomHeroPanelView panel, string label, string key, bool reserve)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var views = panel.HeroViews;
            if (views == null) return;
            b.PushContext(label, Strings.RoleList);
            int n = 0;
            foreach (var v in views)
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                n++;
                int index = n;
                var view = v;
                b.AddItem(ControlId.Structural("run:" + key + ":" + view.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => reserve ? Strings.RunReserveSlot(index) : Strings.RunPartySlot(index), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => view.IsEmpty ? Strings.RunSlotEmpty : SlotSummary(view), live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => SlotSummary(view),
                    OnActivate = () => OpenHeroMenu(view),
                    OnTooltip = () => Core.Speech.Say(SlotTooltips(view) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();
        }

        // The hero occupying a slot: its name, then its ability names and equipped items.
        private static string SlotSummary(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesLine(view._abilitiesView);
            string items = ItemNodes.ItemNames(view._itemSlotViews);
            string body = items == null ? abilities : abilities + "; " + items;
            string name = RunData.HeroName(view);
            return string.IsNullOrEmpty(name) ? body : name + ": " + body;
        }

        // Enter on a hero: a menu of what the mouse would do with it: inspect its card, unequip one of
        // its items (back to the reserve).
        private static void OpenHeroMenu(BottomHeroView view)
        {
            if (view == null || view.IsEmpty) { ClickPortrait(view); return; }
            string heroName = RunData.HeroName(view) ?? Strings.RunParty;
            var options = new List<ChoiceOption> { new ChoiceOption(Strings.RunInspect, () => ClickPortrait(view)) };
            if (RunData.TryHeroId(view, out var heroId) && view._itemSlotViews != null)
            {
                foreach (var slot in view._itemSlotViews)
                {
                    if (!RunData.TryItemId(slot, out var itemId)) continue;
                    string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
                    var id = itemId;
                    options.Add(new ChoiceOption(Strings.RunUnequip(itemName), () =>
                    {
                        if (RunData.Unequip(heroId, id)) Core.Speech.Say(Strings.RunUnequipped(itemName), interrupt: true);
                    }));
                }
            }
            ChoiceSubmenuScreen.Open(Strings.RunHeroActions(heroName), options);
        }

        // Enter on a reserve item: pick the hero to equip it to (what dragging it onto a hero does).
        private void OpenEquipMenu(PlaceholderSlotView slot)
        {
            if (!RunData.TryItemId(slot, out var itemId)) return;
            string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
            var options = new List<ChoiceOption>();
            var party = _party.Get();
            if (party != null)
            {
                AddEquipTargets(options, party._activeHeroPanel, itemId, itemName);
                AddEquipTargets(options, party._reserveHeroPanel, itemId, itemName);
            }
            if (options.Count == 0) { Core.Speech.Say(Strings.RunNoHeroes, interrupt: true); return; }
            ChoiceSubmenuScreen.Open(Strings.RunEquipTo(itemName), options);
        }

        private static void AddEquipTargets(List<ChoiceOption> options, BottomHeroPanelView panel, ItemId itemId, string itemName)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || panel.HeroViews == null) return;
            foreach (var view in panel.HeroViews)
            {
                if (view == null || !view.gameObject.activeInHierarchy || view.IsEmpty) continue;
                if (!RunData.TryHeroId(view, out var heroId)) continue;
                string heroName = RunData.HeroName(view) ?? Strings.RunParty;
                string items = ItemNodes.ItemNames(view._itemSlotViews);
                var id = heroId;
                options.Add(new ChoiceOption(heroName, () =>
                {
                    Core.Speech.Say(RunData.Equip(id, itemId) ? Strings.RunEquipped(itemName, heroName) : Strings.RunEquipFailed, interrupt: true);
                }, items));
            }
        }

        private static string SlotTooltips(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesTooltips(view._abilitiesView);
            string items = ItemNodes.ItemTooltips(view._itemSlotViews);
            if (abilities == null) return items;
            return items == null ? abilities : abilities + ". " + items;
        }

        // The portrait button opens the hero's card in the sidebar (the game's own inspect action).
        private static void ClickPortrait(BottomHeroView view)
        {
            foreach (var button in view.GetComponentsInChildren<Button>(false))
                if (GameNodes.IsShown(button)) { button.onClick.Invoke(); return; }
        }

        // ---- items: the reserve column ----

        private void BuildItems(GraphBuilder b)
        {
            var reserve = _reserve.Get();
            if (reserve == null || !reserve.gameObject.activeInHierarchy) return;
            b.BeginStop("items");
            b.PushContext(Strings.RunItems, Strings.RoleList);
            int n = 0;
            foreach (var slot in reserve.GetComponentsInChildren<PlaceholderSlotView>(false))
            {
                if (!ItemNodes.HasItem(slot)) continue;
                n++;
                var s = slot;
                b.AddItem(ControlId.Structural("run:item:" + slot.GetInstanceID()), ItemNodes.Slot(slot, () => OpenEquipMenu(s)));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:item:none"), GameNodes.Text(() => Strings.RunNoItems));
            b.PopContext();
        }

        // ---- relics ----

        private void BuildRelics(GraphBuilder b)
        {
            var relics = _relics.Get();
            if (relics == null || !relics.gameObject.activeInHierarchy) return;
            b.BeginStop("relics");
            b.PushContext(Strings.RunRelics, Strings.RoleList);
            int n = 0;
            foreach (var relic in relics.GetComponentsInChildren<RelicView>(false))
            {
                if (relic == null || !relic.gameObject.activeInHierarchy) continue;
                n++;
                b.AddItem(ControlId.Structural("run:relic:" + relic.GetInstanceID()), ItemNodes.Relic(relic));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:relic:none"), GameNodes.Text(() => Strings.RunNoRelics));
            b.PopContext();
        }

        // ---- info: gold, shards, difficulty, timer, the act map ----

        private void BuildInfo(GraphBuilder b)
        {
            b.BeginStop("info");
            b.PushContext(Strings.RunInfo, Strings.RoleList);
            var info = _info.Get();
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
            var timer = _timer.Get();
            if (timer != null && timer._timerText != null && timer._timerText.gameObject.activeInHierarchy)
                AddValue(b, "timer", () => Strings.RunTimer, () => timer._timerText.text, null);
            var chunk = _chunk.Get();
            if (chunk != null && chunk.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("run:info:map"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => Strings.RunMap, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => MapLine(chunk), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => Strings.RunMap,
                    OnTooltip = () => Core.Speech.Say(MapTooltips(chunk) ?? Strings.NoTooltip, interrupt: true),
                });
            b.PopContext();
        }

        private static void AddValue(GraphBuilder b, string key, Func<string> label, Func<string> value, Ember.Scopes.Application.UI.Tooltips.TooltipRaycastTarget tooltip)
        {
            b.AddItem(ControlId.Structural("run:info:" + key), new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnTooltip = tooltip == null ? (Action)null
                    : () => Core.Speech.Say(TooltipReader.Describe(tooltip) ?? Strings.NoTooltip, interrupt: true),
            });
        }

        private static List<ActNodeView> MapNodes(ChunkUIController chunk)
        {
            var list = new List<ActNodeView>();
            var parent = chunk._nodeParent;
            if (parent == null) return list;
            foreach (var node in parent.GetComponentsInChildren<ActNodeView>(false))
                if (node != null && node.gameObject.activeInHierarchy) list.Add(node);
            return list;
        }

        // "fight (current), event, boss": each node's title, the current one marked.
        private static string MapLine(ChunkUIController chunk)
        {
            var sb = new StringBuilder();
            foreach (var node in MapNodes(chunk))
            {
                string title = node.Title;
                if (string.IsNullOrWhiteSpace(title)) title = TooltipReader.Title(node.TooltipRaycastTarget);
                if (string.IsNullOrWhiteSpace(title)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(title);
                var marker = node.CurrentMarker;
                if (marker != null && marker.gameObject.activeInHierarchy && marker.enabled)
                    sb.Append(" (").Append(Strings.RunMapCurrent).Append(')');
            }
            return sb.ToString();
        }

        private static string MapTooltips(ChunkUIController chunk)
        {
            var sb = new StringBuilder();
            foreach (var node in MapNodes(chunk))
            {
                var text = TooltipReader.Describe(node.TooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ---- battle speed ----

        private void BuildSpeed(GraphBuilder b)
        {
            var speed = _speed.Get();
            if (speed == null || !speed.gameObject.activeInHierarchy) return;
            b.BeginStop("speed");
            b.PushContext(Strings.RunSpeed, null, positions: false);
            b.StartRow();
            int any = 0;
            if (speed._autoView != null && GameNodes.IsShown(speed._autoView.Toggle))
            {
                b.AddItem(ControlId.Structural("run:speed:auto"), GameNodes.Tab(speed._autoView.Toggle, () => Strings.RunSpeedAuto));
                any++;
            }
            var views = speed._speedViews;
            if (views != null)
                for (int i = 0; i < views.Length; i++)
                {
                    var v = views[i];
                    if (v == null || !GameNodes.IsShown(v.Toggle)) continue;
                    int n = i + 1;
                    b.AddItem(ControlId.Structural("run:speed:" + n), GameNodes.Tab(v.Toggle, () => Strings.RunSpeedN(n)));
                    any++;
                }
            if (any > 0) b.EndRow();
            else
            {
                b.EndRow();
            }
            b.PopContext();
        }

        // ---- menu: heroes panel, settings, feedback ----

        private void BuildMenu(GraphBuilder b)
        {
            var nav = _nav.Get();
            if (nav == null || !nav.gameObject.activeInHierarchy) return;
            b.BeginStop("menu");
            b.PushContext(Strings.RunMenu, Strings.RoleList);
            if (GameNodes.IsShown(nav._heroPanelButton))
                b.AddItem(ControlId.Structural("run:menu:heroes"), GameNodes.Button(nav._heroPanelButton, () => Strings.RunHeroPanel));
            if (GameNodes.IsShown(nav._choiceNavigationButton))
                b.AddItem(ControlId.Structural("run:menu:choice"), GameNodes.Button(nav._choiceNavigationButton,
                    () => nav._choiceNavigationLabel != null && !string.IsNullOrWhiteSpace(nav._choiceNavigationLabel.text)
                        ? nav._choiceNavigationLabel.text : GameNodes.LabelOf(nav._choiceNavigationButton)));
            if (GameNodes.IsShown(nav._showFeedbackPanelButton))
                b.AddItem(ControlId.Structural("run:menu:feedback"), GameNodes.Button(nav._showFeedbackPanelButton, () => Strings.RunFeedback));
            if (GameNodes.IsShown(nav._settingsButton))
                b.AddItem(ControlId.Structural("run:menu:settings"), GameNodes.Button(nav._settingsButton, () => Strings.RunSettings));
            b.PopContext();
        }
    }

    /// <summary>A throttled scene-scan cache for one controller type: re-finds it only after it was
    /// destroyed, at most every few frames, so screens can poll it every frame cheaply.</summary>
    internal sealed class Finder<T> where T : MonoBehaviour
    {
        private T _value;
        private const int SearchEvery = 30;
        private int _lastSearchFrame = -SearchEvery;

        public T Get()
        {
            if (_value != null) return _value;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<T>());
            _value = found != null ? found.TryCast<T>() : null;
            return _value;
        }
    }
}
