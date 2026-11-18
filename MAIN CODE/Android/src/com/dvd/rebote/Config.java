package com.dvd.rebote;

import android.content.Context;

import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;

// Configuración guardada en dvd_config.json (dentro de la app)
public class Config {
    public static final int MAX_CANTIDAD = 1000000;

    public int velocidad = 500;          // píxeles por segundo
    public int tamano = 100;             // porcentaje
    public int cantidad = 1;             // cuántos DVDs (sin límite práctico)
    public int opacidad = 80;            // % (Android no deja pasar los toques por encima del 80%)
    public String estilo = "logo";       // "logo", "disco" o "reloj" (la hora rebotando)
    public int color = 0xFF00BFFF;
    public boolean cambiarColor = true;
    public boolean mostrarContador = true;
    public boolean mostrarMensajeEsquina = true;
    public boolean sonido = false;
    public boolean sonidoSkin = true;   // con skin puesta, en las esquinas suena el metal de la skin
    public boolean pausado = false;
    public boolean modoSoloEsquinas = false;
    public boolean estela = false;          // estela detrás de cada DVD
    public boolean reloj = false;           // la hora en una pastilla debajo de cada DVD
    public boolean vibrar = false;          // vibrar al tocar una esquina
    public boolean vibrarControl = true;    // vibrar el control (gamepad) conectado al tocar una esquina
    public boolean choque = false;          // los DVDs rebotan entre ellos
    public String logoTexto = "DVD";        // texto del logo
    public boolean logoImagen = false;      // usar la imagen guardada (logo_personalizado.png) como logo

    public static File archivoImagen(Context c) { return new File(c.getFilesDir(), "logo_personalizado.png"); }
    public int toleranciaEsquinaPx = 12;
    public int reiniciarContador = 0;

    // La configuración actual (la lee el servicio en cada frame)
    public static volatile Config actual = new Config();

    static File archivo(Context c) { return new File(c.getFilesDir(), "dvd_config.json"); }

    // ---- Límite de DVDs según la RAM del cel ----
    // 4 GB o menos -> 25, hasta 8 GB -> 60, más de 8 GB -> sin límite.
    // (Android reporta un poco menos de lo que dice la caja, por eso el margen de 0.5 GB)
    static final long GB = 1024L * 1024L * 1024L;

    public static long ramTotal(Context c) {
        try {
            android.app.ActivityManager am = c.getSystemService(android.app.ActivityManager.class);
            android.app.ActivityManager.MemoryInfo mi = new android.app.ActivityManager.MemoryInfo();
            am.getMemoryInfo(mi);
            return mi.totalMem;
        } catch (Exception e) { return 0; }
    }

    // ¿El cel tiene motor de vibración?
    public static boolean tieneVibrador(Context c) {
        try {
            android.os.Vibrator v;
            if (android.os.Build.VERSION.SDK_INT >= 31) {
                android.os.VibratorManager vm = c.getSystemService(android.os.VibratorManager.class);
                v = vm != null ? vm.getDefaultVibrator() : null;
            } else {
                v = (android.os.Vibrator) c.getSystemService(Context.VIBRATOR_SERVICE);
            }
            return v != null && v.hasVibrator();
        } catch (Exception e) { return false; }
    }

    // La RAM que dice la caja (4 GB, 6 GB, 8 GB...). Android da la que puede usar (totalMem), que es
    // un poco menos porque el sistema se reserva una parte: por eso un cel de 4 GB salía con "3.6 GB".
    // Android 14+ da la de la caja (advertisedMem); en los demás se redondea al tamaño de RAM más cercano hacia arriba.
    static final double[] TAMANOS_RAM = { 1, 1.5, 2, 3, 4, 6, 8, 10, 12, 16, 18, 20, 24, 32, 48, 64 };

