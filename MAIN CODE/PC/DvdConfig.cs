// Configuración compartida entre DvdRebote.exe y dvd_config.exe
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

public class DvdConfig
{
    public double Velocidad = 260;          // píxeles por segundo
    public int Tamano = 100;                // porcentaje
    public int Cantidad = 1;                // cuántos logos rebotan a la vez (sin límite práctico)
    public const int MaxCantidad = 1000000;
    public int Opacidad = 100;              // porcentaje
    public string Estilo = "logo";          // "logo", "disco", "vhs" o "reloj" (la hora rebotando)
    public string Color = "#00BFFF";
    public bool CambiarColor = true;
    public bool MostrarContador = true;
    public bool MostrarMensajeEsquina = true;
    public bool Sonido = true;
    public bool Pausado = false;
    public bool ModoSoloEsquinas = false;   // el logo viaja solo de esquina a esquina
    public bool Estela = false;             // cada DVD deja una estela que se desvanece
    public bool VibrarControl = true;       // el control (Xbox/XInput) vibra al tocar una esquina
    public bool Choque = false;             // los DVDs rebotan entre ellos
    public bool RebotarVentanas = false;    // los DVDs rebotan en los bordes de las ventanas abiertas
    public string LogoTexto = "DVD";        // texto del logo (en vez de "DVD")
    public string LogoImagen = "";          // ruta de una imagen para usar como logo ("" = sin imagen)
    public bool FondoAnimado = false;       // los DVDs rebotan detrás de los iconos, como fondo de pantalla (solo PC)
    public bool FondoNegro = false;         // en el fondo animado: fondo negro en vez de tu fondo de pantalla
    public bool Reloj = false;              // la hora en una pastilla debajo de cada DVD
    public string Skin = "";                // skin de la TIENDA para el logo: "", "bronce", "plata", "oro" o "diamante" (solo si la compraste)
    public string Idioma = "es";            // idioma de la configuración y de los avisos: "es" o "en"
    public string Pantalla = "principal";   // dónde rebota el DVD: "principal", "todas" o el nombre de una pantalla (\\.\DISPLAY2)
    public string PantallaCompleta = "esconder";   // con un juego o video en pantalla completa: "nada", "esconder", "esconder_pausar" o "pausar"
    public string SonidoArchivo = "";       // .wav o .mp3 que suena al tocar una esquina ("" = el sonido de Windows)
    public int ToleranciaEsquinaPx = 10;    // a cuántos píxeles de la esquina cuenta como esquina
    public int ReiniciarContador = 0;       // al cambiar este número se reinicia el contador

    // Carpeta donde está el .exe que se está ejecutando
    public static string Carpeta { get { return AppDomain.CurrentDomain.BaseDirectory; } }

    // El JSON vive SIEMPRE junto a DvdRebote.exe, así dvd_config.exe usa el mismo aunque
    // esté en otra carpeta (lo encuentra por el registro).
    public static bool EsRebote = false;   // DvdRebote.exe lo pone en true al arrancar
    public static bool EsProtector = false; // el protector (.scr en TEMP) usa el JSON de DvdRebote.exe
    static string carpetaDatos;
    public static string CarpetaDatos
    {
        get
        {
            if (carpetaDatos == null)
            {
                carpetaDatos = Carpeta;
                if ((!EsRebote || EsProtector) && !File.Exists(Path.Combine(Carpeta, "DvdRebote.exe")))
                {
                    string r = RutaRebote();
                    if (r != null) carpetaDatos = Path.GetDirectoryName(r) + "\\";
                }
            }
            return carpetaDatos;
        }
    }
    public static string RutaConfig { get { return Path.Combine(CarpetaDatos, "dvd_config.json"); } }
    public static string RutaStats { get { return Path.Combine(CarpetaDatos, "dvd_stats.json"); } }
    public static string RutaBat { get { return Path.Combine(Carpeta, "modo_solo_esquinas.bat"); } }

    // ---- Funcionar en cualquier carpeta ----
    // En HKCU\Software\DvdRebote se guarda:
    //   Rebote  = ruta de DvdRebote.exe   (lo guarda DvdRebote.exe)
    //   Config  = ruta de dvd_config.exe  (lo guarda dvd_config.exe)
    //   Carpeta = carpeta de dvd_config.exe (para .bat viejos)
    //   Bat     = ruta de modo_solo_esquinas.bat (lo guarda el .bat)
    const string ClaveRegistro = @"Software\DvdRebote";

