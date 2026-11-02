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
import bpy, bmesh, math, os, random, sys
from mathutils import Matrix, Vector, Euler
from mathutils.bvhtree import BVHTree

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

# Every model passes through here, so one change upgrades them all: small bevels that catch the light, then shading
# baked into vertex colours (material colour * this, in the TideWall/Surface shader; the Part colours stay as they are).
BEVEL_MAX_FACES, BAKE_MAX_VERTS = 1500, 8000
HEMI = [Vector(d).normalized() for d in ((0, 0, 1), (.7, 0, .7), (-.7, 0, .7), (0, .7, .7), (0, -.7, .7), (.9, .4, .2), (-.9, -.4, .2), (.4, -.9, .2), (-.4, .9, .2))]

def bevel(o):
    me = o.data
    if len(me.polygons) > BEVEL_MAX_FACES or not me.polygons: return
    dims = sorted(d for d in o.dimensions if d > 1e-4)
    if not dims: return
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(35)]
    if edges:
        made = bmesh.ops.bevel(bm, geom=edges, offset=min(.03, dims[0] * .07), segments=1, profile=.5, affect='EDGES', clamp_overlap=True)['faces']
        # A new face takes the colour of an original neighbour (corner faces, of a new one already settled)
        todo, done = set(made), None
        while todo and todo != done:
            done = set(todo)
            for f in list(todo):
                near = [g for e in f.edges for g in e.link_faces if g is not f and g not in todo]
                if near: f.material_index = near[0].material_index; todo.discard(f)
    bm.to_mesh(me); bm.free()

def bake(root, meshes):
    """Ambient occlusion (short rays against the whole model, so crossing parts darken each other), darker toward the
    ground, and a slight random tone per face: the hand-painted low-poly look."""
    bpy.context.view_layer.update()
    verts, tris = [], []
    for o in meshes:
        n = len(verts)
        verts += [o.matrix_world @ v.co for v in o.data.vertices]
        tris += [[i + n for i in p.vertices] for p in o.data.polygons]
    if not verts: return
    tree = BVHTree.FromPolygons(verts, tris, epsilon=1e-5)
    lo = Vector([min(c) for c in zip(*verts)])
    hi = Vector([max(c) for c in zip(*verts)])
    span = max((hi - lo).length, .01)
    reach = min(max(span * .22, .08), 1.6)
    use_rays = len(verts) <= BAKE_MAX_VERTS
    rng = random.Random(root.name)
    for o in meshes:
        me = o.data
        attr = me.color_attributes.new('Shade', 'FLOAT_COLOR', 'CORNER')
        rot = o.matrix_world.to_3x3()
        for poly in me.polygons:
            tone = rng.uniform(.95, 1.03)
            nrm = (rot @ poly.normal).normalized()
            for li in poly.loop_indices:
                co = o.matrix_world @ me.vertices[me.loops[li].vertex_index].co
                # pulled slightly toward the face's middle so a corner doesn't start inside its neighbour
                mid = o.matrix_world @ poly.center
                start = co.lerp(mid, .08) + nrm * min(.01, span * .002)
                open_ = 1.0
                if use_rays:
                    tangent = nrm.to_track_quat('Z', 'Y').to_matrix()
                    hits = sum(tree.ray_cast(start, tangent @ d, reach)[0] is not None for d in HEMI)
                    open_ = 1 - .68 * hits / len(HEMI)
                h = (co.z - lo.z) / max(hi.z - lo.z, .001)
                v = min(1, open_ * (.8 + .2 * min(1, h * 1.6)) * tone)
                attr.data[li].color = (v, v, v, 1)
        me.color_attributes.active_color = attr

def finish(root):
    meshes = [c for c in root.children_recursive if c.type == 'MESH']
    for o in meshes: bevel(o)
    bake(root, meshes)

def export(root):
    finish(root)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, root.name + '.fbx'), use_selection=True,
        object_types={'EMPTY', 'MESH'}, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='COPY', embed_textures=True, colors_type='LINEAR')
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
        if p[0].endswith('L') and (p[0].startswith('Limb_') or p[0][:8] == 'Part_Leg' and p[0][8].isdigit()):
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
# TideWall 3D — the creatures, redrawn. Limb names and overall sizes are as before (R5: the data's limbs still match); what's
# new is the shape language (big readable silhouettes, eyes you can see from the wall, plates, teeth, undersides)
# and jointed Part_* pieces the game animates (legs, flippers, jaws, body segments, antennae).
COL.update({'crabD': (.72, .25, .08), 'crabL': (1, .72, .45), 'hermD': (.55, .1, .08), 'shellD': (.55, .38, .3), 'sharkD': (.2, .26, .32),
            'gum': (.75, .2, .28), 'barn': (.72, .7, .62), 'rag': (.22, .24, .2), 'bone': (.92, .88, .75), 'lobD': (.5, .07, .06), 'lobL': (.92, .4, .22),
            'jellyG': (.7, .95, 1), 'octD': (.5, .1, .14), 'octL': (.95, .55, .5), 'turtleL': (.5, .62, .3), 'turtleB': (.78, .72, .42), 'pale': (.85, .92, .8), 'eelL': (.5, .62, .4),
            'puffL': (1, .95, .7), 'gullD': (.35, .38, .42)})

def eye(name, x, y, z, joint=None, r=.1, white='white', pupil='black', look=-1):
    """A cartoon eye: a white ball with a black pupil on the front."""
    return [(name, 'ball', (r * 2, r * 2, r * 2), (x, y, z), white, joint),
            (name, 'ball', (r, r * .7, r), (x, y + look * r * .75, z), pupil, joint)]

def teeth(name, n, x0, x1, y, z, h, joint=None, down=True, col='bone'):
    out = []
    for i in range(n):
        x = x0 + (x1 - x0) * i / max(1, n - 1)
        out.append((name, 'cone', (h * .7, h * .7, h), (x, y, z), col, joint, (180 if down else 0, 0, 0), 4))
    return out

def leg(name, joint, reach, drop, col, tip):
    """A jointed leg, out and then down, on the left side (mirror makes the right)."""
    return [(name, 'box', (reach, .08, .08), (-reach * .5, 0, .03), col, joint, (0, 18, 0)),
            (name, 'box', (.07, .07, drop), (-reach * .95, 0, -drop * .42), tip, joint, (0, -12, 0)),
            (name, 'cone', (.07, .07, .1), (-reach * 1.0, 0, -drop * .95), tip, joint, (180, 0, 0), 4)]

# ---- Crabling: a wide shell with a ridge, big unequal claws, stalk eyes, six walking legs
build('Crab', mirror([
    ('Body', 'ball', (.95, .78, .36), (0, 0, .42), 'orange'),
    ('Body', 'ball', (.7, .55, .2), (0, .05, .55), 'crabL'),
    ('Body', 'ball', (.72, .62, .22), (0, -.02, .28), 'crabD'),
    *[('Body', 'cone', (.08, .08, .1), (x, -.3, .55), 'crabD', None, None, 4) for x in (-.28, -.14, 0, .14, .28)],
    ('Body', 'box', (.26, .04, .05), (0, -.38, .36), 'black'),
    *teeth('Body', 4, -.09, .09, -.39, .33, .05),
    *[p for k, (y) in enumerate((-.18, .04, .24)) for p in leg('Part_Leg%dL' % k, (-.38, y, .34), .32, .3, 'orange', 'crabD')],
    ('Limb_ClawL', 'box', (.13, .28, .13), (-.05, -.12, 0), 'orange', (-.42, -.28, .42)),
    ('Limb_ClawL', 'ball', (.3, .34, .22), (-.1, -.34, .02), 'crabD', (-.42, -.28, .42)),
    ('Limb_ClawL', 'cone', (.11, .11, .22), (-.15, -.54, .06), 'crabL', (-.42, -.28, .42), (90, 0, 8), 5),
    ('Limb_ClawL', 'cone', (.09, .09, .18), (-.05, -.52, -.03), 'crabL', (-.42, -.28, .42), (90, 0, -8), 5),
    ('Limb_Eyes', 'cyl', (.05, .05, .24), (-.12, 0, .1), 'orange', (0, -.28, .5)),
    ('Limb_Eyes', 'cyl', (.05, .05, .24), (.12, 0, .1), 'orange', (0, -.28, .5)),
    *eye('Limb_Eyes', -.12, 0, .25, (0, -.28, .5), .08),
    *eye('Limb_Eyes', .12, 0, .25, (0, -.28, .5), .08),
]))

# ---- Hermit crab: a spiral whelk shell far too big for it, and the Mark goes on top
shell = [('Limb_Shell', 'ball', (.76, .8, .7), (0, .15, .22), 'shell', (0, .1, .3))]
for k in range(4):
    shell.append(('Limb_Shell', 'ball', (.62 - k * .13, .6 - k * .12, .46 - k * .08), (0, .28 + k * .15, .34 + k * .09), 'shellD' if k % 2 else 'shell', (0, .1, .3), (-30, 0, 0)))
