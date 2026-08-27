namespace CRT.Core.Localization;

/// <summary>
/// Localization catalog — all four dictionaries ported in full from
/// <c>src/crt/language.py</c> (en, fr, pl, es), plus new English keys for the
/// native-only UI (video retimer, dashboard, segments, Speedrun.com). Missing
/// translations fall back to English, then to the key itself, matching the
/// Python translate behavior.
/// </summary>
public static class LanguageCatalog
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["Framerate"] = "Framerate (FPS)",
        ["Start Frame"] = "Start Frame",
        ["End Frame"] = "End Frame",
        ["Start Frame (Loads)"] = "Start Frame (Loads)",
        ["End Frame (Loads)"] = "End Frame (Loads)",
        ["Paste"] = "Paste",
        ["Paste Start Frame"] = "Paste Start Frame",
        ["Paste End Frame"] = "Paste End Frame",
        ["Paste Start Frame (Loads)"] = "Paste Start Frame (Loads)",
        ["Paste End Frame (Loads)"] = "Paste End Frame (Loads)",
        ["Copy Mod Note"] = "Copy Mod Note",
        ["Copy Discord Message"] = "Copy Discord Message",
        ["Copy YouTube Chapters"] = "Copy YouTube Chapters",
        ["Add Loads"] = "Add Loads",
        ["Add Load"] = "Add Load",
        ["Edit Loads"] = "Edit Loads",
        ["Without Loads"] = "Without Loads",
        ["With Loads"] = "With Loads",
        ["Click to Copy Time"] = "Click to Copy Time",
        ["File"] = "File",
        ["New Time"] = "New Time",
        ["Open Time"] = "Open Time",
        ["Session History"] = "Session History",
        ["Save"] = "Save",
        ["Save As"] = "Save As",
        ["Settings"] = "Settings",
        ["Exit"] = "Exit",
        ["Edit (Menu Bar)"] = "Edit",
        ["Clear Loads"] = "Clear Loads",
        ["View"] = "View",
        ["Always on Top"] = "Always on Top",
        ["Help"] = "Help",
        ["About"] = "About",
        ["Edit Load"] = "Edit Load",
        ["Save Edits"] = "Save Edits",
        ["Discard Changes"] = "Discard Changes",
        ["Edit"] = "Edit",
        ["Delete"] = "Delete",
        ["Loads"] = "Loads",
        ["File Name"] = "File Name",
        ["Cancel"] = "Cancel",
        ["CRT Settings"] = "CRT Settings",
        ["Automatically Check for Updates"] = "Automatically Check for Updates",
        ["Theme"] = "Theme",
        ["Automatic"] = "Automatic",
        ["Dark"] = "Dark",
        ["Light"] = "Light",
        ["Accent Color"] = "Accent Color",
        ["Language"] = "Language",
        ["Mod Note Format"] = "Mod Note Format",
        ["Restore Defaults"] = "Restore Defaults",
        ["Apply"] = "Apply",
        ["Hotkeys"] = "Hotkeys",
        ["Customize Hotkeys"] = "Customize Hotkeys",
        ["Press a Key Combination"] = "Press a key combination",
        ["Reset"] = "Reset",
        ["Reset All"] = "Reset All",
        ["OK"] = "OK",
        ["Duplicate Hotkey"] = "Duplicate Hotkey",
        ["Duplicate Hotkey Message"] = "The same key combination is assigned to more than one action: {names}",

        // ── New keys for the native rewrite (English fallback for all languages) ──
        ["Dashboard"] = "Dashboard",
        ["Frame Retimer"] = "Frame Retimer",
        ["Video Retimer"] = "Video Retimer",
        ["Segments"] = "Segments",
        ["Segment"] = "Segment",
        ["Load"] = "Load",
        ["Segment Mode"] = "Segment Mode",
        ["Load Mode"] = "Load Mode",
        ["Segment Total"] = "Segment Total",
        ["Full Run"] = "Full Run",
        ["Add Segment"] = "Add Segment",
        ["Segment Start"] = "Segment Start",
        ["Segment End"] = "Segment End",
        ["Clear Segments"] = "Clear Segments",
        ["Toggle Mode"] = "Toggle Mode",
        ["No loads yet"] = "No loads yet. Add one with the fields on the left.",
        ["No segments yet"] = "No segments yet. Add one with the fields on the left.",
        ["Mod note copied"] = "Mod note copied",
        ["Discord message copied"] = "Discord message copied",
        ["YouTube chapters copied"] = "YouTube chapters copied",
        ["Time copied"] = "Time copied",
        ["Copy Time"] = "Copy Time",
        ["Load added successfully."] = "Load added successfully.",
        ["Segment added successfully."] = "Segment added successfully.",
        ["Undo"] = "Undo",
        ["Redo"] = "Redo",
        ["Error"] = "Error",
        ["Saved to {path}"] = "Saved to {path}",
        ["Would you like to save the current time first?"] = "Would you like to save the current time first?",
        ["Would you like to save?"] = "Would you like to save?",
        ["Don't Save"] = "Don't Save",
        ["Restore Session"] = "Restore Session",
        ["Restore Session Message"] = "CRT closed unexpectedly with unsaved changes. Restore the last autosaved session?",
        ["Update Available"] = "A new version ({version}) is available — click to download.",
        ["Framerate Mismatch"] = "Framerate Mismatch",
        ["Framerate Mismatch Message"] = "This video appears to be {detected} FPS, but the session is set to {current} FPS.\n\nUpdate the framerate before calculating the frame?",
        ["Woah!"] = "Woah!",
        ["Concerningly Long Load Message"] = "This load is concerningly long. Would you like to add the load anyway?",
        ["Please restart the application to apply the changes."] = "Please restart the application to apply the changes.",
        ["Restore Defaults Message"] = "Are you sure you want to restore the default settings?",

        // Video retimer
        ["Import Video"] = "Import Video",
        ["Local File"] = "Local File",
        ["Video URL"] = "Video URL",
        ["YouTube URL"] = "YouTube URL",
        ["Browse"] = "Browse…",
        ["Import"] = "Import",
        ["Downloading"] = "Downloading…",
        ["Probing"] = "Probing…",
        ["Play"] = "Play",
        ["Pause"] = "Pause",
        ["Frame Back"] = "Frame Back",
        ["Frame Forward"] = "Frame Forward",
        ["Play/Pause"] = "Play/Pause",
        ["Mark Segment Start"] = "Mark Segment Start",
        ["Mark Segment End"] = "Mark Segment End",
        ["Mark Run Start"] = "Mark Run Start",
        ["Mark Run End"] = "Mark Run End",
        ["Mark Load Start"] = "Mark Load Start",
        ["Mark Load End"] = "Mark Load End",
        ["Current Frame"] = "Current Frame",
        ["Current Time"] = "Current Time",
        ["Export Retimed Video"] = "Export Retimed Video",
        ["Exporting"] = "Exporting…",
        ["Export Complete"] = "Export Complete",
        ["Open"] = "Open",
        ["Show in Folder"] = "Show in Folder",
        ["Tool Needed"] = "CRT needs {tool} for this feature. Download it now? (~{size})",
        ["Download"] = "Download",
        ["Framerate set from video"] = "Framerate set to {fps} from the video.",
        ["No video loaded"] = "No video loaded. Import a local file, direct URL, or YouTube link above.",
        ["ffprobe Required"] = "CRT needs ffprobe to import video — it reads the framerate, duration and resolution the retimer and the export depend on.",
        ["Run End Before Start"] = "The run end must be after the run start.",

        // Dashboard
        ["Run Library"] = "Run Library",
        ["New Retime"] = "New Retime",
        ["Open File"] = "Open File…",
        ["Remove from Library"] = "Remove from Library",
        ["Reveal in Explorer"] = "Reveal in Explorer",
        ["Unsaved Session"] = "Unsaved Session",
        ["Continue Editing"] = "Continue Editing",
        ["Empty Library"] = "Runs you open or save appear here.",
        ["Modified"] = "Modified",
        ["File not found"] = "That run file is no longer at {path}.\n\nRemove it from the library?",

        // Speedrun.com
        ["Speedrun.com"] = "Speedrun.com",
        ["Sign In"] = "Sign In",
        ["Sign Out"] = "Sign Out",
        ["API Key"] = "API Key",
        ["Get your key"] = "Get your key",
        ["Signed in as {user}"] = "Signed in as {user}",
        ["Sign-in failed"] = "Sign-in failed. Check the API key and try again.",
        ["Runs to Verify"] = "Runs to Verify",
        ["Runs to Verify (n)"] = "Runs to Verify ({count})",
        ["My Recent Runs"] = "My Recent Runs",
        ["Refresh"] = "Refresh",
        ["Watch"] = "Watch",
        ["Retime This"] = "Retime This",
        ["Verify"] = "Verify",
        ["Reject"] = "Reject",
        ["Reject Reason"] = "Reason",
        ["Verify Run Message"] = "Verify this run?",
        ["Reject Run Message"] = "Reject this run? A reason is required.",
        ["Game"] = "Game",
        ["Category"] = "Category",
        ["Player"] = "Player(s)",
        ["Submitted"] = "Submitted",
        ["Claimed Time"] = "Claimed Time",
        ["Video"] = "Video",
        ["Status"] = "Status",
        ["No runs to verify"] = "No runs awaiting verification. Nice work!",
        ["Sign in explainer"] = "Sign in with your Speedrun.com API key to see runs awaiting verification for the games you moderate.",
        ["Loading"] = "Loading…",
        ["Network error"] = "Couldn't reach Speedrun.com. Check your connection and try again.",

        // Settings (new)
        ["Timer Corner"] = "Timer Corner",
        ["Timer Style"] = "Timer Style",
        ["Top Left"] = "Top Left",
        ["Top Right"] = "Top Right",
        ["Bottom Left"] = "Bottom Left",
        ["Bottom Right"] = "Bottom Right",
        ["Pill"] = "Pill",
        ["Plain"] = "Plain",
        ["FFmpeg Path"] = "FFmpeg Path (empty = auto)",
        ["yt-dlp Path"] = "yt-dlp Path (empty = auto)",
        ["Default Mode"] = "Default Mode",
    };

    private static readonly Dictionary<string, string> French = new()
    {
        ["Framerate"] = "Taux de refraichissement",
        ["Start Frame"] = "Première image",
        ["End Frame"] = "Dernière image",
        ["Start Frame (Loads)"] = "Première image (chargement)",
        ["End Frame (Loads)"] = "Dernière image (chargement)",
        ["Paste"] = "Coller",
        ["Paste Start Frame"] = "Coller la première image",
        ["Paste End Frame"] = "Coller la dernière image",
        ["Paste Start Frame (Loads)"] = "Coller la première image (chargement)",
        ["Paste End Frame (Loads)"] = "Coller la dernière image (chargement)",
        ["Copy Mod Note"] = "Copier la note de modérateur",
        ["Copy Discord Message"] = "Copier le message Discord",
        ["Copy YouTube Chapters"] = "Copier les chapitres YouTube",
        ["Add Loads"] = "Ajouter un chargement",
        ["Add Load"] = "Ajouter un chargement",
        ["Edit Loads"] = "Modifier les chargements",
        ["Without Loads"] = "Sans chargements",
        ["With Loads"] = "Avec chargements",
        ["Click to Copy Time"] = "Cliquer pour copier le temps",
        ["File"] = "Fichier",
        ["New Time"] = "Nouveau temps",
        ["Open Time"] = "Ouvrir un temps",
        ["Session History"] = "Historique de session",
        ["Save"] = "Enregister",
        ["Save As"] = "Enregister sous",
        ["Settings"] = "Paramètres",
        ["Exit"] = "Quitter",
        ["Edit (Menu Bar)"] = "Modifier",
        ["Clear Loads"] = "Effacer les chargements",
        ["View"] = "Affichage",
        ["Always on Top"] = "Toujours au premier plan",
        ["Help"] = "Aide",
        ["About"] = "À propos",
        ["Edit Load"] = "Modifier les chargement",
        ["Save Edits"] = "Enregistrer les modifications",
        ["Discard Changes"] = "Annuler les modifications",
        ["Edit"] = "Modifier",
        ["Delete"] = "Supprimer",
        ["Loads"] = "Chargements",
        ["File Name"] = "Nom du fichier",
        ["Cancel"] = "Annuler",
        ["CRT Settings"] = "Paramètres du CRT",
        ["Automatically Check for Updates"] = "Vérifier automatiquement les mises à jours",
        ["Theme"] = "Thème",
        ["Automatic"] = "Automatique",
        ["Dark"] = "Sombre",
        ["Light"] = "Clair",
        ["Accent Color"] = "Couleur d'accent",
        ["Language"] = "Langue",
        ["Mod Note Format"] = "Format de la note de modérateur",
        ["Restore Defaults"] = "Restaurer les paramètres par défaut",
        ["Apply"] = "Appliquer",
        ["Hotkeys"] = "Raccourcis",
        ["Customize Hotkeys"] = "Personnaliser les raccourcis",
        ["Press a Key Combination"] = "Appuyez sur une combinaison de touches",
        ["Reset"] = "Réinitialiser",
        ["Reset All"] = "Tout réinitialiser",
        ["OK"] = "OK",
        ["Duplicate Hotkey"] = "Raccourci en double",
        ["Duplicate Hotkey Message"] = "La même combinaison de touches est assignée à plusieurs actions : {names}",
    };

    private static readonly Dictionary<string, string> Polish = new()
    {
        ["Framerate"] = "Liczba klatek na sekundę",
        ["Start Frame"] = "Pierwsza klatka",
        ["End Frame"] = "Ostatnia klatka",
        ["Start Frame (Loads)"] = "Pierwsza klatka ładowania",
        ["End Frame (Loads)"] = "Ostatnia klatka ładowania",
        ["Paste"] = "Wklej",
        ["Paste Start Frame"] = "Wklej pierwszą klatkę",
        ["Paste End Frame"] = "Wklej ostatnią klatkę",
        ["Paste Start Frame (Loads)"] = "Wklej pierwszą klatkę ładowania",
        ["Paste End Frame (Loads)"] = "Wklej ostatnią klatkę ładowania",
        ["Copy Mod Note"] = "Skopiuj notatkę moderatora",
        ["Copy Discord Message"] = "Skopiuj wiadomość Discord",
        ["Copy YouTube Chapters"] = "Skopiuj rozdziały YouTube",
        ["Add Loads"] = "Dodaj ładowanie",
        ["Add Load"] = "Dodaj ładowanie",
        ["Edit Loads"] = "Edytuj ładowania",
        ["Without Loads"] = "Bez ładowań",
        ["With Loads"] = "Z ładowaniami",
        ["Click to Copy Time"] = "Kliknij, aby skopiować czas",
        ["File"] = "Plik",
        ["New Time"] = "Nowy czas",
        ["Open Time"] = "Otwórz czas",
        ["Session History"] = "Historia sesji",
        ["Save"] = "Zapisz",
        ["Save As"] = "Zapisz jako",
        ["Settings"] = "Ustawienia",
        ["Exit"] = "Quitter",
        ["Edit (Menu Bar)"] = "Wyjście",
        ["Clear Loads"] = "Wyczyść ładowania",
        ["View"] = "Widok",
        ["Always on Top"] = "Zawsze na wierzchu",
        ["Help"] = "Pomoc",
        ["About"] = "O programie",
        ["Edit Load"] = "Edytuj ładowanie",
        ["Save Edits"] = "Zapisz zmiany",
        ["Discard Changes"] = "Odrzuć zmiany",
        ["Edit"] = "Edytuj",
        ["Delete"] = "Usuń",
        ["Loads"] = "Ładowania",
        ["File Name"] = "Nazwa pliku",
        ["Cancel"] = "Anuluj",
        ["CRT Settings"] = "Ustawienia CRT",
        ["Automatically Check for Updates"] = "Automatycznie sprawdzaj aktualizacje",
        ["Theme"] = "Motyw",
        ["Automatic"] = "Automatyczny",
        ["Dark"] = "Ciemny",
        ["Light"] = "Jasny",
        ["Accent Color"] = "Kolor akcentu",
        ["Language"] = "Język",
        ["Mod Note Format"] = "Format notatki moderatora",
        ["Restore Defaults"] = "Przywróć domyślne",
        ["Apply"] = "Zastosuj",
        ["Hotkeys"] = "Skróty klawiszowe",
        ["Customize Hotkeys"] = "Dostosuj skróty klawiszowe",
        ["Press a Key Combination"] = "Naciśnij kombinację klawiszy",
        ["Reset"] = "Resetuj",
        ["Reset All"] = "Resetuj wszystko",
        ["OK"] = "OK",
        ["Duplicate Hotkey"] = "Zduplikowany skrót",
        ["Duplicate Hotkey Message"] = "Ta sama kombinacja klawiszy jest przypisana do więcej niż jednej akcji: {names}",
    };

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["Framerate"] = "Tasa de Fotogramas",
        ["Start Frame"] = "Primer fotograma",
        ["End Frame"] = "Fotograma Finalmente",
        ["Start Frame (Loads)"] = "Primero Fotograma del Carga",
        ["End Frame (Loads)"] = "Fotograma Final del Carga",
        ["Paste"] = "Pegar",
        ["Paste Start Frame"] = "Pegar Primer Fotograma",
        ["Paste End Frame"] = "Pegar Fotograma Final",
        ["Paste Start Frame (Loads)"] = "Pegar Primero Fotograma del Carga",
        ["Paste End Frame (Loads)"] = "Pegar Fotograma Final del Carga",
        ["Copy Mod Note"] = "Copia Nota de Moderador",
        ["Copy Discord Message"] = "Copiar Mensaje de Discord",
        ["Copy YouTube Chapters"] = "Copiar Capítulos de YouTube",
        ["Add Loads"] = "Agregar un Carga",
        ["Add Load"] = "Agregar un Carga",
        ["Edit Loads"] = "Editar los Cargas",
        ["Without Loads"] = "Sin los Loads",
        ["With Loads"] = "Con los Loads",
        ["Click to Copy Time"] = "Copia el Tiempo",
        ["File"] = "Archivo",
        ["New Time"] = "Nuevo Tiempo",
        ["Open Time"] = "Abrir Tiempo",
        ["Session History"] = "Historial de Sesiones",
        ["Save"] = "Guardar",
        ["Save As"] = "Guardar Como",
        ["Settings"] = "Configuraciónes",
        ["Exit"] = "Salir",
        ["Edit (Menu Bar)"] = "Editar",
        ["Clear Loads"] = "Borrar los Loads",
        ["View"] = "Ver",
        ["Always on Top"] = "Siempre Visible",
        ["Help"] = "Ayuda",
        ["About"] = "Sobre",
        ["Edit Load"] = "Editar un Carga",
        ["Save Edits"] = "Guardar Ediciones",
        ["Discard Changes"] = "Borrar los Modificaciónes",
        ["Edit"] = "Editar",
        ["Delete"] = "Borrar",
        ["Loads"] = "Cargas",
        ["File Name"] = "Nombre del Archivo",
        ["Cancel"] = "Cancelar",
        ["CRT Settings"] = "Configuraciónes de CRT",
        ["Automatically Check for Updates"] = "Buscar Actualizaciones Automáticamente",
        ["Theme"] = "Tema",
        ["Automatic"] = "Automático",
        ["Dark"] = "Oscuro",
        ["Light"] = "Claro",
        ["Accent Color"] = "Color de Acento",
        ["Language"] = "Idioma",
        ["Mod Note Format"] = "Formato de la Nota de Moderador",
        ["Restore Defaults"] = "Restaurar Valores Predeterminados",
        ["Apply"] = "Aplicar",
        ["Hotkeys"] = "Atajos de Teclado",
        ["Customize Hotkeys"] = "Personalizar Atajos de Teclado",
        ["Press a Key Combination"] = "Presiona una combinación de teclas",
        ["Reset"] = "Restablecer",
        ["Reset All"] = "Restablecer Todo",
        ["OK"] = "OK",
        ["Duplicate Hotkey"] = "Atajo Duplicado",
        ["Duplicate Hotkey Message"] = "La misma combinación de teclas está asignada a más de una acción: {names}",
    };

    /// <summary>
    /// Keyed by the same display names used in the settings language dropdown.
    /// Anything else (including the "en" stored by default settings) falls back
    /// to English.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["English"] = English,
            ["Français"] = French,
            ["Polski"] = Polish,
            ["Español"] = Spanish,
        };

    public static IReadOnlyList<string> LanguageNames => Languages.Keys.ToList();

    public static IReadOnlyDictionary<string, string> Resolve(string language) =>
        Languages.TryGetValue(language, out var content) ? content : English;

    /// <summary>
    /// Translates display text between languages by reverse key lookup — used
    /// to store the theme name in English regardless of UI language (port of
    /// <c>Language.translate</c>).
    /// </summary>
    public static string Translate(string fromLanguage, string toLanguage, string text)
    {
        var source = Resolve(fromLanguage);
        var target = Resolve(toLanguage);

        foreach (var (key, value) in source)
        {
            if (value == text)
            {
                return target.TryGetValue(key, out string? translated) ? translated : text;
            }
        }
        return text;
    }
}

/// <summary>Per-app localizer bound to the configured language.</summary>
public sealed class Localizer
{
    private readonly IReadOnlyDictionary<string, string> _content;
    private readonly IReadOnlyDictionary<string, string> _english;

    public Localizer(string language)
    {
        Language = language;
        _content = LanguageCatalog.Resolve(language);
        _english = LanguageCatalog.Resolve("English");
    }

    public string Language { get; }

    /// <summary>Localizes a key: current language → English → the key itself.</summary>
    public string this[string key] =>
        _content.TryGetValue(key, out string? value) ? value
        : _english.TryGetValue(key, out string? fallback) ? fallback
        : key;

    /// <summary>Localizes a key and substitutes {name} placeholders.</summary>
    public string Format(string key, params (string Name, object Value)[] args)
    {
        string text = this[key];
        foreach (var (name, value) in args)
        {
            text = text.Replace("{" + name + "}", value?.ToString() ?? "");
        }
        return text;
    }
}
