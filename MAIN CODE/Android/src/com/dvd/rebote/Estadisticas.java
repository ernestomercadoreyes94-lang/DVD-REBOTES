package com.dvd.rebote;

import android.content.Context;
import android.content.SharedPreferences;

// Estadísticas guardadas (no se borran al cerrar): récord, esquinas en total, cuál toca más y tiempo rebotando.
// Con trucos que ayudan a tocar esquinas (LANZAR, SOLO, TURBO...) no cuentan: ver Cheats.trucoActivo().
// Se juntan en memoria y se guardan cada 10 segundos (con muchos DVDs hay muchas esquinas por segundo).
public class Estadisticas {
    static final String PREFS = "record";   // el récord ya se guardaba aquí
    static final String[] CLAVES = { "supIzq", "supDer", "infIzq", "infDer" };
    static final String[] NOMBRES = { "Arriba izquierda", "Arriba derecha", "Abajo izquierda", "Abajo derecha" };

    static long pendTotal, pendMs, ultimoGuardado;
    static final long[] pendEsq = new long[4];
    static int recordSesion;                // esquinas sin trucos de esta vez (para el récord)

    static SharedPreferences p(Context c) { return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE); }

    // ---- logros: 50 / 100 / 500 / 1000 esquinas y 1 / 2 / 5 horas rebotando (en total, sin trucos) ----
    static final long[] LOGROS_ESQUINAS = { 50, 100, 500, 1000, 2500, 5000, 10000 };
    static final int[] LOGROS_HORAS = { 1, 2, 5, 10, 24 };

    // nombres y puntos (mismo orden que arriba; igual que en la PC)
    static final String[] NOMBRES_ESQUINAS = {
        "10G: Primeras 50 esquinas", "20G: Cazador de esquinas (100)",
        "50G: Maestro de las esquinas (500)", "100G: Leyenda del DVD (1000)",
        "200G: Esquinero profesional (2500)", "300G: El DVD ya te conoce (5000)",
        "400G: Esquinero leyenda (10000)" };
    static final String[] NOMBRES_HORAS = {
        "20G: Una hora mirando el DVD", "40G: ¿No tienes nada que hacer? (2 h)",
        "100G: Tener mucho tiempo libre (5 h)", "150G: Esto ya es un trabajo (10 h)",
        "500G: Un día entero rebotando (24 h)" };
    // ---- LOGROS ESPECIALES (pasan una vez y se guardan; igual que en la PC) ----
    static final String[] ESPECIALES_ID = { "doble", "racha" };
    static final String[] NOMBRES_ESPECIALES = { "150G: Esquina doble", "100G: Racha de esquinas" };
    static final String[] DESC_ESPECIALES = { "Dos DVDs tocan esquina casi al mismo tiempo", "Toca 3 esquinas en menos de 1 minuto" };
    static final String[] DESC_ESPECIALES_EN = { "Two DVDs hit a corner almost at the same time", "Hit 3 corners in under 1 minute" };
    static final int TOTAL_LOGROS = 7 + 5 + 2;   // 14

    // ---- TROFEOS: bronce (1 logro), plata (4), oro (7) y diamante (todos: 14) ----
    static final String[] NOMBRES_TROFEOS = { "Pequeños pasos", "¿Cuánto tiempo tienes?", "¿En serio llegamos aquí?", "Bro consigue un trabajo" };
    static final String[] METALES = { "bronce", "plata", "oro", "diamante" };
    static final String[] CONDICION_TROFEOS = { "Consigue un logro", "Consigue 4 logros", "Consigue 7 logros", "Consigue todos los logros (14)" };
    static final int[] LOGROS_PARA_TROFEO = { 1, 4, 7, 14 };

    // ---- TIENDA: skins para el logo, se compran con los G de los logros (igual que en la PC) ----
    static final String[] SKINS_ID = { "bronce", "plata", "oro", "diamante", "neon", "fuego", "arcoiris", "holograma" };
    static final int[] SKINS_PRECIO = { 100, 200, 350, 800, 120, 160, 200, 210 };   // con todos los logros (2140 G) alcanza para todas

    static int puntosDe(String puntosYNombre) {
        try { return Integer.parseInt(puntosYNombre.substring(0, puntosYNombre.indexOf('G'))); } catch (Exception e) { return 0; }
    }
    static boolean tieneEspecial(String esp, String id) { return esp != null && !esp.isEmpty() && ("," + esp + ",").contains("," + id + ","); }
    static String especiales(Context c) { return p(c).getString("especiales", ""); }
    static int puntosG(long esquinas, long ms, String esp) {
        int g = 0;
        for (int i = 0; i < LOGROS_ESQUINAS.length; i++) if (esquinas >= LOGROS_ESQUINAS[i]) g += puntosDe(NOMBRES_ESQUINAS[i]);
        for (int i = 0; i < LOGROS_HORAS.length; i++) if (ms >= LOGROS_HORAS[i] * 3600_000L) g += puntosDe(NOMBRES_HORAS[i]);
        for (int i = 0; i < ESPECIALES_ID.length; i++) if (tieneEspecial(esp, ESPECIALES_ID[i])) g += puntosDe(NOMBRES_ESPECIALES[i]);
        return g;
    }
    static int saldoG(Context c) { return Math.max(0, puntosG(totalEsquinas(c), totalMs(c), especiales(c)) - p(c).getInt("gastadoG", 0)); }

    // gana un logro especial (k: 0 doble, 1 racha); sale el aviso y, si toca, el trofeo
    static synchronized void ganarEspecial(Context c, int k) {
        String esp = especiales(c), id = ESPECIALES_ID[k];
        if (tieneEspecial(esp, id)) return;
        long esq = totalEsquinas(c), ms = totalMs(c);
        int antes = ganados(esq, ms, esp);
        esp = esp.isEmpty() ? id : esp + "," + id;
        p(c).edit().putString("especiales", esp).apply();
        logro(NOMBRES_ESPECIALES[k]);
        revisarTrofeos(antes, ganados(esq, ms, esp));
    }
    static boolean tieneSkin(Context c, String s) {
        return s != null && !s.isEmpty() && ("," + p(c).getString("skins", "") + ",").contains("," + s + ",");
    }
    static synchronized boolean comprar(Context c, String s, int precio) {
        if (tieneSkin(c, s) || saldoG(c) < precio) return false;
        String tengo = p(c).getString("skins", "");
        p(c).edit().putInt("gastadoG", p(c).getInt("gastadoG", 0) + precio)
            .putString("skins", tengo.isEmpty() ? s : tengo + "," + s).apply();
        return true;
    }
    // la skin elegida (solo si la compraste); null = colores normales
    static String skinActiva(Context c) {
        String s = c.getSharedPreferences("ajustes", Context.MODE_PRIVATE).getString("skin", "");
        return tieneSkin(c, s) ? s : null;
    }
    static int colorSkin(String s) {
        if ("bronce".equals(s)) return android.graphics.Color.rgb(225, 140, 70);
        if ("plata".equals(s)) return android.graphics.Color.rgb(205, 215, 230);
        if ("oro".equals(s)) return android.graphics.Color.rgb(255, 205, 60);
        if ("diamante".equals(s)) return android.graphics.Color.rgb(130, 235, 255);
        if ("neon".equals(s)) return android.graphics.Color.rgb(255, 70, 210);
        if ("fuego".equals(s)) return android.graphics.Color.rgb(255, 130, 40);
        if ("arcoiris".equals(s)) return android.graphics.Color.rgb(120, 200, 255);
        if ("holograma".equals(s)) return android.graphics.Color.rgb(190, 160, 255);
        return android.graphics.Color.WHITE;
    }

    // ruido fijo (siempre da lo mismo para el mismo número): metal cepillado y caras del diamante
    static double ruido(int n) {
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647.0;
    }
    // los 4 tonos de cada metal: sombra profunda, sombra, medio y luz
    static int[][] tonosSkin(String s) {
        if ("bronce".equals(s)) return new int[][] { { 58, 24, 8 }, { 132, 62, 24 }, { 208, 122, 60 }, { 255, 212, 160 } };
        if ("plata".equals(s)) return new int[][] { { 40, 45, 56 }, { 108, 116, 132 }, { 192, 200, 214 }, { 255, 255, 255 } };
        if ("oro".equals(s)) return new int[][] { { 88, 46, 0 }, { 172, 108, 8 }, { 242, 190, 48 }, { 255, 249, 196 } };
        if ("diamante".equals(s)) return new int[][] { { 8, 52, 112 }, { 36, 136, 210 }, { 150, 226, 255 }, { 248, 255, 255 } };
        return null;
    }

    // pinta el logo (lo que no es transparente) de METAL de verdad (igual que DvdConfig.Metalizar en la PC):
    // relieve en los bordes, reflejo tipo cromo, metal cepillado (o caras talladas con arcoíris en el diamante),
    // franjas de brillo y destellos en el diamante. Se hace una vez y queda guardado.
    static void metalizar(android.graphics.Bitmap b, String s) {
        int[][] tono = tonosSkin(s);
        boolean especial = esSkinEspecial(s);
        if (tono == null && !especial) return;
        boolean diamante = "diamante".equals(s);
        int w = b.getWidth(), h = b.getHeight();
        int[] px = new int[w * h];
        b.getPixels(px, 0, w, 0, 0, w, h);
        int[] alfa = new int[w * h];
        for (int i = 0; i < px.length; i++) alfa[i] = px[i] >>> 24;
        int rb = Math.max(1, Math.round(h * 0.035f));   // ancho del relieve
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                int i = y * w + x, a = alfa[i];
                if (a == 0) continue;
                double fy = (double) y / Math.max(1, h - 1), fx = (double) x / Math.max(1, w - 1);
                double rel = relieve(alfa, w, h, x, y, rb);
                double[] c = especial ? tonoEspecial(s, fx, fy, x, y, rel) : tonoMetal(tono, diamante, fx, fy, x, y, rel);
                px[i] = (a << 24) | ((int) c[0] << 16) | ((int) c[1] << 8) | (int) c[2];
            }
        b.setPixels(px, 0, w, 0, 0, w, h);
        if (!diamante && !"holograma".equals(s)) return;   // destellos: diamante y holograma
        android.graphics.Canvas c = new android.graphics.Canvas(b);
        android.graphics.Paint bl = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        bl.setColor(android.graphics.Color.argb(240, 255, 255, 255));
        android.graphics.Paint halo = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        halo.setColor(android.graphics.Color.argb(70, 200, 245, 255));
        double[][] pos = { { 0.16, 0.26 }, { 0.47, 0.18 }, { 0.82, 0.32 }, { 0.3, 0.7 }, { 0.68, 0.76 }, { 0.6, 0.42 }, { 0.1, 0.6 } };
        float[] tam = { 0.1f, 0.065f, 0.09f, 0.06f, 0.08f, 0.045f, 0.04f };
        for (int k = 0; k < pos.length; k++) {
            float cx = (float) (pos[k][0] * w), cy = (float) (pos[k][1] * h), r = Math.max(3f, h * tam[k]);
            c.drawCircle(cx, cy, r * 0.45f, halo);
            android.graphics.Path pt = new android.graphics.Path();
            for (int j = 0; j < 8; j++) {
                double an = j * Math.PI / 4; float rr = j % 2 == 0 ? r : r * 0.22f;
                float xx = cx + (float) (Math.cos(an) * rr), yy = cy + (float) (Math.sin(an) * rr);
                if (j == 0) pt.moveTo(xx, yy); else pt.lineTo(xx, yy);
            }
            pt.close();
            c.drawPath(pt, bl);
        }
    }

    // ---- SKINS ESPECIALES (no son metal): neón, fuego, arcoíris y holograma (igual que DvdConfig.TonoEspecial en la PC) ----
    static boolean esSkinEspecial(String s) { return "neon".equals(s) || "fuego".equals(s) || "arcoiris".equals(s) || "holograma".equals(s); }

    static double[] hsv(double h, double s, double v) {
        h = (h % 1 + 1) % 1 * 6; int i = (int) h; double f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        double r, g, b;
        switch (i) { case 0: r = v; g = t; b = p; break; case 1: r = q; g = v; b = p; break; case 2: r = p; g = v; b = t; break;
                     case 3: r = p; g = q; b = v; break; case 4: r = t; g = p; b = v; break; default: r = v; g = p; b = q; break; }
        return new double[] { r * 255, g * 255, b * 255 };
    }

    static double[] tramos(double pos, double[] puntos, int[][] colores) {
        pos = Math.max(0, Math.min(1, pos));
        int k = 0;
        while (k < puntos.length - 2 && pos > puntos[k + 1]) k++;
        double f = (pos - puntos[k]) / Math.max(1e-6, puntos[k + 1] - puntos[k]);
        return new double[] { colores[k][0] + (colores[k + 1][0] - colores[k][0]) * f, colores[k][1] + (colores[k + 1][1] - colores[k][1]) * f, colores[k][2] + (colores[k + 1][2] - colores[k][2]) * f };
    }

    static double[] tonoEspecial(String skin, double fx, double fy, int x, int y, double relieve) {
        double R, G, B;
        if ("neon".equals(skin)) {
            double borde = Math.min(1, Math.abs(relieve) * 1.8);
            double[] luz = relieve >= 0 ? new double[] { 255, 70, 210 } : new double[] { 60, 230, 255 };
            R = 45 + (luz[0] - 45) * borde; G = 8 + (luz[1] - 8) * borde; B = 70 + (luz[2] - 70) * borde;
            if (borde > 0.75) { double k = (borde - 0.75) / 0.25 * 0.8; R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; }
            double raya = 12 * Math.sin(fy * Math.PI * 14);
            R += raya; B += raya;
        } else if ("fuego".equals(skin)) {
            double llama = fy + 0.2 * Math.sin(fx * Math.PI * 9 + Math.sin(fx * 23) * 1.5) * (1 - fy)
                         + 0.07 * Math.sin(fx * 41 + fy * 13) + 0.05 * Math.sin(fx * 67 - fy * 29);
            double[] c = tramos(llama, new double[] { 0.0, 0.35, 0.62, 0.84, 1.0 },
                new int[][] { { 90, 5, 0 }, { 200, 30, 5 }, { 255, 120, 10 }, { 255, 200, 40 }, { 255, 250, 190 } });
            R = c[0]; G = c[1]; B = c[2];
            double k = relieve * 0.4;
            if (k > 0) { R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; } else { R *= 1 + k; G *= 1 + k; B *= 1 + k; }
        } else if ("arcoiris".equals(skin)) {
            double[] c = hsv(fx * 0.9 + fy * 0.25, 0.85, 1.0);
            double luz = 0.78 + relieve * 0.35 + (fy < 0.5 ? (0.5 - fy) * 0.35 : -(fy - 0.5) * 0.25);
            R = c[0] * luz; G = c[1] * luz; B = c[2] * luz;
        } else {
            double[] c = hsv(fx * 0.6 + fy * 0.4 + relieve * 0.35 + 0.08 * Math.sin(fy * 40), 0.45, 1.0);
            double raya = (y % 4 < 2) ? 1.0 : 0.8;
            R = c[0] * raya; G = c[1] * raya; B = c[2] * raya;
            if (relieve > 0) { double k = relieve * 0.5; R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; }
        }
        double d1 = Math.abs(((fx + fy * 0.55) % 1.0) - 0.3);
        if (d1 < 0.05) { double k = (1 - d1 / 0.05) * ("neon".equals(skin) ? 0.25 : 0.55); R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; }
        return new double[] { Math.max(0, Math.min(255, R)), Math.max(0, Math.min(255, G)), Math.max(0, Math.min(255, B)) };
    }

    // relieve (-1..1): + en los bordes que miran a la luz (arriba a la izquierda), - en los de abajo a la derecha
    static double relieve(int[] alfa, int w, int h, int x, int y, int r) {
        double izq = x - r >= 0 ? alfa[y * w + x - r] : 0, der = x + r < w ? alfa[y * w + x + r] : 0;
        double arr = y - r >= 0 ? alfa[(y - r) * w + x] : 0, aba = y + r < h ? alfa[(y + r) * w + x] : 0;
        return ((der - izq) * 0.6 + (aba - arr) * 0.8) / 255.0;
    }

    // el color de metal de un punto (R, G, B). Igual que DvdConfig.TonoMetal en la PC.
    static double[] tonoMetal(int[][] tono, boolean diamante, double fx, double fy, int x, int y, double relieve) {
        double v;
        if (fy < 0.52) v = 0.92 - fy * 0.75;
        else v = 0.18 + (fy - 0.52) * 0.95;
        v += 0.06 * Math.sin(fy * Math.PI * 9 + fx * 3);
        if (diamante) {
            double u = fx * 7, t = fy * 3.5;
            int cx = (int) Math.floor(u), cy = (int) Math.floor(t);
            int cara = (u - cx) > (t - cy) ? 1 : 0;
            double brillo = ruido(cx * 73 + cy * 151 + cara * 29 + 7);
            v = v * 0.45 + (0.25 + brillo * 0.75) * 0.55;
        } else v += (ruido(y * 131 + (x / 24) * 7) - 0.5) * 0.07 + (ruido(y * 977 + x / 3) - 0.5) * 0.035;
        v += relieve * 0.42;
        v = Math.max(0, Math.min(1, v));
        double pos = v * 3; int k = Math.min(2, (int) pos); double f = pos - k;
        double R = tono[k][0] + (tono[k + 1][0] - tono[k][0]) * f;
        double G = tono[k][1] + (tono[k + 1][1] - tono[k][1]) * f;
        double B = tono[k][2] + (tono[k + 1][2] - tono[k][2]) * f;
        if (diamante) {
            double fase = (fx * 2.2 + fy * 1.3) * Math.PI * 2;
            R += 22 * Math.sin(fase); G += 22 * Math.sin(fase + 2.09); B += 22 * Math.sin(fase + 4.19);
        }
        double d1 = Math.abs(((fx + fy * 0.55) % 1.0) - 0.3), d2 = Math.abs(((fx + fy * 0.55) % 1.0) - 0.42);
        double k2 = 0;
        if (d1 < 0.06) k2 = Math.max(k2, (1 - d1 / 0.06) * 0.7);
        if (d2 < 0.025) k2 = Math.max(k2, (1 - d2 / 0.025) * 0.45);
        if (relieve > 0.5) k2 = Math.max(k2, (relieve - 0.5) * 1.2);
        R += (255 - R) * k2; G += (255 - G) * k2; B += (255 - B) * k2;
        return new double[] { Math.max(0, Math.min(255, R)), Math.max(0, Math.min(255, G)), Math.max(0, Math.min(255, B)) };
    }

    static int ganados(long esquinas, long ms, String esp) {
        int n = 0;
        for (long m : LOGROS_ESQUINAS) if (esquinas >= m) n++;
        for (int h : LOGROS_HORAS) if (ms >= h * 3600_000L) n++;
        for (String id : ESPECIALES_ID) if (tieneEspecial(esp, id)) n++;
        return n;
    }
    static int ganados(Context c) { return ganados(totalEsquinas(c), totalMs(c), especiales(c)); }

    static void logro(String puntosYNombre) {
        Cheats.logroTrofeo = 0;
        Cheats.logroTexto = puntosYNombre;
        Cheats.logroNanos = System.nanoTime();
    }

    // trofeo n (1 bronce, 2 plata, 3 oro); con espera, sale después del aviso del logro
    static void trofeo(int n, boolean conEspera) {
        if (conEspera) { Cheats.trofeoPendiente = n; Cheats.trofeoDesde = System.nanoTime() + 5_400_000_000L; return; }
        Cheats.logroTrofeo = n;
        Cheats.logroTexto = NOMBRES_TROFEOS[n - 1];
        Cheats.logroNanos = System.nanoTime();
    }

    static void revisarTrofeos(int antes, int ahora) {
        for (int t = LOGROS_PARA_TROFEO.length - 1; t >= 0; t--)
            if (antes < LOGROS_PARA_TROFEO[t] && ahora >= LOGROS_PARA_TROFEO[t]) { trofeo(t + 1, true); return; }
    }

    static synchronized long totalEsquinas(Context c) { return p(c).getLong("total", 0) + pendTotal; }
    static synchronized long totalMs(Context c) { return p(c).getLong("ms", 0) + pendMs; }

    // v: 1 = arriba, h: 1 = izquierda (igual que en DvdView)
    static synchronized void esquina(Context c, int v, int h, int sesion) {
        long antes = p(c).getLong("total", 0) + pendTotal;
        long ms = p(c).getLong("ms", 0) + pendMs;
        pendTotal++;
        pendEsq[(v == 1 ? 0 : 2) + (h == 1 ? 0 : 1)]++;
        recordSesion = Math.max(recordSesion, sesion);
        for (int i = 0; i < LOGROS_ESQUINAS.length; i++)
            if (antes < LOGROS_ESQUINAS[i] && antes + 1 >= LOGROS_ESQUINAS[i]) logro(NOMBRES_ESQUINAS[i]);
        String esp = especiales(c);
        revisarTrofeos(ganados(antes, ms, esp), ganados(antes + 1, ms, esp));
        guardarSiToca(c);
    }

    static synchronized void tiempo(Context c, float dt) {
        long antes = p(c).getLong("ms", 0) + pendMs;
        pendMs += (long) (dt * 1000);
        long despues = antes + (long) (dt * 1000);
        for (int i = 0; i < LOGROS_HORAS.length; i++) {
            long ms = LOGROS_HORAS[i] * 3600_000L;
            if (antes < ms && despues >= ms) logro(NOMBRES_HORAS[i]);
        }
        long esq = p(c).getLong("total", 0) + pendTotal;
        String esp = especiales(c);
        revisarTrofeos(ganados(esq, antes, esp), ganados(esq, despues, esp));
        guardarSiToca(c);
    }

    static void guardarSiToca(Context c) {
        if (System.currentTimeMillis() - ultimoGuardado > 10000) guardar(c);
    }

    static synchronized void guardar(Context c) {
        ultimoGuardado = System.currentTimeMillis();
        try {
            SharedPreferences sp = p(c);
            SharedPreferences.Editor e = sp.edit();
            e.putLong("total", sp.getLong("total", 0) + pendTotal);
            for (int i = 0; i < 4; i++) e.putLong(CLAVES[i], sp.getLong(CLAVES[i], 0) + pendEsq[i]);
            e.putLong("ms", sp.getLong("ms", 0) + pendMs);
            if (recordSesion > sp.getInt("record", 0)) e.putInt("record", recordSesion).putLong("fecha", System.currentTimeMillis());
            e.apply();
            pendTotal = 0; pendMs = 0;
            for (int i = 0; i < 4; i++) pendEsq[i] = 0;
        } catch (Exception ignored) { }
        WidgetDvd.actualizar(c);   // el widget de la pantalla de inicio enseña los números nuevos
    }

    static synchronized int record(Context c) { return Math.max(p(c).getInt("record", 0), recordSesion); }

    // texto para la pantalla de ajustes (incluye lo que todavía no se guarda)
    static synchronized String texto(Context c) {
        SharedPreferences sp = p(c);
        long total = sp.getLong("total", 0) + pendTotal;
        int mas = -1; long max = 0;
        for (int i = 0; i < 4; i++) {
            long n = sp.getLong(CLAVES[i], 0) + pendEsq[i];
            if (n > max) { max = n; mas = i; }
        }
        long ms = sp.getLong("ms", 0) + pendMs;
        long seg = ms / 1000;
        boolean en = IdiomaCel.ingles;
        StringBuilder logros = new StringBuilder(en ? "Achievements:" : "Logros:");
        for (long n : LOGROS_ESQUINAS) logros.append(total >= n ? "  ★ " : "  ☆ ").append(n);
        logros.append("  ·");
        for (int hh : LOGROS_HORAS) logros.append(ms >= hh * 3600_000L ? "  ★ " : "  ☆ ").append(hh).append(" h");
        String t = seg >= 3600 ? (seg / 3600) + " h " + (seg % 3600 / 60) + " min"
                 : seg >= 60 ? (seg / 60) + " min" : seg + " s";
        if (en) {
            String[] nom = { "Top left", "Top right", "Bottom left", "Bottom right" };
            return "Total corners: " + total
                + (mas >= 0 ? "   ·   Most hit: " + nom[mas] + " (" + max + ")" : "")
                + "\nTime bouncing: " + t
                + "\n" + logros
                + "\n(cheats like LANZAR, SOLO or TURBO don't count)";
        }
        return "Esquinas en total: " + total
            + (mas >= 0 ? "   ·   La que más: " + NOMBRES[mas] + " (" + max + ")" : "")
            + "\nTiempo rebotando: " + t
            + "\n" + logros
            + "\n(con trucos como LANZAR, SOLO o TURBO no cuentan)";
    }
}
