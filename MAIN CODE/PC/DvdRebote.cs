// Logos DVD rebotando, transparentes sobre el escritorio - .NET Framework (WinForms)
// Todo se dibuja en UNA sola ventana transparente del tamaño de la pantalla, así se pueden
// poner muchísimos logos. La configuración se lee de dvd_config.json y se aplica en vivo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Ventana transparente por píxel, siempre encima y que deja pasar los clics.
// Tiene una superficie (DIB) fija donde se dibuja cada frame sin crear bitmaps nuevos.
class OverlayWindow : Form
{
    IntPtr memDc = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
    public Bitmap Surface;
    public int SW, SH;
    public static bool ClickThrough = true;   // false en modo protector de pantalla

    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // LAYERED | TRANSPARENT (clic atraviesa) | TOOLWINDOW | TOPMOST | NOACTIVATE
            cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000;
            if (ClickThrough) cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: los clics pasan a lo de abajo
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    public void EnsureSurface(int w, int h)
    {
        if (Surface != null && SW == w && SH == h) return;
        FreeSurface();
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        memDc = CreateCompatibleDC(screenDc);
        ReleaseDC(IntPtr.Zero, screenDc);
        IntPtr bits;
        hBmp = CreateDIBSection(memDc, ref bi, 0, out bits, IntPtr.Zero, 0);
        oldBmp = SelectObject(memDc, hBmp);
        Surface = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        SW = w; SH = h;
    }

    public IntPtr MemDc { get { return memDc; } }   // para el fondo animado
    bool vacia;

    // en modo fondo animado la ventana de encima se queda vacía (los DVDs van detrás de los iconos)
    public void Vaciar(int x, int y)
    {
        if (vacia || memDc == IntPtr.Zero) return;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            SIZE size; size.cx = SW; size.cy = SH;
            POINT src; src.x = 0; src.y = 0;
            POINT pos; pos.x = x; pos.y = y;
            BLENDFUNCTION bf; bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 0; bf.AlphaFormat = 1;
            UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref bf, 2);
            vacia = true;
        }
        finally { ReleaseDC(IntPtr.Zero, screenDc); }
    }

    public void Present(int x, int y)
    {
        vacia = false;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            SIZE size; size.cx = SW; size.cy = SH;
            POINT src; src.x = 0; src.y = 0;
            POINT pos; pos.x = x; pos.y = y;
            BLENDFUNCTION bf; bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
            UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref bf, 2);
        }
        finally { ReleaseDC(IntPtr.Zero, screenDc); }
    }

    public void FreeSurface()
    {
        if (Surface != null) { Surface.Dispose(); Surface = null; }
        if (memDc != IntPtr.Zero)
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            memDc = IntPtr.Zero;
        }
    }

    // ---- Atajos de teclado globales (RegisterHotKey: Windows avisa solo cuando pulsas esa combinación) ----
    public event Action<int> HotKey;
    const int WM_HOTKEY = 0x0312;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && HotKey != null) HotKey(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public bool RegistrarAtajo(int id, uint modificadores, Keys tecla)
    {
        return RegisterHotKey(Handle, id, modificadores | 0x4000 /*MOD_NOREPEAT*/, (uint)tecla);
    }

    public void QuitarAtajo(int id)
    {
        if (IsHandleCreated) UnregisterHotKey(Handle, id);
    }

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void BringToTopNoActivate()
    {
        if (!IsHandleCreated) return;
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010); // TOPMOST, NOMOVE|NOSIZE|NOACTIVATE
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
}

// Vibración de controles Xbox / XInput (hasta 4 controles conectados)
static class Mando
{
    [StructLayout(LayoutKind.Sequential)] struct XINPUT_VIBRATION { public ushort Left, Right; }
    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_STATE { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }

    // Windows 8/10/11
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern int GetState14(int i, out XINPUT_STATE s);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern int SetState14(int i, ref XINPUT_VIBRATION v);
    // Windows 7
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] static extern int GetState91(int i, out XINPUT_STATE s);
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")] static extern int SetState91(int i, ref XINPUT_VIBRATION v);

    static int version = 0;          // 0 = sin probar, 14, 91, -1 = no hay XInput
    static double pararEn = -1;
    static bool[] vibrando = new bool[4];

    static bool Conectado(int i)
    {
        XINPUT_STATE s;
        try
        {
            if (version == 0) { try { GetState14(i, out s); version = 14; } catch { try { GetState91(i, out s); version = 91; } catch { version = -1; } } }
            if (version == 14) return GetState14(i, out s) == 0;
            if (version == 91) return GetState91(i, out s) == 0;
        }
        catch { version = -1; }
        return false;
    }

    static void Poner(int i, ushort fuerza)
    {
        XINPUT_VIBRATION v; v.Left = fuerza; v.Right = fuerza;
        try { if (version == 14) SetState14(i, ref v); else if (version == 91) SetState91(i, ref v); } catch { }
    }

    // Vibra todos los controles conectados un momento. Devuelve cuántos vibraron.
    public static int Vibrar(double now, double segundos)
    {
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!Conectado(i)) continue;
            Poner(i, 0xC000);
            vibrando[i] = true;
            n++;
        }
        if (n > 0) pararEn = now + segundos;
        return n;
    }

    // Se llama en cada frame: apaga la vibración cuando pasa el tiempo
    public static void Tick(double now)
    {
        if (pararEn < 0 || now < pararEn) return;
        Parar();
    }

    public static void Parar()
    {
        pararEn = -1;
        for (int i = 0; i < 4; i++) if (vibrando[i]) { Poner(i, 0); vibrando[i] = false; }
    }
}

// Un DVD que rebota
class Logo
{
    public float X, Y, Vx, Vy, Spin;
    public Color Color;
    public double LastCorner = -9999, GlowStart = -1, GlowUntil = -1;
    // modo solo esquinas (recorrido "desdoblado"): de (U0,V0) a la esquina (A,B)
    public bool SoloActive;
    public double U0, V0, T;     // inicio (fracción de pantalla) y avance 0..1
    public int A, B;             // esquina destino desdoblada (números enteros)
    public int SegX, SegY;       // tramo actual (cambia en cada rebote con una pared)

    // Estela: últimas posiciones (anillo circular)
    public const int MaxEstela = 8;
    public readonly float[] Hx = new float[MaxEstela], Hy = new float[MaxEstela];
    public int HCount, HPos;
    public double HLast = -9999;

    public double NextCaos = 0;   // cheat CAOS: cuándo cambia de dirección
    public double EmpujonHasta = -1; // código TOCA: sale disparado al duplicarse (se calma en 1.2 s)
}

class DvdApp : ApplicationContext
{
    const float Pad = 70f;            // margen del brillo alrededor del logo
    const int MaxSpritesEnCache = 120;

    DvdConfig cfg = new DvdConfig();
    DateTime cfgStamp = DateTime.MinValue;
    double nextCfgCheck = 0, nextBatCheck = 0, nextTopmost = 0, lastSound = -9999, lastRumble = -9999;
    int lastResetToken;
    bool batInstalado;

    DvdStats stats = new DvdStats(); // solo en memoria: no se guarda
    readonly List<Logo> logos = new List<Logo>();
    readonly OverlayWindow win = new OverlayWindow();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly EventWaitHandle exitSignal;
    ToolStripMenuItem pauseItem;

    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Random rnd = new Random();
    double lastTime;

    double msgStart = -1, msgUntil = -1;
    Color msgColor = Color.White;
    string lastV = "", lastH = "";

    // Paleta de colores (así los dibujos se reutilizan aunque haya miles de logos)
    readonly Color[] palette = new Color[24];

    // Dibujos ya hechos de cada logo por color (normal y brillo)
    readonly Dictionary<int, Bitmap> spriteCache = new Dictionary<int, Bitmap>();
    readonly Dictionary<int, Bitmap> haloCache = new Dictionary<int, Bitmap>();
    string cacheKey = "";

    // ---- protector de pantalla ----
    readonly bool protector;
    uint entradaInicial;
    double protectorInicio;
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    static uint UltimaEntrada()
    {
        LASTINPUTINFO li = new LASTINPUTINFO();
        li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        GetLastInputInfo(ref li);
        return li.dwTime;
    }

    // En modo protector: se cierra al mover el ratón, hacer clic o pulsar una tecla
    bool ProtectorDebeCerrar(double now)
    {
        if (!protector) return false;
        uint e = UltimaEntrada();
        if (now - protectorInicio < 1.0) { entradaInicial = e; return false; } // 1 s de margen al empezar
        return e != entradaInicial;
    }

    // ¿Está el protector de pantalla (.scr) en marcha? Él tiene el mutex "DvdRebote_Protector".
    bool protectorEncendido;
    double nextProtCheck;

    static bool ProtectorCorriendo()
    {
        try
        {
            Mutex m;
            if (Mutex.TryOpenExisting("DvdRebote_Protector", out m)) { m.Dispose(); return true; }
        }
        catch { }
        return false;
    }

    public DvdApp(EventWaitHandle exitSignal) : this(exitSignal, false) { }

