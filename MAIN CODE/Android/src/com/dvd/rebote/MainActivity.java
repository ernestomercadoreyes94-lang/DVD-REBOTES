package com.dvd.rebote;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.Spinner;
import android.widget.TextView;

import java.util.function.IntConsumer;

// Pantalla de ajustes con el mismo aspecto que dvd_config.exe de Windows.
// Cada cambio se guarda al instante en dvd_config.json y los DVDs lo aplican en vivo.
public class MainActivity extends Activity {
    // Mismos colores que dvd_config.exe
    static final int BG = Color.rgb(24, 24, 30), PANEL = Color.rgb(36, 36, 46), FG = Color.rgb(235, 235, 240),
        MUTED = Color.rgb(150, 150, 165), BORDE = Color.rgb(70, 70, 90), VERDE = Color.rgb(110, 220, 130),
        NARANJA = Color.rgb(255, 200, 90), JSON = Color.rgb(160, 220, 255), AZUL = Color.rgb(0, 120, 215);
    static final int[] COLORES = {
        0xFF00BFFF, 0xFF1E90FF, 0xFF8A2BE2, 0xFFFF00FF, 0xFFFF1493, 0xFFFF4040,
        0xFFFF8C00, 0xFFFFD700, 0xFFADFF2F, 0xFF00FF7F, 0xFF00CED1, 0xFFFFFFFF};