    public static void RegistrarCarpeta()
    {
        try
        {
            string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ClaveRegistro))
            {
                if (EsRebote) k.SetValue("Rebote", exe);
                else { k.SetValue("Config", exe); k.SetValue("Carpeta", Carpeta); }
            }
        }
        catch { }
    }

    static string LeerRegistro(string nombre)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ClaveRegistro))
                return k == null ? null : k.GetValue(nombre) as string;
        }
        catch { return null; }
    }

    // Dónde está DvdRebote.exe: junto a este programa o donde se abrió por última vez
    public static string RutaRebote()
    {
        string local = Path.Combine(Carpeta, "DvdRebote.exe");
        if (File.Exists(local)) return local;
        string reg = LeerRegistro("Rebote");
        return (!string.IsNullOrEmpty(reg) && File.Exists(reg)) ? reg : null;
    }

    // ---- Revisar .NET Framework (sin archivo .config) ----
    // Los programas necesitan .NET Framework 4.5 o más nuevo. Si la PC tiene uno más viejo,
    // se avisa en español y se ofrece abrir la página de descarga.
    // (Si no hay NINGÚN .NET 4, el programa no puede ni arrancar: ahí Windows muestra su propio aviso.)
    public static bool RevisarNetFramework()
    {
        // Para probar el aviso aunque tengas .NET: abrir el programa con --probar-net
        bool probar = Array.IndexOf(Environment.GetCommandLineArgs(), "--probar-net") >= 0;
        int release = 0;
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                object v = k == null ? null : k.GetValue("Release");
                if (v is int) release = (int)v;
            }
        }
        catch { }
        if (release >= 378389 && !probar) return true; // 378389 = .NET Framework 4.5

        System.Windows.Forms.DialogResult r = System.Windows.Forms.MessageBox.Show(
            "Necesitas .NET Framework para usar esta app.\n\n" +
            "Instala .NET Framework 4.8 (es gratis, de Microsoft).\n\n¿Quieres abrir la página de descarga?",
            "DVD Rebote", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
        if (r == System.Windows.Forms.DialogResult.Yes)
        {
            try { System.Diagnostics.Process.Start("https://dotnet.microsoft.com/download/dotnet-framework/net48"); } catch { }
        }
        return false;
    }

    // Versión de .NET Framework 4.x instalada (número "Release" del registro; 0 si no hay)
    public static int ReleaseNet()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                object v = k == null ? null : k.GetValue("Release");
                if (v is int) return (int)v;
            }
        }
        catch { }
        return 0;
    }

    // El código secreto pide la versión más reciente de .NET Framework (4.8 o más; la última es 4.8.1).
    // Si no la tienes, te ofrece descargarla y el código no se abre hasta que la instales.
    public static bool RevisarNetParaSecret()
    {
        bool probar = Array.IndexOf(Environment.GetCommandLineArgs(), "--probar-net") >= 0;
        if (ReleaseNet() >= 528040 && !probar) return true;   // 528040 = .NET Framework 4.8
        System.Windows.Forms.DialogResult r = System.Windows.Forms.MessageBox.Show(
            "Para esto necesitas la versión más reciente de .NET Framework (4.8.1).\n\n" +
            "Es gratis, de Microsoft. ¿Quieres abrir la página de descarga?",
            "DVD Rebote", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Information);
        if (r == System.Windows.Forms.DialogResult.Yes)
        {
            try { System.Diagnostics.Process.Start("https://dotnet.microsoft.com/download/dotnet-framework/net481"); } catch { }
        }
        return false;
    }

    // Dónde está dvd_config.exe: junto a este programa o donde se abrió por última vez
    public static string RutaConfigApp()
    {
        string local = Path.Combine(Carpeta, "dvd_config.exe");
        if (File.Exists(local)) return local;
        string reg = LeerRegistro("Config");
        return (!string.IsNullOrEmpty(reg) && File.Exists(reg)) ? reg : null;
    }

    // El modo solo esquinas (desde el JSON) necesita el .bat instalado: junto a los .exe,
    // en una subcarpeta o carpeta hermana (por ejemplo DVD\cheats) o donde se haya abierto alguna vez.
    public static bool BatInstalado()
    {
        try
        {
            if (File.Exists(RutaBat)) return true;
            foreach (string sub in Directory.GetDirectories(Carpeta))
                if (File.Exists(Path.Combine(sub, "modo_solo_esquinas.bat"))) return true;
            DirectoryInfo padre = Directory.GetParent(Carpeta.TrimEnd('\\'));
            if (padre != null)
                foreach (string sub in Directory.GetDirectories(padre.FullName))
                    if (File.Exists(Path.Combine(sub, "modo_solo_esquinas.bat"))) return true;
            string reg = LeerRegistro("Bat");
            return !string.IsNullOrEmpty(reg) && File.Exists(reg);
        }
        catch { return false; }
    }

    public static DvdConfig Load()
    {
        DvdConfig c = new DvdConfig();
        if (!File.Exists(RutaConfig)) { c.Save(); return c; }
        Dictionary<string, object> d = ReadJson(RutaConfig);
        if (d == null) return null; // archivo a medio escribir: reintentar luego
        c.Velocidad = Clamp(GetD(d, "Velocidad", c.Velocidad), 20, 3000);
        c.Tamano = (int)Clamp(GetD(d, "Tamano", c.Tamano), 20, 400);
        c.Cantidad = (int)Clamp(GetD(d, "Cantidad", c.Cantidad), 1, MaxCantidad);
        c.Opacidad = (int)Clamp(GetD(d, "Opacidad", c.Opacidad), 5, 100);
        string est = GetS(d, "Estilo", c.Estilo);
        c.Estilo = est == "disco" ? "disco" : est == "vhs" ? "vhs" : est == "reloj" ? "reloj" : "logo";
        c.Color = GetS(d, "Color", c.Color);
        c.CambiarColor = GetB(d, "CambiarColor", c.CambiarColor);
        c.MostrarContador = GetB(d, "MostrarContador", c.MostrarContador);
        c.MostrarMensajeEsquina = GetB(d, "MostrarMensajeEsquina", c.MostrarMensajeEsquina);
        c.Sonido = GetB(d, "Sonido", c.Sonido);
        c.Pausado = GetB(d, "Pausado", c.Pausado);
        c.ModoSoloEsquinas = GetB(d, "ModoSoloEsquinas", c.ModoSoloEsquinas);
        c.Estela = GetB(d, "Estela", c.Estela);
        c.VibrarControl = GetB(d, "VibrarControl", c.VibrarControl);
        c.Choque = GetB(d, "Choque", c.Choque);
        c.RebotarVentanas = GetB(d, "RebotarVentanas", c.RebotarVentanas);
        c.FondoAnimado = GetB(d, "FondoAnimado", c.FondoAnimado);
        c.FondoNegro = GetB(d, "FondoNegro", c.FondoNegro);
        c.Skin = GetS(d, "Skin", c.Skin);
        c.Reloj = GetB(d, "Reloj", c.Reloj);
        c.Idioma = GetS(d, "Idioma", c.Idioma) == "en" ? "en" : "es";
        c.Pantalla = GetS(d, "Pantalla", c.Pantalla).Trim();
        if (c.Pantalla.Length == 0) c.Pantalla = "principal";
        string pc = GetS(d, "PantallaCompleta", c.PantallaCompleta);
        c.PantallaCompleta = pc == "nada" || pc == "esconder_pausar" || pc == "pausar" ? pc : "esconder";
        c.LogoTexto = GetS(d, "LogoTexto", c.LogoTexto).Trim();
        if (c.LogoTexto.Length == 0) c.LogoTexto = "DVD";
        if (c.LogoTexto.Length > 20) c.LogoTexto = c.LogoTexto.Substring(0, 20);
        c.LogoImagen = GetS(d, "LogoImagen", c.LogoImagen).Trim();
        c.SonidoArchivo = GetS(d, "SonidoArchivo", c.SonidoArchivo).Trim();
        c.ToleranciaEsquinaPx = (int)Clamp(GetD(d, "ToleranciaEsquinaPx", c.ToleranciaEsquinaPx), 0, 200);
        c.ReiniciarContador = (int)GetD(d, "ReiniciarContador", c.ReiniciarContador);
        return c;
    }

    public System.Drawing.Color GetColor()
    {
        try { return ColorTranslator.FromHtml(Color); } catch { return System.Drawing.Color.DeepSkyBlue; }
    }

    public static string ToHex(System.Drawing.Color c)
    {
        return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
    }

    public string ToJson()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"Velocidad\": ").Append(Velocidad.ToString("0.##", inv)).Append(",\n");
        sb.Append("  \"Tamano\": ").Append(Tamano).Append(",\n");
        sb.Append("  \"Cantidad\": ").Append(Cantidad).Append(",\n");
        sb.Append("  \"Opacidad\": ").Append(Opacidad).Append(",\n");
        sb.Append("  \"Estilo\": \"").Append(Estilo).Append("\",\n");
        sb.Append("  \"Color\": \"").Append(Color).Append("\",\n");
        sb.Append("  \"CambiarColor\": ").Append(B(CambiarColor)).Append(",\n");
        sb.Append("  \"MostrarContador\": ").Append(B(MostrarContador)).Append(",\n");
        sb.Append("  \"MostrarMensajeEsquina\": ").Append(B(MostrarMensajeEsquina)).Append(",\n");
        sb.Append("  \"Sonido\": ").Append(B(Sonido)).Append(",\n");
        sb.Append("  \"Pausado\": ").Append(B(Pausado)).Append(",\n");
        sb.Append("  \"ModoSoloEsquinas\": ").Append(B(ModoSoloEsquinas)).Append(",\n");
        sb.Append("  \"Estela\": ").Append(B(Estela)).Append(",\n");
        sb.Append("  \"VibrarControl\": ").Append(B(VibrarControl)).Append(",\n");
        sb.Append("  \"Choque\": ").Append(B(Choque)).Append(",\n");
        sb.Append("  \"RebotarVentanas\": ").Append(B(RebotarVentanas)).Append(",\n");
        sb.Append("  \"FondoAnimado\": ").Append(B(FondoAnimado)).Append(",\n");
        sb.Append("  \"FondoNegro\": ").Append(B(FondoNegro)).Append(",\n");
        sb.Append("  \"Reloj\": ").Append(B(Reloj)).Append(",\n");
        sb.Append("  \"Skin\": ").Append(new JavaScriptSerializer().Serialize(Skin ?? "")).Append(",\n");
        sb.Append("  \"Idioma\": \"").Append(Idioma == "en" ? "en" : "es").Append("\",\n");
        sb.Append("  \"Pantalla\": ").Append(new JavaScriptSerializer().Serialize(Pantalla ?? "principal")).Append(",\n");
        sb.Append("  \"PantallaCompleta\": \"").Append(PantallaCompleta).Append("\",\n");
        JavaScriptSerializer js = new JavaScriptSerializer();   // escapa comillas y barras de los textos
        sb.Append("  \"LogoTexto\": ").Append(js.Serialize(LogoTexto ?? "DVD")).Append(",\n");
        sb.Append("  \"LogoImagen\": ").Append(js.Serialize(LogoImagen ?? "")).Append(",\n");
        sb.Append("  \"SonidoArchivo\": ").Append(js.Serialize(SonidoArchivo ?? "")).Append(",\n");
        sb.Append("  \"ToleranciaEsquinaPx\": ").Append(ToleranciaEsquinaPx).Append(",\n");
        sb.Append("  \"ReiniciarContador\": ").Append(ReiniciarContador).Append("\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    public void Save() { WriteAtomic(RutaConfig, ToJson()); }

    // nombres y puntos de los logros (mismo orden: 50/100/500/1000/2500/5000 esquinas y 1/2/5/10/24 horas)
    public static readonly string[] NombresLogrosEsquinas = {
        "10G: Primeras 50 esquinas", "20G: Cazador de esquinas (100)",
        "50G: Maestro de las esquinas (500)", "100G: Leyenda del DVD (1000)",
        "200G: Esquinero profesional (2500)", "300G: El DVD ya te conoce (5000)" };
    public static readonly string[] NombresLogrosHoras = {
        "20G: Una hora mirando el DVD", "40G: ¿No tienes nada que hacer? (2 h)",
        "100G: Tener mucho tiempo libre (5 h)", "150G: Esto ya es un trabajo (10 h)",
        "500G: Un día entero rebotando (24 h)" };
    // ---- TROFEOS (bronce / plata / oro / diamante) según cuántos logros tienes ----
    public static readonly long[] MetasEsquinas = { 50, 100, 500, 1000, 2500, 5000 };
    public static readonly int[] MetasHoras = { 1, 2, 5, 10, 24 };
    public static readonly string[] NombresTrofeos = { "Pequeños pasos", "¿Cuánto tiempo tienes?", "¿En serio llegamos aquí?", "Bro consigue un trabajo" };
    public static readonly string[] MetalesTrofeos = { "bronce", "plata", "oro", "diamante" };
    public static readonly string[] CondicionTrofeos = { "Consigue un logro", "Consigue 4 logros", "Consigue 7 logros", "Consigue todos los logros (11)" };
    public static readonly int[] LogrosParaTrofeo = { 1, 4, 7, 11 };

    // ---- TIENDA: skins para el logo, se compran con los G de tus logros ----
    public static readonly string[] SkinsId = { "bronce", "plata", "oro", "diamante" };
    public static readonly string[] SkinsNombre = { "Skin de Bronce", "Skin de Plata", "Skin de Oro", "Skin de Diamante" };
    public static readonly int[] SkinsPrecio = { 100, 200, 350, 800 };   // con todos los logros (1490 G) alcanza para todas

    // los G (puntos) de los logros que ya tienes
    public static int PuntosG(long esquinas, double segundos)
    {
        int g = 0;
        for (int i = 0; i < MetasEsquinas.Length; i++) if (esquinas >= MetasEsquinas[i]) g += PuntosDe(NombresLogrosEsquinas[i]);
        for (int i = 0; i < MetasHoras.Length; i++) if (segundos >= MetasHoras[i] * 3600) g += PuntosDe(NombresLogrosHoras[i]);
        return g;
    }
    // color principal de cada skin (para el brillo de las esquinas y la tienda)
    public static System.Drawing.Color ColorSkin(string skin)
    {
        if (skin == "bronce") return System.Drawing.Color.FromArgb(225, 140, 70);
        if (skin == "plata") return System.Drawing.Color.FromArgb(205, 215, 230);
        if (skin == "oro") return System.Drawing.Color.FromArgb(255, 205, 60);
        if (skin == "diamante") return System.Drawing.Color.FromArgb(130, 235, 255);
        return System.Drawing.Color.White;
    }

    // pinta el logo (lo que no es transparente) de METAL: degradado con reflejos y una franja de brillo;
    // el diamante además lleva destellos. Se hace una vez y queda guardado (no gasta nada al rebotar).
    public static void Metalizar(System.Drawing.Bitmap b, string skin)
    {
        int[] os, cl;
        if (skin == "bronce") { os = new[] { 110, 52, 20 }; cl = new[] { 250, 185, 125 }; }
        else if (skin == "plata") { os = new[] { 92, 98, 112 }; cl = new[] { 250, 252, 255 }; }
        else if (skin == "oro") { os = new[] { 150, 98, 8 }; cl = new[] { 255, 240, 150 }; }
        else if (skin == "diamante") { os = new[] { 30, 120, 190 }; cl = new[] { 235, 252, 255 }; }
        else return;
        System.Drawing.Imaging.PixelFormat pf = b.PixelFormat;
        bool pre = pf == System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
        if (!pre && pf != System.Drawing.Imaging.PixelFormat.Format32bppArgb) return;
        System.Drawing.Rectangle r = new System.Drawing.Rectangle(0, 0, b.Width, b.Height);
        System.Drawing.Imaging.BitmapData bd = b.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadWrite, pf);
        try
        {
            int n = bd.Stride * b.Height;
            byte[] px = new byte[n];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, n);
            int w = b.Width, h = b.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * bd.Stride + x * 4;
                    int a = px[i + 3];
                    if (a == 0) continue;
                    double fy = (double)y / Math.Max(1, h - 1), fx = (double)x / Math.Max(1, w - 1);
                    // reflejos horizontales como de metal pulido
                    double v = 0.5 + 0.32 * Math.Sin(fy * Math.PI * 2.3 + 0.5) + 0.18 * Math.Sin(fy * Math.PI * 7 + fx * 2);
                    v = Math.Max(0, Math.Min(1, v));
                    double R = os[0] + (cl[0] - os[0]) * v, G = os[1] + (cl[1] - os[1]) * v, B = os[2] + (cl[2] - os[2]) * v;
                    // franja de brillo en diagonal
                    double d = Math.Abs(((fx + fy * 0.55) % 1.0) - 0.32);
                    if (d < 0.07) { double k = (1 - d / 0.07) * 0.75; R += (255 - R) * k; G += (255 - G) * k; B += (255 - B) * k; }
                    double m = pre ? a / 255.0 : 1.0;
                    px[i] = (byte)Math.Min(255, B * m); px[i + 1] = (byte)Math.Min(255, G * m); px[i + 2] = (byte)Math.Min(255, R * m);
                }
            System.Runtime.InteropServices.Marshal.Copy(px, 0, bd.Scan0, n);
        }
        finally { b.UnlockBits(bd); }
        if (skin != "diamante") return;
        // DIAMANTE: destellos blancos de 4 puntas encima del logo
        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(b))
        using (System.Drawing.SolidBrush bl = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 255, 255, 255)))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            double[,] pos = { { 0.18, 0.28 }, { 0.47, 0.2 }, { 0.8, 0.35 }, { 0.3, 0.72 }, { 0.66, 0.78 } };
            for (int k = 0; k < pos.GetLength(0); k++)
            {
                float cx = (float)(pos[k, 0] * b.Width), cy = (float)(pos[k, 1] * b.Height);
                float s = Math.Max(3f, b.Height * (k % 2 == 0 ? 0.09f : 0.06f));
                System.Drawing.PointF[] p = new System.Drawing.PointF[8];
                for (int j = 0; j < 8; j++) { double an = j * Math.PI / 4; float rr = j % 2 == 0 ? s : s * 0.25f; p[j] = new System.Drawing.PointF(cx + (float)(Math.Cos(an) * rr), cy + (float)(Math.Sin(an) * rr)); }
                g.FillPolygon(bl, p);
            }
        }
    }

    static int PuntosDe(string puntosYNombre) { int p; int.TryParse(puntosYNombre.Substring(0, puntosYNombre.IndexOf('G')), out p); return p; }

    public static int LogrosGanados(long esquinas, double segundos)
    {
        int n = 0;
        foreach (long m in MetasEsquinas) if (esquinas >= m) n++;
        foreach (int h in MetasHoras) if (segundos >= h * 3600) n++;
        return n;
    }

    // el rectángulo donde rebota el DVD según "Pantalla": la principal, todas juntas, o una en especial
    // (si esa pantalla ya no está conectada, se usa la principal)
    public static Rectangle AreaPantalla(string pantalla)
    {
        if (pantalla == "todas") return System.Windows.Forms.SystemInformation.VirtualScreen;
        if (!string.IsNullOrEmpty(pantalla) && pantalla != "principal")
            foreach (System.Windows.Forms.Screen p in System.Windows.Forms.Screen.AllScreens)
                if (p.DeviceName == pantalla) return p.Bounds;
        return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    }

    // "--campos <archivo>": escribe los ajustes de ESTA versión (con sus valores de fábrica) y se cierra
    // sin abrir nada. Lo usa "Buscar actualizaciones" con el .exe recién bajado para saber si la versión
    // nueva cambia los ajustes del dvd_config.json.
    public static bool AtenderCampos(string[] args)
    {
        int i = Array.IndexOf(args, "--campos");
        if (i < 0) return false;
        try { if (i + 1 < args.Length) File.WriteAllText(args[i + 1], new DvdConfig().ToJson()); } catch { }
        return true;
    }

    // ---------- utilidades ----------
    public static void WriteAtomic(string path, string text)
    {
        // OneDrive puede bloquear el archivo un momento mientras sincroniza: se reintenta
        string tmp = path + ".tmp";
        for (int i = 0; ; i++)
        {
            try
            {
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return;
            }
            catch (IOException)
            {
                if (i >= 15) { try { File.Delete(tmp); } catch { } throw; }
                System.Threading.Thread.Sleep(40);
            }
            catch (UnauthorizedAccessException)
            {
                if (i >= 15) { try { File.Delete(tmp); } catch { } throw; }
                System.Threading.Thread.Sleep(40);
            }
        }
    }

    public static Dictionary<string, object> ReadJson(string path)
    {
        try
        {
            string txt = File.ReadAllText(path);
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(txt);
        }
        catch { return null; }
    }

    static string B(bool b) { return b ? "true" : "false"; }
    static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }
    public static double GetD(Dictionary<string, object> d, string k, double def)
    {
        object o; if (!d.TryGetValue(k, out o) || o == null) return def;
        try { return Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch { return def; }
    }
    public static string GetS(Dictionary<string, object> d, string k, string def)
    {
        object o; if (!d.TryGetValue(k, out o) || o == null) return def; return o.ToString();
    }
    public static bool GetB(Dictionary<string, object> d, string k, bool def)
    {
        object o; if (!d.TryGetValue(k, out o) || !(o is bool)) return def; return (bool)o;
    }
}

