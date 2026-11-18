// dvd_config.exe - ventana de configuración para DvdRebote.exe
// Cada cambio se guarda al instante en dvd_config.json y DvdRebote lo aplica en vivo.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class ConfigForm : Form
{
    DvdConfig cfg;
    bool loading;

    TrackBar tbVel, tbTam, tbOpa;
    NumericUpDown nuVel, nuTam, nuOpa, nuTol, nuCant;
    TrackBar tbCant;
    ComboBox cbEstilo;
    Button btnColor;
    CheckBox chSonidoSkin;
    CheckBox chCambiar, chContador, chMensaje, chSonido, chPausa, chSolo, chEstela, chControl, chChoque, chInicio, chVentanas, chFondo, chFondoNegro;
    ComboBox cbPantallaCompleta;
    Label lblEstadisticas, lblLogrosEsq, lblLogrosHoras;
    ComboBox cbPantalla;
    readonly System.Collections.Generic.List<string> valoresPantalla = new System.Collections.Generic.List<string>();

    // "Principal", cada pantalla conectada y "Todas las pantallas"
    void LlenarPantallas()
    {
        cbPantalla.Items.Clear(); valoresPantalla.Clear();
        Screen[] ps = Screen.AllScreens;
        cbPantalla.Items.Add("Principal (" + Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height + ")");
        valoresPantalla.Add("principal");
        int n = 2;
        foreach (Screen p in ps)
        {
            if (p.Primary) continue;
            cbPantalla.Items.Add("Pantalla " + n++ + " (" + p.Bounds.Width + "x" + p.Bounds.Height + ")");
            valoresPantalla.Add(p.DeviceName);
        }
        cbPantalla.Items.Add(ps.Length > 1 ? "Todas las pantallas" : "Todas las pantallas (solo tienes una)");
        valoresPantalla.Add("todas");
    }

    // traduce el texto de cada control (y de las listas) y lo vuelve a traducir si el programa lo cambia
    static void Traducir(Control raiz)
    {
        if (!Idioma.Ingles) return;
        foreach (Control c in raiz.Controls)
        {
            Control ctl = c;
            string t = Idioma.T(ctl.Text);
            if (t != ctl.Text) ctl.Text = t;
            ctl.TextChanged += delegate { string tt = Idioma.T(ctl.Text); if (tt != ctl.Text) ctl.Text = tt; };
            ComboBox cb = ctl as ComboBox;
            if (cb != null)
                for (int i = 0; i < cb.Items.Count; i++) { string it = cb.Items[i] as string; if (it != null && Idioma.T(it) != it) cb.Items[i] = Idioma.T(it); }
            if (ctl.HasChildren) Traducir(ctl);
        }
    }

    void AbrirTienda()
    {
        // al elegir una skin se guarda en el acto (el DVD la pone mientras la tienda sigue abierta)
        using (VentanaTienda v = new VentanaTienda(Icon, cfg.Skin, s => { cfg.Skin = s; Save(); SonidoSkin.Tocar(s); }))   // al usar una skin, se escucha su sonido
            v.ShowDialog(this);
        try { PintarLogros(EstadisticasDvd.Load()); } catch { }
    }

    void AbrirLogros()
    {
        EstadisticasDvd e;
        try { e = EstadisticasDvd.Load(); } catch { e = new EstadisticasDvd(); }
        using (VentanaLogros v = new VentanaLogros(e, Icon)) v.ShowDialog(this);
    }

    // los logros: ★ ganado, ☆ todavía no (en una línea por tipo) y los G que tienes para la tienda
    void PintarLogros(EstadisticasDvd e)
    {
        string a = Idioma.Ingles ? "Corners:  " : "Esquinas:  ", b;
        foreach (long n in DvdConfig.MetasEsquinas) a += e.Total >= n ? "★" : "☆";
        a += Idioma.Ingles ? "\r\nHours:       " : "\r\nHoras:        ";
        foreach (int h in DvdConfig.MetasHoras) a += e.Segundos >= h * 3600 ? "★" : "☆";
        a += Idioma.Ingles ? "   Special: " : "   Especiales: ";
        foreach (string id in DvdConfig.EspecialesId) a += e.TieneEspecial(id) ? "★" : "☆";
        a += "   (" + e.Ganados + "/" + DvdConfig.TotalLogros + ")";
        b = (Idioma.Ingles ? "You have " : "Tienes ") + e.SaldoG + " G" + (Idioma.Ingles ? " to spend in the Shop" : " para gastar en la Tienda");
        if (lblLogrosEsq.Text != a) lblLogrosEsq.Text = a;
        if (lblLogrosHoras.Text != b) lblLogrosHoras.Text = b;
        lblLogrosEsq.ForeColor = lblLogrosHoras.ForeColor = Color.FromArgb(255, 210, 90);
    }
    static readonly string[] OpcionesPantallaCompleta = { "nada", "esconder", "esconder_pausar", "pausar" };   // mismo orden que la lista
    TextBox txtLogo;
    Button btnImagen, btnSonido;
    Label lblEstado, lblRunning;
    TextBox txtJson;
    TextBox txtCodigo;
    Button btnCodigo;
    int toques;
    double ultimoToque;

    static readonly Color Bg = Color.FromArgb(24, 24, 30);
    static readonly Color Panel = Color.FromArgb(36, 36, 46);
    static readonly Color Fg = Color.FromArgb(235, 235, 240);
    static readonly Color Muted = Color.FromArgb(150, 150, 165);

    public ConfigForm()
    {
        // idioma (se lee antes de armar la ventana)
        try { DvdConfig pre = DvdConfig.Load(); Idioma.Ingles = pre != null && pre.Idioma == "en"; } catch { }
        Text = "DVD Config";
        ClientSize = new Size(1150, 636);   // ancha y baja: cabe en una pantalla de 1280x720 sin achicarse
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Fg;
        Font = new Font("Segoe UI", 10f);

        Label title = new Label();
        title.Text = "Configuración del DVD";
        title.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        title.AutoSize = true; title.Location = new Point(20, 14);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Controls.Add(title);

        // Casilla secreta (igual que en el cel): pulsa 5 veces el título para que aparezca
        txtCodigo = new TextBox();
        txtCodigo.Location = new Point(300, 19); txtCodigo.Width = 120; txtCodigo.MaxLength = 30;
        txtCodigo.BackColor = Panel; txtCodigo.ForeColor = Color.FromArgb(255, 200, 90); txtCodigo.BorderStyle = BorderStyle.FixedSingle;
        txtCodigo.Visible = false;
        txtCodigo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UsarCodigo(); } };
        Controls.Add(txtCodigo);
        btnCodigo = new Button();
        btnCodigo.Text = "Usar"; btnCodigo.Location = new Point(426, 17); btnCodigo.Size = new Size(60, 28);
        btnCodigo.FlatStyle = FlatStyle.Flat; btnCodigo.BackColor = Panel; btnCodigo.ForeColor = Color.FromArgb(255, 200, 90);
        btnCodigo.FlatAppearance.BorderColor = Color.FromArgb(255, 200, 90);
        btnCodigo.Font = new Font("Segoe UI", 9f);
        btnCodigo.Visible = false;
        btnCodigo.Click += delegate { UsarCodigo(); };
        Controls.Add(btnCodigo);
        // al pulsar rápido Windows manda "doble clic" en vez del segundo clic: cuentan los dos
        title.MouseDown += delegate
        {
            double ahora = Environment.TickCount / 1000.0;
            if (ahora - ultimoToque > 1.5) toques = 0; // los 5 toques tienen que ser seguidos
            ultimoToque = ahora;
            if (++toques < 5) return;
            toques = 0;
            bool ver = !txtCodigo.Visible;
            txtCodigo.Visible = btnCodigo.Visible = ver;
            if (ver) { txtCodigo.Text = ""; txtCodigo.Focus(); lblEstado.Text = "Código secreto: escríbelo y pulsa Enter"; }
        };

        lblRunning = new Label();
        lblRunning.AutoSize = true; lblRunning.Location = new Point(24, 50); lblRunning.ForeColor = Muted;
        Controls.Add(lblRunning);

        int y = 86;
        tbVel = Slider("Velocidad (px/s)", 20, 2000, ref y, out nuVel);
        tbTam = Slider("Tamaño (%)", 20, 400, ref y, out nuTam);
        tbOpa = Slider("Opacidad (%)", 5, 100, ref y, out nuOpa);
        tbCant = Slider("Cantidad de DVDs", 1, 200, ref y, out nuCant);
        nuCant.Maximum = DvdConfig.MaxCantidad; // la barra llega a 200, escribiendo el número puedes poner los que quieras
        nuCant.Width = 80;

        AddLabel("Estilo", 24, y + 4);
        cbEstilo = new ComboBox();
        cbEstilo.DropDownStyle = ComboBoxStyle.DropDownList;
        cbEstilo.Items.AddRange(new object[] { "Logo DVD", "Disco", "VHS", "Reloj" });
        cbEstilo.Location = new Point(170, y); cbEstilo.Width = 150;
        cbEstilo.SelectedIndexChanged += delegate { Changed(); };
        Controls.Add(cbEstilo);
        y += 40;

        AddLabel("Color", 24, y + 4);
        btnColor = new Button();
        btnColor.Location = new Point(170, y); btnColor.Size = new Size(150, 28);
        btnColor.FlatStyle = FlatStyle.Flat;
        btnColor.Click += delegate { PickColor(); };
        Controls.Add(btnColor);
        y += 38;
        chCambiar = Check("Cambiar de color al rebotar", 170, y);
        y += 36;

        AddLabel("Tolerancia esquina (px)", 24, y + 2);
        nuTol = new NumericUpDown();
        nuTol.Minimum = 0; nuTol.Maximum = 200; nuTol.Increment = 1;
        nuTol.Location = new Point(200, y); nuTol.Width = 80;
        nuTol.BackColor = Panel; nuTol.ForeColor = Fg;
        nuTol.ValueChanged += delegate { Changed(); };
        Controls.Add(nuTol);
        y += 40;

        chContador = Check("Mostrar contador al tocar esquina", 24, y);
        chMensaje = Check("Mostrar \"¡ESQUINA!\" al tocar", 270, y);
        y += 30;
        chSonido = Check("Sonido en las esquinas", 24, y);
        chPausa = Check("Pausado", 270, y);
        y += 30;
        chEstela = Check("Estela detrás de los DVDs", 24, y);
        chControl = Check("Vibrar el control (Xbox)", 270, y);
        y += 30;
        chSolo = Check("Modo solo esquinas (va de esquina a esquina)", 24, y);
        chSolo.ForeColor = Color.FromArgb(255, 200, 90);
        y += 30;
        chChoque = Check("Choque entre DVDs", 24, y);
        chInicio = Check("Iniciar con Windows", 270, y);
        chInicio.CheckedChanged += delegate { if (!loading) CambiarInicioWindows(chInicio.Checked); };
        y += 30;
        chVentanas = Check("Rebotar en las ventanas abiertas", 24, y);
        chVentanas.ForeColor = Color.FromArgb(120, 210, 255);
        // ---- tercera columna: fondo animado, pantalla completa y botones (así la ventana cabe en 1280x720) ----
        const int c3 = 840;
        AddLabel("Fondo de pantalla", c3, 86).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        chFondo = Check("Fondo de pantalla animado", c3, 116);
        chFondo.ForeColor = Color.FromArgb(200, 160, 255);
        chFondo.CheckedChanged += delegate { chFondoNegro.Enabled = chFondo.Checked; };
        chFondoNegro = Check("Con fondo negro", c3 + 20, 144);
        chFondoNegro.ForeColor = Color.FromArgb(200, 160, 255);

        AddLabel("En pantalla completa (juegos, videos)", c3, 180);
        cbPantallaCompleta = new ComboBox();
        cbPantallaCompleta.DropDownStyle = ComboBoxStyle.DropDownList;
        cbPantallaCompleta.Items.AddRange(new object[] { "No hacer nada", "Esconder el DVD", "Esconder y pausar", "Solo pausar" });
        cbPantallaCompleta.Location = new Point(c3, 206); cbPantallaCompleta.Width = 286;
        cbPantallaCompleta.SelectedIndexChanged += delegate { Changed(); };
        Controls.Add(cbPantallaCompleta);

        AddLabel("Acciones", c3, 262).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        y = 290;
        Btn("▶ Abrir DVD", c3, y, delegate { OpenDvd(); });
        Btn("■ Cerrar DVD", c3 + 146, y, delegate { CloseDvd(); });
        y += 42;
        Btn("Reiniciar contador", c3, y, delegate { cfg.ReiniciarContador++; Save(); });
        Btn("Valores por defecto", c3 + 146, y, delegate
        {
            int token = cfg.ReiniciarContador; // no reiniciar el contador al restablecer valores
            string idioma = cfg.Idioma;        // ni el idioma
            cfg = new DvdConfig();
            cfg.ReiniciarContador = token; cfg.Idioma = idioma;
            ApplyToUi(); Save();
        });
        y += 42;
        Btn("Abrir carpeta", c3, y, delegate { Process.Start("explorer.exe", "/select,\"" + DvdConfig.RutaConfig + "\""); });
        Btn("Protector de pantalla", c3 + 146, y, delegate { CrearProtector(); });
        y += 42;
        Btn("Acceso directo", c3, y, delegate { CrearAccesoDirecto(); });
        y += 42;
        Button bAct = Btn("⟳ Buscar actualizaciones", c3, y, delegate { Actualizador.Buscar(this, s => lblEstado.Text = s); });
        bAct.Size = new Size(286, 34);
        bAct.ForeColor = Color.FromArgb(120, 210, 255);

        // estadísticas guardadas (las escribe el DVD; con trucos no cuentan)
        Label tEst = AddLabel("Estadísticas", c3, 510);
        tEst.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        new ToolTip().SetToolTip(tEst, "Con trucos (.bat como TURBO, SOLO o ESQUINA) no cuentan");
        lblEstadisticas = new Label();
        lblEstadisticas.AutoSize = false; lblEstadisticas.Location = new Point(c3, 536); lblEstadisticas.Size = new Size(286, 96);
        lblEstadisticas.Font = new Font("Segoe UI", 9.5f); lblEstadisticas.ForeColor = Color.FromArgb(255, 210, 90);
        Controls.Add(lblEstadisticas);

        // panel derecho: logo personalizado, JSON actual y atajos
        AddLabel("Logo personalizado", 500, 86).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        AddLabel("Texto", 500, 118);
        txtLogo = new TextBox();
        txtLogo.Location = new Point(560, 115); txtLogo.Width = 250; txtLogo.MaxLength = 20;
        txtLogo.BackColor = Panel; txtLogo.ForeColor = Fg; txtLogo.BorderStyle = BorderStyle.FixedSingle;
        txtLogo.TextChanged += delegate { Changed(); };
        Controls.Add(txtLogo);
        btnImagen = Btn("Elegir imagen...", 500, 148, delegate { ElegirImagen(); });
        btnImagen.Size = new Size(150, 30);
        Button bQuitar = Btn("Quitar imagen", 660, 148, delegate { cfg.LogoImagen = ""; PintarBotonImagen(); Save(); });
        bQuitar.Size = new Size(150, 30);

        AddLabel("Sonido de esquina", 500, 190).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        // con skin puesta: que en las esquinas suene su metal (bronce, plata, oro, diamante) o el sonido normal
        chSonidoSkin = Check("Sonido de la skin", 666, 192);
        btnSonido = Btn("Elegir sonido...", 500, 220, delegate { ElegirSonido(); });
        btnSonido.Size = new Size(150, 30);
        Button bProbar = Btn("▶ Probar", 660, 220, delegate { SonidoEsquina.Tocar(cfg.SonidoArchivo); });
        bProbar.Size = new Size(72, 30);
        Button bQuitarSon = Btn("Quitar", 738, 220, delegate { SonidoEsquina.Parar(); cfg.SonidoArchivo = ""; PintarBotonSonido(); Save(); });
        bQuitarSon.Size = new Size(72, 30);

        // pantalla donde rebota el DVD (si tienes varias)
        AddLabel("Pantalla del DVD", 500, 262).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        cbPantalla = new ComboBox();
        cbPantalla.DropDownStyle = ComboBoxStyle.DropDownList;
        cbPantalla.Location = new Point(500, 288); cbPantalla.Width = 310;
        LlenarPantallas();
        cbPantalla.SelectedIndexChanged += delegate { Changed(); };
        Controls.Add(cbPantalla);

        AddLabel("dvd_config.json", 500, 326).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        txtJson = new TextBox();
        txtJson.Multiline = true; txtJson.ReadOnly = true; txtJson.ScrollBars = ScrollBars.Vertical;
        txtJson.Font = new Font("Consolas", 9.5f);
        txtJson.BackColor = Panel; txtJson.ForeColor = Color.FromArgb(160, 220, 255);
        txtJson.BorderStyle = BorderStyle.None;
        txtJson.Location = new Point(500, 352); txtJson.Size = new Size(310, 100);
        Controls.Add(txtJson);

        // logros (se ganan con las estadísticas, sin trucos)
        AddLabel("Logros", 500, 460).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        lblLogrosEsq = new Label(); lblLogrosEsq.AutoSize = false; lblLogrosEsq.Location = new Point(500, 486); lblLogrosEsq.Size = new Size(310, 44);
        lblLogrosHoras = new Label(); lblLogrosHoras.AutoSize = false; lblLogrosHoras.Location = new Point(500, 532); lblLogrosHoras.Size = new Size(310, 26);
        lblLogrosEsq.Font = lblLogrosHoras.Font = new Font("Segoe UI", 9.5f);
        Controls.Add(lblLogrosEsq); Controls.Add(lblLogrosHoras);
        // al tocar los logros (o el botón) se abre la ventana "Logros"; la TIENDA para gastar los G
        Button bLogros = Btn("★ Ver logros ›", 690, 456, delegate { AbrirLogros(); });
        bLogros.Size = new Size(120, 26); bLogros.ForeColor = Color.FromArgb(255, 210, 90);
        Button bTienda = Btn("◆ Tienda ›", 580, 456, delegate { AbrirTienda(); });
        bTienda.Size = new Size(104, 26); bTienda.ForeColor = Color.FromArgb(130, 235, 255);
        lblLogrosEsq.Cursor = Cursors.Hand; lblLogrosEsq.Click += delegate { AbrirLogros(); };
        lblLogrosHoras.Cursor = Cursors.Hand; lblLogrosHoras.Click += delegate { AbrirTienda(); };
        Label atajos = AddLabel("Atajos:  Ctrl+Alt+D esconder / mostrar\r\n             Ctrl+Alt+P pausar", 500, 566);
        atajos.ForeColor = Muted;

        lblEstado = new Label();
        lblEstado.AutoSize = false; lblEstado.AutoEllipsis = true; lblEstado.ForeColor = Muted;
        lblEstado.Location = new Point(500, 608); lblEstado.Size = new Size(626, 22);
        Controls.Add(lblEstado);

        // IDIOMA: Español / English (arriba a la derecha). Al cambiarlo se vuelve a abrir la configuración.
        AddLabel("Idioma / Language", 840, 22).ForeColor = Muted;
        ComboBox cbIdioma = new ComboBox();
        cbIdioma.DropDownStyle = ComboBoxStyle.DropDownList;
        cbIdioma.Items.AddRange(new object[] { "Español", "English" });
        cbIdioma.Location = new Point(990, 18); cbIdioma.Width = 136;
        cbIdioma.SelectedIndex = Idioma.Ingles ? 1 : 0;
        cbIdioma.SelectedIndexChanged += delegate
        {
            string nuevo = cbIdioma.SelectedIndex == 1 ? "en" : "es";
            if (cfg == null || cfg.Idioma == nuevo) return;
            cfg.Idioma = nuevo; Save();
            Application.Restart();
        };
        Controls.Add(cbIdioma);

        cfg = DvdConfig.Load() ?? new DvdConfig();
        ApplyToUi();
        RefreshJson();
        lblEstado.Text = "Archivo: " + DvdConfig.RutaConfig;
        Traducir(this);   // todos los textos al idioma elegido (y los que cambien después también)

        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 700;
        t.Tick += delegate { RefreshStats(); };
        t.Start();
        RefreshStats();
        HacerRedimensionable();
    }

    // ---------- se puede maximizar: todo se agranda (con las letras) y queda centrado ----------
    Panel contenido;
    Size tamBase;
    readonly System.Collections.Generic.Dictionary<Control, Rectangle> boundsBase = new System.Collections.Generic.Dictionary<Control, Rectangle>();
    readonly System.Collections.Generic.Dictionary<Control, Font> fuenteBase = new System.Collections.Generic.Dictionary<Control, Font>();
    float escalaActual = 1f;

    void HacerRedimensionable()
    {
        tamBase = ClientSize;
        contenido = new Panel();
        contenido.BackColor = Bg;
        contenido.Bounds = new Rectangle(Point.Empty, tamBase);
        Control[] todos = new Control[Controls.Count];
        Controls.CopyTo(todos, 0);
        SuspendLayout();
        foreach (Control c in todos)
        {
            boundsBase[c] = c.Bounds;
            fuenteBase[c] = c.Font;
            contenido.Controls.Add(c);
        }
        Controls.Add(contenido);
        ResumeLayout();

        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        // se puede achicar hasta el 60 % (todo se ve más pequeño pero completo)
        MinimumSize = new Size(Size.Width - tamBase.Width + tamBase.Width * 6 / 10, Size.Height - tamBase.Height + tamBase.Height * 6 / 10);
        Resize += delegate { Acomodar(); };

        // si la ventana no cabe en la pantalla (por ejemplo 1280x720), se ajusta para que se vea toda
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int bordeW = Size.Width - ClientSize.Width, bordeH = Size.Height - ClientSize.Height;
        if (Size.Width > area.Width || Size.Height > area.Height)
        {
            float k = Math.Min((area.Width - bordeW) / (float)tamBase.Width, (area.Height - bordeH) / (float)tamBase.Height);
            ClientSize = new Size((int)(tamBase.Width * k), (int)(tamBase.Height * k));
            Location = new Point(area.X + (area.Width - Size.Width) / 2, area.Y + (area.Height - Size.Height) / 2);
        }
        Acomodar();
    }

    void Acomodar()
    {
        if (contenido == null || WindowState == FormWindowState.Minimized) return;
        float k = Math.Min(ClientSize.Width / (float)tamBase.Width, ClientSize.Height / (float)tamBase.Height);
        k = Math.Max(0.6f, k);
        contenido.SuspendLayout();
        if (Math.Abs(k - escalaActual) > 0.001f)
        {
            escalaActual = k;
            foreach (System.Collections.Generic.KeyValuePair<Control, Rectangle> p in boundsBase)
            {
                Control c = p.Key; Rectangle r = p.Value;
                Font f0 = fuenteBase[c];
                c.Font = new Font(f0.FontFamily, f0.Size * k, f0.Style);
                c.Bounds = new Rectangle((int)(r.X * k), (int)(r.Y * k), (int)(r.Width * k), (int)(r.Height * k));
            }
        }
        int w = (int)(tamBase.Width * k), h = (int)(tamBase.Height * k);
        contenido.Bounds = new Rectangle((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        contenido.ResumeLayout();
    }

    // ---------- construcción de controles ----------
    Label AddLabel(string text, int x, int y)
    {
        Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y);
        Controls.Add(l); return l;
    }

    TrackBar Slider(string name, int min, int max, ref int y, out NumericUpDown nu)
    {
        AddLabel(name, 24, y + 4);
        TrackBar tb = new TrackBar();
        tb.Minimum = min; tb.Maximum = max; tb.TickStyle = TickStyle.None;
        tb.Location = new Point(165, y); tb.Width = 180; tb.BackColor = Bg;
        NumericUpDown n = new NumericUpDown();
        n.Minimum = min; n.Maximum = max; n.Location = new Point(352, y + 2); n.Width = 70;
        n.BackColor = Panel; n.ForeColor = Fg;
        tb.Scroll += delegate { if (!loading) { n.Value = tb.Value; } };
        n.ValueChanged += delegate { tb.Value = (int)Math.Max(tb.Minimum, Math.Min(tb.Maximum, n.Value)); Changed(); };
        Controls.Add(tb); Controls.Add(n);
        nu = n;
        y += 44;
        return tb;
    }

    CheckBox Check(string text, int x, int y)
    {
        CheckBox c = new CheckBox(); c.Text = text; c.AutoSize = true; c.Location = new Point(x, y);
        c.CheckedChanged += delegate { Changed(); };
        Controls.Add(c); return c;
    }

    Button Btn(string text, int x, int y, EventHandler click)
    {
        Button b = new Button(); b.Text = text; b.Location = new Point(x, y); b.Size = new Size(140, 34);
        b.FlatStyle = FlatStyle.Flat; b.BackColor = Panel; b.ForeColor = Fg;
        b.Font = new Font("Segoe UI", 9f); // para que "Valores por defecto" y "Protector de pantalla" quepan
        b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 90);
        b.Click += click; Controls.Add(b); return b;
    }

    // ---------- lógica ----------
    void ApplyToUi()
    {
        loading = true;
        nuVel.Value = (decimal)Math.Max(20, Math.Min(2000, cfg.Velocidad)); tbVel.Value = (int)nuVel.Value;
        nuTam.Value = Math.Max(20, Math.Min(400, cfg.Tamano)); tbTam.Value = (int)nuTam.Value;
        nuOpa.Value = Math.Max(5, Math.Min(100, cfg.Opacidad)); tbOpa.Value = (int)nuOpa.Value;
        nuCant.Value = Math.Max(1, Math.Min(DvdConfig.MaxCantidad, cfg.Cantidad)); tbCant.Value = (int)Math.Min(tbCant.Maximum, nuCant.Value);
        nuTol.Value = Math.Max(0, Math.Min(200, cfg.ToleranciaEsquinaPx));
        cbEstilo.SelectedIndex = cfg.Estilo == "disco" ? 1 : cfg.Estilo == "vhs" ? 2 : cfg.Estilo == "reloj" ? 3 : 0;
        SetColorButton(cfg.GetColor());
        chCambiar.Checked = cfg.CambiarColor;
        chContador.Checked = cfg.MostrarContador;
        chMensaje.Checked = cfg.MostrarMensajeEsquina;
        chSonido.Checked = cfg.Sonido;
        chSonidoSkin.Checked = cfg.SonidoSkin;
        chPausa.Checked = cfg.Pausado;
        chSolo.Checked = cfg.ModoSoloEsquinas;
        chEstela.Checked = cfg.Estela;        chControl.Checked = cfg.VibrarControl;
        chChoque.Checked = cfg.Choque;
        chVentanas.Checked = cfg.RebotarVentanas;
        chFondo.Checked = cfg.FondoAnimado;
        chFondoNegro.Checked = cfg.FondoNegro;
        chFondoNegro.Enabled = cfg.FondoAnimado;
        int ip = valoresPantalla.IndexOf(cfg.Pantalla);
        cbPantalla.SelectedIndex = ip < 0 ? 0 : ip;   // si esa pantalla ya no está, sale "Principal"
        cbPantallaCompleta.SelectedIndex = Array.IndexOf(OpcionesPantallaCompleta, cfg.PantallaCompleta) < 0 ? 1 : Array.IndexOf(OpcionesPantallaCompleta, cfg.PantallaCompleta);
        chInicio.Checked = InicioWindowsActivo();
        txtLogo.Text = cfg.LogoTexto;
        PintarBotonImagen();
        PintarBotonSonido();
        loading = false;
    }

    void Changed()
    {
        if (loading || cfg == null) return;
        cfg.Velocidad = (double)nuVel.Value;
        cfg.Tamano = (int)nuTam.Value;
        cfg.Opacidad = (int)nuOpa.Value;
        cfg.Cantidad = (int)nuCant.Value;
        cfg.ToleranciaEsquinaPx = (int)nuTol.Value;
        cfg.Estilo = cbEstilo.SelectedIndex == 1 ? "disco" : cbEstilo.SelectedIndex == 2 ? "vhs" : cbEstilo.SelectedIndex == 3 ? "reloj" : "logo";
        cfg.CambiarColor = chCambiar.Checked;
        cfg.MostrarContador = chContador.Checked;
        cfg.MostrarMensajeEsquina = chMensaje.Checked;
        cfg.Sonido = chSonido.Checked;
        cfg.SonidoSkin = chSonidoSkin.Checked;
        cfg.Pausado = chPausa.Checked;
        cfg.ModoSoloEsquinas = chSolo.Checked;
        cfg.Estela = chEstela.Checked;        cfg.VibrarControl = chControl.Checked;
        cfg.Choque = chChoque.Checked;
        cfg.RebotarVentanas = chVentanas.Checked;
        cfg.FondoAnimado = chFondo.Checked;
        cfg.FondoNegro = chFondoNegro.Checked;
        if (cbPantalla.SelectedIndex >= 0 && cbPantalla.SelectedIndex < valoresPantalla.Count) cfg.Pantalla = valoresPantalla[cbPantalla.SelectedIndex];
        if (cbPantallaCompleta.SelectedIndex >= 0) cfg.PantallaCompleta = OpcionesPantallaCompleta[cbPantallaCompleta.SelectedIndex];
        string t = txtLogo.Text.Trim();
        cfg.LogoTexto = t.Length == 0 ? "DVD" : t;
        Save();
    }

    void Save()
    {
        try
        {
            cfg.Save();
            myStamp = Stamp();
            lblEstado.Text = "Guardado ✓ " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex) { lblEstado.Text = "Error al guardar: " + ex.Message; }
        RefreshJson();
    }

    void RefreshJson() { txtJson.Text = cfg.ToJson().Replace("\n", "\r\n"); }

    void PickColor()
    {
        using (ColorDialog d = new ColorDialog())
        {
            d.Color = cfg.GetColor(); d.FullOpen = true;
            if (d.ShowDialog(this) != DialogResult.OK) return;
            cfg.Color = DvdConfig.ToHex(d.Color);
            SetColorButton(d.Color);
            Save();
        }
    }

    void SetColorButton(Color c)
    {
        btnColor.BackColor = c;
        btnColor.Text = DvdConfig.ToHex(c);
        btnColor.ForeColor = (c.R * 0.3 + c.G * 0.59 + c.B * 0.11) > 140 ? Color.Black : Color.White;
    }

    // ---------- protector de pantalla: "DVD Rebote.scr" en la MISMA carpeta que DvdRebote.exe ----------
    // (antes el DVD se copiaba a sí mismo a TEMP cada vez que se abría y Windows Defender lo tomaba por
    // troyano). Ahora solo se crea cuando pulsas el botón, junto al DVD, y se instala con la opción
    // "Instalar" de Windows (la misma del clic derecho), que abre la ventana del protector.
    public static string RutaProtector()
    {
        string rebote = DvdConfig.RutaRebote();
        return rebote == null ? null : Path.Combine(Path.GetDirectoryName(rebote), "DVD Rebote.scr");
    }

    void CrearProtector()
    {
        string rebote = DvdConfig.RutaRebote();
        if (rebote == null)
        {
            Msg.Show("No encuentro DvdRebote.exe.\n\nÁbrelo una vez y vuelve a pulsar el botón.", "DVD Config");
            return;
        }
        try
        {
            string scr = RutaProtector();
            File.Copy(rebote, scr, true);   // el protector es el mismo DvdRebote.exe con extensión .scr
            ProcessStartInfo psi = new ProcessStartInfo(scr);
            psi.UseShellExecute = true;
            psi.Verb = "install";            // = clic derecho → Instalar
            Process.Start(psi);
            lblEstado.Text = "Protector creado junto al DVD: elige los minutos y pulsa Aceptar";
        }
        catch (Exception ex)
        {
            Msg.Show("No se pudo crear el protector de pantalla:\n" + ex.Message, "DVD Config");
        }
    }

    // ---------- casilla secreta: manda el código al DVD (igual que los .bat) ----------
    void UsarCodigo()
    {
        string code = txtCodigo.Text.Trim();
        if (code.Length == 0) return;
        txtCodigo.Text = "";
        // En la PC la casilla secreta solo sirve para los códigos secretos (los demás cheats van con los .bat)
        string limpio = code.Replace(" ", "").ToUpperInvariant();
        if (!limpio.StartsWith("EASTEREGG") && limpio != "REBOTANTE" && limpio != "TOCA" && limpio != "TROLL" && limpio != "SECRET") { lblEstado.Text = "Código incorrecto"; return; }
        if (!DvdRunning()) { lblEstado.Text = "Abre el DVD primero para usar el código"; return; }
        try
        {
            using (System.IO.Pipes.NamedPipeClientStream pipe =
                new System.IO.Pipes.NamedPipeClientStream(".", "DvdRebote_Cheats", System.IO.Pipes.PipeDirection.Out))
            {
                pipe.Connect(2000);
                using (StreamWriter w = new StreamWriter(pipe)) { w.WriteLine(code); w.Flush(); }
            }
            lblEstado.Text = "Código enviado al DVD: " + code;
        }
        catch { lblEstado.Text = "No se pudo mandar el código. Prueba otra vez."; }
    }

    // ---------- acceso directo en el escritorio (DVD y configuración) ----------
    void CrearAccesoDirecto()
    {
        string rebote = DvdConfig.RutaRebote();
        if (rebote == null)
        {
            Msg.Show("No encuentro DvdRebote.exe.\n\nÁbrelo una vez y vuelve a pulsar el botón.", "DVD Config");
            return;
        }
        try
        {
            string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CrearLnk(Path.Combine(escritorio, "DVD Rebote.lnk"), rebote, "Abrir el DVD que rebota");
            CrearLnk(Path.Combine(escritorio, "Configurar DVD.lnk"), Application.ExecutablePath, "Configurar el DVD");
            lblEstado.Text = "Accesos directos creados en el escritorio ✓";
            Msg.Show("Listo: en tu escritorio quedaron \"DVD Rebote\" y \"Configurar DVD\".", "DVD Config",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Msg.Show("No se pudo crear el acceso directo:\n" + ex.Message, "DVD Config");
        }
    }

    static void CrearLnk(string lnk, string destino, string descripcion)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        try
        {
            System.Reflection.BindingFlags set = System.Reflection.BindingFlags.SetProperty;
            object a = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type ta = a.GetType();
            ta.InvokeMember("TargetPath", set, null, a, new object[] { destino });
            ta.InvokeMember("WorkingDirectory", set, null, a, new object[] { Path.GetDirectoryName(destino) });
            ta.InvokeMember("Description", set, null, a, new object[] { descripcion });
            ta.InvokeMember("IconLocation", set, null, a, new object[] { destino + ",0" });
            ta.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, a, null);
        }
        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sh); }
    }

    // ---------- logo personalizado: imagen ----------
    void ElegirImagen()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Elige una imagen para el logo";
            d.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                // se copia junto al JSON, así sigue funcionando aunque muevas o borres la original
                string ext = Path.GetExtension(d.FileName).ToLowerInvariant();
                string destino = Path.Combine(DvdConfig.CarpetaDatos, "logo_personalizado" + ext);
                using (Image prueba = Image.FromFile(d.FileName)) { } // comprobar que es una imagen válida
                File.Copy(d.FileName, destino, true);
                cfg.LogoImagen = destino;
                PintarBotonImagen();
                Save();
            }
            catch (Exception ex) { Msg.Show("No se pudo usar esa imagen:\n" + ex.Message, "DVD Config"); }
        }
    }

    // el sonido se copia junto al JSON con un nombre nuevo cada vez (el DVD puede tener abierto el anterior)
    void ElegirSonido()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Elige el sonido para cuando toque una esquina";
            d.Filter = "Sonidos|*.wav;*.mp3;*.wma|Todos los archivos|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            string error = SonidoEsquina.Abrir(d.FileName);   // revisar que Windows lo puede tocar
            SonidoEsquina.Parar();
            if (error != null) { Msg.Show("No se pudo usar ese sonido:\n" + error, "DVD Config"); return; }
            try
            {
                string ext = Path.GetExtension(d.FileName).ToLowerInvariant();
                string destino = Path.Combine(DvdConfig.CarpetaDatos, "sonido_esquina_" + DateTime.Now.Ticks + ext);
                File.Copy(d.FileName, destino, true);
                string anterior = cfg.SonidoArchivo;
                cfg.SonidoArchivo = destino;
                PintarBotonSonido();
                Save();
                SonidoEsquina.Tocar(destino);
                foreach (string f in Directory.GetFiles(DvdConfig.CarpetaDatos, "sonido_esquina_*"))
                    if (!f.Equals(destino, StringComparison.OrdinalIgnoreCase)) try { File.Delete(f); } catch { }   // el que el DVD tenga abierto se borra la próxima vez
            }
            catch (Exception ex) { Msg.Show("No se pudo usar ese sonido:\n" + ex.Message, "DVD Config"); }
        }
    }

    void PintarBotonSonido()
    {
        bool hay = !string.IsNullOrEmpty(cfg.SonidoArchivo) && File.Exists(cfg.SonidoArchivo);
        btnSonido.Text = hay ? "✓ Sonido puesto" : "Elegir sonido...";
        btnSonido.ForeColor = hay ? Color.FromArgb(110, 220, 130) : Fg;
    }

    void PintarBotonImagen()
    {
        bool hay = !string.IsNullOrEmpty(cfg.LogoImagen) && File.Exists(cfg.LogoImagen);
        btnImagen.Text = hay ? "✓ Imagen puesta" : "Elegir imagen...";
        btnImagen.ForeColor = hay ? Color.FromArgb(110, 220, 130) : Fg;
    }

    // ---------- iniciar con Windows (HKCU\...\Run, solo para tu usuario) ----------
    const string ClaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string NombreRun = "DVD Rebote";

    static bool InicioWindowsActivo()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ClaveRun))
                return k != null && k.GetValue(NombreRun) != null;
        }
        catch { return false; }
    }

    void CambiarInicioWindows(bool activar)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ClaveRun))
            {
                if (activar)
                {
                    string rebote = DvdConfig.RutaRebote();
                    if (rebote == null)
                    {
                        Msg.Show("No encuentro DvdRebote.exe. Ábrelo una vez y vuelve a marcar la casilla.", "DVD Config");
                        loading = true; chInicio.Checked = false; loading = false;
                        return;
                    }
                    k.SetValue(NombreRun, "\"" + rebote + "\"");
                    lblEstado.Text = "El DVD se abrirá al encender la PC";
                }
                else
                {
                    k.DeleteValue(NombreRun, false);
                    lblEstado.Text = "El DVD ya no se abre al encender la PC";
                }
            }
        }
        catch (Exception ex) { lblEstado.Text = "No se pudo cambiar el inicio con Windows: " + ex.Message; }
    }

    static bool DvdRunning() { return Process.GetProcessesByName("DvdRebote").Length > 0; }

    // Sin el .bat instalado el modo solo esquinas no se puede usar
    void UpdateBatState()
    {
        bool bat = DvdConfig.BatInstalado();
        chSolo.Enabled = bat;
        chSolo.Text = bat
            ? "Modo solo esquinas (va de esquina a esquina)"
            : "Modo solo esquinas (necesita modo_solo_esquinas.bat)";
        chSolo.ForeColor = bat ? Color.FromArgb(255, 200, 90) : Muted;
    }

    DateTime myStamp = DateTime.MinValue;

    static DateTime Stamp()
    {
        try { return File.GetLastWriteTimeUtc(DvdConfig.RutaConfig); } catch { return DateTime.MinValue; }
    }

    void RefreshStats()
    {
        // si otro programa (por ejemplo el .bat) cambió el JSON, recargarlo
        DateTime st = Stamp();
        if (myStamp == DateTime.MinValue) myStamp = st;
        else if (st != myStamp)
        {
            DvdConfig c = DvdConfig.Load();
            if (c != null)
            {
                myStamp = st; cfg = c; ApplyToUi(); RefreshJson();
                lblEstado.Text = "Recargado (cambio externo) " + DateTime.Now.ToString("HH:mm:ss");
            }
        }
        UpdateBatState();
        try
        {
            EstadisticasDvd est = EstadisticasDvd.Load();
            string t = est.Texto(); if (lblEstadisticas.Text != t) lblEstadisticas.Text = t;
            PintarLogros(est);
        }
        catch { }
        bool run = DvdRunning();
        lblRunning.Text = run ? "● DvdRebote está corriendo — los cambios se aplican al instante" : "○ DvdRebote no está abierto";
        lblRunning.ForeColor = run ? Color.FromArgb(110, 220, 130) : Muted;
    }

    void OpenDvd()
    {
        // DvdRebote.exe puede estar en otra carpeta: se busca junto a este programa o por el registro
        string p = DvdConfig.RutaRebote();
        if (p == null)
        {
            Msg.Show("No encuentro DvdRebote.exe.\n\nPonlo en la misma carpeta que dvd_config.exe, " +
                "o ábrelo una vez desde donde esté y así lo encuentro.", "DVD Config");
            return;
        }
        if (!DvdRunning()) Process.Start(p);
        RefreshStats();
    }

    void CloseDvd()
    {
        try
        {
            EventWaitHandle h;
            if (EventWaitHandle.TryOpenExisting("DvdRebote_Salir", out h)) { h.Set(); h.Dispose(); }
        }
        catch { }
    }
}

