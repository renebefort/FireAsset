# FireAsset – Asset Manager

Zentrale Webanwendung zur Verwaltung von Geräten, ihren Standorten und den
wiederkehrenden Prüfungen (Feuerwehr / Werkstatt).

Dieses Dokument hat zwei Teile:

- **[Teil 1 – Für Anwender](#teil-1--für-anwender)** (Gerätewarte, Werkstatt-, Prüf- und Inventarpersonal):
  Was das Tool leistet und wie die Menübereiche aufgebaut sind.
- **[Teil 2 – Für IT-Administratoren](#teil-2--für-it-administratoren)**:
  Technologie, Installation, Betrieb und Konfiguration.

Die fachliche Spezifikation liegt in [`Spec.md`](Spec.md). Eine ausführliche
Schritt-für-Schritt-Anleitung findest Du direkt in der Anwendung unter **Handbuch**; eine
Installationsanleitung für Administratoren in
[`docs/INSTALL-Windows11.md`](docs/INSTALL-Windows11.md).

> **Version 1.0.0** – erste produktive Freigabe.

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

Konkret nimmt Dir das Tool heute folgende Arbeit ab:

- **Automatische Terminplanung:** Du musst keine Prüftermine mehr manuell nachhalten – das System
  weiß, wann welches Gerät fällig ist, und markiert überfällige Prüfungen farblich.
- **Geführte Prüfung:** Zu jeder Aufgabe öffnet sich das passende Formular. Auf Wunsch werden die
  Werte der letzten Prüfung eingeblendet, um sie direkt zu vergleichen.
- **Lückenlose Historie:** Formulare sind versioniert – alte Protokolle bleiben exakt so lesbar, wie
  sie damals erfasst wurden.
- **Schnelles Arbeiten mit Barcode:** Geräte und Standorte lassen sich per USB-Scanner suchen und
  umlagern.
- **Sonderfall FTZ-Pool-Geräte:** Geräte aus dem Pool eines externen Dienstleisters durchlaufen den
  Prüfzyklus nur einmal – nach Abschluss aller Aufgaben wird der Artikel automatisch stillgelegt.
- **Dokumente für externe Prüfer:** freier Brief und Verwendungsnachweis (z. B. FTZ) aus Vorlagen –
  speicherbar und als PDF. Beim Abschluss eines Verwendungsnachweises werden die erfassten Artikel
  auf den Zielstandort umgebucht und FTZ-Pool-Geräte stillgelegt.
- **Lückenloses Logbuch (nur Administratoren):** protokolliert Anlage, Bearbeitung, Stilllegung und
  Standortwechsel je Artikel (Wer/Wann).
- **Schnellaktionen für Tablet & Smartphone:** Prüfung starten, Standortwechsel und Dokument erstellen
  per Barcode – touch-optimiert für die Arbeit vor Ort.
- **Übersicht & Auswertung:** Dashboard mit Kennzahlen, filterbare Listen und CSV-Export der
  Inventarliste.

## Die Menübereiche

Die Navigation auf der linken Seite gliedert die Anwendung in folgende Bereiche:

### Dashboard
Der Einstieg nach der Anmeldung. Zeigt Kennzahlen (Anzahl Artikel, offene und überfällige
Aufgaben, Protokolle), die Verteilung der aktuellen Prüfstatus und die nächsten fälligen Aufgaben –
farblich markiert (rot = überfällig, orange = in diesem Monat fällig). Inaktive Artikel zählen nicht
in die Kennzahlen.

### Schnellaktionen
Touch-optimierte Seiten für die Arbeit am Tablet/Smartphone mit Barcode-Scanner:

- **Prüfung starten** – Geräte-Barcode scannen; die früheste offene Aufgabe des Artikels öffnet sich sofort.
- **Standortwechsel** – Zielstandort wählen/scannen, dann Artikel nacheinander scannen und gemeinsam umlagern.
- **Neues Dokument** – Vorlage antippen und direkt einen Brief oder Verwendungsnachweis erfassen.

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
- **Dokumentvorlagen** – Vorlagen für Briefe und Verwendungsnachweise (Titel, Empfänger, Absender,
  Signatur usw.), die beim Anlegen eines Dokuments vorbelegt werden; beim Verwendungsnachweis auch
  ein Standard-Zielstandort.

### Artikelstamm
Die Verwaltung der einzelnen Geräte. Hier legst Du Artikel an (Pflicht: Identifikation und
Kategorie; das Anschaffungsdatum ist Basis für die erste Fälligkeit). Beim Speichern werden
automatisch die Prüfaufgaben der Kategorie erzeugt. Funktionen dieser Seite:

- **Anlegen / Bearbeiten / Kopieren** von Artikeln (die Kopie übernimmt alle Stammdaten außer den
  gerätespezifischen Kennungen wie Barcode und Seriennummer).
- **Kennzeichen „FTZ-Pool-Gerät"** für den oben beschriebenen einmaligen Prüfzyklus.
- **Kennzeichen „FTZ-Pool-Gerät"** lässt sich auch nachträglich ändern.
- **Barcode-Suche** filtert die Liste auf passende Geräte; die Checkbox **„Auch inaktive anzeigen"**
  blendet stillgelegte Artikel zusätzlich ein (Standard: nur aktive).
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
Ein Massen-PDF-Export (ZIP mit einem PDF je Protokoll) ist ebenfalls möglich.

### Dokumente
Erzeugen, Speichern und PDF-Export von Schreiben für externe Prüfer – auf Basis der
**Dokumentvorlagen**. Zwei Typen: **freier Brief** und **Verwendungsnachweis** (z. B. FTZ) mit
per Barcode erfasster, nach Kategorie gruppierter Artikelliste. Ein Dokument ist zunächst ein
Entwurf; beim **Abschließen** wird es schreibgeschützt, und beim Verwendungsnachweis werden alle
erfassten Artikel auf den Zielstandort umgebucht sowie FTZ-Pool-Geräte stillgelegt.

### Logbuch (nur Administratoren)
Audit-Trail je Artikel: Anlage, Bearbeitung, Stilllegung und Standortwechsel (Von → Nach) mit
Angabe, wer die Änderung wann ausgelöst hat – inkl. automatischer Folgen aus Verwendungsnachweis
und Prüfabschluss.

### Export
CSV-Export der Inventarliste, optional gefiltert nach Kategorie, Standort und Status – für Excel und
Weiterverarbeitung.

### Informationen
Sammelbereich mit **Handbuch** (Schritt-für-Schritt-Anleitung), **Changelog** (Änderungshistorie,
gruppiert nach Versionen, neueste zuerst) und **Über FireAsset** (Version, Build-Datum und Link zum
GitHub-Repository).

---

# Teil 2 – Für IT-Administratoren

## Technologie
- **.NET 10**, Blazor Web App (Interactive Server)
- **MS SQL Server** + Entity Framework Core (automatische Migration beim Start)
- **Radzen.Blazor** (UI-Komponenten)
- Schlanke **Cookie-Authentifizierung** mit Passwort-Hashing (`PasswordHasher`)

> Hinweis: Die Anwendung kommuniziert nach dem initialen Seitenaufruf über eine
> SignalR-/WebSocket-Verbindung (Blazor Server). Es gibt keine öffentliche REST-API; einzige
> HTTP-Endpunkte sind die Anmeldung, das Abmelden und der CSV-Export (`/export/inventory.csv`,
> nur für angemeldete Benutzer).

## Installation & Start (Entwicklung)
Voraussetzung: ein erreichbarer **MS SQL Server** und eine **`dbsettings.json`** mit dem
Connection-String (siehe [Konfiguration](#konfiguration)).

```bash
cd src/FireAsset
cp dbsettings.example.json dbsettings.json   # einmalig: Vorlage kopieren und anpassen
dotnet run
```

Beim ersten Start legt die Anwendung die Datenbank **FireAsset** auf dem konfigurierten SQL Server
automatisch an (bzw. migriert eine bereits vorhandene) und erzeugt einen initialen Administrator
(Abschnitt `AdminSeed` in der `appsettings.json`):

- **E-Mail:** `admin@fireasset.local`
- **Passwort:** `ChangeMe!123`  ← **vor produktivem Einsatz ändern!**

## Betrieb & Deployment

### Konfiguration
Der **Datenbank-Connection-String** wird in einer externen Datei **`dbsettings.json`** konfiguriert.
Diese Datei liegt neben der Anwendung, ist bewusst **nicht** im Git (enthält Zugangsdaten) und lässt
sich im Betrieb anpassen. Als Vorlage dient die eingecheckte `dbsettings.example.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FireAsset;User Id=<user>;Password=<pw>;TrustServerCertificate=True"
  }
}
```

- `TrustServerCertificate=True` ist bei einem lokalen SQL Server ohne gültiges Zertifikat nötig.
- Der Login benötigt beim ersten Start das Recht, die Datenbank anzulegen (Serverrolle `dbcreator`
  oder `CREATE DATABASE`). Alternativ die Datenbank vorab anlegen und dem Login dort Rechte geben.

Alle weiteren Einstellungen in `appsettings.json` (bzw. `appsettings.Production.json` oder
Umgebungsvariablen):

| Schlüssel | Zweck |
|---|---|
| `ConnectionStrings:DefaultConnection` (in `dbsettings.json`) | MS-SQL-Server-Connection-String |
| `AdminSeed:Email` / `Password` / `FirstName` / `LastName` | initialer Admin (nur wirksam, solange keine Benutzer existieren) |

Das Admin-Passwort sollte in Produktion **nicht** in `appsettings.json` stehen, sondern über
eine Umgebungsvariable gesetzt werden, z. B. `AdminSeed__Password`.

### Veröffentlichen
```bash
dotnet publish src/FireAsset -c Release -o ./publish
```
Anschließend `./publish/FireAsset` auf dem Zielserver starten (hinter einem Reverse-Proxy
wie IIS/Nginx betreiben). Die `dbsettings.json` muss im Ausgabeordner liegen und den
Connection-String der Zielumgebung enthalten.

> Eine ausführliche Schritt-für-Schritt-Installationsanleitung für Windows 11 (inkl. SQL Server,
> Dienstbetrieb, Firewall, HTTPS, Backup und Updates) findest Du in
> [`docs/INSTALL-Windows11.md`](docs/INSTALL-Windows11.md).

### HTTPS
`UseHttpsRedirection` und HSTS sind aktiv (HSTS nur außerhalb der Entwicklung).
Für den produktiven Einsatz wird HTTPS über ein gültiges Zertifikat (Reverse-Proxy oder
Kestrel) dringend empfohlen.

### Backup
Die Anwendungsdaten liegen vollständig in der SQL-Server-Datenbank **FireAsset**. Das Backup
erfolgt über die Bordmittel des SQL Servers – z. B. eine regelmäßige Datenbanksicherung per
Wartungsplan / SQL Server Agent oder `BACKUP DATABASE`. Es gibt bewusst keine anwendungsinterne
Backup-Funktion (gemäß Spec).

### Sicherheit
- Passwörter werden ausschließlich gehasht gespeichert (Mindestlänge 8 Zeichen).
- Login-Drosselung: nach 5 Fehlversuchen wird das Konto 15 Minuten gesperrt.
- Sitzungen werden bei jeder Anfrage revalidiert: deaktivierte/gelöschte Benutzer verlieren
  ihre Sitzung sofort.
- Der Barcode-Scanner wird wie eine Tastatur verwendet (USB-HID); Eingabe + Enter löst Suche/Umlagerung aus.
- Der SQL-Server-Connection-String liegt in der gitignorierten `dbsettings.json` – Zugangsdaten
  gelangen nicht ins Repository.

### Zielumgebung
- Zentraler Server mit mehreren Clients und gleichzeitigen Nutzern.
- Keine Offline-Funktionalität, keine Benachrichtigungen, keine Mehrsprachigkeit.

## Datenbank-Migrationen
```bash
cd src/FireAsset
dotnet ef migrations add <Name> -o Data/Migrations
```
Migrationen werden beim Anwendungsstart automatisch angewendet (`DbInitializer`).
