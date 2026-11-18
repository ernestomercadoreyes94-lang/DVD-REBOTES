package com.dvd.rebote;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.graphics.PixelFormat;
import android.os.Build;
import android.os.IBinder;
import android.provider.Settings;
import android.view.Gravity;
import android.view.WindowManager;

// Servicio que muestra los DVDs encima de todas las apps
public class DvdService extends Service {
    static final String CANAL = "dvd_rebote";
    static final String ACCION_DETENER = "com.dvd.rebote.DETENER";
    static volatile boolean corriendo = false;

    WindowManager wm;
    DvdView view;
    WindowManager.LayoutParams params;

    @Override
    public IBinder onBind(Intent intent) { return null; }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACCION_DETENER.equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }
        startAsForeground();
        if (view == null) {
            if (!Settings.canDrawOverlays(this)) { stopSelf(); return START_NOT_STICKY; }
            Config.load(this);
            addOverlay();
        }
        corriendo = true; IdiomaCel.cargar(this); WidgetDvd.actualizar(this);   // el botón del widget dice "Cerrar DVD"
        return START_STICKY;
    }

    void startAsForeground() {
        NotificationManager nm = getSystemService(NotificationManager.class);
        if (nm.getNotificationChannel(CANAL) == null) {
            NotificationChannel ch = new NotificationChannel(CANAL, "DVD Rebote", NotificationManager.IMPORTANCE_LOW);
            ch.setDescription("Mientras los DVDs están en pantalla");
            nm.createNotificationChannel(ch);
        }
        PendingIntent abrir = PendingIntent.getActivity(this, 0,
            new Intent(this, MainActivity.class), PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        PendingIntent detener = PendingIntent.getService(this, 1,
            new Intent(this, DvdService.class).setAction(ACCION_DETENER), PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        // (la notificación ya no tiene el botón "Cheats": solo "Detener")
        Notification n = new Notification.Builder(this, CANAL)
            .setSmallIcon(R.drawable.ic_notif)
            .setContentTitle("DVD Rebote")
            .setContentText("Los DVDs están rebotando. Toca para ajustar.")
            .setContentIntent(abrir)
            .addAction(new Notification.Action.Builder(null, "Detener", detener).build())
            .setOngoing(true)
            .build();
        if (Build.VERSION.SDK_INT >= 34) startForeground(1, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(1, n);
    }

    void addOverlay() {
        wm = getSystemService(WindowManager.class);
        view = new DvdView(this);
        params = new WindowManager.LayoutParams(
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            // no recibe toques ni teclado: todo pasa a la app de abajo
            WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE
                | WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN
                | WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
            PixelFormat.TRANSLUCENT);
        params.gravity = Gravity.TOP | Gravity.START;
        // Android 12+ solo deja pasar los toques si la ventana tiene 80% de opacidad o menos
        params.alpha = Math.min(0.8f, Config.actual.opacidad / 100f);
        if (Build.VERSION.SDK_INT >= 28)
            params.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        view.setOpacityListener(alpha -> {
            if (wm != null && view != null && params.alpha != alpha) {
                params.alpha = alpha;
                try { wm.updateViewLayout(view, params); } catch (Exception ignored) { }
            }
        });
        // cheat LANZAR: mientras está activo la capa recibe toques (para agarrar los DVDs);
        // al quitarlo vuelve a dejar pasar todos los toques a la app de abajo
        view.setTocableListener(tocable -> {
            if (wm == null || view == null) return;
            if (tocable) params.flags &= ~WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;
            else params.flags |= WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;
            try { wm.updateViewLayout(view, params); } catch (Exception ignored) { }
        });
        wm.addView(view, params);
    }


    @Override
    public void onDestroy() {
        corriendo = false; WidgetDvd.actualizar(this);   // el botón del widget vuelve a "Abrir DVD"
        // al detener el DVD también se quitan las ventanas del código secret (Run, NO ESCAPE y los Error)
        try { stopService(new Intent(this, ServicioSecret.class)); } catch (Exception ignored) { }
        if (view != null && wm != null) {
            try { wm.removeView(view); } catch (Exception ignored) { }
            view.release();
        }
        view = null;
        super.onDestroy();
    }
}