static class Program
{
    [STAThread]
    // dvd_config.exe --alternar-solo-esquinas  -> cambia el modo sin abrir ventana
    // Código de salida: 1 = activado, 0 = desactivado, 2 = error
    static int Main(string[] args)
    {
        if (DvdConfig.AtenderCampos(args)) return 0;   // "--campos": solo dice qué ajustes tiene esta versión
        Application.EnableVisualStyles();
        if (!DvdConfig.RevisarNetFramework()) return 2; // avisa si falta .NET Framework 4.5+
        DvdConfig.RegistrarCarpeta(); // para que el .bat encuentre los .exe desde cualquier carpeta
        Actualizador.LimpiarViejos();   // borra los .viejo de la actualización anterior
        if (Array.IndexOf(args, "--alternar-solo-esquinas") >= 0)
        {
            try
            {
                DvdConfig c = null;
                for (int i = 0; i < 20 && c == null; i++) { c = DvdConfig.Load(); if (c == null) Thread.Sleep(100); }
                if (c == null) return 2; // no se pudo leer: no tocar el archivo para no perder la configuración
                c.ModoSoloEsquinas = !c.ModoSoloEsquinas;
                c.Save();
                return c.ModoSoloEsquinas ? 1 : 0;
            }
            catch { return 2; }
        }
        RunUi();
        return 0;
    }

