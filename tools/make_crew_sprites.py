"""Generate ValleyExchange/assets/workers.png: every hired-hand role in 3 skin/hair variants (pure stdlib).

Layout: one 64x128 block per (role, variant); block at x = variant * 64, y = role * 128.
Inside a block, 16x16 frames, 4 columns:
  rows 0-3  walking: down, right, up, left (4 frames each; frames 1 and 3 mid-stride)
  row  4    role work animation (2 frames)
  rows 5-7  clearing tools: axe, pickaxe, scythe (2 frames: raised, struck)
Role order must match the Role enum in Market.cs.
Run: python -I tools/make_crew_sprites.py [--preview DIR]"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))  # -I drops the script dir; we only import our own sibling
from make_oil_sprites import Canvas, OUT  # noqa: E402

VARIANTS = [  # (skin, hair)
    ((240, 196, 150, 255), (110, 70, 40, 255)),
    ((198, 140, 98, 255), (40, 32, 30, 255)),
    ((126, 82, 52, 255), (24, 20, 20, 255)),
]
EYE, BOOT, DENIM = (30, 24, 28, 255), (100, 60, 30, 255), (70, 100, 170, 255)
WOOD, STEEL, WHITE, GOLD = (150, 100, 50, 255), (190, 196, 206, 255), (245, 245, 240, 255), (250, 210, 60, 255)


def rgb(*c):
    return (*c, 255)


# role: hat style, hat, hat accent, shirt, shirt accent, pants, overalls?
HEROES = 23   # first hero block (after the 23 crew roles)
PIRATES = 31  # first pirate block (after the 8 heroes)
ROLES = [
    ("straw", rgb(226, 190, 100), rgb(170, 130, 60), rgb(90, 160, 70), None, DENIM, True),        # field hand
    ("cowboy", rgb(140, 90, 50), rgb(90, 55, 30), rgb(190, 60, 50), None, DENIM, True),          # ranch hand
    ("souwester", rgb(240, 200, 40), rgb(200, 160, 20), rgb(240, 200, 40), rgb(200, 160, 20), rgb(60, 60, 70), False),  # fisherman
    ("beanie", rgb(230, 110, 40), rgb(180, 80, 30), rgb(40, 50, 90), rgb(220, 220, 230), rgb(110, 80, 50), False),     # crab catcher
    ("hardhat", rgb(240, 190, 30), rgb(255, 250, 180), rgb(120, 120, 130), rgb(240, 120, 30), rgb(100, 70, 45), False), # miner
    ("hood", rgb(60, 120, 60), rgb(40, 90, 40), rgb(60, 120, 60), rgb(120, 80, 40), rgb(90, 70, 50), False),          # adventurer
    ("toque", WHITE, rgb(210, 210, 210), WHITE, rgb(60, 60, 60), rgb(40, 40, 45), False),                            # chef
    ("bowler", rgb(35, 35, 40), rgb(70, 70, 80), rgb(120, 124, 135), rgb(190, 40, 40), rgb(50, 50, 60), False),       # salesman
    ("hardhat", rgb(200, 40, 35), rgb(255, 250, 180), rgb(45, 45, 50), rgb(15, 15, 18), rgb(45, 45, 50), False),       # oil roughneck
    ("sunhat", rgb(236, 205, 120), rgb(230, 100, 140), rgb(240, 130, 110), rgb(250, 240, 220), rgb(90, 130, 70), False),  # harvester
    ("ranger", rgb(150, 110, 60), rgb(90, 60, 30), rgb(80, 120, 60), rgb(130, 90, 50), rgb(100, 80, 55), False),     # forager
    ("messy", None, None, rgb(225, 215, 190), rgb(120, 80, 45), rgb(85, 95, 120), False),                         # brewer
    ("bandana", rgb(190, 45, 40), rgb(150, 30, 30), rgb(70, 70, 78), rgb(120, 75, 40), rgb(50, 45, 45), False),     # blacksmith
    ("beret", rgb(130, 40, 70), rgb(90, 25, 50), rgb(240, 230, 205), rgb(130, 40, 70), rgb(90, 80, 110), False),    # artisan
    ("cap", rgb(50, 80, 160), rgb(30, 45, 100), rgb(80, 120, 200), rgb(120, 80, 40), rgb(60, 60, 80), False),      # courier
    ("tophat", rgb(30, 30, 35), rgb(150, 40, 40), rgb(40, 110, 70), GOLD, rgb(40, 40, 48), False),                # banker
    ("goggles", rgb(235, 235, 240), rgb(90, 200, 220), WHITE, rgb(150, 160, 175), rgb(70, 70, 90), False),          # scientist
    ("beanie", rgb(60, 110, 50), rgb(40, 80, 35), rgb(180, 40, 40), rgb(40, 20, 20), DENIM, False),                # groundskeeper (flannel)
    ("beanie", rgb(120, 110, 90), rgb(90, 80, 60), rgb(110, 115, 70), rgb(150, 90, 50), rgb(80, 70, 60), False),    # scavenger (patched)
    ("tricorn", rgb(30, 28, 32), GOLD, rgb(150, 30, 40), GOLD, rgb(40, 35, 45), False),                            # pirate captain
    ("ranger", rgb(190, 160, 110), rgb(110, 70, 40), rgb(200, 180, 130), rgb(110, 70, 40), rgb(120, 95, 60), False),  # treasure hunter
    ("scarf", rgb(220, 90, 110), rgb(250, 240, 220), rgb(110, 150, 200), rgb(245, 240, 225), rgb(90, 80, 110), False),  # shopkeeper (apron)
    ("straw", rgb(210, 175, 90), rgb(150, 105, 50), rgb(160, 120, 75), rgb(190, 50, 45), DENIM, True),  # scareboy (burlap, red patch)
    # guild heroes (sheet blocks 18-25, one per class; not crew roles)
    ("helm", rgb(160, 165, 175), rgb(50, 50, 60), rgb(130, 135, 145), rgb(90, 60, 30), rgb(70, 70, 80), False),      # tank
    ("plume", rgb(200, 200, 210), rgb(200, 40, 40), rgb(60, 80, 160), rgb(230, 200, 60), rgb(70, 70, 80), False),    # knight
    ("hood", rgb(240, 240, 235), rgb(220, 190, 80), rgb(240, 240, 235), rgb(220, 190, 80), rgb(200, 195, 185), False),  # priest
    ("bandana", rgb(40, 40, 45), rgb(20, 20, 25), rgb(200, 150, 110), rgb(120, 70, 40), rgb(60, 40, 30), False),     # brawler (bare arms)
    ("wizard", rgb(90, 50, 150), rgb(240, 210, 80), rgb(90, 50, 150), rgb(240, 210, 80), rgb(60, 40, 100), False),   # mage
    ("ranger", rgb(110, 80, 45), rgb(70, 110, 50), rgb(70, 110, 50), rgb(110, 80, 45), rgb(80, 65, 45), False),      # ranger
    ("hood", rgb(45, 40, 55), rgb(90, 30, 40), rgb(45, 40, 55), rgb(90, 30, 40), rgb(35, 32, 40), False),            # rogue
    ("feather", rgb(200, 60, 120), rgb(250, 240, 120), rgb(240, 140, 60), rgb(250, 240, 120), rgb(80, 60, 120), False),  # bard
    # the pirate captain's crew (after the heroes, same class order; they fight with the heroes' moves)
    ("bandana", rgb(30, 30, 35), rgb(200, 30, 30), rgb(230, 230, 225), rgb(40, 40, 120), rgb(50, 45, 40), False),    # bosun
    ("tricorn", rgb(120, 30, 30), rgb(230, 230, 225), rgb(230, 230, 225), rgb(150, 30, 40), rgb(50, 45, 40), False),  # boarder
    ("beanie", rgb(235, 235, 230), rgb(200, 40, 40), rgb(235, 235, 230), rgb(200, 40, 40), rgb(60, 60, 70), False),  # surgeon
    ("bandana", rgb(200, 40, 40), rgb(150, 20, 20), rgb(200, 150, 110), rgb(40, 40, 120), rgb(60, 50, 40), False),   # deckhand
    ("beanie", rgb(40, 40, 50), rgb(230, 200, 60), rgb(70, 70, 80), rgb(30, 30, 30), rgb(45, 40, 40), False),       # gunner
    ("tricorn", rgb(40, 60, 120), GOLD, rgb(230, 230, 225), rgb(40, 60, 120), rgb(60, 55, 50), False),               # navigator
    ("hood", rgb(35, 35, 40), rgb(160, 30, 40), rgb(35, 35, 40), rgb(160, 30, 40), rgb(30, 30, 35), False),           # quartermaster
    ("feather", rgb(30, 110, 90), rgb(250, 240, 120), rgb(230, 230, 225), rgb(30, 110, 90), rgb(70, 60, 50), False),  # shantyman
]


def hat(c, style, color, accent, side, back):
    """Hats over a head occupying rows 5-8 (cols 5-10 front, 6-10 side)."""
    if style in ("straw", "cowboy"):
        c.rect(6, 1, 9, 2, color); c.rect(6, 3, 9, 3, accent)
        c.rect(5 if side else 4, 4, 12 if side else 11, 4, color)
        if style == "cowboy" and not side:
            c.put(3, 3, color); c.put(12, 3, color)
    elif style == "souwester":
        c.rect(5, 2, 10, 3, color); c.rect(4, 4, 12 if side else 11, 4, accent)
        if back or side:
            c.rect(4 if not side else 5, 5, 6 if side else 11, 5, accent)  # back flap
    elif style == "beanie":
        c.rect(6, 1, 9, 1, color); c.rect(5, 2, 10, 3, color); c.rect(5, 4, 10, 4, accent)
    elif style == "hardhat":
        c.rect(6, 1, 9, 1, color); c.rect(5, 2, 10, 3, color); c.rect(4 if not side else 5, 4, 11 if not side else 12, 4, color)
        if not back:
            c.rect(10, 2, 10, 3, accent) if side else c.rect(7, 2, 8, 3, accent)  # lamp
    elif style == "hood":
        c.rect(5, 1, 10, 4, color); c.rect(4, 3, 4, 9, color); c.rect(11, 3, 11, 9, color)
        if back:
            c.rect(5, 5, 10, 8, color)
        c.rect(5, 4, 10, 4, accent)
    elif style == "toque":
        c.rect(6, 0, 9, 0, color); c.rect(5, 1, 10, 3, color); c.rect(5, 4, 10, 4, accent)
    elif style == "sunhat":
        c.rect(6, 2, 9, 3, color); c.rect(6, 3, 9, 3, accent)                       # crown + pink band
        c.rect(3 if not side else 4, 4, 12 if not side else 13, 4, color)          # extra wide brim
        if not side and not back:
            c.put(10, 2, accent)                                                     # flower
    elif style == "ranger":
        c.rect(6, 1, 9, 3, color); c.rect(6, 3, 9, 3, accent); c.rect(3 if not side else 4, 4, 12 if not side else 13, 4, color)
    elif style == "bowler":
        c.rect(6, 2, 9, 3, color); c.rect(5, 4, 10, 4, accent) if not side else c.rect(6, 4, 11, 4, accent)
    elif style == "bandana":
        c.rect(5, 2, 10, 4, color); c.rect(5, 4, 10, 4, accent)
        if back or side:
            c.put(4 if not side else 5, 5, color); c.put(4 if not side else 4, 6, accent)  # knot tails
    elif style == "beret":
        c.rect(5, 2, 10, 3, color); c.rect(4 if not side else 5, 3, 11 if not side else 12, 3, color); c.put(8, 1, accent)  # stem
    elif style in ("helm", "plume"):
        c.rect(5, 1, 10, 4, color); c.put(4 if not side else 5, 3, color); c.put(11, 3, color)
        if not back:
            c.rect(6 if not side else 8, 4, 9 if not side else 10, 4, (40, 40, 50, 255))  # visor slit
        if style == "plume":
            c.rect(7, 0, 8, 0, accent); c.put(6 if not side else 9, 0, accent)          # red plume
    elif style == "wizard":
        c.rect(7, 0, 8, 1, color); c.rect(6, 2, 9, 3, color)
        c.rect(4 if not side else 5, 4, 11 if not side else 12, 4, color)
        if not back:
            c.put(7, 2, accent)                                                          # gold star
    elif style == "feather":
        c.rect(5, 2, 10, 4, color); c.rect(11 if not side else 10, 0, 11 if not side else 10, 2, accent)  # cap and feather
    elif style == "goggles":
        c.rect(5, 2, 10, 4, color); c.put(4, 3, color); c.put(11, 3, color); c.put(6, 1, color); c.put(9, 1, color)  # wild white hair
        if not back:
            c.rect(5, 4, 10, 4, (60, 60, 70, 255)); c.put(6, 4, accent); c.put(9, 4, accent)  # goggles up on the forehead
    elif style == "tophat":
        c.rect(6, 0, 9, 3, color); c.rect(6, 3, 9, 3, accent); c.rect(4 if not side else 5, 4, 11 if not side else 12, 4, color)
    elif style == "scarf":
        c.rect(5, 2, 10, 4, color); c.put(7 if not side else 9, 3, accent); c.put(9 if not side else 7, 2, accent)  # polka-dot kerchief
        if back or side:
            c.put(4 if not side else 5, 5, color); c.put(4, 6, color)                                            # the knot
    elif style == "tricorn":
        c.rect(5, 2, 10, 3, color); c.rect(3 if not side else 4, 4, 12 if not side else 13, 4, color)
        c.put(4 if not side else 5, 3, color); c.put(11 if not side else 12, 3, color)              # the three corners
        if not back:
            c.put(7 if not side else 9, 2, accent); c.put(8 if not side else 10, 2, accent)          # skull badge
    elif style == "cap":
        c.rect(5, 2, 10, 4, color)
        if side:
            c.rect(10, 4, 12, 4, accent)        # peak forward
        elif not back:
            c.rect(5, 4, 10, 4, accent); c.put(7, 3, GOLD)  # peak + badge


def hand(role, variant, facing, frame, finish=True):
    """facing: 0 down, 1 right, 2 up, 3 left (mirror of right). Frames 1 and 3 are mid-stride."""
    if facing == 3:
        right = hand(role, variant, 1, frame, finish)
        c = Canvas(16, 16)
        c.px = [row[::-1] for row in right.px]
        return c
    style, hat_c, hat_a, shirt, shirt_a, pants, overalls = ROLES[role]
    skin, hair = VARIANTS[variant]
    c = Canvas(16, 16)
    side, back = facing == 1, facing == 2
    if side:
        c.rect(6, 5, 10, 8, skin); c.rect(6, 5, 7, 7, hair); c.put(9, 6, EYE)
        c.rect(6, 9, 10, 11, shirt)
        if overalls:
            c.rect(7, 10, 9, 11, DENIM)
        elif shirt_a:
            c.rect(9, 9, 9, 11, shirt_a)
        c.put(11 if frame == 1 else 5 if frame == 3 else 8, 10, skin)       # arm swing
        if frame % 2:
            c.rect(6, 12, 7, 13, pants); c.rect(9, 12, 10, 13, pants); c.rect(5, 14, 7, 14, BOOT); c.rect(9, 14, 11, 14, BOOT)
        else:
            c.rect(7, 12, 9, 13, pants); c.rect(7, 14, 10, 14, BOOT)
    else:
        c.rect(5, 5, 10, 8, hair if back else skin)
        if not back:
            c.put(6, 6, EYE); c.put(9, 6, EYE)
        c.rect(5, 9, 10, 11, shirt)
        c.rect(4, 9, 4, 10, shirt); c.rect(11, 9, 11, 10, shirt); c.put(4, 11, skin); c.put(11, 11, skin)
        if overalls:
            c.rect(6, 10, 9, 11, DENIM)
        elif shirt_a and not back:
            if style == "bowler":
                c.rect(7, 9, 8, 9, WHITE); c.rect(7, 10, 8, 11, shirt_a)    # collar + tie
            elif style == "toque":
                c.put(7, 10, shirt_a); c.put(8, 10, shirt_a); c.put(7, 11, shirt_a); c.put(8, 11, shirt_a)
            elif style == "beanie":
                c.rect(5, 10, 10, 10, shirt_a)                                  # sweater stripe
            elif style == "hood":
                c.rect(5, 11, 10, 11, shirt_a)                                  # belt
            elif style == "messy":
                c.rect(6, 9, 6, 11, shirt_a); c.rect(9, 9, 9, 11, shirt_a)  # suspenders
            elif style == "ranger":
                c.rect(5, 9, 5, 11, shirt_a); c.rect(10, 9, 10, 11, shirt_a)  # backpack straps
            elif style in ("sunhat", "scarf"):
                c.rect(7, 9, 8, 11, shirt_a) if style == "sunhat" else c.rect(6, 10, 9, 11, shirt_a)  # apron
            elif style == "bandana":
                c.rect(6, 9, 9, 11, shirt_a)                                    # leather apron
            elif style == "beret":
                c.rect(5, 9, 10, 9, shirt_a)                                    # scarf
            elif style == "goggles":
                c.rect(7, 9, 8, 11, shirt_a); c.put(9, 10, (90, 200, 220, 255))  # lab coat opening + pen
            elif style == "tophat":
                c.rect(7, 9, 8, 9, WHITE); c.put(6, 10, shirt_a); c.put(7, 11, shirt_a)  # collar + gold watch chain
            elif style == "cap":
                c.put(5, 9, shirt_a); c.put(6, 10, shirt_a); c.put(7, 11, shirt_a)  # satchel strap
            elif role == 8:
                c.put(6, 10, shirt_a); c.put(9, 9, shirt_a); c.put(8, 11, shirt_a)  # oil stains
            else:
                c.rect(5, 9, 5, 11, shirt_a); c.rect(10, 9, 10, 11, shirt_a)    # vest / coat edges
        lift_l, lift_r = frame == 1, frame == 3
        c.rect(5, 12, 7, 13 - lift_l, pants); c.rect(5, 14 - lift_l, 7, 14 - lift_l, BOOT)
        c.rect(8, 12, 10, 13 - lift_r, pants); c.rect(8, 14 - lift_r, 10, 14 - lift_r, BOOT)
    if style == "messy":
        c.rect(5, 3, 10, 4, hair); c.put(6, 2, hair); c.put(9, 2, hair); c.put(4, 4, hair); c.put(11, 4, hair)  # bed head
        if not back:
            c.put(8 if side else 7, 7, (215, 70, 60, 255)); c.put(8, 7, (215, 70, 60, 255))  # red nose
    else:
        hat(c, style, hat_c, hat_a, side, back)
    return c.outline() if finish else c


def work(role, variant, frame):
    """Facing down, busy with the role's own job."""
    c = hand(role, variant, 0, 0, finish=False)
    if role >= PIRATES:
        role -= 8  # a pirate fights like the hero of the same class
    if role == 0:    # watering can
        c.rect(11, 10, 13, 12, STEEL); c.put(14, 11, STEEL); c.put(15, 12 - frame, STEEL)
        for x, y in ((15, 14), (14, 15)) if frame == 0 else ((15, 13), (13, 15), (15, 15)):
            c.put(x, y, (90, 170, 255, 255))
    elif role == 1:  # pitchfork of hay
        lift = 2 * frame
        c.line(11, 13 - lift, 14, 7 - lift, WOOD); c.rect(13, 4 - lift, 15, 4 - lift, STEEL)
        c.rect(12, max(0, 2 - lift), 15, 3 - lift, GOLD)
    elif role == 2:  # fishing rod: cast back / line out
        if frame == 0:
            c.line(11, 10, 14, 2, WOOD); c.put(15, 1, WHITE)
        else:
            c.line(11, 10, 15, 7, WOOD); c.line(15, 8, 15, 14, WHITE); c.put(15, 15, (220, 50, 50, 255))
    elif role == 3:  # crab trap
        y = 11 - 3 * frame
        c.rect(11, y, 15, y + 3, WOOD); c.rect(12, y + 1, 14, y + 2, (90, 60, 30, 255))
    elif role == 4:  # pickaxe
        if frame == 0:
            c.line(11, 10, 12, 3, WOOD); c.rect(9, 2, 15, 2, STEEL)
        else:
            c.line(11, 10, 13, 13, WOOD); c.line(12, 15, 15, 12, STEEL)
    elif role == 5:  # sword
        if frame == 0:
            c.line(11, 9, 14, 2, STEEL); c.rect(10, 9, 12, 9, WOOD)
        else:
            c.line(11, 11, 15, 14, STEEL); c.rect(10, 10, 11, 12, WOOD)
    elif role == 6:  # frying pan, flipping
        c.rect(11, 11, 15, 12, (60, 60, 65, 255)); c.put(10, 11, WOOD)
        food = (230, 150, 60, 255)
        c.rect(12, 10 - 4 * frame, 14, 10 - 4 * frame, food)
    elif role == 10:  # forager: bundle of mushrooms and sticks
        y = 11 - 2 * frame
        c.rect(11, y, 15, y + 2, WOOD); c.put(12, y - 1, (220, 200, 170, 255)); c.put(13, y - 2, (200, 60, 50, 255)); c.put(14, y - 1, (220, 200, 170, 255))
    elif role == 11:  # brewer: bottle at his side, then a swig
        glass = (80, 140, 70, 255)
        if frame == 0:
            c.rect(12, 10, 13, 13, glass); c.put(12, 9, glass)
        else:
            c.line(10, 8, 12, 6, glass); c.rect(11, 6, 13, 7, glass)
    elif role == 9:  # basket of produce, lifted
        y = 11 - 2 * frame
        c.rect(10, y, 15, y + 3, WOOD); c.rect(11, y + 1, 14, y + 1, (190, 140, 70, 255))
        c.put(11, y - 1, (220, 50, 50, 255)); c.put(13, y - 1, (240, 160, 30, 255)); c.put(14, y - 1, (120, 190, 60, 255))
    elif role == 8:  # wrench on a pipe, with an oil drip
        c.rect(11, 13, 15, 14, (90, 90, 100, 255))
        if frame == 0:
            c.line(11, 9, 13, 6, STEEL); c.rect(13, 5, 14, 6, STEEL)
        else:
            c.line(11, 10, 14, 12, STEEL); c.rect(14, 11, 15, 12, STEEL); c.put(12, 15, (15, 15, 18, 255))
    elif role == 12:  # blacksmith: hammer up, then down on the anvil with a spark
        c.rect(11, 13, 15, 14, (70, 70, 80, 255))
        if frame == 0:
            c.line(11, 10, 13, 6, WOOD); c.rect(12, 4, 14, 5, STEEL)
        else:
            c.line(11, 10, 13, 12, WOOD); c.rect(13, 11, 15, 12, STEEL); c.put(15, 10, GOLD); c.put(12, 10, (255, 140, 40, 255))
    elif role == 13:  # artisan: a cheese wheel, turned and lifted
        y = 11 - 2 * frame
        c.rect(11, y, 15, y + 2, (240, 200, 70, 255)); c.rect(11, y + 2, 15, y + 2, (200, 160, 40, 255)); c.put(12 + frame, y + 1, (200, 160, 40, 255))
    elif role == 14:  # courier: parcel at the hip, then a letter held up
        if frame == 0:
            c.rect(11, 11, 14, 13, (180, 140, 90, 255)); c.rect(11, 12, 14, 12, (120, 80, 40, 255))
        else:
            c.rect(11, 6, 14, 8, WHITE); c.put(12, 7, (200, 50, 50, 255))
    elif role == 15:  # banker: ledger open, then a coin held up to the light
        if frame == 0:
            c.rect(11, 11, 15, 13, WHITE); c.rect(13, 11, 13, 13, (60, 60, 70, 255)); c.put(12, 12, (60, 60, 70, 255))
        else:
            c.rect(12, 6, 14, 8, GOLD); c.put(13, 7, (200, 150, 30, 255)); c.put(15, 5, WHITE)
    elif role == 16:  # scientist: a flask, then it bubbles over
        glass = (120, 230, 120, 255)
        c.rect(12, 11, 14, 13, glass); c.put(13, 10, WHITE)
        if frame == 1:
            c.put(12, 8, glass); c.put(14, 7, glass); c.put(13, 9, glass)
    elif role == 23:  # tank: shield at the side, then shoved forward with a clang
        if frame == 0:
            c.rect(11, 9, 14, 13, STEEL); c.rect(12, 10, 13, 12, (120, 70, 40, 255))
        else:
            c.rect(10, 8, 14, 12, STEEL); c.rect(11, 9, 13, 11, (120, 70, 40, 255)); c.put(15, 7, WHITE); c.put(15, 13, WHITE)
    elif role in (24, 29):  # knight: a sword; rogue: a pair of daggers
        if role == 24:
            if frame == 0:
                c.line(11, 9, 14, 2, STEEL); c.rect(10, 9, 12, 9, GOLD)
            else:
                c.line(11, 11, 15, 14, STEEL); c.rect(10, 10, 11, 12, GOLD)
        else:
            y = 9 - frame
            c.line(11, y + 2, 13, y, STEEL); c.line(2, y + 2, 4, y, STEEL)
    elif role == 25:  # priest: staff, then it glows
        c.line(12, 4, 12, 14, WOOD); c.rect(11, 3, 13, 4, GOLD)
        if frame == 1:
            for x, y in ((10, 2), (14, 2), (12, 1), (10, 5), (14, 5)):
                c.put(x, y, (255, 250, 170, 255))
    elif role == 26:  # brawler: wind up, then a big punch
        skin = (200, 150, 110, 255)
        if frame == 0:
            c.rect(11, 10, 12, 11, skin)
        else:
            c.rect(12, 9, 14, 10, skin); c.put(15, 8, WHITE); c.put(15, 11, WHITE); c.put(15, 9, GOLD)
    elif role == 27:  # mage: staff, then a fireball
        c.line(12, 5, 12, 14, WOOD); c.put(12, 4, (120, 220, 255, 255))
        if frame == 1:
            c.rect(13, 3, 15, 5, (240, 120, 30, 255)); c.put(14, 4, (255, 230, 90, 255))
    elif role == 28:  # ranger: bow drawn, then the arrow flies
        c.line(13, 5, 14, 9, WOOD); c.line(14, 9, 13, 13, WOOD)
        if frame == 0:
            c.line(10, 9, 13, 9, WHITE)
        else:
            c.line(14, 3, 15, 2, WHITE); c.put(13, 4, STEEL)
    elif role == 30:  # bard: a banjo, and the notes come out
        c.rect(10, 11, 13, 13, (190, 150, 90, 255)); c.line(13, 11, 15, 8, WOOD)
        if frame == 1:
            c.put(14, 4, (40, 40, 40, 255)); c.put(14, 5, (40, 40, 40, 255)); c.put(12, 3, (40, 40, 40, 255))
    elif role == 18:  # scavenger: a trash bag, then holding up a find
        bag = (60, 60, 66, 255)
        c.rect(11, 10, 14, 14, bag); c.put(12, 9, bag)
        if frame == 1:
            c.rect(12, 5, 13, 6, GOLD); c.put(14, 4, WHITE)
    elif role == 19:  # pirate captain: spyglass up, then the cutlass out
        if frame == 0:
            c.line(10, 7, 14, 5, (170, 130, 60, 255)); c.put(15, 4, STEEL)
        else:
            c.line(11, 11, 15, 6, STEEL); c.rect(10, 11, 11, 12, GOLD)
    elif role == 20:  # treasure hunter: shovel in, then a gem pops out
        if frame == 0:
            c.line(11, 6, 12, 12, WOOD); c.rect(11, 13, 13, 15, STEEL)
        else:
            c.line(11, 8, 13, 13, WOOD); c.rect(12, 14, 14, 15, STEEL); c.put(14, 10, (90, 220, 255, 255)); c.put(15, 9, WHITE)
    elif role == 21:  # shopkeeper: clipboard, then a seed packet held up
        if frame == 0:
            c.rect(11, 9, 14, 13, (150, 110, 60, 255)); c.rect(12, 10, 13, 12, WHITE)
        else:
            c.rect(12, 5, 14, 8, (120, 190, 90, 255)); c.put(13, 6, (240, 220, 120, 255))
    elif role == 22:  # scareboy: arms out stiff with straw pokin' out, then a flap and a crow flees
        straw = (230, 200, 100, 255)
        lift = frame
        c.rect(1, 9 - lift, 4, 9 - lift, (160, 120, 75, 255)); c.rect(11, 9 - lift, 14, 9 - lift, (160, 120, 75, 255))
        c.put(0, 9 - lift, straw); c.put(15, 9 - lift, straw); c.put(0, 10 - lift, straw); c.put(15, 10 - lift, straw)
        if frame == 1:
            crow = (25, 25, 30, 255)
            c.rect(12, 2, 14, 2, crow); c.put(11, 1, crow); c.put(15, 1, crow)
    elif role == 17:  # groundskeeper: axe up, axe down
        if frame == 0:
            c.line(11, 10, 13, 3, WOOD); c.rect(13, 2, 15, 4, STEEL)
        else:
            c.line(11, 10, 13, 14, WOOD); c.rect(13, 13, 15, 15, STEEL); c.put(10, 15, (120, 90, 50, 255))
    else:            # salesman: briefcase / coin
        if frame == 0:
            c.rect(11, 11, 15, 14, (90, 55, 30, 255)); c.rect(12, 10, 14, 10, (60, 40, 20, 255))
        else:
            c.rect(12, 6, 14, 8, GOLD); c.put(13, 7, (200, 150, 30, 255))
    return c.outline()


