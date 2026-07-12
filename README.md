# FireAsset – Asset Manager

Zentrale Webanwendung zur Verwaltung von Geräten, ihren Standorten und den
wiederkehrenden Prüfungen (Feuerwehr / Werkstatt).

Dieses Dokument hat zwei Teile:

- **[Teil 1 – Für Anwender](#teil-1--für-anwender)** (Gerätewarte, Werkstatt-, Prüf- und Inventarpersonal):
  Was das Tool leistet und wie die Menübereiche aufgebaut sind.
- **[Teil 2 – Für IT-Administratoren](#teil-2--für-it-administratoren)**:
  Technologie, Installation, Betrieb und Konfiguration.

Die fachliche Spezifikation liegt in [`Spec.md`](Spec.md), der technische Umsetzungsplan in
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md). Eine ausführliche Schritt-für-Schritt-Anleitung
findet sich außerdem direkt in der Anwendung unter **Handbuch**.

---

# Teil 1 – Für Anwender

## Welchen Job erfüllt FireAsset?

FireAsset bildet den betrieblichen **Prüfprozess** vollständig digital ab. Statt Prüfungen in
Listen und Ordnern zu pflegen, verwaltet die Anwendung zentral:

- **Was geprüft werden muss** – jedes Gerät („Artikel") gehört zu einer **Kategorie**. Die Kategorie
  legt über **Intervalle** fest, welche Prüfungen in welchem Rhythmus fällig werden und welches
  **Formular** dabei auszufüllen ist.
- **Wann geprüft werden muss** – aus diesen Regeln erzeugt das System automatisch **Aufgaben** mit
  Fälligkeitsdatum. Nach jeder erledigten Prüfung wird die nächste Aufgabe automatisch angelegt.
- **Was geprüft wurde** – jede abgeschlossene Aufgabe erzeugt ein **Prüfprotokoll**. Die Protokolle
  bleiben dauerhaft nachvollziehbar archiviert.

Konkret nimmt Ihnen das Tool heute folgende Arbeit ab:

- **Automatische Terminplanung:** Sie müssen keine Prüftermine mehr manuell nachhalten – das System
  weiß, wann welches Gerät fällig ist, und markiert überfällige Prüfungen farblich.
- **Geführte Prüfung:** Zu jeder Aufgabe öffnet sich das passende Formular. Auf Wunsch werden die
  Werte der letzten Prüfung eingeblendet, um sie direkt zu vergleichen.
- **Lückenlose Historie:** Formulare sind versioniert – alte Protokolle bleiben exakt so lesbar, wie
  sie damals erfasst wurden.
- **Schnelles Arbeiten mit Barcode:** Geräte und Standorte lassen sich per USB-Scanner suchen und
  umlagern.
- **Sonderfall FTZ-Pool-Geräte:** Geräte aus dem Pool eines externen Dienstleisters durchlaufen den
  Prüfzyklus nur einmal – nach Abschluss aller Aufgaben wird der Artikel automatisch stillgelegt.
- **Übersicht & Auswertung:** Dashboard mit Kennzahlen, filterbare Listen und CSV-Export der
  Inventarliste.

## Die Menübereiche

Die Navigation auf der linken Seite gliedert die Anwendung in folgende Bereiche:

### Dashboard
Der Einstieg nach der Anmeldung. Zeigt Kennzahlen (Anzahl Artikel, offene und überfällige
Aufgaben, Protokolle), die Verteilung der aktuellen Prüfstatus und die nächsten fälligen Aufgaben –
farblich markiert (rot = überfällig, orange = in diesem Monat fällig). Inaktive Artikel zählen nicht
in die Kennzahlen.

### Stammdaten
Die Grundlage aller Prüfungen. Diese Bereiche werden idealerweise **zuerst** eingerichtet:

- **Benutzer** – Konten für die Anmeldung am Portal (Vorname, Nachname, E-Mail, Passwort).
- **Standorte** – hierarchische Lagerorte (z. B. *Fahrzeug → HLF → Innenraum*). Jeder Standort kann
  einen Barcode und ein Icon erhalten; der Barcode wird für den schnellen Standortwechsel genutzt.
- **Formulare** – frei definierbare Prüfformulare mit beliebigen Feldern (Ja/Nein, Text, Ganzzahl,
  Gleitkommazahl, mehrzeiliger Text, Datum; bei Zahlen optional Einheit und Referenzwert). Jede
  Änderung an den Feldern erzeugt eine neue **Version**, sodass bestehende Protokolle unverändert
  bleiben. Das abschließende Prüfergebnis (Bestanden / Mangelhaft / Nicht bestanden) ist immer
  automatisch Teil jeder Prüfung.
- **Kategorien & Intervalle** – Gerätearten und ihre Prüfregeln. Zu jeder Kategorie werden
  Intervalle mit Name, **Rhythmus in Monaten** und zugeordnetem Formular angelegt. Nur aktive
  Intervalle mit hinterlegtem Formular erzeugen Aufgaben.

### Artikelstamm
Die Verwaltung der einzelnen Geräte. Hier legen Sie Artikel an (Pflicht: Identifikation und
Kategorie; das Anschaffungsdatum ist Basis für die erste Fälligkeit). Beim Speichern werden
automatisch die Prüfaufgaben der Kategorie erzeugt. Funktionen dieser Seite:

- **Anlegen / Bearbeiten / Kopieren** von Artikeln (die Kopie übernimmt alle Stammdaten außer den
  gerätespezifischen Kennungen wie Barcode und Seriennummer).
- **Kennzeichen „FTZ-Pool-Gerät"** für den oben beschriebenen einmaligen Prüfzyklus.
- **Standortwechsel per Barcode** – Artikel-Barcode und Ziel-Standort-Barcode scannen, fertig.
- **Barcode-Suche**, um einen Artikel direkt zu öffnen.
- **Ungeplante Prüfung** für spontane Kontrollen (erzeugt ein Protokoll, ohne bestehende Aufgaben zu
  verändern).

### Aufgaben
Die tägliche Arbeitsliste aller offenen Prüfungen, standardmäßig nach Fälligkeit sortiert. Spalten
sind frei anordnbar, filter- und sortierbar. Von hier aus:

- **Prüfung durchführen** – das zugeordnete Formular öffnet sich; Werte erfassen, Ergebnis wählen,
  speichern. Danach entsteht ein Protokoll und die Folgeaufgabe wird angelegt.
- **Stapel abarbeiten** – mehrere gleichartige Prüfungen nacheinander erfassen.
- **Manuelle Aufgabe** – eine zusätzliche Prüfung außerhalb der Intervalle anlegen.
- **Stilllegen** – eine Aufgabe ohne Prüfung schließen (kein Protokoll).

### Protokolle
Die vollständige, schreibgeschützte Prüfhistorie. Filterbar nach Artikel, Kategorie, Status und
Zeitraum. Die Detailansicht zeigt alle erfassten Werte in der exakten Formularversion von damals.

### Export
CSV-Export der Inventarliste, optional gefiltert nach Kategorie, Standort und Status – für Excel und
Weiterverarbeitung.

### Handbuch
Ausführliche Schritt-für-Schritt-Anleitung zu allen Funktionen, direkt in der Anwendung.

### Changelog
Die Änderungshistorie der Anwendung, gruppiert nach Versionen (neueste zuerst).

---

# Teil 2 – Für IT-Administratoren

## Technologie
- **.NET 10**, Blazor Web App (Interactive Server)
- **SQLite** + Entity Framework Core (automatische Migration beim Start)
- **Radzen.Blazor** (UI-Komponenten)
- Schlanke **Cookie-Authentifizierung** mit Passwort-Hashing (`PasswordHasher`)

> Hinweis: Die Anwendung kommuniziert nach dem initialen Seitenaufruf über eine
> SignalR-/WebSocket-Verbindung (Blazor Server). Es gibt keine öffentliche REST-API; einzige
> HTTP-Endpunkte sind die Anmeldung, das Abmelden und der CSV-Export (`/export/inventory.csv`,
> nur für angemeldete Benutzer).

## Installation & Start (Entwicklung)
```bash
cd src/FireAsset
dotnet run
```
Beim ersten Start wird die SQLite-Datenbank (`fireasset.db`) automatisch migriert und ein
initialer Administrator angelegt (Abschnitt `AdminSeed` in der `appsettings.json`):

- **E-Mail:** `admin@fireasset.local`
- **Passwort:** `ChangeMe!123`  ← **vor produktivem Einsatz ändern!**

## Betrieb & Deployment

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

### Zielumgebung
- Zentraler Server mit mehreren Clients, bis zu ca. 3 gleichzeitige Nutzer.
- Keine Offline-Funktionalität, keine Benachrichtigungen, keine Mehrsprachigkeit.

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