public class DvdStats
{
    public int Total, SupIzq, SupDer, InfIzq, InfDer;
    public string Ultima = "-";

    public static DvdStats Load()
    {
        DvdStats s = new DvdStats();
        Dictionary<string, object> d = DvdConfig.ReadJson(DvdConfig.RutaStats);
        if (d == null) return s;
        s.Total = (int)DvdConfig.GetD(d, "Total", 0);
        s.SupIzq = (int)DvdConfig.GetD(d, "SuperiorIzquierda", 0);
        s.SupDer = (int)DvdConfig.GetD(d, "SuperiorDerecha", 0);
        s.InfIzq = (int)DvdConfig.GetD(d, "InferiorIzquierda", 0);
        s.InfDer = (int)DvdConfig.GetD(d, "InferiorDerecha", 0);
        s.Ultima = DvdConfig.GetS(d, "Ultima", "-");
        return s;
    }

    public void Save()
    {
        string json = "{\n" +
            "  \"Total\": " + Total + ",\n" +
            "  \"SuperiorIzquierda\": " + SupIzq + ",\n" +
            "  \"SuperiorDerecha\": " + SupDer + ",\n" +
            "  \"InferiorIzquierda\": " + InfIzq + ",\n" +
            "  \"InferiorDerecha\": " + InfDer + ",\n" +
            "  \"Ultima\": \"" + Ultima + "\"\n}\n";
        try { DvdConfig.WriteAtomic(DvdConfig.RutaStats, json); } catch { }
    }
}

