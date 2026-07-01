# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#meshing_surface_subdivision_ls3_loop
import sys
import os
import time
import shutil
import pymeshlab

time.sleep(2)  # wait for file system to be ready

# Create a new MeshSet
ms = pymeshlab.MeshSet()

ms.load_new_mesh(str(sys.argv[1]))

# Subdivide the mesh using Loop subdivision
# threshold=0 ensures ALL triangles are subdivided, not just long-edged ones
ms.meshing_surface_subdivision_ls3_loop(
    iterations=3,
    threshold=pymeshlab.PercentageValue(0.25)
)

# Save the aligned mesh
ms.save_current_mesh(str(sys.argv[2]))

time.sleep(2)  # wait for file system to be ready

print("Mesh subdivision completed!")