using System;
using System.Collections.Generic;
using System.Text;

namespace GuildrunAccess.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read live
    /// and already localized). Every authored word lives in <see cref="Defaults"/> as a key and its
    /// English value; typed accessors read through the loaded translation, so a lang/&lt;language&gt;.txt
    /// file overrides any value at speak time. Word order lives in "{0}"-style templates, never in code.
    /// Values are spoken by a screen reader: terse, lowercase unless shown otherwise, no decorative
    /// punctuation. Keys follow the wotr-access locale vocabulary (role.*, state.*, nav.*, screen.*).
    /// </summary>
    public static class Strings
    {
        private static KeyValuePair<string, string> D(string key, string value)
            => new KeyValuePair<string, string>(key, value);

        /// <summary>Every authored string, in template order: the key a translation file addresses and
        /// its English default.</summary>
        internal static readonly KeyValuePair<string, string>[] Defaults =
        {
            // Spoken once when the mod finishes loading; {0} = the mod version.
            D("app.loaded", "Guildrun Access {0} loaded"),
            // Spoken at launch instead when the feature module fails to load (the mod is then dead).
            D("app.module_failed", "Guildrun Access features failed to load"),
            // Spoken when the focus-mode toggle engages / releases our keyboard navigation.
            D("app.focus_on", "navigation on"),
            D("app.focus_off", "navigation off"),

            // Control role words, spoken after the label ("Continue, button").
            D("role.button", "button"),
            D("role.toggle", "toggle"),
            D("role.slider", "slider"),
            D("role.radio button", "radio button"),
            D("role.combo box", "combo box"),
            D("role.tab", "tab"),
            D("role.key binding", "key binding"),
            D("role.item", "item"),
            D("role.heading", "heading"),
            D("role.list", "list"),
            D("role.table", "table"),
            D("role.link", "link"),

            // Control states.
            D("state.disabled", "disabled"),
            D("state.selected", "selected"),
            D("state.on", "on"),
            D("state.off", "off"),
            D("state.expanded", "expanded"),
            D("state.collapsed", "collapsed"),
            D("value.blank", "blank"),

            // Navigation feedback. {0} = index, {1} = count.
            D("nav.position", "{0} of {1}"),
            D("nav.no_tooltip", "no description"),
            D("nav.no_details", "no details"),
            D("drag.no_target", "nothing to drag"),
            D("delete.no_target", "nothing to remove"),
            D("nav.minimum", "minimum"),
            D("nav.maximum", "maximum"),

            // Type-ahead search. {0} = the typed text.
            D("search.no_match", "no match for {0}"),
            D("search.cleared", "search cleared"),

            // Input action labels (for a key-help reader). Short imperative phrases.
            D("bind.ui.up", "Navigate up"),
            D("bind.ui.down", "Navigate down"),
            D("bind.ui.left", "Navigate left"),
            D("bind.ui.right", "Navigate right"),
            D("bind.ui.next", "Next control group"),
            D("bind.ui.prev", "Previous control group"),
            D("bind.ui.activate", "Activate"),
            D("bind.ui.secondary", "Secondary action"),
            D("bind.ui.back", "Back"),
            D("bind.ui.home", "Jump to first"),
            D("bind.ui.end", "Jump to last"),
            D("bind.ui.tooltip", "Read description"),
            D("bind.ui.regionPrev", "Previous section"),
            D("bind.ui.regionNext", "Next section"),
            D("bind.ui.readFocus", "Read current control"),
            D("bind.mod.focus", "Toggle navigation"),
            D("bind.mod.reload", "Reload mod code"),

            // Screen names, spoken on entry.
            D("screen.main_menu", "Main menu"),
            D("screen.settings", "Settings"),
            D("screen.privacy", "Privacy"),
            D("screen.confirm", "Confirm"),
            D("screen.error", "Error"),
            D("screen.exit", "Exit"),
            D("screen.survey", "Survey"),
            // The role word for a modal dialog context.
            D("role.dialog", "dialog"),

            // The hero picker at the start of a run and the hero card readouts.
            D("screen.choose_hero", "Choose hero"),
            // Row captions of the hero grid (spoken before the value when the row changes).
            D("hero.stats", "stats"),
            D("hero.abilities", "abilities"),
            D("hero.relic", "relic"),
            // {0} = a stat value, {1} = the stat name as the game labels it.
            D("hero.stat", "{1} {0}"),
            D("hero.health", "health"),
            D("hero.mana", "mana"),
            D("hero.no_relic", "no relic"),
            D("hero.no_abilities", "no abilities"),
            D("hero.reroll", "Reroll"),

            // The run screen (the battlefield HUD between and during fights) and its sections.
            D("screen.run", "Run"),
            D("run.actions", "actions"),
            D("run.board", "battlefield"),
            D("run.party", "party"),
            D("run.reserve", "reserve"),
            D("run.items", "items"),
            D("run.relics", "relics"),
            D("run.info", "info"),
            D("run.speed", "battle speed"),
            D("run.menu", "menu"),
            // {0} = unit name, {1} = health text, spoken for a unit on the board.
            D("run.unit_hero", "{0}, hero, {1} health"),
            D("run.unit_enemy", "{0}, enemy, {1} health"),
            // {0} = current mana, {1} = max mana.
            D("run.mana", "mana {0} of {1}"),
            D("run.no_units", "no units on the board"),
            // {0} = slot number (1-based).
            D("run.party_slot", "party slot {0}"),
            D("run.reserve_slot", "reserve slot {0}"),
            D("run.slot_empty", "empty"),
            D("run.item_slot_empty", "empty item slot"),
            D("run.no_items", "no items in reserve"),
            D("run.no_relics", "no relics"),
            D("run.gold", "gold"),
            D("run.shards", "shards"),
            D("run.difficulty", "difficulty"),
            D("run.timer", "timer"),
            D("run.map", "map"),
            D("run.map_current", "current"),
            D("run.speed_auto", "auto"),
            // {0} = the speed step number.
            D("run.speed_n", "speed {0}"),
            D("run.hero_panel", "Heroes"),
            D("run.settings", "Settings"),
            D("run.feedback", "Feedback"),
            D("run.fight", "Fight"),
            D("run.battle_started", "battle started"),
            D("screen.battle_result", "Battle result"),
            D("run.rewards", "rewards"),
            D("run.proceed", "Proceed"),
            D("run.summary", "Summary"),
            // Item and hero actions from the keyboard (the game's drag and drop).
            // {0} = the item's name.
            D("run.equip_to", "Equip {0} to"),
            // {0} = the item, {1} = the hero.
            D("run.equipped", "{0} equipped to {1}"),
            D("run.equip_failed", "could not equip"),
            // {0} = the hero's name.
            D("run.hero_actions", "{0}"),
            D("run.inspect", "Inspect"),
            // {0} = the item's name.
            D("run.unequip", "Unequip {0}"),
            D("run.unequipped", "{0} unequipped"),
            D("run.no_heroes", "no heroes to equip"),
            // The placement grid: rows counted from the player's back line; the enemy rows beyond.
            D("run.grid", "board"),
            D("run.cell_empty", "empty"),
            // {0} = column number, {1} = row number.
            D("run.cell_pos", "column {0}, row {1}"),
            // {0} = column number, {1} = enemy row number.
            D("run.cell_pos_enemy", "column {0}, enemy row {1}"),
            D("run.move", "Move"),
            D("run.to_reserve", "Move to reserve"),
            D("run.to_board", "Place on board"),
            // {0} = the hero's name.
            D("run.picked_up", "{0} picked up. Choose a cell on the board and press Enter"),
            // {0} = the hero, {1} = the cell.
            D("run.moved", "{0} moved to {1}"),
            D("run.move_cancelled", "move cancelled"),
            D("run.move_invalid", "not a cell your heroes can stand on"),
            D("run.move_failed", "could not move"),
            D("run.reserve_full", "the reserve is full"),

            // The shop between fights.
            D("screen.shop", "Shop"),
            D("shop.heroes", "heroes"),
            D("shop.items", "items"),
            D("shop.relics", "relics"),
            D("shop.actions", "actions"),
            // {0} = the price as the shop shows it.
            D("shop.cost", "cost {0}"),
            D("shop.reroll", "Reroll"),
            D("shop.freeze", "Freeze"),
            D("shop.threat", "threat level"),
            D("shop.nothing", "nothing for sale"),

            // The crossroads after a shop: the next paths.
            D("screen.crossroads", "Crossroads"),
            D("crossroads.paths", "paths"),

            // A random event: story, choices, outcome.
            D("screen.event", "Event"),
            D("event.choices", "choices"),
            D("event.outcome", "outcome"),

            // A hero's rank-up choice (specialization / rank modifier picker).
            D("screen.picker", "Rank up"),
            D("picker.hero", "hero"),
            D("picker.choices", "choices"),
            D("picker.no_choices", "no choices yet"),
        };

        private static readonly Dictionary<string, string> _defaults = BuildDefaults();
        private static Dictionary<string, string> _overrides = new Dictionary<string, string>();

        private static Dictionary<string, string> BuildDefaults()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Defaults) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>The value for a key: the loaded translation, else the English default, else the
        /// key itself (a missing key is then audible rather than silent).</summary>
        public static string Get(string key)
        {
            if (_overrides.TryGetValue(key, out var o)) return o;
            if (_defaults.TryGetValue(key, out var v)) return v;
            CoreLog.Warning("Strings: missing key " + key);
            return key;
        }

        /// <summary>Whether a key exists in the defaults (for optional lookups like role words).</summary>
        public static bool Has(string key) => _defaults.ContainsKey(key) || _overrides.ContainsKey(key);

        /// <summary>The value for a key with "{0}"-style slots filled.</summary>
        public static string F(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException) { return template; }
        }

        /// <summary>Install a translation: "key=value" lines ("#" comments and blanks ignored); missing
        /// keys fall back to English. Pass null to return to the defaults.</summary>
        public static void LoadTranslation(IEnumerable<string> lines)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines != null)
                foreach (var raw in lines)
                {
                    if (raw == null) continue;
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                }
            _overrides = map;
        }

        /// <summary>The translator template: every key with its English default, one per line.</summary>
        public static string DumpTemplate()
        {
            var sb = new StringBuilder();
            foreach (var kv in Defaults)
                sb.Append(kv.Key).Append('=').Append(kv.Value.Replace("\n", "\\n")).Append('\n');
            return sb.ToString();
        }

        // ---- typed accessors ----

        public static string ModLoaded(string version) => F("app.loaded", version);
        public static string ModuleFailed => Get("app.module_failed");
        public static string FocusOn => Get("app.focus_on");
        public static string FocusOff => Get("app.focus_off");

        /// <summary>The role word for a control type key ("button" -> "button"); null when none.</summary>
        public static string Role(string word) => Has("role." + word) ? Get("role." + word) : null;
        public static string RoleTable => Get("role.table");
        public static string RoleList => Get("role.list");
        public static string RoleHeading => Get("role.heading");

        public static string StateDisabled => Get("state.disabled");
        public static string StateSelected => Get("state.selected");
        public static string StateOn => Get("state.on");
        public static string StateOff => Get("state.off");
        public static string ExpandedState(bool expanded) => Get(expanded ? "state.expanded" : "state.collapsed");
        public static string ValueBlank => Get("value.blank");

        public static string Position(int index, int count) => F("nav.position", index, count);
        public static string NoTooltip => Get("nav.no_tooltip");
        public static string NoDetails => Get("nav.no_details");
        public static string NoDragTarget => Get("drag.no_target");
        public static string NoDeleteTarget => Get("delete.no_target");
        public static string Minimum => Get("nav.minimum");
        public static string Maximum => Get("nav.maximum");

        public static string SearchNoMatch(string text) => F("search.no_match", text);
        public static string SearchCleared => Get("search.cleared");

        /// <summary>The spoken label of an input action ("bind.ui.down" -> "Navigate down"); the
        /// registration label when no entry exists.</summary>
        public static string BindLabel(string actionKey, string fallback)
            => Has("bind." + actionKey) ? Get("bind." + actionKey) : fallback;

        public static string ScreenMainMenu => Get("screen.main_menu");
        public static string ScreenSettings => Get("screen.settings");
        public static string ScreenPrivacy => Get("screen.privacy");
        public static string ScreenConfirm => Get("screen.confirm");
        public static string ScreenError => Get("screen.error");
        public static string ScreenExit => Get("screen.exit");
        public static string ScreenSurvey => Get("screen.survey");
        public static string RoleDialog => Get("role.dialog");

        public static string ScreenChooseHero => Get("screen.choose_hero");
        public static string HeroStats => Get("hero.stats");
        public static string HeroAbilities => Get("hero.abilities");
        public static string HeroRelic => Get("hero.relic");
        public static string HeroStat(string name, string value) => F("hero.stat", value, name);
        public static string HeroHealth => Get("hero.health");
        public static string HeroMana => Get("hero.mana");
        public static string HeroNoRelic => Get("hero.no_relic");
        public static string HeroNoAbilities => Get("hero.no_abilities");
        public static string HeroReroll => Get("hero.reroll");

        public static string ScreenRun => Get("screen.run");
        public static string RunActions => Get("run.actions");
        public static string RunBoard => Get("run.board");
        public static string RunParty => Get("run.party");
        public static string RunReserve => Get("run.reserve");
        public static string RunItems => Get("run.items");
        public static string RunRelics => Get("run.relics");
        public static string RunInfo => Get("run.info");
        public static string RunSpeed => Get("run.speed");
        public static string RunMenu => Get("run.menu");
        public static string RunUnit(bool hero, string name, string health) => F(hero ? "run.unit_hero" : "run.unit_enemy", name, health);
        public static string RunMana(string current, string max) => F("run.mana", current, max);
        public static string RunNoUnits => Get("run.no_units");
        public static string RunPartySlot(int index) => F("run.party_slot", index);
        public static string RunReserveSlot(int index) => F("run.reserve_slot", index);
        public static string RunSlotEmpty => Get("run.slot_empty");
        public static string RunItemSlotEmpty => Get("run.item_slot_empty");
        public static string RunNoItems => Get("run.no_items");
        public static string RunNoRelics => Get("run.no_relics");
        public static string RunGold => Get("run.gold");
        public static string RunShards => Get("run.shards");
        public static string RunDifficulty => Get("run.difficulty");
        public static string RunTimer => Get("run.timer");
        public static string RunMap => Get("run.map");
        public static string RunMapCurrent => Get("run.map_current");
        public static string RunSpeedAuto => Get("run.speed_auto");
        public static string RunSpeedN(int n) => F("run.speed_n", n);
        public static string RunHeroPanel => Get("run.hero_panel");
        public static string RunSettings => Get("run.settings");
        public static string RunFeedback => Get("run.feedback");
        public static string RunFight => Get("run.fight");
        public static string RunBattleStarted => Get("run.battle_started");
        public static string ScreenBattleResult => Get("screen.battle_result");
        public static string RunRewards => Get("run.rewards");
        public static string RunProceed => Get("run.proceed");
        public static string RunSummary => Get("run.summary");
        public static string RunEquipTo(string item) => F("run.equip_to", item);
        public static string RunEquipped(string item, string hero) => F("run.equipped", item, hero);
        public static string RunEquipFailed => Get("run.equip_failed");
        public static string RunHeroActions(string hero) => F("run.hero_actions", hero);
        public static string RunInspect => Get("run.inspect");
        public static string RunUnequip(string item) => F("run.unequip", item);
        public static string RunUnequipped(string item) => F("run.unequipped", item);
        public static string RunNoHeroes => Get("run.no_heroes");
        public static string RunGrid => Get("run.grid");
        public static string RunCellEmpty => Get("run.cell_empty");
        public static string RunCellPos(int column, int row) => F("run.cell_pos", column, row);
        public static string RunCellPosEnemy(int column, int row) => F("run.cell_pos_enemy", column, row);
        public static string RunMove => Get("run.move");
        public static string RunToReserve => Get("run.to_reserve");
        public static string RunToBoard => Get("run.to_board");
        public static string RunPickedUp(string hero) => F("run.picked_up", hero);
        public static string RunMoved(string hero, string cell) => F("run.moved", hero, cell);
        public static string RunMoveCancelled => Get("run.move_cancelled");
        public static string RunMoveInvalid => Get("run.move_invalid");
        public static string RunMoveFailed => Get("run.move_failed");
        public static string RunReserveFull => Get("run.reserve_full");

        public static string ScreenShop => Get("screen.shop");
        public static string ShopHeroes => Get("shop.heroes");
        public static string ShopItems => Get("shop.items");
        public static string ShopRelics => Get("shop.relics");
        public static string ShopActions => Get("shop.actions");
        public static string ShopCost(string price) => F("shop.cost", price);
        public static string ShopReroll => Get("shop.reroll");
        public static string ShopFreeze => Get("shop.freeze");
        public static string ShopThreat => Get("shop.threat");
        public static string ShopNothing => Get("shop.nothing");
        public static string ScreenCrossroads => Get("screen.crossroads");
        public static string CrossroadsPaths => Get("crossroads.paths");
        public static string ScreenEvent => Get("screen.event");
        public static string EventChoices => Get("event.choices");
        public static string EventOutcome => Get("event.outcome");
        public static string ScreenPicker => Get("screen.picker");
        public static string PickerHero => Get("picker.hero");
        public static string PickerChoices => Get("picker.choices");
        public static string PickerNoChoices => Get("picker.no_choices");
    }
}