    static void RunUi()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ConfigForm());
    }
}

// Ventana "Logros": todos los logros con su icono, descripción, progreso y puntos.
// Abajo, el logro PLATINO (trofeo con el logo de DVD) que se gana teniendo todos.
class VentanaLogros : Form
{
    class Fila { public string Puntos, Nombre, Desc; public bool Ganado; }
    readonly System.Collections.Generic.List<Fila> filas = new System.Collections.Generic.List<Fila>();
    int puntos, puntosMax, ganados;
    static Bitmap logo;
    static readonly Bitmap[] trofeos = new Bitmap[4];
    const int ArribaTrofeos = 467;   // dónde empiezan las cajas de los trofeos (debajo de los 14 logros: 7 por columna)

    static Bitmap Recurso(string n)
    {
        try { using (Stream s = typeof(VentanaLogros).Assembly.GetManifestResourceStream(n)) return s == null ? null : new Bitmap(s); }
        catch { return null; }
    }

    public VentanaLogros(EstadisticasDvd e, Icon icono)
    {
        Text = Idioma.T("Logros");
        Icon = icono;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(24, 24, 30); ForeColor = Color.FromArgb(235, 235, 240);
        ClientSize = new Size(820, 681);
        DoubleBuffered = true;
        // animación de los trofeos ganados (rayos, brillitos, destello y confeti al abrir)
        animacion.Interval = 30;
        animacion.Tick += delegate { Invalidate(new Rectangle(0, ArribaTrofeos, ClientSize.Width, ClientSize.Height - ArribaTrofeos)); if (confeti.Count > 0) Invalidate(); };
        Shown += delegate { animacion.Start(); };
        FormClosed += delegate { animacion.Stop(); animacion.Dispose(); };
        if (logo == null) logo = Recurso("logo_dvd.png");
        for (int i = 0; i < 4; i++) if (trofeos[i] == null) trofeos[i] = Recurso("trofeo_" + DvdConfig.MetalesTrofeos[i] + ".png");

        long[] esq = DvdConfig.MetasEsquinas; int[] horas = DvdConfig.MetasHoras;
        for (int i = 0; i < esq.Length; i++)
            Agregar(DvdConfig.NombresLogrosEsquinas[i], (Idioma.Ingles ? "Hit " + esq[i] + " corners in total (" : "Toca " + esq[i] + " esquinas en total (") + Math.Min(e.Total, esq[i]) + "/" + esq[i] + ")", e.Total >= esq[i]);
        for (int i = 0; i < horas.Length; i++)
        {
            long min = (long)(e.Segundos / 60), meta = horas[i] * 60L;
            Agregar(DvdConfig.NombresLogrosHoras[i], (Idioma.Ingles ? "Leave the DVD bouncing " + horas[i] + (horas[i] == 1 ? " hour" : " hours") : "Deja el DVD rebotando " + horas[i] + (horas[i] == 1 ? " hora" : " horas")) +
                " (" + Math.Min(min, meta) / 60 + " h " + Math.Min(min, meta) % 60 + " min)", e.Segundos >= horas[i] * 3600);
        }
        // especiales (en la columna de la derecha, debajo de las horas)
        for (int i = 0; i < DvdConfig.EspecialesId.Length; i++)
            Agregar(DvdConfig.NombresEspeciales[i], Idioma.Ingles ? DvdConfig.DescEspecialesEn[i] : DvdConfig.DescEspeciales[i], e.TieneEspecial(DvdConfig.EspecialesId[i]));
    }