shell.append(('Limb_Shell', 'cone', (.2, .2, .3), (0, .78, .68), 'shell', (0, .1, .3), (-55, 0, 0), 6))
shell += [('Limb_Shell', 'cone', (.08, .08, .14), (x, y, z), 'shellD', (0, .1, .3), None, 4) for x, y, z in ((-.3, .2, .5), (.3, .2, .5), (-.36, -.02, .26), (.36, -.02, .26))]
build('HermitCrab', mirror([
    ('Body', 'ball', (.45, .5, .32), (0, -.12, .25), 'red'),
    ('Body', 'ball', (.3, .22, .16), (0, -.3, .2), 'hermD'),
    *shell,
    *[p for k, y in enumerate((-.16, .02)) for p in leg('Part_Leg%dL' % k, (-.18, y - .1, .22), .22, .22, 'red', 'hermD')],
    ('Limb_ClawL', 'ball', (.24, .3, .18), (-.08, -.16, 0), 'hermD', (-.18, -.3, .25)),
    ('Limb_ClawL', 'cone', (.08, .08, .16), (-.1, -.33, .03), 'red', (-.18, -.3, .25), (90, 0, 0), 5),
    ('Limb_Eyes', 'cyl', (.04, .04, .18), (-.07, 0, .08), 'red', (0, -.3, .35)),
    ('Limb_Eyes', 'cyl', (.04, .04, .18), (.07, 0, .08), 'red', (0, -.3, .35)),
    *eye('Limb_Eyes', -.07, 0, .2, (0, -.3, .35), .065),
    *eye('Limb_Eyes', .07, 0, .2, (0, -.3, .35), .065),
]))

# ---- Drowner: a hunched, bloated fish-man in a rotted oilskin: gills, a fin crest, barnacles, webbed claws, a gaping mouth
build('Drowner', mirror([
    ('Limb_LegL', 'box', (.2, .24, .5), (0, 0, -.24), 'fishG', (-.16, 0, .85)),
    ('Limb_LegL', 'box', (.16, .2, .42), (0, .03, -.62), 'rag', (-.16, 0, .85)),
    ('Limb_LegL', 'box', (.24, .42, .08), (0, -.12, -.82), 'fishG', (-.16, 0, .85)),
    ('Body', 'ball', (.72, .58, .95), (0, .1, 1.2), 'fishG', None, (-22, 0, 0)),
    ('Body', 'ball', (.5, .3, .62), (0, -.13, 1.12), 'pale', None, (-22, 0, 0)),
    ('Body', 'ball', (.84, .62, .42), (0, .12, .84), 'rag'),
    ('Body', 'cone', (.3, .12, .55), (-.24, .28, .62), 'rag', None, (172, 0, 6), 4),
    *[('Body', 'ball', (.1, .1, .07), (x, y, z), 'barn') for x, y, z in ((.24, .32, 1.35), (-.2, .36, 1.2), (.05, .4, 1.05), (.32, .15, 1.0))],
    *[('Body', 'cone', (.05, .2, .22), (0, .36 - k * .02, 1.65 - k * .22), 'teal', None, (-20, 0, 0), 4) for k in range(3)],
    ('Limb_ArmL', 'ball', (.22, .22, .22), (0, 0, 0), 'fishG', (-.36, -.03, 1.45)),
    ('Limb_ArmL', 'box', (.14, .62, .14), (-.02, -.3, -.1), 'fishG', (-.36, -.03, 1.45), (-8, 0, 0)),
    ('Limb_ArmL', 'ball', (.22, .2, .1), (-.02, -.66, -.14), 'fishL', (-.36, -.03, 1.45)),
    *[('Limb_ArmL', 'cone', (.05, .05, .15), (x - .02, -.8, -.14), 'bone', (-.36, -.03, 1.45), (90, 0, 0), 4) for x in (-.07, 0, .07)],
    ('Limb_Head', 'ball', (.44, .48, .4), (0, -.05, .2), 'fishL', (0, -.14, 1.55)),
    ('Limb_Head', 'ball', (.34, .24, .14), (0, -.2, .05), 'gum', (0, -.14, 1.55)),
    *teeth('Limb_Head', 4, -.1, .1, -.28, .1, .08, (0, -.14, 1.55)),
    *[('Limb_Head', 'box', (.02, .1, .1), (s * .2, .02, .2 + k * .06), 'teal', (0, -.14, 1.55)) for s in (-1, 1) for k in range(2)],
    ('Limb_Head', 'cone', (.07, .34, .34), (0, .12, .46), 'teal', (0, -.14, 1.55), None, 4),
    *eye('Limb_Head', -.12, -.2, .26, (0, -.14, 1.55), .075, 'yellow'),
    *eye('Limb_Head', .12, -.2, .26, (0, -.14, 1.55), .075, 'yellow'),
]))

# ---- Gull: a mean little bomber with heavy brows, a hooked beak, tucked feet and proper wing tips
build('Gull', mirror([
    ('Body', 'ball', (.38, .68, .34), (0, .02, 0), 'white'),
    ('Body', 'ball', (.3, .5, .16), (0, .05, -.1), 'grey'),
    ('Body', 'ball', (.27, .27, .27), (0, -.33, .1), 'white'),
    ('Body', 'cone', (.11, .11, .26), (0, -.53, .07), 'yellow', None, (90, 0, 0), 5),
    ('Body', 'cone', (.05, .05, .07), (0, -.65, .03), 'red', None, (180, 0, 0), 4),
    *eye('Body', -.09, -.43, .15, None, .04),
    *eye('Body', .09, -.43, .15, None, .04),
    ('Body', 'box', (.09, .06, .02), (-.09, -.44, .2), 'black', None, (0, -18, 0)),
    ('Body', 'box', (.09, .06, .02), (.09, -.44, .2), 'black', None, (0, 18, 0)),
    ('Body', 'box', (.24, .28, .04), (0, .42, .02), 'gullD'),
    ('Body', 'box', (.05, .14, .05), (-.07, .24, -.17), 'orange'), ('Body', 'box', (.05, .14, .05), (.07, .24, -.17), 'orange'),
    ('Limb_WingL', 'box', (.48, .3, .04), (-.24, .02, 0), 'white', (-.15, 0, .05)),
    ('Limb_WingL', 'box', (.34, .22, .04), (-.62, .05, .03), 'grey', (-.15, 0, .05), (0, -8, 0)),
    ('Limb_WingL', 'box', (.2, .16, .045), (-.88, .08, .06), 'black', (-.15, 0, .05), (0, -12, 0)),
]))

# ---- Puffer: an offended beach ball with enormous eyes, kissy lips, side fins and a tail fin
build('Puffer', [
    ('Body', 'ball', (.85, .85, .85), (0, 0, .5), 'yellow'),
    ('Body', 'ball', (.66, .6, .42), (0, -.1, .4), 'puffL'),
    *eye('Body', -.2, -.32, .68, None, .13),
    *eye('Body', .2, -.32, .68, None, .13),
    ('Body', 'box', (.16, .06, .03), (-.2, -.42, .84), 'orange2', None, (0, -18, 0)),
    ('Body', 'box', (.16, .06, .03), (.2, -.42, .84), 'orange2', None, (0, 18, 0)),
    ('Body', 'tor', (.28, .28, .3), (0, -.42, .46), 'orange', None, (90, 0, 0), 8),
    ('Body', 'ball', (.1, .05, .06), (0, -.46, .46), 'gum'),
    ('Body', 'cone', (.3, .08, .26), (0, .5, .54), 'orange', None, (-90, 0, 0), 4),
    *spikes(16, .41, (0, 0, .5), (.07, .07, .2), 'orange2'),
    ('Part_FliL', 'cone', (.26, .05, .22), (-.1, 0, 0), 'orange', (-.4, -.05, .5), (0, -90, 0), 4),
    ('Part_FliR', 'cone', (.26, .05, .22), (.1, 0, 0), 'orange', (.4, -.05, .5), (0, 90, 0), 4),
])

# ---- Eel: a long body in rippling segments, a dorsal crest, a gaping fanged head and glowing eyes
eel = [
    ('Limb_Head', 'ball', (.34, .5, .28), (0, -.14, .03), 'greyD', (0, -.95, .3)),
    ('Limb_Head', 'ball', (.3, .36, .1), (0, -.22, -.09), 'gum', (0, -.95, .3)),
    *teeth('Limb_Head', 5, -.11, .11, -.36, .0, .09, (0, -.95, .3)),
    *teeth('Limb_Head', 3, -.07, .07, -.36, -.08, .07, (0, -.95, .3), down=False),
    *eye('Limb_Head', -.12, -.22, .13, (0, -.95, .3), .06, 'glow'),
    *eye('Limb_Head', .12, -.22, .13, (0, -.95, .3), .06, 'glow'),
    ('Limb_Head', 'cone', (.05, .2, .2), (0, .02, .2), 'teal', (0, -.95, .3), None, 4),
    ('Body', 'cyl', (.3, .3, .55), (0, -.62, .28), 'greyD', None, (90, 0, 0), 8),
    ('Limb_Tail', 'cone', (.22, .2, .9), (0, .45, 0), 'greyD', (0, .75, .25), (-90, 0, 0), 6),
    ('Limb_Tail', 'box', (.03, .55, .3), (0, .65, .02), 'teal', (0, .75, .25)),
]
for k, y in enumerate((-.3, -.02, .25, .52)):
    r = .3 - k * .012
    eel += [('Part_Seg%d' % k, 'cyl', (r, r, .34), (0, 0, 0), 'greyD', (0, y, .27), (90, 0, 0), 8),
            ('Part_Seg%d' % k, 'box', (r * .7, .32, .05), (0, 0, -r * .46), 'eelL', (0, y, .27)),
            ('Part_Seg%d' % k, 'cone', (.03, .24, .16), (0, 0, r * .55), 'teal', (0, y, .27), None, 4),
            ('Part_Seg%d' % k, 'ball', (.05, .05, .05), (r * .4, 0, .03), 'glow', (0, y, .27)),
            ('Part_Seg%d' % k, 'ball', (.05, .05, .05), (-r * .4, 0, .03), 'glow', (0, y, .27))]
