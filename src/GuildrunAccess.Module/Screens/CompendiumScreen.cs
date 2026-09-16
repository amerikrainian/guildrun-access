using System.Collections.Generic;
using System.Text;
using Ember.Balancing.Sheets.Abilities.ActiveAbilities;
using Ember.Balancing.Sheets.Abilities.PassiveAbilities;
using Ember.Balancing.Sheets.Characters;
using Ember.Balancing.Sheets.Characters.Specializations;
using Ember.Balancing.Sheets.RankModifiers;
using Ember.Scopes.Application.Compendium;
using Ember.Scopes.Application.Mastery.UI;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.Application.UI.Tooltips.Sources;
using gg.leyline.balancing;
using gg.leyline.balancing.Data;
using Ember.Utilities.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.GameRun;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The compendium (<see cref="CompendiumUIController"/>, opened from a hero card): its tabs
    /// (Overview, Heroes, Class Upgrades), the filters (search text, class dropdown, mastery switches),
    /// the icon list of the current tab (a hero or class per icon; Enter selects it, which scrolls the
    /// detail into view), and the detail the game shows for the selection: a hero's name, tags,
    /// mastery, its Abilities / Gameplay / Personality tabs and their text, or a class's rank
    /// upgrades. Everything is read from the live views; Escape presses Back.
    /// </summary>
    public sealed class CompendiumScreen : Screen
    {
        public override string Key => "app.compendium";
        public override int Layer => 22;
        public override bool Exclusive => true;

        private static CompendiumUIController Compendium => GameScopes.Controller<CompendiumUIController>();

        private CompendiumUIController Panel()
        {
            var c = Compendium;
            if (c == null || !c.gameObject.activeInHierarchy) return null;
            var root = c._compendiumUIRoot;
            return root == null || root.activeInHierarchy ? c : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var c = Panel();
            if (c == null) return;
            b.PushContext(Strings.ScreenCompendium, null, positions: false);

            // The main tabs.
            b.BeginStop("tabs");
            b.PushContext(Strings.CompendiumSections, null, positions: true);
            foreach (var toggle in TabToggles(c._tabView))
            {
                var t = toggle;
                b.AddItem(ControlId.Structural("compendium:tab:" + t.GetInstanceID()), GameNodes.Tab(t, () => TabCaption(t) ?? Strings.CompendiumOverview));
            }
            b.PopContext();

            // The filters.
            b.BeginStop("filters");
            b.PushContext(Strings.CompendiumFilters, null, positions: false);
            var search = c._searchInputField;
            if (search != null && search.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("compendium:search"), GameNodes.Text(() => Strings.CompendiumSearch(string.IsNullOrWhiteSpace(search.text) ? Strings.CompendiumSearchEmpty : search.text)));
            if (c._classFilterDropdown != null && c._classFilterDropdown.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("compendium:class"), GameNodes.Dropdown(c._classFilterDropdown, () => Strings.CompendiumClassFilter));
            if (GameNodes.IsShown(c._classFilterClearButton))
                b.AddItem(ControlId.Structural("compendium:clear"), GameNodes.Button(c._classFilterClearButton, () => Strings.CompendiumClearFilter));
            if (GameNodes.IsShown(c._masteryAllFilter))
                b.AddItem(ControlId.Structural("compendium:mastery:all"), GameNodes.Radio(c._masteryAllFilter));
            if (GameNodes.IsShown(c._masteryIncompleteFilter))
                b.AddItem(ControlId.Structural("compendium:mastery:incomplete"), GameNodes.Radio(c._masteryIncompleteFilter));
            AddText(b, "compendium:mastery:overview", c._heroOverviewMasteryProgressText != null ? c._heroOverviewMasteryProgressText.GetComponent<TMP_Text>() : null);
            AddText(b, "compendium:mastery:details", c._heroDetailsMasteryProgressText != null ? c._heroDetailsMasteryProgressText.GetComponent<TMP_Text>() : null);
            b.PopContext();

            // The icons of whichever list shows: heroes (overview or details) or classes.
            AddIcons(b, c._heroOverviewPortraitContainer, "compendium:overview", Strings.CompendiumHeroes);
            AddIcons(b, c._heroIconContainer, "compendium:hero", Strings.CompendiumHeroes);
            AddIcons(b, c._classIconContainer, "compendium:class", Strings.CompendiumClasses);

            // The rank tabs and the selected hero's or class's detail.
            var rankTabs = c._rankTabView;
            if (rankTabs != null && rankTabs.gameObject.activeInHierarchy)
            {
                b.BeginStop("ranks");
                b.PushContext(Strings.CompendiumRanks, null, positions: true);
                int n = 0;
                foreach (var toggle in rankTabs.GetComponentsInChildren<Toggle>(false))
                {
                    if (!GameNodes.IsShown(toggle)) continue;
                    int index = n++;
                    var t = toggle;
                    b.AddItem(ControlId.Structural("compendium:rank:" + t.GetInstanceID()), GameNodes.Tab(t, () => TabCaption(t) ?? Strings.CompendiumRank(index + 1)));
                }
                b.PopContext();
            }
            var hero = CurrentHero(c);
            if (hero != null) AddHeroDetail(b, hero);
            var cls = CurrentClass(c);
            if (cls != null) AddClassDetail(b, cls);

            b.BeginStop("actions");
            var back = BackButton(c);
            if (back != null) b.AddItem(ControlId.Structural("compendium:back"), GameNodes.Button(back));

            b.PopContext();
        }

        // The tab view's own tabs, in order, shown ones only: its entries name the toggles (other
        // toggles live under the same transform: the mastery filters).
        private static List<Toggle> TabToggles(TabView view)
        {
            var list = new List<Toggle>();
            var entries = view != null ? view._toggleTabs : null;
            if (entries != null)
                foreach (var entry in entries)
                {
                    var toggle = entry != null ? entry.Toggle : null;
                    if (GameNodes.IsShown(toggle)) list.Add(toggle);
                }
            return list;
        }

        // The hero detail panel the carousel is snapped to. The game pools the panels (OSA): the one
        // just scrolled away stays active while it fades out, so the snapper's middle holder decides;
        // without a snapper, the first active panel.
        private static HeroInfoCompendiumView CurrentHero(CompendiumUIController c)
        {
            HeroInfoCompendiumView middle = null;
            try
            {
                float distance;
                var holder = c._heroSnapper8 != null ? c._heroSnapper8.GetMiddleVH(out distance) : null;
                var typed = holder != null ? holder.TryCast<HeroInfoViewsHolder>() : null;
                middle = typed != null ? typed.View : null;
            }
            catch (System.Exception e) { CoreLog.Warning("Compendium: middle hero: " + e.Message); }
            if (middle != null && middle.gameObject.activeInHierarchy) return middle;
            foreach (var view in c.GetComponentsInChildren<HeroInfoCompendiumView>(false))
                if (view != null && view.gameObject.activeInHierarchy) return view;
            return null;
        }

        private static HeroClassInfoCompendiumView CurrentClass(CompendiumUIController c)
        {
            HeroClassInfoCompendiumView middle = null;
            try
            {
                float distance;
                var holder = c._classSnapper8 != null ? c._classSnapper8.GetMiddleVH(out distance) : null;
                var typed = holder != null ? holder.TryCast<ClassInfoViewsHolder>() : null;
                middle = typed != null ? typed.View : null;
            }
            catch (System.Exception e) { CoreLog.Warning("Compendium: middle class: " + e.Message); }
            if (middle != null && middle.gameObject.activeInHierarchy) return middle;
            foreach (var view in c.GetComponentsInChildren<HeroClassInfoCompendiumView>(false))
                if (view != null && view.gameObject.activeInHierarchy) return view;
            return null;
        }

        // ---- icons ----

        private static void AddIcons(GraphBuilder b, Transform container, string keyPrefix, string label)
        {
            if (container == null || !container.gameObject.activeInHierarchy) return;
            var icons = new List<IconCompendiumView>();
            foreach (var icon in container.GetComponentsInChildren<IconCompendiumView>(false))
                if (icon != null && icon.gameObject.activeInHierarchy) icons.Add(icon);
            if (icons.Count == 0) return;
            b.BeginStop(keyPrefix);
            b.PushContext(label, Strings.RoleList);
            foreach (var icon in icons)
            {
                var i = icon;
                b.AddItem(ControlId.Structural(keyPrefix + ":" + i.GetInstanceID()), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => IconName(i)),
                        new NodeAnnouncement(() => Trophies(i._masteryView), live: true, kind: AnnouncementKinds.Value),
                        GameNodes.SelectedPart(() => i._selectedImage != null && i._selectedImage.activeInHierarchy),
                        GameNodes.DisabledPart(() => i._button == null || i._button.interactable),
                    },
                    SearchText = () => IconName(i),
                    OnActivate = () => { if (i._button != null && i._button.interactable) i._button.onClick.Invoke(); },
                    Details = () => GameNodes.Lines(MasteryTooltip(i)),
                });
            }
            b.PopContext();
        }

        // The icon's caption when it shows one (the overview), else the hero's or class's own name.
        private static string IconName(IconCompendiumView icon)
        {
            var text = icon._text;
            string name = text != null && text.gameObject.activeInHierarchy ? text.text : null;
            if (string.IsNullOrWhiteSpace(name)) name = RunData.NameOf(icon._heroEntry) ?? RunData.NameOf(icon._classEntry);
            return string.IsNullOrWhiteSpace(name) ? icon.gameObject.name : name;
        }

        // "1 of 3 trophies": the mastery stars an icon shows (a star per specialization; the locked
        // sprite is the empty one). The stars' own tooltip is empty in this build, so the stars decide.
        private static string Trophies(HeroMasteryView mastery)
        {
            if (mastery == null || !mastery.gameObject.activeInHierarchy) return null;
            var images = mastery._specializationMasteryImages;
            if (images == null || images.Length == 0) return null;
            var locked = mastery._lockedSprite;
            int total = 0, earned = 0;
            foreach (var image in images)
            {
                if (image == null || !image.gameObject.activeInHierarchy) continue;
                total++;
                if (image.sprite != null && (locked == null || image.sprite.Pointer != locked.Pointer)) earned++;
            }
            return total > 0 ? Strings.CompendiumTrophies(earned, total) : null;
        }

        private static List<string> MasteryTooltip(IconCompendiumView icon)
        {
            var target = icon.GetComponentInChildren<TooltipRaycastTarget>(false);
            return target != null ? TooltipReader.Lines(target) : null;
        }

        // ---- a hero's detail ----

        private static void AddHeroDetail(GraphBuilder b, HeroInfoCompendiumView hero)
        {
            b.BeginStop("detail:" + hero.GetInstanceID());
            b.PushContext(HeroTitle(hero), null, positions: false);

            var mastery = hero._heroMasteryView;
            var masteryTarget = mastery != null ? mastery.GetComponent<TooltipRaycastTarget>() : null;
            b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":name"), new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => HeroTitle(hero)),
                    new NodeAnnouncement(() => Trophies(hero._heroMasteryView), live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = () => hero._nameText != null ? hero._nameText.text : null,
                Details = () => masteryTarget != null ? TooltipReader.Lines(masteryTarget) : null,
            });

            // Abilities / Gameplay / Personality: a tab selects on landing, so the selected tab's page
            // (the only one shown) is listed right under its tab, where Down reaches it; listed after
            // the whole strip it would sit past the tabs that switch the page away on the way down.
            var tabs = hero._tabs;
            bool contentAdded = false;
            if (tabs != null)
                foreach (var toggle in tabs.GetComponentsInChildren<Toggle>(false))
                {
                    if (!GameNodes.IsShown(toggle)) continue;
                    var t = toggle;
                    b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":tab:" + t.GetInstanceID()), GameNodes.Tab(t, () => TabCaption(t)));
                    if (t.isOn && !contentAdded)
                    {
                        AddTabContent(b, hero);
                        contentAdded = true;
                    }
                }
            if (!contentAdded) AddTabContent(b, hero);

            b.PopContext();
        }

        // The selected tab's page: the ability blocks, or the gameplay and personality texts,
        // whichever the game shows.
        private static void AddTabContent(GraphBuilder b, HeroInfoCompendiumView hero)
        {
            // The abilities shown (the signature ability and the specializations), each with the
            // tooltip the game composes for its entry: the compendium's blocks carry no tooltip target,
            // so the entries come from the hero and the controller's specialization table.
            int n = 0;
            var controller = GameScopes.Controller<CompendiumUIController>();
            var specializations = Specializations(controller, hero);
            var abilities = new List<HeroAbilityCompendiumView>();
            if (hero._signatureAbilityView != null) abilities.Add(hero._signatureAbilityView);
            if (hero._specializationViews != null) abilities.AddRange(hero._specializationViews);
            int specIndex = 0;
            foreach (var ability in abilities)
            {
                if (ability == null || !ability.gameObject.activeInHierarchy) continue;
                var a = ability;
                bool signature = ReferenceEquals(a, hero._signatureAbilityView) || a.Pointer == (hero._signatureAbilityView != null ? hero._signatureAbilityView.Pointer : System.IntPtr.Zero);
                var specialization = !signature && specializations != null && specIndex < specializations.Count ? specializations[specIndex++] : null;
                b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":ability:" + n++), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => AbilityText(a)) },
                    SearchText = () => AbilityText(a),
                    Details = () => AbilityTooltip(a, controller, hero, signature ? null : specialization, signature),
                });
            }

            // The other tabs' text (gameplay notes, the personality blurb, guild). The guild line
            // carries the guild banner's tooltip (its motto); the role and motivation texts read under
            // the headers the panel shows over them.
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":gameplay", hero._gameplayText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":alias", hero._aliasText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":subtitle", hero._subtitleText);
            var guild = hero._guildNameText;
            if (guild != null && guild.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(guild.text))
            {
                var guildTip = hero._guildTooltipRaycastTarget;
                b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":guild"), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => guild.text) },
                    SearchText = () => guild.text,
                    Details = () => guildTip != null ? TooltipReader.Lines(guildTip) : null,
                });
            }
            AddCaptioned(b, "compendium:detail:" + hero.GetInstanceID() + ":currentguild", hero._currentGuildText, hero._guildTooltipRaycastTarget);
            AddCaptioned(b, "compendium:detail:" + hero.GetInstanceID() + ":motivation", hero._motivationText);
        }

        // "Aria, Mage, Frost, Burn".
        private static string HeroTitle(HeroInfoCompendiumView hero)
        {
            var sb = new StringBuilder();
            if (hero._nameText != null) sb.Append(hero._nameText.text);
            foreach (var group in new[] { hero._classViews, hero._tagViews })
            {
                if (group == null) continue;
                foreach (var tag in group)
                {
                    if (tag == null || !tag.gameObject.activeInHierarchy) continue;
                    var text = tag.GetComponentInChildren<TMP_Text>(false);
                    if (text == null || string.IsNullOrWhiteSpace(text.text)) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(text.text);
                }
            }
            return sb.Length > 0 ? sb.ToString() : Strings.CompendiumHeroes;
        }

        // An ability block as shown: title, kind, mana, description; "locked" when overlaid.
        private static string AbilityText(HeroAbilityCompendiumView ability)
        {
            var sb = new StringBuilder();
            foreach (var tmp in ability.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(tmp.text.Trim());
            }
            var locked = ability._lockedOverlay;
            if (locked != null && locked.activeInHierarchy) sb.Append(", ").Append(Strings.ProgressionLocked);
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // The ability as buffer lines. A specialization: the game's tooltip for its entry (title, kind,
        // text, keyword definitions). The signature ability: its shown text, then the definitions of
        // the keywords its raw text uses (the game's active-ability tooltip source cannot populate
        // without a specialization: it dereferences one). Else the shown text as the one line.
        private static IEnumerable<string> AbilityTooltip(HeroAbilityCompendiumView ability, CompendiumUIController c,
            HeroInfoCompendiumView hero, IHeroSpecializationEntry specialization, bool signature)
        {
            List<string> lines = null;
            try
            {
                if (signature)
                {
                    lines = new List<string>();
                    string shown = AbilityText(ability);
                    if (!string.IsNullOrEmpty(shown)) lines.Add(shown);
                    lines.AddRange(TooltipReader.KeywordDefinitions(TooltipReader.RawDescription(SignatureEntry(c, hero))));
                }
                else
                {
                    var source = AbilitySource(c, hero, specialization, false);
                    if (source != null) lines = TooltipReader.Lines(source.Cast<ITooltipSource>());
                }
            }
            catch (System.Exception e) { CoreLog.Warning("Compendium: ability tooltip: " + e.Message); }
            return lines != null && lines.Count > 0 ? lines : GameNodes.Lines(AbilityText(ability));
        }

        // The hero's signature ability entry: the character's active ability, else its passive one.
        private static Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase SignatureEntry(CompendiumUIController c, HeroInfoCompendiumView hero)
        {
            var balancing = Balancing(c);
            var character = hero != null && hero.HeroEntry != null ? hero.HeroEntry.TryCast<ICharacterEntry>() : null;
            if (balancing == null || character == null) return null;
            var active = ResolveActive(balancing, character.ActiveAbilityRef);
            if (active != null) return active;
            return ResolvePassive(balancing, character.PassiveAbilityRef);
        }

        // The balancing the compendium reads through (its hero list adapter holds it).
        private static IBalancing Balancing(CompendiumUIController c)
            => c != null && c._heroInfoAdapter != null ? c._heroInfoAdapter.Balancing : null;

        // The specializations the controller keeps for a hero, by the hero's sequential id.
        private static List<IHeroSpecializationEntry> Specializations(CompendiumUIController c, HeroInfoCompendiumView hero)
        {
            try
            {
                var entry = hero != null && hero.HeroEntry != null ? hero.HeroEntry.TryCast<IBalancingEntry>() : null;
                var table = c != null ? c._specializationsByHeroId : null;
                if (entry == null || table == null) return null;
                Il2CppSystem.Collections.Generic.List<IHeroSpecializationEntry> list;
                if (!table.TryGetValue(entry.Id.SequentialId, out list) || list == null) return null;
                var result = new List<IHeroSpecializationEntry>(list.Count);
                for (int i = 0; i < list.Count; i++) result.Add(list[i]);
                return result;
            }
            catch (System.Exception e)
            {
                CoreLog.Warning("Compendium: specializations: " + e.Message);
                return null;
            }
        }

        // A tooltip source for the hero's signature ability (the character entry's active or passive
        // ability) or for one specialization (its active or passive ability), as the game builds them.
        // A reference that is empty (a passive-only specialization has no active ability) throws in
        // the balancing lookup: that miss is expected, so the lookups swallow it.
        private static AbilityEntryTooltipSource AbilitySource(CompendiumUIController c, HeroInfoCompendiumView hero,
            IHeroSpecializationEntry specialization, bool signature)
        {
            var balancing = Balancing(c);
            var character = hero != null && hero.HeroEntry != null ? hero.HeroEntry.TryCast<ICharacterEntry>() : null;
            if (balancing == null || character == null) return null;
            if (!signature && specialization == null) return null;
            var source = new AbilityEntryTooltipSource();
            var active = ResolveActive(balancing, signature ? character.ActiveAbilityRef : specialization.ActiveAbilityRef);
            if (active != null)
            {
                source.SetActiveAbility(character, signature ? null : specialization, active, false);
                return source;
            }
            var passive = ResolvePassive(balancing, signature ? character.PassiveAbilityRef : specialization.PassiveAbilityRef);
            if (passive != null)
            {
                source.SetPassiveAbility(character, signature ? null : specialization, passive, false);
                return source;
            }
            return null;
        }

        private static IActiveAbilityEntry ResolveActive(IBalancing balancing, BalancingRef<IActiveAbilityEntry> reference)
        {
            try { return balancing.Get<IActiveAbilityEntry>(reference); }
            catch (System.Exception) { return null; } // an empty reference: nothing there
        }

        private static IPassiveAbilityEntry ResolvePassive(IBalancing balancing, BalancingRef<IPassiveAbilityEntry> reference)
        {
            try { return balancing.Get<IPassiveAbilityEntry>(reference); }
            catch (System.Exception) { return null; }
        }

        // ---- a class's detail: its rank upgrades ----

        private static void AddClassDetail(GraphBuilder b, HeroClassInfoCompendiumView cls)
        {
            var modifiers = new List<RankModifierCompendiumView>();
            foreach (var m in cls.GetComponentsInChildren<RankModifierCompendiumView>(false))
                if (m != null && m.gameObject.activeInHierarchy) modifiers.Add(m);
            if (modifiers.Count == 0) return;
            // The entry behind each row, for its tooltip (the game's rank-modifier tooltip: the
            // description, then the definitions of the keywords it uses).
            var entries = new Dictionary<System.IntPtr, IRankModifierEntry>();
            try
            {
                var pairs = cls._rankModifiers;
                for (int i = 0; pairs != null && i < pairs.Count; i++)
                {
                    var pair = pairs[i];
                    if (pair.Item2 != null && pair.Item1 != null) entries[pair.Item2.Pointer] = pair.Item1;
                }
            }
            catch (System.Exception e) { CoreLog.Warning("Compendium: rank modifiers: " + e.Message); }

            b.BeginStop("class:" + cls.GetInstanceID());
            b.PushContext(Strings.CompendiumUpgrades, Strings.RoleList);
            foreach (var modifier in modifiers)
            {
                var m = modifier;
                IRankModifierEntry entry;
                entries.TryGetValue(m.Pointer, out entry);
                var node = GameNodes.Text(() =>
                {
                    string title = m._title != null ? m._title.text : null;
                    string desc = m._description != null ? m._description.text : null;
                    return string.IsNullOrEmpty(desc) ? title : title + ", " + desc;
                });
                node.Details = () => UpgradeTooltip(m, entry);
                b.AddItem(ControlId.Structural("compendium:upgrade:" + m.GetInstanceID()), node);
            }
            b.PopContext();
        }

        // A class upgrade as buffer lines: the game's tooltip for its entry, else its shown text.
        private static IEnumerable<string> UpgradeTooltip(RankModifierCompendiumView view, IRankModifierEntry entry)
        {
            List<string> lines = null;
            if (entry != null)
            {
                try
                {
                    var source = new AbilityEntryTooltipSource();
                    source.SetRankModifier(null, entry, false);
                    lines = TooltipReader.Lines(source.Cast<ITooltipSource>());
                }
                catch (System.Exception e) { CoreLog.Warning("Compendium: upgrade tooltip: " + e.Message); }
            }
            if (lines != null && lines.Count > 0) return lines;
            string title = view._title != null ? view._title.text : null;
            string desc = view._description != null ? view._description.text : null;
            return GameNodes.Lines(title, desc);
        }

        // ---- helpers ----

        private static string TabCaption(Toggle toggle)
        {
            var tmp = toggle.GetComponentInChildren<TMP_Text>(true);
            return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : null;
        }

        // A text under a header the panel draws over it ("Motivation to switch to your guild"): the
        // header is the sibling named Header of the text's parent; the line reads "header: text".
        private static void AddCaptioned(GraphBuilder b, string key, TMP_Text text, TooltipRaycastTarget tooltip = null)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            TMP_Text header = null;
            var parent = text.transform.parent;
            if (parent != null)
            {
                var head = parent.Find("Header");
                header = head != null ? head.GetComponent<TMP_Text>() : null;
            }
            var h = header;
            var node = GameNodes.Text(() =>
            {
                string caption = h != null && h.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(h.text) ? h.text.Trim() : null;
                string body = text.text.Trim();
                if (caption == null) return body;
                return caption.EndsWith(":") ? caption + " " + body : caption + ": " + body;
            });
            if (tooltip != null) node.Details = () => TooltipReader.Lines(tooltip);
            b.AddItem(ControlId.Structural(key), node);
        }

        private static void AddText(GraphBuilder b, string key, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => text.text));
        }

        private static Button BackButton(CompendiumUIController c)
        {
            foreach (var button in c.GetComponentsInChildren<Button>(false))
                if (button != null && button.gameObject.name == "Back" && GameNodes.IsShown(button)) return button;
            return null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Panel();
                var back = c != null ? BackButton(c) : null;
                if (back != null && back.interactable) back.onClick.Invoke();
                else if (c != null) c.HidePanel();
            });
        }
    }
}
