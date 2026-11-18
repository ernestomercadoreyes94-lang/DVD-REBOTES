package com.dvd.rebote;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.PorterDuffXfermode;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.media.AudioManager;
import android.media.ToneGenerator;
import android.view.View;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Random;

// Dibuja y mueve todos los DVDs, con el MISMO dibujo que la versión de Windows.
// Ocupa toda la pantalla, es transparente y no recibe toques.
public class DvdView extends View {

    public interface OpacityListener { void onOpacity(float alpha); }

    // Con más de este número de DVDs se usa el modo ligero (sin brillo grande ni anillo)
    static final int LIMITE_MODO_LIGERO = 25;
    // Medidas en "unidades" iguales a las de Windows (logo 220x110, disco 150x150, brillo 70)
    static final float PAD = 70f;

    static class Logo {
        float x, y, vx, vy, spin;
        int color;
        double lastCorner = -9999, glowStart = -1, glowUntil = -1;
        // modo solo esquinas (recorrido "desdoblado"): de (u0,v0) a la esquina (a,b)
        boolean soloActive;
        double u0, v0, t;    // inicio (fracción de pantalla) y avance 0..1
        int a, b;            // esquina destino desdoblada (enteros)
        int segX, segY;      // tramo actual (cambia en cada rebote con una pared)
        // estela: últimas posiciones (anillo circular)
        final float[] hx = new float[MAX_ESTELA], hy = new float[MAX_ESTELA];
        int hCount, hPos;
        double hLast = -9999;
        double nextCaos = 0;   // cheat CAOS: cuándo cambia de dirección
        // cheat LANZAR: agarrado con el dedo, y el impulso con el que lo aventaste (se calma solo)
        boolean agarrado;
        boolean inclinado;   // cheat INCLINAR: su rapidez la decide la gravedad
        float lanzamiento = 1f; double lanzadoEn = -1;
    }

    static final int MAX_ESTELA = 8;
    final Paint trailPaint = new Paint(Paint.FILTER_BITMAP_FLAG);
    android.os.Vibrator vibrador;
    double lastVibe = -9999;

    final ArrayList<Logo> logos = new ArrayList<>();
    final Random rnd = new Random();
    final int[] palette = new int[24];
    final HashMap<Integer, Bitmap> sprites = new HashMap<>();
    final HashMap<Integer, Bitmap> halos = new HashMap<>();
    String cacheKey = "";
    final float density, unidad;   // unidad = píxeles del cel por cada píxel de Windows
    int limite;                                  // máximo de DVDs por la RAM (el código ROOT lo quita)
    int limiteToken = Cheats.limiteToken;
    int limpio;                                  // esquinas de esta vez SIN trucos (para el récord)
    OpacityListener opacityListener;
    int lastOpacity = -1;

    // Estadísticas: solo en memoria, no se guardan
    int total, supIzq, supDer, infIzq, infDer;
    String ultima = "-";
    int lastReset;
    double msgStart = -1, msgUntil = -1, lastSound = -9999;
    int msgColor = Color.WHITE;
    Path msgPath; String msgPathText = "";

    long lastNanos = 0;
    ToneGenerator tone;

    final Paint spriteSmooth = new Paint(Paint.FILTER_BITMAP_FLAG);
    final Paint spriteFast = new Paint();
    final Paint ringPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    final Paint textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    final Paint panelPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    final Typeface black = Typeface.create("sans-serif-black", Typeface.NORMAL);

    public DvdView(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        unidad = density * 0.6f;
        limite = Config.limiteCantidad(c);
        for (int i = 0; i < palette.length; i++) palette[i] = Color.HSVToColor(new float[]{i * 360f / palette.length, 0.75f, 1f});
        ringPaint.setStyle(Paint.Style.STROKE);
        lastReset = Config.actual.reiniciarContador;
        // sonido de esquina por el volumen MULTIMEDIA (el de notificaciones suele estar bajo o silenciado)
        try { tone = new ToneGenerator(AudioManager.STREAM_MUSIC, 90); } catch (Exception ignored) { }
        try {
            if (android.os.Build.VERSION.SDK_INT >= 31) {
                android.os.VibratorManager vm = c.getSystemService(android.os.VibratorManager.class);
                if (vm != null) vibrador = vm.getDefaultVibrator();
            } else {
                vibrador = (android.os.Vibrator) c.getSystemService(Context.VIBRATOR_SERVICE);
            }
        } catch (Exception ignored) { }
    }

    // Vibración corta al tocar una esquina (si está activada)
    void vibrar(double now) {
        if (vibrador == null || !vibrador.hasVibrator() || now - lastVibe < 0.15) return;
        lastVibe = now;
        try {
            vibrador.vibrate(android.os.VibrationEffect.createOneShot(70, android.os.VibrationEffect.DEFAULT_AMPLITUDE));
        } catch (Exception ignored) { }
    }

    void setOpacityListener(OpacityListener l) { opacityListener = l; }

    // ---- cheat LANZAR: agarrar un DVD con el dedo, arrastrarlo y aventarlo ----
    public interface TocableListener { void onTocable(boolean tocable); }
    TocableListener tocableListener;
    boolean tocableActual;
    Logo enDedo;
    float dedoDx, dedoDy;
    android.view.VelocityTracker rastreo;
    long ultimoToqueVacio;

    void setTocableListener(TocableListener l) { tocableListener = l; }

    float toqueX, toqueY; long toqueEn; boolean seMovio;

    // cheat TOCA: al tocar un DVD se duplican TODOS (si hay 10, quedan 20).
    // Cada uno se parte en dos y las dos mitades salen disparadas hacia lados distintos.
    void duplicar(Logo tocado, Config cfg) {
        if (logos.size() >= limite) {
            Cheats.msg = "¡Ya no caben más DVDs en tu cel! (" + logos.size() + ")";
            Cheats.msgNanos = System.nanoTime();
            return;
        }
        int antes = logos.size();
        int cuantos = Math.min(antes, limite - antes);   // si no caben todos, se duplican los que quepan
        for (int i = 0; i < cuantos; i++) partirEnDos(logos.get(i), cfg);
        Cheats.extra += cuantos;   // así no se borran al revisar cuántos DVDs debe haber
        Cheats.msg = "¡DUPLICADOS! " + antes + " → " + logos.size() + " DVDs";
        Cheats.msgNanos = System.nanoTime();
    }

    void partirEnDos(Logo L, Config cfg) {
        Logo n = new Logo();
        n.x = L.x; n.y = L.y; n.spin = L.spin;
        // se esparcen: cada uno sale disparado hacia un lado distinto (casi contrarios), 5 veces más rápido,
        // y en 2.5 s se calman (con INCLINAR conservan su rapidez actual)
        float r = L.inclinado ? Math.max(1f, (float) Math.sqrt(L.vx * L.vx + L.vy * L.vy)) : 1f;
        double ang = rnd.nextDouble() * Math.PI * 2, abre = (rnd.nextDouble() - 0.5) * 0.9;
        L.vx = (float) Math.cos(ang) * r; L.vy = (float) Math.sin(ang) * r;
        n.vx = (float) Math.cos(ang + Math.PI + abre) * r; n.vy = (float) Math.sin(ang + Math.PI + abre) * r;
        double ahora = System.nanoTime() / 1e9;
        L.lanzamiento = n.lanzamiento = 5f;
        L.lanzadoEn = n.lanzadoEn = ahora;
        L.soloActive = false;
        n.color = cfg.cambiarColor ? palette[rnd.nextInt(palette.length)] : L.color;
        n.inclinado = L.inclinado;
        logos.add(n);
        if (cfg.cambiarColor) newColor(L);
    }

    void soltar() {
        if (enDedo != null) enDedo.agarrado = false;
        enDedo = null;
        if (rastreo != null) { rastreo.recycle(); rastreo = null; }
    }

    @Override
    public boolean onTouchEvent(android.view.MotionEvent ev) {
        if (!Cheats.lanzar && !Cheats.toca) return false;
        Config cfg = Config.actual;
        float x = ev.getX(), y = ev.getY(), w = W(cfg), h = H(cfg);
        switch (ev.getActionMasked()) {
            case android.view.MotionEvent.ACTION_DOWN: {
                soltar();
                // el DVD que está más arriba (el último que se dibuja) y que tenga el dedo encima
                // (con un poco de margen para que sea fácil atinarle)
                float m = 16 * density;
                for (int i = logos.size() - 1; i >= 0; i--) {
                    Logo L = logos.get(i);
                    if (x >= L.x - m && x <= L.x + w + m && y >= L.y - m && y <= L.y + h + m) { enDedo = L; break; }
                }
                if (enDedo == null) {
                    // dos toques seguidos donde no hay DVD = salir de LANZAR y TOCA (para usar el cel normal)
                    long t = System.currentTimeMillis();
                    if (t - ultimoToqueVacio < 350) {
                        Cheats.lanzar = false; Cheats.toca = false;
                        Cheats.msg = "LANZAR / TOCA DESACTIVADO";
                        Cheats.msgNanos = System.nanoTime();
                    }
                    ultimoToqueVacio = t;
                    return true;
                }
                toqueX = x; toqueY = y; toqueEn = System.currentTimeMillis(); seMovio = false;
                // con LANZAR se agarra; con TOCA solo, se queda donde está hasta soltar el dedo
                if (Cheats.lanzar) { enDedo.agarrado = true; enDedo.lanzadoEn = -1; }
                dedoDx = x - enDedo.x; dedoDy = y - enDedo.y;
                rastreo = android.view.VelocityTracker.obtain();
                rastreo.addMovement(ev);
                return true;
            }
            case android.view.MotionEvent.ACTION_MOVE:
                if (enDedo != null) {
                    if (Math.hypot(x - toqueX, y - toqueY) > 12 * density) seMovio = true;
                    if (Cheats.lanzar) {
                        enDedo.x = Math.max(0, Math.min(getWidth() - w, x - dedoDx));
                        enDedo.y = Math.max(0, Math.min(getHeight() - h, y - dedoDy));
                    }
                    if (rastreo != null) rastreo.addMovement(ev);
                }
                return true;
            case android.view.MotionEvent.ACTION_UP:
            case android.view.MotionEvent.ACTION_CANCEL:
                // TOCA: un toque rápido (sin arrastrar) duplica ese DVD
                boolean esToque = !seMovio && System.currentTimeMillis() - toqueEn < 400
                    && ev.getActionMasked() == android.view.MotionEvent.ACTION_UP;
                if (enDedo != null && Cheats.toca && esToque) {
                    duplicar(enDedo, cfg);
                    soltar();
                    return true;
                }
                if (enDedo != null && rastreo != null && Cheats.lanzar) {
                    rastreo.addMovement(ev);
                    rastreo.computeCurrentVelocity(1000);   // píxeles por segundo
                    float vx = rastreo.getXVelocity(), vy = rastreo.getYVelocity();
                    float rapidez = (float) Math.sqrt(vx * vx + vy * vy);
                    if (rapidez > 150 * density) {
                        // sale en la dirección del dedo, tan rápido como lo aventaste (hasta 10 veces la normal)
                        enDedo.vx = vx / rapidez; enDedo.vy = vy / rapidez;
                        enDedo.lanzamiento = Math.max(1f, Math.min(10f, rapidez / Math.max(1f, vel(cfg))));
                        enDedo.lanzadoEn = System.nanoTime() / 1e9;
                    }
                    enDedo.soloActive = false;
                    if (cfg.cambiarColor) newColor(enDedo);
                }
                soltar();
                return true;
        }
        return true;
    }