build('Eel', eel)

# ---- Shellback: a domed shell of raised plates with a spiked ridge, a beaked head, heavy flippers
turtle = [
    ('Body', 'ball', (1.25, 1.45, .72), (0, 0, .45), 'greenD'),
    ('Body', 'cyl', (1.36, 1.56, .1), (0, 0, .24), 'brown', None, None, 14),
    ('Body', 'ball', (1.05, 1.25, .3), (0, 0, .17), 'turtleB'),
    ('Body', 'cone', (.18, .22, .2), (0, .78, .22), 'turtleL', None, (-90, 0, 0), 5),
]
for x, y in ((0, -.42), (0, 0), (0, .42), (-.34, -.2), (.34, -.2), (-.34, .22), (.34, .22)):
    z = .78 - (x * x + y * y) * .42
    turtle.append(('Body', 'cyl', (.34, .36, .05), (x, y, z), 'turtleL', None, (y * 28, x * -28, 0), 6))
for y in (-.42, 0, .42):
    turtle.append(('Body', 'cone', (.12, .14, .17), (0, y, .86 - y * y * .35), 'brown', None, None, 4))
for sx in (-1, 1):
    for sy, k in ((-1, 0), (1, 1)):
        turtle += [('Part_Fli%d%s' % (k, 'L' if sx < 0 else 'R'), 'ball', (.3, .52, .13), (sx * .2, sy * .12, -.04), 'turtleL', (sx * .42, sy * .42, .24), (0, sx * 16, 0))]
turtle += [
    ('Limb_Head', 'ball', (.38, .5, .34), (0, -.22, .02), 'turtleL', (0, -.7, .4)),
    ('Limb_Head', 'cone', (.18, .18, .17), (0, -.47, -.02), 'greyD', (0, -.7, .4), (90, 0, 0), 5),
    *eye('Limb_Head', -.13, -.36, .1, (0, -.7, .4), .06),
    *eye('Limb_Head', .13, -.36, .1, (0, -.7, .4), .06),
    ('Limb_Head', 'box', (.12, .05, .02), (-.13, -.38, .17), 'greenD', (0, -.7, .4), (0, -15, 0)),
    ('Limb_Head', 'box', (.12, .05, .02), (.13, -.38, .17), 'greenD', (0, -.7, .4), (0, 15, 0)),
]
build('Turtle', turtle)

# ---- Shark: tapered and countershaded, gill slits, a jaw that drops on the attack, swept fins, a forked tail
build('Shark', [
    ('Body', 'ball', (.92, 2.7, .9), (0, .1, .62), 'shark'),
    ('Body', 'ball', (.78, 2.4, .48), (0, .05, .38), 'belly'),
    ('Body', 'cone', (.58, .75, .8), (0, -1.45, .68), 'shark', None, (90, 0, 0), 8),
    ('Body', 'ball', (.5, .7, .24), (0, -1.08, .47), 'belly'),
    ('Body', 'ball', (.2, .14, .08), (0, -1.1, .55), 'gum'),
    *teeth('Body', 7, -.2, .2, -1.32, .5, .09),
    *eye('Body', -.27, -1.15, .75, None, .08, 'black', 'white', -1),
    *eye('Body', .27, -1.15, .75, None, .08, 'black', 'white', -1),
    ('Body', 'box', (.24, .07, .02), (-.27, -1.17, .86), 'sharkD', None, (0, -15, 0)),
    ('Body', 'box', (.24, .07, .02), (.27, -1.17, .86), 'sharkD', None, (0, 15, 0)),
    *[('Body', 'box', (.02, .03, .24), (sx * .44, y, .62), 'sharkD') for sx in (-1, 1) for y in (-.75, -.62, -.49)],
    ('Part_Jaw', 'ball', (.48, .72, .18), (0, -.32, -.03), 'belly', (0, -.8, .4)),
    *teeth('Part_Jaw', 6, -.17, .17, -.58, .08, .09, (0, -.8, .4), down=False),
    ('Part_Fli0L', 'cone', (.1, .42, .78), (-.36, .1, -.04), 'sharkD', (-.3, -.4, .42), (0, -90, 0), 4),
    ('Part_Fli0R', 'cone', (.1, .42, .78), (.36, .1, -.04), 'sharkD', (.3, -.4, .42), (0, 90, 0), 4),
    ('Limb_Fin', 'cone', (.1, .72, .85), (0, .14, .36), 'sharkD', (0, -.1, .98), None, 5),
    ('Limb_Tail', 'cone', (.4, .4, .9), (0, .4, 0), 'shark', (0, 1.25, .62), (-90, 0, 0), 6),
    ('Limb_Tail', 'cone', (.08, .44, .8), (0, .78, .28), 'sharkD', (0, 1.25, .62), (28, 0, 0), 4),
    ('Limb_Tail', 'cone', (.08, .36, .5), (0, .74, -.16), 'sharkD', (0, 1.25, .62), (152, 0, 0), 4),
])

# ---- Croc: long and low, ridged back, a real snout with interlocking teeth, four splayed legs
croc = [
    ('Body', 'ball', (1.15, 2.2, .5), (0, .08, .42), 'croc'),
    ('Body', 'ball', (.95, 1.95, .3), (0, .05, .28), 'pale'),
]
for y in (-.7, -.35, 0, .35, .7, 1.0):
    for sx in (-.2, .2):
        croc.append(('Body', 'cone', (.17, .22, .17), (sx, y, .68), 'crocD', None, None, 4))
    croc.append(('Body', 'box', (.1, .22, .05), (.4, y, .55), 'crocD')); croc.append(('Body', 'box', (.1, .22, .05), (-.4, y, .55), 'crocD'))
for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    nm = 'Part_Leg%d%s' % (k, 'L' if sx < 0 else 'R')
    croc += [(nm, 'box', (.26, .3, .2), (sx * .1, 0, -.06), 'croc', (sx * .4, sy * .62, .28), (0, sx * 18, 0)),
             (nm, 'box', (.3, .36, .08), (sx * .22, -.1, -.22), 'crocD', (sx * .4, sy * .62, .28))]
hj = (0, -.9, .42)
croc += [
    ('Limb_Head', 'box', (.72, .68, .3), (0, -.26, .03), 'croc', hj),
    ('Limb_Head', 'box', (.5, 1.0, .2), (0, -1.0, .02), 'croc', hj),
    ('Limb_Head', 'box', (.48, 1.0, .1), (0, -1.0, -.16), 'crocD', hj),
    ('Limb_Head', 'box', (.42, .92, .05), (0, -1.0, -.08), 'gum', hj),
    ('Limb_Head', 'ball', (.2, .18, .13), (-.14, -1.45, .13), 'crocD', hj),
    ('Limb_Head', 'ball', (.2, .18, .13), (.14, -1.45, .13), 'crocD', hj),
    *[('Limb_Head', 'cone', (.06, .06, .12), (s * .23, -.62 - k * .17, -.09), 'bone', hj, (180, 0, 0), 4) for s in (-1, 1) for k in range(5)],
    *[('Limb_Head', 'cone', (.05, .05, .1), (s * .2, -.7 - k * .17, -.1), 'bone', hj, None, 4) for s in (-1, 1) for k in range(4)],
    *eye('Limb_Head', -.24, -.32, .22, hj, .09, 'yellow'),
    *eye('Limb_Head', .24, -.32, .22, hj, .09, 'yellow'),
    ('Limb_Head', 'box', (.2, .06, .03), (-.24, -.36, .32), 'crocD', hj, (0, -15, 0)),
    ('Limb_Head', 'box', (.2, .06, .03), (.24, -.36, .32), 'crocD', hj, (0, 15, 0)),
    ('Limb_Tail', 'cone', (.72, .42, 1.85), (0, .9, 0), 'croc', (0, 1.1, .4), (-90, 0, 0), 6),
    *[('Limb_Tail', 'cone', (.05, .18, .18), (0, .25 + k * .32, .2 - k * .03), 'crocD', (0, 1.1, .4), None, 4) for k in range(5)],
]
build('Croc', croc)

