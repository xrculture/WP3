import argparse, glob, json, os
import numpy as np
import torch
from PIL import Image
from transformers import AutoImageProcessor, SegformerForSemanticSegmentation

p = argparse.ArgumentParser()
p.add_argument("--images", required=True)
p.add_argument("--output", required=True)
a = p.parse_args()

os.makedirs(a.output, exist_ok=True)

def pick_device():
    if not torch.cuda.is_available():
        return "cpu"
    major, minor = torch.cuda.get_device_capability()
    if f"sm_{major}{minor}" in torch.cuda.get_arch_list():
        return "cuda"
    return "cpu"

device = pick_device()
name = "nvidia/segformer-b5-finetuned-ade-640-640"
processor = AutoImageProcessor.from_pretrained(name)
model = SegformerForSemanticSegmentation.from_pretrained(name).to(device).eval()

# class id -> name, used later when interpreting the labels
json.dump(model.config.id2label, open(os.path.join(a.output, "classes.json"), "w"), indent=2)

files = []
for ext in ("jpg", "jpeg", "png", "JPG", "JPEG", "PNG"):
    files += glob.glob(os.path.join(a.images, f"*.{ext}"))

for f in sorted(set(files)):
    img = Image.open(f).convert("RGB")
    inputs = processor(images=img, return_tensors="pt").to(device)
    with torch.no_grad():
        logits = model(**inputs).logits
    logits = torch.nn.functional.interpolate(
        logits, size=img.size[::-1], mode="bilinear", align_corners=False)
    labels = logits.argmax(dim=1).squeeze().cpu().numpy().astype(np.uint8)
    name_png = os.path.splitext(os.path.basename(f))[0] + ".png"
    Image.fromarray(labels).save(os.path.join(a.output, name_png))
    print("labeled", os.path.basename(f))