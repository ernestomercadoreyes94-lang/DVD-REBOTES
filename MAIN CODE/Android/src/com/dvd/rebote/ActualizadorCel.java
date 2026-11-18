package com.dvd.rebote;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.Gravity;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.BufferedReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.security.MessageDigest;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

// "Buscar actualizaciones" del cel: revisa el lanzamiento "Latest" de GitHub (DVD-REBOTES) y, si trae una
// DvdRebote.apk más nueva que la instalada, enseña los cambios en una CONSOLA FALSA y pregunta
// "¿Quieres instalar la actualización? (S/N)". Con S la baja, revisa que llegó completa (huella SHA-256
// que da GitHub) y abre el instalador de Android. La APK nueva está firmada con la misma llave.
public class ActualizadorCel {
    // TODOS los lanzamientos (también las pre-releases): se usa el más nuevo
    static final String API = "https://api.github.com/repos/ernestomercadoreyes94-lang/DVD-REBOTES/releases?per_page=20";
    static boolean ocupado;

    final Activity a;
    final Handler h = new Handler(Looper.getMainLooper());
    final List<String> porEscribir = new ArrayList<>();
    TextView texto; ScrollView scroll; LinearLayout botones; AlertDialog dlg;
    String url, huella; long tamano;

    ActualizadorCel(Activity a) { this.a = a; }

    public static void buscar(Activity a) {
        if (ocupado) return;
        ocupado = true;
        new ActualizadorCel(a).empezar();
    }

    int dp(int v) { return (int) (v * a.getResources().getDisplayMetrics().density); }

    // ---------- la consola (estilo cmd de Windows) ----------
    final StringBuilder contenido = new StringBuilder();
    String lineaPregunta = "";      // la pregunta que espera respuesta (con el cursor al final)
    boolean cursorPrendido = true;