# ---- Urchin: a dark ball of long banded spines, with two beady eyes peering out from among them
urchin = [('Body', 'ball', (.72, .72, .72), (0, 0, .42), 'purpleD')]
urchin += spikes(22, .36, (0, 0, .42), (.11, .11, .5), 'purple')
urchin += spikes(22, .49, (0, 0, .42), (.06, .06, .2), 'pinkL')
urchin += eye('Body', -.13, -.33, .5, None, .07, 'yellow') + eye('Body', .13, -.33, .5, None, .07, 'yellow')
build('Urchin', urchin)

# ---- Jellyfish: a ribbed bell over a glowing core, a frill, and trailing tentacles with bends in them
bell = [
    ('Body', 'ball', (1.05, 1.05, .78), (0, 0, 1.4), 'pink'),
    ('Body', 'ball', (.62, .62, .42), (0, 0, 1.3), 'jellyG'),
    ('Body', 'ball', (.3, .3, .24), (0, 0, 1.36), 'glow'),
    ('Body', 'cyl', (.98, .98, .1), (0, 0, 1.08), 'pinkL', None, None, 12),
]
for k in range(10):
    a = k * .628
    bell.append(('Body', 'box', (.07, .13, .16), (math.cos(a) * .5, math.sin(a) * .5, 1.0), 'pink', None, (0, 0, math.degrees(a))))
    bell.append(('Body', 'ball', (.08, .08, .08), (math.cos(a) * .44, math.sin(a) * .44, 1.46), 'jellyG'))
bell += eye('Body', -.15, -.45, 1.42, None, .06) + eye('Body', .15, -.45, 1.42, None, .06)
for i, p in enumerate(ring(4, .28, 1.05, .4)):
    nm = 'Limb_Tent%d' % (i + 1)
    bell += [(nm, 'cyl', (.07, .07, .4), (0, 0, -.2), 'pinkL', p, None, 5),
             (nm, 'cyl', (.055, .055, .35), (.04, .02, -.53), 'pink', p, (8, -8, 0), 5),
             (nm, 'cone', (.05, .05, .32), (.05, .05, -.85), 'jellyG', p, (180, 0, 0), 5)]
build('Jellyfish', bell)

# ---- Octopus: a big bulb of a head, spotted, with slit-pupil eyes under angry brows, a siphon, and suckered tapering arms
oct_parts = [
    ('Body', 'ball', (.95, .95, 1.2), (0, .18, 1.0), 'red'),
    ('Body', 'ball', (.72, .72, .36), (0, 0, .58), 'octD'),
    *[('Body', 'ball', (.13, .1, .13), (x, y, z), 'octD') for x, y, z in ((-.3, .35, 1.25), (.32, .28, 1.05), (.05, .55, 1.3), (-.15, .42, .85), (.25, .48, .8), (0, .32, 1.5))],
    ('Body', 'ball', (.26, .26, .26), (-.23, -.25, .95), 'yellow'),
    ('Body', 'ball', (.26, .26, .26), (.23, -.25, .95), 'yellow'),
    ('Body', 'box', (.15, .04, .05), (-.23, -.38, .95), 'black'),
    ('Body', 'box', (.15, .04, .05), (.23, -.38, .95), 'black'),
    ('Body', 'box', (.3, .08, .05), (-.23, -.34, 1.12), 'octD', None, (0, -18, 0)),
    ('Body', 'box', (.3, .08, .05), (.23, -.34, 1.12), 'octD', None, (0, 18, 0)),
    ('Body', 'cyl', (.13, .13, .28), (.42, -.08, .72), 'octD', None, (0, 60, 0), 6),
]
for i, p in enumerate(ring(6, .35, .45)):
    nm = 'Limb_Tent%d' % (i + 1)
    d = Vector((p[0], p[1], -.45)).normalized()
    a = aim(tuple(d))
    oct_parts += [(nm, 'cone', (.36, .36, 1.05), (p[0] * 1.2, p[1] * 1.2, -.18), 'red2', p, a, 6)]
    for k in (-.32, -.14, .04):
        oct_parts.append((nm, 'ball', (.09, .09, .06), (p[0] * 1.2 + d.x * k, p[1] * 1.2 + d.y * k, -.18 + d.z * k - .09), 'octL', p))
build('Octopus', oct_parts)

# ---- Clawback: an armoured lobster: plated thorax, a segmented tail that sways, huge crusher claws, long antennae
lob = [
    ('Body', 'ball', (.95, 1.15, .62), (0, -.1, .56), 'red'),
    ('Body', 'ball', (.78, .95, .4), (0, -.1, .4), 'lobL'),
    *[('Body', 'cone', (.11, .11, .14), (x, y, .86), 'lobD', None, None, 4) for x, y in ((-.22, -.35), (.22, -.35), (0, -.1), (-.26, .12), (.26, .12))],
    ('Body', 'box', (.92, .05, .08), (0, -.05, .78), 'lobD'),
    *[p for k, y in enumerate((-.3, -.05, .2, .42)) for p in leg('Part_Leg%dL' % k, (-.36, y, .36), .36, .32, 'red', 'lobD')],
    ('Limb_Head', 'ball', (.54, .44, .38), (0, -.15, 0), 'red', (0, -.6, .65)),
    ('Limb_Head', 'cone', (.12, .3, .1), (0, -.38, .13), 'lobD', (0, -.6, .65), (90, 0, 0), 4),
    ('Limb_Head', 'cyl', (.035, .035, 1.1), (-.1, -.55, .3), 'lobD', (0, -.6, .65), (-60, 0, -14), 5),
    ('Limb_Head', 'cyl', (.035, .035, 1.1), (.1, -.55, .3), 'lobD', (0, -.6, .65), (-60, 0, 14), 5),
    ('Limb_Head', 'cyl', (.03, .03, .55), (-.15, -.32, .05), 'lobL', (0, -.6, .65), (-90, 0, -35), 5),
    ('Limb_Head', 'cyl', (.03, .03, .55), (.15, -.32, .05), 'lobL', (0, -.6, .65), (-90, 0, 35), 5),
    *eye('Limb_Head', -.14, -.3, .17, (0, -.6, .65), .08),
    *eye('Limb_Head', .14, -.3, .17, (0, -.6, .65), .08),
    ('Limb_ClawL', 'cyl', (.16, .16, .6), (-.1, -.3, 0), 'red', (-.45, -.55, .5), (90, 0, 0), 6),
    ('Limb_ClawL', 'ball', (.52, .68, .32), (-.16, -.82, .05), 'lobD', (-.45, -.55, .5)),
    ('Limb_ClawL', 'ball', (.3, .4, .18), (-.18, -.8, .17), 'red', (-.45, -.55, .5)),
    ('Limb_ClawL', 'cone', (.2, .2, .44), (-.26, -1.26, .06), 'lobL', (-.45, -.55, .5), (90, 0, 6), 5),
    ('Limb_ClawL', 'cone', (.15, .15, .34), (-.04, -1.22, .0), 'lobL', (-.45, -.55, .5), (90, 0, -6), 5),
    *teeth('Limb_ClawL', 3, -.22, -.06, -1.1, .02, .06, (-.45, -.55, .5)),
]
for k in range(3):
    w = .78 - k * .13
    lob += [('Part_Seg%d' % k, 'ball', (w, .42, .46 - k * .05), (0, .1, 0), 'red', (0, .5 + k * .33, .46 - k * .04)),
            ('Part_Seg%d' % k, 'box', (w * .98, .06, .05), (0, .1, .2 - k * .02), 'lobD', (0, .5 + k * .33, .46 - k * .04))]
lob += [('Part_Seg3', 'box', (.68, .32, .05), (0, .15, 0), 'lobD', (0, 1.5, .34)),
        ('Part_Seg3', 'cone', (.3, .3, .05), (-.26, .24, 0), 'red', (0, 1.5, .34), None, 5)]
build('Lobster', mirror(lob))


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
# A curved trunk and a crown of fronds that arc up and droop, each with a narrowing tip
palm = []
for i in range(6):
    r = .34 - i * .035
    palm.append(('Body', 'cyl', (r, r, 1.0), (i * i * .025, 0, .45 + i * .82), 'brown' if i % 2 else 'woodD', None, (0, i * 2.4, 0), 7))
crown = (.65, 0, 4.75)
for i in range(8):
    a = i * math.pi / 4 + (.2 if i % 2 else 0)
    dx, dy = math.cos(a), math.sin(a)
    deg = math.degrees(a)
    for k, (reach, drop, width, pitch) in enumerate(((.5, .12, .46, -14), (1.2, .08, .42, 12), (1.85, -.22, .3, 34), (2.35, -.68, .15, 58))):
        lift = (.1 if i % 2 else 0)
        palm.append(('Body', 'box', (.82, width, .05), (crown[0] + dx * reach, crown[1] + dy * reach, crown[2] + drop + lift), 'leaf' if (i + k) % 3 else 'greenD', None, (0, pitch, deg)))