    // ---- cheat AGITAR: sensor de movimiento (acelerómetro) ----
    // Solo se escucha el sensor mientras el código está activo (así no gasta batería).
    android.hardware.SensorManager sensores;
    android.hardware.SensorEventListener oyente;
    volatile int sacudidas;
    int sacudidasHechas;
    long ultimaSacudida;
    double impulsoHasta = -1;

    // gravedad vista en la pantalla (m/s²), suavizada; la usa el cheat INCLINAR
    volatile float gravX, gravY;

    void revisarSensor() {
        boolean quiero = Cheats.agitar || Cheats.inclinar;
        if (quiero && oyente == null) {
            sensores = (android.hardware.SensorManager) getContext().getSystemService(Context.SENSOR_SERVICE);
            android.hardware.Sensor acel = sensores == null ? null : sensores.getDefaultSensor(android.hardware.Sensor.TYPE_ACCELEROMETER);
            if (acel == null) return;
            oyente = new android.hardware.SensorEventListener() {
                float px, py, pz; boolean hayPrevio;
                public void onSensorChanged(android.hardware.SensorEvent e) {
                    float x = e.values[0], y = e.values[1], z = e.values[2];
                    // Dos formas de notar una sacudida (con cualquiera basta):
                    //  1) la fuerza total se aleja mucho de 1 g (quieto = 1 g por la gravedad)
                    //  2) la aceleración cambia de golpe entre una lectura y la siguiente
                    double g = Math.sqrt(x * x + y * y + z * z) / android.hardware.SensorManager.GRAVITY_EARTH;
                    double salto = hayPrevio ? Math.sqrt((x - px) * (x - px) + (y - py) * (y - py) + (z - pz) * (z - pz)) : 0;
                    px = x; py = y; pz = z; hayPrevio = true;
                    long t = System.currentTimeMillis();
                    if (Cheats.agitar && (Math.abs(g - 1) > 0.55 || salto > 9) && t - ultimaSacudida > 400) { ultimaSacudida = t; sacudidas++; }

                    // INCLINAR: hacia dónde "cae" en la pantalla, según cómo esté girada la pantalla
                    int rot = android.view.Surface.ROTATION_0;
                    try { if (getDisplay() != null) rot = getDisplay().getRotation(); } catch (Exception ignored) { }
                    float sx, sy;
                    switch (rot) {
                        case android.view.Surface.ROTATION_90:  sx = -y; sy = x;  break;
                        case android.view.Surface.ROTATION_180: sx = -x; sy = -y; break;
                        case android.view.Surface.ROTATION_270: sx = y;  sy = -x; break;
                        default:                                sx = x;  sy = y;  break;
                    }
                    // el sensor marca lo contrario de la gravedad en X; en Y la pantalla crece hacia abajo
                    gravX += (-sx - gravX) * 0.2f;
                    gravY += (sy - gravY) * 0.2f;
                }
                public void onAccuracyChanged(android.hardware.Sensor s, int a) { }
            };
            sensores.registerListener(oyente, acel, android.hardware.SensorManager.SENSOR_DELAY_GAME);
        } else if (!quiero && oyente != null) {
            apagarSensor();
        }
    }

    void apagarSensor() {
        if (sensores != null && oyente != null) sensores.unregisterListener(oyente);
        oyente = null;
    }

    void release() {
        apagarSensor();
        if (tone != null) { tone.release(); tone = null; }
        clearCache();
    }

    static boolean disco(Config c) { return "disco".equals(c.estilo); }
    static boolean reloj(Config c) { return "reloj".equals(c.estilo); }

    // ---- Estilo RELOJ: un cronómetro con el TIEMPO QUE LLEVA REBOTANDO el DVD es lo que rebota, en vez del logo ----
    // (sin contar la pausa). Ocupa la misma caja que el logo (220x110) y se redibuja cada segundo.
    double tiempoRebotando;
    String horaReloj() {
        long s = (long) tiempoRebotando;
        return s >= 3600 ? String.format(java.util.Locale.ROOT, "%d:%02d:%02d", s / 3600, s % 3600 / 60, s % 60)
                         : String.format(java.util.Locale.ROOT, "%d:%02d", s / 60, s % 60);
    }

    Path relojForma() {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        p.setTextSize(100);
        String h = horaReloj();
        Path path = new Path();
        p.getTextPath(h, 0, h.length(), 0, 0, path);
        RectF b = new RectF();
        path.computeBounds(b, true);
        if (b.width() <= 0 || b.height() <= 0) return path;
        float k = Math.min((220 - 8) / b.width(), (110 - 8) / b.height());   // tan grande como quepa, sin deformar
        android.graphics.Matrix m = new android.graphics.Matrix();
        m.postTranslate(-b.left, -b.top);
        m.postScale(k, k);
        m.postTranslate((220 - b.width() * k) / 2, (110 - b.height() * k) / 2);
        path.transform(m);
        return path;
    }
    // con TROLL la caja es cuadrada (la imagen es cuadrada)
    static float baseW(Config c) { return disco(c) || Cheats.troll ? 150f : 220f; }
    static float baseH(Config c) { return disco(c) || Cheats.troll ? 150f : 110f; }
    float escala(Config c) { return unidad * c.tamano / 100f * (Cheats.gigante ? 2f : 1f) * (Cheats.enano ? 0.5f : 1f); }
    static float vel(Config c) { return c.velocidad * (Cheats.turbo ? 2f : 1f) * (Cheats.lento ? 0.5f : 1f) * (Cheats.hiper ? 4f : 1f) * Cheats.easter * Cheats.errorVel; }
    static boolean pausado(Config c) { return c.pausado || Cheats.congelado || Cheats.pausaPantallazo; }
    // con INCLINAR el modo solo esquinas se pausa (la gravedad decide el camino)
    static boolean solo(Config c) { return (c.modoSoloEsquinas ^ Cheats.soloCheat) && !Cheats.inclinar; }
    int esquinaToken = Cheats.esquinaToken, resetToken = Cheats.resetToken;
    int reversaToken = Cheats.reversaToken, teleToken = Cheats.teleToken;
    float W(Config c) { return baseW(c) * escala(c); }
    float H(Config c) { return baseH(c) * escala(c); }

    @Override
    protected void onDraw(Canvas canvas) {
        Config cfg = Config.actual;
        long nowN = System.nanoTime();
        float dt = lastNanos == 0 ? 0 : Math.min(0.05f, (nowN - lastNanos) / 1e9f);
        lastNanos = nowN;
        double now = nowN / 1e9;
        // mientras está el pantallazo "No command" los DVDs no se dibujan (se quedan donde estaban)
        if (Cheats.ocultarPantallazo) { postInvalidateOnAnimation(); return; }

        if (cfg.opacidad != lastOpacity && opacityListener != null) {
            lastOpacity = cfg.opacidad;
            opacityListener.onOpacity(Math.min(0.8f, cfg.opacidad / 100f));
        }
        if (cfg.reiniciarContador != lastReset) {
            lastReset = cfg.reiniciarContador;
            total = supIzq = supDer = infIzq = infDer = 0; ultima = "-"; totalActual = 0; limpio = 0;
        }
        if (Cheats.limiteToken != limiteToken) {   // código ROOT: se vuelve a leer el límite de DVDs
            limiteToken = Cheats.limiteToken;
            limite = Config.limiteCantidad(getContext());
        }

        revisarSensor();
        // cheats LANZAR y TOCA: la capa de los DVDs recibe toques solo mientras uno está activo
        boolean quieroToques = Cheats.lanzar || Cheats.toca;
        if (quieroToques != tocableActual && tocableListener != null) {
            tocableActual = quieroToques;
            tocableListener.onTocable(tocableActual);
            if (!tocableActual) soltar();
        }
        if (sacudidas != sacudidasHechas) {
            // cheat AGITAR: el cel se sacudió -> cada DVD sale disparado hacia un lado al azar
            sacudidasHechas = sacudidas;
            impulsoHasta = now + 1.5;
            for (Logo L : logos) {
                double ang = rnd.nextDouble() * Math.PI * 2;
                L.vx = (float) Math.cos(ang); L.vy = (float) Math.sin(ang);
                L.soloActive = false;
            }
            // aviso en pantalla + vibración cortita para que sepas que la sacudida se notó
            Cheats.msg = "¡SACUDIDA!";
            Cheats.msgNanos = System.nanoTime();
            if (vibrador != null && vibrador.hasVibrator()) {
                try { vibrador.vibrate(android.os.VibrationEffect.createOneShot(60, android.os.VibrationEffect.DEFAULT_AMPLITUDE)); } catch (Exception ignored) { }
            }
        }
        if (Cheats.resetToken != resetToken) {
            resetToken = Cheats.resetToken;
            total = supIzq = supDer = infIzq = infDer = 0; ultima = "-"; totalActual = 0; limpio = 0;
        }
        int w = getWidth(), h = getHeight();
        if (w > 0 && h > 0) {
            syncCount(cfg, w, h);
            if (Cheats.esquinaToken != esquinaToken) { esquinaToken = Cheats.esquinaToken; aimAtCorner(cfg, w, h); }
            if (Cheats.reversaToken != reversaToken) {
                // cheat REVERSA: todos dan la vuelta
                reversaToken = Cheats.reversaToken;
                for (Logo L : logos) { L.vx = -L.vx; L.vy = -L.vy; L.soloActive = false; }
            }
            if (Cheats.teleToken != teleToken) {
                // cheat TELETRANSPORTE: cada DVD aparece en un lugar al azar
                teleToken = Cheats.teleToken;
                for (Logo L : logos) {
                    L.x = rnd.nextFloat() * Math.max(1, w - W(cfg));
                    L.y = rnd.nextFloat() * Math.max(1, h - H(cfg));
                    L.soloActive = false; L.hCount = 0;
                }
            }
            checkCache(cfg);
            float maxX = Math.max(0, w - W(cfg)), maxY = Math.max(0, h - H(cfg));
            for (int i = 0, n = logos.size(); i < n; i++) move(logos.get(i), cfg, now, dt, maxX, maxY);
            if (cfg.choque && !solo(cfg) && !pausado(cfg)) chocar(cfg);
            // tiempo rebotando (sin pausa y sin trucos)
            if (!pausado(cfg) && !Cheats.trucoActivo(cfg)) Estadisticas.tiempo(getContext(), dt);
            if (!pausado(cfg)) tiempoRebotando += dt;   // lo que enseña el estilo RELOJ
            drawAll(canvas, cfg, now);
        }
        postInvalidateOnAnimation();
    }

