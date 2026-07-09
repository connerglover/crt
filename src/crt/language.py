# Standard Library
from typing import NoReturn

_ENGLISH = {
    "Framerate": "Framerate (FPS)",
    "Start Frame": "Start Frame",
    "End Frame": "End Frame",
    "Start Frame (Loads)": "Start Frame (Loads)",
    "End Frame (Loads)": "End Frame (Loads)",
    "Paste": "Paste",
    "Copy Mod Note": "Copy Mod Note",
    "Add Loads": "Add Loads",
    "Edit Loads": "Edit Loads",
    "Without Loads": "Without Loads",
    "With Loads": "With Loads",
    "Click to Copy Time": "Click to Copy Time",
    "File": "File",
    "New Time": "New Time",
    "Open Time": "Open Time",
    "Session History": "Session History",
    "Save": "Save",
    "Save As": "Save As",
    "Settings": "Settings",
    "Exit": "Exit",
    "Edit (Menu Bar)": "Edit",
    "Clear Loads": "Clear Loads",
    "View": "View",
    "Always on Top": "Always on Top",
    "Help": "Help",
    "Check for Updates": "Check for Updates",
    "About": "About",
    "Edit Load": "Edit Load",
    "Save Edits": "Save Edits",
    "Discard Changes": "Discard Changes",
    "Edit": "Edit",
    "Delete": "Delete",
    "Loads": "Loads",
    "File Name": "File Name",
    "Cancel": "Cancel",
    "CRT Settings": "CRT Settings",
    "Automatically Check for Updates": "Automatically Check for Updates",
    "Theme": "Theme",
    "Automatic": "Automatic",
    "Dark": "Dark",
    "Light": "Light",
    "Language": "Language",
    "Mod Note Format": "Mod Note Format",
    "Restore Defaults": "Restore Defaults",
    "Apply": "Apply",
}

_FRENCH = {
    "Framerate": "Taux de refraichissement",
    "Start Frame": "Première image",
    "End Frame": "Dernière image",
    "Start Frame (Loads)": "Première image (chargement)",
    "End Frame (Loads)": "Dernière image (chargement)",
    "Paste": "Coller",
    "Copy Mod Note": "Copier la note de modérateur",
    "Add Loads": "Ajouter un chargement",
    "Edit Loads": "Modifier les chargements",
    "Without Loads": "Sans chargements",
    "With Loads": "Avec chargements",
    "Click to Copy Time": "Cliquer pour copier le temps",
    "File": "Fichier",
    "New Time": "Nouveau temps",
    "Open Time": "Ouvrir un temps",
    "Session History": "Historique de session",
    "Save": "Enregister",
    "Save As": "Enregister sous",
    "Settings": "Paramètres",
    "Exit": "Quitter",
    "Edit (Menu Bar)": "Modifier",
    "Clear Loads": "Effacer les chargements",
    "View": "Affichage",
    "Always on Top": "Toujours au premier plan",
    "Help": "Aide",
    "Check for Updates": "Vérifier les mises à jours",
    "About": "À propos",
    "Edit Load": "Modifier les chargement",
    "Save Edits": "Enregistrer les modifications",
    "Discard Changes": "Annuler les modifications",
    "Edit": "Modifier",
    "Delete": "Supprimer",
    "Loads": "Chargements",
    "File Name": "Nom du fichier",
    "Cancel": "Annuler",
    "CRT Settings": "Paramètres du CRT",
    "Automatically Check for Updates": "Vérifier automatiquement les mises à jours",
    "Theme": "Thème",
    "Automatic": "Automatique",
    "Dark": "Sombre",
    "Light": "Clair",
    "Language": "Langue",
    "Mod Note Format": "Format de la note de modérateur",
    "Restore Defaults": "Restaurer les paramètres par défaut",
    "Apply": "Appliquer",
}

