"""Extract every rat sprite/texture from Probably Stolen into OriginalRats. Args: <Data dir> <out dir>."""
import os, re, sys
import UnityPy

data_dir, out = sys.argv[1], sys.argv[2]
dirs = {k: os.path.join(out, k) for k in ("sprites", "sprites_8x", "atlas", "starter_frames", "other_rat_art")}
for d in dirs.values():
    os.makedirs(d, exist_ok=True)

RAT_ITEM = re.compile(r"^rat_(s|m|l|xl|xxl)_(m|f)(_d)?$")
RATTY = re.compile(r"(^|[_\s])rats?([_\s\d]|$)", re.I)
FRAMES = 4

files = sorted(f for f in os.listdir(data_dir) if f.endswith(".assets") or f.startswith("level"))
rows, others, seen = [], [], set()
for f in files:
    env = UnityPy.load(os.path.join(data_dir, f))
    for obj in env.objects:
        t = obj.type.name
        if t not in ("Sprite", "Texture2D"):
            continue
        try:
            d = obj.read()
            name = d.m_Name
            if t == "Texture2D" and name == "items_husbandry":
                d.image.save(os.path.join(dirs["atlas"], "items_husbandry.png"))
                continue
            if t != "Sprite":
                if RATTY.search(name) and name not in seen:
                    seen.add(name)
                    d.image.save(os.path.join(dirs["other_rat_art"], name + ".png"))
                    others.append((name, "Texture2D", f))
                continue
            m = RAT_ITEM.match(name)
            if m:
                img = d.image.convert("RGBA")
                img.save(os.path.join(dirs["sprites"], name + ".png"))
                img.resize((img.width * 8, img.height * 8), 0).save(os.path.join(dirs["sprites_8x"], name + "_8x.png"))
                rows.append((name, img.width, img.height))
                size, sex, dead = m.groups()
                if not dead:
                    for n in range(FRAMES):
                        img.save(os.path.join(dirs["starter_frames"], f"rat_{size}_{sex}_{n}.png"))
                        champ = f"big_{size}_{n}.png" if sex == "m" else f"big_{size}_f_{n}.png"
                        img.save(os.path.join(dirs["starter_frames"], champ))
            elif RATTY.search(name) and name not in seen:
                seen.add(name)
                d.image.save(os.path.join(dirs["other_rat_art"], name + ".png"))
                others.append((name, "Sprite", f))
        except Exception as e:
            print("skip", t, getattr(obj, "path_id", "?"), e)

with open(os.path.join(out, "INDEX.txt"), "w", encoding="utf-8") as fh:
    fh.write("Original rat art extracted from Probably Stolen 0.46D. GAME PROPERTY: reference/editing base only.\n")
    fh.write("Never commit, ship or upload this folder (gitignored, excluded from the build and packages).\n\n")
    fh.write("sprites\\         the game's 20 rat sprites, real size, 32-bit RGBA PNG with alpha\n")
    fh.write("sprites_8x\\      the same, enlarged 8x (nearest neighbour) for viewing\n")
    fh.write("atlas\\           the whole items_husbandry sprite sheet they come from\n")
    fh.write("starter_frames\\  copies of each live sprite under the mod's frame names (4 frames each):\n")
    fh.write("                 big_<size>_<n> (male art), big_<size>_f_<n> (female art), rat_<size>_<sex>_<n>.\n")
    fh.write("                 Edit them, then copy the finished frames into ..\\Textures\\Items\\.\n")
    fh.write("other_rat_art\\   any other game image with 'rat' in its name\n\n")
    fh.write("RAT SPRITES (name, width x height)\n")
    for r in sorted(rows):
        fh.write("  %-14s %3d x %d\n" % r)
    fh.write("\nOTHER RAT ART (name, type, file)\n")
    for o in sorted(others):
        fh.write("  %-30s %-10s %s\n" % o)
print(open(os.path.join(out, "INDEX.txt"), encoding="utf-8").read())