    void syncCount(Config cfg, int w, int h) {
        int objetivo = (int) Math.min((long) cfg.cantidad + Cheats.extra, limite); // nunca más de lo que aguanta la RAM del cel
        while (logos.size() < objetivo) {
            Logo L = new Logo();
            double ang = rnd.nextDouble() * Math.PI / 3 + Math.PI / 12 + rnd.nextInt(4) * Math.PI / 2;
            L.vx = (float) Math.cos(ang); L.vy = (float) Math.sin(ang);
            L.x = rnd.nextFloat() * Math.max(1, w - W(cfg));
            L.y = rnd.nextFloat() * Math.max(1, h - H(cfg));
            L.spin = rnd.nextInt(360);
            L.color = (logos.isEmpty() || !cfg.cambiarColor) ? cfg.color : palette[rnd.nextInt(palette.length)];
            logos.add(L);
        }
        while (logos.size() > objetivo) logos.remove(logos.size() - 1);
        if (!cfg.cambiarColor) for (int i = 0, n = logos.size(); i < n; i++) logos.get(i).color = cfg.color;
    }

    // Lados: 0 = ninguno. Así no se crean textos en cada rebote.
    static final String[] V = {"", "Superior", "Inferior"}, HZ = {"", "izquierda", "derecha"};

    void move(Logo L, Config cfg, double now, float dt, float maxX, float maxY) {
        if (L.agarrado) return; // lo tienes en el dedo (cheat LANZAR)
        if (solo(cfg)) { moveSolo(L, cfg, now, dt, maxX, maxY); return; }
        L.soloActive = false;
        if (pausado(cfg)) return;

        // cheat INCLINAR: la gravedad del cel empuja al DVD (como una canica en una caja).
        // Aquí la dirección (vx, vy) también lleva la rapidez: más largo = más rápido.
        if (Cheats.inclinar && oyente != null) {
            float k = 0.35f * dt;   // qué tanto jala la gravedad
            L.vx += gravX * k; L.vy += gravY * k;
            float r = (float) Math.sqrt(L.vx * L.vx + L.vy * L.vy);
            if (r > 4f) { L.vx *= 4f / r; L.vy *= 4f / r; }   // como mucho 4 veces la velocidad normal
            L.vx *= (float) Math.exp(-0.25 * dt); L.vy *= (float) Math.exp(-0.25 * dt); // un poco de roce
            L.inclinado = true;
        } else if (L.inclinado) {
            // al quitar INCLINAR vuelve a su velocidad normal
            L.inclinado = false;
            float r = (float) Math.sqrt(L.vx * L.vx + L.vy * L.vy);
            if (r < 1e-3f) { double a = rnd.nextDouble() * Math.PI * 2; L.vx = (float) Math.cos(a); L.vy = (float) Math.sin(a); }
            else { L.vx /= r; L.vy /= r; }
        }

        // cheat CAOS: cada DVD cambia de dirección al azar cada poco tiempo
        if (Cheats.caos && now >= L.nextCaos) {
            L.nextCaos = now + 0.4 + rnd.nextDouble() * 0.6;
            double ang = rnd.nextDouble() * Math.PI * 2;
            L.vx = (float) Math.cos(ang); L.vy = (float) Math.sin(ang);
        }

        // después de una sacudida van hasta 4 veces más rápido y se calman en 1.5 s
        float imp = 1f + 3f * (float) Math.max(0, (impulsoHasta - now) / 1.5);
        // aventado con el dedo: sale a la velocidad del dedo y en 2.5 s regresa a la normal
        if (L.lanzadoEn >= 0) {
            double t = (now - L.lanzadoEn) / 2.5;
            if (t >= 1) L.lanzadoEn = -1;
            else imp *= (float) (1 + (L.lanzamiento - 1) * (1 - t) * (1 - t));
        }
        L.x += L.vx * vel(cfg) * imp * dt;
        L.y += L.vy * vel(cfg) * imp * dt;
        L.spin = (L.spin + 120f * dt) % 360f;

        int hs = 0, vs = 0;
        boolean hitX = false, hitY = false;
        if (L.x <= 0) { L.x = 0; L.vx = Math.abs(L.vx); hitX = true; hs = 1; }
        else if (L.x >= maxX) { L.x = maxX; L.vx = -Math.abs(L.vx); hitX = true; hs = 2; }
        if (L.y <= 0) { L.y = 0; L.vy = Math.abs(L.vy); hitY = true; vs = 1; }
        else if (L.y >= maxY) { L.y = maxY; L.vy = -Math.abs(L.vy); hitY = true; vs = 2; }
        if (!hitX && !hitY) return;

        if (cfg.cambiarColor) newColor(L);
        float tol = cfg.toleranciaEsquinaPx * density;
        boolean corner = hitX && hitY;
        if (!corner && hitX) {
            if (L.y <= tol) { corner = true; vs = 1; }
            else if (maxY - L.y <= tol) { corner = true; vs = 2; }
        } else if (!corner && hitY) {
            if (L.x <= tol) { corner = true; hs = 1; }
            else if (maxX - L.x <= tol) { corner = true; hs = 2; }
        }
        if (corner && now - L.lastCorner > 0.4) {
            L.lastCorner = now;
            registerCorner(L, cfg, vs, hs, now);
        }
    }

    // ---- Modo solo esquinas: rebota DE VERDAD (línea recta, choca con las paredes) ----
    // pero con el ángulo justo para que cada recorrido termine en una esquina: si cruza
    // m anchos de pantalla mientras cruza n altos (m y n sin divisores en común), llega a una
    // esquina tras varios rebotes sin tocar otra antes. Posición exacta "desdoblando" los rebotes.
    static final int[][] PARES = {{1,1},{1,2},{2,1},{2,3},{3,2},{1,3},{3,1},{3,4},{4,3},{2,5},{5,2},{3,5},{5,3}};

    void moveSolo(Logo L, Config cfg, double now, float dt, float maxX, float maxY) {
        double rx = Math.max(1.0, maxX), ry = Math.max(1.0, maxY);
        if (!L.soloActive) {
            L.soloActive = true;
            newPath(L, clamp01(L.x / rx), clamp01(L.y / ry), L.vx >= 0 ? 1 : -1, L.vy >= 0 ? 1 : -1);
        }
        if (pausado(cfg)) return;
        L.spin = (L.spin + 120f * dt) % 360f;

        double du = L.a - L.u0, dv = L.b - L.v0;
        double len = Math.sqrt(du * rx * du * rx + dv * ry * dv * ry);
        L.t += vel(cfg) * dt / Math.max(1.0, len);

        if (L.t >= 1.0) {
            // llegó a la esquina
            double cx = fold(L.a), cy = fold(L.b);
            L.x = (float) (cx * rx); L.y = (float) (cy * ry);
            if (cfg.cambiarColor) newColor(L);
            registerCorner(L, cfg, cy < 0.5 ? 1 : 2, cx < 0.5 ? 1 : 2, now);
            newPath(L, cx, cy, cx < 0.5 ? 1 : -1, cy < 0.5 ? 1 : -1);
            return;
        }

        double u = L.u0 + du * L.t, v = L.v0 + dv * L.t;
        L.x = (float) (fold(u) * rx);
        L.y = (float) (fold(v) * ry);

        // rebote en una pared: cambia de color como en el modo normal
        int sx = seg(u, du), sy = seg(v, dv);
        if (sx != L.segX || sy != L.segY) {
            L.segX = sx; L.segY = sy;
            if (cfg.cambiarColor) newColor(L);
        }

        double vx = slope(u) * du * rx, vy = slope(v) * dv * ry;
        double d = Math.sqrt(vx * vx + vy * vy);
        if (d > 1e-9) { L.vx = (float) (vx / d); L.vy = (float) (vy / d); }
    }

    // Nuevo recorrido desde (fx, fy) hacia (sx, sy) que termina en una esquina
    void newPath(Logo L, double fx, double fy, int sx, int sy) {
        int[] p = PARES[rnd.nextInt(PARES.length)];
        L.u0 = fx; L.v0 = fy;
        L.a = sx > 0 ? (int) Math.floor(fx) + p[0] : (int) Math.ceil(fx) - p[0];
        L.b = sy > 0 ? (int) Math.floor(fy) + p[1] : (int) Math.ceil(fy) - p[1];
        L.t = 0;
        L.segX = seg(fx, L.a - fx);
        L.segY = seg(fy, L.b - fy);
    }

    static double fold(double u) { double m = u % 2.0; if (m < 0) m += 2.0; return m <= 1.0 ? m : 2.0 - m; }
    static double slope(double u) { double m = u % 2.0; if (m < 0) m += 2.0; return m < 1.0 ? 1.0 : -1.0; }
    static int seg(double u, double d) { return (int) Math.floor(u + (d >= 0 ? 1e-6 : -1e-6)); }
    static double clamp01(double v) { return Math.max(0.0, Math.min(1.0, v)); }

    // ---- MODO CHOQUE: los DVDs rebotan entre ellos (se apaga solo con más de 60) ----
    void chocar(Config cfg) {
        int n = logos.size();
        if (n < 2 || n > 60) return;
        float w = W(cfg), h = H(cfg);
        for (int i = 0; i < n; i++) {
            Logo a = logos.get(i);
            for (int j = i + 1; j < n; j++) {
                Logo b = logos.get(j);
                float dx = b.x - a.x, dy = b.y - a.y;
                float penX = w - Math.abs(dx), penY = h - Math.abs(dy);
                if (penX <= 0 || penY <= 0) continue;   // no se tocan
                if (penX < penY) {
                    float sx = dx >= 0 ? 1 : -1;
                    a.x -= sx * penX / 2; b.x += sx * penX / 2;
                    if ((a.vx - b.vx) * sx > 0) { float t = a.vx; a.vx = b.vx; b.vx = t; }
                } else {
                    float sy = dy >= 0 ? 1 : -1;
                    a.y -= sy * penY / 2; b.y += sy * penY / 2;
                    if ((a.vy - b.vy) * sy > 0) { float t = a.vy; a.vy = b.vy; b.vy = t; }
                }
                if (cfg.cambiarColor) { newColor(a); newColor(b); }
            }
        }
    }

    // Cheat ESQUINA: lanza todos los DVDs en diagonal hacia una esquina
    void aimAtCorner(Config cfg, int w, int h) {
        float maxX = Math.max(0, w - W(cfg)), maxY = Math.max(0, h - H(cfg));
        for (int i = 0, n = logos.size(); i < n; i++) {
            Logo L = logos.get(i);
            L.soloActive = false;
            int c = rnd.nextInt(4);
            float cx = (c % 2 == 0) ? 0 : maxX, cy = (c < 2) ? 0 : maxY;
            float sx = (c % 2 == 0) ? -1 : 1, sy = (c < 2) ? -1 : 1;
            float d = Math.min(maxX, maxY) * (0.3f + rnd.nextFloat() * 0.6f);
            L.x = cx - sx * d; L.y = cy - sy * d;
            L.vx = sx * 0.7071f; L.vy = sy * 0.7071f;
        }
    }

    void newColor(Logo L) {
        int c, tries = 0;
        do { c = palette[rnd.nextInt(palette.length)]; } while (c == L.color && ++tries < 10);
        L.color = c;
    }

