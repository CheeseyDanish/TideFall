# TideWall 3D — procedural low-poly models.
# Run:  blender --background --factory-startup --python Blender/generate_models.py -- <TideWall repo root>
# Writes one FBX per model into Assets/Resources/Models/ (Unity loads them with Resources.Load).
#
# Conventions (read by the Unity code, see docs/TECH.md):
#   * 1 unit = 1 metre, models stand on z = 0, FRONT FACES -Y in Blender (= +Z forward in Unity).
#   * Object "Body" = everything that can't come off. Objects named "Limb_<Name>" are separate meshes
#     with their pivot at the joint, so the game can sever and fling them. "Limb_Head"/"Limb_Eyes" are
#     weak points; severing one kills.
#   * Empties named "Socket_<Name>" mark attachment points (hands, muzzles).
#   * Materials are flat colours. A material named "coat" is recoloured per player at runtime.
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector, Euler

ROOT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.getcwd()
OUT = os.path.join(ROOT, 'Assets', 'Content', 'Models')
SRC = os.path.join(ROOT, 'Blender', 'source')
os.makedirs(OUT, exist_ok=True)

COL = {
    'sand': (.93, .82, .6), 'stone': (.58, .56, .52), 'stoneD': (.42, .4, .38), 'wood': (.55, .36, .2), 'woodD': (.36, .22, .12),
    'red': (.85, .15, .12), 'red2': (.95, .35, .25), 'white': (.95, .95, .95), 'black': (.06, .06, .06), 'orange': (.95, .45, .1),
    'orange2': (1, .62, .3), 'yellow': (.98, .8, .15), 'green': (.25, .6, .3), 'greenD': (.16, .36, .2), 'fishG': (.42, .58, .44),
    'fishL': (.55, .7, .55), 'teal': (.2, .65, .65), 'blue': (.2, .4, .85), 'navy': (.12, .15, .35), 'skin': (.92, .76, .6),
    'grey': (.55, .58, .62), 'greyD': (.25, .27, .3), 'steel': (.72, .74, .78), 'gold': (.95, .75, .2), 'purple': (.55, .25, .65),
    'purpleD': (.25, .1, .32), 'pink': (.98, .55, .75), 'pinkL': (1, .8, .9), 'shark': (.42, .52, .62), 'belly': (.92, .92, .88),
    'croc': (.28, .38, .18), 'crocD': (.17, .24, .1), 'brown': (.45, .33, .2), 'shell': (.85, .65, .55), 'coat': (.8, .2, .2),
    'ink': (.3, .1, .3), 'glow': (.75, 1, .6), 'ice': (.8, .95, 1), 'leaf': (.2, .55, .2), 'flame': (1, .6, .1),
}
_mats = {}
def mat(key):
    if key in _mats: return _mats[key]
    m = bpy.data.materials.new(key)
    rgb = COL[key]
    m.diffuse_color = (*rgb, 1)
    if getattr(m, 'node_tree', None) is None:
        try: m.use_nodes = True
        except Exception: pass
    nt = getattr(m, 'node_tree', None)
    if nt and nt.nodes.get('Principled BSDF'):
        b = nt.nodes['Principled BSDF']
        b.inputs['Base Color'].default_value = (*rgb, 1)
        b.inputs['Roughness'].default_value = .85
    _mats[key] = m
    return m

def prim(kind, v):
    if kind == 'box': bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == 'cyl': bpy.ops.mesh.primitive_cylinder_add(vertices=v, radius=.5, depth=1)
    elif kind == 'cone': bpy.ops.mesh.primitive_cone_add(vertices=v, radius1=.5, radius2=0, depth=1)
    elif kind == 'ball': bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=.5)
    elif kind == 'tor': bpy.ops.mesh.primitive_torus_add(major_segments=v, minor_segments=4, major_radius=.4, minor_radius=.1)
    return bpy.context.active_object

