#include "InstallerStrings.h"

#include <cwctype>
#include <string>

namespace
{
    constexpr InstallerStrings English = {
        L"Ornare Setup",
        L"Language",
        L"Developer and publisher: Vellium  \u2022  vellium.dev",
        L"Fast, native and private folder icon customization built for Windows 11.",
        L"\u2022 Seamless File Explorer context menu integration",
        L"\u2022 Completely offline and private (no telemetry, no uploads)",
        L"\u2022 High-resolution icons from PNG, JPG, WebP and ICO",
        L"Create a desktop shortcut",
        L"Ornare - Folder Icon Customizer",
        L"Ready to install.",
        L"Starting the installation...",
        L"Verifying the developer certificate...",
        L"Extracting the installation package...",
        L"Registering with Windows 11...",
        L"Creating shortcuts...",
        L"Installation completed successfully.",
        L"Ornare is installed and ready to use.",
        L"The installation package could not be extracted.",
        L"An error occurred during deployment.",
        L"Install",
        L"Cancel",
        L"Launch Ornare",
        L"Close",
        L"Retry"
    };

    constexpr InstallerStrings Turkish = {
        L"Ornare Kurulumu",
        L"Dil",
        L"Geli\u015ftirici ve yay\u0131nc\u0131: Vellium  \u2022  vellium.dev",
        L"Windows 11 i\u00e7in h\u0131zl\u0131, yerel ve gizlili\u011fe sayg\u0131l\u0131 klas\u00f6r simgesi ki\u015fiselle\u015ftirme.",
        L"\u2022 Dosya Gezgini sa\u011f t\u0131k men\u00fcs\u00fcyle sorunsuz entegrasyon",
        L"\u2022 Tamamen \u00e7evrimd\u0131\u015f\u0131 ve gizli (telemetri yok, y\u00fckleme yok)",
        L"\u2022 PNG, JPG, WebP ve ICO dosyalar\u0131ndan y\u00fcksek \u00e7\u00f6z\u00fcn\u00fcrl\u00fckl\u00fc simgeler",
        L"Masa\u00fcst\u00fcnde k\u0131sayol olu\u015ftur",
        L"Ornare - Klas\u00f6r Simgesi \u00d6zelle\u015ftirici",
        L"Kuruluma haz\u0131r.",
        L"Kurulum ba\u015flat\u0131l\u0131yor...",
        L"Geli\u015ftirici sertifikas\u0131 do\u011frulan\u0131yor...",
        L"Kurulum paketi haz\u0131rlan\u0131yor...",
        L"Windows 11'e kaydediliyor...",
        L"K\u0131sayollar olu\u015fturuluyor...",
        L"Kurulum ba\u015far\u0131yla tamamland\u0131.",
        L"Ornare kuruldu ve kullan\u0131ma haz\u0131r.",
        L"Kurulum paketi a\u00e7\u0131lamad\u0131.",
        L"Y\u00fckleme s\u0131ras\u0131nda bir hata olu\u015ftu.",
        L"Kur",
        L"\u0130ptal",
        L"Ornare'yi Ba\u015flat",
        L"Kapat",
        L"Tekrar Dene"
    };

    constexpr InstallerStrings German = {
        L"Ornare-Setup",
        L"Sprache",
        L"Entwickler und Herausgeber: Vellium  \u2022  vellium.dev",
        L"Schnelle, native und private Anpassung von Ordnersymbolen f\u00fcr Windows 11.",
        L"\u2022 Nahtlose Integration in das Kontextmen\u00fc des Datei-Explorers",
        L"\u2022 Vollst\u00e4ndig offline und privat (keine Telemetrie, keine Uploads)",
        L"\u2022 Hochaufl\u00f6sende Symbole aus PNG, JPG, WebP und ICO",
        L"Desktopverkn\u00fcpfung erstellen",
        L"Ornare - Ordnersymbol anpassen",
        L"Bereit zur Installation.",
        L"Installation wird gestartet...",
        L"Entwicklerzertifikat wird \u00fcberpr\u00fcft...",
        L"Installationspaket wird entpackt...",
        L"Registrierung bei Windows 11...",
        L"Verkn\u00fcpfungen werden erstellt...",
        L"Installation erfolgreich abgeschlossen.",
        L"Ornare ist installiert und einsatzbereit.",
        L"Das Installationspaket konnte nicht entpackt werden.",
        L"Bei der Bereitstellung ist ein Fehler aufgetreten.",
        L"Installieren",
        L"Abbrechen",
        L"Ornare starten",
        L"Schlie\u00dfen",
        L"Erneut versuchen"
    };

