#!/usr/bin/env python3
"""
Inject JSON properties into the extras of a GLB file.

Usage:
    python inject_extras.py <input.glb> <extras.json> [output.glb]

If output.glb is omitted, the input file is overwritten.
"""

import argparse
import json
import struct
import sys

GLB_MAGIC = 0x46546C67  # "glTF"
GLB_VERSION = 2
CHUNK_TYPE_JSON = 0x4E4F534A  # "JSON"
CHUNK_TYPE_BIN = 0x004E4942   # "BIN\0"


def pad(data: bytes, alignment: int, pad_byte: int) -> bytes:
    remainder = len(data) % alignment
    if remainder:
        data += bytes([pad_byte] * (alignment - remainder))
    return data


def read_glb(path: str):
    with open(path, "rb") as f:
        raw = f.read()

    if len(raw) < 12:
        sys.exit(f"Error: {path} is too small to be a valid GLB file.")

    magic, version, total_length = struct.unpack_from("<III", raw, 0)

    if magic != GLB_MAGIC:
        sys.exit(f"Error: {path} does not start with the glTF magic number.")
    if version != GLB_VERSION:
        sys.exit(f"Error: unsupported GLB version {version} (only version 2 is supported).")

    offset = 12
    chunks = []
    while offset < len(raw):
        if offset + 8 > len(raw):
            sys.exit("Error: truncated chunk header.")
        chunk_length, chunk_type = struct.unpack_from("<II", raw, offset)
        offset += 8
        chunk_data = raw[offset : offset + chunk_length]
        if len(chunk_data) < chunk_length:
            sys.exit("Error: truncated chunk data.")
        chunks.append((chunk_type, chunk_data))
        offset += chunk_length

    return chunks


def write_glb(path: str, chunks: list):
    body = b""
    for chunk_type, chunk_data in chunks:
        pad_byte = 0x20 if chunk_type == CHUNK_TYPE_JSON else 0x00
        padded = pad(chunk_data, 4, pad_byte)
        body += struct.pack("<II", len(padded), chunk_type) + padded

    total_length = 12 + len(body)
    header = struct.pack("<III", GLB_MAGIC, GLB_VERSION, total_length)

    with open(path, "wb") as f:
        f.write(header + body)


def inject_extras(input_glb: str, extras_json: str, output_glb: str):
    chunks = read_glb(input_glb)

    if not chunks or chunks[0][0] != CHUNK_TYPE_JSON:
        sys.exit("Error: first chunk is not a JSON chunk.")

    # Parse extras JSON
    with open(extras_json, "r", encoding="utf-8") as f:
        new_extras = json.load(f)

    if not isinstance(new_extras, dict):
        sys.exit("Error: extras JSON must be a JSON object (top-level dict).")

    # Parse glTF JSON
    gltf = json.loads(chunks[0][1].decode("utf-8"))

    # Merge into asset.extras
    asset = gltf.setdefault("asset", {})
    existing_extras = asset.get("extras", {})
    if not isinstance(existing_extras, dict):
        existing_extras = {}
    existing_extras.update(new_extras)
    asset["extras"] = existing_extras

    # Re-encode without extra whitespace to keep the file compact
    updated_json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")

    updated_chunks = [(CHUNK_TYPE_JSON, updated_json_bytes)] + chunks[1:]
    write_glb(output_glb, updated_chunks)
    print(f"Saved: {output_glb}")


def main():
    parser = argparse.ArgumentParser(
        description="Inject JSON properties into the extras of a GLB file."
    )
    parser.add_argument("input_glb", help="Path to the input .glb file")
    parser.add_argument("extras_json", help="Path to the JSON file whose properties will be injected")
    parser.add_argument(
        "output_glb",
        nargs="?",
        help="Path for the output .glb file (defaults to overwriting the input)",
    )
    args = parser.parse_args()

    output = args.output_glb if args.output_glb else args.input_glb
    inject_extras(args.input_glb, args.extras_json, output)


if __name__ == "__main__":
    main()