def aim(d):
    """Euler (degrees) that turns +Z toward direction d."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(d).normalized())
    return tuple(math.degrees(a) for a in q.to_euler())

def piece(name, kind, size, off=(0, 0, 0), col='white', joint=(0, 0, 0), rot=(0, 0, 0), v=8):
    o = prim(kind, v)
    o.name = name
    r = Euler(tuple(math.radians(a) for a in rot)).to_matrix().to_4x4()
    o.data.transform(Matrix.Translation(off) @ r @ Matrix.Diagonal((*size, 1)))
    o.location = joint
    o.data.materials.append(mat(col))
    return o

def build(name, parts, sockets=(), post=None):
    """parts: tuples (objName, kind, size, off, col, joint, rot, v). Pieces sharing a name are joined."""
    bpy.ops.object.select_all(action='DESELECT')
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    groups = {}
    for p in parts:
        p = list(p) + [None] * (8 - len(p))
        nm, kind, size, off, col, joint, rot, v = p
        o = piece(nm, kind, size, off or (0, 0, 0), col or 'white', joint or (0, 0, 0), rot or (0, 0, 0), v or 8)
        groups.setdefault(nm, []).append(o)
    for nm, objs in groups.items():
        if len(objs) > 1:
            bpy.ops.object.select_all(action='DESELECT')
            for o in objs: o.select_set(True)
            bpy.context.view_layer.objects.active = objs[0]
            bpy.ops.object.join()
        objs[0].name = nm
        objs[0].parent = root
    bpy.context.view_layer.update()
    for sock in sockets:
        sname, loc = sock[0], sock[1]
        e = bpy.data.objects.new('Socket_' + sname, None)
        e.location = loc
        bpy.context.scene.collection.objects.link(e)
        # (name, location, part): the socket rides on a moving part (a hand on an arm), keeping its world position
        parent = bpy.data.objects.get(sock[2]) if len(sock) > 2 else None
        if parent is not None:
            e.parent = parent
            e.matrix_parent_inverse = parent.matrix_world.inverted()
        else:
            e.parent = root
    if post: post(root)
    export(root)

def export(root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, root.name + '.fbx'), use_selection=True,
        object_types={'EMPTY', 'MESH'}, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='COPY', embed_textures=True)
    clear()

def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
    _mats.clear()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)

def mirror(parts):
    """Duplicate parts whose name ends in 'L' as an 'R' copy, mirrored in X."""
    out = []
    for p in parts:
        out.append(p)
        if p[0].endswith('L') and p[0].startswith('Limb_'):
            q = list(p) + [None] * (8 - len(p))
            off = q[3] or (0, 0, 0); jt = q[5] or (0, 0, 0); rot = q[6] or (0, 0, 0)
            out.append((p[0][:-1] + 'R', q[1], q[2], (-off[0], off[1], off[2]), q[4], (-jt[0], jt[1], jt[2]), (rot[0], -rot[1], -rot[2]), q[7]))
    return out

def ring(n, r, z, phase=0):
    return [(math.cos(phase + i * 2 * math.pi / n) * r, math.sin(phase + i * 2 * math.pi / n) * r, z) for i in range(n)]

def spikes(n, r, center, size, col, upper_only=False):
    """n cones on a sphere of radius r, pointing outward (Fibonacci spread)."""
    out = []
    g = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - (i + .5) / n * (1 if upper_only else 2)
        rr = math.sqrt(max(0, 1 - z * z)); a = i * g
        d = (math.cos(a) * rr, math.sin(a) * rr, z)
        out.append(('Body', 'cone', size, tuple(center[k] + d[k] * r for k in range(3)), col, None, aim(d), 5))
    return out

# =====================================================================================  ENEMIES
build('Crab', mirror([
    ('Body', 'ball', (.9, .75, .42), (0, 0, .4), 'orange'),
    *[('Body', 'box', (.5, .07, .07), (s * .55, y, .22), 'orange', None, (0, s * 30, 0)) for s in (-1, 1) for y in (-.18, .05, .26)],
    ('Limb_ClawL', 'box', (.12, .3, .12), (-.05, -.15, 0), 'orange', (-.4, -.3, .42)),
    ('Limb_ClawL', 'ball', (.3, .36, .22), (-.12, -.4, .02), 'orange2', (-.4, -.3, .42)),
    ('Limb_Eyes', 'cyl', (.05, .05, .22), (-.1, 0, .1), 'orange', (0, -.28, .5)),
    ('Limb_Eyes', 'cyl', (.05, .05, .22), (.1, 0, .1), 'orange', (0, -.28, .5)),
    ('Limb_Eyes', 'ball', (.12, .12, .12), (-.1, 0, .24), 'white', (0, -.28, .5)),
    ('Limb_Eyes', 'ball', (.12, .12, .12), (.1, 0, .24), 'white', (0, -.28, .5)),
    ('Limb_Eyes', 'ball', (.05, .05, .05), (-.1, -.06, .25), 'black', (0, -.28, .5)),
    ('Limb_Eyes', 'ball', (.05, .05, .05), (.1, -.06, .25), 'black', (0, -.28, .5)),
]))

build('HermitCrab', mirror([
    ('Body', 'ball', (.45, .5, .3), (0, -.1, .25), 'red'),
    ('Limb_Shell', 'ball', (.7, .75, .65), (0, .15, .2), 'shell', (0, .1, .3)),
    ('Limb_Shell', 'cone', (.4, .4, .45), (0, .45, .45), 'shell', (0, .1, .3), (-50, 0, 0)),
    ('Limb_ClawL', 'ball', (.2, .26, .16), (-.08, -.15, 0), 'red2', (-.18, -.3, .25)),
    ('Limb_Eyes', 'cyl', (.04, .04, .18), (0, 0, .08), 'red', (0, -.3, .35)),
    ('Limb_Eyes', 'ball', (.1, .1, .1), (0, 0, .2), 'black', (0, -.3, .35)),
]))

build('Drowner', mirror([
    ('Limb_LegL', 'box', (.18, .2, .85), (0, 0, -.42), 'fishG', (-.15, 0, .85)),
    ('Body', 'box', (.55, .42, .75), (0, .05, 1.18), 'fishG', None, (-15, 0, 0)),
    ('Body', 'box', (.4, .1, .5), (0, -.14, 1.12), 'fishL', None, (-15, 0, 0)),
    ('Limb_ArmL', 'box', (.13, .65, .13), (0, -.32, -.08), 'fishG', (-.33, -.05, 1.42)),
    ('Limb_ArmL', 'ball', (.18, .18, .18), (0, -.68, -.12), 'fishL', (-.33, -.05, 1.42)),
    ('Limb_Head', 'ball', (.38, .42, .38), (0, -.05, .2), 'fishL', (0, -.12, 1.55)),
    ('Limb_Head', 'cone', (.08, .3, .32), (0, .1, .44), 'teal', (0, -.12, 1.55)),
    ('Limb_Head', 'ball', (.08, .08, .08), (-.09, -.24, .25), 'yellow', (0, -.12, 1.55)),
    ('Limb_Head', 'ball', (.08, .08, .08), (.09, -.24, .25), 'yellow', (0, -.12, 1.55)),
    ('Limb_Head', 'box', (.18, .02, .04), (0, -.26, .1), 'black', (0, -.12, 1.55)),
]))

build('Gull', mirror([
    ('Body', 'ball', (.35, .6, .3), (0, 0, 0), 'white'),
    ('Body', 'ball', (.22, .22, .22), (0, -.32, .08), 'white'),
    ('Body', 'cone', (.08, .08, .18), (0, -.48, .06), 'orange', None, (90, 0, 0), 5),
    ('Body', 'box', (.2, .15, .04), (0, .35, .02), 'grey'),
    ('Body', 'ball', (.05, .05, .05), (-.08, -.4, .12), 'black'),
    ('Body', 'ball', (.05, .05, .05), (.08, -.4, .12), 'black'),
    ('Limb_WingL', 'box', (.6, .26, .04), (-.3, .02, 0), 'white', (-.15, 0, .05)),
    ('Limb_WingL', 'box', (.18, .2, .045), (-.55, .04, 0), 'greyD', (-.15, 0, .05)),
]))

build('Puffer', [
    ('Body', 'ball', (.8, .8, .8), (0, 0, .5), 'yellow'),
    ('Body', 'ball', (.6, .5, .4), (0, -.08, .38), 'white'),
    ('Body', 'ball', (.12, .12, .12), (-.2, -.32, .65), 'black'),
    ('Body', 'ball', (.12, .12, .12), (.2, -.32, .65), 'black'),
    ('Body', 'cone', (.12, .2, .1), (0, -.42, .48), 'orange', None, (90, 0, 0), 5),
    *spikes(14, .38, (0, 0, .5), (.08, .08, .22), 'orange2'),
])

build('Eel', [
    ('Limb_Head', 'ball', (.32, .42, .3), (0, -.1, 0), 'greyD', (0, -.95, .3)),
    ('Limb_Head', 'ball', (.07, .07, .07), (-.1, -.25, .08), 'glow', (0, -.95, .3)),
    ('Limb_Head', 'ball', (.07, .07, .07), (.1, -.25, .08), 'glow', (0, -.95, .3)),
    ('Body', 'cyl', (.28, .28, .8), (0, -.5, .28), 'greyD', None, (90, 0, 0), 6),
    ('Body', 'cyl', (.25, .25, .8), (0, .2, .26), 'greyD', None, (90, 0, 0), 6),
    ('Body', 'box', (.03, 1.5, .12), (0, -.15, .44), 'teal'),
    ('Limb_Tail', 'cone', (.22, .2, .9), (0, .45, 0), 'greyD', (0, .6, .25), (-90, 0, 0), 6),
])

build('Turtle', [
    ('Body', 'ball', (1.2, 1.4, .7), (0, 0, .45), 'greenD'),
    ('Body', 'box', (1.25, 1.45, .12), (0, 0, .3), 'brown'),
    *[('Body', 'box', (.35, .25, .12), (sx * .6, sy * .45, .2), 'fishG') for sx in (-1, 1) for sy in (-1, 1)],
    ('Limb_Head', 'ball', (.35, .45, .32), (0, -.2, .02), 'fishG', (0, -.7, .4)),
    ('Limb_Head', 'ball', (.06, .06, .06), (-.1, -.38, .1), 'black', (0, -.7, .4)),
    ('Limb_Head', 'ball', (.06, .06, .06), (.1, -.38, .1), 'black', (0, -.7, .4)),
])

build('Shark', [
    ('Body', 'ball', (.9, 3.0, .9), (0, 0, .6), 'shark'),
    ('Body', 'ball', (.75, 2.6, .55), (0, 0, .42), 'belly'),
    ('Body', 'ball', (.62, .8, .55), (0, -1.35, .6), 'shark'),
    ('Body', 'ball', (.1, .1, .1), (-.3, -1.3, .75), 'black'),
    ('Body', 'ball', (.1, .1, .1), (.3, -1.3, .75), 'black'),
    ('Body', 'box', (.45, .05, .08), (0, -1.62, .42), 'white'),
    ('Body', 'box', (.8, .35, .06), (-.6, -.35, .38), 'shark', None, (0, -20, 0)),
    ('Body', 'box', (.8, .35, .06), (.6, -.35, .38), 'shark', None, (0, 20, 0)),
    ('Limb_Fin', 'cone', (.12, .7, .8), (0, .1, .35), 'shark', (0, -.1, .95), None, 6),
    ('Limb_Tail', 'cone', (.35, .35, .8), (0, .35, 0), 'shark', (0, 1.35, .6), (-90, 0, 0), 6),
    ('Limb_Tail', 'box', (.06, .45, 1.0), (0, .75, .1), 'shark', (0, 1.35, .6), (-25, 0, 0)),
])

build('Croc', [
    ('Body', 'box', (1.0, 2.0, .45), (0, .1, .4), 'croc'),
    *[('Body', 'box', (.18, .25, .1), (sx, y, .66), 'crocD') for sx in (-.25, .25) for y in (-.6, -.1, .4, .9)],
    *[('Body', 'box', (.25, .3, .35), (sx * .55, sy * .6, .18), 'crocD') for sx in (-1, 1) for sy in (-1, 1)],
    ('Limb_Head', 'box', (.7, .7, .35), (0, -.3, 0), 'croc', (0, -.9, .42)),
    ('Limb_Head', 'box', (.5, .9, .25), (0, -1.0, -.04), 'croc', (0, -.9, .42)),
    ('Limb_Head', 'box', (.04, .8, .05), (-.26, -1.0, -.13), 'white', (0, -.9, .42)),
    ('Limb_Head', 'box', (.04, .8, .05), (.26, -1.0, -.13), 'white', (0, -.9, .42)),
    ('Limb_Head', 'ball', (.13, .13, .13), (-.22, -.35, .2), 'yellow', (0, -.9, .42)),
    ('Limb_Head', 'ball', (.13, .13, .13), (.22, -.35, .2), 'yellow', (0, -.9, .42)),
    ('Limb_Head', 'box', (.03, .03, .1), (-.22, -.42, .22), 'black', (0, -.9, .42)),
    ('Limb_Head', 'box', (.03, .03, .1), (.22, -.42, .22), 'black', (0, -.9, .42)),
    ('Limb_Tail', 'cone', (.7, .4, 1.8), (0, .9, 0), 'croc', (0, 1.1, .4), (-90, 0, 0), 6),
])

build('Urchin', [
    ('Body', 'ball', (.7, .7, .7), (0, 0, .42), 'purpleD'),
    *spikes(18, .32, (0, 0, .42), (.07, .07, .42), 'purple'),
])

build('Jellyfish', [
    ('Body', 'ball', (1.0, 1.0, .7), (0, 0, 1.35), 'pink'),
    ('Body', 'ball', (.6, .6, .4), (0, 0, 1.28), 'pinkL'),
    *[('Limb_Tent%d' % (i + 1), 'cyl', (.06, .06, .9), (0, 0, -.45), 'pinkL', p, None, 5) for i, p in enumerate(ring(4, .28, 1.05, .4))],
])

build('Octopus', [
    ('Body', 'ball', (.9, .9, 1.1), (0, .15, .95), 'red'),
    ('Body', 'ball', (.2, .2, .2), (-.2, -.28, .95), 'white'),
    ('Body', 'ball', (.2, .2, .2), (.2, -.28, .95), 'white'),
    ('Body', 'box', (.12, .03, .05), (-.2, -.38, .95), 'black'),
    ('Body', 'box', (.12, .03, .05), (.2, -.38, .95), 'black'),
    *[('Limb_Tent%d' % (i + 1), 'cone', (.22, .22, 1.0), (p[0] * 1.2, p[1] * 1.2, -.18), 'red2', p, aim((p[0], p[1], -.45)), 6)
      for i, p in enumerate(ring(6, .35, .45))],
])

build('Lobster', mirror([
    ('Body', 'ball', (.9, 1.1, .6), (0, -.1, .55), 'red'),
    *[('Body', 'ball', (.75 - i * .12, .4, .45 - i * .05), (0, .55 + i * .33, .45 - i * .04), 'red') for i in range(3)],
    ('Body', 'box', (.6, .25, .05), (0, 1.55, .32), 'red2'),
    *[('Body', 'box', (.45, .06, .06), (s * .5, y, .25), 'red', None, (0, s * 30, 0)) for s in (-1, 1) for y in (-.2, .1, .4)],
    ('Limb_Head', 'ball', (.5, .4, .35), (0, -.15, 0), 'red', (0, -.6, .65)),
    ('Limb_Head', 'cyl', (.03, .03, 1.0), (-.1, -.55, .3), 'red2', (0, -.6, .65), (-60, 0, -10), 5),
    ('Limb_Head', 'cyl', (.03, .03, 1.0), (.1, -.55, .3), 'red2', (0, -.6, .65), (-60, 0, 10), 5),
    ('Limb_Head', 'ball', (.09, .09, .09), (-.12, -.3, .15), 'black', (0, -.6, .65)),
    ('Limb_Head', 'ball', (.09, .09, .09), (.12, -.3, .15), 'black', (0, -.6, .65)),
    ('Limb_ClawL', 'cyl', (.15, .15, .6), (-.1, -.3, 0), 'red', (-.45, -.55, .5), (90, 0, 0), 6),
    ('Limb_ClawL', 'ball', (.45, .6, .3), (-.15, -.8, .05), 'red2', (-.45, -.55, .5)),
    ('Limb_ClawL', 'cone', (.18, .18, .35), (-.08, -1.2, .05), 'red2', (-.45, -.55, .5), (90, 0, 0), 5),
]))

# =====================================================================================  PLAYER
# Sailors are jointed for animation: Part_ objects pivot at hip, shoulder and neck (the game swings them).
# The hands carry the weapon sockets. Arms hang straight down at rest.
def person(name, coat, legs, hat, extra=(), arm_col=None):
    arm_col = arm_col or coat
    parts = [
        ('Part_LegL', 'box', (.22, .24, .8), (0, 0, -.4), legs, (-.14, 0, .8)),
        ('Part_LegL', 'box', (.24, .3, .1), (0, -.04, -.76), 'black', (-.14, 0, .8)),
        ('Part_LegR', 'box', (.22, .24, .8), (0, 0, -.4), legs, (.14, 0, .8)),
        ('Part_LegR', 'box', (.24, .3, .1), (0, -.04, -.76), 'black', (.14, 0, .8)),
        ('Body', 'box', (.6, .38, .7), (0, 0, 1.15), coat),
        ('Body', 'box', (.62, .4, .08), (0, 0, .84), 'black'),
        ('Part_ArmL', 'box', (.16, .16, .58), (0, 0, -.27), arm_col, (-.38, 0, 1.44)),
        ('Part_ArmL', 'ball', (.15, .15, .15), (0, 0, -.6), 'skin', (-.38, 0, 1.44)),
        ('Part_ArmR', 'box', (.16, .16, .58), (0, 0, -.27), arm_col, (.38, 0, 1.44)),
        ('Part_ArmR', 'ball', (.15, .15, .15), (0, 0, -.6), 'skin', (.38, 0, 1.44)),
        ('Part_Head', 'ball', (.42, .42, .42), (0, 0, .22), 'skin', (0, 0, 1.5)),
        ('Part_Head', 'ball', (.05, .05, .05), (-.08, -.2, .26), 'black', (0, 0, 1.5)),
        ('Part_Head', 'ball', (.05, .05, .05), (.08, -.2, .26), 'black', (0, 0, 1.5)),
        *[(p[0], p[1], p[2], p[3], p[4], (0, 0, 1.5)) for p in hat],
        *extra,
    ]
    build(name, parts, sockets=[('Gun', (.38, -.02, .84), 'Part_ArmR'), ('Melee', (-.38, -.02, .84), 'Part_ArmL')])

person('Sailor', 'coat', 'navy', [('Part_Head', 'cyl', (.46, .46, .12), (0, 0, .44), 'white', None, None, 10),
                                  ('Part_Head', 'cyl', (.47, .47, .05), (0, 0, .39), 'navy', None, None, 10)])
# Vendors: the hotdog chef (apron, tall hat, moustache) and the fishmonger (yellow oilskins, sou'wester)
person('VendorChef', 'white', 'greyD', [('Part_Head', 'cyl', (.4, .4, .5), (0, 0, .62), 'white', None, None, 10),
                                         ('Part_Head', 'ball', (.5, .5, .25), (0, 0, .86), 'white'),
                                         ('Part_Head', 'box', (.22, .05, .05), (0, -.21, .16), 'brown')],
       extra=[('Body', 'box', (.5, .05, .6), (0, -.2, 1.0), 'red')])
person('VendorFish', 'yellow', 'yellow', [('Part_Head', 'cone', (.7, .7, .3), (0, 0, .42), 'yellow', None, None, 10),
                                          ('Part_Head', 'box', (.3, .05, .1), (0, -.2, .1), 'greyD')],
       extra=[('Body', 'box', (.5, .05, .55), (0, -.2, 1.05), 'blue')], arm_col='yellow')

# =====================================================================================  WEAPONS (grip at origin, pointing -Y)
build('Revolver', [
    ('Body', 'box', (.06, .1, .2), (0, .03, -.08), 'woodD', None, (-15, 0, 0)),
    ('Body', 'box', (.08, .2, .11), (0, -.08, .03), 'greyD'),
    ('Body', 'cyl', (.12, .12, .1), (0, -.1, .04), 'steel', None, (90, 0, 0), 6),
    ('Body', 'cyl', (.045, .045, .3), (0, -.3, .06), 'steel', None, (90, 0, 0), 6),
], sockets=[('Muzzle', (0, -.46, .06))])
build('Scattergun', [
    ('Body', 'box', (.08, .35, .12), (0, .2, -.02), 'wood'),
    ('Body', 'box', (.09, .25, .11), (0, -.08, .02), 'greyD'),
    ('Body', 'cyl', (.075, .075, .6), (0, -.5, .05), 'steel', None, (90, 0, 0), 6),
    ('Body', 'box', (.1, .18, .08), (0, -.45, -.03), 'wood'),
], sockets=[('Muzzle', (0, -.82, .05))])
build('HarpoonGun', [
    ('Body', 'box', (.1, .5, .12), (0, -.15, 0), 'greyD'),
    ('Body', 'box', (.08, .25, .14), (0, .2, -.03), 'woodD'),
    ('Body', 'cyl', (.03, .03, 1.0), (0, -.65, .1), 'steel', None, (90, 0, 0), 5),
    ('Body', 'cone', (.09, .09, .16), (0, -1.2, .1), 'steel', None, (90, 0, 0), 5),
], sockets=[('Muzzle', (0, -1.25, .1))])
build('FlareGun', [
    ('Body', 'box', (.07, .1, .2), (0, .03, -.08), 'orange', None, (-15, 0, 0)),
    ('Body', 'box', (.09, .22, .12), (0, -.08, .03), 'orange'),
    ('Body', 'cyl', (.11, .11, .25), (0, -.28, .05), 'orange2', None, (90, 0, 0), 8),
], sockets=[('Muzzle', (0, -.42, .05))])
build('RivetSMG', [
    ('Body', 'box', (.06, .1, .18), (0, .02, -.08), 'greyD', None, (-10, 0, 0)),
    ('Body', 'box', (.08, .34, .1), (0, -.12, .03), 'grey'),
    ('Body', 'box', (.05, .08, .2), (0, -.1, -.1), 'greyD'),
    ('Body', 'cyl', (.04, .04, .22), (0, -.38, .05), 'steel', None, (90, 0, 0), 6),
    ('Body', 'box', (.07, .12, .04), (0, .08, .1), 'orange'),
], sockets=[('Muzzle', (0, -.5, .05))])
build('Longrifle', [
    ('Body', 'box', (.07, .45, .1), (0, .28, -.04), 'woodD'),
    ('Body', 'box', (.08, .4, .1), (0, -.15, .02), 'wood'),
    ('Body', 'cyl', (.035, .035, .8), (0, -.7, .06), 'steel', None, (90, 0, 0), 6),
    ('Body', 'cyl', (.04, .04, .3), (0, -.1, .14), 'black', None, (90, 0, 0), 6),
    ('Body', 'box', (.12, .03, .03), (.06, .02, .08), 'steel'),
], sockets=[('Muzzle', (0, -1.1, .06))])
build('EelCaster', [
    ('Body', 'box', (.07, .1, .18), (0, .03, -.08), 'navy', None, (-15, 0, 0)),
    ('Body', 'box', (.1, .3, .13), (0, -.1, .03), 'teal'),
    ('Body', 'cyl', (.08, .08, .18), (0, -.32, .04), 'blue', None, (90, 0, 0), 8),
    ('Body', 'cone', (.07, .07, .12), (0, -.46, .04), 'yellow', None, (90, 0, 0), 6),
    ('Body', 'box', (.03, .2, .03), (0, -.1, .12), 'yellow'),
], sockets=[('Muzzle', (0, -.52, .04))])
build('Blowtorch', [
    ('Body', 'box', (.06, .1, .18), (0, .03, -.08), 'greyD', None, (-15, 0, 0)),
    ('Body', 'cyl', (.14, .14, .3), (0, .0, -.12), 'red', None, (0, 0, 0), 10),
    ('Body', 'box', (.08, .2, .1), (0, -.1, .04), 'steel'),
    ('Body', 'cyl', (.04, .04, .3), (0, -.32, .05), 'steel', None, (90, 0, 0), 6),
    ('Body', 'cone', (.07, .07, .08), (0, -.5, .05), 'black', None, (90, 0, 0), 6),
], sockets=[('Muzzle', (0, -.55, .05))])

build('Oar', [
    ('Body', 'cyl', (.05, .05, 1.5), (0, -.55, 0), 'wood', None, (90, 0, 0), 6),
    ('Body', 'box', (.26, .5, .03), (0, -1.45, 0), 'wood'),
])
build('Cutlass', [
    ('Body', 'box', (.035, .85, .09), (0, -.55, 0), 'steel'),
    ('Body', 'box', (.2, .05, .09), (0, -.1, 0), 'gold'),
    ('Body', 'box', (.05, .18, .06), (0, .02, 0), 'woodD'),
])
build('Anchor', [
    ('Body', 'cyl', (.07, .07, 1.0), (0, -.5, 0), 'greyD', None, (90, 0, 0), 6),
    ('Body', 'box', (.5, .06, .06), (0, -.12, 0), 'greyD'),
    ('Body', 'tor', (.4, .4, .5), (0, .06, 0), 'greyD', None, (0, 90, 0), 8),
    ('Body', 'box', (.7, .08, .08), (0, -.98, 0), 'greyD'),
    ('Body', 'cone', (.16, .08, .28), (-.36, -.86, 0), 'greyD', None, (-90, 0, 0), 4),
    ('Body', 'cone', (.16, .08, .28), (.36, -.86, 0), 'greyD', None, (-90, 0, 0), 4),
])
build('BoatHook', [
    ('Body', 'cyl', (.04, .04, 1.7), (0, -.75, 0), 'wood', None, (90, 0, 0), 6),
    ('Body', 'box', (.03, .22, .03), (0, -1.7, 0), 'steel'),
    ('Body', 'box', (.03, .03, .2), (0, -1.8, .08), 'steel'),
    ('Body', 'cone', (.04, .04, .1), (0, -1.75, .2), 'steel', None, (-60, 0, 0), 4),
])
build('Spade', [
    ('Body', 'cyl', (.045, .045, .65), (0, -.3, 0), 'red', None, (90, 0, 0), 6),
    ('Body', 'box', (.16, .045, .045), (0, .04, 0), 'red'),
    ('Body', 'box', (.24, .3, .03), (0, -.75, 0), 'red2'),
])

# =====================================================================================  PROPS
def open_ends(half):
    """Returns a post-step that deletes the end faces of the 'Run' pieces at x = +-half, so sections placed side by
    side make one unbroken wall (no inner faces, no seams). Exposed ends get a separate WallEnd cap in the game."""
    def post(root):
        import bmesh
        for o in root.children:
            if o.type != 'MESH' or o.name != 'Run': continue
            bm = bmesh.new(); bm.from_mesh(o.data)
            dead = [f for f in bm.faces if abs(f.normal.x) > .99 and all(abs(v.co.x) > half - .01 for v in f.verts)]
            bmesh.ops.delete(bm, geom=dead, context='FACES')
            bm.to_mesh(o.data); bm.free()
    return post

def masonry(y, face):
    """Running-bond stone blocks standing slightly proud of one face, tiling every 4 m (half blocks meet at the seams)."""
    out = []
    for row, z in enumerate((.3, .62, .94)):
        if row % 2 == 0: xs = [(-1.5, .92), (-.5, .92), (.5, .92), (1.5, .92)]
        else: xs = [(-1.77, .44), (-1, .92), (0, .92), (1, .92), (1.77, .44)]
        for x, w in xs:
            col = 'stone' if (int(x * 2) + row) % 3 else 'stoneL'
            out.append(('Body', 'box', (w, .05, .27), (x, y + face * .025, z), col))
    return out

COL.setdefault('stoneL', (.66, .64, .6))

# The wall: 4 m sections that join seamlessly (end faces removed); merlons every 2 m all along it
build('WallSegment', [
    ('Run', 'box', (4, 1, 1.2), (0, 0, .6), 'stoneD'),
    ('Run', 'box', (4, 1.14, .12), (0, 0, 1.26), 'stone'),
    ('Run', 'box', (4, 1.24, .2), (0, 0, .1), 'stone'),
    *[('Body', 'box', (.72, .9, .42), (x, -.05, 1.53), 'stone') for x in (-1, 1)],
    *[('Body', 'box', (.78, .96, .05), (x, -.05, 1.76), 'stoneL') for x in (-1, 1)],
    *masonry(-.5, -1), *masonry(.5, 1),
], post=open_ends(2))
# The broken end of a section next to a breach (placed at the section's edge, facing the gap)
build('WallEnd', [
    ('Body', 'box', (.06, 1.0, 1.2), (0, 0, .6), 'stoneD'),
    ('Body', 'box', (.4, .5, .3), (.12, -.2, 1.15), 'stone', None, (12, -20, 8)),
    ('Body', 'box', (.35, .45, .28), (.15, .25, .75), 'stoneL', None, (-8, 15, 20)),
    ('Body', 'box', (.3, .35, .25), (.18, -.1, .3), 'stone', None, (20, 5, -12)),
])
# What's left of a breached section: a low heap of blocks
build('WallRubble', [
    *[('Body', 'box', (.7 + (i % 3) * .15, .5, .32), ((i - 3.5) * .5, ((i * 7) % 5 - 2) * .14, .14 + (i % 2) * .18), 'stone' if i % 2 else 'stoneD', None, ((i * 23) % 30 - 15, (i * 41) % 40 - 20, (i * 67) % 50 - 25)) for i in range(8)],
    ('Body', 'box', (3.4, 1.2, .16), (0, 0, .08), 'stoneD', None, (0, 0, 0)),
])
# A gatehouse over each sally port: tall pillars and a lintel high enough for anyone (giants too), with a banner
build('WallGate', [
    *[('Body', 'box', (.62, 1.34, 2.9), (x, 0, 1.45), 'stone') for x in (-1.71, 1.71)],
    *[('Body', 'box', (.7, 1.42, .2), (x, 0, .1), 'stoneL') for x in (-1.71, 1.71)],
    ('Body', 'box', (4, 1.3, .5), (0, 0, 2.75), 'stoneD'),
    ('Body', 'box', (4.1, 1.42, .14), (0, 0, 3.07), 'stone'),
    *[('Body', 'box', (.62, 1.0, .45), (x, -.05, 3.36), 'stone') for x in (-1.5, 0, 1.5)],
    ('Body', 'box', (.9, .04, .8), (0, -.68, 2.2), 'red'),
    ('Body', 'box', (.9, .04, .8), (0, .68, 2.2), 'red'),
    ('Body', 'box', (.3, .05, .3), (0, -.71, 2.3), 'gold', None, (0, 45, 0)),
    ('Body', 'box', (.3, .05, .3), (0, .71, 2.3), 'gold', None, (0, 45, 0)),
])
build('Tower', [
    ('Body', 'cyl', (2.4, 2.4, 4), (0, 0, 2), 'stoneD', None, None, 10),
    ('Body', 'cyl', (2.7, 2.7, .3), (0, 0, 4.1), 'stone', None, None, 10),
    *[('Body', 'box', (.5, .5, .5), (p[0], p[1], 4.5), 'stone') for p in ring(8, 1.15, 0)],
    ('Body', 'cone', (2.6, 2.6, 1.8), (0, 0, 5.6), 'red', None, None, 10),
])
build('Lighthouse', [
    *[('Body', 'cyl', (2.4 - i * .2, 2.4 - i * .2, 1.6), (0, 0, .8 + i * 1.6), 'white' if i % 2 == 0 else 'red', None, None, 10) for i in range(6)],
    ('Body', 'cyl', (1.6, 1.6, 1.2), (0, 0, 10.2), 'ice', None, None, 8),
    ('Body', 'cone', (1.9, 1.9, 1.2), (0, 0, 11.4), 'red', None, None, 8),
], sockets=[('Lamp', (0, 0, 10.2))])
build('ShopKiosk', [
    ('Body', 'box', (2.4, 1.0, 1.0), (0, 0, .5), 'wood'),
    ('Body', 'box', (2.5, 1.1, .08), (0, 0, 1.02), 'woodD'),
    *[('Body', 'cyl', (.1, .1, 2.4), (x, y, 1.2), 'woodD', None, None, 6) for x in (-1.15, 1.15) for y in (-.45, .45)],
    *[('Body', 'box', (.62, 1.4, .06), (-.93 + i * .62, -.1, 2.42), 'red' if i % 2 == 0 else 'white', None, (-12, 0, 0)) for i in range(4)],
    ('Body', 'box', (1.6, .06, .45), (0, -.62, 1.95), 'navy'),
    ('Body', 'box', (.4, .4, .4), (-.7, 0, 1.25), 'gold'),
    ('Body', 'box', (.3, .5, .3), (.6, 0, 1.2), 'greyD'),
])
build('FishmongerCart', [
    ('Body', 'box', (2.0, 1.0, .7), (0, 0, .75), 'wood'),
    ('Body', 'box', (1.8, .85, .15), (0, 0, 1.15), 'ice'),
    *[('Body', 'cyl', (.7, .7, .1), (x, 0, .35), 'woodD', None, (0, 90, 0), 8) for x in (-1.05, 1.05)],
    ('Body', 'ball', (.6, .25, .2), (-.4, 0, 1.3), 'teal'),
    ('Body', 'cone', (.25, .1, .25), (-.1, 0, 1.3), 'teal', None, (0, -90, 0), 4),
    ('Body', 'ball', (.5, .22, .18), (.4, .1, 1.3), 'orange2'),
    ('Body', 'box', (.08, .08, 1.4), (-.95, .45, 1.6), 'woodD'),
    ('Body', 'box', (1.0, .06, .45), (-.5, .45, 2.2), 'blue'),
])
build('Bell', [
    ('Body', 'box', (.15, .15, 2.4), (0, 0, 1.2), 'woodD'),
    ('Body', 'box', (.9, .15, .15), (.35, 0, 2.35), 'woodD'),
    ('Body', 'cone', (.55, .55, .55), (.65, 0, 1.95), 'gold', None, None, 10),
    ('Body', 'ball', (.14, .14, .14), (.65, 0, 1.68), 'greyD'),
])
build('BeachUmbrella', [
    ('Body', 'cyl', (.06, .06, 2.2), (0, 0, 1.1), 'white', None, None, 6),
    ('Body', 'cone', (2.4, 2.4, .6), (0, 0, 2.3), 'red', None, None, 8),
    ('Body', 'cone', (1.2, 1.2, .3), (0, 0, 2.48), 'white', None, None, 8),
])
build('Towel', [('Body', 'box', (1.0, 1.8, .03), (0, 0, .015), 'blue'), ('Body', 'box', (1.0, .3, .032), (0, .5, .016), 'white')])
build('PalmTree', [
    *[('Body', 'cyl', (.35 - i * .03, .35 - i * .03, 1.0), (i * .12, 0, .5 + i * .95), 'brown', None, (0, 7, 0), 6) for i in range(5)],
    *[('Body', 'box', (.4, 2.0, .05), (.6 + math.cos(a) * .8, math.sin(a) * .8, 4.9), 'leaf', None, (-20 if i % 2 else 20, 0, math.degrees(a) + 90))
      for i, a in enumerate([k * math.pi / 3 for k in range(6)])],
    *[('Body', 'ball', (.25, .25, .25), (.6 + p[0], p[1], 4.75), 'brown') for p in ring(3, .2, 0)],
])
build('Rock', [('Body', 'ball', (1.4, 1.1, .8), (0, 0, .3), 'grey'), ('Body', 'ball', (.7, .6, .5), (.5, .3, .2), 'greyD')])
build('Torch', [('Body', 'cyl', (.1, .1, 1.8), (0, 0, .9), 'woodD', None, None, 6), ('Body', 'cyl', (.25, .25, .2), (0, 0, 1.85), 'greyD', None, None, 6),
                ('Body', 'cone', (.22, .22, .45), (0, 0, 2.15), 'flame', None, None, 5)], sockets=[('Flame', (0, 0, 2.15))])
build('Crate', [('Body', 'box', (.8, .8, .8), (0, 0, .4), 'wood'), ('Body', 'box', (.82, .82, .12), (0, 0, .4), 'woodD'), ('Body', 'box', (.12, .82, .82), (0, 0, .4), 'woodD')])

# The keep behind the town. Its upper room is the main menu: a fixed camera (Socket_Camera) looks out to sea
# through the big arched window, like the 2D game's shop screen. Front (window) wall at y = 0, room toward +y.
def box(x0, x1, y0, y1, z0, z1, col):
    return ('Body', 'box', (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), col)
W0, W1, SILL, LINTEL, D = -4.5, 4.5, 4.2, 9.4, 10.4   # window opening, room depth
build('Keep', [
    box(-11, W0, -.6, .6, 0, 13.4, 'stoneD'), box(W1, 11, -.6, .6, 0, 13.4, 'stoneD'),          # window piers
    box(W0, W1, -.6, .6, 0, SILL, 'stoneD'), box(W0, W1, -.6, .6, LINTEL, 13.4, 'stoneD'),     # under the sill, over the lintel
    box(W0 - .1, W1 + .1, -.9, .7, SILL - .25, SILL, 'stone'),                                  # the sill
    *[box(W0, W0 + (3 - k) * .45, -.6, .6, LINTEL - (k + 1) * .4, LINTEL - k * .4, 'stoneD') for k in range(3)],   # stepped arch corners
    *[box(W1 - (3 - k) * .45, W1, -.6, .6, LINTEL - (k + 1) * .4, LINTEL - k * .4, 'stoneD') for k in range(3)],
    box(-11, 11, .6, D, 2.6, 3.0, 'woodD'),                                                      # floor of the room
    box(-11, -10.2, .6, D, 0, 13.4, 'stoneD'), box(10.2, 11, .6, D, 0, 13.4, 'stoneD'),         # side walls
    box(-11, 11, D - .4, D + .2, 0, 13.4, 'stoneD'), box(-11, 11, .6, D, 12.9, 13.4, 'stone'),  # back wall, ceiling
    *[box(x - .55, x + .55, -.7, .7, 13.4, 14.3, 'stone') for x in (-10.4, -7.8, -5.2, -2.6, 0, 2.6, 5.2, 7.8, 10.4)],   # battlements
    *[box(x - .55, x + .55, D - .8, D + .2, 13.4, 14.3, 'stone') for x in (-10.4, -5.2, 0, 5.2, 10.4)],
    box(-10.15, -10.05, 1.4, 3.0, 4.6, 11.5, 'red'), box(10.05, 10.15, 1.4, 3.0, 4.6, 11.5, 'red'),   # banners
    box(-10.12, -10.0, 1.9, 2.5, 7.5, 9.5, 'gold'), box(10.0, 10.12, 1.9, 2.5, 7.5, 9.5, 'gold'),
    box(W0 - .9, W0 - .2, .6, 1.1, 6.2, 6.5, 'greyD'), box(W1 + .2, W1 + .9, .6, 1.1, 6.2, 6.5, 'greyD'),   # torch sconces
    ('Body', 'cone', (.35, .35, .6), (W0 - .55, .85, 6.85), 'flame', None, None, 5), ('Body', 'cone', (.35, .35, .6), (W1 + .55, .85, 6.85), 'flame', None, None, 5),
    box(-3.6, -.8, 3.2, 4.8, 3.0, 4.0, 'wood'), box(-3.4, -1.0, 3.4, 4.6, 4.0, 4.03, 'sand'),          # map table
    ('Body', 'cyl', (.12, .12, .3), (-1.5, 4.2, 4.17), 'white', None, None, 6), ('Body', 'cone', (.08, .08, .14), (-1.5, 4.2, 4.38), 'flame', None, None, 5),
    box(3.6, 5.2, 4.0, 5.2, 3.0, 4.2, 'wood'), box(3.75, 5.05, 4.15, 5.05, 4.2, 4.7, 'woodD'),      # a chest of coins
    *[('Body', 'ball', (.22, .22, .1), (4.0 + .3 * (i % 4), 4.4 + .25 * (i // 4), 4.75), 'gold') for i in range(8)],
], sockets=[('Camera', (0, D - 1.2, 6.6)), ('TorchL', (W0 - .55, .85, 7.0)), ('TorchR', (W1 + .55, .85, 7.0)), ('Candle', (-1.5, 4.2, 4.45))])
build('VolleyNet', [
    ('Body', 'cyl', (.1, .1, 2.6), (-3.6, 0, 1.3), 'white', None, None, 6), ('Body', 'cyl', (.1, .1, 2.6), (3.6, 0, 1.3), 'white', None, None, 6),
    ('Body', 'box', (7.2, .03, .9), (0, 0, 2.05), 'greyD'), ('Body', 'box', (7.2, .06, .08), (0, 0, 2.5), 'white'),
])
build('Volleyball', [('Body', 'ball', (.32, .32, .32), (0, 0, 0), 'white'), ('Body', 'tor', (.42, .42, .5), (0, 0, 0), 'yellow', None, (90, 0, 0), 8),
                     ('Body', 'tor', (.42, .42, .5), (0, 0, 0), 'blue', None, (0, 90, 0), 8)])

COL.update({'sandD': (.8, .66, .44), 'sandL': (.97, .9, .72), 'core': (.35, 1, .45), 'sea': (.14, .42, .66), 'seaL': (.38, .72, .84),
            'foam': (.95, .98, 1), 'shellP': (.98, .75, .7), 'star': (.95, .5, .25), 'kelp': (.3, .45, .18), 'hazard': (1, .82, .1)})

# The Nuclear Sand Powerplant: what the team defends. A sandcastle reactor: dome, two cooling towers, a glowing core.
def tower(cx):
    out = []
    for i in range(8):
        z = .3 + i * .75
        r = 1.15 + .55 * ((z - 3.4) / 3.2) ** 2          # hyperboloid waist
        out.append(('Body', 'cyl', (r * 2, r * 2, .78), (cx, 1.2, z), 'sandL' if i % 2 else 'sand', None, None, 10))
    out.append(('Body', 'cyl', (2.9, 2.9, .2), (cx, 1.2, 6.2), 'sandD', None, None, 10))
    return out
build('Powerplant', [
    ('Body', 'box', (12, 8, .6), (0, 0, .3), 'sandD'),                                   # foundation
    *[('Body', 'box', (.6, .6, .7), (x, -3.9, .9), 'sand') for x in (-5.4, -3.6, -1.8, 0, 1.8, 3.6, 5.4)],   # little crenellations
    ('Body', 'cyl', (5.2, 5.2, 2.2), (0, -1.0, 1.7), 'sand', None, None, 14),             # reactor drum
    ('Body', 'ball', (5.2, 5.2, 4.4), (0, -1.0, 2.8), 'sandL'),                           # dome
    ('Body', 'cyl', (1.5, 1.5, .25), (0, -3.55, 2.6), 'hazard', None, (90, 0, 0), 12),    # radiation sign
    *[('Body', 'box', (.32, .08, .5), (math.cos(a) * .35, -3.7, 2.6 + math.sin(a) * .35), 'black', None, (0, -math.degrees(a) + 90, 0))
      for a in (math.pi / 2, math.pi / 2 + 2 * math.pi / 3, math.pi / 2 + 4 * math.pi / 3)],
    ('Body', 'ball', (.3, .3, .3), (0, -3.72, 2.6), 'black'),
    ('Body', 'box', (1.6, .2, 1.2), (0, -3.25, 1.2), 'core'),                             # glowing hatch
    *tower(-4.0), *tower(4.0),
    ('Body', 'cyl', (.5, .5, 3.2), (-2.4, 1.2, 2.2), 'steel', None, (0, 90, 0), 8),       # pipes
    ('Body', 'cyl', (.5, .5, 3.2), (2.4, 1.2, 2.2), 'steel', None, (0, 90, 0), 8),
    ('Body', 'cyl', (.08, .08, 2.2), (0, -1, 6.1), 'woodD', None, None, 5),               # flag
    ('Body', 'box', (1.1, .05, .7), (.55, -1, 6.8), 'core'),
], sockets=[('Core', (0, -1, 3.2)), ('SteamL', (-4.0, 1.2, 6.4)), ('SteamR', (4.0, 1.2, 6.4))])

# A breaking wave (1 m long in X; the game stretches it). Front (-Y) is the curling lip, toward the shore.
# A breaking wave (1 m long in X; the game stretches it): a back slope rising to a crest that curls over into a
# foaming lip, front (-Y) toward the shore. Built as thin slabs along the curl so it reads as one curved wall of water.
def curl(points, thick, cols, offset=0.0):
    out = []
    for k in range(len(points) - 1):
        (y0, z0), (y1, z1) = points[k], points[k + 1]
        length = math.hypot(y1 - y0, z1 - z0) + .06
        ang = math.degrees(math.atan2(z1 - z0, y1 - y0))
        out.append(('Body', 'box', (1, length, thick), ((y0 + y1) / 2 * 0, (y0 + y1) / 2, (z0 + z1) / 2 + offset), cols[min(k, len(cols) - 1)], None, (ang, 0, 0)))
    return out
CURL = [(3.2, -.1), (2.4, .3), (1.6, .85), (.9, 1.45), (.25, 1.95), (-.35, 2.25), (-.85, 2.2), (-1.25, 1.95), (-1.5, 1.55), (-1.5, 1.15)]
build('Breaker', [
    *curl(CURL, .32, ['sea', 'sea', 'sea', 'seaL', 'seaL', 'foam', 'foam', 'foam', 'foam']),
    *curl([(y - .12, z - .22) for y, z in CURL[3:]], .2, ['sea', 'seaL', 'seaL', 'seaL', 'seaL', 'seaL']),   # the darker inside of the tube
    ('Body', 'box', (1, 1.4, .22), (0, -1.6, .25), 'foam'),                                                    # spill at the foot
    *[('Body', 'ball', (.35, .35, .3), (x, -1.45, 1.0), 'foam') for x in (-.3, .05, .35)],                   # foam blobs on the lip
])
build('Surfboard', [('Body', 'box', (.5, 1.8, .07), (0, 0, .035), 'white'), ('Body', 'cone', (.5, .5, .07), (0, -.98, .035), 'white', None, (90, 0, 0), 8),
                    ('Body', 'box', (.08, 1.85, .075), (0, 0, .04), 'red'), ('Body', 'cone', (.1, .25, .2), (0, .75, -.08), 'greyD', None, (180, 0, 0), 4)])
build('Pearl', [('Body', 'ball', (.32, .32, .32), (0, 0, .16), 'gold'), ('Body', 'ball', (.12, .12, .12), (-.06, -.08, .24), 'white')])

# The giant clam: rare, drops golden pearls. Its lid (Limb_Lid) hinges at the back; open, it's soft inside.
build('Clam', [
    ('Body', 'ball', (1.3, 1.1, .5), (0, 0, .25), 'shellP'),
    *[('Body', 'box', (.07, 1.05, .1), (x, 0, .45), 'shell') for x in (-.45, -.22, 0, .22, .45)],
    ('Body', 'ball', (.9, .75, .25), (0, 0, .48), 'pink'),                      # the soft inside
    ('Body', 'ball', (.3, .3, .3), (0, -.05, .62), 'gold'),                     # the pearl
    ('Body', 'cyl', (.05, .05, .25), (-.16, -.38, .7), 'pink', None, None, 5), ('Body', 'cyl', (.05, .05, .25), (.16, -.38, .7), 'pink', None, None, 5),
    ('Body', 'ball', (.1, .1, .1), (-.16, -.38, .85), 'white'), ('Body', 'ball', (.1, .1, .1), (.16, -.38, .85), 'white'),
    ('Limb_Lid', 'ball', (1.3, 1.1, .5), (0, -.55, .1), 'shellP', (0, .55, .45)),
    *[('Limb_Lid', 'box', (.07, 1.05, .1), (x, -.55, .32), 'shell', (0, .55, .45)) for x in (-.45, -.22, 0, .22, .45)],
])

# Buildable defences (paid in sand dollars and golden pearls)
build('Barricade', [*[('Body', 'box', (.8, .45, .3), (x + (.2 if r % 2 else 0), 0, .15 + r * .28), 'sandD' if (r + k) % 2 else 'sand')
                      for r in range(3) for k, x in enumerate((-1.0, -.2, .6)) if not (r == 2 and k == 2)],
                    ('Body', 'cyl', (.05, .05, 1.2), (1.2, 0, .6), 'woodD', None, None, 5), ('Body', 'box', (.4, .02, .3), (1.4, 0, 1.05), 'red')])
build('Spikes', [('Body', 'box', (2.2, 2.2, .08), (0, 0, .04), 'purpleD'),
                 *[('Body', 'cone', (.14, .14, .5), (-.9 + (i % 5) * .45, -.9 + (i // 5) * .45, .3), 'purple', None, None, 4) for i in range(25)]])
build('KelpSnare', [('Body', 'cyl', (2.6, 2.6, .06), (0, 0, .03), 'kelp', None, None, 10),
                    *[('Body', 'box', (.1, .05, .7), (math.cos(a) * r, math.sin(a) * r, .35), 'kelp', None, (math.sin(a * 3) * 25, 0, math.degrees(a)))
                      for i, (a, r) in enumerate([(k * .9, .4 + (k % 3) * .35) for k in range(12)])]])
build('HarpoonTurret', [
    ('Body', 'cyl', (1.2, 1.2, .4), (0, 0, .2), 'woodD', None, None, 8), ('Body', 'cyl', (.3, .3, .8), (0, 0, .7), 'greyD', None, None, 6),
    ('Part_Head', 'box', (.5, .9, .35), (0, -.1, 0), 'wood', (0, 0, 1.2)), ('Part_Head', 'cyl', (.1, .1, 1.3), (0, -.7, .1), 'steel', (0, 0, 1.2), (90, 0, 0), 6),
    ('Part_Head', 'cone', (.18, .18, .3), (0, -1.4, .1), 'steel', (0, 0, 1.2), (90, 0, 0), 4),
], sockets=[('Muzzle', (0, -1.55, 1.3), 'Part_Head')])
build('PearlCannon', [
    ('Body', 'cyl', (1.6, 1.6, .5), (0, 0, .25), 'stoneD', None, None, 10), ('Body', 'ball', (1.0, 1.0, 1.0), (0, 0, .8), 'stone'),
    ('Part_Head', 'cyl', (.5, .5, 1.6), (0, -.7, .1), 'greyD', (0, 0, 1.1), (80, 0, 0), 10),
    ('Part_Head', 'cyl', (.6, .6, .12), (0, -1.45, .25), 'gold', (0, 0, 1.1), (80, 0, 0), 10),
], sockets=[('Muzzle', (0, -1.6, 1.45), 'Part_Head')])
build('BaitBucket', [('Body', 'cyl', (.6, .6, .6), (0, 0, .3), 'blue', None, None, 10), ('Body', 'tor', (.6, .6, .5), (0, 0, .62), 'steel', None, None, 10),
                     *[('Body', 'cone', (.12, .3, .1), (math.cos(a) * .15, math.sin(a) * .15, .7), 'fishG', None, (70, 0, math.degrees(a))) for a in (0, 2.1, 4.2)]])

build('SandDollar', [
    ('Body', 'cyl', (.36, .36, .05), (0, 0, .03), 'sandL', None, None, 12),
    *[('Body', 'box', (.05, .13, .012), (math.sin(a) * .09, math.cos(a) * .09, .06), 'sandD', None, (0, 0, -math.degrees(a))) for a in [k * 2 * math.pi / 5 for k in range(5)]],
    ('Body', 'cyl', (.05, .05, .012), (0, 0, .06), 'sandD', None, None, 6),
])
build('Cloud', [('Body', 'ball', (6, 4, 2.2), (0, 0, 0), 'white'), ('Body', 'ball', (4, 3.4, 2.6), (2.6, .4, .6), 'white'),
                ('Body', 'ball', (3.6, 3, 2), (-2.8, -.3, .3), 'white'), ('Body', 'ball', (3, 2.6, 2.4), (.8, -.6, 1.1), 'white')])
build('Shell', [('Body', 'cone', (.35, .3, .14), (0, 0, .05), 'shellP', None, (90, 0, 0), 7), ('Body', 'ball', (.12, .12, .08), (0, -.16, .04), 'shellP')])
build('Starfish', [*[('Body', 'box', (.07, .22, .04), (math.sin(a) * .11, math.cos(a) * .11, .02), 'star', None, (0, 0, -math.degrees(a)))
                     for a in [k * 2 * math.pi / 5 for k in range(5)]], ('Body', 'cyl', (.12, .12, .05), (0, 0, .025), 'star', None, None, 8)])
build('Sandcastle', [
    ('Body', 'box', (1.6, 1.6, .5), (0, 0, .25), 'sandD'),
    *[('Body', 'cyl', (.5, .5, .9), (x, y, .7), 'sand', None, None, 8) for x, y in ((-.6, -.6), (.6, -.6), (-.6, .6), (.6, .6))],
    ('Body', 'cyl', (.8, .8, 1.2), (0, 0, .9), 'sandL', None, None, 8), ('Body', 'cone', (.8, .8, .5), (0, 0, 1.75), 'sandD', None, None, 8),
    ('Body', 'cyl', (.03, .03, .5), (0, 0, 2.2), 'woodD', None, None, 4), ('Body', 'box', (.3, .02, .18), (.15, 0, 2.35), 'red'),
])
build('Kelp', [*[('Body', 'box', (.12, .05, .9), (math.sin(i) * .1, 0, .45 + i * .8), 'kelp', None, (math.sin(i * 1.7) * 14, 0, i * 25)) for i in range(4)],
               ('Body', 'ball', (.15, .15, .15), (0, 0, 3.2), 'kelp')])

# =====================================================================================  WEBSITE MODELS (food, gas can, tire, glizzy)
def import_glb(fname, target_size, name, axis_hint=None):
    """Import a website .glb, normalise its size so its largest side is target_size, centre it on the ground."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, fname))
    objs = [o for o in bpy.context.selected_objects]
    for o in objs: o.name = name + '_' + o.name        # free the plain name for the export root
    meshes = [o for o in objs if o.type == 'MESH']
    # Keep imported models game-light: decimate anything above ~8k faces in total (the sculpted Glizzy Monster).
    # Weld duplicate vertices first: the sculpt is stored as split triangles, which decimation would shred.
    for o in meshes:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bm.to_mesh(o.data); bm.free()
    faces = sum(len(o.data.polygons) for o in meshes)
    if faces > 8000:
        for o in meshes:
            mod = o.modifiers.new('Decimate', 'DECIMATE')
            mod.ratio = 8000 / faces
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    s = target_size / max(hi - lo)
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for o in objs:
        if o.parent is None:
            o.parent = root
    root.scale = (s, s, s)
    root.location = (-(lo.x + hi.x) / 2 * s, -(lo.y + hi.y) / 2 * s, -lo.z * s)
    bpy.context.view_layer.update()
    # bake the normalisation into the children so the exported root is clean
    for o in root.children_recursive:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    root.scale = (1, 1, 1); root.location = (0, 0, 0)
    for o in objs:
        if o.parent is None: o.parent = root
    for o in meshes:
        o.name = 'Body' if len(meshes) == 1 else o.name
    return root, (hi - lo) * s