    // Esquinas de esta vez (para la pantalla de ajustes) y récord guardado (no se borra al cerrar)
    static volatile int totalActual;

    static int leerRecord(Context c) { return Estadisticas.record(c); }
    static long leerFechaRecord(Context c) { return c.getSharedPreferences("record", Context.MODE_PRIVATE).getLong("fecha", 0); }

    @Override protected void onDetachedFromWindow() {
        Estadisticas.guardar(getContext());   // al cerrar los DVDs se guarda lo que faltaba
        super.onDetachedFromWindow();
    }

    void registerCorner(Logo L, Config cfg, int v, int h, double now) {
        total++;
        totalActual = total;
        // récord y estadísticas: solo sin trucos (LANZAR, SOLO, TURBO...)
        if (!Cheats.trucoActivo(cfg)) {
            limpio++;
            try { Estadisticas.esquina(getContext(), v, h, limpio); } catch (Exception ignored) { }
        }
        if (v == 1 && h == 1) supIzq++;
        else if (v == 1) supDer++;
        else if (h == 1) infIzq++;
        else infDer++;
        ultima = V[v] + " " + HZ[h];
        L.glowStart = now; L.glowUntil = now + 2.0;
        msgStart = now; msgUntil = now + 2.0; msgColor = L.color;
        if (cfg.sonido && tone != null && now - lastSound > 0.15) {
            lastSound = now;
            try { tone.startTone(ToneGenerator.TONE_PROP_BEEP2, 180); } catch (Exception ignored) { }
        }
        if (cfg.vibrar) vibrar(now);
        if (cfg.vibrarControl) vibrarControles(now);
    }

    // Vibra los controles (gamepads) conectados que tengan vibración
    double lastPad = -9999;

    void vibrarControles(double now) {
        if (now - lastPad < 0.15) return;
        lastPad = now;
        int encontrados = 0, vibraron = 0;
        // el control vibra 2 segundos, lo mismo que dura el brillo de la esquina
        android.os.VibrationEffect efecto = android.os.VibrationEffect.createOneShot(2000, android.os.VibrationEffect.DEFAULT_AMPLITUDE);
        for (int id : android.view.InputDevice.getDeviceIds()) {
            android.view.InputDevice d = android.view.InputDevice.getDevice(id);
            if (d == null) continue;
            int src = d.getSources();
            boolean esControl = (src & android.view.InputDevice.SOURCE_GAMEPAD) == android.view.InputDevice.SOURCE_GAMEPAD
                || (src & android.view.InputDevice.SOURCE_JOYSTICK) == android.view.InputDevice.SOURCE_JOYSTICK;
            if (!esControl) continue;
            encontrados++;
            try {
                if (android.os.Build.VERSION.SDK_INT >= 31) {
                    android.os.VibratorManager vm = d.getVibratorManager();
                    if (vm.getVibratorIds().length > 0) {
                        vm.vibrate(android.os.CombinedVibration.createParallel(efecto));
                        vibraron++;
                    }
                } else {
                    android.os.Vibrator v = d.getVibrator();
                    if (v != null && v.hasVibrator()) { v.vibrate(efecto); vibraron++; }
                }
            } catch (Exception ignored) { }
        }
    }

    // ---------------- dibujo ----------------

    // Reloj para los colores: mientras los DVDs están pausados (pantallazo, CONGELAR o pausa) se detiene,
    // así el arcoíris de cuando tocan esquina (y el brillo y PARPADEO) se quedan quietos.
    // Al despausar se recorre el brillo de cada DVD lo que duró la pausa: sigue donde se quedó.
    double relojCongelado = -1;
    double relojDibujo(Config cfg, double now) {
        if (pausado(cfg)) {
            if (relojCongelado < 0) relojCongelado = now;
            return relojCongelado;
        }
        if (relojCongelado >= 0) {
            double pausa = now - relojCongelado;
            for (Logo L : logos) if (L.glowUntil > relojCongelado) { L.glowStart += pausa; L.glowUntil += pausa; }
            relojCongelado = -1;
        }
        return now;
    }

    void drawAll(Canvas canvas, Config cfg, double now) {
        now = relojDibujo(cfg, now);
        boolean ligero = logos.size() > LIMITE_MODO_LIGERO;
        boolean girar = disco(cfg);   // el disco gira siempre, también en modo ligero
        float sc = escala(cfg), W = W(cfg), H = H(cfg), pad = PAD * sc;
        // al girar se usa suavizado para que no se vea pixelado
        Paint sp = (ligero && !girar) ? spriteFast : spriteSmooth;

        for (int i = 0, n = logos.size(); i < n; i++) {
            Logo L = logos.get(i);
            // cheat ARCOIRIS: cada DVD va pasando por todos los colores
            // al tocar una esquina se pone arcoíris 2 segundos (lo que dura el brillo)
            boolean arco = Cheats.arcoiris || now < L.glowUntil;
            int col = arco ? palette[(int) ((now * 8 + i * 3) % palette.length)] : L.color;
            String skinAhora = skinActiva();
            if (skinAhora != null) col = Estadisticas.colorSkin(skinAhora);   // con skin: brillo y anillo del color del metal
            // cheat PARPADEO: cada DVD aparece y desaparece (cada uno a su ritmo)
            if (Cheats.parpadeo && ((now * 2.5 + i * 0.37) % 1.0) >= 0.5) { L.hCount = 0; continue; }

            // ESTELA: copias del logo en sus últimas posiciones, cada vez más transparentes
            if ((cfg.estela || Cheats.estela) && n <= 100) {
                int largo = ligero ? 4 : MAX_ESTELA;
                if (now - L.hLast >= 0.035) {
                    L.hLast = now;
                    L.hx[L.hPos] = L.x; L.hy[L.hPos] = L.y;
                    L.hPos = (L.hPos + 1) % MAX_ESTELA;
                    if (L.hCount < MAX_ESTELA) L.hCount++;
                }
                Bitmap ghost = getSprite(col, cfg);
                int cnt = Math.min(largo, L.hCount);
                for (int k = cnt; k >= 1; k--) {
                    int idx = (L.hPos - k + MAX_ESTELA * 2) % MAX_ESTELA;
                    trailPaint.setAlpha((int) (255 * 0.45f * (1f - (k - 1) / (float) largo) * (Cheats.fantasma ? 0.35f : 1f)));
                    drawBmp(canvas, ghost, L.hx[idx], L.hy[idx], girar, L.spin, trailPaint);
                }
            } else L.hCount = 0;

            boolean glowing = now < L.glowUntil;
            int alpha = 255;
            if (glowing) {
                float t = (float) ((now - L.glowStart) / (L.glowUntil - L.glowStart));
                float inten = 1f - t;
                float pulse = (float) (0.6 + 0.4 * Math.sin((now - L.glowStart) * 25));
                if (ligero) {
                    // modo ligero: solo parpadea, sin brillo ni anillo
                    alpha = (int) (255 * (1f - 0.5f * inten * (1f - pulse)));
                } else {
                    // anillo que se expande (como en Windows)
                    float r = 30 * sc + t * (PAD + 40) * sc;
                    ringPaint.setColor(col);
                    ringPaint.setAlpha((int) (220 * inten));
                    ringPaint.setStrokeWidth(5 * sc);
                    float cx = L.x + W / 2, cy = L.y + H / 2;
                    canvas.drawOval(cx - r - W / 3, cy - r * 0.8f - H / 4, cx + r + W / 3, cy + r * 0.8f + H / 4, ringPaint);
                    Bitmap halo = getHalo(col, cfg);
                    sp.setAlpha((int) (255 * Math.min(1f, inten * (0.5f + 0.5f * pulse))));
                    drawBmp(canvas, halo, L.x - pad, L.y - pad, girar, L.spin, sp);
                }
            }
            sp.setAlpha(Cheats.fantasma ? (int) (alpha * 0.35f) : alpha);   // cheat FANTASMA
            drawBmp(canvas, getSprite(col, cfg), L.x, L.y, girar, L.spin, sp);        }
        sp.setAlpha(255);
        drawMessage(canvas, cfg, now);
        drawHud(canvas, cfg, now);
        drawCheatMsg(canvas);
        drawLogro(canvas);
    }

    // Aviso de LOGRO estilo consola: píldora oscura abajo al centro con el logo de DVD (o el trofeo).
    // Efecto: sube deslizándose, el icono hace "pop", pasa un brillo por encima y el borde brilla del
    // color del trofeo; luego baja y se desvanece. Si hay un trofeo esperando, sale al terminar.
    final Paint logroP = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    final Bitmap[] trofeos = new Bitmap[5];
    static final int[] COLOR_TROFEO = { Color.rgb(0, 191, 255), Color.rgb(225, 140, 70), Color.rgb(205, 215, 230), Color.rgb(255, 205, 60), Color.rgb(130, 235, 255) };

    Bitmap trofeo(int n) {
        if (n < 1 || n > 4) return null;
        if (trofeos[n] == null) {
            int id = n == 1 ? com.dvd.rebote.R.drawable.trofeo_bronce : n == 2 ? com.dvd.rebote.R.drawable.trofeo_plata : n == 3 ? com.dvd.rebote.R.drawable.trofeo_oro : com.dvd.rebote.R.drawable.trofeo_diamante;
            try { trofeos[n] = android.graphics.BitmapFactory.decodeResource(getResources(), id); } catch (Exception e) { }
        }
        return trofeos[n];
    }

    // ---- CONFETI de los trofeos: papelitos que giran y destellos, del color del trofeo (~3.6 s) ----
    long confetiDe, confetiNanos, confetiUltimo;
    int confetiN, confetiTrofeo;
    float[] cfX, cfY, cfVx, cfVy, cfRot, cfVr, cfTam, cfFase;
    int[] cfCol; boolean[] cfEst;
    final Paint confetiP = new Paint(Paint.ANTI_ALIAS_FLAG);

    void lanzarConfeti(long deAviso, int tro, float ox, float oy, float ancho) {
        confetiDe = deAviso; confetiTrofeo = tro;
        confetiNanos = confetiUltimo = System.nanoTime();
        int[][] paletas = {
            { Color.rgb(225, 140, 70), Color.rgb(255, 190, 130), Color.rgb(160, 90, 40), Color.WHITE },
            { Color.rgb(205, 215, 230), Color.rgb(160, 200, 240), Color.rgb(240, 245, 255), Color.WHITE },
            { Color.rgb(255, 205, 60), Color.rgb(255, 235, 140), Color.rgb(230, 160, 20), Color.WHITE, Color.rgb(0, 191, 255) },
            { Color.rgb(130, 235, 255), Color.rgb(200, 250, 255), Color.rgb(60, 170, 240), Color.WHITE, Color.rgb(190, 140, 255), Color.rgb(255, 205, 60) } };
        int[] cols = paletas[tro - 1];
        int n = tro == 4 ? 230 : tro == 3 ? 160 : tro == 2 ? 110 : 80;
        confetiN = n;
        cfX = new float[n]; cfY = new float[n]; cfVx = new float[n]; cfVy = new float[n]; cfRot = new float[n]; cfVr = new float[n];
        cfTam = new float[n]; cfFase = new float[n]; cfCol = new int[n]; cfEst = new boolean[n];
        float d = density;
        for (int i = 0; i < n; i++) {
            double ang = -Math.PI / 2 + (rnd.nextDouble() - 0.5) * Math.PI * 1.25;
            double vel = (260 + rnd.nextDouble() * 420) * d;
            cfX[i] = ox + (rnd.nextFloat() - 0.5f) * ancho * 0.8f; cfY[i] = oy;
            cfVx[i] = (float) (Math.cos(ang) * vel); cfVy[i] = (float) (Math.sin(ang) * vel);
            cfRot[i] = rnd.nextFloat() * 360; cfVr[i] = (rnd.nextFloat() - 0.5f) * 720;
            cfTam[i] = (4 + rnd.nextFloat() * 5) * d; cfFase[i] = rnd.nextFloat() * 6.28f;
            cfCol[i] = cols[rnd.nextInt(cols.length)]; cfEst[i] = rnd.nextFloat() < 0.22f;
        }
    }

