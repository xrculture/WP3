# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#compute_matrix_by_principal_axis
# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#meshing_decimation_quadric_edge_collapse_with_texture
import sys
import time
import pymeshlab

time.sleep(2)  # wait for file system to be ready

# Create a new MeshSet
ms = pymeshlab.MeshSet()

ms.load_new_mesh(str(sys.argv[1]))

# Compute matrix by principal axis and apply
ms.compute_matrix_by_principal_axis()
ms.apply_matrix_freeze()

# Center the mesh at the origin and apply
ms.compute_matrix_from_translation(traslmethod='Center on Scene BBox')
ms.apply_matrix_freeze()

# Apply Quadric Edge Collapse Decimation with texture preservation
ms.meshing_decimation_quadric_edge_collapse_with_texture()

# Save the final mesh
ms.save_current_mesh(str(sys.argv[2]))

time.sleep(2)  # wait for file system to be ready

print("Mesh alignment, centering, and simplification completed!")