    constexpr InstallerStrings French = {
        L"Installation d'Ornare",
        L"Langue",
        L"D\u00e9veloppeur et \u00e9diteur : Vellium  \u2022  vellium.dev",
        L"Personnalisation d'ic\u00f4nes de dossier rapide, native et priv\u00e9e pour Windows 11.",
        L"\u2022 Int\u00e9gration transparente au menu contextuel de l'Explorateur de fichiers",
        L"\u2022 Enti\u00e8rement hors ligne et priv\u00e9 (aucune t\u00e9l\u00e9m\u00e9trie, aucun envoi)",
        L"\u2022 Ic\u00f4nes haute r\u00e9solution \u00e0 partir de PNG, JPG, WebP et ICO",
        L"Cr\u00e9er un raccourci sur le Bureau",
        L"Ornare - Personnalisation d'ic\u00f4nes de dossier",
        L"Pr\u00eat \u00e0 installer.",
        L"D\u00e9marrage de l'installation...",
        L"V\u00e9rification du certificat de d\u00e9veloppeur...",
        L"Extraction du package d'installation...",
        L"Enregistrement aupr\u00e8s de Windows 11...",
        L"Cr\u00e9ation des raccourcis...",
        L"Installation termin\u00e9e avec succ\u00e8s.",
        L"Ornare est install\u00e9 et pr\u00eat \u00e0 l'emploi.",
        L"Le package d'installation n'a pas pu \u00eatre extrait.",
        L"Une erreur est survenue lors du d\u00e9ploiement.",
        L"Installer",
        L"Annuler",
        L"Lancer Ornare",
        L"Fermer",
        L"R\u00e9essayer"
    };

    constexpr InstallerStrings Spanish = {
        L"Instalaci\u00f3n de Ornare",
        L"Idioma",
        L"Desarrollador y editor: Vellium  \u2022  vellium.dev",
        L"Personalizaci\u00f3n de iconos de carpeta r\u00e1pida, nativa y privada para Windows 11.",
        L"\u2022 Integraci\u00f3n perfecta en el men\u00fa contextual del Explorador de archivos",
        L"\u2022 Totalmente sin conexi\u00f3n y privado (sin telemetr\u00eda, sin cargas)",
        L"\u2022 Iconos de alta resoluci\u00f3n desde PNG, JPG, WebP e ICO",
        L"Crear un acceso directo en el escritorio",
        L"Ornare - Personalizador de iconos de carpeta",
        L"Listo para instalar.",
        L"Iniciando la instalaci\u00f3n...",
        L"Comprobando el certificado del desarrollador...",
        L"Extrayendo el paquete de instalaci\u00f3n...",
        L"Registrando en Windows 11...",
        L"Creando accesos directos...",
        L"La instalaci\u00f3n se complet\u00f3 correctamente.",
        L"Ornare est\u00e1 instalado y listo para usar.",
        L"No se pudo extraer el paquete de instalaci\u00f3n.",
        L"Se produjo un error durante la implementaci\u00f3n.",
        L"Instalar",
        L"Cancelar",
        L"Iniciar Ornare",
        L"Cerrar",
        L"Reintentar"
    };

    constexpr InstallerStrings Portuguese = {
        L"Instala\u00e7\u00e3o do Ornare",
        L"Idioma",
        L"Desenvolvedor e publicador: Vellium  \u2022  vellium.dev",
        L"Personaliza\u00e7\u00e3o de \u00edcones de pasta r\u00e1pida, nativa e privada para o Windows 11.",
        L"\u2022 Integra\u00e7\u00e3o perfeita ao menu de contexto do Explorador de Arquivos",
        L"\u2022 Totalmente offline e privado (sem telemetria, sem uploads)",
        L"\u2022 \u00cdcones de alta resolu\u00e7\u00e3o a partir de PNG, JPG, WebP e ICO",
        L"Criar atalho na \u00e1rea de trabalho",
        L"Ornare - Personalizador de \u00edcones de pasta",
        L"Pronto para instalar.",
        L"Iniciando a instala\u00e7\u00e3o...",
        L"Verificando o certificado do desenvolvedor...",
        L"Extraindo o pacote de instala\u00e7\u00e3o...",
        L"Registrando no Windows 11...",
        L"Criando atalhos...",
        L"A instala\u00e7\u00e3o foi conclu\u00edda com \u00eaxito.",
        L"O Ornare est\u00e1 instalado e pronto para uso.",
        L"N\u00e3o foi poss\u00edvel extrair o pacote de instala\u00e7\u00e3o.",
        L"Ocorreu um erro durante a implanta\u00e7\u00e3o.",
        L"Instalar",
        L"Cancelar",
        L"Iniciar o Ornare",
        L"Fechar",
        L"Tentar novamente"
    };

