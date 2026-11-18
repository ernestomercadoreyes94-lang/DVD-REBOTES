// "Buscar actualizaciones" de dvd_config.exe: revisa el lanzamiento marcado como "Latest" en GitHub
// (DVD-REBOTES) y, si trae un DvdRebote.exe o dvd_config.exe MÁS NUEVO que el tuyo, lo descarga,
// revisa que llegó completo (huella SHA-256 que da GitHub), cierra el DVD, cambia los archivos y lo
// vuelve a abrir. Los archivos viejos se renombran a .viejo (Windows deja renombrar un programa
// abierto) y se borran la próxima vez que se abre la configuración.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

static class Actualizador
{
    // TODOS los lanzamientos (también las pre-releases): se usa el más nuevo
    const string Api = "https://api.github.com/repos/ernestomercadoreyes94-lang/DVD-REBOTES/releases?per_page=20";
    static bool esPrerelease;   // el lanzamiento encontrado es de prueba (puede tener errores)

    class Archivo { public string Nombre, Url, Huella, Local; public DateTime Subido; }
    static string paginaLanzamiento = "";   // la página del lanzamiento en GitHub (ahí vienen los cambios)

    // "¿Quieres ver los cambios?": con Sí abre en el navegador la página del lanzamiento que se descargó
    static void OfrecerCambios(Form ventana)
    {
        if (cambiosVistos) return;   // ya se los enseñé antes de instalar
        if (string.IsNullOrEmpty(paginaLanzamiento)) return;
        if (Msg.Show(ventana, "¿Quieres ver los cambios?", "Buscar actualizaciones",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { Process.Start(paginaLanzamiento); } catch { }
    }

    static string cambios = "";       // las notas del lanzamiento (lo que trae de nuevo), ya sin el formato de GitHub
    static bool cambiosVistos;        // si ya se enseñaron, después de instalar no se vuelve a preguntar

    // las notas de GitHub vienen en Markdown (##, **, - ...): se dejan como texto normal y se quita
    // la parte de "Cómo actualizar / Cómo instalar", que no sirve si ya lo estás actualizando
    static string LimpiarNotas(string md)
    {
        if (string.IsNullOrEmpty(md)) return "";
        var sb = new System.Text.StringBuilder();
        bool blanco = false;
        foreach (string crudo in md.Replace("\r", "").Split('\n'))
        {
            string l = crudo.Trim();
            if (l.StartsWith("#"))
            {
                string t = l.TrimStart('#').Trim();
                if (t.StartsWith("Cómo", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Como", StringComparison.OrdinalIgnoreCase)) break;
                if (l.StartsWith("## ") || l.StartsWith("# ")) continue;   // el título ya sale arriba
                l = t.ToUpperInvariant();
            }
            else if (l.StartsWith("- ") || l.StartsWith("* ")) l = "•  " + l.Substring(2);
            l = l.Replace("**", "").Replace("`", "");
            if (l.Length == 0) { if (!blanco && sb.Length > 0) sb.Append("\r\n"); blanco = true; continue; }
            blanco = false;
            sb.Append(l).Append("\r\n");
        }
        return sb.ToString().Trim();
    }

    // ventana de "¡Hay una versión nueva!" con los cambios; si no se pudieron leer, es el cuadro de antes
    static bool PreguntarInstalar(Form ventana, List<Archivo> nuevos, string titulo)
    {
        string encabezado = "¡Hay una versión nueva" + (titulo.Length > 0 ? " (" + titulo + ")" : "") + "!";
        if (cambios.Length == 0)
        {
            string lista = "";
            foreach (Archivo a in nuevos) lista += "\n   • " + a.Nombre + "  (subido el " + a.Subido.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ")";
            return Msg.Show(ventana, encabezado + lista + "\n\n¿Instalarla ahora? El DVD se cierra un momento y se vuelve a abrir.",
                "Buscar actualizaciones", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        using (Form f = new Form())
        {
            f.Text = "Buscar actualizaciones";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.Font = ventana.Font;
            f.ClientSize = new System.Drawing.Size(480, 400);
            f.Icon = ventana.Icon;

            Label arriba = new Label
            {
                Text = encabezado, AutoSize = false, Location = new System.Drawing.Point(16, 14), Size = new System.Drawing.Size(448, 26),
                Font = new System.Drawing.Font(f.Font.FontFamily, 12f, System.Drawing.FontStyle.Bold)
            };
            Label sub = new Label { Text = "Esto es lo que cambia:", AutoSize = true, Location = new System.Drawing.Point(16, 44) };
            TextBox notas = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
                Location = new System.Drawing.Point(16, 66), Size = new System.Drawing.Size(448, 250),
                BackColor = System.Drawing.SystemColors.Window, Text = cambios, TabStop = false
            };
            Label abajo = new Label
            {
                Text = "¿Instalarla ahora? El DVD se cierra un momento y se vuelve a abrir.", AutoSize = false,
                Location = new System.Drawing.Point(16, 324), Size = new System.Drawing.Size(448, 20)
            };
            Button si = new Button { Text = "Instalar", DialogResult = DialogResult.Yes, Location = new System.Drawing.Point(268, 354), Size = new System.Drawing.Size(96, 30) };
            Button no = new Button { Text = "Ahora no", DialogResult = DialogResult.No, Location = new System.Drawing.Point(368, 354), Size = new System.Drawing.Size(96, 30) };
            LinkLabel enGithub = new LinkLabel { Text = "Ver en GitHub", AutoSize = true, Location = new System.Drawing.Point(16, 362) };
            enGithub.LinkClicked += delegate { try { Process.Start(paginaLanzamiento); } catch { } };
            enGithub.Visible = paginaLanzamiento.Length > 0;

            f.Controls.AddRange(new Control[] { arriba, sub, notas, abajo, si, no, enGithub });
            f.AcceptButton = si; f.CancelButton = no;
            f.Shown += delegate { notas.SelectionLength = 0; si.Focus(); };
            cambiosVistos = true;
            return f.ShowDialog(ventana) == DialogResult.Yes;
        }
    }

    // se llama desde el botón: hace todo en otro hilo para que la ventana no se congele
    static bool ocupado;   // ya está buscando o instalando: otro clic al botón no hace nada (si no, salían los cuadros dos veces)

    // Ahora todo pasa en una CONSOLA FALSA (estilo cmd): "Buscando updates...", y si hay versión nueva
    // enseña los cambios y pregunta "¿Quieres instalar la actualización? (S/N)". S = sí, N = no.
    public static void Buscar(Form ventana, Action<string> estado)
    {
        if (ocupado) return;
        ocupado = true;
        ConsolaUpdates con = new ConsolaUpdates();
        con.Show(ventana);
        Action<string> estadoOriginal = estado;
        estado = s =>
        {
            estadoOriginal(s);
            if (!con.IsDisposed && s != "Buscando actualizaciones...") con.Linea("  " + s);
            if (s != "Buscando actualizaciones..." && s != "Descargando..." && s != "Instalando...")
            {
                ocupado = false;
                if (!con.IsDisposed && (s.StartsWith("Actualizado") || s.StartsWith("No se pudo actualizar"))) con.Terminar();
            }
        };
        con.Escribir(new[] {
            "Microsoft Windows [Versión 10.0]",
            "",
            @"C:\DVD> buscar_updates.exe",
            "",
            "Buscando updates...",
            "Conectando con GitHub (DVD-REBOTES)..." });
        estado("Buscando actualizaciones...");
        Thread t = new Thread(delegate ()
        {
            string error = null; List<Archivo> nuevos = null; string titulo = "";
            try { nuevos = QueHayNuevo(out titulo); }
            catch (Exception ex) { error = ex.Message; }
            Thread.Sleep(900);   // que se alcance a leer "Buscando updates..."
            ventana.BeginInvoke((Action)delegate
            {
                if (con.IsDisposed) { ocupado = false; return; }
                if (error != null)
                {
                    estado("No se pudo revisar");
                    con.Escribir(new[] { "", "ERROR: no pude revisar si hay updates.", "Revisa tu internet e inténtalo otra vez.", "(" + error + ")" });
                    con.Terminar();
                    return;
                }
                if (nuevos.Count == 0)
                {
                    estado("Ya tienes la versión más nueva ✓");
                    con.Escribir(new[] { "", "Ya tienes la versión más nueva del DVD. No hay nada que instalar." });
                    con.Terminar();
                    return;
                }
                // hay versión nueva: qué trae y la pregunta S/N
                List<string> l = new List<string> { "", "¡Encontré una versión nueva" + (titulo.Length > 0 ? ": " + titulo : "") + "!", "" };
                if (cambios.Length > 0)
                {
                    l.Add("Esto es lo que cambia:");
                    foreach (string c in cambios.Replace("\r", "").Split('\n')) l.Add("  " + c);
                    l.Add("");
                    cambiosVistos = true;
                }
                foreach (Archivo a in nuevos) l.Add("  > " + a.Nombre + "  (subido el " + a.Subido.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ")");
                l.Add("");
                if (esPrerelease)
                {
                    l.Add("!! AVISO: esta versión es una PRE-RELEASE (versión de prueba).");
                    l.Add("!! Puede tener errores. Si algo falla, instala la versión normal desde GitHub.");
                    l.Add("");
                }
                con.Escribir(l.ToArray());
                con.Preguntar("¿Quieres instalar la actualización? (S/N): ", delegate (bool si)
                {
                    if (!si)
                    {
                        estado("Actualización pendiente");
                        con.Escribir(new[] { "", "Actualización cancelada. Cuando quieras, vuelve a darle a Buscar actualizaciones." });
                        con.Terminar();
                        return;
                    }
                    con.Escribir(new[] { "", "Instalando la actualización (el DVD se cierra un momento y se vuelve a abrir)..." });
                    Instalar(ventana, nuevos, estado);
                });
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    static WebClient Cliente()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 (GitHub lo pide)
        WebClient w = new WebClient();
        w.Headers[HttpRequestHeader.UserAgent] = "DVD-Rebote-Actualizador";
        w.Encoding = System.Text.Encoding.UTF8;   // GitHub manda UTF-8; sin esto los acentos salían como "Ã³"
        return w;
    }

    // qué archivos del lanzamiento son más nuevos que los tuyos
    static List<Archivo> QueHayNuevo(out string titulo)
    {
        string json;
        using (WebClient w = Cliente()) json = w.DownloadString(Api);
        // de la lista, el lanzamiento publicado más nuevo (normal o pre-release; los borradores no)
        Dictionary<string, object> datos = null; string masNuevo = "";
        foreach (object o in (object[])new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json))
        {
            var r = (Dictionary<string, object>)o;
            if (r.ContainsKey("draft") && r["draft"] is bool && (bool)r["draft"]) continue;
            string fecha = r.ContainsKey("published_at") && r["published_at"] != null ? Convert.ToString(r["published_at"]) : "";
            if (datos == null || string.CompareOrdinal(fecha, masNuevo) > 0) { datos = r; masNuevo = fecha; }
        }
        if (datos == null) throw new Exception("No hay lanzamientos en GitHub");
        esPrerelease = datos.ContainsKey("prerelease") && datos["prerelease"] is bool && (bool)datos["prerelease"];
        titulo = Convert.ToString(datos.ContainsKey("name") ? datos["name"] : "") + (esPrerelease ? " (pre-release)" : "");
        paginaLanzamiento = datos.ContainsKey("html_url") ? Convert.ToString(datos["html_url"]) : "";
        cambios = LimpiarNotas(datos.ContainsKey("body") ? Convert.ToString(datos["body"]) : "");
        cambiosVistos = false;
        var lista = new List<Archivo>();
        string rebote = DvdConfig.RutaRebote();
        string config = Application.ExecutablePath;
        foreach (object o in (object[])datos["assets"])
        {
            var a = (Dictionary<string, object>)o;
            string nombre = Convert.ToString(a["name"]);
            string local = nombre.Equals("DvdRebote.exe", StringComparison.OrdinalIgnoreCase) ? rebote
                         : nombre.Equals("dvd_config.exe", StringComparison.OrdinalIgnoreCase) ? config : null;
            if (local == null) continue;   // la APK y lo demás no son para la PC
            Archivo ar = new Archivo
            {
                Nombre = nombre, Local = local,
                Url = Convert.ToString(a["browser_download_url"]),
                Huella = a.ContainsKey("digest") && a["digest"] != null ? Convert.ToString(a["digest"]).Replace("sha256:", "").ToLowerInvariant() : "",
                Subido = DateTime.Parse(Convert.ToString(a["updated_at"]), null, System.Globalization.DateTimeStyles.AdjustToUniversal)
            };
            if (!File.Exists(local)) { lista.Add(ar); continue; }
            if (ar.Huella.Length > 0 && ar.Huella == Sha256(local)) continue;          // es el mismo archivo
            if (ar.Subido <= HoraDeCompilado(local).AddMinutes(1)) continue;            // el de GitHub es más viejo que el tuyo
            lista.Add(ar);
        }
        return lista;
    }

    static void Instalar(Form ventana, List<Archivo> nuevos, Action<string> estado)
    {
        estado("Descargando...");
        Thread t = new Thread(delegate ()
        {
            string error = null;
            var descargados = new List<KeyValuePair<Archivo, string>>();
            try
            {
                using (WebClient w = Cliente())
                    foreach (Archivo a in nuevos)
                    {
                        string tmp = a.Local + ".nuevo";
                        try { w.DownloadFile(a.Url, tmp); }
                        catch { try { File.Delete(tmp); } catch { } throw; }   // que no quede un .nuevo a medias
                        if (a.Huella.Length > 0 && Sha256(tmp) != a.Huella)   // llegó incompleto o cambiado: no se instala
                        {
                            try { File.Delete(tmp); } catch { }
                            throw new Exception(a.Nombre + " no se descargó bien (la huella no coincide).");
                        }
                        descargados.Add(new KeyValuePair<Archivo, string>(a, tmp));
                    }
            }
            catch (Exception ex) { error = ex.Message; foreach (var d in descargados) { try { File.Delete(d.Value); } catch { } } }
            // qué ajustes trae la versión nueva (se le pregunta al .exe recién bajado)
            string campos = null;
            if (error == null) { try { campos = CamposNuevos(descargados); } catch { } }
            // se cierra el DVD aquí (en segundo plano), así la ventana no se congela mientras espera
            bool dvdAbierto = false;
            if (error == null)
            {
                dvdAbierto = Process.GetProcessesByName("DvdRebote").Length > 0;
                if (dvdAbierto)
                {
                    try { EventWaitHandle h; if (EventWaitHandle.TryOpenExisting("DvdRebote_Salir", out h)) { h.Set(); h.Dispose(); } } catch { }
                    for (int i = 0; i < 50 && Process.GetProcessesByName("DvdRebote").Length > 0; i++) Thread.Sleep(100);
                }
            }
            ventana.BeginInvoke((Action)delegate
            {
                if (error != null)
                {
                    estado("No se pudo actualizar");
                    Msg.Show(ventana, "No se pudo descargar la actualización:\n" + error, "Buscar actualizaciones",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                // si la versión nueva cambia los ajustes, se pregunta si se rehace el dvd_config.json
                string rehacer = null;
                if (campos != null && CambianAjustes(campos) &&
                    Msg.Show(ventana, "Esta versión cambia los ajustes de la configuración.\n\n" +
                        "¿Quieres eliminar el dvd_config.json viejo y poner uno nuevo?\n\n" +
                        "Tus ajustes se quedan igual: solo se agregan los nuevos y se quitan los que ya no se usan.",
                        "Buscar actualizaciones", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    rehacer = campos;
                Cambiar(ventana, descargados, estado, dvdAbierto, rehacer);
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    // cierra el DVD, cambia los archivos y vuelve a abrir lo que estaba abierto
    static void Cambiar(Form ventana, List<KeyValuePair<Archivo, string>> descargados, Action<string> estado, bool dvdAbierto, string rehacer)
    {
        estado("Instalando...");
        bool configCambiada = false; string rebote = null;
        try
        {
            foreach (var d in descargados)
            {
                string destino = d.Key.Local;
                if (File.Exists(destino))
                {
                    string viejo = destino + ".viejo";
                    try { if (File.Exists(viejo)) File.Delete(viejo); } catch { viejo = destino + "." + DateTime.Now.Ticks + ".viejo"; }
                    File.Move(destino, viejo);   // un programa abierto sí se puede renombrar
                }
                File.Move(d.Value, destino);
                if (d.Key.Nombre.Equals("dvd_config.exe", StringComparison.OrdinalIgnoreCase)) configCambiada = true;
                else rebote = destino;
            }
        }
        catch (Exception ex)
        {
            estado("No se pudo actualizar");
            Msg.Show(ventana, "No se pudieron cambiar los archivos:\n" + ex.Message, "Buscar actualizaciones",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (rehacer != null) { try { RehacerJson(rehacer); } catch { } }   // antes de volver a abrir el DVD
        // si tienes el protector de pantalla (DVD Rebote.scr junto al DVD), también se pone al día
        if (rebote != null)
        {
            try
            {
                string scr = Path.Combine(Path.GetDirectoryName(rebote), "DVD Rebote.scr");
                if (File.Exists(scr)) File.Copy(rebote, scr, true);
            }
            catch { }
        }
        if (dvdAbierto) { string r = rebote ?? DvdConfig.RutaRebote(); if (r != null) try { Process.Start(r); } catch { } }
        PonerVersionEnAplicaciones();
        estado("Actualizado ✓");
        if (configCambiada)
        {
            Msg.Show(ventana, "¡Listo, ya tienes la versión nueva!\n\nLa configuración se va a volver a abrir.", "Buscar actualizaciones",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            OfrecerCambios(ventana);
            try { Process.Start(Application.ExecutablePath); } catch { }
            Application.Exit();
        }
        else
        {
            Msg.Show(ventana, "¡Listo, ya tienes la versión nueva del DVD!", "Buscar actualizaciones",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            OfrecerCambios(ventana);
        }
    }

    // le pide al .exe recién bajado (dvd_config o DvdRebote) sus ajustes con "--campos". Una versión vieja
    // que no lo entiende no escribe nada: se cierra a los 5 segundos y no se pregunta nada.
    static string CamposNuevos(List<KeyValuePair<Archivo, string>> descargados)
    {
        string exe = null;
        foreach (var d in descargados) if (d.Key.Nombre.Equals("dvd_config.exe", StringComparison.OrdinalIgnoreCase)) exe = d.Value;
        if (exe == null) foreach (var d in descargados) if (d.Key.Nombre.Equals("DvdRebote.exe", StringComparison.OrdinalIgnoreCase)) exe = d.Value;
        if (exe == null) return null;
        string salida = Path.Combine(Path.GetTempPath(), "dvd_campos_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, "--campos \"" + salida + "\"");
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return null; }
            return File.Exists(salida) ? File.ReadAllText(salida) : null;
        }
        finally { try { File.Delete(salida); } catch { } }
    }

    static Dictionary<string, object> Json(string texto)
    {
        return (Dictionary<string, object>)new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(texto);
    }

    // ¿la versión nueva tiene ajustes distintos (nuevos o quitados) a los de tu dvd_config.json?
    static bool CambianAjustes(string campos)
    {
        try
        {
            if (!File.Exists(DvdConfig.RutaConfig)) return false;
            var nuevos = Json(campos);
            var tuyos = Json(File.ReadAllText(DvdConfig.RutaConfig));
            if (nuevos == null || tuyos == null) return false;
            if (nuevos.Count != tuyos.Count) return true;
            foreach (string k in nuevos.Keys) if (!tuyos.ContainsKey(k)) return true;
            return false;
        }
        catch { return false; }   // JSON ocupado o roto: no se pregunta (se instala igual)
    }

    // borra el dvd_config.json viejo y pone uno nuevo con los ajustes de la versión nueva, en su orden:
    // cada ajuste que ya tenías conserva tu valor, los nuevos van con su valor de fábrica y los que ya no
    // existen se quitan
    static void RehacerJson(string campos)
    {
        var nuevos = Json(campos);
        Dictionary<string, object> tuyos = null;
        try { tuyos = Json(File.ReadAllText(DvdConfig.RutaConfig)); } catch { }
        if (nuevos == null) return;
        var js = new System.Web.Script.Serialization.JavaScriptSerializer();
        var sb = new System.Text.StringBuilder("{\n");
        int i = 0;
        foreach (var par in nuevos)
        {
            object valor = tuyos != null && tuyos.ContainsKey(par.Key) ? tuyos[par.Key] : par.Value;
            sb.Append("  ").Append(js.Serialize(par.Key)).Append(": ").Append(js.Serialize(valor));
            sb.Append(++i < nuevos.Count ? ",\n" : "\n");
        }
        sb.Append("}\n");
        try { File.Delete(DvdConfig.RutaConfig); } catch { }
        DvdConfig.WriteAtomic(DvdConfig.RutaConfig, sb.ToString());
    }

    // si el DVD se instaló con el instalador, también se cambia la versión que sale en
    // Configuración → Aplicaciones (si no, seguiría diciendo la versión vieja)
    static void PonerVersionEnAplicaciones()
    {
        try
        {
            string config = DvdConfig.RutaConfigApp() ?? Application.ExecutablePath;
            FileVersionInfo fv = FileVersionInfo.GetVersionInfo(config);
            string v = fv.FileMajorPart + "." + fv.FileMinorPart;
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DvdRebote", true))
                if (k != null) k.SetValue("DisplayVersion", v);
        }
        catch { }
    }

    // borra los .viejo de la actualización anterior y los .nuevo que quedaron de una descarga que falló
    public static void LimpiarViejos()
    {
        foreach (string exe in new[] { Application.ExecutablePath, DvdConfig.RutaRebote() })
        {
            if (string.IsNullOrEmpty(exe)) continue;
            try
            {
                foreach (string patron in new[] { "*.viejo", ".nuevo" })
                    foreach (string f in Directory.GetFiles(Path.GetDirectoryName(exe), Path.GetFileName(exe) + patron))
                        try { File.Delete(f); } catch { }
            }
            catch { }
        }
    }

    static string Sha256(string archivo)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (FileStream f = File.OpenRead(archivo))
            return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }

    // la hora en que se compiló ese .exe (viene escrita dentro del propio archivo)
    static DateTime HoraDeCompilado(string exe)
    {
        try
        {
            byte[] b = new byte[2048];
            using (FileStream f = File.OpenRead(exe)) f.Read(b, 0, b.Length);
            int pe = BitConverter.ToInt32(b, 0x3C);
            int segundos = BitConverter.ToInt32(b, pe + 8);
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(segundos);
        }
        catch { return File.GetLastWriteTimeUtc(exe); }
    }
}

// Consola falsa (estilo cmd) de "Buscar actualizaciones": las líneas salen una por una, y cuando pregunta
// algo espera que escribas S o N y Enter. Al final: "Presiona cualquier tecla para cerrar...".
class ConsolaUpdates : Form
{
    readonly List<string> lineas = new List<string>();
    readonly Queue<string> porEscribir = new Queue<string>();
    readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();
    readonly System.Drawing.Font letra = new System.Drawing.Font("Consolas", 11f);
    const int MaxCar = 92;
    string pregunta; Action<bool> respuesta; string tecleado = "";
    bool terminado, esperandoCerrar;
    int subidas;   // cuántas líneas subiste (0 = abajo del todo, donde va escribiendo)

    // SUBIR / BAJAR: rueda del ratón, flechas, Re Pág / Av Pág, Inicio / Fin
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        subidas = Math.Max(0, subidas + (e.Delta > 0 ? 3 : -3));
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys tecla)
    {
        int pagina = Math.Max(1, (ClientSize.Height - 12) / Math.Max(1, TextRenderer.MeasureText("Ag", letra).Height) - 1);
        switch (tecla)
        {
            case Keys.Up: subidas++; break;
            case Keys.Down: subidas = Math.Max(0, subidas - 1); break;
            case Keys.PageUp: subidas += pagina; break;
            case Keys.PageDown: subidas = Math.Max(0, subidas - pagina); break;
            case Keys.Home: subidas = int.MaxValue / 2; break;
            case Keys.End: subidas = 0; break;
            default: return base.ProcessCmdKey(ref msg, tecla);
        }
        Invalidate();
        return true;
    }

    public ConsolaUpdates()
    {
        Text = @"C:\DVD\buscar_updates.exe";
        ClientSize = new System.Drawing.Size(780, 430);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = System.Drawing.Color.Black;
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        reloj.Interval = 45;
        reloj.Tick += delegate
        {
            if (porEscribir.Count > 0) lineas.Add(porEscribir.Dequeue());   // una línea a la vez
            else if (pregunta != null && respuesta != null && !lineas.Contains("\u0001")) { }
            Invalidate();
        };
        reloj.Start();
        KeyPress += AlTeclear;
        FormClosed += delegate
        {
            reloj.Stop(); reloj.Dispose();
            if (respuesta != null) { Action<bool> r = respuesta; respuesta = null; r(false); }   // cerrar = no
        };
    }

    // agrega líneas (las largas se parten para que quepan)
    public void Escribir(string[] ls)
    {
        if (IsDisposed) return;
        foreach (string l0 in ls)
        {
            string l = System.Text.RegularExpressions.Regex.Replace(l0 ?? "", @"[\uD800-\uDFFF]", "");   // sin emojis (en la consola salen como cuadritos)
            { string nucleo = l.TrimStart(); l = l.Substring(0, l.Length - nucleo.Length) + Idioma.T(nucleo); }   // al inglés si está puesto
            string sangria = l.StartsWith("  ") ? "    " : "";
            while (l.Length > MaxCar)
            {
                int corte = l.LastIndexOf(' ', MaxCar);
                if (corte < 20) corte = MaxCar;
                porEscribir.Enqueue(l.Substring(0, corte));
                l = sangria + l.Substring(corte).TrimStart();
            }
            porEscribir.Enqueue(l);
        }
    }
    public void Linea(string l) { Escribir(new[] { l }); }

    public void Preguntar(string texto, Action<bool> alResponder)
    {
        if (IsDisposed) return;
        pregunta = Idioma.T(texto); respuesta = alResponder; tecleado = "";
        Activate();
    }

    public void Terminar()
    {
        if (IsDisposed || terminado) return;
        terminado = true;
        Escribir(new[] { "", "Presiona cualquier tecla para cerrar..." });
        esperandoCerrar = true;
        Activate();
    }

    void AlTeclear(object s, KeyPressEventArgs e)
    {
        if (porEscribir.Count > 0) return;   // todavía está escribiendo
        subidas = 0;                          // al escribir, regresa abajo (donde está la pregunta)
        if (respuesta != null)
        {
            char c = char.ToUpperInvariant(e.KeyChar);
            if (c == 'S' || c == 'N' || (Idioma.Ingles && c == 'Y')) tecleado = c.ToString();   // en inglés: Y = yes
            else if (e.KeyChar == '\b') tecleado = "";
            else if (e.KeyChar == '\r' && tecleado.Length > 0)
            {
                lineas.Add(pregunta + tecleado);
                bool si = tecleado == "S" || tecleado == "Y";
                Action<bool> r = respuesta; respuesta = null; pregunta = null;
                r(si);
            }
            Invalidate();
            return;
        }
        if (esperandoCerrar && terminado) Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        System.Drawing.Graphics g = e.Graphics;
        System.Drawing.Color gris = System.Drawing.Color.FromArgb(204, 204, 204);
        int alto = TextRenderer.MeasureText("Ag", letra).Height;
        List<string> todo = new List<string>(lineas);
        bool cursor = (Environment.TickCount / 500) % 2 == 0;
        if (pregunta != null && porEscribir.Count == 0) todo.Add(pregunta + tecleado + (cursor ? "_" : ""));
        else if (porEscribir.Count == 0) todo.Add(cursor ? "_" : "");
        int caben = (ClientSize.Height - 12) / alto;
        int maxSubir = Math.Max(0, todo.Count - caben);
        if (subidas > maxSubir) subidas = maxSubir;
        int desde = Math.Max(0, todo.Count - caben - subidas);   // abajo del todo, o lo que hayas subido
        // barra de desplazamiento a la derecha (solo si no cabe todo)
        if (maxSubir > 0)
        {
            int altoBarra = ClientSize.Height - 8;
            int altoPulgar = Math.Max(24, altoBarra * caben / todo.Count);
            int yPulgar = 4 + (altoBarra - altoPulgar) * (maxSubir - subidas) / maxSubir;
            using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(40, 40, 40))) g.FillRectangle(b, ClientSize.Width - 10, 4, 6, altoBarra);
            using (System.Drawing.SolidBrush b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(130, 130, 130))) g.FillRectangle(b, ClientSize.Width - 10, yPulgar, 6, altoPulgar);
        }
        int y = 6;
        for (int i = desde; i < todo.Count && i < desde + caben; i++)
        {
            System.Drawing.Color col = gris;
            string l = todo[i];
            if (l.StartsWith("¡Encontré") || l.StartsWith("Found a new")) col = System.Drawing.Color.FromArgb(110, 220, 130);
            else if (l.StartsWith("ERROR")) col = System.Drawing.Color.FromArgb(255, 110, 110);
            else if (l.StartsWith("!!")) col = System.Drawing.Color.FromArgb(255, 150, 60);   // aviso de pre-release (naranja)
            else if (l.StartsWith("¿Quieres instalar") || l.StartsWith("Do you want to install")) col = System.Drawing.Color.FromArgb(255, 210, 90);
            TextRenderer.DrawText(g, l, letra, new System.Drawing.Point(8, y), col, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            y += alto;
        }
    }
}