palm += [('Body', 'ball', (.26, .26, .26), (crown[0] + p[0], crown[1] + p[1], crown[2] - .18), 'brown') for p in ring(4, .2, 0)]
build('PalmTree', palm)
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

# =====================================================================================  PART II: THE UNDERTOW
# The seabed the sea gave up for one night. The game puts the symbols on these (docs/STORY.md) as decals, so
# the faces they sit on are plain: keep them where the code expects (World/*.cs, the Glyphs.Decal calls).
COL.update({'coral': (.78, .36, .42), 'coralD': (.45, .16, .26), 'egg': (.95, .7, .6), 'kelpD': (.12, .3, .2),
            'wet': (.33, .36, .38), 'wetD': (.2, .22, .25), 'heart': (.45, 1, .75), 'bone': (.88, .84, .74), 'rust': (.5, .26, .14)})

# Brood spire: a twisted coral tower that hatches the sea's creatures. Egg sacs cluster on it; the code puts the
# Spiral at 3.1 m and the Eye at 5.2 m on its face.
spire = []
for i in range(9):
    z = .4 + i * .75
    r = 1.7 - i * .14
    spire.append(('Body', 'cyl', (r * 2, r * 2, .8), (0, 0, z), 'coralD' if i % 2 else 'coral', None, (0, 0, i * 17), 6))
spire += [('Body', 'cone', (.9, .9, 1.4), (0, 0, 7.3), 'coral', None, None, 6)]
for k, (x, y, z, s) in enumerate([(1.1, .5, 1.2, .7), (-1.0, .8, 2.0, .6), (.6, -1.0, 2.6, .55), (-.7, -.6, 3.8, .5), (.8, .3, 4.6, .45),
                                  (-.5, .4, 5.6, .4), (1.4, -.3, .6, .8), (-1.4, -.4, .7, .75)]):
    spire.append(('Body', 'ball', (s, s, s * 1.2), (x, y, z), 'egg'))
for a in range(5):
    ang = a * 72 + 20
    d = (math.cos(math.radians(ang)), math.sin(math.radians(ang)), .35)
    spire.append(('Body', 'cone', (.7, .7, 2.2), (d[0] * 1.6, d[1] * 1.6, .6), 'coralD', None, aim(d), 5))
build('BroodSpire', spire)

# Beacon: an old stone bowl on a pillar, built by Saltmarrow's founders to light the way home. The game lights it.
build('Beacon', [
    ('Body', 'cyl', (1.4, 1.4, .3), (0, 0, .15), 'stoneD', None, None, 5),
    ('Body', 'cyl', (.7, .7, 2.4), (0, 0, 1.5), 'stone', None, None, 5),
    ('Body', 'cone', (1.6, 1.6, .7), (0, 0, 3.0), 'stoneD', None, (180, 0, 0), 5),
    ('Body', 'cyl', (1.5, 1.5, .12), (0, 0, 3.36), 'stone', None, None, 5),
    ('Body', 'box', (.12, .12, 1.1), (0, .38, 1.4), 'kelpD'), ('Body', 'box', (.1, .1, .8), (.3, -.3, 1.0), 'kelpD'),
], sockets=[('Flame', (0, 0, 3.5))])

# Wreck hull: a ship on its side, ribs showing. 1.2 m across (the code's crest sits at 0.62), 5 m long.
hull = [('Body', 'box', (1.2, 5, .25), (0, 0, .12), 'woodD'),
        ('Body', 'box', (.15, 5, 1.6), (.55, 0, .9), 'wood'), ('Body', 'box', (.15, 4.2, 1.2), (-.55, -.4, .7), 'wood'),
        ('Body', 'cone', (1.2, 1.2, 1.2), (0, -2.9, .9), 'woodD', None, (90, 0, 0), 4),
        ('Body', 'box', (.1, .1, 3.2), (0, 1.0, 1.7), 'woodD', None, (25, 0, 0))]
for i in range(6):
    hull.append(('Body', 'box', (1.3, .12, .14), (0, -2 + i * .8, 1.75), 'woodD'))
hull.append(('Body', 'box', (.16, 1.6, .16), (.62, .6, 1.62), 'rust'))
build('WreckHull', hull)

# Shard stone: a standing stone the size of a hand-span, a fifth of the Spiral cut into its face.
build('ShardStone', [
    ('Body', 'cyl', (.8, .8, .14), (0, 0, .07), 'stoneD', None, None, 5),
    ('Body', 'box', (.5, .3, .72), (0, 0, .5), 'wet', None, (0, 0, 0)),
    ('Body', 'cone', (.5, .3, .22), (0, 0, .97), 'wet', None, None, 4),
])

# Supply cache: a sea chest the expedition sent ahead. 1.4 m wide and deep (the crest sits on its face at 0.71).
build('SupplyCache', [
    ('Body', 'box', (1.4, 1.4, .8), (0, 0, .4), 'wood'),
    ('Body', 'box', (1.44, 1.44, .12), (0, 0, .2), 'woodD'), ('Body', 'box', (1.44, 1.44, .12), (0, 0, .7), 'woodD'),
    ('Body', 'cyl', (1.4, 1.4, 1.4), (0, 0, .85), 'woodD', None, (0, 90, 0), 8),
    ('Body', 'box', (.2, .08, .25), (0, -.72, .7), 'gold'),
    ('Body', 'box', (.1, 1.5, 1.5), (-.68, 0, .75), 'steel'), ('Body', 'box', (.1, 1.5, 1.5), (.68, 0, .75), 'steel'),
])

# Exit gate: two weathered pillars and a lintel over the way on, 6 m apart. The Wave glows on the lintel.
build('ExitGate', [
    ('Body', 'box', (1.1, 1.1, 5), (-3, 0, 2.5), 'stone'), ('Body', 'box', (1.1, 1.1, 5), (3, 0, 2.5), 'stone'),
    ('Body', 'box', (1.4, 1.4, .4), (-3, 0, .2), 'stoneD'), ('Body', 'box', (1.4, 1.4, .4), (3, 0, .2), 'stoneD'),
    ('Body', 'box', (7.6, 1.2, .9), (0, 0, 5.35), 'stoneD'),
    ('Body', 'box', (.14, .14, 2), (-2.5, -.6, 4), 'kelpD'), ('Body', 'box', (.12, .12, 1.4), (3.4, .55, 4.2), 'kelpD'),
])

# Heart cradle: five bone-white ribs closing over a stone basin, like fingers. One holds the Heart; the other is
# empty, and its basin is the shape the town's core was cut to fit.
cradle = [('Body', 'cyl', (2.8, 2.8, .4), (0, 0, .2), 'stoneD', None, None, 10),
          ('Body', 'cyl', (2.0, 2.0, .3), (0, 0, .5), 'stone', None, None, 10)]
for i in range(5):
    a = math.radians(i * 72 + 90)
    d = (-math.cos(a) * .35, -math.sin(a) * .35, 1)
    cradle.append(('Body', 'cone', (.4, .4, 2.6), (math.cos(a) * 1.2, math.sin(a) * 1.2, 1.6), 'bone', None, aim(d), 5))
build('HeartCradle', cradle)

# The Heart of the Tide: what was taken. Green, warm, slow.
build('Heart', [
    ('Body', 'ball', (1.6, 1.6, 1.6), (0, 0, 0), 'heart'),
    ('Body', 'ball', (.8, .8, .8), (.45, -.35, .4), 'heart'), ('Body', 'ball', (.7, .7, .7), (-.5, -.2, -.35), 'heart'),
])

# Petal seat: one of five stones around the cradles, each a founder's place. A crest glows on it for each shard.
build('PetalSeat', [
    ('Body', 'cyl', (1.2, 1.2, 1.1), (0, 0, .55), 'stone', None, None, 5),
    ('Body', 'cyl', (1.3, 1.3, .1), (0, 0, 1.15), 'stoneD', None, None, 5),
])

# The Tidemother: the Heart's keeper. A shelled thing the size of a cottage; the Mark is painted on her shell by
# the game. Claws and her one eye can be broken.
tm = [
    ('Body', 'ball', (3.6, 4.2, 2.2), (0, 0, 1.9), 'coralD'),
    ('Body', 'ball', (3.2, 3.6, 1.4), (0, .2, 2.7), 'shell'),
    ('Body', 'cone', (1.2, 1.2, 1.4), (0, .9, 3.6), 'coral', None, None, 6),
    ('Body', 'box', (2.2, .6, .5), (0, -2.0, 1.6), 'coralD'),
    ('Limb_Eye', 'ball', (.9, .9, .9), (0, -.1, .2), 'heart', (0, -2.0, 2.2)),
    ('Limb_ClawL', 'box', (.5, .5, 1.8), (0, 0, -.8), 'coralD', (-1.9, -1.3, 2.0), (-35, 0, 0)),
    ('Limb_ClawL', 'ball', (1.0, 1.6, .7), (0, -1.6, -1.2), 'coral', (-1.9, -1.3, 2.0)),
    ('Limb_ClawL', 'cone', (.4, .4, .9), (0, -2.6, -1.2), 'bone', (-1.9, -1.3, 2.0), (90, 0, 0), 5),
]
for i in range(3):
    tm.append(('Body', 'box', (.3, .3, 2.0), (-2.1, -.8 + i * 1.0, .9), 'coralD', None, (0, -35, 0)))
    tm.append(('Body', 'box', (.3, .3, 2.0), (2.1, -.8 + i * 1.0, .9), 'coralD', None, (0, 35, 0)))
