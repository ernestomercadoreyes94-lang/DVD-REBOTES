package com.dvd.rebote;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.provider.Settings;
import android.widget.RemoteViews;

// Widget de la pantalla de inicio: récord, esquinas en total, los 4 trofeos (en gris los que no tienes)
// y un botón para abrir / cerrar el DVD. Al tocar el resto se abre la app.
public class WidgetDvd extends AppWidgetProvider {
    static final String ALTERNAR = "com.dvd.rebote.WIDGET_ALTERNAR";

    @Override
    public void onUpdate(Context c, AppWidgetManager m, int[] ids) {
        for (int id : ids) m.updateAppWidget(id, vista(c));
    }

    @Override
    public void onReceive(Context c, Intent i) {
        super.onReceive(c, i);
        if (!ALTERNAR.equals(i.getAction())) return;
        if (DvdService.corriendo) {
            c.stopService(new Intent(c, DvdService.class));
        } else if (Settings.canDrawOverlays(c)) {
            try { c.startForegroundService(new Intent(c, DvdService.class)); } catch (Exception e) { abrirApp(c); }
        } else {
            abrirApp(c);   // falta el permiso de mostrarse encima: se abre la app para darlo
        }
        // el botón cambia de "Abrir" a "Cerrar" (o al revés) en un momento
        new android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(() -> actualizar(c), 600);
    }

    static void abrirApp(Context c) {
        Intent a = new Intent(c, MainActivity.class);
        a.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        c.startActivity(a);
    }

    static RemoteViews vista(Context c) {
        IdiomaCel.cargar(c);
        RemoteViews v = new RemoteViews(c.getPackageName(), R.layout.widget_dvd);
        long esq = 0, ms = 0; int rec = 0;
        try { esq = Estadisticas.totalEsquinas(c); ms = Estadisticas.totalMs(c); rec = Estadisticas.record(c); } catch (Exception ignored) { }
        v.setTextViewText(R.id.widget_record, IdiomaCel.ingles ? "🏆 Record: " + rec + " corners" : "🏆 Récord: " + rec + " esquinas");
        v.setTextViewText(R.id.widget_total, IdiomaCel.ingles ? "Total: " + esq + " corners" : "En total: " + esq + " esquinas");
        String esp = "";
        try { esp = Estadisticas.especiales(c); } catch (Exception ignored) { }
        int ganados = Estadisticas.ganados(esq, ms, esp);
        int[] ids = { R.id.widget_bronce, R.id.widget_plata, R.id.widget_oro, R.id.widget_diamante };
        for (int i = 0; i < 4; i++)
            v.setInt(ids[i], "setImageAlpha", ganados >= Estadisticas.LOGROS_PARA_TROFEO[i] ? 255 : 60);   // apagado = medio transparente
        v.setTextViewText(R.id.widget_boton, IdiomaCel.T(DvdService.corriendo ? "■ Cerrar DVD" : "▶ Abrir DVD"));
        // botón: abrir / cerrar el DVD
        Intent alt = new Intent(c, WidgetDvd.class).setAction(ALTERNAR);
        v.setOnClickPendingIntent(R.id.widget_boton, PendingIntent.getBroadcast(c, 1, alt, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
        // el resto: abrir la app
        Intent app = new Intent(c, MainActivity.class);
        v.setOnClickPendingIntent(R.id.widget_raiz, PendingIntent.getActivity(c, 2, app, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
        return v;
    }

    // se llama al guardar las estadísticas y al abrir / cerrar el DVD
    static void actualizar(Context c) {
        try {
            AppWidgetManager m = AppWidgetManager.getInstance(c);
            int[] ids = m.getAppWidgetIds(new ComponentName(c, WidgetDvd.class));
            if (ids.length == 0) return;
            RemoteViews v = vista(c);
            for (int id : ids) m.updateAppWidget(id, v);
        } catch (Exception ignored) { }
    }
}
