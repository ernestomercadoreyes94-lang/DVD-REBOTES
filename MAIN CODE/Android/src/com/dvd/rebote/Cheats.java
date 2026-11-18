package com.dvd.rebote;

import java.util.Locale;

// Cheats: se escriben en la casilla secreta (tocar 5 veces el título de la app).
// Los efectos duran mientras los DVDs estén abiertos; no se guardan en el JSON.
public class Cheats {
    public static volatile boolean turbo, lento, gigante, enano, arcoiris, congelado, soloCheat;
    // pantallazo "No command" del código SECRET: primero se pausan los DVDs y luego se esconden
    public static volatile boolean pausaPantallazo, ocultarPantallazo;
    public static volatile boolean fiesta, fantasma, caos;
    public static volatile boolean hiper, parpadeo, estela;
    public static volatile int extra;
    public static volatile int esquinaToken, resetToken;   // pedidos para la vista de los DVDs
    public static volatile int reversaToken, teleToken;
    public static volatile float easter = 1f;   // código secreto EASTEREGG: multiplica la velocidad
    public static volatile boolean agitar;      // código secreto AGITAR: al sacudir el cel los DVDs salen disparados
    public static volatile boolean lanzar;      // código secreto LANZAR: agarras los DVDs con el dedo y los avientas
    public static volatile boolean inclinar;    // código secreto INCLINAR: los DVDs ruedan hacia donde inclinas el cel
    public static volatile boolean toca;        // código secreto TOCA: al tocar un DVD se duplica
    public static volatile boolean troll;       // código secreto TROLL: el DVD se vuelve la imagen troll
    public static volatile float errorVel = 1f; // código secreto ERROR: los 500 DVDs van 6 veces más rápido
    public static volatile String logroTexto = "";   // aviso de logro (lo dibuja DvdView abajo al centro)
    public static volatile long logroNanos = 0;
    public static volatile int logroTrofeo;          // 0 = logro normal, 1 bronce, 2 plata, 3 oro, 4 diamante (sale el trofeo)
    public static volatile int trofeoPendiente;      // trofeo que sale cuando termine el aviso del logro
    public static volatile long trofeoDesde;
    public static volatile String msg = "";
    public static volatile long msgNanos = 0;

    public static final String STOP = "STOP";
    public static final String ROOT = "ROOT";   // desbloquea el límite de DVDs por RAM (lo maneja la pantalla, se guarda)
    public static volatile int limiteToken;     // ROOT cambió el límite: la vista de los DVDs lo vuelve a leer

    // ---- estadísticas: con trucos que ayudan a tocar esquinas, NO cuentan ----
    public static volatile long trampaHasta;    // ESQUINA, REVERSA y TELE: los siguientes 10 s tampoco cuentan
    static void trampaCorta() { trampaHasta = System.nanoTime() + 10_000_000_000L; }

    public static boolean trucoActivo(Config cfg) {
        return lanzar || toca || inclinar || agitar || caos || turbo || hiper || fiesta || soloCheat
            || easter > 1f || errorVel > 1f || extra > 0
            || (cfg != null && cfg.modoSoloEsquinas)
            || System.nanoTime() < trampaHasta;
    }