    void Agregar(string puntosYNombre, string desc, bool ganado)
    {
        puntosYNombre = Idioma.T(puntosYNombre);
        int i = puntosYNombre.IndexOf(": ");
        Fila f = new Fila { Puntos = puntosYNombre.Substring(0, i), Nombre = puntosYNombre.Substring(i + 2), Desc = desc, Ganado = ganado };
        filas.Add(f);
        int p; int.TryParse(f.Puntos.TrimEnd('G'), out p);
        puntosMax += p; if (ganado) { puntos += p; ganados++; }
    }

    protected override void OnPaint(PaintEventArgs ev)
    {
        Graphics g = ev.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        Color gris = Color.FromArgb(150, 150, 165), oro = Color.FromArgb(255, 210, 90);
        using (Font titulo = new Font("Segoe UI", 20f, FontStyle.Bold))
        using (Font sub = new Font("Segoe UI", 10f))
        using (Font nombre = new Font("Segoe UI", 11f, FontStyle.Bold))
        using (Font chica = new Font("Segoe UI", 9f))
        {
            TextRenderer.DrawText(g, Idioma.T("Logros"), titulo, new Point(20, 12), Color.White);
            TextRenderer.DrawText(g, ganados + (Idioma.Ingles ? " of " : " de ") + filas.Count + (Idioma.Ingles ? " achievements   ·   " : " logros   ·   ") + puntos + " / " + puntosMax + "G", sub, new Point(24, 52), oro);
            // logros en dos columnas: esquinas a la izquierda, horas a la derecha
            int anchoFila = (ClientSize.Width - 32 - 10) / 2, nEsq = DvdConfig.MetasEsquinas.Length;
            for (int k = 0; k < filas.Count; k++)
            {
                Fila f = filas[k];
                int col = k < nEsq ? 0 : 1, ren = k < nEsq ? k : k - nEsq;
                int x = 16 + col * (anchoFila + 10), y = 86 + ren * 53;
                Rectangle fila = new Rectangle(x, y, anchoFila, 48);
                using (SolidBrush b = new SolidBrush(f.Ganado ? Color.FromArgb(44, 44, 54) : Color.FromArgb(32, 32, 38))) g.FillRectangle(b, fila);
                Icono(g, new Rectangle(x + 6, y + 5, 38, 38), f.Ganado);
                TextRenderer.DrawText(g, f.Nombre, nombre, new Rectangle(x + 52, y + 4, anchoFila - 112, 22), f.Ganado ? Color.White : gris, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, f.Desc, chica, new Rectangle(x + 53, y + 26, anchoFila - 60, 20), gris, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, f.Puntos, nombre, new Rectangle(fila.Right - 64, y, 56, 30), f.Ganado ? oro : gris,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
            // ---- TROFEOS: bronce, plata, oro y diamante ----
            int yT = ArribaTrofeos;
            int alto = ClientSize.Height - yT - 14, ancho = (ClientSize.Width - 32 - 18) / 4;
            int ganadosAhora = ganados;
            Color[] metal = { Color.FromArgb(225, 140, 70), Color.FromArgb(205, 215, 230), Color.FromArgb(255, 205, 60), Color.FromArgb(130, 235, 255) };
            for (int i = 0; i < 4; i++)
            {
                bool tiene = ganadosAhora >= DvdConfig.LogrosParaTrofeo[i];
                int y = yT;
                Rectangle caja = new Rectangle(16 + i * (ancho + 6), y, ancho, alto);
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(caja,
                    tiene ? Color.FromArgb(90, metal[i]) : Color.FromArgb(36, 36, 44), tiene ? Color.FromArgb(28, 28, 34) : Color.FromArgb(28, 28, 34), 90f))
                    g.FillRectangle(b, caja);
                if (tiene) using (Pen p = new Pen(Color.FromArgb(160, metal[i]), 1.5f)) g.DrawRectangle(p, caja);
                if (tiene) EfectoCaja(g, caja, metal[i], i);   // rayos, brillitos y destello pasando
                Bitmap tr = trofeos[i];
                if (tr != null)
                {
                    Rectangle rt = new Rectangle(caja.Left + (caja.Width - 56) / 2, caja.Top + 3, 56, 56);
                    if (tiene) g.DrawImage(tr, rt);
                    else using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        // bloqueado: en gris y medio transparente
                        ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] {
                            new float[] { .3f, .3f, .3f, 0, 0 }, new float[] { .59f, .59f, .59f, 0, 0 }, new float[] { .11f, .11f, .11f, 0, 0 },
                            new float[] { 0, 0, 0, .35f, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
                        g.DrawImage(tr, rt, 0, 0, tr.Width, tr.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                TextFormatFlags centro = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
                TextRenderer.DrawText(g, Idioma.T(DvdConfig.MetalesTrofeos[i].ToUpper()), chica, new Rectangle(caja.Left, caja.Top + 58, caja.Width, 16), tiene ? metal[i] : gris, centro);
                using (Font fn = new Font("Segoe UI", 10f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Idioma.T(DvdConfig.NombresTrofeos[i]), fn, new Rectangle(caja.Left + 2, caja.Top + 74, caja.Width - 4, 36), tiene ? Color.White : gris, centro);
                TextRenderer.DrawText(g, Idioma.T(DvdConfig.CondicionTrofeos[i]), chica, new Rectangle(caja.Left + 4, caja.Top + 110, caja.Width - 8, alto - 110), gris, centro);
                if (tiene) centrosTrofeos[i] = new PointF(caja.Left + caja.Width / 2f, caja.Top + 31);
            }
            DibujarConfeti(g);
        }
    }

    // ---- efectos de los trofeos ganados ----
    readonly System.Windows.Forms.Timer animacion = new System.Windows.Forms.Timer();
    readonly Stopwatch reloj = Stopwatch.StartNew();
    readonly PointF[] centrosTrofeos = new PointF[4];
    class Papel { public float X, Y, Vx, Vy, Rot, Vr, Tam, Fase; public Color Col; public bool Estrella; }
    readonly System.Collections.Generic.List<Papel> confeti = new System.Collections.Generic.List<Papel>();
    bool confetiLanzado; double ultimoConfeti;

    static void Estrella(Graphics g, Brush b, float x, float y, float r)
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

    // rayos de luz girando detrás del trofeo, brillitos que parpadean y un destello que pasa cada 3 s
    void EfectoCaja(Graphics g, Rectangle caja, Color col, int i)
    {
        double t = reloj.Elapsed.TotalSeconds;
        System.Drawing.Drawing2D.GraphicsState gs = g.Save();
        g.SetClip(caja);
        float cx = caja.Left + caja.Width / 2f, cy = caja.Top + 31;
        int nr = i == 3 ? 20 : i == 2 ? 16 : 12;
        g.TranslateTransform(cx, cy); g.RotateTransform((float)(t * 30 * (i % 2 == 0 ? 1 : -1)));
        using (SolidBrush rayo = new SolidBrush(Color.FromArgb(i >= 2 ? 45 : 32, col)))
            for (int k = 0; k < nr; k++) { g.FillPolygon(rayo, new[] { new PointF(0, 0), new PointF(80, -6), new PointF(80, 6) }); g.RotateTransform(360f / nr); }
        g.ResetTransform();
        float[,] pos = { { -55, -18 }, { 52, -10 }, { -40, 20 }, { 60, 26 }, { 0, -26 } };
        for (int k = 0; k < pos.GetLength(0); k++)
        {
            float br = (float)(0.5 + 0.5 * Math.Sin(t * 5 + k * 1.9 + i));
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * br), k % 2 == 0 ? Color.White : col)))
                Estrella(g, b, cx + pos[k, 0], cy + pos[k, 1], 2 + br * 4);
        }
        double ciclo = ((t + i * 0.6) % 3.0) / 0.9;   // destello diagonal que cruza la caja
        if (ciclo < 1)
        {
            float x = (float)(caja.Left - 60 + ciclo * (caja.Width + 120));
            g.TranslateTransform(x, caja.Top); g.RotateTransform(20);
            using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(0, -40, 40, caja.Height + 80), Color.Transparent, Color.Transparent, 0f))
            {
                System.Drawing.Drawing2D.ColorBlend cb = new System.Drawing.Drawing2D.ColorBlend();
                cb.Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                cb.Positions = new[] { 0f, 0.5f, 1f };
                b.InterpolationColors = cb;
                g.FillRectangle(b, new RectangleF(0, -40, 40, caja.Height + 80));
            }
        }
        g.Restore(gs);
    }

    // al abrir la ventana, sale confeti de los trofeos que tienes
    void DibujarConfeti(Graphics g)
    {
        double t = reloj.Elapsed.TotalSeconds;
        if (!confetiLanzado && t > 0.2)
        {
            confetiLanzado = true; ultimoConfeti = t;
            Random r = new Random();
            Color[][] pal = {
                new[] { Color.FromArgb(225, 140, 70), Color.FromArgb(255, 190, 130), Color.White },
                new[] { Color.FromArgb(205, 215, 230), Color.FromArgb(160, 200, 240), Color.White },
                new[] { Color.FromArgb(255, 205, 60), Color.FromArgb(255, 235, 140), Color.White, Color.FromArgb(0, 191, 255) },
                new[] { Color.FromArgb(130, 235, 255), Color.FromArgb(200, 250, 255), Color.White, Color.FromArgb(190, 140, 255) } };
            for (int i = 0; i < 4; i++)
            {
                if (centrosTrofeos[i] == PointF.Empty) continue;
                for (int k = 0; k < 40 + i * 20; k++)
                {
                    double ang = -Math.PI / 2 + (r.NextDouble() - 0.5) * Math.PI * 1.3, vel = 200 + r.NextDouble() * 420;
                    confeti.Add(new Papel
                    {
                        X = centrosTrofeos[i].X, Y = centrosTrofeos[i].Y, Vx = (float)(Math.Cos(ang) * vel), Vy = (float)(Math.Sin(ang) * vel),
                        Rot = (float)(r.NextDouble() * 360), Vr = (float)((r.NextDouble() - 0.5) * 720), Tam = 5 + (float)r.NextDouble() * 6,
                        Fase = (float)(r.NextDouble() * 6.28), Col = pal[i][r.Next(pal[i].Length)], Estrella = r.NextDouble() < 0.25
                    });
                }
            }
        }
        if (confeti.Count == 0) return;
        float dt = (float)Math.Min(0.05, t - ultimoConfeti); ultimoConfeti = t;
        float alfa = (float)Math.Max(0, Math.Min(1, (3.2 - t) / 0.8));
        if (alfa <= 0) { confeti.Clear(); return; }
        foreach (Papel p in confeti)
        {
            p.Vy += 700 * dt; p.Vx *= (float)Math.Pow(0.35, dt); p.Vy *= (float)Math.Pow(0.6, dt);
            p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Rot += p.Vr * dt;
            if (p.Estrella)
            {
                float br = (float)(0.5 + 0.5 * Math.Sin(t * 14 + p.Fase));
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * alfa * (0.4 + 0.6 * br)), p.Col))) Estrella(g, b, p.X, p.Y, p.Tam * (0.8f + br * 0.8f));
            }
            else
            {
                float esc = (float)Math.Abs(Math.Cos((p.Rot + p.Fase * 40) * Math.PI / 180));
                g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.Rot);
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * alfa), p.Col)))
                    g.FillRectangle(b, -p.Tam / 2, -p.Tam * 0.3f * (0.25f + esc), p.Tam, p.Tam * 0.6f * (0.25f + esc));
                g.ResetTransform();
            }
        }
    }

    // círculo plateado con el logo de DVD (en gris si todavía no lo tienes)
    void Icono(Graphics g, Rectangle c, bool ganado)
    {
        using (System.Drawing.Drawing2D.LinearGradientBrush plata = new System.Drawing.Drawing2D.LinearGradientBrush(c,
            ganado ? Color.FromArgb(235, 235, 240) : Color.FromArgb(110, 110, 118), ganado ? Color.FromArgb(120, 120, 128) : Color.FromArgb(60, 60, 66), 45f))
            g.FillEllipse(plata, c);
        Rectangle d = Rectangle.Inflate(c, -3, -3);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 22, 26))) g.FillEllipse(b, d);
        if (logo == null) return;
        float k = Math.Min(d.Width * 0.78f / logo.Width, d.Height * 0.6f / logo.Height);
        int lw = (int)(logo.Width * k), lh = (int)(logo.Height * k);
        Color col = ganado ? Color.FromArgb(0, 191, 255) : Color.FromArgb(90, 90, 100);
        using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
        {
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { col.R / 255f, col.G / 255f, col.B / 255f, 0, 1 } }));
            g.DrawImage(logo, new Rectangle(d.Left + (d.Width - lw) / 2, d.Top + (d.Height - lh) / 2, lw, lh), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
        }
    }
}

