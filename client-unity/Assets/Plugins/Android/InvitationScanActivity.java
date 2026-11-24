package site.arnavigator.spell;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.ImageFormat;
import android.hardware.Camera;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.Surface;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import java.io.IOException;
import java.lang.ref.WeakReference;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** A foreground, app-private scanner. Unity pauses its AR session while this activity owns the camera. */
@SuppressWarnings("deprecation")
public final class InvitationScanActivity extends Activity implements SurfaceHolder.Callback {
    private static final String REQUEST = "site.arnavigator.spell.scanRequest";
    private static final int CAMERA_PERMISSION = 47;
    private static String pending;
    private static String result;
    private static WeakReference<InvitationScanActivity> visible = new WeakReference<>(null);

    public static synchronized boolean begin(Activity parent, String id) {
        if (pending != null || parent == null || parent.isFinishing()) return false;
        pending = id;
        result = null;
        parent.runOnUiThread(() -> {
            if (!isPending(id)) return;
            try {
                parent.startActivity(new Intent(parent, InvitationScanActivity.class).putExtra(REQUEST, id));
            } catch (RuntimeException unavailable) {
                publish(id, "!Skenovanie QR sa nepodarilo spustiť.");
            }
        });
        return true;
    }

    private static synchronized boolean isPending(String id) {
        return id != null && id.equals(pending) && result == null;
    }

    private static synchronized void publish(String id, String outcome) {
        if (isPending(id)) result = outcome;
    }

    public static synchronized String take(String id) {
        if (id == null || !id.equals(pending)) return "-";
        if (result == null) return null;
        String completed = result;
        result = null;
        pending = null;
        return completed;
    }

    public static synchronized void cancel(String id) {
        if (id == null || !id.equals(pending)) return;
        pending = null;
        result = null;
        InvitationScanActivity activity = visible.get();
        if (activity != null && id.equals(activity.id)) activity.runOnUiThread(activity::finish);
    }