    // Same order and tags as LocaleCatalog.Entries. The first six are translated; every other
    // entry is selectable and receives the English table until its translation lands.
    const InstallerLocale kLocales[] = {
        { L"en-US", L"English", true },
        { L"tr-TR", L"T\u00fcrk\u00e7e", true },
        { L"de-DE", L"Deutsch", true },
        { L"fr-FR", L"Fran\u00e7ais", true },
        { L"es-ES", L"Espa\u00f1ol", true },
        { L"pt-BR", L"Portugu\u00eas (Brasil)", true },
        { L"af-ZA", L"Afrikaans", false },
        { L"am-ET", L"\u12a0\u121b\u122d\u129b", false },
        { L"ar-SA", L"\u0627\u0644\u0639\u0631\u0628\u064a\u0629", false },
        { L"az-Latn-AZ", L"Az\u0259rbaycan", false },
        { L"bg-BG", L"\u0411\u044a\u043b\u0433\u0430\u0440\u0441\u043a\u0438", false },
        { L"bn-IN", L"\u09ac\u09be\u0982\u09b2\u09be", false },
        { L"bs-Latn-BA", L"Bosanski", false },
        { L"ca-ES", L"Catal\u00e0", false },
        { L"cs-CZ", L"\u010ce\u0161tina", false },
        { L"cy-GB", L"Cymraeg", false },
        { L"da-DK", L"Dansk", false },
        { L"el-GR", L"\u0395\u03bb\u03bb\u03b7\u03bd\u03b9\u03ba\u03ac", false },
        { L"en-GB", L"English (United Kingdom)", false },
        { L"es-MX", L"Espa\u00f1ol (M\u00e9xico)", false },
        { L"et-EE", L"Eesti", false },
        { L"eu-ES", L"Euskara", false },
        { L"fa-IR", L"\u0641\u0627\u0631\u0633\u06cc", false },
        { L"fi-FI", L"Suomi", false },
        { L"fil-PH", L"Filipino", false },
        { L"ga-IE", L"Gaeilge", false },
        { L"gl-ES", L"Galego", false },
        { L"gu-IN", L"\u0a97\u0ac1\u0a9c\u0ab0\u0abe\u0aa4\u0ac0", false },
        { L"he-IL", L"\u05e2\u05d1\u05e8\u05d9\u05ea", false },
        { L"hi-IN", L"\u0939\u093f\u0928\u094d\u0926\u0940", false },
        { L"hr-HR", L"Hrvatski", false },
        { L"hu-HU", L"Magyar", false },
        { L"hy-AM", L"\u0540\u0561\u0575\u0565\u0580\u0565\u0576", false },
        { L"id-ID", L"Bahasa Indonesia", false },
        { L"is-IS", L"\u00cdslenska", false },
        { L"it-IT", L"Italiano", false },
        { L"ja-JP", L"\u65e5\u672c\u8a9e", false },
        { L"ka-GE", L"\u10e5\u10d0\u10e0\u10d7\u10e3\u10da\u10d8", false },
        { L"kk-KZ", L"\u049a\u0430\u0437\u0430\u049b\u0448\u0430", false },
        { L"kn-IN", L"\u0c95\u0ca8\u0ccd\u0ca8\u0ca1", false },
        { L"ko-KR", L"\ud55c\uad6d\uc5b4", false },
        { L"ky-KG", L"\u041a\u044b\u0440\u0433\u044b\u0437\u0447\u0430", false },
        { L"lt-LT", L"Lietuvi\u0173", false },
        { L"lv-LV", L"Latvie\u0161u", false },
        { L"mk-MK", L"\u041c\u0430\u043a\u0435\u0434\u043e\u043d\u0441\u043a\u0438", false },
        { L"ml-IN", L"\u0d2e\u0d32\u0d2f\u0d3e\u0d33\u0d02", false },
        { L"mn-MN", L"\u041c\u043e\u043d\u0433\u043e\u043b", false },
        { L"mr-IN", L"\u092e\u0930\u093e\u0920\u0940", false },
        { L"ms-MY", L"Bahasa Melayu", false },
        { L"nb-NO", L"Norsk Bokm\u00e5l", false },
        { L"ne-NP", L"\u0928\u0947\u092a\u093e\u0932\u0940", false },
        { L"nl-NL", L"Nederlands", false },
        { L"pa-IN", L"\u0a2a\u0a70\u0a1c\u0a3e\u0a2c\u0a40", false },
        { L"pl-PL", L"Polski", false },
        { L"pt-PT", L"Portugu\u00eas (Portugal)", false },
        { L"ro-RO", L"Rom\u00e2n\u0103", false },
        { L"ru-RU", L"\u0420\u0443\u0441\u0441\u043a\u0438\u0439", false },
        { L"si-LK", L"\u0dc3\u0dd2\u0d82\u0dc4\u0dbd", false },
        { L"sk-SK", L"Sloven\u010dina", false },
        { L"sl-SI", L"Sloven\u0161\u010dina", false },
        { L"sq-AL", L"Shqip", false },
        { L"sr-Latn-RS", L"Srpski", false },
        { L"sv-SE", L"Svenska", false },
        { L"sw-KE", L"Kiswahili", false },
        { L"ta-IN", L"\u0ba4\u0bae\u0bbf\u0bb4\u0bcd", false },
        { L"te-IN", L"\u0c24\u0c46\u0c32\u0c41\u0c17\u0c41", false },
        { L"th-TH", L"\u0e44\u0e17\u0e22", false },
        { L"uk-UA", L"\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430", false },
        { L"ur-PK", L"\u0627\u0631\u062f\u0648", false },
        { L"uz-Latn-UZ", L"O\u02bbzbekcha", false },
        { L"vi-VN", L"Ti\u1ebfng Vi\u1ec7t", false },
        { L"zh-CN", L"\u4e2d\u6587\uff08\u7b80\u4f53\uff09", false },
        { L"zh-TW", L"\u4e2d\u6587\uff08\u7e41\u9ad4\uff09", false }
    };