// Estadísticas guardadas (dvd_estadisticas.json, junto a la configuración): récord, esquinas en total,
// cuál toca más y tiempo rebotando. Con trucos (.bat como TURBO, SOLO, ESQUINA...) NO cuentan.
// El DVD junta lo nuevo en memoria y lo SUMA al archivo cada 10 segundos (así el protector de pantalla
// y el DVD no se borran lo que guardó el otro).
public class EstadisticasDvd
{
    // CIFRADO: el archivo va cifrado (AES-256) y firmado (HMAC-SHA256) para que no se pueda leer ni cambiar
    // con el Bloc de notas. Si alguien lo modifica, la firma ya no coincide y las estadísticas se reinician.
    public static string Ruta { get { return Path.Combine(DvdConfig.CarpetaDatos, "dvd_estadisticas.dat"); } }
    static string RutaVieja { get { return Path.Combine(DvdConfig.CarpetaDatos, "dvd_estadisticas.json"); } }   // la de antes, sin cifrar
    public long Total, SupIzq, SupDer, InfIzq, InfDer;
    public double Segundos;
    public int Record;
    public string RecordFecha = "";
    public int GastadoG;          // G gastados en la TIENDA
    public string Skins = "";     // skins compradas, separadas por coma (bronce,oro,diamante)
    public bool Modificado;   // el archivo estaba cambiado o roto: se empezó de cero

