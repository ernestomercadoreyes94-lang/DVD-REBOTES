package com.dvd.rebote;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;

// Entrega la imagen del récord (cache/record.png) a la app con la que la compartes
// (WhatsApp, TikTok, etc.). Solo esa app puede leerla, y solo porque tú la elegiste.
public class ProveedorImagen extends ContentProvider {
    static final String AUTORIDAD = "com.dvd.rebote.imagen";
    static final Uri URI = Uri.parse("content://" + AUTORIDAD + "/record.png");

    static File archivo(android.content.Context c) { return new File(c.getCacheDir(), "record.png"); }
    // la APK nueva que bajó "Buscar actualizaciones" (se la entrega al instalador de Android)
    static final Uri URI_APK = Uri.parse("content://" + AUTORIDAD + "/actualizacion.apk");
    static File archivoApk(android.content.Context c) { return new File(c.getCacheDir(), "actualizacion.apk"); }
    static boolean esApk(Uri uri) { return uri.getPath() != null && uri.getPath().endsWith(".apk"); }

    @Override public boolean onCreate() { return true; }

    @Override public String getType(Uri uri) { return esApk(uri) ? "application/vnd.android.package-archive" : "image/png"; }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        return ParcelFileDescriptor.open(esApk(uri) ? archivoApk(getContext()) : archivo(getContext()), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    // nombre y tamaño del archivo (algunas apps lo piden antes de abrirlo)
    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) {
        File f = esApk(uri) ? archivoApk(getContext()) : archivo(getContext());
        MatrixCursor c = new MatrixCursor(new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE});
        c.addRow(new Object[]{esApk(uri) ? "DvdRebote.apk" : "record_dvd.png", f.length()});
        return c;
    }

    @Override public Uri insert(Uri uri, ContentValues v) { return null; }
    @Override public int delete(Uri uri, String s, String[] a) { return 0; }
    @Override public int update(Uri uri, ContentValues v, String s, String[] a) { return 0; }
}