    public DvdApp(EventWaitHandle exitSignal, bool protector)
    {
        this.exitSignal = exitSignal;
        this.protector = protector;
        for (int i = 0; i < palette.Length; i++) palette[i] = FromHsv(i * 360.0 / palette.Length, 0.75, 1.0);

        DvdConfig loaded = DvdConfig.Load();
        if (loaded != null) cfg = loaded;
        Idioma.Ingles = cfg.Idioma == "en";
        cfgStamp = SafeStamp();
        lastResetToken = cfg.ReiniciarContador;
        batInstalado = DvdConfig.BatInstalado();
        SyncLogoCount();

        if (protector)
        {
            // Protector de pantalla: fondo negro, sin icono, sin atajos ni cheats, sin cursor
            protectorInicio = clock.Elapsed.TotalSeconds;
            entradaInicial = UltimaEntrada();
            Cursor.Hide();
            timer.Interval = 10;
            timer.Tick += delegate { Step(); };
            lastTime = clock.Elapsed.TotalSeconds;
            timer.Start();
            return;
        }

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Configuración...", null, delegate { OpenConfigApp(); });
        pauseItem = new ToolStripMenuItem(cfg.Pausado ? "Reanudar" : "Pausar", null, delegate { TogglePause(); });
        menu.Items.Add(pauseItem);
        menu.Items.Add("Apuntar a una esquina", null, delegate { AimAtCorner(); });
        menu.Items.Add("Esconder / mostrar  (Ctrl+Alt+D)", null, delegate { ToggleOculto(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, delegate { Quit(); });
        tray.ContextMenuStrip = menu;
        tray.Text = "DVD Rebote";
        RefreshTrayIcon();
        tray.Visible = true;
        tray.DoubleClick += delegate { OpenConfigApp(); };

        timer.Interval = 10;
        timer.Tick += delegate { Step(); };
        lastTime = clock.Elapsed.TotalSeconds;
        timer.Start();
        StartCheatPipe();

        // Atajos: Ctrl+Alt+D esconder/mostrar, Ctrl+Alt+P pausar/reanudar
        win.HotKey += OnAtajo;
        const uint CTRL_ALT = 0x0002 | 0x0001;
        if (!win.RegistrarAtajo(AtajoEsconder, CTRL_ALT, Keys.D)) atajosFallidos += "Ctrl+Alt+D ";
        if (!win.RegistrarAtajo(AtajoPausar, CTRL_ALT, Keys.P)) atajosFallidos += "Ctrl+Alt+P ";
        if (atajosFallidos.Length > 0)
            tray.ShowBalloonTip(4000, "DVD Rebote", "Otro programa ya usa: " + atajosFallidos.Trim(), ToolTipIcon.Warning);
    }

    // ---------------- atajos de teclado ----------------
    const int AtajoEsconder = 1, AtajoPausar = 2;
    string atajosFallidos = "";
    bool oculto = false;

    void OnAtajo(int id)
    {
        if (id == AtajoEsconder) ToggleOculto();
        else if (id == AtajoPausar)
        {
            TogglePause();
            MostrarAviso(cfg.Pausado ? "PAUSADO  (Ctrl+Alt+P)" : "REANUDADO  (Ctrl+Alt+P)");
        }
    }

    void ToggleOculto()
    {
        oculto = !oculto;
        if (oculto) Mando.Parar(); // si el control estaba vibrando, se para al esconder
        if (!oculto) MostrarAviso("DVD VISIBLE  (Ctrl+Alt+D)");
    }

    void MostrarAviso(string texto)
    {
        double now = clock.Elapsed.TotalSeconds;
        cheatMsg = texto;
        cheatMsgStart = now; cheatMsgUntil = now + 2.0;
    }

    float Scale { get { return cfg.Tamano / 100f * (gigante ? 2f : 1f) * (enano ? 0.5f : 1f); } }
    // con TROLL la caja es cuadrada (la imagen es cuadrada)
    float BaseW { get { return cfg.Estilo == "disco" || troll ? 150f : 220f; } }
    float BaseH { get { return cfg.Estilo == "disco" || troll ? 150f : 110f; } }
    float W { get { return BaseW * Scale; } }
    float H { get { return BaseH * Scale; } }

    // ---------------- configuración ----------------

    DateTime SafeStamp()
    {
        try { return File.GetLastWriteTimeUtc(DvdConfig.RutaConfig); } catch { return DateTime.MinValue; }
    }

    void CheckConfig(double now)
    {
        if (now < nextCfgCheck) return;
        nextCfgCheck = now + 0.3;
        DateTime st = SafeStamp();
        if (st == cfgStamp) return;
        DvdConfig c = DvdConfig.Load();
        if (c == null) return; // archivo a medio escribir: se reintenta
        cfgStamp = st;
        string oldColor = cfg.Color;
        bool oldCambiar = cfg.CambiarColor;
        cfg = c; Idioma.Ingles = cfg.Idioma == "en";
        if (cfg.Color != oldColor || (oldCambiar && !cfg.CambiarColor))
            foreach (Logo L in logos) L.Color = cfg.GetColor();
        if (cfg.ReiniciarContador != lastResetToken)
        {
            lastResetToken = cfg.ReiniciarContador;
            stats = new DvdStats(); limpio = 0;
        }
        SyncLogoCount();
        pauseItem.Text = cfg.Pausado ? "Reanudar" : "Pausar";
        RefreshTrayIcon();
    }

    void SyncLogoCount()
    {
        Rectangle s = Area;   // la pantalla elegida (o todas)
        while (logos.Count < Count)
        {
            Logo L = new Logo();
            double ang = rnd.NextDouble() * Math.PI / 3 + Math.PI / 12 + rnd.Next(4) * Math.PI / 2;
            L.Vx = (float)Math.Cos(ang); L.Vy = (float)Math.Sin(ang);
            L.X = s.Left + (float)rnd.NextDouble() * Math.Max(1, s.Width - W);
            L.Y = s.Top + (float)rnd.NextDouble() * Math.Max(1, s.Height - H);
            L.Spin = rnd.Next(360);
            L.Color = (logos.Count == 0 || !cfg.CambiarColor) ? cfg.GetColor() : palette[rnd.Next(palette.Length)];
            logos.Add(L);
        }
        if (logos.Count > Count) logos.RemoveRange(Count, logos.Count - Count);
    }

    // ---------------- bucle principal ----------------

    void Step()
    {
        if (exitSignal.WaitOne(0)) { Quit(); return; }

        double now = clock.Elapsed.TotalSeconds;
        if (CheckCheats(now)) { Quit(); return; }
        if (ProtectorDebeCerrar(now)) { Quit(); return; } // protector: se cierra al tocar ratón o teclado
        // DVD normal: si el protector de pantalla (.scr) está en marcha, se esconde para no salir encima
        if (!protector && now >= nextProtCheck)
        {
            nextProtCheck = now + 0.5;
            bool antes = protectorEncendido;
            protectorEncendido = ProtectorCorriendo();
            if (protectorEncendido && !antes) Mando.Parar();
        }
        // juego o video en pantalla completa: se esconde / pausa según la configuración
        if (!protector && now >= nextPantallaCompleta)
        {
            nextPantallaCompleta = now + 0.5;
            bool antes = pantallaCompleta;
            pantallaCompleta = cfg.PantallaCompleta != "nada" && HayPantallaCompleta(Area);
            if (pantallaCompleta && !antes) Mando.Parar();
        }
        Mando.Tick(now); // apaga la vibración del control cuando toca
        float dt = (float)Math.Min(0.05, now - lastTime);
        lastTime = now;
        CheckConfig(now);
        if (now >= nextBatCheck) { nextBatCheck = now + 1.0; batInstalado = DvdConfig.BatInstalado(); }
        if (now >= nextTopmost) { nextTopmost = now + 1.0; win.BringToTopNoActivate(); }

        Rectangle s = Area;   // la pantalla elegida (o todas)
        if (ChocarConVentanas && now >= nextVentanas) { nextVentanas = now + 0.25; LeerVentanas(s); }
        foreach (Logo L in logos) Move(L, now, dt, s);
        // tiempo rebotando (visible, sin pausa y sin trucos); se guarda cada 10 s
        if (!Paused && !oculto && !protectorEncendido && !EscondidoPorPantallaCompleta && !TrucoActivo(now)) { hist.Tiempo(dt); AvanzarLogros(now, 0, dt); }
        if (!Paused && !oculto && !protectorEncendido && !EscondidoPorPantallaCompleta) tiempoRebotando += dt;   // lo que enseña el estilo RELOJ
        if (now >= nextGuardarHist) { nextGuardarHist = now + 10; hist.Save(); }
        if (rebotante && ChocarConVentanas) MoverVentanas(dt);
        else if (moviendo.Count > 0) moviendo.Clear();   // escondido: las ventanas que iban deslizándose se paran
        if (cfg.Choque && !Solo && !Paused) Chocar();
        Render(now, s);
    }

    // ---- REBOTAR EN LAS VENTANAS: los bordes de las ventanas abiertas son paredes ----
    // Se leen las ventanas cada 0.25 s. Si un DVD ya estaba encima de una ventana (por ejemplo
    // cuando la abres encima de él) la atraviesa hasta salir; solo rebota al llegar desde afuera.
    delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECTW r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECTW r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    [StructLayout(LayoutKind.Sequential)] struct RECTW { public int Left, Top, Right, Bottom; }

    class Ventana { public IntPtr H; public RectangleF R; }
    readonly List<Ventana> ventanas = new List<Ventana>();
    double nextVentanas;
    bool rebotante;   // código secreto REBOTANTE: al chocar, el DVD empuja la ventana
    // Con el DVD escondido (Ctrl+Alt+D) o con el protector de pantalla puesto, no se choca con nada:
    // si no se ve el DVD, tus ventanas no deben moverse solas.
    bool ChocarConVentanas { get { return (cfg.RebotarVentanas || rebotante) && !protector && !oculto && !protectorEncendido; } }
    const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

    // Al activar REBOTANTE: las ventanas maximizadas o casi del tamaño de la pantalla no se pueden
    // empujar (no tienen borde por fuera), así que se restauran y se achican al 60 % en un lugar al azar.
    void AchicarVentanasGrandes()
    {
        Rectangle s = Screen.PrimaryScreen.WorkingArea;
        List<IntPtr> grandes = new List<IntPtr>();
        System.Text.StringBuilder clase = new System.Text.StringBuilder(64);
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            try
            {
                if (!IsWindowVisible(h) || IsIconic(h) || GetWindowTextLength(h) == 0) return true;
                int ex = GetWindowLong(h, -20);
                if ((ex & 0x80) != 0 || (ex & 0x20) != 0 || (ex & 0x08000000) != 0) return true;
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == miPid) return true;
                int oculta;
                if (DwmGetWindowAttribute(h, 14, out oculta, 4) == 0 && oculta != 0) return true;
                clase.Length = 0; GetClassName(h, clase, 64);
                string c = clase.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
                RECTW r; GetWindowRect(h, out r);
                Rectangle f = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                Rectangle dentro = Rectangle.Intersect(f, s);
                // para probar sin tocar tus ventanas: DVD_PRUEBA_VENTANA = título de la única ventana a achicar
                string soloEsta = Environment.GetEnvironmentVariable("DVD_PRUEBA_VENTANA");
                if (!string.IsNullOrEmpty(soloEsta))
                {
                    System.Text.StringBuilder t = new System.Text.StringBuilder(256);
                    GetWindowText(h, t, 256);
                    if (t.ToString() != soloEsta) return true;
                }
                if (IsZoomed(h) || (long)dentro.Width * dentro.Height >= (long)s.Width * s.Height * 9 / 10) grandes.Add(h);
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        foreach (IntPtr h in grandes)
        {
            // se guarda cómo estaba para dejarla igual al quitar REBOTANTE
            RECTW r0; GetWindowRect(h, out r0);
            achicadas.Add(new Achicada { H = h, Maximizada = IsZoomed(h), R = Rectangle.FromLTRB(r0.Left, r0.Top, r0.Right, r0.Bottom) });
            if (IsZoomed(h)) ShowWindow(h, 9 /*SW_RESTORE*/);
            int w = s.Width * 6 / 10, hh = s.Height * 6 / 10;
            int x = s.Left + rnd.Next(Math.Max(1, s.Width - w)), y = s.Top + rnd.Next(Math.Max(1, s.Height - hh));
            SetWindowPos(h, IntPtr.Zero, x, y, w, hh, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    // ---- ventanas achicadas por REBOTANTE: al quitarlo vuelven a como estaban ----
    class Achicada { public IntPtr H; public bool Maximizada; public Rectangle R; }
    readonly List<Achicada> achicadas = new List<Achicada>();
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);

    void RestaurarVentanas()
    {
        moviendo.Clear();
        foreach (Achicada a in achicadas)
        {
            if (!IsWindow(a.H)) continue;
            if (a.Maximizada) ShowWindow(a.H, 3 /*SW_MAXIMIZE*/);
            else SetWindowPos(a.H, IntPtr.Zero, a.R.X, a.R.Y, a.R.Width, a.R.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        achicadas.Clear();
    }

    // ---- ventanas en movimiento: cada golpe les da velocidad, se deslizan, se frenan
    //      poco a poco y rebotan en los bordes de la pantalla ----
    class Deslizando { public double X, Y, Vx, Vy; public int UltX, UltY; }
    readonly Dictionary<IntPtr, Deslizando> moviendo = new Dictionary<IntPtr, Deslizando>();

    // El golpe es más fuerte cuanto más rápido va el DVD (con easteregg las ventanas salen volando)
    void EmpujarVentana(Ventana v, float dx, float dy)
    {
        Deslizando d;
        if (!moviendo.TryGetValue(v.H, out d))
        {
            RECTW r;
            if (!GetWindowRect(v.H, out r)) return;
            d = new Deslizando { X = r.Left, Y = r.Top, UltX = r.Left, UltY = r.Top };
            moviendo[v.H] = d;
        }
        double fuerza = Math.Min(2500, 250 + Vel * 0.6);
        d.Vx += Math.Sign(dx) * fuerza;
        d.Vy += Math.Sign(dy) * fuerza;
    }

    void MoverVentanas(float dt)
    {
        if (moviendo.Count == 0) return;
        Rectangle s = Screen.PrimaryScreen.WorkingArea;
        double freno = Math.Exp(-2.5 * dt);   // se va frenando sola
        List<IntPtr> quietas = null;
        foreach (KeyValuePair<IntPtr, Deslizando> p in moviendo)
        {
            IntPtr h = p.Key; Deslizando d = p.Value;
            RECTW r;
            // si la cerraste, la minimizaste o la moviste tú con el ratón, se deja en paz
            if (!IsWindow(h) || IsIconic(h) || !GetWindowRect(h, out r) || Math.Abs(r.Left - d.UltX) > 3 || Math.Abs(r.Top - d.UltY) > 3)
            { (quietas = quietas ?? new List<IntPtr>()).Add(h); continue; }
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            d.X += d.Vx * dt; d.Y += d.Vy * dt;
            d.Vx *= freno; d.Vy *= freno;
            // rebota en los bordes de la pantalla (pierde un poco de fuerza)
            if (d.X < s.Left) { d.X = s.Left; d.Vx = Math.Abs(d.Vx) * 0.6; }
            if (d.X + w > s.Right) { d.X = Math.Max(s.Left, s.Right - w); d.Vx = -Math.Abs(d.Vx) * 0.6; }
            if (d.Y < s.Top) { d.Y = s.Top; d.Vy = Math.Abs(d.Vy) * 0.6; }
            if (d.Y + hh > s.Bottom) { d.Y = Math.Max(s.Top, s.Bottom - hh); d.Vy = -Math.Abs(d.Vy) * 0.6; }
            int nx = (int)Math.Round(d.X), ny = (int)Math.Round(d.Y);
            if (nx != r.Left || ny != r.Top)
            {
                SetWindowPos(h, IntPtr.Zero, nx, ny, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                foreach (Ventana v in ventanas) if (v.H == h) v.R.Offset(nx - r.Left, ny - r.Top);
            }
            d.UltX = nx; d.UltY = ny;
            if (Math.Abs(d.Vx) < 8 && Math.Abs(d.Vy) < 8) (quietas = quietas ?? new List<IntPtr>()).Add(h);
        }
        if (quietas != null) foreach (IntPtr h in quietas) moviendo.Remove(h);
    }
    readonly uint miPid = (uint)Process.GetCurrentProcess().Id;

    void LeerVentanas(Rectangle s)
    {
        ventanas.Clear();
        System.Text.StringBuilder clase = new System.Text.StringBuilder(64);
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            try
            {
                if (!IsWindowVisible(h) || IsIconic(h) || GetWindowTextLength(h) == 0) return true;
                int ex = GetWindowLong(h, -20);
                if ((ex & 0x80) != 0 || (ex & 0x20) != 0 || (ex & 0x08000000) != 0) return true; // herramienta, transparente, sin foco
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == miPid) return true;
                int oculta;
                if (DwmGetWindowAttribute(h, 14 /*CLOAKED*/, out oculta, 4) == 0 && oculta != 0) return true; // en otro escritorio, etc.
                clase.Length = 0; GetClassName(h, clase, 64);
                string c = clase.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
                RECTW r;
                if (DwmGetWindowAttribute(h, 9 /*EXTENDED_FRAME_BOUNDS*/, out r, 16) != 0) GetWindowRect(h, out r);
                RectangleF f = RectangleF.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (f.Width < 60 || f.Height < 40 || !f.IntersectsWith(s)) return true;
                // las ventanas maximizadas o de pantalla completa no cuentan (taparían todo)
                RectangleF dentro = RectangleF.Intersect(f, s);
                if (dentro.Width * dentro.Height >= s.Width * (float)s.Height * 0.9f) return true;
                ventanas.Add(new Ventana { H = h, R = f });
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    void ChocarVentanas(Logo L, float px, float py)
    {
        float w = W, h = H;
        float empuje = 30f * Scale; // cuánto se mueve la ventana en cada golpe (código REBOTANTE)
        for (int i = 0; i < ventanas.Count; i++)
        {
            Ventana vent = ventanas[i];
            RectangleF v = vent.R;
            bool ahora = L.X < v.Right && L.X + w > v.Left && L.Y < v.Bottom && L.Y + h > v.Top;
            if (!ahora) continue;
            bool antes = px < v.Right && px + w > v.Left && py < v.Bottom && py + h > v.Top;
            if (antes) continue; // ya estaba encima: la atraviesa
            float dx = 0, dy = 0;
            if (px + w <= v.Left) { L.X = v.Left - w; L.Vx = -Math.Abs(L.Vx); dx = empuje; }
            else if (px >= v.Right) { L.X = v.Right; L.Vx = Math.Abs(L.Vx); dx = -empuje; }
            else if (py + h <= v.Top) { L.Y = v.Top - h; L.Vy = -Math.Abs(L.Vy); dy = empuje; }
            else { L.Y = v.Bottom; L.Vy = Math.Abs(L.Vy); dy = -empuje; }
            if (cfg.CambiarColor) NewColor(L);
            if (rebotante) EmpujarVentana(vent, dx, dy);
        }
    }

    // ---- código secreto TOCA: haces clic en un DVD y se duplica ----
    // La capa de los DVDs deja pasar los clics, así que se usa un "gancho" del ratón de Windows:
    // si el clic cae encima de un DVD se lo queda el DVD (no le llega a la ventana de abajo);
    // si cae en otro lado, pasa normal.
    delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public int X, Y; public uint mouseData, flags, time; public IntPtr extra; }

    bool toca;
    bool troll;   // código secreto TROLL
    VentanaEstatica ventanaError;   // código secreto ERROR
    PantallaAzul pantallaAzul;      // la pantalla azul falsa que sale 2 s después de "Aceptar"
    ConsolaFalsa consola;           // la consola (parece cmd) que "ejecuta NO ESCAPE" entre "Run" y NO ESCAPE
    double errorVel = 1;            // ERROR: los 500 DVDs van 6 veces más rápido
    IntPtr ganchoRaton = IntPtr.Zero;
    LowLevelMouseProc procRaton;   // se guarda para que no lo borre el recolector de basura
    bool tragarSoltar;
    double ultimoDuplicado = -1;
    const int MaxDuplicados = 1000;

    void PonerGancho(bool poner)
    {
        if (poner && ganchoRaton == IntPtr.Zero)
        {
            procRaton = AlClicDelRaton;
            ganchoRaton = SetWindowsHookEx(14 /*WH_MOUSE_LL*/, procRaton, GetModuleHandle(null), 0);
        }
        else if (!poner && ganchoRaton != IntPtr.Zero)
        {
            UnhookWindowsHookEx(ganchoRaton);
            ganchoRaton = IntPtr.Zero;
            tragarSoltar = false;
        }
    }

    IntPtr AlClicDelRaton(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && toca && !oculto && !protectorEncendido)
            {
                int msg = wParam.ToInt32();
                if (msg == 0x0201 /*WM_LBUTTONDOWN*/)
                {
                    MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    float w = W, h = H;
                    for (int i = logos.Count - 1; i >= 0; i--)   // el de más arriba primero
                    {
                        Logo L = logos[i];
                        if (m.X >= L.X && m.X <= L.X + w && m.Y >= L.Y && m.Y <= L.Y + h)
                        {
                            // un clic = un solo duplicado (aunque Windows mande el aviso repetido)
                            double t = clock.Elapsed.TotalSeconds;
                            if (!tragarSoltar && t - ultimoDuplicado > 0.15) { ultimoDuplicado = t; Duplicar(L); }
                            tragarSoltar = true;
                            return (IntPtr)1;   // el clic se lo queda el DVD
                        }
                    }
                }
                else if (msg == 0x0202 /*WM_LBUTTONUP*/ && tragarSoltar)
                {
                    tragarSoltar = false;
                    return (IntPtr)1;
                }
            }
        }
        catch { }
        return CallNextHookEx(ganchoRaton, nCode, wParam, lParam);
    }

    // Al hacer clic en un DVD se duplican TODOS (si hay 10, quedan 20). Cada uno se parte en dos
    // y las dos mitades salen disparadas hacia lados distintos.
    void Duplicar(Logo tocado)
    {
        double now = clock.Elapsed.TotalSeconds;
        int tope = Math.Min(DvdConfig.MaxCantidad, MaxDuplicados);
        if (logos.Count >= tope)
        {
            cheatMsg = "¡YA HAY DEMASIADOS DVDs! (" + logos.Count + ")";
            cheatMsgStart = now; cheatMsgUntil = now + 2;
            return;
        }
        int antes = logos.Count;
        int cuantos = Math.Min(antes, tope - antes);   // si no caben todos, se duplican los que quepan
        for (int i = 0; i < cuantos; i++) PartirEnDos(logos[i], now);
        extra += cuantos;   // así no se borran al revisar cuántos DVDs debe haber
        cheatMsg = "¡DUPLICADOS! " + antes + " → " + logos.Count + " DVDs";
        cheatMsgStart = now; cheatMsgUntil = now + 1.5;
        if (cfg.Sonido) System.Media.SystemSounds.Asterisk.Play();
    }

    void PartirEnDos(Logo L, double now)
    {
        Logo n = new Logo();
        n.X = L.X; n.Y = L.Y; n.Spin = L.Spin;
        // se esparcen: cada uno sale disparado hacia un lado distinto (casi contrarios) y muy rápido,
        // y en 1.2 s se calman a la velocidad normal
        double ang = rnd.NextDouble() * Math.PI * 2, abre = (rnd.NextDouble() - 0.5) * 0.9;
        L.Vx = (float)Math.Cos(ang); L.Vy = (float)Math.Sin(ang);
        n.Vx = (float)Math.Cos(ang + Math.PI + abre); n.Vy = (float)Math.Sin(ang + Math.PI + abre);
        L.EmpujonHasta = n.EmpujonHasta = now + 1.2;
        L.SoloActive = false;
        n.Color = L.Color;
        if (cfg.CambiarColor) { NewColor(n); NewColor(L); }
        logos.Add(n);
    }

    // ---- MODO CHOQUE: los DVDs rebotan entre ellos ----
    // Si dos se tocan, se separan y cambian la dirección en el eje donde chocaron (como dos bolas iguales).
    // Con muchos DVDs se apaga solo (más de 60) para que no vaya lento.
    void Chocar()
    {
        int n = logos.Count;
        if (n < 2 || n > 60) return;
        float w = W, h = H;
        for (int i = 0; i < n; i++)
        {
            Logo a = logos[i];
            for (int j = i + 1; j < n; j++)
            {
                Logo b = logos[j];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float penX = w - Math.Abs(dx), penY = h - Math.Abs(dy);
                if (penX <= 0 || penY <= 0) continue;   // no se tocan
                if (penX < penY)
                {
                    // choque por los lados: se separan en X y se intercambian la velocidad X
                    float sx = dx >= 0 ? 1 : -1;
                    a.X -= sx * penX / 2; b.X += sx * penX / 2;
                    if ((a.Vx - b.Vx) * sx > 0) { float t = a.Vx; a.Vx = b.Vx; b.Vx = t; }
                }
                else
                {
                    // choque por arriba/abajo: se separan en Y y se intercambian la velocidad Y
                    float sy = dy >= 0 ? 1 : -1;
                    a.Y -= sy * penY / 2; b.Y += sy * penY / 2;
                    if ((a.Vy - b.Vy) * sy > 0) { float t = a.Vy; a.Vy = b.Vy; b.Vy = t; }
                }
                if (cfg.CambiarColor) { NewColor(a); NewColor(b); }
            }
        }
    }

    // ---- CHEATS: se activan abriendo los .bat de la carpeta cheats ----
    // Cada .bat manda su cheat por la conexión local "DvdRebote_Cheats".
    // Ya no se leen códigos escritos con el teclado.
    // Los efectos duran mientras el DVD esté abierto (no se guardan en el JSON).
    bool turbo, lento, gigante, enano, arcoiris, congelado, soloCheat;
    bool fiesta, fantasma, caos;   // cheats nuevos
    bool hiper, parpadeo, estelaCheat;
    double easter = 1;   // código secreto EASTEREGG: multiplica la velocidad
    int extra;

    // ---- estadísticas guardadas: con trucos que ayudan a tocar esquinas NO cuentan ----
    readonly EstadisticasDvd hist = new EstadisticasDvd();
    int limpio;                 // esquinas de esta vez sin trucos (para el récord)
    double trampaHasta = -1;    // ESQUINA, REVERSA y TELE: los siguientes 10 s tampoco cuentan
    double nextGuardarHist;
    // ---- logros: 50 / 100 / 500 / 1000 / 2500 / 5000 esquinas y 1 / 2 / 5 / 10 / 24 horas rebotando (en total, sin trucos) ----
    static readonly long[] LogrosEsquinas = DvdConfig.MetasEsquinas;
    static readonly int[] LogrosHoras = DvdConfig.MetasHoras;
    long esquinasLogros = -1; double segundosLogros;   // lo que llevas en total (lo guardado + lo de ahora)
    string especialesLogros = "";                      // logros especiales ganados (doble, racha)
    readonly List<double> rachaEsquinas = new List<double>();   // cuándo fueron las últimas esquinas (para la RACHA)

    void CargarLogros()
    {
        if (esquinasLogros >= 0) return;
        EstadisticasDvd g = EstadisticasDvd.Load();
        esquinasLogros = g.Total; segundosLogros = g.Segundos; especialesLogros = g.Especiales ?? "";
    }

    void AvanzarLogros(double now, int esquinas, double segundos)
    {
        CargarLogros();
        long e0 = esquinasLogros; double s0 = segundosLogros;
        esquinasLogros += esquinas; segundosLogros += segundos;
        for (int i = 0; i < LogrosEsquinas.Length; i++) if (e0 < LogrosEsquinas[i] && esquinasLogros >= LogrosEsquinas[i]) Logro(now, DvdConfig.NombresLogrosEsquinas[i]);
        for (int i = 0; i < LogrosHoras.Length; i++) if (s0 < LogrosHoras[i] * 3600 && segundosLogros >= LogrosHoras[i] * 3600) Logro(now, DvdConfig.NombresLogrosHoras[i]);
        ComprobarTrofeo(DvdConfig.LogrosGanados(e0, s0, especialesLogros), DvdConfig.LogrosGanados(esquinasLogros, segundosLogros, especialesLogros));
    }

    // LOGROS ESPECIALES (k: 0 doble, 1 racha): salen una sola vez y se guardan
    void LogroEspecial(double now, int k)
    {
        CargarLogros();
        string id = DvdConfig.EspecialesId[k];
        if (DvdConfig.TieneEspecial(especialesLogros, id)) return;
        int antes = DvdConfig.LogrosGanados(esquinasLogros, segundosLogros, especialesLogros);
        especialesLogros = string.IsNullOrEmpty(especialesLogros) ? id : especialesLogros + "," + id;
        try { EstadisticasDvd.GanarEspecial(id); } catch { }
        Logro(now, DvdConfig.NombresEspeciales[k]);
        ComprobarTrofeo(antes, DvdConfig.LogrosGanados(esquinasLogros, segundosLogros, especialesLogros));
    }

    // revisa los logros especiales al tocar una esquina sin trucos
    void RevisarEspeciales(Logo L, double now)
    {
        rachaEsquinas.Add(now);
        rachaEsquinas.RemoveAll(t => now - t > 60);
        if (rachaEsquinas.Count >= 3) LogroEspecial(now, 1);                       // RACHA: 3 en menos de 1 minuto
        foreach (Logo o in logos)
            if (o != L && now - o.LastCorner < 1.0) { LogroEspecial(now, 0); break; }   // ESQUINA DOBLE
    }

    // TROFEOS: bronce (1 logro), plata (4), oro (7) y diamante (todos: 14). Sale el más alto que ganaste.
    void ComprobarTrofeo(int antes, int ahora)
    {
        for (int t = DvdConfig.LogrosParaTrofeo.Length - 1; t >= 0; t--)
            if (antes < DvdConfig.LogrosParaTrofeo[t] && ahora >= DvdConfig.LogrosParaTrofeo[t])
            {
                // (el aviso del trofeo sale un momento después del aviso del logro)
                int n = t + 1;
                System.Windows.Forms.Timer espera = new System.Windows.Forms.Timer();
                espera.Interval = 5400;
                espera.Tick += delegate { espera.Stop(); espera.Dispose(); Logro(clock.Elapsed.TotalSeconds, DvdConfig.NombresTrofeos[n - 1], n); };
                espera.Start();
                break;
            }
    }

    void Logro(double now, string puntosYNombre) { Logro(now, puntosYNombre, 0); }
    void Logro(double now, string puntosYNombre, int trofeo)
    {
        // ¿cuántos trofeos tienes? -> el círculo sale estilo consola con un pedazo prendido por trofeo
        int trofeos = 0;
        try
        {
            CargarLogros();
            int ganados = DvdConfig.LogrosGanados(esquinasLogros, segundosLogros, especialesLogros);
            foreach (int n in DvdConfig.LogrosParaTrofeo) if (ganados >= n) trofeos++;
        }
        catch { }
        try { ToastLogro.Mostrar(puntosYNombre, Area, trofeo, trofeos); } catch { }
        if (cfg.Sonido) System.Media.SystemSounds.Asterisk.Play();
    }

    bool TrucoActivo(double now)
    {
        return turbo || hiper || fiesta || caos || soloCheat || toca || cfg.ModoSoloEsquinas
            || easter > 1 || errorVel > 1 || extra > 0 || now < trampaHasta;
    }

    string cheatMsg = "";
    double cheatMsgStart = -1, cheatMsgUntil = -1;

    // Valores efectivos (configuración + cheats)
    double Vel { get { return cfg.Velocidad * (turbo ? 2 : 1) * (lento ? 0.5 : 1) * (hiper ? 4 : 1) * easter * errorVel; } }
    bool Paused { get { return cfg.Pausado || congelado || pausaPantallazo || PausadoPorPantallaCompleta; } }

    // ---- juegos y videos en pantalla completa (configurable: nada / esconder / esconder y pausar / pausar) ----
    // dónde rebota el DVD: la pantalla que elegiste en la configuración (o todas juntas)
    Rectangle Area { get { return DvdConfig.AreaPantalla(cfg.Pantalla); } }

    bool pantallaCompleta;      // hay algo en pantalla completa en la pantalla del DVD
    double nextPantallaCompleta;
    bool EscondidoPorPantallaCompleta { get { return pantallaCompleta && (cfg.PantallaCompleta == "esconder" || cfg.PantallaCompleta == "esconder_pausar"); } }
    bool PausadoPorPantallaCompleta { get { return pantallaCompleta && (cfg.PantallaCompleta == "pausar" || cfg.PantallaCompleta == "esconder_pausar"); } }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")] static extern bool GetWindowRectPC(IntPtr h, out RECTPC r);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int estado);
    [StructLayout(LayoutKind.Sequential)] struct RECTPC { public int L, T, R, B; }

    // ¿la ventana de enfrente (de otro programa) cubre toda la pantalla principal? (juego, video, F11...)
    static bool HayPantallaCompleta(Rectangle area)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != IntPtr.Zero)
        {
            uint pid; GetWindowThreadProcessId(fg, out pid);
            if (pid != (uint)Process.GetCurrentProcess().Id && IsWindowVisible(fg) && !IsIconic(fg))
            {
                System.Text.StringBuilder c = new System.Text.StringBuilder(64);
                GetClassName(fg, c, 64);
                string clase = c.ToString();
                // el escritorio y la barra de tareas no cuentan
                if (clase != "Progman" && clase != "WorkerW" && clase != "Shell_TrayWnd" && clase != "Shell_SecondaryTrayWnd")
                {
                    // cubre toda la pantalla donde está, y esa pantalla es (o es parte de) donde rebota el DVD
                    RECTPC r; GetWindowRectPC(fg, out r);
                    Rectangle p = Screen.FromHandle(fg).Bounds;
                    if (p.IntersectsWith(area) && r.L <= p.Left && r.T <= p.Top && r.R >= p.Right && r.B >= p.Bottom) return true;
                }
            }
        }
        // juegos de pantalla completa "exclusiva" (Direct3D) y modo presentación
        int estado;
        try { if (SHQueryUserNotificationState(out estado) == 0 && (estado == 3 || estado == 4)) return true; } catch { }
        return false;
    }
    // pantallazo azul: primero se pausan los DVDs, al segundo sale la pantalla azul y se esconden
    bool pausaPantallazo, ocultoPorPantallazo;
    System.Windows.Forms.Timer esperaPantallazo;
    // el .bat (o cerrar el DVD) cancela todo lo del pantallazo: la espera, la pausa y la pantalla azul
    void CancelarPantallazo()
    {
        if (esperaPantallazo != null) { esperaPantallazo.Stop(); esperaPantallazo.Dispose(); esperaPantallazo = null; }
        if (pantallaAzul != null && !pantallaAzul.IsDisposed) pantallaAzul.Quitar();
        QuitarCongelada();
        pausaPantallazo = false; ocultoPorPantallazo = false;
    }
    PantallaCongelada congelada;
    void QuitarCongelada()
    {
        if (congelada != null && !congelada.IsDisposed) congelada.Quitar();
        congelada = null;
    }
    int Count { get { return (int)Math.Min(DvdConfig.MaxCantidad, (long)cfg.Cantidad + extra); } }
    // Con el JSON hace falta el .bat; el cheat SOLO funciona siempre (lo invierte esté o no el .bat)
    // Si los DVDs chocan con las ventanas (casilla o código REBOTANTE) el modo solo esquinas se pausa:
    // su recorrido exacto de esquina a esquina no puede chocar con nada.
    bool Solo { get { return ((cfg.ModoSoloEsquinas && batInstalado) ^ soloCheat) && !ChocarConVentanas; } }

    // Cheats que llegan desde los .bat (conexión local "DvdRebote_Cheats").
    // Un .bat los manda así:  echo TURBO>\\.\pipe\DvdRebote_Cheats
    readonly System.Collections.Concurrent.ConcurrentQueue<string> cheatsRemotos = new System.Collections.Concurrent.ConcurrentQueue<string>();
    volatile bool escuchandoCheats;

    void StartCheatPipe()
    {
        escuchandoCheats = true;
        Thread t = new Thread(delegate ()
        {
            while (escuchandoCheats)
            {
                try
                {
                    using (System.IO.Pipes.NamedPipeServerStream pipe =
                        new System.IO.Pipes.NamedPipeServerStream("DvdRebote_Cheats", System.IO.Pipes.PipeDirection.In))
                    {
                        pipe.WaitForConnection();
                        using (StreamReader r = new StreamReader(pipe))
                        {
                            string line = r.ReadLine();
                            if (!string.IsNullOrEmpty(line) && line.Length <= 32) cheatsRemotos.Enqueue(line.Trim().ToUpperInvariant());
                        }
                    }
                }
                catch { Thread.Sleep(200); }
            }
        });
        t.IsBackground = true;
        t.Name = "Cheats .bat";
        t.Start();
    }

    // Devuelve true si hay que cerrar (cheat STOP)
    bool CheckCheats(double now)
    {
        string remoto;
        while (cheatsRemotos.TryDequeue(out remoto))
        {
            remoto = remoto.Replace("Í", "I").Replace(" ", "");
            if (remoto == "STOP") return true;
            ApplyCheat(remoto, now);
        }
        return false;
    }

    // Código secreto: "easteregg" = velocidad x10; "easteregg x4" = x10 por 4 (x40), etc.
    // "easteregg" solo, estando activo, lo quita. (se escribe en la casilla secreta de dvd_config)
    const double EasterBase = 10;
    string EasterEgg(string resto)
    {
        resto = resto.Replace("X", "").Replace("*", "");
        if (resto.Length == 0)
        {
            if (easter > 1) { easter = 1; return "EASTER EGG DESACTIVADO"; }
            easter = EasterBase;
            return "EASTER EGG: VELOCIDAD x" + EasterBase;
        }
        int n;
        if (!int.TryParse(resto, out n) || n < 1) return null;
        n = Math.Min(n, 100);
        easter = EasterBase * n;
        return "EASTER EGG: VELOCIDAD x" + easter;
    }

    void ApplyCheat(string c, double now)
    {
        string msg;
        if (c.StartsWith("EASTEREGG"))
        {
            msg = EasterEgg(c.Substring(9));
            if (msg == null) return;
            cheatMsg = msg;
            cheatMsgStart = now; cheatMsgUntil = now + 2.5;
            if (cfg.Sonido) System.Media.SystemSounds.Exclamation.Play();
            return;
        }
        switch (c)
        {
            case "ESQUINA": AimAtCorner(); trampaHasta = now + 10; msg = "TODOS A LAS ESQUINAS"; break;
            // probar_logro.bat: solo ENSEÑA cómo se ve un logro (no cuenta nada ni cambia las estadísticas)
            case "PROBARLOGRO": Logro(now, "10G: Logro de prueba"); return;
            case "PROBARPLATINO": Logro(now, DvdConfig.NombresTrofeos[2], 3); return;   // (el de oro)
            case "PROBARBRONCE": Logro(now, DvdConfig.NombresTrofeos[0], 1); return;
            case "PROBARPLATA": Logro(now, DvdConfig.NombresTrofeos[1], 2); return;
            case "PROBARDIAMANTE": Logro(now, DvdConfig.NombresTrofeos[3], 4); return;            case "TURBO": turbo = !turbo; if (turbo) { lento = false; hiper = false; } msg = "TURBO " + OnOff(turbo); break;
            case "LENTO": lento = !lento; if (lento) { turbo = false; hiper = false; } msg = "LENTO " + OnOff(lento); break;
            case "HIPER": hiper = !hiper; if (hiper) { turbo = false; lento = false; } msg = "HÍPER VELOCIDAD x4 " + OnOff(hiper); break;
            case "REVERSA":
                // todos dan la vuelta
                foreach (Logo L in logos) { L.Vx = -L.Vx; L.Vy = -L.Vy; L.SoloActive = false; }
                trampaHasta = now + 10;
                msg = "REVERSA: TODOS DAN LA VUELTA"; break;
            case "TELETRANSPORTE":
            case "TELE":
            {
                trampaHasta = now + 10;
                // cada DVD aparece en un lugar al azar
                Rectangle s = Area;   // la pantalla elegida (o todas)
                float maxX = Math.Max(s.Left, s.Right - W), maxY = Math.Max(s.Top, s.Bottom - H);
                foreach (Logo L in logos)
                {
                    L.X = s.Left + (float)rnd.NextDouble() * (maxX - s.Left);
                    L.Y = s.Top + (float)rnd.NextDouble() * (maxY - s.Top);
                    L.SoloActive = false; L.HCount = 0;
                }
                msg = "TELETRANSPORTE"; break;
            }
            case "REBOTANTE":
                // código secreto: los DVDs chocan con las ventanas y las empujan
                rebotante = !rebotante;
                if (rebotante) { AchicarVentanasGrandes(); nextVentanas = 0; }
                else RestaurarVentanas();
                msg = "REBOTANTE: LAS VENTANAS SE MUEVEN " + OnOff(rebotante); break;
            case "TROLL": troll = !troll; msg = troll ? "TROLL ACTIVADO" : "TROLL DESACTIVADO"; break;
            case "SECRET":
                // código secreto SECRET: sale una ventana de error con estática de tele vieja ("No escape");
                // si le das "Aceptar", aparecen 500 DVDs rebotando muy rápido
                if (!DvdConfig.RevisarNetParaSecret()) return;   // pide .NET Framework 4.8.1 si hace falta
                if (ventanaError == null || ventanaError.IsDisposed)
                {
                    // primero el cuadro de Windows "¿Quieres ejecutar esto?" con estática (Sí o No: sigue igual)
                    if (!Program.PreguntarAdministrador()) return;   // la cerró el .bat: no sale NO ESCAPE
                    if (consola != null) return;
                    Action abrirNoEscape = delegate
                    {
                    ventanaError = new VentanaEstatica();
                    RegistroSecret.Anotar("NO ESCAPE", "Se abrió la ventana NO ESCAPE (código secret)");
                    TrayNoEscape(true);   // junto al reloj: icono rojo "NO ESCAPE"
                    ventanaError.FormClosed += delegate
                    {
                        try { TrayNoEscape(false); } catch { }
                        if (ventanaError.DialogResult == DialogResult.OK)
                        {
                            extra += 500; errorVel = 6; SyncLogoCount();   // sin aviso: aparecen de golpe
                            RegistroSecret.Anotar("ACEPTAR", "Le diste Aceptar: salieron 500 DVDs");
                            // 2 s después se PAUSAN los DVDs; 1 s más y sale la pantalla azul falsa con los DVDs
                            // escondidos. Cuando se quita (sola, con el .bat o al cerrar), vuelven y siguen rebotando.
                            System.Windows.Forms.Timer espera = new System.Windows.Forms.Timer { Interval = 2000 };
                            esperaPantallazo = espera;
                            int paso = 0;
                            espera.Tick += delegate
                            {
                                paso++;
                                if (paso == 1)
                                {
                                    // la pantalla se "congela" donde estaba: foto de todo encima, sin mouse
                                    pausaPantallazo = true;
                                    try { congelada = new PantallaCongelada(); congelada.Show(); } catch { congelada = null; }
                                    espera.Interval = 1000; return;
                                }
                                espera.Stop(); espera.Dispose(); esperaPantallazo = null;
                                if (pantallaAzul != null && !pantallaAzul.IsDisposed) return;
                                ocultoPorPantallazo = true;
                                RegistroSecret.Anotar("PANTALLAZO", "Salió el pantallazo azul (código secret)");
                                pantallaAzul = new PantallaAzul();
                                pantallaAzul.FormClosed += delegate { pantallaAzul = null; ocultoPorPantallazo = false; pausaPantallazo = false; QuitarCongelada(); };
                                pantallaAzul.Show();
                                QuitarCongelada();   // la pantalla azul ya tapa todo
                            };
                            espera.Start();
                        }
                        ventanaError = null;
                    };
                    ventanaError.Show();
                    ventanaError.Activate();
                    };
                    // entre "¿Quieres ejecutar esto?" y NO ESCAPE: una consola (parece cmd) que "ejecuta NO ESCAPE";
                    // cuando termina se cierra solo y sale NO ESCAPE
                    consola = new ConsolaFalsa();
                    consola.FormClosed += delegate
                    {
                        bool completa = consola != null && consola.Completa;
                        consola = null;
                        if (completa && (ventanaError == null || ventanaError.IsDisposed)) abrirNoEscape();
                    };
                    consola.Show();
                    consola.Activate();
                }
                return;
            case "CERRARERRORES":
                // lo manda el .bat "Cerrar ventanas de error": quita la ventana "No escape" y todas las
                // ventanitas de Error sin soltar los 500 DVDs (el DVD sigue rebotando)
                Program.CerrarPregunta();   // también la de "¿Quieres ejecutar esto?"
                if (consola != null) consola.Cancelar();   // y la consola (sin que salga NO ESCAPE)
                if (ventanaError != null && !ventanaError.IsDisposed) ventanaError.CerrarSinPremio();
                CancelarPantallazo();   // y la pantalla azul falsa (o la pausa de antes)
                // y el sonido de los programas regresa sí o sí (aunque el pantallazo ya no esté o se haya quedado a medias)
                try { SilencioSistema.ArreglarSiQuedo(); } catch { }
                try { SilencioSistema.Restaurar(); } catch { }
                return;
            case "TOCA":
                toca = !toca;
                PonerGancho(toca);
                msg = toca ? "TOCA ACTIVADO: haz clic en un DVD y se duplican todos" : "TOCA DESACTIVADO"; break;
            case "PARPADEO": parpadeo = !parpadeo; msg = "PARPADEO " + OnOff(parpadeo); break;
            case "ESTELA": estelaCheat = !estelaCheat; msg = "ESTELA " + OnOff(estelaCheat); break;
            case "DOBLE":
            {
                int mas = Math.Min(Count, 500); // como mucho +500 de golpe para que no se trabe
                extra += mas; SyncLogoCount();
                msg = "DOBLE: +" + mas + " DVDs (" + logos.Count + ")"; break;
            }
            case "GIGANTE": gigante = !gigante; if (gigante) enano = false; msg = "GIGANTE " + OnOff(gigante); break;
            case "ENANO": enano = !enano; if (enano) gigante = false; msg = "ENANO " + OnOff(enano); break;
            case "ARCOIRIS": arcoiris = !arcoiris; msg = "ARCOÍRIS " + OnOff(arcoiris); break;
            case "CONGELAR": congelado = !congelado; msg = congelado ? "CONGELADO" : "DESCONGELADO"; break;
            case "MASDVD": extra += 10; SyncLogoCount(); msg = "+10 DVDs (" + logos.Count + ")"; break;
            case "SOLO": soloCheat = !soloCheat; msg = "SOLO ESQUINAS " + OnOff(Solo); break;
            case "FIESTA":
                // arcoíris + turbo + 10 DVDs más; al quitarlo se deshace
                fiesta = !fiesta;
                if (fiesta) { arcoiris = true; turbo = true; lento = false; extra += 10; }
                else { arcoiris = false; turbo = false; extra = Math.Max(0, extra - 10); }
                SyncLogoCount();
                msg = "FIESTA " + OnOff(fiesta); break;
            case "FANTASMA": fantasma = !fantasma; msg = "FANTASMA " + OnOff(fantasma); break;
            case "CAOS": caos = !caos; msg = "CAOS " + OnOff(caos); break;
            case "RESET":
                stats = new DvdStats(); limpio = 0;
                turbo = lento = gigante = enano = arcoiris = congelado = soloCheat = false;
                fiesta = fantasma = caos = false;
                hiper = parpadeo = estelaCheat = false;
                easter = 1;
                if (rebotante) { rebotante = false; RestaurarVentanas(); }
                toca = false; PonerGancho(false); troll = false; errorVel = 1;
                extra = 0; SyncLogoCount();
                msg = "CONTADOR Y CHEATS REINICIADOS"; break;
            default: return;
        }
        cheatMsg = "CHEAT: " + msg;
        cheatMsgStart = now; cheatMsgUntil = now + 2.5;
        if (cfg.Sonido) System.Media.SystemSounds.Exclamation.Play();
    }

    static string OnOff(bool b) { return b ? "ACTIVADO" : "DESACTIVADO"; }

    // Aviso del cheat arriba al centro
    void DrawCheatMsg(Graphics g, double now)
    {
        if (!(now < cheatMsgUntil)) return;
        float t = (float)((now - cheatMsgStart) / (cheatMsgUntil - cheatMsgStart));
        int a = (int)(255 * Math.Min(1f, (1f - t) * 3f));
        using (GraphicsPath p = new GraphicsPath())
        {
            using (FontFamily ff = new FontFamily("Arial Black"))
                p.AddString(cheatMsg, ff, (int)FontStyle.Bold, 30f, new RectangleF(0, 60, win.SW, 60), Centered());
            using (Pen gp = new Pen(Color.FromArgb(90 * a / 255, 255, 200, 0), 12)) { gp.LineJoin = LineJoin.Round; g.DrawPath(gp, p); }
            using (Pen o = new Pen(Color.FromArgb(220 * a / 255, 0, 0, 0), 4)) { o.LineJoin = LineJoin.Round; g.DrawPath(o, p); }
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(a, 255, 230, 80))) g.FillPath(fb, p);
        }
    }

    void Move(Logo L, double now, float dt, Rectangle s)
    {
        float minX = s.Left, minY = s.Top;
        float maxX = Math.Max(minX, s.Right - W), maxY = Math.Max(minY, s.Bottom - H);

        if (Solo) { MoveSolo(L, now, dt, minX, minY, maxX, maxY); return; }
        L.SoloActive = false;
        if (Paused) return;

        // cheat CAOS: cada DVD cambia de dirección al azar cada poco tiempo
        if (caos && now >= L.NextCaos)
        {
            L.NextCaos = now + 0.4 + rnd.NextDouble() * 0.6;
            double ang = rnd.NextDouble() * Math.PI * 2;
            L.Vx = (float)Math.Cos(ang); L.Vy = (float)Math.Sin(ang);
        }

        float px = L.X, py = L.Y;
        // recién duplicado (código TOCA): hasta 5 veces más rápido y se va calmando
        float empujon = 1f;
        if (L.EmpujonHasta > now) { float t = (float)((L.EmpujonHasta - now) / 1.2); empujon = 1f + 4f * t * t; }
        L.X += L.Vx * (float)Vel * empujon * dt;
        L.Y += L.Vy * (float)Vel * empujon * dt;
        L.Spin = (L.Spin + 120f * dt) % 360f;
        if (ChocarConVentanas && ventanas.Count > 0) ChocarVentanas(L, px, py);

        bool hitX = false, hitY = false;
        string h = "", v = "";
        if (L.X <= minX) { L.X = minX; L.Vx = Math.Abs(L.Vx); hitX = true; h = "izquierda"; }
        else if (L.X >= maxX) { L.X = maxX; L.Vx = -Math.Abs(L.Vx); hitX = true; h = "derecha"; }
        if (L.Y <= minY) { L.Y = minY; L.Vy = Math.Abs(L.Vy); hitY = true; v = "Superior"; }
        else if (L.Y >= maxY) { L.Y = maxY; L.Vy = -Math.Abs(L.Vy); hitY = true; v = "Inferior"; }
        if (!hitX && !hitY) return;

        if (cfg.CambiarColor) NewColor(L);
        bool paredIzqOArriba = hitX ? h == "izquierda" : v == "Superior";   // dónde pegó (para las chispas de la skin)
        bool paredEsX = hitX;
        // Es esquina si al tocar una pared está a menos de N píxeles de la otra
        float tol = cfg.ToleranciaEsquinaPx;
        bool corner = hitX && hitY;
        if (!corner && hitX)
        {
            if (L.Y - minY <= tol) { corner = true; v = "Superior"; }
            else if (maxY - L.Y <= tol) { corner = true; v = "Inferior"; }
        }
        else if (!corner && hitY)
        {
            if (L.X - minX <= tol) { corner = true; h = "izquierda"; }
            else if (maxX - L.X <= tol) { corner = true; h = "derecha"; }
        }
        if (corner && now - L.LastCorner > 0.4) // no contar dos veces la misma esquina
        {
            L.LastCorner = now;
            RegisterCorner(L, v, h, now);
        }
        else if (!corner) ChispasPared(L, now, paredEsX, paredIzqOArriba);
    }

    // ---- Modo solo esquinas: rebota DE VERDAD (línea recta, choca con las paredes) ----
    // pero con el ángulo justo para que cada recorrido termine en una esquina.
    // Si en el tiempo que cruza m anchos de pantalla cruza n altos (m y n sin divisores
    // en común), llega exactamente a una esquina tras m-1 + n-1 rebotes en las paredes,
    // sin tocar otra esquina antes. La posición se calcula exacta ("desdoblando" los
    // rebotes), así no se desvía nunca. Todo en fracciones de pantalla.
    static readonly int[,] Pares = { {1,1},{1,2},{2,1},{2,3},{3,2},{1,3},{3,1},{3,4},{4,3},{2,5},{5,2},{3,5},{5,3} };

    void MoveSolo(Logo L, double now, float dt, float minX, float minY, float maxX, float maxY)
    {
        double rx = Math.Max(1.0, maxX - minX), ry = Math.Max(1.0, maxY - minY);
        if (!L.SoloActive)
        {
            L.SoloActive = true;
            NewPath(L, Clamp01((L.X - minX) / rx), Clamp01((L.Y - minY) / ry), L.Vx >= 0 ? 1 : -1, L.Vy >= 0 ? 1 : -1);
        }
        if (Paused) return;
        L.Spin = (L.Spin + 120f * dt) % 360f;

        double du = L.A - L.U0, dv = L.B - L.V0;
        double len = Math.Sqrt(du * rx * du * rx + dv * ry * dv * ry);
        L.T += Vel * dt / Math.Max(1.0, len);

        if (L.T >= 1.0)
        {
            // llegó a la esquina
            double cx = Fold(L.A), cy = Fold(L.B);
            L.X = (float)(minX + cx * rx); L.Y = (float)(minY + cy * ry);
            if (cfg.CambiarColor) NewColor(L);
            RegisterCorner(L, cy < 0.5 ? "Superior" : "Inferior", cx < 0.5 ? "izquierda" : "derecha", now);
            NewPath(L, cx, cy, cx < 0.5 ? 1 : -1, cy < 0.5 ? 1 : -1);
            return;
        }

        double u = L.U0 + du * L.T, v = L.V0 + dv * L.T;
        L.X = (float)(minX + Fold(u) * rx);
        L.Y = (float)(minY + Fold(v) * ry);

        // rebote en una pared: cambia de color como en el modo normal
        int segX = Seg(u, du), segY = Seg(v, dv);
        if (segX != L.SegX || segY != L.SegY)
        {
            bool enX = segX != L.SegX;
            L.SegX = segX; L.SegY = segY;
            if (cfg.CambiarColor) NewColor(L);
            if (enX) ChispasPared(L, now, true, L.X - minX < maxX - L.X);
            else ChispasPared(L, now, false, L.Y - minY < maxY - L.Y);
        }

        // dirección actual (si se apaga el modo, sigue rebotando desde aquí)
        double vx = Slope(u) * du * rx, vy = Slope(v) * dv * ry;
        double d = Math.Sqrt(vx * vx + vy * vy);
        if (d > 1e-9) { L.Vx = (float)(vx / d); L.Vy = (float)(vy / d); }
    }

    // Nuevo recorrido desde (fx, fy) hacia (sx, sy) que termina en una esquina
    void NewPath(Logo L, double fx, double fy, int sx, int sy)
    {
        int k = rnd.Next(Pares.GetLength(0));
        int m = Pares[k, 0], n = Pares[k, 1];
        L.U0 = fx; L.V0 = fy;
        L.A = sx > 0 ? (int)Math.Floor(fx) + m : (int)Math.Ceiling(fx) - m;
        L.B = sy > 0 ? (int)Math.Floor(fy) + n : (int)Math.Ceiling(fy) - n;
        L.T = 0;
        L.SegX = Seg(fx, L.A - fx);
        L.SegY = Seg(fy, L.B - fy);
    }

    // "Doblar" una coordenada desdoblada al rango 0..1 (cada entero es una pared)
    static double Fold(double u) { double m = u % 2.0; if (m < 0) m += 2.0; return m <= 1.0 ? m : 2.0 - m; }
    static double Slope(double u) { double m = u % 2.0; if (m < 0) m += 2.0; return m < 1.0 ? 1.0 : -1.0; }
    static int Seg(double u, double d) { return (int)Math.Floor(u + (d >= 0 ? 1e-6 : -1e-6)); }
    static double Clamp01(double v) { return Math.Max(0.0, Math.Min(1.0, v)); }

    void NewColor(Logo L)
    {
        Color c;
        int tries = 0;
        do { c = palette[rnd.Next(palette.Length)]; } while (c.ToArgb() == L.Color.ToArgb() && ++tries < 10);
        L.Color = c;
    }

    void RegisterCorner(Logo L, string v, string h, double now)
    {
        stats.Total++;
        if (v == "Superior" && h == "izquierda") stats.SupIzq++;
        else if (v == "Superior") stats.SupDer++;
        else if (h == "izquierda") stats.InfIzq++;
        else stats.InfDer++;
        stats.Ultima = v + " " + h;
        lastV = v; lastH = h;
        L.GlowStart = now; L.GlowUntil = now + 2.0;
        msgStart = now; msgUntil = now + 2.0; msgColor = L.Color;
        if (!trayNoEscape) tray.Text = "DVD Rebote - esquinas: " + stats.Total;
        // récord y estadísticas guardadas: solo sin trucos
        if (!TrucoActivo(now) && !oculto && !protectorEncendido && !EscondidoPorPantallaCompleta) { limpio++; hist.Esquina(v, h, limpio); AvanzarLogros(now, 1, 0); RevisarEspeciales(L, now); }
        if (oculto || protector || protectorEncendido || EscondidoPorPantallaCompleta) return; // escondido o con protector: ni sonido ni vibración
        ExplosionEsquina(L, v, h, now);   // con skin: explosión del metal en la esquina
        if (cfg.Sonido && now - lastSound > 0.15)
        {
            lastSound = now;
            // con skin (y sin sonido propio elegido) suena el metal de la skin
            string skinSon = UsaImagen ? null : SkinActiva;
            if (cfg.SonidoSkin && string.IsNullOrEmpty(cfg.SonidoArchivo) && skinSon != null) SonidoSkin.Tocar(skinSon);
            else SonidoEsquina.Tocar(cfg.SonidoArchivo);
        }
        // control Xbox: vibra lo mismo que dura el brillo (2 s); cada esquina nueva alarga la vibración
        if (cfg.VibrarControl && now - lastRumble > 0.15) { lastRumble = now; Mando.Vibrar(now, 2.0); }
    }

    public void AimAtCorner()
    {
        Rectangle s = Area;   // la pantalla elegida (o todas)
        float maxX = Math.Max(s.Left, s.Right - W), maxY = Math.Max(s.Top, s.Bottom - H);
        foreach (Logo L in logos)
        {
            int c = rnd.Next(4);
            float cx = (c % 2 == 0) ? s.Left : maxX, cy = (c < 2) ? s.Top : maxY;
            float sx = (c % 2 == 0) ? -1 : 1, sy = (c < 2) ? -1 : 1;
            float d = Math.Min(maxX - s.Left, maxY - s.Top) * (0.3f + (float)rnd.NextDouble() * 0.6f);
            L.X = cx - sx * d; L.Y = cy - sy * d;
            L.Vx = sx * 0.7071f; L.Vy = sy * 0.7071f;
        }
    }

    // ---------------- dibujo ----------------

    // Reloj para los colores: mientras los DVDs están pausados (pantallazo, CONGELAR o pausa) se detiene,
    // así el arcoíris de cuando tocan esquina (y el brillo, PARPADEO y la estela) se quedan quietos.
    // Al despausar se recorre el brillo de cada DVD lo que duró la pausa: sigue donde se quedó.
    double relojCongelado = -1;
    double RelojDibujo(double now)
    {
        if (Paused)
        {
            if (relojCongelado < 0) relojCongelado = now;
            return relojCongelado;
        }
        if (relojCongelado >= 0)
        {
            double pausa = now - relojCongelado;
            foreach (Logo L in logos)
                if (L.GlowUntil > relojCongelado) { L.GlowStart += pausa; L.GlowUntil += pausa; }
            foreach (Chispa c in chispas) c.Nace += pausa;   // las chispas de las skins también siguen donde se quedaron
            relojCongelado = -1;
        }
        return now;
    }

    void Render(double now, Rectangle s)
    {
        // escondido con Ctrl+Alt+D o mientras está el protector de pantalla (siguen moviéndose)
        now = RelojDibujo(now);
        bool fondoAnimado = cfg.FondoAnimado && !protector;
        if (oculto || protectorEncendido || ocultoPorPantallazo || (EscondidoPorPantallaCompleta && !fondoAnimado))
        { if (win.Visible) win.Hide(); FondoAnimado.Quitar(); return; }
        win.EnsureSurface(s.Width, s.Height);
        CheckCache();
        float op = cfg.Opacidad / 100f * (fantasma ? 0.35f : 1f); // cheat FANTASMA: medio transparentes
        ImageAttributes opAttr = op < 0.999f ? AlphaAttr(op) : null;
        bool disco = cfg.Estilo == "disco";
        int ox = s.Left, oy = s.Top;

        using (Graphics g = Graphics.FromImage(win.Surface))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(protector ? Color.Black : Color.Transparent); // protector: fondo negro como el clásico
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = disco ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < logos.Count; i++)
            {
                Logo L = logos[i];
                float lx = L.X - ox, ly = L.Y - oy;
                // cheat ARCOIRIS: cada DVD va pasando por todos los colores
                // al tocar una esquina se pone arcoíris 2 segundos (lo que dura el brillo)
                bool arco = arcoiris || now < L.GlowUntil;
                Color col = arco ? palette[(int)((now * 8 + i * 3) % palette.Length)] : L.Color;
                string skinAhora = UsaImagen ? null : SkinActiva;
                if (skinAhora != null) col = DvdConfig.ColorSkin(skinAhora);   // con skin: el brillo y el anillo del color del metal
                // cheat PARPADEO: cada DVD aparece y desaparece (cada uno a su ritmo)
                if (parpadeo && ((now * 2.5 + i * 0.37) % 1.0) >= 0.5) { L.HCount = 0; continue; }

                // ESTELA: copias del logo en sus últimas posiciones, cada vez más transparentes
                if ((cfg.Estela || estelaCheat) && logos.Count <= 100)
                {
                    int largo = logos.Count > 25 ? 4 : Logo.MaxEstela; // modo ligero: estela más corta
                    if (now - L.HLast >= 0.035)
                    {
                        L.HLast = now;
                        L.Hx[L.HPos] = L.X; L.Hy[L.HPos] = L.Y;
                        L.HPos = (L.HPos + 1) % Logo.MaxEstela;
                        if (L.HCount < Logo.MaxEstela) L.HCount++;
                    }
                    Bitmap ghost = GetSprite(col);
                    int n = Math.Min(largo, L.HCount);
                    for (int k = n; k >= 1; k--) // de la más vieja a la más nueva
                    {
                        int idx = (L.HPos - k + Logo.MaxEstela * 2) % Logo.MaxEstela;
                        ImageAttributes ta = TrailAttr(k, largo, op);
                        DrawSprite(g, ghost, L.Hx[idx] - ox, L.Hy[idx] - oy, ghost.Width, ghost.Height, disco, L.Spin, ta);
                    }
                }
                else L.HCount = 0;
                // modo ligero (más de 25 DVDs): sin brillo grande ni anillo, que es lo más pesado
                bool glowing = now < L.GlowUntil && logos.Count <= 25;
                if (glowing)
                {
                    float t = (float)((now - L.GlowStart) / (L.GlowUntil - L.GlowStart));
                    float inten = 1f - t;
                    float pulse = (float)(0.6 + 0.4 * Math.Sin((now - L.GlowStart) * 25));
                    // anillo que se expande
                    float r = 30 + t * (Pad + 40) * Scale;
                    using (Pen ring = new Pen(Color.FromArgb((int)(220 * inten * op), col), 5 * Scale))
                        g.DrawEllipse(ring, lx + W / 2 - r - W / 3, ly + H / 2 - r * 0.8f - H / 4, (r + W / 3) * 2, (r * 0.8f + H / 4) * 2);
                    Bitmap halo = GetHalo(col);
                    using (ImageAttributes ha = AlphaAttr(Math.Min(1f, inten * (0.5f + 0.5f * pulse)) * op))
                        DrawSprite(g, halo, lx - Pad * Scale, ly - Pad * Scale, halo.Width, halo.Height, disco, L.Spin, ha);
                }
                Bitmap sp = GetSprite(col);
                DrawSprite(g, sp, lx, ly, sp.Width, sp.Height, disco, L.Spin, opAttr);            }

            DibujarChispas(g, now, ox, oy, op);
            DrawMessage(g, now);
            DrawHud(g, now);
            DrawCheatMsg(g, now);
        }
        if (opAttr != null) opAttr.Dispose();
        if (fondoAnimado && FondoAnimado.Pintar(win.MemDc, s, now, cfg.FondoNegro)) win.Vaciar(s.Left, s.Top);
        else
        {
            if (!fondoAnimado) FondoAnimado.Quitar();
            win.Present(s.Left, s.Top);   // normal (o el fondo animado no se pudo: mejor encima de todo que invisible)
        }
        if (!win.Visible) win.Show();
    }

    static void DrawSprite(Graphics g, Bitmap b, float x, float y, int w, int h, bool rotate, float angle, ImageAttributes attr)
    {
        if (rotate)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x + w / 2f, y + h / 2f);
            g.RotateTransform(angle);
            g.DrawImage(b, new Rectangle(-w / 2, -h / 2, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
            g.Restore(st);
        }
        else
        {
            g.DrawImage(b, new Rectangle((int)x, (int)y, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
        }
    }

    // Transparencias de la estela, guardadas para no crearlas en cada frame
    ImageAttributes[] trailAttrs;
    int trailLargo = -1;
    float trailOp = -1;

    ImageAttributes TrailAttr(int k, int largo, float op)
    {
        if (trailAttrs == null || trailLargo != largo || Math.Abs(trailOp - op) > 0.001f)
        {
            if (trailAttrs != null) foreach (ImageAttributes a in trailAttrs) if (a != null) a.Dispose();
            trailAttrs = new ImageAttributes[largo + 1];
            for (int j = 1; j <= largo; j++)
                trailAttrs[j] = AlphaAttr(op * 0.45f * (1f - (j - 1) / (float)largo)); // la más reciente se ve más
            trailLargo = largo; trailOp = op;
        }
        return trailAttrs[Math.Max(1, Math.Min(largo, k))];
    }

    static ImageAttributes AlphaAttr(float a)
    {
        ColorMatrix m = new ColorMatrix();
        m.Matrix33 = Math.Max(0f, Math.Min(1f, a));
        ImageAttributes ia = new ImageAttributes();
        ia.SetColorMatrix(m);
        return ia;
    }

    // ---- Estilo RELOJ: un cronómetro con el TIEMPO QUE LLEVA REBOTANDO el DVD es lo que rebota, en vez del logo ----
    // (sin contar pausa ni cuando está escondido). Ocupa la misma caja que el logo (220x110) y se redibuja cada segundo.
    double tiempoRebotando;
    string HoraReloj
    {
        get
        {
            long s = (long)tiempoRebotando;
            return s >= 3600 ? (s / 3600) + ":" + (s % 3600 / 60).ToString("00") + ":" + (s % 60).ToString("00")
                             : (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    GraphicsPath RelojForma()
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        using (FontFamily ff = new FontFamily("Segoe UI"))
            p.AddString(HoraReloj, ff, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
        RectangleF lb = p.GetBounds();
        if (lb.Width <= 0 || lb.Height <= 0) return p;
        float k = Math.Min((BaseW - 8) / lb.Width, (BaseH - 8) / lb.Height);   // tan grande como quepa, sin deformar
        using (Matrix m = new Matrix())
        {
            m.Translate((BaseW - lb.Width * k) / 2, (BaseH - lb.Height * k) / 2);
            m.Scale(k, k);
            m.Translate(-lb.X, -lb.Y);
            p.Transform(m);
        }
        return p;
    }

    // ---- EFECTOS DE SKIN: chispas del metal al rebotar y una explosión distinta por metal en las esquinas ----
    //  bronce: lluvia de chispas · plata: ondas de choque · oro: monedas que caen · diamante: astillas de cristal y estrellas.
    // Solo con skin, con 25 DVDs o menos (modo ligero sin efectos) y mientras se ve.
    class Chispa { public float X, Y, Vx, Vy, Tam, Rot, VRot; public double Nace, Vida; public int Tipo; public Color Col; }
    // tipos: 0 chispa (rayita) · 1 moneda · 2 astilla de cristal · 3 estrella · 4 onda (aro) · 5 destello
    readonly List<Chispa> chispas = new List<Chispa>();
    const int MaxChispas = 400;

    string SkinEfectos { get { return UsaImagen || logos.Count > 25 ? null : SkinActiva; } }

    static Color[] ColoresChispa(string skin)
    {
        if (skin == "bronce") return new[] { Color.FromArgb(255, 205, 150), Color.FromArgb(235, 140, 70), Color.FromArgb(255, 238, 205) };
        if (skin == "plata") return new[] { Color.White, Color.FromArgb(205, 215, 230), Color.FromArgb(170, 182, 205) };
        if (skin == "oro") return new[] { Color.FromArgb(255, 238, 150), Color.FromArgb(255, 200, 50), Color.FromArgb(255, 252, 220) };
        if (skin == "neon") return new[] { Color.FromArgb(255, 70, 210), Color.FromArgb(60, 230, 255), Color.FromArgb(255, 220, 250) };
        if (skin == "fuego") return new[] { Color.FromArgb(255, 200, 40), Color.FromArgb(255, 110, 20), Color.FromArgb(255, 245, 190), Color.FromArgb(230, 40, 10) };
        if (skin == "arcoiris") return new[] { Color.FromArgb(255, 70, 70), Color.FromArgb(255, 170, 40), Color.FromArgb(255, 240, 60), Color.FromArgb(90, 230, 110), Color.FromArgb(60, 170, 255), Color.FromArgb(180, 110, 255) };
        if (skin == "holograma") return new[] { Color.FromArgb(200, 255, 240), Color.FromArgb(220, 190, 255), Color.FromArgb(190, 230, 255), Color.White };
        return new[] { Color.FromArgb(205, 248, 255), Color.FromArgb(130, 235, 255), Color.White };
    }

    // qué tipo de chispa suelta cada skin (i = número de chispa; esquina = la explosión grande)
    static int TipoChispa(string skin, int i, bool esquina)
    {
        switch (skin)
        {
            case "bronce": return i % 6 == 0 ? 3 : 0;
            case "plata": return esquina ? (i % 2 == 0 ? 0 : 3) : (i % 4 == 0 ? 3 : 0);
            case "oro": return esquina ? (i % 3 == 0 ? 3 : 1) : (i % 4 == 0 ? 3 : 0);
            case "diamante": return esquina ? (i % 2 == 0 ? 2 : 3) : (i % 3 == 0 ? 3 : 2);
            case "neon": return 0;                      // rayitas de luz
            case "fuego": return i % 7 == 0 ? 3 : 0;    // brasas
            case "arcoiris": return i % 2 == 0 ? 3 : 0; // estrellas y chispas de colores
            default: return i % 2 == 0 ? 2 : 3;         // holograma: astillas y estrellas
        }
    }

    void AgregarChispa(float x, float y, double ang, float vel, double nace, double vida, int tipo, float tam, Color col)
    {
        if (chispas.Count >= MaxChispas) return;
        Chispa c = new Chispa();
        c.X = x; c.Y = y; c.Vx = (float)Math.Cos(ang) * vel; c.Vy = (float)Math.Sin(ang) * vel;
        c.Nace = nace; c.Vida = vida; c.Tipo = tipo; c.Tam = tam; c.Col = col;
        c.Rot = (float)(rnd.NextDouble() * 360); c.VRot = (float)((rnd.NextDouble() - 0.5) * 900);
        chispas.Add(c);
    }

    // rebote en una pared: chispitas que salen de donde pegó + un destello
    void ChispasPared(Logo L, double now, bool paredX, bool izqOArriba)
    {
        string skin = SkinEfectos;
        if (skin == null || oculto) return;
        Color[] cs = ColoresChispa(skin);
        float x, y; double haciaDentro;
        if (paredX) { x = izqOArriba ? L.X : L.X + W; y = L.Y + H / 2; haciaDentro = izqOArriba ? 0 : Math.PI; }
        else { x = L.X + W / 2; y = izqOArriba ? L.Y : L.Y + H; haciaDentro = izqOArriba ? Math.PI / 2 : -Math.PI / 2; }
        for (int i = 0; i < 14; i++)
        {
            int tipo = skin == "bronce" ? 0 : TipoChispa(skin, i, false);
            float tam = (tipo == 2 ? 5 + (float)rnd.NextDouble() * 3 : tipo == 3 ? 5 + (float)rnd.NextDouble() * 4 : 2.2f + (float)rnd.NextDouble() * 1.6f) * Scale;
            AgregarChispa(x, y, haciaDentro + (rnd.NextDouble() - 0.5) * 2.4, (170 + (float)rnd.NextDouble() * 280) * Scale,
                now, 0.35 + rnd.NextDouble() * 0.35, tipo, tam, cs[rnd.Next(cs.Length)]);
        }
        AgregarChispa(x, y, 0, 0, now, 0.28, 5, 34 * Scale, cs[2]);
    }

    // esquina: explosión grande hacia adentro de la pantalla, distinta para cada metal
    void ExplosionEsquina(Logo L, string v, string h, double now)
    {
        string skin = SkinEfectos;
        if (skin == null) return;
        Color[] cs = ColoresChispa(skin);
        bool izq = h == "izquierda", arriba = v == "Superior";
        float x = izq ? L.X : L.X + W, y = arriba ? L.Y : L.Y + H;
        double centro = Math.Atan2(arriba ? 1 : -1, izq ? 1 : -1);
        AgregarChispa(x, y, 0, 0, now, 0.75, 4, 150 * Scale, cs[1]);
        if (skin == "plata") AgregarChispa(x, y, 0, 0, now + 0.15, 0.75, 4, 210 * Scale, cs[0]);   // plata: doble onda
        if (skin == "neon")   // neón: tres ondas de luz de colores, una tras otra
        {
            AgregarChispa(x, y, 0, 0, now + 0.1, 0.7, 4, 200 * Scale, cs[1]);
            AgregarChispa(x, y, 0, 0, now + 0.2, 0.7, 4, 250 * Scale, cs[0]);
        }
        AgregarChispa(x, y, 0, 0, now, 0.45, 5, 95 * Scale, cs[2]);
        int n = skin == "oro" ? 30 : skin == "fuego" ? 50 : 40;
        for (int i = 0; i < n; i++)
        {
            int tipo = TipoChispa(skin, i, true);
            float tam = (tipo == 1 ? 7 + (float)rnd.NextDouble() * 4 : tipo == 2 ? 6 + (float)rnd.NextDouble() * 5 : tipo == 3 ? 5 + (float)rnd.NextDouble() * 6 : 2 + (float)rnd.NextDouble() * 2) * Scale;
            AgregarChispa(x, y, centro + (rnd.NextDouble() - 0.5) * 2.5, (260 + (float)rnd.NextDouble() * 560) * Scale,
                now + rnd.NextDouble() * 0.08, 0.9 + rnd.NextDouble() * 0.8, tipo, tam, cs[rnd.Next(cs.Length)]);
        }
    }

    void DibujarChispas(Graphics g, double now, int ox, int oy, float op)
    {
        if (chispas.Count == 0) return;
        chispas.RemoveAll(c => now - c.Nace > c.Vida || now < c.Nace - 1);
        float grav = 900f * Scale;
        foreach (Chispa c in chispas)
        {
            float t = (float)(now - c.Nace);
            if (t < 0) continue;
            float p = (float)(t / c.Vida), k = 1f - p;
            // frenado por el aire (la distancia crece cada vez menos) + gravedad para las que caen
            float freno = (float)((1 - Math.Exp(-2.2 * t)) / 2.2), lento = (float)Math.Exp(-2.2 * t);
            float gr = c.Tipo <= 2 ? grav : c.Tipo == 3 ? grav * 0.2f : 0f;
            float x = c.X + c.Vx * freno - ox, y = c.Y + c.Vy * freno + 0.5f * gr * t * t - oy;
            int a = (int)(255 * Math.Min(1f, k * 1.4f) * op);
            if (a <= 0) continue;
            float rot = c.Rot + c.VRot * t;
            switch (c.Tipo)
            {
                case 0:   // chispa: una rayita en la dirección en que va
                {
                    float vx = c.Vx * lento, vy = c.Vy * lento + gr * t;
                    using (Pen pe = new Pen(Color.FromArgb(a, c.Col), c.Tam)) { pe.StartCap = LineCap.Round; pe.EndCap = LineCap.Round; g.DrawLine(pe, x, y, x - vx * 0.03f, y - vy * 0.03f); }
                    break;
                }
                case 1:   // moneda: un óvalo que gira (se aplana y se ensancha)
                {
                    float ancho = Math.Max(1.5f, c.Tam * Math.Abs((float)Math.Cos(rot * Math.PI / 180)));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, c.Col))) g.FillEllipse(b, x - ancho / 2, y - c.Tam / 2, ancho, c.Tam);
                    using (Pen pe = new Pen(Color.FromArgb(a, 150, 95, 0), Math.Max(1f, c.Tam * 0.14f))) g.DrawEllipse(pe, x - ancho / 2, y - c.Tam / 2, ancho, c.Tam);
                    break;
                }
                case 2:   // astilla de cristal: rombo que gira
                {
                    double r = rot * Math.PI / 180, co = Math.Cos(r), si = Math.Sin(r);
                    PointF[] pts = new PointF[4];
                    float[,] f = { { 0, -1 }, { 0.45f, 0 }, { 0, 1 }, { -0.45f, 0 } };
                    for (int j = 0; j < 4; j++) { float px = f[j, 0] * c.Tam, py = f[j, 1] * c.Tam; pts[j] = new PointF(x + (float)(px * co - py * si), y + (float)(px * si + py * co)); }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(a * 0.75f), c.Col))) g.FillPolygon(b, pts);
                    using (Pen pe = new Pen(Color.FromArgb(a, 255, 255, 255), 1f)) g.DrawPolygon(pe, pts);
                    break;
                }
                case 3:   // estrella de 4 puntas que titila
                {
                    float s = c.Tam * (0.55f + 0.45f * (float)Math.Abs(Math.Sin(t * 14 + c.Rot)));
                    PointF[] pts = new PointF[8];
                    for (int j = 0; j < 8; j++) { double an = j * Math.PI / 4 + Math.PI / 8 * 0; float rr = j % 2 == 0 ? s : s * 0.22f; pts[j] = new PointF(x + (float)(Math.Cos(an) * rr), y + (float)(Math.Sin(an) * rr)); }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, c.Col))) g.FillPolygon(b, pts);
                    break;
                }
                case 4:   // onda de choque: un aro que se abre rápido y se desvanece
                {
                    float r = c.Tam * (1 - (1 - p) * (1 - p) * (1 - p));
                    using (Pen pe = new Pen(Color.FromArgb((int)(a * 0.8f), c.Col), Math.Max(1f, 5 * Scale * k))) g.DrawEllipse(pe, x - r, y - r, r * 2, r * 2);
                    break;
                }
                default:  // destello: un círculo de luz que crece y se apaga
                {
                    float r = c.Tam * (0.35f + 0.65f * p);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(x - r, y - r, r * 2, r * 2);
                        using (PathGradientBrush b = new PathGradientBrush(gp))
                        {
                            b.CenterColor = Color.FromArgb((int)(a * 0.85f), c.Col);
                            b.SurroundColors = new[] { Color.FromArgb(0, c.Col) };
                            g.FillPath(b, gp);
                        }
                    }
                    break;
                }
            }
        }
    }

    // ---- SKIN de la tienda (bronce / oro / diamante): solo si la compraste (se revisa en las estadísticas cifradas) ----
    string skinsTengo = ""; int skinRevisado = int.MinValue;
    string SkinActiva
    {
        get
        {
            if (string.IsNullOrEmpty(cfg.Skin) || troll) return null;
            if (unchecked(Environment.TickCount - skinRevisado) > 5000 || skinRevisado == int.MinValue)
            {
                skinRevisado = Environment.TickCount;
                try { skinsTengo = EstadisticasDvd.Load().Skins ?? ""; } catch { }
            }
            return ("," + skinsTengo + ",").Contains("," + cfg.Skin + ",") ? cfg.Skin : null;
        }
    }

    // Si cambia el estilo o el tamaño, los dibujos guardados ya no sirven
    void CheckCache()
    {
        string key = cfg.Estilo + "|" + Scale + "|" + cfg.LogoTexto + "|" + cfg.LogoImagen + "|" + troll + "|" + SkinActiva
            + (cfg.Estilo == "reloj" ? "|" + HoraReloj : "");   // el reloj se vuelve a dibujar cada segundo
        if (key == cacheKey && spriteCache.Count + haloCache.Count < MaxSpritesEnCache) return;
        cacheKey = key;
        foreach (Bitmap b in spriteCache.Values) b.Dispose();
        foreach (Bitmap b in haloCache.Values) b.Dispose();
        spriteCache.Clear(); haloCache.Clear();
        CargarImagenLogo();
    }

    // ---- Logo personalizado: imagen elegida por el usuario ----
    Bitmap imagenLogo;
    string imagenLogoRuta = null;

    void CargarImagenLogo()
    {
        string ruta = troll ? "<troll>" : (cfg.LogoImagen ?? "");
        if (ruta == imagenLogoRuta) return;
        imagenLogoRuta = ruta;
        if (imagenLogo != null) { imagenLogo.Dispose(); imagenLogo = null; }
        if (troll)
        {
            // código secreto TROLL: el DVD se vuelve la imagen troll (viene dentro del .exe)
            try
            {
                Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("troll.png");
                if (s != null) using (Image img = Image.FromStream(s)) imagenLogo = new Bitmap(img);
            }
            catch { imagenLogo = null; }
            return;
        }
        if (ruta.Length == 0) return;
        try
        {
            // se copia a memoria para no dejar el archivo bloqueado
            using (FileStream fs = File.OpenRead(ruta))
            using (Image img = Image.FromStream(fs))
                imagenLogo = new Bitmap(img);
        }
        catch { imagenLogo = null; }
    }

    bool UsaImagen { get { return imagenLogo != null; } }

    // Dónde va la imagen dentro de la caja del logo (220x110), sin deformarla
    RectangleF RectImagen()
    {
        float bw = BaseW, bh = BaseH;
        float k = Math.Min(bw / imagenLogo.Width, bh / imagenLogo.Height);
        float w = imagenLogo.Width * k, h = imagenLogo.Height * k;
        return new RectangleF((bw - w) / 2, (bh - h) / 2, w, h);
    }

    // Dibuja el logo que toque: imagen, disco o logo DVD
    void DrawLogoForma(Graphics g, Color c)
    {
        if (UsaImagen)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(imagenLogo, RectImagen());
        }
        else if (cfg.Estilo == "disco") DrawDisc(g, c);
        else if (cfg.Estilo == "vhs") DrawVhs(g, c);
        else if (cfg.Estilo == "reloj") { using (GraphicsPath p = RelojForma()) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p); }
        else DrawDvdLogo(g, c);
    }

    // ---- Logo VHS (dibujado a mano): letras "VHS" gruesas e inclinadas con rayas de cinta,
    // y abajo un casete chiquito con sus dos carretes. Ocupa la misma caja que el logo DVD (220x110).
    GraphicsPath VhsLetras()
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        using (FontFamily ff = new FontFamily("Arial Black"))
            p.AddString("VHS", ff, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
        RectangleF lb = p.GetBounds();
        float w = BaseW - 30, h = 64;
        using (Matrix m = new Matrix())
        {
            m.Translate(12, 2);
            m.Shear(-0.22f, 0);
            m.Translate(h * 0.22f, 0);
            m.Scale(w / lb.Width, h / lb.Height);
            m.Translate(-lb.X, -lb.Y);
            p.Transform(m);
        }
        return p;
    }

    RectangleF VhsCasete() { return new RectangleF(40, 74, BaseW - 80, 34); }

    GraphicsPath VhsForma()
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        using (GraphicsPath l = VhsLetras()) p.AddPath(l, false);
        RectangleF r = VhsCasete();
        float rad = 7;
        p.StartFigure();
        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90); p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90); p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        p.CloseFigure();
        return p;
    }

    void DrawVhs(Graphics g, Color c)
    {
        using (GraphicsPath forma = VhsForma())
        using (SolidBrush b = new SolidBrush(c))
            g.FillPath(b, forma);
        // lo que se "recorta" (queda transparente): rayas de cinta en las letras y la ventanita con carretes del casete
        g.CompositingMode = CompositingMode.SourceCopy;
        using (SolidBrush clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
        {
            foreach (float y in new[] { 38f, 47f, 56f }) g.FillRectangle(clear, 0, y, BaseW, 3.2f);
            RectangleF r = VhsCasete();
            RectangleF ventana = new RectangleF(r.X + 26, r.Y + 8, r.Width - 52, r.Height - 16);
            g.FillRectangle(clear, ventana);
            float rr = ventana.Height / 2 + 3, cy = ventana.Y + ventana.Height / 2;
            g.FillEllipse(clear, ventana.X + 4 - rr, cy - rr, rr * 2, rr * 2);
            g.FillEllipse(clear, ventana.Right - 4 - rr, cy - rr, rr * 2, rr * 2);
        }
        g.CompositingMode = CompositingMode.SourceOver;
        // centros de los carretes (rueditas) otra vez del color
        using (SolidBrush b = new SolidBrush(c))
        {
            RectangleF r = VhsCasete();
            RectangleF ventana = new RectangleF(r.X + 26, r.Y + 8, r.Width - 52, r.Height - 16);
            float cy = ventana.Y + ventana.Height / 2, rc = 4.5f;
            g.FillEllipse(b, ventana.X + 4 - rc, cy - rc, rc * 2, rc * 2);
            g.FillEllipse(b, ventana.Right - 4 - rc, cy - rc, rc * 2, rc * 2);
        }
    }

    // ---- Logo de la imagen: se prepara UNA vez en blanco (al tamaño de pantalla) y luego solo se tiñe ----
    // Antes se redibujaba desde la imagen grande por cada color nuevo y con el arcoíris de las esquinas
    // (unos 8 colores por segundo) el DVD se trababa. Teñir algo ya preparado es casi instantáneo.
    Bitmap preSprite, preHaloColor, preHaloBlanco;
    string preClave;

    bool UsaMascara { get { return !UsaImagen && cfg.Estilo != "disco" && cfg.Estilo != "vhs" && cfg.Estilo != "reloj" && (cfg.LogoTexto == null || cfg.LogoTexto == "" || cfg.LogoTexto == "DVD") && MascaraLogo() != null; } }

    void PrepararMascara()
    {
        if (preClave == cacheKey && preSprite != null) return;
        preClave = cacheKey;
        if (preSprite != null) { preSprite.Dispose(); preHaloColor.Dispose(); preHaloBlanco.Dispose(); }
        Bitmap m = MascaraLogo();
        RectangleF r = RectMascara(m);
        preSprite = new Bitmap((int)Math.Ceiling(W) + 1, (int)Math.Ceiling(H) + 1, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(preSprite))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(Scale, Scale);
            DibujarMascara(g, m, r, Color.White, 1f);
        }
        int hw = (int)Math.Ceiling((BaseW + Pad * 2) * Scale), hh = (int)Math.Ceiling((BaseH + Pad * 2) * Scale);
        preHaloColor = new Bitmap(hw, hh, PixelFormat.Format32bppPArgb);    // la parte de afuera (se tiñe)
        preHaloBlanco = new Bitmap(hw, hh, PixelFormat.Format32bppPArgb);   // la parte de adentro (blanca)
        for (int capa = 0; capa < 2; capa++)
            using (Graphics g = Graphics.FromImage(capa == 0 ? preHaloColor : preHaloBlanco))
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.ScaleTransform(Scale, Scale);
                g.TranslateTransform(Pad, Pad);
                for (int anillo = capa == 0 ? 6 : 3; anillo >= (capa == 0 ? 4 : 1); anillo--)
                {
                    float radio = anillo * 7f;
                    for (int k = 0; k < 16; k++)
                    {
                        double a = k * Math.PI / 8;
                        RectangleF rr = r; rr.Offset((float)(Math.Cos(a) * radio), (float)(Math.Sin(a) * radio));
                        DibujarMascara(g, m, rr, Color.White, 0.07f);
                    }
                }
                if (capa == 1) DibujarMascara(g, m, r, Color.White, 0.6f);   // centro blanco brillante
            }
    }

    // copia una imagen ya preparada (sin cambiar su tamaño) pintándola de un color
    static void Tenir(Graphics g, Bitmap src, Color c)
    {
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new float[][] {
                new float[] { c.R / 255f, 0, 0, 0, 0 }, new float[] { 0, c.G / 255f, 0, 0, 0 },
                new float[] { 0, 0, c.B / 255f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
    }

    Bitmap GetSprite(Color c)
    {
        Bitmap b;
        string skin = UsaImagen ? null : SkinActiva;   // con skin el logo es de metal (no cambia de color)
        if (skin != null) c = Color.White;
        int clave = UsaImagen ? 0 : skin != null ? 1 : c.ToArgb(); // la imagen no cambia de color
        if (spriteCache.TryGetValue(clave, out b)) return b;
        if (UsaMascara)
        {
            PrepararMascara();
            b = new Bitmap(preSprite.Width, preSprite.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b)) Tenir(g, preSprite, c);
            if (skin != null) DvdConfig.Metalizar(b, skin);
            spriteCache[clave] = b;
            return b;
        }
        b = new Bitmap((int)Math.Ceiling(W) + 1, (int)Math.Ceiling(H) + 1, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.ScaleTransform(Scale, Scale);
            DrawLogoForma(g, c);
        }
        if (skin != null) DvdConfig.Metalizar(b, skin);
        spriteCache[clave] = b;
        return b;
    }

    Bitmap GetHalo(Color c)
    {
        Bitmap b;
        if (haloCache.TryGetValue(c.ToArgb(), out b)) return b;
        b = new Bitmap((int)Math.Ceiling((BaseW + Pad * 2) * Scale), (int)Math.Ceiling((BaseH + Pad * 2) * Scale), PixelFormat.Format32bppPArgb);
        if (UsaMascara)
        {
            // brillo con la forma del logo: la parte de afuera teñida del color + la parte blanca de adentro
            PrepararMascara();
            using (Graphics g = Graphics.FromImage(b))
            {
                Tenir(g, preHaloColor, c);
                g.DrawImageUnscaled(preHaloBlanco, 0, 0);
            }
            haloCache[c.ToArgb()] = b;
            return b;
        }
        using (Graphics g = Graphics.FromImage(b))
        using (GraphicsPath shape = ShapePath())
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(Scale, Scale);
            g.TranslateTransform(Pad, Pad);
            for (int i = 9; i >= 1; i--)
            {
                Color gc = i > 5 ? c : Color.White;
                using (Pen p = new Pen(Color.FromArgb(55, gc), i * 4.5f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, shape); }
            }
            // centro blanco brillante encima del logo
            using (SolidBrush wb = new SolidBrush(Color.FromArgb(150, Color.White))) g.FillPath(wb, shape);
        }
        haloCache[c.ToArgb()] = b;
        return b;
    }

    GraphicsPath ShapePath()
    {
        // "Winding": las piezas que se enciman se rellenan, y los huecos (dibujados al revés) quedan vacíos
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        if (UsaImagen) { p.AddRectangle(RectImagen()); return p; }
        if (cfg.Estilo == "disco") { p.AddEllipse(0, 0, BaseW, BaseH); return p; }
        if (cfg.Estilo == "vhs") { using (GraphicsPath v = VhsForma()) p.AddPath(v, false); return p; }   // el brillo con la forma del VHS
        if (cfg.Estilo == "reloj") { using (GraphicsPath v = RelojForma()) p.AddPath(v, false); return p; }   // el brillo con la forma de la hora
        using (GraphicsPath letters = LettersPath()) p.AddPath(letters, false);
        DiscoConPuntas(p, DiscRect());
        // ™ chiquita y escondida arriba a la derecha (como en el logo clásico)
        using (FontFamily ff = new FontFamily("Arial"))
            p.AddString("TM", ff, (int)FontStyle.Bold, 8f, new PointF(BaseW - 15, 1), StringFormat.GenericTypographic);
        return p;
    }

    // Disco visto de lado: delgado y más ancho que las letras
    RectangleF DiscRect() { return new RectangleF(2, 80, BaseW - 4, 24); }

    // Letras anchas, planas e inclinadas (estilo clásico). Usa el texto del logo (por defecto "DVD").
    GraphicsPath LettersPath()
    {
        string texto = string.IsNullOrEmpty(cfg.LogoTexto) ? "DVD" : cfg.LogoTexto;
        GraphicsPath letters = new GraphicsPath(FillMode.Winding);
        if (texto == "DVD") LetrasDvdDibujadas(letters);   // las del logo clásico, dibujadas a mano
        else
            using (FontFamily ff = new FontFamily("Arial Black"))
                letters.AddString(texto, ff, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
        RectangleF lb = letters.GetBounds();
        if (lb.Width < 1 || lb.Height < 1) return letters;
        float ancho = BaseW - 12, alto = 74;
        float sx = ancho * 0.88f / lb.Width;
        float sy = Math.Min(alto / lb.Height, sx * 1.6f); // textos largos: no se aplastan demasiado
        if (texto == "DVD") { sx = (BaseW - 22) / lb.Width; sy = 60f / lb.Height; }   // las del logo clásico son muy anchas y bajitas
        float h = lb.Height * sy;
        float w = lb.Width * sx;
        float x0 = 6 + (ancho * 0.88f - w) / 2, y0 = (texto == "DVD" ? 77 : alto) - h; // pegado encima del disco
        using (Matrix m = new Matrix())
        {
            m.Translate(x0, y0);
            m.Shear(-0.18f, 0);
            m.Translate(h * 0.18f, 0);
            m.Scale(sx, sy);
            m.Translate(-lb.X, -lb.Y);
            letters.Transform(m);
        }
        return letters;
    }

    // "DVD" como en el logo clásico: cada D es un palito delgado con una panza redonda grande
    // (con hueco), y la V es una cuña delgada metida entre las dos D. Alto 100 (luego se acomoda).
    static void LetrasDvdDibujadas(GraphicsPath p)
    {
        LetraD(p, 0, 150);
        // V: brazo izquierdo grueso, brazo derecho delgado, punta abajo bien afilada
        p.AddPolygon(new PointF[] {
            new PointF(140, 0), new PointF(178, 0), new PointF(205, 70),
            new PointF(240, 0), new PointF(258, 0), new PointF(212, 100), new PointF(196, 100) });
        LetraD(p, 225, 150);
    }

    // D del logo clásico: el lado izquierdo muy inclinado (sin palito recto), panza redonda a la
    // derecha y el hueco es una ranura larga y acostada.
    static void LetraD(GraphicsPath p, float x0, float w)
    {
        const float inclina = 0.30f, grueso = 22, panza = 24;
        float c = x0 + w * 0.45f, rx = w * 0.55f;
        // contorno de afuera (sentido del reloj)
        p.StartFigure();
        p.AddLine(x0 + 100 * inclina, 0, c, 0);
        p.AddArc(c - rx, 0, rx * 2, 100, -90, 180);
        p.AddLine(c, 100, x0, 100);
        p.CloseFigure();
        // ranura de adentro (al revés, para que quede vacía), paralela al lado inclinado
        float arriba = 22, abajo = 78;
        float izqArriba = x0 + (100 - arriba) * inclina + grueso;
        float izqAbajo = x0 + (100 - abajo) * inclina + grueso;
        float irx = rx - panza;
        p.StartFigure();
        p.AddLine(izqAbajo, abajo, c, abajo);
        p.AddArc(c - irx, arriba, irx * 2, abajo - arriba, 90, -180);
        p.AddLine(c, arriba, izqArriba, arriba);
        p.CloseFigure();
    }

    // Disco visto de lado con las puntas afiladas (como un ojo)
    static void DiscoConPuntas(GraphicsPath p, RectangleF r)
    {
        float cy = r.Y + r.Height / 2, k = r.Height / 2 / 0.75f;   // una curva así sube 0.75 de lo que se jala
        p.StartFigure();
        p.AddBezier(r.Left, cy, r.Left + r.Width * 0.22f, cy - k, r.Right - r.Width * 0.22f, cy - k, r.Right, cy);
        p.AddBezier(r.Right, cy, r.Right - r.Width * 0.22f, cy + k, r.Left + r.Width * 0.22f, cy + k, r.Left, cy);
        p.CloseFigure();
    }

    // Logo "DVD" sacado de la imagen que mandaste (viene dentro del .exe): blanco con transparencia,
    // así se puede pintar de cualquier color.
    static Bitmap mascaraLogo;
    static bool mascaraCargada;
    internal static Bitmap MascaraLogo()   // (también la usa el aviso de logros)
    {
        if (!mascaraCargada)
        {
            mascaraCargada = true;
            try
            {
                Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo_dvd.png");
                if (s != null) mascaraLogo = new Bitmap(s);
            }
            catch { mascaraLogo = null; }
        }
        return mascaraLogo;
    }

    // dónde va la imagen del logo: centrada en el espacio del logo, sin deformarse
    RectangleF RectMascara(Bitmap m)
    {
        float k = Math.Min(BaseW / m.Width, BaseH / m.Height);
        float w = m.Width * k, h = m.Height * k;
        return new RectangleF((BaseW - w) / 2, (BaseH - h) / 2, w, h);
    }

    // pinta la imagen del logo (blanca con transparencia) de un color y con cierta opacidad
    static void DibujarMascara(Graphics g, Bitmap m, RectangleF r, Color c, float opacidad)
    {
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new float[][] {
                new float[] { c.R / 255f, 0, 0, 0, 0 }, new float[] { 0, c.G / 255f, 0, 0, 0 },
                new float[] { 0, 0, c.B / 255f, 0, 0 }, new float[] { 0, 0, 0, opacidad, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
            g.DrawImage(m, new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height)),
                0, 0, m.Width, m.Height, GraphicsUnit.Pixel, ia);
        }
    }

    void DrawDvdLogo(Graphics g, Color c)
    {
        Bitmap m = (cfg.LogoTexto == null || cfg.LogoTexto == "" || cfg.LogoTexto == "DVD") ? MascaraLogo() : null;
        if (m != null)
        {
            InterpolationMode antes = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            DibujarMascara(g, m, RectMascara(m), c, 1f);
            g.InterpolationMode = antes;
            return;
        }
        using (GraphicsPath shape = ShapePath())
        using (SolidBrush b = new SolidBrush(c))
            g.FillPath(b, shape);
        // "VIDEO" recortado (transparente) dentro del disco
        using (GraphicsPath word = new GraphicsPath())
        {
            using (FontFamily ff = new FontFamily("Arial"))
                word.AddString("V I D E O", ff, (int)FontStyle.Bold, 19f, DiscRect(), Centered());
            g.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0))) g.FillPath(clear, word);
            g.CompositingMode = CompositingMode.SourceOver;
        }
    }

    void DrawDisc(Graphics g, Color c)
    {
        RectangleF outer = new RectangleF(0, 0, BaseW, BaseH);
        float cx = BaseW / 2, cy = BaseH / 2;
        using (GraphicsPath disc = new GraphicsPath())
        {
            disc.AddEllipse(outer);
            using (SolidBrush b = new SolidBrush(Blend(c, Color.FromArgb(225, 230, 240), 0.55f))) g.FillEllipse(b, outer);
            g.SetClip(disc);
            Color[] hues = { Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta };
            for (int i = 0; i < 14; i++)
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(i % 2 == 0 ? 70 : 25, hues[i % hues.Length])))
                    g.FillPie(wb, outer.X, outer.Y, outer.Width, outer.Height, i * (360f / 14), 360f / 14);
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(50, Color.White)))
                g.FillPie(shine, outer.X, outer.Y, outer.Width, outer.Height, 300, 25);
            g.ResetClip();
            using (Pen ring = new Pen(Color.FromArgb(90, 255, 255, 255), 1.5f))
                for (float r = 30; r < cx - 4; r += 9) g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
            using (SolidBrush hub = new SolidBrush(Color.FromArgb(200, 240, 240, 245))) g.FillEllipse(hub, cx - 24, cy - 24, 48, 48);
            using (Pen o = new Pen(Color.FromArgb(150, 0, 0, 0), 2.5f)) g.DrawEllipse(o, outer);
            using (GraphicsPath txt = new GraphicsPath())
            {
                using (FontFamily ff = new FontFamily("Arial Black"))
                {
                    // texto del logo sobre el disco, ajustado al ancho del disco
                    string texto = string.IsNullOrEmpty(cfg.LogoTexto) ? "DVD" : cfg.LogoTexto;
                    txt.AddString(texto, ff, (int)(FontStyle.Bold | FontStyle.Italic), 34f, PointF.Empty, StringFormat.GenericTypographic);
                    RectangleF tb0 = txt.GetBounds();
                    if (tb0.Width > 0 && tb0.Height > 0)
                    {
                        float k = Math.Min(1f, Math.Min(110f / tb0.Width, 34f / tb0.Height));
                        using (Matrix tm = new Matrix())
                        {
                            tm.Translate(cx - tb0.Width * k / 2, 38 - tb0.Height * k / 2);
                            tm.Scale(k, k);
                            tm.Translate(-tb0.X, -tb0.Y);
                            txt.Transform(tm);
                        }
                    }
                }
                using (Pen tp = new Pen(Color.FromArgb(160, 0, 0, 0), 3)) g.DrawPath(tp, txt);
                using (SolidBrush tb = new SolidBrush(c)) g.FillPath(tb, txt);
            }
            g.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0))) g.FillEllipse(clear, cx - 10, cy - 10, 20, 20);
            g.CompositingMode = CompositingMode.SourceOver;
        }
    }

    void DrawMessage(Graphics g, double now)
    {
        if (!(now < msgUntil) || !cfg.MostrarMensajeEsquina) return;
        float t = (float)((now - msgStart) / (msgUntil - msgStart));
        int a = (int)(255 * Math.Min(1f, (1f - t) * 1.6f));
        float pop = 1f + 0.25f * (float)Math.Max(0, 1 - t * 6);
        GraphicsState st = g.Save();
        g.TranslateTransform(win.SW / 2f, win.SH / 2f - 40);
        g.ScaleTransform(pop, pop);
        using (GraphicsPath p = new GraphicsPath())
        {
            using (FontFamily ff = new FontFamily("Arial Black"))
            {
                p.AddString("¡ESQUINA! #" + stats.Total, ff, (int)FontStyle.Bold, 70f, new RectangleF(-600, -110, 1200, 110), Centered());
                p.AddString(stats.Ultima, ff, (int)FontStyle.Regular, 30f, new RectangleF(-600, 0, 1200, 60), Centered());
            }
            for (int i = 6; i >= 1; i--)
                using (Pen gp = new Pen(Color.FromArgb(40 * a / 255, msgColor), i * 5)) { gp.LineJoin = LineJoin.Round; g.DrawPath(gp, p); }
            using (Pen o = new Pen(Color.FromArgb(200 * a / 255, 0, 0, 0), 5)) { o.LineJoin = LineJoin.Round; g.DrawPath(o, p); }
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(a, Color.White))) g.FillPath(fb, p);
        }
        g.Restore(st);
    }

    // El contador solo aparece mientras dura el efecto de esquina
    void DrawHud(Graphics g, double now)
    {
        if (!(now < msgUntil) || !cfg.MostrarContador) return;
        float t = (float)((now - msgStart) / (msgUntil - msgStart));
        int a = (int)(255 * Math.Min(1f, (1f - t) * 2f));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (GraphicsPath r = RoundRect(new RectangleF(21, 21, 277, 162), 14))
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(170 * a / 255, 15, 15, 20)))
        using (Pen border = new Pen(Color.FromArgb(120 * a / 255, msgColor), 2))
        { g.FillPath(bg, r); g.DrawPath(border, r); }
        using (Font big = new Font("Segoe UI", 17, FontStyle.Bold))
        using (Font f = new Font("Segoe UI", 10.5f))
        using (SolidBrush wb = new SolidBrush(Color.FromArgb(a, Color.White)))
        using (SolidBrush gb = new SolidBrush(Color.FromArgb(a, Color.Gainsboro)))
        {
            g.DrawString("Esquinas tocadas: " + stats.Total, big, wb, 32, 28);
            string det =
                "Superior izquierda:  " + stats.SupIzq + "\n" +
                "Superior derecha:    " + stats.SupDer + "\n" +
                "Inferior izquierda:  " + stats.InfIzq + "\n" +
                "Inferior derecha:    " + stats.InfDer + "\n" +
                "Última: " + stats.Ultima;
            g.DrawString(det, f, gb, 34, 66);
        }
    }

    // ---------------- bandeja y utilidades ----------------

    string iconKey = null;
    IntPtr iconHandle = IntPtr.Zero;

    // Mientras está abierta la ventana NO ESCAPE, el icono de junto al reloj (iconos ocultos ^)
    // cambia a uno negro que dice "NO ESCAPE" en rojo, y al pasar el mouse dice "NO ESCAPE".
    bool trayNoEscape;
    IntPtr iconoNoEscape = IntPtr.Zero;
    void TrayNoEscape(bool poner)
    {
        if (poner == trayNoEscape) return;
        trayNoEscape = poner;
        if (poner)
        {
            if (iconoNoEscape == IntPtr.Zero)
                using (Bitmap b = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (SolidBrush rojo = new SolidBrush(Color.FromArgb(235, 20, 20)))
                    using (Font grande = new Font("Arial Black", 27f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (Font chica = new Font("Arial Black", 13.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        using (GraphicsPath fondo = new GraphicsPath())
                        {
                            fondo.AddArc(0, 0, 16, 16, 180, 90); fondo.AddArc(47, 0, 16, 16, 270, 90);
                            fondo.AddArc(47, 47, 16, 16, 0, 90); fondo.AddArc(0, 47, 16, 16, 90, 90); fondo.CloseFigure();
                            g.FillPath(Brushes.Black, fondo);
                            using (Pen borde = new Pen(rojo, 3f)) g.DrawPath(borde, fondo);
                        }
                        g.DrawString("NO", grande, rojo, new RectangleF(0, 4, 64, 34), sf);
                        g.DrawString("ESCAPE", chica, rojo, new RectangleF(-2, 35, 68, 22), sf);
                    }
                    iconoNoEscape = b.GetHicon();
                }
            tray.Icon = Icon.FromHandle(iconoNoEscape);
            tray.Text = "NO ESCAPE";
            try { CursorInfectado.Poner(); } catch { }    // el mouse cambia al cursor "infectado"
        }
        else
        {
            try { CursorInfectado.Quitar(); } catch { }   // y vuelve el de siempre
            iconKey = null;          // que vuelva a dibujar el logo de siempre
            RefreshTrayIcon();
            tray.Text = "DVD Rebote";
        }
    }
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    // Icono de la bandeja: el mismo logo que rebota (DVD o disco, con su color)
    void RefreshTrayIcon()
    {
        if (trayNoEscape) return;   // mientras está NO ESCAPE se queda su icono rojo
        CargarImagenLogo();
        string key = cfg.Estilo + "|" + cfg.Color + "|" + cfg.LogoTexto + "|" + cfg.LogoImagen;
        if (key == iconKey) return;
        iconKey = key;
        IntPtr old = iconHandle;
        using (Bitmap b = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                float k = 64f / Math.Max(BaseW, BaseH);
                g.TranslateTransform((64 - BaseW * k) / 2, (64 - BaseH * k) / 2);
                g.ScaleTransform(k, k);
                DrawLogoForma(g, cfg.GetColor());
            }
            iconHandle = b.GetHicon();
        }
        tray.Icon = Icon.FromHandle(iconHandle);
        if (old != IntPtr.Zero) DestroyIcon(old); // el viejo se libera después de poner el nuevo
    }

    void TogglePause()
    {
        cfg.Pausado = !cfg.Pausado;
        try { cfg.Save(); cfgStamp = SafeStamp(); } catch { }
        pauseItem.Text = cfg.Pausado ? "Reanudar" : "Pausar";
    }

    void OpenConfigApp()
    {
        string p = DvdConfig.RutaConfigApp();
        if (p != null) Process.Start(p);
        else MessageBox.Show("No encuentro dvd_config.exe.\n\nPonlo en la misma carpeta que DvdRebote.exe, " +
            "o ábrelo una vez desde donde esté y así lo encuentro.", "DVD Rebote");
    }

    void Quit()
    {
        escuchandoCheats = false;
        // al cerrar el DVD se va todo lo del código secret: "Run", "NO ESCAPE" y las ventanitas de Error
        try { Program.CerrarPregunta(); } catch { }
        try { if (ventanaError != null && !ventanaError.IsDisposed) ventanaError.CerrarPorSalida(); } catch { }
        try { CancelarPantallazo(); } catch { }
        try { if (consola != null) consola.Cancelar(); } catch { }
        try { CursorInfectado.Quitar(); } catch { }   // que nunca se quede el cursor de NO ESCAPE
        try { FondoAnimado.Quitar(); } catch { }      // tu fondo de pantalla vuelve a como estaba
        try { hist.Save(); } catch { }                 // lo que faltaba de las estadísticas
        win.QuitarAtajo(AtajoEsconder);
        win.QuitarAtajo(AtajoPausar);
        Mando.Parar(); // que el control no se quede vibrando al cerrar
        try { RestaurarVentanas(); } catch { } // al cerrar el DVD, las ventanas achicadas vuelven a como estaban
        try { PonerGancho(false); } catch { }
        if (protector) Cursor.Show();
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        if (iconHandle != IntPtr.Zero) { DestroyIcon(iconHandle); iconHandle = IntPtr.Zero; }
        win.FreeSurface();
        ExitThread();
    }

    static Color FromHsv(double hDeg, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs((hDeg / 60) % 2 - 1)), m = v - c;
        double r = 0, g = 0, b = 0;
        if (hDeg < 60) { r = c; g = x; } else if (hDeg < 120) { r = x; g = c; } else if (hDeg < 180) { g = c; b = x; }
        else if (hDeg < 240) { g = x; b = c; } else if (hDeg < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static StringFormat Centered()
    {
        StringFormat f = new StringFormat();
        f.Alignment = StringAlignment.Center;
        f.LineAlignment = StringAlignment.Center;
        return f;
    }

    static Color Blend(Color a, Color b, float k)
    {
        k = Math.Max(0, Math.Min(1, k));
        return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
    }
}

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);

    static void LogError(object ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(DvdConfig.Carpeta, "dvd_error.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine + Environment.NewLine);
        }
        catch { }
    }

    // Al usar el código SECRET sale primero un cuadro de mensaje REAL de Windows con el escudo y estática encima:
    // "¿Quieres ejecutar esto?" con Sí y No. Con cualquiera de los dos sigue igual.
    // El cuadro con Sí/No no tiene X ni se cierra con Esc: solo se va eligiendo una opción.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MSGBOXPARAMS
    {
        public uint cbSize; public IntPtr hwndOwner; public IntPtr hInstance;
        public string lpszText; public string lpszCaption; public uint dwStyle;
        public IntPtr lpszIcon; public IntPtr dwContextHelpId; public IntPtr lpfnMsgBoxCallback; public uint dwLanguageId;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxIndirect(ref MSGBOXPARAMS p);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string nombre);

    // Cuadro de mensaje real de Windows con un icono de user32.dll (los clásicos):
    // 101 = advertencia vieja, 103 = error, 104 = información, 106 = escudo
    internal static int MensajeConIcono(IntPtr dueno, string texto, string titulo, uint estilo, int icono)
    {
        MSGBOXPARAMS p = new MSGBOXPARAMS();
        p.cbSize = (uint)Marshal.SizeOf(typeof(MSGBOXPARAMS));
        p.hwndOwner = dueno;
        p.hInstance = GetModuleHandle("user32.dll");
        p.lpszIcon = (IntPtr)icono;
        p.lpszCaption = titulo;
        p.lpszText = texto;
        p.dwStyle = estilo | 0x80;   // MB_USERICON
        return MessageBoxIndirect(ref p);
    }

    const string TituloPregunta = "Run";

    // Tu escudo (el brillante de Windows 7, con borde plateado) en el cuadro "Run".
    // Windows ya solo trae el nuevo, así que va dentro del .exe y se le pone al cuadro cuando aparece
    // (STM_SETICON al dibujito del cuadro, que en los cuadros de mensaje es el control 20).
    static IntPtr escudo = IntPtr.Zero;
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr dlg, int id);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static void PonerEscudoViejo(IntPtr caja)
    {
        try
        {
            using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("escudo_viejo.png"))
            using (Bitmap b = new Bitmap(s))
                escudo = b.GetHicon();
            IntPtr dibujo = GetDlgItem(caja, 20);
            if (dibujo != IntPtr.Zero) SendMessage(dibujo, 0x0170 /*STM_SETICON*/, escudo, IntPtr.Zero);

            // el escudo también en el título: se le quita el marco de diálogo (que esconde el icono)
            // y se le da el icono chico y grande
            using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("escudo_viejo.png"))
            using (Bitmap b = new Bitmap(s))
            using (Bitmap chico = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(chico))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(b, 0, 0, 16, 16);
                }
                escudoChico = chico.GetHicon();
            }
            SetWindowLong(caja, -20 /*GWL_EXSTYLE*/, GetWindowLong(caja, -20) & ~0x1 /*WS_EX_DLGMODALFRAME*/);
            SendMessage(caja, 0x0080 /*WM_SETICON*/, (IntPtr)0 /*ICON_SMALL*/, escudoChico);
            SendMessage(caja, 0x0080, (IntPtr)1 /*ICON_BIG*/, escudo);   // el grande es el de la barra de tareas
            SetWindowPos(caja, IntPtr.Zero, 0, 0, 0, 0, 0x0020 | 0x0001 | 0x0002 | 0x0004 | 0x0010); // FRAMECHANGED, sin mover
            AppIdVentana.Poner(caja, "DvdRebote.Run");   // su propio botón en la barra, con el escudo
        }
        catch { }
    }
    static IntPtr escudoChico = IntPtr.Zero;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    static volatile bool preguntaCancelada;

    // la cierra el .bat "Cerrar ventanas de error" (como si dijeras No, pero sin abrir NO ESCAPE)
    internal static void CerrarPregunta()
    {
        IntPtr caja = FindWindow("#32770", TituloPregunta);
        uint pid = 0; if (caja != IntPtr.Zero) GetWindowThreadProcessId(caja, out pid);
        if (caja == IntPtr.Zero || pid != (uint)Process.GetCurrentProcess().Id) return;
        preguntaCancelada = true;
        PostMessage(caja, 0x0111 /*WM_COMMAND*/, (IntPtr)7 /*IDNO*/, IntPtr.Zero);
    }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);

    // true = sigue (Sí o No); false = la cerró el .bat
    internal static bool PreguntarAdministrador()
    {
        preguntaCancelada = false;
        Estatica estatica = null; CapaEstatica capa = null; Form dueno = null;
        System.Windows.Forms.Timer reloj = null;
        try
        {
            // estática encima del cuadro (una capa que deja pasar los clics a los botones)
            estatica = new Estatica();
            capa = new CapaEstatica(estatica);
            reloj = new System.Windows.Forms.Timer { Interval = 40 };   // la estática sigue al cuadro cada 40 ms
            uint yo = (uint)Process.GetCurrentProcess().Id;
            reloj.Tick += delegate
            {
                IntPtr caja = FindWindow("#32770", TituloPregunta);
                uint pid = 0; if (caja != IntPtr.Zero) GetWindowThreadProcessId(caja, out pid);
                if (caja == IntPtr.Zero || pid != yo) return;
                if (escudo == IntPtr.Zero) PonerEscudoViejo(caja);
                if (!capa.Visible) capa.Show();
                RECT r; GetWindowRect(caja, out r);
                IntPtr arriba = GetWindow(caja, 3 /*GW_HWNDPREV*/);
                if (arriba == capa.Handle) SetWindowPos(capa.Handle, IntPtr.Zero, r.L, r.T, r.R - r.L, r.B - r.T, 0x0010 | 0x0004);
                else SetWindowPos(capa.Handle, arriba == IntPtr.Zero ? (IntPtr)(-1) : arriba, r.L, r.T, r.R - r.L, r.B - r.T, 0x0010);
                estatica.Avanzar();
                capa.Opacity = 0.22 + new Random().NextDouble() * 0.14;   // estática a medias (más suave que en los Error)
                capa.Invalidate();
            };
            reloj.Start();

            MSGBOXPARAMS p = new MSGBOXPARAMS();
            p.cbSize = (uint)Marshal.SizeOf(typeof(MSGBOXPARAMS));
            // sin dueño: así el cuadro sale también en la barra de tareas (con tu escudo de icono)
            p.hwndOwner = IntPtr.Zero;
            p.hInstance = GetModuleHandle("user32.dll");
            p.lpszIcon = (IntPtr)106;   // escudo de Windows (se cambia por el tuyo en cuanto aparece)
            p.lpszCaption = TituloPregunta;
            p.lpszText = "¿Quieres ejecutar esto?";
            // MB_YESNO | MB_USERICON | MB_TOPMOST | MB_SETFOREGROUND
            p.dwStyle = 0x4 | 0x80 | 0x40000 | 0x10000;
            MessageBoxIndirect(ref p);   // Sí o No: sigue igual
        }
        catch { }
        finally
        {
            if (reloj != null) { reloj.Stop(); reloj.Dispose(); }
            if (capa != null) capa.Close();
            if (estatica != null) estatica.Dispose();
            if (dueno != null) dueno.Dispose();
            if (escudo != IntPtr.Zero) { DestroyIcon(escudo); escudo = IntPtr.Zero; }
        }
        return !preguntaCancelada;
    }    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string clase, string titulo);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr d, int x, int y, int cx, int cy, uint f);

    [STAThread]
    static void Main(string[] args)
    {
        if (DvdConfig.AtenderCampos(args)) return;   // "--campos": solo dice qué ajustes tiene esta versión
        // "--arreglar": lo usa el .bat "Cerrar ventanas de error" cuando el DVD ya está cerrado.
        // Solo regresa el sonido de los programas y el cursor normal (por si se quedaron así) y se cierra.
        if (Array.IndexOf(args, "--arreglar") >= 0)
        {
            try { SilencioSistema.ArreglarSiQuedo(); } catch { }
            try { CursorInfectado.ArreglarSiQuedo(); } catch { }
            return;
        }
        bool aim = Array.IndexOf(args, "--esquina") >= 0; // para probar: arranca apuntando a una esquina

        // ---- PROTECTOR DE PANTALLA: el mismo programa llamado "DVD Rebote.scr" ----
        // Windows lo abre con /s (mostrar), /c (configurar) o /p (vista previa pequeña).
        bool protector = false;
        if (Application.ExecutablePath.EndsWith(".scr", StringComparison.OrdinalIgnoreCase)
            || Array.IndexOf(args, "--protector") >= 0)
        {
            string a0 = args.Length > 0 ? args[0].ToLowerInvariant() : "/c";
            if (a0 == "--protector") a0 = "/s";
            if (a0.StartsWith("/p")) return;                 // vista previa pequeña: no se dibuja
            if (a0.StartsWith("/c"))                         // botón "Configuración" de Windows
            {
                Application.EnableVisualStyles();
                DvdConfig.EsRebote = true;
                string cfgApp = DvdConfig.RutaConfigApp();
                if (cfgApp != null) Process.Start(cfgApp);
                else MessageBox.Show("Abre dvd_config.exe para configurar el DVD.", "DVD Rebote");
                return;
            }
            protector = true;                                // /s: mostrar el protector
        }

        bool created;
        using (Mutex m = new Mutex(true, protector ? "DvdRebote_Protector" : "DvdRebote_UnaSolaInstancia", out created))
        {
            if (!created) return; // ya está corriendo
            Application.EnableVisualStyles();
            if (!DvdConfig.RevisarNetFramework()) return; // avisa si falta .NET Framework 4.5+
            DvdConfig.EsRebote = true;
            DvdConfig.EsProtector = protector;
            if (!protector) DvdConfig.RegistrarCarpeta();   // (ya no se copia a sí mismo como .scr: el protector lo pones tú)
            OverlayWindow.ClickThrough = !protector;      // el protector sí recibe el ratón
            try { SetProcessDPIAware(); } catch { }
            try { CursorInfectado.ArreglarSiQuedo(); } catch { }   // si la vez pasada se quedó el cursor de NO ESCAPE
            try { SilencioSistema.ArreglarSiQuedo(); } catch { }   // o el sonido quitado por el pantallazo
            try { if (!protector) FondoAnimado.ArreglarSiQuedo(); } catch { }   // o el fondo animado (si se cerró de golpe)
            try { timeBeginPeriod(1); } catch { } // temporizador más preciso = movimiento más suave
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { LogError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject); };
            using (EventWaitHandle exit = new EventWaitHandle(false, EventResetMode.AutoReset, "DvdRebote_Salir"))
            {
                DvdApp app = new DvdApp(exit, protector);
                if (aim) app.AimAtCorner();
                Application.Run(app);
            }
            Environment.Exit(0);   // termina todo, también los hilos de las ventanitas de Error
        }
    }
}