build('Tidemother', mirror(tm))

# Set dressing for the stages.
# Arch: the kelp cathedral's ribs. Someone built these.
arch = [('Body', 'box', (1, 1, 7), (-3.5, 0, 3.5), 'wet'), ('Body', 'box', (1, 1, 7), (3.5, 0, 3.5), 'wet')]
for i in range(7):
    a = math.radians(i * 30)
    arch.append(('Body', 'box', (1.4, 1, .9), (-math.cos(a) * 3.5, 0, 7 + math.sin(a) * 2.2), 'wetD', None, (0, math.degrees(a) - 90, 0)))
for x in (-3.3, 3.6, 0.4):
    arch.append(('Body', 'box', (.16, .16, 3.0), (x, -.55, 5.0), 'kelpD'))
build('Arch', arch)

# Mural slab: a carved stone panel. The game cuts the story into it (glyph decals on its -Y face at 0.26).
build('MuralSlab', [
    ('Body', 'box', (3.2, .5, 2.6), (0, 0, 1.6), 'wet'),
    ('Body', 'box', (3.6, .7, .3), (0, 0, .15), 'wetD'), ('Body', 'box', (3.4, .6, .25), (0, 0, 3.0), 'wetD'),
])

# =====================================================================================  THE CASINO (the Lucky Barnacle)
COL.update({'felt': (.08, .42, .26), 'feltD': (.05, .28, .17), 'cream': (.96, .92, .8)})

# The booth: a counter with green baize, a striped awning on four poles, a sign board and a big wheel on top.
# The counter's front (the customers' side) faces -Y; the crab stands behind it.
booth = [
    ('Body', 'box', (3.0, 1.2, 1.0), (0, 0, .5), 'woodD'),
    ('Body', 'box', (3.1, 1.3, .1), (0, 0, 1.05), 'felt'),
    ('Body', 'box', (3.12, .08, .12), (0, -.66, 1.05), 'gold'),
    ('Body', 'box', (2.6, .05, .5), (0, -.62, .55), 'red'),
]
for x in (-1.45, 1.45):
    for y in (-.55, .55):
        booth.append(('Body', 'cyl', (.1, .1, 2.6), (x, y, 1.3), 'gold', None, None, 6))
for i in range(8):   # the awning: alternating red and cream stripes, sloping down to the front
    booth.append(('Body', 'box', (3.4 / 8, 1.6, .08), (-1.7 + 3.4 / 16 + i * 3.4 / 8, -.1, 2.68), 'red' if i % 2 == 0 else 'cream', None, (12, 0, 0)))
    booth.append(('Body', 'box', (3.4 / 8, .06, .32), (-1.7 + 3.4 / 16 + i * 3.4 / 8, -.9, 2.42), 'red' if i % 2 == 0 else 'cream'))
booth += [
    ('Body', 'box', (2.2, .16, .7), (0, .3, 3.15), 'navy'),
    ('Body', 'box', (2.3, .12, .08), (0, .22, 3.52), 'gold'), ('Body', 'box', (2.3, .12, .08), (0, .22, 2.78), 'gold'),
    # The wheel above the sign: gold rim, red and black pockets, a hub
    ('Body', 'cyl', (1.3, 1.3, .1), (0, .25, 4.25), 'gold', None, (90, 0, 0), 20),
    ('Body', 'cyl', (1.1, 1.1, .12), (0, .22, 4.25), 'black', None, (90, 0, 0), 20),
    ('Body', 'cyl', (.3, .3, .2), (0, .16, 4.25), 'gold', None, (90, 0, 0), 8),
    ('Body', 'cyl', (.12, .12, .5), (0, .25, 3.7), 'gold', None, None, 6),
]
for k in range(9):
    a = math.radians(k * 40)
    booth.append(('Body', 'box', (.16, .05, .42), (math.sin(a) * .34, .15, 4.25 + math.cos(a) * .34), 'red', None, (0, -k * 40, 0)))
# Stacks of chips and a fan of cards on the baize
for x, col in ((-1.0, 'red'), (-.82, 'blue'), (-.64, 'white')):
    booth.append(('Body', 'cyl', (.14, .14, .14), (x, -.25, 1.17), col, None, None, 10))
for k in range(4):
    booth.append(('Body', 'box', (.16, .24, .01), (.7 + k * .1, -.2, 1.11 + k * .005), 'white', None, (0, 0, -20 + k * 12)))
build('CasinoBooth', booth)

# The crab who runs it: a big shore crab in a top hat and bow tie. Its claws are the "arms" a vendor waves (Part_ArmL/R,
# pivoting at the shoulder) and its eye stalks and hat are its "head" (Part_Head).
crab = [
    ('Body', 'ball', (1.1, .9, .7), (0, 0, 1.0), 'orange'),
    ('Body', 'ball', (.9, .7, .45), (0, -.05, .78), 'orange2'),
    ('Body', 'box', (.32, .06, .12), (0, -.44, 1.05), 'black'), ('Body', 'ball', (.1, .08, .1), (0, -.47, 1.05), 'red'),
]
for sx in (-1, 1):
    for k in range(3):
        crab.append(('Body', 'box', (.6, .08, .08), (sx * .62, -.15 + k * .2, .62), 'orange', None, (0, sx * 45, 0)))
        crab.append(('Body', 'box', (.08, .08, .55), (sx * .9, -.15 + k * .2, .32), 'orange'))
    side = 'L' if sx < 0 else 'R'
    crab += [
        ('Part_Arm' + side, 'box', (.14, .14, .5), (0, 0, -.25), 'orange', (sx * .55, -.2, 1.1)),
        ('Part_Arm' + side, 'ball', (.34, .3, .44), (0, -.05, -.62), 'orange2', (sx * .55, -.2, 1.1)),
        ('Part_Arm' + side, 'box', (.08, .08, .26), (sx * .06, -.12, -.88), 'orange', (sx * .55, -.2, 1.1)),
    ]
crab += [
    ('Part_Head', 'cyl', (.06, .06, .35), (-.16, 0, .18), 'orange', (0, -.3, 1.25)),
    ('Part_Head', 'cyl', (.06, .06, .35), (.16, 0, .18), 'orange', (0, -.3, 1.25)),
    ('Part_Head', 'ball', (.16, .16, .16), (-.16, 0, .38), 'white', (0, -.3, 1.25)),
    ('Part_Head', 'ball', (.16, .16, .16), (.16, 0, .38), 'white', (0, -.3, 1.25)),
    ('Part_Head', 'ball', (.07, .07, .07), (-.16, -.07, .39), 'black', (0, -.3, 1.25)),
    ('Part_Head', 'ball', (.07, .07, .07), (.16, -.07, .39), 'black', (0, -.3, 1.25)),
    ('Part_Head', 'cyl', (.62, .62, .04), (0, .25, .22), 'black', (0, -.3, 1.25), None, 12),
    ('Part_Head', 'cyl', (.38, .38, .42), (0, .25, .44), 'black', (0, -.3, 1.25), None, 12),
    ('Part_Head', 'cyl', (.4, .4, .07), (0, .25, .3), 'red', (0, -.3, 1.25), None, 12),
]
build('VendorCrab', crab)

# The crate the crab brought down to the seabed: a sea chest with a baize lid, a little wheel and a deck of cards.
build('CasinoCrate', [
    ('Body', 'box', (1.6, 1.0, .8), (0, 0, .4), 'wood'),
    ('Body', 'box', (1.64, 1.04, .1), (0, 0, .2), 'woodD'), ('Body', 'box', (1.64, 1.04, .1), (0, 0, .62), 'woodD'),
    ('Body', 'box', (1.6, 1.0, .06), (0, 0, .83), 'felt'),
    ('Body', 'cyl', (.6, .6, .06), (.35, .1, .89), 'gold', None, None, 16),
    ('Body', 'cyl', (.5, .5, .07), (.35, .1, .9), 'black', None, None, 16),
    ('Body', 'cyl', (.14, .14, .1), (.35, .1, .93), 'red', None, None, 8),
    ('Body', 'box', (.2, .3, .08), (-.4, -.1, .9), 'white'), ('Body', 'box', (.2, .3, .01), (-.15, -.15, .87), 'white', None, (0, 0, 25)),
    ('Body', 'cyl', (.12, .12, .1), (-.55, .3, .9), 'red', None, None, 10), ('Body', 'cyl', (.12, .12, .1), (-.38, .3, .9), 'blue', None, None, 10),
])