    void abrirConsola() {
        LinearLayout raiz = new LinearLayout(a);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setBackgroundColor(Color.rgb(12, 12, 12));
        // barra de título como la de Windows: icono de cmd, la ruta y los botones — ☐ ✕
        LinearLayout barra = new LinearLayout(a);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(Color.WHITE);
        barra.setPadding(dp(8), 0, 0, 0);
        android.widget.ImageView ic = new android.widget.ImageView(a);
        ic.setImageResource(R.drawable.icono_cmd);
        barra.addView(ic, new LinearLayout.LayoutParams(dp(16), dp(16)));
        TextView titulo = new TextView(a);
        titulo.setText("C:\\DVD\\buscar_updates.exe");
        titulo.setTextColor(Color.rgb(30, 30, 30)); titulo.setTextSize(12);
        titulo.setSingleLine(true);
        titulo.setPadding(dp(8), 0, 0, 0);
        barra.addView(titulo, new LinearLayout.LayoutParams(0, dp(32), 1f));
        titulo.setGravity(Gravity.CENTER_VERTICAL);
        // — minimiza, ☐ maximiza (❐ restaura), ✕ cierra; al apretarlos se marcan como en Windows
        for (String g : new String[] { "—", "☐", "✕" }) {
            TextView b = new TextView(a);
            b.setText(g); b.setTextSize(13); b.setGravity(Gravity.CENTER);
            b.setTextColor(Color.rgb(30, 30, 30));
            final boolean esX = g.equals("✕");
            if (esX) b.setOnClickListener(v -> dlg.dismiss());
            else if (g.equals("—")) b.setOnClickListener(v -> { if (minimizada) restaurar(); else minimizar(); });
            else { btnMax = b; b.setOnClickListener(v -> { maximizada = !maximizada; if (minimizada) restaurar(); else aplicarTamano(); }); }
            b.setOnTouchListener((v, ev) -> {
                if (ev.getAction() == android.view.MotionEvent.ACTION_DOWN) {
                    v.setBackgroundColor(esX ? Color.rgb(232, 17, 35) : Color.rgb(229, 229, 229));
                    if (esX) ((TextView) v).setTextColor(Color.WHITE);
                } else if (ev.getAction() == android.view.MotionEvent.ACTION_UP || ev.getAction() == android.view.MotionEvent.ACTION_CANCEL) {
                    v.setBackgroundColor(Color.TRANSPARENT); ((TextView) v).setTextColor(Color.rgb(30, 30, 30));
                }
                return false;
            });
            barra.addView(b, new LinearLayout.LayoutParams(dp(46), dp(32)));
        }
        // tocar la barra minimizada la restaura; doble toque maximiza / restaura (como en Windows)
        barra.setOnClickListener(v -> {
            long ahora = System.currentTimeMillis();
            if (minimizada) restaurar();
            else if (ahora - ultimoToque < 350) { maximizada = !maximizada; aplicarTamano(); ahora = 0; }
            ultimoToque = ahora;
        });
        barra.setOnTouchListener(this::arrastrar);   // arrastrando la barra se mueve la ventana
        raiz.addView(barra);
        scroll = new ScrollView(a);
        scroll.setBackgroundColor(Color.rgb(12, 12, 12));
        texto = new TextView(a);
        texto.setTypeface(Typeface.MONOSPACE); texto.setTextSize(12.5f);
        texto.setTextColor(Color.rgb(204, 204, 204));
        texto.setLineSpacing(0, 1.08f);
        texto.setPadding(dp(10), dp(8), dp(10), dp(8));
        scroll.addView(texto);   // se puede subir y bajar con el dedo
        raiz.addView(scroll, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(400)));
        botones = new LinearLayout(a);
        botones.setGravity(Gravity.END);
        botones.setBackgroundColor(Color.rgb(12, 12, 12));
        botones.setPadding(dp(8), dp(4), dp(8), dp(8));
        raiz.addView(botones);
        dlg = new AlertDialog.Builder(a).setView(raiz).setCancelable(true).create();
        dlg.setCanceledOnTouchOutside(false);   // como cmd: solo se cierra con la X (o con Cerrar / atrás)
        dlg.setOnDismissListener(d -> ocupado = false);
        dlg.show();
        if (dlg.getWindow() != null) {
            dlg.getWindow().setBackgroundDrawable(new android.graphics.drawable.ColorDrawable(Color.rgb(12, 12, 12)));
            dlg.getWindow().setLayout(android.view.WindowManager.LayoutParams.MATCH_PARENT, android.view.WindowManager.LayoutParams.WRAP_CONTENT);
        }
        parpadear();
    }

    // ---------- minimizar / maximizar ----------
    boolean minimizada, maximizada;
    long ultimoToque;
    TextView btnMax;

    // minimizada: solo la barra de título, chiquita abajo a la izquierda; la app de atrás se puede usar
    // y la consola sigue trabajando (buscando o descargando)
    void minimizar() {
        android.view.Window w = dlg.getWindow();
        if (w == null) return;
        minimizada = true;
        scroll.setVisibility(android.view.View.GONE);
        botones.setVisibility(android.view.View.GONE);
        android.view.WindowManager.LayoutParams lp = w.getAttributes();
        lp.gravity = Gravity.BOTTOM | Gravity.START; lp.x = 0; lp.y = 0;
        lp.flags &= ~android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;
        w.setAttributes(lp);
        w.setLayout(dp(280), android.view.WindowManager.LayoutParams.WRAP_CONTENT);
        w.clearFlags(android.view.WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        w.addFlags(android.view.WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL);
    }

    void restaurar() {
        android.view.Window w = dlg.getWindow();
        if (w == null) return;
        minimizada = false;
        scroll.setVisibility(android.view.View.VISIBLE);
        botones.setVisibility(android.view.View.VISIBLE);
        w.addFlags(android.view.WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        w.clearFlags(android.view.WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL);
        aplicarTamano();
    }

    // ---------- mover la ventana arrastrando la barra de título ----------
    boolean movida, arrastrando;     // movida: ya no está en el centro, se queda donde la dejaste
    int posX, posY, desdeX, desdeY;
    float tocoX, tocoY;

    // pone la ventana en su lugar: centrada, o donde la moviste (maximizada siempre llena la pantalla)
    void ponerPosicion() {
        android.view.Window w = dlg.getWindow();
        if (w == null) return;
        android.view.WindowManager.LayoutParams lp = w.getAttributes();
        if (movida && !maximizada) {
            lp.gravity = Gravity.TOP | Gravity.START;
            lp.x = posX; lp.y = posY;
            lp.flags |= android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;   // coordenadas de toda la pantalla
        } else {
            lp.gravity = Gravity.CENTER;
            lp.x = 0; lp.y = 0;
            lp.flags &= ~android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;
        }
        w.setAttributes(lp);
    }

    boolean arrastrar(android.view.View v, android.view.MotionEvent ev) {
        android.view.Window w = dlg.getWindow();
        if (w == null) return false;
        switch (ev.getActionMasked()) {
            case android.view.MotionEvent.ACTION_DOWN: {
                tocoX = ev.getRawX(); tocoY = ev.getRawY();
                arrastrando = false;
                return false;   // todavía puede ser un toque (restaurar / doble toque)
            }
            case android.view.MotionEvent.ACTION_MOVE: {
                float dx = ev.getRawX() - tocoX, dy = ev.getRawY() - tocoY;
                if (!arrastrando) {
                    if (Math.hypot(dx, dy) < dp(8)) return false;
                    arrastrando = true;
                    v.setPressed(false); v.cancelLongPress();
                    if (maximizada && !minimizada) {   // como en Windows: al arrastrar una maximizada se restaura
                        maximizada = false;
                        aplicarTamano();
                    }
                    int[] p = new int[2];
                    w.getDecorView().getLocationOnScreen(p);
                    if (maximizada || !movida && !minimizada) {   // centrada: que no brinque, empieza desde donde está
                        android.util.DisplayMetrics dm = a.getResources().getDisplayMetrics();
                        p[0] = Math.max(0, (dm.widthPixels - w.getDecorView().getWidth()) / 2);
                        p[1] = Math.max(0, p[1]);
                    }
                    desdeX = p[0]; desdeY = p[1];
                    movida = true;
                }
                // no se puede sacar del todo: siempre queda visible un pedazo de la barra de título
                android.util.DisplayMetrics dm = a.getResources().getDisplayMetrics();
                int ancho = w.getDecorView().getWidth();
                posX = Math.max(dp(90) - ancho, Math.min(dm.widthPixels - dp(90), desdeX + (int) dx));
                posY = Math.max(0, Math.min(dm.heightPixels - dp(40), desdeY + (int) dy));
                if (minimizada) {
                    android.view.WindowManager.LayoutParams lp = w.getAttributes();
                    lp.gravity = Gravity.TOP | Gravity.START; lp.x = posX; lp.y = posY;
                    lp.flags |= android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;
                    w.setAttributes(lp);
                } else ponerPosicion();
                return true;
            }
            case android.view.MotionEvent.ACTION_UP:
            case android.view.MotionEvent.ACTION_CANCEL:
                if (arrastrando) { arrastrando = false; v.setPressed(false); ultimoToque = 0; return true; }   // fue arrastre, no toque
                return false;
        }
        return false;
    }

    // normal (400 dp de alto) o maximizada (toda la pantalla)
    void aplicarTamano() {
        android.view.Window w = dlg.getWindow();
        if (w == null) return;
        // maximizada: el texto ocupa la pantalla menos la barra de título, las teclas y las barras de Android
        int alto = maximizada ? Math.max(dp(400), a.getResources().getDisplayMetrics().heightPixels - dp(32 + 58 + 90)) : dp(400);
        scroll.setLayoutParams(new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, alto));
        w.setLayout(android.view.WindowManager.LayoutParams.MATCH_PARENT,
            maximizada ? android.view.WindowManager.LayoutParams.MATCH_PARENT : android.view.WindowManager.LayoutParams.WRAP_CONTENT);
        ponerPosicion();
        if (btnMax != null) btnMax.setText(maximizada ? "❐" : "☐");
        scroll.post(() -> scroll.fullScroll(ScrollView.FOCUS_DOWN));
    }

    // cursor "_" que parpadea al final, como en cmd
    void parpadear() {
        if (dlg == null || !dlg.isShowing()) return;
        cursorPrendido = !cursorPrendido;
        pintar();
        h.postDelayed(this::parpadear, 500);
    }

    void pintar() {
        if (texto == null) return;
        String cur = porEscribir.isEmpty() ? (cursorPrendido ? "_" : " ") : "";
        android.text.SpannableStringBuilder sb = new android.text.SpannableStringBuilder(contenido);
        sb.append(lineaPregunta).append(cur);
        colorear(sb);
        texto.setText(sb);
    }

    // verde la versión nueva, rojo los errores, amarillo la pregunta (como el de la PC)
    static void colorear(android.text.SpannableStringBuilder sb) {
        String s = sb.toString();
        int ini = 0;
        for (String l : s.split("\n", -1)) {
            int col = 0;
            if (l.startsWith("¡Encontré") || l.startsWith("Found a new")) col = Color.rgb(110, 220, 130);
            else if (l.startsWith("ERROR")) col = Color.rgb(255, 110, 110);
            else if (l.startsWith("!!")) col = Color.rgb(255, 150, 60);   // aviso de pre-release (naranja)
            else if (l.startsWith("¿Quieres instalar") || l.startsWith("Do you want to install")) col = Color.rgb(255, 210, 90);
            if (col != 0) sb.setSpan(new android.text.style.ForegroundColorSpan(col), ini, ini + l.length(), 0);
            ini += l.length() + 1;
        }
    }

    // líneas una por una (como si se escribieran), ya en el idioma elegido
    void escribir(String... ls) {
        boolean vacia = porEscribir.isEmpty();
        for (String l : ls) {
            String nucleo = l.replaceFirst("^\\s+", "");
            porEscribir.add(l.substring(0, l.length() - nucleo.length()) + IdiomaCel.T(nucleo));
        }
        if (vacia) siguiente();
    }

    void siguiente() {
        if (porEscribir.isEmpty() || texto == null) return;
        String l = porEscribir.remove(0);
        contenido.append(l).append("\n");
        pintar();
        scroll.post(() -> scroll.fullScroll(ScrollView.FOCUS_DOWN));
        if (!porEscribir.isEmpty()) h.postDelayed(this::siguiente, 45);
    }

    // espera a que termine de escribir y luego hace algo
    void despues(Runnable r) {
        if (porEscribir.isEmpty()) r.run(); else h.postDelayed(() -> despues(r), 60);
    }

    // botones con forma de TECLA (gris, letra de consola)
    Button boton(String t, Runnable r) {
        Button b = new Button(a);
        b.setText(t); b.setAllCaps(false);
        b.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        b.setTextColor(Color.rgb(230, 230, 230));
        android.graphics.drawable.GradientDrawable fondo = new android.graphics.drawable.GradientDrawable();
        fondo.setColor(Color.rgb(45, 45, 45)); fondo.setStroke(dp(1), Color.rgb(110, 110, 110)); fondo.setCornerRadius(dp(4));
        b.setBackground(fondo);
        b.setOnClickListener(v -> r.run());
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(t.length() <= 1 ? dp(56) : LinearLayout.LayoutParams.WRAP_CONTENT, dp(42));
        lp.leftMargin = dp(8);
        botones.addView(b, lp);
        return b;
    }

    void terminar() {
        despues(() -> {
            lineaPregunta = "";
            escribir("", "Toca \"Cerrar\" para salir.");
            botones.removeAllViews();
            boton(IdiomaCel.T("Cerrar"), () -> dlg.dismiss());
        });
    }

    // ---------- revisar ----------
    void empezar() {
        abrirConsola();
        escribir("Microsoft Windows [Versión 10.0]", "", "C:\\DVD> buscar_updates.exe", "", "Buscando updates...", "Conectando con GitHub (DVD-REBOTES)...");
        new Thread(() -> {
            String error = null, titulo = "", notas = "", subido = "";
            boolean hay = false, pre = false;
            try {
                // de la lista, el lanzamiento publicado más nuevo (normal o pre-release; los borradores no)
                JSONArray todos = new JSONArray(bajarTexto(API));
                JSONObject j = null; String masNuevo = "";
                for (int i = 0; i < todos.length(); i++) {
                    JSONObject r = todos.getJSONObject(i);
                    if (r.optBoolean("draft", false)) continue;
                    String fecha = r.optString("published_at", "");
                    if (j == null || fecha.compareTo(masNuevo) > 0) { j = r; masNuevo = fecha; }
                }
                if (j == null) throw new Exception("No hay lanzamientos en GitHub");
                pre = j.optBoolean("prerelease", false);
                titulo = j.optString("name", "") + (pre ? " (pre-release)" : "");
                notas = limpiarNotas(j.optString("body", ""));
                JSONArray assets = j.getJSONArray("assets");
                for (int i = 0; i < assets.length(); i++) {
                    JSONObject as = assets.getJSONObject(i);
                    if (!"DvdRebote.apk".equalsIgnoreCase(as.optString("name"))) continue;
                    url = as.getString("browser_download_url");
                    huella = as.optString("digest", "").replace("sha256:", "").toLowerCase(Locale.ROOT);
                    tamano = as.optLong("size", 0);
                    String fecha = as.optString("updated_at", "");
                    SimpleDateFormat f = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.ROOT);
                    f.setTimeZone(TimeZone.getTimeZone("UTC"));
                    Date d = f.parse(fecha);
                    subido = new SimpleDateFormat("dd/MM/yyyy HH:mm", Locale.ROOT).format(d);
                    // la versión de la APK es "minutos desde 1970" de cuando se compiló
                    long compilada = a.getPackageManager().getPackageInfo(a.getPackageName(), 0).getLongVersionCode() * 60_000L;
                    boolean distinta = huella.isEmpty() || !huella.equals(sha256(new File(a.getApplicationInfo().sourceDir)));
                    hay = distinta && d.getTime() > compilada + 120_000L;
                }
            } catch (Exception e) { error = e.getMessage() == null ? e.toString() : e.getMessage(); }
            final String err = error, tit = titulo, nts = notas, sub = subido;
            final boolean nueva = hay, esPre = pre;
            h.postDelayed(() -> resultado(err, nueva, tit, nts, sub, esPre), 900);
        }).start();
    }

    void resultado(String error, boolean nueva, String titulo, String notas, String subido, boolean esPre) {
        if (!dlg.isShowing()) return;
        if (error != null) {
            escribir("", "ERROR: no pude revisar si hay updates.", "Revisa tu internet e inténtalo otra vez.", "(" + error + ")");
            terminar(); return;
        }
        if (!nueva) {
            escribir("", "Ya tienes la versión más nueva del DVD. No hay nada que instalar.");
            terminar(); return;
        }
        List<String> l = new ArrayList<>();
        l.add(""); l.add("¡Encontré una versión nueva" + (titulo.isEmpty() ? "" : ": " + titulo) + "!"); l.add("");
        if (!notas.isEmpty()) {
            l.add("Esto es lo que cambia:");
            for (String n : notas.split("\n")) l.add("  " + n);
            l.add("");
        }
        l.add("  > DvdRebote.apk  (subido el " + subido + ")");
        l.add("");
        if (esPre) {
            l.add("!! AVISO: esta versión es una PRE-RELEASE (versión de prueba).");
            l.add("!! Puede tener errores. Si algo falla, instala la versión normal desde GitHub.");
            l.add("");
        }
        escribir(l.toArray(new String[0]));
        despues(() -> {
            // la pregunta se queda abajo con el cursor parpadeando; S (o Y en inglés) = sí, N = no
            lineaPregunta = IdiomaCel.T("¿Quieres instalar la actualización? (S/N)") + ": ";
            pintar();
            String si = IdiomaCel.ingles ? "Y" : "S";
            boton("N", () -> { botones.removeAllViews(); String p = lineaPregunta; lineaPregunta = ""; escribir(p + "N", "", "Actualización cancelada. Cuando quieras, vuelve a darle a Buscar actualizaciones."); terminar(); });
            boton(si, () -> { botones.removeAllViews(); String p = lineaPregunta; lineaPregunta = ""; escribir(p + si, ""); instalar(); });
        });
    }

    // ---------- bajar e instalar ----------
    void instalar() {
        if (!a.getPackageManager().canRequestPackageInstalls()) {
            // Android pide permiso para instalar apps de "fuentes desconocidas" (solo la primera vez)
            escribir("Android necesita que le des permiso a DVD Rebote para instalar apps.",
                     "Se abrió el ajuste: actívalo, regresa y vuelve a darle a Buscar actualizaciones.");
            try { a.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + a.getPackageName()))); } catch (Exception ignored) { }
            terminar(); return;
        }
        escribir("Descargando DvdRebote.apk...");
        new Thread(() -> {
            String error = null;
            File destino = ProveedorImagen.archivoApk(a);
            try {
                HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
                c.setRequestProperty("User-Agent", "DVD-Rebote-Actualizador");
                c.setInstanceFollowRedirects(true);
                long total = tamano > 0 ? tamano : c.getContentLengthLong();
                try (InputStream in = c.getInputStream(); FileOutputStream out = new FileOutputStream(destino)) {
                    byte[] buf = new byte[32768]; long hecho = 0; int n, ultimo = -1;
                    while ((n = in.read(buf)) > 0) {
                        out.write(buf, 0, n); hecho += n;
                        int pct = total > 0 ? (int) (hecho * 100 / total) : -1;
                        if (pct >= 0 && pct / 20 != ultimo) { ultimo = pct / 20; final int p = pct; h.post(() -> escribir("  " + p + " %")); }
                    }
                }
                if (!huella.isEmpty() && !huella.equals(sha256(destino))) {
                    destino.delete();
                    throw new Exception("La APK no se descargó bien (la huella no coincide).");
                }
            } catch (Exception e) { error = e.getMessage() == null ? e.toString() : e.getMessage(); destino.delete(); }
            final String err = error;
            h.post(() -> {
                if (err != null) { escribir("", "ERROR: no se pudo descargar la actualización.", "(" + err + ")"); terminar(); return; }
                escribir("Descarga completa y revisada ✓", "", "Abriendo el instalador de Android... toca \"Actualizar\".");
                despues(() -> {
                    try {
                        Intent i = new Intent(Intent.ACTION_VIEW);
                        i.setDataAndType(ProveedorImagen.URI_APK, "application/vnd.android.package-archive");
                        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
                        a.startActivity(i);
                    } catch (Exception e) { escribir("ERROR: no pude abrir el instalador (" + e.getMessage() + ")"); }
                    terminar();
                });
            });
        }).start();
    }

    // ---------- ayudas ----------
    static String bajarTexto(String u) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(u).openConnection();
        c.setRequestProperty("User-Agent", "DVD-Rebote-Actualizador");
        c.setConnectTimeout(15000); c.setReadTimeout(15000);
        StringBuilder sb = new StringBuilder();
        try (BufferedReader r = new BufferedReader(new InputStreamReader(c.getInputStream(), "UTF-8"))) {
            String l; while ((l = r.readLine()) != null) sb.append(l).append('\n');
        }
        return sb.toString();
    }

    static String sha256(File f) throws Exception {
        MessageDigest md = MessageDigest.getInstance("SHA-256");
        try (FileInputStream in = new FileInputStream(f)) {
            byte[] buf = new byte[65536]; int n;
            while ((n = in.read(buf)) > 0) md.update(buf, 0, n);
        }
        StringBuilder sb = new StringBuilder();
        for (byte b : md.digest()) sb.append(String.format("%02x", b));
        return sb.toString();
    }

    // las notas de GitHub vienen en Markdown: se dejan como texto y se quita "Cómo actualizar / Cómo instalar"
    static String limpiarNotas(String md) {
        StringBuilder sb = new StringBuilder();
        boolean blanco = false;
        for (String crudo : md.replace("\r", "").split("\n")) {
            String l = crudo.trim();
            if (l.startsWith("#")) {
                String t = l.replaceAll("^#+", "").trim();
                if (t.startsWith("Cómo") || t.startsWith("Como")) break;
                if (l.startsWith("## ") || l.startsWith("# ")) continue;
                l = t.toUpperCase(Locale.ROOT);
            } else if (l.startsWith("- ") || l.startsWith("* ")) l = "•  " + l.substring(2);
            l = l.replace("**", "").replace("`", "");
            if (l.isEmpty()) { if (!blanco && sb.length() > 0) sb.append("\n"); blanco = true; continue; }
            blanco = false;
            sb.append(l).append("\n");
        }
        return sb.toString().trim();
    }
}
