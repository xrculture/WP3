"""
Transfers the 2D semantic label maps (from segment_images.py) onto the 3D mesh.

Method: per-face voting.
  1. For every camera (view) in sfm_data.json, project each face centroid into the image.
  2. Skip faces outside the image, behind the camera, seen at a grazing angle, or occluded.
  3. Read the class id from the label map at the projected pixel and add a vote
     weighted by cos(angle between face normal and view direction).
  4. The class with the most weighted votes wins for each face.
  5. Split each class into connected components (instances: window_1, window_2, ...).

Outputs (in --output):
  face_labels.npy    class id per mesh face
  labeled.ply        colored preview mesh
  instances/*.ply    one mesh per connected component of a class
  instances.json     class, instance name, face count, bounds per instance
  model_labeled.obj  textured OBJ with one group ("g") per class (only with --textured-obj)

Docs:
  trimesh:                    https://trimesh.org/
  trimesh ray queries:        https://trimesh.org/trimesh.ray.html
  trimesh submesh / split:    https://trimesh.org/trimesh.base.html#trimesh.base.Trimesh.submesh
  openMVG sfm_data format:    https://openmvg.readthedocs.io/en/latest/software/SfM/SfM_Data/
  openMVG data conversion:    https://openmvg.readthedocs.io/en/latest/software/SfM/ConvertSfM_DataFormat/
  scipy cKDTree:              https://docs.scipy.org/doc/scipy/reference/generated/scipy.spatial.cKDTree.html
  Wavefront OBJ format:       https://en.wikipedia.org/wiki/Wavefront_.obj_file
  Pinhole camera model:       https://en.wikipedia.org/wiki/Pinhole_camera_model

Usage:
  python label_mesh.py --sfm sfm_data.json --mesh model_dense_mesh_refine.ply
                       --labels <label maps folder> --output <folder> [--textured-obj model.obj]
"""
import argparse, json, os
import numpy as np
import trimesh
from PIL import Image
from scipy.spatial import cKDTree

# Command line arguments
p = argparse.ArgumentParser()
p.add_argument("--sfm", required=True)        # sfm_data.json
p.add_argument("--mesh", required=True)       # untextured PLY, e.g. model_dense_mesh_refine.ply
p.add_argument("--labels", required=True)     # folder with <image>.png label maps + classes.json
p.add_argument("--output", required=True)     # output folder
p.add_argument("--textured-obj")              # optional: final textured OBJ to receive labels
p.add_argument("--min-cos", type=float, default=0.2)   # ignore views more grazing than this (cosine of angle)
p.add_argument("--min-faces", type=int, default=50)    # ignore instances smaller than this (noise)
a = p.parse_args()

os.makedirs(a.output, exist_ok=True)

# Class id -> class name, written by segment_images.py
names = json.load(open(os.path.join(a.labels, "classes.json")))
n_classes = len(names)

# process=False keeps the original vertex and face order of the file
sfm = json.load(open(a.sfm))
mesh = trimesh.load(a.mesh, force="mesh", process=False)
centers = np.asarray(mesh.triangles_center)    # one 3D point per face, used for the projection
normals = np.asarray(mesh.face_normals)
votes = np.zeros((len(centers), n_classes), dtype=np.float32)   # weighted votes per face and class

# Look up the camera data of the openMVG scene by key
# (intrinsics: focal length, principal point; extrinsics: rotation, center)
intr = {i["key"]: i["value"]["ptr_wrapper"]["data"] for i in sfm["intrinsics"]}
poses = {e["key"]: e["value"] for e in sfm["extrinsics"]}
views = [v["value"]["ptr_wrapper"]["data"] for v in sfm["views"]]

used = 0
for v in views:
    pose = poses.get(v["id_pose"])
    lp = os.path.join(a.labels, os.path.splitext(v["filename"])[0] + ".png")
    # Skip views that were not reconstructed or have no label map
    if pose is None or not os.path.exists(lp):
        continue

    lab = np.array(Image.open(lp))
    h, w = lab.shape[:2]
    K = intr[v["id_intrinsic"]]
    f = K["focal_length"]
    cx, cy = K["principal_point"]
    # The label map may have a different resolution than the one in sfm_data.json
    sx, sy = w / K["width"], h / K["height"]

    # Project the face centers into the image (pinhole model, no distortion because
    # the undistorted images are used). openMVG stores the camera center C and the
    # world-to-camera rotation R, so a point X maps to R * (X - C).
    R = np.array(pose["rotation"])
    C = np.array(pose["center"])
    cam = (centers - C) @ R.T                       # world -> camera
    z = cam[:, 2]
    with np.errstate(divide="ignore", invalid="ignore"):
        u = (f * cam[:, 0] / z + cx) * sx           # pixel column
        t = (f * cam[:, 1] / z + cy) * sy           # pixel row

    # Direction from each face to the camera, and the view angle weight:
    # faces seen head-on (cos close to 1) are more reliable than grazing ones
    to_cam = C - centers
    dist = np.linalg.norm(to_cam, axis=1)
    to_cam /= dist[:, None]
    cos = np.abs(np.einsum("ij,ij->i", normals, to_cam))   # view angle weight

    # Keep faces in front of the camera, inside the image and not too grazing
    ok = (z > 0) & (u >= 0) & (u < w) & (t >= 0) & (t < h) & (cos >= a.min_cos)
    idx = np.where(ok)[0]
    if len(idx) == 0:
        continue

    # Occlusion test: cast a ray from the camera to each face; the first face it hits
    # must be the face itself, otherwise something is in front of it.
    # (embreex makes this fast: https://trimesh.org/trimesh.ray.html)
    hit = mesh.ray.intersects_first(np.repeat(C[None, :], len(idx), axis=0), -to_cam[idx])
    idx = idx[hit == idx]

    # Read the class id at the projected pixel and add the weighted vote
    ids = lab[t[idx].astype(int), u[idx].astype(int)]
    np.add.at(votes, (idx, ids), cos[idx])   # unbuffered add, handles repeated indices
    used += 1
    print(f"view {v['filename']}: {len(idx)} faces")