    void dibujarConfeti(Canvas c, long ahoraN) {
        if (confetiN == 0) return;
        float t = (ahoraN - confetiNanos) / 1e9f;
        if (t > 3.6f) { confetiN = 0; return; }
        float dt = Math.min(0.05f, (ahoraN - confetiUltimo) / 1e9f);
        confetiUltimo = ahoraN;
        float d = density;
        float alfa = t < 2.9f ? 1f : (3.6f - t) / 0.7f;
        // ORO: flash de luz al principio
        float dFl = confetiTrofeo == 4 ? 0.9f : 0.6f;   // ORO y DIAMANTE
        if (confetiTrofeo >= 3 && t < dFl && confetiN > 0) {
            float k = 1 - t / dFl, rad = (80 + t * (confetiTrofeo == 4 ? 850 : 600)) * d;
            float fx = getWidth() / 2f, fy = getHeight() - 102 * d;
            confetiP.setShader(new android.graphics.RadialGradient(fx, fy, rad, confetiTrofeo == 4 ? Color.argb((int) (190 * k), 200, 250, 255) : Color.argb((int) (170 * k), 255, 230, 140), confetiTrofeo == 4 ? Color.argb(0, 120, 220, 255) : Color.argb(0, 255, 210, 80), android.graphics.Shader.TileMode.CLAMP));
            c.drawCircle(fx, fy, rad, confetiP);
            confetiP.setShader(null);
        }
        for (int i = 0; i < confetiN; i++) {
            cfVy[i] += 600 * d * dt;
            cfVx[i] *= (float) Math.pow(0.35, dt); cfVy[i] *= (float) Math.pow(0.6, dt);
            cfX[i] += cfVx[i] * dt; cfY[i] += cfVy[i] * dt; cfRot[i] += cfVr[i] * dt;
            if (cfY[i] > getHeight() + 30) continue;
            confetiP.setColor(cfCol[i]);
            if (cfEst[i]) {
                float br = 0.5f + 0.5f * (float) Math.sin(t * 14 + cfFase[i]);
                confetiP.setAlpha((int) (255 * alfa * (0.4f + 0.6f * br)));
                estrella(c, cfX[i], cfY[i], cfTam[i] * (0.8f + br * 0.8f), confetiP);
            } else {
                float esc = Math.abs((float) Math.cos(Math.toRadians(cfRot[i] + cfFase[i] * 40)));
                confetiP.setAlpha((int) (255 * alfa));
                c.save(); c.translate(cfX[i], cfY[i]); c.rotate(cfRot[i]);
                float hh = cfTam[i] * 0.3f * (0.25f + esc);
                c.drawRect(-cfTam[i] / 2, -hh, cfTam[i] / 2, hh, confetiP);
                c.restore();
            }
        }
    }

    // destello de 4 puntas
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

    long especialDe; int trofeosTienes;
    static final int[] COLOR_PEDAZO = { Color.rgb(235, 140, 60), Color.rgb(215, 225, 245), Color.rgb(255, 205, 50), Color.rgb(120, 230, 255) };

    // círculo ESPECIAL estilo consola: el aro tiene 3 pedazos, uno por trofeo (se prenden los que tienes)
    // (1.º bronce, 2.º plata, 3.º oro; con el de oro se prende también el 4.º). Los apagados, plateados.
    void circuloEspecial(Canvas c, float cx, float cy, float R, int A, int trofeos, float t, int nuevo) {
        float d = density;
        logroP.setShader(null); logroP.setColorFilter(null); logroP.setStyle(Paint.Style.FILL);
        logroP.setColor(Color.rgb(28, 28, 32)); logroP.setAlpha(A); c.drawCircle(cx, cy, R, logroP);
        float ra = R - 3 * d, grueso = ra * 2 * 0.17f;
        RectF medio = new RectF(cx - ra + grueso / 2, cy - ra + grueso / 2, cx + ra - grueso / 2, cy + ra - grueso / 2);
        int prendidos = Math.min(4, trofeos);
        logroP.setStyle(Paint.Style.STROKE);
        for (int i = 0; i < 4; i++) {
            float ini = 225 + i * 90 + 6, barrido = 90 - 12;   // 4 pedazos, uno por trofeo
            boolean esNuevo = i == nuevo - 1;
            boolean on = i < prendidos && !(esNuevo && t < 1.6f && Math.sin(t * 18) < 0);   // el nuevo prende y apaga al principio
            if (on) {
                float br = esNuevo && t < 2.5f ? 1.6f : 0.7f + 0.3f * (float) Math.sin(t * 4 + i);
                logroP.setStrokeWidth(grueso + 6 * d); logroP.setColor(COLOR_PEDAZO[i]); logroP.setAlpha((int) (A * 0.43f * br));
                c.drawArc(medio, ini, barrido, false, logroP);
                logroP.setStrokeWidth(grueso); logroP.setColor(COLOR_PEDAZO[i]); logroP.setAlpha(A);
                c.drawArc(medio, ini, barrido, false, logroP);
                logroP.setStrokeWidth(grueso * 0.3f); logroP.setColor(Color.WHITE); logroP.setAlpha((int) (A * 0.63f));
                RectF afuera = new RectF(medio); afuera.inset(-grueso * 0.2f, -grueso * 0.2f);
                c.drawArc(afuera, ini + 6, barrido - 12, false, logroP);
            } else {
                logroP.setStrokeWidth(grueso); logroP.setColor(Color.rgb(150, 150, 158)); logroP.setAlpha(A);
                c.drawArc(medio, ini, barrido, false, logroP);
            }
        }
        logroP.setStyle(Paint.Style.FILL);
        float rh = ra * 0.66f;
        logroP.setColor(Color.rgb(40, 40, 46)); logroP.setAlpha(A); c.drawCircle(cx, cy, rh, logroP);
        float re = rh - 2 * d;
        logroP.setShader(new android.graphics.RadialGradient(cx - re * 0.24f, cy - re * 0.36f, re * 1.3f, Color.rgb(250, 250, 252), Color.rgb(150, 152, 160), android.graphics.Shader.TileMode.CLAMP));
        logroP.setAlpha(A); c.drawCircle(cx, cy, re, logroP);
        logroP.setShader(null);
        Bitmap logo = mascaraLogo();
        if (logo != null) {
            float k = Math.min(re * 2 * 0.86f / logo.getWidth(), re * 2 * 0.62f / logo.getHeight());
            float lw = logo.getWidth() * k, lh = logo.getHeight() * k;
            logroP.setColorFilter(new android.graphics.PorterDuffColorFilter(Color.rgb(0, 150, 220), PorterDuff.Mode.SRC_IN));
            logroP.setAlpha(A);
            c.drawBitmap(logo, null, new RectF(cx - lw / 2, cy - lh / 2, cx + lw / 2, cy + lh / 2), logroP);
            logroP.setColorFilter(null);
        }
    }

