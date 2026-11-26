package site.arnavigator.spell;

import android.app.Activity;
import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorManager;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Bundle;
import android.os.Looper;
import android.os.SystemClock;
import java.util.Locale;

public final class GnssProbe {
    private static LocationManager manager;
    private static long lastFixNs;
    private static long count;
    private static final FieldGnssCapture[] field = { new FieldGnssCapture(), new FieldGnssCapture() };
    private static int active = -1;
    public static synchronized void armField(int board) {
        if (board < 0 || board >= field.length || manager == null) return;
        if (active != -1 && field[active].pending(SystemClock.elapsedRealtimeNanos())) return;
        active = board;
        field[board].arm(SystemClock.elapsedRealtimeNanos());
    }
    public static synchronized String readField(int board) {
        return board >= 0 && board < field.length ? field[board].read(SystemClock.elapsedRealtimeNanos()) : "";
    }
    public static synchronized void clearField() {
        active = -1;
        for (FieldGnssCapture capture : field) capture.clear();
    }
    private static float accuracy;
    private static String state = "Bez povolenia na presnú polohu.";

    private GnssProbe() {}

    public static long elapsedUs() { return SystemClock.elapsedRealtimeNanos() / 1000; }

    public static int thermalStatus(Activity activity) {
        if (android.os.Build.VERSION.SDK_INT < 29) return -1;
        android.os.PowerManager power = (android.os.PowerManager) activity.getSystemService(Context.POWER_SERVICE);
        return power == null ? -1 : power.getCurrentThermalStatus();
    }

    public static synchronized long lastFixUs() { return lastFixNs / 1000; }

    public static String capabilities(Activity activity) {
        SensorManager sensors = (SensorManager) activity.getSystemService(Context.SENSOR_SERVICE);
        boolean accelerometer = sensors != null && sensors.getDefaultSensor(Sensor.TYPE_ACCELEROMETER) != null;
        boolean gyro = sensors != null && sensors.getDefaultSensor(Sensor.TYPE_GYROSCOPE) != null;
        return "Akcelerometer: " + (accelerometer ? "áno" : "nie") + ", gyroskop: " + (gyro ? "áno" : "nie");
    }

    private static final LocationListener listener = new LocationListener() {
        @Override public void onLocationChanged(Location location) {
            synchronized (GnssProbe.class) {
                if (manager == null || location.getElapsedRealtimeNanos() <= lastFixNs) return;
                lastFixNs = location.getElapsedRealtimeNanos();
                count++;
                long now = SystemClock.elapsedRealtimeNanos();
                if (active != -1 && field[active].pending(now)) {
                    double vertical = android.os.Build.VERSION.SDK_INT >= 26 && location.hasVerticalAccuracy()
                        ? location.getVerticalAccuracyMeters() : Double.NaN;
                    field[active].offer(now, lastFixNs, location.getTime(), location.getLongitude(), location.getLatitude(),
                        location.hasAltitude() ? location.getAltitude() : Double.NaN,
                        location.hasAccuracy() ? location.getAccuracy() : Double.NaN, vertical, location.isFromMockProvider());
                }
                accuracy = location.hasAccuracy() ? location.getAccuracy() : Float.NaN;
            }
        }
        @Override public void onProviderDisabled(String provider) {
            synchronized (GnssProbe.class) { state = "GPS je vypnuté."; }
        }
        @Override public void onProviderEnabled(String provider) {
            synchronized (GnssProbe.class) { state = "Čakám na GPS meranie."; }
        }
        @Override public void onStatusChanged(String provider, int status, Bundle extras) {}
    };

    public static synchronized void start(Activity activity) {
        if (manager != null) return;
        LocationManager source = (LocationManager)activity.getSystemService(Context.LOCATION_SERVICE);
        try {
            if (source == null || !source.getAllProviders().contains(LocationManager.GPS_PROVIDER)) {
                state = "Zariadenie nemá GPS.";
                return;
            }
            source.requestLocationUpdates(LocationManager.GPS_PROVIDER, 0, 0, listener, Looper.getMainLooper());
            manager = source;
            state = source.isProviderEnabled(LocationManager.GPS_PROVIDER) ? "Čakám na GPS meranie." : "GPS je vypnuté.";
        } catch (SecurityException denied) {
            state = "Chýba povolenie na presnú polohu.";
        }
    }

    public static synchronized void stop() {
        clearField();
        if (manager != null) {
            try { manager.removeUpdates(listener); } catch (SecurityException revoked) { /* already revoked */ }
        }
        manager = null;
        lastFixNs = 0;
        count = 0;
    }

    public static synchronized String status() {
        if (lastFixNs == 0) return "GNSS: " + state;
        double age = (SystemClock.elapsedRealtimeNanos() - lastFixNs) / 1e9;
        return String.format(Locale.ROOT, "GNSS: %d meraní, vek %.1f s, presnosť %.1f m.", count, age, accuracy);
    }
}
