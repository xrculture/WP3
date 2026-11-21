# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#compute_matrix_by_principal_axis
# https://pymeshlab.readthedocs.io/en/latest/filter_list.html#meshing_decimation_quadric_edge_collapse_with_texture
import sys
import time
import json
import numpy as np
import pymeshlab


def fit_rigid_transform(src, dst):
    """Least-squares rigid transform (Kabsch): dst ~= R @ src + t. Returns a 4x4 matrix and the RMS error."""
    cs, cd = src.mean(axis=0), dst.mean(axis=0)
    h = (src - cs).T @ (dst - cd)
    u, _, vt = np.linalg.svd(h)
    d = np.sign(np.linalg.det(vt.T @ u.T))
    r = vt.T @ np.diag([1.0, 1.0, d]) @ u.T
    t = cd - r @ cs
    m = np.eye(4)
    m[:3, :3] = r
    m[:3, 3] = t
    rms = float(np.sqrt(np.mean(np.sum((src @ r.T + t - dst) ** 2, axis=1))))
    return m, rms


time.sleep(2)  # wait for file system to be ready

# Create a new MeshSet
ms = pymeshlab.MeshSet()

ms.load_new_mesh(str(sys.argv[1]))

# Vertices before the alignment (same order after the freeze, so they can be compared)
before = np.array(ms.current_mesh().vertex_matrix(), dtype=np.float64)

# Compute matrix by principal axis and apply
ms.compute_matrix_by_principal_axis()
ms.apply_matrix_freeze()

# Center the mesh at the origin and apply
ms.compute_matrix_from_translation(traslmethod='Center on Scene BBox')
ms.apply_matrix_freeze()

# Recover the combined transform (original frame -> aligned frame) and save it
after = np.array(ms.current_mesh().vertex_matrix(), dtype=np.float64)
matrix, rms = fit_rigid_transform(before, after)
with open(str(sys.argv[2]) + ".transform.json", "w") as f:
    json.dump(matrix.tolist(), f)
print(f"Alignment transform saved, fit RMS error: {rms:.3e}")

# Apply Quadric Edge Collapse Decimation with texture preservation
ms.meshing_decimation_quadric_edge_collapse_with_texture()

# Save the final mesh
ms.save_current_mesh(str(sys.argv[2]))

time.sleep(2)  # wait for file system to be ready

print("Mesh alignment, centering, and simplification completed!")