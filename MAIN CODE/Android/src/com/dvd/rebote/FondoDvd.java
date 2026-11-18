package com.dvd.rebote;

import android.graphics.Canvas;
import android.graphics.Color;
import android.os.Handler;
import android.os.Looper;
import android.service.wallpaper.WallpaperService;
import android.view.MotionEvent;
import android.view.SurfaceHolder;

// Fondo de pantalla animado: el DVD rebotando como fondo del cel, sobre negro.
// Usa el mismo dibujo y la misma configuración que los DVDs de la app (velocidad, color, cantidad,
// arcoíris en las esquinas, contador...). Solo se dibuja mientras el fondo está a la vista.
public class FondoDvd extends WallpaperService {

    @Override
    public Engine onCreateEngine() { return new Motor(); }

    class Motor extends Engine {
        final Handler handler = new Handler(Looper.getMainLooper());
        DvdView dvd;
        boolean visible;
        int ancho, alto;

        final Runnable cuadro = new Runnable() {
            @Override public void run() { dibujar(); }
        };

        @Override
        public void onCreate(SurfaceHolder holder) {
            super.onCreate(holder);
            Config.load(FondoDvd.this);
            dvd = new DvdView(FondoDvd.this);
            setTouchEventsEnabled(true);   // para los códigos que usan el dedo (toca, lanzar)
        }

        @Override
        public void onSurfaceChanged(SurfaceHolder holder, int format, int w, int h) {
            super.onSurfaceChanged(holder, format, w, h);
            ancho = w; alto = h;
            // la vista de los DVDs no está en pantalla: se le da el tamaño a mano
            dvd.layout(0, 0, w, h);
            dibujar();
        }

        @Override
        public void onVisibilityChanged(boolean v) {
            visible = v;
            handler.removeCallbacks(cuadro);
            if (v) { dvd.lastNanos = 0; dibujar(); }   // al volver no "salta": empieza a contar desde ahora
        }

        @Override
        public void onSurfaceDestroyed(SurfaceHolder holder) {
            visible = false;
            handler.removeCallbacks(cuadro);
            super.onSurfaceDestroyed(holder);
        }

        @Override
        public void onDestroy() {
            handler.removeCallbacks(cuadro);
            if (dvd != null) dvd.release();
            super.onDestroy();
        }

        @Override
        public void onTouchEvent(MotionEvent e) {
            if (dvd != null && (Cheats.toca || Cheats.lanzar)) dvd.onTouchEvent(e);
        }

        void dibujar() {
            if (ancho <= 0 || alto <= 0) return;
            SurfaceHolder h = getSurfaceHolder();
            Canvas c = null;
            try {
                c = h.lockHardwareCanvas();
                if (c != null) {
                    c.drawColor(Color.BLACK);
                    dvd.draw(c);
                }
            } catch (Exception ignored) {
            } finally {
                if (c != null) try { h.unlockCanvasAndPost(c); } catch (Exception ignored) { }
            }
            handler.removeCallbacks(cuadro);
            if (visible) handler.postDelayed(cuadro, 16);   // unos 60 cuadros por segundo
        }
    }
}