    void drawLogro(Canvas c) {
        long ahoraN = System.nanoTime();
        // el trofeo que estaba esperando sale cuando ya terminó el aviso anterior
        if (Cheats.trofeoPendiente > 0 && ahoraN >= Cheats.trofeoDesde
                && (Cheats.logroNanos == 0 || (ahoraN - Cheats.logroNanos) / 1e9f > 5.2f)) {
            int n = Cheats.trofeoPendiente;
            Cheats.trofeoPendiente = 0;
            Estadisticas.trofeo(n, false);
        }
        dibujarConfeti(c, ahoraN);
        long t0 = Cheats.logroNanos;
        if (t0 == 0) return;
        float t = (ahoraN - t0) / 1e9f;
        if (t > 5.2f || t < 0) return;
        final float ENTRA = 0.45f, SALE = 4.6f, FIN = 5.2f;
        float d = density;
        float alfa, dy;
        if (t < ENTRA) { float k = 1 - (float) Math.pow(1 - t / ENTRA, 3); alfa = k; dy = 40 * d * (1 - k); }
        else if (t < SALE) { alfa = 1; dy = 0; }
        else { float k = (t - SALE) / (FIN - SALE); alfa = 1 - k; dy = 24 * d * k * k; }
        int A = (int) (245 * Math.max(0f, Math.min(1f, alfa)));
        int tro = Cheats.logroTrofeo;
        // ¿ya tienes al menos un trofeo? -> el círculo sale estilo consola (se revisa una vez por aviso)
        if (especialDe != t0) {
            especialDe = t0;
            trofeosTienes = 0;
            try { int g = Estadisticas.ganados(Estadisticas.totalEsquinas(getContext()), Estadisticas.totalMs(getContext())); for (int n : Estadisticas.LOGROS_PARA_TROFEO) if (g >= n) trofeosTienes++; } catch (Exception ex) { trofeosTienes = 0; }
        }
        int ac = COLOR_TROFEO[Math.max(0, Math.min(3, tro))];
        float h = 64 * d, w = Math.min(getWidth() * 0.92f, 340 * d);
        float x0 = (getWidth() - w) / 2f, y0 = getHeight() - h - 70 * d + dy;
        RectF r = new RectF(x0, y0, x0 + w, y0 + h);
        logroP.setColorFilter(null);
        logroP.setStyle(Paint.Style.FILL);
        // fondo y reflejo de color a la izquierda
        logroP.setShader(new android.graphics.LinearGradient(0, y0, 0, y0 + h, Color.rgb(70, 70, 78), Color.rgb(30, 30, 34), android.graphics.Shader.TileMode.CLAMP));
        logroP.setAlpha(A);
        c.drawRoundRect(r, h / 2, h / 2, logroP);
        logroP.setShader(new android.graphics.LinearGradient(x0, 0, x0 + w * 0.6f, 0, Color.argb(70, Color.red(ac), Color.green(ac), Color.blue(ac)), Color.argb(0, Color.red(ac), Color.green(ac), Color.blue(ac)), android.graphics.Shader.TileMode.CLAMP));
        logroP.setAlpha(A);
        c.drawRoundRect(r, h / 2, h / 2, logroP);
        // BRILLO que cruza la píldora (entre 0.55 y 1.35 s)
        float sb = (t - 0.55f) / 0.8f;
        if (sb > 0 && sb < 1) {
            float bx = x0 - 90 * d + sb * (w + 180 * d);
            c.save();
            Path clip = new Path(); clip.addRoundRect(r, h / 2, h / 2, Path.Direction.CW);
            c.clipPath(clip);
            c.rotate(20, bx, y0 + h / 2);
            logroP.setShader(new android.graphics.LinearGradient(bx - 22 * d, 0, bx + 22 * d, 0,
                new int[] { Color.argb(0, 255, 255, 255), Color.argb(110, 255, 255, 255), Color.argb(0, 255, 255, 255) }, null, android.graphics.Shader.TileMode.CLAMP));
            logroP.setAlpha(A);
            c.drawRect(bx - 22 * d, y0 - h, bx + 22 * d, y0 + 2 * h, logroP);
            c.restore();
        }
        logroP.setShader(null);
        // borde que brilla (fuerte al aparecer y luego "respira")
        float pulso = t < 1.2f ? 1 - t / 1.2f * 0.5f : 0.5f + 0.2f * (float) Math.sin(t * 4);
        logroP.setStyle(Paint.Style.STROKE); logroP.setStrokeWidth(2f * d);
        logroP.setColor(ac); logroP.setAlpha((int) (A * Math.max(0f, Math.min(1f, pulso))));
        c.drawRoundRect(r, h / 2, h / 2, logroP);
        logroP.setStyle(Paint.Style.FILL);

        float cxI = x0 + h / 2, cyI = y0 + h / 2;
        if (tro > 0) {
            // CONFETI: sale cuando el aviso ya casi llegó (una vez por aviso)
            if (confetiDe != t0 && t > 0.25f) lanzarConfeti(t0, tro, x0 + w / 2, getHeight() - h / 2 - 70 * d, w);
            c.save();
            Path pil = new Path(); pil.addRoundRect(r, h / 2, h / 2, Path.Direction.CW);
            c.clipPath(pil);
            // RAYOS de luz que giran detrás del trofeo
            int nr = tro == 4 ? 20 : tro == 3 ? 16 : 12;
            logroP.setColor(ac); logroP.setAlpha((int) (A * (tro >= 3 ? 0.35f : 0.24f)));
            Path rayo = new Path();
            rayo.moveTo(0, 0); rayo.lineTo(150 * d, -9 * d); rayo.lineTo(150 * d, 9 * d); rayo.close();
            for (int i = 0; i < nr; i++) {
                c.save(); c.translate(cxI, cyI); c.rotate(t * 35 + i * 360f / nr);
                c.drawPath(rayo, logroP);
                c.restore();
            }
            // BRILLITOS que parpadean por la píldora
            float[][] pos = { { .27f, .2f }, { .44f, .78f }, { .62f, .18f }, { .77f, .74f }, { .9f, .3f }, { .53f, .46f } };
            for (int i = 0; i < pos.length; i++) {
                float br = 0.5f + 0.5f * (float) Math.sin(t * 6 + i * 1.7f);
                logroP.setColor(i % 2 == 0 ? Color.WHITE : ac); logroP.setAlpha((int) (A * 0.86f * br));
                estrella(c, x0 + w * pos[i][0], y0 + h * pos[i][1], (2.5f + br * 4) * d, logroP);
            }
            c.restore();
        }

        // icono con "pop" (crece de más y regresa) y resplandor detrás
        float pt = Math.max(0f, Math.min(1f, (t - 0.12f) / 0.5f));
        float esc = pt < 0.6f ? 0.55f + pt / 0.6f * 0.6f : 1.15f - (pt - 0.6f) / 0.4f * 0.15f;
        float cx = x0 + h / 2, cy = y0 + h / 2, R = h / 2 - 6 * d;
        c.save();
        c.scale(esc, esc, cx, cy);
        logroP.setShader(new android.graphics.RadialGradient(cx, cy, R + 10 * d,
            Color.argb((int) (150 * Math.max(0.3f, pulso)), Color.red(ac), Color.green(ac), Color.blue(ac)), Color.argb(0, Color.red(ac), Color.green(ac), Color.blue(ac)), android.graphics.Shader.TileMode.CLAMP));
        logroP.setAlpha(A); c.drawCircle(cx, cy, R + 10 * d, logroP);
        logroP.setShader(null);
        // TROFEO: el aro estilo consola con un pedazo prendido por cada trofeo que tienes (el nuevo parpadea)
        if (tro > 0) {
            circuloEspecial(c, cx, cy, R, A, Math.max(tro, trofeosTienes), t, tro);
        } else {
            // aro plateado, centro oscuro y el logo de DVD en azul
            logroP.setShader(new android.graphics.LinearGradient(cx - R, cy - R, cx + R, cy + R, Color.rgb(240, 240, 245), Color.rgb(120, 120, 128), android.graphics.Shader.TileMode.CLAMP));
            logroP.setAlpha(A); c.drawCircle(cx, cy, R, logroP);
            logroP.setShader(new android.graphics.LinearGradient(0, cy - R, 0, cy + R, Color.rgb(60, 60, 66), Color.rgb(18, 18, 22), android.graphics.Shader.TileMode.CLAMP));
            logroP.setAlpha(A); c.drawCircle(cx, cy, R - 4 * d, logroP);
            logroP.setShader(null);
            Bitmap logo = mascaraLogo();
            if (logo != null) {
                float ri = R - 4 * d;
                float k = Math.min(ri * 2 * 0.78f / logo.getWidth(), ri * 2 * 0.6f / logo.getHeight());
                float lw = logo.getWidth() * k, lh = logo.getHeight() * k;
                logroP.setColorFilter(new android.graphics.PorterDuffColorFilter(Color.rgb(0, 191, 255), PorterDuff.Mode.SRC_IN));
                logroP.setAlpha(A);
                c.drawBitmap(logo, null, new RectF(cx - lw / 2, cy - lh / 2, cx + lw / 2, cy + lh / 2), logroP);
                logroP.setColorFilter(null);
            }
        }
        c.restore();
        // textos
        float tx = x0 + h + 4 * d, maxW = x0 + w - tx - h / 3;
        String titulo = tro == 0 ? IdiomaCel.T("Logro desbloqueado") : IdiomaCel.ingles ? new String[] { "Bronze", "Silver", "Gold", "Diamond" }[tro - 1] + " trophy unlocked!" : "¡Trofeo de " + Estadisticas.METALES[tro - 1] + " desbloqueado!";
        logroP.setTypeface(tro == 0 ? Typeface.DEFAULT : Typeface.DEFAULT_BOLD); logroP.setTextAlign(Paint.Align.LEFT);
        logroP.setTextSize(16 * d); logroP.setColor(tro == 0 ? Color.WHITE : ac); logroP.setAlpha(A);
        String t1 = titulo;
        while (t1.length() > 3 && logroP.measureText(t1) > maxW) t1 = t1.substring(0, t1.length() - 2) + "…";
        c.drawText(t1, tx, y0 + h * 0.44f, logroP);
        logroP.setTypeface(Typeface.DEFAULT);
        logroP.setTextSize(13.5f * d); logroP.setColor(Color.rgb(215, 215, 220)); logroP.setAlpha(A);
        String s = IdiomaCel.T(Cheats.logroTexto);
        while (s.length() > 3 && logroP.measureText(s) > maxW) s = s.substring(0, s.length() - 2) + "…";
        c.drawText(s, tx, y0 + h * 0.78f, logroP);
    }

    // Aviso "CHEAT: ..." arriba al centro durante 2.5 s
    void drawCheatMsg(Canvas c) {
        long t0 = Cheats.msgNanos;
        if (t0 == 0) return;
        float t = (System.nanoTime() - t0) / 2.5e9f;
        if (t >= 1f) return;
        int a = (int) (255 * Math.min(1f, (1f - t) * 3f));
        String s = Cheats.msg;
        textPaint.setTypeface(black);
        textPaint.setTextAlign(Paint.Align.CENTER);
        float size = 30 * unidad;
        textPaint.setTextSize(size);
        float tw = textPaint.measureText(s);
        if (tw > getWidth() * 0.92f) { size *= getWidth() * 0.92f / tw; textPaint.setTextSize(size); }
        float x = getWidth() / 2f, y = 60 * density;
        textPaint.setStyle(Paint.Style.STROKE);
        textPaint.setStrokeJoin(Paint.Join.ROUND);
        textPaint.setStrokeWidth(size * 0.4f);
        textPaint.setColor(Color.rgb(255, 200, 0)); textPaint.setAlpha(90 * a / 255);
        c.drawText(s, x, y, textPaint);
        textPaint.setStrokeWidth(size * 0.14f);
        textPaint.setColor(Color.BLACK); textPaint.setAlpha(220 * a / 255);
        c.drawText(s, x, y, textPaint);
        textPaint.setStyle(Paint.Style.FILL);
        textPaint.setColor(Color.rgb(255, 230, 80)); textPaint.setAlpha(a);
        c.drawText(s, x, y, textPaint);
    }

    static void drawBmp(Canvas c, Bitmap b, float x, float y, boolean rotate, float angle, Paint p) {
        if (rotate) {
            c.save();
            c.rotate(angle, x + b.getWidth() / 2f, y + b.getHeight() / 2f);
            c.drawBitmap(b, x, y, p);
            c.restore();
        } else {
            c.drawBitmap(b, x, y, p);
        }
    }

    void checkCache(Config cfg) {
        long modImg = cfg.logoImagen ? Config.archivoImagen(getContext()).lastModified() : 0;
        String key = cfg.estilo + "|" + escala(cfg) + "|" + cfg.logoTexto + "|" + cfg.logoImagen + "|" + modImg + "|" + Cheats.troll
            + (reloj(cfg) ? "|" + horaReloj() : "");   // el reloj se vuelve a dibujar cada segundo
        if (key.equals(cacheKey) && sprites.size() + halos.size() < 120) return;
        cacheKey = key;
        clearCache();
        cargarImagen(cfg);
    }

    // ---- Logo personalizado: imagen ----
    Bitmap imagenLogo;

    void cargarImagen(Config cfg) {
        if (imagenLogo != null) { imagenLogo.recycle(); imagenLogo = null; }
        // código secreto TROLL: el DVD se vuelve la imagen troll (viene dentro de la app)
        if (Cheats.troll) {
            try { imagenLogo = android.graphics.BitmapFactory.decodeResource(getResources(), R.drawable.troll); }
            catch (Exception ignored) { imagenLogo = null; }
            return;
        }
        if (!cfg.logoImagen) return;
        try { imagenLogo = android.graphics.BitmapFactory.decodeFile(Config.archivoImagen(getContext()).getPath()); }
        catch (Exception ignored) { imagenLogo = null; }
    }

    boolean usaImagen() { return imagenLogo != null; }

    // dónde va la imagen dentro de la caja del logo, sin deformarla
    RectF rectImagen(Config cfg) {
        float bw = baseW(cfg), bh = baseH(cfg);
        float k = Math.min(bw / imagenLogo.getWidth(), bh / imagenLogo.getHeight());
        float w = imagenLogo.getWidth() * k, h = imagenLogo.getHeight() * k;
        return new RectF((bw - w) / 2, (bh - h) / 2, (bw + w) / 2, (bh + h) / 2);
    }

