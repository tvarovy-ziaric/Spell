package site.arnavigator.spell;

import com.google.zxing.BinaryBitmap;
import com.google.zxing.PlanarYUVLuminanceSource;
import com.google.zxing.ReaderException;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;

/** Reads the Y plane only. Camera preview bytes never leave the device. */
public final class InvitationDecoder {
    private final QRCodeReader reader = new QRCodeReader();

    public String decode(byte[] nv21, int width, int height) {
        if (nv21 == null || width <= 0 || height <= 0 || (long) width * height > nv21.length) return null;
        PlanarYUVLuminanceSource luminance = new PlanarYUVLuminanceSource(
            nv21, width, height, 0, 0, width, height, false);
        try {
            return reader.decode(new BinaryBitmap(new HybridBinarizer(luminance))).getText();
        } catch (ReaderException noReadableCode) {
            return null;
        } finally {
            reader.reset();
        }
    }
}