# =====================================================================================  GUN ATTACHMENTS
# Small parts the game fits to a gun (Player.DressGun): placed from the gun's muzzle, so their origin is where they
# sit and they point -Y like the guns. Also the shop's pictures.
COL.update({'lens': (.55, .85, 1), 'shark': (.36, .44, .5)})
build('AttSight', [
    ('Body', 'box', (.03, .1, .025), (0, 0, .012), 'gold'),
    ('Body', 'cyl', (.06, .06, .012), (0, .03, .045), 'gold', None, (90, 0, 0), 10),
    ('Body', 'box', (.008, .008, .03), (0, -.04, .04), 'gold'),
])
build('AttScope', [
    ('Body', 'cyl', (.055, .055, .3), (0, 0, .05), 'gold', None, (90, 0, 0), 10),
    ('Body', 'cyl', (.065, .065, .03), (0, -.15, .05), 'lens', None, (90, 0, 0), 10),
    ('Body', 'cyl', (.06, .06, .02), (0, .05, .05), 'woodD', None, (90, 0, 0), 10),
    ('Body', 'box', (.02, .03, .04), (0, -.06, .015), 'greyD'), ('Body', 'box', (.02, .03, .04), (0, .07, .015), 'greyD'),
])
build('AttBrake', [
    ('Body', 'cyl', (.075, .075, .12), (0, -.05, 0), 'greyD', None, (90, 0, 0), 8),
    ('Body', 'box', (.09, .02, .015), (0, -.05, .02), 'black'),
    ('Body', 'ball', (.03, .03, .025), (.03, -.03, .03), 'white'), ('Body', 'ball', (.025, .025, .02), (-.035, -.08, .02), 'white'),
])
build('AttBarrel', [
    ('Body', 'cyl', (.05, .05, .32), (0, -.16, 0), 'steel', None, (90, 0, 0), 8),
    ('Body', 'cyl', (.06, .06, .03), (0, -.31, 0), 'greyD', None, (90, 0, 0), 8),
])
build('AttDrum', [
    ('Body', 'cyl', (.2, .2, .08), (0, 0, -.1), 'greyD', None, (0, 90, 0), 12),
    ('Body', 'cyl', (.08, .08, .09), (0, 0, -.1), 'steel', None, (0, 90, 0), 8),
])
build('AttClip', [
    ('Body', 'box', (.04, .11, .05), (0, 0, -.03), 'gold'),
    *[('Body', 'cyl', (.02, .02, .05), (0, -.04 + k * .027, 0), 'orange2', None, None, 6) for k in range(4)],
])
build('AttGrip', [
    ('Body', 'box', (.075, .1, .14), (0, .03, -.08), 'shark', None, (-15, 0, 0)),
    ('Body', 'box', (.08, .02, .14), (0, -.02, -.08), 'belly', None, (-15, 0, 0)),
])
build('AttStock', [
    ('Body', 'box', (.06, .34, .1), (0, .2, -.03), 'wood'),
    ('Body', 'box', (.07, .05, .16), (0, .37, -.05), 'woodD'),
])

# =====================================================================================  CASINO PIECES
# The slot machine cabinet (beside the Lucky Barnacle, and the lobby's picture of Slots), and the other games' pictures.
# Front faces -Y: the slot machine's three reel windows and lever face the player.
COL.update({'cabinet': (.62, .1, .14), 'cabinetD': (.38, .05, .08), 'glass': (.85, .95, 1), 'cardW': (.97, .96, .92)})
slot = [
    ('Body', 'box', (1.0, .8, 1.0), (0, 0, .5), 'cabinetD'),
    ('Body', 'box', (.95, .75, .9), (0, .02, 1.45), 'cabinet'),
    ('Body', 'cyl', (1.0, .78, 1.0), (0, .02, 1.95), 'cabinet', None, (0, 90, 0), 16),
    ('Body', 'box', (.98, .78, .06), (0, 0, 1.0), 'gold'),
    ('Body', 'box', (.8, .06, .38), (0, -.39, 1.48), 'gold'),
    ('Body', 'box', (.86, .06, .2), (0, -.36, 2.1), 'navy'),
    ('Body', 'box', (.9, .3, .08), (0, -.45, 1.1), 'gold'),
    ('Body', 'cyl', (.12, .12, .1), (-.3, -.4, 1.18), 'red', None, (90, 0, 0), 10),
    ('Body', 'cyl', (.12, .12, .1), (0, -.4, 1.18), 'yellow', None, (90, 0, 0), 10),
    ('Body', 'cyl', (.12, .12, .1), (.3, -.4, 1.18), 'green', None, (90, 0, 0), 10),
    # The lever on the right side, and its red ball
    ('Body', 'cyl', (.16, .16, .2), (.55, 0, 1.45), 'steel', None, (0, 90, 0), 8),
    ('Body', 'cyl', (.05, .05, .7), (.62, 0, 1.8), 'steel', None, None, 6),
    ('Body', 'ball', (.16, .16, .16), (.62, 0, 2.18), 'red'),
    # A tray of sand dollars at the bottom
    ('Body', 'box', (.6, .2, .1), (0, -.45, .45), 'greyD'),
]
for i, x in enumerate((-.26, 0, .26)):   # the reel windows: glass over a pale strip
    slot.append(('Body', 'box', (.22, .03, .3), (x, -.41, 1.48), 'glass'))
build('SlotMachine', slot)

def card(x, z, rot, pip):
    return [('Body', 'box', (.32, .02, .45), (x, 0, z), 'cardW', None, (0, rot, 0)),
            ('Body', 'box', (.08, .03, .08), (x + math.sin(math.radians(rot)) * .0, -.012, z), pip, None, (0, rot + 45, 0))]
build('CardFan', card(-.1, .3, 12, 'black') + card(.1, .32, -12, 'red'))
build('PokerHand', [p for k, r in enumerate((24, 12, 0, -12, -24)) for p in card(-.24 + k * .12, .3 + (2 - abs(k - 2)) * .02, r, 'red' if k % 2 else 'black')])
wheel = [('Body', 'cyl', (.9, .9, .12), (0, 0, .06), 'woodD', None, None, 24),
         ('Body', 'cyl', (.78, .78, .13), (0, 0, .07), 'gold', None, None, 24),
         ('Body', 'cyl', (.7, .7, .14), (0, 0, .08), 'black', None, None, 24),
         ('Body', 'cyl', (.22, .22, .2), (0, 0, .12), 'gold', None, None, 12),
         ('Body', 'cone', (.12, .12, .2), (0, 0, .3), 'gold', None, None, 8),
         ('Body', 'ball', (.06, .06, .06), (.25, -.1, .17), 'white')]
for k in range(12):
    a = math.radians(k * 30)
    wheel.append(('Body', 'box', (.1, .2, .02), (math.cos(a) * .25, math.sin(a) * .25, .155), 'red' if k % 2 else 'black', None, (0, 0, k * 30 + 90)))
wheel.append(('Body', 'box', (.1, .2, .02), (.25, 0, .156), 'green', None, (0, 0, 90)))
build('RouletteWheel', wheel)

# Ammo modifiers: three standing rounds each, brass cases with tips that say what they do (the shop's pictures)
COL.update({'shockTip': (.35, .85, 1), 'fireTip': (1, .42, .1)})
def rounds(name, tip, tipKind='cone', band=None):
    parts = []
    for k, x in enumerate((-.07, 0, .07)):
        y = (k - 1) * .02
        parts += [('Body', 'cyl', (.05, .05, .14), (x, y, .07), 'gold', None, None, 10),
                  ('Body', tipKind, (.05, .05, .07), (x, y, .175), tip, None, None, 10)]
        if band: parts.append(('Body', 'cyl', (.054, .054, .02), (x, y, .12), band, None, None, 10))
    build(name, parts)
rounds('AmmoFire', 'fireTip', band='red')
rounds('AmmoShock', 'shockTip', band='blue')
rounds('AmmoPierce', 'steel')
rounds('AmmoBlast', 'red', 'ball', band='black')
rounds('AmmoHollow', 'greyD', 'cyl')

print('TIDEWALL_MODELS_DONE', sorted(os.listdir(OUT)))

# Laser sights, and the scope, choke and stock that came with the five-slot guns
build('AttReflex', [
    ('Body', 'box', (.05, .1, .025), (0, 0, .012), 'gold'),
    ('Body', 'box', (.07, .012, .07), (0, -.03, .055), 'gold'),
    ('Body', 'box', (.055, .006, .055), (0, -.03, .055), 'lens'),
    ('Body', 'ball', (.012, .012, .012), (0, -.035, .058), 'red'),
])
build('AttChoke', [
    ('Body', 'cyl', (.08, .08, .08), (0, -.03, 0), 'orange', None, (90, 0, 0), 8),
    ('Body', 'cone', (.11, .11, .1), (0, -.11, 0), 'pinkL', None, (-90, 0, 0), 8),
    *[('Body', 'ball', (.025, .025, .025), (x, -.02, z), 'white') for x, z in ((.04, .02), (-.03, .035), (.015, -.04))],
])
build('AttWhalebone', [
    ('Body', 'box', (.045, .3, .05), (0, .17, .015), 'bone', None, (6, 0, 0)),
    ('Body', 'box', (.045, .28, .04), (0, .17, -.075), 'bone', None, (-6, 0, 0)),
    ('Body', 'box', (.06, .05, .15), (0, .33, -.03), 'bone'),
])
def laser_part(name, lens):
    build(name, [
        ('Body', 'cyl', (.04, .04, .13), (0, 0, 0), 'greyD', None, (90, 0, 0), 8),
        ('Body', 'cyl', (.048, .048, .02), (0, -.06, 0), 'gold', None, (90, 0, 0), 8),
        ('Body', 'cyl', (.03, .03, .012), (0, -.072, 0), lens, None, (90, 0, 0), 8),
    ])