    static String textoLogo(Config cfg) {
        return (cfg.logoTexto == null || cfg.logoTexto.trim().isEmpty()) ? "DVD" : cfg.logoTexto;
    }

    void clearCache() {
        for (Bitmap b : sprites.values()) b.recycle();
        for (Bitmap b : halos.values()) b.recycle();
        sprites.clear(); halos.clear();
        if (preSprite != null) { preSprite.recycle(); preHaloColor.recycle(); preHaloBlanco.recycle(); preSprite = null; }
    }

    // ---- Logo de la imagen: se prepara UNA vez en blanco (al tamaño de pantalla) y luego solo se tiñe ----
    // Antes se redibujaba por cada color nuevo y con el arcoíris de las esquinas se trababa.
    Bitmap preSprite, preHaloColor, preHaloBlanco;

    boolean usaMascara(Config cfg) { return !usaImagen() && !disco(cfg) && !reloj(cfg) && "DVD".equals(textoLogo(cfg)) && mascaraLogo() != null; }

    void prepararMascara(Config cfg) {
        if (preSprite != null) return;
        Bitmap m = mascaraLogo();
        float sc = escala(cfg);
        RectF r = rectMascara(m);
        Paint pm = new Paint(Paint.FILTER_BITMAP_FLAG | Paint.ANTI_ALIAS_FLAG);
        preSprite = Bitmap.createBitmap(Math.max(1, (int) Math.ceil(W(cfg)) + 1), Math.max(1, (int) Math.ceil(H(cfg)) + 1), Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(preSprite);
        c.scale(sc, sc);
        c.drawBitmap(m, null, r, pm);
        int hw = (int) Math.ceil((baseW(cfg) + PAD * 2) * sc), hh = (int) Math.ceil((baseH(cfg) + PAD * 2) * sc);
        preHaloColor = Bitmap.createBitmap(hw, hh, Bitmap.Config.ARGB_8888);
        preHaloBlanco = Bitmap.createBitmap(hw, hh, Bitmap.Config.ARGB_8888);
        for (int capa = 0; capa < 2; capa++) {
            Canvas hc = new Canvas(capa == 0 ? preHaloColor : preHaloBlanco);
            hc.scale(sc, sc);
            hc.translate(PAD, PAD);
            pm.setAlpha(18);
            for (int anillo = capa == 0 ? 6 : 3; anillo >= (capa == 0 ? 4 : 1); anillo--) {
                float radio = anillo * 7f;
                for (int k = 0; k < 16; k++) {
                    double a = k * Math.PI / 8;
                    RectF rr = new RectF(r); rr.offset((float) (Math.cos(a) * radio), (float) (Math.sin(a) * radio));
                    hc.drawBitmap(m, null, rr, pm);
                }
            }
            if (capa == 1) { pm.setAlpha(150); hc.drawBitmap(m, null, r, pm); }
        }
    }

    // copia algo ya preparado pintándolo de un color (casi instantáneo)
    static Bitmap tenir(Bitmap src, int color) {
        Bitmap b = Bitmap.createBitmap(src.getWidth(), src.getHeight(), Bitmap.Config.ARGB_8888);
        Paint p = new Paint();
        p.setColorFilter(new android.graphics.PorterDuffColorFilter(color, PorterDuff.Mode.SRC_IN));
        new Canvas(b).drawBitmap(src, 0, 0, p);
        return b;
    }

    // ---- SKIN de la tienda: se revisa cada 2 s (si cambia, se vuelven a preparar los dibujos) ----
    String skinCache; long skinRevisado; boolean skinLeida;
    String skinActiva() {
        long ahora = System.currentTimeMillis();
        if (!skinLeida || ahora - skinRevisado > 2000) {
            skinRevisado = ahora;
            String s = null;
            try { s = Estadisticas.skinActiva(getContext()); } catch (Exception e) { }
            if (skinLeida && !java.util.Objects.equals(s, skinCache)) { sprites.clear(); halos.clear(); }
            skinCache = s; skinLeida = true;
        }
        return usaImagen() ? null : skinCache;
    }

    Bitmap getSprite(int color, Config cfg) {
        String skin = skinActiva();   // con skin el logo es de metal (no cambia de color)
        if (skin != null) color = Color.WHITE;
        int clave = usaImagen() ? 0 : skin != null ? 1 : color;   // la imagen no cambia de color
        Bitmap b = sprites.get(clave);
        if (b != null) return b;
        if (usaMascara(cfg)) {
            prepararMascara(cfg);
            b = tenir(preSprite, color);
            if (skin != null) Estadisticas.metalizar(b, skin);
            sprites.put(clave, b);
            return b;
        }
        float sc = escala(cfg);
        b = Bitmap.createBitmap(Math.max(1, (int) Math.ceil(W(cfg)) + 1), Math.max(1, (int) Math.ceil(H(cfg)) + 1), Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(b);
        c.scale(sc, sc);
        if (usaImagen()) c.drawBitmap(imagenLogo, null, rectImagen(cfg), new Paint(Paint.FILTER_BITMAP_FLAG));
        else if (disco(cfg)) drawDisc(c, color);
        else if (reloj(cfg)) { Paint pr = new Paint(Paint.ANTI_ALIAS_FLAG); pr.setColor(color); c.drawPath(relojForma(), pr); }
        else drawDvdLogo(c, color);
        if (skin != null) Estadisticas.metalizar(b, skin);
        sprites.put(clave, b);
        return b;
    }

    // Brillo igual que en Windows: contornos anchos del logo (color por fuera, blanco por dentro)
    Bitmap getHalo(int color, Config cfg) {
        Bitmap b = halos.get(color);
        if (b != null) return b;
        if (usaMascara(cfg)) {
            // brillo con la forma del logo: la parte de afuera teñida del color + la parte blanca de adentro
            prepararMascara(cfg);
            b = tenir(preHaloColor, color);
            new Canvas(b).drawBitmap(preHaloBlanco, 0, 0, null);
            halos.put(color, b);
            return b;
        }
        float sc = escala(cfg);
        b = Bitmap.createBitmap((int) Math.ceil((baseW(cfg) + PAD * 2) * sc), (int) Math.ceil((baseH(cfg) + PAD * 2) * sc), Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(b);
        c.scale(sc, sc);
        c.translate(PAD, PAD);
        Bitmap m = (!usaImagen() && !disco(cfg) && "DVD".equals(textoLogo(cfg))) ? mascaraLogo() : null;
        if (m != null) {
            // brillo con la forma del logo de la imagen: copias del logo corridas en círculos,
            // de color por fuera y blancas por dentro, muy transparentes
            RectF r = rectMascara(m);
            Paint pm = new Paint(Paint.FILTER_BITMAP_FLAG);
            for (int anillo = 6; anillo >= 1; anillo--) {
                float radio = anillo * 7f;
                pm.setColorFilter(new android.graphics.PorterDuffColorFilter(anillo > 3 ? color : Color.WHITE, PorterDuff.Mode.SRC_IN));
                pm.setAlpha(18);
                for (int k = 0; k < 16; k++) {
                    double a = k * Math.PI / 8;
                    RectF rr = new RectF(r); rr.offset((float) (Math.cos(a) * radio), (float) (Math.sin(a) * radio));
                    c.drawBitmap(m, null, rr, pm);
                }
            }
            pm.setColorFilter(new android.graphics.PorterDuffColorFilter(Color.WHITE, PorterDuff.Mode.SRC_IN));
            pm.setAlpha(150);
            c.drawBitmap(m, null, r, pm);
            halos.put(color, b);
            return b;
        }
        Path shape = shapePath(cfg);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeJoin(Paint.Join.ROUND);
        for (int i = 9; i >= 1; i--) {
            p.setColor(i > 5 ? color : Color.WHITE);
            p.setAlpha(55);
            p.setStrokeWidth(i * 4.5f);
            c.drawPath(shape, p);
        }
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.WHITE); p.setAlpha(150);
        c.drawPath(shape, p);
        halos.put(color, b);
        return b;
    }

    Path shapePath(Config cfg) {
        Path p = new Path();
        if (usaImagen()) { p.addRect(rectImagen(cfg), Path.Direction.CW); return p; }
        if (disco(cfg)) { p.addOval(new RectF(0, 0, 150, 150), Path.Direction.CW); return p; }
        if (reloj(cfg)) return relojForma();   // el brillo con la forma de la hora
        p.addPath(lettersPath());
        discoConPuntas(p, discRect());
        // ™ chiquita y escondida arriba a la derecha (como en el logo clásico)
        Paint tm = new Paint(Paint.ANTI_ALIAS_FLAG);
        tm.setTypeface(Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD));
        tm.setTextSize(8);
        Path tmPath = new Path();
        tm.getTextPath("TM", 0, 2, 220 - 15, 8, tmPath);
        p.addPath(tmPath);
        return p;
    }

    // Disco visto de lado: delgado y más ancho que las letras
    static RectF discRect() { return new RectF(2, 80, 220 - 2, 104); }

    // Letras anchas, planas e inclinadas (misma transformación que en Windows). Usa el texto del logo.
    Path lettersPath() {
        String texto = textoLogo(Config.actual);
        boolean clasico = "DVD".equals(texto);
        Path letters = new Path();
        if (clasico) letrasDvdDibujadas(letters);   // las del logo clásico, dibujadas a mano
        else {
            Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
            p.setTypeface(black);
            p.setTextSize(100);
            p.getTextPath(texto, 0, texto.length(), 0, 0, letters);
        }
        RectF lb = new RectF();
        letters.computeBounds(lb, true);
        if (lb.width() < 1 || lb.height() < 1) return letters;
        float ancho = 220 - 12, alto = 74;
        float sx = ancho * 0.88f / lb.width();
        float sy = Math.min(alto / lb.height(), sx * 1.6f);   // textos largos: no se aplastan demasiado
        if (clasico) { sx = (220 - 22) / lb.width(); sy = 60f / lb.height(); }   // las del logo clásico son muy anchas y bajitas
        float h = lb.height() * sy, w = lb.width() * sx;
        float x0 = 6 + (ancho * 0.88f - w) / 2, y0 = (clasico ? 77 : alto) - h;
        Matrix m = new Matrix();
        m.setTranslate(x0, y0);
        m.preSkew(-0.18f, 0);                       // inclinación hacia la derecha
        m.preTranslate(h * 0.18f, 0);
        m.preScale(sx, sy);
        m.preTranslate(-lb.left, -lb.top);
        letters.transform(m);
        return letters;
    }

    // "DVD" como en el logo clásico (igual que en Windows): cada D es un palito delgado con una panza
    // redonda grande con hueco, y la V es una cuña delgada entre las dos D. Alto 100.
    static void letrasDvdDibujadas(Path p) {
        letraD(p, 0, 150);
        // V: brazo izquierdo grueso, brazo derecho delgado, punta abajo bien afilada
        p.moveTo(140, 0); p.lineTo(178, 0); p.lineTo(205, 70); p.lineTo(240, 0);
        p.lineTo(258, 0); p.lineTo(212, 100); p.lineTo(196, 100); p.close();
        letraD(p, 225, 150);
    }

    // D del logo clásico: lado izquierdo muy inclinado, panza redonda y ranura larga acostada
    static void letraD(Path p, float x0, float w) {
        final float inclina = 0.30f, grueso = 22, panza = 24;
        float c = x0 + w * 0.45f, rx = w * 0.55f;
        // contorno de afuera (sentido del reloj)
        p.moveTo(x0 + 100 * inclina, 0);
        p.lineTo(c, 0);
        p.arcTo(new RectF(c - rx, 0, c + rx, 100), -90, 180, false);
        p.lineTo(x0, 100);
        p.close();
        // ranura de adentro (al revés, para que quede vacía), paralela al lado inclinado
        float arriba = 22, abajo = 78, irx = rx - panza;
        float izqArriba = x0 + (100 - arriba) * inclina + grueso;
        float izqAbajo = x0 + (100 - abajo) * inclina + grueso;
        p.moveTo(izqAbajo, abajo);
        p.lineTo(c, abajo);
        p.arcTo(new RectF(c - irx, arriba, c + irx, abajo), 90, -180, false);
        p.lineTo(izqArriba, arriba);
        p.close();
    }

    // Disco visto de lado con las puntas afiladas (como un ojo)
    static void discoConPuntas(Path p, RectF r) {
        float cy = r.centerY(), k = r.height() / 2 / 0.75f;
        p.moveTo(r.left, cy);
        p.cubicTo(r.left + r.width() * 0.22f, cy - k, r.right - r.width() * 0.22f, cy - k, r.right, cy);
        p.cubicTo(r.right - r.width() * 0.22f, cy + k, r.left + r.width() * 0.22f, cy + k, r.left, cy);
        p.close();
    }

    // Logo "DVD" sacado de la imagen que mandaste (res/drawable-nodpi/logo_dvd.png): blanco con
    // transparencia, así se puede pintar de cualquier color.
    Bitmap mascaraLogo;
    boolean mascaraCargada;

    Bitmap mascaraLogo() {
        if (!mascaraCargada) {
            mascaraCargada = true;
            try { mascaraLogo = android.graphics.BitmapFactory.decodeResource(getResources(), R.drawable.logo_dvd); }
            catch (Exception e) { mascaraLogo = null; }
        }
        return mascaraLogo;
    }

    static RectF rectMascara(Bitmap m) {
        float k = Math.min(220f / m.getWidth(), 110f / m.getHeight());
        float w = m.getWidth() * k, h = m.getHeight() * k;
        return new RectF((220 - w) / 2, (110 - h) / 2, (220 + w) / 2, (110 + h) / 2);
    }

    void drawDvdLogo(Canvas c, int color) {
        Bitmap m = "DVD".equals(textoLogo(Config.actual)) ? mascaraLogo() : null;
        if (m != null) {
            // se acomoda en el espacio del logo (220x110) sin deformarse, y se pinta del color actual
            RectF r = rectMascara(m);
            Paint pm = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
            pm.setColorFilter(new android.graphics.PorterDuffColorFilter(color, PorterDuff.Mode.SRC_IN));
            c.drawBitmap(m, null, r, pm);
            return;
        }
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(color);
        c.drawPath(shapePath(Config.actual), p);
        // "V I D E O" recortado (transparente) dentro del disco
        RectF disc = discRect();
        Paint clear = new Paint(Paint.ANTI_ALIAS_FLAG);
        clear.setXfermode(new PorterDuffXfermode(PorterDuff.Mode.CLEAR));
        clear.setTypeface(Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD));
        clear.setTextSize(19);
        clear.setTextAlign(Paint.Align.CENTER);
        Paint.FontMetrics fm = clear.getFontMetrics();
        c.drawText("V I D E O", disc.centerX(), disc.centerY() - (fm.ascent + fm.descent) / 2f, clear);
    }