_POLISH = {
    "Framerate": "Liczba klatek na sekundę",
    "Start Frame": "Pierwsza klatka",
    "End Frame": "Ostatnia klatka",
    "Start Frame (Loads)": "Pierwsza klatka ładowania",
    "End Frame (Loads)": "Ostatnia klatka ładowania",
    "Paste": "Wklej",
    "Copy Mod Note": "Skopiuj notatkę moderatora",
    "Add Loads": "Dodaj ładowanie",
    "Edit Loads": "Edytuj ładowania",
    "Without Loads": "Bez ładowań",
    "With Loads": "Z ładowaniami",
    "Click to Copy Time": "Kliknij, aby skopiować czas",
    "File": "Plik",
    "New Time": "Nowy czas",
    "Open Time": "Otwórz czas",
    "Session History": "Historia sesji",
    "Save": "Zapisz",
    "Save As": "Zapisz jako",
    "Settings": "Ustawienia",
    "Exit": "Quitter",
    "Edit (Menu Bar)": "Wyjście",
    "Clear Loads": "Wyczyść ładowania",
    "View": "Widok",
    "Always on Top": "Zawsze na wierzchu",
    "Help": "Pomoc",
    "Check for Updates": "Sprawdź aktualizacje",
    "About": "O programie",
    "Edit Load": "Edytuj ładowanie",
    "Save Edits": "Zapisz zmiany",
    "Discard Changes": "Odrzuć zmiany",
    "Edit": "Edytuj",
    "Delete": "Usuń",
    "Loads": "Ładowania",
    "File Name": "Nazwa pliku",
    "Cancel": "Anuluj",
    "CRT Settings": "Ustawienia CRT",
    "Automatically Check for Updates": "Automatycznie sprawdzaj aktualizacje",
    "Theme": "Motyw",
    "Automatic": "Automatyczny",
    "Dark": "Ciemny",
    "Light": "Jasny",
    "Language": "Język",
    "Mod Note Format": "Format notatki moderatora",
    "Restore Defaults": "Przywróć domyślne",
    "Apply": "Zastosuj",
}

_SPANISH = {
    "Framerate": "Tasa de Fotogramas",
    "Start Frame": "Primer fotograma",
    "End Frame": "Fotograma Finalmente",
    "Start Frame (Loads)": "Primero Fotograma del Carga",
    "End Frame (Loads)": "Fotograma Final del Carga",
    "Paste": "Pegar",
    "Copy Mod Note": "Copia Nota de Moderador",
    "Add Loads": "Agregar un Carga",
    "Edit Loads": "Editar los Cargas",
    "Without Loads": "Sin los Loads",
    "With Loads": "Con los Loads",
    "Click to Copy Time": "Copia el Tiempo",
    "File": "Archivo",
    "New Time": "Nuevo Tiempo",
    "Open Time": "Abrir Tiempo",
    "Session History": "Historial de Sesiones",
    "Save": "Guardar",
    "Save As": "Guardar Como",
    "Settings": "Configuraciónes",
    "Exit": "Salir",
    "Edit (Menu Bar)": "Editar",
    "Clear Loads": "Borrar los Loads",
    "View": "Ver",
    "Always on Top": "Siempre Visible",
    "Help": "Ayuda",
    "Check for Updates": "Buscar Actualizaciones",
    "About": "Sobre",
    "Edit Load": "Editar un Carga",
    "Save Edits": "Guardar Ediciones",
    "Discard Changes": "Borrar los Modificaciónes",
    "Edit": "Editar",
    "Delete": "Borrar",
    "Loads": "Cargas",
    "File Name": "Nombre del Archivo",
    "Cancel": "Cancelar",
    "CRT Settings": "Configuraciónes de CRT",
    "Automatically Check for Updates": "Buscar Actualizaciones Automáticamente",
    "Theme": "Tema",
    "Automatic": "Automático",
    "Dark": "Oscuro",
    "Light": "Claro",
    "Language": "Idioma",
    "Mod Note Format": "Formato de la Nota de Moderador",
    "Restore Defaults": "Restaurar Valores Predeterminados",
    "Apply": "Aplicar",
}

# Keyed by the same display names used in the settings language dropdown.
# Anything else (including the "en" stored by default settings) falls back
# to English — see Language.__init__.
LANGUAGES = {
    "English": _ENGLISH,
    "Français": _FRENCH,
    "Polski": _POLISH,
    "Español": _SPANISH,
}


class Language:
    def __init__(self, language: str) -> NoReturn:
        """Initializes the language object.

        Args:
            language (str): The language.
        """
        self.language = language
        self.content = LANGUAGES.get(language, _ENGLISH)

    def translate(self, from_lang: str, to_lang: str, text: str) -> str:
        """
        Translates text from one language to another using language content dictionaries.

        Args:
            from_lang (str): The language to translate from.
            to_lang (str): The language to translate to.
            text (str): The text to translate.

        Returns:
            str: The translated text.
        """
        source = LANGUAGES.get(from_lang, _ENGLISH)
        target = LANGUAGES.get(to_lang, _ENGLISH)

        try:
            key = next(k for k, v in source.items() if v == text)
            return target[key]
        except StopIteration:
            return text  # Return original text if translation not found
