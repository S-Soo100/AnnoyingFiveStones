import bpy, bmesh, os
from mathutils import Vector
CUT_X = 3.10
KEEP = {"Mano", "Huesos Mano"}
for o in list(bpy.data.objects):
    if o.name not in KEEP: bpy.data.objects.remove(o, do_unlink=True)

o = bpy.data.objects["Mano"]; arm = bpy.data.objects["Huesos Mano"]
mw=o.matrix_world; me=o.data
bm=bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
doomed=[v for v in bm.verts if (mw @ v.co).x < CUT_X]
bmesh.ops.delete(bm, geom=doomed, context='VERTS')
bm.edges.ensure_lookup_table()
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
print(f"팔뚝 제거 {len(doomed)} → verts={len(me.vertices)}")

# 메시 중심을 월드 원점으로. 메시와 아마추어를 **같은 양** 옮겨야 스키닝이 안 깨진다.
vs=[o.matrix_world @ v.co for v in me.vertices]
center = sum(vs, Vector((0,0,0))) / len(vs)
print(f"메시 중심 {tuple(round(c,3) for c in center)} → 원점으로 이동")
# Mano가 아마추어의 자식이면 부모만 옮긴다 (둘 다 옮기면 이동이 두 번 먹는다)
root = arm if o.parent == arm else o
print("이동 대상:", root.name, "| Mano.parent =", o.parent.name if o.parent else None)
root.location = root.location - center

bpy.context.view_layer.update()
vs2=[o.matrix_world @ v.co for v in me.vertices]
print(f"이동 후 X {min(v.x for v in vs2):.2f}~{max(v.x for v in vs2):.2f}  Y {min(v.y for v in vs2):.2f}~{max(v.y for v in vs2):.2f}")

vl=bpy.context.view_layer
for ob in bpy.data.objects:
    try: ob.select_set(True)
    except Exception: pass
vl.objects.active=o
out="/Users/baek/unityProjects/AnnoyingFiveStones/Assets/Models/Hand/Hand.fbx"
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    object_types={'ARMATURE','MESH'}, use_mesh_modifiers=True,
    add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True)
print("EXPORTED", os.path.getsize(out))