for fname, size, name in (('burger.glb', .35, 'Burger'), ('fries.glb', .32, 'Fries'), ('cup.glb', .38, 'Cup'), ('hotdog.glb', .42, 'Hotdog'),
                          ('donut.glb', .3, 'Donut'), ('gascan.glb', .6, 'GasCan'), ('tire.glb', .8, 'Tire'), ('glizzymonster.glb', 3.2, 'GlizzyMonster')):
    root, dims = import_glb(fname, size, name)
    if name == "GlizzyMonster":   # the source sculpt is untextured white: make it a sausage
        for o in root.children_recursive:
            for m in (o.data.materials if o.type == "MESH" else []):
                m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.62, .16, .08, 1)
                m.diffuse_color = (.62, .16, .08, 1)
    export(root)

# Hotdog stand: a cart on two of the website's tires, with ketchup, mustard and ??? sauce bottles on the counter.
root, dims = import_glb('tire.glb', .7, 'TireTmp')
tire_parts = list(root.children_recursive)
bpy.data.objects.remove(root, do_unlink=True)
thin = min(range(3), key=lambda k: dims[k])          # the axle is the tire's thinnest axis
stand = bpy.data.objects.new('HotdogStand', None)
bpy.context.scene.collection.objects.link(stand)
# Two wheels on the cart's long sides: turn the tire so its axle (thinnest axis) runs along Y.
rot = {0: Euler((0, 0, math.pi / 2)), 1: Euler((0, 0, 0)), 2: Euler((math.pi / 2, 0, 0))}[thin].to_matrix().to_4x4()
centre = Matrix.Translation((0, 0, -dims[2] / 2))
for side in (-1, 1):
    for o in tire_parts:
        d = o if side == -1 else o.copy()
        if side == 1:
            if o.data: d.data = o.data.copy()
            bpy.context.scene.collection.objects.link(d)
        d.matrix_world = Matrix.Translation((.35, side * .55, .36)) @ rot @ centre @ o.matrix_world
        d.name = 'Tire_' + ('L' if side < 0 else 'R')
        d.parent = stand
