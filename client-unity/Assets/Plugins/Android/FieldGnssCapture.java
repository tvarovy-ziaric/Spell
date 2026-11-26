package site.arnavigator.spell;

import java.util.Locale;

/** Explicit, RAM-only, one-fix field observation. Never includes an inferred board position. */
public final class FieldGnssCapture {
    private long armedAtNs = -1;
    private String display = "";
    private static final long TIMEOUT_NS = 30_000_000_000L;
    public synchronized void arm(long nowNs) {
        armedAtNs = nowNs;
        display = "Čakám na nový fix na tomto mieste (najviac 30 s).";
    }
    public synchronized boolean pending(long nowNs) {
        if (armedAtNs >= 0 && nowNs - armedAtNs > TIMEOUT_NS) {
            armedAtNs = -1;
            display = "Nový použiteľný fix neprišiel do 30 s.";
        }
        return armedAtNs >= 0;
    }
    public synchronized void offer(long nowNs, long fixNs, long utcMs, double longitude, double latitude,
                                  double altitude, double horizontalError, double verticalError, boolean simulated) {
        if (!pending(nowNs) || fixNs <= armedAtNs || fixNs > nowNs || simulated || utcMs <= 0 ||
            !Double.isFinite(longitude) || Math.abs(longitude) > 180 || !Double.isFinite(latitude) ||
            Math.abs(latitude) > 90 || !Double.isFinite(altitude) || !Double.isFinite(horizontalError) ||
            horizontalError < 0) return;
        armedAtNs = -1;
        String vertical = Double.isFinite(verticalError) && verticalError >= 0
            ? String.format(Locale.ROOT, "%.2f m", verticalError) : "nedostupná";
        display = String.format(Locale.ROOT,
            "Anténa WGS84 · lon %.8f°, lat %.8f°, elipsoid %.3f m. Presnosť H %.2f m, V %s. UTC epoch ms %d, monotónny čas µs %d.",
            longitude, latitude, altitude, horizontalError, vertical, utcMs, fixNs / 1000);
    }
    public synchronized String read(long nowNs) { pending(nowNs); return display; }
    public synchronized void clear() { armedAtNs = -1; display = ""; }
}