    void drawDisc(Canvas c, int color) {
        RectF outer = new RectF(0, 0, 150, 150);
        float cx = 75, cy = 75;
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(blend(color, Color.rgb(225, 230, 240), 0.55f));
        c.drawOval(outer, p);
        int[] hues = {Color.RED, 0xFFFFA500, Color.YELLOW, 0xFF00FF00, Color.CYAN, Color.BLUE, Color.MAGENTA};
        for (int i = 0; i < 14; i++) {
            p.setColor(hues[i % hues.length]);
            p.setAlpha(i % 2 == 0 ? 70 : 25);
            c.drawArc(outer, i * (360f / 14), 360f / 14, true, p);
        }
        p.setColor(Color.WHITE); p.setAlpha(50);
        c.drawArc(outer, 300, 25, true, p);
        Paint ring = new Paint(Paint.ANTI_ALIAS_FLAG);
        ring.setStyle(Paint.Style.STROKE);
        ring.setStrokeWidth(1.5f);
        ring.setColor(Color.argb(90, 255, 255, 255));
        for (float r = 30; r < cx - 4; r += 9) c.drawCircle(cx, cy, r, ring);
        p.setColor(Color.argb(200, 240, 240, 245));
        c.drawCircle(cx, cy, 24, p);
        ring.setColor(Color.argb(150, 0, 0, 0)); ring.setStrokeWidth(2.5f);
        c.drawOval(outer, ring);

        Paint t = new Paint(Paint.ANTI_ALIAS_FLAG);
        t.setTypeface(Typeface.create(black, Typeface.ITALIC));
        t.setTextSkewX(-0.2f);
        t.setTextAlign(Paint.Align.CENTER);
        t.setTextSize(34);
        String texto = textoLogo(Config.actual);
        float tw = t.measureText(texto);
        if (tw > 110) t.setTextSize(34 * 110 / tw);          // textos largos se achican para caber
        Paint.FontMetrics fm = t.getFontMetrics();
        float base = 38 - (fm.ascent + fm.descent) / 2f;   // centrado en la franja 18..58 como en Windows
        t.setStyle(Paint.Style.STROKE); t.setStrokeWidth(3); t.setColor(Color.argb(160, 0, 0, 0));
        c.drawText(texto, cx, base, t);
        t.setStyle(Paint.Style.FILL); t.setColor(color);
        c.drawText(texto, cx, base, t);

        Paint clear = new Paint(Paint.ANTI_ALIAS_FLAG);
        clear.setXfermode(new PorterDuffXfermode(PorterDuff.Mode.CLEAR));
        c.drawCircle(cx, cy, 10, clear);
    }

    // "¡ESQUINA! #N" en el centro, igual que en Windows (glow de color, borde negro, relleno blanco)
    void drawMessage(Canvas c, Config cfg, double now) {
        if (!(now < msgUntil) || !cfg.mostrarMensajeEsquina) return;
        float t = (float) ((now - msgStart) / (msgUntil - msgStart));
        int a = (int) (255 * Math.min(1f, (1f - t) * 1.6f));
        float pop = 1f + 0.25f * Math.max(0, 1 - t * 6);

        String key = total + "|" + ultima;
        if (!key.equals(msgPathText)) {
            msgPathText = key;
            msgPath = new Path();
            Paint tp = new Paint(Paint.ANTI_ALIAS_FLAG);
            tp.setTypeface(black);
            tp.setTextAlign(Paint.Align.CENTER);
            tp.setTextSize(70);
            Path a1 = new Path();
            tp.getTextPath("¡ESQUINA! #" + total, 0, ("¡ESQUINA! #" + total).length(), 0, -25, a1);
            tp.setTypeface(Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD));
            tp.setTextSize(30);
            Path a2 = new Path();
            tp.getTextPath(ultima, 0, ultima.length(), 0, 30, a2);
            msgPath.addPath(a1);
            msgPath.addPath(a2);
        }
        RectF bounds = new RectF();
        msgPath.computeBounds(bounds, true);
        float k = Math.min(unidad * 1.2f, getWidth() * 0.9f / Math.max(1, bounds.width())) * pop;

        c.save();
        c.translate(getWidth() / 2f, getHeight() / 2f);
        c.scale(k, k);
        textPaint.setStyle(Paint.Style.STROKE);
        textPaint.setStrokeJoin(Paint.Join.ROUND);
        textPaint.setColor(msgColor);
        for (int i = 6; i >= 1; i--) {
            textPaint.setAlpha(40 * a / 255);
            textPaint.setStrokeWidth(i * 5);
            c.drawPath(msgPath, textPaint);
        }
        textPaint.setColor(Color.BLACK); textPaint.setAlpha(200 * a / 255); textPaint.setStrokeWidth(5);
        c.drawPath(msgPath, textPaint);
        textPaint.setStyle(Paint.Style.FILL);
        textPaint.setColor(Color.WHITE); textPaint.setAlpha(a);
        c.drawPath(msgPath, textPaint);
        c.restore();
    }

    // Panel "Esquinas tocadas" arriba a la izquierda, igual que en Windows (solo al tocar esquina)
    void drawHud(Canvas c, Config cfg, double now) {
        if (!(now < msgUntil) || !cfg.mostrarContador) return;
        float t = (float) ((now - msgStart) / (msgUntil - msgStart));
        int a = (int) (255 * Math.min(1f, (1f - t) * 2f));
        float k = density * 0.75f;
        c.save();
        c.translate(0, 16 * density);   // debajo de la barra de estado
        c.scale(k, k);
        RectF r = new RectF(21, 21, 21 + 277, 21 + 162);
        panelPaint.setStyle(Paint.Style.FILL);
        panelPaint.setColor(Color.rgb(15, 15, 20)); panelPaint.setAlpha(170 * a / 255);
        c.drawRoundRect(r, 14, 14, panelPaint);
        panelPaint.setStyle(Paint.Style.STROKE); panelPaint.setStrokeWidth(2);
        panelPaint.setColor(msgColor); panelPaint.setAlpha(120 * a / 255);
        c.drawRoundRect(r, 14, 14, panelPaint);

        textPaint.setStyle(Paint.Style.FILL);
        textPaint.setTextAlign(Paint.Align.LEFT);
        textPaint.setTypeface(Typeface.DEFAULT_BOLD);
        textPaint.setTextSize(22);
        textPaint.setColor(Color.WHITE); textPaint.setAlpha(a);
        c.drawText("Esquinas tocadas: " + total, 32, 28 + 24, textPaint);
        textPaint.setTypeface(Typeface.DEFAULT);
        textPaint.setTextSize(14);
        textPaint.setColor(Color.rgb(220, 220, 220)); textPaint.setAlpha(a);
        String[] lines = {
            "Superior izquierda:  " + supIzq, "Superior derecha:    " + supDer,
            "Inferior izquierda:  " + infIzq, "Inferior derecha:    " + infDer, "Última: " + ultima};
        for (int i = 0; i < lines.length; i++) c.drawText(lines[i], 34, 66 + 14 + i * 18.6f, textPaint);
        c.restore();
    }

    static int blend(int a, int b, float k) {
        return Color.rgb(
            (int) (Color.red(a) + (Color.red(b) - Color.red(a)) * k),
            (int) (Color.green(a) + (Color.green(b) - Color.green(a)) * k),
            (int) (Color.blue(a) + (Color.blue(b) - Color.blue(a)) * k));
    }
}