// Ventana "Tienda": las skins del logo se compran con los G de los logros (bronce, oro y diamante).
// Lo comprado y lo gastado se guarda en las estadísticas cifradas; la skin elegida, en dvd_config.json.
class VentanaTienda : Form
{
    static readonly string[] ids = TodasLasSkins();   // "" (normal) + las 8 skins
    readonly Bitmap[] vistas = new Bitmap[ids.Length];
    readonly Button[] botones = new Button[ids.Length];
    const int PorFila = 5;
    static string[] TodasLasSkins() { string[] r = new string[DvdConfig.SkinsId.Length + 1]; r[0] = ""; Array.Copy(DvdConfig.SkinsId, 0, r, 1, DvdConfig.SkinsId.Length); return r; }
    static Point Lugar(int i) { return new Point(16 + (i % PorFila) * (AnchoCaja + 10), Arriba + (i / PorFila) * (AltoCaja + 12)); }
    readonly Action<string> alElegir;
    string actual;
    EstadisticasDvd est;
    readonly System.Windows.Forms.Timer brillo = new System.Windows.Forms.Timer();
    readonly Stopwatch reloj = Stopwatch.StartNew();
    const int AnchoCaja = 172, AltoCaja = 270, Arriba = 92;

    public VentanaTienda(Icon icono, string skinActual, Action<string> alElegir)
    {
        this.alElegir = alElegir; actual = skinActual ?? "";
        Text = Idioma.Ingles ? "Shop" : "Tienda";
        Icon = icono;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(24, 24, 30); ForeColor = Color.FromArgb(235, 235, 240);
        int filas = (ids.Length + PorFila - 1) / PorFila;
        ClientSize = new Size(16 + PorFila * (AnchoCaja + 10) + 6, Arriba + filas * (AltoCaja + 12) + 8);
        DoubleBuffered = true;
        Bitmap logo = null;
        try { using (Stream s = typeof(VentanaTienda).Assembly.GetManifestResourceStream("logo_dvd.png")) if (s != null) logo = new Bitmap(s); } catch { }
        for (int i = 0; i < ids.Length; i++)
        {
            vistas[i] = HacerVista(logo, ids[i]);
            int k = i;
            Button b = new Button();
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 1;
            b.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            b.Size = new Size(AnchoCaja - 24, 36);
            Point p0 = Lugar(i);
            b.Location = new Point(p0.X + 12, p0.Y + AltoCaja - 48);
            b.Click += delegate { Tocar(k); };
            botones[i] = b; Controls.Add(b);
        }
        if (logo != null) logo.Dispose();
        Recargar();
        brillo.Interval = 40;
        brillo.Tick += delegate { for (int f = 0; f < filas; f++) Invalidate(new Rectangle(0, Arriba + f * (AltoCaja + 12), ClientSize.Width, 140)); };
        Shown += delegate { brillo.Start(); };
        FormClosed += delegate { brillo.Stop(); brillo.Dispose(); foreach (Bitmap v in vistas) if (v != null) v.Dispose(); };
    }