// Código secreto SECRET: una ventana de error sin título, con estática de tele vieja detrás del
// mensaje "No escape" y también en el botón "Aceptar". No se puede cerrar: si lo intentas sale
// otro "Error" con el signo de advertencia. Solo se va con "Aceptar" (500 DVDs) o cerrando el DVD.
// Silencio mientras está el pantallazo, SIN tocar la bocina principal (así el icono de volumen y la
// tecla de silencio de las laptops se ven normales y no lo "arreglan"): a cada programa que suena
// (música, videos, juegos, sonidos de Windows) se le baja SU volumen a 0 desde el mezclador, guardando
// cómo estaba. Mientras dure el pantallazo se vuelve a revisar cada 300 ms: si algo empieza a sonar o
// le suben el volumen, se vuelve a 0. Al terminar, a cada uno se le regresa su volumen. Se guarda en
// disco qué programas fueron y su volumen: si el DVD se cerrara a la fuerza, al abrirlo se les regresa.
static class SilencioSistema
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeradorCom { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flujo, int estado, out IntPtr lista);
        [PreserveSig] int GetDefaultAudioEndpoint(int flujo, int rol, out IMMDevice disp);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr a, int b, out IntPtr c);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr a, int b, out IntPtr c);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetSession(int i, out IAudioSessionControl2 s);
    }
    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int s); [PreserveSig] int GetDisplayName(out IntPtr n); [PreserveSig] int SetDisplayName(IntPtr n, ref Guid g);
        [PreserveSig] int GetIconPath(out IntPtr p); [PreserveSig] int SetIconPath(IntPtr p, ref Guid g);
        [PreserveSig] int GetGroupingParam(out Guid g); [PreserveSig] int SetGroupingParam(ref Guid a, ref Guid g);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n); [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier(out IntPtr s); [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr s);
        [PreserveSig] int GetProcessId(out uint pid);
    }
    // el volumen de cada canal de ese programa (es lo que mueve el mezclador de volumen)
    [ComImport, Guid("1C158861-B533-4B30-B1CF-E853E51C59B8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IChannelAudioVolume
    {
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetChannelVolume(uint i, float v, ref Guid g);
        [PreserveSig] int GetChannelVolume(uint i, out float v);
        [PreserveSig] int SetAllVolumes(uint n, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] v, ref Guid g);
        [PreserveSig] int GetAllVolumes(uint n, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out] float[] v);
    }

    class Callado { public IChannelAudioVolume Vol; public float[] Antes; public string Nombre; }
    static readonly Dictionary<uint, Callado> callados = new Dictionary<uint, Callado>();   // por proceso
    static System.Windows.Forms.Timer reloj;
    static bool activo;
    static string Marca { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "sonido_quitado"); } }

    static string NombreDe(uint pid)
    {
        if (pid == 0) return "(sonidos del sistema)";
        try { return Process.GetProcessById((int)pid).ProcessName; } catch { return ""; }
    }

    // recorre todos los programas con sonido de la bocina que se está usando
    static void Recorrer(Action<uint, IChannelAudioVolume> hacer)
    {
        IMMDeviceEnumerator en = (IMMDeviceEnumerator)new EnumeradorCom();
        IMMDevice disp;
        if (en.GetDefaultAudioEndpoint(0 /*salida*/, 1 /*multimedia*/, out disp) != 0 || disp == null) return;
        Guid iid = typeof(IAudioSessionManager2).GUID;
        object o;
        if (disp.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return;
        IAudioSessionEnumerator lista;
        if (((IAudioSessionManager2)o).GetSessionEnumerator(out lista) != 0 || lista == null) return;
        int n; lista.GetCount(out n);
        uint yo = (uint)Process.GetCurrentProcess().Id;
        for (int i = 0; i < n; i++)
        {
            IAudioSessionControl2 s;
            if (lista.GetSession(i, out s) != 0 || s == null) continue;
            uint pid = 0; s.GetProcessId(out pid);
            if (pid == yo) continue;
            IChannelAudioVolume v = s as IChannelAudioVolume;
            if (v != null) hacer(pid, v);
        }
    }

    static float[] Leer(IChannelAudioVolume v)
    {
        uint n; v.GetChannelCount(out n);
        float[] a = new float[n];
        if (n > 0) v.GetAllVolumes(n, a);
        return a;
    }

    static void Poner(IChannelAudioVolume v, float[] a)
    {
        if (a.Length == 0) return;
        Guid g = Guid.Empty; v.SetAllVolumes((uint)a.Length, a, ref g);
    }

    static void CallarTodo()
    {
        bool hayNuevos = false;
        Recorrer(delegate (uint pid, IChannelAudioVolume v)
        {
            float[] ahora = Leer(v);
            if (!callados.ContainsKey(pid))
            {
                callados[pid] = new Callado { Vol = v, Antes = ahora, Nombre = NombreDe(pid) };
                hayNuevos = true;
            }
            bool suena = false; foreach (float f in ahora) if (f > 0f) suena = true;
            if (suena) Poner(v, new float[ahora.Length]);   // todos los canales a 0
        });
        if (hayNuevos) GuardarMarca();
    }

    static void GuardarMarca()
    {
        List<string> l = new List<string>();
        foreach (Callado c in callados.Values)
        {
            string[] vs = new string[c.Antes.Length];
            for (int i = 0; i < vs.Length; i++) vs[i] = c.Antes[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            l.Add(c.Nombre + "|" + string.Join(",", vs));
        }
        try { File.WriteAllLines(Marca, l.ToArray()); } catch { }
    }

    public static void Silenciar()
    {
        if (activo) return;
        activo = true;
        Directory.CreateDirectory(Path.GetDirectoryName(Marca));
        CallarTodo();
        reloj = new System.Windows.Forms.Timer { Interval = 300 };
        reloj.Tick += delegate { try { CallarTodo(); } catch { } };
        reloj.Start();
    }

    public static void Restaurar()
    {
        if (reloj != null) { reloj.Stop(); reloj.Dispose(); reloj = null; }
        foreach (Callado c in callados.Values) { try { Poner(c.Vol, c.Antes); } catch { } }
        callados.Clear();
        activo = false;
        try { File.Delete(Marca); } catch { }
    }

    // si el DVD se cerró a la fuerza con programas callados: se les regresa su volumen por nombre
    public static void ArreglarSiQuedo()
    {
        if (!File.Exists(Marca)) return;
        Dictionary<string, float[]> antes = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string l in File.ReadAllLines(Marca))
        {
            int p = l.LastIndexOf('|'); if (p < 0) continue;
            string[] vs = l.Substring(p + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            float[] a = new float[vs.Length];
            for (int i = 0; i < vs.Length; i++) float.TryParse(vs[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a[i]);
            antes[l.Substring(0, p)] = a;
        }
        Recorrer(delegate (uint pid, IChannelAudioVolume v)
        {
            float[] a;
            if (antes.TryGetValue(NombreDe(pid), out a)) { uint n; v.GetChannelCount(out n); if (n == a.Length) Poner(v, a); }
        });
        try { File.Delete(Marca); } catch { }
    }

    // para las pruebas: cuántos programas con sonido hay y cuántos están en volumen 0
    public static string Estado()
    {
        int total = 0, callados0 = 0;
        Recorrer(delegate (uint pid, IChannelAudioVolume v) { total++; bool cero = true; foreach (float f in Leer(v)) if (f > 0f) cero = false; if (cero) callados0++; });
        return callados0 + " de " + total;
    }
}

// Cursor "infectado" mientras está abierta la ventana NO ESCAPE: se cambian los cursores de Windows
// (flecha, texto, manita y "cargando") por el tuyo, y al cerrarse se vuelven a cargar los de siempre.
// Se deja una marca en disco: si el DVD se cerrara a la fuerza con el cursor puesto, al abrirlo otra vez se arregla.
static class CursorInfectado
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr h, string f, uint tipo, int cx, int cy, uint fl);
    [DllImport("user32.dll")] static extern bool SetSystemCursor(IntPtr cursor, uint id);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint accion, uint p, IntPtr pv, uint ini);
    static readonly uint[] Cuales = { 32512 /*flecha*/, 32513 /*texto*/, 32649 /*manita*/, 32650 /*cargando*/ };
    static string Marca { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "cursor_puesto"); } }
    static bool puesto;

    public static void Poner()
    {
        if (puesto) return;
        string archivo = Path.Combine(Path.GetTempPath(), "dvdrebote_noescape.cur");
        using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("cursor_noescape.cur"))
        using (FileStream f = File.Create(archivo)) s.CopyTo(f);
        Directory.CreateDirectory(Path.GetDirectoryName(Marca));
        File.WriteAllText(Marca, DateTime.Now.ToString("o"));
        foreach (uint id in Cuales)
        {
            // un cursor nuevo para cada uno (Windows se queda con él), del tamaño de cursor que tengas puesto
            IntPtr h = LoadImage(IntPtr.Zero, archivo, 2 /*IMAGE_CURSOR*/, 0, 0, 0x10 /*LR_LOADFROMFILE*/ | 0x40 /*LR_DEFAULTSIZE*/);
            if (h != IntPtr.Zero) SetSystemCursor(h, id);
        }
        puesto = true;
    }

    public static void Quitar()
    {
        if (!puesto && !File.Exists(Marca)) return;
        SystemParametersInfo(0x0057 /*SPI_SETCURSORS: recarga los cursores de tu esquema*/, 0, IntPtr.Zero, 0);
        puesto = false;
        try { File.Delete(Marca); } catch { }
    }

    public static void ArreglarSiQuedo() { if (File.Exists(Marca)) Quitar(); }
}