    public static double ramGB(Context c) {
        try {
            if (android.os.Build.VERSION.SDK_INT >= 34) {
                android.app.ActivityManager am = c.getSystemService(android.app.ActivityManager.class);
                android.app.ActivityManager.MemoryInfo mi = new android.app.ActivityManager.MemoryInfo();
                am.getMemoryInfo(mi);
                if (mi.advertisedMem > 0)
                {
                    // puede venir en GB "de caja" (1000^3 bytes): se toma el tamaño normal más cercano
                    double g = mi.advertisedMem / (double) GB, mejor = TAMANOS_RAM[0];
                    for (double t : TAMANOS_RAM) if (Math.abs(t - g) < Math.abs(mejor - g)) mejor = t;
                    return Math.abs(mejor - g) < 0.6 ? mejor : Math.round(g);
                }
            }
        } catch (Exception e) { }
        long ram = ramTotal(c);
        if (ram <= 0) return 0;
        double gb = ram / (double) GB;
        for (double t : TAMANOS_RAM) if (gb <= t) return t;
        return Math.ceil(gb);
    }

    // código ROOT: quita el límite de DVDs por RAM (se queda guardado hasta que lo vuelvas a escribir)
    public static boolean root(Context c) {
        try { return c.getSharedPreferences("cheats", Context.MODE_PRIVATE).getBoolean("root", false); }
        catch (Exception e) { return false; }
    }

    public static boolean alternarRoot(Context c) {
        boolean nuevo = !root(c);
        c.getSharedPreferences("cheats", Context.MODE_PRIVATE).edit().putBoolean("root", nuevo).apply();
        Cheats.limiteToken++;
        return nuevo;
    }

    public static int limiteCantidad(Context c) {
        if (root(c)) return MAX_CANTIDAD;
        double gb = ramGB(c);
        if (gb <= 0) return MAX_CANTIDAD;
        if (gb <= 4.01) return 25;
        if (gb <= 8.01) return 60;
        return MAX_CANTIDAD;
    }

    public static String textoLimite(Context c) {
        double r = ramGB(c);
        int lim = limiteCantidad(c);
        String gb = r == Math.floor(r) ? String.valueOf((long) r) : String.format(java.util.Locale.US, "%.1f", r);   // "4", "1.5"
        if (root(c)) return "ROOT: sin límite de DVDs (tu cel tiene " + gb + " GB de RAM, con muchos se puede trabar)";
        return lim >= MAX_CANTIDAD
            ? "Tu cel tiene " + gb + " GB de RAM: sin límite de DVDs"
            : "Tu cel tiene " + gb + " GB de RAM: máximo " + lim + " DVDs";
    }

    public static Config load(Context c) {
        Config k = new Config();
        try {
            File f = archivo(c);
            if (f.exists()) {
                byte[] b = new byte[(int) f.length()];
                try (FileInputStream in = new FileInputStream(f)) {
                    int off = 0;
                    while (off < b.length) { int n = in.read(b, off, b.length - off); if (n < 0) break; off += n; }
                }
                JSONObject o = new JSONObject(new String(b, StandardCharsets.UTF_8));
                k.velocidad = clamp(o.optInt("Velocidad", k.velocidad), 20, 5000);
                k.tamano = clamp(o.optInt("Tamano", k.tamano), 20, 400);
                k.cantidad = clamp(o.optInt("Cantidad", k.cantidad), 1, limiteCantidad(c));
                k.opacidad = clamp(o.optInt("Opacidad", k.opacidad), 5, 80);
                String est = o.optString("Estilo", k.estilo);
                k.estilo = "disco".equals(est) ? "disco" : "reloj".equals(est) ? "reloj" : "logo";
                k.color = parseColor(o.optString("Color", toHex(k.color)), k.color);
                k.cambiarColor = o.optBoolean("CambiarColor", k.cambiarColor);
                k.mostrarContador = o.optBoolean("MostrarContador", k.mostrarContador);
                k.mostrarMensajeEsquina = o.optBoolean("MostrarMensajeEsquina", k.mostrarMensajeEsquina);
                k.sonido = o.optBoolean("Sonido", k.sonido);
                k.sonidoSkin = o.optBoolean("SonidoSkin", k.sonidoSkin);
                k.pausado = o.optBoolean("Pausado", k.pausado);
                k.modoSoloEsquinas = o.optBoolean("ModoSoloEsquinas", k.modoSoloEsquinas);
                k.estela = o.optBoolean("Estela", k.estela);
                k.reloj = o.optBoolean("Reloj", k.reloj);
                k.vibrar = o.optBoolean("Vibrar", k.vibrar) && tieneVibrador(c);
                k.vibrarControl = o.optBoolean("VibrarControl", k.vibrarControl);
                k.choque = o.optBoolean("Choque", k.choque);
                String t = o.optString("LogoTexto", k.logoTexto).trim();
                k.logoTexto = t.isEmpty() ? "DVD" : (t.length() > 20 ? t.substring(0, 20) : t);
                k.logoImagen = o.optBoolean("LogoImagen", k.logoImagen) && archivoImagen(c).exists();
                k.toleranciaEsquinaPx = clamp(o.optInt("ToleranciaEsquinaPx", k.toleranciaEsquinaPx), 0, 200);
                k.reiniciarContador = o.optInt("ReiniciarContador", k.reiniciarContador);
            }
        } catch (Exception ignored) { }
        actual = k;
        return k;
    }

