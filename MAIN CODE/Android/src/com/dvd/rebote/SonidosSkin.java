package com.dvd.rebote;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioTrack;

import java.util.HashMap;

// SONIDOS DE LAS SKINS: cada metal suena distinto al tocar una esquina (se crean con el código):
//  bronce: "dong" grave de gong · plata: "ting" de campana · oro: "clink-clink" de monedas · diamante: campanitas de cristal.
// Igual que SonidoSkin en la PC (DvdConfig.cs).
public class SonidosSkin {
    static final int HZ = 44100;
    static final HashMap<String, short[]> hechos = new HashMap<>();
    static AudioTrack sonando;

    static boolean existe(String skin) {
        return "bronce".equals(skin) || "plata".equals(skin) || "oro".equals(skin) || "diamante".equals(skin) || Estadisticas.esSkinEspecial(skin);
    }

    // un golpe: varios tonos (frecuencia, volumen) que se apagan juntos
    static void golpe(double[] onda, double desde, double[] frec, double[] vol, double apagado, double ataque) {
        int ini = (int) (desde * HZ);
        for (int i = ini; i < onda.length; i++) {
            double t = (i - ini) / (double) HZ;
            double env = Math.exp(-t * apagado) * Math.min(1, t / ataque);
            double v = 0;
            for (int k = 0; k < frec.length; k++) v += vol[k] * Math.sin(2 * Math.PI * frec[k] * t);
            onda[i] += v * env;
        }
    }

    static short[] crear(String skin) {
        double dur = "bronce".equals(skin) ? 1.3 : "oro".equals(skin) ? 0.7 : "neon".equals(skin) ? 0.6 : "fuego".equals(skin) ? 0.9 : "holograma".equals(skin) ? 1.3 : 1.0;
        double[] o = new double[(int) (dur * HZ)];
        if ("neon".equals(skin)) {   // "zap" eléctrico que baja rápido, dos veces
            for (int z = 0; z < 2; z++) {
                int ini = (int) (z * 0.16 * HZ); double fase = 0;
                for (int i = ini; i < o.length; i++) {
                    double t = (i - ini) / (double) HZ;
                    double f = 300 + 1700 * Math.exp(-t * 14);
                    fase += 2 * Math.PI * f / HZ;
                    double env = Math.exp(-t * 9) * Math.min(1, t / 0.002) * (z == 0 ? 1 : 0.6);
                    o[i] += env * (Math.sin(fase) + 0.33 * Math.sin(3 * fase) + 0.2 * Math.sin(5 * fase));
                }
            }
        } else if ("fuego".equals(skin)) {   // llamarada: soplido de ruido con un golpe grave
            java.util.Random r = new java.util.Random(7); double suave = 0;
            for (int i = 0; i < o.length; i++) {
                double t = i / (double) HZ;
                suave += (r.nextDouble() * 2 - 1 - suave) * 0.18;
                double env = Math.min(1, t / 0.06) * Math.exp(-t * 3.2);
                o[i] = suave * env * 2.2 + 0.6 * Math.sin(2 * Math.PI * 85 * t) * Math.exp(-t * 7);
            }
        } else if ("arcoiris".equals(skin)) {   // arpegio alegre que sube
            double[] notas = { 1046.5, 1318.5, 1568.0, 2093.0 };
            for (int n = 0; n < 4; n++)
                golpe(o, n * 0.06, new double[] { notas[n], notas[n] * 2 }, new double[] { 1.0, 0.2 }, 5.0, 0.003);
        } else if ("holograma".equals(skin)) {   // acorde que vibra
            double[] notas = { 880.0, 1108.7, 1318.5, 1760.0 };
            for (int i = 0; i < o.length; i++) {
                double t = i / (double) HZ, v = 0;
                double vib = 1 + 0.012 * Math.sin(2 * Math.PI * 6 * t);
                for (int n = 0; n < 4; n++) v += Math.sin(2 * Math.PI * notas[n] * vib * t + n);
                double brillo = 0.75 + 0.25 * Math.sin(2 * Math.PI * 9 * t);
                o[i] = v * brillo * Math.min(1, t / 0.03) * Math.exp(-t * 2.4);
            }
        } else if ("bronce".equals(skin))
            golpe(o, 0, new double[] { 196.0, 392.0, 541.0, 1058.0, 1490.0 }, new double[] { 1.0, 0.55, 0.4, 0.18, 0.08 }, 2.8, 0.006);
        else if ("plata".equals(skin))
            golpe(o, 0, new double[] { 1318.5, 3637.0, 7120.0, 2637.0 }, new double[] { 1.0, 0.35, 0.12, 0.25 }, 4.5, 0.002);
        else if ("oro".equals(skin)) {
            golpe(o, 0, new double[] { 2637.0, 3520.0, 4699.0, 6272.0 }, new double[] { 1.0, 0.7, 0.45, 0.2 }, 13, 0.001);
            golpe(o, 0.085, new double[] { 2960.0, 3951.0, 5274.0, 6645.0 }, new double[] { 0.85, 0.6, 0.4, 0.18 }, 12, 0.001);
        } else {
            double[] notas = { 2093.0, 2637.0, 3136.0 };
            for (int n = 0; n < 3; n++)
                golpe(o, n * 0.07, new double[] { notas[n], notas[n] * 2.01, notas[n] * 3.98 }, new double[] { 1.0, 0.25, 0.08 }, 6.5, 0.002);
        }
        double max = 0;
        for (double v : o) max = Math.max(max, Math.abs(v));
        short[] pcm = new short[o.length];
        for (int i = 0; i < o.length; i++) {
            double v = max > 0 ? o[i] / max * 0.7 : 0;
            pcm[i] = (short) Math.max(-32767, Math.min(32767, v * 32767));
        }
        return pcm;
    }

    // suena sin esperar (el DVD sigue rebotando)
    static synchronized void tocar(String skin) {
        if (!existe(skin)) return;
        try {
            short[] pcm = hechos.get(skin);
            if (pcm == null) { pcm = crear(skin); hechos.put(skin, pcm); }
            if (sonando != null) { try { sonando.stop(); } catch (Exception ignored) { } sonando.release(); sonando = null; }
            AudioTrack at = new AudioTrack.Builder()
                .setAudioAttributes(new AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_GAME)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build())
                .setAudioFormat(new AudioFormat.Builder()
                    .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                    .setSampleRate(HZ)
                    .setChannelMask(AudioFormat.CHANNEL_OUT_MONO).build())
                .setTransferMode(AudioTrack.MODE_STATIC)
                .setBufferSizeInBytes(pcm.length * 2)
                .build();
            at.write(pcm, 0, pcm.length);
            at.play();
            sonando = at;
        } catch (Exception ignored) { }
    }
}