// Aviso de LOGRO estilo consola: una "píldora" oscura con el logo de DVD (o el trofeo) a la izquierda,
// "Logro desbloqueado" y abajo los puntos y el nombre. Efecto: sube deslizándose, el icono hace "pop",
// pasa un brillo por encima y el borde brilla del color del trofeo; luego baja y se desvanece.
// No quita el foco ni los clics.
class ToastLogro : Form
{
    static ToastLogro actual;
    static readonly Bitmap[] trofeos = new Bitmap[5];
    readonly string linea2;
    readonly int trofeo;   // 0 = logro normal, 1 = bronce, 2 = plata, 3 = oro
    readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();
    readonly Stopwatch tiempo = Stopwatch.StartNew();
    readonly int yFinal;
    bool confeti;
    const int An = 480, Al = 96;
    const double Entra = 0.45, Sale = 4.6, Fin = 5.2;

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr d, int x, int y, int cx, int cy, uint f);

    public static void Mostrar(string puntosYNombre, Rectangle area) { Mostrar(puntosYNombre, area, 0, 0); }
    public static void Mostrar(string puntosYNombre, Rectangle area, int trofeo) { Mostrar(puntosYNombre, area, trofeo, 0); }
    // trofeosQueTienes > 0: el círculo sale estilo consola con un pedazo del aro prendido por cada trofeo
    public static void Mostrar(string puntosYNombre, Rectangle area, int trofeo, int trofeosQueTienes)
    {
        try { if (actual != null && !actual.IsDisposed) actual.Close(); } catch { }
        actual = new ToastLogro(puntosYNombre, area, trofeo);
        actual.trofeosQueTienes = trofeosQueTienes;
        actual.Show();
    }
    int trofeosQueTienes;

    static Bitmap Trofeo(int n)
    {
        if (n < 1 || n > 3) return null;
        if (trofeos[n] == null)
            try
            {
                using (Stream s = typeof(ToastLogro).Assembly.GetManifestResourceStream("trofeo_" + DvdConfig.MetalesTrofeos[n - 1] + ".png"))
                    if (s != null) trofeos[n] = new Bitmap(s);
            }
            catch { }
        return trofeos[n];
    }

    Color Acento
    {
        get
        {
            switch (trofeo)
            {
                case 1: return Color.FromArgb(225, 140, 70);    // bronce
                case 2: return Color.FromArgb(205, 215, 230);   // plata
                case 3: return Color.FromArgb(255, 205, 60);    // oro
                case 4: return Color.FromArgb(130, 235, 255);   // diamante
                default: return Color.FromArgb(0, 191, 255);    // azul DVD
            }
        }
    }

    ToastLogro(string texto, Rectangle area, int trofeo)
    {
        linea2 = Idioma.T(texto);   // al inglés si está puesto
        this.trofeo = trofeo;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Rectangle trabajo = Screen.FromRectangle(area).WorkingArea;
        yFinal = trabajo.Bottom - Al - 40;
        Bounds = new Rectangle(trabajo.Left + (trabajo.Width - An) / 2, yFinal + 40, An, Al);
        BackColor = Color.FromArgb(40, 40, 46);
        DoubleBuffered = true;
        Opacity = 0;
        using (GraphicsPath p = Pildora(new RectangleF(0, 0, An, Al))) Region = new Region(p);
        reloj.Interval = 15;
        reloj.Tick += delegate
        {
            double t = tiempo.Elapsed.TotalSeconds;
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010);   // que los DVDs no lo tapen
            // TROFEO: sale el confeti cuando el aviso ya casi llegó a su lugar
            if (trofeo > 0 && !confeti && t > 0.25) { confeti = true; EfectoTrofeo.Lanzar(new Rectangle(Left, yFinal, An, Al), trofeo); }
            if (t < Entra)
            {
                double k = 1 - Math.Pow(1 - t / Entra, 3);   // entra rápido y frena suave
                Opacity = k * 0.97;
                Top = yFinal + (int)(40 * (1 - k));
            }
            else if (t < Sale) { Opacity = 0.97; Top = yFinal; }
            else if (t < Fin)
            {
                double k = (t - Sale) / (Fin - Sale);
                Opacity = (1 - k) * 0.97;
                Top = yFinal + (int)(24 * k * k);
            }
            else { reloj.Stop(); Close(); return; }
            Invalidate();
        };
        reloj.Start();
        FormClosed += delegate { reloj.Dispose(); if (actual == this) actual = null; };
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x20 /*TRANSPARENT: los clics pasan*/ | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/ | 0x8 /*TOPMOST*/;
            return cp;
        }
    }

    static GraphicsPath Pildora(RectangleF r)
    {
        GraphicsPath p = new GraphicsPath();
        float d = r.Height;
        p.AddArc(r.Left, r.Top, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        double t = tiempo.Elapsed.TotalSeconds;
        Color ac = Acento;
        RectangleF todo = new RectangleF(0, 0, An - 1, Al - 1);

        // fondo
        using (GraphicsPath p = Pildora(todo))
        using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(70, 70, 78), Color.FromArgb(30, 30, 34), 90f))
            g.FillPath(b, p);
        // reflejo de color del trofeo a la izquierda
        using (GraphicsPath p = Pildora(todo))
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1, 0, An + 2, Al), Color.FromArgb(70, ac), Color.FromArgb(0, ac), 0f))
        {
            b.SetBlendTriangularShape(0f, 1f);                       // fuerte a la izquierda...
            b.Blend = new Blend { Factors = new[] { 0f, 1f, 1f }, Positions = new[] { 0f, 0.6f, 1f } };   // ...y nada desde el 60 %
            g.FillPath(b, p);
        }

        // BRILLO que cruza la píldora (entre 0.55 y 1.35 s)
        double sb = (t - 0.55) / 0.8;
        if (sb > 0 && sb < 1)
        {
            float x = (float)(-140 + sb * (An + 280));
            using (GraphicsPath p = Pildora(todo))
            {
                Region antes = g.Clip;
                g.SetClip(p);
                g.TranslateTransform(x, 0); g.RotateTransform(20);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, -60, 70, Al + 120),
                    Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                    cb.Positions = new[] { 0f, 0.5f, 1f };
                    b.InterpolationColors = cb;
                    g.FillRectangle(b, new RectangleF(0, -60, 70, Al + 120));
                }
                g.ResetTransform();
                g.Clip = antes;
            }
        }

        // borde que brilla (más fuerte al aparecer y luego "respira")
        double pulso = t < 1.2 ? 1 - t / 1.2 * 0.5 : 0.5 + 0.2 * Math.Sin(t * 4);
        using (GraphicsPath p = Pildora(new RectangleF(1, 1, An - 3, Al - 3)))
        using (Pen borde = new Pen(Color.FromArgb((int)(255 * Math.Max(0, Math.Min(1, pulso))), ac), 2.5f))
            g.DrawPath(borde, p);

        // icono con "pop" (crece un poco de más y regresa)
        double pt = Math.Max(0, Math.Min(1, (t - 0.12) / 0.5));
        float esc = (float)(pt < 0.6 ? 0.55 + pt / 0.6 * 0.6 : 1.15 - (pt - 0.6) / 0.4 * 0.15);
        RectangleF c = new RectangleF(8, 8, Al - 16, Al - 16);
        float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2;

        if (trofeo > 0)
        {
            using (GraphicsPath pil = Pildora(todo))
            {
                Region antes = g.Clip;
                g.SetClip(pil);
                // RAYOS de luz que giran detrás del trofeo
                g.TranslateTransform(cx, cy); g.RotateTransform((float)(t * 35));
                int nr = trofeo == 4 ? 20 : trofeo == 3 ? 16 : 12;
                using (SolidBrush rayo = new SolidBrush(Color.FromArgb((int)(trofeo >= 3 ? 90 : 60), ac)))
                    for (int i = 0; i < nr; i++)
                    {
                        g.FillPolygon(rayo, new[] { new PointF(0, 0), new PointF(140, -9), new PointF(140, 9) });
                        g.RotateTransform(360f / nr);
                    }
                g.ResetTransform();
                // BRILLITOS que parpadean por la píldora
                float[,] pos = { { 130, 18 }, { 210, 74 }, { 300, 16 }, { 370, 70 }, { 430, 28 }, { 255, 44 } };
                for (int i = 0; i < pos.GetLength(0); i++)
                {
                    float br = (float)(0.5 + 0.5 * Math.Sin(t * 6 + i * 1.7));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * br), i % 2 == 0 ? Color.White : ac)))
                        EfectoTrofeo.Estrella(g, b, pos[i, 0], pos[i, 1], 3 + br * 5);
                }
                g.Clip = antes;
            }
        }
        g.TranslateTransform(cx, cy); g.ScaleTransform(esc, esc); g.TranslateTransform(-cx, -cy);
        // resplandor detrás del icono
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddEllipse(RectangleF.Inflate(c, 8, 8));
            using (PathGradientBrush pg = new PathGradientBrush(gp))
            {
                pg.CenterColor = Color.FromArgb((int)(150 * Math.Max(0.3, pulso)), ac);
                pg.SurroundColors = new[] { Color.FromArgb(0, ac) };
                g.FillPath(pg, gp);
            }
        }
        // TROFEO: el aro estilo consola con un pedazo prendido por cada trofeo que tienes (el nuevo parpadea)
        if (trofeo > 0) PintarCirculoEspecial(g, Rectangle.Round(c), Math.Max(trofeo, trofeosQueTienes), t, trofeo);
        else PintarCirculo(g, Rectangle.Round(c));

        g.ResetTransform();

        // textos
        string titulo = trofeo == 0 ? Idioma.T("Logro desbloqueado")
            : Idioma.Ingles ? new[] { "Bronze", "Silver", "Gold", "Diamond" }[trofeo - 1] + " trophy unlocked!"
            : "¡Trofeo de " + DvdConfig.MetalesTrofeos[trofeo - 1] + " desbloqueado!";
        using (Font f1 = new Font("Segoe UI", 15f, trofeo == 0 ? FontStyle.Regular : FontStyle.Bold))
        using (Font f2 = new Font("Segoe UI", 12f))
        {
            TextRenderer.DrawText(g, titulo, f1, new Point(Al + 4, 14), trofeo == 0 ? Color.White : ac, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, linea2, f2, new Rectangle(Al + 4, 52, An - Al - 30, 26), Color.FromArgb(215, 215, 220),
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    // círculo ESPECIAL (cuando ya tienes al menos un trofeo), estilo consola: borde oscuro, aro plateado
    // cortado en 4 partes y en medio una esfera plateada con el logo de DVD
    // el aro tiene 4 pedazos, uno por trofeo: se prende el de cada trofeo que tienes (1.º bronce,
    // 2.º plata, 3.º oro, 4.º diamante). Los apagados se quedan plateados.
    static readonly Color[] ColorPedazo = { Color.FromArgb(235, 140, 60), Color.FromArgb(215, 225, 245), Color.FromArgb(255, 205, 50), Color.FromArgb(120, 230, 255) };

    static void PintarCirculoEspecial(Graphics g, Rectangle c, int trofeos, double t, int nuevo)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 28, 32))) g.FillEllipse(b, c);   // borde oscuro
        Rectangle aro = Rectangle.Inflate(c, -4, -4);
        int prendidos = Math.Min(4, trofeos);
        float grueso = aro.Width * 0.17f;
        RectangleF medio = RectangleF.Inflate(aro, -grueso / 2, -grueso / 2);
        for (int i = 0; i < 4; i++)
        {
            float ini = 225 + i * 90 + 6, barrido = 90 - 12;   // 4 pedazos: arriba, derecha, abajo e izquierda
            bool on = i < prendidos;
            // el pedazo del trofeo NUEVO parpadea (prende y apaga) al principio y luego se queda prendido
            bool esNuevo = i == nuevo - 1;
            if (on && esNuevo && t < 1.6 && Math.Sin(t * 18) < 0) on = false;
            if (on)
            {
                // resplandor del pedazo prendido (respira; el nuevo brilla más)
                float br = (float)(0.7 + 0.3 * Math.Sin(t * 4 + i));
                if (esNuevo && t < 2.5) br = 1.6f;
                using (Pen glow = new Pen(Color.FromArgb((int)(110 * br), ColorPedazo[i]), grueso + 6))
                    g.DrawArc(glow, medio, ini, barrido);
                using (Pen p = new Pen(ColorPedazo[i], grueso)) g.DrawArc(p, medio, ini, barrido);
                using (Pen brillo = new Pen(Color.FromArgb(160, 255, 255, 255), grueso * 0.3f))
                    g.DrawArc(brillo, RectangleF.Inflate(medio, grueso * 0.2f, grueso * 0.2f), ini + 6, barrido - 12);
            }
            else
                using (Pen p = new Pen(Color.FromArgb(150, 150, 158), grueso)) g.DrawArc(p, medio, ini, barrido);
        }
        // anillo oscuro entre el aro y la esfera
        Rectangle hueco = Rectangle.Inflate(aro, -aro.Width * 17 / 100, -aro.Height * 17 / 100);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46))) g.FillEllipse(b, hueco);
        // esfera plateada con brillo
        Rectangle esfera = Rectangle.Inflate(hueco, -3, -3);
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddEllipse(esfera);
            using (PathGradientBrush pg = new PathGradientBrush(gp))
            {
                pg.CenterPoint = new PointF(esfera.X + esfera.Width * 0.38f, esfera.Y + esfera.Height * 0.32f);
                pg.CenterColor = Color.FromArgb(250, 250, 252);
                pg.SurroundColors = new[] { Color.FromArgb(150, 152, 160) };
                g.FillPath(pg, gp);
            }
        }
        Bitmap logo = DvdApp.MascaraLogo();
        if (logo == null) return;
        float k = Math.Min(esfera.Width * 0.86f / logo.Width, esfera.Height * 0.62f / logo.Height);
        float lw = logo.Width * k, lh = logo.Height * k;
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new float[][] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { 0f, 150 / 255f, 220 / 255f, 0, 1 } }));
            g.DrawImage(logo, new Rectangle((int)(esfera.Left + (esfera.Width - lw) / 2), (int)(esfera.Top + (esfera.Height - lh) / 2), (int)lw, (int)lh),
                0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
        }
    }

    // círculo: aro plateado, centro oscuro y el LOGO DE DVD del programa en azul
    static void PintarCirculo(Graphics g, Rectangle c)
    {
        using (LinearGradientBrush plata = new LinearGradientBrush(c, Color.FromArgb(240, 240, 245), Color.FromArgb(120, 120, 128), 45f))
            g.FillEllipse(plata, c);
        Rectangle dentro = Rectangle.Inflate(c, -6, -6);
        using (LinearGradientBrush oscuro = new LinearGradientBrush(dentro, Color.FromArgb(60, 60, 66), Color.FromArgb(18, 18, 22), 90f))
            g.FillEllipse(oscuro, dentro);
        Bitmap logo = DvdApp.MascaraLogo();
        if (logo == null) return;
        float k = Math.Min(dentro.Width * 0.78f / logo.Width, dentro.Height * 0.6f / logo.Height);
        float lw = logo.Width * k, lh = logo.Height * k;
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new float[][] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { 0f, 191 / 255f, 1f, 0, 1 } }));
            g.DrawImage(logo, new Rectangle((int)(dentro.Left + (dentro.Width - lw) / 2), (int)(dentro.Top + (dentro.Height - lh) / 2), (int)lw, (int)lh),
                0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
        }
    }
}

