"""Run after provisioning the local grid. No network access or survey data required."""
import math
from pathlib import Path
import sys

import rasterio
from rasterio.windows import Window
from pyproj.exceptions import ProjError

from georeference import GRID, transformer


def main(grid_dir):
    lon, lat, ellipsoid = 19, 48.5, 350
    op = transformer(grid_dir)
    e, n, bpv = op.transform(lon, lat, ellipsoid, errcheck=True)
    # Check vertical correction independently against bilinear interpolation of raw grid nodes.
    with rasterio.open(grid_dir / GRID) as d:
        col, row = (~d.transform) * (lon, lat)
        u, v = col - .5, row - .5
        i, j = math.floor(u), math.floor(v)
        a, b = u - i, v - j
        elevations = d.read(1, window=Window(i, j, 2, 2))
        correction = sum(float(elevations[y, x]) * wx * wy
                         for y, wy in enumerate((1-b, b)) for x, wx in enumerate((1-a, a)))
    if abs(ellipsoid - correction - bpv) > .001:
        raise AssertionError("Bpv grid result differs by more than 1 mm")
    # Frozen software reference. Independent physical control points are needed on the field.
    if abs(e + 430462.882) > .01 or abs(n + 1253824.228) > .01:
        raise AssertionError("Unexpected horizontal coordinates or axis order")
    try:
        op.transform(lat, lon, ellipsoid, errcheck=True)
    except ProjError:
        print("DVRM05 height, horizontal regression and swapped axes checked")
    else:
        raise AssertionError("Swapped input axes must fail outside grid")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