    public JSONObject toJson() {
        JSONObject o = new JSONObject();
        try {
            o.put("Velocidad", velocidad);
            o.put("Tamano", tamano);
            o.put("Cantidad", cantidad);
            o.put("Opacidad", opacidad);
            o.put("Estilo", estilo);
            o.put("Color", toHex(color));
            o.put("CambiarColor", cambiarColor);
            o.put("MostrarContador", mostrarContador);
            o.put("MostrarMensajeEsquina", mostrarMensajeEsquina);
            o.put("Sonido", sonido);
            o.put("SonidoSkin", sonidoSkin);
            o.put("Pausado", pausado);
            o.put("ModoSoloEsquinas", modoSoloEsquinas);
            o.put("Estela", estela);
            o.put("Reloj", reloj);
            o.put("Vibrar", vibrar);
            o.put("VibrarControl", vibrarControl);
            o.put("Choque", choque);
            o.put("LogoTexto", logoTexto);
            o.put("LogoImagen", logoImagen);
            o.put("ToleranciaEsquinaPx", toleranciaEsquinaPx);
            o.put("ReiniciarContador", reiniciarContador);
        } catch (Exception ignored) { }
        return o;
    }

    public String toJsonText() {
        try { return toJson().toString(2); } catch (Exception e) { return "{}"; }
    }

    public void save(Context c) {
        actual = copy();
        try {
            File f = archivo(c);
            File tmp = new File(f.getPath() + ".tmp");
            try (FileOutputStream out = new FileOutputStream(tmp)) {
                out.write(toJsonText().getBytes(StandardCharsets.UTF_8));
            }
            if (!tmp.renameTo(f)) { f.delete(); tmp.renameTo(f); }
        } catch (Exception ignored) { }
    }

    public Config copy() {
        Config k = new Config();
        k.velocidad = velocidad; k.tamano = tamano; k.cantidad = cantidad; k.opacidad = opacidad;
        k.estilo = estilo; k.color = color; k.cambiarColor = cambiarColor; k.mostrarContador = mostrarContador;
        k.mostrarMensajeEsquina = mostrarMensajeEsquina; k.sonido = sonido; k.sonidoSkin = sonidoSkin; k.pausado = pausado;
        k.modoSoloEsquinas = modoSoloEsquinas; k.toleranciaEsquinaPx = toleranciaEsquinaPx;
        k.estela = estela; k.reloj = reloj; k.vibrar = vibrar; k.vibrarControl = vibrarControl;
        k.choque = choque; k.logoTexto = logoTexto; k.logoImagen = logoImagen;
        k.reiniciarContador = reiniciarContador;
        return k;
    }

    static int clamp(int v, int lo, int hi) { return Math.max(lo, Math.min(hi, v)); }

    public static String toHex(int c) { return String.format("#%06X", c & 0xFFFFFF); }

    static int parseColor(String s, int def) {
        try {
            if (s.startsWith("#")) s = s.substring(1);
            return 0xFF000000 | Integer.parseInt(s, 16);
        } catch (Exception e) { return def; }
    }
}