    // el logo de DVD en blanco y luego de metal (igual que lo pinta el DVD)
    static Bitmap HacerVista(Bitmap logo, string skin)
    {
        Bitmap b = new Bitmap(AnchoCaja - 30, 76, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        if (logo == null) return b;
        using (Graphics g = Graphics.FromImage(b))
        using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            Color c = skin == "" ? Color.FromArgb(0, 191, 255) : Color.White;   // sin skin: el azul de siempre
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 } }));
            float k = Math.Min((b.Width - 8f) / logo.Width, (b.Height - 8f) / logo.Height);
            int w = (int)(logo.Width * k), h = (int)(logo.Height * k);
            g.DrawImage(logo, new Rectangle((b.Width - w) / 2, (b.Height - h) / 2, w, h), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
        }
        if (skin != "") DvdConfig.Metalizar(b, skin);
        return b;
    }

    void Recargar()
    {
        try { est = EstadisticasDvd.Load(); } catch { est = new EstadisticasDvd(); }
        if (actual != "" && !est.TieneSkin(actual)) actual = "";   // una skin que no compraste no cuenta
        for (int i = 0; i < ids.Length; i++)
        {
            Button b = botones[i];
            bool tiene = i == 0 || est.TieneSkin(ids[i]);
            int precio = i == 0 ? 0 : DvdConfig.SkinsPrecio[i - 1];
            Color col = i == 0 ? Color.FromArgb(0, 191, 255) : DvdConfig.ColorSkin(ids[i]);
            b.Enabled = true;
            if (tiene && actual == ids[i]) { b.Text = Idioma.Ingles ? "In use ✓" : "En uso ✓"; b.BackColor = Color.FromArgb(40, 70, 50); b.ForeColor = Color.FromArgb(140, 240, 160); b.Enabled = false; }
            else if (tiene) { b.Text = Idioma.Ingles ? "Use" : "Usar"; b.BackColor = Color.FromArgb(44, 44, 54); b.ForeColor = Color.White; }
            else if (est.SaldoG >= precio) { b.Text = (Idioma.Ingles ? "Buy · " : "Comprar · ") + precio + " G"; b.BackColor = Color.FromArgb(60, col); b.ForeColor = col; }
            else { b.Text = (Idioma.Ingles ? "Missing " : "Te faltan ") + (precio - est.SaldoG) + " G"; b.BackColor = Color.FromArgb(32, 32, 38); b.ForeColor = Color.FromArgb(130, 130, 145); b.Enabled = false; }
            b.FlatAppearance.BorderColor = Color.FromArgb(120, b.ForeColor);
        }
        Invalidate();
    }

    void Tocar(int i)
    {
        string id = ids[i];
        if (i > 0 && !est.TieneSkin(id))
        {
            int precio = DvdConfig.SkinsPrecio[i - 1];
            string nombre = Idioma.Ingles ? DvdConfig.SkinsNombreEn[i - 1] : DvdConfig.SkinsNombre[i - 1];
            if (MessageBox.Show(this, (Idioma.Ingles ? "Buy the " + nombre + " for " : "¿Comprar la " + nombre + " por ") + precio + " G?" + (Idioma.Ingles ? "" : ""),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (!EstadisticasDvd.Comprar(id, precio)) { MessageBox.Show(this, Idioma.Ingles ? "You don't have enough G." : "No te alcanzan los G.", Text); Recargar(); return; }
        }
        actual = id;
        if (alElegir != null) alElegir(id);
        Recargar();
    }

    protected override void OnPaint(PaintEventArgs ev)
    {
        Graphics g = ev.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color oro = Color.FromArgb(255, 210, 90), gris = Color.FromArgb(150, 150, 165);
        using (Font titulo = new Font("Segoe UI", 20f, FontStyle.Bold))
        using (Font sub = new Font("Segoe UI", 11f, FontStyle.Bold))
        using (Font nombre = new Font("Segoe UI", 12f, FontStyle.Bold))
        using (Font chica = new Font("Segoe UI", 9f))
        {
            TextRenderer.DrawText(g, Text, titulo, new Point(18, 10), Color.White);
            TextRenderer.DrawText(g, (Idioma.Ingles ? "You have " : "Tienes ") + est.SaldoG + " G", sub, new Point(22, 52), oro);
            TextRenderer.DrawText(g, Idioma.Ingles ? "(G are earned with achievements)" : "(los G se ganan con los logros)", chica, new Point(160, 55), gris);
            string[] nombres = Idioma.Ingles
                ? new[] { "Normal", "Bronze", "Silver", "Gold", "Diamond", "Neon", "Fire", "Rainbow", "Hologram" }
                : new[] { "Normal", "Bronce", "Plata", "Oro", "Diamante", "Neón", "Fuego", "Arcoíris", "Holograma" };
            string[] desc = Idioma.Ingles
                ? new[] { "Your DVD with its normal colors", "Polished copper metal", "Shiny polished silver", "Shiny solid gold", "Ice-blue diamond with sparkles",
                          "Glowing pink and blue light tube", "Flames from yellow to red", "Every color, with relief", "Iridescent, changes color" }
                : new[] { "Tu DVD con sus colores de siempre", "Metal cobrizo pulido", "Plata pulida y brillante", "Oro brillante", "Diamante azul hielo con destellos",
                          "Tubo de luz rosa y celeste", "Llamas de amarillo a rojo", "Todos los colores, con relieve", "Tornasol que cambia de color" };
            double t = reloj.Elapsed.TotalSeconds;
            for (int i = 0; i < ids.Length; i++)
            {
                Point p0 = Lugar(i);
                Rectangle caja = new Rectangle(p0.X, p0.Y, AnchoCaja, AltoCaja);
                bool tiene = i == 0 || est.TieneSkin(ids[i]);
                Color col = i == 0 ? Color.FromArgb(0, 191, 255) : DvdConfig.ColorSkin(ids[i]);
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(caja, Color.FromArgb(70, col), Color.FromArgb(28, 28, 34), 90f))
                    g.FillRectangle(b, caja);
                using (Pen p = new Pen(Color.FromArgb(actual == ids[i] ? 230 : 90, col), actual == ids[i] ? 2.5f : 1f)) g.DrawRectangle(p, caja);
                // vista del logo + franja de brillo que pasa
                Rectangle rv = new Rectangle(caja.Left + 15, caja.Top + 22, vistas[i].Width, vistas[i].Height);
                g.DrawImage(vistas[i], rv);
                if (i > 0)
                {
                    double ciclo = ((t + i * 0.7) % 2.8) / 0.8;
                    if (ciclo < 1)
                    {
                        System.Drawing.Drawing2D.GraphicsState gs = g.Save();
                        g.SetClip(rv);
                        float x = (float)(rv.Left - 40 + ciclo * (rv.Width + 80));
                        using (System.Drawing.Drawing2D.LinearGradientBrush lb = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(x, rv.Top, 30, rv.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                        {
                            System.Drawing.Drawing2D.ColorBlend cb = new System.Drawing.Drawing2D.ColorBlend();
                            cb.Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                            cb.Positions = new[] { 0f, 0.5f, 1f };
                            lb.InterpolationColors = cb;
                            g.FillRectangle(lb, x, rv.Top, 30, rv.Height);
                        }
                        g.Restore(gs);
                    }
                }
                TextFormatFlags centro = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
                TextRenderer.DrawText(g, nombres[i], nombre, new Rectangle(caja.Left, caja.Top + 112, caja.Width, 24), Color.White, centro);
                TextRenderer.DrawText(g, desc[i], chica, new Rectangle(caja.Left + 8, caja.Top + 138, caja.Width - 16, 36), gris, centro);
                string precio = i == 0 ? (Idioma.Ingles ? "Free" : "Gratis") : DvdConfig.SkinsPrecio[i - 1] + " G";
                TextRenderer.DrawText(g, tiene && i > 0 ? (Idioma.Ingles ? "Bought" : "Comprada") : precio, sub, new Rectangle(caja.Left, caja.Top + 182, caja.Width, 26), tiene ? Color.FromArgb(140, 240, 160) : oro, centro);
            }
        }
    }
}
