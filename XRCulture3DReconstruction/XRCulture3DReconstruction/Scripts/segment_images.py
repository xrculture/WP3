"""
Semantic segmentation of the undistorted photos with SegFormer (ADE20K, 150 classes).

For every input image it writes a single-channel PNG label map (pixel value = class id)
with the same base name, plus classes.json (class id -> class name).
The label maps are consumed by label_mesh.py, which votes the labels onto the 3D mesh.

Docs:
  SegFormer model in Transformers:  https://huggingface.co/docs/transformers/model_doc/segformer
  Semantic segmentation guide:      https://huggingface.co/docs/transformers/tasks/semantic_segmentation
  Model card (weights, classes):    https://huggingface.co/nvidia/segformer-b5-finetuned-ade-640-640
  ADE20K dataset (class list):      https://ade20k.csail.mit.edu/
  PyTorch CUDA semantics:           https://pytorch.org/docs/stable/notes/cuda.html
  PyTorch install selector:         https://pytorch.org/get-started/locally/

Usage:
  python segment_images.py --images <folder with images> --output <folder for label maps>
"""
import argparse, glob, json, os
import numpy as np
import torch
from PIL import Image
# https://huggingface.co/docs/transformers/model_doc/segformer#transformers.SegformerForSemanticSegmentation
from transformers import AutoImageProcessor, SegformerForSemanticSegmentation

# Command line arguments
p = argparse.ArgumentParser()
p.add_argument("--images", required=True)   # input folder (jpg/png images)
p.add_argument("--output", required=True)   # output folder for label maps and classes.json
a = p.parse_args()

os.makedirs(a.output, exist_ok=True)


def pick_device():
    """Use the GPU only if the installed PyTorch build contains kernels for it.

    A new GPU (e.g. Blackwell, sm_120) with an older PyTorch build reports
    cuda.is_available() == True but fails at the first kernel call, so check the
    supported architectures explicitly and fall back to the CPU.
    https://pytorch.org/docs/stable/generated/torch.cuda.get_arch_list.html
    """
    if not torch.cuda.is_available():
        return "cpu"
    major, minor = torch.cuda.get_device_capability()
    if f"sm_{major}{minor}" in torch.cuda.get_arch_list():
        return "cuda"
    return "cpu"


device = pick_device()

# SegFormer-B5 trained on ADE20K at 640x640 (most accurate variant; use b2 for speed).
# Weights are downloaded on first use and cached (cache folder can be set with HF_HOME).
# https://huggingface.co/docs/huggingface_hub/guides/manage-cache
name = "nvidia/segformer-b5-finetuned-ade-640-640"
processor = AutoImageProcessor.from_pretrained(name)   # resizes and normalizes the image
model = SegformerForSemanticSegmentation.from_pretrained(name).to(device).eval()

# Class id -> class name (wall, building, windowpane, door, ...), used when interpreting the labels
json.dump(model.config.id2label, open(os.path.join(a.output, "classes.json"), "w"), indent=2)

# Collect the images (both lower and upper case extensions, for case-sensitive file systems)
files = []
for ext in ("jpg", "jpeg", "png", "JPG", "JPEG", "PNG"):
    files += glob.glob(os.path.join(a.images, f"*.{ext}"))

for f in sorted(set(files)):
    img = Image.open(f).convert("RGB")
    inputs = processor(images=img, return_tensors="pt").to(device)

    # Forward pass without gradients; logits shape is (1, num_classes, H/4, W/4)
    with torch.no_grad():
        logits = model(**inputs).logits

    # Upsample the logits to the original image size (img.size is (W, H), tensors use (H, W))
    logits = torch.nn.functional.interpolate(
        logits, size=img.size[::-1], mode="bilinear", align_corners=False)

    # Most likely class per pixel; ids are < 150, so uint8 is enough
    labels = logits.argmax(dim=1).squeeze().cpu().numpy().astype(np.uint8)

    # The label map keeps the image base name, which label_mesh.py uses to match it
    # with the view in sfm_data.json
    name_png = os.path.splitext(os.path.basename(f))[0] + ".png"
    Image.fromarray(labels).save(os.path.join(a.output, name_png))
    print("labeled", os.path.basename(f))