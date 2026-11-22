import hashlib
import struct
from pathlib import Path
import tempfile
import unittest

from arena import HEADER, encode, extract, fixture


class FixtureTests(unittest.TestCase):
    def test_reproducible_bundle(self):
        folder = Path(__file__).resolve().parents[1] / "contracts" / "fixtures"
        bundle = fixture()
        self.assertEqual(bundle, (folder / "hill.bytes").read_bytes())
        self.assertEqual(hashlib.sha256(bundle).hexdigest(), (folder / "hill.sha256").read_text().strip())

    def test_invalid_bounds(self):
        with self.assertRaises(ValueError):
            encode(2, 2, (0, 0), (0, 0, 2, 1), [100] * 4, "s", "1")


class RasterTests(unittest.TestCase):
    def setUp(self):
        import numpy as np
        import rasterio
        from rasterio.transform import from_origin

        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.raster = Path(self.folder.name) / "test.tif"
        # Pixel centres .5..8.5, raster row zero is north. Distinct row/col slopes.
        with rasterio.open(self.raster, "w", driver="GTiff", height=9, width=9, count=1,
                           dtype="float32", transform=from_origin(0, 9, 1, 1), crs="EPSG:8353",
                           nodata=-999) as dst:
            a = np.fromfunction(lambda row, col: 10 * row + col, (9, 9), dtype=float).astype("float32")
            a[0, 0] = -999
            dst.write(a, 1)
        self.sidecar = Path(str(self.raster) + ".xml")
        self.sidecar.write_text(
            "<metadata><Esri><DataProperties><coordRef><peXml>"
            "&lt;ProjectedCoordinateSystem&gt;&lt;VCSWKID&gt;8357&lt;/VCSWKID&gt;&lt;/ProjectedCoordinateSystem&gt;"
            "</peXml></coordRef></DataProperties></Esri></metadata>")

    def test_axes_pixel_centres_padding_and_determinism(self):
        args = (self.raster, (2.5, 3.5, 5.5, 6.5), 1, "r1")
        data = extract(*args)
        self.assertEqual(data, extract(*args))
        fields = HEADER.unpack_from(data)
        self.assertEqual(fields[3:7], (6, 6, 8353, 8357))
        self.assertEqual(fields[7:14], (1.5, 2.5, 1, 1, 1, 4, 4))
        start = HEADER.size + fields[-2] + fields[-1]
        heights = struct.unpack_from("<36f", data, start)
        self.assertEqual((heights[0], heights[5], heights[-1]), (61, 66, 16))

    def test_missing_vertical_reference(self):
        self.sidecar.unlink()
        with self.assertRaisesRegex(ValueError, "8357"):
            extract(self.raster, (2, 3, 5, 6), 1, "r1")

    def test_margin_outside_region(self):
        with self.assertRaisesRegex(ValueError, "exceeds"):
            extract(self.raster, (.5, .5, 4, 6), 1, "r1")

    def test_nodata_inside_playable_area(self):
        import rasterio
        from rasterio.windows import Window
        import numpy as np
        with rasterio.open(self.raster, "r+") as dst:
            dst.write(np.array([[-999]], dtype="float32"), 1, window=Window(3, 4, 1, 1))
        with self.assertRaisesRegex(ValueError, "NoData"):
            extract(self.raster, (2, 3, 5, 6), 1, "r1")


if __name__ == "__main__":
    unittest.main()