// EFECTO de TROFEO: una capa transparente encima de todo (no quita clics) donde sale disparado confeti del
// color del trofeo desde el aviso, con destellos que parpadean y, en el de oro, un flash de luz. Dura ~3.5 s.
class EfectoTrofeo
{
    class Pieza { public float X, Y, Vx, Vy, Rot, Vr, Tam; public Color Col; public bool Estrella; public float Fase; }

    static EfectoTrofeo actual;
    readonly OverlayWindow win = new OverlayWindow();
    readonly List<Pieza> piezas = new List<Pieza>();
    readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();
    readonly Stopwatch tiempo = Stopwatch.StartNew();
    readonly Rectangle pantalla;
    readonly PointF origen;
    readonly int trofeo;
    double ultimo;
    const double Dura = 3.6;

    public static void Lanzar(Rectangle aviso, int trofeo)
    {
        try { if (actual != null) actual.Terminar(); } catch { }
        try { actual = new EfectoTrofeo(aviso, trofeo); } catch { actual = null; }
    }

    static Color[] Colores(int t)
    {
        switch (t)
        {
            case 1: return new[] { Color.FromArgb(225, 140, 70), Color.FromArgb(255, 190, 130), Color.FromArgb(160, 90, 40), Color.White };
            case 2: return new[] { Color.FromArgb(205, 215, 230), Color.FromArgb(160, 200, 240), Color.FromArgb(240, 245, 255), Color.White };
            case 4: return new[] { Color.FromArgb(130, 235, 255), Color.FromArgb(200, 250, 255), Color.FromArgb(60, 170, 240), Color.White, Color.FromArgb(190, 140, 255), Color.FromArgb(255, 205, 60) };
            default: return new[] { Color.FromArgb(255, 205, 60), Color.FromArgb(255, 235, 140), Color.FromArgb(230, 160, 20), Color.White, Color.FromArgb(0, 191, 255) };
        }
    }