    public bool TieneSkin(string s) { return !string.IsNullOrEmpty(s) && ("," + (Skins ?? "") + ",").Contains("," + s + ","); }
    // G que tienes para gastar: los puntos de tus logros menos lo que ya gastaste
    public int SaldoG { get { return Math.Max(0, DvdConfig.PuntosG(Total, Segundos) - GastadoG); } }

    static string ComoJson(EstadisticasDvd f)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        JavaScriptSerializer js = new JavaScriptSerializer();
        return "{\n" +
            "  \"Record\": " + f.Record + ",\n" +
            "  \"RecordFecha\": " + js.Serialize(f.RecordFecha ?? "") + ",\n" +
            "  \"Total\": " + f.Total + ",\n" +
            "  \"SuperiorIzquierda\": " + f.SupIzq + ",\n" +
            "  \"SuperiorDerecha\": " + f.SupDer + ",\n" +
            "  \"InferiorIzquierda\": " + f.InfIzq + ",\n" +
            "  \"InferiorDerecha\": " + f.InfDer + ",\n" +
            "  \"SegundosRebotando\": " + f.Segundos.ToString("0", inv) + ",\n" +
            "  \"GastadoG\": " + f.GastadoG + ",\n" +
            "  \"Skins\": " + js.Serialize(f.Skins ?? "") + "\n}\n";
    }

    long pTotal, pSI, pSD, pII, pID; double pSeg; int recordSesion; bool sucio;

    static byte[] Clave(string uso)
    {
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            return sha.ComputeHash(Encoding.UTF8.GetBytes("DvdRebote|estadisticas|v1|" + uso + "|q7#Lm2!xR9@vT4"));
    }

    static string Cifrar(string texto)
    {
        using (System.Security.Cryptography.RijndaelManaged aes = new System.Security.Cryptography.RijndaelManaged())
        {
            aes.KeySize = 256; aes.BlockSize = 128;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC; aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
            aes.Key = Clave("aes"); aes.GenerateIV();
            byte[] plano = Encoding.UTF8.GetBytes(texto), cifrado;
            using (System.Security.Cryptography.ICryptoTransform t = aes.CreateEncryptor()) cifrado = t.TransformFinalBlock(plano, 0, plano.Length);
            byte[] datos = new byte[16 + cifrado.Length];
            Buffer.BlockCopy(aes.IV, 0, datos, 0, 16); Buffer.BlockCopy(cifrado, 0, datos, 16, cifrado.Length);
            byte[] firma;
            using (System.Security.Cryptography.HMACSHA256 h = new System.Security.Cryptography.HMACSHA256(Clave("firma"))) firma = h.ComputeHash(datos);
            byte[] todo = new byte[datos.Length + 32];
            Buffer.BlockCopy(datos, 0, todo, 0, datos.Length); Buffer.BlockCopy(firma, 0, todo, datos.Length, 32);
            return "DVD1:" + Convert.ToBase64String(todo);
        }
    }

    // devuelve null si el archivo fue modificado o está roto
    static string Descifrar(string archivo)
    {
        try
        {
            archivo = archivo.Trim();
            if (!archivo.StartsWith("DVD1:")) return null;
            byte[] todo = Convert.FromBase64String(archivo.Substring(5));
            if (todo.Length < 16 + 16 + 32) return null;
            int n = todo.Length - 32;
            byte[] firma;
            using (System.Security.Cryptography.HMACSHA256 h = new System.Security.Cryptography.HMACSHA256(Clave("firma"))) firma = h.ComputeHash(todo, 0, n);
            int dif = 0;
            for (int i = 0; i < 32; i++) dif |= firma[i] ^ todo[n + i];
            if (dif != 0) return null;   // lo cambiaron
            using (System.Security.Cryptography.RijndaelManaged aes = new System.Security.Cryptography.RijndaelManaged())
            {
                aes.KeySize = 256; aes.BlockSize = 128;
                aes.Mode = System.Security.Cryptography.CipherMode.CBC; aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                aes.Key = Clave("aes");
                byte[] iv = new byte[16]; Buffer.BlockCopy(todo, 0, iv, 0, 16); aes.IV = iv;
                using (System.Security.Cryptography.ICryptoTransform t = aes.CreateDecryptor())
                    return Encoding.UTF8.GetString(t.TransformFinalBlock(todo, 16, n - 16));
            }
        }
        catch { return null; }
    }

    public static EstadisticasDvd Load()
    {
        EstadisticasDvd e = new EstadisticasDvd();
        Dictionary<string, object> d = null;
        try
        {
            if (File.Exists(Ruta))
            {
                string json = Descifrar(File.ReadAllText(Ruta));
                if (json == null) { e.Modificado = true; return e; }   // modificado: se empieza de cero
                d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            }
            else if (File.Exists(RutaVieja)) d = DvdConfig.ReadJson(RutaVieja);   // la de antes (sin cifrar): se pasa al cifrado al guardar
        }
        catch { }
        if (d == null) return e;
        e.Total = (long)DvdConfig.GetD(d, "Total", 0);
        e.SupIzq = (long)DvdConfig.GetD(d, "SuperiorIzquierda", 0);
        e.SupDer = (long)DvdConfig.GetD(d, "SuperiorDerecha", 0);
        e.InfIzq = (long)DvdConfig.GetD(d, "InferiorIzquierda", 0);
        e.InfDer = (long)DvdConfig.GetD(d, "InferiorDerecha", 0);
        e.Segundos = DvdConfig.GetD(d, "SegundosRebotando", 0);
        e.Record = (int)DvdConfig.GetD(d, "Record", 0);
        e.RecordFecha = DvdConfig.GetS(d, "RecordFecha", "");
        e.GastadoG = (int)DvdConfig.GetD(d, "GastadoG", 0);
        e.Skins = DvdConfig.GetS(d, "Skins", "");
        return e;
    }

    // v: "Superior"/"Inferior", h: "izquierda"/"derecha"; sesion = esquinas sin trucos de esta vez
    public void Esquina(string v, string h, int sesion)
    {
        pTotal++;
        bool arriba = v == "Superior", izq = h == "izquierda";
        if (arriba && izq) pSI++; else if (arriba) pSD++; else if (izq) pII++; else pID++;
        if (sesion > recordSesion) recordSesion = sesion;
        sucio = true;
    }

    public void Tiempo(double s) { if (s > 0) { pSeg += s; sucio = true; } }

    // escribe ESTOS valores tal cual (lo usa el editor de estadísticas); cifrado y firmado igual
    public void Escribir()
    {
        DvdConfig.WriteAtomic(Ruta, Cifrar(ComoJson(this)));
    }

    // TIENDA: compra una skin si te alcanzan los G (lee el archivo de nuevo para no pisar lo que guardó el DVD)
    public static bool Comprar(string skin, int precio)
    {
        EstadisticasDvd f = Load();
        if (f.Modificado || f.TieneSkin(skin) || f.SaldoG < precio) return false;
        f.GastadoG += precio;
        f.Skins = string.IsNullOrEmpty(f.Skins) ? skin : f.Skins + "," + skin;
        f.Escribir();
        return true;
    }

    public void Save()
    {
        if (!sucio) return;
        try
        {
            EstadisticasDvd f = Load();
            f.Total += pTotal; f.SupIzq += pSI; f.SupDer += pSD; f.InfIzq += pII; f.InfDer += pID; f.Segundos += pSeg;
            if (recordSesion > f.Record) { f.Record = recordSesion; f.RecordFecha = DateTime.Now.ToString("dd/MM/yyyy"); }
            DvdConfig.WriteAtomic(Ruta, Cifrar(ComoJson(f)));   // (lleva también los G gastados y las skins)
            try { if (File.Exists(RutaVieja)) File.Delete(RutaVieja); } catch { }   // ya no se deja el de texto
            pTotal = pSI = pSD = pII = pID = 0; pSeg = 0; sucio = false;
        }
        catch { }
    }

    // para la configuración
    public string Texto()
    {
        bool en = Idioma.Ingles;
        string[] nombres = en ? new[] { "Top left", "Top right", "Bottom left", "Bottom right" } : new[] { "Arriba izquierda", "Arriba derecha", "Abajo izquierda", "Abajo derecha" };
        long[] n = { SupIzq, SupDer, InfIzq, InfDer };
        int mas = -1; long max = 0;
        for (int i = 0; i < 4; i++) if (n[i] > max) { max = n[i]; mas = i; }
        long seg = (long)Segundos;
        string t = seg >= 3600 ? (seg / 3600) + " h " + (seg % 3600 / 60) + " min" : seg >= 60 ? (seg / 60) + " min" : seg + " s";
        if (en)
            return "Record: " + Record + " corners" + (RecordFecha.Length > 0 ? " (" + RecordFecha + ")" : "") + "\r\n" +
                   "Total corners: " + Total + "\r\n" +
                   "Most hit: " + (mas >= 0 ? nombres[mas] + " (" + max + ")" : "-") + "\r\n" +
                   "Time bouncing: " + t + (Modificado ? "\r\n(the file was modified: it was reset)" : "");
        return "Récord: " + Record + " esquinas" + (RecordFecha.Length > 0 ? " (" + RecordFecha + ")" : "") + "\r\n" +
               "Esquinas en total: " + Total + "\r\n" +
               "La que más: " + (mas >= 0 ? nombres[mas] + " (" + max + ")" : "-") + "\r\n" +
               "Tiempo rebotando: " + t +
               (Modificado ? "\r\n(el archivo fue modificado: se reinició)" : "");
    }
}