    // Aplica un código. Devuelve el mensaje a mostrar, o null si el código no existe.
    // "STOP" no se aplica aquí: lo maneja la pantalla (cierra el servicio).
    public static String aplicar(String codigo) {
        String c = codigo.trim().toUpperCase(Locale.ROOT).replace(" ", "");
        String m;
        if (c.startsWith("EASTEREGG")) {
            m = easterEgg(c.substring(9));
            if (m == null) return null;
            msg = m;
            msgNanos = System.nanoTime();
            return msg;
        }
        switch (c) {
            case "ESQUINA": esquinaToken++; trampaCorta(); m = "TODOS A LAS ESQUINAS"; break;
            // solo ENSEÑA cómo se ve un logro (no cuenta nada)
            case "PROBARLOGRO": Estadisticas.logro("10G: Logro de prueba"); return "Logro de prueba";
            case "PROBARBRONCE": Estadisticas.trofeo(1, false); return "Trofeo de bronce de prueba";
            case "PROBARPLATA": Estadisticas.trofeo(2, false); return "Trofeo de plata de prueba";
            case "PROBARPLATINO": case "PROBARORO": Estadisticas.trofeo(3, false); return "Trofeo de oro de prueba";
            case "PROBARDIAMANTE": Estadisticas.trofeo(4, false); return "Trofeo de diamante de prueba";            case "TURBO": turbo = !turbo; if (turbo) { lento = false; hiper = false; } m = "TURBO " + onOff(turbo); break;
            case "LENTO": lento = !lento; if (lento) { turbo = false; hiper = false; } m = "LENTO " + onOff(lento); break;
            case "HIPER": case "HÍPER": hiper = !hiper; if (hiper) { turbo = false; lento = false; } m = "HÍPER VELOCIDAD x4 " + onOff(hiper); break;
            case "REVERSA": reversaToken++; trampaCorta(); m = "REVERSA: TODOS DAN LA VUELTA"; break;
            case "TELETRANSPORTE": case "TELE": teleToken++; trampaCorta(); m = "TELETRANSPORTE"; break;
            case "LANZAR":
                lanzar = !lanzar;
                m = lanzar ? "LANZAR ACTIVADO: arrastra y avienta los DVDs (toca 2 veces donde no haya DVD para salir)"
                           : "LANZAR DESACTIVADO";
                break;
            // lo manda la ventana de estática del código ERROR al tocar "Aceptar"
            case "ERRORACEPTADO": extra += 500; errorVel = 6f; return "";   // sin aviso: aparecen de golpe
            case "TROLL": troll = !troll; m = troll ? "TROLL ACTIVADO 😏" : "TROLL DESACTIVADO"; break;
            case "INCLINAR": inclinar = !inclinar; m = inclinar ? "INCLINAR ACTIVADO: ¡inclina el cel!" : "INCLINAR DESACTIVADO"; break;
            case "TOCA":
                toca = !toca;
                m = toca ? "TOCA ACTIVADO: toca un DVD y se duplican todos (toca 2 veces donde no haya DVD para salir)"
                         : "TOCA DESACTIVADO";
                break;
            case "AGITAR": agitar = !agitar; m = agitar ? "AGITAR ACTIVADO: ¡sacude el cel!" : "AGITAR DESACTIVADO"; break;
            case "PARPADEO": parpadeo = !parpadeo; m = "PARPADEO " + onOff(parpadeo); break;
            case "ESTELA": estela = !estela; m = "ESTELA " + onOff(estela); break;
            case "DOBLE": {
                int ahora = Config.actual.cantidad + extra;
                int mas = Math.min(ahora, 500);   // como mucho +500 de golpe para que no se trabe
                extra += mas;
                m = "DOBLE: +" + mas + " DVDs"; break;
            }
            case "GIGANTE": gigante = !gigante; if (gigante) enano = false; m = "GIGANTE " + onOff(gigante); break;
            case "ENANO": enano = !enano; if (enano) gigante = false; m = "ENANO " + onOff(enano); break;
            case "ARCOIRIS": case "ARCOÍRIS": arcoiris = !arcoiris; m = "ARCOÍRIS " + onOff(arcoiris); break;
            case "CONGELAR": congelado = !congelado; m = congelado ? "CONGELADO" : "DESCONGELADO"; break;
            case "MASDVD": extra += 10; m = "+10 DVDs"; break;
            case "FIESTA":
                fiesta = !fiesta;
                if (fiesta) { arcoiris = true; turbo = true; lento = false; extra += 10; }
                else { arcoiris = false; turbo = false; extra = Math.max(0, extra - 10); }
                m = "FIESTA " + onOff(fiesta); break;
            case "FANTASMA": fantasma = !fantasma; m = "FANTASMA " + onOff(fantasma); break;
            case "CAOS": caos = !caos; m = "CAOS " + onOff(caos); break;
            case "SOLO": soloCheat = !soloCheat; m = "SOLO ESQUINAS " + onOff(Config.actual.modoSoloEsquinas ^ soloCheat); break;
            case "RESET":
                turbo = lento = gigante = enano = arcoiris = congelado = soloCheat = false;
                fiesta = fantasma = caos = false;
                hiper = parpadeo = estela = false;
                easter = 1f; agitar = false; lanzar = false; inclinar = false; toca = false; troll = false; errorVel = 1f;
                extra = 0; resetToken++;
                m = "CONTADOR Y CHEATS REINICIADOS"; break;
            default: return null;
        }
        msg = "CHEAT: " + m;
        msgNanos = System.nanoTime();
        return msg;
    }

    static String onOff(boolean b) { return b ? "ACTIVADO" : "DESACTIVADO"; }

    // Código secreto: "easteregg" = velocidad x10; "easteregg x4" = x10 por 4 (x40), etc.
    // "easteregg" solo, estando activo, lo quita.
    static final float EASTER_BASE = 10f;
    static String easterEgg(String resto) {
        resto = resto.replace("X", "").replace("*", "");
        if (resto.isEmpty()) {
            if (easter > 1f) { easter = 1f; return "EASTER EGG DESACTIVADO"; }
            easter = EASTER_BASE;
            return "EASTER EGG: VELOCIDAD x" + (int) EASTER_BASE;
        }
        int n;
        try { n = Integer.parseInt(resto); } catch (NumberFormatException e) { return null; }
        if (n < 1) return null;
        n = Math.min(n, 100);
        easter = EASTER_BASE * n;
        return "EASTER EGG: VELOCIDAD x" + (int) easter;
    }
}