bpy.ops.object.select_all(action='DESELECT')
for p in [
    ('Cart', 'box', (1.8, .9, .8), (0, 0, .95), 'red'), ('Cart', 'box', (1.82, .92, .15), (0, 0, .75), 'white'),
    ('Cart', 'box', (1.9, 1.0, .06), (0, 0, 1.38), 'steel'), ('Cart', 'cyl', (.06, .06, 1.8), (.6, .3, 2.2), 'white', None, None, 6),
    ('Cart', 'cone', (2.2, 2.2, .55), (.6, .3, 3.1), 'yellow', None, None, 8), ('Cart', 'cone', (1.1, 1.1, .28), (.6, .3, 3.27), 'red', None, None, 8),
    ('Cart', 'cyl', (.12, .12, .3), (-.6, -.25, 1.56), 'red', None, None, 6), ('Cart', 'cone', (.08, .08, .1), (-.6, -.25, 1.76), 'red', None, None, 5),
    ('Cart', 'cyl', (.12, .12, .3), (-.4, -.25, 1.56), 'yellow', None, None, 6), ('Cart', 'cone', (.08, .08, .1), (-.4, -.25, 1.76), 'yellow', None, None, 5),
    ('Cart', 'cyl', (.12, .12, .3), (-.2, -.25, 1.56), 'purple', None, None, 6), ('Cart', 'cone', (.08, .08, .1), (-.2, -.25, 1.76), 'purple', None, None, 5),
    ('Cart', 'box', (.9, .05, .3), (0, -.47, 1.0), 'yellow'),
]:
    p = list(p) + [None] * (8 - len(p))
    o = piece(p[0], p[1], p[2], p[3], p[4], (0, 0, 0), p[6] or (0, 0, 0), p[7] or 8)
    o.parent = stand
carts = [o for o in stand.children if o.name.startswith('Cart')]
bpy.ops.object.select_all(action='DESELECT')
for o in carts: o.select_set(True)
bpy.context.view_layer.objects.active = carts[0]
bpy.ops.object.join()
bpy.context.view_layer.objects.active.name = 'Body'
export(stand)

print('TIDEWALL_MODELS_DONE', sorted(os.listdir(OUT)))
