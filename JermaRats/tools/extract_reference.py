"""Extract rat-related sprites and sounds from Probably Stolen for reference. Args: <Data dir> <out dir>."""
import sys, os, glob, io
import UnityPy
data_dir, out = sys.argv[1], sys.argv[2]
sp_dir = os.path.join(out, "sprites"); big_dir = os.path.join(out, "sprites_8x"); oth_dir = os.path.join(out, "husbandry_other_sprites")
snd_dir = os.path.join(out, "sounds"); atl_dir = os.path.join(out, "atlas")
for d in (sp_dir, big_dir, oth_dir, snd_dir, atl_dir): os.makedirs(d, exist_ok=True)
want_clips = {"rat_pickup", "rat_drop", "rat_kill", "rat_happy", "move_item_default"}
files = [os.path.join(data_dir, f) for f in os.listdir(data_dir) if f.endswith(".assets")]
rows, clips = [], []
for f in files:
    env = UnityPy.load(f)
    for obj in env.objects:
        t = obj.type.name
        try:
            if t == "Sprite":
                d = obj.read()
                tex = d.m_RD.texture.read()
                if tex.m_Name != "items_husbandry":
                    continue
                img = d.image
                name = d.m_Name
                target = sp_dir if name.startswith("rat_") else oth_dir
                img.save(os.path.join(target, name + ".png"))
                if name.startswith("rat_"):
                    img.resize((img.width * 8, img.height * 8), 0).save(os.path.join(big_dir, name + "_8x.png"))
                rows.append((name, img.width, img.height, img.mode))
            elif t == "Texture2D":
                d = obj.read()
                if d.m_Name == "items_husbandry":
                    d.image.save(os.path.join(atl_dir, "items_husbandry.png"))
            elif t == "AudioClip":
                d = obj.read()
                if d.m_Name in want_clips:
                    for n, data in d.samples.items():
                        with open(os.path.join(snd_dir, d.m_Name + ".wav"), "wb") as fh:
                            fh.write(data)
                    clips.append((d.m_Name, d.m_Frequency, d.m_Channels, round(d.m_Length, 3)))
        except Exception as e:
            print("skip", t, getattr(obj, "path_id", "?"), e)
with open(os.path.join(out, "INDEX.txt"), "w") as fh:
    fh.write("Extracted from Probably Stolen (game property; reference only, do not commit or redistribute).\n\n")
    fh.write("SPRITES (name, width, height, mode)\n")
    for r in sorted(rows): fh.write("  %-28s %3d x %-3d %s\n" % r)
    fh.write("\nSOUNDS (name, Hz, channels, seconds)\n")
    for c in sorted(clips): fh.write("  %-20s %6d  %d  %s\n" % c)
print(open(os.path.join(out, "INDEX.txt")).read())