// sonido al tocar una esquina: el .wav/.mp3 que elegiste en la configuración, o el de Windows si no hay
// (o si el archivo ya no existe o no se puede abrir). Usa MCI de Windows, que abre wav, mp3 y wma.
public static class SonidoEsquina
{
    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int mciSendString(string comando, StringBuilder ret, int largo, IntPtr ventana);

    const string Alias = "dvdson";
    static string cargado;   // el archivo que está abierto ahora

    static int Mci(string comando) { return mciSendString(comando, null, 0, IntPtr.Zero); }

    static void Cerrar()
    {
        if (cargado != null) { Mci("close " + Alias); cargado = null; }
    }

    // abre el archivo; devuelve null si se pudo o el error si no
    public static string Abrir(string archivo)
    {
        if (archivo == cargado) return null;
        Cerrar();
        if (!File.Exists(archivo)) return "No encontré el archivo.";
        int r = Mci("open \"" + archivo + "\" type mpegvideo alias " + Alias);
        if (r != 0) return "Windows no puede abrir ese archivo de sonido (código " + r + ").";
        cargado = archivo;
        return null;
    }

    public static void Tocar(string archivo)
    {
        try
        {
            if (!string.IsNullOrEmpty(archivo) && Abrir(archivo) == null && Mci("play " + Alias + " from 0") == 0) return;
        }
        catch { }
        System.Media.SystemSounds.Asterisk.Play();
    }