    EfectoTrofeo(Rectangle aviso, int trofeo)
    {
        this.trofeo = trofeo;
        pantalla = Screen.FromRectangle(aviso).Bounds;
        origen = new PointF(aviso.Left + aviso.Width / 2f - pantalla.Left, aviso.Top + aviso.Height / 2f - pantalla.Top);
        Random r = new Random();
        Color[] cols = Colores(trofeo);
        int n = trofeo == 4 ? 320 : trofeo == 3 ? 220 : trofeo == 2 ? 150 : 100;   // más confeti entre mejor el trofeo
        for (int i = 0; i < n; i++)
        {
            double ang = -Math.PI / 2 + (r.NextDouble() - 0.5) * Math.PI * 1.25;   // hacia arriba, abierto en abanico
            double vel = 380 + r.NextDouble() * 650;
            piezas.Add(new Pieza
            {
                X = origen.X + (float)(r.NextDouble() - 0.5) * aviso.Width * 0.8f, Y = origen.Y,
                Vx = (float)(Math.Cos(ang) * vel), Vy = (float)(Math.Sin(ang) * vel),
                Rot = (float)(r.NextDouble() * 360), Vr = (float)((r.NextDouble() - 0.5) * 720),
                Tam = 6 + (float)r.NextDouble() * 8, Col = cols[r.Next(cols.Length)],
                Estrella = r.NextDouble() < 0.22, Fase = (float)(r.NextDouble() * 6.28)
            });
        }
        win.Bounds = pantalla;
        win.EnsureSurface(pantalla.Width, pantalla.Height);
        reloj.Interval = 16;
        reloj.Tick += delegate { Cuadro(); };
        reloj.Start();
        Cuadro();
        win.Show();
    }

    void Terminar()
    {
        reloj.Stop(); reloj.Dispose();
        try { win.Hide(); win.FreeSurface(); win.Close(); win.Dispose(); } catch { }
        if (actual == this) actual = null;
    }

    void Cuadro()
    {
        double t = tiempo.Elapsed.TotalSeconds;
        if (t > Dura) { Terminar(); return; }
        float dt = (float)Math.Min(0.05, t - ultimo); ultimo = t;
        float alfaTodo = t < Dura - 0.7 ? 1f : (float)((Dura - t) / 0.7);
        using (Graphics g = Graphics.FromImage(win.Surface))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // ORO: flash de luz al principio
            double dFlash = trofeo == 4 ? 0.9 : 0.6;   // ORO y DIAMANTE (este más largo y azul hielo)
            if (trofeo >= 3 && t < dFlash)
            {
                float k = (float)(1 - t / dFlash);
                float rad = 120 + (float)t * (trofeo == 4 ? 1300 : 900);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(origen.X - rad, origen.Y - rad, rad * 2, rad * 2);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterColor = trofeo == 4 ? Color.FromArgb((int)(190 * k), 200, 250, 255) : Color.FromArgb((int)(170 * k), 255, 230, 140);
                        pg.SurroundColors = new[] { trofeo == 4 ? Color.FromArgb(0, 120, 220, 255) : Color.FromArgb(0, 255, 210, 80) };
                        g.FillPath(pg, gp);
                    }
                }
            }
            foreach (Pieza p in piezas)
            {
                p.Vy += 900 * dt;          // gravedad
                p.Vx *= (float)Math.Pow(0.35, dt); p.Vy *= (float)Math.Pow(0.6, dt);   // aire
                p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Rot += p.Vr * dt;
                if (p.Y > pantalla.Height + 30) continue;
                int a = (int)(255 * alfaTodo);
                if (p.Estrella)
                {
                    // destello de 4 puntas que parpadea
                    float br = (float)(0.5 + 0.5 * Math.Sin(t * 14 + p.Fase));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(a * (0.4 + 0.6 * br)), p.Col)))
                        Estrella(g, b, p.X, p.Y, p.Tam * (0.8f + br * 0.8f));
                }
                else
                {
                    // papelito que gira (se "aplana" al voltearse)
                    float esc = (float)Math.Abs(Math.Cos((p.Rot + p.Fase * 40) * Math.PI / 180));
                    g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.Rot);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, p.Col)))
                        g.FillRectangle(b, -p.Tam / 2, -p.Tam * 0.3f * (0.25f + esc), p.Tam, p.Tam * 0.6f * (0.25f + esc));
                    g.ResetTransform();
                }
            }
        }
        win.Present(pantalla.Left, pantalla.Top);
    }

    internal static void Estrella(Graphics g, Brush b, float x, float y, float r)
    {
        PointF[] pts = new PointF[8];
        for (int i = 0; i < 8; i++)
        {
            double ang = i * Math.PI / 4;
            float rr = i % 2 == 0 ? r : r * 0.28f;
            pts[i] = new PointF(x + (float)(Math.Cos(ang) * rr), y + (float)(Math.Sin(ang) * rr));
        }
        g.FillPolygon(b, pts);
    }
}

