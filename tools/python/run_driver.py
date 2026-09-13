"""Play a run forward through the mod's own navigation until a stage you want to look at.

    uv run python tools/python/run_driver.py [--until STAGE] [--buy] [--max N]

Loops over the screens the mod knows and presses what advances them (Escape on a result, Proceed
in the shop, the first path at a crossroads, the first choice then Proceed in an event, a
specialization in a rank-up picker, Escape on the Heroes panel, Fight at placement), reading
`dev.py nav` between steps. Stops when it reaches --until (placement, result, shop, crossroads,
event, picker, heroes, end) or a screen it does not know, so new screens surface for inspection.
--buy buys the first shop hero each visit (ranking duplicates up exercises the picker).
"""
from __future__ import annotations

import argparse
import re
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import dev  # noqa: E402

PLACING = ("UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Ember.Scopes.Battle.UI.BattleFlow."
           "BattleFlowUIStateController>())?.TryCast<Ember.Scopes.Battle.UI.BattleFlow.BattleFlowUIStateController>()"
           "?._placementParent?.activeInHierarchy == true")
RESULT_OR_END = ("UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Ember.Scopes.Battle.UI.BattleFlow."
                 "BattleFlowUIStateController>())?.TryCast<Ember.Scopes.Battle.UI.BattleFlow.BattleFlowUIStateController>()"
                 "?._resultParent?.activeInHierarchy == true || UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime."
                 "Il2CppType.Of<Ember.Scopes.Battle.EndScreen.EndScreenController>()) != null")
FIGHT = ('var f = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Ember.Scopes.Battle.Board.Controllers.'
         'BoardController>())?.TryCast<Ember.Scopes.Battle.Board.Controllers.BoardController>(); var btn = f?._startBattleButton; '
         'if (btn == null || !btn.interactable) return "no fight button"; btn.onClick.Invoke(); return "fight";')
PICKER_DONE = ("(UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Ember.Scopes.GameRun.UI.Navigation."
               "NavigationUIController>())?.TryCast<Ember.Scopes.GameRun.UI.Navigation.NavigationUIController>()"
               "?._specializationPickerView?._continueButton?.gameObject.activeInHierarchy == true) || "
               "(UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Ember.Scopes.GameRun.UI.Navigation."
               "NavigationUIController>())?.TryCast<Ember.Scopes.GameRun.UI.Navigation.NavigationUIController>()"
               "?._rankModifierPickerView?._continueButton?.gameObject.activeInHierarchy == true)")

STAGE_KEYS = {"placement": "gamerun", "result": "gamerun.result", "shop": "gamerun.shop", "crossroads": "gamerun.crossroads",
              "event": "gamerun.event", "picker": "gamerun.picker", "heroes": "gamerun.heroes", "end": "gamerun.end"}


def screen() -> str:
    m = re.search(r"^screen: (\S+)", dev.call("/nav"), re.M)
    return m.group(1) if m else ""


def nav_has(pattern: str) -> bool:
    return re.search(pattern, dev.call("/nav")) is not None


def press(verb: str) -> str:
    return dev.call("/input", body=verb).strip()


def wait_change(before: str, seconds: float = 60) -> str:
    deadline = time.time() + seconds
    while time.time() < deadline:
        time.sleep(1.5)
        now = screen()
        if now != before:
            return now
    return before


def placing() -> bool:
    return "True" in dev.call("/eval", body="return (" + PLACING + ").ToString();")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--until", choices=sorted(STAGE_KEYS), default="placement")
    ap.add_argument("--buy", action="store_true")
    ap.add_argument("--max", type=int, default=20)
    args = ap.parse_args()
    target = STAGE_KEYS[args.until]

    for step in range(1, args.max + 1):
        s = screen()
        if s == "(none)":
            # Between two panels no screen is active (the run HUD is inactive outside placement and
            # fights): wait for the next one rather than treat the gap as an unknown screen.
            s = wait_change(s, seconds=20)
            if s == "(none)":
                print("  no screen active for 20 s"); return 5
        print(f"[{step}] {s}")
        if s == target and (target != "gamerun" or placing()):
            print(f"  at {args.until}")
            return 0
        if s == "gamerun.result":
            press("ui.back"); wait_change(s)
        elif s == "gamerun.shop":
            if args.buy:
                press("ui.home"); press("ui.activate"); time.sleep(1)
            press("ui.back"); wait_change(s)
        elif s == "gamerun.crossroads":
            press("ui.activate"); wait_change(s)
        elif s == "gamerun.event":
            if nav_has(r"event:choice:"):
                # One group: Down from the story text is the first choice. The choices are not
                # interactable while the panel animates in, so give it a moment before Enter.
                time.sleep(3); press("ui.down"); press("ui.activate"); time.sleep(4)
            if nav_has(r"event:proceed"):
                press("ui.back"); wait_change(s)
            else:
                print("  event with nothing to press"); return 2
        elif s == "gamerun.picker":
            press("ui.next"); press("ui.activate")
            dev.call("/wait", body=PICKER_DONE, query={"timeout": 25000}, timeout=40)
            time.sleep(1); press("ui.back"); wait_change(s)
        elif s == "gamerun.heroes":
            press("ui.back"); wait_change(s)
        elif s == "gamerun":
            ok = False
            for _ in range(6):
                if placing():
                    ok = True; break
                if screen() != s:
                    break
                time.sleep(1.5)
            if screen() != s:
                continue
            if not ok:
                print("  run HUD without placement: something the driver does not know is showing"); return 3
            print("  " + dev.call("/eval", body=FIGHT).strip())
            time.sleep(3)
            dev.call("/wait", body=RESULT_OR_END, query={"timeout": 90000}, timeout=100)
        else:
            print(f"  unknown screen: {s}"); return 4
    print("step limit reached")
    return 1


if __name__ == "__main__":
    sys.exit(main())
