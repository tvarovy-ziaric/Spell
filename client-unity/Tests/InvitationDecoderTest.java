import java.awt.image.BufferedImage;
import java.nio.file.Path;
import java.util.Arrays;
import javax.imageio.ImageIO;
import site.arnavigator.spell.InvitationDecoder;

/** Supply the exact invitation and its PNG produced by the Spell server. */
public final class InvitationDecoderTest {
    public static void main(String[] args) throws Exception {
        BufferedImage original = ImageIO.read(Path.of(args[1]).toFile());
        if (original == null) throw new AssertionError("Invalid PNG fixture");
        InvitationDecoder decoder = new InvitationDecoder();
        int width = 640, height = 480, side = original.getWidth();
        if (side != original.getHeight() || side > 390) throw new AssertionError("Expected a square QR");
        for (int rotation = 0; rotation < 4; rotation++) {
            byte[] preview = new byte[width * height * 3 / 2];
            Arrays.fill(preview, 0, width * height, (byte) 208);
            for (int row = 0; row < side; row++) for (int col = 0; col < side; col++) {
                int x = col, y = row;
                for (int turn = 0; turn < rotation; turn++) {
                    int previous = x;
                    x = side - y - 1;
                    y = previous;
                }
                preview[(20 + row) * width + 98 + col] = (byte) (original.getRGB(x, y) & 255);
            }
            if (!args[0].equals(decoder.decode(preview, width, height)))
                throw new AssertionError("Rotation " + rotation);
        }
        if (decoder.decode(new byte[width * height], width, height) != null ||
            decoder.decode(new byte[3], width, height) != null) throw new AssertionError("Invalid frame accepted");
        System.out.println("QR decoder: 4 orientations, uniform frame and truncated frame verified.");
    }
}
