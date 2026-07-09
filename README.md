# FireAsset – Asset Manager

Zentrale Webanwendung zur Verwaltung von Geräten, Prüfungen, Aufgaben und Standorten
(Feuerwehr / Werkstatt). Siehe [`Spec.md`](Spec.md) für die fachliche Spezifikation und
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) für den technischen Umsetzungsplan.

## Technologie
- .NET 10, Blazor Web App (Interactive Server)
- SQLite + Entity Framework Core (Auto-Migrate beim Start)
- Radzen.Blazor (UI-Komponenten)
- Schlanke Cookie-Authentifizierung mit Passwort-Hashing (`PasswordHasher`)

## Funktionsumfang
- **Dashboard** – Kennzahlen, Prüfstatus-Verteilung, fällige Aufgaben
- **Stammdaten** – Benutzer, hierarchische Standorte, Kategorien & Intervalle, versionierte Formulare
- **Artikelstamm** – Artikelverwaltung, Barcode-Suche, Standortwechsel per Scan, automatische Aufgabenanlage
- **Aufgaben** – Grid mit verschiebbaren Spalten, Filter je Spalte, Mehrfachauswahl/Stapelabarbeitung, Prüfformular-Erfassung
- **Protokolle** – archivierte Prüfhistorie mit Read-only-Detailansicht
- **Export** – CSV-Export der Inventarliste (filterbar)

## Starten (Entwicklung)
```bash
cd src/FireAsset
dotnet run
```
Beim ersten Start wird die SQLite-Datenbank (`fireasset.db`) automatisch migriert und ein
initialer Administrator angelegt (Abschnitt `AdminSeed` in der `appsettings.json`):

- **E-Mail:** `admin@fireasset.local`
- **Passwort:** `ChangeMe!123`  ← **vor produktivem Einsatz ändern!**

## Betrieb / Deployment

### Konfiguration
Alle Einstellungen in `appsettings.json` (bzw. `appsettings.Production.json` oder Umgebungsvariablen):

| Schlüssel | Zweck |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite-Datei (`Data Source=...`) |
| `AdminSeed:Email` / `Password` / `FirstName` / `LastName` | initialer Admin (nur wirksam, solange keine Benutzer existieren) |

Das Admin-Passwort sollte in Produktion **nicht** in `appsettings.json` stehen, sondern über
eine Umgebungsvariable gesetzt werden, z. B. `AdminSeed__Password`.

### Veröffentlichen
```bash
dotnet publish src/FireAsset -c Release -o ./publish
```
Anschließend `./publish/FireAsset` auf dem Zielserver starten (hinter einem Reverse-Proxy
wie IIS/Nginx betreiben).

### HTTPS
`UseHttpsRedirection` und HSTS sind aktiv (HSTS nur außerhalb der Entwicklung).
Für den produktiven Einsatz wird HTTPS über ein gültiges Zertifikat (Reverse-Proxy oder
Kestrel) dringend empfohlen.

### Backup
Die gesamte Anwendungsdatenhaltung liegt in der SQLite-Datei (`fireasset.db`).
Die Datenbank läuft im WAL-Modus; zur Datei gehören dann auch `fireasset.db-wal`
und `fireasset.db-shm` (beim Kopieren im laufenden Betrieb mitnehmen bzw.
SQLite-Online-Backup verwenden).
Backup = regelmäßiges Kopieren dieser Datei (idealerweise bei gestoppter Anwendung oder
per SQLite-Online-Backup). Es gibt keine anwendungsinterne Backup-Funktion (bewusst, gemäß Spec).

### Sicherheit
- Passwörter werden ausschließlich gehasht gespeichert (Mindestlänge 8 Zeichen).
- Login-Drosselung: nach 5 Fehlversuchen wird das Konto 15 Minuten gesperrt.
- Sitzungen werden bei jeder Anfrage revalidiert: deaktivierte/gelöschte Benutzer verlieren
  ihre Sitzung sofort.
- Der Barcode-Scanner wird wie eine Tastatur verwendet (USB-HID); Eingabe + Enter löst Suche/Umlagerung aus.

## Datenbank-Migrationen
```bash
cd src/FireAsset
dotnet ef migrations add <Name> -o Data/Migrations
```
Migrationen werden beim Anwendungsstart automatisch angewendet (`DbInitializer`).

## Bekannte Hinweise
- **NuGet-Advisory `SQLitePCLRaw.lib.e_sqlite3` (GHSA-2m69-gcr7-jv3q):** Diese native Bibliothek
  kommt transitiv über EF Core 10 von Microsoft. Ein gepatchtes Release ist abzuwarten und per
  `dotnet outdated`/Update einzuspielen. Risiko im vorliegenden Szenario (lokale Datei-DB,
  wenige interne Nutzer, keine Verarbeitung fremder DB-Dateien) gering.

## Umsetzungsstand
- [x] M1 – Projektgerüst: Blazor + Radzen + SQLite + Login + Admin-Seed
- [x] M2 – Datenmodell + EF-Migration
- [x] M3 – Stammdaten-CRUD (Benutzer, Standorte, Kategorien & Intervalle)
- [x] M4 – Formulare + Versionierung
- [x] M5 – Artikel + automatische Aufgabenanlage
- [x] M6 – Aufgaben + Prüfung
- [x] M7 – Protokolle
- [x] M8 – Dashboard + CSV-Export
- [x] M9 – Feinschliff