if used == 0:
    raise SystemExit("No views matched the label maps. Check image names and folders.")

# Winner per face; faces that no view could see take the label of the nearest seen face
seen = votes.sum(axis=1) > 0
face_label = votes.argmax(axis=1)
if (~seen).any():
    tree = cKDTree(centers[seen])
    _, nn = tree.query(centers[~seen])
    face_label[~seen] = face_label[seen][nn]

np.save(os.path.join(a.output, "face_labels.npy"), face_label)

# Colored preview (one random but repeatable color per class), open it in MeshLab to check the result
palette = np.random.default_rng(1).integers(50, 255, (n_classes, 3)).astype(np.uint8)
preview = trimesh.Trimesh(mesh.vertices, mesh.faces, process=False)
preview.visual.face_colors = np.hstack([palette[face_label], np.full((len(face_label), 1), 255, np.uint8)])
preview.export(os.path.join(a.output, "labeled.ply"))

# Instances: connected components of the faces of each class (e.g. each window separately)
inst_dir = os.path.join(a.output, "instances")
os.makedirs(inst_dir, exist_ok=True)
instances = []
for cls in np.unique(face_label):
    faces = np.where(face_label == cls)[0]
    sub = mesh.submesh([faces], append=True)
    parts = sub.split(only_watertight=False)     # connected components
    n = 0
    for part in parts:
        if len(part.faces) < a.min_faces:        # drop tiny components (label noise)
            continue
        n += 1
        # Class names look like "windowpane, window"; use the first one as a file-safe label
        label = names[str(cls)].split(",")[0].strip().replace(" ", "_")
        file = f"{label}_{n}.ply"
        part.export(os.path.join(inst_dir, file))
        instances.append({
            "class_id": int(cls), "class": label, "instance": f"{label}_{n}",
            "faces": int(len(part.faces)), "file": file,
            "bounds": part.bounds.tolist()})
json.dump(instances, open(os.path.join(a.output, "instances.json"), "w"), indent=2)

# Transfer labels to the textured OBJ, keeping texture coordinates and materials.
# The OBJ is rewritten as text so that "vt", "vn" and "mtllib" lines stay unchanged;
# only the faces are regrouped under one "g <class>" line per class and material.
if a.textured_obj:
    verts, head, faces, mat = [], [], [], None
    for line in open(a.textured_obj, encoding="utf-8"):
        if line.startswith("v "):
            verts.append([float(x) for x in line.split()[1:4]]); head.append(line)
        elif line.startswith("usemtl"):
            mat = line                           # remember the current material for the next faces
        elif line.startswith("f "):
            faces.append((line, mat))
        elif not line.startswith(("g ", "o ", "s ")):
            head.append(line)                    # vt, vn, mtllib, comments

    # Face centers of the OBJ (the first index of each "v/vt/vn" is the vertex index, 1-based)
    verts = np.array(verts)
    cent = np.array([verts[[int(t.split("/")[0]) - 1 for t in l.split()[1:4]]].mean(axis=0) for l, _ in faces])

    # The OBJ may have different faces than the PLY (e.g. after decimation),
    # so each OBJ face takes the label of the nearest PLY face center
    _, nn = cKDTree(centers).query(cent)
    groups = {}
    for (l, m), c in zip(faces, face_label[nn]):
        groups.setdefault((int(c), m), []).append(l)

    out_obj = os.path.join(a.output, "model_labeled.obj")
    with open(out_obj, "w", encoding="utf-8") as o:
        o.writelines(head)
        for (c, m), lines in sorted(groups.items(), key=lambda x: x[0][0]):
            o.write(f"g {names[str(c)].split(',')[0].strip().replace(' ', '_')}\n")
            if m:
                o.write(m)
            o.writelines(lines)
    print("wrote", out_obj)

# Number of faces per class id
print("done", np.bincount(face_label, minlength=n_classes))