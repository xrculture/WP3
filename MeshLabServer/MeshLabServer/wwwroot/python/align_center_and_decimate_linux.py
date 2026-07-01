# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#compute_matrix_by_principal_axis
# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#meshing_decimation_quadric_edge_collapse
import os
import shutil
import sys
import time
import pymeshlab

time.sleep(2)  # wait for file system to be ready

input_path = os.path.abspath(str(sys.argv[1]))
output_path = os.path.abspath(str(sys.argv[2]))

ms = pymeshlab.MeshSet()
ms.load_new_mesh(input_path)

# Compute matrix by principal axis and apply
ms.compute_matrix_by_principal_axis()
ms.apply_matrix_freeze()

# Center the mesh at the origin and apply
ms.compute_matrix_from_translation(traslmethod='Center on Scene BBox')
ms.apply_matrix_freeze()

# Decimate preserving original UV coordinates
ms.meshing_decimation_quadric_edge_collapse(targetperc=0.5)

os.makedirs(os.path.dirname(output_path), exist_ok=True)
ms.save_current_mesh(output_path)

# pymeshlab saves the texture under its internal name; copy the original beside the output
out_dir = os.path.dirname(output_path)
in_dir = os.path.dirname(input_path)
out_stem = os.path.splitext(os.path.basename(output_path))[0]
mtl_path = os.path.join(out_dir, out_stem + '.mtl')

if os.path.exists(mtl_path):
    with open(mtl_path, 'r') as f:
        mtl = f.read()
    for line in mtl.splitlines():
        if line.strip().lower().startswith('map_kd '):
            saved_tex = line.strip().split(None, 1)[1].strip()
            original_tex = os.path.join(in_dir, saved_tex)
            if not os.path.exists(original_tex):
                # try matching by extension
                for candidate in os.listdir(in_dir):
                    if candidate.lower().endswith(('.jpg', '.jpeg', '.png')):
                        original_tex = os.path.join(in_dir, candidate)
                        break
            if os.path.exists(original_tex):
                shutil.copy2(original_tex, os.path.join(out_dir, os.path.basename(original_tex)))

time.sleep(2)  # wait for file system to be ready

print("Mesh alignment, centering, and simplification completed!")