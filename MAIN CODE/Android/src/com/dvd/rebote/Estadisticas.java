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
    static final long[] LOGROS_ESQUINAS = { 50, 100, 500, 1000, 2500, 5000 };
    static final int[] LOGROS_HORAS = { 1, 2, 5, 10, 24 };

    // nombres y puntos (mismo orden que arriba; igual que en la PC)
    static final String[] NOMBRES_ESQUINAS = {
        "10G: Primeras 50 esquinas", "20G: Cazador de esquinas (100)",
        "50G: Maestro de las esquinas (500)", "100G: Leyenda del DVD (1000)",
        "200G: Esquinero profesional (2500)", "300G: El DVD ya te conoce (5000)" };
    static final String[] NOMBRES_HORAS = {
        "20G: Una hora mirando el DVD", "40G: ¿No tienes nada que hacer? (2 h)",
        "100G: Tener mucho tiempo libre (5 h)", "150G: Esto ya es un trabajo (10 h)",
        "500G: Un día entero rebotando (24 h)" };

    // ---- TROFEOS: bronce (1 logro), plata (4), oro (7) y diamante (todos: 11) ----
    static final String[] NOMBRES_TROFEOS = { "Pequeños pasos", "¿Cuánto tiempo tienes?", "¿En serio llegamos aquí?", "Bro consigue un trabajo" };
    static final String[] METALES = { "bronce", "plata", "oro", "diamante" };
    static final String[] CONDICION_TROFEOS = { "Consigue un logro", "Consigue 4 logros", "Consigue 7 logros", "Consigue todos los logros (11)" };
    static final int[] LOGROS_PARA_TROFEO = { 1, 4, 7, 11 };

    // ---- TIENDA: skins para el logo, se compran con los G de los logros (igual que en la PC) ----
    static final String[] SKINS_ID = { "bronce", "plata", "oro", "diamante" };
    static final int[] SKINS_PRECIO = { 100, 200, 350, 800 };   // con todos los logros (1490 G) alcanza para todas

    static int puntosDe(String puntosYNombre) {
        try { return Integer.parseInt(puntosYNombre.substring(0, puntosYNombre.indexOf('G'))); } catch (Exception e) { return 0; }
    }
    static int puntosG(long esquinas, long ms) {
        int g = 0;
        for (int i = 0; i < LOGROS_ESQUINAS.length; i++) if (esquinas >= LOGROS_ESQUINAS[i]) g += puntosDe(NOMBRES_ESQUINAS[i]);
        for (int i = 0; i < LOGROS_HORAS.length; i++) if (ms >= LOGROS_HORAS[i] * 3600_000L) g += puntosDe(NOMBRES_HORAS[i]);
        return g;
    }
    static int saldoG(Context c) { return Math.max(0, puntosG(totalEsquinas(c), totalMs(c)) - p(c).getInt("gastadoG", 0)); }
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
        return android.graphics.Color.WHITE;
    }

    // pinta el logo (lo que no es transparente) de METAL: degradado con reflejos y una franja de brillo;
    // el diamante además lleva destellos. Se hace una vez y queda guardado.
    static void metalizar(android.graphics.Bitmap b, String s) {
        int[] os, cl;
        if ("bronce".equals(s)) { os = new int[] { 110, 52, 20 }; cl = new int[] { 250, 185, 125 }; }
        else if ("plata".equals(s)) { os = new int[] { 92, 98, 112 }; cl = new int[] { 250, 252, 255 }; }
        else if ("oro".equals(s)) { os = new int[] { 150, 98, 8 }; cl = new int[] { 255, 240, 150 }; }
        else if ("diamante".equals(s)) { os = new int[] { 30, 120, 190 }; cl = new int[] { 235, 252, 255 }; }
        else return;
        int w = b.getWidth(), h = b.getHeight();
        int[] px = new int[w * h];
        b.getPixels(px, 0, w, 0, 0, w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) {
                int i = y * w + x, a = px[i] >>> 24;
                if (a == 0) continue;
                double fy = (double) y / Math.max(1, h - 1), fx = (double) x / Math.max(1, w - 1);
                double v = 0.5 + 0.32 * Math.sin(fy * Math.PI * 2.3 + 0.5) + 0.18 * Math.sin(fy * Math.PI * 7 + fx * 2);
                v = Math.max(0, Math.min(1, v));
                double R = os[0] + (cl[0] - os[0]) * v, G = os[1] + (cl[1] - os[1]) * v, B = os[2] + (cl[2] - os[2]) * v;
                double d = Math.abs(((fx + fy * 0.55) % 1.0) - 0.32);
                if (d < 0.07) { double k = (1 - d / 0.07) * 0.75; R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; }
                px[i] = (a << 24) | ((int) Math.min(255, R) << 16) | ((int) Math.min(255, G) << 8) | (int) Math.min(255, B);
            }
        b.setPixels(px, 0, w, 0, 0, w, h);
        if (!"diamante".equals(s)) return;
        android.graphics.Canvas c = new android.graphics.Canvas(b);
        android.graphics.Paint bl = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        bl.setColor(android.graphics.Color.argb(235, 255, 255, 255));
        double[][] pos = { { 0.18, 0.28 }, { 0.47, 0.2 }, { 0.8, 0.35 }, { 0.3, 0.72 }, { 0.66, 0.78 } };
        for (int k = 0; k < pos.length; k++) {
            float cx = (float) (pos[k][0] * w), cy = (float) (pos[k][1] * h), r = Math.max(3f, h * (k % 2 == 0 ? 0.09f : 0.06f));
            android.graphics.Path pt = new android.graphics.Path();
            for (int j = 0; j < 8; j++) {
                double an = j * Math.PI / 4; float rr = j % 2 == 0 ? r : r * 0.25f;
                float xx = cx + (float) (Math.cos(an) * rr), yy = cy + (float) (Math.sin(an) * rr);
                if (j == 0) pt.moveTo(xx, yy); else pt.lineTo(xx, yy);
            }
            pt.close();
            c.drawPath(pt, bl);
        }
    }

    static int ganados(long esquinas, long ms) {
        int n = 0;
        for (long m : LOGROS_ESQUINAS) if (esquinas >= m) n++;
        for (int h : LOGROS_HORAS) if (ms >= h * 3600_000L) n++;
        return n;
    }

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
        revisarTrofeos(ganados(antes, ms), ganados(antes + 1, ms));
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
        revisarTrofeos(ganados(esq, antes), ganados(esq, despues));
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