    public static void Parar()
    {
        try { Cerrar(); } catch { }
    }
}

// IDIOMA: español (como está escrito el programa) o inglés. Idioma.T("texto en español") devuelve el
// texto en inglés si está puesto el inglés. Los textos cortos se cambian solo si son exactos; las
// frases largas también dentro de textos más grandes (por ejemplo en mensajes con un error al final).
public static class Idioma
{
    public static bool Ingles;

    static readonly string[,] Exactos = {
        { "Configuración del DVD", "DVD Settings" }, { "Usar", "Use" }, { "Estilo", "Style" }, { "Disco", "Disc" },
        { "Logo DVD", "DVD logo" }, { "Color", "Color" }, { "Pausado", "Paused" }, { "Texto", "Text" }, { "Quitar", "Remove" },
        { "Acciones", "Actions" }, { "Logros", "Achievements" }, { "Estadísticas", "Statistics" }, { "Fondo de pantalla", "Wallpaper" },
        { "▶ Probar", "▶ Test" }, { "▶ Abrir DVD", "▶ Open DVD" }, { "■ Cerrar DVD", "■ Close DVD" }, { "Abrir carpeta", "Open folder" },
        { "Acceso directo", "Shortcut" }, { "Valores por defecto", "Reset defaults" }, { "Reiniciar contador", "Reset counter" },
        { "Protector de pantalla", "Screensaver" }, { "⟳ Buscar actualizaciones", "⟳ Check for updates" }, { "Buscar actualizaciones", "Check for updates" },
        { "★ Ver logros ›", "★ Achievements ›" }, { "Elegir imagen...", "Choose image..." }, { "Quitar imagen", "Remove image" },
        { "Elegir sonido...", "Choose sound..." }, { "✓ Sonido puesto", "✓ Sound set" }, { "✓ Imagen puesta", "✓ Image set" },
        { "Logo personalizado", "Custom logo" }, { "Sonido de esquina", "Corner sound" }, { "Pantalla del DVD", "DVD screen" },
        { "Idioma / Language", "Idioma / Language" },
        { "No hacer nada", "Do nothing" }, { "Esconder el DVD", "Hide the DVD" }, { "Esconder y pausar", "Hide and pause" }, { "Solo pausar", "Only pause" },
        { "Todas las pantallas", "All screens" }, { "Todas las pantallas (solo tienes una)", "All screens (you only have one)" },
        { "bronce", "bronze" }, { "plata", "silver" }, { "oro", "gold" }, { "BRONCE", "BRONZE" }, { "PLATA", "SILVER" }, { "ORO", "GOLD" },
        { "Pequeños pasos", "Baby steps" }, { "¿Cuánto tiempo tienes?", "How much free time do you have?" }, { "¿En serio llegamos aquí?", "Did we really get here?" },
        { "Consigue un logro", "Get one achievement" }, { "Consigue la mitad de los logros (4 de 7)", "Get half of the achievements (4 of 7)" },
        { "Consigue todos los logros", "Get all the achievements" },
        { "Reloj en el DVD", "Clock on the DVD" }, { "diamante", "diamond" }, { "DIAMANTE", "DIAMOND" }, { "Bro consigue un trabajo", "Bro, get a job" },
        { "!! AVISO: esta versión es una PRE-RELEASE (versión de prueba).", "!! WARNING: this version is a PRE-RELEASE (test version)." },
        { "!! Puede tener errores. Si algo falla, instala la versión normal desde GitHub.", "!! It may have bugs. If something fails, install the normal version from GitHub." },
        { "Consigue 4 logros", "Get 4 achievements" }, { "Consigue 7 logros", "Get 7 achievements" }, { "Consigue todos los logros (11)", "Get all the achievements (11)" },
        { "200G: Esquinero profesional (2500)", "200G: Professional corner hitter (2500)" }, { "300G: El DVD ya te conoce (5000)", "300G: The DVD knows you now (5000)" },
        { "150G: Esto ya es un trabajo (10 h)", "150G: This is a job now (10 h)" }, { "500G: Un día entero rebotando (24 h)", "500G: A whole day bouncing (24 h)" },
        { "10G: Primeras 50 esquinas", "10G: First 50 corners" }, { "20G: Cazador de esquinas (100)", "20G: Corner hunter (100)" },
        { "50G: Maestro de las esquinas (500)", "50G: Corner master (500)" }, { "100G: Leyenda del DVD (1000)", "100G: DVD legend (1000)" },
        { "20G: Una hora mirando el DVD", "20G: One hour watching the DVD" }, { "40G: ¿No tienes nada que hacer? (2 h)", "40G: Nothing better to do? (2 h)" },
        { "100G: Tener mucho tiempo libre (5 h)", "100G: Way too much free time (5 h)" }, { "10G: Logro de prueba", "10G: Test achievement" },
        { "Logro desbloqueado", "Achievement unlocked" }, { "Código incorrecto", "Wrong code" },
        { "Actualización pendiente", "Update pending" }, { "No se pudo revisar", "Couldn't check" }, { "Descargando...", "Downloading..." },
        { "Instalando...", "Installing..." }, { "Buscando actualizaciones...", "Checking for updates..." }, { "No se pudo actualizar", "Couldn't update" },
    };
    static readonly string[,] Frases = {
        { "Mostrar contador al tocar esquina", "Show counter on corner hit" }, { "Mostrar \"¡ESQUINA!\" al tocar", "Show \"CORNER!\" on hit" },
        { "Cambiar de color al rebotar", "Change color on bounce" }, { "Tolerancia esquina (px)", "Corner tolerance (px)" },
        { "Sonido en las esquinas", "Sound on corners" }, { "Estela detrás de los DVDs", "Trail behind the DVDs" }, { "Vibrar el control (Xbox)", "Vibrate controller (Xbox)" },
        { "Modo solo esquinas (va de esquina a esquina)", "Corners-only mode (goes corner to corner)" },
        { "Modo solo esquinas (necesita modo_solo_esquinas.bat)", "Corners-only mode (needs modo_solo_esquinas.bat)" },
        { "Choque entre DVDs", "DVDs collide" }, { "Iniciar con Windows", "Start with Windows" }, { "Rebotar en las ventanas abiertas", "Bounce off open windows" },
        { "Fondo de pantalla animado", "Animated wallpaper" }, { "Con fondo negro", "With black background" },
        { "En pantalla completa (juegos, videos)", "In fullscreen (games, videos)" }, { "Velocidad (px/s)", "Speed (px/s)" }, { "Tamaño (%)", "Size (%)" },
        { "Opacidad (%)", "Opacity (%)" }, { "Cantidad de DVDs", "Number of DVDs" },
        { "Atajos:  Ctrl+Alt+D esconder / mostrar\r\n             Ctrl+Alt+P pausar", "Shortcuts:  Ctrl+Alt+D hide / show\r\n                   Ctrl+Alt+P pause" },
        { "● DvdRebote está corriendo — los cambios se aplican al instante", "● DvdRebote is running — changes apply instantly" },
        { "○ DvdRebote no está abierto", "○ DvdRebote is not open" }, { "Recargado (cambio externo)", "Reloaded (external change)" },
        { "Código secreto: escríbelo y pulsa Enter", "Secret code: type it and press Enter" }, { "Abre el DVD primero para usar el código", "Open the DVD first to use the code" },
        { "Código enviado al DVD:", "Code sent to the DVD:" }, { "No se pudo mandar el código. Prueba otra vez.", "Couldn't send the code. Try again." },
        { "Protector creado junto al DVD: elige los minutos y pulsa Aceptar", "Screensaver created next to the DVD: choose the minutes and press OK" },
        { "No se pudo crear el protector de pantalla:", "Couldn't create the screensaver:" },
        { "Accesos directos creados en el escritorio ✓", "Desktop shortcuts created ✓" },
        { "Listo: en tu escritorio quedaron \"DVD Rebote\" y \"Configurar DVD\".", "Done: \"DVD Rebote\" and \"Configurar DVD\" are now on your desktop." },
        { "No se pudo crear el acceso directo:", "Couldn't create the shortcut:" }, { "No se pudo usar esa imagen:", "Couldn't use that image:" },
        { "No se pudo usar ese sonido:", "Couldn't use that sound:" }, { "El DVD se abrirá al encender la PC", "The DVD will open when the PC starts" },
        { "El DVD ya no se abre al encender la PC", "The DVD no longer opens when the PC starts" },
        { "No se pudo cambiar el inicio con Windows:", "Couldn't change start with Windows:" },
        { "No encuentro DvdRebote.exe.", "I can't find DvdRebote.exe." }, { "Ábrelo una vez y vuelve a pulsar el botón.", "Open it once and press the button again." },
        { "Ábrelo una vez y vuelve a marcar la casilla.", "Open it once and check the box again." },
        { "Ponlo en la misma carpeta que dvd_config.exe, o ábrelo una vez desde donde esté y así lo encuentro.", "Put it in the same folder as dvd_config.exe, or open it once from wherever it is so I can find it." },
        { "Guardado ✓", "Saved ✓" }, { "Error al guardar:", "Error saving:" }, { "Archivo:", "File:" },
        { "Sigue los pasos para poner el protector", "Follow the steps to set the screensaver" },
        { "Ya tienes la versión más nueva ✓", "You already have the newest version ✓" }, { "Actualizado ✓", "Updated ✓" },
        { "Esta versión cambia los ajustes de la configuración.", "This version changes the settings." },
        { "¿Quieres eliminar el dvd_config.json viejo y poner uno nuevo?", "Do you want to delete the old dvd_config.json and create a new one?" },
        { "Tus ajustes se quedan igual: solo se agregan los nuevos y se quitan los que ya no se usan.", "Your settings stay the same: only new ones are added and unused ones removed." },
        { "¡Listo, ya tienes la versión nueva!", "Done, you have the new version!" }, { "La configuración se va a volver a abrir.", "The settings will reopen." },
        { "¡Listo, ya tienes la versión nueva del DVD!", "Done, you have the new DVD version!" }, { "¿Quieres ver los cambios?", "Do you want to see the changes?" },
        { "No se pudo descargar la actualización:", "Couldn't download the update:" }, { "No se pudieron cambiar los archivos:", "Couldn't replace the files:" },
        { "Buscando updates...", "Searching for updates..." }, { "Conectando con GitHub (DVD-REBOTES)...", "Connecting to GitHub (DVD-REBOTES)..." },
        { "ERROR: no pude revisar si hay updates.", "ERROR: couldn't check for updates." }, { "Revisa tu internet e inténtalo otra vez.", "Check your internet and try again." },
        { "Ya tienes la versión más nueva del DVD. No hay nada que instalar.", "You already have the newest DVD version. Nothing to install." },
        { "¡Encontré una versión nueva", "Found a new version" }, { "Esto es lo que cambia:", "This is what changes:" }, { "(subido el", "(uploaded" },
        { "¿Quieres instalar la actualización? (S/N): ", "Do you want to install the update? (Y/N): " },
        { "Actualización cancelada. Cuando quieras, vuelve a darle a Buscar actualizaciones.", "Update cancelled. Whenever you want, press Check for updates again." },
        { "Instalando la actualización (el DVD se cierra un momento y se vuelve a abrir)...", "Installing the update (the DVD closes for a moment and reopens)..." },
        { "Presiona cualquier tecla para cerrar...", "Press any key to close..." },
        { "Principal (", "Main (" }, { "Pantalla ", "Screen " }, { "[Versión", "[Version" },
    };

