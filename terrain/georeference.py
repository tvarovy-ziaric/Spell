"""Transform an ETRS89 control point into the coordinate systems of DMR5."""
import argparse
import hashlib
from pathlib import Path
from urllib.request import urlopen

from arena import replace

GRID = "sk_gku_Slovakia_ETRS89h_to_Baltic1957.tif"
SHA256 = "d280e6f7f6c84714d4c060519cf2213a43c0f9064f2cd3de23265a2671c7e865"


def verify(data):
    if hashlib.sha256(data).hexdigest() != SHA256:
        raise ValueError("Unexpected DVRM05 grid checksum")


def provision(directory):
    file = directory / GRID
    if file.exists():
        verify(file.read_bytes())
        return
    with urlopen("https://cdn.proj.org/" + GRID, timeout=30) as response:
        data = response.read(500_000)
    verify(data)
    replace(file, data)


def transformer(directory):
    from pyproj import datadir, network, Transformer
    from pyproj.aoi import AreaOfInterest

    verify((directory / GRID).read_bytes())
    network.set_network_enabled(False)
    datadir.append_data_dir(str(directory.resolve()))
    return Transformer.from_crs("EPSG:4937", "EPSG:8353+8357", always_xy=True,
                                area_of_interest=AreaOfInterest(16.8, 47.7, 22.6, 49.7),
                                allow_ballpark=False, only_best=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="private local directory for PROJ grid")
    modes = parser.add_subparsers(dest="mode", required=True)
    modes.add_parser("provision")
    point = modes.add_parser("point")
    point.add_argument("lon", type=float)
    point.add_argument("lat", type=float)
    point.add_argument("ellipsoid_m", type=float)
    args = parser.parse_args()
    if args.mode == "provision":
        provision(args.directory)
        print("DVRM05 SHA-256 verified")
    else:
        operation = transformer(args.directory)
        e, n, h = operation.transform(args.lon, args.lat, args.ellipsoid_m, errcheck=True)
        print(f"{e:.3f} {n:.3f} {h:.3f}")


if __name__ == "__main__":
    main()