def tool(role, variant, which, frame):
    c = hand(role, variant, 0, 0, finish=False)
    if which == 0:      # axe
        if frame == 0:
            c.line(11, 10, 13, 3, WOOD); c.rect(13, 2, 15, 4, STEEL)
        else:
            c.line(11, 10, 13, 14, WOOD); c.rect(13, 13, 15, 15, STEEL)
    elif which == 1:    # pickaxe
        if frame == 0:
            c.line(11, 10, 12, 3, WOOD); c.rect(9, 2, 15, 2, STEEL)
        else:
            c.line(11, 10, 13, 13, WOOD); c.line(12, 15, 15, 12, STEEL)
    else:               # scythe
        if frame == 0:
            c.line(11, 11, 13, 4, WOOD); c.rect(13, 3, 15, 3, STEEL); c.put(15, 4, STEEL)
        else:
            c.line(11, 9, 13, 14, WOOD); c.rect(9, 15, 13, 15, STEEL); c.put(9, 14, STEEL)
    return c.outline()


def main():
    preview = Path(sys.argv[sys.argv.index("--preview") + 1]) if "--preview" in sys.argv else None
    sheet = Canvas(64 * len(VARIANTS), 128 * len(ROLES))
    for role in range(len(ROLES)):
        for v in range(len(VARIANTS)):
            ox, oy = v * 64, role * 128
            for facing in range(4):
                for frame in range(4):
                    sheet.paste(hand(role, v, facing, frame), ox + frame * 16, oy + facing * 16)
            for frame in range(2):
                sheet.paste(work(role, v, frame), ox + frame * 16, oy + 4 * 16)
                for which in range(3):
                    sheet.paste(tool(role, v, which, frame), ox + frame * 16, oy + (5 + which) * 16)
    (OUT / "workers.png").write_bytes(sheet.png())
    if preview:
        preview.mkdir(parents=True, exist_ok=True)
        (preview / "workers.png").write_bytes(sheet.png(4))
    print("wrote workers.png", sheet.w, "x", sheet.h)


if __name__ == "__main__":
    main()
