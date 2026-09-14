using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Application.Compendium;
using Ember.Scopes.Application.UI.Tooltips;
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
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => HeroTitle(hero)) },
                SearchText = () => hero._nameText != null ? hero._nameText.text : null,
                Details = () => masteryTarget != null ? TooltipReader.Lines(masteryTarget) : null,
            });

            // Abilities / Gameplay / Personality.
            var tabs = hero._tabs;
            if (tabs != null)
                foreach (var toggle in tabs.GetComponentsInChildren<Toggle>(false))
                {
                    if (!GameNodes.IsShown(toggle)) continue;
                    var t = toggle;
                    b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":tab:" + t.GetInstanceID()), GameNodes.Tab(t, () => TabCaption(t)));
                }

            // The abilities shown (the signature ability and the specializations).
            int n = 0;
            foreach (var ability in hero.GetComponentsInChildren<HeroAbilityCompendiumView>(false))
            {
                if (ability == null || !ability.gameObject.activeInHierarchy) continue;
                var a = ability;
                b.AddItem(ControlId.Structural("compendium:detail:" + hero.GetInstanceID() + ":ability:" + n++), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => AbilityText(a)) },
                    SearchText = () => AbilityText(a),
                    Details = () => AbilityTooltip(a),
                });
            }

            // The other tabs' text (gameplay notes, the personality blurb, guild).
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":gameplay", hero._gameplayText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":alias", hero._aliasText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":subtitle", hero._subtitleText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":guild", hero._guildNameText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":currentguild", hero._currentGuildText);
            AddText(b, "compendium:detail:" + hero.GetInstanceID() + ":motivation", hero._motivationText);

            b.PopContext();
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

        // The ability as buffer lines: its tooltip, else its shown text as the one line.
        private static IEnumerable<string> AbilityTooltip(HeroAbilityCompendiumView ability)
        {
            var target = ability.GetComponentInChildren<TooltipRaycastTarget>(false);
            var lines = target != null ? TooltipReader.Lines(target) : null;
            return lines != null && lines.Count > 0 ? lines : GameNodes.Lines(AbilityText(ability));
        }

        // ---- a class's detail: its rank upgrades ----

        private static void AddClassDetail(GraphBuilder b, HeroClassInfoCompendiumView cls)
        {
            var modifiers = new List<RankModifierCompendiumView>();
            foreach (var m in cls.GetComponentsInChildren<RankModifierCompendiumView>(false))
                if (m != null && m.gameObject.activeInHierarchy) modifiers.Add(m);
            if (modifiers.Count == 0) return;
            b.BeginStop("class:" + cls.GetInstanceID());
            b.PushContext(Strings.CompendiumUpgrades, Strings.RoleList);
            foreach (var modifier in modifiers)
            {
                var m = modifier;
                b.AddItem(ControlId.Structural("compendium:upgrade:" + m.GetInstanceID()), GameNodes.Text(() =>
                {
                    string title = m._title != null ? m._title.text : null;
                    string desc = m._description != null ? m._description.text : null;
                    return string.IsNullOrEmpty(desc) ? title : title + ", " + desc;
                }));
            }
            b.PopContext();
        }

        // ---- helpers ----

        private static string TabCaption(Toggle toggle)
        {
            var tmp = toggle.GetComponentInChildren<TMP_Text>(true);
            return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : null;
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
