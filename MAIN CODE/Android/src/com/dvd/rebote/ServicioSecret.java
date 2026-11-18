package com.dvd.rebote;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.StateListDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.Random;

// Código secreto SECRET en el cel: la ventana "NO ESCAPE" y los cuadros de "Error" se dibujan ENCIMA de
// todas las apps (como los DVDs), así siguen en pantalla y funcionando aunque te salgas de la app.
// Igual que en la PC: no se puede cerrar con la X (sale "Error"); cada "Error" que cierras saca 5 más
// (con tope); al décimo cierre sale el mensaje maldito; con "Aceptar" se cierra todo y salen 500 DVDs.
public class ServicioSecret extends Service {
    static volatile boolean abierto = false;
    static final String CANAL = "dvd_secret";
    static final int MAX_AVISOS = 31;   // 1 + 5 + 25

    WindowManager wm;
    final Handler handler = new Handler(Looper.getMainLooper());
    final Random rnd = new Random();
    View ventana, pregunta;
    final ArrayList<View> avisos = new ArrayList<>();
    int avisosCreados, cierresAvisos;   // avisosCreados: los que salieron en esta ronda (tope MAX_AVISOS)
    boolean cerrandoTodo;

    @Override public IBinder onBind(Intent intent) { return null; }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        enPrimerPlano();
        if (!Settings.canDrawOverlays(this)) { stopSelf(); return START_NOT_STICKY; }
        if (wm == null) wm = getSystemService(WindowManager.class);
        if (ventana == null && pregunta == null) preguntar();   // primero "¿Quieres ejecutar esto?"
        abierto = true;
        return START_NOT_STICKY;
    }

    @Override
    public void onDestroy() {
        cerrarTodo();
        abierto = false;
        super.onDestroy();
    }

    void enPrimerPlano() {
        NotificationManager nm = getSystemService(NotificationManager.class);
        if (nm.getNotificationChannel(CANAL) == null) {
            NotificationChannel ch = new NotificationChannel(CANAL, "Error", NotificationManager.IMPORTANCE_LOW);
            nm.createNotificationChannel(ch);
        }
        Notification n = new Notification.Builder(this, CANAL)
            .setSmallIcon(R.drawable.ic_notif)
            .setContentTitle(MainActivity.maldecir("Error", 2, 4))
            .setContentText(MainActivity.maldecir("No escape", 2, 4))
            .setOngoing(true)
            .build();
        if (Build.VERSION.SDK_INT >= 34) startForeground(2, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(2, n);
    }

    // ---------- ayudas ----------
    int dp(float v) { return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics()); }

    TextView texto(String s, float sp, int color) {
        TextView t = new TextView(this);
        t.setText(s); t.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp); t.setTextColor(color);
        return t;
    }

    WindowManager.LayoutParams parametros(int ancho, int alto) {
        WindowManager.LayoutParams lp = new WindowManager.LayoutParams(ancho, alto,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
            PixelFormat.TRANSLUCENT);
        lp.gravity = Gravity.CENTER;
        return lp;
    }

    // Barra de título como las de Windows: título chiquito, la X a la derecha (roja al tocarla),
    // y si la arrastras con el dedo mueves la ventana.
    View barraTitulo(String titulo, int colorFondo, Runnable alCerrar, View raiz, WindowManager.LayoutParams lp) {
        LinearLayout barra = new LinearLayout(this);
        barra.setOrientation(LinearLayout.HORIZONTAL);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(colorFondo);
        TextView t = texto(titulo, 12, Color.rgb(30, 30, 30));
        t.setSingleLine(true);
        t.setPadding(dp(12), 0, 0, 0);
        barra.addView(t, new LinearLayout.LayoutParams(0, -2, 1f));
        TextView x = texto("✕", 14, Color.rgb(30, 30, 30));
        x.setGravity(Gravity.CENTER);
        StateListDrawable alTocar = new StateListDrawable();
        alTocar.addState(new int[] { android.R.attr.state_pressed }, new ColorDrawable(Color.rgb(232, 17, 35)));
        alTocar.addState(new int[0], new ColorDrawable(Color.TRANSPARENT));
        x.setBackground(alTocar);
        x.setOnTouchListener((v, ev) -> {
            if (ev.getAction() == MotionEvent.ACTION_DOWN) x.setTextColor(Color.WHITE);
            else if (ev.getAction() == MotionEvent.ACTION_UP || ev.getAction() == MotionEvent.ACTION_CANCEL) x.setTextColor(Color.rgb(30, 30, 30));
            return false;
        });
        x.setOnClickListener(v -> alCerrar.run());
        barra.addView(x, new LinearLayout.LayoutParams(dp(46), -1));
        float[] ultimo = new float[2];
        barra.setOnTouchListener((v, ev) -> {
            switch (ev.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    ultimo[0] = ev.getRawX(); ultimo[1] = ev.getRawY();
                    return true;
                case MotionEvent.ACTION_MOVE:
                    lp.x += (int) (ev.getRawX() - ultimo[0]);
                    lp.y += (int) (ev.getRawY() - ultimo[1]);
                    ultimo[0] = ev.getRawX(); ultimo[1] = ev.getRawY();
                    try { wm.updateViewLayout(raiz, lp); } catch (Exception ignored) { }
                    return true;
            }
            return true;
        });
        return barra;
    }

    // la ventana tiembla todo el tiempo (poquito) y de repente da un jalón fuerte; la estática parpadea
    void temblar(View raiz, View estatica) {
        float dens = getResources().getDisplayMetrics().density;
        Runnable[] paso = new Runnable[1];
        paso[0] = () -> {
            if (raiz.getParent() == null) return;   // ya se cerró
            if (estatica != null) estatica.setAlpha(0.32f + rnd.nextFloat() * 0.28f);
            if (rnd.nextInt(25) == 0) {
                raiz.setTranslationX((rnd.nextInt(33) - 16) * dens);
                raiz.setTranslationY((rnd.nextInt(19) - 9) * dens);
            } else {
                raiz.setTranslationX((rnd.nextInt(9) - 4) * dens);
                raiz.setTranslationY((rnd.nextInt(7) - 3) * dens);
            }
            handler.postDelayed(paso[0], 40);
        };
        handler.post(paso[0]);
    }

    // ---------- antes de todo: "¿Quieres ejecutar esto?" (con estática) ----------
    // Como el cuadro de mensaje de Windows: el escudo, el texto y los botones "Sí" y "No".
    // No tiene X: solo se va eligiendo una opción, y con las dos sigue igual (sale "NO ESCAPE").
    void preguntar() {
        int ancho = Math.min(dp(320), getResources().getDisplayMetrics().widthPixels - dp(40));
        WindowManager.LayoutParams lp = parametros(ancho, WindowManager.LayoutParams.WRAP_CONTENT);
        LinearLayout caja = new LinearLayout(this);
        caja.setOrientation(LinearLayout.VERTICAL);
        GradientDrawable borde = new GradientDrawable();
        borde.setColor(Color.WHITE); borde.setStroke(Math.max(1, dp(1)), Color.rgb(170, 170, 170)); borde.setCornerRadius(dp(6));
        caja.setBackground(borde);
        caja.setClipToOutline(true);

        // la raíz se crea primero para que la barra de título la pueda mover
        FrameLayout raiz = new FrameLayout(this);

        // barra de título como la de Windows: "Run" y la X en gris (desactivada, como en la PC: no cierra)
        LinearLayout barraT = new LinearLayout(this);
        barraT.setOrientation(LinearLayout.HORIZONTAL);
        barraT.setGravity(Gravity.CENTER_VERTICAL);
        // el escudo de admin chiquito en el título, como el icono de las ventanas de Windows
        ImageView escudoT = new ImageView(this);
        escudoT.setImageResource(R.drawable.escudo);
        LinearLayout.LayoutParams lpEsc = new LinearLayout.LayoutParams(dp(16), dp(16));
        lpEsc.leftMargin = dp(9);
        barraT.addView(escudoT, lpEsc);
        TextView titulo = texto("Run", 12, Color.rgb(30, 30, 30));
        titulo.setSingleLine(true);
        titulo.setPadding(dp(7), 0, 0, 0);
        barraT.addView(titulo, new LinearLayout.LayoutParams(0, -2, 1f));
        TextView xGris = texto("✕", 14, Color.rgb(190, 190, 190));
        xGris.setGravity(Gravity.CENTER);
        barraT.addView(xGris, new LinearLayout.LayoutParams(dp(46), -1));
        // arrastrar la barra mueve la ventana
        float[] ultimo = new float[2];
        barraT.setOnTouchListener((v, ev) -> {
            switch (ev.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    ultimo[0] = ev.getRawX(); ultimo[1] = ev.getRawY();
                    return true;
                case MotionEvent.ACTION_MOVE:
                    lp.x += (int) (ev.getRawX() - ultimo[0]);
                    lp.y += (int) (ev.getRawY() - ultimo[1]);
                    ultimo[0] = ev.getRawX(); ultimo[1] = ev.getRawY();
                    try { wm.updateViewLayout(raiz, lp); } catch (Exception ignored) { }
                    return true;
            }
            return true;
        });
        caja.addView(barraT, new LinearLayout.LayoutParams(-1, dp(32)));

        LinearLayout cuerpo = new LinearLayout(this);
        cuerpo.setOrientation(LinearLayout.HORIZONTAL);
        cuerpo.setGravity(Gravity.CENTER_VERTICAL);
        cuerpo.setPadding(dp(20), dp(18), dp(20), dp(22));
        ImageView escudo = new ImageView(this);
        escudo.setImageResource(R.drawable.escudo);   // tu escudo
        escudo.setScaleType(ImageView.ScaleType.FIT_CENTER);
        cuerpo.addView(escudo, new LinearLayout.LayoutParams(dp(32), dp(32)));
        TextView txt = texto("¿Quieres ejecutar esto?", 14, Color.BLACK);
        txt.setPadding(dp(14), 0, 0, 0);
        cuerpo.addView(txt, new LinearLayout.LayoutParams(0, -2, 1f));
        caja.addView(cuerpo);

        LinearLayout abajo = new LinearLayout(this);
        abajo.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        abajo.setBackgroundColor(Color.rgb(240, 240, 240));
        abajo.setPadding(dp(12), dp(10), dp(12), dp(10));
        for (String nombre : new String[] { "Sí", "No" }) {
            TextView b = texto(nombre, 13, Color.BLACK);
            b.setGravity(Gravity.CENTER);
            GradientDrawable fondo = new GradientDrawable();
            fondo.setColor(Color.rgb(253, 253, 253));
            fondo.setStroke(Math.max(1, dp(1)), nombre.equals("Sí") ? Color.rgb(0, 103, 192) : Color.rgb(170, 170, 170));
            fondo.setCornerRadius(dp(4));
            b.setBackground(fondo);
            b.setOnClickListener(v -> {
                // Sí o No: sigue igual
                if (pregunta != null) { try { wm.removeView(pregunta); } catch (Exception ignored) { } pregunta = null; }
                mostrarVentana();
            });
            LinearLayout.LayoutParams bl = new LinearLayout.LayoutParams(dp(80), dp(30));
            bl.leftMargin = dp(8);
            abajo.addView(b, bl);
        }
        caja.addView(abajo, new LinearLayout.LayoutParams(-1, -2));

        // estática encima de todo el cuadro (no agarra los toques: "Sí", "No" y arrastrar siguen funcionando)
        raiz.addView(caja, new FrameLayout.LayoutParams(-1, -2));
        MainActivity.Estatica est = new MainActivity.Estatica(this);
        est.setAlpha(0.30f);   // estática a medias (más suave que en los Error)
        raiz.addView(est, new FrameLayout.LayoutParams(-1, -1));
        try { wm.addView(raiz, lp); } catch (Exception e) { stopSelf(); return; }
        pregunta = raiz;
    }

    // ---------- la ventana "NO ESCAPE" ----------
    void mostrarVentana() {
        int ancho = Math.min(dp(360), getResources().getDisplayMetrics().widthPixels - dp(32));
        int altoTitulo = dp(32), altoCuerpo = dp(112), altoAbajo = dp(54);
        WindowManager.LayoutParams lp = parametros(ancho, altoTitulo + altoCuerpo + altoAbajo);

        FrameLayout raiz = new FrameLayout(this);
        GradientDrawable borde = new GradientDrawable();
        borde.setColor(Color.rgb(200, 200, 200)); borde.setStroke(Math.max(1, dp(1)), Color.rgb(150, 150, 150)); borde.setCornerRadius(dp(6));
        raiz.setBackground(borde);
        raiz.setClipToOutline(true);
        raiz.addView(new MainActivity.Estatica(this), new FrameLayout.LayoutParams(-1, -1));

        LinearLayout todo = new LinearLayout(this);
        todo.setOrientation(LinearLayout.VERTICAL);
        LinearLayout barraNE = (LinearLayout) barraTitulo("NO ESCAPE", Color.argb(200, 255, 255, 255), () -> aviso(false), raiz, lp);
        // icono de la consola (imageres,263) en el título, como en la PC
        ImageView iconoCmd = new ImageView(this);
        iconoCmd.setImageResource(R.drawable.icono_cmd);
        LinearLayout.LayoutParams lpCmd = new LinearLayout.LayoutParams(dp(16), dp(16));
        lpCmd.leftMargin = dp(9);
        barraNE.addView(iconoCmd, 0, lpCmd);
        // la X de cerrar siempre roja con la X blanca (como en la PC al pasar el mouse)
        TextView xNE = (TextView) barraNE.getChildAt(barraNE.getChildCount() - 1);
        xNE.setBackgroundColor(Color.rgb(196, 43, 28));
        xNE.setTextColor(Color.WHITE);
        xNE.setOnTouchListener((v, ev) -> false);
        todo.addView(barraNE, new LinearLayout.LayoutParams(-1, altoTitulo));

        LinearLayout fila = new LinearLayout(this);
        fila.setOrientation(LinearLayout.HORIZONTAL);
        fila.setGravity(Gravity.CENTER_VERTICAL);
        fila.setPadding(dp(22), 0, dp(12), 0);
        ImageView icono = new ImageView(this);
        icono.setImageResource(R.drawable.icono_error);
        icono.setScaleType(ImageView.ScaleType.FIT_CENTER);
        fila.addView(icono, new LinearLayout.LayoutParams(dp(30), dp(30)));
        TextView msg = texto("N̶̨̘̲̫̲͌ͦ̂͆̽̽̊Ȯ̧̗̝̹̮͍͔̟̔ Ȅ̶̪͔̤͍͇Ş͖̺̋C̵͊̋Ā̟̥̣͓̪̓̽͆̽̋͊ͩP̲̹̯̠͈͒̉E̶̴̘̝̞̜̞̘͖̊ͩ̔͌ͅ", 19, Color.BLACK);
        msg.setTypeface(Typeface.DEFAULT_BOLD);
        msg.setSingleLine(true);
        msg.setIncludeFontPadding(false);
        msg.setGravity(Gravity.CENTER_VERTICAL);
        msg.setPadding(dp(14), 0, 0, 0);
        fila.addView(msg, new LinearLayout.LayoutParams(-1, -1));
        todo.addView(fila, new LinearLayout.LayoutParams(-1, altoCuerpo));

        LinearLayout barra = new LinearLayout(this);
        barra.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        barra.setPadding(dp(12), 0, dp(14), 0);
        FrameLayout boton = new FrameLayout(this);
        GradientDrawable bordeBoton = new GradientDrawable();
        bordeBoton.setColor(Color.TRANSPARENT); bordeBoton.setStroke(Math.max(1, dp(1)), Color.rgb(0, 103, 192)); bordeBoton.setCornerRadius(dp(3));
        boton.setForeground(bordeBoton);
        boton.addView(new MainActivity.Estatica(this), new FrameLayout.LayoutParams(-1, -1));
        TextView txtBoton = texto("Aceptar", 13, Color.BLACK);
        txtBoton.setShadowLayer(4, 0, 0, Color.WHITE);
        txtBoton.setGravity(Gravity.CENTER);
        boton.addView(txtBoton, new FrameLayout.LayoutParams(-1, -1));
        boton.setOnClickListener(v -> aceptar());
        barra.addView(boton, new LinearLayout.LayoutParams(dp(92), dp(30)));
        todo.addView(barra, new LinearLayout.LayoutParams(-1, altoAbajo));
        raiz.addView(todo, new FrameLayout.LayoutParams(-1, -1));

        try { wm.addView(raiz, lp); } catch (Exception e) { stopSelf(); return; }
        ventana = raiz;
        temblar(raiz, null);
        try {
            android.media.ToneGenerator tg = new android.media.ToneGenerator(android.media.AudioManager.STREAM_MUSIC, 70);
            tg.startTone(android.media.ToneGenerator.TONE_PROP_NACK, 400);
            handler.postDelayed(tg::release, 600);
        } catch (Exception ignored) { }
    }

    // "Aceptar": se cierra todo y aparecen los 500 DVDs rebotando muy rápido
    void aceptar() {
        cerrarTodo();
        Cheats.aplicar("ERRORACEPTADO");
        if (!DvdService.corriendo) {
            try { startForegroundService(new Intent(this, DvdService.class)); } catch (Exception ignored) { }
        }
        // 2 s después se PAUSAN los DVDs; 1 s más y sale la pantalla "No command" con los DVDs escondidos
        handler.postDelayed(() -> {
            Cheats.pausaPantallazo = true;
            handler.postDelayed(this::pantallazo, 1000);
        }, 2000);
    }

    // ---------- pantalla "No command" FALSA (de broma: no le hace nada al cel) ----------
    // Como la del modo recuperación de Android: todo negro, el robot caído con el triángulo rojo y "No command".
    View azul;
    void pantallazo() {
        if (wm == null || azul != null) { terminarPantallazo(); return; }
        WindowManager.LayoutParams lp = new WindowManager.LayoutParams(
            WindowManager.LayoutParams.MATCH_PARENT, WindowManager.LayoutParams.MATCH_PARENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS
                | WindowManager.LayoutParams.FLAG_FULLSCREEN | WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON,
            PixelFormat.OPAQUE);
        lp.gravity = Gravity.TOP | Gravity.START;
        if (Build.VERSION.SDK_INT >= 28) lp.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;

        android.util.DisplayMetrics dm = getResources().getDisplayMetrics();
        int ancho = dm.widthPixels, alto = Math.max(dm.heightPixels, dm.widthPixels);
        FrameLayout pantalla = new FrameLayout(this);
        pantalla.setBackgroundColor(Color.BLACK);
        pantalla.setClickable(true);   // se traga los toques: no se puede tocar nada de abajo
        pantalla.setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);

        // el robot: centrado, como a un tercio de la pantalla (igual que en la foto)
        int lado = (int) (ancho * 0.21f);
        ImageView robot = new ImageView(this);
        robot.setImageResource(R.drawable.robot_caido);
        robot.setScaleType(ImageView.ScaleType.FIT_CENTER);
        FrameLayout.LayoutParams lpR = new FrameLayout.LayoutParams(lado, lado, Gravity.TOP | Gravity.CENTER_HORIZONTAL);
        lpR.topMargin = (int) (alto * 0.33f) - lado / 2;
        pantalla.addView(robot, lpR);

        TextView txt = texto("No command", 15, Color.WHITE);
        txt.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        FrameLayout.LayoutParams lpT = new FrameLayout.LayoutParams(-2, -2, Gravity.TOP | Gravity.CENTER_HORIZONTAL);
        lpT.topMargin = (int) (alto * 0.69f);
        pantalla.addView(txt, lpT);

        try { wm.addView(pantalla, lp); } catch (Exception e) { terminarPantallazo(); return; }
        azul = pantalla;
        Cheats.ocultarPantallazo = true;   // ahora sí se esconden los DVDs
        handler.postDelayed(this::terminarPantallazo, 9000);   // se quita sola a los 9 s
    }

    // se quita la pantalla, los DVDs vuelven y siguen rebotando, y se apaga este servicio
    void terminarPantallazo() {
        quitarAzul();
        Cheats.pausaPantallazo = false;
        Cheats.ocultarPantallazo = false;
        stopSelf();
    }

    void quitarAzul() {
        if (azul != null) { try { wm.removeView(azul); } catch (Exception ignored) { } azul = null; }
        Cheats.pausaPantallazo = false;
        Cheats.ocultarPantallazo = false;
    }

    // ---------- los cuadros de "Error" (como el cuadro de mensaje de Windows) ----------
    void aviso(boolean maldito) {
        if (cerrandoTodo || wm == null) return;
        if (!maldito) { if (avisosCreados >= MAX_AVISOS) return; avisosCreados++; }
        int ancho = Math.min(dp(300), getResources().getDisplayMetrics().widthPixels - dp(40));
        WindowManager.LayoutParams lp = parametros(ancho, WindowManager.LayoutParams.WRAP_CONTENT);
        lp.x = (int) ((rnd.nextFloat() - 0.5f) * dp(160));
        lp.y = (int) ((rnd.nextFloat() - 0.5f) * dp(220));

        FrameLayout raiz = new FrameLayout(this);
        LinearLayout caja = new LinearLayout(this);
        caja.setOrientation(LinearLayout.VERTICAL);
        GradientDrawable borde = new GradientDrawable();
        borde.setColor(Color.WHITE); borde.setStroke(Math.max(1, dp(1)), Color.rgb(170, 170, 170)); borde.setCornerRadius(dp(6));
        caja.setBackground(borde);
        caja.setClipToOutline(true);
        Runnable cerrar = () -> cerrarAviso(raiz);
        caja.addView(barraTitulo(MainActivity.maldecir("Error", 2, 4), Color.WHITE, cerrar, raiz, lp), new LinearLayout.LayoutParams(-1, dp(32)));

        LinearLayout cuerpo = new LinearLayout(this);
        cuerpo.setOrientation(LinearLayout.HORIZONTAL);
        cuerpo.setGravity(Gravity.CENTER_VERTICAL);
        cuerpo.setPadding(dp(20), dp(maldito ? 26 : 22), dp(28), dp(maldito ? 26 : 22));
        ImageView advertencia = new ImageView(this);
        advertencia.setImageResource(R.drawable.advertencia_clasica);   // el de Windows 10
        advertencia.setScaleType(ImageView.ScaleType.FIT_CENTER);
        cuerpo.addView(advertencia, new LinearLayout.LayoutParams(dp(32), dp(32)));
        TextView txt = texto(maldito ? MainActivity.maldecir("No intentes escapar, hermano.", 6, 12) : MainActivity.maldecir("Error", 5, 10), 14, Color.BLACK);
        txt.setIncludeFontPadding(true);
        txt.setLineSpacing(0, 1.8f);
        txt.setPadding(dp(14), dp(6), 0, dp(6));
        cuerpo.addView(txt);
        caja.addView(cuerpo);

        LinearLayout abajo = new LinearLayout(this);
        abajo.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        abajo.setBackgroundColor(Color.rgb(240, 240, 240));
        abajo.setPadding(dp(12), dp(10), dp(12), dp(10));
        TextView aceptar = texto("Aceptar", 13, Color.BLACK);
        aceptar.setGravity(Gravity.CENTER);
        GradientDrawable fondoBoton = new GradientDrawable();
        fondoBoton.setColor(Color.rgb(253, 253, 253)); fondoBoton.setStroke(Math.max(1, dp(1)), Color.rgb(0, 103, 192)); fondoBoton.setCornerRadius(dp(4));
        aceptar.setBackground(fondoBoton);
        aceptar.setOnClickListener(v -> cerrar.run());
        abajo.addView(aceptar, new LinearLayout.LayoutParams(dp(88), dp(30)));
        caja.addView(abajo, new LinearLayout.LayoutParams(-1, -2));

        raiz.addView(caja, new FrameLayout.LayoutParams(-1, -2));
        // estática encima de todo el cuadro (no agarra los toques: la X y "Aceptar" siguen funcionando)
        MainActivity.Estatica est = new MainActivity.Estatica(this);
        est.setAlpha(0.42f);
        raiz.addView(est, new FrameLayout.LayoutParams(-1, -1));

        try { wm.addView(raiz, lp); } catch (Exception e) { return; }
        avisos.add(raiz);
        temblar(raiz, est);
    }

    // al cerrar un cuadro de Error salen 5 más; al décimo cierre sale el mensaje maldito
    void cerrarAviso(View raiz) {
        if (!avisos.remove(raiz)) return;
        try { wm.removeView(raiz); } catch (Exception ignored) { }
        if (cerrandoTodo) return;
        // si ya salieron todos los de la ronda y cerraste el último: se reinicia la cuenta
        // (antes se quedaba en el tope y la X de "NO ESCAPE" ya no sacaba más "Error")
        if (avisosCreados >= MAX_AVISOS) { if (avisos.isEmpty()) avisosCreados = 0; return; }
        cierresAvisos++;
        if (cierresAvisos == 2) handler.post(() -> aviso(true));   // al 2.º cierre: el mensaje maldito
        for (int i = 0; i < 5; i++) handler.postDelayed(() -> aviso(false), 60L * i);
    }

    void cerrarTodo() {
        cerrandoTodo = true;
        handler.removeCallbacksAndMessages(null);
        for (View v : new ArrayList<>(avisos)) { try { wm.removeView(v); } catch (Exception ignored) { } }
        avisos.clear();
        if (ventana != null) { try { wm.removeView(ventana); } catch (Exception ignored) { } ventana = null; }
        if (pregunta != null) { try { wm.removeView(pregunta); } catch (Exception ignored) { } pregunta = null; }
        quitarAzul();
    }
}
