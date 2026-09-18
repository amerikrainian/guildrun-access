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
            // Follows the loaded line when the newest release outranks the running build; {0} = that
            // newer version. Up to date (or ahead, a dev build) stays silent.
            D("app.update_available", "update {0} available"),
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
            // Buffer review (Ctrl plus arrows): the buffer names, the "name: line" readout on switching,
            // and the words when every buffer is empty.
            D("buffer.ui", "control"),
            D("buffer.hero", "hero"),
            D("buffer.item", "items"),
            D("buffer.relic", "relics"),
            D("buffer.party", "party"),
            D("buffer.enemies", "enemies"),
            D("buffer.combat", "combat"),
            D("buffer.none", "no buffer lines"),
            // {0} = the buffer's name, {1} = its current line.
            D("buffer.line", "{0}: {1}"),
            D("tooltip.stat_definition", "{0}: {1}"),
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
            // The battle board's hex keys (Q E A D Z C, the hexagon's layout on the keyboard) and the
            // Shift+letter hero moves.
            D("bind.run.hex.upleft", "Cell up left"),
            D("bind.run.hex.upright", "Cell up right"),
            D("bind.run.hex.left", "Cell left"),
            D("bind.run.hex.right", "Cell right"),
            D("bind.run.hex.downleft", "Cell down left"),
            D("bind.run.hex.downright", "Cell down right"),
            D("bind.run.move.upleft", "Move hero up left"),
            D("bind.run.move.upright", "Move hero up right"),
            D("bind.run.move.left", "Move hero left"),
            D("bind.run.move.right", "Move hero right"),
            D("bind.run.move.downleft", "Move hero down left"),
            D("bind.run.move.downright", "Move hero down right"),
            D("bind.mod.focus", "Toggle navigation"),
            // The glance keys: a digit reads one fact group of the unit the focused control concerns,
            // in place, focus unmoved; Shift+2, 3, 4 the same group with each stat's breakdown.
            D("bind.glance.vitals", "Unit health, shield and mana"),
            D("bind.glance.attack", "Unit attack, magic and defense"),
            D("bind.glance.tempo", "Unit attack speed, crit, range and move speed"),
            D("bind.glance.sustain", "Unit regen, omnivamp and resistances"),
            D("bind.glance.statuses", "Unit statuses"),
            D("bind.glance.target", "Unit target"),
            D("bind.glance.attack.detail", "Unit attack, magic and defense, with breakdown"),
            D("bind.glance.tempo.detail", "Unit attack speed, crit, range and move speed, with breakdown"),
            D("bind.glance.sustain.detail", "Unit regen, omnivamp and resistances, with breakdown"),
            D("bind.run.shards", "Shards"),
            D("bind.run.position", "Board position"),
            D("bind.run.timer", "Battle timer"),
            D("bind.run.nearby", "Nearby units"),
            D("bind.run.hostiles", "Nearby hostiles"),
            D("bind.shop.reroll", "Shop reroll"),
            D("bind.shop.freeze", "Shop freeze"),
            D("bind.mod.reload", "Reload mod code"),

            // Screen names, spoken on entry.
            D("screen.main_menu", "Main menu"),
            D("screen.settings", "Settings"),
            D("screen.privacy", "Privacy"),
            D("screen.confirm", "Confirm"),
            D("screen.error", "Error"),
            D("dialog.stack_trace", "stack trace"),
            D("dialog.copied", "copied to clipboard"),
            D("dialog.copy_failed", "could not copy"),
            D("mainmenu.version", "version {0}"),
            D("leaderboard.visible", "Show leaderboard"),
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
            D("hero.relic_named", "relic {0}"),
            // {0} = a stat value, {1} = the stat name as the game labels it.
            D("hero.stat", "{1} {0}"),
            D("hero.health", "health"),
            D("hero.mana", "mana"),
            // {0} = the rank letter the game's hero card shows (C, B, A, S).
            D("hero.rank", "rank {0}"),
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
            // {0} = the hero's equipped items, comma-separated: after the hero's name wherever a hero is listed.
            D("run.wearing", "wearing {0}"),
            D("run.relics", "relics"),
            D("run.info", "info"),
            D("run.speed", "battle speed"),
            D("run.menu", "menu"),
            // {0} = unit name, {1} = health text, spoken for a unit on the board.
            // {0} = the unit's name (with its items), {1} = its health phrase ("health 650").
            D("run.unit_hero", "{0}, hero, {1}"),
            D("run.unit_enemy", "{0}, enemy, {1}"),
            // {0} = current mana, {1} = max mana.
            D("run.mana", "mana {0}/{1}"),
            // {0} = shield points, spoken only while a shield is up.
            D("run.shield", "shield {0}"),
            // A status icon on a unit's bar, in the party and enemies buffers. {0} = the status,
            // {1} = the number the icon shows: the stacks of a stacking status (Poison 3)...
            D("run.status", "{0} {1}"),
            // ...or the seconds left on a timed one (Stun), which the game counts down on the icon.
            D("run.status_timed", "{0} {1} seconds"),
            // The glance keys' lines. {0} = a name (health, mana), {1} = the current value, {2} = the max.
            D("glance.pair", "{0} {1}/{2}"),
            // {0} = a counter's title (the game's tooltip title), {1} = its value.
            D("glance.value", "{0} {1}"),
            // A stat's breakdown after its total, each part only when it is nonzero: {0} = the base
            // value, the rank bonus, the other bonuses (items, relics, effects).
            D("glance.base", "base {0}"),
            D("glance.rank", "rank {0}"),
            D("glance.bonus", "bonus {0}"),
            // {0} = the unit a fighting unit is attacking right now.
            D("glance.target", "attacking {0}"),
            D("glance.no_target", "no target"),
            // One unit of the nearby-units list: {0} = its name, {1} = its distance in hex steps. A
            // numbered enemy reads "Slime 1 4": Slime 1, 4 away.
            D("glance.nearby", "{0} {1}"),
            // An enemy that shares its name with another on the board: {0} = the name, {1} = its
            // number among them, in the grid's reading order.
            D("run.enemy_numbered", "{0} {1}"),
            // {0} = slot number (1-based).
            D("run.party_slot", "party slot {0}"),
            D("run.reserve_slot", "reserve slot {0}"),
            D("run.slot_empty", "empty"),
            D("run.item_slot_empty", "empty item slot"),
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
            // The result panel's stats and leaderboard (the run's end).
            D("result.stats", "stats"),
            D("result.tracker", "combat tracker"),
            D("result.damage_dealt", "damage dealt"),
            D("result.damage_taken", "damage taken"),
            D("result.healing_done", "healing done"),
            D("result.status_applied", "applied"),
            D("result.previous_combat", "Previous combat"),
            D("result.next_combat", "Next combat"),
            D("result.leaderboard", "leaderboard"),
            D("result.versus", "{0} versus {1}"),
            D("result.entries", "entries"),
            // {0} = rank, {1} = player name, {2} = floor reached.
            D("result.entry", "{0}, {1}, floor {2}"),
            D("result.reset", "time until reset"),

            // The run's end screen.
            D("screen.end", "Run over"),
            D("end.victory", "Run won"),
            D("end.defeat", "Run lost"),
            D("end.info", "run"),
            D("end.floor", "floor reached"),
            D("end.rank", "leaderboard rank"),
            D("end.rank_change", "change"),
            D("end.top_percent", "top"),
            D("end.heroes", "final team"),
            D("end.backup", "backup team"),
            D("end.no_items", "no items"),
            D("end.continue", "Continue"),
            D("end.no_highlights", "no highlights"),

            // The meta-progression (unlock timeline) after a run.
            D("screen.progression", "Progression"),
            // {0} = XP, {1} = level.
            D("progression.xp", "{0} XP, level {1}"),
            D("progression.milestones", "milestones"),
            // {0} = the XP the milestone needs.
            D("progression.threshold", "{0} XP"),
            D("progression.locked", "locked"),
            D("progression.unlocked", "unlocked"),
            D("progression.new", "new"),

            // The run-start (difficulty) screen.
            D("screen.difficulty", "New run"),
            D("difficulty.levels", "difficulty"),
            D("difficulty.modifiers", "modifiers"),
            D("difficulty.streak", "streak"),
            // {0} = the tier's name as the game labels it ("LETHAL"), {1} = the rank its icon shows (C, B, A, S, SS, SSS).
            D("difficulty.tier_rank", "{0} {1}"),

            // The run's Heroes panel.
            D("heroes.counters", "counters"),
            D("heroes.close", "Close"),
            D("heroes.none", "no heroes"),

            // Comics (click-anywhere panels) and the mod's own menu.
            D("screen.comic", "Comic"),
            D("comic.continue", "Continue"),
            D("screen.mod_menu", "Guildrun Access"),
            D("mod.settings", "Settings"),
            D("mod.key_help", "Key help"),
            D("mod.close", "Close"),
            D("mod.speak_positions", "Speak list positions"),
            D("mod.focus_on_launch", "Keyboard navigation on at launch"),
            // {0} = the key category (Global, UI, Game).
            D("mod.key_category", "{0} keys"),
            D("bind.mod.menu", "Mod menu"),

            // The run's sidebar and the fight narration.
            D("run.sidebar", "sidebar"),
            D("run.damage_tracker", "Damage tracker"),
            D("run.challenge", "Challenge"),
            // The battle HUD's events, as the game shows them (floating numbers, status icons, deaths, casts).
            D("run.events", "battle events"),
            // {0} = the unit, {1} = the number shown.
            D("battle.damage", "{0}: {1} damage"),
            D("battle.crit", "{0}: {1} critical damage"),
            D("battle.healed", "{0}: {1} healed"),
            // {0} = the unit, {1} = the status, {2} = its stack count.
            D("battle.status", "{0}: {1} {2}"),
            D("battle.status_gone", "{0}: {1} gone"),
            // {0} = the unit, {1} = the ability.
            D("battle.cast", "{0} casts {1}"),
            D("battle.ability", "ability"),
            D("battle.defeated", "{0} defeated"),
            D("status.burn", "Burn"),
            D("status.frost", "Frost"),
            D("status.poison", "Poison"),
            D("status.stun", "Stun"),
            D("status.bleed", "Bleed"),
            D("status.cantattack", "Can't attack"),
            D("status.cantmove", "Can't move"),
            D("status.cantcast", "Can't cast"),
            D("status.stealth", "Stealth"),
            D("status.shield", "Shield"),
            D("status.damageimmunity", "Damage immunity"),
            D("status.statreduction", "Stat reduction"),
            D("status.antiheal", "Anti-heal"),
            D("status.selkherasstoning", "Stoning"),
            // The compendium.
            D("screen.compendium", "Compendium"),
            D("compendium.sections", "sections"),
            D("compendium.overview", "Overview"),
            D("compendium.filters", "filters"),
            // {0} = the search text.
            D("compendium.search", "search: {0}"),
            D("compendium.search_empty", "empty"),
            D("compendium.class_filter", "class"),
            D("compendium.clear_filter", "Clear class filter"),
            D("compendium.heroes", "heroes"),
            D("compendium.trophies", "{0} of {1} trophies"),
            D("compendium.classes", "classes"),
            D("compendium.ranks", "rank"),
            // {0} = the rank tab's number when it has no caption.
            D("compendium.rank_n", "rank {0}"),
            D("compendium.upgrades", "rank upgrades"),
            D("hero.items", "items"),
            D("hero.no_items", "no items"),
            // Item and hero actions from the keyboard (the game's drag and drop).
            // {0} = the item's name.
            D("run.equip_to", "Equip {0} to"),
            // {0} = the item, {1} = the hero.
            D("run.equipped", "{0} equipped to {1}"),
            D("run.equip_failed", "could not equip"),
            D("run.slots_full", "item slots full"),
            // {0} = the hero's name.
            D("run.hero_actions", "{0}"),
            D("run.inspect", "Inspect"),
            D("run.inspect_failed", "could not inspect"),
            // {0} = the item's name.
            D("run.unequip", "Unequip {0}"),
            D("run.unequipped", "{0} unequipped"),
            D("run.sell", "Sell {0}"),
            D("run.sell_for", "Sell {0} for {1} shards"),
            D("run.sold", "{0} sold"),
            D("run.no_heroes", "no heroes to equip"),
            // The placement grid: rows counted from the player's back line; the enemy rows beyond.
            D("run.grid", "board"),
            D("run.cell_empty", "empty"),
            // {0} = column number, {1} = row number, both one-based on the game's single grid (rows run
            // from the player's back line up through the enemy side): bare coordinates, column first.
            D("run.cell_pos", "{0}, {1}"),
            D("run.board_heroes", "heroes"),
            D("run.board_enemies", "enemies"),
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
            D("shop.items", "shop items"),
            D("shop.relics", "shop relics"),
            D("shop.actions", "actions"),
            // {0} = the price as the shop shows it.
            D("shop.cost", "cost {0}"),
            D("shop.reroll", "Reroll"),
            D("shop.freeze", "Freeze"),
            // The freeze key's feedback when the button's own caption does not change with the state.
            D("shop.frozen", "frozen"),
            D("shop.unfrozen", "unfrozen"),
            D("shop.threat", "threat level"),

            // The crossroads after a shop: the next paths.
            D("screen.crossroads", "Crossroads"),
            D("crossroads.paths", "paths"),

            // A random event: story, choices, outcome.
            D("screen.event", "Event"),
            D("event.outcome", "outcome"),

            // A hero's rank-up choice (specialization / rank modifier picker).
            D("screen.picker", "Rank up"),
            D("picker.choices", "choices"),
            D("picker.no_choices", "no choices yet"),

            // The relic reward picker (after a challenge fight), when its own title text is missing.
            D("screen.relic_picker", "Choose your reward"),
            // The tutorial's modal texts, read as a dialog whose Enter skips the text shown.
            D("screen.tutorial", "Tutorial"),
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
        public static string UpdateAvailable(string version) => F("app.update_available", version);
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
        public static string BufferUi => Get("buffer.ui");
        public static string BufferHero => Get("buffer.hero");
        public static string BufferItem => Get("buffer.item");
        public static string BufferRelic => Get("buffer.relic");
        public static string BufferParty => Get("buffer.party");
        public static string BufferEnemies => Get("buffer.enemies");
        public static string BufferCombat => Get("buffer.combat");
        public static string BufferNone => Get("buffer.none");
        public static string BufferLine(string name, string line) => F("buffer.line", name, line);
        public static string StatDefinition(string name, string description) => F("tooltip.stat_definition", name, description);
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
        public static string DialogStackTrace => Get("dialog.stack_trace");
        public static string DialogCopied => Get("dialog.copied");
        public static string DialogCopyFailed => Get("dialog.copy_failed");
        public static string MainMenuVersion(string version) => F("mainmenu.version", version);
        public static string LeaderboardVisible => Get("leaderboard.visible");
        public static string ScreenExit => Get("screen.exit");
        public static string ScreenSurvey => Get("screen.survey");
        public static string RoleDialog => Get("role.dialog");

        public static string ScreenChooseHero => Get("screen.choose_hero");
        public static string HeroStats => Get("hero.stats");
        public static string HeroAbilities => Get("hero.abilities");
        public static string HeroRelic => Get("hero.relic");
        public static string HeroRelicNamed(string name) => F("hero.relic_named", name);
        public static string HeroStat(string name, string value) => F("hero.stat", value, name);
        public static string HeroHealth => Get("hero.health");
        public static string HeroMana => Get("hero.mana");

        /// <summary>"rank C": a hero's rank as the game's card shows it.</summary>
        public static string HeroRank(string rank) => F("hero.rank", rank);

        /// <summary>"Skorn, Warrior, rank C": a hero's name, then its classes, then its rank, whatever is
        /// missing left out: the line every control standing for a hero opens with.</summary>
        public static string HeroTitle(string name, IEnumerable<string> classes, string rank)
        {
            var parts = new List<string> { name };
            if (classes != null)
                foreach (var c in classes)
                    if (!string.IsNullOrEmpty(c)) parts.Add(c);
            if (!string.IsNullOrEmpty(rank)) parts.Add(HeroRank(rank));
            return string.Join(", ", parts);
        }
        public static string HeroNoRelic => Get("hero.no_relic");
        public static string HeroNoAbilities => Get("hero.no_abilities");
        public static string HeroReroll => Get("hero.reroll");

        public static string ScreenRun => Get("screen.run");
        public static string RunActions => Get("run.actions");
        public static string RunBoard => Get("run.board");
        public static string RunParty => Get("run.party");
        public static string RunReserve => Get("run.reserve");
        public static string RunItems => Get("run.items");
        public static string RunWearing(string items) => F("run.wearing", items);
        public static string RunRelics => Get("run.relics");
        public static string RunInfo => Get("run.info");
        public static string RunSpeed => Get("run.speed");
        public static string RunMenu => Get("run.menu");
        public static string RunUnit(bool hero, string name, string health) => F(hero ? "run.unit_hero" : "run.unit_enemy", name, health);
        public static string RunMana(string current, string max) => F("run.mana", current, max);
        public static string RunStatus(string status, int stacks) => F("run.status", status, stacks);
        public static string RunStatusTimed(string status, int seconds) => F("run.status_timed", status, seconds);
        public static string GlancePair(string name, string current, string max) => F("glance.pair", name, current, max);
        public static string GlanceValue(string name, string value) => F("glance.value", name, value);
        public static string GlanceBase(string value) => F("glance.base", value);
        public static string GlanceRank(string value) => F("glance.rank", value);
        public static string GlanceBonus(string value) => F("glance.bonus", value);
        public static string GlanceTarget(string unit) => F("glance.target", unit);
        public static string GlanceNoTarget => Get("glance.no_target");
        public static string GlanceNearby(string unit, int distance) => F("glance.nearby", unit, distance);
        public static string EnemyNumbered(string name, int number) => F("run.enemy_numbered", name, number);
        public static string RunShield(string amount) => F("run.shield", amount);
        public static string RunPartySlot(int index) => F("run.party_slot", index);
        public static string RunReserveSlot(int index) => F("run.reserve_slot", index);
        public static string RunSlotEmpty => Get("run.slot_empty");
        public static string RunItemSlotEmpty => Get("run.item_slot_empty");
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
        public static string ResultStats => Get("result.stats");
        public static string ResultTracker => Get("result.tracker");
        /// <summary>The tracker mode's name: 0 damage dealt, 1 damage taken, 2 healing done.</summary>
        public static string ResultTrackerMode(int mode)
            => Get(mode == 0 ? "result.damage_dealt" : mode == 1 ? "result.damage_taken" : "result.healing_done");
        public static string ResultStatusApplied => Get("result.status_applied");
        public static string ResultPreviousCombat => Get("result.previous_combat");
        public static string ResultNextCombat => Get("result.next_combat");
        public static string ResultLeaderboard => Get("result.leaderboard");
        public static string ResultVersus(string heroes, string enemies) => F("result.versus", heroes, enemies);
        public static string ResultEntries => Get("result.entries");
        public static string ResultEntry(string rank, string name, string floor) => F("result.entry", rank, name, floor);
        public static string ResultReset => Get("result.reset");
        public static string ScreenEnd => Get("screen.end");
        public static string EndVictory => Get("end.victory");
        public static string EndDefeat => Get("end.defeat");
        public static string EndInfo => Get("end.info");
        public static string EndFloor => Get("end.floor");
        public static string EndRank => Get("end.rank");
        public static string EndRankChange => Get("end.rank_change");
        public static string EndTopPercent => Get("end.top_percent");
        public static string EndHeroes => Get("end.heroes");
        public static string EndBackup => Get("end.backup");
        public static string EndNoItems => Get("end.no_items");
        public static string EndContinue => Get("end.continue");
        public static string EndNoHighlights => Get("end.no_highlights");
        public static string ScreenProgression => Get("screen.progression");
        public static string ProgressionXp(int xp, int level) => F("progression.xp", xp, level);
        public static string ProgressionMilestones => Get("progression.milestones");
        public static string ProgressionThreshold(string xp) => F("progression.threshold", xp);
        public static string ProgressionLocked => Get("progression.locked");
        public static string ProgressionUnlocked => Get("progression.unlocked");
        public static string ProgressionNew => Get("progression.new");
        public static string ScreenDifficulty => Get("screen.difficulty");
        public static string DifficultyLevels => Get("difficulty.levels");
        public static string DifficultyModifiers => Get("difficulty.modifiers");
        public static string DifficultyStreak => Get("difficulty.streak");
        public static string DifficultyTierRank(string name, string rank) => F("difficulty.tier_rank", name, rank);
        public static string HeroesCounters => Get("heroes.counters");
        public static string HeroesClose => Get("heroes.close");
        public static string HeroesNone => Get("heroes.none");
        public static string ScreenComic => Get("screen.comic");
        public static string ComicContinue => Get("comic.continue");
        public static string ScreenModMenu => Get("screen.mod_menu");
        public static string ModSettings => Get("mod.settings");
        public static string ModKeyHelp => Get("mod.key_help");
        public static string ModClose => Get("mod.close");
        public static string ModSpeakPositions => Get("mod.speak_positions");
        public static string ModFocusOnLaunch => Get("mod.focus_on_launch");
        public static string ModKeyCategory(string category) => F("mod.key_category", category);
        public static string RunSidebar => Get("run.sidebar");
        public static string RunDamageTracker => Get("run.damage_tracker");
        public static string RunChallenge => Get("run.challenge");
        public static string RunEvents => Get("run.events");
        public static string BattleDamage(string unit, int amount) => F("battle.damage", unit, amount);
        public static string BattleCrit(string unit, int amount) => F("battle.crit", unit, amount);
        public static string BattleHealed(string unit, int amount) => F("battle.healed", unit, amount);
        public static string BattleStatus(string unit, string status, int stacks) => F("battle.status", unit, status, stacks);
        public static string BattleStatusGone(string unit, string status) => F("battle.status_gone", unit, status);
        public static string BattleCast(string unit, string ability) => F("battle.cast", unit, ability);
        public static string BattleAbility => Get("battle.ability");
        public static string BattleDefeated(string unit) => F("battle.defeated", unit);
        /// <summary>A status icon's name from the game's status type name ("Poison"); the type name itself when unknown.</summary>
        public static string Status(string typeName)
        {
            string key = "status." + (typeName ?? "").ToLowerInvariant();
            return Has(key) ? Get(key) : typeName;
        }
        public static string ScreenCompendium => Get("screen.compendium");
        public static string CompendiumSections => Get("compendium.sections");
        public static string CompendiumOverview => Get("compendium.overview");
        public static string CompendiumFilters => Get("compendium.filters");
        public static string CompendiumSearch(string text) => F("compendium.search", text);
        public static string CompendiumSearchEmpty => Get("compendium.search_empty");
        public static string CompendiumClassFilter => Get("compendium.class_filter");
        public static string CompendiumClearFilter => Get("compendium.clear_filter");
        public static string CompendiumHeroes => Get("compendium.heroes");
        public static string CompendiumTrophies(int earned, int total) => F("compendium.trophies", earned, total);
        public static string CompendiumClasses => Get("compendium.classes");
        public static string CompendiumRanks => Get("compendium.ranks");
        public static string CompendiumRank(int n) => F("compendium.rank_n", n);
        public static string CompendiumUpgrades => Get("compendium.upgrades");
        public static string HeroItems => Get("hero.items");
        public static string HeroNoItems => Get("hero.no_items");
        public static string RunEquipTo(string item) => F("run.equip_to", item);
        public static string RunEquipped(string item, string hero) => F("run.equipped", item, hero);
        public static string RunEquipFailed => Get("run.equip_failed");
        public static string RunSlotsFull => Get("run.slots_full");
        public static string RunHeroActions(string hero) => F("run.hero_actions", hero);
        public static string RunInspect => Get("run.inspect");
        public static string RunInspectFailed => Get("run.inspect_failed");
        public static string RunUnequip(string item) => F("run.unequip", item);
        public static string RunSell(string name) => F("run.sell", name);
        public static string RunSellFor(string name, int shards) => F("run.sell_for", name, shards);
        public static string RunSold(string name) => F("run.sold", name);
        public static string RunUnequipped(string item) => F("run.unequipped", item);
        public static string RunNoHeroes => Get("run.no_heroes");
        public static string RunGrid => Get("run.grid");
        public static string RunCellEmpty => Get("run.cell_empty");
        public static string RunCellPos(int column, int row) => F("run.cell_pos", column, row);
        public static string RunBoardHeroes => Get("run.board_heroes");
        public static string RunBoardEnemies => Get("run.board_enemies");
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
        public static string ShopFrozen => Get("shop.frozen");
        public static string ShopUnfrozen => Get("shop.unfrozen");
        public static string ShopThreat => Get("shop.threat");
        public static string ScreenCrossroads => Get("screen.crossroads");
        public static string CrossroadsPaths => Get("crossroads.paths");
        public static string ScreenEvent => Get("screen.event");
        public static string EventOutcome => Get("event.outcome");
        public static string ScreenPicker => Get("screen.picker");
        public static string PickerChoices => Get("picker.choices");
        public static string PickerNoChoices => Get("picker.no_choices");
        public static string ScreenRelicPicker => Get("screen.relic_picker");
        public static string ScreenTutorial => Get("screen.tutorial");
    }
}