// FONDO ANIMADO: los DVDs se dibujan en la capa del fondo de pantalla de Windows (la ventana "WorkerW"
// que está detrás de los iconos del escritorio), encima de una copia de tu fondo de pantalla.
// Al quitarlo, Windows vuelve a poner tu fondo tal cual (no se cambia nada de tu configuración).
static class FondoAnimado
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string clase, string titulo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr padre, IntPtr despues, string clase, string titulo);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint ms, out IntPtr r);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDCEx(IntPtr h, IntPtr clip, uint flags);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint accion, uint p, System.Text.StringBuilder pv, uint ini);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")] static extern bool SystemParametersInfoTexto(uint accion, uint p, string pv, uint ini);
    [DllImport("user32.dll")] static extern int GetSysColor(int i);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("msimg32.dll")] static extern bool AlphaBlend(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, int blend);

    static IntPtr workerw;
    static Bitmap fondo, buffer;          // copia de tu fondo de pantalla y el cuadro que se arma cada vez
    static Graphics gBuffer;
    static string fondoDe = ""; static DateTime fondoFecha; static double revisado = -100;
    static bool activo;
    static string Marca { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "fondo_puesto"); } }

    // la ventana del fondo: se le pide a Progman que la cree (mensaje 0x052C) y se busca
    static IntPtr BuscarWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;
        IntPtr r;
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 1000, out r);   // (0xD, 1): así también la crea Windows 11 24H2
        // Windows 11 24H2: la capa del fondo va DENTRO de Progman, detrás de los iconos
        IntPtr encontrada = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (encontrada != IntPtr.Zero) return encontrada;
        // Windows 10 / 11 de antes: los iconos se mudan a una WorkerW y la del fondo es la que va justo después
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero && h != progman)
                encontrada = FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
            return encontrada == IntPtr.Zero;
        }, IntPtr.Zero);
        return encontrada;
    }

    static string RutaFondo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(520);
        SystemParametersInfo(0x0073 /*SPI_GETDESKWALLPAPER*/, (uint)sb.Capacity, sb, 0);
        string p = sb.ToString();
        if (p.Length > 0 && File.Exists(p)) return p;
        string t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
        return File.Exists(t) ? t : p;
    }

    // dibuja tu fondo de pantalla como lo pone Windows (rellenar, ajustar, expandir o centrar)
    static void CargarFondo(int w, int h)
    {
        string ruta = RutaFondo();
        DateTime fecha = File.Exists(ruta) ? File.GetLastWriteTimeUtc(ruta) : DateTime.MinValue;
        if (fondo != null && fondo.Width == w && fondo.Height == h && ruta == fondoDe && fecha == fondoFecha) return;
        if (fondo != null) fondo.Dispose();
        fondo = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        fondoDe = ruta; fondoFecha = fecha;
        int c = GetSysColor(1 /*COLOR_DESKTOP*/);
        using (Graphics g = Graphics.FromImage(fondo))
        {
            g.Clear(Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF));
            if (!File.Exists(ruta)) return;
            string estilo = "10";
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    if (k != null) estilo = Convert.ToString(k.GetValue("WallpaperStyle", "10"));
            }
            catch { }
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Image img = Image.FromStream(fs))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float kx = w / (float)img.Width, ky = h / (float)img.Height, k;
                    RectangleF d;
                    if (estilo == "2") d = new RectangleF(0, 0, w, h);                                       // expandir
                    else if (estilo == "0") d = new RectangleF((w - img.Width) / 2f, (h - img.Height) / 2f, img.Width, img.Height);   // centrar
                    else
                    {
                        k = estilo == "6" ? Math.Min(kx, ky) : Math.Max(kx, ky);                            // ajustar / rellenar
                        d = new RectangleF((w - img.Width * k) / 2f, (h - img.Height * k) / 2f, img.Width * k, img.Height * k);
                    }
                    g.DrawImage(img, d);
                }
            }
            catch { }
        }
    }

    // nuestra propia ventanita metida DENTRO de la capa del fondo (así lo hacen los programas de fondos animados)
    class Hija : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014 /*WM_ERASEBKGND*/) { m.Result = (IntPtr)1; return; }   // sin parpadeo
            if (m.Msg == 0x0021 /*WM_MOUSEACTIVATE*/) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
            base.WndProc(ref m);
        }
    }
    static Hija hija;
    static Rectangle hijaTam;   // la pantalla para la que se creó (si cambia la resolución, se vuelve a crear)
    static double reintentar = -100;

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repintar);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr despues, int x, int y, int w, int hh, uint flags);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);

    static bool PrepararHija(Rectangle s, double now)
    {
        if (hija != null && hija.Handle != IntPtr.Zero && IsWindow(hija.Handle) && IsWindow(workerw) && hijaTam == s) return true;
        if (hija != null && hijaTam != s) reintentar = -100;   // cambió la resolución: se vuelve a crear ya, con el tamaño nuevo
        if (now - reintentar < 3) return false;   // si falló, se vuelve a intentar cada 3 segundos (no en cada cuadro)
        reintentar = now;
        if (hija != null) { try { hija.DestroyHandle(); } catch { } hija = null; }
        workerw = BuscarWorkerW();
        if (workerw == IntPtr.Zero) return false;   // no se encontró la capa del fondo: el DVD sigue encima de todo
        // Windows 11 24H2: la WorkerW está DENTRO de Progman junto a los iconos (SHELLDLL_DefView). Ahí la
        // ventana del DVD va directo en Progman, justo debajo de los iconos (dentro de la WorkerW no se ve).
        IntPtr padre = workerw, debajoDe = IntPtr.Zero;
        IntPtr pm = GetParent(workerw);
        if (pm != IntPtr.Zero)
        {
            IntPtr dv = FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero) { padre = pm; debajoDe = dv; }
        }
        RECT r; GetWindowRect(padre, out r);
        try
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "DVD Rebote fondo";
            cp.Parent = padre;
            cp.Style = 0x40000000 /*WS_CHILD*/ | 0x10000000 /*WS_VISIBLE*/ | 0x04000000 /*WS_CLIPSIBLINGS*/;
            // EN CAPAS (WS_EX_LAYERED): en Windows 11 24H2 la capa del fondo no guarda dibujos normales
            // (Progman tiene WS_EX_NOREDIRECTIONBITMAP); una ventana en capas sí tiene su propia imagen.
            // Para ventanas hijas en capas el .exe declara Windows 8+ en su manifiesto (dvd.manifest).
            cp.ExStyle = 0x08000000 /*WS_EX_NOACTIVATE*/ | 0x80000 /*WS_EX_LAYERED*/ | 0x20 /*WS_EX_TRANSPARENT*/;
            cp.X = s.Left - r.L; cp.Y = s.Top - r.T; cp.Width = s.Width; cp.Height = s.Height;   // la pantalla principal, dentro de la capa
            hija = new Hija();
            hija.CreateHandle(cp);
            hijaTam = s;
            // en capas "con opacidad" (no UpdateLayeredWindow): así Windows sí la muestra en la capa del fondo
            SetLayeredWindowAttributes(hija.Handle, 0, 255, 2 /*LWA_ALPHA*/);
            if (debajoDe != IntPtr.Zero)   // en el orden: iconos → DVD → WorkerW
                SetWindowPos(hija.Handle, debajoDe, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /*NOSIZE|NOMOVE|NOACTIVATE*/);
        }
        catch { hija = null; return false; }
        return true;
    }

    // un cuadro: tu fondo + los DVDs (la superficie del DVD, ya con transparencia) → la ventana del fondo.
    // Devuelve false si no se pudo (entonces el DVD se dibuja normal, encima de todo).
    public static bool Pintar(IntPtr superficieDc, Rectangle s, double now, bool negro)
    {
        try { if (!PrepararHija(s, now)) return false; }
        catch { return false; }
        if (!activo)
        {
            activo = true;
            try { Directory.CreateDirectory(Path.GetDirectoryName(Marca)); File.WriteAllText(Marca, DateTime.Now.ToString("o")); } catch { }
        }
        if (!negro && (now - revisado > 3 || fondo == null || fondo.Width != s.Width || fondo.Height != s.Height)) { revisado = now; CargarFondo(s.Width, s.Height); }
        PrepararBuffer(s.Width, s.Height);
        gBuffer.CompositingMode = CompositingMode.SourceCopy;
        if (negro) gBuffer.Clear(Color.Black);   // como el protector de pantalla clásico
        else gBuffer.DrawImageUnscaled(fondo, 0, 0);
        gBuffer.Flush(FlushIntention.Sync);
        // los DVDs encima (con su transparencia); el resultado queda opaco en todos lados
        AlphaBlend(bufDc, 0, 0, s.Width, s.Height, superficieDc, 0, 0, s.Width, s.Height, 0x01FF0000 /*AC_SRC_OVER, 255, AC_SRC_ALPHA*/);
        IntPtr dc = GetDC(hija.Handle);
        if (dc == IntPtr.Zero || !BitBlt(dc, 0, 0, s.Width, s.Height, bufDc, 0, 0, 0x00CC0020 /*SRCCOPY*/))
        {
            if (dc != IntPtr.Zero) ReleaseDC(hija.Handle, dc);
            try { hija.DestroyHandle(); } catch { }
            hija = null; reintentar = now;
            return false;
        }
        ReleaseDC(hija.Handle, dc);
        return true;
    }

    // el cuadro se arma en una superficie de 32 bits CON canal de transparencia (DIB). Si no, la parte
    // de tu fondo quedaba con transparencia 0 y Windows la mostraba negra (solo se veían los DVDs).
    static IntPtr bufDc = IntPtr.Zero, bufBmp = IntPtr.Zero, bufViejo = IntPtr.Zero;
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BMIH bi, uint uso, out IntPtr bits, IntPtr sec, uint off);
    [StructLayout(LayoutKind.Sequential)]
    struct BMIH { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biX, biY, biClrUsed, biClrImportant; }

    static void PrepararBuffer(int w, int h)
    {
        if (buffer != null && buffer.Width == w && buffer.Height == h) return;
        LiberarBuffer();
        BMIH bi = new BMIH(); bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
        IntPtr pantalla = GetDC(IntPtr.Zero);
        bufDc = CreateCompatibleDC(pantalla);
        ReleaseDC(IntPtr.Zero, pantalla);
        IntPtr bits;
        bufBmp = CreateDIBSection(bufDc, ref bi, 0, out bits, IntPtr.Zero, 0);
        bufViejo = SelectObject(bufDc, bufBmp);
        buffer = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        gBuffer = Graphics.FromImage(buffer);
    }

    static void LiberarBuffer()
    {
        if (gBuffer != null) { gBuffer.Dispose(); gBuffer = null; }
        if (buffer != null) { buffer.Dispose(); buffer = null; }
        if (bufDc != IntPtr.Zero) { SelectObject(bufDc, bufViejo); DeleteObject(bufBmp); DeleteDC(bufDc); bufDc = IntPtr.Zero; }
    }

    // quita la ventana del DVD de la capa del fondo: tu fondo vuelve a verse tal cual
    public static void Quitar()
    {
        if (!activo) return;   // se llama en cada cuadro cuando el modo está apagado: no hace nada
        activo = false;
        if (hija != null) { try { hija.DestroyHandle(); } catch { } hija = null; }
        try { LiberarBuffer(); } catch { }
        reintentar = -100;
        // no se "vuelve a poner" el fondo con SPI_SETDESKWALLPAPER: al quitar nuestra ventana Windows
        // vuelve a mostrar tu fondo solo (y así no se rompe la presentación de fondos ni Windows Spotlight)
        try { File.Delete(Marca); } catch { }
    }

    public static void ArreglarSiQuedo() { if (File.Exists(Marca)) { activo = true; Quitar(); } }
}

// Cambia la "identidad" (AppUserModelID) de una ventana: la barra de tareas junta en un botón
// solo las ventanas con la misma identidad, así cada tipo de ventana tiene su botón con su icono.
static class AppIdVentana
{
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        int GetCount(out uint n);
        int GetAt(uint i, out PropKey k);
        int GetValue(ref PropKey k, out PropVar v);
        int SetValue(ref PropKey k, ref PropVar v);
        int Commit();
    }
    [StructLayout(LayoutKind.Sequential)] struct PropKey { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVar { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ps);

    public static void Poner(IntPtr ventana, string id)
    {
        try
        {
            Guid iid = typeof(IPropertyStore).GUID;
            IPropertyStore ps;
            if (SHGetPropertyStoreForWindow(ventana, ref iid, out ps) != 0 || ps == null) return;
            PropKey k = new PropKey { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
            PropVar v = new PropVar { vt = 31 /*VT_LPWSTR*/, p = Marshal.StringToCoTaskMemUni(id) };
            ps.SetValue(ref k, ref v);
            ps.Commit();
            Marshal.FreeCoTaskMem(v.p);
            Marshal.ReleaseComObject(ps);
        }
        catch { }
    }
}

class VentanaEstatica : Form
{
    readonly Estatica estatica = new Estatica();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Panel arriba = new Panel();
    readonly BotonEstatica aceptar;
    readonly Bitmap icono;
    bool aceptado;

    public VentanaEstatica()
    {
        Text = "NO ESCAPE";   // al lado del icono de la consola
        // icono de la consola (imageres,263) en la barra de título
        using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("icono_cmd.ico")) Icon = new Icon(s);
        ShowIcon = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = true;   // sale en la barra de tareas con la X roja antigua
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(380, 172);
        BackColor = SystemColors.Control;
        Font = new Font("Segoe UI", 9f);
        icono = new Icon(SystemIcons.Error, 32, 32).ToBitmap();

        // parte de arriba: estática + X roja + "No escape"
        arriba.Bounds = new Rectangle(0, 0, 380, 172);   // toda la ventana (el botón va encima)
        PonerDobleBuffer(arriba);
        arriba.Paint += PintarArriba;
        Controls.Add(arriba);

        // botón "Aceptar" también con estática
        aceptar = new BotonEstatica(estatica);
        aceptar.Text = "Aceptar";
        aceptar.Bounds = new Rectangle(278, 134, 88, 26);
        aceptar.Click += delegate { aceptado = true; DialogResult = DialogResult.OK; Close(); };
        Controls.Add(aceptar);
        aceptar.BringToFront();   // encima de la estática de fondo
        AcceptButton = aceptar;

        timer.Interval = 40;
        timer.Tick += delegate { estatica.Avanzar(); arriba.Invalidate(); aceptar.Invalidate(); MoverCuadrosNuevos(); PintarEstaticaEnCuadros(); TemblarVentana(); EstaticaEnTitulo(); };
        timer.Start();
        Shown += delegate { PonerIconos(); System.Media.SystemSounds.Hand.Play(); aceptar.Focus(); };
    }

    // Dos iconos distintos: en el título el de cmd (chico) y en la barra de tareas / Alt+Tab
    // la X roja de error antigua (user32,3 = recurso 103), que es el icono grande de la ventana.
    [DllImport("user32.dll")] static extern IntPtr LoadImage(IntPtr h, IntPtr nombre, uint tipo, int cx, int cy, uint fl);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string n);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    static IntPtr errorViejo, cmdChico, advertenciaVieja;
    void PonerIconos()
    {
        try
        {
            if (errorViejo == IntPtr.Zero) errorViejo = LoadImage(GetModuleHandle("user32.dll"), (IntPtr)103, 1 /*IMAGE_ICON*/, 32, 32, 0);
            if (cmdChico == IntPtr.Zero) cmdChico = new Icon(Icon, 16, 16).Handle;
            SendMessage(Handle, 0x0080 /*WM_SETICON*/, (IntPtr)0 /*ICON_SMALL*/, cmdChico);
            if (errorViejo != IntPtr.Zero) SendMessage(Handle, 0x0080, (IntPtr)1 /*ICON_BIG*/, errorViejo);
        }
        catch { }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); PonerIconos(); }

    // FixedDialog esconde el icono del título (marco de diálogo); se le quita ese marco para que se vea
    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle &= ~0x1 /*WS_EX_DLGMODALFRAME*/; return cp; }
    }

    internal static void PonerDobleBuffer(Control c)
    {
        typeof(Control).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, c, new object[] { true });
    }

    // la ventana "NO ESCAPE" también tiembla todo el tiempo y de repente da un jalón fuerte
    // (igual que los cuadros de Error). Se guarda cuánto se corrió para que no se vaya caminando.
    Point corridoVentana = Point.Empty;
    void TemblarVentana()
    {
        if (!IsHandleCreated || !Visible || aceptado) return;
        Point ahora = rndAviso.Next(30) == 0
            ? new Point(rndAviso.Next(-16, 17), rndAviso.Next(-9, 10))    // jalón fuerte
            : new Point(rndAviso.Next(-4, 5), rndAviso.Next(-3, 4));      // temblor de siempre
        RECTA r; GetWindowRect(Handle, out r);
        SetWindowPos(Handle, IntPtr.Zero, r.L - corridoVentana.X + ahora.X, r.T - corridoVentana.Y + ahora.Y, 0, 0, 0x0001 | 0x0004 | 0x0010);
        corridoVentana = ahora;
    }

    // la barra de título también lleva estática (una capa encima que deja pasar los clics a la X)
    CapaEstatica capaTitulo;
    void EstaticaEnTitulo()
    {
        if (!IsHandleCreated || !Visible) return;
        if (capaTitulo == null) { capaTitulo = new CapaEstatica(estatica); capaTitulo.Show(); }
        RECTA r; GetWindowRect(Handle, out r);
        int arribaCliente = PointToScreen(Point.Empty).Y;
        int alto = Math.Max(1, arribaCliente - r.T);
        // la X de cerrar siempre en rojo (como cuando pasas el mouse): un cuadrito rojo encima de la X de verdad
        if (xRoja == null) { xRoja = new XRoja(); xRoja.Show(); }
        Rectangle rx = RectanguloCerrar();
        IntPtr encima = GetWindow(Handle, 3 /*GW_HWNDPREV*/);
        bool enOrden = encima == capaTitulo.Handle && GetWindow(capaTitulo.Handle, 3) == xRoja.Handle;
        if (enOrden)
        {
            SetWindowPos(capaTitulo.Handle, IntPtr.Zero, r.L, r.T, r.R - r.L, alto, 0x0010 | 0x0004);
            SetWindowPos(xRoja.Handle, IntPtr.Zero, rx.X, rx.Y, rx.Width, rx.Height, 0x0010 | 0x0004);
        }
        else
        {
            IntPtr arribaDe = (encima == IntPtr.Zero || encima == capaTitulo.Handle || encima == xRoja.Handle) ? (IntPtr)(-1) : encima;
            SetWindowPos(xRoja.Handle, arribaDe, rx.X, rx.Y, rx.Width, rx.Height, 0x0010);
            SetWindowPos(capaTitulo.Handle, xRoja.Handle, r.L, r.T, r.R - r.L, alto, 0x0010);   // capa justo debajo de la X roja
        }
        capaTitulo.Invalidate();
    }

    XRoja xRoja;
    // dónde está la X de cerrar de esta ventana (WM_GETTITLEBARINFOEX, el botón 5 es cerrar)
    Rectangle RectanguloCerrar()
    {
        IntPtr m = Marshal.AllocHGlobal(140);
        try
        {
            for (int i = 0; i < 140; i += 4) Marshal.WriteInt32(m, i, 0);
            Marshal.WriteInt32(m, 0, 140);
            SendMessage(Handle, 0x033F, IntPtr.Zero, m);
            int l = Marshal.ReadInt32(m, 124), t = Marshal.ReadInt32(m, 128), rr = Marshal.ReadInt32(m, 132), b = Marshal.ReadInt32(m, 136);
            if (rr - l < 4 || b - t < 4)
            {   // por si no responde: la esquina de arriba a la derecha
                RECTA w; GetWindowRect(Handle, out w);
                return new Rectangle(w.R - 47, w.T + 1, 46, Math.Max(20, PointToScreen(Point.Empty).Y - w.T - 1));
            }
            return new Rectangle(l, t, rr - l, b - t);
        }
        finally { Marshal.FreeHGlobal(m); }
    }

    void PintarArriba(object s, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        estatica.Pintar(g, arriba.ClientRectangle);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(icono, 26, 40, 32, 32);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using (Font f = new Font("Segoe UI", 15f, FontStyle.Bold))
{
            // "NO ESCAPE" con letras cursed, en negro y SIN brillo alrededor (más alto el renglón para los acentos raros)
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                g.DrawString("N̶̨̘̲̫̲͌ͦ̂͆̽̽̊Ȯ̧̗̝̹̮͍͔̟̔ Ȅ̶̪͔̤͍͇Ş͖̺̋C̵͊̋Ā̟̥̣͓̪̓̽͆̽̋͊ͩP̲̹̯̠͈͒̉E̶̴̘̝̞̜̞̘͖̊ͩ̔͌ͅ", f, Brushes.Black, new RectangleF(74, 12, 300, 92), sf);
        }
    }

    // al cerrar el DVD: se cierra todo sin anotar nada ni soltar DVDs
    public void CerrarPorSalida()
    {
        aceptado = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    // cerrar todo (ventana y ventanitas de Error) sin los 500 DVDs
    public void CerrarSinPremio()
    {
        RegistroSecret.Anotar("CERRADO", "Se cerró todo con el .bat (sin los 500 DVDs)");
        aceptado = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    // No se deja cerrar con la X ni con Alt+F4: sale "Error" con el signo de advertencia.
    // (Sí se cierra si se cierra el DVD o se apaga Windows.)
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // solo se deja cerrar si se cierra el DVD o se apaga Windows
        bool permitido = e.CloseReason == CloseReason.ApplicationExitCall || e.CloseReason == CloseReason.WindowsShutDown;
        if (!aceptado && !permitido)
        {
            e.Cancel = true;
            AbrirAviso();
            return;
        }
        base.OnFormClosing(e);
    }

    // ---- ventanitas de "Error": el cuadro de mensaje REAL de Windows (con advertencia). Cada vez que
    //      cierras uno salen 5 más, un poco corridos para que se vean amontonados (con un tope). ----
    const int MaxAvisos = 31;   // 1 + 5 + 25
    int avisosCreados;   // cuántas ventanitas de Error salieron en esta ronda (tope: MaxAvisos)
    int avisosAbiertosN; // cuántas están abiertas ahorita
    bool cerrandoTodo;
    readonly List<IntPtr> avisosAbiertos = new List<IntPtr>();
    readonly Random rndAviso = new Random();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr h, string texto, string titulo, uint tipo);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint hilo, EnumProc f, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECTA r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr d, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RECTA { public int L, T, R, B; }

    int cierres;   // cuántas ventanitas de Error has cerrado

    // maldito = el aviso especial de los 10 cierres (no cuenta para el tope)
    void AbrirAviso(bool maldito = false)
    {
        lock (avisosAbiertos)
        {
            if (cerrandoTodo || IsDisposed) return;
            if (!maldito && avisosCreados >= MaxAvisos) return;
            avisosCreados++;
            avisosAbiertosN++;
        }
        // renglones vacíos arriba y abajo para que quepan los acentos raros
        RegistroSecret.Anotar(maldito ? "MALDITO" : "ERROR", maldito ? "Salió el mensaje \"No intentes escapar, hermano.\"" : "Salió una ventanita de Error");
        string texto = maldito ? "\n\n\n" + Maldecir("No intentes escapar, hermano.", 6, 12) + "\n\n\n"
                               : "\n\n" + Maldecir("Error", 5, 10) + "\n\n";
        string titulo = Maldecir("Error", 2, 4);   // el título también, con menos para que quepa
        Thread t = new Thread(delegate ()
        {
            // MB_OK | MB_ICONWARNING | MB_TOPMOST | MB_SETFOREGROUND
            // sin dueño: así cada cuadro sale también en la barra de tareas (con la advertencia antigua)
            IntPtr hDueno = IntPtr.Zero;
            // gancho en este hilo: al arrastrar el cuadro, su estática se mueve en el mismo instante
            HookProc gancho = delegate (int code, IntPtr wp, IntPtr lp)
            {
                if (code >= 0)
                {
                    CWPSTRUCT c = (CWPSTRUCT)Marshal.PtrToStructure(lp, typeof(CWPSTRUCT));
                    IntPtr capa;
                    if (c.message == 0x0216 /*WM_MOVING*/ && capaDe.TryGetValue(c.hwnd, out capa))
                    {
                        RECTA nuevo = (RECTA)Marshal.PtrToStructure(c.lParam, typeof(RECTA));
                        SetWindowPos(capa, IntPtr.Zero, nuevo.L, nuevo.T, nuevo.R - nuevo.L, nuevo.B - nuevo.T, 0x0010 | 0x0004);
                    }
                }
                return CallNextHookEx(IntPtr.Zero, code, wp, lp);
            };
            IntPtr hGancho = SetWindowsHookEx(4 /*WH_CALLWNDPROC*/, gancho, IntPtr.Zero, GetCurrentThreadId());
            // con el signo de advertencia VIEJO (el clásico de user32.dll), no el nuevo de Windows 11
            Program.MensajeConIcono(hDueno, texto, titulo, 0x00000000 | 0x00040000 | 0x00010000, 101);
            lock (avisosAbiertos) avisosAbiertosN--;   // este cuadro ya se cerró
            if (hGancho != IntPtr.Zero) UnhookWindowsHookEx(hGancho);
            GC.KeepAlive(gancho);
            // al cerrarlo salen 5 más; al décimo cierre sale el mensaje maldito
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (cerrandoTodo) return;
                    // si ya salieron todas las de la ronda y cerraste la última: se reinicia la cuenta
                    // (así la X de "NO ESCAPE" vuelve a sacar ventanitas de Error)
                    lock (avisosAbiertos)
                        if (avisosCreados >= MaxAvisos) { if (avisosAbiertosN <= 0) avisosCreados = 0; return; }
                    cierres++;
                    if (cierres == 2) AbrirAviso(true);   // al 2.º cierre: "No intentes escapar, hermano." (con advertencia)
                    for (int i = 0; i < 5; i++) AbrirAviso();
                });
            }
            catch { }
        });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    // Los cuadros de mensaje de este programa (se buscan entre todas las ventanas; el título va "cursed")
    List<IntPtr> CuadrosError()
    {
        List<IntPtr> o = new List<IntPtr>();
        uint yo = (uint)Process.GetCurrentProcess().Id;
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != yo || !IsWindowVisible(h)) return true;
            System.Text.StringBuilder c = new System.Text.StringBuilder(32); GetClassName(h, c, 32);
            System.Text.StringBuilder tt = new System.Text.StringBuilder(64); GetWindowText(h, tt, 64);
            if (c.ToString() == "#32770" && tt.Length > 0 && tt[0] == 'E') o.Add(h);
            return true;
        }, IntPtr.Zero);
        return o;
    }

    // cada cuadro nuevo se corre un poco al azar (si no, salen todos encimados)
    readonly HashSet<IntPtr> yaMovidos = new HashSet<IntPtr>();
    void MoverCuadrosNuevos()
    {
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        foreach (IntPtr h in CuadrosError())
        {
            if (!yaMovidos.Add(h)) continue;
            // en la barra de tareas: el signo de advertencia antiguo (user32,1 = recurso 101)
            if (advertenciaVieja == IntPtr.Zero) advertenciaVieja = LoadImage(GetModuleHandle("user32.dll"), (IntPtr)101, 1, 32, 32, 0);
            if (advertenciaVieja != IntPtr.Zero) SendMessage(h, 0x0080 /*WM_SETICON*/, (IntPtr)1 /*ICON_BIG*/, advertenciaVieja);
            // su propio botón en la barra (si no, Windows las junta con NO ESCAPE y sale la X roja)
            AppIdVentana.Poner(h, "DvdRebote.Error");
            RECTA r; GetWindowRect(h, out r);
            int w = r.R - r.L, hh = r.B - r.T;
            int x = Math.Max(area.Left, Math.Min(area.Right - w, r.L + rndAviso.Next(-280, 281)));
            int y = Math.Max(area.Top, Math.Min(area.Bottom - hh, r.T + rndAviso.Next(-200, 201)));
            SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
        }
    }

    void CerrarAvisos()
    {
        cerrandoTodo = true;
        foreach (IntPtr h in CuadrosError()) PostMessage(h, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);
        capaDe.Clear();
        foreach (CapaEstatica c in capas.Values) c.Close();
        capas.Clear();
    }

    // letras "cursed": a cada letra se le encima un montón de acentos raros arriba y abajo.
    // No se usan los que tachan la letra por en medio, para que el texto se siga leyendo bien.
    static readonly char[] marcasArriba = ArmarMarcas(new int[] { 0x300, 0x315, 0x33D, 0x344, 0x346, 0x346, 0x34A, 0x34C, 0x350, 0x352, 0x357, 0x357, 0x35B, 0x35B, 0x363, 0x36F });
    static readonly char[] marcasAbajo = ArmarMarcas(new int[] { 0x316, 0x319, 0x31C, 0x333, 0x339, 0x33C, 0x345, 0x345, 0x347, 0x349, 0x34D, 0x34E, 0x353, 0x356, 0x359, 0x35A });
    static char[] ArmarMarcas(int[] rangos)
    {
        List<char> l = new List<char>();
        for (int i = 0; i < rangos.Length; i += 2) for (int c = rangos[i]; c <= rangos[i + 1]; c++) l.Add((char)c);
        return l.ToArray();
    }
    static readonly Random rndMaldito = new Random();
    static string Maldecir(string s, int min, int max)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        lock (rndMaldito)
            foreach (char ch in s)
            {
                b.Append(ch);
                if (ch == ' ' || ch == ',' || ch == '.') continue;
                int n = min + rndMaldito.Next(max - min + 1);
                for (int i = 0; i < n; i++)
                {
                    char[] m = (i % 2 == 0) ? marcasArriba : marcasAbajo;
                    b.Append(m[rndMaldito.Next(m.Length)]);
                }
            }
        return b.ToString();
    }
    // Estática encima de cada ventanita de Error (una capa que deja pasar los clics)
    readonly Dictionary<IntPtr, CapaEstatica> capas = new Dictionary<IntPtr, CapaEstatica>();
    readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, IntPtr> capaDe = new System.Collections.Concurrent.ConcurrentDictionary<IntPtr, IntPtr>();
    delegate IntPtr HookProc(int code, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc f, IntPtr mod, uint hilo);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wp, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] struct CWPSTRUCT { public IntPtr lParam, wParam; public uint message; public IntPtr hwnd; }
    void PintarEstaticaEnCuadros()
    {
        if (cerrandoTodo) return;
        List<IntPtr> vivos = CuadrosError();
        foreach (IntPtr h in new List<IntPtr>(capas.Keys))
            if (!vivos.Contains(h)) { IntPtr x; capaDe.TryRemove(h, out x); capas[h].Close(); capas.Remove(h); corrido.Remove(h); }
        foreach (IntPtr h in vivos)
        {
            CapaEstatica c;
            if (!capas.TryGetValue(h, out c)) { c = new CapaEstatica(estatica); capas[h] = c; c.Show(); capaDe[h] = c.Handle; }
            Temblar(h);
            PegarCapa(h, c);
            c.Opacity = 0.32 + rndAviso.NextDouble() * 0.28;   // la estática parpadea
            c.Invalidate();
        }
    }

    // pone la capa de estática exactamente encima de su cuadro (y justo arriba de él, no de los demás)
    void PegarCapa(IntPtr h, CapaEstatica c)
    {
        RECTA r; GetWindowRect(h, out r);
        IntPtr arriba = GetWindow(h, 3 /*GW_HWNDPREV*/);
        if (arriba == c.Handle) SetWindowPos(c.Handle, IntPtr.Zero, r.L, r.T, r.R - r.L, r.B - r.T, 0x0010 | 0x0004);
        else SetWindowPos(c.Handle, arriba == IntPtr.Zero ? (IntPtr)(-1) : arriba, r.L, r.T, r.R - r.L, r.B - r.T, 0x0010);
    }

    // Windows avisa al instante cada vez que una ventana de este programa se mueve: así la estática
    // de la barra de título y la de los cuadros de Error siguen a su ventana sin quedarse atrás.
    delegate void WinEventProc(IntPtr hook, uint evento, IntPtr hwnd, int idObjeto, int idHijo, uint hilo, uint tiempo);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc f, uint pid, uint hilo, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    WinEventProc alMoverse;   // se guarda para que no se lo lleve el recolector de basura
    IntPtr ganchoMover = IntPtr.Zero;

    // Mientras arrastras la ventana, Windows avisa ANTES de moverla (WM_MOVING) con el lugar nuevo:
    // la estática de la barra de título se mueve ahí mismo, al mismo tiempo que la ventana.
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0216 /*WM_MOVING*/ && capaTitulo != null && !capaTitulo.IsDisposed)
        {
            RECTA nuevo = (RECTA)Marshal.PtrToStructure(m.LParam, typeof(RECTA));
            RECTA ahora; GetWindowRect(Handle, out ahora);
            int alto = Math.Max(1, PointToScreen(Point.Empty).Y - ahora.T);
            SetWindowPos(capaTitulo.Handle, IntPtr.Zero, nuevo.L, nuevo.T, nuevo.R - nuevo.L, alto, 0x0010 | 0x0004);
        }
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        alMoverse = delegate (IntPtr hook, uint evento, IntPtr hwnd, int idObjeto, int idHijo, uint hilo, uint tiempo)
        {
            if (idObjeto != 0 /*OBJID_WINDOW*/ || IsDisposed) return;
            if (hwnd == Handle) { EstaticaEnTitulo(); return; }
            CapaEstatica c;
            if (capas.TryGetValue(hwnd, out c) && !c.IsDisposed) PegarCapa(hwnd, c);
        };
        // EVENT_OBJECT_LOCATIONCHANGE, solo de este programa, sin meterse en otros procesos
        ganchoMover = SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, alMoverse, (uint)Process.GetCurrentProcess().Id, 0, 0 /*WINEVENT_OUTOFCONTEXT*/);
    }

    // efecto de error: los cuadros tiemblan todo el tiempo (poquito) y de repente dan un jalón fuerte,
    // como si la tele perdiera la señal. Se guarda cuánto se corrió cada uno para no irse "caminando".
    readonly Dictionary<IntPtr, Point> corrido = new Dictionary<IntPtr, Point>();
    void Temblar(IntPtr h)
    {
        Point antes;
        if (!corrido.TryGetValue(h, out antes)) antes = Point.Empty;
        Point ahora = rndAviso.Next(30) == 0
            ? new Point(rndAviso.Next(-16, 17), rndAviso.Next(-9, 10))    // jalón fuerte
            : new Point(rndAviso.Next(-4, 5), rndAviso.Next(-3, 4));      // temblor de siempre
        RECTA r; GetWindowRect(h, out r);
        // se quita lo que se había corrido y se pone lo nuevo (si lo arrastras, sigue a donde lo dejes)
        SetWindowPos(h, IntPtr.Zero, r.L - antes.X + ahora.X, r.T - antes.Y + ahora.Y, 0, 0, 0x0001 | 0x0004 | 0x0010);
        corrido[h] = ahora;
    }
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (ganchoMover != IntPtr.Zero) { UnhookWinEvent(ganchoMover); ganchoMover = IntPtr.Zero; }
        if (capaTitulo != null) capaTitulo.Close();
        if (xRoja != null) xRoja.Close();
        CerrarAvisos();   // al darle "Aceptar" (o cerrar el DVD) se van también las ventanitas de Error
        timer.Stop();
        estatica.Dispose();
        icono.Dispose();
        base.OnFormClosed(e);
    }
}