    Config cfg;
    LinearLayout root;
    TextView estado, json, archivo;
    Button btnPermiso, btnColor;
    boolean cargando = false;
    final Handler handler = new Handler(Looper.getMainLooper());
    float d;

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        IdiomaCel.cargar(this);   // idioma: español / inglés
        d = getResources().getDisplayMetrics().density;
        cfg = Config.load(this).copy();
        if (Build.VERSION.SDK_INT >= 33) requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"}, 1);
        getWindow().setStatusBarColor(BG);
        getWindow().setNavigationBarColor(BG);

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(BG);
        root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(20), dp(14), dp(20), dp(30));
        scroll.addView(root);
        setContentView(scroll);
        // Android 15/16 (One UI 7/8) dibuja la app debajo de la barra de estado y de navegación:
        // se deja ese espacio para que el título y los botones no queden tapados.
        scroll.setOnApplyWindowInsetsListener((v, insets) -> {
            int arriba, abajo, izq, der;
            if (Build.VERSION.SDK_INT >= 30) {
                android.graphics.Insets barras = insets.getInsets(
                    android.view.WindowInsets.Type.systemBars() | android.view.WindowInsets.Type.displayCutout());
                arriba = barras.top; abajo = barras.bottom; izq = barras.left; der = barras.right;
            } else {
                arriba = insets.getSystemWindowInsetTop(); abajo = insets.getSystemWindowInsetBottom();
                izq = insets.getSystemWindowInsetLeft(); der = insets.getSystemWindowInsetRight();
            }
            root.setPadding(dp(20) + izq, dp(14) + arriba, dp(20) + der, dp(30) + abajo);
            return insets;
        });
        scroll.requestApplyInsets();

        TextView title = text("Configuración del DVD", 22, FG);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        root.addView(title);
        // Tocar 5 veces el título abre la casilla secreta de cheats
        cheatBox = cajaCheats();
        cheatBox.setVisibility(View.GONE);
        long[] toques = {0, 0}; // [cantidad, último toque]
        title.setPadding(0, dp(6), 0, dp(6)); // zona de toque más grande
        title.setOnClickListener(v -> {
            long ahora = System.currentTimeMillis();
            toques[0] = (ahora - toques[1] < 1500) ? toques[0] + 1 : 1;
            toques[1] = ahora;
            if (toques[0] >= 5) {
                toques[0] = 0;
                alternarCheats();
            }
        });
        // También se abre dejando presionado el título
        title.setOnLongClickListener(v -> { alternarCheats(); return true; });
        root.addView(cheatBox);
        // versión instalada (para comprobar que tienes la APK nueva)
        try {
            String v = getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
            root.addView(text("Versión " + v, 11, MUTED));
        } catch (Exception ignored) { }
        // IDIOMA: Español / English (al cambiarlo se vuelve a abrir la pantalla)
        LinearLayout filaIdioma = new LinearLayout(this);
        filaIdioma.setOrientation(LinearLayout.HORIZONTAL);
        for (int k = 0; k < 2; k++) {
            final boolean en = k == 1;
            Button bi = boton(en ? "English" : "Español", v -> { if (IdiomaCel.ingles != en) { IdiomaCel.guardar(this, en); recreate(); } });
            boolean activo = IdiomaCel.ingles == en;
            bi.setTextColor(activo ? Color.rgb(0, 191, 255) : MUTED);
            bi.setBackground(caja(PANEL, activo ? Color.rgb(0, 191, 255) : BORDE));
            LinearLayout.LayoutParams lpi = new LinearLayout.LayoutParams(0, dp(38), 1f);
            if (k == 1) lpi.leftMargin = dp(8);
            filaIdioma.addView(bi, lpi);
        }
        root.addView(filaIdioma);
        estado = text("", 13, MUTED);
        root.addView(estado);
        txtRecord = text("", 13, NARANJA);
        txtRecord.setOnClickListener(v -> abrirLogros());   // al tocar las estadísticas también se abren los logros
        root.addView(txtRecord);
        Button btnLogros = boton("🏆 Logros", v -> abrirLogros());
        btnLogros.setTextColor(NARANJA);
        root.addView(btnLogros, full(dp(44)));
        // TIENDA: skins para el logo que se compran con los G de los logros
        Button btnTienda = boton("◆ Tienda", v -> abrirTienda());
        btnTienda.setTextColor(Color.rgb(130, 235, 255));
        root.addView(btnTienda, full(dp(44)));
        // revisa GitHub y, si hay APK nueva, la baja y abre el instalador (con la consola falsa)
        Button btnActualizar = boton("⟳ Buscar actualizaciones", v -> ActualizadorCel.buscar(this));
        btnActualizar.setTextColor(Color.rgb(120, 210, 255));
        root.addView(btnActualizar, full(dp(44)));

        btnPermiso = boton("Dar permiso: mostrar encima de otras apps", v -> startActivity(
            new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:" + getPackageName()))));
        btnPermiso.setTextColor(NARANJA);
        root.addView(btnPermiso, full(dp(44)));

        espacio(10);
        slider("Velocidad (px/s)", 20, 2000, cfg.velocidad, v -> { cfg.velocidad = v; changed(); });
        slider("Tamaño (%)", 20, 400, cfg.tamano, v -> { cfg.tamano = v; changed(); });
        slider("Opacidad (%)", 5, 80, cfg.opacidad, v -> { cfg.opacidad = v; changed(); });
        cantidad();

        // Estilo
        LinearLayout fe = fila("Estilo");
        Spinner sp = new Spinner(this, Spinner.MODE_DROPDOWN);
        ArrayAdapter<String> ad = new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, new String[]{"Logo DVD", "Disco", IdiomaCel.T("Reloj")}) {
            @Override public View getView(int pos, View cv, ViewGroup parent) { TextView t = (TextView) super.getView(pos, cv, parent); t.setTextColor(Color.BLACK); return t; }
        };
        ad.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        sp.setAdapter(ad);
        sp.setBackground(caja(Color.WHITE, Color.rgb(170, 170, 170)));
        sp.setSelection("disco".equals(cfg.estilo) ? 1 : "reloj".equals(cfg.estilo) ? 2 : 0);
        sp.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            public void onItemSelected(AdapterView<?> p, View v, int pos, long id) {
                String nuevo = pos == 1 ? "disco" : pos == 2 ? "reloj" : "logo";
                if (!nuevo.equals(cfg.estilo)) { cfg.estilo = nuevo; changed(); }
            }
            public void onNothingSelected(AdapterView<?> p) { }
        });
        fe.addView(sp, new LinearLayout.LayoutParams(dp(150), dp(36)));
        root.addView(fe);

        // Color
        LinearLayout fc = fila("Color");
        btnColor = new Button(this);
        btnColor.setAllCaps(false);
        btnColor.setOnClickListener(v -> elegirColor());
        fc.addView(btnColor, new LinearLayout.LayoutParams(dp(150), dp(36)));
        root.addView(fc);
        pintarBotonColor();
        CheckBox cc = check("Cambiar de color al rebotar", cfg.cambiarColor, on -> { cfg.cambiarColor = on; changed(); });
        ((LinearLayout.LayoutParams) cc.getLayoutParams()).leftMargin = dp(130);

        // Logo personalizado: texto e imagen
        LinearLayout fl = fila("Texto del logo");
        EditText txtLogo = new EditText(this);
        txtLogo.setText(cfg.logoTexto);
        txtLogo.setSingleLine(true);
        txtLogo.setFilters(new android.text.InputFilter[]{ new android.text.InputFilter.LengthFilter(20) });
        txtLogo.setTextColor(FG); txtLogo.setTextSize(14);
        txtLogo.setPadding(dp(8), 0, dp(4), 0);
        txtLogo.setBackground(caja(PANEL, BORDE));
        txtLogo.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence c, int a, int b, int x) { }
            public void onTextChanged(CharSequence c, int a, int b, int x) { }
            public void afterTextChanged(Editable ed) {
                String t = ed.toString().trim();
                cfg.logoTexto = t.isEmpty() ? "DVD" : t;
                changed();
            }
        });
        fl.addView(txtLogo, new LinearLayout.LayoutParams(0, dp(36), 1f));
        root.addView(fl);
        LinearLayout fi = fila("Imagen del logo");
        btnImagen = boton("", v -> startActivityForResult(
            new Intent(Intent.ACTION_GET_CONTENT).setType("image/*").addCategory(Intent.CATEGORY_OPENABLE), PEDIR_IMAGEN));
        fi.addView(btnImagen, new LinearLayout.LayoutParams(0, dp(38), 1f));
        Button quitar = boton("Quitar", v -> { cfg.logoImagen = false; pintarBotonImagen(); changed(); });
        LinearLayout.LayoutParams lq = new LinearLayout.LayoutParams(dp(80), dp(38));
        lq.leftMargin = dp(6);
        fi.addView(quitar, lq);
        root.addView(fi);
        pintarBotonImagen();

        // Tolerancia
        LinearLayout ft = fila("Tolerancia esquina (px)");
        ft.addView(numero(0, 200, cfg.toleranciaEsquinaPx, v -> { cfg.toleranciaEsquinaPx = v; changed(); }), new LinearLayout.LayoutParams(dp(80), dp(36)));
        root.addView(ft);

        espacio(6);
        check("Mostrar contador al tocar esquina", cfg.mostrarContador, on -> { cfg.mostrarContador = on; changed(); });
        check("Mostrar \"¡ESQUINA!\" al tocar", cfg.mostrarMensajeEsquina, on -> { cfg.mostrarMensajeEsquina = on; changed(); });
        check("Sonido en las esquinas", cfg.sonido, on -> { cfg.sonido = on; changed(); });
        check("Sonido de la skin (en las esquinas)", cfg.sonidoSkin, on -> { cfg.sonidoSkin = on; changed(); });
        // Si el cel no tiene vibrador, solo queda la opción de vibrar el control
        if (Config.tieneVibrador(this))
            check("Vibrar al tocar esquina", cfg.vibrar, on -> { cfg.vibrar = on; changed(); });
        check("Vibrar el control al tocar esquina", cfg.vibrarControl, on -> { cfg.vibrarControl = on; changed(); });
        check("Estela detrás de los DVDs", cfg.estela, on -> { cfg.estela = on; changed(); });        check("Choque entre DVDs", cfg.choque, on -> { cfg.choque = on; changed(); });
        check("Pausado", cfg.pausado, on -> { cfg.pausado = on; changed(); });
        CheckBox solo = check("Modo solo esquinas (va de esquina a esquina)", cfg.modoSoloEsquinas, on -> { cfg.modoSoloEsquinas = on; changed(); });
        solo.setTextColor(NARANJA);


        // Botones (2 columnas, como en Windows)
        espacio(10);
        GridLayout g = new GridLayout(this);
        g.setColumnCount(2);
        g.addView(boton("▶ Abrir DVD", v -> abrir()), celda());
        g.addView(boton("■ Cerrar DVD", v -> cerrar()), celda());
        g.addView(boton("Reiniciar contador", v -> { cfg.reiniciarContador++; changed(); }), celda());
        g.addView(boton("Valores por defecto", v -> {
            int token = cfg.reiniciarContador;
            cfg = new Config();
            cfg.reiniciarContador = token;
            cfg.save(this);
            recreate();
        }), celda());
        root.addView(g);
        Button compartir = boton("📤 Compartir mi récord", v -> compartirRecord());
        compartir.setTextColor(NARANJA);
        compartir.setBackground(caja(PANEL, NARANJA));
        root.addView(compartir, full(dp(44)));
        // el botón del fondo animado solo aparece si el cel de verdad deja ponerlo
        if (aceptaFondoAnimado()) {
            Button fondo = boton("🖼 Poner el DVD de fondo de pantalla", v -> ponerFondo());
            fondo.setTextColor(Color.rgb(120, 210, 255));
            fondo.setBackground(caja(PANEL, Color.rgb(120, 210, 255)));
            root.addView(fondo, full(dp(44)));
        }
        // poner el widget en la pantalla de inicio (Android lo pone solo si el cel lo deja; si no, explica cómo)
        Button btnWidget = boton("➕ Poner el widget en la pantalla de inicio", v -> ponerWidget());
        btnWidget.setTextColor(Color.rgb(200, 160, 255));
        btnWidget.setBackground(caja(PANEL, Color.rgb(200, 160, 255)));
        root.addView(btnWidget, full(dp(44)));

        // JSON
        espacio(14);
        TextView jt = text("dvd_config.json", 15, FG);
        jt.setTypeface(Typeface.DEFAULT_BOLD);
        root.addView(jt);
        json = text("", 12, JSON);
        json.setTypeface(Typeface.MONOSPACE);
        json.setBackgroundColor(PANEL);
        json.setPadding(dp(10), dp(8), dp(10), dp(8));
        json.setTextIsSelectable(true);
        root.addView(json);
        archivo = text("Archivo: " + Config.archivo(this).getPath(), 11, MUTED);
        root.addView(archivo);

        IdiomaCel.traducir(root);   // todos los textos al idioma elegido
        refreshJson();
        handler.post(refresco);
        if (getIntent() != null && getIntent().getBooleanExtra(EXTRA_CHEATS, false)) mostrarCheats();
    }

    static final String EXTRA_CHEATS = "abrir_cheats";
    LinearLayout cheatBox;

    // Viene del botón "Cheats" de la notificación
    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        if (intent.getBooleanExtra(EXTRA_CHEATS, false)) mostrarCheats();
    }

    void alternarCheats() {
        boolean ver = cheatBox.getVisibility() != View.VISIBLE;
        if (ver) mostrarCheats(); else cheatBox.setVisibility(View.GONE);
        android.widget.Toast.makeText(this, IdiomaCel.T(ver ? "🔓 Cheats desbloqueados" : "🔒 Cheats ocultos"), android.widget.Toast.LENGTH_SHORT).show();
    }

    void mostrarCheats() {
        cheatBox.setVisibility(View.VISIBLE);
        EditText e = (EditText) cheatBox.getChildAt(0);
        e.requestFocus();
        e.postDelayed(() -> {
            android.view.inputmethod.InputMethodManager imm =
                (android.view.inputmethod.InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
            if (imm != null) imm.showSoftInput(e, android.view.inputmethod.InputMethodManager.SHOW_IMPLICIT);
        }, 250);
    }

    final Runnable refresco = new Runnable() {
        @Override public void run() { refreshEstado(); handler.postDelayed(this, 700); }
    };

    @Override protected void onResume() { super.onResume(); refreshEstado(); }
    @Override protected void onDestroy() { handler.removeCallbacks(refresco); super.onDestroy(); }

    void refreshEstado() {
        boolean permiso = Settings.canDrawOverlays(this);
        btnPermiso.setVisibility(permiso ? View.GONE : View.VISIBLE);
        if (!permiso) { estado.setText(IdiomaCel.T("○ Falta el permiso para mostrar encima de otras apps")); estado.setTextColor(NARANJA); }
        else if (DvdService.corriendo) { estado.setText(IdiomaCel.T("● DvdRebote está corriendo — los cambios se aplican al instante")); estado.setTextColor(VERDE); }
        else { estado.setText(IdiomaCel.T("○ DvdRebote no está abierto")); estado.setTextColor(MUTED); }
        int rec = DvdView.leerRecord(this);
        txtRecord.setText((IdiomaCel.ingles ? "🏆 Record: " + rec + " corners" : "🏆 Récord: " + rec + " esquinas") + (DvdService.corriendo ? (IdiomaCel.ingles ? "   ·   Now: " : "   ·   Ahora: ") + DvdView.totalActual : "")
            + "\n" + Estadisticas.texto(this));
    }

    // ---------- fondo de pantalla animado: abre la vista previa de Android con el DVD ----------
    Intent intentFondo() {
        Intent i = new Intent(android.app.WallpaperManager.ACTION_CHANGE_LIVE_WALLPAPER);
        i.putExtra(android.app.WallpaperManager.EXTRA_LIVE_WALLPAPER_COMPONENT,
            new android.content.ComponentName(this, FondoDvd.class));
        return i;
    }

    // ¿El cel tiene fondos animados Y la pantalla para elegirlos? (muchos Samsung no la traen)
    boolean aceptaFondoAnimado() {
        try {
            android.content.pm.PackageManager pm = getPackageManager();
            if (!pm.hasSystemFeature(android.content.pm.PackageManager.FEATURE_LIVE_WALLPAPER)) return false;
            return intentFondo().resolveActivity(pm) != null
                || new Intent(android.app.WallpaperManager.ACTION_LIVE_WALLPAPER_CHOOSER).resolveActivity(pm) != null;
        } catch (Exception e) { return false; }
    }

    // ---------- poner el widget: Android lo pone solo (pregunta dónde); si el cel no deja, explica cómo ----------
    void ponerWidget() {
        try {
            android.appwidget.AppWidgetManager m = getSystemService(android.appwidget.AppWidgetManager.class);
            if (m != null && m.isRequestPinAppWidgetSupported()) {
                m.requestPinAppWidget(new android.content.ComponentName(this, WidgetDvd.class), null, null);
                return;
            }
        } catch (Exception ignored) { }
        new android.app.AlertDialog.Builder(this)
            .setTitle(IdiomaCel.ingles ? "Add the widget" : "Poner el widget")
            .setMessage(IdiomaCel.ingles
                ? "1. Press and hold an empty space on your home screen.\n2. Tap \"Widgets\".\n3. Find \"DVD Rebote\" and drag it to your screen."
                : "1. Deja presionado un espacio vacío de tu pantalla de inicio.\n2. Toca \"Widgets\".\n3. Busca \"DVD Rebote\" y arrástralo a tu pantalla.")
            .setPositiveButton("OK", null).show();
    }

    void ponerFondo() {
        try {
            startActivity(intentFondo());
            android.widget.Toast.makeText(this, IdiomaCel.T("Toca \"Establecer fondo\" para ponerlo"), android.widget.Toast.LENGTH_LONG).show();
        } catch (Exception e) {
            try { startActivity(new Intent(android.app.WallpaperManager.ACTION_LIVE_WALLPAPER_CHOOSER)); }
            catch (Exception ignored) { }
        }
    }

    // ---------- compartir el récord: se crea una imagen y se manda con WhatsApp, TikTok, etc. ----------
    TextView txtRecord;

    void compartirRecord() {
        int rec = DvdView.leerRecord(this);
        long fecha = DvdView.leerFechaRecord(this);
        try {
            android.graphics.Bitmap img = imagenRecord(rec, fecha);
            try (java.io.FileOutputStream out = new java.io.FileOutputStream(ProveedorImagen.archivo(this))) {
                img.compress(android.graphics.Bitmap.CompressFormat.PNG, 100, out);
            }
            // el "?t=" hace que las apps no muestren una imagen vieja guardada
            Uri uri = ProveedorImagen.URI.buildUpon().appendQueryParameter("t", String.valueOf(System.currentTimeMillis())).build();
            Intent i = new Intent(Intent.ACTION_SEND);
            i.setType("image/png");
            i.putExtra(Intent.EXTRA_STREAM, uri);
            i.putExtra(Intent.EXTRA_TEXT, "¡Mi récord en DVD Rebote: " + rec + " esquinas! 📀");
            i.setClipData(android.content.ClipData.newRawUri("record", uri));
            i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivity(Intent.createChooser(i, "Compartir mi récord"));
        } catch (Exception e) {
            android.widget.Toast.makeText(this, IdiomaCel.T("No se pudo compartir: " + e.getMessage()), android.widget.Toast.LENGTH_SHORT).show();
        }
    }

    // Imagen cuadrada (1080x1080) con el logo del DVD y el récord
    static android.graphics.Bitmap imagenRecord(int rec, long fecha) {
        int S = 1080;
        android.graphics.Bitmap b = android.graphics.Bitmap.createBitmap(S, S, android.graphics.Bitmap.Config.ARGB_8888);
        android.graphics.Canvas c = new android.graphics.Canvas(b);
        c.drawColor(Color.rgb(12, 12, 18));
        android.graphics.Paint p = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        p.setTextAlign(android.graphics.Paint.Align.CENTER);
        int cian = Color.rgb(0, 191, 255);

        // logo DVD (texto grueso inclinado + óvalo con "V I D E O")
        Typeface negra = Typeface.create("sans-serif-black", Typeface.NORMAL);
        p.setTypeface(negra); p.setTextSkewX(-0.22f); p.setTextSize(250); p.setColor(cian);
        c.drawText("DVD", S / 2f, 310, p);
        p.setTextSkewX(0);
        android.graphics.RectF ovalo = new android.graphics.RectF(S / 2f - 260, 340, S / 2f + 260, 430);
        c.drawOval(ovalo, p);
        p.setColor(Color.rgb(12, 12, 18)); p.setTextSize(58);
        c.drawText("V I D E O", S / 2f, 406, p);

        // récord
        p.setTypeface(Typeface.DEFAULT_BOLD); p.setColor(Color.WHITE); p.setTextSize(78);
        c.drawText("MI RÉCORD", S / 2f, 570, p);
        p.setTypeface(negra); p.setColor(Color.rgb(255, 210, 60)); p.setTextSize(rec >= 10000 ? 190 : 250);
        c.drawText(String.valueOf(rec), S / 2f, 800, p);
        p.setTypeface(Typeface.DEFAULT_BOLD); p.setColor(Color.WHITE); p.setTextSize(64);
        c.drawText(rec == 1 ? "esquina" : "esquinas", S / 2f, 880, p);

        p.setTypeface(Typeface.DEFAULT); p.setColor(Color.rgb(150, 150, 165)); p.setTextSize(36);
        if (fecha > 0)
            c.drawText(new java.text.SimpleDateFormat("dd/MM/yyyy", java.util.Locale.ROOT).format(new java.util.Date(fecha)), S / 2f, 950, p);
        p.setColor(cian); p.setTextSize(38);
        c.drawText("DVD Rebote  ·  @ernestomercadoreyes691", S / 2f, 1030, p);
        return b;
    }

    // Casilla secreta: escribes el código y tocas "Usar" (o Enter)
    LinearLayout cajaCheats() {
        LinearLayout f = new LinearLayout(this);
        f.setOrientation(LinearLayout.HORIZONTAL);
        f.setGravity(Gravity.CENTER_VERTICAL);
        f.setPadding(0, dp(6), 0, dp(6));
        EditText e = new EditText(this);
        e.setHint("Código secreto");
        e.setHintTextColor(MUTED);
        e.setTextColor(NARANJA);
        e.setSingleLine(true);
        e.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        e.setPadding(dp(10), 0, dp(10), 0);
        e.setBackground(caja(PANEL, NARANJA));
        Button usar = boton("Usar", v -> usarCheat(e));
        usar.setTextColor(NARANJA);
        e.setOnEditorActionListener((tv, id, ev) -> { usarCheat(e); return true; });
        f.addView(e, new LinearLayout.LayoutParams(0, dp(40), 1f));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(dp(80), dp(40));
        lp.leftMargin = dp(8);
        f.addView(usar, lp);
        return f;
    }

    void usarCheat(EditText e) {
        String code = e.getText().toString().trim();
        if (code.isEmpty()) return;
        e.setText("");
        String msg;
        if (code.replace(" ", "").equalsIgnoreCase("SECRET")) { abrirSecret(); return; }
        if (code.replace(" ", "").equalsIgnoreCase(Cheats.ROOT)) {
            boolean on = Config.alternarRoot(this);
            android.widget.Toast.makeText(this, on ? "ROOT: límite de DVDs DESBLOQUEADO" : "ROOT: límite de DVDs otra vez según tu RAM",
                android.widget.Toast.LENGTH_SHORT).show();
            recreate();   // para que la barra de "Cantidad de DVDs" llegue al nuevo máximo
            return;
        }
        if (code.equalsIgnoreCase(Cheats.STOP)) { cerrar(); msg = "CHEAT: DVDs CERRADOS"; }
        else {
            msg = Cheats.aplicar(code);
            if (msg == null) msg = "Código incorrecto";
            else if (!DvdService.corriendo && Settings.canDrawOverlays(this)) msg += " (abre el DVD para verlo)";
        }
        android.widget.Toast.makeText(this, IdiomaCel.T(msg), android.widget.Toast.LENGTH_SHORT).show();
    }

    // ---------- pantalla "Logros": todos los logros y abajo el PLATINO ----------
    // ---- TIENDA ----
    android.app.AlertDialog dlgTienda;
    void abrirTienda() {
        if (dlgTienda != null && dlgTienda.isShowing()) dlgTienda.dismiss();
        boolean en = IdiomaCel.ingles;
        String[] ids = { "", "bronce", "plata", "oro", "diamante", "neon", "fuego", "arcoiris", "holograma" };
        String[] nombres = en ? new String[] { "Normal", "Bronze", "Silver", "Gold", "Diamond", "Neon", "Fire", "Rainbow", "Hologram" }
                              : new String[] { "Normal", "Bronce", "Plata", "Oro", "Diamante", "Neón", "Fuego", "Arcoíris", "Holograma" };
        String[] desc = en ? new String[] { "Your DVD with its normal colors", "Polished copper metal", "Shiny polished silver", "Shiny solid gold", "Ice-blue diamond with sparkles",
                                            "Glowing pink and blue light tube", "Flames from yellow to red", "Every color, with relief", "Iridescent, changes color" }
                           : new String[] { "Tu DVD con sus colores de siempre", "Metal cobrizo pulido", "Plata pulida y brillante", "Oro brillante", "Diamante azul hielo con destellos",
                                            "Tubo de luz rosa y celeste", "Llamas de amarillo a rojo", "Todos los colores, con relieve", "Tornasol que cambia de color" };
        String actual = getSharedPreferences("ajustes", MODE_PRIVATE).getString("skin", "");
        if (!actual.isEmpty() && !Estadisticas.tieneSkin(this, actual)) actual = "";
        int saldo = Estadisticas.saldoG(this);
        LinearLayout lista = new LinearLayout(this);
        lista.setOrientation(LinearLayout.VERTICAL);
        lista.setPadding(dp(16), dp(12), dp(16), dp(16));
        lista.setBackgroundColor(BG);
        lista.addView(text((en ? "You have " : "Tienes ") + saldo + " G", 18, Color.rgb(255, 210, 90)));
        lista.addView(text(en ? "G are earned with achievements" : "Los G se ganan con los logros", 12, MUTED));
        android.graphics.Bitmap logo = android.graphics.BitmapFactory.decodeResource(getResources(), R.drawable.logo_dvd);
        for (int i = 0; i < ids.length; i++) {
            final String id = ids[i];
            final int precio = i == 0 ? 0 : Estadisticas.SKINS_PRECIO[i - 1];
            boolean tiene = i == 0 || Estadisticas.tieneSkin(this, id);
            int col = i == 0 ? Color.rgb(0, 191, 255) : Estadisticas.colorSkin(id);
            LinearLayout fila = new LinearLayout(this);
            fila.setOrientation(LinearLayout.HORIZONTAL);
            fila.setGravity(android.view.Gravity.CENTER_VERTICAL);
            fila.setPadding(dp(10), dp(10), dp(10), dp(10));
            android.graphics.drawable.GradientDrawable fondo = new android.graphics.drawable.GradientDrawable(android.graphics.drawable.GradientDrawable.Orientation.LEFT_RIGHT,
                new int[] { Color.argb(70, Color.red(col), Color.green(col), Color.blue(col)), Color.rgb(28, 28, 34) });
            fondo.setCornerRadius(dp(10));
            if (actual.equals(id)) fondo.setStroke(dp(2), col);
            fila.setBackground(fondo);
            // vista del logo: el de siempre (azul) o de metal
            android.widget.ImageView vista = new android.widget.ImageView(this);
            if (logo != null) {
                int vw = dp(96), vh = dp(48);
                android.graphics.Bitmap vb = android.graphics.Bitmap.createBitmap(vw, vh, android.graphics.Bitmap.Config.ARGB_8888);
                android.graphics.Canvas vc = new android.graphics.Canvas(vb);
                android.graphics.Paint pp = new android.graphics.Paint(android.graphics.Paint.FILTER_BITMAP_FLAG);
                pp.setColorFilter(new android.graphics.PorterDuffColorFilter(i == 0 ? col : Color.WHITE, android.graphics.PorterDuff.Mode.SRC_IN));
                float k = Math.min((vw - dp(6)) / (float) logo.getWidth(), (vh - dp(6)) / (float) logo.getHeight());
                float lw = logo.getWidth() * k, lh = logo.getHeight() * k;
                vc.drawBitmap(logo, null, new android.graphics.RectF((vw - lw) / 2, (vh - lh) / 2, (vw + lw) / 2, (vh + lh) / 2), pp);
                if (i > 0) Estadisticas.metalizar(vb, id);
                vista.setImageBitmap(vb);
            }
            fila.addView(vista, new LinearLayout.LayoutParams(dp(96), dp(48)));
            LinearLayout textos = new LinearLayout(this);
            textos.setOrientation(LinearLayout.VERTICAL);
            textos.setPadding(dp(10), 0, dp(6), 0);
            TextView n = text(nombres[i], 15, Color.WHITE); n.setTypeface(Typeface.DEFAULT_BOLD);
            textos.addView(n);
            textos.addView(text(desc[i], 11, MUTED));
            textos.addView(text(i == 0 ? (en ? "Free" : "Gratis") : tiene ? (en ? "Bought" : "Comprada") : precio + " G", 13, tiene ? Color.rgb(140, 240, 160) : Color.rgb(255, 210, 90)));
            fila.addView(textos, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            Button b = new Button(this);
            b.setAllCaps(false); b.setTextSize(13);
            if (tiene && actual.equals(id)) { b.setText(en ? "In use ✓" : "En uso ✓"); b.setEnabled(false); b.setTextColor(Color.rgb(140, 240, 160)); }
            else if (tiene) { b.setText(en ? "Use" : "Usar"); b.setTextColor(Color.WHITE); }
            else if (saldo >= precio) { b.setText((en ? "Buy · " : "Comprar · ") + precio + " G"); b.setTextColor(col); }
            else { b.setText((en ? "Missing " : "Te faltan ") + (precio - saldo) + " G"); b.setEnabled(false); b.setTextColor(MUTED); }
            final boolean yaLaTiene = tiene;
            final String nombre = nombres[i];
            b.setOnClickListener(v -> {
                if (!yaLaTiene) {
                    new android.app.AlertDialog.Builder(this)
                        .setMessage((en ? "Buy the " + nombre + " skin for " : "¿Comprar la skin de " + nombre + " por ") + precio + " G?")
                        .setPositiveButton(en ? "Buy" : "Comprar", (d, w) -> {
                            if (Estadisticas.comprar(this, id, precio)) usarSkin(id);
                            else android.widget.Toast.makeText(this, en ? "You don't have enough G" : "No te alcanzan los G", android.widget.Toast.LENGTH_SHORT).show();
                            abrirTienda();
                        })
                        .setNegativeButton(IdiomaCel.T("Cancelar"), null).show();
                } else { usarSkin(id); abrirTienda(); }
            });
            fila.addView(b, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, dp(44)));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.topMargin = dp(10);
            lista.addView(fila, lp);
        }
        android.widget.ScrollView sv = new android.widget.ScrollView(this);
        sv.setBackgroundColor(BG);
        sv.addView(lista);
        dlgTienda = new android.app.AlertDialog.Builder(this)
            .setTitle(en ? "◆ Shop" : "◆ Tienda")
            .setView(sv)
            .setPositiveButton(IdiomaCel.T("Cerrar"), null)
            .show();
    }

    void usarSkin(String id) {
        getSharedPreferences("ajustes", MODE_PRIVATE).edit().putString("skin", id).apply();
        SonidosSkin.tocar(id);   // al usar una skin, se escucha su sonido
        android.widget.Toast.makeText(this, id.isEmpty() ? IdiomaCel.T("Colores normales") : (IdiomaCel.ingles ? "Skin applied ✓" : "Skin puesta ✓"), android.widget.Toast.LENGTH_SHORT).show();
    }

    void abrirLogros() {
        long esq = Estadisticas.totalEsquinas(this), ms = Estadisticas.totalMs(this);
        LinearLayout lista = new LinearLayout(this);
        lista.setOrientation(LinearLayout.VERTICAL);
        lista.setPadding(dp(16), dp(12), dp(16), dp(16));
        lista.setBackgroundColor(BG);

        String espGanados = Estadisticas.especiales(this);
        String[] nombres = new String[Estadisticas.NOMBRES_ESQUINAS.length + Estadisticas.NOMBRES_HORAS.length + Estadisticas.ESPECIALES_ID.length];
        String[] desc = new String[nombres.length];
        boolean[] ganado = new boolean[nombres.length];
        int k = 0;
        for (int i = 0; i < Estadisticas.LOGROS_ESQUINAS.length; i++, k++) {
            long meta = Estadisticas.LOGROS_ESQUINAS[i];
            nombres[k] = Estadisticas.NOMBRES_ESQUINAS[i];
            desc[k] = (IdiomaCel.ingles ? "Hit " + meta + " corners in total (" : "Toca " + meta + " esquinas en total (") + Math.min(esq, meta) + "/" + meta + ")";
            ganado[k] = esq >= meta;
        }
        for (int i = 0; i < Estadisticas.LOGROS_HORAS.length; i++, k++) {
            int hh = Estadisticas.LOGROS_HORAS[i];
            long min = Math.min(ms / 60000, hh * 60L);
            nombres[k] = Estadisticas.NOMBRES_HORAS[i];
            desc[k] = (IdiomaCel.ingles ? "Leave the DVD bouncing " + hh + (hh == 1 ? " hour" : " hours") : "Deja el DVD rebotando " + hh + (hh == 1 ? " hora" : " horas")) + " (" + (min / 60) + " h " + (min % 60) + " min)";
            ganado[k] = ms >= hh * 3600_000L;
        }
        for (int i = 0; i < Estadisticas.ESPECIALES_ID.length; i++, k++) {   // logros especiales
            nombres[k] = Estadisticas.NOMBRES_ESPECIALES[i];
            desc[k] = IdiomaCel.ingles ? Estadisticas.DESC_ESPECIALES_EN[i] : Estadisticas.DESC_ESPECIALES[i];
            ganado[k] = Estadisticas.tieneEspecial(espGanados, Estadisticas.ESPECIALES_ID[i]);
        }
        int puntos = 0, max = 0, cuantos = 0;
        for (int i = 0; i < nombres.length; i++) {
            int p = Integer.parseInt(nombres[i].substring(0, nombres[i].indexOf("G:")));
            max += p; if (ganado[i]) { puntos += p; cuantos++; }
        }
        lista.addView(text(cuantos + (IdiomaCel.ingles ? " of " : " de ") + nombres.length + (IdiomaCel.ingles ? " achievements   ·   " : " logros   ·   ") + puntos + " / " + max + "G", 14, NARANJA));
        for (int i = 0; i < nombres.length; i++) {
            int dos = nombres[i].indexOf(": ");
            LinearLayout fila = new LinearLayout(this);
            fila.setOrientation(LinearLayout.HORIZONTAL);
            fila.setGravity(android.view.Gravity.CENTER_VERTICAL);
            fila.setPadding(dp(8), dp(6), dp(8), dp(6));
            fila.setBackgroundColor(ganado[i] ? Color.rgb(44, 44, 54) : Color.rgb(32, 32, 38));
            android.widget.ImageView ic = new android.widget.ImageView(this);
            ic.setImageBitmap(iconoLogro(ganado[i]));
            fila.addView(ic, new LinearLayout.LayoutParams(dp(40), dp(40)));
            LinearLayout textos = new LinearLayout(this);
            textos.setOrientation(LinearLayout.VERTICAL);
            textos.setPadding(dp(10), 0, dp(6), 0);
            TextView n = text(nombres[i].substring(dos + 2), 15, ganado[i] ? Color.WHITE : MUTED);
            n.setTypeface(Typeface.DEFAULT_BOLD);
            textos.addView(n);
            textos.addView(text(desc[k = i], 12, MUTED));
            fila.addView(textos, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            TextView pts = text(nombres[i].substring(0, dos), 15, ganado[i] ? NARANJA : MUTED);
            pts.setTypeface(Typeface.DEFAULT_BOLD);
            fila.addView(pts);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.topMargin = dp(6);
            lista.addView(fila, lp);
        }
        // TROFEOS: bronce, plata, oro y diamante, de 2 en 2
        int ganadosTotal = Estadisticas.ganados(esq, ms, Estadisticas.especiales(this));
        int[] colorMetal = { Color.rgb(225, 140, 70), Color.rgb(205, 215, 230), Color.rgb(255, 205, 60), Color.rgb(130, 235, 255) };
        int[] imgs = { R.drawable.trofeo_bronce, R.drawable.trofeo_plata, R.drawable.trofeo_oro, R.drawable.trofeo_diamante };
        LinearLayout filaTrofeos = null;
        for (int i = 0; i < 4; i++) {
            if (i % 2 == 0) {
                filaTrofeos = new LinearLayout(this);
                filaTrofeos.setOrientation(LinearLayout.HORIZONTAL);
                LinearLayout.LayoutParams lpc = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
                lpc.topMargin = dp(i == 0 ? 12 : 6);
                lista.addView(filaTrofeos, lpc);
            }
            boolean tiene = ganadosTotal >= Estadisticas.LOGROS_PARA_TROFEO[i];
            LinearLayout caja = new LinearLayout(this);
            caja.setOrientation(LinearLayout.VERTICAL);
            caja.setGravity(android.view.Gravity.CENTER_HORIZONTAL);
            caja.setPadding(dp(4), dp(6), dp(4), dp(8));
            android.graphics.drawable.GradientDrawable fondo = new android.graphics.drawable.GradientDrawable(android.graphics.drawable.GradientDrawable.Orientation.TOP_BOTTOM,
                tiene ? new int[] { Color.argb(90, Color.red(colorMetal[i]), Color.green(colorMetal[i]), Color.blue(colorMetal[i])), Color.rgb(28, 28, 34) }
                      : new int[] { Color.rgb(36, 36, 44), Color.rgb(28, 28, 34) });
            if (tiene) fondo.setStroke(dp(1), colorMetal[i]);
            caja.setBackground(fondo);
            // trofeo animado: rayos, brillitos, destello y confeti si ya lo tienes; en gris si no
            caja.addView(new TrofeoAnimado(this, imgs[i], tiene, colorMetal[i], i),
                new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(72)));
            TextView metal = text(Estadisticas.METALES[i].toUpperCase(), 11, tiene ? colorMetal[i] : MUTED);
            metal.setGravity(android.view.Gravity.CENTER);
            caja.addView(metal);
            TextView nom = text(Estadisticas.NOMBRES_TROFEOS[i], 13, tiene ? Color.WHITE : MUTED);
            nom.setTypeface(Typeface.DEFAULT_BOLD); nom.setGravity(android.view.Gravity.CENTER);
            caja.addView(nom);
            TextView cond = text(Estadisticas.CONDICION_TROFEOS[i], 10, MUTED);
            cond.setGravity(android.view.Gravity.CENTER);
            caja.addView(cond);
            LinearLayout.LayoutParams lpt = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
            if (i % 2 == 1) lpt.leftMargin = dp(6);
            filaTrofeos.addView(caja, lpt);
        }

        android.widget.ScrollView sv = new android.widget.ScrollView(this);
        sv.setBackgroundColor(BG);
        sv.addView(lista);
        IdiomaCel.traducir(lista);
        TextView titulo = text(IdiomaCel.T("Logros"), 24, Color.WHITE);
        titulo.setTypeface(Typeface.DEFAULT_BOLD);
        titulo.setPadding(dp(20), dp(16), dp(20), 0);
        titulo.setBackgroundColor(BG);
        android.app.AlertDialog dlg = new android.app.AlertDialog.Builder(this)
            .setCustomTitle(titulo).setView(sv).setPositiveButton(IdiomaCel.T("Cerrar"), null).create();
        dlg.show();
        if (dlg.getWindow() != null) dlg.getWindow().setBackgroundDrawable(new android.graphics.drawable.ColorDrawable(BG));
    }

    // círculo plateado con el logo de DVD (en gris si todavía no lo tienes)
    android.graphics.Bitmap iconoLogro(boolean ganado) {
        int S = dp(40);
        android.graphics.Bitmap b = android.graphics.Bitmap.createBitmap(S, S, android.graphics.Bitmap.Config.ARGB_8888);
        android.graphics.Canvas c = new android.graphics.Canvas(b);
        android.graphics.Paint p = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG | android.graphics.Paint.FILTER_BITMAP_FLAG);
        p.setShader(new android.graphics.LinearGradient(0, 0, S, S, ganado ? Color.rgb(235, 235, 240) : Color.rgb(110, 110, 118),
            ganado ? Color.rgb(120, 120, 128) : Color.rgb(60, 60, 66), android.graphics.Shader.TileMode.CLAMP));
        c.drawCircle(S / 2f, S / 2f, S / 2f, p);
        p.setShader(null); p.setColor(Color.rgb(22, 22, 26));
        c.drawCircle(S / 2f, S / 2f, S / 2f - dp(3), p);
        android.graphics.Bitmap logo = android.graphics.BitmapFactory.decodeResource(getResources(), R.drawable.logo_dvd);
        if (logo != null) {
            float ri = S / 2f - dp(3);
            float kk = Math.min(ri * 2 * 0.78f / logo.getWidth(), ri * 2 * 0.6f / logo.getHeight());
            float lw = logo.getWidth() * kk, lh = logo.getHeight() * kk;
            p.setColorFilter(new android.graphics.PorterDuffColorFilter(ganado ? Color.rgb(0, 191, 255) : Color.rgb(90, 90, 100), android.graphics.PorterDuff.Mode.SRC_IN));
            c.drawBitmap(logo, null, new android.graphics.RectF(S / 2f - lw / 2, S / 2f - lh / 2, S / 2f + lw / 2, S / 2f + lh / 2), p);
        }
        return b;
    }

    // ---------- código secreto SECRET ----------
    // La ventana "NO ESCAPE" y los cuadros de "Error" los muestra ServicioSecret ENCIMA de todas las apps,
    // así siguen en pantalla y funcionando aunque te salgas de la app (igual que en la PC).
    void abrirSecret() {
        if (!Settings.canDrawOverlays(this)) {
            android.widget.Toast.makeText(this, IdiomaCel.T("Primero dale permiso de mostrarse encima de otras apps"), android.widget.Toast.LENGTH_LONG).show();
            btnPermiso.performClick();
            return;
        }
        if (!ServicioSecret.abierto) startForegroundService(new Intent(this, ServicioSecret.class));
    }
    // el signo de advertencia amarillo de Windows (triángulo con "!")
    static class SignoAdvertencia extends View {
        final android.graphics.Paint p = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        final android.graphics.Path tri = new android.graphics.Path();
        SignoAdvertencia(android.content.Context c) { super(c); }
        @Override protected void onDraw(android.graphics.Canvas c) {
            float w = getWidth(), h = getHeight();
            tri.reset();
            tri.moveTo(w / 2f, h * 0.06f); tri.lineTo(w * 0.97f, h * 0.92f); tri.lineTo(w * 0.03f, h * 0.92f); tri.close();
            p.setStyle(android.graphics.Paint.Style.FILL); p.setColor(Color.rgb(255, 200, 40));
            p.setPathEffect(new android.graphics.CornerPathEffect(w * 0.12f));
            c.drawPath(tri, p);
            p.setStyle(android.graphics.Paint.Style.STROKE); p.setStrokeWidth(w * 0.05f); p.setColor(Color.rgb(170, 125, 0));
            c.drawPath(tri, p);
            p.setPathEffect(null);
            p.setStyle(android.graphics.Paint.Style.FILL); p.setColor(Color.BLACK);
            c.drawRoundRect(w * 0.455f, h * 0.33f, w * 0.545f, h * 0.66f, w * 0.04f, w * 0.04f, p);
            c.drawCircle(w / 2f, h * 0.77f, w * 0.055f, p);
        }
    }
    // letras "cursed": a cada letra se le encima un montón de acentos raros arriba y abajo.
    // No se usan los que tachan la letra por en medio, para que el texto se siga leyendo bien.
    static final int[] RANGOS_ARRIBA = { 0x300, 0x315, 0x33D, 0x344, 0x346, 0x346, 0x34A, 0x34C, 0x350, 0x352, 0x357, 0x357, 0x35B, 0x35B, 0x363, 0x36F };
    static final int[] RANGOS_ABAJO = { 0x316, 0x319, 0x31C, 0x333, 0x339, 0x33C, 0x345, 0x345, 0x347, 0x349, 0x34D, 0x34E, 0x353, 0x356, 0x359, 0x35A };
    static char[] marcas(int[] rangos) {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < rangos.length; i += 2) for (int c = rangos[i]; c <= rangos[i + 1]; c++) b.append((char) c);
        return b.toString().toCharArray();
    }
    static final char[] MARCAS_ARRIBA = marcas(RANGOS_ARRIBA), MARCAS_ABAJO = marcas(RANGOS_ABAJO);
    static String maldecir(String s, int min, int max) {
        java.util.Random r = new java.util.Random();
        StringBuilder b = new StringBuilder();
        for (char ch : s.toCharArray()) {
            b.append(ch);
            if (ch == ' ' || ch == ',' || ch == '.') continue;
            int n = min + r.nextInt(max - min + 1);
            for (int i = 0; i < n; i++) {
                char[] m = (i % 2 == 0) ? MARCAS_ARRIBA : MARCAS_ABAJO;
                b.append(m[r.nextInt(m.length)]);
            }
        }
        return b.toString();
    }

    // Estática de tele vieja (nieve de canal sin señal), lo más real posible: grano nuevo en cada cuadro
    // un poco corrido a lo ancho, líneas de barrido, franjas que ruedan, a veces un bloque rasgado,
    // flashazos, rayas blancas, tinte frío y orillas más oscuras.
    static class Estatica extends View {
        final java.util.Random rnd = new java.util.Random();
        final android.graphics.Paint p = new android.graphics.Paint(android.graphics.Paint.FILTER_BITMAP_FLAG);
        final android.graphics.Rect destino = new android.graphics.Rect();
        android.graphics.Bitmap bmp;
        int[] px, tmp;
        float[] vineta;
        int paso, rasgaIni = -1, rasgaFin = -1, rasgaDx;
        int semilla = 0x2545F491;

        Estatica(android.content.Context c) { super(c); }

        // La estática no pide tamaño propio: solo llena lo que le den. (Antes pedía "lo más grande posible"
        // y eso estiraba los cuadros de Error hasta ocupar casi toda la pantalla.)
        @Override protected void onMeasure(int anchoSpec, int altoSpec) {
            int w = MeasureSpec.getMode(anchoSpec) == MeasureSpec.EXACTLY ? MeasureSpec.getSize(anchoSpec) : 0;
            int h = MeasureSpec.getMode(altoSpec) == MeasureSpec.EXACTLY ? MeasureSpec.getSize(altoSpec) : 0;
            setMeasuredDimension(w, h);
        }

        int azar() { semilla ^= semilla << 13; semilla ^= semilla >>> 17; semilla ^= semilla << 5; return semilla; }

        @Override protected void onDraw(android.graphics.Canvas c) {
            int w = Math.max(1, getWidth() / 2), h = Math.max(1, getHeight() / 2);   // media resolución + suavizado
            if (bmp == null || bmp.getWidth() != w || bmp.getHeight() != h) {
                bmp = android.graphics.Bitmap.createBitmap(w, h, android.graphics.Bitmap.Config.ARGB_8888);
                px = new int[w * h]; tmp = new int[w];
                vineta = new float[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
                    float nx = (x - w / 2f) / (w / 2f), ny = (y - h / 2f) / (h / 2f);
                    vineta[y * w + x] = 1f - 0.28f * Math.min(1f, (nx * nx + ny * ny) * 0.6f);
                }
            }
            paso++;
            semilla ^= rnd.nextInt() | 1;
            int rueda1 = (paso * 2) % (h + 30) - 15;
            int rueda2 = h - paso % (h + 40) + 20;
            int brillo = rnd.nextInt(100) < 4 ? 30 : 6 - rnd.nextInt(13);
            if (rasgaIni < 0 && rnd.nextInt(14) == 0) {
                rasgaIni = rnd.nextInt(h); rasgaFin = rasgaIni + 2 + rnd.nextInt(Math.max(3, h / 6)); rasgaDx = rnd.nextInt(41) - 20;
            } else if (rasgaIni >= 0 && rnd.nextInt(3) == 0) rasgaIni = -1;
            for (int y = 0; y < h; y++) {
                boolean rayaBlanca = ((azar() & 0x7fffffff) % 90) == 0;
                int d1 = Math.abs(y - rueda1), d2 = Math.abs(y - rueda2);
                int sombra = d1 < 7 ? (7 - d1) * 8 : 0;
                int luz = d2 < 10 ? (10 - d2) * 4 : 0;
                int barrido = (y & 1) == 0 ? 0 : 22;
                boolean rasgada = y >= rasgaIni && y < rasgaFin;
                int ruidoFila = ((azar() & 0x7fffffff) % 21) - 10;
                int prev = azar() & 255;
                int fila = y * w;
                for (int x = 0; x < w; x++) {
                    int n = ((azar() & 255) + (azar() & 255)) >> 1;
                    int v = (n * 5 + prev * 3) >> 3;
                    prev = v;
                    v = 70 + (v * 185 >> 8) + brillo + luz + ruidoFila - sombra - barrido;
                    if (rasgada) v = (v + 150 + ((x + rasgaDx) & 4) * 8) >> 1;
                    if (rayaBlanca) v = 225 + (n & 31);
                    float k = vineta[fila + x];
                    int rr = (int) (v * k), bb = (int) ((v + 10) * k);
                    if (rr < 0) rr = 0; else if (rr > 255) rr = 255;
                    if (bb < 0) bb = 0; else if (bb > 255) bb = 255;
                    px[fila + x] = 0xFF000000 | (rr << 16) | (rr << 8) | bb;
                }
                if (rasgada && rasgaDx != 0) {
                    for (int x = 0; x < w; x++) tmp[x] = px[fila + (((x - rasgaDx) % w) + w) % w];
                    System.arraycopy(tmp, 0, px, fila, w);
                }
            }
            bmp.setPixels(px, 0, w, 0, 0, w, h);
            destino.set(0, 0, getWidth(), getHeight());
            c.drawBitmap(bmp, null, destino, p);
            postInvalidateDelayed(40);
        }
    }

    void abrir() {
        if (!Settings.canDrawOverlays(this)) { btnPermiso.performClick(); return; }
        if (!DvdService.corriendo) startForegroundService(new Intent(this, DvdService.class));
        handler.postDelayed(this::refreshEstado, 300);
    }

    void cerrar() {
        stopService(new Intent(this, DvdService.class));
        handler.postDelayed(this::refreshEstado, 300);
    }

    void changed() {
        if (cargando) return;
        cfg.save(this);
        refreshJson();
    }

    void refreshJson() { json.setText(cfg.toJsonText()); }

    // ---------- construcción de la interfaz ----------
    int dp(float v) { return Math.round(v * d); }

    LinearLayout.LayoutParams full(int h) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, h);
        lp.topMargin = dp(6);
        return lp;
    }

    void espacio(int h) { View v = new View(this); root.addView(v, new LinearLayout.LayoutParams(1, dp(h))); }

    TextView text(String s, float sp, int color) {
        TextView t = new TextView(this);
        t.setText(s); t.setTextSize(sp); t.setTextColor(color);
        t.setPadding(0, dp(3), 0, dp(3));
        return t;
    }

    GradientDrawable caja(int fondo, int borde) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(fondo); g.setStroke(dp(1), borde); g.setCornerRadius(dp(2));
        return g;
    }

    // Fila con etiqueta a la izquierda (130dp), como en dvd_config
    LinearLayout fila(String etiqueta) {
        LinearLayout f = new LinearLayout(this);
        f.setOrientation(LinearLayout.HORIZONTAL);
        f.setGravity(Gravity.CENTER_VERTICAL);
        f.setPadding(0, dp(5), 0, dp(5));
        TextView t = text(etiqueta, 14, FG);
        f.addView(t, new LinearLayout.LayoutParams(dp(130), LinearLayout.LayoutParams.WRAP_CONTENT));
        return f;
    }

    EditText numero(int min, int max, int valor, IntConsumer onChange) {
        EditText e = new EditText(this);
        e.setInputType(InputType.TYPE_CLASS_NUMBER);
        e.setText(String.valueOf(valor));
        e.setTextColor(FG); e.setTextSize(14);
        e.setSingleLine(true);
        e.setPadding(dp(8), 0, dp(4), 0);
        e.setBackground(caja(PANEL, BORDE));
        e.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence c, int a, int b, int x) { }
            public void onTextChanged(CharSequence c, int a, int b, int x) { }
            public void afterTextChanged(Editable ed) {
                if (cargando) return;
                try {
                    long v = Long.parseLong(ed.toString().trim());
                    int n = (int) Math.max(min, Math.min(max, v));
                    if (n != v) { cargando = true; ed.replace(0, ed.length(), String.valueOf(n)); cargando = false; }
                    onChange.accept(n);
                } catch (NumberFormatException ignored) { }
            }
        });
        return e;
    }

    // Barra + casilla de número (como TrackBar + NumericUpDown en Windows)
    void slider(String etiqueta, int min, int max, int valor, IntConsumer onChange) {
        sliderCon(etiqueta, min, max, max, valor, onChange);
    }

    void sliderCon(String etiqueta, int min, int barraMax, int max, int valor, IntConsumer onChange) {
        LinearLayout f = fila(etiqueta);
        SeekBar s = new SeekBar(this);
        s.setMin(min); s.setMax(barraMax); s.setProgress(Math.max(min, Math.min(barraMax, valor)));
        s.setProgressTintList(ColorStateList.valueOf(AZUL));
        s.setThumbTintList(ColorStateList.valueOf(AZUL));
        EditText n = numero(min, max, valor, v -> {
            cargando = true; s.setProgress(Math.min(barraMax, v)); cargando = false;
            onChange.accept(v);
        });
        s.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            public void onProgressChanged(SeekBar sb, int v, boolean user) {
                if (!user || cargando) return;
                cargando = true; n.setText(String.valueOf(v)); cargando = false;
                onChange.accept(v);
            }
            public void onStartTrackingTouch(SeekBar sb) { }
            public void onStopTrackingTouch(SeekBar sb) { }
        });
        f.addView(s, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(dp(76), dp(36));
        lp.leftMargin = dp(6);
        f.addView(n, lp);
        root.addView(f);
    }

    // Cantidad de DVDs: limitada según la RAM del cel
    void cantidad() {
        int limite = Config.limiteCantidad(this);
        if (cfg.cantidad > limite) { cfg.cantidad = limite; cfg.save(this); }
        sliderCon("Cantidad de DVDs", 1, Math.min(200, limite), limite, cfg.cantidad, v -> { cfg.cantidad = v; changed(); });
        TextView aviso = text(Config.textoLimite(this) +
            "  ·  con más de " + DvdView.LIMITE_MODO_LIGERO + " se activa el modo ligero", 12,
            limite >= Config.MAX_CANTIDAD ? MUTED : NARANJA);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.leftMargin = dp(130);
        root.addView(aviso, lp);
    }

    interface BoolConsumer { void accept(boolean b); }

    CheckBox check(String etiqueta, boolean valor, BoolConsumer onChange) {
        CheckBox c = new CheckBox(this);
        c.setText(etiqueta); c.setTextColor(FG); c.setTextSize(14);
        c.setChecked(valor);
        c.setButtonTintList(ColorStateList.valueOf(AZUL));
        c.setOnCheckedChangeListener((bt, on) -> onChange.accept(on));
        root.addView(c, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        return c;
    }

    Button boton(String s, View.OnClickListener l) {
        Button b = new Button(this);
        b.setText(s); b.setAllCaps(false); b.setTextColor(FG); b.setTextSize(14);
        b.setBackground(caja(PANEL, BORDE));
        b.setOnClickListener(l);
        return b;
    }

    GridLayout.LayoutParams celda() {
        GridLayout.LayoutParams lp = new GridLayout.LayoutParams(GridLayout.spec(GridLayout.UNDEFINED, 1f), GridLayout.spec(GridLayout.UNDEFINED, 1f));
        lp.width = 0; lp.height = dp(42);
        lp.setMargins(0, dp(4), dp(8), dp(4));
        return lp;
    }

    void pintarBotonColor() {
        int c = cfg.color;
        btnColor.setText(Config.toHex(c));
        btnColor.setBackground(caja(c, BORDE));
        double lum = Color.red(c) * 0.3 + Color.green(c) * 0.59 + Color.blue(c) * 0.11;
        btnColor.setTextColor(lum > 140 ? Color.BLACK : Color.WHITE);
    }

    // Selector de color (como el ColorDialog de Windows, con colores fijos)
    // ---------- logo personalizado: imagen ----------
    static final int PEDIR_IMAGEN = 7;
    Button btnImagen;

    void pintarBotonImagen() {
        boolean hay = cfg.logoImagen && Config.archivoImagen(this).exists();
        btnImagen.setText(hay ? "✓ Imagen puesta (cambiar)" : "Elegir imagen...");
        btnImagen.setTextColor(hay ? VERDE : FG);
    }

    @Override
    protected void onActivityResult(int req, int res, Intent data) {
        super.onActivityResult(req, res, data);
        if (req != PEDIR_IMAGEN || res != RESULT_OK || data == null || data.getData() == null) return;
        try (java.io.InputStream in = getContentResolver().openInputStream(data.getData())) {
            android.graphics.Bitmap img = android.graphics.BitmapFactory.decodeStream(in);
            if (img == null) throw new Exception("no es una imagen");
            // se guarda una copia más pequeña (máx. 512 px) dentro de la app
            float k = Math.min(1f, 512f / Math.max(img.getWidth(), img.getHeight()));
            if (k < 1f) img = android.graphics.Bitmap.createScaledBitmap(img, Math.round(img.getWidth() * k), Math.round(img.getHeight() * k), true);
            try (java.io.FileOutputStream out = new java.io.FileOutputStream(Config.archivoImagen(this))) {
                img.compress(android.graphics.Bitmap.CompressFormat.PNG, 100, out);
            }
            cfg.logoImagen = true;
            pintarBotonImagen();
            changed();
        } catch (Exception e) {
            android.widget.Toast.makeText(this, IdiomaCel.T("No se pudo usar esa imagen"), android.widget.Toast.LENGTH_SHORT).show();
        }
    }

    void elegirColor() {
        GridLayout g = new GridLayout(this);
        g.setColumnCount(6);
        g.setPadding(dp(16), dp(16), dp(16), dp(8));
        AlertDialog[] dlg = new AlertDialog[1];
        for (int c : COLORES) {
            View v = new View(this);
            GradientDrawable gd = new GradientDrawable();
            gd.setColor(c);
            gd.setStroke(dp(c == cfg.color ? 3 : 1), c == cfg.color ? Color.WHITE : BORDE);
            v.setBackground(gd);
            GridLayout.LayoutParams lp = new GridLayout.LayoutParams();
            lp.width = dp(40); lp.height = dp(40);
            lp.setMargins(dp(4), dp(4), dp(4), dp(4));
            v.setOnClickListener(x -> {
                cfg.color = c;
                pintarBotonColor();
                changed();
                if (dlg[0] != null) dlg[0].dismiss();
            });
            g.addView(v, lp);
        }
        dlg[0] = new AlertDialog.Builder(this).setTitle("Color").setView(g).setNegativeButton("Cancelar", null).show();
    }
}
