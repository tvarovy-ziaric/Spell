import site.arnavigator.spell.FieldGnssCapture;
public final class CaptureChecks {
    static void require(boolean b) { if (!b) throw new AssertionError(); }
    static void offer(FieldGnssCapture c, long ns, boolean mock, double alt) {
        c.offer(ns, ns, 12345, 19.12345678, 48, alt, 1, Double.NaN, mock);
    }
    public static void main(String[] args) {
        var c = new FieldGnssCapture();
        offer(c, 11, false, 100); require(c.read(12).isEmpty());
        c.arm(20); offer(c, 19, false, 100); require(c.pending(21));
        offer(c, 22, true, 100); require(c.pending(23));
        offer(c, 24, false, Double.NaN); require(c.pending(25));
        offer(c, 26, false, 100);
        String accepted = c.read(27);
        require(accepted.contains("WGS84") && accepted.contains("19.12345678") && accepted.contains("nedostupná"));
        offer(c, 28, false, 0); require(c.read(29).equals(accepted));
        c.arm(30); require(!c.pending(30_000_000_031L));
        c.clear(); require(c.read(31_000_000_000L).isEmpty());
        System.out.println("PASS opt-in capture, stale/invalid/mock inputs, ellipsoid label, time-out and erasure");
    }
}