laser_part('AttLaserRed', 'red')
laser_part('AttLaserGreen', 'glow')
laser_part('AttLaserBlue', 'shockTip')

# Damage on a wall section, added in stages over its intact model (Wall.Scars): cracks, then holes, then a heap at the foot
def crack(x, z, y, n, seed):
    out = []
    a = 25 + seed * 40
    for k in range(n):
        a += 55 if k % 2 else -48
        ln = .34 - k * .02
        out.append(('Body', 'box', (ln, .03, .035), (x, y, z), 'black', None, (0, a, 0)))
        x += math.cos(math.radians(a)) * ln * .9
        z -= math.sin(math.radians(a)) * ln * .9
    return out
scar1 = []
for y in (-.56, .56):
    scar1 += crack(-1.1, 1.05, y, 5, 0) + crack(.9, .9, y, 5, 1) + crack(.1, .55, y, 4, 2)
build('WallDamage1', scar1)
scar2 = []
for y in (-.56, .56):
    scar2 += crack(-.3, 1.1, y, 6, 1) + crack(1.5, .6, y, 4, 2)
    scar2 += [('Body', 'box', (.62, .07, .3), (-1.15, y, .62), 'black', None, (0, 8, 0)), ('Body', 'box', (.5, .08, .26), (.75, y, 1.0), 'black', None, (0, -12, 0)),
              ('Body', 'box', (.4, .07, .22), (.1, y, .3), 'black', None, (0, 5, 0))]
scar2 += [('Body', 'box', (.36, .3, .22), (x, y, .12), 'stone', None, (r, r * 2, r * 3)) for x, y, r in ((-1.2, -.85, 10), (.7, -.95, -14), (.1, -.8, 22), (-.5, -1.05, -6))]
scar2 += [('Body', 'box', (.8, .96, .1), (1, -.05, 1.76), 'black')]   # a merlon's cap is gone
build('WallDamage2', scar2)
scar3 = []
for y in (-.57, .57):
    scar3 += [('Body', 'ball', (1.5, .1, .85), (-.2, y, .62), 'black'), ('Body', 'ball', (.9, .1, .55), (1.25, y, 1.02), 'black')]
scar3 += [('Body', 'box', (.5, .45, .32), (x, y, .16), 'stoneL' if k % 2 else 'stone', None, (k * 17, k * 29, k * 41))
          for k, (x, y) in enumerate(((-1.5, -.8), (-.9, -1.1), (-.3, -.85), (.2, -1.2), (.8, -.9), (1.4, -1.05)))]
scar3 += [('Body', 'box', (.8, .96, .46), (-1, -.05, 1.56), 'greyD')]    # one merlon knocked to a stump
build('WallDamage3', scar3)

# First-person hand and sleeve (the coat colour is the player's)
build('ViewArm', [
    ('Body', 'ball', (.14, .17, .17), (0, .01, -.08), 'skin'),
    ('Body', 'box', (.13, .55, .14), (.03, .34, -.27), 'coat', None, (-34, 0, 4)),
    ('Body', 'box', (.15, .07, .16), (.012, .1, -.13), 'white', None, (-34, 0, 4)),
])

# The Last Light: the lamp's beam (apex at the origin, shining -Y: the game stretches it), sea chests, fishing
build('LampBeam', [('Body', 'cone', (1, 1, 1), (0, -.5, 0), 'white', None, (-90, 0, 0), 20)])
build('SalvageCrate', [
    ('Body', 'box', (1.1, .7, .55), (0, 0, .28), 'woodD'),
    ('Body', 'cyl', (.7, 1.1, .5), (0, 0, .55), 'wood', None, (0, 90, 0), 10),
    *[('Body', 'box', (.09, .74, .6), (x, 0, .3), 'gold') for x in (-.4, .4)],
    ('Body', 'box', (.16, .05, .2), (0, -.37, .5), 'gold'),
    *[('Body', 'ball', (.14, .14, .1), (x, y, .08), 'barn') for x, y in ((.55, .3), (-.5, -.3), (.2, .36))],
])
build('FishSpot', [
    ('Body', 'tor', (2.4, 2.4, .2), (0, 0, 0), 'ice', None, None, 16),
    ('Body', 'tor', (1.2, 1.2, .2), (0, 0, 0), 'white', None, None, 12),
    ('Body', 'ball', (.2, .2, .22), (0, 0, .1), 'red'),
    ('Body', 'ball', (.2, .2, .1), (0, 0, .2), 'white'),
])
COL.update({'cork': (.72, .55, .35)})
build('FishingRod', [
    ('Body', 'cyl', (.05, .05, .32), (0, .04, 0), 'cork', None, (90, 0, 0), 8),
    ('Body', 'cyl', (.03, .03, 1.5), (0, -.85, 0), 'woodD', None, (90, 0, 0), 6),
    ('Body', 'tor', (.14, .14, .3), (.05, -.08, -.03), 'gold', None, (0, 90, 0), 8),
], sockets=[('Tip', (0, -1.6, 0))])
def fish(name, body, belly, length=.5, height=.2, fin=None, eye_col='white'):
    fin = fin or body
    build(name, [
        ('Body', 'ball', (height * .7, length, height), (0, 0, 0), body),
        ('Body', 'ball', (height * .55, length * .8, height * .5), (0, 0, -height * .24), belly),
        ('Body', 'cone', (.03, height * .9, height * .9), (0, length * .6, 0), fin, None, (-90, 0, 0), 4),
        ('Body', 'cone', (.03, length * .35, height * .55), (0, 0, height * .55), fin, None, None, 4),
        ('Body', 'ball', (.05, .05, .05), (-height * .32, -length * .32, height * .1), eye_col),
        ('Body', 'ball', (.05, .05, .05), (height * .32, -length * .32, height * .1), eye_col),
    ])
fish('FishSardine', 'steel', 'white', .36, .13)
fish('FishMackerel', 'teal', 'belly', .55, .2, 'navy')
fish('FishOil', 'greyD', 'yellow', .6, .28, 'black', 'yellow')
fish('FishSun', 'orange', 'yellow', .4, .42, 'orange2')
fish('FishGold', 'gold', 'yellow', .55, .36, 'gold', 'glow')
build('FishSword', [
    ('Body', 'ball', (.2, .85, .3), (0, 0, 0), 'navy'), ('Body', 'ball', (.16, .7, .15), (0, 0, -.07), 'belly'),
    ('Body', 'cone', (.05, .05, .7), (0, -.75, .02), 'steel', None, (90, 0, 0), 5),
    ('Body', 'cone', (.03, .3, .5), (0, -.05, .32), 'navy', None, None, 4), ('Body', 'cone', (.03, .3, .4), (0, .52, 0), 'navy', None, (-90, 0, 0), 4),
])
build('Conch', [('Body', 'cone', (.24, .24, .62), (0, 0, .24), 'orange', None, (0, 72, 0), 10), ('Body', 'ball', (.26, .26, .22), (-.16, 0, .22), 'white'),
                ('Body', 'ball', (.18, .2, .16), (.04, 0, .27), 'orange2'), ('Body', 'ball', (.1, .12, .09), (.2, 0, .31), 'white'), ('Body', 'ball', (.16, .12, .12), (-.2, -.13, .2), 'crabD')])
build('FishBoot', [('Body', 'box', (.2, .24, .38), (0, .06, .19), 'brown'), ('Body', 'box', (.2, .44, .14), (0, -.05, .05), 'woodD'), ('Body', 'box', (.21, .25, .03), (0, .06, .33), 'greyD')])
build('FishCan', [('Body', 'cyl', (.16, .16, .32), (0, 0, .16), 'red', None, None, 10), ('Body', 'cyl', (.165, .165, .03), (0, 0, .32), 'steel', None, None, 10), ('Body', 'box', (.17, .02, .1), (0, -.08, .17), 'white')])
build('FishMimic', [('Body', 'ball', (.4, .55, .2), (0, 0, 0), 'orange'), ('Body', 'cone', (.03, .2, .2), (0, .32, 0), 'orange2', None, (-90, 0, 0), 4),
                    *eye('Body', -.1, -.2, .1, None, .06), *eye('Body', .1, -.2, .1, None, .06), ('Body', 'ball', (.14, .18, .08), (-.24, -.2, 0), 'crabD'), ('Body', 'ball', (.14, .18, .08), (.24, -.2, 0), 'crabD')])