    private String id;
    private SurfaceView preview;
    private FrameLayout frame;
    private boolean resumed, surfaceReady;
    private int openAttempts, layoutWidth, layoutHeight;
    private Camera camera;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final ExecutorService decodeQueue = Executors.newSingleThreadExecutor();
    private final InvitationDecoder decoder = new InvitationDecoder();

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        id = getIntent().getStringExtra(REQUEST);
        if (!isPending(id)) { finish(); return; }
        synchronized (InvitationScanActivity.class) { visible = new WeakReference<>(this); }
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        frame = new FrameLayout(this);
        frame.setBackgroundColor(Color.BLACK);
        preview = new SurfaceView(this);
        frame.addView(preview, new FrameLayout.LayoutParams(-1, -1, Gravity.CENTER));
        preview.getHolder().addCallback(this);
        frame.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> fitPreview());
        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.VERTICAL);
        int edge = Math.round(12 * getResources().getDisplayMetrics().density);
        controls.setPadding(edge, edge, edge, edge);
        frame.setOnApplyWindowInsetsListener((view, insets) -> {
            controls.setPadding(edge + insets.getSystemWindowInsetLeft(), edge,
                edge + insets.getSystemWindowInsetRight(), edge + insets.getSystemWindowInsetBottom());
            return insets;
        });
        controls.setBackgroundColor(0xc0000000);
        TextView hint = new TextView(this);
        hint.setText("Namier kameru na QR pozvánku z druhého telefónu.");
        hint.setTextColor(Color.WHITE);
        hint.setTextSize(18);
        controls.addView(hint);
        Button cancel = new Button(this);
        cancel.setText("Zrušiť skenovanie");
        cancel.setOnClickListener(view -> complete("-"));
        controls.addView(cancel);
        frame.addView(controls, new FrameLayout.LayoutParams(-1, -2, Gravity.BOTTOM));
        setContentView(frame);
        if (saved == null && !cameraAllowed()) requestPermissions(new String[] {Manifest.permission.CAMERA}, CAMERA_PERMISSION);
    }

    private boolean cameraAllowed() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    @Override public void onRequestPermissionsResult(int code, String[] names, int[] outcomes) {
        super.onRequestPermissionsResult(code, names, outcomes);
        if (code != CAMERA_PERMISSION) return;
        if (!cameraAllowed()) complete("!Pre skenovanie povoľ prístup ku kamere.");
        else tryOpen();
    }
    @Override public void onResume() { super.onResume(); resumed = true; openAttempts = 0; tryOpen(); }
    @Override public void onPause() { resumed = false; closeCamera(); super.onPause(); }
    @Override public void onDestroy() {
        closeCamera();
        decodeQueue.shutdown();
        if (isFinishing()) publish(id, "-");
        synchronized (InvitationScanActivity.class) {
            if (visible.get() == this) visible.clear();
        }
        super.onDestroy();
    }

    @Override public void surfaceCreated(SurfaceHolder holder) { surfaceReady = true; tryOpen(); }
    @Override public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) { fitPreview(); }
    @Override public void surfaceDestroyed(SurfaceHolder holder) { surfaceReady = false; closeCamera(); }

    private void tryOpen() {
        if (!resumed || !surfaceReady || camera != null || !cameraAllowed() || !isPending(id)) return;
        try {
            Camera.CameraInfo info = new Camera.CameraInfo();
            int back = -1;
            for (int i = 0; i < Camera.getNumberOfCameras(); i++) {
                Camera.getCameraInfo(i, info);
                if (info.facing == Camera.CameraInfo.CAMERA_FACING_BACK) { back = i; break; }
            }
            if (back < 0) { complete("!Zadná kamera nie je dostupná."); return; }
            camera = Camera.open(back);
            Camera.Parameters settings = camera.getParameters();
            Camera.Size size = selectSize(settings.getSupportedPreviewSizes());
            settings.setPreviewSize(size.width, size.height);
            settings.setPreviewFormat(ImageFormat.NV21);
            List<String> focus = settings.getSupportedFocusModes();
            if (focus != null && focus.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))
                settings.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);
            camera.setParameters(settings);
            int display = getWindowManager().getDefaultDisplay().getRotation();
            int angle = (info.orientation - (display - Surface.ROTATION_0) * 90 + 360) % 360;
            camera.setDisplayOrientation(angle);
            layoutWidth = angle % 180 == 0 ? size.width : size.height;
            layoutHeight = angle % 180 == 0 ? size.height : size.width;
            fitPreview();
            camera.setPreviewDisplay(preview.getHolder());
            camera.setErrorCallback((error, source) -> {
                if (source == camera) complete("!Kamera prerušila skenovanie. Skús to znova.");
            });
            camera.setPreviewCallbackWithBuffer((bytes, source) -> {
                if (source != camera || bytes == null) return;
                decodeQueue.execute(() -> {
                    String decoded = decoder.decode(bytes, size.width, size.height);
                    main.post(() -> {
                        if (camera != source || !isPending(id)) return;
                        if (decoded != null) complete("+" + decoded);
                        else main.postDelayed(() -> { if (camera == source) source.addCallbackBuffer(bytes); }, 100);
                    });
                });
            });
            camera.addCallbackBuffer(new byte[size.width * size.height * 3 / 2]);
            camera.startPreview();
        } catch (IOException | RuntimeException failure) {
            closeCamera();
            if (++openAttempts < 5) main.postDelayed(this::tryOpen, 240);
            else complete("!Kameru sa nepodarilo otvoriť. Zavri aplikácie používajúce kameru a skús to znova.");
        }
    }

    private static Camera.Size selectSize(List<Camera.Size> sizes) {
        Camera.Size smallest = sizes.get(0), best = null;
        for (Camera.Size size : sizes) {
            int pixels = size.width * size.height;
            if (pixels < smallest.width * smallest.height) smallest = size;
            if (size.width <= 1280 && size.height <= 960 && (best == null || pixels > best.width * best.height)) best = size;
        }
        return best != null ? best : smallest;
    }

    private void fitPreview() {
        if (layoutWidth == 0 || frame == null || frame.getWidth() == 0 || frame.getHeight() == 0) return;
        float scale = Math.min(frame.getWidth() / (float) layoutWidth, frame.getHeight() / (float) layoutHeight);
        int width = Math.round(layoutWidth * scale), height = Math.round(layoutHeight * scale);
        if (preview.getLayoutParams().width != width || preview.getLayoutParams().height != height)
            preview.setLayoutParams(new FrameLayout.LayoutParams(width, height, Gravity.CENTER));
    }

    private void closeCamera() {
        main.removeCallbacksAndMessages(null);
        Camera old = camera;
        camera = null;
        if (old == null) return;
        try { old.setPreviewCallbackWithBuffer(null); old.stopPreview(); }
        catch (RuntimeException disconnected) { /* Android can revoke a disconnected camera. */ }
        finally { old.release(); }
    }

    private void complete(String outcome) {
        publish(id, outcome);
        finish();
    }
}