    const InstallerStrings* const kTranslated[] = {
        &English,
        &Turkish,
        &German,
        &French,
        &Spanish,
        &Portuguese
    };

    constexpr int LocaleCount = static_cast<int>(sizeof(kLocales) / sizeof(kLocales[0]));
    constexpr int TranslatedCount = static_cast<int>(sizeof(kTranslated) / sizeof(kTranslated[0]));

    std::wstring LowercaseOf(const wchar_t* value)
    {
        std::wstring result;
        if (!value)
        {
            return result;
        }

        for (const wchar_t* cursor = value; *cursor; ++cursor)
        {
            result.push_back(static_cast<wchar_t>(std::towlower(*cursor)));
        }

        return result;
    }

    std::wstring LanguageOf(const std::wstring& tag)
    {
        const std::wstring::size_type dash = tag.find(L'-');
        return dash == std::wstring::npos ? tag : tag.substr(0, dash);
    }

    int FindLocaleIndex(const wchar_t* tag)
    {
        const std::wstring lowered = LowercaseOf(tag);
        if (lowered.empty())
        {
            return -1;
        }

        for (int i = 0; i < LocaleCount; ++i)
        {
            if (LowercaseOf(kLocales[i].tag) == lowered)
            {
                return i;
            }
        }

        const std::wstring language = LanguageOf(lowered);
        if (language.empty())
        {
            return -1;
        }

        for (int i = 0; i < LocaleCount; ++i)
        {
            if (LanguageOf(LowercaseOf(kLocales[i].tag)) == language)
            {
                return i;
            }
        }

        return -1;
    }
}

int InstallerLocaleCount()
{
    return LocaleCount;
}

const InstallerLocale& InstallerLocaleAt(int index)
{
    if (index < 0 || index >= LocaleCount)
    {
        return kLocales[0];
    }

    return kLocales[index];
}

const InstallerStrings& InstallerStringsAt(int index)
{
    if (index >= 0 && index < TranslatedCount && kLocales[index].isTranslated)
    {
        return *kTranslated[index];
    }

    return English;
}

int DetectInstallerLocaleIndex()
{
    wchar_t localeName[LOCALE_NAME_MAX_LENGTH] = {};

    if (GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH) > 0)
    {
        const int index = FindLocaleIndex(localeName);
        if (index >= 0)
        {
            return index;
        }
    }

    // Older or trimmed Windows images can lack a locale name; the UI language id is always set.
    const LCID lcid = MAKELCID(GetUserDefaultUILanguage(), SORT_DEFAULT);
    if (LCIDToLocaleName(lcid, localeName, LOCALE_NAME_MAX_LENGTH, 0) > 0)
    {
        const int index = FindLocaleIndex(localeName);
        if (index >= 0)
        {
            return index;
        }
    }

    return 0;
}