    public static string T(string es)
    {
        if (!Ingles || string.IsNullOrEmpty(es)) return es;
        for (int i = 0; i < Exactos.GetLength(0); i++) if (es == Exactos[i, 0]) return Exactos[i, 1];
        string r = es;
        for (int i = 0; i < Frases.GetLength(0); i++) if (r.Contains(Frases[i, 0])) r = r.Replace(Frases[i, 0], Frases[i, 1]);
        return r;
    }
}

// MessageBox con los textos traducidos (si está en inglés)
public static class Msg
{
    public static System.Windows.Forms.DialogResult Show(string t) { return System.Windows.Forms.MessageBox.Show(Idioma.T(t)); }
    public static System.Windows.Forms.DialogResult Show(string t, string c) { return System.Windows.Forms.MessageBox.Show(Idioma.T(t), Idioma.T(c)); }
    public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window o, string t, string c)
    { return System.Windows.Forms.MessageBox.Show(o, Idioma.T(t), Idioma.T(c)); }
    public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window o, string t, string c, System.Windows.Forms.MessageBoxButtons b)
    { return System.Windows.Forms.MessageBox.Show(o, Idioma.T(t), Idioma.T(c), b); }
    public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window o, string t, string c, System.Windows.Forms.MessageBoxButtons b, System.Windows.Forms.MessageBoxIcon i)
    { return System.Windows.Forms.MessageBox.Show(o, Idioma.T(t), Idioma.T(c), b, i); }
    public static System.Windows.Forms.DialogResult Show(string t, string c, System.Windows.Forms.MessageBoxButtons b, System.Windows.Forms.MessageBoxIcon i)
    { return System.Windows.Forms.MessageBox.Show(Idioma.T(t), Idioma.T(c), b, i); }
}
