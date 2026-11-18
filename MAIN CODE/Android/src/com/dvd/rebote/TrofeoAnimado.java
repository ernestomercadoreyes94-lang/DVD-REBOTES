package com.dvd.rebote;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorMatrix;
import android.graphics.ColorMatrixColorFilter;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Shader;
import android.view.View;

import java.util.Random;

// Trofeo de la pantalla "Logros". Si ya lo tienes: rayos de luz que giran detrás, brillitos que
// parpadean, un destello que pasa cada 3 s y confeti al abrir. Si no: en gris y medio transparente.
public class TrofeoAnimado extends View {
    final Bitmap bmp;
    final boolean tiene;
    final int color, indice;
    final long inicio = System.nanoTime();
    final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    final float d;
    // confeti
    final int n;
    final float[] x, y, vx, vy, rot, vr, tam, fase;
    final int[] col; final boolean[] est;
    long ultimo;
    boolean lanzado;

    public TrofeoAnimado(Context c, int resId, boolean tiene, int color, int indice) {
        super(c);
        this.bmp = BitmapFactory.decodeResource(c.getResources(), resId);
        this.tiene = tiene; this.color = color; this.indice = indice;
        this.d = c.getResources().getDisplayMetrics().density;
        n = tiene ? 26 + indice * 10 : 0;
        x = new float[n]; y = new float[n]; vx = new float[n]; vy = new float[n]; rot = new float[n]; vr = new float[n];
        tam = new float[n]; fase = new float[n]; col = new int[n]; est = new boolean[n];
        if (!tiene) {
            ColorMatrix cm = new ColorMatrix(); cm.setSaturation(0);
            p.setColorFilter(new ColorMatrixColorFilter(cm));
        }
    }

    void lanzar() {
        lanzado = true;
        Random r = new Random();
        int[] cols = { color, Color.WHITE, indice == 3 ? Color.rgb(190, 140, 255) : indice == 2 ? Color.rgb(0, 191, 255) : Color.rgb(255, 235, 170) };
        for (int i = 0; i < n; i++) {
            double ang = -Math.PI / 2 + (r.nextDouble() - 0.5) * Math.PI * 1.3, vel = (120 + r.nextDouble() * 220) * d;
            x[i] = getWidth() / 2f; y[i] = getHeight() / 2f;
            vx[i] = (float) (Math.cos(ang) * vel); vy[i] = (float) (Math.sin(ang) * vel);
            rot[i] = r.nextFloat() * 360; vr[i] = (r.nextFloat() - 0.5f) * 720;
            tam[i] = (3 + r.nextFloat() * 3) * d; fase[i] = r.nextFloat() * 6.28f;
            col[i] = cols[r.nextInt(cols.length)]; est[i] = r.nextFloat() < 0.25f;
        }
        ultimo = System.nanoTime();
    }

    static void estrella(Canvas c, float x, float y, float r, Paint p) {
        Path s = new Path();
        for (int i = 0; i < 8; i++) {
            double ang = i * Math.PI / 4;
            float rr = i % 2 == 0 ? r : r * 0.28f;
            float px = x + (float) (Math.cos(ang) * rr), py = y + (float) (Math.sin(ang) * rr);
            if (i == 0) s.moveTo(px, py); else s.lineTo(px, py);
        }
        s.close();
        c.drawPath(s, p);
    }

    @Override protected void onDraw(Canvas c) {
        float w = getWidth(), h = getHeight(), cx = w / 2f, cy = h / 2f;
        float t = (System.nanoTime() - inicio) / 1e9f;
        Paint e = new Paint(Paint.ANTI_ALIAS_FLAG);
        if (tiene) {
            // rayos de luz que giran
            int nr = indice == 3 ? 20 : indice == 2 ? 16 : 12;
            e.setColor(color); e.setAlpha(indice >= 2 ? 60 : 45);
            Path rayo = new Path(); rayo.moveTo(0, 0); rayo.lineTo(w, -6 * d); rayo.lineTo(w, 6 * d); rayo.close();
            for (int i = 0; i < nr; i++) {
                c.save(); c.translate(cx, cy); c.rotate(t * 30 * (indice % 2 == 0 ? 1 : -1) + i * 360f / nr);
                c.drawPath(rayo, e); c.restore();
            }
            // brillitos
            float[][] pos = { { -.36f, -.3f }, { .34f, -.18f }, { -.26f, .3f }, { .4f, .34f }, { 0f, -.42f } };
            for (int i = 0; i < pos.length; i++) {
                float br = 0.5f + 0.5f * (float) Math.sin(t * 5 + i * 1.9f + indice);
                e.setColor(i % 2 == 0 ? Color.WHITE : color); e.setAlpha((int) (230 * br));
                estrella(c, cx + pos[i][0] * w, cy + pos[i][1] * h, (1.5f + br * 3) * d, e);
            }
        }
        // el trofeo
        float s = Math.min(w, h) * 0.86f;
        p.setAlpha(tiene ? 255 : 90);
        if (bmp != null) c.drawBitmap(bmp, null, new RectF(cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2), p);
        if (tiene) {
            // destello diagonal que pasa cada 3 s
            float ciclo = ((t + indice * 0.6f) % 3f) / 0.9f;
            if (ciclo < 1) {
                float bx = -30 * d + ciclo * (w + 60 * d);
                c.save(); c.rotate(20, bx, cy);
                e.setShader(new LinearGradient(bx - 14 * d, 0, bx + 14 * d, 0,
                    new int[] { Color.argb(0, 255, 255, 255), Color.argb(70, 255, 255, 255), Color.argb(0, 255, 255, 255) }, null, Shader.TileMode.CLAMP));
                e.setAlpha(255);
                c.drawRect(bx - 14 * d, -h, bx + 14 * d, 2 * h, e);
                e.setShader(null);
                c.restore();
            }
            // confeti al abrir
            if (!lanzado && t > 0.2f && w > 0) lanzar();
            if (lanzado && t < 3.4f) {
                long ahora = System.nanoTime();
                float dt = Math.min(0.05f, (ahora - ultimo) / 1e9f); ultimo = ahora;
                float alfa = Math.max(0f, Math.min(1f, (3.4f - t) / 0.8f));
                for (int i = 0; i < n; i++) {
                    vy[i] += 420 * d * dt; vx[i] *= (float) Math.pow(0.35, dt); vy[i] *= (float) Math.pow(0.6, dt);
                    x[i] += vx[i] * dt; y[i] += vy[i] * dt; rot[i] += vr[i] * dt;
                    e.setColor(col[i]);
                    if (est[i]) {
                        float br = 0.5f + 0.5f * (float) Math.sin(t * 14 + fase[i]);
                        e.setAlpha((int) (255 * alfa * (0.4f + 0.6f * br)));
                        estrella(c, x[i], y[i], tam[i] * (0.8f + br * 0.8f), e);
                    } else {
                        float esc = Math.abs((float) Math.cos(Math.toRadians(rot[i] + fase[i] * 40)));
                        e.setAlpha((int) (255 * alfa));
                        c.save(); c.translate(x[i], y[i]); c.rotate(rot[i]);
                        float hh = tam[i] * 0.3f * (0.25f + esc);
                        c.drawRect(-tam[i] / 2, -hh, tam[i] / 2, hh, e);
                        c.restore();
                    }
                }
            }
            postInvalidateOnAnimation();
        }
    }
}
