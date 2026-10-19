"""Checks that the game members the mod uses directly are public in the real assembly_valheim.dll.

The mod compiles against a publicized copy of the game where everything is public, so a private member
compiles fine but can throw FieldAccessException / MethodAccessException in game. Private members must be
read through GameAccess (reflection) instead, and listed under REFLECTION below.

Usage: python3 tools/check_game_members.py <game dll> [<game dll> ...]   (needs ilspycmd on PATH)
Pass assembly_valheim.dll and the game's other assembly_*.dll files; each type is looked up in all of them.
"""
import re
import subprocess
import sys

# type -> members the mod accesses directly. For a member that may be declared on a base class,
# list it under each candidate type; it passes if any of them declares it public.
DIRECT = {
    "CraftingStation": ["m_name", "m_rangeBuild", "m_extraRangePerLevel", "m_areaMarker", "GetLevel", "GetHoverText"],
    "CircleProjector": ["m_radius", "m_nrOfSegments"],
    "Player": ["m_localPlayer", "GetCurrentCraftingStation", "InPlaceMode|Character"],
    "Character": ["Message"],
    "ItemDrop": ["m_shared", "m_canBeReparied", "m_name"],
    "Recipe": ["m_craftingStation", "m_repairStation", "m_minStationLevel"],
    "ObjectDB": ["instance", "GetRecipe"],
    "Console": ["IsVisible"],
    "TextInput": ["IsVisible"],
    "Menu": ["IsVisible"],
    "Chat": ["instance", "HasFocus"],
    "Terminal": ["AddString"],
    "Localization": ["instance", "Localize"],
    "ShieldGenerator": ["m_minShieldRadius", "m_maxShieldRadius", "m_maxFuel", "GetHoverText"],
}

# Private members the mod reads through GameAccess; only checked to exist.
REFLECTION = {
    "CraftingStation": ["m_allStations"],
    "CircleProjector": ["m_segments"],
    "ShieldGenerator": ["m_instances", "GetFuel", "SetFuel"],
}


def decompile(dlls, type_name):
    for dll in dlls:
        result = subprocess.run(["ilspycmd", "-t", type_name, dll], capture_output=True, text=True)
        if result.returncode == 0 and re.search(r"\b(class|struct)\s+" + re.escape(type_name) + r"\b", result.stdout):
            return result.stdout
    return ""


def declarations(source, member):
    pattern = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*((?:public|private|protected|internal)[^;{=(]*\b" + re.escape(member) + r"\b)\s*(?:[;{=(]|$)", re.M)
    return [m.group(1) for m in pattern.finditer(source)]


def main():
    dlls = sys.argv[1:]
    cache = {}
    source = lambda t: cache[t] if t in cache else cache.setdefault(t, decompile(dlls, t))
    problems = []

    for type_name, members in DIRECT.items():
        for entry in members:
            member, *fallbacks = entry.split("|")
            found = []
            for candidate in [type_name] + fallbacks:
                found += declarations(source(candidate), member)
            if not found:
                problems.append(f"{type_name}.{member}: not found")
            elif not any(d.lstrip().startswith("public") for d in found):
                problems.append(f"{type_name}.{member}: not public ({found[0].strip()}); read it through GameAccess")
            else:
                print(f"ok   {type_name}.{member}")

    for type_name, members in REFLECTION.items():
        for member in members:
            if declarations(source(type_name), member):
                print(f"ok   {type_name}.{member} (via reflection)")
            else:
                problems.append(f"{type_name}.{member}: not found (used through GameAccess)")

    for problem in problems:
        print(f"::error::{problem}")
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
