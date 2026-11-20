import argparse, json, os
import numpy as np
import trimesh
from PIL import Image
from scipy.spatial import cKDTree

p = argparse.ArgumentParser()
p.add_argument("--sfm", required=True)        # sfm_data.json
p.add_argument("--mesh", required=True)       # untextured PLY, e.g. model_dense_mesh_refine.ply
p.add_argument("--labels", required=True)     # folder with <image>.png label maps + classes.json
p.add_argument("--output", required=True)     # output folder
p.add_argument("--textured-obj")              # optional: final textured OBJ to receive labels
p.add_argument("--min-cos", type=float, default=0.2)
p.add_argument("--min-faces", type=int, default=50)
a = p.parse_args()

os.makedirs(a.output, exist_ok=True)
names = json.load(open(os.path.join(a.labels, "classes.json")))
n_classes = len(names)

sfm = json.load(open(a.sfm))
mesh = trimesh.load(a.mesh, force="mesh", process=False)
centers = np.asarray(mesh.triangles_center)
normals = np.asarray(mesh.face_normals)
votes = np.zeros((len(centers), n_classes), dtype=np.float32)

intr = {i["key"]: i["value"]["ptr_wrapper"]["data"] for i in sfm["intrinsics"]}
poses = {e["key"]: e["value"] for e in sfm["extrinsics"]}
views = [v["value"]["ptr_wrapper"]["data"] for v in sfm["views"]]

used = 0
for v in views:
    pose = poses.get(v["id_pose"])
    lp = os.path.join(a.labels, os.path.splitext(v["filename"])[0] + ".png")
    if pose is None or not os.path.exists(lp):
        continue

    lab = np.array(Image.open(lp))
    h, w = lab.shape[:2]
    K = intr[v["id_intrinsic"]]
    f = K["focal_length"]
    cx, cy = K["principal_point"]
    sx, sy = w / K["width"], h / K["height"]

    R = np.array(pose["rotation"])
    C = np.array(pose["center"])
    cam = (centers - C) @ R.T                       # world -> camera
    z = cam[:, 2]
    with np.errstate(divide="ignore", invalid="ignore"):
        u = (f * cam[:, 0] / z + cx) * sx
        t = (f * cam[:, 1] / z + cy) * sy

    to_cam = C - centers
    dist = np.linalg.norm(to_cam, axis=1)
    to_cam /= dist[:, None]
    cos = np.abs(np.einsum("ij,ij->i", normals, to_cam))   # view angle weight

    ok = (z > 0) & (u >= 0) & (u < w) & (t >= 0) & (t < h) & (cos >= a.min_cos)
    idx = np.where(ok)[0]
    if len(idx) == 0:
        continue

    # Occlusion test: the first face hit from the camera must be the face itself
    hit = mesh.ray.intersects_first(np.repeat(C[None, :], len(idx), axis=0), -to_cam[idx])
    idx = idx[hit == idx]

    ids = lab[t[idx].astype(int), u[idx].astype(int)]
    np.add.at(votes, (idx, ids), cos[idx])
    used += 1
    print(f"view {v['filename']}: {len(idx)} faces")

if used == 0:
    raise SystemExit("No views matched the label maps. Check image names and folders.")

seen = votes.sum(axis=1) > 0
face_label = votes.argmax(axis=1)
if (~seen).any():                                   # unseen faces take the nearest seen face's label
    tree = cKDTree(centers[seen])
    _, nn = tree.query(centers[~seen])
    face_label[~seen] = face_label[seen][nn]

np.save(os.path.join(a.output, "face_labels.npy"), face_label)

# Colored preview
palette = np.random.default_rng(1).integers(50, 255, (n_classes, 3)).astype(np.uint8)
preview = trimesh.Trimesh(mesh.vertices, mesh.faces, process=False)
preview.visual.face_colors = np.hstack([palette[face_label], np.full((len(face_label), 1), 255, np.uint8)])
preview.export(os.path.join(a.output, "labeled.ply"))

# Instances: connected components per class
inst_dir = os.path.join(a.output, "instances")
os.makedirs(inst_dir, exist_ok=True)
instances = []
for cls in np.unique(face_label):
    faces = np.where(face_label == cls)[0]
    sub = mesh.submesh([faces], append=True)
    parts = sub.split(only_watertight=False)
    n = 0
    for part in parts:
        if len(part.faces) < a.min_faces:
            continue
        n += 1
        label = names[str(cls)].split(",")[0].strip().replace(" ", "_")
        file = f"{label}_{n}.ply"
        part.export(os.path.join(inst_dir, file))
        instances.append({
            "class_id": int(cls), "class": label, "instance": f"{label}_{n}",
            "faces": int(len(part.faces)), "file": file,
            "bounds": part.bounds.tolist()})
json.dump(instances, open(os.path.join(a.output, "instances.json"), "w"), indent=2)

# Transfer labels to the textured OBJ, keeping texture coordinates and materials
if a.textured_obj:
    verts, head, faces, mat = [], [], [], None
    for line in open(a.textured_obj, encoding="utf-8"):
        if line.startswith("v "):
            verts.append([float(x) for x in line.split()[1:4]]); head.append(line)
        elif line.startswith("usemtl"):
            mat = line
        elif line.startswith("f "):
            faces.append((line, mat))
        elif not line.startswith(("g ", "o ", "s ")):
            head.append(line)

    verts = np.array(verts)
    cent = np.array([verts[[int(t.split("/")[0]) - 1 for t in l.split()[1:4]]].mean(axis=0) for l, _ in faces])
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

print("done", np.bincount(face_label, minlength=n_classes))