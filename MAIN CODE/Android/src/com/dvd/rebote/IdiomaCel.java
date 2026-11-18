package com.dvd.rebote;

import android.content.Context;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.TextView;

// IDIOMA del cel: español (como está escrita la app) o inglés. T("texto en español") devuelve el texto
// en inglés si está puesto. Los textos cortos solo si son exactos; las frases largas también dentro
// de textos más grandes. Se guarda en las preferencias "ajustes" (clave "idioma").
public class IdiomaCel {
    public static volatile boolean ingles;

    static void cargar(Context c) {
        try { ingles = "en".equals(c.getSharedPreferences("ajustes", Context.MODE_PRIVATE).getString("idioma", "es")); } catch (Exception ignored) { }
    }

    static void guardar(Context c, boolean en) {
        ingles = en;
        c.getSharedPreferences("ajustes", Context.MODE_PRIVATE).edit().putString("idioma", en ? "en" : "es").apply();
    }

    static final String[][] EXACTOS = {
        { "Configuración del DVD", "DVD Settings" }, { "Estilo", "Style" }, { "Logo DVD", "DVD logo" }, { "Disco", "Disc" },
        { "Color", "Color" }, { "Quitar", "Remove" }, { "Pausado", "Paused" }, { "Cheats", "Cheats" }, { "Cerrar", "Close" },
        { "Cancelar", "Cancel" }, { "Logros", "Achievements" }, { "🏆 Logros", "🏆 Achievements" },
        { "⟳ Buscar actualizaciones", "⟳ Check for updates" }, { "▶ Abrir DVD", "▶ Open DVD" }, { "■ Cerrar DVD", "■ Close DVD" },
        { "Reiniciar contador", "Reset counter" }, { "Valores por defecto", "Reset defaults" }, { "Código secreto", "Secret code" },
        { "Usar", "Use" }, { "Elegir imagen...", "Choose image..." }, { "✓ Imagen puesta (cambiar)", "✓ Image set (change)" },
        { "Código incorrecto", "Wrong code" }, { "Imagen del logo", "Logo image" }, { "Texto del logo", "Logo text" },
        { "bronce", "bronze" }, { "plata", "silver" }, { "oro", "gold" }, { "BRONCE", "BRONZE" }, { "PLATA", "SILVER" }, { "ORO", "GOLD" },
        { "Pequeños pasos", "Baby steps" }, { "¿Cuánto tiempo tienes?", "How much free time do you have?" }, { "¿En serio llegamos aquí?", "Did we really get here?" },
        { "Consigue un logro", "Get one achievement" }, { "Consigue la mitad de los logros (4 de 7)", "Get half of the achievements (4 of 7)" },
        { "Consigue todos los logros", "Get all the achievements" },
        { "diamante", "diamond" }, { "DIAMANTE", "DIAMOND" }, { "Bro consigue un trabajo", "Bro, get a job" },
        { "Consigue 4 logros", "Get 4 achievements" }, { "Consigue 7 logros", "Get 7 achievements" }, { "Consigue todos los logros (11)", "Get all the achievements (11)" }, { "Consigue todos los logros (14)", "Get all the achievements (14)" },
        { "400G: Esquinero leyenda (10000)", "400G: Legendary corner hitter (10000)" }, { "150G: Esquina doble", "150G: Double corner" },
        { "100G: Racha de esquinas", "100G: Corner streak" }, { "80G: Noctámbulo", "80G: Night owl" },
        { "Esquinero leyenda (10000)", "Legendary corner hitter (10000)" }, { "Esquina doble", "Double corner" },
        { "Racha de esquinas", "Corner streak" }, { "Noctámbulo", "Night owl" },
        { "200G: Esquinero profesional (2500)", "200G: Professional corner hitter (2500)" }, { "300G: El DVD ya te conoce (5000)", "300G: The DVD knows you now (5000)" },
        { "150G: Esto ya es un trabajo (10 h)", "150G: This is a job now (10 h)" }, { "500G: Un día entero rebotando (24 h)", "500G: A whole day bouncing (24 h)" },
        { "Trofeo de diamante de prueba", "Test diamond trophy" },
        { "◆ Tienda", "◆ Shop" }, { "Colores normales", "Normal colors" }, { "Reloj", "Clock" },
        { "!! AVISO: esta versión es una PRE-RELEASE (versión de prueba).", "!! WARNING: this version is a PRE-RELEASE (test version)." },
        { "!! Puede tener errores. Si algo falla, instala la versión normal desde GitHub.", "!! It may have bugs. If something fails, install the normal version from GitHub." },
        { "10G: Primeras 50 esquinas", "10G: First 50 corners" }, { "20G: Cazador de esquinas (100)", "20G: Corner hunter (100)" },
        { "50G: Maestro de las esquinas (500)", "50G: Corner master (500)" }, { "100G: Leyenda del DVD (1000)", "100G: DVD legend (1000)" },
        { "20G: Una hora mirando el DVD", "20G: One hour watching the DVD" }, { "40G: ¿No tienes nada que hacer? (2 h)", "40G: Nothing better to do? (2 h)" },
        { "100G: Tener mucho tiempo libre (5 h)", "100G: Way too much free time (5 h)" }, { "10G: Logro de prueba", "10G: Test achievement" },
        { "Logro desbloqueado", "Achievement unlocked" }, { "Logro de prueba", "Test achievement" },
    };
    static final String[][] FRASES = {
        { "Dar permiso: mostrar encima de otras apps", "Give permission: show over other apps" },
        { "Velocidad (px/s)", "Speed (px/s)" }, { "Tamaño (%)", "Size (%)" }, { "Opacidad (%)", "Opacity (%)" }, { "Cantidad de DVDs", "Number of DVDs" },
        { "Cambiar de color al rebotar", "Change color on bounce" }, { "Tolerancia esquina (px)", "Corner tolerance (px)" },
        { "Mostrar contador al tocar esquina", "Show counter on corner hit" }, { "Mostrar \"¡ESQUINA!\" al tocar", "Show \"CORNER!\" on hit" },
        { "Sonido en las esquinas", "Sound on corners" }, { "Sonido de la skin (en las esquinas)", "Skin sound (on corners)" }, { "Vibrar al tocar esquina", "Vibrate on corner hit" },
        { "Vibrar el control al tocar esquina", "Vibrate controller on corner hit" }, { "Estela detrás de los DVDs", "Trail behind the DVDs" },
        { "Choque entre DVDs", "DVDs collide" }, { "Modo solo esquinas (va de esquina a esquina)", "Corners-only mode (goes corner to corner)" },
        { "📤 Compartir mi récord", "📤 Share my record" }, { "Compartir mi récord", "Share my record" },
        { "🖼 Poner el DVD de fondo de pantalla", "🖼 Set the DVD as wallpaper" }, { "➕ Poner el widget en la pantalla de inicio", "➕ Add the widget to the home screen" },
        { "🔓 Cheats desbloqueados", "🔓 Cheats unlocked" }, { "🔒 Cheats ocultos", "🔒 Cheats hidden" },
        { "○ Falta el permiso para mostrar encima de otras apps", "○ Missing permission to show over other apps" },
        { "● DvdRebote está corriendo — los cambios se aplican al instante", "● DvdRebote is running — changes apply instantly" },
        { "○ DvdRebote no está abierto", "○ DvdRebote is not open" },
        { "ROOT: límite de DVDs DESBLOQUEADO", "ROOT: DVD limit UNLOCKED" }, { "ROOT: límite de DVDs otra vez según tu RAM", "ROOT: DVD limit back to your RAM" },
        { "CHEAT: DVDs CERRADOS", "CHEAT: DVDs CLOSED" }, { " (abre el DVD para verlo)", " (open the DVD to see it)" },
        { "Primero dale permiso de mostrarse encima de otras apps", "First give it permission to show over other apps" },
        { "No se pudo compartir:", "Couldn't share:" }, { "No se pudo usar esa imagen", "Couldn't use that image" },
        { "se activa el modo ligero", "light mode turns on" }, { "con más de", "with more than" },
        { "sin límite de DVDs", "no DVD limit" }, { "Tu cel tiene", "Your phone has" }, { "GB de RAM", "GB of RAM" }, { "máximo", "max" },
        { "(con trucos como LANZAR, SOLO o TURBO no cuentan)", "(cheats like LANZAR, SOLO or TURBO don't count)" },
        { "Versión ", "Version " }, { "Archivo: ", "File: " },
        // consola de actualizaciones
        { "Buscando updates...", "Searching for updates..." }, { "Conectando con GitHub (DVD-REBOTES)...", "Connecting to GitHub (DVD-REBOTES)..." },
        { "ERROR: no pude revisar si hay updates.", "ERROR: couldn't check for updates." }, { "Revisa tu internet e inténtalo otra vez.", "Check your internet and try again." },
        { "Ya tienes la versión más nueva del DVD. No hay nada que instalar.", "You already have the newest DVD version. Nothing to install." },
        { "¡Encontré una versión nueva", "Found a new version" }, { "Esto es lo que cambia:", "This is what changes:" }, { "(subido el", "(uploaded" },
        { "¿Quieres instalar la actualización? (S/N)", "Do you want to install the update? (Y/N)" },
        { "Actualización cancelada. Cuando quieras, vuelve a darle a Buscar actualizaciones.", "Update cancelled. Whenever you want, press Check for updates again." },
        { "Android necesita que le des permiso a DVD Rebote para instalar apps.", "Android needs you to allow DVD Rebote to install apps." },
        { "Se abrió el ajuste: actívalo, regresa y vuelve a darle a Buscar actualizaciones.", "The setting opened: turn it on, come back and press Check for updates again." },
        { "Descargando DvdRebote.apk...", "Downloading DvdRebote.apk..." }, { "Descarga completa y revisada ✓", "Download complete and verified ✓" },
        { "Abriendo el instalador de Android... toca \"Actualizar\".", "Opening the Android installer... tap \"Update\"." },
        { "ERROR: no se pudo descargar la actualización.", "ERROR: couldn't download the update." },
        { "ERROR: no pude abrir el instalador", "ERROR: couldn't open the installer" }, { "Toca \"Cerrar\" para salir.", "Tap \"Close\" to exit." },
        { "[Versión", "[Version" },
    };

    public static String T(String es) {
        if (!ingles || es == null || es.isEmpty()) return es;
        for (String[] p : EXACTOS) if (es.equals(p[0])) return p[1];
        String r = es;
        for (String[] p : FRASES) if (r.contains(p[0])) r = r.replace(p[0], p[1]);
        return r;
    }

    // traduce todos los textos de una pantalla (botones, casillas, títulos...)
    public static void traducir(View v) {
        if (!ingles || v == null) return;
        if (v instanceof TextView) {
            TextView t = (TextView) v;
            CharSequence txt = t.getText();
            if (txt != null) { String n = T(txt.toString()); if (!n.contentEquals(txt)) t.setText(n); }
            if (v instanceof EditText && t.getHint() != null) t.setHint(T(t.getHint().toString()));
        }
        if (v instanceof ViewGroup) {
            ViewGroup g = (ViewGroup) v;
            for (int i = 0; i < g.getChildCount(); i++) traducir(g.getChildAt(i));
        }
    }
}
