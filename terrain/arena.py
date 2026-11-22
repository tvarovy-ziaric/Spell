"""Produce Spell v1 arena bundles, independent of either reference application."""
import argparse
import hashlib
import math
from pathlib import Path
import struct
import tempfile
import os
from xml.etree import ElementTree

HEADER = struct.Struct("<8sHHIIiiddd4dHH")
MAX_SIDE = 2048


def encode(width, height, origin, bounds, elevations, dataset, revision, crs=(0, 0)):
    if not (2 <= width <= MAX_SIDE and 2 <= height <= MAX_SIDE):
        raise ValueError("arena dimensions outside supported range")
    if len(elevations) != width * height:
        raise ValueError("wrong number of samples")
    dataset, revision = dataset.encode("utf-8"), revision.encode("utf-8")
    if not all(0 < len(item) <= 256 for item in (dataset, revision)):
        raise ValueError("dataset name and revision need 1 to 256 UTF-8 bytes each")
    if not all(math.isfinite(x) for x in (*origin, *bounds)):
        raise ValueError("coordinates must be finite")
    west, south, east, north = bounds
    if not (0 <= west < east <= width - 1 and 0 <= south < north <= height - 1):
        raise ValueError("play boundary lies outside sample grid")
    header = HEADER.pack(b"SPLARENA", 1, 1, width, height, *crs, *origin,
                         1.0, *bounds, len(dataset), len(revision))
    heights = bytearray(4 * len(elevations))
    mask = bytearray((len(elevations) + 7) // 8)
    for i, value in enumerate(elevations):
        if value is not None:
            if not math.isfinite(value):
                raise ValueError("valid sample must be finite")
            struct.pack_into("<f", heights, i * 4, value)
            mask[i // 8] |= 1 << (i % 8)
    return b"".join((header, dataset, revision, heights, mask))


def fixture():
    heights = [100 + max(0, 6 - 2 * (abs(e - 3) + abs(n - 3)))
               for n in range(7) for e in range(7)]
    heights[0] = None
    return encode(7, 7, (0, 0), (1, 1, 5, 5), heights, "spell-synthetic-hill", "1")


def extract(source, bounds, padding, revision):
    import rasterio
    from rasterio.windows import Window
    from pyproj import CRS

    if padding < 1:
        raise ValueError("at least one metre of data margin is required")
    west, south, east, north = bounds
    if not all(math.isfinite(x) for x in bounds) or west >= east or south >= north:
        raise ValueError("invalid play boundary")
    with rasterio.open(source, "r") as raster:
        crs = CRS(raster.crs)
        parts = crs.sub_crs_list or [crs]
        # Some DMR deliveries declare 8353 as the authority of a LOCAL_CS.
        # PROJ's CRS matcher does not resolve that still-explicit authority.
        local_id = parts[0].to_json_dict().get("id", {})
        legacy_jtsk03 = (local_id == {"authority": "EPSG", "code": 8353}
                         and parts[0].is_engineering
                         and [axis.direction for axis in parts[0].axis_info] == ["east", "north"]
                         and all(axis.unit_conversion_factor == 1 for axis in parts[0].axis_info))
        if parts[0].to_epsg() != 8353 and not legacy_jtsk03:
            raise ValueError("source must be in JTSK03 / Krovak East North (EPSG:8353)")
        if len(parts) > 1:
            vertical = parts[1].to_epsg()
        else:
            # This GKÚ delivery stores the vertical reference in the ArcGIS sidecar.
            sidecar = Path(str(source) + ".xml")
            definition = (ElementTree.parse(sidecar).findtext("./Esri/DataProperties/coordRef/peXml")
                          if sidecar.exists() else None)
            vertical = (ElementTree.fromstring(definition).findtext("VCSWKID")
                        if definition else None)
        if str(vertical) != "8357":
            raise ValueError("source metadata must identify Bpv (EPSG:8357)")
        transform = raster.transform
        if (transform.b != 0 or transform.d != 0 or
                not math.isclose(transform.a, 1, rel_tol=0, abs_tol=1e-10) or
                not math.isclose(transform.e, -1, rel_tol=0, abs_tol=1e-10)):
            raise ValueError("source must have unrotated one-metre native samples")
        # c/f are outside pixel corners, whereas the Spell origin is a sample centre.
        center_e, center_n = transform * (0.5, 0.5)
        left = math.floor((west - center_e) / transform.a) - padding
        right = math.ceil((east - center_e) / transform.a) + padding
        top = math.floor((north - center_n) / transform.e) - padding
        bottom = math.ceil((south - center_n) / transform.e) + padding
        width, height = right - left + 1, bottom - top + 1
        if width > MAX_SIDE or height > MAX_SIDE:
            raise ValueError("play area plus margin exceeds supported dimensions")
        if left < 0 or top < 0 or right >= raster.width or bottom >= raster.height:
            raise ValueError("play area plus margin exceeds source raster")
        window = raster.read(1, window=Window(left, top, width, height), masked=True)
        sw_e, sw_n = transform * (left + 0.5, bottom + 0.5)
        values = window[::-1].astype("float32").filled(float("nan"))
        aw, ass, ae, an = west - sw_e, south - sw_n, east - sw_e, north - sw_n
        import numpy as np
        required = values[math.floor(ass):math.ceil(an)+1, math.floor(aw):math.ceil(ae)+1]
        if not np.isfinite(required).all():
            raise ValueError("NoData intersects the playable area")
        samples = [v if math.isfinite(v) else None for v in values.ravel()]
        return encode(width, height, (sw_e, sw_n), (aw, ass, ae, an), samples,
                      "UGKK-SR-DMR-5.0", revision, crs=(8353, 8357))


def replace(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as handle:
        temp = Path(handle.name)
        try:
            handle.write(data)
        except BaseException:
            handle.close()
            temp.unlink(missing_ok=True)
            raise
    try:
        os.replace(temp, path)
    finally:
        temp.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_subparsers(dest="mode", required=True)
    generate = modes.add_parser("fixture")
    field = modes.add_parser("dmr")
    for command in (generate, field):
        command.add_argument("output", type=Path)
    field.add_argument("--source", required=True, type=Path,
                       help="source DMR5 raster with verified Bpv elevations")
    field.add_argument("--bounds", required=True, type=float, nargs=4,
                       metavar=("WEST", "SOUTH", "EAST", "NORTH"))
    field.add_argument("--margin", type=int, default=2)
    field.add_argument("--revision", required=True, help="source release identifier")
    args = parser.parse_args()
    if args.output.suffix != ".bytes":
        parser.error("output must use .bytes extension")
    if args.mode == "dmr" and args.source.resolve() == args.output.resolve():
        parser.error("output would overwrite source")
    data = fixture() if args.mode == "fixture" else extract(
        args.source, args.bounds, args.margin, args.revision)
    digest = hashlib.sha256(data).hexdigest()
    replace(args.output, data)
    if args.mode == "dmr":
        attribution = (
            f"ÚGKK SR — DMR 5.0. Revízia datasetu: {args.revision}\n"
            "https://www.gku.sk/gku/produkty-sluzby/na-stiahnutie/zbgis.html\n"
            "CC BY 4.0 https://creativecommons.org/licenses/by/4.0/\n"
            "Úpravy: výrez natívnej mriežky, zmena poradia riadkov, maska platnosti "
            "a balenie do formátu Spell v1.\n")
        replace(args.output.with_suffix(".source.txt"), attribution.encode("utf-8"))
    replace(args.output.with_suffix(".sha256"), (digest + "\n").encode("ascii"))
    print(f"{len(data)} bytes, SHA-256 {digest}")


if __name__ == "__main__":
    main()
