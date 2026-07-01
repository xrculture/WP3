
# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#compute_matrix_by_principal_axis 
import sys
import os
import time
import shutil
import pymeshlab

time.sleep(2)  # wait for file system to be ready

# Create a new MeshSet
ms = pymeshlab.MeshSet()

ms.load_new_mesh(str(sys.argv[1]))

# Compute matrix by principal axis
ms.compute_matrix_by_principal_axis()

# Apply the alignment transformation to mesh geometry
ms.apply_matrix_freeze()

# Center the mesh at the origin
ms.compute_matrix_from_translation(traslmethod='Center on Scene BBox')

# Apply the centering transformation to mesh geometry
ms.apply_matrix_freeze()

# Save the aligned mesh
ms.save_current_mesh(str(sys.argv[2]))

time.sleep(2)  # wait for file system to be ready

print("Mesh alignment completed!")