// Capa semitransparente con estática que se pone encima de una ventanita de Error.
// Los clics la atraviesan, así que el botón "Aceptar" de abajo sigue funcionando.
// Código QR de verdad (corrección L, modo bytes, máscara 0). Usa el tamaño más chico donde quepa:
// versión 2 = 25x25 (hasta 32 letras), 3 = 29x29 (53), 4 = 33x33 (78). Todas de un solo bloque.
// Devuelve [x, y] = true para los cuadritos oscuros.
static class CodigoQR
{
    static int N, DatosCW, EccCW;
    public static bool[,] Crear(string texto)
    {
        byte[] t = System.Text.Encoding.UTF8.GetBytes(texto);
        int version = t.Length <= 32 ? 2 : t.Length <= 53 ? 3 : 4;
        if (t.Length > 78) Array.Resize(ref t, 78);
        N = 17 + 4 * version;
        DatosCW = version == 2 ? 34 : version == 3 ? 55 : 80;
        EccCW = version == 2 ? 10 : version == 3 ? 15 : 20;
        int centroAlin = N - 7;   // el cuadrito de alineación (en las versiones 2 a 6 solo hay uno)

        // 1) bits de datos: modo bytes (0100), cuántas (8 bits), las letras, fin (0000) y relleno
        List<bool> bits = new List<bool>();
        Action<int, int> poner = (v, cuantos) => { for (int i = cuantos - 1; i >= 0; i--) bits.Add(((v >> i) & 1) != 0); };
        poner(4, 4); poner(t.Length, 8);
        foreach (byte b in t) poner(b, 8);
        poner(0, Math.Min(4, DatosCW * 8 - bits.Count));
        while (bits.Count % 8 != 0) bits.Add(false);
        byte[] datos = new byte[DatosCW];
        for (int i = 0; i < bits.Count / 8; i++) for (int j = 0; j < 8; j++) if (bits[i * 8 + j]) datos[i] |= (byte)(0x80 >> j);
        for (int i = bits.Count / 8, k = 0; i < DatosCW; i++, k++) datos[i] = (byte)(k % 2 == 0 ? 0xEC : 0x11);

        // 2) corrección de errores Reed-Solomon
        byte[] ecc = ReedSolomon(datos, EccCW);
        byte[] todo = new byte[DatosCW + EccCW];
        datos.CopyTo(todo, 0); ecc.CopyTo(todo, DatosCW);

        // 3) dibujo: patrones fijos
        bool[,] m = new bool[N, N], fijo = new bool[N, N];
        Action<int, int, bool> fij = (x, y, osc) => { m[x, y] = osc; fijo[x, y] = true; };
        for (int i = 0; i < N; i++) { fij(6, i, i % 2 == 0); fij(i, 6, i % 2 == 0); }          // líneas de tiempo
        foreach (int[] c in new[] { new[] { 3, 3 }, new[] { N - 4, 3 }, new[] { 3, N - 4 } })   // los 3 cuadros de las esquinas
            for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
            {
                int x = c[0] + dx, y = c[1] + dy;
                if (x < 0 || y < 0 || x >= N || y >= N) continue;
                int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                fij(x, y, d != 2 && d != 4);
            }
        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)                        // cuadrito de alineación
            fij(centroAlin + dx, centroAlin + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        PonerFormato(m, fijo, 0);   // reserva su lugar (luego se pone el bueno)

        // 4) los bytes en zigzag desde abajo a la derecha, de dos en dos columnas
        int bit = 0;
        for (int der = N - 1; der >= 1; der -= 2)
        {
            if (der == 6) der = 5;
            for (int v = 0; v < N; v++)
                for (int j = 0; j < 2; j++)
                {
                    int x = der - j;
                    bool sube = ((der + 1) & 2) == 0;
                    int y = sube ? N - 1 - v : v;
                    if (fijo[x, y]) continue;
                    if (bit < todo.Length * 8) { m[x, y] = ((todo[bit >> 3] >> (7 - (bit & 7))) & 1) != 0; bit++; }
                }
        }
        // 5) máscara 0: se invierten los cuadritos donde (x + y) es par
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (!fijo[x, y] && (x + y) % 2 == 0) m[x, y] = !m[x, y];

        // 6) información de formato (corrección L = 01, máscara 0) con su código BCH
        int dat = (1 << 3) | 0, rem = dat;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        PonerFormato(m, fijo, ((dat << 10) | rem) ^ 0x5412);
        return m;
    }

    static void PonerFormato(bool[,] m, bool[,] fijo, int f)
    {
        Func<int, bool> b = i => ((f >> i) & 1) != 0;
        Action<int, int, bool> s = (x, y, v) => { m[x, y] = v; fijo[x, y] = true; };
        for (int i = 0; i <= 5; i++) s(8, i, b(i));
        s(8, 7, b(6)); s(8, 8, b(7)); s(7, 8, b(8));
        for (int i = 9; i < 15; i++) s(14 - i, 8, b(i));
        for (int i = 0; i < 8; i++) s(N - 1 - i, 8, b(i));
        for (int i = 8; i < 15; i++) s(8, N - 15 + i, b(i));
        s(8, N - 8, true);   // el cuadrito oscuro que siempre va
    }

    static int Mult(int x, int y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--) { z = (z << 1) ^ ((z >> 7) * 0x11D); z ^= ((y >> i) & 1) * x; }
        return z & 0xFF;
    }

    static byte[] ReedSolomon(byte[] datos, int grado)
    {
        int[] div = new int[grado]; div[grado - 1] = 1; int raiz = 1;
        for (int i = 0; i < grado; i++)
        {
            for (int j = 0; j < grado; j++) { div[j] = Mult(div[j], raiz); if (j + 1 < grado) div[j] ^= div[j + 1]; }
            raiz = Mult(raiz, 2);
        }
        int[] res = new int[grado];
        foreach (byte d in datos)
        {
            int factor = d ^ res[0];
            Array.Copy(res, 1, res, 0, grado - 1); res[grado - 1] = 0;
            for (int i = 0; i < grado; i++) res[i] ^= Mult(div[i], factor);
        }
        byte[] o = new byte[grado]; for (int i = 0; i < grado; i++) o[i] = (byte)res[i]; return o;
    }
}

// Consola falsa (parece cmd): sale al darle "Sí" a "¿Quieres ejecutar esto?", "ejecuta NO ESCAPE"
// escribiendo sola y al terminar se cierra y sale NO ESCAPE. No se puede cerrar con la X ni con Alt+F4;
// solo se cierra sola, con el .bat "Cerrar ventanas de error" o al cerrar el DVD.
class ConsolaFalsa : Form
{
    struct Linea { public string Texto; public Color Color; }
    readonly List<Linea> lineas = new List<Linea>();
    readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();
    readonly Font letra = new Font("Consolas", 15f, GraphicsUnit.Pixel);
    // letras verdes de "hacker" (el último "NO ESCAPE." va en rojo)
    static readonly Color Gris = Color.FromArgb(22, 220, 60), Rojo = Color.FromArgb(231, 72, 86);
    IEnumerator<int> guion;
    readonly Stopwatch crono = new Stopwatch();
    long siguiente;   // en qué milisegundo toca el siguiente paso del guion
    int parpadeo;
    bool puedeCerrar;
    public bool Completa;   // true = terminó de escribir (entonces sí sale NO ESCAPE)

    public ConsolaFalsa()
    {
        Text = @"C:\WINDOWS\system32\cmd.exe";
        try { using (Stream s = typeof(Program).Assembly.GetManifestResourceStream("icono_cmd.ico")) Icon = new Icon(s); } catch { }
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 440);
        BackColor = Color.Black;
        TopMost = true;
        DoubleBuffered = true;
        guion = Guion();
        reloj.Interval = 15;
        reloj.Tick += delegate
        {
            // el tiempo se mide con un cronómetro de verdad (el temporizador de Windows es impreciso)
            long ahora = crono.ElapsedMilliseconds;
            int fase = (int)(ahora / 500);
            if (fase != parpadeo) { parpadeo = fase; Invalidate(); }   // el guioncito parpadea cada medio segundo
            while (ahora >= siguiente)
            {
                if (!guion.MoveNext()) { reloj.Stop(); Completa = true; puedeCerrar = true; Close(); return; }
                siguiente += guion.Current;
                Invalidate();
            }
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppIdVentana.Poner(Handle, "DvdRebote.Cmd");   // su propio botón en la barra, con el icono de cmd
        crono.Start();
        reloj.Start();
    }

    void Nueva(string t, Color c) { lineas.Add(new Linea { Texto = t, Color = c }); }
    void Cambiar(string t) { Linea l = lineas[lineas.Count - 1]; l.Texto = t; lineas[lineas.Count - 1] = l; }

    // lo que va saliendo; cada "yield return" es cuánto espera (en milisegundos) antes de lo siguiente
    IEnumerator<int> Guion()
    {
        Nueva("Microsoft Windows [Versión 10.0.26200]", Gris);
        Nueva("", Gris);
        string prompt = @"C:\Windows\System32>";
        Nueva(prompt, Gris); yield return 700;
        foreach (char ch in "NO_ESCAPE.exe") { Cambiar(lineas[lineas.Count - 1].Texto + ch); yield return 70; }
        yield return 350;
        Nueva("", Gris);
        Nueva("EJECUTANDO NO ESCAPE...", Gris); yield return 600;
        for (int p = 0; p <= 100; p += 4)
        {
            int llenos = p / 5;
            string barra = "[" + new string('█', llenos) + new string('░', 20 - llenos) + "] " + p + "%";
            if (p == 0) Nueva(barra, Gris); else Cambiar(barra);
            yield return 60 + (p % 20 == 8 ? 250 : 0);   // a veces se atora un poquito
        }
        yield return 300;
        Nueva("Cargando dvd_rebote_secret.sys... OK", Gris); yield return 350;
        Nueva("Bloqueando salidas... OK", Gris); yield return 350;
        Nueva("Buscando salida... 0 encontradas", Gris); yield return 700;
        Nueva("", Gris);
        Nueva("NO ESCAPE.", Rojo); yield return 1100;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        int alto = (int)Math.Ceiling(letra.GetHeight(g)) + 2, y = 6;
        int primera = Math.Max(0, lineas.Count - (ClientSize.Height - 12) / alto);   // si no cabe, se recorre hacia arriba
        for (int i = primera; i < lineas.Count; i++)
        {
            TextRenderer.DrawText(g, lineas[i].Texto, letra, new Point(4, y), lineas[i].Color, TextFormatFlags.NoPrefix);
            if (i == lineas.Count - 1 && parpadeo % 2 == 0)   // el guioncito que parpadea al final
            {
                int ancho = TextRenderer.MeasureText(g, lineas[i].Texto, letra, new Size(4000, alto), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                using (SolidBrush b = new SolidBrush(Gris)) g.FillRectangle(b, 4 + ancho + 2, y + alto - 5, 9, 3);
            }
            y += alto;
        }
    }

    // el .bat o cerrar el DVD: se va sin que salga NO ESCAPE
    public void Cancelar() { Completa = false; puedeCerrar = true; reloj.Stop(); Close(); }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // la X y Alt+F4 no la cierran (solo apagar Windows o el Administrador de tareas)
        bool apagando = e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing || e.CloseReason == CloseReason.ApplicationExitCall;
        if (!puedeCerrar && !apagando) { e.Cancel = true; System.Media.SystemSounds.Beep.Play(); return; }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) { reloj.Dispose(); letra.Dispose(); base.OnFormClosed(e); }
}

// El segundo antes del pantallazo: una foto de la pantalla tal como estaba, encima de todo y sin mouse,
// para que parezca que la PC se trabó ahí mismo. Los clics y Alt+F4 no le hacen nada.
class PantallaCongelada : Form
{
    readonly Bitmap foto;
    bool puedeCerrar, cursorEscondido;
    public PantallaCongelada()
    {
        Rectangle s = Screen.PrimaryScreen.Bounds;
        foto = new Bitmap(s.Width, s.Height);
        using (Graphics g = Graphics.FromImage(foto)) g.CopyFromScreen(s.Left, s.Top, 0, 0, s.Size);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = s;
        TopMost = true;
        ShowInTaskbar = false;
        BackgroundImage = foto;
        BackgroundImageLayout = ImageLayout.None;
        DoubleBuffered = true;
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); Cursor.Hide(); cursorEscondido = true; Activate(); }
    public void Quitar() { puedeCerrar = true; Close(); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool apagando = e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing || e.CloseReason == CloseReason.ApplicationExitCall;
        if (!puedeCerrar && !apagando) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (cursorEscondido) { Cursor.Show(); cursorEscondido = false; }
        BackgroundImage = null; foto.Dispose();
        base.OnFormClosed(e);
    }
}

// Pantalla azul FALSA (de broma, no le hace nada a la PC): sale 2 s después de darle "Aceptar" a NO ESCAPE,
// tapa la pantalla, cuenta de 0 a 100 % y se quita sola. Alt+F4 no la cierra; el .bat y cerrar el DVD sí.
class PantallaAzul : Form
{
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Random rnd = new Random();
    // código QR DE VERDAD (se puede escanear con el cel)
    // lleva a la página NOESCAPE (fondo negro, "NO ESCAPE" y "ERROR 404")
    readonly bool[,] qr = CodigoQR.Crear("https://noescape.pages.dev");
    int porcentaje, pausaFinal;

    // Windows 11 (compilación 22000 o más): la pantalla es NEGRA; en Windows 10, azul
    static bool EsWindows11()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                int build;
                return k != null && int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out build) && build >= 22000;
            }
        }
        catch { return false; }
    }
    bool puedeCerrar, cursorEscondido;

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr d, int x, int y, int cx, int cy, uint f);

    public PantallaAzul()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = EsWindows11() ? Color.Black : Color.FromArgb(0, 120, 215);
        DoubleBuffered = true;
        KeyPreview = true;

        timer.Interval = 150;
        timer.Tick += delegate
        {
            if (porcentaje < 100)
            {
                if (rnd.Next(2) == 0) porcentaje = Math.Min(100, porcentaje + rnd.Next(1, 7));
                Invalidate();
            }
            else if (++pausaFinal > 7) Quitar();   // se queda un ratito en 100 % y se va
            // siempre hasta arriba (los DVDs también son "TopMost")
            if (IsHandleCreated) SetWindowPos(Handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Cursor.Hide(); cursorEscondido = true;
        try { SilencioSistema.Silenciar(); } catch { }   // no se oye nada mientras está el pantallazo
        Activate();
        timer.Start();
    }

    public void Quitar() { puedeCerrar = true; timer.Stop(); Close(); }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 (o cualquier otro intento) no la cierra; solo ella misma, el .bat, cerrar el DVD o apagar Windows
        bool apagando = e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing || e.CloseReason == CloseReason.ApplicationExitCall;
        if (!puedeCerrar && !apagando) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (cursorEscondido) { Cursor.Show(); cursorEscondido = false; }
        try { SilencioSistema.Restaurar(); } catch { }   // vuelve el sonido como estaba
        timer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        int W = ClientSize.Width, H = ClientSize.Height;
        // Windows 11 (negra): letras más grandes y normales, el QR grande con cuadritos azules.
        // Windows 10 (azul): letras delgadas y el QR más chico con cuadritos del azul de la pantalla.
        bool w11 = BackColor.ToArgb() == Color.Black.ToArgb();
        float x = W * (w11 ? 0.13f : 0.11f), y = H * (w11 ? 0.14f : 0.12f), ancho = W * (w11 ? 0.62f : 0.78f);
        using (Font cara = new Font(w11 ? "Segoe UI" : "Segoe UI Light", H * (w11 ? 0.165f : 0.15f), GraphicsUnit.Pixel))
        using (Font grande = new Font(w11 ? "Segoe UI Semilight" : "Segoe UI Light", H * (w11 ? 0.037f : 0.034f), GraphicsUnit.Pixel))
        using (Font chica = new Font("Segoe UI", H * (w11 ? 0.0165f : 0.017f), GraphicsUnit.Pixel))
        {
            g.DrawString(":(", cara, Brushes.White, x - H * 0.012f, y);
            y += H * (w11 ? 0.25f : 0.23f);
            string msg = "Your device ran into NO ESCAPE and needs to restart. We're just collecting some error info, and then we'll restart for you.";
            SizeF t = g.MeasureString(msg, grande, (int)ancho);
            g.DrawString(msg, grande, Brushes.White, new RectangleF(x, y, ancho, t.Height + 4));
            y += t.Height + H * 0.04f;
            g.DrawString(porcentaje + "% complete", grande, Brushes.White, x, y);
            y += H * (w11 ? 0.09f : 0.09f);

            // el QR: cuadro blanco con margen y los cuadritos de color, en píxeles enteros
            // para que se vea nítido y el cel lo pueda leer
            int n = qr.GetLength(0), margen = w11 ? 2 : 1;
            int celda = Math.Max(2, (int)(H * (w11 ? 0.2f : 0.115f) / (n + 2 * margen)));
            int lado = celda * (n + 2 * margen);
            int qx0 = (int)x, qy0 = (int)y;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using (SolidBrush blanco = new SolidBrush(Color.White)) g.FillRectangle(blanco, qx0, qy0, lado, lado);
            using (SolidBrush b = new SolidBrush(w11 ? Color.FromArgb(0, 103, 192) : BackColor))
                for (int qy = 0; qy < n; qy++) for (int qx = 0; qx < n; qx++)
                    if (qr[qx, qy]) g.FillRectangle(b, qx0 + (qx + margen) * celda, qy0 + (qy + margen) * celda, celda, celda);

            // el texto de al lado, renglón por renglón (con más aire, como en el de verdad)
            float tx = x + lado + H * 0.02f, ty = y + 2, renglon = chica.GetHeight(g) * (w11 ? 1.9f : 1.35f);
            string[] lineas = {
                "No intentes escapar, hermano.",
                "If you call a support person, give them this info:",
                "Stop code: NO_ESCAPE" };
            foreach (string l in lineas) { g.DrawString(l, chica, Brushes.White, tx, ty); ty += l.Length == 0 ? renglon * 0.6f : renglon; }
        }
    }
}

// X de cerrar roja (como la de Windows al pasar el mouse). Deja pasar los clics a la X de verdad.
class XRoja : Form
{
    public XRoja()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000); Size = new Size(46, 30);
        TopMost = true;
        BackColor = Color.FromArgb(196, 43, 28);
        Opacity = 0.99;   // ventana "en capas" (si no, con WS_EX_LAYERED no se dibujaría)
        DoubleBuffered = true;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;
            return cp;
        }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float t = Math.Max(5f, Math.Min(ClientSize.Width, ClientSize.Height) * 0.17f);   // medio tamaño de la X
        float cx = ClientSize.Width / 2f, cy = ClientSize.Height / 2f;
        using (Pen p = new Pen(Color.White, 1.1f))
        {
            g.DrawLine(p, cx - t, cy - t, cx + t, cy + t);
            g.DrawLine(p, cx - t, cy + t, cx + t, cy - t);
        }
    }
}

class CapaEstatica : Form
{
    readonly Estatica estatica;
    public CapaEstatica(Estatica e)
    {
        estatica = e;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000); Size = new Size(10, 10);
        TopMost = true;
        Opacity = 0.42;
        DoubleBuffered = true;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;
            return cp;
        }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { estatica.Pintar(e.Graphics, ClientRectangle); }
}

// Estática de tele vieja (nieve de canal sin señal), lo más real posible:
//  - grano nuevo en cada cuadro (sin patrones que se repitan), un poco "corrido" a lo ancho como en las teles
//  - líneas de barrido, franja oscura y franja clara que van rodando (la imagen "no amarra")
//  - a veces un bloque de líneas se rasga hacia un lado, saltos de brillo y rayas blancas
//  - tinte frío azulado y orillas más oscuras (como el vidrio curvo de la tele)
class Estatica : IDisposable
{
    readonly Random rnd = new Random();
    Bitmap cuadro;
    int[] px;
    float[] vineta;
    int paso;
    uint semilla = 2463534242;
    int rasgaIni = -1, rasgaFin = -1, rasgaDx;   // bloque de líneas rasgado

    public void Avanzar() { paso++; }

    uint Azar() { semilla ^= semilla << 13; semilla ^= semilla >> 17; semilla ^= semilla << 5; return semilla; }

    public void Pintar(Graphics g, Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        int w = r.Width, h = r.Height;
        if (cuadro == null || cuadro.Width != w || cuadro.Height != h)
        {
            if (cuadro != null) cuadro.Dispose();
            cuadro = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            px = new int[w * h];
            vineta = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - w / 2f) / (w / 2f), ny = (y - h / 2f) / (h / 2f);
                    vineta[y * w + x] = 1f - 0.28f * Math.Min(1f, (nx * nx + ny * ny) * 0.6f);
                }
        }
        semilla ^= (uint)rnd.Next() | 1;

        int rueda1 = (paso * 4) % (h + 60) - 30;                    // franja oscura que baja
        int rueda2 = h - (paso * 2) % (h + 80) + 40;                // franja clara que sube despacio
        int brillo = rnd.Next(100) < 4 ? 30 : 6 - rnd.Next(13);     // parpadeo (a veces un flashazo)
        if (rasgaIni < 0 && rnd.Next(14) == 0)
        {
            rasgaIni = rnd.Next(h); rasgaFin = rasgaIni + 3 + rnd.Next(Math.Max(4, h / 6)); rasgaDx = rnd.Next(-40, 41);
        }
        else if (rasgaIni >= 0 && rnd.Next(3) == 0) rasgaIni = -1;

        for (int y = 0; y < h; y++)
        {
            bool rayaBlanca = (Azar() % 90) == 0;
            int d1 = Math.Abs(y - rueda1), d2 = Math.Abs(y - rueda2);
            int sombra = d1 < 14 ? (14 - d1) * 4 : 0;
            int luz = d2 < 20 ? (20 - d2) * 2 : 0;
            int barrido = (y & 1) == 0 ? 0 : 22;
            bool rasgada = y >= rasgaIni && y < rasgaFin;
            int ruidoFila = (int)(Azar() % 21) - 10;                // cada línea con su propio brillo
            int prev = (int)(Azar() & 255);
            int fila = y * w;
            for (int x = 0; x < w; x++)
            {
                int n = (int)(Azar() & 255);
                n = (n + (int)(Azar() & 255)) >> 1;                 // campana: más grises que extremos
                int v = (n * 5 + prev * 3) >> 3;                    // corrido a lo ancho (como el haz de la tele)
                prev = v;
                v = 70 + (v * 185 >> 8) + brillo + luz + ruidoFila - sombra - barrido;
                if (rasgada) v = (v + 150 + ((x + rasgaDx) & 8) * 4) >> 1;   // bloque rasgado más claro y rayado
                if (rayaBlanca) v = 225 + (n & 31);
                float k = vineta[fila + x];
                int rr = (int)(v * k), gg = (int)(v * k), bb = (int)((v + 10) * k);   // tinte frío
                if (rr < 0) rr = 0; else if (rr > 255) rr = 255;
                if (gg < 0) gg = 0; else if (gg > 255) gg = 255;
                if (bb < 0) bb = 0; else if (bb > 255) bb = 255;
                px[fila + x] = (255 << 24) | (rr << 16) | (gg << 8) | bb;
            }
            if (rasgada && rasgaDx != 0)
            {
                // corre toda la línea hacia un lado
                int[] tmp = new int[w];
                for (int x = 0; x < w; x++) tmp[x] = px[fila + ((x - rasgaDx) % w + w) % w];
                Array.Copy(tmp, 0, px, fila, w);
            }
        }
        var d = cuadro.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        System.Runtime.InteropServices.Marshal.Copy(px, 0, d.Scan0, px.Length);
        cuadro.UnlockBits(d);
        g.DrawImageUnscaled(cuadro, r.X, r.Y);
    }

    // texto negro con un borde claro, para que se lea encima de la estática
    public static void TextoLegible(Graphics g, string t, Font f, RectangleF r, StringAlignment alinear)
    {
        using (StringFormat sf = new StringFormat { Alignment = alinear, LineAlignment = StringAlignment.Center })
        {
            using (SolidBrush claro = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++)
                    if (dx != 0 || dy != 0) { RectangleF rr = r; rr.Offset(dx, dy); g.DrawString(t, f, claro, rr, sf); }
            g.DrawString(t, f, Brushes.Black, r, sf);
        }
    }

    public void Dispose() { if (cuadro != null) cuadro.Dispose(); }
}

// Botón "Aceptar" dibujado a mano con estática de fondo
class BotonEstatica : Button
{
    readonly Estatica estatica;
    public BotonEstatica(Estatica e)
    {
        estatica = e;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        estatica.Pintar(g, ClientRectangle);
        using (Pen borde = new Pen(Focused ? Color.FromArgb(0, 120, 215) : Color.FromArgb(110, 110, 110), Focused ? 2 : 1))
            g.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        Estatica.TextoLegible(g, Text, Font, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
    }
}

// Bitácora del código secreto SECRET: cada vez que sale "NO ESCAPE" o una ventanita de "Error" se anota
// una línea en %LOCALAPPDATA%\DvdRebote\secreto.log. El Revisor de errores la lee y los muestra.
// Formato de cada línea:  fecha ISO | TIPO | texto
static class RegistroSecret
{
    static readonly object candado = new object();
    public static string Ruta
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "secreto.log"); }
    }

    public static void Anotar(string tipo, string texto)
    {
        try
        {
            lock (candado)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
                File.AppendAllText(Ruta, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "|" + tipo + "|" + texto + Environment.NewLine, System.Text.Encoding.UTF8);
                // que no crezca para siempre: se quedan las últimas 2000 líneas
                FileInfo fi = new FileInfo(Ruta);
                if (fi.Length > 400000)
                {
                    string[] l = File.ReadAllLines(Ruta, System.Text.Encoding.UTF8);
                    int desde = Math.Max(0, l.Length - 2000);
                    string[] quedan = new string[l.Length - desde];
                    Array.Copy(l, desde, quedan, 0, quedan.Length);
                    File.WriteAllLines(Ruta, quedan, System.Text.Encoding.UTF8);
                }
            }
        }
        catch { }   // si no se puede escribir, no pasa nada: el código secret sigue funcionando
    }
}
