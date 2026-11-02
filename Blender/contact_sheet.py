# TideWall 3D — one picture of many models: the cheap way to review art (one image instead of one per model).
# Run:  blender --background --factory-startup --python Blender/contact_sheet.py -- <repo root> <out.png> [Name Name ...]
# With no names it takes every FBX in Assets/Content/Models. Shows base colours times the baked vertex shading.
import bpy, glob, math, os, sys
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
root, out, names = args[0], args[1], args[2:]
folder = os.path.join(root, 'Assets', 'Content', 'Models')
names = names or sorted(os.path.splitext(os.path.basename(f))[0] for f in glob.glob(os.path.join(folder, '*.fbx')))
bpy.ops.wm.read_factory_settings(use_empty=True)
for m in list(bpy.data.objects): bpy.data.objects.remove(m)

cols = math.ceil(math.sqrt(len(names)))
for i, name in enumerate(names):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(folder, name + '.fbx'))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == 'MESH']
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box] or [Vector()]
    lo = Vector([min(c) for c in zip(*pts)])
    hi = Vector([max(c) for c in zip(*pts)])
    size = max((hi - lo).length, .001)
    tops = [o for o in new if o.parent is None]
    k = 1.6 / size
    base = Vector(((i % cols) * 2.2, -(i // cols) * 2.2, 0))
    centre = (lo + hi) / 2
    pivot = bpy.data.objects.new('p', None); bpy.context.scene.collection.objects.link(pivot)
    for o in tops: o.parent = pivot; o.matrix_parent_inverse = pivot.matrix_world.inverted()
    pivot.scale = (k, k, k)
    pivot.location = base - centre * k

rows = math.ceil(len(names) / cols)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); bpy.context.scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(cols, rows) * 2.3
mid = Vector(((cols - 1) * 1.1, -(rows - 1) * 1.1, 0))
cam.location = mid + Vector((.55, -1, .75)).normalized() * 60
cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
s = bpy.context.scene
s.camera = cam
s.render.engine = 'BLENDER_WORKBENCH'
s.display.shading.light = 'STUDIO'
s.display.shading.color_type = 'VERTEX'
s.display.shading.show_cavity = False
s.render.resolution_x = s.render.resolution_y = 1100
s.render.filepath = out
bpy.ops.render.render(write_still=True)
print('SHEET_DONE', len(names), 'models')
